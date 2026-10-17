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
