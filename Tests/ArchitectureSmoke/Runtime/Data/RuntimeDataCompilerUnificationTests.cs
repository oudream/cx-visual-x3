using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Runtime.Execution;

namespace CxVisual.Tests.ArchitectureSmoke
{
    /// <summary>W1/W2：代码优先与工程编排使用同一具名Data端口契约/同一Compiler。</summary>
    internal static class RuntimeDataCompilerUnificationTests
    {
        internal static void Run()
        {
            ThreeLevelNamedDataContract();
            MissingAndWrongSubflowInputsFailBeforeRuntime();
            MissingDeclaredCapabilityInputRejected();
            StandaloneCompilerNamedExports();
            OrdinaryCapabilityAndExistingStepResultsRemainUsable();
            DynamicLoopCommittedDataFrames();
            R07_CompletedLoopOutputsAreFullyScoped();
            R07_NestedLoopOutputsPreserveInvocationPath();
            R07_ParallelForeachRealOverlapAndOutputIdentity();
            R07_ForeachBudgetMustRejectBeforeAnyBusinessWork();
            R07_ParallelFailureWaitsForUncooperativeTail();
            ConcurrentInvocationFrameIsolation();
            RepeatedInvocationFrameIsolation();
            DeveloperBuilderCanEnableStaticOutputReclamation();
            R08_DynamicLoopReclaimsOnlyUnretainedHistory();
            R08_ParallelForeachReclaimsWithoutBreakingJoin();
            R08_InvalidHistoryPolicyRejected();
        }

        private static void DeveloperBuilderCanEnableStaticOutputReclamation()
        {
            MRKCodeRuntimeBlueprint blueprint = new MRKCodeRuntimeBuilder()
                .DataCapability("Emit", (ctx, ct) =>
                {
                    ctx.Data.Return("Image", new byte[] { 1, 2, 3 });
                    return Task.FromResult((object)true);
                }, new Dictionary<string, Type> { { "Image", typeof(byte[]) } })
                .DataCapability("Count", (ctx, ct) =>
                {
                    ctx.Data.Return("Size", ctx.Data.Get<byte[]>("Input").Length);
                    return Task.FromResult((object)true);
                }, new Dictionary<string, Type> { { "Size", typeof(int) } },
                    new[] { new MRKCapabilityInput("Input", typeof(byte[])) })
                .Flow("simple", f => f
                    .ReclaimConsumedData()
                    .Step("producer", "Emit")
                    .StepData("count", "Count", new[] {
                        new MRKExecutionInput("Input", MRKInputSource.StepOutput,
                            "producer.Image", typeof(byte[]))
                    }, "producer")
                    .Export("Final", "count.Size"))
                .Freeze();
            MRKFlowCompileDefinition definition;
            Check(blueprint.TryGet("simple", out definition) &&
                definition.ReclaimCommittedWhenConsumersSettle,
                "代码编排一行启用回收策略必须进入正式只读编译定义");
            MRKCompileResult compiled = new MRKExecutionPlanCompiler().Compile(
                "external-compile", new[] { new MRKStepCompileRequest("producer", "Emit") },
                blueprint, reclaimCommittedWhenConsumersSettle: true);
            Check(compiled.Succeeded &&
                compiled.Plan.ReclaimCommittedWhenConsumersSettle,
                "外部工程编排与代码便捷入口共用同一MRK计划的静态回收合同");
            MRKDevelopmentRuntime runtime = blueprint.BuildForDevelopment(new FCRWorkerOptions(2, 8));
            try
            {
                MRKFlowResult result = runtime.RunAsync("simple",
                    new Dictionary<string, object>()).GetAwaiter().GetResult();
                Check(result.State == MRKFlowState.Completed &&
                    result.GetCommittedOutput<int>("Final") == 3,
                    "代码优先入口必须真实经过MRK编译/数据端口/结果导出");
            }
            finally { runtime.StopAsync().GetAwaiter().GetResult(); }
        }

        private static MRKExecutionPlan BuildR08Plan(bool parallel, MRKLoopDataRetention retention)
        {
            var body = new MRKExecutionPlan("r08.body", new[] {
                new MRKExecutionStep("image", new string[0], (ctx, ct) =>
                {
                    ctx.Data.Return("Image", new byte[64]);
                    return Task.FromResult((object)true);
                }, dataOutputs: new Dictionary<string, Type> { { "Image", typeof(byte[]) } }),
                new MRKExecutionStep("measure", new[] { "image" }, (ctx, ct) =>
                {
                    byte[] value = ctx.Data.Get<byte[]>("Input");
                    ctx.Data.Return("Count", value.Length + ctx.IterationIndex);
                    return Task.FromResult((object)true);
                }, inputs: new[] { new MRKExecutionInput("Input", MRKInputSource.StepOutput,
                    "image.Image", typeof(byte[])) },
                    dataOutputs: Output("Count"))
            });
            var loop = new MRKLoopRegion(body, 4,
                parallel ? null : (Func<MRKExecutionContext, int, bool>)((ctx, i) => i < 4),
                collectionInput: parallel ? "Items" : null,
                maxParallelIterations: parallel ? 2 : 1, dataRetention: retention);
            return new MRKExecutionPlan("r08.main", new[] {
                new MRKExecutionStep("loop", new string[0],
                    (ctx, ct) => Task.FromResult((object)true), loopRegion: loop),
                new MRKExecutionStep("join", new[] { "loop" }, (ctx, ct) =>
                {
                    IReadOnlyList<MRKLoopOutput<int>> rows =
                        ctx.CollectLoopOutputs<int>("loop", "measure", "Count");
                    Check(rows.Count == 4 && rows.Select(x => x.FrameKey).Distinct().Count() == 4,
                        "动态Join未能看到全部真实Invocation");
                    if (retention != null && !retention.Retains("image"))
                    {
                        bool refused = false;
                        try { ctx.CollectLoopOutputs<byte[]>("loop", "image", "Image"); }
                        catch (InvalidOperationException error)
                        { refused = error.Message.Contains("history_not_retained"); }
                        Check(refused, "Join查询已声明放弃的历史应有明确诊断，不能返回旧缓冲");
                    }
                    ctx.Data.Return("Total", rows.Sum(x => x.Value));
                    return Task.FromResult((object)true);
                }, dataOutputs: Output("Total"))
            }, new Dictionary<string, string> { { "Total", "join.Total" } });
        }

