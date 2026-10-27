using CxVisual.Foundation.Transport;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Mechanism.Core.Modules;
using CxVisual.Mechanism.Data.Configuration;
using CxVisual.Mechanism.Connectivity;
using CxVisual.Mechanism.Presentation.Wpf;
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
            "五类模块唯一配置Schema的保存、重开、范围隔离",
            TCTTestAspect.Integration, "TestCenter / Manual / Module",
            ObjectId = "MHSConfigService")]
        public static void FiveProductionConfigSectionsRoundTrip(TCTTestContext test)
        {
            string root = Path.Combine(Path.GetTempPath(),
                "TCTModule-" + Guid.NewGuid().ToString("N"));
            // 通用MPW检查全部正式Schema字段，缺页立即可见而非沉默丢失。
            string[] complete = MPWConfigWorkspaceLayout.UnassignedLeafPaths(
                typeof(MFMDriverModuleConfiguration), new[] {
                    new[] { "DisplayName", "Enabled", "CapabilityIds" },
                    new[] { "DriverTypeId", "DeviceRef", "Backend" },
                    new[] { "InternalResourceRefs" }
                });
            test.AssertTrue(complete.Length == 0, "Driver正式Schema分页完整覆盖", true, complete.Length == 0);
            string[] missing = MPWConfigWorkspaceLayout.UnassignedLeafPaths(
                typeof(MFMDriverModuleConfiguration), new[] {
                    new[] { "DisplayName", "Enabled", "CapabilityIds" },
                    new[] { "DriverTypeId", "Backend" },
                    new[] { "InternalResourceRefs" }
                });
            test.AssertTrue(missing.SequenceEqual(new[] { "DeviceRef" }),
                "遗漏的设备引用字段被MPW识别", true, missing.SequenceEqual(new[] { "DeviceRef" }));
            string[] connector = MPWConfigWorkspaceLayout.UnassignedLeafPaths(
                typeof(MCNConnectorConfiguration), new[] {
                    new[] { "DisplayName", "Enabled", "CapabilityIds" },
                    new[] { "Channel", "Protocol" }
                });
            test.AssertTrue(connector.Length == 0,
                "Connector内嵌通道与规约描述无遗漏", true, connector.Length == 0);
            foreach (FTRChannelKind kind in Enum.GetValues(typeof(FTRChannelKind)))
            {
                Type channelType = FTRChannelConfigurations.TypeOf(kind);
                bool correct = typeof(FTRChannelConfiguration).IsAssignableFrom(channelType) &&
                    FCMConfigSchemaBuilder.Members(channelType).Any(member => member.Name == "Kind") &&
                    FCMConfigSchemaBuilder.Members(channelType).Any(member => member.Name == "Id");
                test.AssertTrue(correct, "专有Channel正式配置可自动生成界面: " + kind, true, correct);
            }
            try
            {
                MHSConfigService first = OpenService(root);
                CheckSection<MFMDriverModuleConfiguration>(
                    first, "DriverTest", "Driver", MFMModuleKind.Driver, test);
                CheckSection<MCNConnectorConfiguration>(
                    first, "ConnectorTest", "Connector", MFMModuleKind.Connector, test);
                CheckSection<PVCComputeModuleConfiguration>(
                    first, "ComputeTest", "Compute", MFMModuleKind.Compute, test);
                CheckSection<PSMSimulationModuleConfiguration>(
                    first, "SimulationTest", "Simulation", MFMModuleKind.Simulation, test);
                CheckSection<MLCServiceModuleConfiguration>(
                    first, "ServiceTest", "Service", MFMModuleKind.Service, test);

                MHSConfigService reopened = OpenService(root);
                CheckReopened<MFMDriverModuleConfiguration>(reopened, "DriverTest", "Driver", test);
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
            service.RegisterAssembly(typeof(MFMDriverModuleConfiguration).Assembly);
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
                model => {
                    model.DisplayName = label + " production config";
                    MFMDriverModuleConfiguration driver = model as MFMDriverModuleConfiguration;
                    if (driver != null)
                    {
                        driver.DriverTypeId = "test.detector.sdk";
                        driver.Backend = MFMDriverBackendKind.Sdk;
                        driver.DeviceRef = "test.detector01";
                    }
                },
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
            MFMDriverModuleConfiguration driver = value as MFMDriverModuleConfiguration;
            if (driver != null)
                test.AssertTrue(driver.DriverTypeId == "test.detector.sdk" &&
                    driver.Backend == MFMDriverBackendKind.Sdk &&
                    driver.InternalResourceRefs.Count == 0,
                    "Driver持久化后仍不要求EmptyChannel", true, true);
            test.AssertTrue(value.DisplayName == label + " production config",
                label + "已保存配置重新打开", true, true);
            FCMConfigValidationReport report =
                FCMConfigValidationEngine.Validate(value, "module:" + id);
            test.AssertTrue(report.IsValid, label + "正式FCM校验", true, report.IsValid);
        }

        [TCTTest("TCT.MODULE.ASSEMBLY.001",
            "生产Connector Owner引用装配和Virtual真实收发及拒绝路径",
            TCTTestAspect.Integration, "TestCenter / Manual / Module",
            ObjectId = "MCNConnectorModule")]
        public static void ProductionConnectorVirtualAssembly(TCTTestContext test)
        {
            MCNConnectorConfiguration config = new MCNConnectorConfiguration {
                DisplayName = "Virtual Connector",
                Channel = new FTRVirtualChannelConfiguration { Id = "virtual01", Kind = FTRChannelKind.Virtual },
                Protocol = new MPAProtocolConfiguration { Id = "length01", Kind = MPAProtocolKind.LengthFrame }
            };
            MFMModuleDescriptor identity = config.Describe("ModuleVirtual01", MFMModuleKind.Connector);
            FTRVirtualChannel[] pair = FTRVirtualChannel.CreatePair("virtual01", "virtualPeer");
            MPALengthFrameProtocol protocol = new MPALengthFrameProtocol();
            MCNConnectorModule module = new MCNConnectorModule(identity, config,
                pair[0], protocol);
            test.AssertTrue(module.Descriptor.Id == "ModuleVirtual01" &&
                module.Channel == pair[0] && module.Protocol == protocol &&
                module.Points.Count == 0 && pair[0].State == FTRChannelState.Closed,
                "装配只绑定正式生产实例，不会提前启动或生成点表值", true, true);

            byte[] decoded = null;
            MPALengthFrameProtocol receiverProtocol = new MPALengthFrameProtocol();
            pair[1].Received += received => {
                foreach (byte[] result in receiverProtocol.ProcessReceived(received,
                    new MPAProtocolContext("length", "testcenter", received.Address)))
                    decoded = result;
            };
            try
            {
                pair[1].Open();
                module.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
                module.SendPayloadAsync(new byte[] { 0x11, 0x22, 0x33 },
                    CancellationToken.None).GetAwaiter().GetResult();
                FTRChannelFrame observed = pair[1].ReadFrames()
                    .Single(x => x.Direction == FTRFrameDirection.Rx);
                test.AssertTrue(decoded != null && decoded.SequenceEqual(new byte[] { 0x11, 0x22, 0x33 }) &&
                    observed.DecodedPayload == null &&
                    pair[0].ReadFrames().Any(x => x.Direction == FTRFrameDirection.Tx),
                    "Virtual纯传输、正式Protocol独立解析并产生实际TX/RX观察", true, true);
            }
            finally
            {
                if (module.IsStarted) module.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
                pair[1].Close();
            }

            bool missingRejected = false;
            try
            {
                new MCNConnectorModule(identity, config,
                    null, protocol);
            }
            catch (ArgumentNullException) { missingRejected = true; }
            test.AssertTrue(missingRejected, "缺失Channel Provider必须拒绝装配", true, missingRejected);

            bool physicalRejected = false;
            config.Channel = new FTRTcpClientChannelConfiguration { Id = config.Channel.Id };
            try
            {
                new MCNConnectorModule(identity, config,
                    pair[0], protocol);
            }
            catch (NotSupportedException) { physicalRejected = true; }
            test.AssertTrue(physicalRejected, "非Virtual物理Provider未接线时必须拒绝装配", true, physicalRejected);
        }

        // 仅用于测试IMPADataTargetAccess调用语义，不替代生产MDS、消息或命令Owner。
        private sealed class RecordingDataTarget : IMPADataTargetAccess
        {
            public MPADataTargetKind WrittenKind;
            public string WrittenReference;
            public object WrittenValue;
            public int Reads;
            public Task<object> ReadAsync(MPADataTargetKind kind, string targetRef, CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                Reads++;
                return Task.FromResult((object)42);
            }
            public Task WriteAsync(MPADataTargetKind kind, string targetRef, object value, CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                WrittenKind = kind; WrittenReference = targetRef; WrittenValue = value;
                return Task.CompletedTask;
            }
            public Task PublishAsync(string messageRef, object value, CancellationToken ct)
            { throw new InvalidOperationException("当前用例不允许发布消息"); }
            public Task InvokeAsync(string commandRef, object value, CancellationToken ct)
            { throw new InvalidOperationException("当前用例不允许执行设备命令"); }
        }

        [TCTTest("TCT.MODULE.PROTOCOL.003",
            "正式PointTable与DataMapping的入站/出站语义及非法映射反证",
            TCTTestAspect.Integration, "TestCenter / Manual / Protocol",
            ObjectId = "MPADataMappingExecutor")]
        public static void ProductionPointTableAndMapping(TCTTestContext test)
        {
            MPAProtocolConfiguration protocol = new MPAProtocolConfiguration {
                Id = "modbus-profile",
                Kind = MPAProtocolKind.ModbusRtuDraft,
                Points = new List<MPAPointDefinition> {
                    new MPAPointDefinition {
                        Name = "temperature", Address = "holding:100",
                        StationAddress = "7", ValueType = "UInt16",
                        CanRead = true, CanWrite = true }
                },
                Mappings = new List<MPADataMapping> {
                    new MPADataMapping {
                        PointRef = "temperature", TargetKind = MPADataTargetKind.Variable,
                        TargetRef = "line/temperature", Direction = MPADataDirection.Inbound,
                        Operation = MPADataOperation.WriteValue,
                        Transform = MPADataTransformKind.Int32
                    },
                    new MPADataMapping {
                        PointRef = "temperature", TargetKind = MPADataTargetKind.DataHeap,
                        TargetRef = "line/command-temperature",
                        Direction = MPADataDirection.Outbound,
                        Operation = MPADataOperation.ReadValue
                    }
                }
            };
            protocol.Validate();
            RecordingDataTarget observer = new RecordingDataTarget();
            MPADataMappingExecutor executor = new MPADataMappingExecutor(observer);
            executor.ApplyInboundAsync(protocol, "temperature", (ushort)19, CancellationToken.None)
                .GetAwaiter().GetResult();
            test.AssertTrue(observer.WrittenKind == MPADataTargetKind.Variable &&
                observer.WrittenReference == "line/temperature" &&
                observer.WrittenValue is int && (int)observer.WrittenValue == 19,
                "入站点值按正式Mapping交给外部Variable Owner", true, true);
            object outbound = executor.ReadOutboundAsync(protocol, "temperature", CancellationToken.None)
                .GetAwaiter().GetResult();
            test.AssertTrue(observer.Reads == 1 && outbound is int && (int)outbound == 42,
                "出站点通过正式DataHeap读取接口，不能用固定测试值代替实际Owner调用", true, true);

            protocol.Points[0].StationAddress = "248";
            bool stationRejected = false;
            try { protocol.Validate(); } catch (ArgumentException) { stationRejected = true; }
            test.AssertTrue(stationRejected, "Modbus多站号点表拒绝越界站号", true, stationRejected);
            protocol.Points[0].StationAddress = "7";

            protocol.Mappings[1].Operation = MPADataOperation.WriteValue;
            bool wrongDirection = false;
            try { protocol.Validate(); } catch (ArgumentException) { wrongDirection = true; }
            test.AssertTrue(wrongDirection, "映射出站方向与操作不兼容必须拒绝", true, wrongDirection);
            protocol.Mappings[1].Operation = MPADataOperation.ReadValue;

            protocol.Mappings[1].PointRef = "missing";
            bool missingPoint = false;
            try { protocol.Validate(); } catch (ArgumentException) { missingPoint = true; }
            test.AssertTrue(missingPoint, "映射不得指向不存在的Point", true, missingPoint);
        }

        [TCTTest("TCT.MODULE.CHANNEL.004",
            "Channel Single/Multi/SharedBus正式拓扑与IO能力声明",
            TCTTestAspect.Integration, "TestCenter / Manual / Channel",
            ObjectId = "FTRChannelConfiguration")]
        public static void ChannelTopologies(TCTTestContext test)
        {
            FTRChannelTypeDescriptor single = new FTRTcpClientChannelConfiguration {
                Kind = FTRChannelKind.TcpClient }.DescribeType();
            FTRChannelTypeDescriptor multi = new FTRTcpServerChannelConfiguration {
                Kind = FTRChannelKind.TcpServer }.DescribeType();
            FTRChannelTypeDescriptor udp = new FTRUdpServerChannelConfiguration {
                Kind = FTRChannelKind.UdpServer }.DescribeType();
            FTRChannelTypeDescriptor serial = new FTRSerialChannelConfiguration {
                Kind = FTRChannelKind.Serial }.DescribeType();
            test.AssertTrue(single.Topology == FTRChannelTopology.SinglePeer &&
                (single.IO & FTRIOCapabilities.ByteStream) != 0,
                "TCP Client是单对端ByteStream", true, true);
            test.AssertTrue(multi.Topology == FTRChannelTopology.MultiPeer &&
                (multi.IO & FTRIOCapabilities.Listener) != 0,
                "TCP Server按MultiPeer及Listener能力声明", true, true);
            test.AssertTrue(udp.Topology == FTRChannelTopology.MultiPeer &&
                (udp.IO & FTRIOCapabilities.Datagram) != 0,
                "UDP Server采用多对端Datagram，不冒充TCP字节流", true, true);
            test.AssertTrue(serial.Topology == FTRChannelTopology.SharedBus &&
                (serial.IO & FTRIOCapabilities.ByteStream) != 0,
                "RS-485为共享物理通道，站号应由规约点表管理", true, true);
        }

        [TCTTest("TCT.MODULE.CONFIG.005",
            "正式MPW/MHS集合编辑路径：规约点和映射增删、持久化重开及悬空引用反证",
            TCTTestAspect.Integration, "TestCenter / Manual / Protocol",
            ObjectId = "MHSConfigEditorModel")]
        public static void ProtocolSchemaCollectionsPersist(TCTTestContext test)
        {
            string root = Path.Combine(Path.GetTempPath(), "TCT-PointMapping-" +
                Guid.NewGuid().ToString("N"));
            try
            {
                MHSConfigService service = OpenService(root);
                MHSConfigSection<MCNConnectorConfiguration> section =
                    service.ForScope("module", "PointMappingP0").Open<MCNConnectorConfiguration>();
                using (MHSConfigEditorModel<MCNConnectorConfiguration> editor =
                    new MHSConfigEditorModel<MCNConnectorConfiguration>(section, "TestCenter P0集合操作"))
                {
                    editor.InsertListItem("Protocol.Points", 0,
                        FCMJsonValueCodec.Serialize(new MPAPointDefinition {
                            Name = "temperature", Address = "holding:100",
                            StationAddress = "7", ValueType = "UInt16", CanWrite = true }));
                    editor.InsertListItem("Protocol.Mappings", 0,
                        FCMJsonValueCodec.Serialize(new MPADataMapping {
                            PointRef = "temperature", TargetRef = "line/temperature",
                            TargetKind = MPADataTargetKind.Variable,
                            Direction = MPADataDirection.Inbound,
                            Operation = MPADataOperation.WriteValue }));
                    test.AssertTrue(editor.Draft.Protocol.Points.Count == 1 &&
                        editor.Draft.Protocol.Mappings.Count == 1,
                        "正式Schema路径新增PointTable和DataMapping", true, true);
                    MHSConfigCommitResult committed =
                        editor.CommitAsync(CancellationToken.None).GetAwaiter().GetResult();
                    test.AssertTrue(committed.Success, "正式MHS事务校验并保存", true, committed.Success);
                }
                MHSConfigService reopened = OpenService(root);
                MCNConnectorConfiguration persisted =
                    reopened.ForScope("module", "PointMappingP0")
                        .Open<MCNConnectorConfiguration>().GetLatestSaved();
                test.AssertTrue(persisted.Protocol.Points.Single().Name == "temperature" &&
                    persisted.Protocol.Mappings.Single().PointRef == "temperature",
                    "重开后点表与映射保留同一正式Protocol引用", true, true);

                MHSConfigSection<MCNConnectorConfiguration> saved =
                    reopened.ForScope("module", "PointMappingP0").Open<MCNConnectorConfiguration>();
                using (MHSConfigEditorModel<MCNConnectorConfiguration> editor =
                    new MHSConfigEditorModel<MCNConnectorConfiguration>(saved, "删除与无效路径反证"))
                {
                    editor.RemoveListItem("Protocol.Mappings", 0);
                    editor.RemoveListItem("Protocol.Points", 0);
                    MHSConfigCommitResult removed =
                        editor.CommitAsync(CancellationToken.None).GetAwaiter().GetResult();
                    test.AssertTrue(removed.Success, "正式集合路径删除并保存", true, removed.Success);
                }
                reopened = OpenService(root);
                persisted = reopened.ForScope("module", "PointMappingP0")
                    .Open<MCNConnectorConfiguration>().GetLatestSaved();
                test.AssertTrue(persisted.Protocol.Points.Count == 0 &&
                    persisted.Protocol.Mappings.Count == 0,
                    "删除后重新打开无幽灵点表或映射", true, true);

                using (MHSConfigEditorModel<MCNConnectorConfiguration> editor =
                    new MHSConfigEditorModel<MCNConnectorConfiguration>(
                        reopened.ForScope("module", "PointMappingP0")
                            .Open<MCNConnectorConfiguration>(), "悬空映射反证"))
                {
                    editor.InsertListItem("Protocol.Mappings", 0,
                        FCMJsonValueCodec.Serialize(new MPADataMapping {
                            PointRef = "not-found", TargetRef = "line/temperature" }));
                    MHSConfigCommitResult invalid =
                        editor.CommitAsync(CancellationToken.None).GetAwaiter().GetResult();
                    test.AssertTrue(!invalid.Success,
                        "不存在的PointRef不能持久化为合法配置", true, !invalid.Success);
                }
                reopened = OpenService(root);
                persisted = reopened.ForScope("module", "PointMappingP0")
                    .Open<MCNConnectorConfiguration>().GetLatestSaved();
                test.AssertTrue(persisted.Protocol.Mappings.Count == 0,
                    "被拒绝的提交不得污染最新Saved版本", true, true);
            }
            finally
            {
                try { if (Directory.Exists(root)) Directory.Delete(root, true); }
                catch (IOException) { }
            }
        }

        [TCTTest("TCT.MODULE.CHANNEL.006",
            "生产TCP/UDP Server的MultiPeer子通道代次、重注册和断开反证（不开Socket）",
            TCTTestAspect.Integration, "TestCenter / Manual / Channel",
            ObjectId = "FTRMultiChannelBase")]
        public static void ActualMultiPeerChannelTopology(TCTTestContext test)
        {
            FTRMultiChannelBase[] channels = {
                new FTRTcpServerChannel(new FTRTcpServerChannelConfiguration {
                    Id = "P0-TCP-Server", Kind = FTRChannelKind.TcpServer }),
                new FTRUdpServerChannel(new FTRUdpServerChannelConfiguration {
                    Id = "P0-UDP-Server", Kind = FTRChannelKind.UdpServer })
            };
            foreach (FTRMultiChannelBase channel in channels)
            {
                test.AssertTrue(channel.Topology == FTRChannelTopology.MultiPeer &&
                    channel.State == FTRChannelState.Closed,
                    "实际Server Owner实例处于Closed，未偷开监听Socket：" + channel.Id,
                    true, channel.State == FTRChannelState.Closed);
                FTRSubChannelInfo first = channel.RegisterPeer("client01", "127.0.0.1:13001");
                FTRSubChannelInfo second = channel.RegisterPeer("client02", "127.0.0.1:13002");
                FTRSubChannel left = new FTRSubChannel(channel, first.Address);
                test.AssertTrue(channel.SnapshotSubChannels().Length == 2 &&
                    left.State == FTRSubChannelState.Open,
                    "正式MultiPeer登记两个不同运行对端：" + channel.Id, true, true);
                bool duplicate = false;
                try { channel.RegisterPeer("client01", "127.0.0.1:13001"); }
                catch (InvalidOperationException) { duplicate = true; }
                test.AssertTrue(duplicate, "相同在线对端不得重复登记：" + channel.Id,
                    true, duplicate);
                test.AssertTrue(channel.RemovePeer(first.Address) &&
                    channel.FindSubChannel(first.Address) == null &&
                    left.State == FTRSubChannelState.Closed,
                    "移除对端后原代次即刻失效：" + channel.Id, true, true);
                FTRSubChannelInfo newPeer = channel.RegisterPeer("client01", "127.0.0.1:13003");
                bool oldRejected = false;
                try { new FTRSubChannel(channel, first.Address); }
                catch (ArgumentException) { oldRejected = true; }
                test.AssertTrue(oldRejected &&
                    newPeer.Address.Generation > first.Address.Generation &&
                    channel.FindSubChannel(newPeer.Address) != null,
                    "重连使用新Generation，旧地址不能代替新身份：" + channel.Id,
                    true, oldRejected);
                channel.RemovePeer(newPeer.Address);
                channel.RemovePeer(second.Address);
                test.AssertTrue(channel.SnapshotSubChannels().Length == 0 &&
                    channel.State == FTRChannelState.Closed,
                    "清理全部对端而不启动或关闭真实Socket：" + channel.Id, true, true);
            }
        }

        [TCTTest("TCT.MODULE.CONFIG.002",
            "Connector 缺少 Channel/Protocol 与无效点映射必须拒绝",
            TCTTestAspect.Integration, "TestCenter / Manual / Module",
            ObjectId = "MCNConnectorConfiguration")]
        public static void ConnectorRejection(TCTTestContext test)
        {
            MCNConnectorConfiguration config = new MCNConnectorConfiguration();
            config.Channel = null;
            FCMConfigValidationReport invalid = FCMConfigValidationEngine.Validate(config, "module:invalid");
            bool noChannel = invalid.Issues.Any(x => x.Code == "CHANNEL_REQUIRED");
            test.AssertTrue(!invalid.IsValid && noChannel,
                "必须配置单一 Channel", true, noChannel);
            config.Channel = new FTRVirtualChannelConfiguration();
            config.Protocol.Points.Add(new MPAPointDefinition { Name = "alarm", Address = "alarm" });
            config.Protocol.Mappings.Add(new MPADataMapping {
                PointRef = "missing", TargetRef = "plc/alarm",
                TargetKind = MPADataTargetKind.Variable, Operation = MPADataOperation.WriteValue
            });
            invalid = FCMConfigValidationEngine.Validate(config, "module:invalid");
            bool noPoint = invalid.Issues.Any(x => x.Code == "PROTOCOL_INVALID");
            test.AssertTrue(!invalid.IsValid && noPoint,
                "映射点引用不存在应拒绝", true, noPoint);
        }
    }
}
