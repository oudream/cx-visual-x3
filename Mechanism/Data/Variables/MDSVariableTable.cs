using System;
using System.Collections.Generic;
using System.Linq;

namespace CxVisual.Mechanism.Data.Variables
{
    // 基于 ReferTo/VariableInfo/VariableTable 的作用域、类型和变更通知语义直接重构；
    // 不保留进程级静态 VariableManager，且不复制 Flow Heap/Stack。
    public enum MDSVariableQuality { Good, Bad, Uncertain, Stale }

    public sealed class MDSVariableDefinition
    {
        public MDSVariableDefinition(string name, Type valueType, string unit = null, bool writable = true)
        {
            if (string.IsNullOrWhiteSpace(name) || valueType == null || valueType == typeof(void))
                throw new ArgumentException("变量名称/类型无效");
            Name = name.Trim(); ValueType = valueType; Unit = unit; Writable = writable;
        }
        public string Name { get; private set; }
        public Type ValueType { get; private set; }
        public string Unit { get; private set; }
        public bool Writable { get; private set; }
    }

    public sealed class MDSVariableTable : IMDSVariableTable
    {
        private sealed class Entry
        {
            internal MDSVariableDefinition Definition;
            internal object Value;
            internal bool Initialized;
            internal DateTime UpdatedUtc;
            internal MDSVariableQuality Quality;
        }
        private readonly object _gate = new object();
        private readonly Dictionary<string, Entry> _items =
            new Dictionary<string, Entry>(StringComparer.Ordinal);
        public MDSVariableTable(string scopeId)
        {
            if (string.IsNullOrWhiteSpace(scopeId)) throw new ArgumentException("ScopeId 不能为空");
            ScopeId = scopeId.Trim();
        }
        public string ScopeId { get; private set; }
        public event Action<string> VariableChanged;
        private static object Capture(object value)
        {
            if (value == null) return null;
            byte[] data = value as byte[];
            if (data != null) return (byte[])data.Clone();
            Type type = value.GetType();
            if (type.IsValueType || value is string) return value;
            // 对可变的复杂类型需要 Foundation.Data 显式 Adapter，不允许外部引用逃逸。
            throw new NotSupportedException("复杂可变引用类型需要先登记 Foundation.Data 值适配器");
        }
        public void Define(MDSVariableDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException("definition");
            lock (_gate)
            {
                if (_items.ContainsKey(definition.Name)) throw new ArgumentException("变量重复: " + definition.Name);
                _items.Add(definition.Name, new Entry { Definition = definition, Quality = MDSVariableQuality.Uncertain });
            }
        }
        public bool Remove(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            lock (_gate) return _items.Remove(name);
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
        public T Get<T>(string name)
        {
            T value;
            if (!TryGet<T>(name, out value)) throw new InvalidOperationException("变量未定义、未初始化或类型不兼容: " + name);
            return value;
        }
        public bool TryGet<T>(string name, out T value)
        {
            value = default(T);
            if (string.IsNullOrWhiteSpace(name)) return false;
            lock (_gate)
            {
                Entry entry;
                if (!_items.TryGetValue(name, out entry) || !entry.Initialized || entry.Definition.ValueType != typeof(T))
                    return false;
                value = (T)Capture(entry.Value);
                return true;
            }
        }
        public void Update<T>(string name, T value)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("变量名称无效");
            object copy = Capture(value);
            lock (_gate)
            {
                Entry entry;
                if (!_items.TryGetValue(name, out entry)) throw new KeyNotFoundException("变量未定义: " + name);
                if (entry.Definition.ValueType != typeof(T)) throw new InvalidCastException("变量类型不匹配: " + name);
                if (!entry.Definition.Writable) throw new InvalidOperationException("变量为只读: " + name);
                entry.Value = copy; entry.Initialized = true; entry.UpdatedUtc = DateTime.UtcNow;
                entry.Quality = MDSVariableQuality.Good;
            }
            Action<string> handler = VariableChanged;
            if (handler != null) handler(name); // 不能在锁内调用外部事件
        }
        public MDSVariableSample<T> Sample<T>(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("变量名称无效");
            lock (_gate)
            {
                Entry entry;
                if (!_items.TryGetValue(name, out entry) || entry.Definition.ValueType != typeof(T))
                    throw new KeyNotFoundException("变量不存在或类型不匹配: " + name);
                return new MDSVariableSample<T>(entry.Initialized ? (T)Capture(entry.Value) : default(T),
                    entry.UpdatedUtc, entry.Initialized);
            }
        }
    }
}
