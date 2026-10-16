using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Mechanism.Data.Configuration;

namespace CxVisual.Tests.ArchitectureSmoke
{
    [FCMConfigSection("Tests.SecureSample")]
    public sealed class ConfigSecureSample
    {
        [FCMConfigItem("普通参数")]
        public int Level { get; set; } = 10;
        [FCMConfigSensitive]
        [FCMConfigItem("保密引用")]
        public string SecretRef { get; set; } = "vault:test-key";
    }

    [FCMConfigSection("Tests.SecretCollection")]
    public sealed class ConfigSensitiveCollectionSample
    {
        public System.Collections.Generic.Dictionary<string, ConfigSensitiveItem> Credentials { get; set; } =
            new System.Collections.Generic.Dictionary<string, ConfigSensitiveItem>
            { { "source", new ConfigSensitiveItem() } };
    }
    public sealed class ConfigSensitiveItem
    {
        [FCMConfigSensitive]
        public string Password { get; set; } = "secret-in-nested-list";
    }

    [FCMConfigSection("Tests.RichTypes")]
    public sealed class ConfigRichTypeSample
    {
        public ulong LargestUnsigned { get; set; } = ulong.MaxValue;
        public decimal ExactDecimal { get; set; } = 123456789012345.123456789m;
        public DateTimeOffset TimeWithOffset { get; set; } =
            new DateTimeOffset(2026, 10, 16, 8, 5, 0, TimeSpan.FromHours(8));
        public int? OptionalThreshold { get; set; }
        public HashSet<string> Tags { get; set; } =
            new HashSet<string>(new[] { "Z", "A", "M" }, StringComparer.Ordinal);
        public Dictionary<string, List<Dictionary<string, double>>> Deep { get; set; } =
            new Dictionary<string, List<Dictionary<string, double>>>();
        public FCMConfigSecretRef Credential { get; set; } = new FCMConfigSecretRef("vault:sample");
    }

    internal sealed class ConfigTestResourceProvider : IFCMConfigResourceReferenceProvider
    {
        public string ResourceType { get { return "calibration"; } }
        public bool Exists(string resourceId, out string reason)
        {
            reason = "标定资源未找到：" + resourceId;
            return resourceId == "cal-01";
        }
    }

    internal static class ConfigArchitectureCompletionTests
    {
        public static void Run()
        {
            string root = Path.Combine(Path.GetTempPath(), "CxVisual-Config-Atomic-" + Guid.NewGuid().ToString("N"));
            try { VerifyAtomicGroup(root); VerifyDurableEvents(root); VerifySecurity(root); VerifyReferences(root); VerifyEffectiveJournal(root); VerifyLiveReplay(root); VerifyRichTypes(root); VerifyAtomicLayers(root); }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
            Console.WriteLine("PASS atomic multi-model configuration transaction, validation and snapshots");
        }

