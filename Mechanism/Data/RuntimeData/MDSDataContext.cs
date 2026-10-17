using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Types;
using CxVisual.Foundation.Data.Execution;
using CxVisual.Mechanism.Data.Execution;

namespace CxVisual.Mechanism.Data.RuntimeData
{
    /// <summary>业务节点的统一数据入口；提交、取消由运行宿主负责。</summary>
    public interface IMDSDataContext
    {
        T Get<T>(string name);
        bool TryGet<T>(string name, out T value);
        void Set<T>(string name, T value);
        void Return<T>(string name, T value);
        T Get<T>(FDTDataKey<T> key);
        void Set<T>(FDTDataKey<T> key, T value);
        void Return<T>(FDTDataKey<T> key, T value);
        IDictionary<string, object> InspectPendingOutputs();
        FDTDataAdapterRegistry Adapters { get; }
        void SetFile(string name, string filePath);
        void SetFile<T>(string name, string filePath, IMDSDataFileLoader loader);
        Task<T> LoadAsync<T>(string name, CancellationToken cancellation = default(CancellationToken));
        void Unload(string name);
        IList<MDSDataSnapshot> Search(MDSDataQuery query = null);
        MDSDataSnapshot Inspect(string name, MDSDataLocation location = MDSDataLocation.Any);
        T HeapGet<T>(string name);
        void HeapSet<T>(string name, T value);
        void MoveToHeap(string localName, string heapName = null, bool replace = false);
        void CopyToHeap(string localName, string heapName = null, bool replace = false);
        void MoveFromHeap(string heapName, string localName = null, bool replace = false);
        void CopyFromHeap(string heapName, string localName = null, bool replace = false);
    }

    /// <summary>
    /// 复用既有Scope。本类型不另建Scope或流程执行器。
    /// 托管帧的Get只允许本地数据与显式映射的输入，不继承父Scope隐式Local。
    /// </summary>
    public sealed class MDSDataContext : IMDSDataContext
    {
        private readonly object _gate = new object();
        private readonly IFDEExecutionDataScope _scope;
        private readonly MDSDataHeap _heap;
        public FDTDataAdapterRegistry Adapters
        { get { return _heap == null ? null : _heap.Adapters; } }

        private object Capture(object value)
        { return MDSDataSafety.Capture(value, Adapters); }
        private readonly IDictionary<string, object> _inputs;
        private readonly IDictionary<string, Type> _outputs;
        private readonly Action<IDictionary<string, object>> _publish;
        private readonly Dictionary<string, object> _pending =
            new Dictionary<string, object>(StringComparer.Ordinal);
        private bool _finished;
        private bool _heapMutated;

        /// <summary>向后兼容独立使用，未显式声明输出时只做本地输出快照。</summary>
        public MDSDataContext(IFDEExecutionDataScope scope)
            : this(scope, null, null, null, null) { }

        internal MDSDataContext(IFDEExecutionDataScope scope,
            IDictionary<string, object> inputs, IDictionary<string, Type> outputs,
            Action<IDictionary<string, object>> publish, MDSDataHeap heap)
        {
            if (scope == null) throw new ArgumentNullException("scope");
            _scope = scope;
            _heap = heap;
            _inputs = inputs == null ? null : MDSDataSafety.Snapshot(inputs, Adapters);
            if (outputs != null)
            {
                Dictionary<string, Type> declared = new Dictionary<string, Type>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, Type> item in outputs)
                {
                    CheckName(item.Key);
                    if (item.Value == null || item.Value == typeof(void))
                        throw new ArgumentException("输出类型无效：" + item.Key, "outputs");
                    declared.Add(item.Key, item.Value);
                }
                _outputs = new ReadOnlyDictionary<string, Type>(declared);
            }
            _publish = publish;
        }

