using System;
using System.IO;
using System.Threading;
using CxVisual.Foundation.Data;
using CxVisual.Mechanism.Core;

namespace CxVisual.Mechanism.Runtime
{
    public enum MRKConfigApplyPolicy
    {
        Immediate = 0, NextTask = 1, RestartSession = 2, RestartHost = 3
    }

    /// <summary>专业设备应用契约。null错误表示成功；没有Provider时仅更新软件快照，不能视为硬件确认。</summary>
    public interface IMRKConfigApplier<T> where T : class
    {
        string Prepare(T oldConfig, T proposedConfig);
        string Apply(T oldConfig, T proposedConfig);
        string Verify(T oldConfig, T proposedConfig);
        void Rollback(T oldConfig);
    }

    public enum MRKConfigApplyStatus
    {
        SoftwareOnly = 0,
        Pending = 1,
        VerifiedByProvider = 2,
        Failed = 3,
        Faulted = 4
    }

    /// <summary>本次任务持有稳定配置及版本，释放任务时允许NextTask配置安全切换。</summary>
    public sealed class MRKConfigTaskSnapshot<T> : IDisposable where T : class
    {
        private Action _release;
        internal MRKConfigTaskSnapshot(T value, long revision, Action release)
        { Value = value; Revision = revision; _release = release; }
        public T Value { get; private set; }
        public long Revision { get; private set; }
        public void Dispose()
        {
            Action release = Interlocked.Exchange(ref _release, null);
            if (release != null) release();
        }
    }

    /// <summary>
    /// L3运行生效器：保存版本≠运行版本。任务中不能切换已有任务快照；
    /// 专业检查不通过时保留旧ActiveRevision，等待人工处理。
    /// </summary>
    public sealed class MRKConfigRuntimeSession<T> where T : class
    {
        private readonly object _gate = new object();
        private readonly MTCConfigManager _manager;
        private readonly string _id;
        private readonly Func<T, string> _applyValidator;
        private readonly IMRKConfigApplier<T> _applier;
        private bool _isFaulted;
        private T _active;
        private long _activeRevision;
        private T _pending;
        private long _pendingRevision;
        private MRKConfigApplyPolicy _pendingPolicy;
        private int _runningTasks;
        public string LastError { get; private set; }
        public MRKConfigApplyStatus LastApplyStatus { get; private set; }
        public bool IsFaulted { get { lock (_gate) return _isFaulted; } }

        public MRKConfigRuntimeSession(MTCConfigManager manager, string id, Func<T, string> applyValidator = null,
            IMRKConfigApplier<T> applier = null)
        {
            if (manager == null) throw new ArgumentNullException("manager");
            _manager = manager;
            _id = id;
            _applyValidator = applyValidator;
            _applier = applier;
            LastApplyStatus = MRKConfigApplyStatus.SoftwareOnly;
            MTCConfigSnapshot<T> initial = manager.GetSnapshot<T>(id);
            if (!initial.Saved)
                throw new InvalidOperationException("Runtime cannot activate an unsaved configuration");
            _active = initial.Value;
            _activeRevision = initial.Revision;
        }

        public long ActiveRevision
        {
            get { lock (_gate) return _activeRevision; }
        }
        public long? PendingRevision
        {
            get { lock (_gate) return _pending == null ? (long?)null : _pendingRevision; }
        }

        public T CaptureActive()
        {
            lock (_gate) return FCMConfigJsonFile.Clone(_active);
        }

        /// <summary>捕获并校验指定策略的下一版本；不把文件保存错误等同于运行应用成功。</summary>
        public bool RequestApply(MRKConfigApplyPolicy policy)
        {
            MTCConfigSnapshot<T> snapshot = _manager.GetSnapshot<T>(_id);
            if (!snapshot.Saved)
            {
                lock (_gate) LastError = "CONFIG_UNSAVED";
                return false;
            }
            T candidate = snapshot.Value;
            long version = snapshot.Revision;
            string err = _applyValidator == null ? null : _applyValidator(candidate);
            if (!string.IsNullOrWhiteSpace(err))
            {
                lock (_gate) LastError = err;
                return false;
            }
            lock (_gate)
            {
                if (_isFaulted)
                {
                    LastError = "CONFIG_RUNTIME_FAULTED_NEEDS_MANUAL_RECOVERY";
                    return false;
                }
                if (policy == MRKConfigApplyPolicy.Immediate && _runningTasks > 0)
                {
                    LastError = "CONFIG_IMMEDIATE_REQUIRES_IDLE";
                    return false;
                }
                LastError = null;
                _pending = candidate;
                _pendingRevision = version;
                _pendingPolicy = policy;
                LastApplyStatus = MRKConfigApplyStatus.Pending;
                if (policy == MRKConfigApplyPolicy.Immediate)
                    return ActivatePendingLocked();
                return true;
            }
        }