        private static void VerifyAtomicGroup(string root)
        {
            MHSConfigService service = new MHSConfigService(root);
            MHSConfigAtomicGroup group = service.OpenAtomicGroup("Inspection.Setup");
            group.Register<V13ConfigSample>("camera");
            group.Register<V13AsyncConfig>("algorithm");
            group.Seal();
            bool allowed = false;
            group.ConfigureApplyGuard(review => allowed
                ? MHSConfigApplyDecision.Allow()
                : MHSConfigApplyDecision.Deny("HOLD", "尚未授权整组生效"));
            string before = group.SavedVersion;
            using (MHSConfigAtomicEdit edit = group.BeginEdit("成组相机与算法设置"))
            {
                V13ConfigSample camera = edit.Get<V13ConfigSample>("camera");
                camera.ExposureMs = 155;
                V13AsyncConfig algorithm = edit.Get<V13AsyncConfig>("algorithm");
                algorithm.Mode = 1; // Async rule explicitly refuses this member.
                edit.Set("camera", camera);
                edit.Set("algorithm", algorithm);
                MHSConfigCommitResult rejected = edit.CommitAsync(CancellationToken.None)
                    .GetAwaiter().GetResult();
                Check(!rejected.Success && rejected.ErrorCode == "CONFIG_GROUP_VALIDATION" &&
                    group.SavedVersion == before, "one invalid member rolls back entire group");
                algorithm.Mode = 0;
                edit.Set("algorithm", algorithm);
                MHSConfigCommitResult committed = edit.CommitAsync(CancellationToken.None)
                    .GetAwaiter().GetResult();
                Check(committed.Success && committed.PendingApply &&
                    group.SavedVersion != before && group.EffectiveVersion == before,
                    "two distinct models commit under one revision and remain pending");
            }
            using (MHSConfigGroupSnapshot applied = group.CaptureForTask("job-previous"))
            using (MHSConfigGroupSnapshot staged = group.CaptureSaved())
            {
                Check(applied.Get<V13ConfigSample>("camera").ExposureMs == 80 &&
                    applied.Get<V13AsyncConfig>("algorithm").Count == 2 &&
                    staged.Get<V13ConfigSample>("camera").ExposureMs == 155,
                    "effective group remains old while saved group is new");
            }
            MHSConfigApplyResult denial = group.TryApplyPendingAtBoundary(FCMConfigApplyPolicy.NextTask, null);
            Check(!denial.Applied && denial.Status == "HOLD" &&
                group.EffectiveVersion == before, "mandatory guard holds entire group");
            allowed = true;
            MHSConfigApplyResult activated = group.TryApplyPendingAtBoundary(FCMConfigApplyPolicy.NextTask, null);
            Check(activated.Applied && group.EffectiveVersion == group.SavedVersion,
                "entire group activates with one durable marker");
            using (MHSConfigGroupSnapshot fixedTask = group.CaptureForTask("job-001"))
            {
                Check(fixedTask.MemberKeys.SequenceEqual(new[] { "algorithm", "camera" }) &&
                    fixedTask.Get<V13ConfigSample>("camera").ExposureMs == 155 &&
                    fixedTask.Version == group.EffectiveVersion,
                    "strongly typed two-model task snapshot and one version");
                MHSConfigAtomicEdit first = group.BeginEdit("编辑1");
                MHSConfigAtomicEdit stale = group.BeginEdit("并发过期编辑2");
                try
                {
                    V13ConfigSample camera = first.Get<V13ConfigSample>("camera");
                    camera.ExposureMs = 190;
                    first.Set("camera", camera);
                    Check(first.CommitAsync(CancellationToken.None).GetAwaiter().GetResult().Success,
                        "first group writer committed");
                    V13AsyncConfig algorithm = stale.Get<V13AsyncConfig>("algorithm");
                    algorithm.Count = 11;
                    stale.Set("algorithm", algorithm);
                    MHSConfigCommitResult conflict = stale.CommitAsync(CancellationToken.None)
                        .GetAwaiter().GetResult();
                    Check(!conflict.Success && conflict.ErrorCode == "CONFIG_REVISION_CONFLICT",
                        "entire second concurrent edit rejected, no partial overwrite");
                }
                finally { first.Dispose(); stale.Dispose(); }
                Check(group.EffectiveVersion == fixedTask.Version &&
                    fixedTask.Get<V13ConfigSample>("camera").ExposureMs == 155 &&
                    fixedTask.Get<V13AsyncConfig>("algorithm").Count == 2,
                    "pinned task retains both old values after later commit");
            }
            MHSConfigService restarted = new MHSConfigService(root);
            MHSConfigAtomicGroup loaded = restarted.OpenAtomicGroup("Inspection.Setup");
            loaded.Register<V13ConfigSample>("camera");
            loaded.Register<V13AsyncConfig>("algorithm");
            loaded.Seal();
            using (MHSConfigGroupSnapshot active = loaded.CaptureForTask("reopened"))
            using (MHSConfigGroupSnapshot staged = loaded.CaptureSaved())
                Check(active.Get<V13ConfigSample>("camera").ExposureMs == 155 &&
                    staged.Get<V13ConfigSample>("camera").ExposureMs == 190 &&
                    loaded.HasPendingApply, "restart rehydrates saved and effective atomically");
            Check(loaded.GetStatus().AuditCount == 2,
                "two committed multi-model transactions have one audit entry each");
            Expect<InvalidOperationException>(() => loaded.Register<V13ConfigSample>("extra"),
                "sealed registry refuses silent schema change");
        }
        private static void VerifyDurableEvents(string root)
        {
            string folder = Path.Combine(root, "journal");
            MHSConfigService service = new MHSConfigService(folder);
            MHSConfigSection<V13ConfigSample> config =
                service.ForProject("Persistent.Events").Open<V13ConfigSample>();
            Check(config.ReadCommittedAfter(null, 20).Count == 0,
                "initial factory defaults are not invented committed events");
            Check(config.SetValueAsync(x => x.ExposureMs, 101, "第一次修改",
                CancellationToken.None).GetAwaiter().GetResult().Success, "first audited commit");
            Check(config.SetValueAsync(x => x.ExposureMs, 102, "第二次修改",
                CancellationToken.None).GetAwaiter().GetResult().Success, "second audited commit");
            MHSConfigCommittedRecord first = config.ReadCommittedAfter(null, 1).Single();
            MHSConfigCommittedRecord second = config.ReadCommittedAfter(first.Cursor, 1).Single();
            Check(first.Sequence < second.Sequence && first.Reason == "第一次修改" &&
                second.Reason == "第二次修改" && first.TransactionId != null &&
                second.TransactionId != null && first.TransactionId != second.TransactionId &&
                second.ChangedPaths.Contains("ExposureMs") &&
                second.BodySha256 == config.ExportPackage().Sha256,
                "persistent event cursor, reason, transaction id and body integrity");
            Check(config.ReadCommittedAfter(second.Cursor, 10).Count == 0,
                "reading at newest cursor never duplicates old records");
            MHSConfigService restarted = new MHSConfigService(folder);
            MHSConfigSection<V13ConfigSample> reopened =
                restarted.ForProject("Persistent.Events").Open<V13ConfigSample>();
            Check(reopened.ReadCommittedAfter(first.Cursor, 10).Single().Cursor == second.Cursor,
                "durable replay cursor works after process restart");
            Expect<ArgumentOutOfRangeException>(() => reopened.ReadCommittedAfter(null, 0),
                "bounded journal read protects against unlimited allocation");
            string history = Directory.GetFiles(folder, "*.audit.json", SearchOption.AllDirectories)
                .OrderBy(x => x, StringComparer.Ordinal).Last();
            File.Delete(history);
            Expect<InvalidOperationException>(() => reopened.ReadCommittedAfter(null, 20),
                "missing persisted audit evidence must be reported instead of silently skipping an event");
        }

