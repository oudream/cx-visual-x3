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
        [TCTTest("TCT.MDS.030", "生命周期五维独立入口与组合缓存回收",
            TCTTestAspect.Mechanism, "Data / RuntimeData", ObjectId = "MDSDataLifecyclePolicy")]
        public static void LifecycleFiveDimensions(TCTTestContext t)
        {
            using (var heap = new MDSDataHeap())
            {
                heap.Set("Image", new byte[] { 1, 2, 3 });
                var policy = MDSDataLifecyclePolicy.Default
                    .WithLifetime(MDSDataLifetimePolicy.UntilLastConsumer())
                    .WithRetention(MDSDataRetentionPolicy.CacheFor(TimeSpan.FromMinutes(1)))
                    .WithTransfer(MDSDataTransferPolicy.CopyOnly)
                    .WithReclamation(MDSDataReclamationPolicy.OnExpiry);
                heap.Lifecycle.Configure("Image", policy);
                heap.Lifecycle.ConfigureOwnership("Image",
                    MDSDataOwnershipPolicy.Default.WithMove(false));
                using (var reader = heap.Lifecycle.AcquireUse("Image"))
                {
                    t.AssertEqual(1, heap.Lifecycle.Inspect("Image").ActiveConsumers,
                        "独立策略配置可通过统一入口诊断");
                    t.AssertEqual(0, heap.Lifecycle.Sweep(DateTime.UtcNow.AddMinutes(2)),
                        "活跃消费者不因TTL模拟到期而失去数据");
                    t.AssertEqual(3, heap.Get<byte[]>("Image").Length, "数据仍保持有效");
                }
                t.AssertEqual(0, heap.Lifecycle.Sweep(DateTime.UtcNow.AddMinutes(2)),
                    "ConsumerBound仍开放新消费者时即使当前无人使用也不能回收");
                heap.Lifecycle.CloseConsumerAdmission("Image");
                t.AssertEqual(1, heap.Lifecycle.Sweep(DateTime.UtcNow.AddMinutes(2)),
                    "关闭新消费者入口后到期可安全释放");
                t.AssertEqual(false, heap.Contains("Image"), "回收必须实际影响原Heap");
            }
        }

        [TCTTest("TCT.MDS.031", "生命周期策略冲突与活跃使用拒绝",
            TCTTestAspect.Mechanism, "Data / RuntimeData", ObjectId = "MDSDataLifecycleAccess")]
        public static void LifecycleFailureRoutes(TCTTestContext t)
        {
            using (var heap = new MDSDataHeap())
            using (var other = new MDSDataHeap())
            {
                heap.Set("Result", 8);
                heap.Lifecycle.ConfigureTransfer("Result", MDSDataTransferPolicy.CopyOnly);
                t.AssertThrows<InvalidOperationException>(() =>
                    heap.MoveTo("Result", other, "Moved"), "仅允许复制的策略必须拒绝Move");
                using (var active = heap.Lifecycle.AcquireUse("Result"))
                {
                    t.AssertThrows<MDSDataException>(() => heap.Remove("Result"),
                        "被借用的数据必须阻止外部移除");
                    t.AssertEqual(false, heap.Lifecycle.ReleaseEligible("Result"),
                        "自动与手动回收都遵守活跃保护");
                }
                t.AssertThrows<ArgumentException>(() =>
                    MDSDataLifecyclePolicy.Default.WithReclamation(
                        MDSDataReclamationPolicy.AfterLastConsumer),
                    "未启用ConsumerBound时拒绝消费者自动回收的矛盾组合");
                t.AssertEqual(true, heap.Lifecycle.ReleaseEligible("Result"),
                    "解除活跃引用后可安全释放");
            }
        }

        [TCTTest("TCT.MDS.032", "代码编排MRK静态DAG消费后安全回收与Flow导出",
            TCTTestAspect.Mechanism, "Data / RuntimeData", ObjectId = "MRKCommittedOutputReclaimer")]
        public static void RuntimeDataStaticConsumerReclamation(TCTTestContext t)
        {
            var blueprint = new MRKCodeRuntimeBuilder()
                .DataCapability("Image", (ctx, ct) =>
                {
                    ctx.Data.Return("Image", new byte[] { 5, 6, 7 });
                    return Task.FromResult((object)true);
                }, new Dictionary<string, Type> { { "Image", typeof(byte[]) } })
                .DataCapability("Measure", (ctx, ct) =>
                {
                    ctx.Data.Return("Size", ctx.Data.Get<byte[]>("Input").Length);
                    return Task.FromResult((object)true);
                }, new Dictionary<string, Type> { { "Size", typeof(int) } },
                    new[] { new MRKCapabilityInput("Input", typeof(byte[])) })
                .Flow("real", flow => flow.ReclaimConsumedData()
                    .Step("acquire", "Image")
                    .StepData("measure", "Measure", new[] {
                        new MRKExecutionInput("Input", MRKInputSource.StepOutput,
                            "acquire.Image", typeof(byte[])) }, "acquire")
                    .Export("Final", "measure.Size"))
                .Freeze();
            MRKFlowCompileDefinition source;
            t.AssertEqual(true,
                blueprint.TryGet("real", out source) &&
                    source.ReclaimCommittedWhenConsumersSettle,
                "代码编排便捷入口须沿正式编译定义保留策略");
            MRKDevelopmentRuntime runtime = blueprint.BuildForDevelopment(new FCRWorkerOptions(2, 8));
            try
            {
                MRKFlowResult result = runtime.RunAsync("real", new Dictionary<string, object>())
                    .GetAwaiter().GetResult();
                t.AssertEqual(MRKFlowState.Completed, result.State,
                    "实际MRK节点执行与MDS数据回收不得影响最终Flow终态");
                t.AssertEqual(3, result.GetCommittedOutput<int>("Final"),
                    "最后消费者结算不应使正式Flow导出失效");
            }
            finally { runtime.StopAsync().GetAwaiter().GetResult(); }
        }

        [TCTTest("TCT.MDS.033", "Loop逐轮临时释放同时保留Join需要的历史",
            TCTTestAspect.Mechanism, "Data / RuntimeData", ObjectId = "MRKLoopDataRetention")]
        public static void LoopRetentionAndJoin(TCTTestContext t)
        {
            var body = new MRKExecutionPlan("body", new[] {
                new MRKExecutionStep("image", new string[0], (ctx, ct) =>
                {
                    ctx.Data.Return("Image", new byte[64]);
                    return Task.FromResult((object)true);
                }, dataOutputs: new Dictionary<string, Type> { { "Image", typeof(byte[]) } }),
                new MRKExecutionStep("measure", new[] { "image" }, (ctx, ct) =>
                {
                    ctx.Data.Return("Value", ctx.Data.Get<byte[]>("Source").Length +
                        ctx.IterationIndex);
                    return Task.FromResult((object)true);
                }, inputs: new[] { new MRKExecutionInput("Source",
                    MRKInputSource.StepOutput, "image.Image", typeof(byte[])) },
                dataOutputs: new Dictionary<string, Type> { { "Value", typeof(int) } })
            });
            var region = new MRKLoopRegion(body, 4, (ctx, i) => i < 4,
                dataRetention: MRKLoopDataRetention.KeepForJoin("measure"));
            var plan = new MRKExecutionPlan("loop-with-history", new[] {
                new MRKExecutionStep("loop", new string[0],
                    (ctx, ct) => Task.FromResult((object)true), loopRegion: region),
                new MRKExecutionStep("join", new[] { "loop" }, (ctx, ct) =>
                {
                    var rows = ctx.CollectLoopOutputs<int>("loop", "measure", "Value");
                    ctx.Data.Return("Total", rows.Count == 4 ? 1 : 0);
                    return Task.FromResult((object)true);
                }, dataOutputs: new Dictionary<string, Type> { { "Total", typeof(int) } })
            }, new Dictionary<string, string> { { "Result", "join.Total" } });
            var engine = new MRKExecutionEngine(new FCRWorkerOptions(3, 16),
                dataPayloadBudgetBytes: 200);
            try
            {
                MRKFlowResult result = engine.StartFlow(plan).Completion.GetAwaiter().GetResult();
                t.AssertEqual(MRKFlowState.Completed, result.State,
                    "每轮64B图像应在消费结束后释放，32B历史结果可供Join读取");
                t.AssertEqual(1, result.GetCommittedOutput<int>("Result"),
                    "保留的真实Invocation历史能在Join阶段正确聚合");
            }
            finally { engine.StopAsync(true).GetAwaiter().GetResult(); }
        }

        private sealed class TCTExistingOwner : IMDSExternalBindingResolver
        {
            public object Resolve(MDSBindingSource source, string name, Type target)
            {
                if (source == MDSBindingSource.Variable && name == "PLC.Temperature"
                    && target == typeof(int)) return 88;
                throw new InvalidOperationException("外部解析器未授权该数据");
            }
        }

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
                    t.AssertEqual(stack.FlowHeap.RunId,
                        frame.Data.Inspect("CameraLocal").RunId, "局部数据Run归属正确");
                    t.AssertEqual(1, frame.Data.Search(new MDSDataQuery {
                        RunId = stack.FlowHeap.RunId, ScopeId = frame.Data.Inspect("CameraLocal").ScopeId,
                        NamePrefix = "CameraLocal"
                    }).Count, "按Run与Scope过滤可见数据");
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

        [TCTTest("TCT.MDS.021", "参数来源选择只复用已有数据和外部Owner解析器",
            TCTTestAspect.Mechanism, "Data / RuntimeData", ObjectId = "MDSDataBindingResolver")]
        public static void AllBindingCategories(TCTTestContext t)
        {
            using (var stack = new MDSDataStack())
            using (var frame = stack.OpenFrame("bind", stack.RootScope.CreateChild("bind"),
                new Dictionary<string, object>{{"NodeAlias", 41}},
                new Dictionary<string, Type>()))
            {
                frame.Data.Set("LocalValue", 7);
                stack.FlowHeap.Set("SharedValue", 5);
                t.AssertEqual(2, MDSDataBindingResolver.Resolve<int>(frame.Data,
                    new MDSDataBinding(MDSBindingSource.Constant, typeof(int), constant: 2)),
                    "常量绑定不用运行时搜索");
                t.AssertEqual(41, MDSDataBindingResolver.Resolve<int>(frame.Data,
                    new MDSDataBinding(MDSBindingSource.NodeOutput, typeof(int), "NodeAlias")),
                    "节点Output必须以既有MRK映射的Input别名注入");
                t.AssertEqual(7, MDSDataBindingResolver.Resolve<int>(frame.Data,
                    new MDSDataBinding(MDSBindingSource.CurrentSlot, typeof(int), "LocalValue")),
                    "当前Local绑定");
                t.AssertEqual(5, MDSDataBindingResolver.Resolve<int>(frame.Data,
                    new MDSDataBinding(MDSBindingSource.FlowHeap, typeof(int), "SharedValue")),
                    "FlowHeap显式绑定");
                t.AssertThrows<InvalidOperationException>(() =>
                    MDSDataBindingResolver.Resolve<int>(frame.Data,
                        new MDSDataBinding(MDSBindingSource.Variable, typeof(int),
                            "PLC.Temperature")),
                    "外部PLC变量没有Owner解析器时不能暗自读取");
                t.AssertEqual(88, MDSDataBindingResolver.Resolve<int>(frame.Data,
                    new MDSDataBinding(MDSBindingSource.Variable, typeof(int),
                        "PLC.Temperature"), new TCTExistingOwner()),
                    "PLC变量只由已授权Owner解析器提供");
                t.AssertThrows<InvalidCastException>(() =>
                    MDSDataBindingResolver.Resolve<string>(frame.Data,
                        new MDSDataBinding(MDSBindingSource.CurrentSlot, typeof(int),
                            "LocalValue")), "不兼容类型提前拒绝");
                var candidates = MDSDataBindingResolver.CompatibleCandidates(frame.Data,
                    typeof(int));
                t.AssertEqual(3, candidates.Count,
                    "参数选择只给出当前可见且类型匹配的三个int来源");
            }
        }
    }
}
