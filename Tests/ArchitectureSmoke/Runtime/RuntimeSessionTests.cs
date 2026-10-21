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
            CancelEscalatesAnExistingDrain();
            RoleBasedSessionGatewayDeniesUnauthorizedActions();
        }
        private static void CancelEscalatesAnExistingDrain()
        {
            TaskCompletionSource<bool> entered = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            MRKExecutionPlan plan = new MRKExecutionPlan("upgrade", new[]{
                new MRKExecutionStep("await",new string[0],async(c,t)=>{
                    entered.TrySetResult(true);
                    await Task.Delay(Timeout.Infinite,t).ConfigureAwait(false);
                    return (object)0;
                })
            });
            MRKExecutionEngine engine=new MRKExecutionEngine(new FCRWorkerOptions(1,3));
            MRKRuntimeSession session=new MRKRuntimeSession("upgrade",engine,
                new MRKExecutionPlanSlot(plan,1));
            session.Start();
            MRKFlowHandle flow=session.TryStartFlow();
            Wait(entered.Task);
            Task graceful=session.StopAsync(false);
            if(graceful.IsCompleted)throw new InvalidOperationException("升级前Drain提前完成");
            Task upgraded=session.StopAsync(true);
            if(!object.ReferenceEquals(graceful,upgraded))
                throw new InvalidOperationException("重复停止返回了不同任务");
            Wait(upgraded);
            if(Wait(flow.Completion).State!=MRKFlowState.Cancelled ||
               session.Snapshot().State!=MRKRuntimeSessionState.Stopped)
                throw new InvalidOperationException("停止升级没有取消在途Flow");
            Wait(engine.StopAsync());
        }

        private sealed class RolePolicy : IMRKRuntimeAccessPolicy
        {
            public bool IsAuthorized(string principal,MRKRuntimeAccessAction action,string sessionId)
            {
                if(principal=="admin") return true;
                if(principal=="viewer") return action==MRKRuntimeAccessAction.Observe;
                if(principal=="operator")
                    return action==MRKRuntimeAccessAction.Observe ||
                        action==MRKRuntimeAccessAction.Start ||
                        action==MRKRuntimeAccessAction.Cancel ||
                        action==MRKRuntimeAccessAction.Stop;
                if(principal=="debugger")
                    return action==MRKRuntimeAccessAction.Observe ||
                        action==MRKRuntimeAccessAction.Debug;
                return false;
            }
        }
        private static void RoleBasedSessionGatewayDeniesUnauthorizedActions()
        {
            MRKExecutionPlan plan=new MRKExecutionPlan("roles",new[]{
                new MRKExecutionStep("run",new string[0],
                    (c,t)=>Task.FromResult((object)11))
            });
            MRKExecutionEngine engine=new MRKExecutionEngine(new FCRWorkerOptions(2,4));
            MRKRuntimeSession session=new MRKRuntimeSession("restricted",engine,
                new MRKExecutionPlanSlot(plan,7));
            session.Start();
            MRKRuntimeAccessGateway access=new MRKRuntimeAccessGateway(engine,session,
                new RolePolicy());
            MRKExecutionDebugController debug=new MRKExecutionDebugController();
            try
            {
                if(access.Observe("viewer").State!=MRKRuntimeSessionState.Ready)
                    throw new InvalidOperationException("只读观察不可用");
                bool denied=false;
                try {access.TryStart("viewer");}
                catch(UnauthorizedAccessException){denied=true;}
                if(!denied || session.Snapshot().ActiveFlows!=0)
                    throw new InvalidOperationException("只读主体启动了Flow");
                // 网关不能把调用者控制的Token保留为运行期取消捷径。
                foreach (System.Reflection.MethodInfo method in
                    typeof(MRKRuntimeAccessGateway).GetMethods())
                    if (method.Name=="TryStart" || method.Name=="TryStartDebug")
                        foreach (System.Reflection.ParameterInfo parameter in method.GetParameters())
                            if (parameter.ParameterType==typeof(CancellationToken))
                                throw new InvalidOperationException(
                                    "Start/Debug调用方可绕过Cancel权限保留取消令牌");
                MRKRuntimeFlowTicket ordinaryTicket=access.TryStart("operator");
                if (typeof(MRKRuntimeFlowTicket).GetMethod("RequestCancel") != null ||
                    typeof(MRKRuntimeFlowTicket).GetProperty("Context") != null)
                    throw new InvalidOperationException(
                        "仅有Start权限却通过票据获得了裸Handle或取消能力");
                MRKFlowResult ordinary=Wait(ordinaryTicket.Completion);
                if(ordinary.State!=MRKFlowState.Completed ||
                    ordinary.Metadata.PlanRevision!=7)
                    throw new InvalidOperationException("授权操作员执行失败");
                denied=false;
                try {access.TryStartDebug("operator",debug);}
                catch(UnauthorizedAccessException){denied=true;}
                if(!denied)throw new InvalidOperationException("操作员越权调试");
                debug.Pause();
                MRKRuntimeFlowTicket controlled=access.TryStartDebug("debugger",debug);
                if(!SpinWait.SpinUntil(()=>debug.WaitingCount>0,3000))
                    throw new InvalidOperationException("授权调试入口没有到达暂停点");
                denied=false;
                try {access.RequestCancel("viewer",controlled);}
                catch(UnauthorizedAccessException){denied=true;}
                if(!denied || controlled.GetSnapshot().CancelRequested)
                    throw new InvalidOperationException("只读主体取消了调试Flow");
                denied=false;
                try {access.RequestCancel("debugger",controlled);}
                catch(UnauthorizedAccessException){denied=true;}
                if(!denied || controlled.GetSnapshot().CancelRequested)
                    throw new InvalidOperationException("调试权限绕过了取消权限检查");
                debug.Resume();
                if(Wait(controlled.Completion).State!=MRKFlowState.Completed)
                    throw new InvalidOperationException("授权调试恢复失败");
                denied=false;
                try {Wait(access.StopSessionAsync("viewer"));}
                catch(UnauthorizedAccessException){denied=true;}
                if(!denied || session.Snapshot().State!=MRKRuntimeSessionState.Ready)
                    throw new InvalidOperationException("只读主体停止了会话");
                Wait(access.StopSessionAsync("operator"));
            }
            finally
            {
                debug.Resume();
                Wait(session.StopAsync(true));
                Wait(engine.StopAsync(true));
            }
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
