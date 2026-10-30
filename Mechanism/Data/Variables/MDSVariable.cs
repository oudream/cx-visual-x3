using System;
using System.Collections.Generic;

namespace CxVisual.Mechanism.Data.Variables
{
    // 一个运行变量只保留一份状态；Table/Registry 不得复制其 CurrentValue。
    // 仅负责非泛型索引；状态仍只保存在MDSVariable<T>中。
    public abstract class MDSVariableBase
    {
        private static long _nextInstance;
        protected MDSVariableBase()
        {
            InstanceGeneration = System.Threading.Interlocked.Increment(ref _nextInstance);
        }
        // 运行对象重建后允许Revision重新从0开始，但旧世代消息不得污染新对象。
        internal long InstanceGeneration { get; private set; }
        internal abstract MDSVariableDefinition EntryDefinition { get; }
        internal abstract void Attach(Action<string> notify);
        internal abstract void Observe(Action<MDSVariableChange> callback);
        internal abstract object ReadBoxed();
        internal abstract void UpdateBoxed(object value);
        internal abstract void RefreshProviderBoxed(object value, MDSVariableQuality quality, string source);
        internal abstract void MarkProviderQualityBoxed(MDSVariableQuality quality, string source);
        internal abstract object BatchGate { get; }
        internal abstract void ValidateBatch(MDSVariableBatchItem item);
        internal abstract MDSVariableBatchCommit ApplyBatch(MDSVariableBatchItem item, bool captureBatch);
        internal abstract object SnapshotBoxed();
        internal abstract void ValidateRestore(object saved);
        internal abstract void ApplyRestore(object saved);
        internal abstract void Detach();
    }

    internal static class MDSVariableValueRules
    {
        // 结构体可能嵌套byte[]、List等可变引用，不能仅凭IsValueType就声称按值隔离。
        // 检查仅在定义阶段和每个泛型T首次使用时进行，数字热路径不做逐次反射。
        private static bool IsSelfContained(Type type)
        {
            if (type == typeof(string)) return true;
            if (!type.IsValueType) return false;
            if (type.IsPrimitive || type.IsEnum || type == typeof(IntPtr) ||
                type == typeof(UIntPtr)) return true;
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic;
            foreach (System.Reflection.FieldInfo field in type.GetFields(flags))
                if (!IsSelfContained(field.FieldType)) return false;
            return true;
        }

        private static class TypeSafety<T>
        {
            internal static readonly bool Safe = IsSelfContained(typeof(T));
        }

        internal static void ValidateDefinitionType(Type type)
        {
            if (type != null && type.IsValueType && !IsSelfContained(type))
                throw new NotSupportedException("变量结构体包含可变引用字段，必须采用明确的深复制值适配器");
        }

        internal static object Copy(object value)
        {
            if (value == null) return null;
            byte[] bytes = value as byte[];
            if (bytes != null) return (byte[])bytes.Clone();
            Type type = value.GetType();
            if (type.IsValueType)
            {
                ValidateDefinitionType(type);
                return value;
            }
            if (value is string) return value;
            throw new NotSupportedException("复杂可变引用类型需要先登记 Foundation.Data 值适配器");
        }

        internal static T CopyTyped<T>(T value)
        {
            // 通常数值变量无需装箱、堆分配或逐次反射；byte[]仍防御复制。
            Type type = typeof(T);
            if (type.IsValueType)
            {
                if (!TypeSafety<T>.Safe)
                    throw new NotSupportedException("变量结构体包含可变引用字段，不能进行浅复制");
                return value;
            }
            if (type == typeof(string)) return value;
            object copied = Copy(value);
            return copied == null ? default(T) : (T)copied;
        }

        internal static bool Equal<T>(T left, T right)
        {
            if (typeof(T) != typeof(byte[]))
                return EqualityComparer<T>.Default.Equals(left, right);
            byte[] first = (object)left as byte[];
            byte[] second = (object)right as byte[];
            if (first == null || second == null) return first == second;
            if (first.Length != second.Length) return false;
            for (int i = 0; i < first.Length; i++) if (first[i] != second[i]) return false;
            return true;
        }
    }

    // 供Studio、TestCenter及专业工具作类型无关观察；运行时值仍只有MDSVariable<T>一个Owner。
    public interface IMDSVariableSnapshot
    {
        string VariableId { get; }
        string Name { get; }
        Type ValueType { get; }
        object CurrentObject { get; }
        object PreviousObject { get; }
        object DefaultObject { get; }
        bool HasValue { get; }
        bool HasPreviousValue { get; }
        bool HasDefaultValue { get; }
        MDSVariableQuality Quality { get; }
        DateTime RefreshUtc { get; }
        DateTime ChangeUtc { get; }
        string Source { get; }
        long Revision { get; }
    }

