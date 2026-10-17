using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Types;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Data.RuntimeData;
using CxVisual.Mechanism.Runtime.Execution;
using CxVisual.TestCenter.Core;
using CxVisual.TestCenter.Discovery;

namespace CxVisual.TestCenter.Cases
{
    public static class TCTRuntimeDataUsabilityCases
    {
        [TCTTest("TCT.MDS.016", "当前执行Scope元数据检索不会窥探其他节点",
            TCTTestAspect.Mechanism, "Data / RuntimeData", ObjectId = "MDSDataContext")]
        public static void VisibleScopeSearch(TCTTestContext t)
        {
            using (var stack = new MDSDataStack())
            {
                stack.FlowHeap.Set("CameraShared", 1);
                using (var frame = stack.OpenFrame("node", stack.RootScope.CreateChild("node"),
                    new Dictionary<string, object>{{"CameraInput", 2}},
                    new Dictionary<string, Type>{{"CameraResult", typeof(int)}}))
                {
                    frame.Data.Set("CameraLocal", 3);
                    frame.Data.Return("CameraResult", 4);
                    var items = frame.Data.Search(new MDSDataQuery { NamePrefix="Camera" });
                    t.AssertEqual(4, items.Count, "输入、本地、待发布输出与Heap统一元数据可见");
                    t.AssertEqual(MDSDataLocation.Input,
                        frame.Data.Inspect("CameraInput").Location, "自动输入描述正确");
                    t.AssertEqual(MDSDataLocation.Local,
                        frame.Data.Inspect("CameraLocal").Location, "局部数据描述正确");
                    t.AssertEqual(MDSDataLocation.PendingOutput,
                        frame.Data.Inspect("CameraResult").Location, "未提交输出可供本节点检查");
                    t.AssertEqual(MDSDataLocation.FlowHeap,
                        frame.Data.Inspect("CameraShared").Location, "共享Heap描述正确");
                    t.AssertEqual(1, frame.Data.Search(new MDSDataQuery {
                        Location=MDSDataLocation.FlowHeap
                    }).Count, "按数据所有权位置筛选");
                    frame.Data.HeapSet("Extra", 77);
                    t.AssertEqual(77, frame.Data.HeapGet<int>("Extra"), "显式Heap一行读写");
                }
                using (var unrelated = stack.OpenFrame("other", stack.RootScope.CreateChild("other"),
                    new Dictionary<string, object>(), new Dictionary<string, Type>()))
                {
                    t.AssertEqual(0, unrelated.Data.Search(new MDSDataQuery {
                        NamePrefix = "CameraInput"
                    }).Count, "不能搜索到别的节点私有Input");
                    t.AssertEqual(0, unrelated.Data.Search(new MDSDataQuery {
                        NamePrefix = "CameraResult"
                    }).Count, "不能绕过端口直接搜索其他节点Output");
                }
            }
        }

        [TCTTest("TCT.MDS.017", "强类型DataKey让普通C#函数自动读取输入并返回输出",
            TCTTestAspect.Mechanism, "Data / RuntimeData", ObjectId = "MDSDataFunctions")]
        public static void TypedAlgorithmBinding(TCTTestContext t)
        {
            var input = new FDTDataKey<int>("Detected");
            var output = new FDTDataKey<int>("Area");
            MRKExecutionPlan plan = new MRKExecutionPlan("typed-data", new[]
            {
                new MRKExecutionStep("sensor", new string[0], (ctx, ct) =>
                {
                    ctx.Data.Return(input, 21);
                    return Task.FromResult((object)true);
                }, dataOutputs: new Dictionary<string, Type> {{input.Name, typeof(int)}}),
                new MRKExecutionStep("measure", new []{"sensor"}, (ctx, ct) =>
                {
                    int result = MDSDataFunctions.Invoke(ctx.Data, input, output, x => x * 2);
                    return Task.FromResult((object)result);
                }, inputs: new[] {
                    new MRKExecutionInput(input.Name, MRKInputSource.StepOutput,
                        "sensor.Detected", typeof(int))
                }, output: new MRKExecutionOutput(typeof(int)),
                   dataOutputs: new Dictionary<string, Type> {{output.Name, typeof(int)}})
            }, new Dictionary<string, string>{{"FinalArea", "measure.Area"}});
            var engine = new MRKExecutionEngine(new FCRWorkerOptions(2, 6));
            try
            {
                MRKFlowResult result = engine.StartFlow(plan).Completion.GetAwaiter().GetResult();
                t.AssertEqual(MRKFlowState.Completed, result.State,
                    "算法函数实际通过现有MRK调度而非测试伪实现");
                t.AssertEqual(42, result.GetCommittedOutput<int>("FinalArea"),
                    "强类型输入→函数→Return→提交→Flow输出整条链路");
            }
            finally { engine.StopAsync().GetAwaiter().GetResult(); }
        }

        [TCTTest("TCT.MDS.018", "异步算法取消不能Return半成品输出",
            TCTTestAspect.Mechanism, "Data / RuntimeData", ObjectId = "MDSDataFunctions")]
        public static void TypedAlgorithmCancellation(TCTTestContext t)
        {
            using (var stack = new MDSDataStack())
            using (var frame = stack.OpenFrame("algorithm", stack.RootScope.CreateChild("algorithm"),
                new Dictionary<string, object>{{"Input", 8}},
                new Dictionary<string, Type>{{"Output", typeof(int)}}))
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel();
                t.AssertThrows<OperationCanceledException>(() =>
                    MDSDataFunctions.InvokeAsync(frame.Data,
                        new FDTDataKey<int>("Input"), new FDTDataKey<int>("Output"),
                        (value, ct) => Task.FromResult(value*2), cancel.Token)
                    .GetAwaiter().GetResult(), "取消的算法不能发布输出");
                t.AssertEqual(0, frame.Data.InspectPendingOutputs().Count,
                    "取消后没有部分输出");
            }
        }
    }
}
