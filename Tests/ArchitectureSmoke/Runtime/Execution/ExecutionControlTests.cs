using System;
using System.Collections.Generic;
using System.Linq;
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
            ResultInspectionNeverCopiesRawBuffersOrLeaksText();
            ResultInspectionIsBoundedForLongFlows();
            ResultSummariesSeparateFailedAndSuccessfulProducts();
            QualifiedLoopIterationBreakpoint();
            CancellationUnblocksPausedFlow();
            CancelledBreakpointsAreRemovedImmediately();
            GlobalFlowQuotaRejectsBeforeAdmission();
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
        private static void ResultInspectionNeverCopiesRawBuffersOrLeaksText()
        {
            var plan = new MRKExecutionPlan("b5.result-inspection", new[]
            {
                new MRKExecutionStep("raw", new string[0],
                    (ctx,ct) => Task.FromResult((object)new byte[] { 1,2,3,4 })),
                new MRKExecutionStep("secret", new[] { "raw" },
                    (ctx,ct) => Task.FromResult((object)"PRIVATE-SDK-TOKEN"))
            });
            var engine = new MRKExecutionEngine(new FCRWorkerOptions(1,4));
            try
            {
                MRKFlowHandle flow = engine.StartFlow(plan);
                MRKFlowResult result = Await(flow.Completion);
                var view = flow.Context.ResultSummariesSnapshot();
                Check(result.State == MRKFlowState.Completed &&
                    view.ContainsKey("raw") && view["raw"].Contains("length=4") &&
                    view.ContainsKey("secret") && view["secret"].Contains("length=17") &&
                    !view.Values.Any(x => x.Contains("PRIVATE-SDK-TOKEN")),
                    "B5调试摘要泄漏原始敏感文本或未显示真实数组长度");
                bool immutable = false;
                try { view.Add("hijack", "value"); }
                catch (NotSupportedException) { immutable = true; }
                Check(immutable, "B5调试结果快照可被外部写入");
                bool bounds = false;
                try { flow.Context.ResultSummariesSnapshot(513); }
                catch (ArgumentOutOfRangeException) { bounds = true; }
                Check(bounds, "B5调试结果数量没有硬上限");
            }
            finally { Await(engine.StopAsync(true)); }
        }

        private static void ResultInspectionIsBoundedForLongFlows()
        {
            var steps = new List<MRKExecutionStep>();
            for (int i = 0; i < 72; i++)
            {
                string stepId = "k" + i.ToString("D3",
                    System.Globalization.CultureInfo.InvariantCulture);
                string previous = "k" + (i - 1).ToString("D3",
                    System.Globalization.CultureInfo.InvariantCulture);
                int value = i;
                steps.Add(new MRKExecutionStep(stepId,
                    i == 0 ? new string[0] : new[] { previous },
                    (ctx,ct) => Task.FromResult((object)value)));
            }
            var engine = new MRKExecutionEngine(new FCRWorkerOptions(2,16));
            try
            {
                MRKFlowHandle flow = engine.StartFlow(
                    new MRKExecutionPlan("b5.summary.bound",steps));
                Check(Await(flow.Completion).State == MRKFlowState.Completed,
                    "B5大量结果的正式MRK链未能完成");
                IDictionary<string,string> view=flow.Context.ResultSummariesSnapshot(32);
                Check(view.Count==32&&view.ContainsKey("k000")&&
                    view.ContainsKey("k031")&&!view.ContainsKey("k032")&&
                    view.Keys.SequenceEqual(view.Keys.OrderBy(x=>x,StringComparer.Ordinal)),
                    "B5大流程摘要数量或排序不确定");
            }
            finally { Await(engine.StopAsync(true)); }
        }

        private static void ResultSummariesSeparateFailedAndSuccessfulProducts()
        {
            // B4/B5 negative proof: failures must not merge another product's
            // committed data into the debug view, even on a shared MRK engine.
            var plan=new MRKExecutionPlan("b4.multi-product-summary",new[]
            {
                new MRKExecutionStep("measure",new string[0],(ctx,ct)=>
                    Task.FromResult((object)(int)ctx.Inputs["ProductId"])),
                new MRKExecutionStep("judge",new[]{"measure"},(ctx,ct)=>
                {
                    int product=(int)ctx.Inputs["ProductId"];
                    if(product==101)
                        throw new InvalidOperationException("product-101 inspection failed");
                    return Task.FromResult((object)(product+1));
                })
            });
            var engine=new MRKExecutionEngine(new FCRWorkerOptions(2,8));
            try
            {
                var failed=engine.StartFlow(plan,
                    new Dictionary<string,object>{{"ProductId",101}});
                var passed=engine.StartFlow(plan,
                    new Dictionary<string,object>{{"ProductId",202}});
                MRKFlowResult first=Await(failed.Completion);
                MRKFlowResult second=Await(passed.Completion);
                IDictionary<string,string> failView=failed.Context.ResultSummariesSnapshot(8);
                IDictionary<string,string> okView=passed.Context.ResultSummariesSnapshot(8);
                Check(first.State==MRKFlowState.Failed&&
                    second.State==MRKFlowState.Completed&&failed.FlowId!=passed.FlowId&&
                    failView.Count==1&&failView["measure"]=="Int32 value=101"&&
                    !failView.ContainsKey("judge")&&
                    okView.Count==2&&okView["measure"]=="Int32 value=202"&&
                    okView["judge"]=="Int32 value=203",
                    "B4故障产品与正常产品的真实MRK结果摘要串流或发布了失败节点");
            }
            finally { Await(engine.StopAsync(true)); }
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

        /// <summary>
        /// R09：相同逻辑Step每轮共享定义，但调试者可精确选择某一次Invocation。
        /// 不修改原有AddBreakpoint("work")匹配所有迭代的兼容语义。
        /// </summary>
        private static void QualifiedLoopIterationBreakpoint()
        {
            int executed=0;
            var body=new MRKExecutionPlan("r09.breakpoint.body",new[]{
                new MRKExecutionStep("work",new string[0],(ctx,ct)=>{
                    Interlocked.Increment(ref executed);
                    return Task.FromResult((object)ctx.IterationIndex);
                })
            });
            var plan=new MRKExecutionPlan("r09.breakpoint",new[]{
                new MRKExecutionStep("loop",new string[0],(ctx,ct)=>
                    Task.FromResult((object)true),
                    loopRegion:new MRKLoopRegion(body,4,collectionInput:"Items"))
            });
            var runtime=new MRKExecutionEngine(new FCRWorkerOptions(2,6));
            var debug=new MRKExecutionDebugController();
            debug.AddBreakpoint("loop.i2.work");
            try
            {
                MRKFlowHandle flow=runtime.StartFlowWithControl(plan,debug,
                    new Dictionary<string,object>{{"Items",new[]{1,2,3,4}}});
                Check(Until(()=>debug.WaitingCount==1),
                    "指定第三轮的限定路径断点没有暂停真实执行");
                MRKStepState paused;
                Check(Volatile.Read(ref executed)==2&&
                    flow.Context.StatesSnapshot().TryGetValue("loop.i2.work",out paused)&&
                    paused==MRKStepState.Paused,
                    "限定路径断点提前暂停前两轮，或未在第三轮Step执行前停止");
                Check(debug.StepOnce(),
                    "限定路径断点单步执行未解除等待节点");
                Check(Until(()=>Volatile.Read(ref executed)==3&&debug.WaitingCount==1),
                    "单步后未精确执行第三轮，或下一轮未保持暂停状态");
                debug.Resume();
                MRKFlowResult done=Await(flow.Completion);
                Check(done.State==MRKFlowState.Completed&&
                    Volatile.Read(ref executed)==4,
                    "断点继续后未完整执行全批次");
                var all=new MRKExecutionDebugController();
                all.AddBreakpoint("work");
                MRKFlowHandle legacy=runtime.StartFlowWithControl(plan,all,
                    new Dictionary<string,object>{{"Items",new[]{1,2,3,4}}});
                Check(Until(()=>all.WaitingCount==1)&&Volatile.Read(ref executed)==4,
                    "历史StepId断点未兼容第一轮动态循环");
                all.RemoveBreakpoint("work");
                all.Resume();
                Check(Await(legacy.Completion).State==MRKFlowState.Completed&&
                    Volatile.Read(ref executed)==8,
                    "旧式StepId断点恢复后未正常执行");
            }
            finally
            {
                debug.Resume();
                Await(runtime.StopAsync(true));
            }
        }

        private static void CancelledBreakpointsAreRemovedImmediately()
        {
            MRKExecutionDebugController debug = new MRKExecutionDebugController();
            debug.Pause();
            List<MRKExecutionStep> steps=new List<MRKExecutionStep>();
            for(int i=0;i<120;i++)
            {
                string name="paused-"+i;
                steps.Add(new MRKExecutionStep(name,new string[0],
                    (ctx,ct)=>Task.FromResult((object)1)));
            }
            MRKExecutionEngine engine=new MRKExecutionEngine(new FCRWorkerOptions(2,8));
            MRKFlowHandle flow=engine.StartFlowWithControl(
                new MRKExecutionPlan("cancel-paused-large",steps),debug);
            Check(Until(()=>debug.WaitingCount==120),
                "并发等待断点数量丢失");
            flow.RequestCancel();
            Check(Await(flow.Completion).State==MRKFlowState.Cancelled &&
                debug.WaitingCount==0,
                "取消后失效的断点等待者仍滞留在调试队列");
            Await(engine.StopAsync());
        }

        private static void GlobalFlowQuotaRejectsBeforeAdmission()
        {
            TaskCompletionSource<bool> entered=new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> release=new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            int calls=0;
            MRKExecutionPlan plan=new MRKExecutionPlan("quota",new[]{
                new MRKExecutionStep("work",new string[0],async(c,t)=>{
                    Interlocked.Increment(ref calls);
                    entered.TrySetResult(true);
                    await release.Task.ConfigureAwait(false);
                    return (object)1;
                })
            });
            MRKExecutionEngine engine=new MRKExecutionEngine(
                new FCRWorkerOptions(2,4),maxActiveFlows:1);
            try
            {
                MRKFlowHandle first=engine.StartFlow(plan);
                Await(entered.Task);
                bool refused=false;
                try { engine.StartFlow(plan); }
                catch(MRKFlowCapacityExceededException ex) { refused=ex.Limit==1; }
                Check(refused && engine.ActiveFlowCount==1 && calls==1,
                    "超额Flow未在产生外部副作用前拒绝");
                release.TrySetResult(true);
                Check(Await(first.Completion).State==MRKFlowState.Completed,
                    "排空原Flow失败");
                Check(Until(()=>engine.ActiveFlowCount==0),"终态Flow未归还全局准入额度");
                MRKFlowResult next=Await(engine.StartFlow(plan).Completion);
                Check(next.State==MRKFlowState.Completed && calls==2,
                    "全局Flow额度未在真实结算后重新开放");
            }
            finally
            {
                release.TrySetResult(true);
                Await(engine.StopAsync(true));
            }
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
            // Debug history is lossy by contract, but committed MDS/MRK results
            // remain authoritative and can be inspected without reconstructing trace.
            IDictionary<string,string> safe=flow.Context.ResultSummariesSnapshot(8);
            Check(safe.Count==2&&safe["A"]=="Int32 value=1"&&
                safe["B"]=="Int32 value=2",
                "B5追踪环形缓冲丢失后误删真实结果或从缺失事件重建错误数据");
            Await(engine.StopAsync());
        }

    }
}
