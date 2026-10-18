using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Runtime.Execution;
using CxVisual.TestCenter.Core;
using CxVisual.TestCenter.Discovery;

namespace CxVisual.TestCenter.Cases
{
    public static class TCTRuntimeDataCompilationCases
    {
        [TCTTest("TCT.MDS.028", "代码优先与统一编排共享子流程具名输入输出契约",
            TCTTestAspect.Mechanism, "Data / RuntimeData", ObjectId = "MRKExecutionOrchestrationCompiler")]
        public static void TwoLevelNamedPortFlow(TCTTestContext t)
        {
            var builder = new MRKCodeRuntimeBuilder()
                .DataCapability("Calc", (ctx, ct) =>
                {
                    ctx.Data.Return("Value", ctx.Data.Get<int>("N") * 2);
                    return Task.FromResult((object)true);
                }, new Dictionary<string, Type>{{"Value",typeof(int)}})
                .Flow("Child", flow => flow.StepData("double", "Calc",
                    new [] { new MRKExecutionInput("N", MRKInputSource.FlowInput,
                        "ChildValue", typeof(int)) })
                    .Export("Result", "double.Value"))
                .Flow("Root", flow => flow.Subflow("invoke", "Child", new [] {
                    new MRKExecutionInput("ChildValue",MRKInputSource.FlowInput,
                        "Seed",typeof(int))
                }).Export("Final", "invoke.Result"));
            MRKCodeRuntimeBlueprint blueprint = builder.Freeze();
            MRKCompileResult compiled = new MRKExecutionOrchestrationCompiler()
                .Compile("Root", blueprint, blueprint);
            t.AssertEqual(true, compiled.Succeeded, "两级编排使用相同Compiler");
            t.AssertEqual(typeof(int), compiled.Plan.DataExportTypes["Final"],
                "具名Flow出口保留专业数据类型");
            MRKDevelopmentRuntime runtime = blueprint.BuildForDevelopment(new FCRWorkerOptions(2,8));
            try
            {
                MRKFlowResult result = runtime.RunAsync("Root",
                    new Dictionary<string,object>{{"Seed",7}})
                    .GetAwaiter().GetResult();
                if (result.State != MRKFlowState.Completed)
                    throw new InvalidOperationException("具名端口流程失败：" +
                        (result.Error == null ? "" : result.Error.ToString()));
                t.AssertEqual(14, result.GetCommittedOutput<int>("Final"),
                    "显式调用端口到子流程再到公开出口真实生效");
                t.AssertEqual(1, result.CommittedOutputs.Count,
                    "内部专业输出不得绕开公开出口");
            }
            finally { runtime.StopAsync().GetAwaiter().GetResult(); }
        }

        [TCTTest("TCT.MDS.029", "子流程缺少显式输入映射不应偷取根Flow数据",
            TCTTestAspect.Mechanism, "Data / RuntimeData", ObjectId = "MRKExecutionPlan")]
        public static void ImplicitSubflowInputIsRejected(TCTTestContext t)
        {
            MRKCodeRuntimeBuilder builder = new MRKCodeRuntimeBuilder()
                .DataCapability("Read",(ctx,ct) => {
                    ctx.Data.Return("Out",ctx.Data.Get<int>("In"));
                    return Task.FromResult((object)true);
                },new Dictionary<string,Type>{{"Out",typeof(int)}})
                .Flow("Child", f=>f.StepData("read","Read",new [] {
                    new MRKExecutionInput("In",MRKInputSource.FlowInput,
                        "Required",typeof(int))
                }).Export("Public","read.Out"))
                .Flow("Parent",f=>f.Subflow("child","Child"));
            bool rejected = false;
            try { builder.Freeze(); }
            catch(MRKCodeRuntimeBuildException ex)
            { rejected = ex.Diagnostics.Any(x => x.Code == "INVALID_ORCHESTRATION"); }
            t.AssertEqual(true,rejected,"隐式继承父Flow参数必须在编译期拒绝");
        }
    }
}
