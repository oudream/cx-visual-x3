using System;
using System.IO;
using CxVisual.Mechanism.Core;
using CxVisual.Professional.Automation;
using CxVisual.Professional.Vision;
using CxVisual.Professional.CT3D;

namespace CxVisual.Tests.ArchitectureSmoke
{
    /// <summary>专业模块实际通过同一个L1基础使用配置，先测试再实现。</summary>
    internal static class ConfigProfessionalL1Tests
    {
        public static void Run()
        {
            string root = Path.Combine(Path.GetTempPath(), "cxvisual-prof-config-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                ThreeProfessionalTypesShareOneManager(root);
                CrossFieldRulesRejectUnsafeOrInconsistentParameters(root);
                EnumAndMetadataFlowToEditor(root);
                NoOpAndObserverFailureAreSafe(root);
                MultiMachineIsolationAndReopen(root);
                Console.WriteLine("PASS Professional Config L1: Automation, Vision, CT3D, cross-field validation, metadata, isolation, persistence");
            }
            finally { Directory.Delete(root, true); }
        }

        private static CxConfigManager Make(string directory)
        {
            CxConfigManager m = new CxConfigManager(directory);
            m.Register<CxAutomationDeviceOptions>("Automation.Device01", validator: CxAutomationDeviceOptions.Validate);
            m.Register<CxAutomationDeviceOptions>("Automation.Device02", validator: CxAutomationDeviceOptions.Validate);
            m.Register<CxVisionInspectionOptions>("Vision.Inspection", validator: CxVisionInspectionOptions.Validate);
            m.Register<CxCtReconstructionOptions>("CT3D.Reconstruction", validator: CxCtReconstructionOptions.Validate);
            m.Initialize();
            return m;
        }

        private static void ThreeProfessionalTypesShareOneManager(string root)
        {
            string path = Path.Combine(root, "all");
            CxConfigManager m = Make(path);
            Check(m.Get<CxAutomationDeviceOptions>("Automation.Device01").ControllerPort == 502, "Automation default");
            Check(m.Get<CxVisionInspectionOptions>("Vision.Inspection").PixelDepth == CxVisionPixelDepth.Bits16, "Vision enum default");
            Check(m.Get<CxCtReconstructionOptions>("CT3D.Reconstruction").ReconstructionMode == CxCtReconstructionMode.Fdk, "CT3D default");
            m.UpdateAndSave<CxAutomationDeviceOptions>("Automation.Device01", x => x.ControllerPort = 1502);
            m.UpdateAndSave<CxVisionInspectionOptions>("Vision.Inspection", x => x.GrayThreshold = 3000);
            m.UpdateAndSave<CxCtReconstructionOptions>("CT3D.Reconstruction", x => x.GpuIndex = 1);
            CxConfigManager reloaded = Make(path);
            Check(reloaded.Get<CxAutomationDeviceOptions>("Automation.Device01").ControllerPort == 1502, "Automation reload");
            Check(reloaded.Get<CxVisionInspectionOptions>("Vision.Inspection").GrayThreshold == 3000, "Vision reload");
            Check(reloaded.Get<CxCtReconstructionOptions>("CT3D.Reconstruction").GpuIndex == 1, "CT3D reload");
            Check(reloaded.GetRevision("Vision.Inspection") == 1 && reloaded.GetRevision("CT3D.Reconstruction") == 1,
                "Professional revision persisted");
        }

