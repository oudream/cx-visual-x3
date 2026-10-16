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
            AuthorizedPublishingAndSafeRollback();
            NestedOrchestrationRunsAndSkipsCorrectly();
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

        private sealed class SimulatedPublicationPolicy : IMRKPlanPublicationPolicy
        {
            internal bool Safe;
            public bool Authorize(string principal, MRKPlanPublishAction action,
                MRKExecutionPlan plan, long revision)
            { return principal == "release-officer"; }
            public bool IsSafeToActivate(MRKExecutionPlan plan, long revision)
            { return Safe; }
        }
        private static void AuthorizedPublishingAndSafeRollback()
        {
            TaskCompletionSource<bool> started = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> release = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            MRKExecutionPlan initial = new MRKExecutionPlan("stable",new[]{
                new MRKExecutionStep("baseline",new string[0],async(c,t)=>{
                    started.TrySetResult(true);
                    await release.Task.ConfigureAwait(false);
                    return (object)1;
                })
            });
            MRKExecutionPlanSlot slot = new MRKExecutionPlanSlot(initial, 1, true);
            SimulatedPublicationPolicy policy = new SimulatedPublicationPolicy();
            MRKExecutionPlanPublisher publisher =
                new MRKExecutionPlanPublisher(slot, initial, 1, policy);
            Resolver resolver=new Resolver((c,t)=>Task.FromResult((object)2));
            MRKExecutionEngine engine=new MRKExecutionEngine(new FCRWorkerOptions(1,4));
            try
            {
                bool bypass=false;
                try { slot.StageValidated(initial,3); }
                catch (UnauthorizedAccessException) { bypass=true; }
                Check(bypass, "生产Slot允许绕过授权提交");
                bypass=false;
                try { slot.TryActivatePending(); }
                catch (UnauthorizedAccessException) { bypass=true; }
                Check(bypass, "生产Slot允许绕过安全点切换");
                MRKFlowHandle old=engine.StartFromSlot(slot);
                Wait(started.Task);
                bool denied=false;
                try { publisher.CompileAndStage("readonly","stable",
                    new[]{new MRKStepCompileRequest("new","Calculate")},
                    resolver,2); }
                catch (UnauthorizedAccessException) { denied=true; }
                Check(denied && !publisher.PendingRevision.HasValue,
                    "只读主体可发布或被拒绝的计划污染Slot");
                MRKPlanPublicationReceipt staged=publisher.CompileAndStage(
                    "release-officer","stable",
                    new[]{new MRKStepCompileRequest("new","Calculate")},
                    resolver,2);
                Check(staged.Accepted && publisher.PendingRevision==2,
                    "授权后不能阶段发布");
                policy.Safe=false;
                Check(!publisher.TryActivate("release-officer"),
                    "未到安全点切换了执行Plan");
                policy.Safe=true;
                Check(!publisher.TryActivate("release-officer") &&
                    slot.RunningFlows==1,"有在途Flow时意外切换");
                release.TrySetResult(true);
                Check(Wait(old.Completion).State==MRKFlowState.Completed &&
                    old.Context.Metadata.PlanRevision==1,
                    "原始Flow没有固定原版Plan");
                Check(SpinWait.SpinUntil(()=>slot.RunningFlows==0,3000),
                    "旧Plan引用未释放");
                Check(publisher.TryActivate("release-officer") &&
                    publisher.ActiveRevision==2,"授权激活失败");
                MRKFlowResult newer=Wait(engine.StartFromSlot(slot).Completion);
                Check(newer.State==MRKFlowState.Completed &&
                    (int)newer.Results["new"]==2 && newer.Metadata.PlanRevision==2,
                    "新Plan实际执行与发布版本不一致");
                Check(SpinWait.SpinUntil(()=>slot.RunningFlows==0,3000),
                    "新Plan引用未释放");
                publisher.StageRollback("release-officer",3);
                Check(publisher.TryActivate("release-officer") &&
                    publisher.ActiveRevision==3,"回滚没有产生新的单调版本");
                MRKFlowResult restored=Wait(engine.StartFromSlot(slot).Completion);
                Check(restored.State==MRKFlowState.Completed &&
                    (int)restored.Results["baseline"]==1 &&
                    restored.Metadata.PlanRevision==3,
                    "回滚未恢复原始Plan委托");
            }
            finally
            {
                release.TrySetResult(true);
                Wait(engine.StopAsync(true));
            }
        }


        private sealed class FlowCatalog : IMRKFlowCompileCatalog
        {
            private readonly Dictionary<string, MRKFlowCompileDefinition> _definitions =
                new Dictionary<string, MRKFlowCompileDefinition>(StringComparer.Ordinal);
            internal void Add(string id, params MRKStepCompileRequest[] steps)
            { _definitions.Add(id,new MRKFlowCompileDefinition(id,steps)); }
            public bool TryGet(string id, out MRKFlowCompileDefinition value)
            { return _definitions.TryGetValue(id,out value); }
        }

        private static void NestedOrchestrationRunsAndSkipsCorrectly()
        {
            FlowCatalog definitions=new FlowCatalog();
            definitions.Add("leaf",
                new MRKStepCompileRequest("compute","Calculate"));
            definitions.Add("middle",
                new MRKStepCompileRequest("inner",null,subflowId:"leaf"),
                new MRKStepCompileRequest("afterInner","Calculate",new[]{"inner"}));
            definitions.Add("root",
                new MRKStepCompileRequest("outer",null,
                    condition:ctx=>(bool)ctx.Inputs["enabled"],subflowId:"middle"),
                new MRKStepCompileRequest("finish","Calculate",new[]{"outer"}));
            Resolver capabilities=new Resolver((ctx,ct)=>Task.FromResult((object)42));
            MRKExecutionOrchestrationCompiler compiler=new MRKExecutionOrchestrationCompiler();
            MRKCompileResult compiled=compiler.Compile("root",definitions,capabilities);
            Check(compiled.Succeeded && compiled.Plan.Steps.Count>=7,
                "多层子流程入口/出口没有转换为运行计划");
            MRKExecutionEngine engine=new MRKExecutionEngine(new FCRWorkerOptions(2,16));
            try
            {
                MRKFlowResult good=Wait(engine.StartFlow(compiled.Plan,
                    new Dictionary<string,object>{{"enabled",true}}).Completion);
                Check(good.State==MRKFlowState.Completed &&
                    (int)good.Results["outer.inner.compute"]==42 &&
                    (int)good.Results["outer.afterInner"]==42 &&
                    (int)good.Results["finish"]==42,
                    "多级子流程结果路径、依赖出口或能力调用错误");
                MRKFlowResult skipped=Wait(engine.StartFlow(compiled.Plan,
                    new Dictionary<string,object>{{"enabled",false}}).Completion);
                Check(skipped.State==MRKFlowState.Completed &&
                    skipped.StepStates["outer"]==MRKStepState.Skipped &&
                    skipped.StepStates["outer.inner.compute"]==MRKStepState.Skipped &&
                    skipped.StepStates["finish"]==MRKStepState.Skipped,
                    "子流程条件false未阻断内部副作用与后继");
                FlowCatalog cyclic=new FlowCatalog();
                cyclic.Add("self",new MRKStepCompileRequest("again",null,subflowId:"self"));
                MRKCompileResult bad=compiler.Compile("self",cyclic,capabilities);
                Check(!bad.Succeeded && bad.Plan==null &&
                    bad.Diagnostics.Any(x=>x.Code=="INVALID_ORCHESTRATION"),
                    "子流程循环引用未在编译期拒绝");
            }
            finally { Wait(engine.StopAsync()); }
        }

        private static void Wait(Task task)
        {
            Task winner = Task.WhenAny(task, Task.Delay(6000)).GetAwaiter().GetResult();
            if (!object.ReferenceEquals(winner, task)) throw new TimeoutException("计划编译测试超时");
            task.GetAwaiter().GetResult();
        }
    }
}