        private static void R08_DynamicLoopReclaimsOnlyUnretainedHistory()
        {
            RunR08Bounded(false, 200);
        }

        private static void R08_ParallelForeachReclaimsWithoutBreakingJoin()
        {
            RunR08Bounded(true, 300);
        }

        private static void RunR08Bounded(bool parallel, long budget)
        {
            var inputs = new Dictionary<string, object>();
            if (parallel) inputs["Items"] = new List<int> { 1, 2, 3, 4 };
            var engine = new MRKExecutionEngine(new FCRWorkerOptions(4, 32),
                dataPayloadBudgetBytes: budget);
            try
            {
                MRKFlowResult bounded = engine.StartFlow(
                    BuildR08Plan(parallel, MRKLoopDataRetention.KeepForJoin("measure")),
                    inputs).Completion.GetAwaiter().GetResult();
                Check(bounded.State == MRKFlowState.Completed &&
                    bounded.GetCommittedOutput<int>("Total") == 262,
                    "按Iteration安全释放临时图像后Join仍能读取全部保留结果：" +
                    (bounded.Error == null ? "" : bounded.Error.ToString()));
                MRKFlowResult conservative = engine.StartFlow(
                    BuildR08Plan(parallel, null), inputs).Completion.GetAwaiter().GetResult();
                Check(conservative.State == MRKFlowState.Failed &&
                    conservative.CommittedOutputs.Count == 0,
                    "默认KeepAll不得悄悄更改，容量不足时必须明确失败");
            }
            finally { engine.StopAsync(true).GetAwaiter().GetResult(); }
        }

        private static void R08_InvalidHistoryPolicyRejected()
        {
            var body = new MRKExecutionPlan("body", new[] {
                new MRKExecutionStep("value", new string[0], (ctx, ct) =>
                    Task.FromResult((object)true))
            });
            bool unknown = false;
            try { new MRKLoopRegion(body, 2,
                dataRetention: MRKLoopDataRetention.KeepForJoin("absent")); }
            catch (ArgumentException) { unknown = true; }
            Check(unknown, "循环保留历史的未知节点必须编译前拒绝");
            var nestedBody = new MRKExecutionPlan("outer", new[] {
                new MRKExecutionStep("inner", new string[0], (ctx, ct) =>
                    Task.FromResult((object)true), loopRegion: new MRKLoopRegion(body, 2))
            });
            bool nested = false;
            try { new MRKLoopRegion(nestedBody, 2,
                dataRetention: MRKLoopDataRetention.ReleaseAfterIteration); }
            catch (ArgumentException) { nested = true; }
            Check(nested, "未证明的嵌套动态循环不能自动回收");
        }

        private static void Check(bool ok, string name)
        {
            if (!ok) throw new InvalidOperationException("W1统一数据端口：" + name);
        }

        private static IDictionary<string, Type> Output(string name)
        {
            return new Dictionary<string, Type>(StringComparer.Ordinal)
            { { name, typeof(int) } };
        }

        private static MRKCodeRuntimeBuilder Configure()
        {
            return new MRKCodeRuntimeBuilder()
                .DataCapability("Double", (ctx, ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    ctx.Data.Return("Doubled", ctx.Data.Get<int>("N") * 2);
                    return Task.FromResult((object)true);
                }, Output("Doubled"),
                    new[]{new MRKCapabilityInput("N",typeof(int))})
                .DataCapability("Plus", (ctx, ct) =>
                {
                    ctx.Data.Return("Raised", ctx.Data.Get<int>("Value") + 1);
                    return Task.FromResult((object)true);
                }, Output("Raised"),
                    new[]{new MRKCapabilityInput("Value",typeof(int))})
                .DataCapability("Final", (ctx, ct) =>
                {
                    ctx.Data.Return("Answer", ctx.Data.Get<int>("FromMid") * 3);
                    return Task.FromResult((object)true);
                }, Output("Answer"),
                    new[]{new MRKCapabilityInput("FromMid",typeof(int))})
                .Flow("Child", f => f
                    .StepData("scale", "Double", new [] {
                        new MRKExecutionInput("N", MRKInputSource.FlowInput,
                            "inputNumber", typeof(int))
                    })
                    .Export("LeafValue", "scale.Doubled"))
                .Flow("Mid", f => f
                    .Subflow("leaf", "Child", new [] {
                        new MRKExecutionInput("inputNumber", MRKInputSource.FlowInput,
                            "middleSeed", typeof(int))
                    })
                    .StepData("plus", "Plus", new [] {
                        new MRKExecutionInput("Value", MRKInputSource.StepOutput,
                            "leaf.LeafValue", typeof(int))
                    }, "leaf")
                    .Export("MidResult", "plus.Raised"))
                .Flow("Top", f => f
                    .ReclaimConsumedData()
                    .Subflow("mid", "Mid", new [] {
                        new MRKExecutionInput("middleSeed", MRKInputSource.FlowInput,
                            "seed", typeof(int))
                    })
                    .StepData("check", "Final", new [] {
                        new MRKExecutionInput("FromMid", MRKInputSource.StepOutput,
                            "mid.MidResult", typeof(int))
                    }, "mid")
                    .Export("FinalResult", "check.Answer"));
        }

