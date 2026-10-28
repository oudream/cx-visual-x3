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
        internal readonly Dictionary<string, int> HoldReasons =
            new Dictionary<string, int>(StringComparer.Ordinal);
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
        public IDisposable Retain(string name, string reason = "Manual")
        { return _owner.AcquireLifecycleUse(name, true, reason); }
        public MDSDataUseLease<T> AcquireRead<T>(string name)
        {
            IDisposable token = _owner.AcquireLifecycleUse(name, false);
            return new MDSDataUseLease<T>(_owner, name, token);
        }
        public void CloseConsumerAdmission(string name) { _owner.CloseLifecycleAdmission(name); }
        /// <summary>按显式的TTL或最后消费者规则回收，仅回收满足安全条件的数据。</summary>
        public int Sweep(DateTime? nowUtc = null) { return _owner.SweepLifecycle(nowUtc ?? DateTime.UtcNow); }
        /// <summary>明确以字节为目标驱逐有资格的缓存，返回真实释放的逻辑载荷字节。</summary>
        public long SweepPressure(long requiredBytes)
        { return _owner.SweepLifecyclePressure(requiredBytes); }
        public bool ReleaseEligible(string name) { return _owner.ReleaseLifecycleEligible(name); }
    }

    /// <summary>高级开发者的便捷读取入口；读值与释放使用登记串行化。</summary>
    public sealed class MDSDataUseLease<T> : IDisposable
    {
        private readonly object _gate = new object();
        private readonly MDSDataHeap _heap;
        private readonly string _name;
        private IDisposable _token;
        internal MDSDataUseLease(MDSDataHeap heap, string name, IDisposable token)
        { _heap = heap; _name = name; _token = token; }
        public T Read()
        {
            lock (_gate)
            {
                if (_token == null) throw new ObjectDisposedException("MDSDataUseLease");
                return _heap.Get<T>(_name);
            }
        }
        public void Dispose()
        {
            lock (_gate)
            {
                if (_token == null) return;
                _token.Dispose();
                _token = null;
            }
        }
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
                if (record.Holds != 0)
                {
                    reasons.Add("RetentionHolds");
                    foreach (string reason in record.HoldReasons.Keys) reasons.Add("Retained:" + reason);
                }
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
                    LifecycleBlockers(record), record.HoldReasons.Keys, record.Policy);
            }
        }

        internal IDisposable AcquireLifecycleUse(string name, bool hold, string reason = null)
        {
            if (hold && (string.IsNullOrWhiteSpace(reason) || reason.Length > 128))
                throw new ArgumentException("保留理由不能为空或过长", "reason");
            lock (_gate)
            {
                var record = EnsureLifecycleRecord(name);
                if (!hold && record.Closed)
                    throw new InvalidOperationException("数据已经关闭新消费者入场：" + name);
                if (hold)
                {
                    checked { record.Holds++; }
                    int count;
                    record.HoldReasons.TryGetValue(reason, out count);
                    record.HoldReasons[reason] = checked(count + 1);
                }
                else checked { record.Users++; }
                return new MDSDataUseRegistration(this, name, record, hold, reason);
            }
        }

        private sealed class MDSDataUseRegistration : IDisposable
        {
            private MDSDataHeap _owner;
            private readonly string _name;
            private readonly MDSDataLifecycleRecord _record;
            private readonly bool _hold;
            private readonly string _reason;
            internal MDSDataUseRegistration(MDSDataHeap owner, string name,
                MDSDataLifecycleRecord record, bool hold, string reason)
            { _owner = owner; _name = name; _record = record; _hold = hold; _reason = reason; }
            public void Dispose()
            {
                MDSDataHeap heap = System.Threading.Interlocked.Exchange(ref _owner, null);
                if (heap == null) return;
                heap.ReleaseLifecycleUse(_name, _record, _hold, _reason);
            }
        }

        private void ReleaseLifecycleUse(string name, MDSDataLifecycleRecord record,
            bool hold, string reason)
        {
            lock (_gate)
            {
                MDSDataLifecycleRecord current;
                if (!_lifecycles.TryGetValue(name, out current) ||
                    !object.ReferenceEquals(record, current))
                    throw new InvalidOperationException("生命周期使用凭证已与数据脱离");
                if (hold)
                {
                    int count;
                    if (record.Holds == 0 || !record.HoldReasons.TryGetValue(reason, out count) || count == 0)
                        throw new InvalidOperationException("保留凭证重复释放或来源无效");
                    record.Holds--;
                    if (count == 1) record.HoldReasons.Remove(reason);
                    else record.HoldReasons[reason] = count - 1;
                }
                else { if (record.Users == 0) throw new InvalidOperationException("消费者重复释放"); record.Users--; }
                TrySweepRecord(name, record, DateTime.UtcNow);
            }
        }

        // 在文件缓存通过代次和预算验证后续期TTL；不在外部异步Loader上持Heap锁。
        private void OnLifecycleFileLoaded(string name)
        {
            MDSDataLifecycleRecord record;
            if (_lifecycles.TryGetValue(name, out record) &&
                record.Policy.Retention.TimeToLive.HasValue)
                record.ExpiresUtc = DateTime.UtcNow.Add(record.Policy.Retention.TimeToLive.Value);
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
            // FileLocator是持久路径说明，不是加载缓存。TTL只卸载已加载内容；
            // 只有显式ReleaseEligible或整个Heap关闭才移除定位符。
            MDSDataFileEntry file;
            if (record.Policy.Reclamation.ReleaseOnExpiry &&
                record.ExpiresUtc.HasValue && record.ExpiresUtc.Value <= nowUtc &&
                _files.TryGetValue(name, out file))
            {
                if (file.State == MDSDataLoadState.Loading ||
                    file.State != MDSDataLoadState.Loaded) return false;
                file.Cache = null;
                if (Budget != null) Budget.Release(file.CachedBytes);
                file.CachedBytes = 0;
                file.State = MDSDataLoadState.Unloaded;
                file.Generation++;
                record.ExpiresUtc = null;
                return true;
            }
            return DropLifecycleItem(name);
        }

        private bool DropLifecycleItem(string name)
        {
            // 仅在原Heap锁中调用，使用原有预算账目，不另建存储。
            MDSDataFileEntry file;
            if (_files.TryGetValue(name, out file))
            {
                if (file.State == MDSDataLoadState.Loading) return false;
                _files.Remove(name);
                if (Budget != null) Budget.Release(file.CachedBytes);
            }
            else
            {
                MDSDataEntry value;
                if (!_values.TryGetValue(name, out value)) return false;
                _values.Remove(name);
                if (Budget != null) Budget.Release(value.Bytes);
            }
            _lifecycles.Remove(name);
            return true;
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

        // 仅允许回收无使用/保留的显式TTL缓存；所有预算操作仍归原Heap。
        internal long SweepLifecyclePressure(long requiredBytes, string excludeName = null)
        {
            if (requiredBytes < 0) throw new ArgumentOutOfRangeException("requiredBytes");
            if (requiredBytes == 0) return 0;
            lock (_gate)
            {
                EnsureOpen();
                if (Budget == null)
                    throw new NotSupportedException("没有MDSDataBudget不能证明压力回收的载荷字节数");
                long freed = 0;
                var names = new List<string>(_lifecycles.Keys);
                names.Sort(StringComparer.Ordinal);
                foreach (string name in names)
                {
                    if (string.Equals(name, excludeName, StringComparison.Ordinal)) continue;
                    MDSDataLifecycleRecord record;
                    if (!_lifecycles.TryGetValue(name, out record) ||
                        !record.Policy.Reclamation.ReleaseOnPressure ||
                        LifecycleBlockers(record).Count != 0) continue;
                    MDSDataFileEntry file;
                    if (_files.TryGetValue(name, out file))
                    {
                        if (file.State != MDSDataLoadState.Loaded) continue;
                        long bytes = file.CachedBytes;
                        file.Cache = null;
                        file.CachedBytes = 0;
                        file.State = MDSDataLoadState.Unloaded;
                        file.Generation++;
                        record.ExpiresUtc = null;
                        Budget.Release(bytes);
                        freed = checked(freed + bytes);
                    }
                    else
                    {
                        MDSDataEntry item;
                        if (!_values.TryGetValue(name, out item) || item.Bytes == 0) continue;
                        long bytes = item.Bytes;
                        if (DropLifecycleItem(name))
                            freed = checked(freed + bytes);
                    }
                    if (freed >= requiredBytes) break;
                }
                return freed;
            }
        }

        // 调用方持有Heap锁；最终是否足够仍由Budget.Reserve原子判断。
        private void ReserveHeapBudgetWithPressure(long bytes, string excludeName)
        {
            if (Budget == null) return;
            long available = Budget.LimitBytes - Budget.RetainedBytes;
            if (bytes > available)
                SweepLifecyclePressure(bytes - available, excludeName);
            Budget.Reserve(bytes);
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
                return DropLifecycleItem(name);
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