using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CxVisual.Mechanism.Data.RuntimeData
{
    internal sealed class MDSDataLifecycleRecord
    {
        internal MDSDataLifecyclePolicy Policy;
        internal int Users;
        internal int Holds;
        internal bool Closed;
        internal DateTime? ExpiresUtc;
        internal MDSDataLifecycleRecord(MDSDataLifecyclePolicy policy)
        {
            Policy = policy;
            if (policy.Retention.TimeToLive.HasValue)
                ExpiresUtc = DateTime.UtcNow.Add(policy.Retention.TimeToLive.Value);
        }
    }

    /// <summary>附着到唯一的MDSDataHeap；所有操作在原Heap锁中核对。</summary>
    public sealed class MDSDataLifecycleAccess
    {
        private readonly MDSDataHeap _owner;
        internal MDSDataLifecycleAccess(MDSDataHeap owner) { _owner = owner; }

        public void Configure(string name, MDSDataLifecyclePolicy policy)
        { _owner.ConfigureLifecycle(name, policy); }

        public void ConfigureOwnership(string name, MDSDataOwnershipPolicy policy)
        { Configure(name, GetPolicy(name).WithOwnership(policy)); }
        public void ConfigureLifetime(string name, MDSDataLifetimePolicy policy)
        { Configure(name, GetPolicy(name).WithLifetime(policy)); }
        public void ConfigureRetention(string name, MDSDataRetentionPolicy policy)
        { Configure(name, GetPolicy(name).WithRetention(policy)); }
        public void ConfigureTransfer(string name, MDSDataTransferPolicy policy)
        { Configure(name, GetPolicy(name).WithTransfer(policy)); }
        public void ConfigureReclamation(string name, MDSDataReclamationPolicy policy)
        { Configure(name, GetPolicy(name).WithReclamation(policy)); }

        public MDSDataLifecyclePolicy GetPolicy(string name) { return _owner.LifecyclePolicyOf(name); }
        public MDSDataLifecycleSnapshot Inspect(string name) { return _owner.InspectLifecycle(name); }
        public IList<string> GetBlockers(string name) { return Inspect(name).Blockers; }

        /// <summary>需要跨异步/专业过程持有数据时使用。Dispose后不再保证读取安全。</summary>
        public IDisposable AcquireUse(string name) { return _owner.AcquireLifecycleUse(name, false); }
        /// <summary>显式诊断/结果保留。Dispose解除该条保留理由。</summary>
        public IDisposable Retain(string name) { return _owner.AcquireLifecycleUse(name, true); }
        public void CloseConsumerAdmission(string name) { _owner.CloseLifecycleAdmission(name); }
        /// <summary>按显式的TTL或最后消费者规则回收，仅回收满足安全条件的数据。</summary>
        public int Sweep(DateTime? nowUtc = null) { return _owner.SweepLifecycle(nowUtc ?? DateTime.UtcNow); }
        public bool ReleaseEligible(string name) { return _owner.ReleaseLifecycleEligible(name); }
    }

    public sealed partial class MDSDataHeap
    {
        private readonly Dictionary<string, MDSDataLifecycleRecord> _lifecycles =
            new Dictionary<string, MDSDataLifecycleRecord>(StringComparer.Ordinal);
        private MDSDataLifecycleAccess _lifecycleAccess;
        public MDSDataLifecycleAccess Lifecycle
        {
            get
            {
                lock (_gate)
                {
                    EnsureOpen();
                    if (_lifecycleAccess == null)
                        _lifecycleAccess = new MDSDataLifecycleAccess(this);
                    return _lifecycleAccess;
                }
            }
        }

        private bool HasLifecycleItem(string name)
        { return _values.ContainsKey(name) || _files.ContainsKey(name); }

        internal MDSDataLifecyclePolicy LifecyclePolicyOf(string name)
        {
            lock (_gate)
            {
                EnsureOpen();
                if (!HasLifecycleItem(name)) throw new KeyNotFoundException("找不到数据：" + name);
                MDSDataLifecycleRecord record;
                return _lifecycles.TryGetValue(name, out record)
                    ? record.Policy : MDSDataLifecyclePolicy.Default;
            }
        }

        internal void ConfigureLifecycle(string name, MDSDataLifecyclePolicy policy)
        {
            if (policy == null) throw new ArgumentNullException("policy");
            policy.Validate();
            lock (_gate)
            {
                EnsureOpen();
                if (!HasLifecycleItem(name)) throw new KeyNotFoundException("找不到数据：" + name);
                MDSDataLifecycleRecord current;
                if (_lifecycles.TryGetValue(name, out current) &&
                    (current.Users != 0 || current.Holds != 0 || current.Closed))
                    throw new MDSDataException(MDSDataErrorCode.Busy,
                        "有活跃消费者/保留或已封闭入口，不能重设生命周期：" + name, name);
                _lifecycles[name] = new MDSDataLifecycleRecord(policy);
            }
        }

        private MDSDataLifecycleRecord EnsureLifecycleRecord(string name)
        {
            EnsureOpen();
            if (!HasLifecycleItem(name)) throw new KeyNotFoundException("找不到数据：" + name);
            MDSDataLifecycleRecord record;
            if (!_lifecycles.TryGetValue(name, out record))
            {
                record = new MDSDataLifecycleRecord(MDSDataLifecyclePolicy.Default);
                _lifecycles.Add(name, record);
            }
            return record;
        }

        private List<string> LifecycleBlockers(MDSDataLifecycleRecord record)
        {
            var reasons = new List<string>();
            if (record != null)
            {
                if (record.Users != 0) reasons.Add("ActiveConsumers");
                if (record.Holds != 0) reasons.Add("RetentionHolds");
                if (record.Policy.Retention.IsPinned) reasons.Add("Pinned");
            }
            return reasons;
        }

        internal MDSDataLifecycleSnapshot InspectLifecycle(string name)
        {
            lock (_gate)
            {
                MDSDataLifecycleRecord record = EnsureLifecycleRecord(name);
                return new MDSDataLifecycleSnapshot(name, RunId, record.Users,
                    record.Holds, record.Closed, record.ExpiresUtc,
                    LifecycleBlockers(record), record.Policy);
            }
        }

        internal IDisposable AcquireLifecycleUse(string name, bool hold)
        {
            lock (_gate)
            {
                var record = EnsureLifecycleRecord(name);
                if (!hold && record.Closed)
                    throw new InvalidOperationException("数据已经关闭新消费者入场：" + name);
                if (hold) checked { record.Holds++; }
                else checked { record.Users++; }
                return new MDSDataUseRegistration(this, name, record, hold);
            }
        }

        private sealed class MDSDataUseRegistration : IDisposable
        {
            private MDSDataHeap _owner;
            private readonly string _name;
            private readonly MDSDataLifecycleRecord _record;
            private readonly bool _hold;
            internal MDSDataUseRegistration(MDSDataHeap owner, string name,
                MDSDataLifecycleRecord record, bool hold)
            { _owner = owner; _name = name; _record = record; _hold = hold; }
            public void Dispose()
            {
                MDSDataHeap heap = System.Threading.Interlocked.Exchange(ref _owner, null);
                if (heap == null) return;
                heap.ReleaseLifecycleUse(_name, _record, _hold);
            }
        }

        private void ReleaseLifecycleUse(string name, MDSDataLifecycleRecord record, bool hold)
        {
            lock (_gate)
            {
                MDSDataLifecycleRecord current;
                if (!_lifecycles.TryGetValue(name, out current) ||
                    !object.ReferenceEquals(record, current))
                    throw new InvalidOperationException("生命周期使用凭证已与数据脱离");
                if (hold) { if (record.Holds == 0) throw new InvalidOperationException("保留凭证重复释放"); record.Holds--; }
                else { if (record.Users == 0) throw new InvalidOperationException("消费者重复释放"); record.Users--; }
                TrySweepRecord(name, record, DateTime.UtcNow);
            }
        }

        internal void CloseLifecycleAdmission(string name)
        {
            lock (_gate)
            {
                var record = EnsureLifecycleRecord(name);
                record.Closed = true;
                TrySweepRecord(name, record, DateTime.UtcNow);
            }
        }

        private bool IsAutoEligible(MDSDataLifecycleRecord record, DateTime nowUtc)
        {
            return (record.Policy.Reclamation.ReleaseAfterConsumers &&
                    record.Policy.Lifetime.ConsumerBound && record.Closed) ||
                   (record.Policy.Reclamation.ReleaseOnExpiry &&
                    record.ExpiresUtc.HasValue && record.ExpiresUtc.Value <= nowUtc);
        }

        private bool TrySweepRecord(string name, MDSDataLifecycleRecord record, DateTime nowUtc)
        {
            if (!IsAutoEligible(record, nowUtc) || LifecycleBlockers(record).Count != 0)
                return false;
            DropLifecycleItem(name);
            return true;
        }

        private void DropLifecycleItem(string name)
        {
            // 仅在原Heap锁中调用，使用原有预算账目，不另建存储。
            MDSDataFileEntry file;
            if (_files.TryGetValue(name, out file))
            {
                if (file.State == MDSDataLoadState.Loading) return;
                _files.Remove(name);
                if (Budget != null) Budget.Release(file.CachedBytes);
            }
            else
            {
                MDSDataEntry value;
                if (!_values.TryGetValue(name, out value)) return;
                _values.Remove(name);
                if (Budget != null) Budget.Release(value.Bytes);
            }
            _lifecycles.Remove(name);
        }

        internal int SweepLifecycle(DateTime nowUtc)
        {
            if (nowUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException("必须传入UTC时间", "nowUtc");
            lock (_gate)
            {
                EnsureOpen();
                var names = new List<string>(_lifecycles.Keys);
                int released = 0;
                foreach (string name in names)
                {
                    MDSDataLifecycleRecord record;
                    if (_lifecycles.TryGetValue(name, out record) &&
                        TrySweepRecord(name, record, nowUtc)) released++;
                }
                return released;
            }
        }

        internal bool ReleaseLifecycleEligible(string name)
        {
            lock (_gate)
            {
                MDSDataLifecycleRecord record = EnsureLifecycleRecord(name);
                if (LifecycleBlockers(record).Count != 0) return false;
                MDSDataFileEntry file;
                if (_files.TryGetValue(name, out file) &&
                    file.State == MDSDataLoadState.Loading) return false;
                DropLifecycleItem(name);
                return true;
            }
        }

        private void CheckLifecycleMutation(string name, bool transferring = false, bool copying = false)
        {
            MDSDataLifecycleRecord record;
            if (!_lifecycles.TryGetValue(name, out record)) return;
            if (transferring &&
                (copying ? (!record.Policy.Transfer.AllowCopy || !record.Policy.Ownership.AllowCopy)
                    : (!record.Policy.Transfer.AllowMove || !record.Policy.Ownership.AllowMove)))
                throw new InvalidOperationException("数据交接已被生命周期策略禁止：" + name);
            if (!copying && LifecycleBlockers(record).Count != 0)
                throw new MDSDataException(MDSDataErrorCode.Busy, "活跃数据禁止替换、移动或释放：" + name, name);
        }

        private void TransferLifecycleTo(MDSDataHeap target, string sourceName,
            string targetName, bool copy)
        {
            // 调用时源/目标Heap的稳定顺序锁均已持有。
            MDSDataLifecycleRecord state;
            target._lifecycles.Remove(targetName);
            if (_lifecycles.TryGetValue(sourceName, out state))
            {
                target._lifecycles[targetName] = new MDSDataLifecycleRecord(state.Policy);
                if (!copy) _lifecycles.Remove(sourceName);
            }
        }
    }
}