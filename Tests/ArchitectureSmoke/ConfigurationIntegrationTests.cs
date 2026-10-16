using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Mechanism.Data.Configuration;

namespace CxVisual.Tests.ArchitectureSmoke
{
    [FCMConfigSection("Automation.DemoDetector", SchemaVersion = 2)]
    [FCMConfigValidator(typeof(PDLTestMachineValidator))]
    public sealed class PDLTestMachineConfig
    {
        [FCMConfigItem("编号", MemberId = "name", Order = 100)]
        [Required]
        [RegularExpression("^MCH-[A-Z0-9]+$")]
        public string Name { get; set; } = "MCH-001";

        [FCMConfigItem("启用", MemberId = "enabled", Order = 200)]
        public bool Enabled { get; set; } = true;
        [FCMConfigItem("无损大整数", MemberId = "frames", Order = 300)]
        public ulong FrameCount { get; set; } = 18446744073709551615UL;
        [FCMConfigItem("十进制", MemberId = "factor", Order = 400)]
        public decimal Factor { get; set; } = 123.4567890123456789m;
        [FCMConfigItem("字符", MemberId = "char", Order = 500)]
        public char Mark { get; set; } = '测';
        [FCMConfigItem("可空时间", MemberId = "calibration", Order = 600)]
        public DateTimeOffset? CalibrationAt { get; set; }
        [FCMConfigItem("地址", MemberId = "uri", Order = 700)]
        public Uri Endpoint { get; set; } = new Uri("https://example.org/api");
        [FCMConfigItem("探测器", MemberId = "detectors", Order = 800)]
        public Dictionary<string, PDLTestDetectorConfig> Detectors { get; set; } = new Dictionary<string, PDLTestDetectorConfig>();
        [FCMConfigItem("禁用缺陷", MemberId = "defect", Order = 900)]
        public HashSet<string> DisabledCodes { get; set; } = new HashSet<string>();
        [FCMConfigItem("系数", MemberId = "weights", Order = 1000)]
        public double[] Weights { get; set; } = new double[] { 0.1, 0.2, 0.7 };
    }
    public sealed class PDLTestDetectorConfig
    {
        [FCMConfigItem("增益", MemberId = "gain", Order = 100)]
        public double Gain { get; set; } = 1.0;
        [FCMConfigItem("曝光", MemberId = "exposure", Order = 150, AfterId = "gain", BeforeId = "trigger",
            SinceSchemaVersion = 2, ApplyPolicy = FCMConfigApplyPolicy.NextTask)]
        [Range(1, 2000)]
        [FCMConfigCustomValidator(typeof(PDLTestDetectorConfig), "CheckExposure")]
        public int ExposureMs { get; set; } = 80;
        [FCMConfigItem("触发", MemberId = "trigger", Order = 200)]
        public string TriggerMode { get; set; } = "Hardware";
        [FCMConfigItem("超时", MemberId = "timeout", Order = 300)]
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);
        [FCMConfigItem("ROI", MemberId = "roi", Order = 400)]
        public PDLTestRoi Roi { get; set; } = new PDLTestRoi();
        private static FCMConfigIssue CheckExposure(int ms, FCMConfigValidationContext ctx)
        { return ms % 10 == 0 ? null : FCMConfigIssue.Error(ctx.Path, "STEP", "曝光必须以10ms递增"); }
    }
    public sealed class PDLTestRoi
    {
        public int Width { get; set; } = 256;
        public int Height { get; set; } = 256;
        public List<PDLTestRegion> Exclusions { get; set; } = new List<PDLTestRegion>();
    }
    public sealed class PDLTestRegion
    {
        public string Id { get; set; }
        public double[] Vertices { get; set; } = new double[0];
    }
    public sealed class PDLTestMachineValidator : IFCMConfigValidator<PDLTestMachineConfig>
    {
        public IEnumerable<FCMConfigIssue> Validate(PDLTestMachineConfig model, FCMConfigValidationContext ctx)
        {
            if (model.Enabled && model.Detectors.Count == 0)
                yield return FCMConfigIssue.Error("Detectors", "EMPTY_DETECTORS", "已启用但无探测器");
            foreach (KeyValuePair<string, PDLTestDetectorConfig> detector in model.Detectors)
                if (detector.Value.Roi.Width <= 0 || detector.Value.Roi.Height <= 0)
                    yield return FCMConfigIssue.Error("Detectors[" + detector.Key + "].Roi", "ROI", "ROI无效");
        }
    }
    [FCMConfigSection("Tests.Rename", SchemaVersion = 2)]
    public sealed class PDLAliasConfig
    {
        [FCMConfigItem("机台名", MemberId = "machine.name", AliasFrom = "LegacyName", Order = 100)]
        public string Name { get; set; } = "new-default";
    }
    internal static class ConfigurationIntegrationTests
    {
        public static int RunAll()
        {
            int checks = 0;
            string root = Path.Combine(Path.GetTempPath(), "CxVisualCfgTests_" + Guid.NewGuid().ToString("N"));
            try
            {
                FCMConfigSchema schema = FCMConfigSchemaBuilder.Describe<PDLTestMachineConfig>();
                Check(schema.SchemaVersion == 2 && schema.Members.Count >= 8, "schema reflect", ref checks);
                PDLTestMachineConfig sample = new PDLTestMachineConfig();
                sample.Detectors["D1"] = new PDLTestDetectorConfig();
                sample.Detectors["D1"].Roi.Exclusions.Add(new PDLTestRegion { Id = "r1", Vertices = new double[] { 1, 2, 3, 4 } });
                string json = FCMJsonValueCodec.Serialize(sample);
                PDLTestMachineConfig restored = FCMJsonValueCodec.Deserialize<PDLTestMachineConfig>(json);
                Check(restored.FrameCount == ulong.MaxValue && restored.Factor == sample.Factor && restored.Mark == '测', "lossless scalar", ref checks);
                Check(restored.Detectors["D1"].Roi.Exclusions[0].Vertices[3] == 4, "nested list dictionary", ref checks);
                Check(restored.Endpoint.ToString() == sample.Endpoint.ToString(), "Uri", ref checks);
                string renamedLegacy = "{\"LegacyName\":\"old\",\"Unknown\":123}";
                PDLAliasConfig renamedTyped = FCMJsonValueCodec.Deserialize<PDLAliasConfig>(renamedLegacy);
                string renamedJson = FCMOrderedJsonDocument.Patch(renamedLegacy, renamedTyped, renamedTyped);
                Check(renamedTyped.Name == "old" && renamedJson.Contains("\"Name\":\"old\"") &&
                    renamedJson.Contains("\"Unknown\":123") && !renamedJson.Contains("LegacyName"), "schema alias preserves value and unknown members", ref checks);
                bool badJsonRejected = false;
                try { FCMJsonValueCodec.Deserialize<PDLAliasConfig>("{\"Name\":1,\"Name\":2}"); }
                catch (FormatException) { badJsonRejected = true; }
                Check(badJsonRejected, "duplicate JSON key rejected", ref checks);
                sample.Detectors["D1"].ExposureMs = 123;
                Check(!FCMConfigValidationEngine.Validate(sample, "machine:X").IsValid, "custom method validation", ref checks);
                sample.Detectors["D1"].ExposureMs = 80;
                sample.Name = "invalid";
                Check(!FCMConfigValidationEngine.Validate(sample, "machine:X").IsValid, "regex validation", ref checks);
                sample.Name = "MCH-001";
                sample.Detectors["D1"].Roi.Width = 0;
                Check(!FCMConfigValidationEngine.Validate(sample, "machine:X").IsValid, "cross field validation", ref checks);
                sample.Detectors["D1"].Roi.Width = 256;
                Check(FCMConfigValidationEngine.Validate(sample, "machine:X").IsValid, "valid sample", ref checks);

                MHSConfigService bootstrap = new MHSConfigService(root);
                Check(bootstrap.RegisterAssembly(typeof(PDLTestMachineConfig).Assembly) >= 1, "auto discovery", ref checks);
                bootstrap.ForMachine("MCH-A").Open<PDLTestMachineConfig>();
                string current = Directory.GetFiles(root, "current.json", SearchOption.AllDirectories).Single();
                // Simulate an existing v1 file with an unknown vendor field and intentionally distinct formatting.
                string legacy = "{\n  \"Name\": \"MCH-001\",\n  \"Enabled\": true,\n  \"Detectors\": {\n    \"D1\": {\n      \"Gain\": 1.00,\n      \"TriggerMode\": \"Hardware\",\n      \"VendorExtra\": \"unmodified-old-value\"\n    }\n  },\n  \"UnrecognizedRoot\": 99\n}\n";
                File.WriteAllText(current, legacy, new UTF8Encoding(false));
                // Simulate a legacy machine imported before this versioning service existed.
                string configFolder = Path.GetDirectoryName(current);
                foreach (string revision in Directory.GetFiles(Path.Combine(configFolder, "history"), "*.json"))
                    File.Delete(revision);
                string marker = Path.Combine(configFolder, "effective.txt");
                if (File.Exists(marker)) File.Delete(marker);
                File.Delete(Path.Combine(configFolder, "schema.contract"));
                // Explicit offline adoption verifies the exact source hash, validates
                // the typed model and writes a preserved original before opening.
                new MHSConfigService(root).AdoptUnversionedSection<PDLTestMachineConfig>(
                    new FCMConfigIdentity(schema.SectionId, "machine", "MCH-A"),
                    MHSConfigPackage.Hash(legacy));
                Check(File.Exists(Path.Combine(configFolder, "legacy-original-backup.json")),
                    "legacy source is preserved before adopting", ref checks);
                // Re-open with a new service instance after reviewed legacy adoption.
                MHSConfigService service = new MHSConfigService(root);
                MHSConfigSection<PDLTestMachineConfig> cfg = service.ForMachine("MCH-A").Open<PDLTestMachineConfig>();
                int savedNotifications = 0, effectiveNotifications = 0;
                IDisposable a = cfg.Subscribe(x => x.Detectors["D1"].ExposureMs, FCMConfigChangeStage.Committed,
                    ch => { if (ch.OldValue == 80 && ch.NewValue == 120) savedNotifications++; });
                IDisposable b = cfg.Subscribe(x => x.Detectors["D1"].ExposureMs, FCMConfigChangeStage.Effective,
                    ch => { if (ch.OldValue == 80 && ch.NewValue == 120) effectiveNotifications++; });
                string firstRevision = cfg.SavedVersion;
                MHSConfigEdit<PDLTestMachineConfig> stale = cfg.BeginEdit("stale test");
                using (MHSConfigEdit<PDLTestMachineConfig> edit = cfg.BeginEdit("insert exposure"))
                {
                    edit.WorkingCopy.Detectors["D1"].ExposureMs = 120;
                    MHSConfigPreview preview = edit.Preview();
                    Check(preview.Validation.IsValid, "valid preview", ref checks);
                    Check(preview.ChangedPaths.Contains("Detectors[\"D1\"].ExposureMs"), "deep diff path", ref checks);
                    MHSConfigCommitResult commit = edit.CommitAsync(CancellationToken.None).GetAwaiter().GetResult();
                    Check(commit.Success && commit.PendingApply, "revision commit and pending", ref checks);
                }
                string after = File.ReadAllText(current);
                int gain = after.IndexOf("\"Gain\"", StringComparison.Ordinal);
                int exposure = after.IndexOf("\"ExposureMs\"", StringComparison.Ordinal);
                int trigger = after.IndexOf("\"TriggerMode\"", StringComparison.Ordinal);
                Check(gain < exposure && exposure < trigger, "ordered insertion", ref checks);
                Check(after.Contains("1.00") && after.Contains("\"VendorExtra\": \"unmodified-old-value\"") &&
                    after.Contains("\"UnrecognizedRoot\": 99"), "unknown/trivia stable", ref checks);
                Check(cfg.Value(x => x.Detectors["D1"].ExposureMs) == 80 &&
                    cfg.GetLatestSaved().Detectors["D1"].ExposureMs == 120, "effective vs saved", ref checks);
                Check(savedNotifications == 1 && effectiveNotifications == 0, "commit event stage", ref checks);
                Check(cfg.Explain(x => x.Detectors["D1"].ExposureMs).PreviousSavedValue == 80, "history previous value", ref checks);
                Check(cfg.AtSavedVersion(firstRevision).Detectors["D1"].ExposureMs == 80, "read specific revision", ref checks);
                Check(cfg.ApplyPendingAtTaskBoundary() && cfg.Value(x => x.Detectors["D1"].ExposureMs) == 120,
                    "task boundary apply", ref checks);
                Check(effectiveNotifications == 1, "effective event stage", ref checks);
                using (MHSConfigSnapshot<PDLTestMachineConfig> pinned = cfg.CaptureForTask("scan-001"))
                    Check(pinned.Value(x => x.Detectors["D1"].ExposureMs) == 120, "task snapshot", ref checks);
                stale.WorkingCopy.Detectors["D1"].ExposureMs = 140;
                Check(!stale.CommitAsync(CancellationToken.None).GetAwaiter().GetResult().Success, "optimistic conflict", ref checks);
                stale.Dispose();
                a.Dispose(); b.Dispose();
                using (MHSConfigEdit<PDLTestMachineConfig> edit = cfg.BeginEdit("invalid"))
                {
                    edit.WorkingCopy.Detectors["D1"].ExposureMs = 127;
                    Check(!edit.CommitAsync(CancellationToken.None).GetAwaiter().GetResult().Success, "invalid commit rejected", ref checks);
                }
                Check(File.ReadAllText(current) == after, "failed commit leaves file unchanged", ref checks);
                Check(cfg.ListSavedVersions().Count >= 2, "history stored", ref checks);
                MHSConfigService restart = new MHSConfigService(root);
                MHSConfigSection<PDLTestMachineConfig> opened = restart.ForMachine("MCH-A").Open<PDLTestMachineConfig>();
                Check(opened.Value(x => x.Detectors["D1"].ExposureMs) == 120, "restart effective persistence", ref checks);
                Check(opened.Explain(x => x.Detectors["D1"].ExposureMs).HasPreviousEffective &&
                    opened.Explain(x => x.Detectors["D1"].ExposureMs).PreviousEffectiveValue == 80,
                    "previous effective retained across restart", ref checks);
                Console.WriteLine("CONFIGURATION INTEGRATION: PASS; checks=" + checks);
                return checks;
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
        private static void Check(bool okay, string text, ref int count)
        { if (!okay) throw new InvalidOperationException("CONFIG TEST FAILED: " + text); count++; }
    }
}