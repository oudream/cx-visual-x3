using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CxVisual.Foundation.Data.Execution;
using CxVisual.Foundation.Data.Types;
using CxVisual.Mechanism.Data.Execution;

namespace CxVisual.Mechanism.Data.RuntimeData
{
    /// <summary>已验证的M1/M2值边界；不把object或可写Buffer假装不可变。</summary>
    internal static class MDSDataSafety
    {
        internal static object Capture(object value, FDTDataAdapterRegistry adapters = null)
        { return FDTDataValueSafety.Capture(value, adapters); }

        internal static IDictionary<string, object> Snapshot(IDictionary<string, object> source,
            FDTDataAdapterRegistry adapters = null)
        {
            Dictionary<string, object> output = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> item in source)
                output.Add(item.Key, Capture(item.Value, adapters));
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
            : this(root, null) { }

        public MDSDataStack(IFDEExecutionDataScope root, FDTDataAdapterRegistry adapters,
            string runId = null)
        {
            _ownsRoot = root == null;
            _root = root ?? MDEExecutionDataScope.CreateRoot("data-" + Guid.NewGuid().ToString("N"));
            FlowHeap = new MDSDataHeap(adapters);
            FlowHeap.RunId = runId ?? _root.Id;
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
                inputs ?? new Dictionary<string, object>(), FlowHeap.Adapters);
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
            IDictionary<string, object> safe = MDSDataSafety.Snapshot(outputs, FlowHeap.Adapters);
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
            object found;
            lock (_gate)
            {
                EnsureOpen();
                IDictionary<string, object> outputs;
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
            }
            // CLR托管对象由源快照保活；外部专业复制器绝不能在Stack锁内执行。
            value = (T)MDSDataSafety.Capture(found, FlowHeap.Adapters);
            return true;
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
                // 旧式DataScope不是MDSFrame的一部分，但仍归同一个执行树。
                MDEExecutionDataScope original = _root as MDEExecutionDataScope;
                if (original != null) original.EnsureCanClose();
                if (_root.IsClosed)
                    throw new ObjectDisposedException("MDSDataStack.RootScope");
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
        private readonly object _disposeGate = new object();
        public void Dispose()
        {
            lock (_disposeGate)
            {
                if (_closed) return;
                ((MDSDataContext)Data).AbortOutputs();
                try
                {
                    _scope.Dispose();
                }
                catch
                {
                    // 有未结束子Scope时Dispose被拒绝，此Frame依然活动且可重试关闭。
                    if (!_scope.IsClosed) throw;
                    _closed = true;
                    _owner.FrameClosed(InvocationId);
                    throw;
                }
                _closed = true;
                _owner.FrameClosed(InvocationId);
            }
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
                        object original = scope.TransferPeek(from);
                        if (move) scope.TransferCheckDetachable(from);
                        Type valueType = scope.TransferPeekType(from);
                        // Move转移已由MDS接管的原数据；Copy才创建独立副本。
                        // 旧Scope Put的外部别名不受控，必须先防御复制再接管。
                        object owned = move && scope.TransferCanMoveOwned(from)
                            ? original : MDSDataSafety.Capture(original, heap.Adapters);
                        heap.TransferStoreOwned(to, owned, valueType, replace);
                        if (move) scope.TransferDetach(from);
                    }
                    else
                    {
                        Type valueType;
                        object original = heap.TransferPeekOwned(from, out valueType);
                        object owned = move ? original : MDSDataSafety.Capture(original, heap.Adapters);
                        scope.TransferStore(to, owned, replace, valueType);
                        if (move) heap.Remove(from);
                    }
                }
            }
        }
    }
}
