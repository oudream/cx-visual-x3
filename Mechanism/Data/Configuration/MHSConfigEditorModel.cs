using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Mechanism.Data.Configuration
{
    /// <summary>UI 无关的属性状态：Schema、当前草稿、保存值、有效值与验证错误。</summary>
    public sealed class MHSConfigEditorField
    {
        internal MHSConfigEditorField(FCMConfigFieldInfo schema, string draft, string saved,
            string effective, IList<FCMConfigIssue> issues)
        {
            Schema = schema;
            DraftJson = draft; SavedJson = saved; EffectiveJson = effective;
            HasDraftValue = draft != null; HasSavedValue = saved != null;
            IsModified = !string.Equals(draft, saved, StringComparison.Ordinal);
            Issues = new List<FCMConfigIssue>(issues).AsReadOnly();
        }
        public FCMConfigFieldInfo Schema { get; private set; }
        public string DraftJson { get; private set; }
        public string SavedJson { get; private set; }
        public string EffectiveJson { get; private set; }
        public bool HasDraftValue { get; private set; }
        public bool HasSavedValue { get; private set; }
        public bool IsModified { get; private set; }
        public IList<FCMConfigIssue> Issues { get; private set; }
    }

    /// <summary>
    /// One live collection entry; a set has no positional path (ItemPath is null)
    /// and is identified only by its value JSON. No runtime data is owned here.
    /// </summary>
    public sealed class MHSConfigCollectionItem
    {
        internal MHSConfigCollectionItem(string collectionPath, string itemPath,
            FCMConfigTypeKind kind, Type itemType, string keyJson, int? index,
            string draft, string saved, string effective, bool sensitive,
            IList<FCMConfigIssue> issues)
        {
            CollectionPath = collectionPath; ItemPath = itemPath;
            CollectionKind = kind; ItemType = itemType; KeyJson = keyJson; Index = index;
            DraftJson = draft; SavedJson = saved; EffectiveJson = effective;
            HasDraft = draft != null; HasSaved = saved != null;
            HasEffective = effective != null;
            IsModified = HasDraft != HasSaved ||
                !string.Equals(draft, saved, StringComparison.Ordinal);
            IsSensitive = sensitive;
            Issues = new List<FCMConfigIssue>(issues).AsReadOnly();
        }
        public string CollectionPath { get; private set; }
        /// <summary>Null for HashSet: sets are not indexable.</summary>
        public string ItemPath { get; private set; }
        public FCMConfigTypeKind CollectionKind { get; private set; }
        public Type ItemType { get; private set; }
        /// <summary>JSON string key for maps, serialized value for sets; null for lists.</summary>
        public string KeyJson { get; private set; }
        public int? Index { get; private set; }
        public string DraftJson { get; private set; }
        public string SavedJson { get; private set; }
        public string EffectiveJson { get; private set; }
        public bool HasDraft { get; private set; }
        public bool HasSaved { get; private set; }
        public bool HasEffective { get; private set; }
        public bool IsModified { get; private set; }
        public bool IsSensitive { get; private set; }
        public bool CanReorder { get { return CollectionKind == FCMConfigTypeKind.List; } }
        public IList<FCMConfigIssue> Issues { get; private set; }
    }

    /// <summary>
    /// 与 UI 无关的编辑会话：深拷贝、事务预览、Undo/Redo、Schema/依赖诊断。
    /// 一次提交仍委托 MHSConfigEdit<T>，不构建第二套存储、校验器或事务真源。
    /// </summary>
    public sealed class MHSConfigEditorModel<T> : IDisposable where T : new()
    {
        private MHSConfigEdit<T> _edit;
        private readonly MHSConfigSection<T> _section;
        private readonly List<string> _states = new List<string>();
        private readonly FCMConfigDependencyGraph _dependencies;
        private int _position;
        private readonly int _maxStates;
        public MHSConfigEditorModel(MHSConfigSection<T> section, string reason, int maxUndoStates = 64)
        {
            if (section == null) throw new ArgumentNullException("section");
            if (maxUndoStates < 2 || maxUndoStates > 1000) throw new ArgumentOutOfRangeException("maxUndoStates");
            _section = section;
            _dependencies = FCMConfigCatalog.BuildDependencyGraph<T>();
            _maxStates = maxUndoStates;
            _edit = section.BeginEdit(reason);
            _states.Add(FCMJsonValueCodec.Serialize(_edit.WorkingCopy));
        }
        public FCMConfigIdentity Identity { get { return _section.Identity; } }
        public IList<FCMConfigFieldInfo> SchemaFields { get { return FCMConfigCatalog.Describe<T>(); } }
        public T Draft { get { EnsureOpen(); return Clone(_edit.WorkingCopy); } }
        public bool CanUndo { get { EnsureOpen(); return _position > 0; } }
        public bool CanRedo { get { EnsureOpen(); return _position + 1 < _states.Count; } }
        public IList<string> AffectedBy(string path)
        { EnsureOpen(); return _dependencies.AffectedBy(path); }

        public void Modify(Action<T> change)
        {
            EnsureOpen();
            if (change == null) throw new ArgumentNullException("change");
            T detached = Clone(_edit.WorkingCopy);
            change(detached);
            string json = FCMJsonValueCodec.Serialize(detached);
            if (json == _states[_position]) return;
            // Redo history after the current position belongs to the previous branch.
            if (_position + 1 < _states.Count)
                _states.RemoveRange(_position + 1, _states.Count - _position - 1);
            _states.Add(json); _position++;
            if (_states.Count > _maxStates) { _states.RemoveAt(0); _position--; }
            _edit.ReplaceWorkingCopy(detached);
        }
        /// <summary>
        /// Strongly typed single/path assignment. Nested dictionary keys and list
        /// indices use the existing path engine; no UI-side JSON editing is required.
        /// Failures occur on a detached copy and never mutate undo history.
        /// </summary>
        public void SetValue<V>(System.Linq.Expressions.Expression<Func<T, V>> selector, V value)
        {
            EnsureOpen();
            if (selector == null) throw new ArgumentNullException("selector");
            string path = FCMConfigPathUtil.FromExpression(selector);
            string json = FCMJsonValueCodec.Serialize(value);
            MVBConfigLayerPath.Validate<T>(path, json);
            Modify(x => MVBConfigLayerPath.Apply(x, path, json));
        }

        /// <summary>Dictionary keys are string-typed, as required by the common codec.</summary>
        public void AddKey<V>(System.Linq.Expressions.Expression<Func<T, Dictionary<string, V>>> selector,
            string key, V value)
        {
            if (selector == null) throw new ArgumentNullException("selector");
            if (key == null) throw new ArgumentNullException("key");
            Func<T, Dictionary<string, V>> read = selector.Compile();
            RequireDefaultComparer(read(_edit.WorkingCopy));
            Modify(x => read(x).Add(key, value));
        }
        public void RenameKey<V>(System.Linq.Expressions.Expression<Func<T, Dictionary<string, V>>> selector,
            string from, string to)
        {
            if (selector == null) throw new ArgumentNullException("selector");
            if (from == null || to == null) throw new ArgumentNullException(from == null ? "from" : "to");
            Func<T, Dictionary<string, V>> read = selector.Compile();
            RequireDefaultComparer(read(_edit.WorkingCopy));
            Modify(x =>
            {
                Dictionary<string, V> dict = read(x);
                if (!dict.ContainsKey(from)) throw new KeyNotFoundException("CONFIG_EDITOR_KEY_MISSING");
                if (string.Equals(from, to, StringComparison.Ordinal)) return;
                if (dict.ContainsKey(to)) throw new ArgumentException("CONFIG_EDITOR_DUPLICATE_KEY");
                V item = dict[from];
                dict.Remove(from);
                dict.Add(to, item);
            });
        }
        public void RemoveKey<V>(System.Linq.Expressions.Expression<Func<T, Dictionary<string, V>>> selector,
            string key)
        {
            if (selector == null) throw new ArgumentNullException("selector");
            if (key == null) throw new ArgumentNullException("key");
            Func<T, Dictionary<string, V>> read = selector.Compile();
            RequireDefaultComparer(read(_edit.WorkingCopy));
            Modify(x =>
            {
                if (!read(x).Remove(key)) throw new KeyNotFoundException("CONFIG_EDITOR_KEY_MISSING");
            });
        }
        public void InsertItem<V>(System.Linq.Expressions.Expression<Func<T, List<V>>> selector,
            int index, V value)
        {
            if (selector == null) throw new ArgumentNullException("selector");
            Func<T, List<V>> read = selector.Compile();
            Modify(x => read(x).Insert(index, value));
        }
        public void RemoveItemAt<V>(System.Linq.Expressions.Expression<Func<T, List<V>>> selector, int index)
        {
            if (selector == null) throw new ArgumentNullException("selector");
            Func<T, List<V>> read = selector.Compile();
            Modify(x => read(x).RemoveAt(index));
        }
        /// <summary>toIndex is the FINAL position after removing the source item.</summary>
        public void MoveItem<V>(System.Linq.Expressions.Expression<Func<T, List<V>>> selector,
            int fromIndex, int toIndex)
        {
            if (selector == null) throw new ArgumentNullException("selector");
            Func<T, List<V>> read = selector.Compile();
            Modify(x =>
            {
                List<V> list = read(x);
                if (fromIndex < 0 || fromIndex >= list.Count ||
                    toIndex < 0 || toIndex >= list.Count) throw new ArgumentOutOfRangeException("index");
                if (fromIndex == toIndex) return;
                V item = list[fromIndex];
                list.RemoveAt(fromIndex);
                list.Insert(toIndex, item);
            });
        }
        public void AddSetItem<V>(System.Linq.Expressions.Expression<Func<T, HashSet<V>>> selector, V value)
        {
            if (selector == null) throw new ArgumentNullException("selector");
            Func<T, HashSet<V>> read = selector.Compile();
            RequireDefaultComparer(read(_edit.WorkingCopy));
            Modify(x => read(x).Add(value));
        }
        public void RemoveSetItem<V>(System.Linq.Expressions.Expression<Func<T, HashSet<V>>> selector, V value)
        {
            if (selector == null) throw new ArgumentNullException("selector");
            Func<T, HashSet<V>> read = selector.Compile();
            RequireDefaultComparer(read(_edit.WorkingCopy));
            Modify(x => read(x).Remove(value));
        }
        private static void RequireDefaultComparer<V>(Dictionary<string, V> value)
        {
            if (value == null) throw new InvalidOperationException("CONFIG_EDITOR_NULL_COLLECTION");
            if (!value.Comparer.Equals(EqualityComparer<string>.Default))
                throw new NotSupportedException("CONFIG_EDITOR_CUSTOM_COMPARER_UNSUPPORTED");
        }
        private static void RequireDefaultComparer<V>(HashSet<V> value)
        {
            if (value == null) throw new InvalidOperationException("CONFIG_EDITOR_NULL_COLLECTION");
            if (!value.Comparer.Equals(EqualityComparer<V>.Default))
                throw new NotSupportedException("CONFIG_EDITOR_CUSTOM_COMPARER_UNSUPPORTED");
        }

        public bool Undo()
        {
            EnsureOpen();
            if (!CanUndo) return false;
            _position--; LoadCurrent(); return true;
        }
        public bool Redo()
        {
            EnsureOpen();
            if (!CanRedo) return false;
            _position++; LoadCurrent(); return true;
        }
        public MHSConfigPreview Preview()
        { EnsureOpen(); return _edit.Preview(); }
        public Task<MHSConfigPreview> PreviewAsync(CancellationToken token)
        { EnsureOpen(); return _edit.PreviewAsync(token); }

        public IList<MHSConfigEditorField> DescribeFields()
        {
            EnsureOpen();
            MHSConfigPreview preview = _edit.Preview();
            T original = _section.GetLatestSaved();
            T effective = _section.Get();
            List<MHSConfigEditorField> fields = new List<MHSConfigEditorField>();
            foreach (FCMConfigFieldInfo member in SchemaFields)
            {
                string draft, saved, active;
                MVBConfigLayerPath.TryReadJson(_edit.WorkingCopy, member.Path, out draft);
                MVBConfigLayerPath.TryReadJson(original, member.Path, out saved);
                MVBConfigLayerPath.TryReadJson(effective, member.Path, out active);
                IList<FCMConfigIssue> matched = preview.Validation.Issues
                    .Where(x => x.Path == member.Path || x.Path.StartsWith(member.Path + ".", StringComparison.Ordinal) ||
                        x.Path.StartsWith(member.Path + "[", StringComparison.Ordinal)).ToList();
                fields.Add(new MHSConfigEditorField(member, draft, saved, active, matched));
            }
            return fields.AsReadOnly();
        }
        /// <summary>
        /// Dynamic entries are the UNION of draft/saved/effective keys and indices.
        /// Thus deleted entries remain visible as removed in a visual diff.
        /// Lists have indices, dictionaries have serialized string keys, sets are
        /// value-addressed and cannot be reordered by an imaginary index.
        /// </summary>
        public IList<MHSConfigCollectionItem> DescribeCollectionItems()
        {
            EnsureOpen();
            T draft = Clone(_edit.WorkingCopy);
            T saved = _section.GetLatestSaved();
            T effective = _section.Get();
            FCMConfigValidationReport validation = _edit.Preview().Validation;
            List<MHSConfigCollectionItem> items = new List<MHSConfigCollectionItem>();
            VisitCollection(typeof(T), draft, saved, effective, "", false,
                validation.Issues, items, 0);
            return items.AsReadOnly();
        }
        private static void VisitCollection(Type type, object draft, object saved,
            object effective, string path, bool sensitive, IList<FCMConfigIssue> issues,
            List<MHSConfigCollectionItem> items, int depth)
        {
            if (depth > 32 || items.Count > 10000)
                throw new InvalidOperationException("CONFIG_EDITOR_DYNAMIC_NODE_LIMIT");
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (FCMConfigSchemaBuilder.IsScalar(type)) return;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                Type itemType = type.GetGenericArguments()[1];
                IDictionary d = draft as IDictionary, s = saved as IDictionary, e = effective as IDictionary;
                HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
                foreach (IDictionary map in new[] { d, s, e })
                    if (map != null) foreach (DictionaryEntry item in map) keys.Add((string)item.Key);
                foreach (string key in keys.OrderBy(x => x, StringComparer.Ordinal))
                {
                    string child = path + "[" + FCMJsonValueCodec.Serialize(key) + "]";
                    AddItem(items, path, child, FCMConfigTypeKind.Map, itemType,
                        FCMJsonValueCodec.Serialize(key), null, d != null && d.Contains(key) ? d[key] : null,
                        s != null && s.Contains(key) ? s[key] : null,
                        e != null && e.Contains(key) ? e[key] : null,
                        d != null && d.Contains(key), s != null && s.Contains(key),
                        e != null && e.Contains(key), sensitive, issues);
                    VisitCollection(itemType, d != null && d.Contains(key) ? d[key] : null,
                        s != null && s.Contains(key) ? s[key] : null,
                        e != null && e.Contains(key) ? e[key] : null,
                        child, sensitive, issues, items, depth + 1);
                }
                return;
            }
            if (type.IsArray || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)))
            {
                Type element = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
                FCMConfigTypeKind kind = type.IsArray ? FCMConfigTypeKind.Array : FCMConfigTypeKind.List;
                IList d = draft as IList, s = saved as IList, e = effective as IList;
                int count = Math.Max(d == null ? 0 : d.Count,
                    Math.Max(s == null ? 0 : s.Count, e == null ? 0 : e.Count));
                for (int i = 0; i < count; ++i)
                {
                    string child = path + "[" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]";
                    bool hd = d != null && i < d.Count, hs = s != null && i < s.Count,
                        he = e != null && i < e.Count;
                    object dv = hd ? d[i] : null, sv = hs ? s[i] : null, ev = he ? e[i] : null;
                    AddItem(items, path, child, kind, element, null, i,
                        dv, sv, ev, hd, hs, he, sensitive, issues);
                    VisitCollection(element, dv, sv, ev, child, sensitive, issues, items, depth + 1);
                }
                return;
            }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(HashSet<>))
            {
                Type element = type.GetGenericArguments()[0];
                Dictionary<string, object>[] sets = new[] { draft, saved, effective }
                    .Select(x => SetValues(x)).ToArray();
                foreach (string key in sets.SelectMany(x => x.Keys).Distinct(StringComparer.Ordinal)
                    .OrderBy(x => x, StringComparer.Ordinal))
                {
                    // ItemPath is deliberately null: HashSet does not have indices.
                    AddItem(items, path, null, FCMConfigTypeKind.Set, element, key, null,
                        sets[0].ContainsKey(key) ? sets[0][key] : null,
                        sets[1].ContainsKey(key) ? sets[1][key] : null,
                        sets[2].ContainsKey(key) ? sets[2][key] : null,
                        sets[0].ContainsKey(key), sets[1].ContainsKey(key),
                        sets[2].ContainsKey(key), sensitive, issues);
                }
                return;
            }
            foreach (FCMConfigMemberDescriptor member in FCMConfigSchemaBuilder.Members(type))
            {
                string child = path.Length == 0 ? member.Name : path + "." + member.Name;
                bool protectedChild = sensitive || member.Property.IsDefined(
                    typeof(FCMConfigSensitiveAttribute), true) ||
                    FCMConfigSensitivity.ContainsSensitive(member.MemberType);
                VisitCollection(member.MemberType,
                    draft == null ? null : member.Property.GetValue(draft, null),
                    saved == null ? null : member.Property.GetValue(saved, null),
                    effective == null ? null : member.Property.GetValue(effective, null),
                    child, protectedChild, issues, items, depth + 1);
            }
        }
        private static Dictionary<string, object> SetValues(object value)
        {
            Dictionary<string, object> values = new Dictionary<string, object>(StringComparer.Ordinal);
            IEnumerable items = value as IEnumerable;
            if (items != null)
                foreach (object item in items)
                    values[FCMJsonValueCodec.Serialize(item)] = item;
            return values;
        }
        private static void AddItem(List<MHSConfigCollectionItem> result, string parent,
            string itemPath, FCMConfigTypeKind kind, Type type, string key, int? index,
            object draft, object saved, object effective, bool hasDraft,
            bool hasSaved, bool hasEffective, bool sensitive, IList<FCMConfigIssue> issues)
        {
            if (result.Count >= 10000)
                throw new InvalidOperationException("CONFIG_EDITOR_DYNAMIC_NODE_LIMIT");
            string prefix = itemPath ?? parent;
            IList<FCMConfigIssue> matched = issues.Where(x =>
                x.Path == prefix || (itemPath != null &&
                (x.Path.StartsWith(prefix + ".", StringComparison.Ordinal) ||
                 x.Path.StartsWith(prefix + "[", StringComparison.Ordinal)))).ToList();
            result.Add(new MHSConfigCollectionItem(parent, itemPath, kind, type, key, index,
                hasDraft ? FCMJsonValueCodec.Serialize(draft) : null,
                hasSaved ? FCMJsonValueCodec.Serialize(saved) : null,
                hasEffective ? FCMJsonValueCodec.Serialize(effective) : null,
                sensitive, matched));
        }

        public async Task<MHSConfigCommitResult> CommitAsync(CancellationToken token)
        {
            EnsureOpen();
            MHSConfigCommitResult result = await _edit.CommitAsync(token).ConfigureAwait(false);
            if (result.Success) Dispose();
            return result;
        }
        private void LoadCurrent() { _edit.ReplaceWorkingCopy(FCMJsonValueCodec.Deserialize<T>(_states[_position])); }
        private static T Clone(T value)
        { return FCMJsonValueCodec.Deserialize<T>(FCMJsonValueCodec.Serialize(value)); }
        private void EnsureOpen()
        { if (_edit == null) throw new ObjectDisposedException("MHSConfigEditorModel"); }
        public void Dispose()
        {
            MHSConfigEdit<T> edit = _edit;
            _edit = null;
            if (edit != null) edit.Dispose();
            _states.Clear();
        }
    }
}
