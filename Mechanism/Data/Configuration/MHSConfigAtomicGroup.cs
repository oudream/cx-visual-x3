using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Mechanism.Data.Configuration
{
    /// <summary>
    /// 这是一个物理配置实例，不是多个独立配置文件的批量调用。
    /// 整组成員使用同一版本、事务、有效标记和任务快照。
    /// </summary>
    [FCMConfigSection("Mechanism.Config.AtomicGroup")]
    public sealed class MHSConfigGroupDocument
    {
        [FCMConfigItem("层级档案定义", ApplyPolicy = FCMConfigApplyPolicy.NextTask)]
        public string ProfileDefinition { get; set; }
        [FCMConfigItem("配置成员", ApplyPolicy = FCMConfigApplyPolicy.NextTask)]
        public Dictionary<string, MHSConfigGroupEntry> Members { get; set; } =
            new Dictionary<string, MHSConfigGroupEntry>(StringComparer.Ordinal);
    }

    public sealed class MHSConfigGroupEntry
    {
        public string SectionId { get; set; }
        public int SchemaVersion { get; set; }
        public string Json { get; set; }
    }

    internal sealed class MHSConfigGroupRegistration
    {
        public string Key;
        public Type ModelType;
        public FCMConfigSchema Schema;
        public Func<string, string, CancellationToken, Task<FCMConfigValidationReport>> ValidateAsync;
    }

    /// <summary>Bound, typed member key. Handles from another atomic group are rejected.</summary>
    public sealed class MHSConfigGroupMember<T> where T : new()
    {
        internal MHSConfigGroupMember(string owner, string key) { Owner = owner; Key = key; }
        internal string Owner { get; private set; }
        public string Key { get; private set; }
    }

    /// <summary>一个持久修订的多个成员；对外提供隔离的强类型读取。</summary>
    public sealed class MHSConfigGroupSnapshot : IDisposable
    {
        private MHSConfigGroupDocument _document;
        private readonly string _owner;
        internal MHSConfigGroupSnapshot(string owner, string revision, MHSConfigGroupDocument document)
        { _owner = owner; Version = revision; _document = Copy(document); }
        public string Version { get; private set; }
        public IList<string> MemberKeys
        {
            get { return new List<string>(Active.Members.Keys.OrderBy(x => x, StringComparer.Ordinal)).AsReadOnly(); }
        }
        public bool Contains(string key) { return Active.Members.ContainsKey(key); }
        public T Get<T>(string key) where T : new()
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Member key required", "key");
            MHSConfigGroupEntry entry;
            if (!Active.Members.TryGetValue(key, out entry)) return new T();
            FCMConfigSchema schema = FCMConfigSchemaBuilder.Describe<T>();
            if (entry.SectionId != schema.SectionId || entry.SchemaVersion != schema.SchemaVersion)
                throw new InvalidOperationException("CONFIG_GROUP_MEMBER_SCHEMA_MISMATCH: " + key);
            return FCMJsonValueCodec.Deserialize<T>(entry.Json);
        }
        public T Get<T>(MHSConfigGroupMember<T> member) where T : new()
        {
            if (member == null) throw new ArgumentNullException("member");
            if (!string.Equals(member.Owner, _owner, StringComparison.Ordinal))
                throw new InvalidOperationException("CONFIG_GROUP_FOREIGN_MEMBER");
            return Get<T>(member.Key);
        }
        private MHSConfigGroupDocument Active
        {
            get
            {
                if (_document == null) throw new ObjectDisposedException("MHSConfigGroupSnapshot");
                return _document;
            }
        }
        internal static MHSConfigGroupDocument Copy(MHSConfigGroupDocument document)
        { return FCMJsonValueCodec.Deserialize<MHSConfigGroupDocument>(FCMJsonValueCodec.Serialize(document)); }
        public void Dispose() { Interlocked.Exchange(ref _document, null); }
    }

    /// <summary>
    /// 跨多个配置类型的一个原子提交边界；校验所有成员，任意一项失败均拒绝整个事务。
    /// </summary>
    public sealed class MHSConfigAtomicEdit : IDisposable
    {
        private MHSConfigEdit<MHSConfigGroupDocument> _inner;
        private readonly IList<MHSConfigGroupRegistration> _registered;
        private readonly string _scope;
        private readonly string _owner;
        internal MHSConfigAtomicEdit(MHSConfigEdit<MHSConfigGroupDocument> edit,
            IList<MHSConfigGroupRegistration> registered, string scope, string owner)
        { _inner = edit; _registered = registered; _scope = scope; _owner = owner; }
        private string MemberKey<T>(MHSConfigGroupMember<T> member) where T : new()
        {
            if (member == null) throw new ArgumentNullException("member");
            if (!string.Equals(member.Owner, _owner, StringComparison.Ordinal))
                throw new InvalidOperationException("CONFIG_GROUP_FOREIGN_MEMBER");
            return member.Key;
        }
        public void Set<T>(MHSConfigGroupMember<T> member, T value) where T : new()
        { Set(MemberKey(member), value); }
        public T Get<T>(MHSConfigGroupMember<T> member) where T : new()
        { return Get<T>(MemberKey(member)); }
        public void Remove<T>(MHSConfigGroupMember<T> member) where T : new()
        { Remove(MemberKey(member)); }

        public void Set<T>(string key, T value) where T : new()
        {
            EnsureOpen();
            if (value == null) throw new ArgumentNullException("value");
            MHSConfigGroupRegistration registration = Lookup(key);
            if (registration.ModelType != typeof(T))
                throw new InvalidOperationException("CONFIG_GROUP_MEMBER_TYPE_MISMATCH: " + key);
            string json = FCMJsonValueCodec.Serialize(value);
            // Type roundtrip must be valid before any write.
            FCMJsonValueCodec.Deserialize<T>(json);
            _inner.WorkingCopy.Members[key] = new MHSConfigGroupEntry
            {
                SectionId = registration.Schema.SectionId,
                SchemaVersion = registration.Schema.SchemaVersion,
                Json = json
            };
        }
        internal void SetProfileDefinition(string definition)
        {
            EnsureOpen();
            if (string.IsNullOrWhiteSpace(definition)) throw new ArgumentException("Definition required");
            _inner.WorkingCopy.ProfileDefinition = definition;
        }
        public void Remove(string key)
        {
            EnsureOpen();
            Lookup(key);
            _inner.WorkingCopy.Members.Remove(key);
        }
        public T Get<T>(string key) where T : new()
        {
            EnsureOpen();
            MHSConfigGroupRegistration registration = Lookup(key);
            if (registration.ModelType != typeof(T))
                throw new InvalidOperationException("CONFIG_GROUP_MEMBER_TYPE_MISMATCH: " + key);
            MHSConfigGroupEntry entry;
            return _inner.WorkingCopy.Members.TryGetValue(key, out entry)
                ? FCMJsonValueCodec.Deserialize<T>(entry.Json) : new T();
        }
        public async Task<MHSConfigCommitResult> CommitAsync(CancellationToken cancellationToken)
        {
            EnsureOpen();
            cancellationToken.ThrowIfCancellationRequested();
            MHSConfigGroupDocument draft = MHSConfigGroupSnapshot.Copy(_inner.WorkingCopy);
            FCMConfigValidationReport validation = await ValidateAllAsync(draft, cancellationToken)
                .ConfigureAwait(false);
            if (!validation.IsValid)
                return new MHSConfigCommitResult { Success = false, ErrorCode = "CONFIG_GROUP_VALIDATION",
                    Validation = validation };
            // _inner cannot be accessed/mutated by callers; the frozen candidate is handed to its
            // established single-file Compare-And-Swap commit + audit pipeline.
            _inner.ReplaceWorkingCopy(draft);
            return await _inner.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        private async Task<FCMConfigValidationReport> ValidateAllAsync(MHSConfigGroupDocument draft,
            CancellationToken cancellationToken)
        {
            FCMConfigValidationReport report = new FCMConfigValidationReport();
            Dictionary<string, MHSConfigGroupRegistration> registry =
                _registered.ToDictionary(x => x.Key, x => x, StringComparer.Ordinal);
            foreach (KeyValuePair<string, MHSConfigGroupEntry> item in
                draft.Members.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                MHSConfigGroupRegistration registration;
                if (!registry.TryGetValue(item.Key, out registration) || item.Value == null ||
                    item.Value.SectionId != registration.Schema.SectionId ||
                    item.Value.SchemaVersion != registration.Schema.SchemaVersion)
                {
                    report.Add(FCMConfigIssue.Error("Members[" + item.Key + "]",
                        "CONFIG_GROUP_SCHEMA", "Member not registered or schema mismatched"));
                    continue;
                }
                try
                {
                    FCMConfigValidationReport detail = await registration.ValidateAsync(
                        item.Value.Json, _scope, cancellationToken).ConfigureAwait(false);
                    foreach (FCMConfigIssue issue in detail.Issues)
                        report.Add(new FCMConfigIssue("Members[" + item.Key + "]." + issue.Path,
                            issue.Code, issue.Message, issue.Severity));
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    report.Add(FCMConfigIssue.Error("Members[" + item.Key + "]",
                        "CONFIG_GROUP_INVALID_MEMBER", ex.Message));
                }
            }
            return report;
        }
        private MHSConfigGroupRegistration Lookup(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Member key required", "key");
            MHSConfigGroupRegistration item = _registered.FirstOrDefault(x => x.Key == key);
            if (item == null) throw new InvalidOperationException("CONFIG_GROUP_UNKNOWN_MEMBER: " + key);
            return item;
        }
        private void EnsureOpen()
        { if (_inner == null) throw new ObjectDisposedException("MHSConfigAtomicEdit"); }
        public void Dispose()
        {
            MHSConfigEdit<MHSConfigGroupDocument> edit = Interlocked.Exchange(ref _inner, null);
            if (edit != null) edit.Dispose();
        }
    }

    /// <summary>
    /// 单次原子修订/生效/快照。成员模型保持普通强类型 C# 类；
    /// 不增加第三套文件存储或新的独立 ConfigManager。
    /// </summary>
    public sealed class MHSConfigAtomicGroup
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, MHSConfigGroupRegistration> _members =
            new Dictionary<string, MHSConfigGroupRegistration>(StringComparer.Ordinal);
        private readonly MHSConfigSection<MHSConfigGroupDocument> _section;
        private bool _sealed;
        internal MHSConfigAtomicGroup(MHSConfigSection<MHSConfigGroupDocument> section)
        { _section = section ?? throw new ArgumentNullException("section"); }
        public FCMConfigIdentity Identity { get { return _section.Identity; } }
        public string SavedVersion { get { return _section.SavedVersion; } }
        public string EffectiveVersion { get { return _section.EffectiveVersion; } }
        public bool HasPendingApply { get { return _section.HasPendingApply; } }
        public FCMConfigRevision Revision { get { return _section.Revision; } }
        internal string SavedProfileDefinition { get { return _section.GetLatestSaved().ProfileDefinition; } }
        internal string EffectiveProfileDefinition { get { return _section.Get().ProfileDefinition; } }
        internal int SavedMemberCount { get { return _section.GetLatestSaved().Members.Count; } }
        internal int EffectiveMemberCount { get { return _section.Get().Members.Count; } }

        public MHSConfigGroupMember<T> Register<T>(string key) where T : new()
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Member key required", "key");
            FCMConfigSchema schema = FCMConfigSchemaBuilder.Describe<T>();
            lock (_gate)
            {
                if (_sealed) throw new InvalidOperationException("CONFIG_GROUP_REGISTRY_SEALED");
                if (_members.ContainsKey(key)) throw new InvalidOperationException("CONFIG_GROUP_DUPLICATE_MEMBER: " + key);
                _members.Add(key, new MHSConfigGroupRegistration
                {
                    Key = key, ModelType = typeof(T), Schema = schema,
                    ValidateAsync = async (json, scope, token) =>
                    {
                        T model = FCMJsonValueCodec.Deserialize<T>(json);
                        return await FCMConfigValidationEngine.ValidateAsync(model, scope, token)
                            .ConfigureAwait(false);
                    }
                });
                return new MHSConfigGroupMember<T>(_section.Identity.CanonicalKey, key);
            }
        }
        public void Seal()
        {
            lock (_gate)
            {
                foreach (KeyValuePair<string, MHSConfigGroupEntry> item in
                    _section.GetLatestSaved().Members)
                {
                    MHSConfigGroupRegistration known;
                    if (!_members.TryGetValue(item.Key, out known) || item.Value == null ||
                        item.Value.SectionId != known.Schema.SectionId ||
                        item.Value.SchemaVersion != known.Schema.SchemaVersion)
                        throw new InvalidOperationException("CONFIG_GROUP_UNREGISTERED_STORED_MEMBER: " + item.Key);
                }
                _sealed = true;
            }
        }
        public async Task<MHSConfigCommitResult> UpdateAsync(Action<MHSConfigAtomicEdit> change,
            string reason, CancellationToken token)
        {
            if (change == null) throw new ArgumentNullException("change");
            using (MHSConfigAtomicEdit edit = BeginEdit(reason))
            {
                change(edit);
                return await edit.CommitAsync(token).ConfigureAwait(false);
            }
        }
        public MHSConfigAtomicEdit BeginEdit(string reason)
        {
            lock (_gate)
            {
                RequireSealed();
                return new MHSConfigAtomicEdit(_section.BeginEdit(reason),
                    _members.Values.OrderBy(x => x.Key, StringComparer.Ordinal).ToArray(),
                    _section.ScopeId, Identity.CanonicalKey);
            }
        }
        public MHSConfigGroupSnapshot CaptureForTask(string taskId)
        {
            lock (_gate) RequireSealed();
            using (MHSConfigSnapshot<MHSConfigGroupDocument> snapshot = _section.CaptureForTask(taskId))
                return new MHSConfigGroupSnapshot(Identity.CanonicalKey, snapshot.Version, snapshot.Get());
        }
        public MHSConfigGroupSnapshot CaptureSaved()
        {
            lock (_gate) RequireSealed();
            return new MHSConfigGroupSnapshot(Identity.CanonicalKey, _section.SavedVersion, _section.GetLatestSaved());
        }
        public MHSConfigApplyResult TryApplyPendingAtBoundary(FCMConfigApplyPolicy boundary,
            Func<MHSConfigApplyReview<MHSConfigGroupDocument>, MHSConfigApplyDecision> approval)
        {
            lock (_gate) RequireSealed();
            return _section.TryApplyPendingAtBoundary(boundary, approval);
        }
        public void ConfigureApplyGuard(
            Func<MHSConfigApplyReview<MHSConfigGroupDocument>, MHSConfigApplyDecision> guard)
        { _section.ConfigureApplyGuard(guard); }
        public MHSConfigStatus GetStatus() { return _section.GetStatus(); }
        public IList<string> ListSavedVersions() { return _section.ListSavedVersions(); }
        private void RequireSealed()
        { if (!_sealed) throw new InvalidOperationException("CONFIG_GROUP_NOT_SEALED"); }
    }
}
