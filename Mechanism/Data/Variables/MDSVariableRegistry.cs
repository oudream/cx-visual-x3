using System;
using System.Collections.Generic;
using System.Linq;

namespace CxVisual.Mechanism.Data.Variables
{
    // 按工程/Host 会话实例建立，禁止全局静态 VariableManager。
    public sealed class MDSVariableRegistry
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, MDSVariableTable> _tables =
            new Dictionary<string, MDSVariableTable>(StringComparer.Ordinal);
        public void Register(MDSVariableTable table)
        {
            if (table == null) throw new ArgumentNullException("table");
            lock (_gate)
            {
                if (_tables.ContainsKey(table.ScopeId)) throw new ArgumentException("变量作用域重复: " + table.ScopeId);
                _tables.Add(table.ScopeId, table);
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
        public bool Remove(string scopeId)
        {
            if (string.IsNullOrWhiteSpace(scopeId)) return false;
            lock (_gate) return _tables.Remove(scopeId);
        }
        public MDSVariableTable[] Snapshot()
        {
            lock (_gate) return _tables.Values.ToArray();
        }
    }
}
