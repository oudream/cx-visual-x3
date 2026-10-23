using System;
using System.IO;
using System.Linq;
using System.Threading;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Mechanism.Core.Modules;
using CxVisual.Mechanism.Data.Configuration;
using CxVisual.Mechanism.Connectivity;
using CxVisual.Mechanism.Lifecycle.Modules;
using CxVisual.Professional.Vision.Modules;
using CxVisual.Professional.Simulation.Modules;
using CxVisual.TestCenter.Core;
using CxVisual.TestCenter.Discovery;

namespace CxVisual.TestCenter.Cases
{
    /// <summary>
    /// Validate the exact production FCM/MHS configuration types used by Manual.Module.
    /// This does not simulate equipment or create fake module execution results.
    /// </summary>
    public static class TCTModuleConfigurationCases
    {
        [TCTTest("TCT.MODULE.CONFIG.001",
            "四类模块唯一配置Schema的保存、重开、范围隔离",
            TCTTestAspect.Integration, "TestCenter / Manual / Module",
            ObjectId = "MHSConfigService")]
        public static void FourProductionConfigSectionsRoundTrip(TCTTestContext test)
        {
            string root = Path.Combine(Path.GetTempPath(),
                "TCTModule-" + Guid.NewGuid().ToString("N"));
            try
            {
                MHSConfigService first = OpenService(root);
                CheckSection<MCNConnectorConfiguration>(
                    first, "ConnectorTest", "Connector", MFMModuleKind.Connector, test);
                CheckSection<PVCComputeModuleConfiguration>(
                    first, "ComputeTest", "Compute", MFMModuleKind.Compute, test);
                CheckSection<PSMSimulationModuleConfiguration>(
                    first, "SimulationTest", "Simulation", MFMModuleKind.Simulation, test);
                CheckSection<MLCServiceModuleConfiguration>(
                    first, "ServiceTest", "Service", MFMModuleKind.Service, test);

                MHSConfigService reopened = OpenService(root);
                CheckReopened<MCNConnectorConfiguration>(reopened, "ConnectorTest", "Connector", test);
                CheckReopened<PVCComputeModuleConfiguration>(reopened, "ComputeTest", "Compute", test);
                CheckReopened<PSMSimulationModuleConfiguration>(reopened, "SimulationTest", "Simulation", test);
                CheckReopened<MLCServiceModuleConfiguration>(reopened, "ServiceTest", "Service", test);
            }
            finally
            {
                try { if (Directory.Exists(root)) Directory.Delete(root, true); }
                catch (IOException) { /* test observations already contain all verification */ }
            }
        }

        private static MHSConfigService OpenService(string root)
        {
            MHSConfigService service = new MHSConfigService(root);
            service.RegisterAssembly(typeof(MFMModuleIdentityConfig).Assembly);
            service.RegisterAssembly(typeof(MCNConnectorConfiguration).Assembly);
            service.RegisterAssembly(typeof(PVCComputeModuleConfiguration).Assembly);
            service.RegisterAssembly(typeof(PSMSimulationModuleConfiguration).Assembly);
            service.RegisterAssembly(typeof(MLCServiceModuleConfiguration).Assembly);
            return service;
        }

        private static void CheckSection<T>(MHSConfigService service, string id,
            string label, MFMModuleKind kind, TCTTestContext test)
            where T : MFMModuleConfiguration, new()
        {
            MHSConfigSection<MFMModuleIdentityConfig> identity =
                service.ForScope("module", id).Open<MFMModuleIdentityConfig>();
            MHSConfigCommitResult typeCommit = identity.UpdateAsync(
                model => model.Kind = kind, "登记唯一ModuleKind", CancellationToken.None)
                .GetAwaiter().GetResult();
            test.AssertTrue(typeCommit.Success, label + "身份提交", true, typeCommit.Success);

            MHSConfigSection<T> section = service.ForScope("module", id).Open<T>();
            MHSConfigCommitResult result = section.UpdateAsync(
                model => model.DisplayName = label + " production config",
                "保存生产正式配置", CancellationToken.None).GetAwaiter().GetResult();
            test.AssertTrue(result.Success, label + "真实MHS保存", true, result.Success);
            test.AssertTrue(section.GetLatestSaved().DisplayName ==
                label + " production config", label + "已保存内容", true, true);
        }

        private static void CheckReopened<T>(MHSConfigService service, string id,
            string label, TCTTestContext test) where T : MFMModuleConfiguration, new()
        {
            T value = service.ForScope("module", id).Open<T>().GetLatestSaved();
            MFMModuleIdentityConfig identity =
                service.ForScope("module", id).Open<MFMModuleIdentityConfig>().GetLatestSaved();
            test.AssertTrue(identity.Kind.HasValue, label + "真实身份重开", true, identity.Kind.HasValue);
            test.AssertTrue(value.DisplayName == label + " production config",
                label + "已保存配置重新打开", true, true);
            FCMConfigValidationReport report =
                FCMConfigValidationEngine.Validate(value, "module:" + id);
            test.AssertTrue(report.IsValid, label + "正式FCM校验", true, report.IsValid);
        }

        [TCTTest("TCT.MODULE.CONFIG.002",
            "Connector悬空点引用与重复通道Id不能通过生产Validator",
            TCTTestAspect.Integration, "TestCenter / Manual / Module",
            ObjectId = "MCNConnectorConfiguration")]
        public static void ConnectorRejection(TCTTestContext test)
        {
            MCNConnectorConfiguration config = new MCNConnectorConfiguration();
            config.Channels.Add(new MCCChannelConfiguration { Id = "C1" });
            config.Channels.Add(new MCCChannelConfiguration { Id = "C1" });
            FCMConfigValidationReport result =
                FCMConfigValidationEngine.Validate(config, "module:invalid");
            bool duplicate = result.Issues.Any(x => x.Code == "CHANNEL_DUPLICATE_OR_EMPTY");
            test.AssertTrue(!result.IsValid && duplicate,
                "FCM正式Validator拒绝重复通道", true, duplicate);
        }
    }
}
