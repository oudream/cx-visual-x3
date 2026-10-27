using System;
using System.Collections.Generic;
using System.Linq;
using CxVisual.Foundation.Data.Variables;

namespace CxVisual.Mechanism.Data.Variables
{
    // 按工程/Host 会话实例建立，禁止全局静态 VariableManager。
    public sealed class MDSVariableRegistry
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, MDSVariableTable> _tables =
            new Dictionary<string, MDSVariableTable>(StringComparer.Ordinal);
        private readonly Dictionary<string, MDSVariableScope> _scopes =
            new Dictionary<string, MDSVariableScope>(StringComparer.Ordinal);
        private long _generation;
        public void Register(MDSVariableTable table)
        {
            if (table == null) throw new ArgumentNullException("table");
            lock (_gate)
            {
                if (_tables.ContainsKey(table.ScopeId)) throw new ArgumentException("变量作用域重复: " + table.ScopeId);
                _tables.Add(table.ScopeId, table);
                _scopes.Add(table.ScopeId, new MDSVariableScope(table, ++_generation));
            }
        }
        public MDSVariableTable Find(string scopeId)
        {
            if (string.IsNullOrWhiteSpace(scopeId)) return null;
            lock (_gate)
            {
                MDSVariableTable table;
                return _tables.TryGetValue(scopeId, out table) ? table : null;
            }
        }
        public MDSVariableScope FindScope(string scopeId)
        {
            if (string.IsNullOrWhiteSpace(scopeId)) return null;
            lock (_gate)
            {
                MDSVariableScope scope;
                return _scopes.TryGetValue(scopeId, out scope) ? scope : null;
            }
        }
        public MDSVariable<T> Resolve<T>(FDVVariableKey key)
        {
            MDSVariableScope scope = FindScope(key.ScopeId);
            if (scope == null) throw new InvalidOperationException("变量Scope不存在: " + key.ScopeId);
            return scope.Resolve<T>(key);
        }
        public MDSVariableHandle<T> ResolveHandle<T>(FDVVariableKey key)
        {
            MDSVariableScope scope = FindScope(key.ScopeId);
            if (scope == null) throw new InvalidOperationException("变量Scope不存在: " + key.ScopeId);
            return scope.ResolveHandle<T>(key);
        }
        public bool Remove(string scopeId)
        {
            if (string.IsNullOrWhiteSpace(scopeId)) return false;
            MDSVariableScope scope;
            lock (_gate)
            {
                if (!_scopes.TryGetValue(scopeId, out scope)) return false;
                scope.Invalidate();
                _scopes.Remove(scopeId);
                _tables.Remove(scopeId);
            }
            // 不持Registry锁等待已进入的句柄/Provider操作。
            // 完成注销后旧Scope不会再产生新的写入；直接持有原Table的历史兼容调用不受影响。
            scope.Drain();
            return true;
        }
        public MDSVariableTable[] Snapshot()
        {
            lock (_gate) return _tables.Values.ToArray();
        }
    }
}
