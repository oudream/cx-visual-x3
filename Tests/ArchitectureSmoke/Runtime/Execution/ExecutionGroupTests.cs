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

        private static void Await(Task task)
        {
            Task winner = Task.WhenAny(task, Task.Delay(6000)).GetAwaiter().GetResult();
            if (!object.ReferenceEquals(winner, task)) throw new TimeoutException("执行组等待超时");
            task.GetAwaiter().GetResult();
        }
    }
}
