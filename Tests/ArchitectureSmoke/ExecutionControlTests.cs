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
        }
        private static void Check(bool ok, string msg)
        { if (!ok) throw new InvalidOperationException("运行控制测试：" + msg); }
        private static T Await<T>(Task<T> task)
        {
            Task winner = Task.WhenAny(task, Task.Delay(6000)).GetAwaiter().GetResult();
            if (!object.ReferenceEquals(winner, task)) throw new TimeoutException("控制测试等待超时");
            return task.GetAwaiter().GetResult();
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
            MRKExecutionTrace trace = new MRKExecutionTrace(2);
            trace.Record("A", MRKStepState.Running);
            trace.Record("B", MRKStepState.Completed);
            trace.Record("C", MRKStepState.Waiting);
            IList<MRKExecutionTransition> events = trace.Snapshot();
            Check(trace.Dropped == 1 && events.Count == 2 && events[0].StepId == "B" &&
                events[1].StepId == "C" && events[1].Sequence == 3, "trace ring buffer wrong");
        }
    }
}