        private void EnsureActive()
        {
            if (_scope.IsClosed) throw new ObjectDisposedException("MDSDataContext");
            if (_finished) throw new InvalidOperationException("当前执行上下文已经结算");
        }
        private static void CheckName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("数据名称不能为空", "name");
        }

        public T Get<T>(FDTDataKey<T> key)
        { return Get<T>((key ?? throw new ArgumentNullException("key")).Name); }
        public void Set<T>(FDTDataKey<T> key, T value)
        { Set((key ?? throw new ArgumentNullException("key")).Name, value); }
        public void Return<T>(FDTDataKey<T> key, T value)
        { Return((key ?? throw new ArgumentNullException("key")).Name, value); }

        public T Get<T>(string name)
        {
            T value;
            if (!TryGet<T>(name, out value))
                throw new KeyNotFoundException("找不到已映射的数据：" + name);
            return value;
        }

        public bool TryGet<T>(string name, out T value)
        {
            CheckName(name);
            lock (_gate)
            {
                EnsureActive();
                if (_inputs == null)
                {
                    T legacy;
                    if (!_scope.TryRead(name, out legacy))
                    { value = default(T); return false; }
                    value = (T)Capture(legacy);
                    return true;
                }
                // 严格边界：先读取带类型标识的Local，再查显式Input；不继承父Local。
                MDEExecutionDataScope managed = _scope as MDEExecutionDataScope;
                if (managed != null)
                {
                    T localValue;
                    if (managed.TryReadLocal<T>(name, out localValue))
                    {
                        value = (T)Capture(localValue);
                        return true;
                    }
                }
                IDictionary<string, object> local = managed == null
                    ? _scope.SnapshotLocal() : new Dictionary<string, object>();
                object found;
                if (!local.TryGetValue(name, out found) &&
                    !_inputs.TryGetValue(name, out found))
                { value = default(T); return false; }
                if (found != null && !(found is T))
                    throw new InvalidCastException("数据类型不匹配：" + name);
                if (found == null && typeof(T).IsValueType &&
                    Nullable.GetUnderlyingType(typeof(T)) == null)
                    throw new InvalidCastException("null不能转换为非空值类型：" + name);
                value = (T)Capture(found);
                return true;
            }
        }

        public void Set<T>(string name, T value)
        {
            CheckName(name);
            lock (_gate)
            {
                EnsureActive();
                if (_inputs != null && _inputs.ContainsKey(name))
                    throw new MDSDataException(MDSDataErrorCode.InputReadOnly, "只读输入不能被同名Local覆盖：" + name, name);
                T frozen = (T)Capture(value);
                MDEExecutionDataScope managed = _scope as MDEExecutionDataScope;
                if (managed != null) managed.PutManaged(name, frozen);
                else _scope.Put(name, frozen);
            }
        }

        public void Return<T>(string name, T value)
        {
            CheckName(name);
            lock (_gate)
            {
                EnsureActive();
                Type expected;
                if (_outputs != null)
                {
                    if (!_outputs.TryGetValue(name, out expected))
                        throw new MDSDataException(MDSDataErrorCode.OutputNotDeclared, "未声明的输出端口：" + name, name);
                    if (value != null && !expected.IsInstanceOfType(value))
                        throw new InvalidCastException("输出类型不符合端口契约：" + name);
                    if (value == null && expected.IsValueType &&
                        Nullable.GetUnderlyingType(expected) == null)
                        throw new InvalidCastException("非空输出不能返回null：" + name);
                }
                if (_pending.ContainsKey(name))
                    throw new MDSDataException(MDSDataErrorCode.OutputConflict, "输出不能重复Return：" + name, name);
                _pending.Add(name, Capture(value));
            }
        }

        public IDictionary<string, object> InspectPendingOutputs()
        {
            lock (_gate)
            {
                EnsureActive();
                return MDSDataSafety.Snapshot(_pending, Adapters);
            }
        }

        public IDictionary<string, object> CommitOutputs()
        {
            lock (_gate)
            {
                EnsureActive();
                if (_outputs != null)
                    foreach (string name in _outputs.Keys)
                        if (!_pending.ContainsKey(name))
                            throw new MDSDataException(MDSDataErrorCode.OutputMissing, "必要输出尚未返回：" + name, name);
                IDictionary<string, object> ready = MDSDataSafety.Snapshot(_pending, Adapters);
                // 先将完整快照交付到Flow级DataStack，再使当前帧结算。
                // 失败则原Pending保持原状，不向任何消费者暴露部分输出。
                if (_publish != null) _publish(ready);
                _finished = true;
                _pending.Clear();
                return MDSDataSafety.Snapshot(ready, Adapters);
            }
        }

        /// <summary>
        /// 显式移动/复制Local与FlowHeap的数据。Heap变化是即时副作用，
        /// 不与Return的暂存输出混为一谈；调用方要避免在可能失败的业务节点里
        /// 将暂时数据作为已完成的生产结果写入共享堆。
        /// </summary>
        /// <summary>当前运行调用可见的Input/Local/Pending与FlowHeap元数据，不自动加载文件或遍历其他节点。</summary>
        public IList<MDSDataSnapshot> Search(MDSDataQuery query = null)
        {
            List<MDSDataSnapshot> items = new List<MDSDataSnapshot>();
            lock (_gate)
            {
                EnsureActive();
                if (_inputs != null)
                    foreach (KeyValuePair<string, object> item in _inputs)
                        items.Add(new MDSDataSnapshot(item.Key,
                            item.Value == null ? typeof(object) : item.Value.GetType(),
                            MDSDataLocation.Input, MDSDataLoadState.NotFile,
                            null, MDSDataQueryLogic.SizeOf(item.Value), 1));
                IDictionary<string, object> local = _scope.SnapshotLocal();
                MDEExecutionDataScope typedScope = _scope as MDEExecutionDataScope;
                IDictionary<string, Type> types = typedScope == null
                    ? null : typedScope.SnapshotLocalTypes();
                foreach (KeyValuePair<string, object> item in local)
                {
                    Type declared;
                    if (types == null || !types.TryGetValue(item.Key, out declared))
                        declared = item.Value == null ? typeof(object) : item.Value.GetType();
                    items.Add(new MDSDataSnapshot(item.Key, declared,
                        MDSDataLocation.Local, MDSDataLoadState.NotFile, null,
                        MDSDataQueryLogic.SizeOf(item.Value), 1));
                }
                foreach (KeyValuePair<string, object> item in _pending)
                {
                    Type declared;
                    if (_outputs == null || !_outputs.TryGetValue(item.Key, out declared))
                        declared = item.Value == null ? typeof(object) : item.Value.GetType();
                    items.Add(new MDSDataSnapshot(item.Key, declared,
                        MDSDataLocation.PendingOutput, MDSDataLoadState.NotFile,
                        null, MDSDataQueryLogic.SizeOf(item.Value), 1));
                }
                if (_heap != null) _heap.AppendSnapshots(items);
            }
            return MDSDataQueryLogic.Filter(items, query);
        }

        public MDSDataSnapshot Inspect(string name,
            MDSDataLocation location = MDSDataLocation.Any)
        {
            CheckName(name);
            IList<MDSDataSnapshot> result = Search(new MDSDataQuery {
                NamePrefix = name, Location = location, Take = 500
            });
            MDSDataSnapshot found = null;
            foreach (MDSDataSnapshot entry in result)
            {
                if (!string.Equals(entry.Name, name, StringComparison.Ordinal)) continue;
                if (found != null)
                    throw new MDSDataException(MDSDataErrorCode.NameConflict,
                        "当前作用域有多个同名数据，请明确Location：" + name, name);
                found = entry;
            }
            if (found == null) throw new KeyNotFoundException("找不到数据：" + name);
            return found;
        }
        public T HeapGet<T>(string name)
        {
            lock (_gate)
            {
                EnsureActive();
                if (_heap == null) throw new NotSupportedException("没有FlowHeap");
                return _heap.Get<T>(name);
            }
        }
        public void HeapSet<T>(string name, T value)
        {
            lock (_gate)
            {
                EnsureActive();
                if (_heap == null) throw new NotSupportedException("没有FlowHeap");
                _heap.Set(name, value);
                _heapMutated = true;
            }
        }

        public void SetFile(string name, string filePath)
        {
            lock (_gate)
            {
                EnsureActive();
                if (_heap == null) throw new NotSupportedException("没有FlowHeap");
                _heap.SetFile(name, filePath);
                _heapMutated = true;
            }
        }
        public void SetFile<T>(string name, string filePath, IMDSDataFileLoader loader)
        {
            lock (_gate)
            {
                EnsureActive();
                if (_heap == null) throw new NotSupportedException("没有FlowHeap");
                _heap.SetFile<T>(name, filePath, loader);
                _heapMutated = true;
            }
        }
        public async Task<T> LoadAsync<T>(string name, CancellationToken cancellation = default(CancellationToken))
        {
            lock (_gate) EnsureActive();
            if (_heap == null) throw new NotSupportedException("没有FlowHeap");
            // 数据堆本身在异步完成前再次校验Owner状态，不持有DataContext锁等待IO。
            return await _heap.LoadAsync<T>(name, cancellation).ConfigureAwait(false);
        }
        public void Unload(string name)
        {
            lock (_gate)
            {
                EnsureActive();
                if (_heap == null) throw new NotSupportedException("没有FlowHeap");
                _heap.Unload(name);
                _heapMutated = true;
            }
        }

        public void MoveToHeap(string localName, string heapName = null, bool replace = false)
        { Transfer(localName, heapName ?? localName, true, true, replace); }

        public void CopyToHeap(string localName, string heapName = null, bool replace = false)
        { Transfer(localName, heapName ?? localName, true, false, replace); }

        public void MoveFromHeap(string heapName, string localName = null, bool replace = false)
        { Transfer(heapName, localName ?? heapName, false, true, replace); }

        public void CopyFromHeap(string heapName, string localName = null, bool replace = false)
        { Transfer(heapName, localName ?? heapName, false, false, replace); }

        private void Transfer(string from, string to, bool toHeap, bool move, bool replace)
        {
            CheckName(from);
            CheckName(to);
            lock (_gate)
            {
                EnsureActive();
                if (!toHeap && _inputs != null && _inputs.ContainsKey(to))
                    throw new InvalidOperationException("不能移动或复制到只读Input槽：" + to);
                MDSDataTransfer.Apply(_scope, _heap, from, to, toHeap, move, replace);
                _heapMutated = true;
            }
        }

        /// <summary>
        /// 仅供运行策略在已经确认允许幂等重试时调用。
        /// 清除上一次尝试的Local和PendingOutput，防止结果穿越重试边界。
        /// Heap写入属于显式外部副作用，不能在这里悄悄回滚。
        /// </summary>
        public void PrepareRetry()
        {
            lock (_gate)
            {
                EnsureActive();
                if (_heapMutated)
                    throw new InvalidOperationException("本节点已修改Heap，不允许隐式幂等重试");
                MDEExecutionDataScope data = _scope as MDEExecutionDataScope;
                if (data == null)
                    throw new NotSupportedException("旧自定义Scope尚不支持局部数据重试复位");
                data.ResetLocalForRetry();
                _pending.Clear();
            }
        }

        public void AbortOutputs()
        {
            lock (_gate)
            {
                if (_finished) return;
                _pending.Clear();
                _finished = true;
            }
        }
    }
}
