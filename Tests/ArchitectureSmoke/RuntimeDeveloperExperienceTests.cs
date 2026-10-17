using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Runtime.Execution;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class RuntimeDeveloperExperienceTests
    {
        internal static void Run()
        {
            MinimalRunAndTypedRequest();
            InvalidDefinitionsNeverCreateRunnableRuntime();
            MultiFlowSharedWorkerAndCancellation();
            OneBlueprintBothDevelopmentAndAuthorizedProductionPublication();
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Runtime开发者入口：" + message);
        }
        private static T Wait<T>(Task<T> task)
        {
            Task first = Task.WhenAny(task, Task.Delay(8000)).GetAwaiter().GetResult();
            if (!object.ReferenceEquals(first, task))
                throw new TimeoutException("Runtime简洁API测试超时");
            return task.GetAwaiter().GetResult();
        }
        private static void Wait(Task task)
        {
            Task first = Task.WhenAny(task, Task.Delay(8000)).GetAwaiter().GetResult();
            if (!object.ReferenceEquals(first, task))
                throw new TimeoutException("Runtime简洁API测试超时");
            task.GetAwaiter().GetResult();
        }

        private static void MinimalRunAndTypedRequest()
        {
            // 两个流程与一个嵌套子流程，全程不用实现Resolver/Catalog或手动构造Plan。
            MRKCodeRuntimeBuilder builder = new MRKCodeRuntimeBuilder()
                .Capability<int>("Normalize",(c,t) =>
                    Task.FromResult((int)c.Inputs["request"] + 1))
                .Capability<int>("Double",(c,t) =>
                {
                    object value;
                    Check(c.TryGetResult("normalize",out value),
                        "内部StepResult未正确映射嵌套相对作用域");
                    return Task.FromResult((int)value * 2);
                })
                .Capability<int>("Finish",(c,t) =>
                {
                    object value;
                    Check(c.TryGetResult("pipeline.twice",out value),
                        "子流程结果没有正确传播到根流程");
                    return Task.FromResult((int)value + 1);
                })
                .Flow("Child",f => f
                    .Step("normalize","Normalize")
                    .Step("twice","Double","normalize"))
                .Flow("Main",f => f
                    .Subflow("pipeline","Child")
                    .Step("finish","Finish","pipeline"));

            MRKDevelopmentRuntime runtime = builder.Build(new FCRWorkerOptions(2,8));
            try
            {
                Check(runtime.FlowIds.SequenceEqual(new[]{"Child","Main"}),
                    "流程名称集合不稳定");
                MRKFlowResult result = Wait(runtime.RunRequestAsync("Main", 5));
                Check(result.State == MRKFlowState.Completed &&
                    (int)result.Results["pipeline.normalize"] == 6 &&
                    (int)result.Results["pipeline.twice"] == 12 &&
                    (int)result.Results["finish"] == 13,
                    "简洁API未通过同一OrchestrationCompiler/Engine执行");
                builder.Capability<int>("Future",(c,t) => Task.FromResult(9))
                    .Flow("Later",f => f.Step("f","Future"));
                bool missing = false;
                try { runtime.StartFlow("Later"); }
                catch(ArgumentException) { missing = true; }
                Check(missing && runtime.FlowIds.Count == 2,
                    "Build以后注册的能力/流程污染了已构建的Runtime");
            }
            finally { Wait(runtime.StopAsync()); }
        }

        private static void InvalidDefinitionsNeverCreateRunnableRuntime()
        {
            MRKCodeRuntimeBuilder invalid = new MRKCodeRuntimeBuilder()
                .Flow("Unresolvable",f => f.Step("x","Missing"));
            bool rejected = false;
            try { invalid.Build(); }
            catch (MRKCodeRuntimeBuildException ex)
            {
                rejected = ex.FlowId == "Unresolvable" &&
                    ex.Diagnostics.Any(d => d.Code == "UNKNOWN_CAPABILITY");
            }
            Check(rejected, "未解析能力没有在启动Worker前编译拒绝");

            MRKCodeRuntimeBuilder cycles = new MRKCodeRuntimeBuilder()
                .Flow("A",f => f.Subflow("a","B"))
                .Flow("B",f => f.Subflow("b","A"));
            rejected = false;
            try { cycles.Build(); }
            catch(MRKCodeRuntimeBuildException ex)
            { rejected = ex.Diagnostics.Any(d => d.Code == "INVALID_ORCHESTRATION"); }
            Check(rejected, "跨流程循环引用未拒绝");

            MRKCodeRuntimeBuilder duplicates = new MRKCodeRuntimeBuilder();
            rejected = false;
            try
            {
                duplicates.Flow("d", f => f.Step("a","A").Step("a","A"));
            }
            catch(ArgumentException) { rejected = true; }
            Check(rejected, "同一流程重复节点被静默接受");
        }


        private sealed class Approval : IMRKPlanPublicationPolicy
        {
            internal bool Safe;
            public bool Authorize(string principal, MRKPlanPublishAction action,
                MRKExecutionPlan plan, long revision)
            { return principal == "publisher"; }
            public bool IsSafeToActivate(MRKExecutionPlan plan, long revision)
            { return Safe; }
        }

        private static void OneBlueprintBothDevelopmentAndAuthorizedProductionPublication()
        {
            MRKCodeRuntimeBuilder developer = new MRKCodeRuntimeBuilder()
                .Capability<int>("Number",(ctx,ct)=>Task.FromResult(17))
                .Flow("sub",f=>f.Step("read","Number"))
                .Flow("business",f=>f.Subflow("child","sub"));
            MRKCodeRuntimeBlueprint blueprint = developer.Freeze();
            developer.Capability<int>("New",(ctx,ct)=>Task.FromResult(99))
                .Flow("late",f=>f.Step("only","New"));
            Check(blueprint.FlowIds.SequenceEqual(new[]{"business","sub"}),
                "已经冻结的编排定义被开发者后续修改污染");

            MRKDevelopmentRuntime sandbox = blueprint.BuildForDevelopment();
            try
            {
                MRKFlowResult simulated = Wait(sandbox.RunAsync("business"));
                Check(simulated.State == MRKFlowState.Completed &&
                    (int)simulated.Results["child.read"] == 17,
                    "冻结编排无法用于开发模拟运行");
            }
            finally { Wait(sandbox.StopAsync()); }

            MRKExecutionPlan initial=new MRKExecutionPlan("initial",new[]{
                new MRKExecutionStep("old",new string[0],
                    (c,t)=>Task.FromResult((object)1))
            });
            MRKExecutionPlanSlot slot=new MRKExecutionPlanSlot(initial,1,true);
            Approval approval=new Approval();
            MRKExecutionPlanPublisher publisher=
                new MRKExecutionPlanPublisher(slot,initial,1,approval);
            bool unauthorized=false;
            try
            {
                publisher.CompileAndStageBlueprint("readonly","business",blueprint,2);
            }
            catch (UnauthorizedAccessException) { unauthorized=true; }
            Check(unauthorized && !publisher.PendingRevision.HasValue,
                "统一Blueprint绕过了正式发布权限");
            MRKPlanPublicationReceipt staged=publisher.CompileAndStageBlueprint(
                "publisher","business",blueprint,2);
            Check(staged.Accepted && publisher.PendingRevision==2,
                "同一Blueprint不能经官方Publisher部署");
            Check(!publisher.TryActivate("publisher"),
                "未到安全点的发布被错误激活");
            approval.Safe = true;
            Check(publisher.TryActivate("publisher"),
                "正式发布安全点不能激活已经审核的代码定义");

            MRKExecutionEngine engine=new MRKExecutionEngine(new FCRWorkerOptions(1,4));
            try
            {
                MRKFlowResult published=Wait(engine.StartFromSlot(slot).Completion);
                Check(published.State==MRKFlowState.Completed &&
                    published.Metadata.PlanRevision==2 &&
                    (int)published.Results["child.read"]==17,
                    "代码定义发布后未走同一编排执行内核");
            }
            finally { Wait(engine.StopAsync()); }
        }

        private static void MultiFlowSharedWorkerAndCancellation()
        {
            TaskCompletionSource<bool> entered = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> release = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            int count = 0;
            MRKDevelopmentRuntime runtime = new MRKCodeRuntimeBuilder()
                .Capability<int>("Blocked", async (c,t) =>
                {
                    Interlocked.Increment(ref count);
                    entered.TrySetResult(true);
                    await release.Task.ConfigureAwait(false);
                    t.ThrowIfCancellationRequested();
                    return 1;
                })
                .Flow("slow",f => f.Step("wait","Blocked"))
                .Build(new FCRWorkerOptions(1,2));
            try
            {
                MRKFlowHandle first = runtime.StartFlow("slow");
                Wait(entered.Task);
                MRKFlowHandle second = runtime.StartFlow("slow");
                Check(runtime.Snapshot().Active == 1 &&
                    runtime.Snapshot().Pending == 1, "代码接口创建了额外调度器");
                second.RequestCancel();
                Check(Wait(second.Completion).State == MRKFlowState.Cancelled,
                    "运行票据主动取消未生效");
                release.TrySetResult(true);
                Check(Wait(first.Completion).State == MRKFlowState.Completed && count == 1,
                    "第二次调用绕过单Worker调度或者取消影响第一个Flow");
                bool unknown = false;
                try { Wait(runtime.RunAsync("not-exist")); }
                catch(ArgumentException) { unknown = true; }
                Check(unknown, "未知流程没有明确拒绝");
            }
            finally
            {
                release.TrySetResult(true);
                Wait(runtime.StopAsync(true));
            }
        }
    }
}
