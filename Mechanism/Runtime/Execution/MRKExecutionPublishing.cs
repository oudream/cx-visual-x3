using System;
using System.Collections.Generic;

namespace CxVisual.Mechanism.Runtime.Execution
{
    /// <summary>Host注入的授权与安全点检查，Runtime不硬编码用户/设备权限。</summary>
    public enum MRKPlanPublishAction { Stage, Activate, Rollback }
    public interface IMRKPlanPublicationPolicy
    {
        bool Authorize(string principal, MRKPlanPublishAction action,
            MRKExecutionPlan plan, long revision);
        bool IsSafeToActivate(MRKExecutionPlan plan, long revision);
    }

    /// <summary>成功阶段与原Plan对象固定；失败禁止修改运行Slot。</summary>
    public sealed class MRKPlanPublicationReceipt
    {
        internal MRKPlanPublicationReceipt(long revision, bool accepted,
            IList<MRKCompileDiagnostic> diagnostics)
        { Revision = revision; Accepted = accepted; Diagnostics = diagnostics; }
        public long Revision { get; private set; }
        public bool Accepted { get; private set; }
        public IList<MRKCompileDiagnostic> Diagnostics { get; private set; }
    }

    /// <summary>
    /// 唯一生产发布门面：Validate→Compile→Authorize→Stage→Safe Activate。
    /// 只控制软件版本；物理设备联锁由注入的Host安全策略判定，不可默认通过。
    /// </summary>
    public sealed class MRKExecutionPlanPublisher
    {
        private readonly object _gate = new object();
        private readonly MRKExecutionPlanSlot _slot;
        private readonly IMRKPlanPublicationPolicy _policy;
        private readonly MRKExecutionPlanCompiler _compiler = new MRKExecutionPlanCompiler();
        private MRKExecutionPlan _active;
        private MRKExecutionPlan _previous;
        private MRKExecutionPlan _pending;
        private long _revision, _pendingRevision;
        private bool _rollbackPending;

        public MRKExecutionPlanPublisher(MRKExecutionPlanSlot slot, MRKExecutionPlan initial,
            long initialRevision, IMRKPlanPublicationPolicy policy)
        {
            if (slot == null) throw new ArgumentNullException("slot");
            if (initial == null) throw new ArgumentNullException("initial");
            if (policy == null) throw new ArgumentNullException("policy");
            if (!slot.RequiresAuthorization)
                throw new ArgumentException("Publisher必须绑定启用授权保护的PlanSlot", "slot");
            if (slot.ActiveRevision != initialRevision)
                throw new ArgumentException("初始Plan版本不一致", "initialRevision");
            using (MRKExecutionPlanLease lease = slot.Capture())
                if (!object.ReferenceEquals(lease.Plan, initial))
                    throw new ArgumentException("初始Plan对象不一致", "initial");
            _slot = slot; _policy = policy; _active = initial; _revision = initialRevision;
        }

        public long ActiveRevision { get { lock (_gate) return _revision; } }
        public long? PendingRevision { get { lock (_gate) return _pending == null ?
            (long?)null : _pendingRevision; } }

        public MRKPlanPublicationReceipt CompileAndStage(string principal,
            string planId, IEnumerable<MRKStepCompileRequest> requests,
            IMRKCapabilityResolver resolver, long revision)
        {
            MRKCompileResult compile = _compiler.Compile(planId, requests, resolver);
            if (!compile.Succeeded)
                return new MRKPlanPublicationReceipt(revision, false, compile.Diagnostics);
            StageCore(principal, compile.Plan, revision, false);
            return new MRKPlanPublicationReceipt(revision, true, compile.Diagnostics);
        }

        private void StageCore(string principal, MRKExecutionPlan plan,
            long revision, bool rollback)
        {
            if (string.IsNullOrWhiteSpace(principal))
                throw new ArgumentException("发布主体不能为空", "principal");
            if (plan == null) throw new ArgumentNullException("plan");
            lock (_gate)
            {
                if (!_policy.Authorize(principal,
                    rollback ? MRKPlanPublishAction.Rollback : MRKPlanPublishAction.Stage,
                    plan, revision))
                    throw new UnauthorizedAccessException("发布未获授权");
                long limit = _pending == null ? _revision : _pendingRevision;
                if (revision <= limit)
                    throw new InvalidOperationException("发布修订号必须严格递增");
                _slot.StageAuthorized(plan, revision);
                _pending = plan; _pendingRevision = revision; _rollbackPending = rollback;
            }
        }

        /// <summary>安全回滚使用新修订号重新发布旧Plan，不伪造历史修订。</summary>
        public void StageRollback(string principal, long newRevision)
        {
            MRKExecutionPlan previous;
            lock (_gate) previous = _previous;
            if (previous == null)
                throw new InvalidOperationException("没有成功激活过的历史Plan供回滚");
            StageCore(principal, previous, newRevision, true);
        }

        public bool TryActivate(string principal)
        {
            lock (_gate)
            {
                if (_pending == null) return false;
                if (!_policy.Authorize(principal, MRKPlanPublishAction.Activate,
                    _pending, _pendingRevision))
                    throw new UnauthorizedAccessException("运行切换未获授权");
                if (!_policy.IsSafeToActivate(_pending, _pendingRevision))
                    return false;
                if (!_slot.TryActivateAuthorized()) return false;
                MRKExecutionPlan old = _active;
                _active = _pending; _revision = _pendingRevision;
                _pending = null; _pendingRevision = 0;
                _previous = old;
                _rollbackPending = false;
                return true;
            }
        }
    }
}
