using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Mechanism.Data.Configuration
{
    /// <summary>原子层级配置的事务草稿，可同时修改多个来源层而只提交一次。</summary>
    public sealed class MVBConfigAtomicLayerEdit<T> : IDisposable where T : new()
    {
        private MHSConfigAtomicEdit _edit;
        private readonly IList<MVBConfigLayerSpec> _layers;
        private readonly FCMConfigIdentity _groupIdentity;
        private readonly string _version;
        private readonly string _definition;
        internal MVBConfigAtomicLayerEdit(MHSConfigAtomicEdit edit, IList<MVBConfigLayerSpec> layers,
            FCMConfigIdentity groupIdentity, string version, string definition)
        { _edit = edit; _layers = layers; _groupIdentity = groupIdentity;
          _version = version; _definition = definition; }

        public void Set<V>(string layerName, Expression<Func<T, V>> selector, V value)
        {
            EnsureOpen();
            string path = FCMConfigPathUtil.FromExpression(selector);
            string json = FCMJsonValueCodec.Serialize(value);
            MVBConfigLayerPath.Validate<T>(path, json);
            MVBConfigLayerOverrides doc = Layer(layerName);
            doc.Values[path] = json;
            _edit.Set(layerName, doc);
        }
        public void Remove<V>(string layerName, Expression<Func<T, V>> selector)
        {
            EnsureOpen();
            string path = FCMConfigPathUtil.FromExpression(selector);
            MVBConfigLayerOverrides doc = Layer(layerName);
            doc.Values.Remove(path);
            _edit.Set(layerName, doc);
        }
        public MVBConfigResolved<T> Preview()
        {
            EnsureOpen();
            return MVBConfigAtomicLayerProfile<T>.Compose(_layers,
                name => _edit.Get<MVBConfigLayerOverrides>(name), _version, _groupIdentity);
        }
        public async Task<MHSConfigCommitResult> CommitAsync(CancellationToken token)
        {
            EnsureOpen();
            token.ThrowIfCancellationRequested();
            T candidate;
            try { candidate = Preview().Get(); }
            catch (Exception ex)
            {
                FCMConfigValidationReport invalid = new FCMConfigValidationReport();
                invalid.Add(FCMConfigIssue.Error("", "CONFIG_LAYER_COMPOSE", ex.Message));
                return new MHSConfigCommitResult { Success = false,
                    ErrorCode = "CONFIG_ATOMIC_LAYER_VALIDATION", Validation = invalid };
            }
            FCMConfigValidationReport report = await FCMConfigValidationEngine.ValidateAsync(
                candidate, "atomic-layer", token).ConfigureAwait(false);
            if (!report.IsValid)
                return new MHSConfigCommitResult { Success = false,
                    ErrorCode = "CONFIG_ATOMIC_LAYER_VALIDATION", Validation = report };
            _edit.SetProfileDefinition(_definition);
            return await _edit.CommitAsync(token).ConfigureAwait(false);
        }
        private MVBConfigLayerOverrides Layer(string name)
        {
            if (!_layers.Any(x => x.Name == name))
                throw new KeyNotFoundException("Unknown config layer: " + name);
            return _edit.Get<MVBConfigLayerOverrides>(name);
        }
        private void EnsureOpen()
        { if (_edit == null) throw new ObjectDisposedException("MVBConfigAtomicLayerEdit"); }
        public void Dispose()
        {
            MHSConfigAtomicEdit edit = Interlocked.Exchange(ref _edit, null);
            if (edit != null) edit.Dispose();
        }
    }

    /// <summary>
    /// 五层显式覆盖作为一个物理配置聚合单元持久化，保证跨层一次提交/生效/快照。
    /// 不修改其它原本独立的 MVBConfigLayeredProfile<T>。
    /// </summary>
    public sealed class MVBConfigAtomicLayerProfile<T> where T : new()
    {
        private readonly MHSConfigAtomicGroup _group;
        private readonly IList<MVBConfigLayerSpec> _layers;
        private readonly string _definition;
        private readonly MHSConfigService _service;
        private readonly string _groupId;
        private readonly string _profileId;
        public MVBConfigAtomicLayerProfile(MHSConfigService service, string profileId,
            params MVBConfigLayerSpec[] layers)
        {
            if (service == null) throw new ArgumentNullException("service");
            if (layers == null || layers.Length == 0 || layers.Any(x => x == null))
                throw new ArgumentException("At least one source layer required", "layers");
            if (layers.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != layers.Length ||
                layers.Select(x => x.Priority).Distinct().Count() != layers.Length ||
                layers.Select(x => x.Kind + "\0" + x.Id).Distinct(StringComparer.Ordinal).Count() != layers.Length)
                throw new ArgumentException("Layer names and priorities must be unique");
            if (string.IsNullOrWhiteSpace(profileId))
                throw new ArgumentException("ProfileId required", "profileId");
            string modelSectionId = FCMConfigSchemaBuilder.Describe<T>().SectionId;
            _service = service;
            _profileId = profileId;
            _layers = Array.AsReadOnly(layers.OrderBy(x => x.Priority).ToArray());
            _definition = Contract(profileId, _layers);
            // Length-prefixed identity prevents ambiguous group names across Section IDs.
            string groupId = modelSectionId.Length + ":" + modelSectionId +
                profileId.Length + ":" + profileId;
            _groupId = "atomic-layer:" + groupId;
            _group = service.OpenAtomicGroup(_groupId);
            foreach (MVBConfigLayerSpec layer in _layers)
                _group.Register<MVBConfigLayerOverrides>(layer.Name);
            _group.Seal();
            RequireSavedDefinition();
        }
        private static string Part(string value)
        { return value.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + value; }
        internal static string Contract(string profileId, IEnumerable<MVBConfigLayerSpec> layers)
        {
            FCMConfigSchema schema = FCMConfigSchemaBuilder.Describe<T>();
            return "v1" + Part(schema.SectionId) + Part(profileId) +
                Part(schema.SchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)) +
                string.Concat(layers.OrderBy(x => x.Priority).Select(x => Part(x.Name) +
                    Part(x.Kind) + Part(x.Id) +
                    Part(x.Priority.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        }
        private void AssertDefinition(string stored, int memberCount)
        {
            if (stored == null && memberCount == 0) return; // fresh empty profile
            if (stored == null) throw new InvalidOperationException("CONFIG_LAYER_DEFINITION_UNVERSIONED");
            if (!string.Equals(stored, _definition, StringComparison.Ordinal))
                throw new InvalidOperationException("CONFIG_LAYER_DEFINITION_MISMATCH");
        }
        private void RequireSavedDefinition()
        { AssertDefinition(_group.SavedProfileDefinition, _group.SavedMemberCount); }
        private void RequireEffectiveDefinition()
        { AssertDefinition(_group.EffectiveProfileDefinition, _group.EffectiveMemberCount); }
        /// <summary>
        /// A reviewed layer-definition change uses the SAME atomic group transaction.
        /// Removed layers with values require a move map; no entry is silently discarded.
        /// After committing, open a new profile with the replacement definition to apply.
        /// Existing pinned snapshots retain their original revision.
        /// </summary>
        public async Task<MHSConfigCommitResult> UpgradeDefinitionAsync(
            MVBConfigLayerSpec[] replacement, IDictionary<string, string> movedLayers,
            string reason, CancellationToken token)
        {
            RequireSavedDefinition();
            token.ThrowIfCancellationRequested();
            if (replacement == null || replacement.Length == 0 ||
                replacement.Any(x => x == null) ||
                replacement.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != replacement.Length ||
                replacement.Select(x => x.Priority).Distinct().Count() != replacement.Length ||
                replacement.Select(x => x.Kind + "\0" + x.Id).Distinct(StringComparer.Ordinal).Count() != replacement.Length)
                throw new ArgumentException("Invalid replacement layer definition");
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Upgrade reason required");
            MVBConfigLayerSpec[] sorted = replacement.OrderBy(x => x.Priority).ToArray();
            string next = Contract(_profileId, sorted);
            if (string.Equals(next, _definition, StringComparison.Ordinal))
                return new MHSConfigCommitResult { Success = true, Revision = SavedVersion,
                    PendingApply = HasPendingApply };
            HashSet<string> oldNames = new HashSet<string>(_layers.Select(x => x.Name), StringComparer.Ordinal);
            HashSet<string> newNames = new HashSet<string>(sorted.Select(x => x.Name), StringComparer.Ordinal);
            MHSConfigAtomicGroup upgrade = _service.OpenAtomicGroup(_groupId);
            foreach (string name in oldNames.Union(newNames).OrderBy(x => x, StringComparer.Ordinal))
                upgrade.Register<MVBConfigLayerOverrides>(name);
            upgrade.Seal();
            using (MHSConfigAtomicEdit edit = upgrade.BeginEdit(reason))
            {
                foreach (string oldName in oldNames.Where(x => !newNames.Contains(x)))
                {
                    MVBConfigLayerOverrides removed = edit.Get<MVBConfigLayerOverrides>(oldName);
                    string newName;
                    bool mapped = movedLayers != null &&
                        movedLayers.TryGetValue(oldName, out newName) && newNames.Contains(newName);
                    if (removed.Values.Count > 0 && !mapped)
                        return new MHSConfigCommitResult { Success = false,
                            ErrorCode = "CONFIG_LAYER_MIGRATION_REQUIRES_MAPPING" };
                    if (mapped)
                    {
                        MVBConfigLayerOverrides target = edit.Get<MVBConfigLayerOverrides>(newName);
                        foreach (var entry in removed.Values)
                        {
                            if (target.Values.ContainsKey(entry.Key))
                                return new MHSConfigCommitResult { Success = false,
                                    ErrorCode = "CONFIG_LAYER_MIGRATION_OVERLAP" };
                            target.Values.Add(entry.Key, entry.Value);
                        }
                        edit.Set(newName, target);
                    }
                    edit.Remove(oldName);
                }
                T candidate;
                try
                {
                    candidate = Compose(sorted, name => edit.Get<MVBConfigLayerOverrides>(name),
                        SavedVersion, upgrade.Identity).Get();
                }
                catch (Exception)
                {
                    return new MHSConfigCommitResult { Success = false,
                        ErrorCode = "CONFIG_LAYER_UPGRADE_INVALID" };
                }
                FCMConfigValidationReport report = await FCMConfigValidationEngine.ValidateAsync(
                    candidate, "atomic-layer", token).ConfigureAwait(false);
                if (!report.IsValid)
                    return new MHSConfigCommitResult { Success = false,
                        ErrorCode = "CONFIG_LAYER_UPGRADE_INVALID", Validation = report };
                edit.SetProfileDefinition(next);
                return await edit.CommitAsync(token).ConfigureAwait(false);
            }
        }

        public FCMConfigIdentity Identity { get { return _group.Identity; } }
        public string SavedVersion { get { return _group.SavedVersion; } }
        public string EffectiveVersion { get { return _group.EffectiveVersion; } }
        public bool HasPendingApply { get { return _group.HasPendingApply; } }
        public async Task<MHSConfigCommitResult> UpdateAsync(Action<MVBConfigAtomicLayerEdit<T>> change,
            string reason, CancellationToken token)
        {
            if (change == null) throw new ArgumentNullException("change");
            using (MVBConfigAtomicLayerEdit<T> edit = BeginEdit(reason))
            {
                change(edit);
                return await edit.CommitAsync(token).ConfigureAwait(false);
            }
        }
        public MVBConfigAtomicLayerEdit<T> BeginEdit(string reason)
        {
            RequireSavedDefinition();
            return new MVBConfigAtomicLayerEdit<T>(_group.BeginEdit(reason),
                _layers, _group.Identity, _group.SavedVersion, _definition);
        }
        public async Task<MHSConfigCommitResult> SetOverrideAsync<V>(string layer,
            Expression<Func<T, V>> selector, V value, CancellationToken token)
        {
            using (MVBConfigAtomicLayerEdit<T> edit = BeginEdit("修改层级参数"))
            {
                edit.Set(layer, selector, value);
                return await edit.CommitAsync(token).ConfigureAwait(false);
            }
        }
        public MVBConfigResolved<T> ResolveEffective()
        {
            RequireEffectiveDefinition();
            using (MHSConfigGroupSnapshot snapshot = _group.CaptureForTask("atomic-layer-resolve"))
                return Compose(_layers, name => snapshot.Get<MVBConfigLayerOverrides>(name),
                    snapshot.Version, _group.Identity);
        }
        public MVBConfigResolved<T> ResolveSaved()
        {
            RequireSavedDefinition();
            using (MHSConfigGroupSnapshot snapshot = _group.CaptureSaved())
                return Compose(_layers, name => snapshot.Get<MVBConfigLayerOverrides>(name),
                    snapshot.Version, _group.Identity);
        }
        public MVBConfigTaskSnapshot<T> CaptureForTask(string taskId)
        {
            if (string.IsNullOrWhiteSpace(taskId)) throw new ArgumentException("TaskId required", "taskId");
            return new MVBConfigTaskSnapshot<T>(taskId, ResolveEffective());
        }
        public MHSConfigApplyResult TryApplyPendingAtBoundary(FCMConfigApplyPolicy boundary,
            Func<MHSConfigApplyReview<MHSConfigGroupDocument>, MHSConfigApplyDecision> approval)
        { RequireSavedDefinition(); return _group.TryApplyPendingAtBoundary(boundary, approval); }
        public MHSConfigStatus GetStatus() { return _group.GetStatus(); }

        internal static MVBConfigResolved<T> Compose(IList<MVBConfigLayerSpec> layers,
            Func<string, MVBConfigLayerOverrides> get, string revision, FCMConfigIdentity identity)
        {
            T model = new T();
            List<MVBConfigLayerOrigin> origins = new List<MVBConfigLayerOrigin>();
            Dictionary<string, string> versions = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (MVBConfigLayerSpec layer in layers)
            {
                versions.Add(layer.Name, revision);
                MVBConfigLayerOverrides overrides = get(layer.Name);
                if (overrides == null || overrides.Values == null) continue;
                foreach (KeyValuePair<string, string> value in overrides.Values
                    .OrderBy(x => MVBConfigLayerPath.Depth(x.Key))
                    .ThenBy(x => x.Key, StringComparer.Ordinal))
                {
                    MVBConfigLayerPath.Apply(model, value.Key, value.Value);
                    origins.Add(new MVBConfigLayerOrigin(layer.Name, value.Key, revision));
                }
            }
            FCMConfigRevisionVector vector = new FCMConfigRevisionVector(new[]
            {
                new FCMConfigRevision(identity, revision, revision, 1)
            });
            return new MVBConfigResolved<T>(model, origins, versions, vector);
        }
    }
}
