using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Types;
using CxVisual.Mechanism.Data.RuntimeData;
using CxVisual.TestCenter.Core;
using CxVisual.TestCenter.Discovery;

namespace CxVisual.TestCenter.Cases
{
    public static class TCTRuntimeDataStorageCases
    {
        private sealed class TCTImageBlock
        {
            internal readonly byte[] Pixels;
            internal TCTImageBlock(byte[] bytes) { Pixels = bytes; }
        }

        private static string TempFile()
        {
            string path = Path.Combine(Path.GetTempPath(), "CxVisual-MDS-" +
                Guid.NewGuid().ToString("N") + ".bin");
            File.WriteAllBytes(path, new byte[] { 10, 20, 30 });
            return path;
        }

        [TCTTest("TCT.MDS.009", "专业DTO使用Flow局部复制适配，防止未知类型浅复制",
            TCTTestAspect.Mechanism, "Data / RuntimeData", ObjectId = "FDTDataAdapterRegistry")]
        public static void CustomTypeCopy(TCTTestContext t)
        {
            var adapters = new FDTDataAdapterRegistry();
            adapters.Register<TCTImageBlock>(x => new TCTImageBlock((byte[])x.Pixels.Clone()));
            adapters.Freeze();
            using (var heap = new MDSDataHeap(adapters))
            {
                var original = new TCTImageBlock(new byte[] { 1, 2 });
                heap.Set("Image", original);
                original.Pixels[0] = 99;
                t.AssertEqual((byte)1, heap.Get<TCTImageBlock>("Image").Pixels[0],
                    "自定义类型Set必须复制");
                heap.CopyTo("Image", heap, "Copy");
                TCTImageBlock derived = heap.Get<TCTImageBlock>("Copy");
                derived.Pixels[1] = 88;
                t.AssertEqual((byte)2, heap.Get<TCTImageBlock>("Image").Pixels[1],
                    "自定义类型Copy必须隔离嵌套数组");
                t.AssertThrows<InvalidOperationException>(() =>
                    adapters.Register<TCTImageBlock>(x => x), "冻结后不能改适配器");
            }
            using (var noAdapters = new MDSDataHeap())
            {
                t.AssertThrows<NotSupportedException>(() =>
                    noAdapters.Set("Unknown", new TCTImageBlock(new byte[] { 1 })),
                    "默认拒绝未知可变DTO");
            }
        }

        [TCTTest("TCT.MDS.010", "未加载磁盘文件可查询但不会因搜索加载",
            TCTTestAspect.Mechanism, "Data / RuntimeData", ObjectId = "MDSDataHeap")]
        public static void LazyFileSearch(TCTTestContext t)
        {
            string path = TempFile();
            try
            {
                using (var heap = new MDSDataHeap())
                {
                    heap.SetFile("CT.Volume", path);
                    MDSDataSnapshot meta = heap.Inspect("CT.Volume");
                    t.AssertEqual(MDSDataLoadState.Unloaded, meta.LoadState,
                        "SetFile只注册定位信息");
                    t.AssertEqual(3L, meta.SizeBytes.Value, "保存源文件长度");
                    t.AssertEqual(MDSDataLoadState.Unloaded,
                        heap.Query(new MDSDataQuery { NamePrefix = "CT." })[0].LoadState,
                        "Search不触发磁盘读取");
                    t.AssertThrows<InvalidOperationException>(() =>
                        heap.Get<byte[]>("CT.Volume"), "Get未加载数据不暗中Load");
                    byte[] result = heap.LoadAsync<byte[]>("CT.Volume").GetAwaiter().GetResult();
                    t.AssertEqual((byte)10, result[0], "按需Load读取真实文件内容");
                    result[0] = 91;
                    t.AssertEqual((byte)10, heap.Get<byte[]>("CT.Volume")[0],
                        "加载后的缓存不会泄漏可写数组别名");
                    heap.Unload("CT.Volume");
                    t.AssertEqual(MDSDataLoadState.Unloaded, heap.Inspect("CT.Volume").LoadState,
                        "Unload仅释放缓存，保留定位信息");
                    heap.Remove("CT.Volume");
                }
                t.AssertTrue(File.Exists(path), "移除Heap条目不能删除用户原磁盘文件", true, true);
            }
            finally { File.Delete(path); }
        }

        [TCTTest("TCT.MDS.011", "源文件被替换时拒绝旧数据版本加载",
            TCTTestAspect.Mechanism, "Data / RuntimeData", ObjectId = "MDSDataHeap")]
        public static void RejectChangedFile(TCTTestContext t)
        {
            string path = TempFile();
            try
            {
                using (var heap = new MDSDataHeap())
                {
                    heap.SetFile("Source", path);
                    File.WriteAllBytes(path, new byte[] { 9, 9, 9, 9 });
                    t.AssertThrows<IOException>(() =>
                        heap.LoadAsync<byte[]>("Source").GetAwaiter().GetResult(),
                        "文件内容更改不得伪装为原文件快照");
                    t.AssertEqual(MDSDataLoadState.Failed, heap.Inspect("Source").LoadState,
                        "失败状态可诊断但不会删除路径");
                }
            }
            finally { File.Delete(path); }
        }

