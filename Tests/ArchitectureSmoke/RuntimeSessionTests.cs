using System;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Runtime.Execution;
namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class RuntimeSessionTests
    {
        internal static void Run()
        {
            TaskCompletionSource<bool> ready=new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> release=new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            MRKExecutionPlan initial=new MRKExecutionPlan("initial",new[]{
                new MRKExecutionStep("x",new string[0],async(c,t)=>
                {ready.TrySetResult(true);await release.Task.ConfigureAwait(false);return (object)1;})});
            MRKExecutionPlan newer=new MRKExecutionPlan("next",new[]{
                new MRKExecutionStep("x",new string[0],(c,t)=>Task.FromResult((object)2))});
            MRKExecutionEngine engine=new MRKExecutionEngine(new FCRWorkerOptions(2,3));
            MRKExecutionPlanSlot slot=new MRKExecutionPlanSlot(initial,5);
            MRKRuntimeSession one=new MRKRuntimeSession("one",engine,slot);
            MRKRuntimeSession two=new MRKRuntimeSession("two",engine,new MRKExecutionPlanSlot(newer,7));
            one.Start();two.Start();
            MRKFlowHandle flow=one.TryStartFlow(businessKey:"P001");
            Wait(ready.Task);
            if(flow==null || one.TryStartFlow()!=null || one.Snapshot().Rejected!=1)
                throw new InvalidOperationException("会话容量失效");
            slot.StageValidated(newer,6);
            if(one.TryActivatePendingPlan())throw new InvalidOperationException("计划正在运行却切换成功");
            MRKFlowResult other=Wait(two.TryStartFlow().Completion);
            if(other.Metadata.SessionId!="two" || other.Metadata.PlanRevision!=7)
                throw new InvalidOperationException("共享引擎会话身份冲突");
            Task drain=one.StopAsync();
            if(drain.IsCompleted || one.TryStartFlow()!=null)
                throw new InvalidOperationException("停止未排空或仍接单");
            release.TrySetResult(true);
            MRKFlowResult result=Wait(flow.Completion);
            Wait(drain);
            if(result.Metadata.BusinessKey!="P001" || result.Metadata.PlanRevision!=5 ||
                one.Snapshot().State!=MRKRuntimeSessionState.Stopped ||
                two.Snapshot().State!=MRKRuntimeSessionState.Ready)
                throw new InvalidOperationException("会话状态与计划版本不一致");
            Wait(two.StopAsync());Wait(engine.StopAsync());
        }
        private static T Wait<T>(Task<T> task)
        {
            Task first=Task.WhenAny(task,Task.Delay(6000)).GetAwaiter().GetResult();
            if(!object.ReferenceEquals(first,task))throw new TimeoutException("会话测试超时");
            return task.GetAwaiter().GetResult();
        }
        private static void Wait(Task task)
        {
            Task first=Task.WhenAny(task,Task.Delay(6000)).GetAwaiter().GetResult();
            if(!object.ReferenceEquals(first,task))throw new TimeoutException("会话测试超时");
            task.GetAwaiter().GetResult();
        }
    }
}
