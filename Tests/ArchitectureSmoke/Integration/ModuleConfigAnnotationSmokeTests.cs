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
        private static void CheckIssue(MCNConnectorEditableConfig model, string code)
        {
            FCMConfigValidationReport report = FCMConfigValidationEngine.Validate(model, "test");
            Check(!report.IsValid && report.Issues.Any(x => x.Code == code),
                "Expected invalid config: " + code);
        }

        public static void Run()
        {
            Type[] types = { typeof(MFMModuleIdentityConfig), typeof(MCNConnectorEditableConfig),
                typeof(PVCComputeEditableConfig), typeof(PSMSimulationEditableConfig), typeof(MLCServiceEditableConfig) };
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

            MCNConnectorEditableConfig connector = new MCNConnectorEditableConfig {
                DisplayName = "PLC 接入",
                Channels = new List<MCNEditableChannel> {
                    new MCNEditableChannel { Id = "tcp01", Kind = MCNEditableChannelKind.TcpClient,
                        Host = "127.0.0.1", Port = 502 }
                },
                Protocols = new List<MCNEditableProtocol> {
                    new MCNEditableProtocol { Id = "mod01", Kind = MCNEditableProtocolKind.ModbusTcpDraft,
                        ChannelRef = "tcp01" }
                },
                Points = new List<MCNEditablePoint> {
                    new MCNEditablePoint { Name = "T", ProtocolRef = "mod01", Address = "holding:100",
                        TargetRef = "PLC.T" }
                }
            };
            Check(FCMConfigValidationEngine.Validate(connector, "test").IsValid,
                "Valid connector must pass the same schema validator as UI");
            connector.Channels.Add(new MCNEditableChannel { Id = "tcp01" });
            CheckIssue(connector, "CHANNEL_DUPLICATE_OR_EMPTY");
            connector.Channels.RemoveAt(1);
            connector.Protocols[0].ChannelRef = "missing";
            CheckIssue(connector, "CHANNEL_REFERENCE");
            connector.Protocols[0].ChannelRef = "tcp01";
            connector.Protocols[0].Kind = MCNEditableProtocolKind.ModbusRtuDraft;
            CheckIssue(connector, "PROTOCOL_IO_MISMATCH");
            connector.Protocols[0].Kind = MCNEditableProtocolKind.ModbusTcpDraft;
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
                service.RegisterAssembly(typeof(MCNConnectorEditableConfig).Assembly);
                service.RegisterAssembly(typeof(PVCComputeEditableConfig).Assembly);
                service.RegisterAssembly(typeof(PSMSimulationEditableConfig).Assembly);
                service.RegisterAssembly(typeof(MLCServiceEditableConfig).Assembly);
                Check(service.FindRegisteredSection("CxVisual.Module.Connector") != null,
                    "Module schema registration");
                MHSConfigSection<MFMModuleIdentityConfig> identity =
                    service.ForScope("module", scopeId).Open<MFMModuleIdentityConfig>();
                MHSConfigCommitResult record = identity.UpdateAsync(
                    x => x.Kind = MFMModuleKind.Connector, "登记模块分类", CancellationToken.None)
                    .GetAwaiter().GetResult();
                Check(record.Success && identity.GetLatestSaved().Kind == MFMModuleKind.Connector,
                    "Module kind persistence");
                MHSConfigSection<MCNConnectorEditableConfig> module =
                    service.ForScope("module", scopeId).Open<MCNConnectorEditableConfig>();
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
                MCNConnectorEditableConfig restored = reopened.ForScope("module", scopeId)
                    .Open<MCNConnectorEditableConfig>().GetLatestSaved();
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
            PVCComputeEditableConfig compute = new PVCComputeEditableConfig { MaxConcurrency = 3 };
            Check(compute.ToOptions().MaxConcurrency == 3, "Compute options");
            Check(FCMConfigValidationEngine.Validate(compute, "test").IsValid, "Compute schema");
            compute.MaxConcurrency = 0;
            Check(!FCMConfigValidationEngine.Validate(compute, "test").IsValid, "Compute Range");

            PSMSimulationEditableConfig simulation = new PSMSimulationEditableConfig();
            Check(simulation.ToDefinition().FixedStep == TimeSpan.FromMilliseconds(10), "Simulation definition");
            Check(FCMConfigValidationEngine.Validate(simulation, "test").IsValid, "Simulation schema");

            MLCServiceEditableConfig serviceConfig = new MLCServiceEditableConfig();
            Check(serviceConfig.ToOptions().MaxConcurrentRequests == 1, "Service options");
            Check(FCMConfigValidationEngine.Validate(serviceConfig, "test").IsValid, "Service schema");

            Console.WriteLine("MODULE.CONFIG: annotated schema/editor metadata + MHS persisted roundtrip PASS");
        }
    }
}
