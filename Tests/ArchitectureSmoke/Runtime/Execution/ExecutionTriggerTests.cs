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
            StopWaitsForOwnedSettlement();
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
                "周期触发重入或在途队列没有收敛：peak=" + peak + ", rejected=" + trigger.Rejected + ", inFlight=" + trigger.InFlight);
            Await(engine.StopAsync());
        }
        private static void StopWaitsForOwnedSettlement()
        {
            // 公共行为竞态压力：Stop必须等待本Trigger登记清理，不依赖私有门或线程池调度顺序。
            for (int attempt = 0; attempt < 64; attempt++)
            {
                var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var plan = new MRKExecutionPlan("trigger-settlement", new[] {
                    new MRKExecutionStep("work", new string[0], async (context, token) => {
                        await release.Task.ConfigureAwait(false); return (object)7;
                    }) });
                var engine = new MRKExecutionEngine(new FCRWorkerOptions(2, 4));
                var trigger = new MRKExecutionTrigger(engine, plan, 1);
                var flow = trigger.TryTrigger();
                Task stopping = trigger.StopAsync();
                Check(!stopping.IsCompleted && trigger.InFlight == 1, "Stop未保留未结算的工作");
                release.SetResult(true); Await(stopping);
                Check(trigger.InFlight == 0 && flow.Completion.IsCompleted && trigger.TryTrigger() == null,
                    "Stop返回必须结算本Trigger在途登记并拒绝新触发，attempt=" + attempt);
                Await(engine.StopAsync());
            }
        }
    }
}
