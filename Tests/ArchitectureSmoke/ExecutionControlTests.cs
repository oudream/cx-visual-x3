using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Runtime.Execution;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class ExecutionControlTests
    {
        internal static void Run()
        {
            ConditionalBranchesAndBoundedLoops();
            DebugBreakpointStepAndResume();
            CancellationUnblocksPausedFlow();
            BoundedTraceDropsOldest();
            TraceCursorDetectsLostTransitions();
            StopDrainsOrCancelsEntireFlow();
            ResourcesAreDeclarativeAndDoNotBlockWorker();
        }
        private static void Check(bool ok, string msg)
        { if (!ok) throw new InvalidOperationException("运行控制测试：" + msg); }
        private static T Await<T>(Task<T> task)
        {
            Task winner = Task.WhenAny(task, Task.Delay(6000)).GetAwaiter().GetResult();
            if (!object.ReferenceEquals(winner, task)) throw new TimeoutException("控制测试等待超时");
            return task.GetAwaiter().GetResult();
        }
        private static void Await(Task task)
        {
            Task winner = Task.WhenAny(task, Task.Delay(6000)).GetAwaiter().GetResult();
            if (!object.ReferenceEquals(winner, task)) throw new TimeoutException("控制测试等待超时");
            task.GetAwaiter().GetResult();
        }
        private static bool Until(Func<bool> condition)
        { return SpinWait.SpinUntil(condition, 3000); }
        private static void ConditionalBranchesAndBoundedLoops()
        {
            int trueCalls = 0, falseCalls = 0, loops = 0;
            MRKExecutionStep start = new MRKExecutionStep("condition", new string[0],
                MRKExecutionOperations.Condition(
                    ctx => (int)ctx.Inputs["Mode"] == 1,
                    (ctx, ct) => { Interlocked.Increment(ref trueCalls);
                        return Task.FromResult((object)"YES"); },
                    (ctx, ct) => { Interlocked.Increment(ref falseCalls);
                        return Task.FromResult((object)"NO"); }));
            MRKExecutionStep loop = new MRKExecutionStep("loop", new[] { "condition" },
                MRKExecutionOperations.Loop(8,
                    (ctx, index) => index < 3,
                    (ctx, index, token) =>
                    {
                        Interlocked.Increment(ref loops);
                        return Task.FromResult((object)index);
                    }));
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(1, 6));
            MRKExecutionPlan plan = new MRKExecutionPlan("conditions", new[] { start, loop });
            MRKFlowResult one = Await(engine.StartFlow(plan,
                new Dictionary<string, object> { { "Mode", 1 } }).Completion);
            MRKFlowResult two = Await(engine.StartFlow(plan,
                new Dictionary<string, object> { { "Mode", 0 } }).Completion);
            Check(one.State == MRKFlowState.Completed && (string)one.Results["condition"] == "YES"
                && (int)one.Results["loop"] == 2, "true branch/loop incorrect");
            Check(two.State == MRKFlowState.Completed && (string)two.Results["condition"] == "NO"
                && trueCalls == 1 && falseCalls == 1 && loops == 6, "false branch/loop incorrect");
            Await(engine.StopAsync());
        }
        private static void DebugBreakpointStepAndResume()
        {
            int count = 0;
            MRKExecutionDebugController debug = new MRKExecutionDebugController();
            debug.AddBreakpoint("first");
            MRKExecutionPlan plan = new MRKExecutionPlan("debug", new[]
            {
                new MRKExecutionStep("first", new string[0], (c, ct) =>
                { Interlocked.Increment(ref count); return Task.FromResult((object)1); }),
                new MRKExecutionStep("second", new[] { "first" }, (c, ct) =>
                { Interlocked.Increment(ref count); return Task.FromResult((object)2); })
            });
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(1, 4));
            MRKFlowHandle flow = engine.StartFlowWithControl(plan, debug);
            Check(Until(() => debug.WaitingCount > 0), "breakpoint not reached");
            Check(count == 0 && debug.IsPaused, "breakpoint did not stop before node");
            Check(debug.StepOnce(), "single step rejected");
            Check(Until(() => flow.Context.StatesSnapshot()["second"] == MRKStepState.Paused),
                "dependent node did not pause");
            Check(count == 1, "step should execute only one node");
            debug.Resume();
            MRKFlowResult result = Await(flow.Completion);
            Check(result.State == MRKFlowState.Completed && count == 2, "debug completion wrong");
            Await(engine.StopAsync());
        }
        private static void CancellationUnblocksPausedFlow()
        {
            MRKExecutionDebugController debug = new MRKExecutionDebugController();
            debug.Pause();
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(1, 2));
            MRKFlowHandle flow = engine.StartFlowWithControl(new MRKExecutionPlan("paused", new[]
            {
                new MRKExecutionStep("blocked", new string[0],
                    (ctx, ct) => Task.FromResult((object)1))
            }), debug);
            Check(Until(() => debug.WaitingCount > 0), "no waiting step");
            Check(flow.RequestCancel(), "cancel rejected");
            MRKFlowResult result = Await(flow.Completion);
            Check(result.State == MRKFlowState.Cancelled && !result.Results.ContainsKey("blocked"),
                "cancelled paused flow executed side effect");
            Await(engine.StopAsync());
        }
        private static void BoundedTraceDropsOldest()
        {
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(1, 2));
            MRKExecutionPlan plan = new MRKExecutionPlan("trace", new[]
            {
                new MRKExecutionStep("A", new string[0],
                    (c, ct) => Task.FromResult((object)1)),
                new MRKExecutionStep("B", new[] { "A" },
                    (c, ct) => Task.FromResult((object)2))
            });
            MRKFlowHandle handle = engine.StartFlow(plan);
            MRKFlowResult result = Await(handle.Completion);
            IList<MRKExecutionTransition> records = handle.Context.Trace.Snapshot();
            Check(result.State == MRKFlowState.Completed && records.Count >= 4,
                "actual flow produced too few trace events");
            for (int i = 1; i < records.Count; i++)
                Check(records[i].Sequence > records[i - 1].Sequence,
                    "trace sequence not ordered");
            Check(records[records.Count - 1].StepId == "B" &&
                records[records.Count - 1].State == MRKStepState.Completed,
                "missing terminal trace");
            Await(engine.StopAsync());
        }        private static void StopDrainsOrCancelsEntireFlow()
        {
            TaskCompletionSource<bool> entered = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> proceed = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            MRKExecutionPlan plan = new MRKExecutionPlan("lifecycle", new[]
            {
                new MRKExecutionStep("slow", new string[0], async (ctx, token) =>
                {
                    entered.TrySetResult(true);
                    await proceed.Task.ConfigureAwait(false);
                    return (object)1;
                })
            });
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(1, 3));
            MRKFlowHandle flow = engine.StartFlow(plan);
            Await(entered.Task);
            Check(engine.ActiveFlowCount == 1, "running flow not registered");
            Task stopping = engine.StopAsync();
            Check(!stopping.IsCompleted, "drain stopped before flow completed");
            bool rejected = false;
            try { engine.StartFlow(plan); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "engine accepted flow during shutdown");
            proceed.TrySetResult(true);
            Check(Await(flow.Completion).State == MRKFlowState.Completed, "drain interrupted flow");
            Await(stopping);
            Check(engine.ActiveFlowCount == 0, "completed flow retained");

            TaskCompletionSource<bool> started = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            MRKExecutionPlan cancelPlan = new MRKExecutionPlan("cancel", new[]
            {
                new MRKExecutionStep("await", new string[0], async (ctx, token) =>
                {
                    started.TrySetResult(true);
                    await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
                    return (object)0;
                })
            });
            MRKExecutionEngine second = new MRKExecutionEngine(new FCRWorkerOptions(1, 2));
            MRKFlowHandle current = second.StartFlow(cancelPlan);
            Await(started.Task);
            Await(second.StopAsync(true));
            Check(Await(current.Completion).State == MRKFlowState.Cancelled,
                "stop with cancellation did not settle flow");
        }

        private static void ResourcesAreDeclarativeAndDoNotBlockWorker()
        {
            TaskCompletionSource<bool> entered = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> release = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            int secondStarted = 0;
            MRKExecutionPlan plan = new MRKExecutionPlan("resources", new[]
            {
                new MRKExecutionStep("first", new string[0], async (ctx, ct) =>
                {
                    entered.TrySetResult(true);
                    await release.Task.ConfigureAwait(false);
                    return (object)1;
                }, requiredResources: new[] { "device2", "device1" }),
                new MRKExecutionStep("second", new string[0], (ctx, ct) =>
                {
                    Interlocked.Increment(ref secondStarted);
                    return Task.FromResult((object)2);
                }, requiredResources: new[] { "device1", "device2" }),
                new MRKExecutionStep("unrelated", new string[0], (ctx, ct) =>
                    Task.FromResult((object)3))
            });
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(2, 4));
            MRKFlowHandle flow = engine.StartFlow(plan);
            Await(entered.Task);
            Check(Until(() => flow.Context.StatesSnapshot()["second"] ==
                MRKStepState.WaitingResources), "resource wait not observable");
            Check(secondStarted == 0 && engine.GetSnapshot().Active <= 2,
                "second device job ran while resource held");
            release.TrySetResult(true);
            MRKFlowResult result = Await(flow.Completion);
            Check(result.State == MRKFlowState.Completed &&
                secondStarted == 1 && (int)result.Results["unrelated"] == 3,
                "resource acquisition/release did not complete");
            Await(engine.StopAsync());
        }

        private static void TraceCursorDetectsLostTransitions()
        {
            MRKExecutionPlan plan = new MRKExecutionPlan("tracecursor", new[]
            {
                new MRKExecutionStep("A", new string[0],
                    (ctx,ct) => Task.FromResult((object)1)),
                new MRKExecutionStep("B", new []{"A"},
                    (ctx,ct) => Task.FromResult((object)2))
            });
            MRKExecutionEngine engine = new MRKExecutionEngine(
                new FCRWorkerOptions(2,4), traceCapacity:2);
            MRKFlowHandle flow = engine.StartFlow(plan);
            Check(Await(flow.Completion).State == MRKFlowState.Completed,
                "trace cursor baseline flow failed");
            MRKTraceBatch first = flow.Context.Trace.ReadSince(0);
            Check(first.Changes.Count == 2 && first.HasGap && first.LostCount > 0 &&
                first.Changes[1].Sequence == first.NextCursor,
                "trace ring buffer did not report lost records");
            MRKTraceBatch next = flow.Context.Trace.ReadSince(first.NextCursor);
            Check(!next.HasGap && next.Changes.Count == 0 &&
                next.NextCursor == first.NextCursor,
                "incremental cursor replayed old transitions");
            Await(engine.StopAsync());
        }

    }
}