    /// <summary>单次锁内抓取的只读快照；byte[] 入参和出参均防御复制。</summary>
    public sealed class MDSVariableSnapshot<T> : IMDSVariableSnapshot
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
        public Type ValueType { get { return typeof(T); } }
        public object CurrentObject { get { return HasValue ? (object)CurrentValue : null; } }
        public object PreviousObject { get { return HasPreviousValue ? (object)PreviousValue : null; } }
        public object DefaultObject { get { return HasDefaultValue ? (object)DefaultValue : null; } }
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
        // 质量变化不是新的物理采样：保留最后一笔真实值及RefreshUtc/ChangeUtc。
        // 仅由具有有效ProviderHost绑定与世代的来源调用，不能伪造Good新采样。
        internal override void MarkProviderQualityBoxed(MDSVariableQuality quality, string source)
        {
            if (quality == MDSVariableQuality.Good ||
                !Enum.IsDefined(typeof(MDSVariableQuality), quality))
                throw new ArgumentOutOfRangeException("quality");
            Action<string> notify;
            Action<MDSVariableChange> observer;
            MDSVariableChange captured = null;
            lock (_gate)
            {
                if (_removed) throw new ObjectDisposedException("MDSVariable");
                if (!_hasValue || _quality == quality) return;
                if (_definition.AccessPolicy != null)
                    _definition.AccessPolicy.Demand(_definition, null, source, true);
                if (_revision == long.MaxValue) throw new OverflowException("变量Revision溢出");
                _quality = quality;
                _revision = checked(_revision + 1);
                notify = _notify; observer = _observer;
                if (observer != null)
                    captured = new MDSVariableChange(_definition.VariableId, _definition.Name,
                        _current, _previous, _hasPrevious, _quality, _source, DateTime.UtcNow,
                        _revision, MDSVariableChangeKind.Quality, InstanceGeneration);
            }
            if (observer != null) observer(captured);
            if (notify != null) notify(_definition.Name);
        }
        // 下述仅为显式Batch/SnapshotBatch服务，不给普通Update增加全表事务锁。
        internal override object BatchGate { get { return _gate; } }
        internal override object SnapshotBoxed() { return Snapshot(); }

        // 恢复只能针对未开始执行/未赋值的全新变量，完整验证后由Table统一提交。
        internal override void ValidateRestore(object saved)
        {
            if (_removed || _hasValue || _revision != 0)
                throw new InvalidOperationException("只能将运行快照恢复到未初始化的有效变量");
            MDSVariableSnapshot<T> state = saved as MDSVariableSnapshot<T>;
            if (state == null || state.VariableId != _definition.VariableId ||
                state.Name != _definition.Name || state.Revision < 0 ||
                state.HasDefaultValue != _definition.HasDefaultValue ||
                (state.HasDefaultValue && !MDSVariableValueRules.Equal(
                    state.DefaultValue, _defaultValue)))
                throw new InvalidOperationException("变量快照的身份、类型或默认值定义不匹配");
        }

        internal override void ApplyRestore(object saved)
        {
            MDSVariableSnapshot<T> state = (MDSVariableSnapshot<T>)saved;
            _current = MDSVariableValueRules.CopyTyped(state.CurrentValue);
            _previous = MDSVariableValueRules.CopyTyped(state.PreviousValue);
            _hasValue = state.HasValue;
            _hasPrevious = state.HasPreviousValue;
            _quality = state.Quality;
            _source = state.Source;
            _refreshUtc = state.RefreshUtc;
            _changeUtc = state.ChangeUtc;
            _revision = state.Revision;
            // 恢复不发变化事件：这是全新Scope在Provider激活之前的状态装载。
        }

        internal override void ValidateBatch(MDSVariableBatchItem item)
        {
            // 调用方已经按ID顺序取得BatchGate；此方法只做验证，无状态修改。
            if (_removed) throw new ObjectDisposedException("MDSVariable");
            if (_definition.AccessPolicy != null)
                _definition.AccessPolicy.Demand(_definition, item.Options.PrincipalId, item.Options.Source, false);
            else if (!_definition.Writable)
                throw new InvalidOperationException("批量写入只读变量: " + _definition.Name);
            object value = item.Value;
            if (value != null && !(value is T))
                throw new InvalidCastException("批量更新类型不匹配: " + _definition.Name);
            if (value == null && typeof(T).IsValueType && Nullable.GetUnderlyingType(typeof(T)) == null)
                throw new InvalidCastException("非Nullable变量不能更新为null: " + _definition.Name);
            if (item.Options.ExpectedRevision.HasValue &&
                item.Options.ExpectedRevision.Value != _revision)
                throw new MDSVariableVersionConflictException(_definition.VariableId,
                    item.Options.ExpectedRevision.Value, _revision);
            if (_revision == long.MaxValue) throw new OverflowException("变量Revision溢出");
        }

