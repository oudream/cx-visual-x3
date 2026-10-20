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
            ConcurrentInvocationFrameIsolation();
            RepeatedInvocationFrameIsolation();
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
            Check(compiled.Succeeded,
                "三层真实端口编译失败：" +
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
