using System;
using System.Collections.Generic;
using System.Linq;

namespace CxVisual.Mechanism.Data.Variables
{
    // 基于ReferTo与x2实际源码收敛：变量对象唯一持有运行值；表仅负责索引和兼容API。
    public enum MDSVariableQuality { Good, Bad, Uncertain, Stale }

    public sealed class MDSVariableDefinition
    {
        private readonly object _defaultValue;

        // 保留原四参数构造用法。显式VariableId用于绑定工程稳定身份；
        // 未提供时沿用名称作为旧API兼容身份，不声称改名后仍稳定。
        public MDSVariableDefinition(string name, Type valueType, string unit = null,
            bool writable = true, object defaultValue = null, string variableId = null)
        {
            if (string.IsNullOrWhiteSpace(name) || valueType == null || valueType == typeof(void))
                throw new ArgumentException("变量名称/类型无效");
            Name = name.Trim();
            ValueType = valueType;
            Unit = unit;
            Writable = writable;
            VariableId = string.IsNullOrWhiteSpace(variableId) ? Name : variableId.Trim();
            if (defaultValue != null && !valueType.IsInstanceOfType(defaultValue))
                throw new ArgumentException("默认值类型不匹配");
            _defaultValue = MDSVariableValueRules.Copy(defaultValue);
            HasDefaultValue = defaultValue != null;
        }

        public string Name { get; private set; }
        public string VariableId { get; private set; }
        public Type ValueType { get; private set; }
        public string Unit { get; private set; }
        public bool Writable { get; private set; }
        public bool HasDefaultValue { get; private set; }
        public object DefaultValue { get { return MDSVariableValueRules.Copy(_defaultValue); } }
    }

    // 保留现有Get/TryGet/Update/Sample/VariableChanged签名和异常行为。
    public sealed class MDSVariableTable : IMDSVariableTable
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, IMDSVariableEntry> _items =
            new Dictionary<string, IMDSVariableEntry>(StringComparer.Ordinal);

        public MDSVariableTable(string scopeId)
        {
            if (string.IsNullOrWhiteSpace(scopeId)) throw new ArgumentException("ScopeId 不能为空");
            ScopeId = scopeId.Trim();
        }

        public string ScopeId { get; private set; }
        public event Action<string> VariableChanged;

        private void ForwardChanged(string name)
        {
            Action<string> handler = VariableChanged;
            if (handler != null) handler(name);
        }

        public void Define(MDSVariableDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException("definition");
            lock (_gate)
            {
                if (_items.ContainsKey(definition.Name))
                    throw new ArgumentException("变量重复: " + definition.Name);
                Type runtimeType = typeof(MDSVariable<>).MakeGenericType(definition.ValueType);
                IMDSVariableEntry variable = (IMDSVariableEntry)Activator.CreateInstance(runtimeType, definition);
                // 只在定义时反射一次，日常更新从已解析的强类型对象直接写。
                _items.Add(definition.Name, variable);
                variable.Attach(ForwardChanged);
            }
        }

        public bool Remove(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            lock (_gate)
            {
                IMDSVariableEntry item;
                if (!_items.TryGetValue(name, out item)) return false;
                item.Detach();
                return _items.Remove(name);
            }
        }

        public bool Contains(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            lock (_gate) return _items.ContainsKey(name);
        }

        public MDSVariableDefinition[] Definitions()
        {
            lock (_gate) return _items.Values.Select(x => x.Definition).ToArray();
        }

        // 预绑定强类型句柄，无后续字典查找；是同一权威状态，不是缓存副本。
        public MDSVariable<T> Resolve<T>(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("变量名称无效");
            lock (_gate)
            {
                IMDSVariableEntry variable;
                if (!_items.TryGetValue(name, out variable))
                    throw new KeyNotFoundException("变量未定义: " + name);
                MDSVariable<T> typed = variable as MDSVariable<T>;
                if (typed == null) throw new InvalidCastException("变量类型不匹配: " + name);
                return typed;
            }
        }

        public T Get<T>(string name)
        {
            T value;
            if (!TryGet<T>(name, out value))
                throw new InvalidOperationException("变量未定义、未初始化或类型不兼容: " + name);
            return value;
        }

        public bool TryGet<T>(string name, out T value)
        {
            value = default(T);
            if (string.IsNullOrWhiteSpace(name)) return false;
            MDSVariable<T> typed;
            lock (_gate)
            {
                IMDSVariableEntry entry;
                if (!_items.TryGetValue(name, out entry)) return false;
                typed = entry as MDSVariable<T>;
                if (typed == null) return false;
            }
            return typed.TryGet(out value);
        }

        public void Update<T>(string name, T value)
        {
            Resolve<T>(name).Update(value);
        }

        public MDSVariableSample<T> Sample<T>(string name)
        {
            try { return Resolve<T>(name).Sample(); }
            catch (InvalidCastException)
            {
                throw new KeyNotFoundException("变量不存在或类型不匹配: " + name);
            }
        }
    }
}