        private static void ThreeLevelNamedDataContract()
        {
            var blueprint = Configure().Freeze();
            MRKCompileResult compiled = new MRKExecutionOrchestrationCompiler()
                .Compile("Top", blueprint, blueprint);
            Check(compiled.Succeeded && compiled.Plan.ReclaimCommittedWhenConsumersSettle,
                "三层真实端口编译或消费回收策略未固定：" +
                string.Join("; ",compiled.Diagnostics.Select(x=>x.Code+":"+x.Message)));
            MRKExecutionStep root = compiled.Plan.GetStep("check");
            Check(root.Inputs.Count == 1 &&
                  root.Inputs[0].SourceName == "mid.MidResult" &&
                  compiled.Plan.DataExportTypes["FinalResult"] == typeof(int),
                "代码入口与编排编译器丢失具名DataOutputs");

            // 先编译所有流程，再由同一MRK引擎实际跑；不会自建第二个Runtime。
            MRKDevelopmentRuntime runtime = blueprint.BuildForDevelopment(
                new FCRWorkerOptions(4, 20));
            try
            {
                var result = runtime.RunAsync("Top",
                    new Dictionary<string, object> {{"seed", 4}})
                    .GetAwaiter().GetResult();
                Check(result.State == MRKFlowState.Completed,
                    "三层数据流执行失败：" + (result.Error == null ? "" : result.Error.ToString()));
                Check(result.GetCommittedOutput<int>("FinalResult") == 27,
                    "子流程调用真实Input→子Flow导出→主Flow输出错误");
                Check(!result.CommittedOutputs.ContainsKey("LeafValue") &&
                    !result.CommittedOutputs.ContainsKey("MidResult"),
                    "子流程私有输出未经Top的DataExports错误地泄漏");
                var repeat = runtime.RunAsync("Top",
                    new Dictionary<string, object> {{"seed", 10}})
                    .GetAwaiter().GetResult();
                Check(repeat.State == MRKFlowState.Completed &&
                      repeat.GetCommittedOutput<int>("FinalResult") == 63,
                    "同一Blueprint不同Run数据未隔离");
            }
            finally { runtime.StopAsync().GetAwaiter().GetResult(); }
        }

        private static void MissingAndWrongSubflowInputsFailBeforeRuntime()
        {
            MRKCodeRuntimeBuilder missing = new MRKCodeRuntimeBuilder()
                .DataCapability("Read", (ctx,ct) => {
                    ctx.Data.Return("Out",ctx.Data.Get<int>("Value"));
                    return Task.FromResult((object)true);
                }, Output("Out"))
                .Flow("Child", f=>f
                    .StepData("read","Read",new [] {
                        new MRKExecutionInput("Value",MRKInputSource.FlowInput,
                            "Required",typeof(int))
                    }).Export("Public","read.Out"))
                .Flow("Top", f=>f.Subflow("call","Child"));
            bool refused = false;
            try { missing.Freeze(); }
            catch(MRKCodeRuntimeBuildException ex)
            { refused = ex.Diagnostics.Any(x=>x.Code=="INVALID_ORCHESTRATION"); }
            Check(refused, "未声明调用口仍隐式获得根Flow同名输入");

            MRKCodeRuntimeBuilder wrong = new MRKCodeRuntimeBuilder()
                .DataCapability("Read", (ctx,ct) => {
                    ctx.Data.Return("Out",ctx.Data.Get<int>("Value"));
                    return Task.FromResult((object)true);
                }, Output("Out"))
                .Flow("Child", f=>f.StepData("read","Read",new [] {
                    new MRKExecutionInput("Value",MRKInputSource.FlowInput,
                        "Required",typeof(int))
                }).Export("Public","read.Out"))
                .Flow("Top", f=>f.Subflow("call","Child",new [] {
                    new MRKExecutionInput("Required",MRKInputSource.FlowInput,
                        "wrongType",typeof(string))
                }));
            refused = false;
            try { wrong.Freeze(); }
            catch(MRKCodeRuntimeBuildException ex)
            { refused = ex.Diagnostics.Any(x=>x.Code=="INVALID_ORCHESTRATION"); }
            Check(refused, "调用口类型不兼容仍然编译通过");
        }

        private static void MissingDeclaredCapabilityInputRejected()
        {
            var unresolved = new MRKCodeRuntimeBuilder()
                .DataCapability("NeedsImage", (c,t) => {
                    c.Data.Return("Done", c.Data.Get<int>("Image") * 2);
                    return Task.FromResult((object)true);
                }, Output("Done"), new[]{new MRKCapabilityInput("Image",typeof(int))})
                .Flow("Test", f => f.Step("missingInput", "NeedsImage"));
            bool rejected = false;
            try { unresolved.Freeze(); }
            catch (MRKCodeRuntimeBuildException error)
            {
                rejected = error.Diagnostics.Any(d=>d.Message.Contains("必需输入未绑定"));
            }
            Check(rejected, "能力已声明必要输入，但没有数据连线时仍编译成功");

            var wrongType = new MRKCodeRuntimeBuilder()
                .DataCapability("NeedsImage", (c,t) => {
                    c.Data.Return("Done", 1);
                    return Task.FromResult((object)true);
                }, Output("Done"), new[]{new MRKCapabilityInput("Image",typeof(int))})
                .Flow("Test", f=>f.StepData("wrongType","NeedsImage",new [] {
                    new MRKExecutionInput("Image", MRKInputSource.FlowInput,
                        "text", typeof(string))
                }));
            rejected = false;
            try { wrongType.Freeze(); }
            catch (MRKCodeRuntimeBuildException error)
            {
                rejected = error.Diagnostics.Any(d=>d.Message.Contains("类型不匹配"));
            }
            Check(rejected,"错误能力输入类型未被自动声明校验拦截");
        }

