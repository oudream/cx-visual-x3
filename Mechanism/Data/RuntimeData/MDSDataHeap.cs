using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using CxVisual.Foundation.Data.Types;

namespace CxVisual.Mechanism.Data.RuntimeData
{
    /// <summary>运行数据堆的第一阶段实现：容器持有值，不公开引用计数或通用Carrier。</summary>
    public sealed partial class MDSDataHeap : IDisposable
    {
        private sealed class MDSDataEntry
        {
            internal readonly object Value;
            internal readonly Type Type;
            internal MDSDataEntry(object value, Type type) { Value = value; Type = type; }
        }

        private static long _nextOrder;
        private readonly long _order = Interlocked.Increment(ref _nextOrder);
        private readonly object _gate = new object();
        public FDTDataAdapterRegistry Adapters { get; private set; }
        public MDSDataHeap(FDTDataAdapterRegistry adapters = null)
        { Adapters = adapters ?? new FDTDataAdapterRegistry(); }
        private object CaptureValue(object value)
        { return FDTDataValueSafety.Capture(value, Adapters); }
        private bool _closed;
        internal object SyncRoot { get { return _gate; } }
        private void EnsureOpen()
        { if (_closed) throw new ObjectDisposedException("MDSDataHeap"); }
        private readonly Dictionary<string, MDSDataEntry> _values =
            new Dictionary<string, MDSDataEntry>(StringComparer.Ordinal);

        private static void CheckName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("数据名不能为空", "name");
        }

        private object Freeze(object value, Type type)
        { return CaptureValue(value); }

        private object Read(MDSDataEntry entry)
        { return CaptureValue(entry.Value); }

        public int Count { get { lock (_gate) { EnsureOpen(); return _values.Count + _files.Count; } } }

        public void Set<T>(string name, T value)
        {
            CheckName(name);
            Type type = value == null ? typeof(T) : value.GetType();
            object frozen = Freeze(value, type);
            lock (_gate)
            {
                EnsureOpen();
                if (_values.ContainsKey(name) || _files.ContainsKey(name))
                    throw new MDSDataException(MDSDataErrorCode.NameConflict, "数据名已存在：" + name, name);
                _values.Add(name, new MDSDataEntry(frozen, type));
            }
        }

        public T Get<T>(string name)
        {
            CheckName(name);
            lock (_gate)
            {
                EnsureOpen();
                MDSDataEntry entry;
                if (!_values.TryGetValue(name, out entry))
                {
                    MDSDataFileEntry file;
                    if (!_files.TryGetValue(name, out file))
                        throw new KeyNotFoundException("找不到数据：" + name);
                    if (file.State != MDSDataLoadState.Loaded)
                        throw new InvalidOperationException("文件数据尚未加载：" + name);
                    entry = new MDSDataEntry(file.Cache, file.Type);
                }
                if (entry.Value != null && !(entry.Value is T))
                    throw new InvalidCastException("数据类型不匹配：" + name);
                if (entry.Value == null)
                {
                    if (typeof(T).IsValueType && Nullable.GetUnderlyingType(typeof(T)) == null)
                        throw new InvalidCastException("null不能赋值给值类型：" + name);
                    if (!typeof(T).IsAssignableFrom(entry.Type) &&
                        Nullable.GetUnderlyingType(typeof(T)) != entry.Type)
                        throw new InvalidCastException("null条目的声明类型不匹配：" + name);
                }
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
                foreach (string name in _files.Keys)
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
                MDSDataFileEntry file;
                if (_files.TryGetValue(name, out file))
                {
                    if (file.State == MDSDataLoadState.Loading)
                        throw new MDSDataException(MDSDataErrorCode.Busy, "加载中不可移除：" + name, name);
                    _files.Remove(name);
                }
                else if (!_values.Remove(name))
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
                if (!_values.ContainsKey(name) && !_files.ContainsKey(name))
                    throw new KeyNotFoundException("需要覆盖的目标不存在：" + name);
                MDSDataFileEntry file;
                if (_files.TryGetValue(name, out file))
                {
                    if (file.State == MDSDataLoadState.Loading)
                        throw new MDSDataException(MDSDataErrorCode.Busy, "加载中不可替换：" + name, name);
                    _files.Remove(name);
                }
                _values[name] = new MDSDataEntry(frozen, type);
            }
        }

