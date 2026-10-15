using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Runtime.Execution;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class ExecutionFlowTests
    {
        internal static void Run()
        {
            ParallelJoinAndResultContext();
            InvalidGraphFailsBeforeExecution();
            FailedStepDoesNotAdvanceDependent();
            CancellationIsCooperative();
            DirectSdkAndFlowShareTheWorker();
        }
        private static void Check(bool ok, string message)
        { if (!ok) throw new InvalidOperationException("Runtime Execution: " + message); }
        private static T Await<T>(Task<T> task)
        {
            if (!task.Wait(TimeSpan.FromSeconds(8))) throw new TimeoutException("Execution test hang");
            return task.GetAwaiter().GetResult();
        }
        private static void Await(Task task)
        {
            if (!task.Wait(TimeSpan.FromSeconds(8))) throw new TimeoutException("Execution test hang");
            task.GetAwaiter().GetResult();
        }
        private static TaskCompletionSource<bool> Gate()
        { return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); }
        private static MRKExecutionStep Step(string id, string[] deps,
            Func<MRKExecutionContext, CancellationToken, Task<object>> task)
        { return new MRKExecutionStep(id, deps, task); }
        private static void ParallelJoinAndResultContext()
        {
            IMRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(3, 8));
            TaskCompletionSource<bool> aiGate = Gate(), cpuDone = Gate();
            int joined = 0;
            MRKExecutionPlan plan = new MRKExecutionPlan("Vision-Plan-V1", new[]
            {
                Step("capture", new string[0], (c, ct) => Task.FromResult((object)"image-A")),
                Step("cpu", new[] { "capture" }, (c, ct) =>
                { cpuDone.TrySetResult(true); return Task.FromResult((object)7); }),
                Step("ai", new[] { "capture" }, async (c, ct) =>
                { await aiGate.Task.ConfigureAwait(false); return (object)11; }),
                Step("join", new[] { "cpu", "ai" }, (c, ct) =>
                {
                    Interlocked.Increment(ref joined);
                    object x = null, y = null;
                    Check(c.TryGetResult("cpu", out x) && c.TryGetResult("ai", out y),
                        "join observed missing branch data");
                    return Task.FromResult((object)((int)x + (int)y));
                })
            });
            MRKFlowHandle flow = engine.StartFlow(plan, new Dictionary<string, object> { { "LineId", "Line1" } });
            Await(cpuDone.Task);
            Check(joined == 0 && !flow.Completion.IsCompleted, "join ran ahead of AI dependency");
            Check(flow.Context.Inputs["LineId"].Equals("Line1"), "input lost");
            aiGate.TrySetResult(true);
            MRKFlowResult result = Await(flow.Completion);
            Check(result.State == MRKFlowState.Completed && (int)result.Results["join"] == 18,
                "DAG join incorrect");
            Check(result.StepStates["ai"] == MRKStepState.Completed && joined == 1, "step statuses incorrect");
            Await(engine.StopAsync());
        }
        private static void InvalidGraphFailsBeforeExecution()
        {
            bool cycle = false, missing = false;
            try { new MRKExecutionPlan("cycle", new[]
                { Step("a", new[] { "b" }, (c, ct) => Task.FromResult((object)1)),
                  Step("b", new[] { "a" }, (c, ct) => Task.FromResult((object)1)) }); }
            catch (ArgumentException) { cycle = true; }
            try { new MRKExecutionPlan("missing", new[]
                { Step("a", new[] { "unknown" }, (c, ct) => Task.FromResult((object)1)) }); }
            catch (ArgumentException) { missing = true; }
            Check(cycle && missing, "cycle or dangling dependency not rejected");
        }
        private static void FailedStepDoesNotAdvanceDependent()
        {
            IMRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(2, 3));
            int visited = 0;
            MRKExecutionPlan plan = new MRKExecutionPlan("failure", new[]
            {
                Step("bad", new string[0], (c, ct) =>
                { throw new InvalidOperationException("provider failed"); }),
                Step("next", new[] { "bad" }, (c, ct) =>
                { Interlocked.Increment(ref visited); return Task.FromResult((object)1); })
            });
            MRKFlowResult result = Await(engine.StartFlow(plan).Completion);
            Check(result.State == MRKFlowState.Failed && visited == 0
                && result.StepStates["next"] == MRKStepState.Failed,
                "dependent work advanced after failed dependency");
            Await(engine.StopAsync());
        }
        private static void CancellationIsCooperative()
        {
            IMRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(1, 2));
            TaskCompletionSource<bool> started = Gate();
            MRKExecutionPlan plan = new MRKExecutionPlan("cancel", new[]
            {
                Step("wait", new string[0], async (c, ct) =>
                {
                    started.TrySetResult(true);
                    await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                    return (object)1;
                })
            });
            MRKFlowHandle flow = engine.StartFlow(plan);
            Await(started.Task);
            Check(flow.RequestCancel(), "cancel request not accepted");
            MRKFlowResult result = Await(flow.Completion);
            Check(result.State == MRKFlowState.Cancelled
                && result.StepStates["wait"] == MRKStepState.Cancelled,
                "cooperative cancellation not tracked");
            Await(engine.StopAsync());
        }
        private static void DirectSdkAndFlowShareTheWorker()
        {
            IMRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(1, 2));
            FCRWorkHandle<int> direct = engine.Submit<int>(ct => Task.FromResult(5), "shared");
            MRKExecutionPlan plan = new MRKExecutionPlan("single", new[]
            {
                new MRKExecutionStep("step", new string[0], (c, ct) =>
                    Task.FromResult((object)6), "shared"),
                new MRKExecutionStep("null-value", new string[0], (c, ct) =>
                    Task.FromResult((object)null), "other")
            });
            MRKFlowHandle flow = engine.StartFlow(plan);
            MRKFlowResult result = Await(flow.Completion);
            object nullValue = new object();
            Check(Await(direct.Completion).Value == 5
                && (int)result.Results["step"] == 6 && result.Results["null-value"] == null
                && flow.Context.TryGetResult("null-value", out nullValue) && nullValue == null,
                "SDK, flow or null result semantics incorrect");
            Await(engine.StopAsync());
        }
    }
}
