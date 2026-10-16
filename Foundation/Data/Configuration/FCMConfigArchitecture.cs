using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace CxVisual.Foundation.Data.Configuration
{
    /// <summary>统一配置读视图。隔离副本意味着调用者修改结果不能回写配置真源。</summary>
    public interface IFCMConfigView<T>
    {
        T Get();
        V Value<V>(System.Linq.Expressions.Expression<Func<T, V>> selector);
        V Section<V>(System.Linq.Expressions.Expression<Func<T, V>> selector);
    }

    /// <summary>配置定义与实例严格分离，InstanceId 不拼接未转义的分隔符。</summary>
    public sealed class FCMConfigIdentity : IEquatable<FCMConfigIdentity>
    {
        public FCMConfigIdentity(string sectionId, string scopeKind, string scopeId)
        {
            SectionId = Required(sectionId, "sectionId");
            ScopeKind = Required(scopeKind, "scopeKind");
            ScopeId = Required(scopeId, "scopeId");
        }
        public string SectionId { get; private set; }
        public string ScopeKind { get; private set; }
        public string ScopeId { get; private set; }
        // 长度前缀保证 ":"、"/"、"_" 等合法字符不会形成身份碰撞。
        public string CanonicalKey { get { return Part(SectionId) + Part(ScopeKind) + Part(ScopeId); } }
        private static string Part(string v) { return v.Length.ToString(CultureInfo.InvariantCulture) + ":" + v; }
        private static string Required(string v, string name)
        {
            if (string.IsNullOrWhiteSpace(v)) throw new ArgumentException("Configuration identity is empty", name);
            return v;
        }
        public bool Equals(FCMConfigIdentity other)
        { return other != null && string.Equals(CanonicalKey, other.CanonicalKey, StringComparison.Ordinal); }
        public override bool Equals(object value) { return Equals(value as FCMConfigIdentity); }
        public override int GetHashCode() { return StringComparer.Ordinal.GetHashCode(CanonicalKey); }
        public override string ToString() { return CanonicalKey; }
    }

    /// <summary>一个实例的保存与生效修订；SchemaVersion 不等于配置内容版本。</summary>
    public sealed class FCMConfigRevision
    {
        public FCMConfigRevision(FCMConfigIdentity identity, string saved, string effective, int schemaVersion)
        {
            Identity = identity ?? throw new ArgumentNullException("identity");
            if (string.IsNullOrWhiteSpace(saved) || string.IsNullOrWhiteSpace(effective))
                throw new ArgumentException("Missing config revision");
            if (schemaVersion < 1) throw new ArgumentOutOfRangeException("schemaVersion");
            Saved = saved; Effective = effective; SchemaVersion = schemaVersion;
        }
        public FCMConfigIdentity Identity { get; private set; }
        public string Saved { get; private set; }
        public string Effective { get; private set; }
        public int SchemaVersion { get; private set; }
        public bool IsPending { get { return !string.Equals(Saved, Effective, StringComparison.Ordinal); } }
        public string CanonicalKey
        {
            get
            {
                return Identity.CanonicalKey + Saved.Length + ":" + Saved +
                    Effective.Length + ":" + Effective + SchemaVersion.ToString(CultureInfo.InvariantCulture);
            }
        }
    }

    /// <summary>对一组配置来源的一致版本身份；顺序、重名与哈希完全确定。</summary>
    public sealed class FCMConfigRevisionVector : IEquatable<FCMConfigRevisionVector>
    {
        private readonly ReadOnlyCollection<FCMConfigRevision> _entries;
        public FCMConfigRevisionVector(IEnumerable<FCMConfigRevision> revisions)
        {
            if (revisions == null) throw new ArgumentNullException("revisions");
            FCMConfigRevision[] unsorted = revisions.ToArray();
            if (unsorted.Any(x => x == null)) throw new ArgumentException("Null revision");
            FCMConfigRevision[] sorted = unsorted.OrderBy(x => x.Identity.CanonicalKey, StringComparer.Ordinal).ToArray();
            if (sorted.Select(x => x.Identity.CanonicalKey).Distinct(StringComparer.Ordinal).Count() != sorted.Length)
                throw new ArgumentException("Duplicate instance in revision vector");
            _entries = Array.AsReadOnly(sorted);
            string data = string.Join("\n", sorted.Select(x => x.CanonicalKey));
            using (SHA256 sha = SHA256.Create())
                Fingerprint = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(data))).Replace("-", "").ToLowerInvariant();
        }
        public IList<FCMConfigRevision> Entries { get { return _entries; } }
        public string Fingerprint { get; private set; }
        public bool HasPending { get { return _entries.Any(x => x.IsPending); } }
        public bool Equals(FCMConfigRevisionVector other)
        {
            return other != null && string.Equals(Fingerprint, other.Fingerprint, StringComparison.Ordinal) &&
                _entries.Count == other._entries.Count &&
                !_entries.Where((x, i) => x.CanonicalKey != other._entries[i].CanonicalKey).Any();
        }
        public override bool Equals(object other) { return Equals(other as FCMConfigRevisionVector); }
        public override int GetHashCode() { return StringComparer.Ordinal.GetHashCode(Fingerprint); }
    }

    public enum FCMConfigValuePresence { Missing, ExplicitNull, Defaulted, Present, Invalid }

    /// <summary>存在性与数值分开，避免数值 default(T) 被误认为存在的历史值。</summary>
    public sealed class FCMConfigValueState<T>
    {
        private FCMConfigValueState(FCMConfigValuePresence presence, T value, string origin)
        { Presence = presence; Value = value; Origin = origin; }
        public FCMConfigValuePresence Presence { get; private set; }
        public bool HasValue { get { return Presence == FCMConfigValuePresence.Present || Presence == FCMConfigValuePresence.Defaulted || Presence == FCMConfigValuePresence.ExplicitNull; } }
        public T Value { get; private set; }
        public string Origin { get; private set; }
        public static FCMConfigValueState<T> Missing()
        { return new FCMConfigValueState<T>(FCMConfigValuePresence.Missing, default(T), null); }
        public static FCMConfigValueState<T> Invalid(string origin)
        { return new FCMConfigValueState<T>(FCMConfigValuePresence.Invalid, default(T), origin); }
        public static FCMConfigValueState<T> Of(T value, string origin, bool isDefault)
        {
            FCMConfigValuePresence kind = object.ReferenceEquals(value, null)
                ? FCMConfigValuePresence.ExplicitNull
                : isDefault ? FCMConfigValuePresence.Defaulted : FCMConfigValuePresence.Present;
            return new FCMConfigValueState<T>(kind, value, origin);
        }
    }

    public sealed class FCMConfigFieldChange
    {
        public FCMConfigFieldChange(string path, string oldJson, string newJson)
        {
            if (path == null) throw new ArgumentNullException("path");
            Path = path; OldJson = oldJson; NewJson = newJson;
        }
        public string Path { get; private set; }
        public string OldJson { get; private set; }
        public string NewJson { get; private set; }
    }

    /// <summary>一次事务的结构化变更集合，携带预期版本和按字典序排序的字段列表。</summary>
    public sealed class FCMConfigChangeSet
    {
        public FCMConfigChangeSet(FCMConfigIdentity identity, string expectedRevision,
            IEnumerable<FCMConfigFieldChange> fields)
        {
            Identity = identity ?? throw new ArgumentNullException("identity");
            ExpectedRevision = expectedRevision ?? throw new ArgumentNullException("expectedRevision");
            if (fields == null) throw new ArgumentNullException("fields");
            FCMConfigFieldChange[] items = fields.OrderBy(x => x.Path, StringComparer.Ordinal).ToArray();
            if (items.Any(x => x == null) || items.Select(x => x.Path).Distinct(StringComparer.Ordinal).Count() != items.Length)
                throw new ArgumentException("Duplicate or null configuration paths");
            Fields = Array.AsReadOnly(items);
        }
        public FCMConfigIdentity Identity { get; private set; }
        public string ExpectedRevision { get; private set; }
        public IList<FCMConfigFieldChange> Fields { get; private set; }
        public bool IsEmpty { get { return Fields.Count == 0; } }
    }

    /// <summary>单向依赖（依赖者->被依赖路径）；环检测、增量影响集合、拓扑顺序来自同一图。</summary>
    public sealed class FCMConfigDependencyGraph
    {
        private readonly Dictionary<string, HashSet<string>> _dependencies =
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        public void Register(string dependent, string dependency)
        {
            Require(dependent); Require(dependency);
            if (!_dependencies.ContainsKey(dependent))
                _dependencies[dependent] = new HashSet<string>(StringComparer.Ordinal);
            if (!_dependencies.ContainsKey(dependency))
                _dependencies[dependency] = new HashSet<string>(StringComparer.Ordinal);
            if (!_dependencies[dependent].Add(dependency)) return;
            try { TopologicalOrder(); }
            catch { _dependencies[dependent].Remove(dependency); throw; }
        }
        public IList<string> TopologicalOrder()
        {
            List<string> order = new List<string>();
            Dictionary<string, int> state = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string key in _dependencies.Keys.OrderBy(x => x, StringComparer.Ordinal))
                Visit(key, state, order);
            return order.AsReadOnly();
        }
        public IList<string> AffectedBy(string changed)
        {
            Require(changed);
            HashSet<string> affected = new HashSet<string>(StringComparer.Ordinal);
            Queue<string> pending = new Queue<string>();
            pending.Enqueue(changed);
            while (pending.Count != 0)
            {
                string source = pending.Dequeue();
                foreach (KeyValuePair<string, HashSet<string>> pair in _dependencies)
                    if (pair.Value.Contains(source) && affected.Add(pair.Key)) pending.Enqueue(pair.Key);
            }
            return TopologicalOrder().Where(affected.Contains).ToArray();
        }
        private void Visit(string key, IDictionary<string, int> state, IList<string> order)
        {
            int value;
            if (state.TryGetValue(key, out value))
            {
                if (value == 1) throw new InvalidOperationException("CONFIG_DEPENDENCY_CYCLE: " + key);
                return;
            }
            state[key] = 1;
            foreach (string dep in _dependencies[key].OrderBy(x => x, StringComparer.Ordinal))
                Visit(dep, state, order);
            state[key] = 2;
            order.Add(key);
        }
        private static void Require(string path)
        { if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Empty configuration dependency path"); }
    }
}
