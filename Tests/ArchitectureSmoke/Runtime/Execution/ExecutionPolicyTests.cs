using System;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Runtime.Execution;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class ExecutionPolicyTests
    {
        internal static void Run()
        {
            RetryRequiresExplicitIdempotency();
            BoundedRetryAndFaultPropagation();
            CooperativeTimeoutDoesNotPrematurelyAdvance();
            NonCooperativeTimeoutRemainsUnknownAndKeepsResources();
            TimeoutDiagnosticFailureCannotTriggerRetryOrEarlyRelease();
        }
        private static void Check(bool yes, string message)
        { if (!yes) throw new InvalidOperationException("运行策略：" + message); }
        private static T Await<T>(Task<T> task)
        {
            Task winner = Task.WhenAny(task, Task.Delay(6000)).GetAwaiter().GetResult();
            if (!object.ReferenceEquals(winner, task)) throw new TimeoutException("运行策略等待超时");
            return task.GetAwaiter().GetResult();
        }

        private static void NonCooperativeTimeoutRemainsUnknownAndKeepsResources()
        {
            TaskCompletionSource<object> finish = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            MRKResourceLeaseCoordinator resources = new MRKResourceLeaseCoordinator();
            MRKExecutionEngine engine = new MRKExecutionEngine(
                new FCRWorkerOptions(2, 4), resources);
            int advanced = 0;
            MRKExecutionPlan plan = new MRKExecutionPlan("non-cooperative", new[]
            {
                new MRKExecutionStep("external", new string[0],
                    (ctx, ct) => finish.Task,
                    policy: new MRKExecutionPolicy(timeout: TimeSpan.FromMilliseconds(40)),
                    requiredResources: new[] { "robot-axis" }),
                new MRKExecutionStep("dependent", new[] { "external" },
                    (ctx, ct) =>
                    {
                        Interlocked.Increment(ref advanced);
                        return Task.FromResult((object)2);
                    })
            });
            MRKFlowHandle flow = engine.StartFlow(plan);
            Task stop = null;
            try
            {
                bool unknown = false;
                for (int i = 0; i < 200; i++)
                {
                    MRKStepState state;
                    if (flow.Context.StatesSnapshot().TryGetValue("external", out state) &&
                        state == MRKStepState.ResultUnknown)
                    { unknown = true; break; }
                    Thread.Sleep(10);
                }
                Check(unknown && flow.State == MRKFlowState.Running && advanced == 0,
                    "非协作操作超时未暴露Unknown，或错误推进下游");
                MRKResourceLease stranger = resources.TryAcquire("robot-axis", "other");
                if (stranger != null) stranger.Dispose();
                Check(stranger == null && resources.CurrentOwner("robot-axis") != null,
                    "Unknown期间错误释放了资源并允许双重占用");
                stop = engine.StopAsync(true);
                Check(!stop.IsCompleted && !flow.Completion.IsCompleted,
                    "Stop对未结算非协作工作错误宣告停止成功");
            }
            finally
            {
                finish.TrySetResult(1);
                Await(flow.Completion);
                Await(stop ?? engine.StopAsync());
            }
            Check(advanced == 0 && resources.CurrentOwner("robot-axis") == null,
                "真实结算后下游误执行或资源没有最终释放");
        }


        private static void TimeoutDiagnosticFailureCannotTriggerRetryOrEarlyRelease()
        {
            int attempts = 0;
            ManualResetEventSlim timeoutObserved = new ManualResetEventSlim(false);
            TaskCompletionSource<object> pending = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            MRKExecutionPolicy policy = new MRKExecutionPolicy(
                maxAttempts: 3, idempotent: true,
                timeout: TimeSpan.FromMilliseconds(30));
            Task<object> execution = policy.ExecuteAsync(null,
                (ctx,ct) =>
                {
                    Interlocked.Increment(ref attempts);
                    return pending.Task;
                }, CancellationToken.None, () =>
                {
                    timeoutObserved.Set();
                    throw new System.IO.IOException("Journal不可用");
                });
            try
            {
                Check(timeoutObserved.Wait(2000) && !execution.IsCompleted &&
                    attempts == 1, "诊断故障提前结束了未结算的原始操作");
            }
            finally { pending.TrySetResult((object)1); }
            bool timedOut = false;
            try { Await(execution); }
            catch (TimeoutException ex)
            { timedOut = ex.InnerException is System.IO.IOException; }
            Check(timedOut && attempts == 1,
                "诊断故障吞掉了超时信息或把未知结果操作自动重试");
            timeoutObserved.Dispose();
        }

        private static void Await(Task task)
        {
            Task winner = Task.WhenAny(task, Task.Delay(6000)).GetAwaiter().GetResult();
            if (!object.ReferenceEquals(winner, task)) throw new TimeoutException("运行策略等待超时");
            task.GetAwaiter().GetResult();
        }
        private static void RetryRequiresExplicitIdempotency()
        {
            bool invalid = false;
            try { new MRKExecutionPolicy(maxAttempts: 2); }
            catch (ArgumentException) { invalid = true; }
            Check(invalid, "非幂等操作被允许自动重试");
        }
        private static void BoundedRetryAndFaultPropagation()
        {
            int attempts = 0;
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(1, 4));
            MRKExecutionPlan plan = new MRKExecutionPlan("retry", new[]
            {
                new MRKExecutionStep("work", new string[0], (ctx, ct) =>
                {
                    if (Interlocked.Increment(ref attempts) < 3)
                        throw new InvalidOperationException("暂时不可用");
                    return Task.FromResult((object)99);
                }, policy: new MRKExecutionPolicy(3, idempotent: true,
                    retryCondition: ex => ex is InvalidOperationException))
            });
            MRKFlowResult result = Await(engine.StartFlow(plan).Completion);
            Check(result.State == MRKFlowState.Completed &&
                attempts == 3 && (int)result.Results["work"] == 99, "重试或结果错误");
            int failedAttempts = 0;
            MRKExecutionPlan fails = new MRKExecutionPlan("retryFailed", new[]
            {
                new MRKExecutionStep("bad", new string[0], (ctx, ct) =>
                {
                    Interlocked.Increment(ref failedAttempts);
                    throw new InvalidOperationException("总是失败");
                }, policy: new MRKExecutionPolicy(2, idempotent: true))
            });
            Check(Await(engine.StartFlow(fails).Completion).State == MRKFlowState.Failed &&
                failedAttempts == 2, "失败重试无边界");
            Await(engine.StopAsync());
        }
        private static void CooperativeTimeoutDoesNotPrematurelyAdvance()
        {
            int active = 0, after = 0;
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(2, 4));
            MRKExecutionPlan plan = new MRKExecutionPlan("timeout", new[]
            {
                new MRKExecutionStep("slow", new string[0], async (ctx, ct) =>
                {
                    Interlocked.Increment(ref active);
                    try { await Task.Delay(TimeSpan.FromSeconds(3), ct).ConfigureAwait(false); }
                    finally { Interlocked.Decrement(ref active); }
                    return (object)1;
                }, policy: new MRKExecutionPolicy(timeout: TimeSpan.FromMilliseconds(30))),
                new MRKExecutionStep("next", new[] { "slow" }, (ctx, ct) =>
                {
                    Interlocked.Increment(ref after);
                    return Task.FromResult((object)2);
                })
            });
            MRKFlowResult result = Await(engine.StartFlow(plan).Completion);
            Check(result.State == MRKFlowState.Failed && active == 0 &&
                after == 0 && result.Error != null, "节点超时后错误推进或释放资源");
            Await(engine.StopAsync());
        }
    }
}
