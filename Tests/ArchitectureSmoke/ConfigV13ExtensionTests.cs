using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Mechanism.Data.Configuration;

namespace CxVisual.Tests.ArchitectureSmoke
{
    [FCMConfigSection("Tests.V13Extension")]
    public sealed class V13ConfigSample
    {
        [FCMConfigItem("曝光", ApplyPolicy = FCMConfigApplyPolicy.NextTask)]
        public int ExposureMs { get; set; } = 80;
        public string Note { get; set; } = "factory";
        [FCMConfigItem("采图配置", MemberId = "acquisition", Category = "采集",
            Description = "通用基础配置嵌套元数据")]
        public V13AcquisitionConfig Acquisition { get; set; } = new V13AcquisitionConfig();
        public Dictionary<string, V13DetectorSample> Detectors { get; set; } =
            new Dictionary<string, V13DetectorSample>();
    }
    public sealed class V13DetectorSample
    {
        public int Gain { get; set; } = 1;
    }
    public sealed class V13AcquisitionConfig
    {
        [FCMConfigItem("曝光组", MemberId = "exposure-group", Order = 10)]
        public V13ExposureGroup Group { get; set; } = new V13ExposureGroup();
    }
    public sealed class V13ExposureGroup
    {
        [FCMConfigItem("采集超时", MemberId = "timeout-ms", Order = 20, Unit = "ms")]
        [Range(1, 2000)]
        public int TimeoutMs { get; set; } = 300;

        [FCMConfigItem("采集标识", MemberId = "capture-key", Order = 30)]
        [Required]
        [RegularExpression(@"^CAP-[0-9]{3}$")]
        public string CaptureKey { get; set; } = "CAP-001";
    }

    [FCMConfigSection("Tests.V13AsyncValidation")]
    [FCMConfigValidator(typeof(V13AsyncConfigRules))]
    public sealed class V13AsyncConfig
    {
        public int Mode { get; set; }
        public int Count { get; set; } = 2;
    }
    public sealed class V13AsyncConfigRules : IFCMAsyncConfigValidator<V13AsyncConfig>
    {
        public Task<IEnumerable<FCMConfigIssue>> ValidateAsync(V13AsyncConfig value,
            FCMConfigValidationContext context, CancellationToken cancellationToken)
        {
            if (value.Mode == 2) throw new InvalidOperationException("模拟专业校验器故障");
            if (value.Mode == 3) value.Count = 999;
            IEnumerable<FCMConfigIssue> issues = value.Mode == 1
                ? new[] { FCMConfigIssue.Error("Mode", "ASYNC_DOMAIN_REJECTED", "模拟设备检查不通过") }
                : new FCMConfigIssue[0];
            return Task.FromResult(issues);
        }
    }

    [FCMConfigSection("Tests.ResourceReferences")]
    public sealed class V13ResourceReferenceConfig
    {
        [FCMConfigItem("设备密钥引用")]
        public FCMConfigSecretRef Credential { get; set; } =
            new FCMConfigSecretRef("vault:detector-access");
        [FCMConfigItem("体积数据句柄")]
        public FCMConfigDataHandle Volume { get; set; } =
            new FCMConfigDataHandle { ProviderId = "shm", ResourceId = "ct-volume-01", Version = "v4" };
        [FCMConfigItem("标定资源")]
        public FCMConfigResourceRef Calibration { get; set; } =
            new FCMConfigResourceRef { ResourceType = "calibration", ResourceId = "cal-01" };
    }

