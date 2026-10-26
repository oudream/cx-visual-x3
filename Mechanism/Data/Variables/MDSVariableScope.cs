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
}
