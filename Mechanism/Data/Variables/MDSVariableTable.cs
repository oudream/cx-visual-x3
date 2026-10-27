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
        // CLR完整保留原四参数构造函数签名，避免已经编译的调用方MissingMethodException。
        public MDSVariableDefinition(string name, Type valueType, string unit = null, bool writable = true)
            : this(name, valueType, unit, writable, null, null) { }

        // 快捷定义默认值；若需单位/权限/稳定ID，使用下面的完整构造函数。
        public MDSVariableDefinition(string name, Type valueType, object defaultValue)
            : this(name, valueType, null, true, defaultValue, null) { }

        public MDSVariableDefinition(string name, Type valueType, string unit,
            bool writable, object defaultValue, string variableId = null)
            : this(name, valueType, unit, writable, defaultValue, variableId, null) { }

        public MDSVariableDefinition(string name, Type valueType, string unit,
            bool writable, object defaultValue, string variableId, MDSVariableAccessPolicy accessPolicy)
        {
            if (string.IsNullOrWhiteSpace(name) || valueType == null || valueType == typeof(void))
                throw new ArgumentException("变量名称/类型无效");
            Name = name.Trim();
            ValueType = valueType;
            Unit = unit;
            Writable = writable;
            VariableId = string.IsNullOrWhiteSpace(variableId) ? Name : variableId.Trim();
            AccessPolicy = accessPolicy;
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
        public MDSVariableAccessPolicy AccessPolicy { get; private set; }
        public bool HasDefaultValue { get; private set; }
        public object DefaultValue { get { return MDSVariableValueRules.Copy(_defaultValue); } }
    }

    // 保留现有Get/TryGet/Update/Sample/VariableChanged签名和异常行为。
    public sealed class MDSVariableTable : IMDSVariableTable
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, MDSVariableBase> _items =
            new Dictionary<string, MDSVariableBase>(StringComparer.Ordinal);

        public MDSVariableTable(string scopeId)
        {
            if (string.IsNullOrWhiteSpace(scopeId)) throw new ArgumentException("ScopeId 不能为空");
            ScopeId = scopeId.Trim();
            Tree = new MDSVariableTree();
        }

        public string ScopeId { get; private set; }
        public MDSVariableTree Tree { get; private set; }
        public event Action<string> VariableChanged;
        private long _batchSequence;
        private Action<MDSVariableChangeBatch> _batchCommitted;
        public event Action<MDSVariableChangeBatch> BatchCommitted
        {
            add { lock (_gate) _batchCommitted += value; }
            remove { lock (_gate) _batchCommitted -= value; }
        }
        // 热插入Reliable时必须在同一表锁内设置起始序列并注册回调，避免时序空洞。
        internal void SubscribeReliableBatches(Action<MDSVariableChangeBatch> receiver, Action<long> initializeBaseline)
        {
            if (receiver == null) throw new ArgumentNullException("receiver");
            if (initializeBaseline == null) throw new ArgumentNullException("initializeBaseline");
            lock (_gate)
            {
                initializeBaseline(_batchSequence);
                _batchCommitted += receiver;
            }
        }
        private Action<MDSVariableChange> _captured;
        public event Action<MDSVariableChange> ChangeCaptured
        {
            add
            {
                lock (_gate)
                {
                    _captured += value;
                    foreach (MDSVariableBase variable in _items.Values) variable.Observe(ForwardCaptured);
                }
            }
            remove
            {
                lock (_gate)
                {
                    _captured -= value;
                    foreach (MDSVariableBase variable in _items.Values)
                        variable.Observe(_captured == null ? null : (Action<MDSVariableChange>)ForwardCaptured);
                }
            }
        }
        private void ForwardCaptured(MDSVariableChange change)
        {
            Action<MDSVariableChange> callback = _captured;
            if (callback != null) callback(change);
        }
        // 仅用户主动开启变化派发，不影响旧Get/Update简单使用。
        public MDSVariableChangeDispatcher CreateDispatcher(int capacity = 4096)
        {
            return new MDSVariableChangeDispatcher(this, capacity);
        }

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
                MDSVariableBase variable = (MDSVariableBase)Activator.CreateInstance(runtimeType, definition);
                // 只在定义时反射一次，日常更新从已解析的强类型对象直接写。
                Tree.AddLeaf(variable);
                try { variable.Attach(ForwardChanged);
                    if (_captured != null) variable.Observe(ForwardCaptured);
                    _items.Add(definition.Name, variable); }
                catch { Tree.RemoveLeaf(definition.VariableId); throw; }
            }
        }

        public bool Remove(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            lock (_gate)
            {
                MDSVariableBase item;
                if (!_items.TryGetValue(name, out item)) return false;
                Tree.RemoveLeaf(item.EntryDefinition.VariableId);
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
            lock (_gate) return _items.Values.Select(x => x.EntryDefinition).ToArray();
        }

        // Type-erased只读投影：避免每个专业UI再次写一套变量运行态或反射读取逻辑。
        public IMDSVariableSnapshot Inspect(string name)
        {
            return (IMDSVariableSnapshot)ResolveEntry(name).SnapshotBoxed();
        }

        internal MDSVariableBase ResolveEntry(string name)
        {
            lock (_gate)
            {
                MDSVariableBase variable;
                if (string.IsNullOrWhiteSpace(name) || !_items.TryGetValue(name, out variable))
                    throw new KeyNotFoundException("变量未定义: " + name);
                return variable;
            }
        }

        public MDSVariableBatchResult UpdateBatch(MDSVariableBatch request)
        {
            if (request == null) throw new ArgumentNullException("request");
            if (!string.Equals(request.ScopeId, ScopeId, StringComparison.Ordinal))
                throw new InvalidOperationException("批次Scope不匹配，禁止跨运行会话写入");
            return UpdateBatchCore(request.Items.ToArray(), request.BatchId);
        }

        // 跨变量一致提交：只锁当前批次引用的变量；普通MDSVariable<T>.Update不引入表级热路径开销。
        public MDSVariableBatchResult UpdateBatch(params MDSVariableBatchItem[] batch)
        {
            return UpdateBatchCore(batch, null);
        }
        private MDSVariableBatchResult UpdateBatchCore(MDSVariableBatchItem[] batch, string batchId)
        {
            if (batch == null) throw new ArgumentNullException("batch");
            if (batch.Length == 0)
                return new MDSVariableBatchResult(new MDSVariableUpdateResult[0], new Exception[0]);
            MDSVariableBatchCommit[] commits = new MDSVariableBatchCommit[batch.Length];
            Action<MDSVariableChangeBatch> batchCallback;
            MDSVariableChangeBatch captured = null;
            lock (_gate)
            {
                if (batch.Any(x => x == null)) throw new ArgumentException("批量条目不得为null");
                if (batch.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != batch.Length)
                    throw new ArgumentException("同一批次不能多次修改同名变量");
                MDSVariableBase[] objects = new MDSVariableBase[batch.Length];
                for (int i = 0; i < batch.Length; ++i)
                {
                    if (!_items.TryGetValue(batch[i].Name, out objects[i]))
                        throw new KeyNotFoundException("批量变量未定义: " + batch[i].Name);
                }
                MDSVariableBase[] ordered = objects.OrderBy(x => x.EntryDefinition.VariableId,
                    StringComparer.Ordinal).ToArray();
                for (int i = 0; i < ordered.Length; ++i)
                    System.Threading.Monitor.Enter(ordered[i].BatchGate);
                try
                {
                    // 全量校验成功后才修改任何一个值；Revision冲突/类型错/只读不得半提交。
                    for (int i = 0; i < batch.Length; ++i) objects[i].ValidateBatch(batch[i]);
                    // 先为整个批次预留递增序号，避免Overflow导致已提交部分批次。
                    long sequence = checked(_batchSequence + 1);
                    batchCallback = _batchCommitted;
                    for (int i = 0; i < batch.Length; ++i)
                        commits[i] = objects[i].ApplyBatch(batch[i], batchCallback != null);
                    _batchSequence = sequence;
                    if (batchCallback != null)
                        captured = new MDSVariableChangeBatch(ScopeId,
                            string.IsNullOrWhiteSpace(batchId) ? Guid.NewGuid().ToString("N") : batchId,
                            sequence, commits.Select(x => x.Change).ToArray());
                }
                finally
                {
                    for (int i = ordered.Length - 1; i >= 0; --i)
                        System.Threading.Monitor.Exit(ordered[i].BatchGate);
                }
            }
            // 事件位于所有状态锁和表锁之外，不能让慢订阅者占住变量事务锁。
            List<Exception> failures = new List<Exception>();
            foreach (MDSVariableBatchCommit result in commits)
            {
                try { result.Notify(); }
                catch (Exception error) { failures.Add(error); }
            }
            if (batchCallback != null)
            {
                foreach (Action<MDSVariableChangeBatch> handler in batchCallback.GetInvocationList())
                    try { handler(captured); }
                    catch (Exception error) { failures.Add(error); }
            }
            return new MDSVariableBatchResult(commits.Select(x => x.Result).ToArray(),
                failures.ToArray(), captured);
        }

        // 多变量一致快照仅适用于同一Table Scope，返回值为具体MDSVariableSnapshot<T>。
        public IReadOnlyDictionary<string, object> SnapshotBatch(params string[] names)
        {
            if (names == null) throw new ArgumentNullException("names");
            lock (_gate)
            {
                if (names.Any(x => string.IsNullOrWhiteSpace(x)) ||
                    names.Distinct(StringComparer.Ordinal).Count() != names.Length)
                    throw new ArgumentException("快照变量名不可为空或重复");
                MDSVariableBase[] variables = new MDSVariableBase[names.Length];
                for (int i = 0; i < names.Length; i++)
                    if (!_items.TryGetValue(names[i], out variables[i]))
                        throw new KeyNotFoundException("快照变量未定义: " + names[i]);
                MDSVariableBase[] ordered = variables.OrderBy(x => x.EntryDefinition.VariableId,
                    StringComparer.Ordinal).ToArray();
                for (int i = 0; i < ordered.Length; ++i)
                    System.Threading.Monitor.Enter(ordered[i].BatchGate);
                try
                {
                    Dictionary<string, object> snapshots = new Dictionary<string, object>(StringComparer.Ordinal);
                    for (int i = 0; i < names.Length; i++)
                        snapshots.Add(names[i], variables[i].SnapshotBoxed());
                    return new System.Collections.ObjectModel.ReadOnlyDictionary<string, object>(snapshots);
                }
                finally
                {
                    for (int i = ordered.Length - 1; i >= 0; --i)
                        System.Threading.Monitor.Exit(ordered[i].BatchGate);
                }
            }
        }

        // 每个Scope整体抓取一致状态，导入要求定义身份/类型/默认值完全匹配。
        public MDSVariableStoreSnapshot ExportSnapshot()
        {
            lock (_gate)
            {
                MDSVariableBase[] ordered = _items.Values.OrderBy(x => x.EntryDefinition.VariableId,
                    StringComparer.Ordinal).ToArray();
                foreach (MDSVariableBase entry in ordered)
                    System.Threading.Monitor.Enter(entry.BatchGate);
                try
                {
                    Dictionary<string, object> captured = new Dictionary<string, object>(StringComparer.Ordinal);
                    Dictionary<string, MDSVariableDefinition> definitions =
                        new Dictionary<string, MDSVariableDefinition>(StringComparer.Ordinal);
                    foreach (MDSVariableBase entry in ordered)
                    {
                        captured.Add(entry.EntryDefinition.Name, entry.SnapshotBoxed());
                        definitions.Add(entry.EntryDefinition.Name, entry.EntryDefinition);
                    }
                    return new MDSVariableStoreSnapshot(ScopeId, captured, definitions);
                }
                finally
                {
                    for (int i = ordered.Length - 1; i >= 0; --i)
                        System.Threading.Monitor.Exit(ordered[i].BatchGate);
                }
            }
        }

        public void RestoreSnapshot(MDSVariableStoreSnapshot saved)
        {
            if (saved == null) throw new ArgumentNullException("saved");
            if (saved.ScopeId != ScopeId)
                throw new InvalidOperationException("快照Scope身份与目标不一致");
            lock (_gate)
            {
                if (saved.Variables.Count != _items.Count)
                    throw new InvalidOperationException("变量定义数量与快照不一致");
                MDSVariableBase[] ordered = _items.Values.OrderBy(x => x.EntryDefinition.VariableId,
                    StringComparer.Ordinal).ToArray();
                foreach (MDSVariableBase entry in ordered)
                    System.Threading.Monitor.Enter(entry.BatchGate);
                try
                {
                    // 定义、默认值、类型、未启动状态全量检查成功后才恢复任何变量。
                    foreach (MDSVariableBase entry in ordered)
                    {
                        object state;
                        if (!saved.Variables.TryGetValue(entry.EntryDefinition.Name, out state))
                            throw new InvalidOperationException("快照缺少变量: " + entry.EntryDefinition.Name);
                        MDSVariableDefinition schema;
                        if (!saved.Definitions.TryGetValue(entry.EntryDefinition.Name, out schema) ||
                            schema.VariableId != entry.EntryDefinition.VariableId ||
                            schema.ValueType != entry.EntryDefinition.ValueType ||
                            schema.Unit != entry.EntryDefinition.Unit ||
                            schema.Writable != entry.EntryDefinition.Writable ||
                            ((schema.AccessPolicy == null) != (entry.EntryDefinition.AccessPolicy == null)) ||
                            (schema.AccessPolicy != null &&
                             !schema.AccessPolicy.EquivalentTo(entry.EntryDefinition.AccessPolicy)))
                            throw new InvalidOperationException("快照变量Schema不一致: " + entry.EntryDefinition.Name);
                        entry.ValidateRestore(state);
                    }
                    foreach (MDSVariableBase entry in ordered)
                        entry.ApplyRestore(saved.Variables[entry.EntryDefinition.Name]);
                }
                finally
                {
                    for (int i = ordered.Length - 1; i >= 0; --i)
                        System.Threading.Monitor.Exit(ordered[i].BatchGate);
                }
            }
        }

        // 预绑定强类型句柄，无后续字典查找；是同一权威状态，不是缓存副本。
        public MDSVariable<T> Resolve<T>(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("变量名称无效");
            lock (_gate)
            {
                MDSVariableBase variable;
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
                MDSVariableBase entry;
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
