using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CxVisual.Foundation.Data.Execution;

namespace CxVisual.Mechanism.Data.RuntimeData
{
    /// <summary>业务节点的统一数据入口；提交、取消由运行宿主负责。</summary>
    public interface IMDSDataContext
    {
        T Get<T>(string name);
        bool TryGet<T>(string name, out T value);
        void Set<T>(string name, T value);
        void Return<T>(string name, T value);
        IDictionary<string, object> InspectPendingOutputs();
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
        private readonly IDictionary<string, object> _inputs;
        private readonly IDictionary<string, Type> _outputs;
        private readonly Action<IDictionary<string, object>> _publish;
        private readonly Dictionary<string, object> _pending =
            new Dictionary<string, object>(StringComparer.Ordinal);
        private bool _finished;

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
            _inputs = inputs == null ? null : MDSDataSafety.Snapshot(inputs);
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
                    value = (T)MDSDataSafety.Capture(legacy);
                    return true;
                }
                // 严格边界：只看本地及显式输入，绝不依赖父级自动继承。
                IDictionary<string, object> local = _scope.SnapshotLocal();
                object found;
                if (!local.TryGetValue(name, out found) &&
                    !_inputs.TryGetValue(name, out found))
                { value = default(T); return false; }
                if (found != null && !(found is T))
                    throw new InvalidCastException("数据类型不匹配：" + name);
                if (found == null && typeof(T).IsValueType &&
                    Nullable.GetUnderlyingType(typeof(T)) == null)
                    throw new InvalidCastException("null不能转换为非空值类型：" + name);
                value = (T)MDSDataSafety.Capture(found);
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
                    throw new InvalidOperationException("只读输入不能被同名Local覆盖：" + name);
                _scope.Put(name, (T)MDSDataSafety.Capture(value));
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
                        throw new InvalidOperationException("未声明的输出端口：" + name);
                    if (value != null && !expected.IsInstanceOfType(value))
                        throw new InvalidCastException("输出类型不符合端口契约：" + name);
                    if (value == null && expected.IsValueType &&
                        Nullable.GetUnderlyingType(expected) == null)
                        throw new InvalidCastException("非空输出不能返回null：" + name);
                }
                if (_pending.ContainsKey(name))
                    throw new InvalidOperationException("输出不能重复Return：" + name);
                _pending.Add(name, MDSDataSafety.Capture(value));
            }
        }

        public IDictionary<string, object> InspectPendingOutputs()
        {
            lock (_gate)
            {
                EnsureActive();
                return MDSDataSafety.Snapshot(_pending);
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
                            throw new InvalidOperationException("必要输出尚未返回：" + name);
                IDictionary<string, object> ready = MDSDataSafety.Snapshot(_pending);
                // 先将完整快照交付到Flow级DataStack，再使当前帧结算。
                // 失败则原Pending保持原状，不向任何消费者暴露部分输出。
                if (_publish != null) _publish(ready);
                _finished = true;
                _pending.Clear();
                return MDSDataSafety.Snapshot(ready);
            }
        }

        /// <summary>
        /// 显式移动/复制Local与FlowHeap的数据。Heap变化是即时副作用，
        /// 不与Return的暂存输出混为一谈；调用方要避免在可能失败的业务节点里
        /// 将暂时数据作为已完成的生产结果写入共享堆。
        /// </summary>
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