        private static void VerifySecurity(string root)
        {
            MHSConfigService service = new MHSConfigService(Path.Combine(root, "authorized"));
            FCMConfigIdentity id = new FCMConfigIdentity("Tests.SecureSample", "machine", "Machine-A");
            FCMConfigRolePolicy policy = new FCMConfigRolePolicy();
            FCMConfigPrincipal reader = new FCMConfigPrincipal("reader-01", new[] { "viewer" });
            FCMConfigPrincipal operatorUser = new FCMConfigPrincipal("operator-01", new[] { "operator" });
            policy.Grant("viewer", FCMConfigAccessAction.Read, id.SectionId, id.ScopeKind, id.ScopeId);
            FCMConfigIdentity nestedId = new FCMConfigIdentity(
                "Tests.SecretCollection", "machine", "Nested-01");
            policy.Grant("viewer", FCMConfigAccessAction.Read,
                nestedId.SectionId, nestedId.ScopeKind, nestedId.ScopeId);
            Check(FCMConfigSensitivity.ContainsSensitive(typeof(ConfigSensitiveCollectionSample)),
                "nested collection sensitive attributes must be discovered recursively");
            Expect<UnauthorizedAccessException>(() =>
                service.Authorize(reader, policy).Open<ConfigSensitiveCollectionSample>(nestedId),
                "reader is denied complete document with sensitive child inside dictionary");
            policy.Grant("operator", FCMConfigAccessAction.Read, id.SectionId, id.ScopeKind, id.ScopeId);
            policy.Grant("operator", FCMConfigAccessAction.ReadSensitive, id.SectionId, id.ScopeKind, id.ScopeId);
            policy.Grant("operator", FCMConfigAccessAction.Edit, id.SectionId, id.ScopeKind, id.ScopeId);
            policy.Grant("operator", FCMConfigAccessAction.Commit, id.SectionId, id.ScopeKind, id.ScopeId);
            MHSConfigAuthorizedSession readerSession = service.Authorize(reader, policy);
            Expect<UnauthorizedAccessException>(() => readerSession.Open<ConfigSecureSample>(id),
                "read role cannot open entire sensitive model without ReadSensitive");
            MHSConfigAuthorizedSection<ConfigSecureSample> secured =
                service.Authorize(operatorUser, policy).Open<ConfigSecureSample>(id);
            Check(secured.Get().SecretRef == "vault:test-key", "authorized sensitive read");
            using (MHSConfigAuthorizedEdit<ConfigSecureSample> edit = secured.BeginEdit("审批前写入"))
            {
                edit.WorkingCopy.Level = 99;
                policy.Revoke("operator", FCMConfigAccessAction.Commit,
                    id.SectionId, id.ScopeKind, id.ScopeId);
                Expect<UnauthorizedAccessException>(() =>
                    edit.CommitAsync(CancellationToken.None).GetAwaiter().GetResult(),
                    "revoked permission is checked again at commit time");
                policy.Grant("operator", FCMConfigAccessAction.Commit,
                    id.SectionId, id.ScopeKind, id.ScopeId);
                Check(edit.CommitAsync(CancellationToken.None).GetAwaiter().GetResult().Success,
                    "write granted again");
            }
            Check(secured.Value(x => x.Level) == 10 && secured.Revision.IsPending,
                "authorized editing cannot auto-apply Immediate before Apply permission");
            Expect<UnauthorizedAccessException>(() =>
                secured.TryApplyPendingAtBoundary(FCMConfigApplyPolicy.Immediate),
                "explicit apply permission is required for Immediate");
            policy.Grant("operator", FCMConfigAccessAction.Apply,
                id.SectionId, id.ScopeKind, id.ScopeId);
            Check(secured.TryApplyPendingAtBoundary(FCMConfigApplyPolicy.Immediate).Applied &&
                secured.Value(x => x.Level) == 99, "authorized explicit activation");
            Expect<UnauthorizedAccessException>(() => secured.ExportPackage(),
                "export denied without separate permission");
            Expect<UnauthorizedAccessException>(() => secured.ReadCommittedAfter(null, 10),
                "audit/replay denied without separate permission");
            policy.Grant("operator", FCMConfigAccessAction.Audit,
                id.SectionId, id.ScopeKind, id.ScopeId);
            Check(secured.ReadCommittedAfter(null, 10).Count == 1,
                "authorized audit history read");
            policy.Revoke("operator", FCMConfigAccessAction.ReadSensitive,
                id.SectionId, id.ScopeKind, id.ScopeId);
            Expect<UnauthorizedAccessException>(() => secured.Get(),
                "revoked sensitive privilege is checked for every read");
        }