    internal static class ConfigV13ExtensionTests
    {
        public static void Run()
        {
            string root = Path.Combine(Path.GetTempPath(), "CxConfigV13-" + Guid.NewGuid().ToString("N"));
            try
            {
                MHSConfigService service = new MHSConfigService(root);
                int registered = service.RegisterAssembly(typeof(V13ConfigSample).Assembly);
                Verify(registered >= 1, "initial assembly registration");
                Verify(service.RegisterAssembly(typeof(V13ConfigSample).Assembly) == 0, "idempotent discovery");
                Verify(service.GetRegisteredSections().Any(s => s.SectionId == "Tests.V13Extension"),
                    "registry list for Studio");
                Verify(service.FindRegisteredSection("Tests.V13Extension").ModelType == typeof(V13ConfigSample),
                    "registry lookup");
                Verify(service.UnregisterAssembly(typeof(V13ConfigSample).Assembly) == registered,
                    "unregister idle assembly");
                Verify(service.FindRegisteredSection("Tests.V13Extension") == null, "lookup after unload");
                service.RegisterAssembly(typeof(V13ConfigSample).Assembly);
                MHSConfigSection<V13ConfigSample> cfg = service.ForMachine("Machine-A").Open<V13ConfigSample>();
                MHSConfigSection<V13ConfigSample> other = service.ForMachine("Machine-B").Open<V13ConfigSample>();
                Verify(other.Value(x => x.ExposureMs) == 80, "other machine default");
                Expect<InvalidOperationException>(() =>
                    service.UnregisterAssembly(typeof(V13ConfigSample).Assembly), "active type unload forbidden");

                FCMConfigValueInfo<int> missing = cfg.Explain(x => x.Detectors["Detector-01"].Gain);
                Verify(!missing.HasDefaultValue && !missing.HasLatestSavedValue &&
                    !missing.HasEffectiveValue && !missing.HasPreviousSaved,
                    "missing must not be confused with integer zero");
                string first = cfg.SavedVersion;
                using (MHSConfigEdit<V13ConfigSample> edit = cfg.BeginEdit("首次配置"))
                {
                    edit.WorkingCopy.ExposureMs = 100;
                    edit.WorkingCopy.Note = null;
                    edit.WorkingCopy.Detectors["Detector-01"] = new V13DetectorSample { Gain = 5 };
                    Verify(edit.CommitAsync(CancellationToken.None).GetAwaiter().GetResult().Success,
                        "first commit");
                }
                FCMConfigValueInfo<string> explicitNull = cfg.Explain(x => x.Note);
                Verify(explicitNull.HasLatestSavedValue && explicitNull.LatestSavedIsExplicitNull &&
                    !explicitNull.EffectiveIsExplicitNull && explicitNull.EffectiveValue == "factory",
                    "explicit null vs unchanged effective");
                FCMConfigValueInfo<int> newDynamicField =
                    cfg.Explain(x => x.Detectors["Detector-01"].Gain);
                Verify(newDynamicField.HasLatestSavedValue && newDynamicField.LatestSavedValue == 5 &&
                    !newDynamicField.HasPreviousSaved, "no invented previous value for dynamic key");
                Verify(cfg.ApplyPendingAtTaskBoundary(), "first NextTask apply");
                string history100 = cfg.SavedVersion;
                using (MHSConfigSnapshot<V13ConfigSample> view = cfg.AtSavedVersionView(history100))
                {
                    Verify(view.Value(x => x.ExposureMs) == 100 &&
                        view.Section(x => x.Detectors["Detector-01"]).Gain == 5,
                        "historical scalar and section");
                    V13ConfigSample mutatedCopy = view.Get();
                    mutatedCopy.Detectors["Detector-01"].Gain = 999;
                    Verify(view.Value(x => x.Detectors["Detector-01"].Gain) == 5,
                        "historical view must isolate callers");
                }
                using (MHSConfigEdit<V13ConfigSample> edit = cfg.BeginEdit("修改运行参数"))
                {
                    edit.WorkingCopy.ExposureMs = 200;
                    edit.WorkingCopy.Note = "second";
                    Verify(edit.CommitAsync(CancellationToken.None).GetAwaiter().GetResult().Success,
                        "second commit");
                }
                FCMConfigValueInfo<string> note = cfg.Explain(x => x.Note);
                Verify(note.HasPreviousSaved && note.PreviousSavedIsExplicitNull &&
                    note.PreviousSavedValue == null && note.LatestSavedValue == "second",
                    "previous saved explicit null must survive");
                FCMConfigValueInfo<int> exposure = cfg.Explain(x => x.ExposureMs);
                Verify(exposure.HasPreviousSaved && exposure.PreviousSavedValue == 100 &&
                    exposure.LatestSavedValue == 200 && exposure.EffectiveValue == 100,
                    "saved/effective/previous are distinct");

                string second = cfg.SavedVersion;
                MHSConfigCommitResult reverted = cfg.RestoreSavedVersionAsync(history100, "恢复已验证参数",
                    CancellationToken.None).GetAwaiter().GetResult();
                Verify(reverted.Success && cfg.SavedVersion != second &&
                    cfg.GetLatestSaved().ExposureMs == 100, "restore must create new revision");
                Verify(cfg.AtSavedVersion(second).ExposureMs == 200 &&
                    cfg.AtSavedVersion(first).ExposureMs == 80, "restore must preserve history");
                Verify(cfg.ListSavedVersions().Count >= 4, "all saved versions retained");
                Verify(other.Value(x => x.ExposureMs) == 80, "machine scope independence");
                Expect<ArgumentException>(() => cfg.AtSavedVersionView("v9999"), "unknown history forbidden");
                AsyncValidationRegressions(root);
                LayeredProfileRegressions(root);
                ConfigurationFoundationRegressions(root);
                ReferenceContractsRegressions(root);
                AuditRegressions(root);
                Console.WriteLine("PASS V1.3 config registry, value state, history view, restore and async validation");
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        private static void AsyncValidationRegressions(string root)
        {
            V13AsyncConfig allowed = new V13AsyncConfig { Mode = 0 };
            Verify(FCMConfigValidationEngine.ValidateAsync(allowed, "test", CancellationToken.None)
                .GetAwaiter().GetResult().IsValid, "async valid model");
            V13AsyncConfig rejected = new V13AsyncConfig { Mode = 1 };
            FCMConfigValidationReport refusal = FCMConfigValidationEngine.ValidateAsync(
                rejected, "test", CancellationToken.None).GetAwaiter().GetResult();
            Verify(!refusal.IsValid && refusal.Issues.Any(x => x.Code == "ASYNC_DOMAIN_REJECTED"),
                "async domain validation issue");
            V13AsyncConfig broken = new V13AsyncConfig { Mode = 2 };
            FCMConfigValidationReport exception = FCMConfigValidationEngine.ValidateAsync(
                broken, "test", CancellationToken.None).GetAwaiter().GetResult();
            Verify(!exception.IsValid && exception.Issues.Any(x => x.Code == "ASYNC_VALIDATOR_EXCEPTION"),
                "async validator exception becomes report");
            V13AsyncConfig changed = new V13AsyncConfig { Mode = 3 };
            FCMConfigValidationReport mutation = FCMConfigValidationEngine.ValidateAsync(
                changed, "test", CancellationToken.None).GetAwaiter().GetResult();
            Verify(!mutation.IsValid && mutation.Issues.Any(x => x.Code == "VALIDATOR_MUTATED_CONFIG"),
                "validator side effects rejected");
            CancellationTokenSource cancel = new CancellationTokenSource();
            cancel.Cancel();
            Expect<OperationCanceledException>(() => FCMConfigValidationEngine.ValidateAsync(
                allowed, "test", cancel.Token).GetAwaiter().GetResult(), "cancel must propagate");
            MHSConfigService service = new MHSConfigService(Path.Combine(root, "async"));
            MHSConfigSection<V13AsyncConfig> config = service.ForProject("project-a").Open<V13AsyncConfig>();
            string version = config.SavedVersion;
            using (MHSConfigEdit<V13AsyncConfig> edit = config.BeginEdit("专业异步校验异常"))
            {
                edit.WorkingCopy.Mode = 2;
                MHSConfigCommitResult invalid = edit.CommitAsync(CancellationToken.None).GetAwaiter().GetResult();
                Verify(!invalid.Success && invalid.ErrorCode == "CONFIG_VALIDATION" &&
                    config.SavedVersion == version, "invalid async validation never commits");
            }
        }

        private static void LayeredProfileRegressions(string root)
        {
            string folder = Path.Combine(root, "layers");
            MHSConfigService service = new MHSConfigService(folder);
            MVBConfigLayerSpec[] specs = new[]
            {
                new MVBConfigLayerSpec("模板", "template", "T-01", 10),
                new MVBConfigLayerSpec("工程", "project", "P-01", 20),
                new MVBConfigLayerSpec("机台", "machine", "M-01", 30),
                new MVBConfigLayerSpec("产品", "product", "R-01", 40),
                new MVBConfigLayerSpec("运行覆盖", "runtime", "Runtime-01", 50)
            };
            MVBConfigLayeredProfile<V13ConfigSample> profile =
                new MVBConfigLayeredProfile<V13ConfigSample>(service, specs);
            Verify(profile.ResolveEffective().Value(x => x.ExposureMs) == 80,
                "no overrides means POCO default");
            Verify(profile.SetOverrideAsync("模板", x => x.ExposureMs, 100, CancellationToken.None)
                .GetAwaiter().GetResult().Success, "template exposure override");
            Verify(profile.SetOverrideAsync("模板", x => x.Detectors["D1"],
                new V13DetectorSample { Gain = 2 }, CancellationToken.None)
                .GetAwaiter().GetResult().Success, "template adds detector");
            Verify(profile.SetOverrideAsync("工程", x => x.Note, "project", CancellationToken.None)
                .GetAwaiter().GetResult().Success, "project note");
            Verify(profile.SetOverrideAsync("机台", x => x.ExposureMs, 180, CancellationToken.None)
                .GetAwaiter().GetResult().Success, "machine override exposure");
            Verify(profile.SetOverrideAsync("机台", x => x.Detectors["D1"].Gain, 7,
                CancellationToken.None).GetAwaiter().GetResult().Success, "machine overrides nested dictionary member");
            Verify(profile.SetOverrideAsync("产品", x => x.Note, "product", CancellationToken.None)
                .GetAwaiter().GetResult().Success, "product overrides project note");
            Verify(profile.SetOverrideAsync("运行覆盖", x => x.Note, "runtime", CancellationToken.None)
                .GetAwaiter().GetResult().Success, "runtime overrides product note");

            // Saved versions are not effective before the declared task boundary.
            Verify(profile.ResolveEffective().Value(x => x.ExposureMs) == 80,
                "saved layers cannot leak into current effective values");
            foreach (MVBConfigLayerSpec spec in specs)
                profile.GetLayer(spec.Name).ApplyPendingAtTaskBoundary();
            MVBConfigResolved<V13ConfigSample> resolved = profile.ResolveEffective();
            Verify(resolved.Value(x => x.ExposureMs) == 180 &&
                resolved.Value(x => x.Detectors["D1"].Gain) == 7 &&
                resolved.Value(x => x.Note) == "runtime", "five-layer resolution");
            Verify(resolved.Versions.Count == 5 && resolved.Versions["机台"].StartsWith("v"),
                "per-layer committed effective version vector");
            MVBConfigLayerValueInfo<int> source = resolved.Explain(x => x.ExposureMs);
            Verify(source.EffectiveSource == "机台" && source.SourceChain.Count == 2 &&
                source.SourceChain[0].Name == "模板", "field-level provenance chain");
            Verify(resolved.Explain(x => x.Detectors["D1"].Gain).EffectiveSource == "机台",
                "deep dictionary path provenance");
            Verify(resolved.Explain(x => x.Detectors["D2"].Gain).HasValue == false,
                "unavailable dynamic item has no value");
            V13ConfigSample loose = resolved.Get();
            loose.Detectors["D1"].Gain = 1000;
            Verify(resolved.Value(x => x.Detectors["D1"].Gain) == 7,
                "resolved snapshot isolated from caller mutation");

            using (MVBConfigTaskSnapshot<V13ConfigSample> pinned = profile.CaptureForTask("scan-001"))
            {
                Verify(pinned.Value(x => x.ExposureMs) == 180 &&
                    pinned.Versions.Count == 5, "group task captures one version vector");
                Verify(profile.SetOverrideAsync("机台", x => x.ExposureMs, 220, CancellationToken.None)
                    .GetAwaiter().GetResult().Success, "stage next machine value");
                Verify(profile.ResolveEffective().Value(x => x.ExposureMs) == 180,
                    "pending overrides must not affect resolved effective value");
                Verify(profile.GetLayer("机台").ApplyPendingAtTaskBoundary(),
                    "task boundary activates machine layer");
                Verify(profile.ResolveEffective().Value(x => x.ExposureMs) == 220,
                    "new effective version selected");
                Verify(pinned.Value(x => x.ExposureMs) == 180,
                    "existing task retains captured version after activation");
            }

            Verify(profile.RemoveOverrideAsync("机台", x => x.ExposureMs, CancellationToken.None)
                .GetAwaiter().GetResult().Success, "remove explicit override");
            Verify(profile.ResolveEffective().Value(x => x.ExposureMs) == 220, "remove pending until task");
            Verify(profile.GetLayer("机台").ApplyPendingAtTaskBoundary(), "remove takes effect");
            Verify(profile.ResolveEffective().Value(x => x.ExposureMs) == 100 &&
                profile.ResolveEffective().Explain(x => x.ExposureMs).EffectiveSource == "模板",
                "removal falls back to template instead of schema default");

            MVBConfigLayeredProfile<V13ConfigSample> reopened =
                new MVBConfigLayeredProfile<V13ConfigSample>(new MHSConfigService(folder), specs);
            Verify(reopened.ResolveEffective().Value(x => x.ExposureMs) == 100 &&
                reopened.ResolveEffective().Value(x => x.Detectors["D1"].Gain) == 7,
                "persisted layers and effective markers survive process reopen");
            int effectiveNotifications = 0;
            using (IDisposable subscription = profile.Subscribe(x => x.ExposureMs, change =>
            {
                effectiveNotifications++;
                if (effectiveNotifications == 1)
                    Verify(change.OldValue == 100 && change.NewValue == 140 &&
                        change.HasDefaultValue && change.DefaultValue == 80 &&
                        change.OldSource == "模板" && change.NewSource == "机台" &&
                        change.NewVersions.ContainsKey("机台"), "old/new/default/source on effective change");
                else if (effectiveNotifications == 2)
                    Verify(change.OldValue == 140 && change.NewValue == 120 &&
                        change.NewSource == "模板", "source fallback when high-priority override removed");
            }))
            {
                Verify(profile.SetOverrideAsync("机台", x => x.ExposureMs, 140,
                    CancellationToken.None).GetAwaiter().GetResult().Success,
                    "stage machine change for notification");
                Verify(effectiveNotifications == 0, "saved stage must not raise effective event");
                Verify(profile.GetLayer("机台").ApplyPendingAtTaskBoundary(), "apply machine update");
                Verify(effectiveNotifications == 1, "one effective event expected");

                Verify(profile.SetOverrideAsync("模板", x => x.ExposureMs, 120,
                    CancellationToken.None).GetAwaiter().GetResult().Success,
                    "stage lower-priority change");
                Verify(profile.GetLayer("模板").ApplyPendingAtTaskBoundary(), "apply lower-priority change");
                Verify(effectiveNotifications == 1,
                    "shadowed low-priority change must not raise duplicate effective event");
                Verify(profile.RemoveOverrideAsync("机台", x => x.ExposureMs,
                    CancellationToken.None).GetAwaiter().GetResult().Success,
                    "remove high-priority override");
                Verify(profile.GetLayer("机台").ApplyPendingAtTaskBoundary(), "apply removal");
                Verify(effectiveNotifications == 2, "one event on inherited fallback");
            }
            Verify(profile.SetOverrideAsync("机台", x => x.ExposureMs, 160,
                CancellationToken.None).GetAwaiter().GetResult().Success, "write after unsubscribe");
            profile.GetLayer("机台").ApplyPendingAtTaskBoundary();
            Verify(effectiveNotifications == 2, "disposed subscription must not be called");
            Expect<ArgumentException>(() => new MVBConfigLayeredProfile<V13ConfigSample>(
                service, specs[0], specs[0]), "duplicate layer must be rejected");
            Console.WriteLine("PASS V1.3 explicit sparse five-layer scope, source vector and fallback");
        }

        private static void ConfigurationFoundationRegressions(string root)
        {
            IList<FCMConfigFieldInfo> fields = FCMConfigCatalog.Describe<V13ConfigSample>();
            FCMConfigFieldInfo timeout = fields.Single(x => x.Path == "Acquisition.Group.TimeoutMs");
            Verify(timeout.MemberId == "acquisition.exposure-group.timeout-ms" &&
                timeout.DefaultJson == "300" && timeout.Range.Contains("2000") &&
                timeout.Unit == "ms", "nested metadata, stable IDs, defaults and range");
            Verify(FCMConfigCatalog.Search<V13ConfigSample>("超时").Any(x =>
                x.Path == "Acquisition.Group.TimeoutMs"), "catalog search");
            Verify(FCMConfigCatalog.ExportMarkdown<V13ConfigSample>().Contains("Acquisition.Group.CaptureKey"),
                "automatic configuration documentation");
            Verify(fields.Single(x => x.Path == "Acquisition.Group.CaptureKey").Required &&
                fields.Single(x => x.Path == "Acquisition.Group.CaptureKey").Regex != null,
                "required and regex metadata");

            MHSConfigService svc = new MHSConfigService(Path.Combine(root, "foundation-complete"));
            MHSConfigSection<V13ConfigSample> section = svc.ForMachine("SRC").Open<V13ConfigSample>();
            string initialVersion = section.SavedVersion;
            MHSConfigCommitResult set = section.SetValueAsync(x => x.ExposureMs, 130,
                "单字段调整", CancellationToken.None).GetAwaiter().GetResult();
            Verify(set.Success && section.GetLatestSaved().ExposureMs == 130 &&
                section.Value(x => x.ExposureMs) == 80, "single field uses transaction and pending apply");
            MHSConfigApplyResult notReady = section.TryApplyPendingAtBoundary(
                FCMConfigApplyPolicy.NextFrame, null);
            Verify(!notReady.Applied && notReady.Status == "BOUNDARY_NOT_REACHED",
                "early boundary denied");

            MHSConfigApplyResult denied = section.TryApplyPendingAtBoundary(
                FCMConfigApplyPolicy.NextTask,
                review => MHSConfigApplyDecision.Deny("INTERLOCK", "测试模拟禁止生效"));
            Verify(!denied.Applied && denied.Status == "INTERLOCK" &&
                section.EffectiveVersion == initialVersion && section.HasPendingApply,
                "guard refusal keeps active version unchanged");
            MHSConfigApplyResult guardError = section.TryApplyPendingAtBoundary(
                FCMConfigApplyPolicy.NextTask, review => { throw new InvalidOperationException("模拟审查异常"); });
            Verify(!guardError.Applied && guardError.Status == "GUARD_ERROR" && section.HasPendingApply,
                "guard exception is isolated and pending remains");
            MHSConfigApplyResult applied = section.TryApplyPendingAtBoundary(
                FCMConfigApplyPolicy.NextTask, review =>
                {
                    Verify(review.Candidate.ExposureMs == 130 && review.Current.ExposureMs == 80 &&
                        review.RequiredBoundary == FCMConfigApplyPolicy.NextTask &&
                        review.ChangedPaths.Contains("ExposureMs"), "guard receives diff and isolated candidate");
                    review.Candidate.ExposureMs = 999;
                    return MHSConfigApplyDecision.Allow();
                });
            Verify(applied.Applied && applied.Status == "APPLIED" &&
                section.Value(x => x.ExposureMs) == 130, "approved apply ignores guard mutation");

            using (MHSConfigEdit<V13ConfigSample> edit = section.BeginEdit("异步预览"))
            {
                edit.WorkingCopy.Acquisition.Group.CaptureKey = "INVALID";
                MHSConfigPreview invalid = edit.PreviewAsync(CancellationToken.None).GetAwaiter().GetResult();
                Verify(!invalid.Validation.IsValid && invalid.CandidateJson == null,
                    "async preview uses complete validator pipeline");
                edit.WorkingCopy.Acquisition.Group.CaptureKey = "CAP-002";
                MHSConfigPreview valid = edit.PreviewAsync(CancellationToken.None).GetAwaiter().GetResult();
                Verify(valid.Validation.IsValid && valid.HasChanges &&
                    valid.ChangedPaths.Contains("Acquisition.Group.CaptureKey"), "async preview diff");
                Verify(edit.CommitAsync(CancellationToken.None).GetAwaiter().GetResult().Success,
                    "previewed config persisted");
            }
            MHSConfigPackage bundle = section.ExportPackage();
            Verify(bundle.Verify() && bundle.SectionId == "Tests.V13Extension" &&
                bundle.SourceRevision == section.SavedVersion, "portable package identity/hash");
            MHSConfigSection<V13ConfigSample> target = svc.ForMachine("DST").Open<V13ConfigSample>();
            string oldTarget = target.SavedVersion;
            MHSConfigPackage broken = new MHSConfigPackage
            {
                SectionId = bundle.SectionId, SchemaVersion = bundle.SchemaVersion,
                Json = bundle.Json + " ", Sha256 = bundle.Sha256
            };
            Verify(!target.ImportPackageAsync(broken, CancellationToken.None).GetAwaiter().GetResult().Success
                && target.SavedVersion == oldTarget, "tampered package rejected without writes");
            broken.Json = bundle.Json;
            broken.Sha256 = bundle.Sha256;
            broken.SectionId = "Other.Schema";
            Verify(target.ImportPackageAsync(broken, CancellationToken.None).GetAwaiter().GetResult().ErrorCode ==
                "CONFIG_PACKAGE_SCHEMA_MISMATCH", "schema mismatch rejected");
            MHSConfigCommitResult imported = target.ImportPackageAsync(bundle, CancellationToken.None)
                .GetAwaiter().GetResult();
            Verify(imported.Success && target.GetLatestSaved().ExposureMs == 130 &&
                target.GetLatestSaved().Acquisition.Group.CaptureKey == "CAP-002",
                "valid package imports through existing validation/commit");
            Verify(target.Value(x => x.ExposureMs) == 80 && target.HasPendingApply,
                "import never pretends device settings are effective");
            Verify(target.ApplyPendingAtTaskBoundary() && target.Value(x => x.ExposureMs) == 130,
                "import activates only at correct boundary");
            Console.WriteLine("PASS configuration catalog, apply guards, async preview and package");
        }

        private static void ReferenceContractsRegressions(string root)
        {
            V13ResourceReferenceConfig config = new V13ResourceReferenceConfig();
            string json = FCMJsonValueCodec.Serialize(config);
            Verify(json.Contains("vault:detector-access") && json.Contains("ct-volume-01") &&
                !json.Contains("private-key-material") && !json.Contains("ACTUAL_SECRET"),
                "only references, never secrets or large binary");
            V13ResourceReferenceConfig copy = FCMJsonValueCodec.Deserialize<V13ResourceReferenceConfig>(json);
            Verify(copy.Credential.Reference == "vault:detector-access" &&
                copy.Credential.ToString() == "[SecretRef]" &&
                copy.Volume.ProviderId == "shm" && copy.Volume.Version == "v4" &&
                copy.Calibration.ResourceId == "cal-01",
                "typed references round trip without loading resource content");
            Expect<ArgumentException>(() => new FCMConfigSecretRef("not allowed spaces"),
                "malformed secret identity rejected");
            MHSConfigService service = new MHSConfigService(Path.Combine(root, "reference"));
            MHSConfigSection<V13ResourceReferenceConfig> section =
                service.ForProject("CT-Project").Open<V13ResourceReferenceConfig>();
            Verify(section.Get().Credential.Reference == "vault:detector-access",
                "config supports converter-backed secret identity");
            Verify(FCMConfigCatalog.Search<V13ResourceReferenceConfig>("数据句柄").Count == 1,
                "referenced data is visible in schema without loading large object");
            Console.WriteLine("PASS typed secret/resource/data references");
        }

        private static void AuditRegressions(string root)
        {
            MHSConfigService service = new MHSConfigService(Path.Combine(root, "audit"));
            MHSConfigSection<V13ConfigSample> section = service.ForMachine("AUDIT-01").Open<V13ConfigSample>();
            string initial = section.SavedVersion;
            Verify(section.SetValueAsync(x => x.ExposureMs, 140, "审查曝光调整",
                CancellationToken.None).GetAwaiter().GetResult().Success, "audited edit");
            IList<MHSConfigAuditEntry> entries = section.ListAudit();
            Verify(entries.Count == 1 && entries[0].Revision == section.SavedVersion &&
                entries[0].Reason == "审查曝光调整" &&
                entries[0].ChangedPaths.Contains("ExposureMs") &&
                entries[0].JsonSha256 == section.ExportPackage().Sha256,
                "audited reason, changed paths, revision and body hash");
            Verify(section.RestoreSavedVersionAsync(initial, "恢复工厂默认",
                CancellationToken.None).GetAwaiter().GetResult().Success, "restore is audited");
            entries = section.ListAudit();
            Verify(entries.Count == 2 &&
                entries[1].Reason.Contains("恢复工厂默认") &&
                entries[1].Revision == section.SavedVersion &&
                section.AtSavedVersion(entries[0].Revision).ExposureMs == 140,
                "restore forms a new revision without modifying earlier history");

            // Tampered audit evidence is reported, not silently accepted.
            string[] auditFiles = Directory.GetFiles(Path.Combine(root, "audit"),
                "*.audit.json", SearchOption.AllDirectories);
            Verify(auditFiles.Length == 2, "two immutable audit files");
            File.WriteAllText(auditFiles[0], "{\"Revision\":\"v1\",\"JsonSha256\":\"tampered\"}");
            Expect<InvalidOperationException>(() => section.ListAudit(), "tampered audit must be rejected");
            Console.WriteLine("PASS configuration audit chain and integrity verification");
        }

        private static void Verify(bool condition, string reason)
        { if (!condition) throw new InvalidOperationException("CONFIG_V13_TEST: " + reason); }
        private static void Expect<T>(Action action, string reason) where T : Exception
        { try { action(); } catch (T) { return; } throw new InvalidOperationException("CONFIG_V13_TEST: " + reason); }
    }
}