        private static void CrossFieldRulesRejectUnsafeOrInconsistentParameters(string root)
        {
            CxConfigManager m = Make(Path.Combine(root, "cross"));
            ExpectArgument(() => m.Update<CxAutomationDeviceOptions>("Automation.Device01",
                x => x.ControllerHost = " "), "Blank controller host accepted");
            ExpectArgument(() => m.Update<CxAutomationDeviceOptions>("Automation.Device01",
                x => x.ControllerPort = 70000), "Out-of-range port accepted");
            ExpectArgument(() => m.Update<CxAutomationDeviceOptions>("Automation.Device01",
                x => x.ReconnectDelayMs = 100), "Unsafe reconnect pace accepted");

            ExpectArgument(() => m.Update<CxVisionInspectionOptions>("Vision.Inspection",
                x => { x.PixelDepth = CxVisionPixelDepth.Bits8; x.GrayThreshold = 256; }),
                "8-bit threshold accepted 256");
            ExpectArgument(() => m.Update<CxVisionInspectionOptions>("Vision.Inspection",
                x => x.MinDefectArea = x.MaxDefectArea + 1), "Inverted defect area accepted");
            ExpectArgument(() => m.Update<CxVisionInspectionOptions>("Vision.Inspection",
                x => { x.UseAi = true; x.AiModelName = ""; }), "Missing AI model accepted");

            ExpectArgument(() => m.Update<CxCtReconstructionOptions>("CT3D.Reconstruction",
                x => x.FddMm = x.FodMm), "CT geometry FDD <= FOD accepted");
            ExpectArgument(() => m.Update<CxCtReconstructionOptions>("CT3D.Reconstruction",
                x => x.ProjectionCount = 0), "Empty CT projections accepted");
            Check(m.GetRevision("Vision.Inspection") == 0 &&
                  m.GetRevision("CT3D.Reconstruction") == 0, "Rejected professional edit changed revision");
        }

        private static void EnumAndMetadataFlowToEditor(string root)
        {
            CxConfigProperty[] vision = CxConfigDescription.Describe<CxVisionInspectionOptions>();
            CxConfigProperty[] ct = CxConfigDescription.Describe<CxCtReconstructionOptions>();
            Check(Array.Exists(vision, x => x.Name == "GrayThreshold" && x.Maximum == 65535 && x.Unit == "Gray"),
                "Vision precision/unit metadata lost");
            Check(Array.Exists(vision, x => x.Name == "PixelDepth" && x.PropertyType == typeof(CxVisionPixelDepth)),
                "Vision enum UI metadata missing");
            Check(Array.Exists(ct, x => x.Name == "FddMm" && x.Unit == "mm" && x.Minimum == 1),
                "CT geometry metadata missing");
        }

        private static void NoOpAndObserverFailureAreSafe(string root)
        {
            CxConfigManager m = Make(Path.Combine(root, "notifications"));
            int observed = 0, failures = 0;
            m.NotificationFailed += (id, error) => failures++;
            using (m.Subscribe<CxVisionInspectionOptions>("Vision.Inspection", (x, c) =>
                   { observed++; throw new InvalidOperationException("simulated observer failure"); }))
            {
                m.Update<CxVisionInspectionOptions>("Vision.Inspection", x => x.GrayThreshold = x.GrayThreshold);
                Check(m.GetRevision("Vision.Inspection") == 0 && observed == 0, "No-op created spurious revision");
                m.Update<CxVisionInspectionOptions>("Vision.Inspection", x => x.GrayThreshold = 2000);
                Check(observed == 1 && failures == 1, "Listener exception isolation failed");
                Check(m.Get<CxVisionInspectionOptions>("Vision.Inspection").GrayThreshold == 2000,
                    "Listener error rolled back committed value");
            }
        }

        private static void MultiMachineIsolationAndReopen(string root)
        {
            string directory = Path.Combine(root, "multimachine");
            CxConfigManager m = Make(directory);
            m.UpdateAndSave<CxAutomationDeviceOptions>("Automation.Device02", x => x.StationId = 7);
            Check(m.Get<CxAutomationDeviceOptions>("Automation.Device01").StationId == 1,
                "Device02 edit leaked to Device01");
            CxConfigManager reloaded = Make(directory);
            Check(reloaded.Get<CxAutomationDeviceOptions>("Automation.Device01").StationId == 1 &&
                  reloaded.Get<CxAutomationDeviceOptions>("Automation.Device02").StationId == 7,
                  "Multi-device isolation failed after reload");
        }

        private static void ExpectArgument(Action action, string message)
        {
            try { action(); }
            catch (ArgumentException) { return; }
            throw new InvalidOperationException(message);
        }
        private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    }
}