        private static void VerifyReferences(string root)
        {
            MHSConfigService service = new MHSConfigService(Path.Combine(root, "refs"));
            MHSConfigReferenceRegistry references = new MHSConfigReferenceRegistry(service);
            FCMConfigIdentity cameraId = new FCMConfigIdentity("Tests.V13Extension", "project", "Refs");
            FCMConfigIdentity otherId = new FCMConfigIdentity("Tests.ArchitectureImmediate", "project", "Refs");
            references.Register<V13ConfigSample>(cameraId);
            references.Register<ConfigImmediateContract>(otherId);
            FCMConfigReference exposure = new FCMConfigReference(cameraId, "ExposureMs");
            MHSConfigReferenceValue current = references.Resolve(exposure);
            Check(current.HasValue && current.Read<int>() == 80 &&
                current.Presence == FCMConfigValuePresence.Defaulted &&
                current.Version == "v1", "typed reference resolves factory default and source version");
            MHSConfigSection<V13ConfigSample> config = service.Open<V13ConfigSample>(cameraId);
            Check(config.SetValueAsync(x => x.ExposureMs, 135, "更新被引用参数",
                CancellationToken.None).GetAwaiter().GetResult().Success, "referenced value saved");
            Check(references.Resolve(exposure).Read<int>() == 80,
                "reference resolves effective value rather than pending saved value");
            Check(config.ApplyPendingAtTaskBoundary() &&
                references.Resolve(exposure).Read<int>() == 135 &&
                references.Resolve(exposure).Version == config.EffectiveVersion,
                "reference sees newly applied value with correct version");
            MHSConfigReferenceValue invalid = references.Resolve(new FCMConfigReference(
                cameraId, "Detectors[\"not-present\"].Gain"));
            Check(!invalid.HasValue && invalid.Presence == FCMConfigValuePresence.Missing &&
                invalid.Issue.Code == "CONFIG_REFERENCE_PATH_MISSING",
                "missing dynamic reference has actionable diagnostic");
            MHSConfigReferenceValue optional = references.Resolve(new FCMConfigReference(
                cameraId, "Detectors[\"not-present\"].Gain", true));
            Check(!optional.HasValue && optional.Issue == null,
                "optional reference can be missing without validation error");
            MHSConfigReferenceValue unknown = references.Resolve(new FCMConfigReference(
                new FCMConfigIdentity("Tests.V13Extension", "project", "unknown"), "ExposureMs"));
            Check(unknown.Issue.Code == "CONFIG_REFERENCE_SOURCE_MISSING",
                "unregistered source is not silently resolved by another scope");
            references.RegisterDependency(cameraId, otherId);
            Expect<InvalidOperationException>(() => references.RegisterDependency(otherId, cameraId),
                "cross-section dependency cycle detected at registration");
            Check(references.DependencyOrder().Count == 2, "rejected edge does not corrupt graph");
            Check(references.ValidateResource(new FCMConfigResourceRef {
                ResourceType = "calibration", ResourceId = "cal-01" }).Code ==
                "CONFIG_RESOURCE_PROVIDER_MISSING", "resource provider absence explicit");
            references.RegisterResourceProvider(new ConfigTestResourceProvider());
            Check(references.ValidateResource(new FCMConfigResourceRef {
                ResourceType = "calibration", ResourceId = "cal-01" }) == null,
                "resource exists through registered provider");
            Check(references.ValidateResource(new FCMConfigResourceRef {
                ResourceType = "calibration", ResourceId = "cal-unknown" }).Code ==
                "CONFIG_RESOURCE_NOT_FOUND", "missing resource actionable without loading content");
        }

