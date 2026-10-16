using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CxVisual.Foundation.Data.Execution;
using CxVisual.Mechanism.Data.Execution;

namespace CxVisual.Mechanism.Data.RuntimeData
{
    /// <summary>已验证的M1/M2值边界；不把object或可写Buffer假装不可变。</summary>
    internal static class MDSDataSafety
    {
        internal static object Capture(object value)
        {
            if (value == null) return null;
            Type type = value.GetType();
            if (type == typeof(byte[])) return ((byte[])value).Clone();
            if (type.IsPrimitive || type.IsEnum ||
                type == typeof(string) || type == typeof(decimal) ||
                type == typeof(DateTime) || type == typeof(DateTimeOffset) ||
                type == typeof(TimeSpan) || type == typeof(Guid)) return value;
            throw new NotSupportedException("数据类型尚无安全拷贝或不可变协议：" + type.FullName);
        }

        internal static IDictionary<string, object> Snapshot(IDictionary<string, object> source)
        {
            Dictionary<string, object> output = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> item in source)
                output.Add(item.Key, Capture(item.Value));
            return new ReadOnlyDictionary<string, object>(output);
        }
    }

    /// <summary>
    /// 一个Flow独占一个逻辑DataStack。既不是全局栈，也不依赖CLR线程。
    /// 实际执行Scope继续使用已有IFDEExecutionDataScope；已提交输出只在这里保存一份真值。
    /// </summary>
    public sealed class MDSDataStack : IDisposable
    {
        private readonly object _gate = new object();
        private readonly IFDEExecutionDataScope _root;
        private readonly bool _ownsRoot;
        public MDSDataHeap FlowHeap { get; private set; }
        private readonly HashSet<string> _active = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, IDictionary<string, object>> _committed =
            new Dictionary<string, IDictionary<string, object>>(StringComparer.Ordinal);
        private bool _closed;

        public MDSDataStack(IFDEExecutionDataScope root = null)
        {
            _ownsRoot = root == null;
            _root = root ?? MDEExecutionDataScope.CreateRoot("data-" + Guid.NewGuid().ToString("N"));
            FlowHeap = new MDSDataHeap();
        }

        public IFDEExecutionDataScope RootScope { get { return _root; } }
        public int ActiveFrames { get { lock (_gate) return _active.Count; } }

        public MDSDataFrame OpenFrame(string invocationId, IFDEExecutionDataScope scope,
            IDictionary<string, object> inputs, IDictionary<string, Type> declaredOutputs)
        {
            if (string.IsNullOrWhiteSpace(invocationId))
                throw new ArgumentException("调用帧ID不能为空", "invocationId");
            if (scope == null) throw new ArgumentNullException("scope");
            // 在锁外制作副本；复制失败不修改Stack。
            IDictionary<string, object> safeInputs = MDSDataSafety.Snapshot(
                inputs ?? new Dictionary<string, object>());
            lock (_gate)
            {
                EnsureOpen();
                if (_active.Contains(invocationId) || _committed.ContainsKey(invocationId))
                    throw new MDSDataException(MDSDataErrorCode.NameConflict, "重复的运行调用身份：" + invocationId, invocationId, invocationId);
                MDSDataFrame frame = new MDSDataFrame(this, invocationId, scope,
                    safeInputs, declaredOutputs);
                _active.Add(invocationId);
                return frame;
            }
        }

        internal void Publish(string invocationId, IDictionary<string, object> outputs)
        {
            IDictionary<string, object> safe = MDSDataSafety.Snapshot(outputs);
            lock (_gate)
            {
                EnsureOpen();
                if (!_active.Contains(invocationId))
                    throw new InvalidOperationException("未活动或已结算的调用帧：" + invocationId);
                if (_committed.ContainsKey(invocationId))
                    throw new MDSDataException(MDSDataErrorCode.OutputConflict, "该调用帧的输出已提交：" + invocationId, invocationId, invocationId);
                _committed.Add(invocationId, safe); // 所有Outputs同时可见
            }
        }

        public bool TryReadOutput<T>(string invocationId, string outputName, out T value)
        {
            if (string.IsNullOrWhiteSpace(invocationId) || string.IsNullOrWhiteSpace(outputName))
                throw new ArgumentException("调用帧及输出名不能为空");
            lock (_gate)
            {
                EnsureOpen();
                IDictionary<string, object> outputs;
                object found;
                if (!_committed.TryGetValue(invocationId, out outputs) ||
                    !outputs.TryGetValue(outputName, out found))
                {
                    value = default(T);
                    return false;
                }
                if (found != null && !(found is T))
                    throw new InvalidCastException("输出类型不匹配：" + invocationId + "." + outputName);
                if (found == null && typeof(T).IsValueType &&
                    Nullable.GetUnderlyingType(typeof(T)) == null)
                    throw new InvalidCastException("null不能转换为非空值类型");
                value = (T)MDSDataSafety.Capture(found);
                return true;
            }
        }

        public bool TryReadOutput(string invocationId, string outputName, out object value)
        {
            return TryReadOutput<object>(invocationId, outputName, out value);
        }

        internal void FrameClosed(string invocationId)
        {
            lock (_gate)
            {
                if (!_active.Remove(invocationId))
                    throw new InvalidOperationException("重复关闭运行调用帧：" + invocationId);
            }
        }

        private void EnsureOpen()
        {
            if (_closed) throw new ObjectDisposedException("MDSDataStack");
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_closed) return;
                if (_active.Count > 0)
                    throw new MDSDataException(MDSDataErrorCode.Busy, "仍有执行节点，禁止回收DataStack");
                _closed = true;
                _committed.Clear();
            }
            try { FlowHeap.Dispose(); }
            finally { if (_ownsRoot) _root.Dispose(); }
        }
    }

    /// <summary>每个调用一次Frame；Frame拥有本次输出暂存，数据Scope仍由现有Scope对象负责。</summary>
    public sealed class MDSDataFrame : IDisposable
    {
        private readonly MDSDataStack _owner;
        private readonly IFDEExecutionDataScope _scope;
        private bool _closed;
        internal MDSDataFrame(MDSDataStack owner, string id, IFDEExecutionDataScope scope,
            IDictionary<string, object> inputs, IDictionary<string, Type> outputs)
        {
            _owner = owner;
            _scope = scope;
            InvocationId = id;
            Data = new MDSDataContext(scope, inputs, outputs,
                committed => owner.Publish(id, committed), owner.FlowHeap);
        }
        public string InvocationId { get; private set; }
        public IMDSDataContext Data { get; private set; }
        public IDictionary<string, object> CommitOutputs()
        {
            if (_closed) throw new ObjectDisposedException("MDSDataFrame");
            return ((MDSDataContext)Data).CommitOutputs();
        }
        public void AbortOutputs()
        {
            if (_closed) return;
            ((MDSDataContext)Data).AbortOutputs();
        }
        public void Dispose()
        {
            if (_closed) return;
            _closed = true;
            AbortWhileClosing();
            try { _scope.Dispose(); }
            finally { _owner.FrameClosed(InvocationId); }
        }
        private void AbortWhileClosing()
        {
            ((MDSDataContext)Data).AbortOutputs();
        }
    }

    /// <summary>
    /// 同一进程内Heap/Stack转移；Scope锁始终先于Heap锁。
    /// 所有普通读写也持有对应容器锁，成功转移不会向观察者暴露半成品。
    /// 外部文件、设备句柄与Owned释放回调不参与本P0转移。
    /// </summary>
    internal static class MDSDataTransfer
    {
        internal static void Apply(IFDEExecutionDataScope dataScope, MDSDataHeap heap,
            string from, string to, bool toHeap, bool move, bool replace)
        {
            if (heap == null) throw new InvalidOperationException("当前上下文没有FlowHeap");
            MDEExecutionDataScope scope = dataScope as MDEExecutionDataScope;
            if (scope == null)
                throw new NotSupportedException("Move/Copy要求使用既有MDE执行作用域");
            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
                throw new ArgumentException("来源或目标名称为空");
            lock (scope.SyncRoot)
            {
                lock (heap.SyncRoot)
                {
                    if (toHeap)
                    {
                        object value = scope.TransferPeek(from);
                        if (move) scope.TransferCheckDetachable(from);
                        // 复制准备先于目标可见性变更。
                        object safe = MDSDataSafety.Capture(value);
                        bool exists = heap.Contains(to);
                        if (exists && !replace)
                            throw new InvalidOperationException("目标Heap名称已存在：" + to);
                        if (exists) heap.Replace(to, safe);
                        else heap.Set(to, safe);
                        if (move) scope.TransferDetach(from);
                    }
                    else
                    {
                        object safe = heap.Get<object>(from);
                        scope.TransferStore(to, MDSDataSafety.Capture(safe), replace);
                        if (move) heap.Remove(from);
                    }
                }
            }
        }
    }
}
