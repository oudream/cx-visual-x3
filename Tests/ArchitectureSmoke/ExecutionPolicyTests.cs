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
        }
        private static void Check(bool yes, string message)
        { if (!yes) throw new InvalidOperationException("运行策略：" + message); }
        private static T Await<T>(Task<T> task)
        {
            Task winner = Task.WhenAny(task, Task.Delay(6000)).GetAwaiter().GetResult();
            if (!object.ReferenceEquals(winner, task)) throw new TimeoutException("运行策略等待超时");
            return task.GetAwaiter().GetResult();
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