        private static void VerifyEffectiveJournal(string root)
        {
            string path = Path.Combine(root, "effective-journal");
            MHSConfigService service = new MHSConfigService(path);
            MHSConfigSection<V13ConfigSample> config =
                service.ForMachine("Machine-01").Open<V13ConfigSample>();
            Check(config.ReadEffectiveAfter(null, 20).Count == 0,
                "factory initial marker is not a false effective transition");
            Check(config.SetValueAsync(x => x.ExposureMs, 111, "新候选参数",
                CancellationToken.None).GetAwaiter().GetResult().Success,
                "save staged config");
            Check(config.ReadEffectiveAfter(null, 20).Count == 0,
                "saved-but-pending version must not have effective event");
            Check(config.ApplyPendingAtTaskBoundary(), "apply first version");
            MHSConfigEffectiveRecord first = config.ReadEffectiveAfter(null, 10).Single();
            Check(first.Revision == config.EffectiveVersion && first.PreviousVersion == "v1" &&
                !string.IsNullOrEmpty(first.TransactionId) &&
                first.ChangedPaths.Contains("ExposureMs"),
                "effective journal stores transition identity, previous version and changed paths");
            Check(config.SetValueAsync(x => x.ExposureMs, 122, "第二个候选版本",
                CancellationToken.None).GetAwaiter().GetResult().Success &&
                config.ReadEffectiveAfter(first.Revision, 10).Count == 0,
                "later committed revision remains outside effective history until applied");
            Check(config.ApplyPendingAtTaskBoundary(), "apply second version");
            MHSConfigEffectiveRecord second = config.ReadEffectiveAfter(first.Revision, 10).Single();
            Check(second.PreviousVersion == first.Revision && second.Revision == config.EffectiveVersion &&
                second.BodySha256 == config.ExportPackage().Sha256,
                "effective journal follows correct persisted predecessor chain");

            MHSConfigService reopenedService = new MHSConfigService(path);
            MHSConfigSection<V13ConfigSample> reopened =
                reopenedService.ForMachine("Machine-01").Open<V13ConfigSample>();
            Check(reopened.ReadEffectiveAfter(null, 10).Count == 2 &&
                reopened.ReadEffectiveAfter(first.Revision, 10).Single().Revision == second.Revision,
                "effective event cursor survives restart and replay follows committed marker");
            string[] events = Directory.GetFiles(path, "*.effective.json", SearchOption.AllDirectories);
            Check(events.Length == 2, "one effective evidence per activation");
            File.WriteAllText(events.OrderBy(x => x, StringComparer.Ordinal).Last(), "{\"Revision\":\"v9999\"}");
            Expect<InvalidOperationException>(() => reopened.ReadEffectiveAfter(null, 10),
                "tampered effective chain must be detected");
            File.Delete(events.OrderBy(x => x, StringComparer.Ordinal).Last());
            Expect<InvalidOperationException>(() => reopened.ReadEffectiveAfter(null, 10),
                "missing event after journal origin must be detected, not treated as pre-journal history");
            Check(File.Exists(Directory.GetFiles(path, "effective-journal-origin.txt",
                SearchOption.AllDirectories).Single()), "journal origin remains durable");
        }

