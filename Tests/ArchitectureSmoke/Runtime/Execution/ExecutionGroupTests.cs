using System;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Runtime.Execution;
namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class ExecutionGroupTests
    {
        internal static void Run()
        {
            QuotaAndBackpressureDoNotBlockThreads();
            UnknownGroupRejectedBeforeRunning();
            GroupAndDeviceResourcesHaveUniformOrder();
            R08_GroupCancellationReclaimsWaitingBudget();
            R08_WholeFlowSerializesAcrossFlowsAndCancelsQueue();
            R08_WholeFlowLoopUsesOneLeaseAndRejectsUnknownInnerGroups();
            R08_CancelledNonCooperativeWorkRetainsWholeFlowPermit();
        }
        private static void Check(bool condition, string msg)
        { if (!condition) throw new InvalidOperationException("执行组：" + msg); }
        private static T Await<T>(Task<T> task)
        {
            Task winner = Task.WhenAny(task, Task.Delay(6000)).GetAwaiter().GetResult();
            if (!object.ReferenceEquals(winner, task)) throw new TimeoutException("执行组等待超时");
            return task.GetAwaiter().GetResult();
        }
        private static void QuotaAndBackpressureDoNotBlockThreads()
        {
            MRKExecutionGroups groups = new MRKExecutionGroups(new[] {
                new MRKExecutionGroupDefinition("line", 1, 1),
                new MRKExecutionGroupDefinition("cpu", 2, 0) });
            MRKExecutionGroupLease one = Await(groups.AcquireAsync("line"));
            Task<MRKExecutionGroupLease> waiting = groups.AcquireAsync("line");
            Check(!waiting.IsCompleted, "并发上限失效");
            bool rejected = false;
            try { Await(groups.AcquireAsync("line")); }
            catch (MRKExecutionGroupRejectedException) { rejected = true; }
            Check(rejected && groups.Snapshot()[0].Waiting == 1 &&
                groups.Snapshot()[0].Rejected == 1, "拒绝/等待指标不正确");
            using (CancellationTokenSource cancel = new CancellationTokenSource())
            {
                Task<MRKExecutionGroupLease> noCapacity = groups.AcquireAsync("cpu", cancel.Token);
                using (MRKExecutionGroupLease acquired = Await(noCapacity))
                    Check(acquired != null, "其他组不应被阻塞");
            }
            one.Dispose();
            using (MRKExecutionGroupLease acquired = Await(waiting))
                Check(acquired != null, "队列无法恢复");
            Check(groups.Snapshot()[0].Active == 0 && groups.Snapshot()[0].Waiting == 0,
                "组许可泄漏");
        }
        private static void UnknownGroupRejectedBeforeRunning()
        {
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(1, 2));
            MRKExecutionPlan plan = new MRKExecutionPlan("unknown", new[]
            {
                new MRKExecutionStep("node", new string[0],
                    (ctx, token) => Task.FromResult((object)1), executionGroup: "unknown")
            });
            bool rejected = false;
            try { engine.StartFlow(plan); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "未注册的运行组不应开始流程");
            Await(engine.StopAsync());
        }
        private static void GroupAndDeviceResourcesHaveUniformOrder()
        {
            int active = 0, peak = 0;
            TaskCompletionSource<bool> entered = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> done = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            MRKExecutionPlan plan = new MRKExecutionPlan("groups", new[]
            {
                new MRKExecutionStep("a", new string[0], async (ctx, token) =>
                {
                    int current = Interlocked.Increment(ref active);
                    if (current > peak) peak = current;
                    entered.TrySetResult(true);
                    await done.Task.ConfigureAwait(false);
                    Interlocked.Decrement(ref active);
                    return (object)1;
                }, executionGroup: "g", requiredResources: new []{"device"}),
                new MRKExecutionStep("b", new string[0], (ctx, token) =>
                { Interlocked.Increment(ref active); Interlocked.Decrement(ref active);
                    return Task.FromResult((object)2);
                }, executionGroup: "g", requiredResources: new []{"device"})
            });
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(2, 3),
                groups: new[] { new MRKExecutionGroupDefinition("g", 1, 2) });
            MRKFlowHandle flow = engine.StartFlow(plan);
            Await(entered.Task);
            Check(SpinWait.SpinUntil(() => flow.Context.StatesSnapshot()["b"]
                == MRKStepState.WaitingGroup, 3000), "组等待状态不可见");
            done.TrySetResult(true);
            Check(Await(flow.Completion).State == MRKFlowState.Completed && peak == 1,
                "组配额未生效");
            Await(engine.StopAsync());
        }

        /// <summary>R08：取消的等待者立即归还组配额，不能夺走已有运行者的许可。</summary>
        private static void R08_GroupCancellationReclaimsWaitingBudget()
        {
            MRKExecutionGroups groups=new MRKExecutionGroups(new[]{
                new MRKExecutionGroupDefinition("device",1,1),
                new MRKExecutionGroupDefinition("cpu",1,0)
            });
            MRKExecutionGroupLease held=Await(groups.AcquireAsync("device"));
            try
            {
                for(int i=0;i<64;i++)
                {
                    using(CancellationTokenSource c=new CancellationTokenSource())
                    {
                        Task<MRKExecutionGroupLease> pending=groups.AcquireAsync("device",c.Token);
                        Check(!pending.IsCompleted&&groups.Snapshot()[0].Waiting==1,
                            "R08等待状态没有占用组配额");
                        bool rejected=false;
                        try{Await(groups.AcquireAsync("device"));}
                        catch(MRKExecutionGroupRejectedException){rejected=true;}
                        Check(rejected,"R08已满的等待队列未拒绝额外申请");
                        c.Cancel();
                        bool cancelled=false;
                        try{Await(pending);}
                        catch(OperationCanceledException){cancelled=true;}
                        Check(cancelled&&groups.Snapshot()[0].Waiting==0&&
                            groups.Snapshot()[0].Active==1,
                            "R08等待者取消未归还配额或影响当前持有者");
                    }
                }
                using(MRKExecutionGroupLease other=Await(groups.AcquireAsync("cpu")))
                    Check(groups.Snapshot()[1].Active==1,"R08另一执行组被错误阻塞");
            }
            finally{held.Dispose();}
            using(MRKExecutionGroupLease again=Await(groups.AcquireAsync("device")))
                Check(groups.Snapshot()[0].Active==1,"R08取消风暴后新申请没有进入");
            Check(groups.Snapshot()[0].Active==0&&groups.Snapshot()[0].Waiting==0&&
                groups.Snapshot()[0].Rejected==64,
                "R08组许可与拒绝次数未全部正确结算");
        }


        /// <summary>跨Flow整个组席位不在a/b节点之间释放；失败准入、取消等待均无业务副作用。</summary>
        private static void R08_WholeFlowSerializesAcrossFlowsAndCancelsQueue()
        {
            int startedSecond=0;
            TaskCompletionSource<bool> entered=new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> release=new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            MRKExecutionPlan plan=new MRKExecutionPlan("r08-whole-flow",new[]{
                new MRKExecutionStep("a",new string[0],async (ctx,ct)=>{
                    if((string)ctx.Inputs["instance"]=="first")
                    {
                        entered.TrySetResult(true);
                        await release.Task.ConfigureAwait(false);
                    }
                    else Interlocked.Increment(ref startedSecond);
                    return (object)1;
                },executionGroup:"whole"),
                new MRKExecutionStep("b",new[]{"a"},(ctx,ct)=>
                    Task.FromResult((object)2),executionGroup:"whole")
            });
            MRKExecutionEngine engine=new MRKExecutionEngine(new FCRWorkerOptions(1,6),
                groups:new[]{new MRKExecutionGroupDefinition("whole",1,1,
                    MRKExecutionGroupScope.WholeFlow)});
            MRKFlowHandle first=null;
            try
            {
                first=engine.StartFlow(plan,new System.Collections.Generic.Dictionary<string,object>{
                    {"instance","first"}});
                Await(entered.Task);
                Check(engine.GroupSnapshot()[0].Active==1,
                    "WholeFlow在第一个节点执行时应占一份组席位");
                MRKFlowHandle waiting=engine.StartFlow(plan,
                    new System.Collections.Generic.Dictionary<string,object>{{"instance","second"}});
                Check(engine.GroupSnapshot()[0].Waiting==1 &&
                    Volatile.Read(ref startedSecond)==0,
                    "WholeFlow另一Flow应完整排队，而非进入Step");
                MRKFlowHandle rejected=engine.StartFlow(plan,
                    new System.Collections.Generic.Dictionary<string,object>{{"instance","rejected"}});
                Check(Await(rejected.Completion).State==MRKFlowState.Failed &&
                    engine.GroupSnapshot()[0].Rejected==1 &&
                    Volatile.Read(ref startedSecond)==0,
                    "WholeFlow达到MaxWaiting时不能执行任何业务Step");
                Check(waiting.RequestCancel(),"WholeFlow等待流不可协作取消");
                Check(Await(waiting.Completion).State==MRKFlowState.Cancelled &&
                    engine.GroupSnapshot()[0].Waiting==0 &&
                    engine.GroupSnapshot()[0].Active==1,
                    "WholeFlow取消等待应释放Waiting，但不夺走现有Holder");
                release.TrySetResult(true);
                Check(Await(first.Completion).State==MRKFlowState.Completed,
                    "WholeFlow相同组跨两个依赖节点发生自死锁");
                MRKFlowHandle successor=engine.StartFlow(plan,
                    new System.Collections.Generic.Dictionary<string,object>{{"instance","next"}});
                Check(Await(successor.Completion).State==MRKFlowState.Completed &&
                    Volatile.Read(ref startedSecond)==1,
                    "WholeFlow成功结算后不能重新接纳下一Flow");
                Check(engine.GroupSnapshot()[0].Active==0 &&
                    engine.GroupSnapshot()[0].Waiting==0,
                    "WholeFlow成功、拒绝、取消后许可未完全释放");
            }
            finally
            {
                release.TrySetResult(true);
                Await(engine.StopAsync(true));
            }
        }

        /// <summary>循环体WholeFlow组必须只在父Flow入口获取一次；未知内部组提前拒绝。</summary>
        private static void R08_WholeFlowLoopUsesOneLeaseAndRejectsUnknownInnerGroups()
        {
            int iterations=0;
            MRKExecutionStep inner=new MRKExecutionStep("inner",new string[0],
                (ctx,ct)=>{
                    Interlocked.Increment(ref iterations);
                    return Task.FromResult((object)42);
                },executionGroup:"whole");
            MRKExecutionPlan body=new MRKExecutionPlan("r08-body",new[]{inner});
            MRKLoopRegion loop=new MRKLoopRegion(body,2);
            MRKExecutionStep outer=new MRKExecutionStep("loop",new string[0],
                (ctx,ct)=>Task.FromResult((object)0),loopRegion:loop);
            MRKExecutionPlan plan=new MRKExecutionPlan("r08-outer",new[]{outer});
            MRKExecutionEngine engine=new MRKExecutionEngine(new FCRWorkerOptions(1,4),
                groups:new[]{new MRKExecutionGroupDefinition("whole",1,0,
                    MRKExecutionGroupScope.WholeFlow)});
            try
            {
                Check(Await(engine.StartFlow(plan).Completion).State==MRKFlowState.Completed &&
                    iterations==2 && engine.GroupSnapshot()[0].Active==0,
                    "嵌套循环WholeFlow应只取得一次许可并完整释放（不得自死锁）");
                bool badEnum=false;
                try{new MRKExecutionGroupDefinition("bad",1,0,
                    (MRKExecutionGroupScope)123);}
                catch(ArgumentOutOfRangeException){badEnum=true;}
                Check(badEnum,"未知Stage等未实现组作用域不能降级为Step");

                int sideEffects=0;
                MRKExecutionPlan unknownBody=new MRKExecutionPlan("invalid-body",new[]{
                    new MRKExecutionStep("missing",new string[0],(ctx,ct)=>{
                        Interlocked.Increment(ref sideEffects);
                        return Task.FromResult((object)1);
                    },executionGroup:"missing")
                });
                MRKExecutionPlan invalid=new MRKExecutionPlan("invalid-outer",new[]{
                    new MRKExecutionStep("loop",new string[0],
                        (ctx,ct)=>Task.FromResult((object)0),
                        loopRegion:new MRKLoopRegion(unknownBody,1))
                });
                bool rejected=false;
                try{engine.StartFlow(invalid);}
                catch(ArgumentException){rejected=true;}
                Check(rejected && sideEffects==0,
                    "未知内部组必须在运行前整体拒绝，不得执行已有业务副作用");
            }
            finally{Await(engine.StopAsync(true));}
        }


        /// <summary>WholeFlow取消仅发送协作信号，非合作尾部未结算不可释放执行组。</summary>
        private static void R08_CancelledNonCooperativeWorkRetainsWholeFlowPermit()
        {
            TaskCompletionSource<bool> entered = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> externalFinished = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            int nextEntered=0;
            MRKExecutionPlan plan=new MRKExecutionPlan("r08-noncooperative",new[]{
                new MRKExecutionStep("external",new string[0],async (ctx,ct)=>{
                    if ((string)ctx.Inputs["instance"]=="first")
                    {
                        entered.TrySetResult(true);
                        // 故意忽略取消：仿真已发出的不可中断外部设备操作。
                        await externalFinished.Task.ConfigureAwait(false);
                    }
                    else Interlocked.Increment(ref nextEntered);
                    return (object)1;
                },executionGroup:"hardware")
            });
            MRKExecutionEngine engine=new MRKExecutionEngine(new FCRWorkerOptions(2,4),
                groups:new[]{new MRKExecutionGroupDefinition("hardware",1,1,
                    MRKExecutionGroupScope.WholeFlow)});
            try
            {
                MRKFlowHandle first=engine.StartFlow(plan,
                    new System.Collections.Generic.Dictionary<string,object>{
                        {"instance","first"}});
                Await(entered.Task);
                Check(first.RequestCancel(),"R08非合作外部Flow无法请求取消");
                MRKFlowHandle successor=engine.StartFlow(plan,
                    new System.Collections.Generic.Dictionary<string,object>{
                        {"instance","second"}});
                Check(engine.GroupSnapshot()[0].Active==1 &&
                    engine.GroupSnapshot()[0].Waiting==1 &&
                    !first.Completion.IsCompleted && Volatile.Read(ref nextEntered)==0,
                    "R08取消尚未结算的外部操作不应释放WholeFlow许可");
                externalFinished.TrySetResult(true);
                Check(Await(first.Completion).State==MRKFlowState.Cancelled,
                    "R08非合作外部操作迟到成功不可把已取消Flow判为成功");
                Check(Await(successor.Completion).State==MRKFlowState.Completed &&
                    Volatile.Read(ref nextEntered)==1,
                    "R08尾部结算释放后应允许后继Flow实际开始");
                Check(engine.GroupSnapshot()[0].Active==0 &&
                    engine.GroupSnapshot()[0].Waiting==0,
                    "R08非合作尾部结算后执行组许可泄漏");
            }
            finally
            {
                externalFinished.TrySetResult(true);
                Await(engine.StopAsync(true));
            }
        }

        private static void Await(Task task)
        {
            Task winner = Task.WhenAny(task, Task.Delay(6000)).GetAwaiter().GetResult();
            if (!object.ReferenceEquals(winner, task)) throw new TimeoutException("执行组等待超时");
            task.GetAwaiter().GetResult();
        }
    }
}
