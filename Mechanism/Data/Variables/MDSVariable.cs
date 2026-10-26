using System;
using System.Collections.Generic;

namespace CxVisual.Mechanism.Data.Variables
{
    // 一个运行变量只保留一份状态；Table/Registry 不得复制其 CurrentValue。
    internal interface IMDSVariableEntry
    {
        MDSVariableDefinition Definition { get; }
        void Attach(Action<string> notify);
        void Detach();
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

        internal static bool Equal<T>(T left, T right)
        {
            byte[] first = left as byte[];
            byte[] second = right as byte[];
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
            _value = (T)MDSVariableValueRules.Copy(value);
            _previous = (T)MDSVariableValueRules.Copy(previous);
            _defaultValue = (T)MDSVariableValueRules.Copy(defaultValue);
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
        public T CurrentValue { get { return (T)MDSVariableValueRules.Copy(_value); } }
        public T PreviousValue { get { return (T)MDSVariableValueRules.Copy(_previous); } }
        public T DefaultValue { get { return (T)MDSVariableValueRules.Copy(_defaultValue); } }
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
    public sealed class MDSVariable<T> : IMDSVariableEntry
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

        public MDSVariable(MDSVariableDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException("definition");
            if (definition.ValueType != typeof(T)) throw new InvalidCastException("变量类型与声明不一致");
            _definition = definition;
            _defaultValue = (T)MDSVariableValueRules.Copy(definition.DefaultValue);
        }

        public MDSVariableDefinition Definition { get { return _definition; } }

        public void Attach(Action<string> notify)
        {
            lock (_gate)
            {
                if (_removed) throw new ObjectDisposedException("MDSVariable");
                _notify = notify;
            }
        }

        void IMDSVariableEntry.Detach()
        {
            lock (_gate) { _removed = true; _notify = null; }
        }

        public bool TryGet(out T value)
        {
            lock (_gate)
            {
                value = default(T);
                if (_removed || !_hasValue) return false;
                value = (T)MDSVariableValueRules.Copy(_current);
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
                return new MDSVariableSample<T>(_hasValue ? (T)MDSVariableValueRules.Copy(_current) : default(T),
                    _refreshUtc, _hasValue);
            }
        }

        // 原有Table.Update仍然调用此方法，保证只有变量本身拥有值和状态。
        public void Update(T value)
        {
            Commit(value, MDSVariableQuality.Good, null, false);
        }

        // 后续Provider接入从同一Owner提交，允许Provider刷新只读变量。
        internal void RefreshFromProvider(T value, MDSVariableQuality quality, string source)
        {
            Commit(value, quality, source, true);
        }

        private void Commit(T value, MDSVariableQuality quality, string source, bool fromProvider)
        {
            object copied = MDSVariableValueRules.Copy(value);
            if (!Enum.IsDefined(typeof(MDSVariableQuality), quality))
                throw new ArgumentOutOfRangeException("quality");
            Action<string> notify;
            lock (_gate)
            {
                if (_removed) throw new ObjectDisposedException("MDSVariable");
                if (!fromProvider && !_definition.Writable)
                    throw new InvalidOperationException("变量为只读: " + _definition.Name);
                T next = (T)copied;
                bool changed = !_hasValue || !MDSVariableValueRules.Equal(_current, next);
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
                notify = _notify;
            }
            // 兼容旧事件的调用时机；P0-3将替换为有界异步派发。
            if (notify != null) notify(_definition.Name);
        }
    }
}