        private static void StandaloneCompilerNamedExports()
        {
            MRKCodeRuntimeBlueprint catalog = new MRKCodeRuntimeBuilder()
                .DataCapability("Double", (ctx, ct) => {
                    ctx.Data.Return("Doubled", ctx.Data.Get<int>("N") * 2);
                    return Task.FromResult((object)true);
                }, Output("Doubled"), new[] {
                    new MRKCapabilityInput("N",typeof(int))
                })
                .Flow("Definition",f=>f.StepData("calculate","Double",new[] {
                    new MRKExecutionInput("N",MRKInputSource.FlowInput,
                        "n",typeof(int))
                }).Export("Result","calculate.Doubled"))
                .Freeze();
            var single = new MRKExecutionPlanCompiler().Compile("Standalone",
                new[] {new MRKStepCompileRequest("calculate","Double",
                    inputs:new[] {
                        new MRKExecutionInput("N", MRKInputSource.FlowInput,
                            "n", typeof(int))
                    })}, catalog,
                new Dictionary<string,string>{{"Result","calculate.Doubled"}});
            Check(single.Succeeded &&
                single.Plan.DataExportTypes["Result"] == typeof(int),
                "单层Compiler没有复用具名DataOutputs/Flow导出协议");
            var runtime = new MRKExecutionEngine(new FCRWorkerOptions(2,8));
            try
            {
                MRKFlowResult value = runtime.StartFlow(single.Plan,
                    new Dictionary<string,object>{{"n",11}})
                    .Completion.GetAwaiter().GetResult();
                Check(value.State==MRKFlowState.Completed &&
                      value.GetCommittedOutput<int>("Result")==22,
                    "单层直接编译与子流程编译数据语义不同");
            }
            finally { runtime.StopAsync().GetAwaiter().GetResult(); }
        }

        /// <summary>
        /// R05：重复执行同一Data Step四轮；每轮有新的真实MDS输出帧，
        /// 下一节点从本轮Frame读取，不从上轮、其它Flow或共享字典偷读。
        /// </summary>
        private static void DynamicLoopCommittedDataFrames()
        {
            var seen=new System.Collections.Concurrent.ConcurrentDictionary<int,string>();
            var accepted=new List<int>();
            MRKExecutionPlan body=new MRKExecutionPlan("r05.body",new[]{
                new MRKExecutionStep("write",new string[0],(ctx,ct)=>{
                    ct.ThrowIfCancellationRequested();
                    int i=ctx.IterationIndex;
                    Check(ctx.Invocation!=null&&ctx.Data!=null&&
                        ctx.Invocation.FrameKey.Contains(".i"+i+"."),
                        "R05 MDS data Step has no per-iteration Invocation");
                    Check(seen.TryAdd(i,ctx.Invocation.FrameKey),
                        "R05 same iteration activated twice");
                    ctx.Data.Return("Value",i*11);
                    return Task.FromResult((object)true);
                },dataOutputs:new Dictionary<string,Type>{{"Value",typeof(int)}}),
                new MRKExecutionStep("read",new[]{"write"},(ctx,ct)=>{
                    ct.ThrowIfCancellationRequested();
                    int value=(int)ctx.BoundInputs["Value"];
                    lock(accepted){accepted.Add(value);}
                    return Task.FromResult((object)value);
                },inputs:new[]{new MRKExecutionInput("Value",MRKInputSource.StepOutput,
                    "write.Value",typeof(int))})
            });
            var region=new MRKLoopRegion(body,100000,(ctx,i)=>i<4);
            var plan=new MRKExecutionPlan("r05.dynamic",new[]{
                new MRKExecutionStep("loop",new string[0],
                    (ctx,ct)=>Task.FromResult((object)true),loopRegion:region)
            });
            Check(plan.Steps.Count==1&&body.Steps.Count==2,
                "R05 Body Plan repeated in compile stage");
            var engine=new MRKExecutionEngine(new FCRWorkerOptions(2,16));
            try
            {
                MRKFlowHandle run=engine.StartFlow(plan);
                MRKFlowResult result=run.Completion.GetAwaiter().GetResult();
                Check(result.State==MRKFlowState.Completed&&
                    (int)result.Results["loop"]==4,
                    "R05 loop did not really run four activations");
                lock(accepted)
                {
                    Check(accepted.Count==4&&
                        accepted.OrderBy(x=>x).SequenceEqual(new[]{0,11,22,33}),
                        "R05 committed StepOutput mixed up different iterations");
                }
                Check(seen.Count==4&&seen.Values.Distinct().Count()==4&&
                    run.Context.ObservationsSnapshot().ContainsKey("loop.i3.read"),
                    "R05 FrameKey or Step observation reused across activations");
            }
            finally{engine.StopAsync(true).GetAwaiter().GetResult();}
        }


