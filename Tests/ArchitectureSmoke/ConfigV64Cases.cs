using System;
using System.IO;
using System.Linq;
using CxVisual.Foundation.Data;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Runtime;
using CxVisual.Mechanism.Lifecycle;
using CxVisual.Mechanism.Presentation.Wpf;
using CxVisual.Professional.Vision;

namespace CxVisual.Tests.ArchitectureSmoke
{
    /// <summary>V6.4子方面前缀 + 真实工业配置案例：先定义断言，再调整实际公开类型。</summary>
    internal static class ConfigV64Cases
    {
        internal static void Run()
        {
            string root = Path.Combine(Path.GetTempPath(), "CxVisual-ConfigV64-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                PrefixAndOwnerContracts();
                OneCallStartupAndTypedKeys(root);
                ImmutableReleaseManifest(root);
                SourceVersionVector(root);
                Console.WriteLine("PASS Config V6.4: FCM/MPM/MTC/MRK/MVD/MPW prefixes, simple startup, scope keys, release identity, source revisions");
            }
            finally { Directory.Delete(root, true); }
        }

        private static void PrefixAndOwnerContracts()
        {
            Check(typeof(FCMConfigKey).Namespace == "CxVisual.Foundation.Data", "FCM F15 owner");
            Check(typeof(FCMConfigJsonFile).Namespace == "CxVisual.Foundation.Data", "FCM JSON owner");
            Check(typeof(MPMConfigItemAttribute).Namespace == "CxVisual.Mechanism.Core", "MPM metadata owner");
            Check(typeof(MPMConfigLayeredProfile<>).Namespace == "CxVisual.Mechanism.Core", "MPM configuration profile owner");
            Check(typeof(MTCConfigManager).Namespace == "CxVisual.Mechanism.Core", "MTC mutation owner");
            Check(typeof(MRKConfigRuntimeSession<>).Namespace == "CxVisual.Mechanism.Runtime", "MRK runtime owner");
            Check(typeof(MVDConfigReleaseManifest).Namespace == "CxVisual.Mechanism.Lifecycle", "MVD release owner");
            Check(typeof(MPWConfigEditorDialog).Namespace == "CxVisual.Mechanism.Presentation.Wpf", "MPW WPF owner");
        }

        private static void OneCallStartupAndTypedKeys(string root)
        {
            FCMConfigKey key = new FCMConfigKey("Vision", "Station01");
            Check(key.Id == "Vision.Station01" && key.Scope == "Vision" && key.Path == "Station01", "ConfigKey incorrectly normalized");
            Expect<ArgumentException>(() => new FCMConfigKey("Vision", "../data"), "Path traversal key accepted");
            MTCConfigManager m = MTCConfigManager.Create<CxVisionInspectionOptions>(
                Path.Combine(root, "simple"), key, CxVisionInspectionOptions.Validate);
            Check(m.Get<CxVisionInspectionOptions>(key).GrayThreshold == 1800, "One-call default startup failed");
            m.UpdateAndSave<CxVisionInspectionOptions>(key.Id, x => x.GrayThreshold = 2020);
            MTCConfigManager reopened = MTCConfigManager.Create<CxVisionInspectionOptions>(
                Path.Combine(root, "simple"), key, CxVisionInspectionOptions.Validate);
            Check(reopened.Get<CxVisionInspectionOptions>(key).GrayThreshold == 2020, "One-call startup failed to reload");
        }

        private static void ImmutableReleaseManifest(string root)
        {
            MTCConfigManager m = MTCConfigManager.Create<CxVisionInspectionOptions>(
                Path.Combine(root, "data"), new FCMConfigKey("Vision", "Inspection"), CxVisionInspectionOptions.Validate);
            MVDConfigReleasePackage pkg = MVDConfigReleaseBuilder.Build(m, new[] { "Vision.Inspection" },
                Path.Combine(root, "packages"), "release-immutable");
            MVDConfigReleaseReceiver target = new MVDConfigReleaseReceiver(Path.Combine(root, "target"));
            target.Stage(pkg.Path);
            string forged = Path.Combine(root, "modified", "release-immutable");
            Directory.CreateDirectory(Path.Combine(forged, "payload"));
            File.Copy(Path.Combine(pkg.Path, "payload", "Vision.Inspection.json"),
                Path.Combine(forged, "payload", "Vision.Inspection.json"));
            string manifest = File.ReadAllText(Path.Combine(pkg.Path, "manifest.json"));
            File.WriteAllText(Path.Combine(forged, "manifest.json"),
                manifest.Replace("2026-", "2021-")); // 同一ReleaseId，变更发布元数据，不改变配置的SHA
            // 若时间字段因运行年不同没有匹配，则拒绝这个测试而非偶然通过。
            Check(File.ReadAllText(Path.Combine(forged, "manifest.json")) != manifest, "Manifest alteration did not change bytes");
            Expect<InvalidDataException>(() => target.Stage(forged), "Same ReleaseId accepted different manifest");
            Check(target.ActiveReleaseId == null, "Stage must not activate configuration");
        }

        private static void SourceVersionVector(string root)
        {
            MTCConfigManager manager = new MTCConfigManager(Path.Combine(root, "layers"));
            MPMConfigLayeredProfile<CxVisionInspectionOptions> profile =
                MPMConfigLayeredProfile<CxVisionInspectionOptions>.Register(manager, "Vision.Base", CxVisionInspectionOptions.Validate,
                    new MPMConfigLayerSpec("Vision.Template", 10), new MPMConfigLayerSpec("Vision.Machine", 20));
            manager.Initialize();
            profile.UpdateLayer("Vision.Template", x => x.GrayThreshold = 1500);
            profile.UpdateLayer("Vision.Machine", x => x.GrayThreshold = 1700);
            MPMResolvedConfig<CxVisionInspectionOptions> effective = profile.Resolve();
            Check(effective.Value.GrayThreshold == 1700 && effective.Origins["GrayThreshold"] == "Vision.Machine",
                "Effective value/source mismatch");
            Check(effective.SourceRevisions["Vision.Template"] == manager.GetRevision("Vision.Template") &&
                effective.SourceRevisions["Vision.Machine"] == manager.GetRevision("Vision.Machine"), "Source version mismatch");
        }

        private static void Expect<T>(Action action, string reason) where T : Exception
        {
            try { action(); } catch (T) { return; }
            throw new InvalidOperationException(reason);
        }
        private static void Check(bool ok, string reason)
        { if (!ok) throw new InvalidOperationException(reason); }
    }
}
