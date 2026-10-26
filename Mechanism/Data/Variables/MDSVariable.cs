using System;
using System.Collections.Generic;

namespace CxVisual.Mechanism.Data.Variables
{
    // 一个运行变量只保留一份状态；Table/Registry 不得复制其 CurrentValue。
    // 仅负责非泛型索引；状态仍只保存在MDSVariable<T>中。
    public abstract class MDSVariableBase
    {
        internal abstract MDSVariableDefinition EntryDefinition { get; }
        internal abstract void Attach(Action<string> notify);
        internal abstract void Observe(Action<MDSVariableChange> callback);
        internal abstract object ReadBoxed();
        internal abstract void UpdateBoxed(object value);
        internal abstract void RefreshProviderBoxed(object value, MDSVariableQuality quality, string source);
        internal abstract void Detach();
    }

    internal static class MDSVariableValueRules
    {
        internal static object Copy(object value)
        {
            if (value == null) return null;
            byte[] bytes = value as byte[];
            if (bytes != null) return (byte[])bytes.Clone();
            Type type = value.GetType();
            if (type.IsValueType || value is string) return value;
            throw new NotSupportedException("复杂可变引用类型需要先登记 Foundation.Data 值适配器");
        }

        internal static T CopyTyped<T>(T value)
        {
            object copied = Copy(value);
            return copied == null ? default(T) : (T)copied;
        }

        internal static bool Equal<T>(T left, T right)
        {
            byte[] first = (object)left as byte[];
            byte[] second = (object)right as byte[];
            if (first != null && second != null)
            {
                if (first.Length != second.Length) return false;
                for (int i = 0; i < first.Length; i++)
                    if (first[i] != second[i]) return false;
                return true;
            }
            return EqualityComparer<T>.Default.Equals(left, right);
        }
    }

    /// <summary>单次锁内抓取的只读快照；byte[] 入参和出参均防御复制。</summary>
    public sealed class MDSVariableSnapshot<T>
    {
        private readonly T _value;
        private readonly T _previous;
        private readonly T _defaultValue;

        internal MDSVariableSnapshot(MDSVariableDefinition definition,
            T value, T previous, T defaultValue, bool hasValue, bool hasPrevious,
            MDSVariableQuality quality, DateTime refreshUtc, DateTime changeUtc,
            string source, long revision)
        {
            VariableId = definition.VariableId;
            Name = definition.Name;
            _value = MDSVariableValueRules.CopyTyped(value);
            _previous = MDSVariableValueRules.CopyTyped(previous);
            _defaultValue = MDSVariableValueRules.CopyTyped(defaultValue);
            HasValue = hasValue;
            HasPreviousValue = hasPrevious;
            HasDefaultValue = definition.HasDefaultValue;
            Quality = quality;
            RefreshUtc = refreshUtc;
            ChangeUtc = changeUtc;
            Source = source;
            Revision = revision;
        }

        public string VariableId { get; private set; }
        public string Name { get; private set; }
        public T CurrentValue { get { return MDSVariableValueRules.CopyTyped(_value); } }
        public T PreviousValue { get { return MDSVariableValueRules.CopyTyped(_previous); } }
        public T DefaultValue { get { return MDSVariableValueRules.CopyTyped(_defaultValue); } }
        public bool HasValue { get; private set; }
        public bool HasPreviousValue { get; private set; }
        public bool HasDefaultValue { get; private set; }
        public MDSVariableQuality Quality { get; private set; }
        public DateTime RefreshUtc { get; private set; }
        public DateTime ChangeUtc { get; private set; }
        public string Source { get; private set; }
        public long Revision { get; private set; }
    }

    /// <summary>
    /// 通用强类型变量：吸收x2 Variable&lt;T&gt;/RuntimeState语义。
    /// 更新热路径不需要表字典或反射；变化派发尚沿用Table的兼容同步事件。
    /// </summary>
    public sealed class MDSVariable<T> : MDSVariableBase
    {
        private readonly object _gate = new object();
        private readonly MDSVariableDefinition _definition;
        private readonly T _defaultValue;
        private T _current;
        private T _previous;
        private bool _hasValue;
        private bool _hasPrevious;
        private DateTime _refreshUtc;
        private DateTime _changeUtc;
        private MDSVariableQuality _quality = MDSVariableQuality.Uncertain;
        private string _source;
        private long _revision;
        private bool _removed;
        private Action<string> _notify;
        private Action<MDSVariableChange> _observer;

        public MDSVariable(MDSVariableDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException("definition");
            if (definition.ValueType != typeof(T)) throw new InvalidCastException("变量类型与声明不一致");
            _definition = definition;
            object defaultValue = definition.DefaultValue;
            _defaultValue = defaultValue == null ? default(T) : MDSVariableValueRules.CopyTyped((T)defaultValue);
        }

        // 点表/Provider绑定时一次解析具体变量，后续避免反射和表全量扫描。
        internal override object ReadBoxed() { return Get(); }
        internal override void UpdateBoxed(object value)
        {
            if (value == null && typeof(T).IsValueType) throw new InvalidCastException("变量类型不匹配");
            if (value != null && value.GetType() != typeof(T)) throw new InvalidCastException("变量类型不匹配");
            Update((T)value);
        }
        internal override void RefreshProviderBoxed(object value, MDSVariableQuality quality, string source)
        {
            if (value == null && typeof(T).IsValueType) throw new InvalidCastException("变量类型不匹配");
            if (value != null && value.GetType() != typeof(T)) throw new InvalidCastException("变量类型不匹配");
            RefreshFromProvider((T)value, quality, source);
        }
        public MDSVariableDefinition Definition { get { return _definition; } }
        internal override MDSVariableDefinition EntryDefinition { get { return _definition; } }