        private static void VerifyLiveReplay(string root)
        {
            string folder = Path.Combine(root, "live-replay");
            MHSConfigService service = new MHSConfigService(folder);
            MHSConfigSection<V13ConfigSample> config =
                service.ForProject("Monitor").Open<V13ConfigSample>();
            System.Collections.Generic.List<string> received =
                new System.Collections.Generic.List<string>();
            using (MHSConfigCommitFeed<V13ConfigSample> feed =
                config.SubscribeCommittedFrom(null, record =>
                {
                    lock (received) received.Add(record.Cursor);
                    Thread.Sleep(20); // Slow consumer must not block durable Commit.
                }, 1))
            {
                Check(config.SetValueAsync(x => x.ExposureMs, 105, "事件一",
                    CancellationToken.None).GetAwaiter().GetResult().Success &&
                    config.SetValueAsync(x => x.ExposureMs, 106, "事件二",
                    CancellationToken.None).GetAwaiter().GetResult().Success, "two commits during slow consumer");
                Check(SpinWait.SpinUntil(() =>
                { lock (received) return received.Count == 2 && feed.Cursor == "v3"; }, 5000),
                    "feed drains both historical and live events in order");
                lock (received)
                    Check(received[0] == "v2" && received[1] == "v3" &&
                        feed.Cursor == "v3", "exactly ordered and no duplicate cursor");
            }
            int replayCount = 0;
            using (MHSConfigCommitFeed<V13ConfigSample> resumed =
                config.SubscribeCommittedFrom("v2", record =>
                {
                    Interlocked.Increment(ref replayCount);
                    Check(record.Cursor == "v3", "resume uses durable cursor");
                }, 1))
            {
                Check(SpinWait.SpinUntil(() => Volatile.Read(ref replayCount) == 1 && resumed.Cursor == "v3", 5000),
                    "reconnected feed replays only unseen committed revision");
                Check(resumed.Cursor == "v3", "client can persist last successfully delivered cursor");
            }
            int attempts = 0, successes = 0;
            using (MHSConfigCommitFeed<V13ConfigSample> failed =
                config.SubscribeCommittedFrom(null, record =>
                {
                    if (Interlocked.Increment(ref attempts) == 1)
                        throw new InvalidOperationException("模拟消费者异常");
                    Interlocked.Increment(ref successes);
                }, 1))
            {
                Check(SpinWait.SpinUntil(() => failed.LastError != null, 5000) &&
                    failed.Cursor == null, "consumer failure must not acknowledge undelivered event");
                failed.Resume();
                Check(SpinWait.SpinUntil(() => Volatile.Read(ref successes) == 2 && failed.Cursor == "v3", 5000) &&
                    failed.Cursor == "v3", "replay from prior cursor after explicit resume");
            }
        }

