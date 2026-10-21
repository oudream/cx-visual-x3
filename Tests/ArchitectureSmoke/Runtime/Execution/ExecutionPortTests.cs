using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Runtime.Execution;
namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class ExecutionPortTests
    {
        internal static void Run()
        {
            FlowInputsAreValidatedBeforeActions();
            ParallelStepsBindTypedInputsIndependently();
            InvalidLinksAndOutputTypeFail();
        }
        private static void Check(bool ok, string message)
        { if (!ok) throw new InvalidOperationException("端口契约：" + message); }
        private static T Await<T>(Task<T> task)
        {
            Task winner = Task.WhenAny(task, Task.Delay(7000)).GetAwaiter().GetResult();
            if (!object.ReferenceEquals(winner, task)) throw new TimeoutException("端口测试超时");
            return task.GetAwaiter().GetResult();
        }
        private static void FlowInputsAreValidatedBeforeActions()
        {
            int actions = 0;
            MRKExecutionStep step = new MRKExecutionStep("typed", new string[0],
                (ctx, ct) => { Interlocked.Increment(ref actions); return Task.FromResult((object)1); },
                inputs: new[] { new MRKExecutionInput("Image", MRKInputSource.FlowInput,
                    "Raw", typeof(byte[])) }, output: new MRKExecutionOutput(typeof(int)));
            MRKExecutionPlan plan = new MRKExecutionPlan("preflight", new[] { step });
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(1, 2));
            bool missing = false;
            try { engine.StartFlow(plan); }
            catch (MRKExecutionPortException) { missing = true; }
            Check(missing && actions == 0, "preflight allowed side effects before missing inputs");
            bool mismatch = false;
            try { engine.StartFlow(plan, new Dictionary<string, object> {{"Raw", "not-image"}}); }
            catch (MRKExecutionPortException) { mismatch = true; }
            Check(mismatch && actions == 0, "mismatched flow input was admitted");
            Await(engine.StartFlow(plan,
                new Dictionary<string, object> {{"Raw", new byte[3]}}).Completion);
            Check(actions == 1, "valid inputs not executed");
            Await(engine.StopAsync());
        }
        private static void ParallelStepsBindTypedInputsIndependently()
        {
            MRKExecutionPlan plan = new MRKExecutionPlan("typed-join", new[]
            {
                new MRKExecutionStep("source", new string[0], (c, ct) =>
                    Task.FromResult((object)40), output: new MRKExecutionOutput(typeof(int))),
                new MRKExecutionStep("left", new[] {"source"}, (c, ct) =>
                { int v; Check(c.TryGetBoundInput<int>("v", out v) && v == 40, "left bound result lost");
                    return Task.FromResult((object)(v + 1)); },
                    inputs: new[] { new MRKExecutionInput("v", MRKInputSource.StepResult,
                        "source", typeof(int)) }, output: new MRKExecutionOutput(typeof(int))),
                new MRKExecutionStep("right", new[] {"source"}, (c, ct) =>
                { int v; Check(c.TryGetBoundInput<int>("value", out v) && v == 40, "right binding mixed");
                    return Task.FromResult((object)(v + 2)); },
                    inputs: new[] { new MRKExecutionInput("value", MRKInputSource.StepResult,
                        "source", typeof(int)) }, output: new MRKExecutionOutput(typeof(int)))
            });
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(3, 3));
            MRKFlowResult result = Await(engine.StartFlow(plan).Completion);
            Check(result.State == MRKFlowState.Completed && (int)result.Results["left"] == 41 &&
                (int)result.Results["right"] == 42, "parallel bound data polluted");
            Await(engine.StopAsync());
        }
        private static void InvalidLinksAndOutputTypeFail()
        {
            MRKExecutionStep source = new MRKExecutionStep("a", new string[0],
                (c, ct) => Task.FromResult((object)"wrong"),
                output: new MRKExecutionOutput(typeof(int)));
            MRKExecutionStep downstream = new MRKExecutionStep("b", new []{"a"},
                (c, ct) => Task.FromResult((object)1),
                inputs: new [] {new MRKExecutionInput("port", MRKInputSource.StepResult,
                    "a", typeof(int))});
            MRKExecutionPlan valid = new MRKExecutionPlan("bad-result",new [] {source,downstream});
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(2, 2));
            MRKFlowResult result = Await(engine.StartFlow(valid).Completion);
            Check(result.State == MRKFlowState.Failed && !result.Results.ContainsKey("b"),
                "invalid output was passed downstream");
            bool invalid = false;
            try {
                new MRKExecutionPlan("broken-link",new [] {source,new MRKExecutionStep("c",
                    new string[0], (c,ct) => Task.FromResult((object)1),
                    inputs: new [] {new MRKExecutionInput("port",MRKInputSource.StepResult,
                        "a",typeof(int))})});
            } catch (ArgumentException) { invalid = true; }
            Check(invalid, "missing direct dependency not rejected");
            Await(engine.StopAsync());
        }
        private static void Await(Task task)
        {
            Task winner = Task.WhenAny(task, Task.Delay(7000)).GetAwaiter().GetResult();
            if (!object.ReferenceEquals(winner, task)) throw new TimeoutException("端口测试超时");
            task.GetAwaiter().GetResult();
        }
    }
}
