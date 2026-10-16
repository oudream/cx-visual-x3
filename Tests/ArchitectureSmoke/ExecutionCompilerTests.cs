using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Runtime.Execution;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class ExecutionCompilerTests
    {
        private sealed class Resolver : IMRKCapabilityResolver
        {
            private readonly Func<MRKExecutionContext, CancellationToken, Task<object>> _function;
            internal int Resolved;
            internal Resolver(Func<MRKExecutionContext, CancellationToken, Task<object>> function)
            { _function = function; }
            public bool TryResolve(string key, out MRKResolvedCapability result)
            {
                if (key == "Calculate")
                {
                    Resolved++;
                    result = new MRKResolvedCapability(_function,
                        new MRKExecutionOutput(typeof(int)));
                    return true;
                }
                result = null;
                return false;
            }
        }
        internal static void Run()
        {
            CompilesToSameCapabilityAndStableOrder();
            AggregatesBrokenNodeDiagnostics();
            RejectsGraphCycleBeforeAnyExecution();
        }
        private static void Check(bool ok, string msg)
        { if (!ok) throw new InvalidOperationException("计划编译：" + msg); }
        private static T Wait<T>(Task<T> task)
        {
            Task winner = Task.WhenAny(task, Task.Delay(6000)).GetAwaiter().GetResult();
            if (!object.ReferenceEquals(winner, task)) throw new TimeoutException("计划编译测试超时");
            return task.GetAwaiter().GetResult();
        }
        private static void CompilesToSameCapabilityAndStableOrder()
        {
            int count = 0;
            Func<MRKExecutionContext, CancellationToken, Task<object>> function =
                (c, ct) => { Interlocked.Increment(ref count); return Task.FromResult((object)42); };
            Resolver resolver = new Resolver(function);
            MRKStepCompileRequest first = new MRKStepCompileRequest("a", "Calculate");
            MRKStepCompileRequest second = new MRKStepCompileRequest("b", "Calculate", new[]{"a"},
                inputs: new[]{new MRKExecutionInput("origin", MRKInputSource.StepResult,
                    "a", typeof(int))});
            MRKExecutionPlanCompiler compiler = new MRKExecutionPlanCompiler();
            MRKCompileResult compiled = compiler.Compile("test", new[]{second,first}, resolver);
            Check(compiled.Succeeded && compiled.Plan.Steps[0].Id == "a" &&
                compiled.Plan.Steps[1].Id == "b" && resolver.Resolved == 2, "unreliable plan resolution");
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(2,4));
            MRKFlowResult result = Wait(engine.StartFlow(compiled.Plan).Completion);
            Check(result.State == MRKFlowState.Completed && count == 2 &&
                (int)result.Results["b"] == 42, "compiled plan failed to use same delegate");
            Wait(engine.StopAsync());
        }
        private static void AggregatesBrokenNodeDiagnostics()
        {
            MRKExecutionPlanCompiler compiler = new MRKExecutionPlanCompiler();
            Resolver resolver = new Resolver((c,ct) => Task.FromResult((object)1));
            MRKCompileResult failed = compiler.Compile("bad", new[]{
                new MRKStepCompileRequest("first", "missing", new[]{"ghost"}),
                new MRKStepCompileRequest("first", "Calculate"),
                new MRKStepCompileRequest("last", "another")
            }, resolver);
            Check(!failed.Succeeded && failed.Plan == null &&
                failed.Diagnostics.Any(x => x.Code == "DUPLICATE_STEP") &&
                failed.Diagnostics.Any(x => x.Code == "UNKNOWN_DEPENDENCY") &&
                failed.Diagnostics.Count(x => x.Code == "UNKNOWN_CAPABILITY") == 2,
                "broken plan did not aggregate errors");
        }
        private static void RejectsGraphCycleBeforeAnyExecution()
        {
            int ran = 0;
            Resolver resolver = new Resolver((c,ct) =>
            { Interlocked.Increment(ref ran); return Task.FromResult((object)1); });
            MRKCompileResult result = new MRKExecutionPlanCompiler().Compile("cycle",new[]{
                new MRKStepCompileRequest("a", "Calculate",new[]{"b"}),
                new MRKStepCompileRequest("b", "Calculate",new[]{"a"})
            }, resolver);
            Check(!result.Succeeded && result.Diagnostics.Any(x=>x.Code=="INVALID_GRAPH") &&
                ran==0, "cyclic graph was compiled or executed");
        }
        private static void Wait(Task task)
        {
            Task winner = Task.WhenAny(task, Task.Delay(6000)).GetAwaiter().GetResult();
            if (!object.ReferenceEquals(winner, task)) throw new TimeoutException("计划编译测试超时");
            task.GetAwaiter().GetResult();
        }
    }
}
