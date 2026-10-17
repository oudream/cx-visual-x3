using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CxVisual.Foundation.Data.Execution;

namespace CxVisual.Mechanism.Data.Execution
{
    /// <summary>
    /// Flow/子流程/节点的内存作用域；每次运行实例独立，无全局静态数据栈。
    /// 有子作用域时禁止释放父级，确保运行中的子节点可安全读取其父级数据。
    /// </summary>
    public sealed class MDEExecutionDataScope : IFDEExecutionDataScope
    {
        private sealed class MDEExecutionDataEntry
        {
            internal readonly object Value;
            internal readonly Action Release;
            internal MDEExecutionDataEntry(object value, Action release) { Value = value; Release = release; }
        }
        private readonly object _gate = new object();
        private readonly Dictionary<string, MDEExecutionDataEntry> _local =
            new Dictionary<string, MDEExecutionDataEntry>(StringComparer.Ordinal);
        private readonly List<string> _insertionOrder = new List<string>();
        private readonly MDEExecutionDataScope _parent;
        private int _liveChildren;
        private bool _closed;
        private MDEExecutionDataScope(string id, MDEExecutionDataScope parent)
        { Id = id; _parent = parent; }
        public static MDEExecutionDataScope CreateRoot(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("作用域名称不能为空", "id");
            return new MDEExecutionDataScope(id, null);
        }
        public string Id { get; private set; }
        public IFDEExecutionDataScope Parent { get { return _parent; } }
        public bool IsClosed { get { lock (_gate) return _closed; } }
        public IFDEExecutionDataScope CreateChild(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("子作用域名称不能为空", "name");
            lock (_gate)
            {
                EnsureOpen();
                checked { _liveChildren++; }
                return new MDEExecutionDataScope(Id + "/" + name, this);
            }
        }
        private void EnsureOpen()
        { if (_closed) throw new ObjectDisposedException("MDEExecutionDataScope", Id); }
        public void Put<T>(string name, T value) { Add(name, value, null); }
        public void PutOwned<T>(string name, T value, Action<T> release)
        {
            if (release == null) throw new ArgumentNullException("release");
            Add(name, value, () => release(value));
        }
        private void Add(string name, object value, Action release)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("数据名称不能为空", "name");
            lock (_gate)
            {
                EnsureOpen();
                if (_local.ContainsKey(name))
                    throw new InvalidOperationException("同一作用域禁止覆盖已公布数据：" + name);
                _local.Add(name, new MDEExecutionDataEntry(value, release));
                _insertionOrder.Add(name);
            }
        }
        public bool TryRead<T>(string name, out T value)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("数据名称不能为空", "name");
            MDEExecutionDataEntry entry;
            lock (_gate)
            {
                EnsureOpen();
                if (_local.TryGetValue(name, out entry))
                {
                    if (entry.Value == null)
                    {
                        if (typeof(T).IsValueType && Nullable.GetUnderlyingType(typeof(T)) == null)
                            throw new InvalidCastException("null不能赋值给值类型：" + name);
                        value = default(T);
                        return true;
                    }
                    if (!(entry.Value is T))
                        throw new InvalidCastException("数据类型不匹配：" + name);
                    value = (T)entry.Value;
                    return true;
                }
            }
            if (_parent != null) return _parent.TryRead(name, out value);
            value = default(T);
            return false;
        }
        public IDictionary<string, object> SnapshotLocal()
        {
            lock (_gate)
            {
                EnsureOpen();
                Dictionary<string, object> copy =
                    new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, MDEExecutionDataEntry> item in _local)
                    copy.Add(item.Key, item.Value.Value);
                return new ReadOnlyDictionary<string, object>(copy);
            }
        }
        // 仅供Mechanism.Data的Heap/Stack受控转移使用；调用方必须持有SyncRoot。
        // 不向一般业务暴露移除Owned数据的捷径，避免绕开Release生命周期。
        internal object SyncRoot { get { return _gate; } }

        /// <summary>预检执行子作用域，避免DataStack先清Heap后发现遗留子节点仍活动。</summary>
        internal void EnsureCanClose()
        {
            lock (_gate)
            {
                EnsureOpen();
                if (_liveChildren > 0)
                    throw new InvalidOperationException("尚有子运行作用域，不能提前关闭：" + Id);
            }
        }

        internal object TransferPeek(string name)
        {
            lock (_gate)
            {
                EnsureOpen();
                MDEExecutionDataEntry entry;
                if (!_local.TryGetValue(name, out entry))
                    throw new KeyNotFoundException("局部数据不存在：" + name);
                if (entry.Release != null)
                    throw new InvalidOperationException("带资源释放回调的数据禁止直接转移：" + name);
                return entry.Value;
            }
        }

        internal void TransferStore(string name, object value, bool replace)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("局部数据名称不能为空", "name");
            lock (_gate)
            {
                EnsureOpen();
                MDEExecutionDataEntry old;
                if (_local.TryGetValue(name, out old))
                {
                    if (!replace) throw new InvalidOperationException("目标名称已存在：" + name);
                    if (old.Release != null || _liveChildren != 0)
                        throw new InvalidOperationException("目标正被占用或带有释放回调：" + name);
                    _local[name] = new MDEExecutionDataEntry(value, null);
                    return;
                }
                _local.Add(name, new MDEExecutionDataEntry(value, null));
                _insertionOrder.Add(name);
            }
        }

        internal void TransferCheckDetachable(string name)
        {
            lock (_gate)
            {
                EnsureOpen();
                MDEExecutionDataEntry entry;
                if (!_local.TryGetValue(name, out entry))
                    throw new KeyNotFoundException("局部数据不存在：" + name);
                if (entry.Release != null || _liveChildren != 0)
                    throw new InvalidOperationException("来源被子作用域占用或具有释放回调：" + name);
            }
        }

        internal void TransferDetach(string name)
        {
            lock (_gate)
            {
                EnsureOpen();
                MDEExecutionDataEntry existing;
                if (!_local.TryGetValue(name, out existing))
                    throw new KeyNotFoundException("局部数据不存在：" + name);
                if (existing.Release != null || _liveChildren != 0)
                    throw new InvalidOperationException("来源正被占用或带有释放回调：" + name);
                _local.Remove(name);
                _insertionOrder.Remove(name);
            }
        }

        internal void ResetLocalForRetry()
        {
            List<Action> releases = new List<Action>();
            lock (_gate)
            {
                EnsureOpen();
                if (_liveChildren != 0)
                    throw new InvalidOperationException("子作用域未结算，不能开始新尝试");
                for (int index = _insertionOrder.Count - 1; index >= 0; index--)
                {
                    Action release = _local[_insertionOrder[index]].Release;
                    if (release != null) releases.Add(release);
                }
                _local.Clear();
                _insertionOrder.Clear();
            }
            List<Exception> errors = new List<Exception>();
            foreach (Action release in releases)
                try { release(); } catch (Exception ex) { errors.Add(ex); }
            if (errors.Count != 0)
                throw new AggregateException("重试前释放上一尝试数据失败", errors);
        }

        public void Dispose()
        {
            List<Action> releases = new List<Action>();
            lock (_gate)
            {
                if (_closed) return;
                if (_liveChildren > 0)
                    throw new InvalidOperationException("尚有子运行作用域，禁止提前释放：" + Id);
                _closed = true;
                for (int index = _insertionOrder.Count - 1; index >= 0; index--)
                {
                    Action release = _local[_insertionOrder[index]].Release;
                    if (release != null) releases.Add(release);
                }
                _local.Clear();
                _insertionOrder.Clear();
            }
            List<Exception> failures = new List<Exception>();
            foreach (Action action in releases)
                try { action(); } catch (Exception ex) { failures.Add(ex); }
            if (_parent != null) _parent.ChildClosed();
            if (failures.Count > 0)
                throw new AggregateException("数据资源回收时发生错误", failures);
        }
        private void ChildClosed()
        {
            lock (_gate)
            {
                if (_liveChildren <= 0) throw new InvalidOperationException("子作用域引用数不正确");
                _liveChildren--;
            }
        }
    }
}
