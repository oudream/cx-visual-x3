using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Mechanism.Core.Modules;
using CxVisual.Mechanism.Connectivity;
using CxVisual.Mechanism.Data.Configuration;
using CxVisual.Mechanism.Lifecycle.Modules;
using CxVisual.Professional.Simulation.Modules;
using CxVisual.Professional.Vision.Modules;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class ModuleConfigAnnotationSmokeTests
    {
        private static void Check(bool condition, string reason)
        {
            if (!condition) throw new InvalidOperationException("MODULE.CONFIG " + reason);
        }
        private static void CheckIssue(MCNConnectorConfiguration model, string code)
        {
            FCMConfigValidationReport report = FCMConfigValidationEngine.Validate(model, "test");
            Check(!report.IsValid && report.Issues.Any(x => x.Code == code),
                "Expected invalid config: " + code);
        }

        public static void Run()
        {
            Type[] types = { typeof(MFMModuleIdentityConfig), typeof(MCNConnectorConfiguration),
                typeof(PVCComputeModuleConfiguration), typeof(PSMSimulationModuleConfiguration), typeof(MLCServiceModuleConfiguration) };
            HashSet<string> sectionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (Type type in types)
            {
                FCMConfigSchema schema = FCMConfigSchemaBuilder.Describe(type);
                Check(sectionIds.Add(schema.SectionId) && schema.Members.Count > 0,
                    "Missing/duplicate section schema: " + type);
                Check(FCMConfigTypeCatalog.Describe(type).CanRoundtrip,
                    "Configuration model must be a roundtrip-safe POCO: " + type);
                Check(FCMConfigCatalog.Describe(type).Count > 0,
                    "No auto-editor metadata for: " + type);
            }

            MCNConnectorConfiguration connector = new MCNConnectorConfiguration {
                DisplayName = "PLC 接入",
                Channels = new List<MCCChannelConfiguration> {
                    new MCCChannelConfiguration { Id = "tcp01", Kind = MCCChannelConfigurationKind.TcpClient,
                        Host = "127.0.0.1", Port = 502 }
                },
                Protocols = new List<MPAProtocolBinding> {
                    new MPAProtocolBinding { Id = "mod01", Kind = MPAProtocolBindingKind.ModbusTcpDraft,
                        ChannelRef = "tcp01" }
                },
                Points = new List<MCNPointDefinition> {
                    new MCNPointDefinition { Name = "T", ProtocolRef = "mod01", Address = "holding:100",
                        TargetRef = "PLC.T" }
                }
            };
            Check(FCMConfigValidationEngine.Validate(connector, "test").IsValid,
                "Valid connector must pass the same schema validator as UI");
            // UI 与运行读取的是同一正式配置类，不存在 EditableXXX -> Options 的转换。
            Check(connector.Channels[0].DescribeType().IO == MCCIOCapabilities.ByteStream,
                "Channel config directly provides runtime metadata");
            Check(connector.Protocols[0].ProtocolTypeId == "modbus.tcp" &&
                  connector.Points[0].ValueType == "UInt16", "Protocol/Point single config");

            connector.Channels.Add(new MCCChannelConfiguration { Id = "tcp01" });
            CheckIssue(connector, "CHANNEL_DUPLICATE_OR_EMPTY");
            connector.Channels.RemoveAt(1);
            connector.Protocols[0].ChannelRef = "missing";
            CheckIssue(connector, "CHANNEL_REFERENCE");
            connector.Protocols[0].ChannelRef = "tcp01";
            connector.Protocols[0].Kind = MPAProtocolBindingKind.ModbusRtuDraft;
            CheckIssue(connector, "PROTOCOL_IO_MISMATCH");
            connector.Protocols[0].Kind = MPAProtocolBindingKind.ModbusTcpDraft;
            connector.Points[0].ProtocolRef = "missing";
            CheckIssue(connector, "POINT_PROTOCOL_MISSING");
            connector.Points[0].ProtocolRef = "mod01";
            Check(FCMConfigValidationEngine.Validate(connector, "test").IsValid, "Recovered connector");

            string root = Path.Combine(Path.GetTempPath(), "cxvisual-module-config-" + Guid.NewGuid().ToString("N"));
            try
            {
                string scopeId = "module-test-" + Guid.NewGuid().ToString("N");
                MHSConfigService service = new MHSConfigService(root);
                service.RegisterAssembly(typeof(MFMModuleIdentityConfig).Assembly);
                service.RegisterAssembly(typeof(MCNConnectorConfiguration).Assembly);
                service.RegisterAssembly(typeof(PVCComputeModuleConfiguration).Assembly);
                service.RegisterAssembly(typeof(PSMSimulationModuleConfiguration).Assembly);
                service.RegisterAssembly(typeof(MLCServiceModuleConfiguration).Assembly);
                Check(service.FindRegisteredSection("CxVisual.Module.Connector") != null,
                    "Module schema registration");
                MHSConfigSection<MFMModuleIdentityConfig> identity =
                    service.ForScope("module", scopeId).Open<MFMModuleIdentityConfig>();
                MHSConfigCommitResult record = identity.UpdateAsync(
                    x => x.Kind = MFMModuleKind.Connector, "登记模块分类", CancellationToken.None)
                    .GetAwaiter().GetResult();
                Check(record.Success && identity.GetLatestSaved().Kind == MFMModuleKind.Connector,
                    "Module kind persistence");
                MHSConfigSection<MCNConnectorConfiguration> module =
                    service.ForScope("module", scopeId).Open<MCNConnectorConfiguration>();
                MHSConfigCommitResult saved = module.UpdateAsync(model => {
                    model.DisplayName = connector.DisplayName;
                    model.Channels = connector.Channels;
                    model.Protocols = connector.Protocols;
                    model.Points = connector.Points;
                }, "首次模块配置", CancellationToken.None).GetAwaiter().GetResult();
                Check(saved.Success && module.GetLatestSaved().Channels.Count == 1,
                    "MHS persisted annotated module options");
                // 从新 MHS 服务重新打开真实磁盘修订，确保没有额外 ModuleConfigStore。
                MHSConfigService reopened = new MHSConfigService(root);
                MCNConnectorConfiguration restored = reopened.ForScope("module", scopeId)
                    .Open<MCNConnectorConfiguration>().GetLatestSaved();
                Check(restored.Channels.Count == 1 && restored.Protocols.Count == 1 &&
                    restored.Points.Count == 1 && restored.Points[0].TargetRef == "PLC.T",
                    "Module configuration disk roundtrip");
                Check(reopened.ForScope("module", scopeId).Open<MFMModuleIdentityConfig>()
                    .GetLatestSaved().Kind == MFMModuleKind.Connector, "Identity roundtrip");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
            PVCComputeModuleConfiguration compute = new PVCComputeModuleConfiguration { MaxConcurrency = 3 };
            Check(compute.MaxConcurrency == 3, "Compute options");
            Check(FCMConfigValidationEngine.Validate(compute, "test").IsValid, "Compute schema");
            compute.MaxConcurrency = 0;
            Check(!FCMConfigValidationEngine.Validate(compute, "test").IsValid, "Compute Range");

            PSMSimulationModuleConfiguration simulation = new PSMSimulationModuleConfiguration();
            Check(TimeSpan.FromMilliseconds(simulation.StepMilliseconds) == TimeSpan.FromMilliseconds(10), "Simulation definition");
            Check(FCMConfigValidationEngine.Validate(simulation, "test").IsValid, "Simulation schema");

            MLCServiceModuleConfiguration serviceConfig = new MLCServiceModuleConfiguration();
            Check(serviceConfig.MaxConcurrentRequests == 1, "Service options");
            Check(FCMConfigValidationEngine.Validate(serviceConfig, "test").IsValid, "Service schema");

            Console.WriteLine("MODULE.CONFIG: annotated schema/editor metadata + MHS persisted roundtrip PASS");
        }
    }
}