        public MRKConfigTaskSnapshot<T> BeginTask()
        {
            lock (_gate)
            {
                if (_isFaulted)
                    throw new InvalidOperationException("CONFIG_RUNTIME_FAULTED_NEEDS_MANUAL_RECOVERY");
                if (_runningTasks == 0 && _pending != null &&
                    _pendingPolicy == MRKConfigApplyPolicy.NextTask)
                    ActivatePendingLocked();
                _runningTasks++;
                return new MRKConfigTaskSnapshot<T>(FCMConfigJsonFile.Clone(_active),
                    _activeRevision, EndTask);
            }
        }

        private void EndTask()
        {
            lock (_gate)
            {
                if (_runningTasks <= 0) throw new InvalidOperationException("Task snapshot was over-released");
                _runningTasks--;
            }
        }

        public bool OnSessionRestarted()
        {
            lock (_gate)
            {
                if (_runningTasks > 0 || _pending == null ||
                    _pendingPolicy != MRKConfigApplyPolicy.RestartSession)
                    return false;
                return ActivatePendingLocked();
            }
        }
        public bool OnHostRestarted()
        {
            lock (_gate)
            {
                if (_runningTasks > 0 || _pending == null ||
                    _pendingPolicy != MRKConfigApplyPolicy.RestartHost)
                    return false;
                return ActivatePendingLocked();
            }
        }

        /// <summary>无专业Applier时仅软件生效；专业确认失败则补偿并保持旧ActiveRevision。</summary>
        private bool ActivatePendingLocked()
        {
            if (_pending == null || _isFaulted) return false;
            T previous = FCMConfigJsonFile.Clone(_active);
            T proposed = FCMConfigJsonFile.Clone(_pending);
            if (_applier != null)
            {
                bool beganApply = false;
                try
                {
                    string error = _applier.Prepare(FCMConfigJsonFile.Clone(previous),
                        FCMConfigJsonFile.Clone(proposed));
                    if (!string.IsNullOrWhiteSpace(error)) throw new InvalidOperationException("PREPARE: " + error);
                    beganApply = true;
                    error = _applier.Apply(FCMConfigJsonFile.Clone(previous), FCMConfigJsonFile.Clone(proposed));
                    if (!string.IsNullOrWhiteSpace(error)) throw new InvalidOperationException("APPLY: " + error);
                    error = _applier.Verify(FCMConfigJsonFile.Clone(previous), FCMConfigJsonFile.Clone(proposed));
                    if (!string.IsNullOrWhiteSpace(error)) throw new InvalidOperationException("VERIFY: " + error);
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    LastApplyStatus = MRKConfigApplyStatus.Failed;
                    if (beganApply)
                    {
                        try { _applier.Rollback(FCMConfigJsonFile.Clone(previous)); }
                        catch (Exception rollbackError)
                        {
                            LastError += " | ROLLBACK_FAILED: " + rollbackError.Message;
                            _isFaulted = true;
                            LastApplyStatus = MRKConfigApplyStatus.Faulted;
                        }
                    }
                    _pending = null;
                    _pendingRevision = 0;
                    return false;
                }
            }
            _active = proposed;
            _activeRevision = _pendingRevision;
            _pending = null;
            _pendingRevision = 0;
            LastApplyStatus = _applier == null
                ? MRKConfigApplyStatus.SoftwareOnly : MRKConfigApplyStatus.VerifiedByProvider;
            LastError = null;
            return true;
        }
    }

    /// <summary>连续运行的数值参数：整个struct作为一次快照原子切换，读取无锁/无文件IO。</summary>
    public struct MRKConfigAtomicSnapshot<T> where T : struct
    {
        public T Value { get; internal set; }
        public long Revision { get; internal set; }
    }

    public sealed class MRKConfigAtomicSlot<T> where T : struct
    {
        private sealed class State
        {
            internal readonly T Value;
            internal readonly long Revision;
            internal State(T value, long revision) { Value = value; Revision = revision; }
        }
        private readonly object _writerGate = new object();
        private State _state;
        public MRKConfigAtomicSlot(T value, long revision)
        {
            if (revision < 0) throw new ArgumentOutOfRangeException("revision");
            _state = new State(value, revision);
        }
        public MRKConfigAtomicSnapshot<T> Read()
        {
            State state = Volatile.Read(ref _state);
            return new MRKConfigAtomicSnapshot<T> { Value = state.Value, Revision = state.Revision };
        }
        public void Publish(T value, long revision)
        {
            lock (_writerGate)
            {
                if (revision <= _state.Revision)
                    throw new InvalidOperationException("CONFIG_RUNTIME_REVISION_MUST_INCREASE");
                Volatile.Write(ref _state, new State(value, revision));
            }
        }
    }
}
