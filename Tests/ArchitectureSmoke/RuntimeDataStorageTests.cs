using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Types;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Data.RuntimeData;
using CxVisual.Mechanism.Runtime.Execution;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class RuntimeDataStorageTests
    {
        private sealed class TestVolume
        {
            internal readonly byte[] Bytes;
            internal TestVolume(byte[] bytes) { Bytes = bytes; }
        }

        internal static void Run()
        {
            SafeCustomType();
            LazyFileAndMetadata();
            FileChangedAndCopyLocation();
            CancelLoadNeverPublishes();
            StrongTypedRuntimeFlow();
            ExternalBindingIsExplicit();
        }

        private static void Check(bool success, string message)
        {
            if (!success) throw new InvalidOperationException("M3/M4验收：" + message);
        }

        private static string NewFile()
        {
            string path = Path.Combine(Path.GetTempPath(), "CxVisual-M3M4-" +
                Guid.NewGuid().ToString("N") + ".raw");
            File.WriteAllBytes(path, new byte[]{1,2,3});
            return path;
        }

        private static void SafeCustomType()
        {
            var registry = new FDTDataAdapterRegistry();
            registry.Register<TestVolume>(v => new TestVolume((byte[])v.Bytes.Clone()));
            using (var heap = new MDSDataHeap(registry))
            {
                var original = new TestVolume(new byte[]{8,9});
                heap.Set("Volume",original);
                original.Bytes[0] = 55;
                Check(heap.Get<TestVolume>("Volume").Bytes[0] == 8,
                    "自定义类型未复制输入");
                heap.CopyTo("Volume", heap, "Derived");
                TestVolume derived = heap.Get<TestVolume>("Derived");
                derived.Bytes[0] = 99;
                Check(heap.Get<TestVolume>("Volume").Bytes[0] == 8,
                    "自定义类型Copy共享可写底层Buffer");
            }
            using (var unregistered = new MDSDataHeap())
            {
                bool rejected = false;
                try { unregistered.Set("Unknown",new TestVolume(new byte[]{1})); }
                catch (NotSupportedException) { rejected = true; }
                Check(rejected, "没有安全复制器的DTO被直接接受");
            }
        }

        private static void LazyFileAndMetadata()
        {
            string path = NewFile();
            try
            {
                using (var stack = new MDSDataStack())
                {
                    var frame = stack.OpenFrame("node",stack.RootScope.CreateChild("node"),
                        new Dictionary<string,object>{{"Input", 12}},
                        new Dictionary<string,Type>());
                    using(frame)
                    {
                        frame.Data.SetFile("CT.Raw",path);
                        Check(frame.Data.Search(new MDSDataQuery {
                            LoadState=MDSDataLoadState.Unloaded,
                            DataType=typeof(byte[])
                        }).Count == 1,"Search暗中加载文件或返回错误数据类型");
                        Check(frame.Data.Inspect("CT.Raw").LoadState == MDSDataLoadState.Unloaded,
                            "Inspect改变文件加载状态");
                        byte[] first = frame.Data.LoadAsync<byte[]>("CT.Raw").GetAwaiter().GetResult();
                        first[0]=66;
                        Check(frame.Data.HeapGet<byte[]>("CT.Raw")[0] == 1,
                            "磁盘缓存被可写外部别名污染");
                        frame.Data.Unload("CT.Raw");
                        Check(frame.Data.Inspect("CT.Raw").LoadState == MDSDataLoadState.Unloaded,
                            "Unload必须只丢缓存");
                    }
                }
                Check(File.Exists(path),"释放Heap误删磁盘原文件");
            }
            finally { File.Delete(path); }
        }

        private static void FileChangedAndCopyLocation()
        {
            string path = NewFile();
            try
            {
                using (var source = new MDSDataHeap())
                using (var dest = new MDSDataHeap())
                {
                    source.SetFile("Original",path);
                    source.CopyTo("Original",dest,"Copy");
                    Check(source.Inspect("Original").LoadState == MDSDataLoadState.Unloaded &&
                          dest.Inspect("Copy").LoadState == MDSDataLoadState.Unloaded,
                        "FileLocator的Copy不得加载大文件");
                    File.WriteAllBytes(path,new byte[]{1,2,3,4});
                    bool rejected=false;
                    try { dest.LoadAsync<byte[]>("Copy").GetAwaiter().GetResult(); }
                    catch(IOException) { rejected=true; }
                    Check(rejected && dest.Inspect("Copy").LoadState == MDSDataLoadState.Failed,
                        "源文件改变时仍读取旧版本");
                }
            }
            finally { File.Delete(path); }
        }

        private static void CancelLoadNeverPublishes()
        {
            string path=NewFile();
            try
            {
                using (var heap=new MDSDataHeap())
                using (var cancel=new CancellationTokenSource())
                {
                    var entered=new TaskCompletionSource<bool>();
                    var exit=new TaskCompletionSource<bool>();
                    heap.SetFile<byte[]>("File",path,new MDSDataFileLoader<byte[]>(
                        async (f,ct) => {
                            entered.TrySetResult(true);
                            await exit.Task.ConfigureAwait(false);
                            return File.ReadAllBytes(f);
                        }));
                    Task<byte[]> reading=heap.LoadAsync<byte[]>("File",cancel.Token);
                    entered.Task.GetAwaiter().GetResult();
                    bool busy=false;
                    try { heap.Unload("File"); } catch(MDSDataException) { busy=true; }
                    cancel.Cancel(); exit.TrySetResult(true);
                    bool cancelled=false;
                    try { reading.GetAwaiter().GetResult(); }
                    catch(OperationCanceledException) { cancelled=true; }
                    Check(busy && cancelled && heap.Inspect("File").LoadState ==
                        MDSDataLoadState.Failed,"取消加载数据发布或占用状态错误");
                }
            }
            finally { File.Delete(path); }
        }

        private static void StrongTypedRuntimeFlow()
        {
            var input=new FDTDataKey<int>("Src");
            var output=new FDTDataKey<int>("Dst");
            var plan=new MRKExecutionPlan("M4-Typed",new[] {
                new MRKExecutionStep("capture",new string[0],(ctx,ct)=>{
                    ctx.Data.Return(input,3);
                    return Task.FromResult((object)true);
                },dataOutputs:new Dictionary<string,Type>{{"Src",typeof(int)}}),
                new MRKExecutionStep("algorithm",new[]{"capture"},(ctx,ct)=>{
                    int result=MDSDataFunctions.Invoke(ctx.Data,input,output,x=>x*7);
                    return Task.FromResult((object)result);
                },inputs:new[]{new MRKExecutionInput("Src",MRKInputSource.StepOutput,
                    "capture.Src",typeof(int))},
                output:new MRKExecutionOutput(typeof(int)),
                dataOutputs:new Dictionary<string,Type>{{"Dst",typeof(int)}})
            },new Dictionary<string,string>{{"Value","algorithm.Dst"}});
            var engine=new MRKExecutionEngine(new FCRWorkerOptions(2,5));
            try
            {
                var result=engine.StartFlow(plan).Completion.GetAwaiter().GetResult();
                Check(result.State==MRKFlowState.Completed &&
                    result.GetCommittedOutput<int>("Value")==21,
                    "纯算法未由既有MRK输入自动绑定并完成原子输出");
            }
            finally { engine.StopAsync().GetAwaiter().GetResult(); }
        }

        private static void ExternalBindingIsExplicit()
        {
            using(var stack=new MDSDataStack())
            using(var frame=stack.OpenFrame("binding",stack.RootScope.CreateChild("binding"),
                new Dictionary<string,object>{{"Input",10}},new Dictionary<string,Type>()))
            {
                frame.Data.Set("Local",8);
                var binding=new MDSDataBinding(MDSBindingSource.CurrentSlot,typeof(int),"Local");
                Check(MDSDataBindingResolver.Resolve<int>(frame.Data,binding)==8,
                    "绑定未读取当前实际Local");
                bool rejected=false;
                try
                {
                    MDSDataBindingResolver.Resolve<int>(frame.Data,
                        new MDSDataBinding(MDSBindingSource.Variable,typeof(int),"PLC.Variable"));
                }
                catch(InvalidOperationException) { rejected=true; }
                Check(rejected,"外部变量绕过现有Owner自动可读");
                Check(MDSDataBindingResolver.CompatibleCandidates(frame.Data,typeof(int)).Count==2,
                    "候选过滤当前可见Scope和端口类型失败");
            }
        }
    }
}