        internal override MDSVariableBatchCommit ApplyBatch(MDSVariableBatchItem item, bool captureBatch)
        {
            // 由Table锁住事务的所有变量后调用；校验已全量通过。
            T next = MDSVariableValueRules.CopyTyped((T)item.Value);
            bool changed = !_hasValue || !MDSVariableValueRules.Equal(_current, next);
            bool qualityChanged = _quality != item.Options.Quality;
            bool sourceChanged = item.Options.Source != null && item.Options.Source != _source;
            DateTime now = DateTime.UtcNow;
            if (changed)
            {
                if (_hasValue) { _previous = _current; _hasPrevious = true; }
                _current = next; _hasValue = true; _changeUtc = now;
            }
            _quality = item.Options.Quality;
            _source = item.Options.Source ?? _source;
            _refreshUtc = now;
            _revision++;
            MDSVariableUpdateResult result = new MDSVariableUpdateResult(
                _definition.VariableId, changed, _revision, _quality, now);
            Action<string> notify = _notify;
            Action<MDSVariableChange> observer = _observer;
            MDSVariableChange capture = null;
            if (observer != null || captureBatch)
            {
                MDSVariableChangeKind kind = changed ? MDSVariableChangeKind.Value : MDSVariableChangeKind.Refresh;
                if (qualityChanged) kind |= MDSVariableChangeKind.Quality;
                if (sourceChanged) kind |= MDSVariableChangeKind.Source;
                capture = new MDSVariableChange(_definition.VariableId, _definition.Name,
                    _current, _previous, _hasPrevious, _quality, _source, now, _revision, kind,
                    InstanceGeneration);
            }
            MDSVariableChange frozen = capture;
            return new MDSVariableBatchCommit(result, frozen, () =>
            {
                try { if (observer != null) observer(frozen); }
                finally { if (notify != null) notify(_definition.Name); }
            });
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
            Commit(value, MDSVariableQuality.Good, null, false, null, false, null);
        }
        public MDSVariableUpdateResult Update(T value, MDSVariableUpdateOptions options)
        {
            if (options == null) throw new ArgumentNullException("options");
            return Commit(value, options.Quality, options.Source, false, options.ExpectedRevision, true, options.PrincipalId);
        }
        public bool TryUpdate(T value, long expectedRevision, out MDSVariableUpdateResult result)
        {
            try
            {
                result = Commit(value, MDSVariableQuality.Good, null, false, expectedRevision, true, null);
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
            Commit(value, quality, source, true, null, false, null);
        }

        private MDSVariableUpdateResult Commit(T value, MDSVariableQuality quality,
            string source, bool fromProvider, long? expectedRevision, bool returnResult, string principalId)
        {
            T copied = MDSVariableValueRules.CopyTyped(value);
            if ((uint)quality > (uint)MDSVariableQuality.Stale)
                throw new ArgumentOutOfRangeException("quality");
            Action<string> notify;
            Action<MDSVariableChange> observer;
            MDSVariableChange captured = null;
            MDSVariableUpdateResult result = null;
            lock (_gate)
            {
                if (_removed) throw new ObjectDisposedException("MDSVariable");
                if (_definition.AccessPolicy != null)
                    _definition.AccessPolicy.Demand(_definition, principalId, source, fromProvider);
                else if (!fromProvider && !_definition.Writable)
                    throw new InvalidOperationException("变量为只读: " + _definition.Name);
                if (expectedRevision.HasValue && _revision != expectedRevision.Value)
                    throw new MDSVariableVersionConflictException(_definition.VariableId,
                        expectedRevision.Value, _revision);
                // Revision溢出必须在修改任何值/质量/时间之前拒绝，保证单变量提交原子性。
                if (_revision == long.MaxValue) throw new OverflowException("变量Revision溢出");
                T next = copied;
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
                if (returnResult)
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
                        _current, _previous, _hasPrevious, _quality, _source, now, _revision, kind,
                    InstanceGeneration);
                }
            }
            // 高速路径只有在订阅变化捕获时才创建不可变快照，旧通知继续兼容。
            if (observer != null) observer(captured);
            if (notify != null) notify(_definition.Name);
            return result;
        }
    }
}