        internal override void Attach(Action<string> notify)
        {
            lock (_gate)
            {
                if (_removed) throw new ObjectDisposedException("MDSVariable");
                _notify = notify;
            }
        }

        internal override void Observe(Action<MDSVariableChange> callback)
        {
            lock (_gate) { if (!_removed) _observer = callback; }
        }

        internal override void Detach()
        {
            lock (_gate) { _removed = true; _notify = null; _observer = null; }
        }

        public bool TryGet(out T value)
        {
            lock (_gate)
            {
                value = default(T);
                if (_removed || !_hasValue) return false;
                value = MDSVariableValueRules.CopyTyped(_current);
                return true;
            }
        }

        public T Get()
        {
            T value;
            if (!TryGet(out value)) throw new InvalidOperationException("变量未初始化或已失效: " + _definition.Name);
            return value;
        }

        public MDSVariableSnapshot<T> Snapshot()
        {
            lock (_gate)
            {
                if (_removed) throw new ObjectDisposedException("MDSVariable");
                return new MDSVariableSnapshot<T>(_definition, _current, _previous, _defaultValue,
                    _hasValue, _hasPrevious, _quality, _refreshUtc, _changeUtc, _source, _revision);
            }
        }

        public MDSVariableSample<T> Sample()
        {
            lock (_gate)
            {
                if (_removed) throw new ObjectDisposedException("MDSVariable");
                return new MDSVariableSample<T>(_hasValue ? MDSVariableValueRules.CopyTyped(_current) : default(T),
                    _refreshUtc, _hasValue);
            }
        }

        // 原有Table.Update仍然调用此方法，保证只有变量本身拥有值和状态。
        public void Update(T value)
        {
            Commit(value, MDSVariableQuality.Good, null, false, null);
        }
        public MDSVariableUpdateResult Update(T value, MDSVariableUpdateOptions options)
        {
            if (options == null) throw new ArgumentNullException("options");
            return Commit(value, options.Quality, options.Source, false, options.ExpectedRevision);
        }
        public bool TryUpdate(T value, long expectedRevision, out MDSVariableUpdateResult result)
        {
            try
            {
                result = Commit(value, MDSVariableQuality.Good, null, false, expectedRevision);
                return true;
            }
            catch (MDSVariableVersionConflictException)
            {
                result = null; return false;
            }
        }

        // 后续Provider接入从同一Owner提交，允许Provider刷新只读变量。
        internal void RefreshFromProvider(T value, MDSVariableQuality quality, string source)
        {
            Commit(value, quality, source, true, null);
        }

        private MDSVariableUpdateResult Commit(T value, MDSVariableQuality quality,
            string source, bool fromProvider, long? expectedRevision)
        {
            object copied = MDSVariableValueRules.Copy(value);
            if (!Enum.IsDefined(typeof(MDSVariableQuality), quality))
                throw new ArgumentOutOfRangeException("quality");
            Action<string> notify;
            Action<MDSVariableChange> observer;
            MDSVariableChange captured = null;
            MDSVariableUpdateResult result;
            lock (_gate)
            {
                if (_removed) throw new ObjectDisposedException("MDSVariable");
                if (!fromProvider && !_definition.Writable)
                    throw new InvalidOperationException("变量为只读: " + _definition.Name);
                if (expectedRevision.HasValue && _revision != expectedRevision.Value)
                    throw new MDSVariableVersionConflictException(_definition.VariableId,
                        expectedRevision.Value, _revision);
                T next = (T)copied;
                bool changed = !_hasValue || !MDSVariableValueRules.Equal(_current, next);
                bool qualityChanged = _quality != quality;
                bool sourceChanged = source != null && source != _source;
                DateTime now = DateTime.UtcNow;
                if (changed)
                {
                    if (_hasValue)
                    {
                        _previous = _current;
                        _hasPrevious = true;
                    }
                    _current = next;
                    _hasValue = true;
                    _changeUtc = now;
                }
                _quality = quality;
                _source = source ?? _source;
                _refreshUtc = now;
                _revision = checked(_revision + 1);
                result = new MDSVariableUpdateResult(_definition.VariableId,
                    changed, _revision, _quality, now);
                notify = _notify;
                observer = _observer;
                if (observer != null)
                {
                    MDSVariableChangeKind kind = changed ? MDSVariableChangeKind.Value : MDSVariableChangeKind.Refresh;
                    if (qualityChanged) kind |= MDSVariableChangeKind.Quality;
                    if (sourceChanged) kind |= MDSVariableChangeKind.Source;
                    captured = new MDSVariableChange(_definition.VariableId, _definition.Name,
                        _current, _previous, _hasPrevious, _quality, _source, now, _revision, kind);
                }
            }
            // 高速路径只有在订阅变化捕获时才创建不可变快照，旧通知继续兼容。
            if (observer != null) observer(captured);
            if (notify != null) notify(_definition.Name);
            return result;
        }
    }
}
