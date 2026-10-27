using System;
using CxVisual.Foundation.Data.Variables;

namespace CxVisual.Mechanism.Data.Variables
{
    /// <summary>单个Registry代次中的Scope引用，注销后旧引用失效但不销毁调用方持有的Table。</summary>
    public sealed class MDSVariableScope
    {
        private volatile bool _active = true;
        internal MDSVariableScope(MDSVariableTable table, long generation)
        { Table = table; ScopeId = table.ScopeId; Generation = generation; }
        public string ScopeId { get; private set; }
        public long Generation { get; private set; }
        public bool IsActive { get { return _active; } }
        internal MDSVariableTable Table { get; private set; }
        internal void Invalidate() { _active = false; }
        public MDSVariableHandle<T> ResolveHandle<T>(FDVVariableKey key)
        {
            return new MDSVariableHandle<T>(this, key, Resolve<T>(key));
        }
        public MDSVariable<T> Resolve<T>(FDVVariableKey key)
        {
            if (!_active) throw new ObjectDisposedException("MDSVariableScope");
            if (!string.Equals(key.ScopeId, ScopeId, StringComparison.Ordinal))
                throw new InvalidOperationException("变量引用Scope不匹配");
            MDSVariableLeaf leaf = Table.Tree.FindById(key.VariableId) as MDSVariableLeaf;
            if (leaf == null) throw new InvalidOperationException("变量身份不存在: " + key.VariableId);
            if (!_active) throw new ObjectDisposedException("MDSVariableScope");
            return leaf.Resolve<T>();
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
        public T Get() { EnsureActive(); return _variable.Get(); }
        public bool TryGet(out T value)
        {
            EnsureActive(); return _variable.TryGet(out value);
        }
        public MDSVariableSnapshot<T> Snapshot()
        {
            EnsureActive(); return _variable.Snapshot();
        }
        public void Update(T value)
        {
            EnsureActive(); _variable.Update(value);
        }
        public MDSVariableUpdateResult Update(T value, MDSVariableUpdateOptions options)
        {
            EnsureActive(); return _variable.Update(value, options);
        }
    }

}
