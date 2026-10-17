using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Types;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Data.RuntimeData;
using CxVisual.Mechanism.Runtime.Execution;
using CxVisual.Professional.Vision;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class RuntimeDataHardeningTests
    {
        private sealed class Sample
        {
            public readonly int Value;
            public Sample(int value) { Value = value; }
        }

        private static void Check(bool value, string label)
        { if (!value) throw new InvalidOperationException("MDS_HARDENING: " + label); }

        internal static void Run()
        {
            HeapQuotaAndAtomicCopy();
            SharedFlowStackAndHeapBudget();
            CrossHeapQuotaAndUnknownDto();
            MetadataLargeScaleSearch();
            FileLoadOverBudgetNoCache();
            LateNodeFileLoadMustNotPublish();
            RealVisionBudgetFailClosed();
            InspectAcrossLargePage();
            SlowAdapterCannotHoldStackLock();
        }

        private static void HeapQuotaAndAtomicCopy()
        {
            var budget = new MDSDataBudget(200);
            using (var heap = new MDSDataHeap(null, budget))
            {
                heap.Set("Original", new byte[100]);
                Check(budget.RetainedBytes == 100, "首次Set未计费");
                bool denied = false;
                try { heap.Set("TooLarge", new byte[150]); }
                catch (MDSDataException ex) { denied = ex.Code == MDSDataErrorCode.QuotaExceeded; }
                Check(denied && !heap.Contains("TooLarge") && budget.RetainedBytes == 100,
                    "超过预算仍能创建条目");
                heap.CopyTo("Original", heap, "Copy");
                Check(budget.RetainedBytes == 200, "Copy没有独立计费");
                denied = false;
                try { heap.CopyTo("Original", heap, "Rejected"); }
                catch (MDSDataException ex) { denied = ex.Code == MDSDataErrorCode.QuotaExceeded; }
                Check(denied && !heap.Contains("Rejected") && heap.Get<byte[]>("Original").Length == 100,
                    "容量拒绝没有保持源目标事务安全");
                heap.Replace("Original", new byte[20]);
                Check(budget.RetainedBytes == 120, "Replace未释放旧数据容量");
                heap.MoveTo("Copy", heap, "Moved");
                Check(budget.RetainedBytes == 120 && !heap.Contains("Copy"),
                    "同一预算Move竟然重复计费");
                heap.Remove("Moved");
                Check(budget.RetainedBytes == 20, "Remove未释放预算");
            }
            Check(budget.RetainedBytes == 0, "Heap关闭后容量泄露");
        }

        private static void SharedFlowStackAndHeapBudget()
        {
            var budget = new MDSDataBudget(100);
            using (var stack = new MDSDataStack(null, null, null, budget))
            {
                stack.FlowHeap.Set("Shared", new byte[55]);
                using (var frame = stack.OpenFrame("producer",
                    stack.RootScope.CreateChild("producer"),
                    new Dictionary<string, object>(),
                    new Dictionary<string, Type>{{"Image", typeof(byte[])}}))
                {
                    frame.Data.Return("Image", new byte[60]);
                    bool denied = false;
                    try { frame.CommitOutputs(); }
                    catch (MDSDataException ex) { denied = ex.Code == MDSDataErrorCode.QuotaExceeded; }
                    byte[] nonexistent;
                    Check(denied && !stack.TryReadOutput("producer","Image",out nonexistent) &&
                        budget.RetainedBytes == 55,
                        "预算不足仍发布半成品或占用未释放");
                }
                using (var frame = stack.OpenFrame("small",
                    stack.RootScope.CreateChild("small"),
                    new Dictionary<string, object>(),
                    new Dictionary<string, Type>{{"Image", typeof(byte[])}}))
                {
                    frame.Data.Return("Image", new byte[40]);
                    frame.CommitOutputs();
                    Check(budget.RetainedBytes == 95, "堆栈预算未共享");
                }
                Check(budget.RetainedBytes == 95,
                    "已提交Output应保活直到Flow结束");
            }
            Check(budget.RetainedBytes == 0, "Flow关闭后预算未完全释放");
        }

        private static void CrossHeapQuotaAndUnknownDto()
        {
            var sourceBudget = new MDSDataBudget(300);
            var targetBudget = new MDSDataBudget(50);
            using (var source = new MDSDataHeap(null, sourceBudget))
            using (var target = new MDSDataHeap(null, targetBudget))
            {
                source.Set("Image", new byte[120]);
                target.Set("Original", new byte[20]);
                bool denied = false;
                try { source.MoveTo("Image", target, "Original", replace: true); }
                catch (MDSDataException ex)
                { denied = ex.Code == MDSDataErrorCode.QuotaExceeded; }
                Check(denied && source.Contains("Image") &&
                    target.Get<byte[]>("Original").Length == 20 &&
                    targetBudget.RetainedBytes == 20 && sourceBudget.RetainedBytes == 120,
                    "跨Heap容量拒绝后源或旧目标被破坏");
            }
            Check(sourceBudget.RetainedBytes == 0 && targetBudget.RetainedBytes == 0,
                "跨Heap各自数据预算在Dispose后泄漏");
            var adapters = new FDTDataAdapterRegistry();
            adapters.Register<Sample>(x => new Sample(x.Value));
            using (var limited = new MDSDataHeap(adapters, new MDSDataBudget(100)))
            {
                bool rejected = false;
                try { limited.Set("UnknownSize", new Sample(8)); }
                catch (MDSDataException ex)
                { rejected = ex.Code == MDSDataErrorCode.QuotaExceeded; }
                Check(rejected && limited.Count == 0,
                    "专业DTO未注册SizeEstimator却绕过有限数据预算");
            }
        }

        private static void MetadataLargeScaleSearch()
        {
            using (var heap = new MDSDataHeap())
            {
                for (int index = 0; index < 4200; index++)
                    heap.Set("Node." + index.ToString("D5"), index);
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var page0 = heap.Query(new MDSDataQuery {
                    NamePrefix="Node.04", Take=100
                });
                var page1 = heap.Query(new MDSDataQuery {
                    NamePrefix="Node.04", Skip=100, Take=100
                });
                var exact = heap.Inspect("Node.04001");
                watch.Stop();
                Check(page0.Count == 100 && page1.Count == 100 &&
                    page0[0].Name == "Node.04000" &&
                    page1[0].Name == "Node.04100" &&
                    exact.Name == "Node.04001", "大量元数据的过滤/稳定分页/精确查找错误");
                Check(watch.Elapsed < TimeSpan.FromSeconds(15),
                    "4200条元数据查询明显退化，耗时：" + watch.Elapsed);
            }
        }

        private static void FileLoadOverBudgetNoCache()
        {
            string path = Path.Combine(Path.GetTempPath(),
                "cxvisual-budget-" + Guid.NewGuid().ToString("N") + ".raw");
            File.WriteAllBytes(path, new byte[120]);
            var budget = new MDSDataBudget(100);
            try
            {
                using (var heap = new MDSDataHeap(null, budget))
                {
                    heap.SetFile("File", path);
                    bool denied = false;
                    try { heap.LoadAsync<byte[]>("File").GetAwaiter().GetResult(); }
                    catch (MDSDataException ex) { denied = ex.Code == MDSDataErrorCode.QuotaExceeded; }
                    Check(denied && budget.RetainedBytes == 0 &&
                        heap.Inspect("File").LoadState == MDSDataLoadState.Failed,
                        "文件Load超过预算仍驻留数据");
                    heap.Unload("File");
                    Check(heap.Inspect("File").LoadState == MDSDataLoadState.Unloaded,
                        "Unload不能回到未加载状态");
                }
                Check(File.Exists(path), "失败的FileLoad误删磁盘原文件");
            }
            finally { File.Delete(path); }
        }

        private static void LateNodeFileLoadMustNotPublish()
        {
            string path = Path.Combine(Path.GetTempPath(),
                "cxvisual-lateio-" + Guid.NewGuid().ToString("N") + ".raw");
            File.WriteAllBytes(path, new byte[]{3, 4, 5});
            try
            {
                var entered = new TaskCompletionSource<bool>();
                var complete = new TaskCompletionSource<bool>();
                using (var stack = new MDSDataStack())
                {
                    var frame = stack.OpenFrame("late",
                        stack.RootScope.CreateChild("late"),
                        new Dictionary<string, object>(), new Dictionary<string, Type>());
                    frame.Data.SetFile<byte[]>("Image", path,
                        new MDSDataFileLoader<byte[]>(async (file, ct) =>
                        {
                            entered.TrySetResult(true);
                            await complete.Task.ConfigureAwait(false);
                            return File.ReadAllBytes(file);
                        }));
                    Task<byte[]> pending = frame.Data.LoadAsync<byte[]>("Image");
                    try
                    {
                        Check(entered.Task.Wait(TimeSpan.FromSeconds(4)),
                            "迟到IO测试未进入Loader");
                        frame.Dispose();
                    }
                    finally { complete.TrySetResult(true); }
                    bool denied = false;
                    try { pending.GetAwaiter().GetResult(); }
                    catch (ObjectDisposedException) { denied = true; }
                    Check(denied &&
                        stack.FlowHeap.Inspect("Image").LoadState == MDSDataLoadState.Failed,
                        "Frame结束后文件异步结果仍在Heap发布");
                }
            }
            finally { File.Delete(path); }
        }

        private static void RealVisionBudgetFailClosed()
        {
            var opts = new CxVisionInspectionOptions {
                PixelDepth = CxVisionPixelDepth.Bits8,
                GrayThreshold = 200, MinDefectArea = 2, MaxDefectArea = 100
            };
            var plan = CxVisionDataFlow.CreateCpuInspectionPlan(opts);
            var inputs = new Dictionary<string,object> {
                {"RawPixels",new FDTReadOnlyBuffer(new byte[]{255,255,0,0,0,0})},
                {"Width",3},{"Height",2}
            };
            var tiny = new MRKExecutionEngine(new FCRWorkerOptions(3,10),
                dataPayloadBudgetBytes: 48);
            try
            {
                MRKFlowResult result = tiny.StartFlow(plan, inputs).Completion.GetAwaiter().GetResult();
                Check(result.State == MRKFlowState.Failed &&
                    result.CommittedOutputs.Count == 0,
                    "真实专业流程超预算后仍导出部分结果");
            }
            finally { tiny.StopAsync().GetAwaiter().GetResult(); }

            var enough = new MRKExecutionEngine(new FCRWorkerOptions(3,10),
                dataPayloadBudgetBytes: 2000);
            try
            {
                MRKFlowResult result = enough.StartFlow(plan, inputs).Completion.GetAwaiter().GetResult();
                Check(result.State == MRKFlowState.Completed &&
                    result.GetCommittedOutput<CxVisionInspectionResult>("Inspection").Regions.Count == 1,
                    "在容量允许时真实专业Flow无法通过");
            }
            finally { enough.StopAsync().GetAwaiter().GetResult(); }
        }

        private static void InspectAcrossLargePage()
        {
            using (var stack = new MDSDataStack())
            {
                Dictionary<string, object> manyInputs = new Dictionary<string,object>();
                for (int i = 0; i < 560; i++) manyInputs.Add("A" + i.ToString("D4"),i);
                stack.FlowHeap.Set("A", 17);
                using (var frame = stack.OpenFrame("node",
                    stack.RootScope.CreateChild("node"),
                    manyInputs, new Dictionary<string, Type>()))
                {
                    Check(frame.Data.Inspect("A").Location == MDSDataLocation.FlowHeap,
                        "前一Location超过500条导致精确Inspect找不到Heap数据");
                    var found = frame.Data.Search(new MDSDataQuery {
                        ExactName = "A", Take = 1
                    });
                    Check(found.Count == 1 && found[0].Location == MDSDataLocation.FlowHeap,
                        "ExactName查询仍被旧分页截断");
                }
            }
        }

        private static void SlowAdapterCannotHoldStackLock()
        {
            var inside = new TaskCompletionSource<bool>();
            var release = new TaskCompletionSource<bool>();
            int blockCopies = 0;
            var adapters = new FDTDataAdapterRegistry();
            adapters.Register<Sample>(value =>
            {
                if (Interlocked.CompareExchange(ref blockCopies,0,0) == 1)
                {
                    inside.TrySetResult(true);
                    release.Task.GetAwaiter().GetResult();
                }
                return new Sample(value.Value);
            }, sizeEstimator: value => 32);
            using (var stack = new MDSDataStack(null, adapters))
            {
                using (var frame = stack.OpenFrame("source",
                    stack.RootScope.CreateChild("source"),
                    new Dictionary<string, object>(),
                    new Dictionary<string, Type> {
                        {"Complex", typeof(Sample)},{"Counter", typeof(int)}
                    }))
                {
                    frame.Data.Return("Complex",new Sample(5));
                    frame.Data.Return("Counter",7);
                    frame.CommitOutputs();
                }
                Interlocked.Exchange(ref blockCopies,1);
                Task blocked = Task.Run(() => {
                    Sample x; stack.TryReadOutput("source","Complex",out x);
                });
                try
                {
                    Check(inside.Task.Wait(TimeSpan.FromSeconds(4)),
                        "未进入专业SlowCopy测试点");
                    Task<int> fast = Task.Run(() => {
                        int number; return stack.TryReadOutput("source","Counter",out number)
                            ? number : -1;
                    });
                    Check(fast.Wait(TimeSpan.FromSeconds(3)) && fast.Result == 7,
                        "一个专业复制器阻塞了无关DataStack输出读取");
                }
                finally { release.TrySetResult(true); }
                blocked.GetAwaiter().GetResult();
            }
        }
    }
}
