using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;

namespace CxVisual.Mechanism.Data.RuntimeData
{
    /// <summary>运行数据堆的第一阶段实现：容器持有值，不公开引用计数或通用Carrier。</summary>
    public sealed class MDSDataHeap
    {
        private sealed class Entry
        {
            internal readonly object Value;
            internal readonly Type Type;
            internal Entry(object value, Type type) { Value = value; Type = type; }
        }

        private static long _nextOrder;
        private readonly long _order = Interlocked.Increment(ref _nextOrder);
        private readonly object _gate = new object();
        private readonly Dictionary<string, Entry> _values =
            new Dictionary<string, Entry>(StringComparer.Ordinal);

        private static void CheckName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("数据名不能为空", "name");
        }

        private static bool IsSimpleValue(Type type)
        {
            return type.IsPrimitive || type.IsEnum ||
                   type == typeof(string) || type == typeof(decimal) ||
                   type == typeof(DateTime) || type == typeof(TimeSpan) ||
                   type == typeof(Guid) || type == typeof(DateTimeOffset);
        }

        private static object Freeze(object value, Type type)
        {
            if (value == null) return null;
            if (type == typeof(byte[])) return ((byte[])value).Clone();
            if (IsSimpleValue(type)) return value;
            throw new NotSupportedException(
                "可变或未知对象不能直接共享；请使用受控的数据类型适配器：" + type.FullName);
        }

        private static object Read(Entry entry)
        {
            return Freeze(entry.Value, entry.Type);
        }

        public int Count { get { lock (_gate) return _values.Count; } }

        public void Set<T>(string name, T value)
        {
            CheckName(name);
            Type type = value == null ? typeof(T) : value.GetType();
            object frozen = Freeze(value, type);
            lock (_gate)
            {
                if (_values.ContainsKey(name))
                    throw new InvalidOperationException("数据名已存在：" + name);
                _values.Add(name, new Entry(frozen, type));
            }
        }

        public T Get<T>(string name)
        {
            CheckName(name);
            lock (_gate)
            {
                Entry entry;
                if (!_values.TryGetValue(name, out entry))
                    throw new KeyNotFoundException("找不到数据：" + name);
                if (entry.Value != null && !(entry.Value is T))
                    throw new InvalidCastException("数据类型不匹配：" + name);
                if (entry.Value == null && typeof(T).IsValueType &&
                    Nullable.GetUnderlyingType(typeof(T)) == null)
                    throw new InvalidCastException("null不能赋值给值类型：" + name);
                return (T)Read(entry);
            }
        }

        /// <summary>仅返回名称快照，不暴露底层数据，也不加载外部文件。</summary>
        public IList<string> Search(string prefix = "")
        {
            if (prefix == null) throw new ArgumentNullException("prefix");
            lock (_gate)
            {
                List<string> names = new List<string>();
                foreach (string name in _values.Keys)
                    if (name.StartsWith(prefix, StringComparison.Ordinal)) names.Add(name);
                names.Sort(StringComparer.Ordinal);
                return new ReadOnlyCollection<string>(names);
            }
        }

        public void Remove(string name)
        {
            CheckName(name);
            lock (_gate)
            {
                if (!_values.Remove(name))
                    throw new KeyNotFoundException("找不到数据：" + name);
            }
        }

        /// <summary>两堆原子移动，目标同名默认拒绝；不允许未经验证的覆盖。</summary>
        public void MoveTo(string sourceName, MDSDataHeap target, string targetName)
        {
            Transfer(sourceName, target, targetName, false);
        }

        /// <summary>独立复制数据；byte[]被克隆，简单值按值语义复制。</summary>
        public void CopyTo(string sourceName, MDSDataHeap target, string targetName)
        {
            Transfer(sourceName, target, targetName, true);
        }

        private void Transfer(string sourceName, MDSDataHeap target,
            string targetName, bool copy)
        {
            CheckName(sourceName);
            CheckName(targetName);
            if (target == null) throw new ArgumentNullException("target");
            MDSDataHeap first = _order < target._order ? this : target;
            MDSDataHeap second = _order < target._order ? target : this;
            lock (first._gate)
            {
                lock (second._gate)
                {
                    Entry source;
                    if (!_values.TryGetValue(sourceName, out source))
                        throw new KeyNotFoundException("找不到源数据：" + sourceName);
                    if (target._values.ContainsKey(targetName))
                        throw new InvalidOperationException("目标数据名已存在：" + targetName);
                    Entry output = copy ? new Entry(Freeze(source.Value, source.Type), source.Type) : source;
                    target._values.Add(targetName, output);
                    if (!copy) _values.Remove(sourceName);
                }
            }
        }
    }
}