        /// <summary>
        /// R07 E08-10：Join只读同一Flow、每轮独立MDS Frame的已提交结果；
        /// 多Flow复用同Plan不串账；跳过一轮时拒绝局部聚合。
        /// </summary>
        private static void R07_CompletedLoopOutputsAreFullyScoped()
        {
            MRKExecutionPlan body = new MRKExecutionPlan("r07.loop.body", new[]
            {
                new MRKExecutionStep("write", new string[0], (ctx, ct) =>
                {
                    int seed = (int)ctx.Inputs["Seed"];
                    ctx.Data.Return("Value", seed + ctx.IterationIndex);
                    return Task.FromResult((object)true);
                }, condition: ctx => !((bool)ctx.Inputs["SkipMiddle"] &&
                    ctx.IterationIndex == 1),
                    dataOutputs: Output("Value"))
            });
            MRKLoopRegion region = new MRKLoopRegion(body, 3, (ctx, i) => i < 3);
            MRKExecutionPlan plan = new MRKExecutionPlan("r07.loop.all", new[]
            {
                new MRKExecutionStep("loop", new string[0],
                    (ctx, ct) => Task.FromResult((object)true), loopRegion: region),
                new MRKExecutionStep("join", new[] { "loop" }, (ctx, ct) =>
                {
                    IReadOnlyList<MRKLoopOutput<int>> records =
                        ctx.CollectLoopOutputs<int>("loop", "write", "Value");
                    int sum = 0;
                    for (int i = 0; i < records.Count; i++)
                    {
                        MRKLoopOutput<int> item = records[i];
                        string key = "loop.i" + i + ".write";
                        Check(item.FlowId == ctx.FlowId &&
                            item.IterationIndex == i && item.FrameKey == key &&
                            item.InvocationId == ctx.FlowId.ToString("N") + ":" + key,
                            "R07迭代归并丢失原Flow/Invocation/索引身份");
                        sum += item.Value;
                    }
                    bool rejected = false;
                    try { ctx.CollectLoopOutputs<int>("loop", "write", "Missing"); }
                    catch (InvalidOperationException ex)
                    { rejected = ex.Message.Contains("missing_committed_output"); }
                    Check(rejected || records.Count == 0,
                        "未声明的具名输出被当作正常结果返回");
                    ctx.Data.Return("Total", sum);
                    return Task.FromResult((object)true);
                }, dataOutputs: Output("Total"))
            }, new Dictionary<string, string> { { "Final", "join.Total" } });
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(4, 16));
            try
            {
                MRKFlowHandle first = engine.StartFlow(plan, new Dictionary<string, object>
                    { { "Seed", 10 }, { "SkipMiddle", false } });
                MRKFlowHandle second = engine.StartFlow(plan, new Dictionary<string, object>
                    { { "Seed", 100 }, { "SkipMiddle", false } });
                MRKFlowResult a = first.Completion.GetAwaiter().GetResult();
                MRKFlowResult b = second.Completion.GetAwaiter().GetResult();
                Check(a.State == MRKFlowState.Completed &&
                    b.State == MRKFlowState.Completed &&
                    a.GetCommittedOutput<int>("Final") == 33 &&
                    b.GetCommittedOutput<int>("Final") == 303 &&
                    a.FlowId != b.FlowId,
                    "R07同Plan并发Flow的迭代值交叉或统计错误");
                Check(first.Context.ObservationsSnapshot()["loop.i1.write"].InvocationId !=
                    second.Context.ObservationsSnapshot()["loop.i1.write"].InvocationId,
                    "R07不同Flow复用相同循环节点没有独立Invocation身份");
                MRKFlowResult skipped = engine.StartFlow(plan, new Dictionary<string, object>
                    { { "Seed", 10 }, { "SkipMiddle", true } }).Completion
                    .GetAwaiter().GetResult();
                Check(skipped.State == MRKFlowState.Failed &&
                    skipped.CommittedOutputs.Count == 0 &&
                    skipped.StepStates["loop.i1.write"] == MRKStepState.Skipped &&
                    skipped.StepStates["join"] == MRKStepState.Failed,
                    "R07缺少一轮提交却发布部分Join结果");
            }
            finally { engine.StopAsync().GetAwaiter().GetResult(); }
        }

