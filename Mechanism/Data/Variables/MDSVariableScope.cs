using System;
using CxVisual.Foundation.Data.Variables;

namespace CxVisual.Mechanism.Data.Variables
{
    /// <summary>单个Registry代次中的Scope引用，注销后旧引用失效但不销毁调用方持有的Table。</summary>
    public sealed class MDSVariableScope
    {
        private volatile bool _active = true;
        private readonly object _leaseGate = new object();
        private int _inFlight;
        [ThreadStatic] private static MDSVariableScope _executingScope;

        internal MDSVariableScope(MDSVariableTable table, long generation)
        { Table = table; ScopeId = table.ScopeId; Generation = generation; }
        public string ScopeId { get; private set; }
        public long Generation { get; private set; }
        public bool IsActive { get { return _active; } }
        internal MDSVariableTable Table { get; private set; }

        // 仅在进入/退出时短暂持锁；变量更新及订阅回调不持Scope锁。
        // Invalidate与Enter互斥：注销开始后不能再进入新的Handle/Provider操作。
        private void Enter()
        {
            lock (_leaseGate)
            {
                if (!_active) throw new ObjectDisposedException("MDSVariableScope");
                _inFlight++;
            }
        }
        private void Exit()
        {
            lock (_leaseGate)
            {
                if (--_inFlight == 0) System.Threading.Monitor.PulseAll(_leaseGate);
            }
        }
        internal struct Lease : IDisposable
        {
            private readonly MDSVariableScope _scope;
            private readonly MDSVariableScope _previous;
            internal Lease(MDSVariableScope scope, MDSVariableScope previous)
            { _scope = scope; _previous = previous; }
            public void Dispose()
            {
                _executingScope = _previous;
                _scope.Exit();
            }
        }
        // 热句柄仅租借栈上Lease，不生成每次写入的委托或闭包。
        internal Lease Acquire()
        {
            Enter();
            MDSVariableScope previous = _executingScope;
            _executingScope = this;
            return new Lease(this, previous);
        }
        internal TResult Execute<TResult>(Func<TResult> operation)
        {
            if (operation == null) throw new ArgumentNullException("operation");
            using (Acquire()) return operation();
        }
        internal void Execute(Action operation)
        {
            if (operation == null) throw new ArgumentNullException("operation");
            using (Acquire()) operation();
        }
        internal void Invalidate()
        {
            lock (_leaseGate) _active = false;
        }
        // 必须在Registry锁之外等待，否则变量回调反查Registry可能死锁。
        internal void Drain()
        {
            // 当前线程在变量通知中注销自己的Scope时不可等待自己；
            // 此时值提交已发生，失效门禁会拒绝回调里的后续写入。
            if (ReferenceEquals(_executingScope, this)) return;
            lock (_leaseGate)
            {
                while (_inFlight != 0) System.Threading.Monitor.Wait(_leaseGate);
            }
        }
        public MDSVariableHandle<T> ResolveHandle<T>(FDVVariableKey key)
        {
            return new MDSVariableHandle<T>(this, key, Resolve<T>(key));
        }
        public MDSVariable<T> Resolve<T>(FDVVariableKey key)
        {
            return Execute(() =>
            {
                if (!string.Equals(key.ScopeId, ScopeId, StringComparison.Ordinal))
                    throw new InvalidOperationException("变量引用Scope不匹配");
                MDSVariableLeaf leaf = Table.Tree.FindById(key.VariableId) as MDSVariableLeaf;
                if (leaf == null) throw new InvalidOperationException("变量身份不存在: " + key.VariableId);
                return leaf.Resolve<T>();
            });
        }
    }

    /// <summary>
    /// 绑定具体Scope代次的强类型运行引用，不缓存第二份值。
    /// 适用于应用或流程持有跨操作周期的引用；最高吞吐采集可在有效宿主内直接复用MDSVariable<T>。
    /// </summary>
    public sealed class MDSVariableHandle<T>
    {
        private readonly MDSVariableScope _scope;
        private readonly MDSVariable<T> _variable;
        internal MDSVariableHandle(MDSVariableScope scope, FDVVariableKey key, MDSVariable<T> variable)
        {
            _scope = scope ?? throw new ArgumentNullException("scope");
            _variable = variable ?? throw new ArgumentNullException("variable");
            Key = key;
            Generation = scope.Generation;
        }
        public FDVVariableKey Key { get; private set; }
        public long Generation { get; private set; }
        public bool IsActive { get { return _scope.IsActive && _scope.Generation == Generation; } }
        private void EnsureActive()
        {
            if (!IsActive) throw new ObjectDisposedException("MDSVariableHandle",
                "引用对应的运行Scope已经卸载，必须重新ResolveHandle");
        }
        public T Get()
        {
            using (_scope.Acquire()) return _variable.Get();
        }
        public bool TryGet(out T value)
        {
            using (_scope.Acquire()) return _variable.TryGet(out value);
        }
        public MDSVariableSnapshot<T> Snapshot()
        {
            using (_scope.Acquire()) return _variable.Snapshot();
        }
        public void Update(T value)
        {
            using (_scope.Acquire()) _variable.Update(value);
        }
        public MDSVariableUpdateResult Update(T value, MDSVariableUpdateOptions options)
        {
            using (_scope.Acquire()) return _variable.Update(value, options);
        }
    }

}