        /// <summary>仅供同一Mechanism.Data程序集的容器级转移使用；绝不公开裸数据。</summary>
        internal object TransferPeekOwned(string name, out Type declaredType)
        {
            CheckName(name);
            lock (_gate)
            {
                EnsureOpen();
                MDSDataEntry entry;
                if (!_values.TryGetValue(name, out entry))
                {
                    MDSDataFileEntry file;
                    if (!_files.TryGetValue(name, out file))
                        throw new KeyNotFoundException("找不到源数据：" + name);
                    if (file.State != MDSDataLoadState.Loaded)
                        throw new InvalidOperationException("未加载文件不能作为值移动：" + name);
                    declaredType = file.Type;
                    return file.Cache;
                }
                declaredType = entry.Type;
                return entry.Value;
            }
        }

        /// <summary>调用方须持有Scope及Heap的受控事务锁，数据已经经过安全接管。</summary>
        internal void TransferStoreOwned(string name, object safeOwnedValue,
            Type declaredType, bool replace)
        {
            CheckName(name);
            if (declaredType == null) throw new ArgumentNullException("declaredType");
            lock (_gate)
            {
                EnsureOpen();
                if ((_values.ContainsKey(name) || _files.ContainsKey(name)) && !replace)
                    throw new MDSDataException(MDSDataErrorCode.NameConflict,
                        "目标Heap名称已存在：" + name, name);
                MDSDataFileEntry file;
                if (_files.TryGetValue(name, out file))
                {
                    if (file.State == MDSDataLoadState.Loading)
                        throw new MDSDataException(MDSDataErrorCode.Busy, "目标加载中：" + name, name);
                    _files.Remove(name);
                }
                _values[name] = new MDSDataEntry(safeOwnedValue, declaredType);
            }
        }

        public bool Contains(string name)
        {
            CheckName(name);
            lock (_gate) { EnsureOpen(); return _values.ContainsKey(name) || _files.ContainsKey(name); }
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
                    MDSDataEntry source;
                    MDSDataFileEntry file;
                    bool isFile = _files.TryGetValue(sourceName, out file);
                    if (!isFile && !_values.TryGetValue(sourceName, out source))
                        throw new KeyNotFoundException("找不到源数据：" + sourceName);
                    if ((target._values.ContainsKey(targetName) || target._files.ContainsKey(targetName)) && !replace)
                        throw new MDSDataException(MDSDataErrorCode.NameConflict, "目标数据名已存在：" + targetName, targetName);
                    MDSDataFileEntry targetFile;
                    if (target._files.TryGetValue(targetName, out targetFile) &&
                        targetFile.State == MDSDataLoadState.Loading)
                        throw new MDSDataException(MDSDataErrorCode.Busy, "目标文件加载中：" + targetName, targetName);
                    if (isFile)
                    {
                        if (file.State == MDSDataLoadState.Loading)
                            throw new MDSDataException(MDSDataErrorCode.Busy, "来源文件加载中：" + sourceName, sourceName);
                        MDSDataFileEntry outputFile = copy ? file.CopyLocation() : file;
                        target._values.Remove(targetName);
                        target._files[targetName] = outputFile;
                        if (!copy) _files.Remove(sourceName);
                    }
                    else
                    {
                        MDSDataEntry output = copy
                            ? new MDSDataEntry(Freeze(source.Value, source.Type), source.Type) : source;
                        target._files.Remove(targetName);
                        target._values[targetName] = output;
                        if (!copy) _values.Remove(sourceName);
                    }
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
                _files.Clear();
            }
        }
    }
}
