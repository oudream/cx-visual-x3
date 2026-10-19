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
            NaturalTypedBusinessRequest();
            TypedOutputRunValue();
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


        /// <summary>R01：自然的单值返回，且不得隐藏失败、绕开Export或猜测输出。</summary>
        private static void TypedOutputRunValue()
        {
            MRKCodeRuntimeBuilder builder = new MRKCodeRuntimeBuilder()
                .DataCapability("DoubleValue", (context, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    context.Data.Return("Result", context.Data.Get<int>("N") * 2);
                    return Task.FromResult((object)true);
                }, new Dictionary<string, Type> { { "Result", typeof(int) } },
                new[] { new MRKCapabilityInput("N", typeof(int)) })
                .DataCapability("FailValue", (context, token) =>
                    Task.FromException<object>(new InvalidOperationException("R01.expected.failure")),
                    new Dictionary<string, Type> { { "Result", typeof(int) } })
                .Flow("double", flow => flow.StepData("step", "DoubleValue",
                    new[] { new MRKExecutionInput("N", MRKInputSource.FlowInput,
                        "request", typeof(int)) }).Export("answer", "step.Result"))
                .Flow("private", flow => flow.StepData("step", "DoubleValue",
                    new[] { new MRKExecutionInput("N", MRKInputSource.FlowInput,
                        "request", typeof(int)) }))
                .Flow("fails", flow => flow.Step("fail", "FailValue")
                    .Export("answer", "fail.Result"));
            MRKDevelopmentRuntime runtime = builder.Build();
            try
            {
                int number = Wait(runtime.RunValueAsync<int>("double", (object)12, "answer"));
                Check(number == 24, "业务单请求未正确返回具名提交值");
                int fromPorts = Wait(runtime.RunValueAsync<int>("double",
                    new Dictionary<string, object> { { "request", 7 } }, "answer"));
                Check(fromPorts == 14, "显式多端口输入未返回同一契约的值");

                bool refused = false;
                try { Wait(runtime.RunValueAsync<int>("double", (object)12, "missing")); }
                catch (KeyNotFoundException) { refused = true; }
                Check(refused, "未声明Export的输出被错误读取");

                refused = false;
                try { Wait(runtime.RunValueAsync<string>("double", (object)12, "answer")); }
                catch (InvalidCastException) { refused = true; }
                Check(refused, "输出类型不匹配未拒绝");

                refused = false;
                try { Wait(runtime.RunValueAsync<int>("private", (object)12, "Result")); }
                catch (KeyNotFoundException) { refused = true; }
                Check(refused, "私有Step输出不能冒充Flow Export");

                refused = false;
                try { Wait(runtime.RunValueAsync<int>("fails", (object)1, "answer")); }
                catch (InvalidOperationException ex)
                {
                    refused = ex.InnerException is InvalidOperationException &&
                        ex.InnerException.Message == "R01.expected.failure" &&
                        ex.Message.Contains("FlowId=") && ex.Message.Contains("Failed");
                }
                Check(refused, "流程失败被当作成功或丢失FlowId及原始异常");

                refused = false;
                try { Wait(runtime.RunValueAsync<int>("double", (object)1, " ")); }
                catch (ArgumentException) { refused = true; }
                Check(refused, "空Export名称应在执行前明确拒绝");
            }
            finally { Wait(runtime.StopAsync()); }
        }

        private static void NaturalTypedBusinessRequest()
        {
            MRKDevelopmentRuntime runtime = new MRKCodeRuntimeBuilder()
                .CapabilityForRequest<int,int>("Offset",(request,ct) =>
                    Task.FromResult(request+100))
                .Flow("business",flow => flow.Step("action","Offset"))
                .Build();
            try
            {
                MRKFlowResult simple=Wait(runtime.RunAsync("business", 3));
                Check(simple.State==MRKFlowState.Completed &&
                    (int)simple.Results["action"]==103,
                    "RunAsync(flow,request)没有直接调用类型安全的业务能力");
                MRKFlowResult explicitInput=Wait(runtime.RunAsync("business",
                    new Dictionary<string,object>{{"request",2}}));
                Check(explicitInput.State==MRKFlowState.Completed &&
                    (int)explicitInput.Results["action"]==102,
                    "显式Flow输入被误当成单个request对象");
                MRKFlowResult wrong=Wait(runtime.RunAsync("business","not-an-int"));
                Check(wrong.State==MRKFlowState.Failed && wrong.Error!=null,
                    "不匹配业务类型未返回明确失败结果");
            }
            finally { Wait(runtime.StopAsync()); }
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
