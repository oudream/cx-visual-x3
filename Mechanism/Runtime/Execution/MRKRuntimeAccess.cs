using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Mechanism.Runtime.Execution
{
    public enum MRKRuntimeAccessAction
    {
        Observe, Start, Debug, Cancel, Stop, Compensate
    }

    /// <summary>权限决策从Host注入。任何未授权返回均拒绝，不提供默认放行实现。</summary>
    public interface IMRKRuntimeAccessPolicy
    {
        bool IsAuthorized(string principal, MRKRuntimeAccessAction action, string sessionId);
    }

    /// <summary>
    /// 不可取消的运行票据：仅公开观察与最终完成结果，保留真实Handle给受控网关。
    /// 启动权限不自动赋予取消/补偿权限，不能将原始MRKFlowHandle交给外部调用方。
    /// </summary>
    public sealed class MRKRuntimeFlowTicket
    {
        internal MRKRuntimeFlowTicket(MRKFlowHandle handle)
        { Handle = handle ?? throw new ArgumentNullException("handle"); }
        internal MRKFlowHandle Handle { get; private set; }
        public Guid FlowId { get { return Handle.FlowId; } }
        public Task<MRKFlowResult> Completion { get { return Handle.Completion; } }
        public MRKFlowSnapshot GetSnapshot() { return Handle.GetSnapshot(); }
    }

    /// <summary>
    /// RuntimeSession的受控对外入口；只读操作不持有执行/调试权。
    /// Host不得向不受信任的UI/网络直接暴露底层Engine及Session引用。
    /// </summary>
    public sealed class MRKRuntimeAccessGateway
    {
        private readonly MRKExecutionEngine _engine;
        private readonly MRKRuntimeSession _session;
        private readonly IMRKRuntimeAccessPolicy _policy;
        public MRKRuntimeAccessGateway(MRKExecutionEngine engine,
            MRKRuntimeSession session, IMRKRuntimeAccessPolicy policy)
        {
            if (engine == null) throw new ArgumentNullException("engine");
            if (session == null) throw new ArgumentNullException("session");
            if (policy == null) throw new ArgumentNullException("policy");
            _engine = engine; _session = session; _policy = policy;
        }
        private void Demand(string principal, MRKRuntimeAccessAction action)
        {
            if (string.IsNullOrWhiteSpace(principal) ||
                !_policy.IsAuthorized(principal, action, _session.Id))
                throw new UnauthorizedAccessException(
                    "未获运行权限：" + action + " / " + _session.Id);
        }
        public MRKRuntimeSessionSnapshot Observe(string principal)
        {
            Demand(principal, MRKRuntimeAccessAction.Observe);
            return _session.Snapshot();
        }
        public MRKRuntimeFlowTicket TryStart(string principal,
            IDictionary<string, object> inputs = null,
            string businessKey = null, string correlationId = null,
            CancellationToken cancellation = default(CancellationToken))
        {
            Demand(principal, MRKRuntimeAccessAction.Start);
            MRKFlowHandle flow = _session.TryStartFlow(inputs,businessKey,correlationId,cancellation);
            return flow == null ? null : new MRKRuntimeFlowTicket(flow);
        }
        public MRKRuntimeFlowTicket TryStartDebug(string principal,
            MRKExecutionDebugController controller,
            IDictionary<string, object> inputs = null,
            CancellationToken cancellation = default(CancellationToken))
        {
            Demand(principal, MRKRuntimeAccessAction.Debug);
            MRKFlowHandle flow = _session.TryStartFlowWithControl(controller,inputs,
                cancellation:cancellation);
            return flow == null ? null : new MRKRuntimeFlowTicket(flow);
        }
        private MRKFlowHandle CheckFlow(MRKRuntimeFlowTicket ticket)
        {
            if (ticket == null) throw new ArgumentNullException("ticket");
            MRKFlowHandle flow = ticket.Handle;
            if (!flow.IsOwnedBy(_engine) ||
                !string.Equals(flow.Context.Metadata.SessionId, _session.Id,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Flow不属于当前Session及运行Engine，禁止越权控制");
            return flow;
        }
        public bool RequestCancel(string principal, MRKRuntimeFlowTicket ticket)
        {
            Demand(principal, MRKRuntimeAccessAction.Cancel);
            return CheckFlow(ticket).RequestCancel();
        }
        public Task StopSessionAsync(string principal, bool cancel = false)
        {
            Demand(principal, MRKRuntimeAccessAction.Stop);
            return _session.StopAsync(cancel);
        }
        public Task<MRKCompensationReport> CompensateAsync(string principal,
            MRKExecutionPlan plan, MRKRuntimeFlowTicket ticket,
            CancellationToken cancellation = default(CancellationToken))
        {
            Demand(principal, MRKRuntimeAccessAction.Compensate);
            return _engine.CompensateAsync(plan,CheckFlow(ticket),cancellation);
        }
    }
}