        [TCTTest("TCT.MDS.012", "磁盘Locator跨Heap Copy不复制磁盘文件和缓存",
            TCTTestAspect.Mechanism, "Data / RuntimeData", ObjectId = "MDSDataHeap")]
        public static void CopyFileLocation(TCTTestContext t)
        {
            string path = TempFile();
            try
            {
                using (var heap = new MDSDataHeap())
                using (var other = new MDSDataHeap())
                {
                    heap.SetFile("Original", path);
                    heap.LoadAsync<byte[]>("Original").GetAwaiter().GetResult();
                    heap.CopyTo("Original", other, "LocatorCopy");
                    t.AssertEqual(MDSDataLoadState.Unloaded, other.Inspect("LocatorCopy").LoadState,
                        "文件定位Copy不可悄悄复制已加载缓存或整个文件");
                    t.AssertEqual(path, other.Inspect("LocatorCopy").Source,
                        "定位记录指向同一源文件");
                    heap.MoveTo("Original", other, "LocatorMoved");
                    t.AssertTrue(!heap.Contains("Original"), "文件定位Move要撤销源条目", false, false);
                    t.AssertEqual(MDSDataLoadState.Loaded, other.Inspect("LocatorMoved").LoadState,
                        "文件定位Move可整体接管当前缓存");
                }
                t.AssertTrue(File.Exists(path), "Copy/Move不得影响外部原文件", true, true);
            }
            finally { File.Delete(path); }
        }

        [TCTTest("TCT.MDS.013", "元数据搜索类型位置状态筛选和稳定分页",
            TCTTestAspect.Mechanism, "Data / RuntimeData", ObjectId = "MDSDataQuery")]
        public static void MetadataPaging(TCTTestContext t)
        {
            string path = TempFile();
            try
            {
                using (var heap = new MDSDataHeap())
                {
                    heap.Set("Cam.B", 2);
                    heap.Set("Cam.A", 1);
                    heap.SetFile("Cam.File", path);
                    heap.Set("Other", 20);
                    var page0 = heap.Query(new MDSDataQuery { NamePrefix = "Cam.", Take = 2 });
                    var page1 = heap.Query(new MDSDataQuery { NamePrefix = "Cam.", Skip = 2, Take = 2 });
                    t.AssertEqual("Cam.A", page0[0].Name, "稳定字典序");
                    t.AssertEqual("Cam.B", page0[1].Name, "第一页按字典序");
                    t.AssertEqual("Cam.File", page1[0].Name, "第二页正确翻页");
                    t.AssertEqual(1, heap.Query(new MDSDataQuery {
                        LoadState = MDSDataLoadState.Unloaded, DataType = typeof(byte[])
                    }).Count, "按加载状态及CLR类型筛选");
                    t.AssertThrows<ArgumentOutOfRangeException>(() =>
                        new MDSDataQuery { Take = 100000 }, "必须限制查询单页上限");
                }
            }
            finally { File.Delete(path); }
        }

        [TCTTest("TCT.MDS.014", "加载进行中不允许Unload/Remove，取消后可以重新加载",
            TCTTestAspect.Mechanism, "Data / RuntimeData", ObjectId = "MDSDataHeap")]
        public static void CancelFileLoad(TCTTestContext t)
        {
            string path = TempFile();
            try
            {
                var entered = new TaskCompletionSource<bool>();
                var release = new TaskCompletionSource<bool>();
                using (var heap = new MDSDataHeap())
                using (var cancel = new CancellationTokenSource())
                {
                    heap.SetFile<byte[]>("Async", path, new MDSDataFileLoader<byte[]>(
                        async (file, ct) =>
                        {
                            entered.TrySetResult(true);
                            await release.Task.ConfigureAwait(false);
                            return File.ReadAllBytes(file);
                        }));
                    Task<byte[]> task = heap.LoadAsync<byte[]>("Async", cancel.Token);
                    entered.Task.GetAwaiter().GetResult();
                    t.AssertThrows<MDSDataException>(() => heap.Unload("Async"),
                        "加载时不能卸载");
                    t.AssertThrows<MDSDataException>(() => heap.Remove("Async"),
                        "加载时不能删除条目");
                    cancel.Cancel();
                    release.TrySetResult(true);
                    t.AssertThrows<OperationCanceledException>(() => task.GetAwaiter().GetResult(),
                        "已取消的结果不允许发布");
                    t.AssertEqual(MDSDataLoadState.Failed, heap.Inspect("Async").LoadState,
                        "取消后应保留可重试的失败状态");
                    t.AssertEqual((byte)10, heap.LoadAsync<byte[]>("Async").GetAwaiter().GetResult()[0],
                        "取消后成功重试加载");
                }
            }
            finally { File.Delete(path); }
        }

        [TCTTest("TCT.MDS.015", "默认Loader限制整卷大文件意外加载",
            TCTTestAspect.Mechanism, "Data / RuntimeData", ObjectId = "MDSLimitedBinaryFileLoader")]
        public static void LargeFileRejected(TCTTestContext t)
        {
            string path = Path.Combine(Path.GetTempPath(), "CxVisual-MDS-large-" +
                Guid.NewGuid().ToString("N") + ".bin");
            try
            {
                using (FileStream stream = File.Create(path))
                    stream.SetLength(65L * 1024 * 1024);
                using (var heap = new MDSDataHeap())
                {
                    heap.SetFile("CT.Projection", path);
                    t.AssertEqual(MDSDataLoadState.Unloaded, heap.Inspect("CT.Projection").LoadState,
                        "大文件只登记路径，不占大块内存");
                    t.AssertThrows<InvalidOperationException>(() =>
                        heap.LoadAsync<byte[]>("CT.Projection").GetAwaiter().GetResult(),
                        "默认大文件不能意外整卷读入内存");
                }
            }
            finally { File.Delete(path); }
        }
    }
}
