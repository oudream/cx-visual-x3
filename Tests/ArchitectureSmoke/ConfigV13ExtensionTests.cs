using System;
using System.Collections.Generic;
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
        public Dictionary<string, V13DetectorSample> Detectors { get; set; } =
            new Dictionary<string, V13DetectorSample>();
    }
    public sealed class V13DetectorSample
    {
        public int Gain { get; set; } = 1;
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

        private static void Verify(bool condition, string reason)
        { if (!condition) throw new InvalidOperationException("CONFIG_V13_TEST: " + reason); }
        private static void Expect<T>(Action action, string reason) where T : Exception
        { try { action(); } catch (T) { return; } throw new InvalidOperationException("CONFIG_V13_TEST: " + reason); }
    }
}
