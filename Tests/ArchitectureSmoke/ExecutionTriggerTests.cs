using System;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Runtime.Execution;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class ExecutionTriggerTests
    {
        internal static void Run()
        {
            ManualTriggerConcurrencyAndDrain();
            PeriodicTriggerNeverLaunchesUnboundedFlows();
        }
        private static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException("触发测试：" + message); }
        private static T Await<T>(Task<T> task)
        {
            Task winner = Task.WhenAny(task, Task.Delay(6000)).GetAwaiter().GetResult();
            if (!object.ReferenceEquals(winner, task)) throw new TimeoutException("触发测试超时");
            return task.GetAwaiter().GetResult();
        }
        private static void Await(Task task)
        {
            Task winner = Task.WhenAny(task, Task.Delay(6000)).GetAwaiter().GetResult();
            if (!object.ReferenceEquals(winner, task)) throw new TimeoutException("触发测试超时");
            task.GetAwaiter().GetResult();
        }
        private static void ManualTriggerConcurrencyAndDrain()
        {
            TaskCompletionSource<bool> entered = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> release = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            MRKExecutionPlan plan = new MRKExecutionPlan("event-plan", new[]
            {
                new MRKExecutionStep("run", new string[0], async (context, token) =>
                {
                    entered.TrySetResult(true);
                    await release.Task.ConfigureAwait(false);
                    return (object)5;
                })
            });
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(2, 4));
            MRKExecutionTrigger trigger = new MRKExecutionTrigger(engine, plan, 1);
            MRKFlowHandle first = trigger.TryTrigger();
            Check(first != null, "首次触发未接纳");
            Await(entered.Task);
            Check(trigger.TryTrigger() == null && trigger.Rejected == 1 &&
                trigger.InFlight == 1, "在途限制或拒绝统计不正确");
            Task stopping = trigger.StopAsync();
            Check(!stopping.IsCompleted && trigger.TryTrigger() == null,
                "Stop未等待在途流程或意外继续接单");
            release.TrySetResult(true);
            Check(Await(first.Completion).State == MRKFlowState.Completed,
                "已有流程未正常排空");
            Await(stopping);
            Await(engine.StopAsync());
        }
        private static void PeriodicTriggerNeverLaunchesUnboundedFlows()
        {
            int active = 0, peak = 0, count = 0;
            MRKExecutionPlan plan = new MRKExecutionPlan("periodic", new[]
            {
                new MRKExecutionStep("tick", new string[0], async (ctx, token) =>
                {
                    int now = Interlocked.Increment(ref active);
                    if (now > peak) peak = now;
                    try { await Task.Delay(40, token).ConfigureAwait(false); }
                    finally { Interlocked.Decrement(ref active); }
                    Interlocked.Increment(ref count);
                    return (object)count;
                })
            });
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(3, 8));
            MRKExecutionTrigger trigger = new MRKExecutionTrigger(engine, plan, 1);
            trigger.StartPeriodic(TimeSpan.FromMilliseconds(5));
            Check(SpinWait.SpinUntil(() => Volatile.Read(ref count) >= 2, 3000),
                "周期触发没有执行两次");
            Await(trigger.StopAsync());
            Check(peak == 1 && trigger.Rejected > 0 && trigger.InFlight == 0,
                "周期触发重入或在途队列没有收敛");
            Await(engine.StopAsync());
        }
    }
}
