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

            // 每种专属 Channel 配置必须保留具体 CLR 类型且往返不混入别种通道参数。
            foreach (MCCChannelKind kind in new[] { MCCChannelKind.Virtual, MCCChannelKind.TcpClient,
                MCCChannelKind.TcpServer, MCCChannelKind.Udp, MCCChannelKind.UdpServer, MCCChannelKind.Serial })
            {
                MCCChannelConfiguration selected = MCCChannelConfigurations.Create(kind);
                selected.Id = "typed-" + kind;
                string stored = FCMConfigValueCodec.Serialize(selected);
                MCCChannelConfiguration restoredChannel = (MCCChannelConfiguration)
                    FCMConfigValueCodec.Deserialize(stored, typeof(MCCChannelConfiguration));
                Check(restoredChannel.Kind == kind && restoredChannel.GetType() == selected.GetType() &&
                      restoredChannel.Id == selected.Id,
                    "Specialized Channel roundtrip: " + kind);
            }
            // 历史平铺配置可迁移有效字段；不能悄悄吃掉未知/冲突字段。
            string legacy = "{\"Id\":\"oldtcp\",\"Kind\":\"TcpClient\",\"Host\":\"10.0.0.2\",\"Port\":1502,\"PortName\":\"COM1\"}";
            MCCChannelConfiguration migrated = (MCCChannelConfiguration)
                FCMConfigValueCodec.Deserialize(legacy, typeof(MCCChannelConfiguration));
            MCCTcpClientChannelConfiguration migratedTcp = migrated as MCCTcpClientChannelConfiguration;
            Check(migratedTcp != null && migratedTcp.Host == "10.0.0.2" &&
                  migratedTcp.Port == 1502 && migratedTcp.Id == "oldtcp",
                "Legacy flattened TCP migration");
            bool legacyConflictRejected = false;
            try
            {
                FCMConfigValueCodec.Deserialize(
                    "{\"Id\":\"old\",\"Kind\":\"TcpClient\",\"PortName\":\"COM9\"}",
                    typeof(MCCChannelConfiguration));
            }
            catch (FormatException) { legacyConflictRejected = true; }
            Check(legacyConflictRejected, "Reject conflicting old Channel settings");

            MCNConnectorConfiguration connector = new MCNConnectorConfiguration {
                DisplayName = "PLC 接入",
                Channel = new MCCTcpClientChannelConfiguration { Id = "tcp01", Kind = MCCChannelKind.TcpClient,
                    Host = "127.0.0.1", Port = 502 },
                Protocol = new MPAProtocolConfiguration {
                    Id = "mod01", Kind = MPAProtocolKind.ModbusTcpDraft,
                    Points = new List<MPAPointDefinition> {
                        new MPAPointDefinition { Name = "T", Address = "holding:100",
                            StationAddress = "1", ValueType = "UInt16", CanWrite = true }
                    },
                    Mappings = new List<MPADataMapping> {
                        new MPADataMapping { PointRef = "T", TargetKind = MPADataTargetKind.Variable,
                            TargetRef = "plc/T", Operation = MPADataOperation.WriteValue }
                    }
                }
            };
            Check(FCMConfigValidationEngine.Validate(connector, "test").IsValid,
                "Single Channel/Protocol annotated model valid");
            Check(connector.Channel.DescribeType().IO == MCCIOCapabilities.ByteStream &&
                  connector.Protocol.Points.Count == 1, "Single configuration source");
            connector.Channel = null;
            CheckIssue(connector, "CHANNEL_REQUIRED");
            connector.Channel = new MCCTcpClientChannelConfiguration { Id = "tcp01", Kind = MCCChannelKind.TcpClient };
            connector.Protocol.Kind = MPAProtocolKind.ModbusRtuDraft;
            CheckIssue(connector, "PROTOCOL_IO_MISMATCH");
            connector.Protocol.Kind = MPAProtocolKind.ModbusTcpDraft;
            connector.Protocol.Points[0].StationAddress = "256";
            CheckIssue(connector, "PROTOCOL_INVALID");
            connector.Protocol.Points[0].StationAddress = "1";
            connector.Protocol.Mappings[0].PointRef = "missing";
            CheckIssue(connector, "PROTOCOL_INVALID");
            connector.Protocol.Mappings[0].PointRef = "T";
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
                    model.Channel = connector.Channel;
                    model.Protocol = connector.Protocol;
                }, "首次模块配置", CancellationToken.None).GetAwaiter().GetResult();
                Check(saved.Success && module.GetLatestSaved().Channel.Id == "tcp01",
                    "MHS persisted annotated module options");
                // 同一正式编辑器：通过具体运行类型修改专有字段，类型变更作为一个原子 Undo 步骤。
                using (MHSConfigEditorModel<MCNConnectorConfiguration> editor =
                    new MHSConfigEditorModel<MCNConnectorConfiguration>(module, "通道专有配置编辑"))
                {
                    editor.SetPathJson("Channel.Port", "1502");
                    Check(((MCCTcpClientChannelConfiguration)editor.Draft.Channel).Port == 1502,
                        "TCP 专属 Port 路径编辑");
                    MCCChannelConfiguration serial =
                        (MCCChannelConfiguration)editor.Draft.Channel.SelectVariant("Kind", "Serial");
                    editor.SetPathJson("Channel", FCMJsonValueCodec.Serialize(serial));
                    Check(editor.Draft.Channel is MCCSerialChannelConfiguration &&
                          editor.Draft.Channel.Id == "tcp01", "切换到串口保留公共身份");
                    editor.SetPathJson("Channel.PortName", "\"COM7\"");
                    Check(((MCCSerialChannelConfiguration)editor.Draft.Channel).PortName == "COM7",
                        "串口专属字段路径编辑");
                    editor.Undo();
                    Check(((MCCSerialChannelConfiguration)editor.Draft.Channel).PortName == "COM1",
                        "串口字段 Undo");
                    editor.Undo();
                    Check(editor.Draft.Channel is MCCTcpClientChannelConfiguration &&
                          ((MCCTcpClientChannelConfiguration)editor.Draft.Channel).Port == 1502,
                        "通道子类型切换 Undo");
                    editor.Redo();
                    Check(editor.Draft.Channel is MCCSerialChannelConfiguration, "通道子类型切换 Redo");
                }
                // 从新 MHS 服务重新打开真实磁盘修订，确保没有额外 ModuleConfigStore。
                MHSConfigService reopened = new MHSConfigService(root);
                MCNConnectorConfiguration restored = reopened.ForScope("module", scopeId)
                    .Open<MCNConnectorConfiguration>().GetLatestSaved();
                Check(restored.Channel.Id == "tcp01" && restored.Protocol.Points.Count == 1 &&
                    restored.Protocol.Mappings.Count == 1 && restored.Protocol.Mappings[0].TargetRef == "plc/T",
                    "Module configuration disk roundtrip");
                Check(reopened.ForScope("module", scopeId).Open<MFMModuleIdentityConfig>()
                    .GetLatestSaved().Kind == MFMModuleKind.Connector, "Identity roundtrip");

                string driverScope = scopeId + ".camera";
                MHSConfigSection<MFMModuleIdentityConfig> driverIdentity =
                    service.ForScope("module", driverScope).Open<MFMModuleIdentityConfig>();
                Check(driverIdentity.UpdateAsync(
                    x => x.Kind = MFMModuleKind.Driver, "登记设备驱动分类", CancellationToken.None)
                    .GetAwaiter().GetResult().Success, "Driver identity persisted");
                MHSConfigSection<MFMDriverModuleConfiguration> driverSection =
                    service.ForScope("module", driverScope).Open<MFMDriverModuleConfiguration>();
                Check(driverSection.UpdateAsync(x => {
                    x.DisplayName = "探测器 SDK";
                    x.DriverTypeId = "detector.sdk";
                    x.Backend = MFMDriverBackendKind.Sdk;
                    x.DeviceRef = "device.detector.01";
                }, "保存驱动配置", CancellationToken.None).GetAwaiter().GetResult().Success,
                    "Driver FCM/MHS persistence");
                MHSConfigService driverReopened = new MHSConfigService(root);
                MFMDriverModuleConfiguration persistedDriver =
                    driverReopened.ForScope("module", driverScope)
                        .Open<MFMDriverModuleConfiguration>().GetLatestSaved();
                Check(persistedDriver.DriverTypeId == "detector.sdk" &&
                      persistedDriver.Backend == MFMDriverBackendKind.Sdk &&
                      persistedDriver.InternalResourceRefs.Count == 0 &&
                      driverReopened.ForScope("module", driverScope).Open<MFMModuleIdentityConfig>()
                        .GetLatestSaved().Kind == MFMModuleKind.Driver,
                    "Driver configuration and identity disk roundtrip without Channel");

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