        private static void VerifyRichTypes(string root)
        {
            FCMConfigTypeContract type = FCMConfigTypeCatalog.Describe<ConfigRichTypeSample>();
            Check(type.CanRoundtrip && type.Kind == FCMConfigTypeKind.Object,
                "nested numeric, offset, nullable, list/map/set and custom Converter contract");
            Check(FCMConfigTypeCatalog.Describe<ConfigSecureSample>().CanRoundtrip &&
                FCMConfigTypeCatalog.Describe(typeof(ulong)).Kind == FCMConfigTypeKind.Integer,
                "scalar and nested type capability");
            Check(!FCMConfigTypeCatalog.Describe(typeof(Dictionary<int, string>)).CanRoundtrip &&
                FCMConfigTypeCatalog.Describe(typeof(Dictionary<int, string>)).Issues.Any(
                    x => x.Code == "CONFIG_MAP_KEY"), "non-string dictionary keys rejected at design time");
            Check(!FCMConfigTypeCatalog.Describe(typeof(int[,])).CanRoundtrip &&
                !FCMConfigTypeCatalog.Describe(typeof(object)).CanRoundtrip &&
                !FCMConfigTypeCatalog.Describe(typeof(IEnumerable<int>)).CanRoundtrip,
                "array rank, unknown CLR object and abstract collection explicitly unsupported");
            ConfigRichTypeSample original = new ConfigRichTypeSample();
            original.Deep["D-01"] = new List<Dictionary<string, double>>
                { new Dictionary<string, double> { { "Gain", 3.141592653589793 } } };
            string json = FCMJsonValueCodec.Serialize(original);
            ConfigRichTypeSample copy = FCMJsonValueCodec.Deserialize<ConfigRichTypeSample>(json);
            Check(copy.LargestUnsigned == ulong.MaxValue &&
                copy.ExactDecimal == original.ExactDecimal &&
                copy.TimeWithOffset.Offset == TimeSpan.FromHours(8) &&
                copy.TimeWithOffset == original.TimeWithOffset &&
                !copy.OptionalThreshold.HasValue &&
                copy.Deep["D-01"][0]["Gain"] == original.Deep["D-01"][0]["Gain"] &&
                copy.Tags.SetEquals(original.Tags) &&
                copy.Credential.Reference == "vault:sample",
                "rich type value roundtrip preserves ulong, decimal, timezone, deep collection and reference");
            ConfigRichTypeSample reordered = new ConfigRichTypeSample();
            reordered.Tags = new HashSet<string>(new[] { "M", "Z", "A" });
            Check(FCMJsonValueCodec.Serialize(original.Tags) == FCMJsonValueCodec.Serialize(reordered.Tags),
                "HashSet serialization stable independent of insertion order");
            Expect<FormatException>(() => FCMJsonValueCodec.Serialize(double.NaN),
                "NaN must never be serialized to invalid JSON");
            Console.WriteLine("PASS rich scalar, nested collections, exact precision and codec boundaries");
        }

