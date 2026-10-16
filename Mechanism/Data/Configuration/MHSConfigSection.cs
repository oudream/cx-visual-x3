using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Mechanism.Data.Configuration
{
    public sealed class MHSConfigPreview
    {
        public bool HasChanges { get; internal set; }
        public FCMConfigValidationReport Validation { get; internal set; }
        public string BaseVersion { get; internal set; }
        public FCMConfigChangeSet ChangeSet { get; internal set; }
        public string CandidateJson { get; internal set; }
        public IList<string> ChangedPaths { get; internal set; }
    }
    public sealed class MHSConfigCommitResult
    {
        public bool Success { get; internal set; }
        public string Revision { get; internal set; }
        public FCMConfigChangeSet ChangeSet { get; internal set; }
        public bool PendingApply { get; internal set; }
        public FCMConfigValidationReport Validation { get; internal set; }
        public string ErrorCode { get; internal set; }
    }
    public sealed class MHSConfigSnapshot<T> : IFCMConfigView<T>, IDisposable
    {
        private T _data;
        internal MHSConfigSnapshot(T data, string version) { _data = data; Version = version; }
        public string Version { get; private set; }
        public TValue Value<TValue>(Expression<Func<T, TValue>> accessor)
        {
            if (_data == null) throw new ObjectDisposedException("MHSConfigSnapshot");
            return FCMJsonValueCodec.Deserialize<TValue>(FCMJsonValueCodec.Serialize(accessor.Compile()(_data)));
        }
        public T Get()
        {
            if (_data == null) throw new ObjectDisposedException("MHSConfigSnapshot");
            return FCMJsonValueCodec.Deserialize<T>(FCMJsonValueCodec.Serialize(_data));
        }
        public TPart Section<TPart>(Expression<Func<T, TPart>> accessor) { return Value(accessor); }
        public void Dispose() { _data = default(T); }
    }
    public sealed class MHSConfigEdit<T> : IDisposable where T : new()
    {
        private MHSConfigSection<T> _section;
        private readonly int _baseRevision;
        public T WorkingCopy { get; private set; }
        public string Reason { get; private set; }
        internal MHSConfigEdit(MHSConfigSection<T> section, T model, int revision, string reason)
        { _section = section; WorkingCopy = model; _baseRevision = revision; Reason = reason; }
        public MHSConfigPreview Preview()
        {
            if (_section == null) throw new ObjectDisposedException("MHSConfigEdit");
            return _section.Preview(WorkingCopy, _baseRevision);
        }
        /// <summary>异步预览会固定草稿并执行与 Commit 相同的同步/异步校验规则。</summary>
        public async Task<MHSConfigPreview> PreviewAsync(CancellationToken cancellationToken)
        {
            if (_section == null) throw new ObjectDisposedException("MHSConfigEdit");
            cancellationToken.ThrowIfCancellationRequested();
            T candidate = FCMJsonValueCodec.Deserialize<T>(FCMJsonValueCodec.Serialize(WorkingCopy));
            FCMConfigValidationReport report = await FCMConfigValidationEngine.ValidateAsync(
                candidate, _section.ScopeId, cancellationToken).ConfigureAwait(false);
            return _section.PreviewValidated(candidate, _baseRevision, report);
        }
        public async Task<MHSConfigCommitResult> CommitAsync(CancellationToken cancellationToken)
        {
            if (_section == null) throw new ObjectDisposedException("MHSConfigEdit");
            cancellationToken.ThrowIfCancellationRequested();
            // Freeze the candidate before asynchronous validation, preventing the UI thread
            // from modifying values after they have already been validated.
            T candidate = FCMJsonValueCodec.Deserialize<T>(FCMJsonValueCodec.Serialize(WorkingCopy));
            FCMConfigValidationReport report = await FCMConfigValidationEngine.ValidateAsync(candidate,
                _section.ScopeId, cancellationToken).ConfigureAwait(false);
            return _section.Commit(candidate, _baseRevision, cancellationToken, report, Reason);
        }
        internal void ReplaceWorkingCopy(T value)
        {
            if (_section == null) throw new ObjectDisposedException("MHSConfigEdit");
            WorkingCopy = value;
        }
        public void Dispose() { _section = null; WorkingCopy = default(T); }
    }

    public sealed class MHSConfigSection<T> : IFCMConfigView<T>, IMHSConfigInstance where T : new()
    {
        private readonly object _gate = new object();
        private readonly MHSConfigStorage _store;
        private readonly FCMConfigSchema _schema;
        private readonly string _scope;
        private readonly MNMConfigChangePublisher<T> _publisher = new MNMConfigChangePublisher<T>();
        private string _savedJson;
        private T _saved;
        private int _savedRev;
        private T _effective;
        private int _effectiveRev;
        private T _previousEffective;
        private bool _hasPreviousEffective;
        private Func<MHSConfigApplyReview<T>, MHSConfigApplyDecision> _applyGuard;
        public FCMConfigIdentity Identity { get; private set; }
        public Type ConfigType { get { return typeof(T); } }

        internal MHSConfigSection(string directory, FCMConfigSchema schema, string scope, FCMConfigIdentity identity)
        {
            Identity = identity ?? throw new ArgumentNullException("identity");
            _schema = schema;
            _scope = scope;
            _store = new MHSConfigStorage(directory);
            _savedRev = _store.Initialize(FCMJsonValueCodec.Serialize(new T()));
            _savedJson = _store.ReadCurrent();
            _saved = FCMJsonValueCodec.Deserialize<T>(_savedJson);
            _effectiveRev = _store.ReadEffectiveOr(_savedRev);
            _store.SetEffective(_effectiveRev); // Establish durable effective marker before any later commit.
            _effective = FCMJsonValueCodec.Deserialize<T>(_store.ReadVersion(_effectiveRev));
            int previous = _store.PreviousEffectiveRevision;
            if (previous > 0)
            {
                _previousEffective = FCMJsonValueCodec.Deserialize<T>(_store.ReadVersion(previous));
                _hasPreviousEffective = true;
            }
        }
        internal string ScopeId { get { return _scope; } }
        public string SavedVersion { get { lock (_gate) return MHSConfigStorage.Name(_savedRev); } }
        public string EffectiveVersion { get { lock (_gate) return MHSConfigStorage.Name(_effectiveRev); } }
        public bool HasPendingApply { get { lock (_gate) return _savedRev != _effectiveRev; } }
        public long LastNotificationSequence { get { return _publisher.LastSequence; } }
        public FCMConfigRevision Revision
        {
            get
            {
                lock (_gate)
                    return new FCMConfigRevision(Identity, MHSConfigStorage.Name(_savedRev),
                        MHSConfigStorage.Name(_effectiveRev), _schema.SchemaVersion);
            }
        }
        /// <summary>同一实例只能设置一次公共生效许可回调，包含 Immediate 生效。</summary>
        public void ConfigureApplyGuard(Func<MHSConfigApplyReview<T>, MHSConfigApplyDecision> guard)
        {
            if (guard == null) throw new ArgumentNullException("guard");
            lock (_gate)
            {
                if (_applyGuard != null) throw new InvalidOperationException("CONFIG_APPLY_GUARD_ALREADY_REGISTERED");
                _applyGuard = guard;
            }
        }
        public T Get() { lock (_gate) return Clone(_effective); }
        public T GetLatestSaved() { lock (_gate) return Clone(_saved); }
        public TPart Section<TPart>(Expression<Func<T, TPart>> accessor) { return Value(accessor); }
        public TValue Value<TValue>(Expression<Func<T, TValue>> accessor)
        { lock (_gate) return CloneValue(accessor.Compile()(_effective)); }
        public FCMConfigValidationReport ValidateEffective()
        { lock (_gate) return FCMConfigValidationEngine.Validate(_effective, _scope); }
        /// <summary>一次性诊断，保存/生效/待生效/审计使用同一把实例锁。</summary>
        public MHSConfigStatus GetStatus()
        {
            lock (_gate)
            {
                FCMConfigValidationReport saved = FCMConfigValidationEngine.Validate(_saved, _scope);
                FCMConfigValidationReport effective = FCMConfigValidationEngine.Validate(_effective, _scope);
                FCMConfigChangeSet pending = BuildChangeSet(_effective, _saved, _effectiveRev);
                List<FCMConfigIssue> integrity = new List<FCMConfigIssue>();
                int auditCount = 0;
                try { auditCount = _store.ListAudit(_savedRev).Count; }
                catch (Exception ex)
                {
                    integrity.Add(FCMConfigIssue.Error("", "CONFIG_AUDIT_INTEGRITY_FAILURE", ex.Message));
                }
                return new MHSConfigStatus(Identity, _schema, Revision, pending,
                    saved, effective, integrity, auditCount);
            }
        }
        public MHSConfigSnapshot<T> CaptureForTask(string taskId)
        {
            if (string.IsNullOrWhiteSpace(taskId)) throw new ArgumentException("Missing taskId");
            lock (_gate)
            {
                FCMConfigValidationReport report = FCMConfigValidationEngine.Validate(_effective, _scope);
                if (!report.IsValid) throw new InvalidOperationException("CONFIG_EFFECTIVE_INVALID: cannot start task");
                return new MHSConfigSnapshot<T>(Clone(_effective), MHSConfigStorage.Name(_effectiveRev));
            }
        }
        public MHSConfigEdit<T> BeginEdit(string reason)
        { lock (_gate) return new MHSConfigEdit<T>(this, Clone(_saved), _savedRev, reason); }
        /// <summary>开发者单字段修改；仍经过相同校验、版本及持久化事务，不绕过安全检查。</summary>
        public async Task<MHSConfigCommitResult> SetValueAsync<V>(
            Expression<Func<T, V>> selector, V value, string reason, CancellationToken token)
        {
            if (selector == null) throw new ArgumentNullException("selector");
            using (MHSConfigEdit<T> edit = BeginEdit(reason))
            {
                MVBConfigLayerPath.Apply(edit.WorkingCopy, FCMConfigPathUtil.FromExpression(selector),
                    FCMJsonValueCodec.Serialize(value));
                return await edit.CommitAsync(token).ConfigureAwait(false);
            }
        }

        /// <summary>导出当前已保存配置；只创建含哈希的离线传输包，不执行网络发布。</summary>
        public MHSConfigPackage ExportPackage()
        {
            lock (_gate)
            {
                return new MHSConfigPackage
                {
                    SectionId = _schema.SectionId, SchemaVersion = _schema.SchemaVersion,
                    SourceScope = _scope, SourceRevision = MHSConfigStorage.Name(_savedRev),
                    CreatedAtUtc = DateTimeOffset.UtcNow, Json = _savedJson,
                    Sha256 = MHSConfigPackage.Hash(_savedJson)
                };
            }
        }

        /// <summary>校验包的身份/版本/哈希/业务规则，通过当前事务导入而不是覆盖文件。</summary>
        public async Task<MHSConfigCommitResult> ImportPackageAsync(MHSConfigPackage package,
            CancellationToken token)
        {
            if (package == null) throw new ArgumentNullException("package");
            if (package.SectionId != _schema.SectionId || package.SchemaVersion != _schema.SchemaVersion)
                return new MHSConfigCommitResult { Success = false, ErrorCode = "CONFIG_PACKAGE_SCHEMA_MISMATCH" };
            if (!package.Verify())
                return new MHSConfigCommitResult { Success = false, ErrorCode = "CONFIG_PACKAGE_HASH_MISMATCH" };
            token.ThrowIfCancellationRequested();
            T value;
            try { value = FCMJsonValueCodec.Deserialize<T>(package.Json); }
            catch (Exception)
            { return new MHSConfigCommitResult { Success = false, ErrorCode = "CONFIG_PACKAGE_INVALID_JSON" }; }
            int revision;
            lock (_gate) revision = _savedRev;
            FCMConfigValidationReport report = await FCMConfigValidationEngine.ValidateAsync(value, _scope, token)
                .ConfigureAwait(false);
            return Commit(value, revision, token, report, "导入离线配置包");
        }

        public T AtSavedVersion(string name)
        {
            lock (_gate)
            {
                int revision = MHSConfigStorage.ParseName(name);
                if (!_store.CommittedHistory(_savedRev).Contains(revision))
                    throw new ArgumentException("Not a committed ancestor of the current config: " + name);
                return FCMJsonValueCodec.Deserialize<T>(_store.ReadVersion(revision));
            }
        }
        /// <summary>读取已提交历史版本，返回与当前配置隔离的只读快照。</summary>
        public MHSConfigSnapshot<T> AtSavedVersionView(string name)
        {
            lock (_gate) return new MHSConfigSnapshot<T>(AtSavedVersion(name), name);
        }

        /// <summary>指定历史版本恢复为新的保存修订；不会修改原始历史。</summary>
        public async Task<MHSConfigCommitResult> RestoreSavedVersionAsync(string name, string reason,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Restore reason required", "reason");
            cancellationToken.ThrowIfCancellationRequested();
            T candidate;
            int baseline;
            lock (_gate)
            {
                candidate = AtSavedVersion(name);
                baseline = _savedRev;
            }
            FCMConfigValidationReport report = await FCMConfigValidationEngine.ValidateAsync(
                candidate, _scope, cancellationToken).ConfigureAwait(false);
            return Commit(candidate, baseline, cancellationToken, report, "历史恢复：" + reason);
        }

        public IList<string> ListSavedVersions()
        { lock (_gate) return _store.CommittedHistory(_savedRev).Select(MHSConfigStorage.Name).ToArray(); }
        /// <summary>只返回当前已提交历史链中的审计记录，未提交的中断文件不进入结果。</summary>
        public IList<MHSConfigAuditEntry> ListAudit()
        { lock (_gate) return _store.ListAudit(_savedRev); }
        /// <summary>严格区分缺失、显式 null、初始化默认与用户保存的值。</summary>
        public FCMConfigValueState<TValue> ExplainState<TValue>(Expression<Func<T, TValue>> accessor)
        {
            FCMConfigValueInfo<TValue> info = Explain(accessor);
            if (!info.HasEffectiveValue) return FCMConfigValueState<TValue>.Missing();
            bool generatedDefault;
            lock (_gate) generatedDefault = _effectiveRev == 1;
            return FCMConfigValueState<TValue>.Of(info.EffectiveValue, info.EffectiveSource, generatedDefault);
        }

        public FCMConfigValueInfo<TValue> Explain<TValue>(Expression<Func<T, TValue>> accessor)
        {
            if (accessor == null) throw new ArgumentNullException("accessor");
            Func<T, TValue> getter = accessor.Compile();
            lock (_gate)
            {
                Tuple<bool, TValue> defaults = TryValue(new T(), getter);
                Tuple<bool, TValue> saved = TryValue(_saved, getter);
                Tuple<bool, TValue> effective = TryValue(_effective, getter);
                int previousRevision = _store.PreviousCommitted(_savedRev);
                Tuple<bool, TValue> previousSaved = previousRevision > 0
                    ? TryValue(FCMJsonValueCodec.Deserialize<T>(_store.ReadVersion(previousRevision)), getter)
                    : Tuple.Create(false, default(TValue));
                Tuple<bool, TValue> previousEffective = _hasPreviousEffective
                    ? TryValue(_previousEffective, getter) : Tuple.Create(false, default(TValue));
                return new FCMConfigValueInfo<TValue>
                {
                    HasDefaultValue = defaults.Item1, DefaultValue = defaults.Item2,
                    HasLatestSavedValue = saved.Item1, LatestSavedValue = saved.Item2,
                    LatestSavedIsExplicitNull = saved.Item1 && object.ReferenceEquals(saved.Item2, null),
                    HasEffectiveValue = effective.Item1, EffectiveValue = effective.Item2,
                    EffectiveIsExplicitNull = effective.Item1 && object.ReferenceEquals(effective.Item2, null),
                    HasPreviousSaved = previousSaved.Item1, PreviousSavedValue = previousSaved.Item2,
                    PreviousSavedIsExplicitNull = previousSaved.Item1 && object.ReferenceEquals(previousSaved.Item2, null),
                    HasPreviousEffective = previousEffective.Item1, PreviousEffectiveValue = previousEffective.Item2,
                    PreviousEffectiveIsExplicitNull = previousEffective.Item1 && object.ReferenceEquals(previousEffective.Item2, null),
                    SavedVersion = MHSConfigStorage.Name(_savedRev),
                    EffectiveVersion = MHSConfigStorage.Name(_effectiveRev),
                    IsPending = _savedRev != _effectiveRev,
                    EffectiveSource = _scope
                };
            }
        }
        private static Tuple<bool, TValue> TryValue<TValue>(T source, Func<T, TValue> accessor)
        {
            try { return Tuple.Create(true, CloneValue(accessor(source))); }
            catch (KeyNotFoundException) { return Tuple.Create(false, default(TValue)); }
            catch (IndexOutOfRangeException) { return Tuple.Create(false, default(TValue)); }
            catch (ArgumentOutOfRangeException) { return Tuple.Create(false, default(TValue)); }
            catch (NullReferenceException) { return Tuple.Create(false, default(TValue)); }
        }
        public IDisposable Subscribe<TValue>(Expression<Func<T, TValue>> accessor,
            FCMConfigChangeStage stage, Action<FCMConfigChange<TValue>> callback)
        { return _publisher.Subscribe(FCMConfigPathUtil.FromExpression(accessor), accessor.Compile(), new T(), stage, callback); }
        public IDisposable SubscribeSection<TPart>(Expression<Func<T, TPart>> accessor,
            FCMConfigChangeStage stage, Action<FCMConfigChange<TPart>> callback)
        { return Subscribe(accessor, stage, callback); }
        public IDisposable SubscribeAll(FCMConfigChangeStage stage, Action<FCMConfigChange<T>> callback)
        { return Subscribe(x => x, stage, callback); }
        internal MHSConfigPreview Preview(T proposed, int baseRevision)
        { return PreviewValidated(proposed, baseRevision, FCMConfigValidationEngine.Validate(proposed, _scope)); }

        internal MHSConfigPreview PreviewValidated(T proposed, int baseRevision, FCMConfigValidationReport validation)
        {
            lock (_gate)
            {
                return new MHSConfigPreview { BaseVersion = MHSConfigStorage.Name(baseRevision), Validation = validation,
                    HasChanges = FCMJsonValueCodec.Serialize(proposed) != FCMJsonValueCodec.Serialize(_saved),
                    CandidateJson = validation.IsValid ? FCMOrderedJsonDocument.Patch(_savedJson, _saved, proposed) : null,
                    ChangedPaths = FCMConfigDiffUtil.Diff(_saved, proposed),
                    ChangeSet = BuildChangeSet(_saved, proposed, baseRevision)
                };
            }
        }
        internal MHSConfigCommitResult Commit(T candidate, int baseRevision, CancellationToken cancellationToken,
            FCMConfigValidationReport validation, string reason)
        {
            T oldSaved = default(T), oldEffective = default(T), savedNow = default(T);
            int newVersion = 0, oldEffectiveVersion = 0;
            bool immediate = false;
            string transactionId = Guid.NewGuid().ToString("N");
            MHSConfigCommitResult result;
            lock (_gate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (baseRevision != _savedRev)
                    return new MHSConfigCommitResult { Success = false, ErrorCode = "CONFIG_REVISION_CONFLICT" };
                if (!validation.IsValid) return new MHSConfigCommitResult { Success = false, ErrorCode = "CONFIG_VALIDATION", Validation = validation };
                string patched = FCMOrderedJsonDocument.Patch(_savedJson, _saved, candidate);
                if (patched == _savedJson)
                    return new MHSConfigCommitResult { Success = true, Revision = SavedVersion, Validation = validation,
                        PendingApply = _savedRev != _effectiveRev };
                // Round-trip verified BEFORE touching the current file.
                T roundtrip = FCMJsonValueCodec.Deserialize<T>(patched);
                if (FCMJsonValueCodec.Serialize(roundtrip) != FCMJsonValueCodec.Serialize(candidate))
                    throw new InvalidOperationException("CONFIG_ROUNDTRIP_MISMATCH");
                oldSaved = Clone(_saved);
                oldEffective = Clone(_effective);
                oldEffectiveVersion = _effectiveRev;
                immediate = oldEffectiveVersion == baseRevision &&
                    GetApplyPolicy(oldSaved, candidate, typeof(T)) == FCMConfigApplyPolicy.Immediate;
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    MHSConfigAuditEntry entry = new MHSConfigAuditEntry
                    {
                        Scope = _scope,
                        Reason = string.IsNullOrWhiteSpace(reason) ? "修改配置" : reason,
                        RecordedAtUtc = DateTimeOffset.UtcNow,
                        ChangedPaths = FCMConfigDiffUtil.Diff(_saved, candidate).ToList()
                    };
                    newVersion = _store.Commit(_savedJson, patched, entry);
                }
                catch (InvalidOperationException ex) when (ex.Message.StartsWith("CONFIG_WRITE_CONFLICT", StringComparison.Ordinal))
                { return new MHSConfigCommitResult { Success = false, ErrorCode = "CONFIG_WRITE_CONFLICT", Validation = validation }; }
                _savedRev = newVersion; _savedJson = patched; _saved = roundtrip;
                savedNow = Clone(_saved);
                result = new MHSConfigCommitResult { Success = true, Revision = MHSConfigStorage.Name(_savedRev),
                    ChangeSet = BuildChangeSet(oldSaved, savedNow, baseRevision),
                    PendingApply = true, Validation = validation };
            }
            _publisher.Publish(oldSaved, savedNow, newVersion, transactionId, FCMConfigChangeStage.Committed,
                FCMConfigDiffUtil.Diff(oldSaved, savedNow));
            if (immediate && SavedVersion == MHSConfigStorage.Name(newVersion) &&
                EffectiveVersion == MHSConfigStorage.Name(baseRevision))
            {
                // Only auto-apply the exact revision associated with this transaction.
                // Reentrant callbacks may have already committed a newer candidate.
                ApplyPendingCore(FCMConfigApplyPolicy.Immediate, null, transactionId);
            }
            result.PendingApply = HasPendingApply;
            return result;
        }
        public bool ApplyPendingAtFrameBoundary()
        { return TryApplyPendingAtBoundary(FCMConfigApplyPolicy.NextFrame, null).Applied; }
        public bool ApplyPendingAtTaskBoundary()
        { return TryApplyPendingAtBoundary(FCMConfigApplyPolicy.NextTask, null).Applied; }
        public bool ApplyPendingAtReconnect()
        { return TryApplyPendingAtBoundary(FCMConfigApplyPolicy.Reconnect, null).Applied; }
        public bool ApplyPendingAtRestart()
        { return TryApplyPendingAtBoundary(FCMConfigApplyPolicy.Restart, null).Applied; }

        /// <summary>
        /// 生效检查与提交分离。审查器在锁外运行，返回拒绝原因；同一版本发生变化则拒绝，
        /// 而不是错误地将刚才审查的版本用于当前已变化的配置。
        /// </summary>
        public MHSConfigApplyResult TryApplyPendingAtBoundary(FCMConfigApplyPolicy boundary,
            Func<MHSConfigApplyReview<T>, MHSConfigApplyDecision> approval)
        { return ApplyPendingCore(boundary, approval, null); }

        private MHSConfigApplyResult ApplyPendingCore(FCMConfigApplyPolicy boundary,
            Func<MHSConfigApplyReview<T>, MHSConfigApplyDecision> approval, string originalTransactionId)
        {
            T proposed, current;
            int proposedRevision, activeRevision;
            FCMConfigApplyPolicy required;
            IList<string> changed;
            Func<MHSConfigApplyReview<T>, MHSConfigApplyDecision> requiredGuard;
            lock (_gate)
            {
                if (_savedRev == _effectiveRev)
                    return new MHSConfigApplyResult { Applied = false, Status = "NOT_PENDING",
                        SavedVersion = SavedVersion, EffectiveVersion = EffectiveVersion };
                required = GetApplyPolicy(_effective, _saved, typeof(T));
                if (required > boundary)
                    return new MHSConfigApplyResult { Applied = false, Status = "BOUNDARY_NOT_REACHED",
                        SavedVersion = SavedVersion, EffectiveVersion = EffectiveVersion,
                        RequiredBoundary = required };
                proposed = Clone(_saved);
                current = Clone(_effective);
                requiredGuard = _applyGuard;
                proposedRevision = _savedRev;
                activeRevision = _effectiveRev;
                changed = FCMConfigDiffUtil.Diff(current, proposed);
            }
            FCMConfigValidationReport validation = FCMConfigValidationEngine.Validate(proposed, _scope);
            if (!validation.IsValid)
                return new MHSConfigApplyResult { Applied = false, Status = "CONFIG_INVALID",
                    Reason = string.Join("; ", validation.Issues
                        .Where(x => x.Severity == FCMConfigIssueSeverity.Error).Select(x => x.Path + ": " + x.Message)),
                    SavedVersion = MHSConfigStorage.Name(proposedRevision),
                    EffectiveVersion = MHSConfigStorage.Name(activeRevision), RequiredBoundary = required };
            foreach (Func<MHSConfigApplyReview<T>, MHSConfigApplyDecision> check in
                new[] { requiredGuard, approval })
            {
                if (check == null) continue;
                MHSConfigApplyDecision decision;
                try
                {
                    decision = check(new MHSConfigApplyReview<T>(
                        Clone(proposed), Clone(current), MHSConfigStorage.Name(proposedRevision),
                        MHSConfigStorage.Name(activeRevision), required, changed));
                    if (decision == null) throw new InvalidOperationException("Apply guard returned null.");
                }
                catch (Exception ex)
                {
                    return new MHSConfigApplyResult { Applied = false, Status = "GUARD_ERROR",
                        Reason = ex.Message, SavedVersion = MHSConfigStorage.Name(proposedRevision),
                        EffectiveVersion = MHSConfigStorage.Name(activeRevision), RequiredBoundary = required };
                }
                if (!decision.Approved)
                    return new MHSConfigApplyResult { Applied = false, Status = decision.Code, Reason = decision.Reason,
                        SavedVersion = MHSConfigStorage.Name(proposedRevision),
                        EffectiveVersion = MHSConfigStorage.Name(activeRevision), RequiredBoundary = required };
            }
            T oldValue, newValue;
            int revision;
            lock (_gate)
            {
                if (proposedRevision != _savedRev || activeRevision != _effectiveRev)
                    return new MHSConfigApplyResult { Applied = false, Status = "APPLY_VERSION_CONFLICT",
                        SavedVersion = SavedVersion, EffectiveVersion = EffectiveVersion, RequiredBoundary = required };
                oldValue = Clone(_effective);
                // SetEffective is durable and must succeed before changing the in-memory snapshot.
                _store.SetEffective(_savedRev);
                _previousEffective = oldValue;
                _hasPreviousEffective = true;
                _effective = Clone(_saved);
                _effectiveRev = _savedRev;
                revision = _effectiveRev;
                newValue = Clone(_effective);
            }
            _publisher.Publish(oldValue, newValue, revision,
                originalTransactionId ?? Guid.NewGuid().ToString("N"),
                FCMConfigChangeStage.Effective, FCMConfigDiffUtil.Diff(oldValue, newValue));
            return new MHSConfigApplyResult { Applied = true, Status = "APPLIED",
                SavedVersion = MHSConfigStorage.Name(revision),
                EffectiveVersion = MHSConfigStorage.Name(revision), RequiredBoundary = required };
        }
        private FCMConfigChangeSet BuildChangeSet(T original, T proposed, int revision)
        {
            IList<string> paths = FCMConfigDiffUtil.Diff(original, proposed);
            List<FCMConfigFieldChange> changes = new List<FCMConfigFieldChange>();
            foreach (string path in paths)
            {
                string oldJson, newJson;
                MVBConfigLayerPath.TryReadJson(original, path, out oldJson);
                MVBConfigLayerPath.TryReadJson(proposed, path, out newJson);
                changes.Add(new FCMConfigFieldChange(path, oldJson, newJson));
            }
            return new FCMConfigChangeSet(Identity, MHSConfigStorage.Name(revision), changes);
        }

        private static FCMConfigApplyPolicy GetApplyPolicy(object before, object after, Type type)
        {
            if (before == null || after == null) return FCMConfigApplyPolicy.NextTask;
            if (FCMConfigSchemaBuilder.IsScalar(type)) return FCMConfigApplyPolicy.Immediate;
            IDictionary d1 = before as IDictionary, d2 = after as IDictionary;
            if (d1 != null && d2 != null)
            {
                FCMConfigApplyPolicy required = FCMConfigApplyPolicy.Immediate;
                foreach (DictionaryEntry v in d2)
                {
                    if (!d1.Contains(v.Key)) return FCMConfigApplyPolicy.NextTask;
                    FCMConfigApplyPolicy item = GetApplyPolicy(d1[v.Key], v.Value,
                        v.Value == null ? typeof(object) : v.Value.GetType());
                    if (item > required) required = item;
                }
                if (d1.Count != d2.Count && required < FCMConfigApplyPolicy.NextTask)
                    return FCMConfigApplyPolicy.NextTask;
                return required;
            }
            FCMConfigApplyPolicy result = FCMConfigApplyPolicy.Immediate;
            foreach (FCMConfigMemberDescriptor member in FCMConfigSchemaBuilder.Members(type))
            {
                object a = member.Property.GetValue(before, null), b = member.Property.GetValue(after, null);
                if (FCMJsonValueCodec.Serialize(a) == FCMJsonValueCodec.Serialize(b)) continue;
                if (member.ApplyPolicy > result) result = member.ApplyPolicy;
                if (a != null && b != null && !FCMConfigSchemaBuilder.IsScalar(member.MemberType) &&
                    !(a is IEnumerable && !(a is IDictionary)))
                {
                    FCMConfigApplyPolicy child = GetApplyPolicy(a, b, member.MemberType);
                    if (child > result) result = child;
                }
            }
            return result;
        }
        private static T Clone(T model) { return FCMJsonValueCodec.Deserialize<T>(FCMJsonValueCodec.Serialize(model)); }
        private static TValue CloneValue<TValue>(TValue value)
        { return FCMJsonValueCodec.Deserialize<TValue>(FCMJsonValueCodec.Serialize(value)); }
    }
}