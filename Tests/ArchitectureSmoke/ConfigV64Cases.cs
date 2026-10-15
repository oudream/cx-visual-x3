using System;
using System.IO;
using System.Linq;
using CxVisual.Foundation.Data;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Runtime;
using CxVisual.Mechanism.Lifecycle;
using CxVisual.Mechanism.Presentation.Wpf;
using CxVisual.Professional.Vision;
using CxVisual.Professional.Automation;
using CxVisual.Professional.CT3D;

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
                ProfessionalProviderApplyAndFailureRecovery(root);
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
                manifest.Replace("\"CreatedUtc\":", "\"ReportedUtc\":")); // 同一ReleaseId，变更发布元数据，不改变配置的SHA
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


        /// <summary>运行软件快照不是设备ACK：只有明确提供Applier并Verify成功才标记专业确认。</summary>
        private static void ProfessionalProviderApplyAndFailureRecovery(string root)
        {
            MTCConfigManager vision = MTCConfigManager.Create<CxVisionInspectionOptions>(
                Path.Combine(root, "provider-vision"), new FCMConfigKey("Vision", "Run"), CxVisionInspectionOptions.Validate);
            SampleApplier<CxVisionInspectionOptions> v = new SampleApplier<CxVisionInspectionOptions>();
            MRKConfigRuntimeSession<CxVisionInspectionOptions> run =
                new MRKConfigRuntimeSession<CxVisionInspectionOptions>(
                    vision, "Vision.Run", applier: v);
            vision.UpdateAndSave<CxVisionInspectionOptions>("Vision.Run", x => x.GrayThreshold = 2200);
            Check(run.RequestApply(MRKConfigApplyPolicy.Immediate), "Successful professional apply rejected");
            Check(run.ActiveRevision == 1 && run.LastApplyStatus == MRKConfigApplyStatus.VerifiedByProvider,
                "Software active was wrongly equated to provider verification");
            Check(v.Prepared == 1 && v.Applied == 1 && v.Verified == 1, "Full provider pipeline did not run");
            vision.UpdateAndSave<CxVisionInspectionOptions>("Vision.Run", x => x.GrayThreshold = 2300);
            v.FailVerify = true;
            Check(!run.RequestApply(MRKConfigApplyPolicy.Immediate), "Failed Verify was reported successful");
            Check(run.ActiveRevision == 1 && v.RolledBack == 1 &&
                run.LastApplyStatus == MRKConfigApplyStatus.Failed, "Provider failure did not preserve old active");
            v.FailVerify = false;
            vision.UpdateAndSave<CxVisionInspectionOptions>("Vision.Run", x => x.GrayThreshold = 2400);
            Check(run.RequestApply(MRKConfigApplyPolicy.Immediate) && run.ActiveRevision == 3,
                "Verified application could not resume after compensated failure");
            vision.UpdateAndSave<CxVisionInspectionOptions>("Vision.Run", x => x.GrayThreshold = 2500);
            v.FailVerify = true;
            v.FailRollback = true;
            Check(!run.RequestApply(MRKConfigApplyPolicy.Immediate), "Rollback fault was treated as success");
            Check(run.IsFaulted, "Failure to restore professional device state did not set Faulted");
            Expect<InvalidOperationException>(() => run.BeginTask(), "Faulted professional runtime permitted new jobs");

            MTCConfigManager motion = MTCConfigManager.Create<CxAutomationDeviceOptions>(
                Path.Combine(root, "provider-automation"), new FCMConfigKey("Automation", "Drive"),
                CxAutomationDeviceOptions.Validate);
            SampleApplier<CxAutomationDeviceOptions> a = new SampleApplier<CxAutomationDeviceOptions>();
            MRKConfigRuntimeSession<CxAutomationDeviceOptions> autoRuntime =
                new MRKConfigRuntimeSession<CxAutomationDeviceOptions>(motion, "Automation.Drive", applier: a);
            motion.UpdateAndSave<CxAutomationDeviceOptions>("Automation.Drive", x => x.ControllerPort = 1502);
            Check(autoRuntime.RequestApply(MRKConfigApplyPolicy.RestartSession), "Reconnect policy prepare failed");
            Check(autoRuntime.ActiveRevision == 0 && a.Applied == 0, "Reconnect config applied prematurely");
            Check(autoRuntime.OnSessionRestarted() && a.Verified == 1 &&
                autoRuntime.LastApplyStatus == MRKConfigApplyStatus.VerifiedByProvider,
                "Automation provider did not verify after reconnection boundary");

            MTCConfigManager ct = MTCConfigManager.Create<CxCtReconstructionOptions>(
                Path.Combine(root, "provider-ct"), new FCMConfigKey("CT3D", "Recon"),
                CxCtReconstructionOptions.Validate);
            SampleApplier<CxCtReconstructionOptions> c = new SampleApplier<CxCtReconstructionOptions>();
            MRKConfigRuntimeSession<CxCtReconstructionOptions> ctRuntime =
                new MRKConfigRuntimeSession<CxCtReconstructionOptions>(ct, "CT3D.Recon", applier: c);
            using (MRKConfigTaskSnapshot<CxCtReconstructionOptions> task = ctRuntime.BeginTask())
            {
                ct.UpdateAndSave<CxCtReconstructionOptions>("CT3D.Recon", x => x.GpuIndex = 2);
                Check(ctRuntime.RequestApply(MRKConfigApplyPolicy.NextTask), "CT next task staging failed");
                Check(ctRuntime.ActiveRevision == 0 && c.Applied == 0,
                    "CT configuration applied during active reconstruction");
            }
            using (MRKConfigTaskSnapshot<CxCtReconstructionOptions> next = ctRuntime.BeginTask())
                Check(next.Revision == 1 && next.Value.GpuIndex == 2 &&
                    ctRuntime.LastApplyStatus == MRKConfigApplyStatus.VerifiedByProvider,
                    "CT configuration was not verified at next-task boundary");
        }

        private sealed class SampleApplier<T> : IMRKConfigApplier<T> where T : class
        {
            public int Prepared, Applied, Verified, RolledBack;
            public bool FailVerify, FailRollback;
            public string Prepare(T oldValue, T newValue) { Prepared++; return null; }
            public string Apply(T oldValue, T newValue) { Applied++; return null; }
            public string Verify(T oldValue, T newValue)
            {
                Verified++;
                return FailVerify ? "Simulated device did not acknowledge setting" : null;
            }
            public void Rollback(T oldValue)
            {
                RolledBack++;
                if (FailRollback) throw new InvalidOperationException("Simulated rollback failed");
            }
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
