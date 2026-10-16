using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;

namespace CxVisual.Mechanism.Data.RuntimeData
{
    /// <summary>运行数据堆的第一阶段实现：容器持有值，不公开引用计数或通用Carrier。</summary>
    public sealed class MDSDataHeap : IDisposable
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
        private bool _closed;
        internal object SyncRoot { get { return _gate; } }
        private void EnsureOpen()
        { if (_closed) throw new ObjectDisposedException("MDSDataHeap"); }
        private readonly Dictionary<string, Entry> _values =
            new Dictionary<string, Entry>(StringComparer.Ordinal);

        private static void CheckName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("数据名不能为空", "name");
        }

        private static object Freeze(object value, Type type)
        { return MDSDataSafety.Capture(value); }

        private static object Read(Entry entry)
        {
            return Freeze(entry.Value, entry.Type);
        }

        public int Count { get { lock (_gate) { EnsureOpen(); return _values.Count; } } }

        public void Set<T>(string name, T value)
        {
            CheckName(name);
            Type type = value == null ? typeof(T) : value.GetType();
            object frozen = Freeze(value, type);
            lock (_gate)
            {
                EnsureOpen();
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
                EnsureOpen();
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
                EnsureOpen();
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
                EnsureOpen();
                if (!_values.Remove(name))
                    throw new KeyNotFoundException("找不到数据：" + name);
            }
        }

        /// <summary>显式Replace；普通Set仍保持单次赋值，不能隐式覆盖。</summary>
        public void Replace<T>(string name, T value)
        {
            CheckName(name);
            Type type = value == null ? typeof(T) : value.GetType();
            object frozen = Freeze(value, type);
            lock (_gate)
            {
                EnsureOpen();
                if (!_values.ContainsKey(name))
                    throw new KeyNotFoundException("需要覆盖的目标不存在：" + name);
                _values[name] = new Entry(frozen, type);
            }
        }

        public bool Contains(string name)
        {
            CheckName(name);
            lock (_gate) { EnsureOpen(); return _values.ContainsKey(name); }
        }

        /// <summary>逻辑原子Move；不复制已由Heap拥有的值，目标同名默认拒绝。</summary>
        public void MoveTo(string sourceName, MDSDataHeap target, string targetName,
            bool replace = false)
        {
            Transfer(sourceName, target, targetName, false, replace);
        }

        /// <summary>Copy对可变数据生成独立副本，不允许未知对象浅复制。</summary>
        public void CopyTo(string sourceName, MDSDataHeap target, string targetName,
            bool replace = false)
        {
            Transfer(sourceName, target, targetName, true, replace);
        }

        private void Transfer(string sourceName, MDSDataHeap target,
            string targetName, bool copy, bool replace)
        {
            CheckName(sourceName);
            CheckName(targetName);
            if (target == null) throw new ArgumentNullException("target");
            if (object.ReferenceEquals(this, target) &&
                string.Equals(sourceName, targetName, StringComparison.Ordinal))
                throw new ArgumentException("来源和目标是同一条数据，不能原地Move/Copy");
            MDSDataHeap first = _order < target._order ? this : target;
            MDSDataHeap second = _order < target._order ? target : this;
            lock (first._gate)
            {
                lock (second._gate)
                {
                    EnsureOpen();
                    target.EnsureOpen();
                    Entry source;
                    if (!_values.TryGetValue(sourceName, out source))
                        throw new KeyNotFoundException("找不到源数据：" + sourceName);
                    if (target._values.ContainsKey(targetName) && !replace)
                        throw new InvalidOperationException("目标数据名已存在：" + targetName);
                    Entry output = copy ? new Entry(Freeze(source.Value, source.Type), source.Type) : source;
                    target._values[targetName] = output;
                    if (!copy) _values.Remove(sourceName);
                }
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_closed) return;
                _closed = true;
                _values.Clear();
            }
        }
    }
}
