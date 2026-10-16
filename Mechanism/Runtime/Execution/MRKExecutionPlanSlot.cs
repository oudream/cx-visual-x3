using System;
using System.Threading;

namespace CxVisual.Mechanism.Runtime.Execution
{
    /// <summary>运行任务使用中的Plan快照，Dispose仅表示此运行实例不再占用旧计划。</summary>
    public sealed class MRKExecutionPlanLease : IDisposable
    {
        private Action _release;
        internal MRKExecutionPlanLease(MRKExecutionPlan plan, long revision, Action release)
        { Plan = plan; Revision = revision; _release = release; }
        public MRKExecutionPlan Plan { get; private set; }
        public long Revision { get; private set; }
        public void Dispose()
        {
            Action release = Interlocked.Exchange(ref _release, null);
            if (release != null) release();
        }
    }

    /// <summary>
    /// Runtime内存级计划版本门槛：正在执行的流程继续使用旧Plan；
    /// 只有没有持有运行快照时才切换已由上游校验的Plan。
    /// 不负责工程草案编译、跨进程发布、专业设备安全联锁或硬件参数生效。
    /// </summary>
    public sealed class MRKExecutionPlanSlot
    {
        private readonly object _gate = new object();
        private MRKExecutionPlan _active, _pending;
        private long _activeRevision, _pendingRevision;
        private int _running;
        private readonly bool _requiresAuthorization;

        public MRKExecutionPlanSlot(MRKExecutionPlan initial, long revision,
            bool requireAuthorizedPublication = false)
        {
            if (initial == null) throw new ArgumentNullException("initial");
            if (revision < 0) throw new ArgumentOutOfRangeException("revision");
            _active = initial;
            _activeRevision = revision;
            _requiresAuthorization = requireAuthorizedPublication;
        }
        public long ActiveRevision { get { lock (_gate) return _activeRevision; } }
        public bool RequiresAuthorization { get { return _requiresAuthorization; } }
        public long? PendingRevision
        { get { lock (_gate) return _pending == null ? (long?)null : _pendingRevision; } }
        public int RunningFlows { get { lock (_gate) return _running; } }

        public MRKExecutionPlanLease Capture()
        {
            lock (_gate)
            {
                checked { _running++; }
                return new MRKExecutionPlanLease(_active, _activeRevision, Release);
            }
        }
        private void Release()
        {
            lock (_gate)
            {
                if (_running <= 0) throw new InvalidOperationException("运行计划租约被过度释放");
                _running--;
            }
        }
        public void StageValidated(MRKExecutionPlan candidate, long revision)
        {
            if (_requiresAuthorization)
                throw new UnauthorizedAccessException("生产Slot禁止绕过授权发布入口");
            StageAuthorized(candidate, revision);
        }
        internal void StageAuthorized(MRKExecutionPlan candidate, long revision)
        {
            if (candidate == null) throw new ArgumentNullException("candidate");
            lock (_gate)
            {
                long limit = _pending == null ? _activeRevision : _pendingRevision;
                if (revision <= limit)
                    throw new InvalidOperationException("候选计划版本必须单调递增");
                _pending = candidate;
                _pendingRevision = revision;
            }
        }
        /// <summary>只执行软件Plan切换；调用前仍需由实际运行Host确认硬件安全点。</summary>
        public bool TryActivatePending()
        {
            if (_requiresAuthorization)
                throw new UnauthorizedAccessException("生产Slot禁止绕过Host安全点");
            return TryActivateAuthorized();
        }
        internal bool TryActivateAuthorized()
        {
            lock (_gate)
            {
                if (_running != 0 || _pending == null) return false;
                _active = _pending;
                _activeRevision = _pendingRevision;
                _pending = null;
                _pendingRevision = 0;
                return true;
            }
        }
    }
}
