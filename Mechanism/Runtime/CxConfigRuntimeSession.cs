using System;
using System.IO;
using System.Threading;
using CxVisual.Foundation.Data;
using CxVisual.Mechanism.Core;

namespace CxVisual.Mechanism.Runtime
{
    public enum CxConfigApplyPolicy
    {
        Immediate = 0, NextTask = 1, RestartSession = 2, RestartHost = 3
    }

    /// <summary>本次任务持有稳定配置及版本，释放任务时允许NextTask配置安全切换。</summary>
    public sealed class CxConfigTaskSnapshot<T> : IDisposable where T : class
    {
        private Action _release;
        internal CxConfigTaskSnapshot(T value, long revision, Action release)
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
    public sealed class CxConfigRuntimeSession<T> where T : class
    {
        private readonly object _gate = new object();
        private readonly CxConfigManager _manager;
        private readonly string _id;
        private readonly Func<T, string> _applyValidator;
        private T _active;
        private long _activeRevision;
        private T _pending;
        private long _pendingRevision;
        private CxConfigApplyPolicy _pendingPolicy;
        private int _runningTasks;
        public string LastError { get; private set; }

        public CxConfigRuntimeSession(CxConfigManager manager, string id, Func<T, string> applyValidator = null)
        {
            if (manager == null) throw new ArgumentNullException("manager");
            _manager = manager;
            _id = id;
            _applyValidator = applyValidator;
            if (_manager.IsDirty(id))
                throw new InvalidOperationException("Runtime cannot activate an unsaved configuration");
            _active = manager.Get<T>(id);
            _activeRevision = manager.GetRevision(id);
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
            lock (_gate) return CxConfigJsonFile.Clone(_active);
        }

        /// <summary>捕获并校验指定策略的下一版本；不把文件保存错误等同于运行应用成功。</summary>
        public bool RequestApply(CxConfigApplyPolicy policy)
        {
            T candidate = null;
            long version = -1;
            for (int attempt = 0; attempt < 3; ++attempt)
            {
                if (_manager.IsDirty(_id))
                {
                    lock (_gate) LastError = "CONFIG_UNSAVED";
                    return false;
                }
                long oldVersion = _manager.GetRevision(_id);
                T snapshot = _manager.Get<T>(_id);
                long newerVersion = _manager.GetRevision(_id);
                if (oldVersion != newerVersion) continue;
                candidate = snapshot;
                version = newerVersion;
                break;
            }
            if (candidate == null)
            {
                lock (_gate) LastError = "CONFIG_CONCURRENT_UPDATE";
                return false;
            }
            string err = _applyValidator == null ? null : _applyValidator(candidate);
            if (!string.IsNullOrWhiteSpace(err))
            {
                lock (_gate) LastError = err;
                return false;
            }
            lock (_gate)
            {
                LastError = null;
                _pending = candidate;
                _pendingRevision = version;
                _pendingPolicy = policy;
                if (policy == CxConfigApplyPolicy.Immediate)
                    ActivatePendingLocked();
                return true;
            }
        }

        public CxConfigTaskSnapshot<T> BeginTask()
        {
            lock (_gate)
            {
                if (_runningTasks == 0 && _pending != null &&
                    _pendingPolicy == CxConfigApplyPolicy.NextTask)
                    ActivatePendingLocked();
                _runningTasks++;
                return new CxConfigTaskSnapshot<T>(CxConfigJsonFile.Clone(_active),
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
                    _pendingPolicy != CxConfigApplyPolicy.RestartSession)
                    return false;
                ActivatePendingLocked();
                return true;
            }
        }
        public bool OnHostRestarted()
        {
            lock (_gate)
            {
                if (_runningTasks > 0 || _pending == null ||
                    _pendingPolicy != CxConfigApplyPolicy.RestartHost)
                    return false;
                ActivatePendingLocked();
                return true;
            }
        }
        private void ActivatePendingLocked()
        {
            _active = CxConfigJsonFile.Clone(_pending);
            _activeRevision = _pendingRevision;
            _pending = null;
            _pendingRevision = 0;
        }
    }

    /// <summary>连续运行的数值参数：整个struct作为一次快照原子切换，读取无锁/无文件IO。</summary>
    public struct CxConfigAtomicSnapshot<T> where T : struct
    {
        public T Value { get; internal set; }
        public long Revision { get; internal set; }
    }

    public sealed class CxConfigAtomicSlot<T> where T : struct
    {
        private sealed class State
        {
            internal readonly T Value;
            internal readonly long Revision;
            internal State(T value, long revision) { Value = value; Revision = revision; }
        }
        private readonly object _writerGate = new object();
        private State _state;
        public CxConfigAtomicSlot(T value, long revision)
        {
            if (revision < 0) throw new ArgumentOutOfRangeException("revision");
            _state = new State(value, revision);
        }
        public CxConfigAtomicSnapshot<T> Read()
        {
            State state = Volatile.Read(ref _state);
            return new CxConfigAtomicSnapshot<T> { Value = state.Value, Revision = state.Revision };
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