        /// <summary>R07：双层循环同名write，内层先按真实Invocation汇总，外层再汇总。</summary>
        private static void R07_NestedLoopOutputsPreserveInvocationPath()
        {
            MRKExecutionPlan innerBody = new MRKExecutionPlan("r07.nested.inner", new[]
            {
                new MRKExecutionStep("write", new string[0], (ctx, ct) =>
                {
                    ctx.Data.Return("Value", 1 + ctx.IterationIndex);
                    return Task.FromResult((object)true);
                }, dataOutputs: Output("Value"))
            });
            MRKExecutionPlan outerBody = new MRKExecutionPlan("r07.nested.outer", new[]
            {
                new MRKExecutionStep("inner", new string[0],
                    (ctx, ct) => Task.FromResult((object)true),
                    loopRegion: new MRKLoopRegion(innerBody, 2, (ctx, i) => i < 2)),
                new MRKExecutionStep("rollup", new[] { "inner" }, (ctx, ct) =>
                {
                    IReadOnlyList<MRKLoopOutput<int>> rows =
                        ctx.CollectLoopOutputs<int>("inner", "write", "Value");
                    Check(rows.Count == 2 && rows[0].Value == 1 && rows[1].Value == 2 &&
                        rows[0].FrameKey.StartsWith("outer.i" + ctx.IterationIndex + ".inner.i",
                            StringComparison.Ordinal),
                        "R07内层循环Output错误匹配其他层Invocation");
                    ctx.Data.Return("Partial", rows.Sum(x => x.Value));
                    return Task.FromResult((object)true);
                }, dataOutputs: Output("Partial"))
            });
            MRKExecutionPlan root = new MRKExecutionPlan("r07.nested.root", new[]
            {
                new MRKExecutionStep("outer", new string[0],
                    (ctx, ct) => Task.FromResult((object)true),
                    loopRegion: new MRKLoopRegion(outerBody, 2, (ctx, i) => i < 2)),
                new MRKExecutionStep("join", new[] { "outer" }, (ctx, ct) =>
                {
                    IReadOnlyList<MRKLoopOutput<int>> rows =
                        ctx.CollectLoopOutputs<int>("outer", "rollup", "Partial");
                    Check(rows.Count == 2 &&
                        rows[0].FrameKey == "outer.i0.rollup" &&
                        rows[1].FrameKey == "outer.i1.rollup" &&
                        rows[0].InvocationId != rows[1].InvocationId,
                        "R07两层归并的原Invocation路径错误");
                    ctx.Data.Return("Total", rows.Sum(x => x.Value));
                    return Task.FromResult((object)true);
                }, dataOutputs: Output("Total"))
            }, new Dictionary<string, string> { { "Total", "join.Total" } });
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(3, 10));
            try
            {
                MRKFlowResult result = engine.StartFlow(root).Completion.GetAwaiter().GetResult();
                Check(result.State == MRKFlowState.Completed &&
                    result.GetCommittedOutput<int>("Total") == 6 &&
                    result.StepStates.ContainsKey("outer.i1.inner.i1.write"),
                    "R07双层迭代真正执行或具名输出归并失败");
            }
            finally { engine.StopAsync().GetAwaiter().GetResult(); }
        }


        /// <summary>R07：实测重叠迭代、并发上限、原Invocation与有序汇总。</summary>
        private static void R07_ParallelForeachRealOverlapAndOutputIdentity()
        {
            var entered=new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var release=new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            int startCount=0,active=0,peak=0;
            var body=new MRKExecutionPlan("r07.parallel.body",new[]{
                new MRKExecutionStep("write",new string[0],async (ctx,token)=>{
                    int now=Interlocked.Increment(ref active);
                    int old;
                    while((old=Volatile.Read(ref peak))<now &&
                        Interlocked.CompareExchange(ref peak,now,old)!=old){}
                    if(Interlocked.Increment(ref startCount)==2)entered.TrySetResult(true);
                    try
                    {
                        await release.Task.ConfigureAwait(false);
                        ctx.Data.Return("Value",ctx.IterationIndex+1);
                        return (object)true;
                    }
                    finally{Interlocked.Decrement(ref active);}
                },dataOutputs:Output("Value"))
            });
            var parallel=new MRKLoopRegion(body,4,
                collectionInput:"Items",maxParallelIterations:2);
            var plan=new MRKExecutionPlan("r07.parallel",new[]{
                new MRKExecutionStep("loop",new string[0],(ctx,ct)=>
                    Task.FromResult((object)true),loopRegion:parallel),
                new MRKExecutionStep("join",new[]{"loop"},(ctx,ct)=>{
                    IReadOnlyList<MRKLoopOutput<int>> rows=
                        ctx.CollectLoopOutputs<int>("loop","write","Value");
                    Check(rows.Select(x=>x.Value).SequenceEqual(new[]{1,2,3,4}) &&
                        rows.Select(x=>x.InvocationId).Distinct().Count()==4 &&
                        rows.All(x=>x.FlowId==ctx.FlowId),
                        "R07并行迭代原Frame身份丢失或值被完成顺序覆盖");
                    ctx.Data.Return("Sum",rows.Sum(x=>x.Value));
                    return Task.FromResult((object)true);
                },dataOutputs:Output("Sum"))
            },new Dictionary<string,string>{{"Final","join.Sum"}});
            var runtime=new MRKExecutionEngine(new FCRWorkerOptions(2,8));
            try
            {
                MRKFlowHandle flow=runtime.StartFlow(plan,
                    new Dictionary<string,object>{{"Items",new[]{1,2,3,4}}});
                Task winner=Task.WhenAny(entered.Task,Task.Delay(TimeSpan.FromSeconds(7)))
                    .GetAwaiter().GetResult();
                Check(object.ReferenceEquals(winner,entered.Task)&&
                    !flow.Completion.IsCompleted,
                    "R07两轮未真正重叠却冒充并行，或提前结算Flow");
                release.TrySetResult(true);
                MRKFlowResult result=flow.Completion.GetAwaiter().GetResult();
                Check(result.State==MRKFlowState.Completed &&
                    result.GetCommittedOutput<int>("Final")==10 &&
                    startCount==4&&peak==2&&Volatile.Read(ref active)==0,
                    "R07并发上限或真实MDS输出失败");
            }
            finally{release.TrySetResult(true);runtime.StopAsync().GetAwaiter().GetResult();}
            bool invalid=false;
            try{new MRKLoopRegion(body,3,maxParallelIterations:2);}
            catch(ArgumentException){invalid=true;}
            Check(invalid,"R07未指定集合的任意循环不应并行");
            invalid=false;
            try{new MRKLoopRegion(body,3,(ctx,i)=>i<3,
                collectionInput:"Items",maxParallelIterations:2);}
            catch(ArgumentException){invalid=true;}
            Check(invalid,"R07依赖前轮谓词的循环不应并行");
        }

        /// <summary>
        /// R07：MaxIterations是安全预算，不是吞掉批次剩余图片的limit。
        /// Serial/parallel不允许静默截断，且超限必须在第一项能力执行前拒绝。
        /// </summary>
        private static void R07_ForeachBudgetMustRejectBeforeAnyBusinessWork()
        {
            int called=0;
            var body=new MRKExecutionPlan("r07.budget.body",new[]{
                new MRKExecutionStep("inspect",new string[0],(ctx,ct)=>{
                    Interlocked.Increment(ref called);
                    return Task.FromResult((object)true);
                })
            });
            var engine=new MRKExecutionEngine(new FCRWorkerOptions(2,6));
            try
            {
                foreach(int width in new[]{1,2})
                {
                    var loop=new MRKLoopRegion(body,2,collectionInput:"Items",
                        maxParallelIterations:width);
                    var plan=new MRKExecutionPlan("r07.budget."+width,new[]{
                        new MRKExecutionStep("foreach",new string[0],
                            (ctx,ct)=>Task.FromResult((object)true),loopRegion:loop)
                    });
                    int previous=Volatile.Read(ref called);
                    MRKFlowResult tooMany=engine.StartFlow(plan,
                        new Dictionary<string,object>{{"Items",new[]{1,2,3}}})
                        .Completion.GetAwaiter().GetResult();
                    Check(tooMany.State==MRKFlowState.Failed&&
                        tooMany.Error!=null&&
                        tooMany.Error.ToString().Contains("moc.foreach_budget_exceeded")&&
                        tooMany.CommittedOutputs.Count==0&&
                        Volatile.Read(ref called)==previous,
                        "R07 "+width+"个并行槽的Foreach静默截断/提前执行业务");
                    MRKFlowResult exact=engine.StartFlow(plan,
                        new Dictionary<string,object>{{"Items",new[]{1,2}}})
                        .Completion.GetAwaiter().GetResult();
                    Check(exact.State==MRKFlowState.Completed&&
                        Volatile.Read(ref called)==previous+2,
                        "R07 正好等于MaxIterations的集合不能完整执行");
                    MRKFlowResult empty=engine.StartFlow(plan,
                        new Dictionary<string,object>{{"Items",new int[0]}})
                        .Completion.GetAwaiter().GetResult();
                    Check(empty.State==MRKFlowState.Completed&&
                        Volatile.Read(ref called)==previous+2,
                        "R07 空集合不应触发业务");
                }
            }
            finally{engine.StopAsync().GetAwaiter().GetResult();}
        }

        /// <summary>R07：一轮失败后不合作的在途工作必须真实结束才释放流程。</summary>
        private static void R07_ParallelFailureWaitsForUncooperativeTail()
        {
            var first=new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var cancelled=new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var release=new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var body=new MRKExecutionPlan("r07.parallel.failed.body",new[]{
                new MRKExecutionStep("work",new string[0],async (ctx,ct)=>{
                    if(ctx.IterationIndex==0)
                    {
                        first.TrySetResult(true);
                        using(ct.Register(()=>cancelled.TrySetResult(true)))
                            await release.Task.ConfigureAwait(false);
                        ctx.Data.Return("Value",99);
                        return (object)true;
                    }
                    await first.Task.ConfigureAwait(false);
                    throw new InvalidOperationException("expected.parallel.failure");
                },dataOutputs:Output("Value"))
            });
            var loop=new MRKLoopRegion(body,2,
                collectionInput:"Items",maxParallelIterations:2);
            var plan=new MRKExecutionPlan("r07.parallel.failed",new[]{
                new MRKExecutionStep("loop",new string[0],
                    (ctx,ct)=>Task.FromResult((object)true),loopRegion:loop)
            });
            var runtime=new MRKExecutionEngine(new FCRWorkerOptions(2,5));
            try
            {
                MRKFlowHandle flow=runtime.StartFlow(plan,
                    new Dictionary<string,object>{{"Items",new[]{10,20}}});
                Task seen=Task.WhenAny(cancelled.Task,Task.Delay(TimeSpan.FromSeconds(7)))
                    .GetAwaiter().GetResult();
                Check(object.ReferenceEquals(seen,cancelled.Task) &&
                    !flow.Completion.IsCompleted,
                    "R07失败分支没有取消其余分支或提前释放非合作尾部");
                release.TrySetResult(true);
                MRKFlowResult result=flow.Completion.GetAwaiter().GetResult();
                Check(result.State==MRKFlowState.Failed&&
                    result.CommittedOutputs.Count==0&&
                    result.StepStates["loop.i1.work"]==MRKStepState.Failed&&
                    result.StepStates["loop.i0.work"]==MRKStepState.Cancelled,
                    "R07并行失败泄漏部分输出或误标真实状态");
            }
            finally{release.TrySetResult(true);runtime.StopAsync().GetAwaiter().GetResult();}
            bool rejected=false;
            var controlBody=new MRKExecutionPlan("r07.control",new[]{
                new MRKExecutionStep("break",new string[0],(ctx,ct)=>{
                    ctx.SignalControlExit("break");
                    return Task.FromResult((object)true);
                })
            });
            var controlPlan=new MRKExecutionPlan("r07.control.root",new[]{
                new MRKExecutionStep("loop",new string[0],
                    (ctx,ct)=>Task.FromResult((object)true),
                    loopRegion:new MRKLoopRegion(controlBody,2,
                        collectionInput:"Items",maxParallelIterations:2))
            });
            var checker=new MRKExecutionEngine(new FCRWorkerOptions(2,5));
            try
            {
                MRKFlowResult result=checker.StartFlow(controlPlan,
                    new Dictionary<string,object>{{"Items",new[]{1,2}}})
                    .Completion.GetAwaiter().GetResult();
                rejected=result.State==MRKFlowState.Failed&&
                    result.CommittedOutputs.Count==0;
            }
            finally{checker.StopAsync().GetAwaiter().GetResult();}
            Check(rejected,"R07并行Break不能被当作合法的随机退出");
        }

        /// <summary>R04：两个并行Flow运行同一个编译Step，各有独立Invocation和MDS Frame。</summary>
        private static void ConcurrentInvocationFrameIsolation()
        {
            var seen=new System.Collections.Concurrent.ConcurrentDictionary<int,string>();
            MRKCodeRuntimeBlueprint blueprint=new MRKCodeRuntimeBuilder()
                .DataCapability("Twice",(ctx,ct)=>{
                    Check(ctx.Invocation!=null&&ctx.Invocation.FlowId==ctx.FlowId&&
                        ctx.Invocation.StepId=="calculate"&&
                        ctx.Invocation.FrameKey=="calculate"&&ctx.Data!=null,
                        "实际节点没有绑定Invocation/MDS Frame");
                    int n=ctx.Data.Get<int>("N");
                    Check(seen.TryAdd(n,ctx.Invocation.InvocationId),
                        "并行流程错误共享输入或Invocation");
                    ctx.Data.Return("Value",n*2);
                    return Task.FromResult((object)true);
                },new Dictionary<string,Type>{{"Value",typeof(int)}},
                new[]{new MRKCapabilityInput("N",typeof(int))})
                .Flow("R04",f=>f.StepData("calculate","Twice",new[]{
                    new MRKExecutionInput("N",MRKInputSource.FlowInput,
                        "request",typeof(int))
                }).Export("answer","calculate.Value"))
                .Freeze();
            MRKCompileResult compiled=new MRKExecutionOrchestrationCompiler()
                .Compile("R04",blueprint,blueprint);
            Check(compiled.Succeeded,"R04独立Flow编译失败");
            MRKExecutionEngine engine=new MRKExecutionEngine(
                new FCRWorkerOptions(2,8));
            try
            {
                MRKFlowHandle left=engine.StartFlow(compiled.Plan,
                    new Dictionary<string,object>{{"request",7}});
                MRKFlowHandle right=engine.StartFlow(compiled.Plan,
                    new Dictionary<string,object>{{"request",11}});
                MRKFlowResult[] values=Task.WhenAll(left.Completion,right.Completion)
                    .GetAwaiter().GetResult();
                Check(values.All(x=>x.State==MRKFlowState.Completed)&&
                    values[0].GetCommittedOutput<int>("answer")==14&&
                    values[1].GetCommittedOutput<int>("answer")==22,
                    "并发Flow数据被覆盖");
                Check(seen.Count==2&&seen[7]!=seen[11]&&
                    seen[7].StartsWith(left.FlowId.ToString("N")+":",
                        StringComparison.Ordinal)&&
                    seen[11].StartsWith(right.FlowId.ToString("N")+":",
                        StringComparison.Ordinal),
                    "相同StepId的Invocation身份在并发Flow中冲突");
                Check(left.Context.ObservationsSnapshot()["calculate"].InvocationId==seen[7]&&
                    right.Context.ObservationsSnapshot()["calculate"].InvocationId==seen[11],
                    "观察快照没有沿用真实Invocation");
                Check(left.Context.Invocation==null&&right.Context.Invocation==null,
                    "Flow根Context错误变成某个节点的运行Context");
            }
            finally{engine.StopAsync().GetAwaiter().GetResult();}
        }

        /// <summary>R04：同一逻辑Step不同激活索引必须分离帧，失败帧不可泄漏输出。</summary>
        private static void RepeatedInvocationFrameIsolation()
        {
            using(var stack=new CxVisual.Mechanism.Data.RuntimeData.MDSDataStack())
            {
                Guid flowId=Guid.NewGuid();
                string[] keys={"sameStep","sameStep$activation.1"};
                for(int i=0;i<2;i++)
                {
                    var frame=stack.OpenFrame(keys[i],
                        stack.RootScope.CreateChild(keys[i]),
                        new Dictionary<string,object>{{"N",i+1}},
                        new Dictionary<string,Type>{{"Value",typeof(int)}});
                    Check(frame.InvocationId==keys[i]&&
                        frame.Data.Get<int>("N")==i+1,
                        "不同激活帧无法保留各自输入");
                    frame.Data.Return("Value",(i+1)*10);
                    frame.CommitOutputs();
                    frame.Dispose();
                }
                int first,second;
                Check(stack.TryReadOutput("sameStep","Value",out first)&&first==10&&
                    stack.TryReadOutput("sameStep$activation.1","Value",out second)&&second==20,
                    "相同逻辑节点多轮提交输出发生串账");
                var aborted=stack.OpenFrame("sameStep$activation.2",
                    stack.RootScope.CreateChild("sameStep$activation.2"),
                    new Dictionary<string,object>(),
                    new Dictionary<string,Type>{{"Value",typeof(int)}});
                aborted.Data.Return("Value",99);
                aborted.Dispose();
                int hidden;
                Check(!stack.TryReadOutput("sameStep$activation.2","Value",out hidden),
                    "失败或放弃的Frame公开了未提交输出");
            }
        }

        private static void OrdinaryCapabilityAndExistingStepResultsRemainUsable()
        {
            MRKDevelopmentRuntime runtime = new MRKCodeRuntimeBuilder()
                .Capability<int>("Old",(ctx,ct)=>Task.FromResult(6))
                .Flow("Legacy", f=>f.Step("a","Old"))
                .Build();
            try
            {
                MRKFlowResult result = runtime.RunAsync("Legacy")
                    .GetAwaiter().GetResult();
                Check(result.State == MRKFlowState.Completed &&
                    (int)result.Results["a"] == 6,
                    "增加具名输出后原普通StepResult能力被破坏");
            }
            finally { runtime.StopAsync().GetAwaiter().GetResult(); }
        }
    }
}