        private static void VerifyAtomicLayers(string root)
        {
            string folder = Path.Combine(root, "atomic-layers");
            MVBConfigLayerSpec[] specs = new[]
            {
                new MVBConfigLayerSpec("模板", "template", "T", 10),
                new MVBConfigLayerSpec("工程", "project", "P", 20),
                new MVBConfigLayerSpec("机台", "machine", "M", 30),
                new MVBConfigLayerSpec("产品", "product", "R", 40),
                new MVBConfigLayerSpec("运行覆盖", "runtime", "O", 50)
            };
            MVBConfigAtomicLayerProfile<V13ConfigSample> profile =
                new MVBConfigAtomicLayerProfile<V13ConfigSample>(
                    new MHSConfigService(folder), "Machine-A", specs);
            string initial = profile.SavedVersion;
            using (MVBConfigAtomicLayerEdit<V13ConfigSample> edit = profile.BeginEdit("五层参数一次提交"))
            {
                edit.Set("模板", x => x.ExposureMs, 100);
                edit.Set("机台", x => x.ExposureMs, 170);
                edit.Set("产品", x => x.Note, "Product-A");
                edit.Set("模板", x => x.Detectors["D1"], new V13DetectorSample { Gain = 2 });
                edit.Set("机台", x => x.Detectors["D1"].Gain, 9);
                MVBConfigResolved<V13ConfigSample> draft = edit.Preview();
                Check(draft.Value(x => x.ExposureMs) == 170 &&
                    draft.Value(x => x.Detectors["D1"].Gain) == 9,
                    "five-layer proposal resolves all changed subtrees in one draft");
                Check(edit.CommitAsync(CancellationToken.None).GetAwaiter().GetResult().Success,
                    "all five layers committed in one revision");
            }
            Check(profile.SavedVersion != initial && profile.EffectiveVersion == initial &&
                profile.ResolveEffective().Value(x => x.ExposureMs) == 80,
                "single revision saved while complete original effective profile remains active");
            Check(profile.ResolveSaved().Value(x => x.ExposureMs) == 170 &&
                profile.ResolveSaved().Value(x => x.Note) == "Product-A",
                "saved profile contains all layer modifications atomically");
            Check(profile.TryApplyPendingAtBoundary(FCMConfigApplyPolicy.NextTask, null).Applied,
                "entire five-layer profile activates at one boundary");
            using (MVBConfigTaskSnapshot<V13ConfigSample> task = profile.CaptureForTask("CT-Scan-01"))
            {
                Check(task.Value(x => x.ExposureMs) == 170 &&
                    task.Value(x => x.Detectors["D1"].Gain) == 9 &&
                    task.Explain(x => x.ExposureMs).EffectiveSource == "机台" &&
                    task.RevisionVector.Entries.Count == 1 &&
                    task.RevisionVector.Entries[0].Effective == profile.EffectiveVersion,
                    "one physical revision and complete layer provenance pinned to task");
                using (MVBConfigAtomicLayerEdit<V13ConfigSample> second =
                    profile.BeginEdit("联合修改模板和机台"))
                {
                    second.Set("模板", x => x.ExposureMs, 130);
                    second.Remove("机台", x => x.ExposureMs);
                    Check(second.CommitAsync(CancellationToken.None).GetAwaiter().GetResult().Success,
                        "template update and machine fallback committed in same transaction");
                }
                Check(profile.ResolveEffective().Value(x => x.ExposureMs) == 170 &&
                    profile.ResolveSaved().Value(x => x.ExposureMs) == 130,
                    "saved and effective group remain isolated");
                Check(profile.TryApplyPendingAtBoundary(FCMConfigApplyPolicy.NextTask, null).Applied,
                    "one atomic switch activates new fallback");
                Check(profile.ResolveEffective().Value(x => x.ExposureMs) == 130 &&
                    profile.ResolveEffective().Explain(x => x.ExposureMs).EffectiveSource == "模板" &&
                    task.Value(x => x.ExposureMs) == 170,
                    "fallback/source and pinned old scan remain consistent");
            }
            string beforeInvalid = profile.SavedVersion;
            using (MVBConfigAtomicLayerEdit<V13ConfigSample> invalid =
                profile.BeginEdit("原子校验拒绝整组"))
            {
                invalid.Set("工程", x => x.Acquisition.Group.CaptureKey, "INVALID");
                invalid.Set("机台", x => x.ExposureMs, 999);
                MHSConfigCommitResult result = invalid.CommitAsync(CancellationToken.None)
                    .GetAwaiter().GetResult();
                Check(!result.Success && result.ErrorCode == "CONFIG_ATOMIC_LAYER_VALIDATION" &&
                    profile.SavedVersion == beforeInvalid &&
                    profile.ResolveEffective().Value(x => x.ExposureMs) == 130,
                    "invalid member rejects all five-layer candidate changes");
            }
            MVBConfigAtomicLayerProfile<V13ConfigSample> reopened =
                new MVBConfigAtomicLayerProfile<V13ConfigSample>(new MHSConfigService(folder),
                    "Machine-A", specs);
            Check(reopened.ResolveEffective().Value(x => x.ExposureMs) == 130 &&
                reopened.ResolveEffective().Value(x => x.Detectors["D1"].Gain) == 9 &&
                reopened.EffectiveVersion == profile.EffectiveVersion,
                "complete profile persists and reopens under one effective version");
            Console.WriteLine("PASS atomic template/project/machine/product/override profile");
        }

        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("CONFIG_COMPLETION: " + message); }
        private static void Expect<T>(Action action, string message) where T : Exception
        { try { action(); } catch (T) { return; } throw new InvalidOperationException("CONFIG_COMPLETION: " + message); }
    }
}
