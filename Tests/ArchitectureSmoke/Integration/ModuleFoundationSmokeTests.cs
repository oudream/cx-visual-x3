using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Mechanism.Core.Modules;
using CxVisual.Mechanism.Connectivity;
using CxVisual.Mechanism.Data.Variables;
using CxVisual.Mechanism.Data;
using CxVisual.Mechanism.Data.RuntimeData;
using CxVisual.Mechanism.Lifecycle.Modules;
using CxVisual.Professional.Vision.Modules;
using CxVisual.Professional.Simulation.Modules;

namespace CxVisual.Tests.ArchitectureSmoke
{
    // 真实构造/校验/状态测试；不以架构骨架冒充 TCP/PLC 实机测试。
    internal static class ModuleFoundationSmokeTests
    {
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("FM.FOUNDATION: " + message);
        }
        private static void Reject<TException>(Action action) where TException : Exception
        {
            bool raised = false;
            try { action(); }
            catch (TException) { raised = true; }
            Check(raised, "Expected exception: " + typeof(TException).Name);
        }
        private sealed class Increment : IMFMComputeOperation<int, int>
        {
            public int Execute(int x) { return x + 1; }
        }
        private sealed class CounterModel : IPSMSimulationModel
        {
            public string ModelId { get { return "axis"; } }
            public int Count { get; private set; }
            public void Initialize(int seed) { Count = seed; }
            public void Advance(TimeSpan delta) { Count++; }
            public void Reset() { Count = 0; }
        }
        private sealed class SimpleService : MLCServiceModuleBase
        {
            public int Starts { get; private set; }
            public int Stops { get; private set; }
            public SimpleService(MFMModuleDescriptor descriptor) : base(descriptor) { }
            protected override Task OnStartAsync(CancellationToken ct) { Starts++; return Task.CompletedTask; }
            protected override Task OnStopAsync(CancellationToken ct) { Stops++; return Task.CompletedTask; }
        }

        public static void Run()
        {
            MFMModuleComposition module = new MFMModuleComposition("PLC01", new[]
            {
                new MFMComponentReference(MFMComponentRole.Channel, "TCP01", true),
                new MFMComponentReference(MFMComponentRole.Protocol, "MOD01")
            });
            Check(module.References.Count == 2 && module.Find(MFMComponentRole.Channel).Count == 1,
                "Module component references");
            Reject<ArgumentException>(() => new MFMModuleComposition("PLC01",
                new[] { new MFMComponentReference(MFMComponentRole.Channel, "X"),
                    new MFMComponentReference(MFMComponentRole.Channel, "X") }));

            MCCChannelConfiguration channel = new MCCChannelConfiguration {
                Id = "tcp01", Kind = MCCChannelKind.TcpClient, Host = "127.0.0.1", Port = 502
            };
            channel.Validate();
            MCCChannelTypeDescriptor tcp = channel.DescribeType();
            Check(tcp.IO == MCCIOCapabilities.ByteStream &&
                  tcp.Topology == MCCChannelTopology.SinglePeer, "Channel metadata");
            Reject<ArgumentException>(() => new MCCChannelConfiguration {
                Id = "bad", Kind = MCCChannelKind.TcpClient, Port = 0 }.Validate());
            Reject<ArgumentException>(() => new MCCChannelConfiguration {
                QueueCapacity = 0 }.Validate());

            MCCChannelTypeDescriptor tcpServer = new MCCChannelConfiguration
                { Kind = MCCChannelKind.TcpServer, Port = 9000 }.DescribeType();
            MCCChannelTypeDescriptor udpServer = new MCCChannelConfiguration
                { Kind = MCCChannelKind.UdpServer, Port = 9001 }.DescribeType();
            Check(tcpServer.Topology == MCCChannelTopology.MultiPeer &&
                  (tcpServer.IO & MCCIOCapabilities.Listener) != 0 &&
                  udpServer.Topology == MCCChannelTopology.MultiPeer &&
                  udpServer.IO == MCCIOCapabilities.Datagram,
                "TCP/UDP Server manage multiple peers inside one parent Channel");
            MCCChannelTypeDescriptor serial = new MCCChannelConfiguration
                { Kind = MCCChannelKind.Serial, PortName = "COM5" }.DescribeType();
            Check(serial.Topology == MCCChannelTopology.SharedBus,
                "RS-485 shares a Serial Channel, stations belong to Protocol points");

            MCCSubChannelDirectory directory = new MCCSubChannelDirectory("server01");
            MCCSubChannelAddress first = directory.Register("peer01", "127.0.0.1:9000").Address;
            Check(directory.FindSubChannel(first) != null && directory.SnapshotSubChannels().Length == 1,
                "SubChannel registration");
            Reject<InvalidOperationException>(() => directory.Register("peer01", "duplicate"));
            Check(directory.Remove(first), "SubChannel removal");
            MCCSubChannelAddress next = directory.Register("peer01", "127.0.0.1:9001").Address;
            Check(next.Generation > first.Generation && directory.FindSubChannel(first) == null &&
                !directory.Remove(first) && directory.FindSubChannel(next) != null, "Stale generation must not route");
            byte[] input = { 1, 2, 3 };
            MCCChannelReceiveContext received = new MCCChannelReceiveContext(next, input);
            input[0] = 99;
            Check(received.Data[0] == 1, "Receive context input copy");
            byte[] output = received.Data; output[0] = 8;
            Check(received.Data[0] == 1, "Receive context output copy");

            MPAProtocolDescriptor modbus = new MPAProtocolDescriptor("modbus.tcp", "Modbus TCP", 1,
                MPAProtocolCapabilities.Interpret | MPAProtocolCapabilities.Read |
                MPAProtocolCapabilities.Write | MPAProtocolCapabilities.RequestResponse,
                MCCIOCapabilities.ByteStream);
            MPAProtocolConfiguration binding = new MPAProtocolConfiguration("mod01", "modbus.tcp");
            binding.ValidateCompatibility(modbus, tcp);
            Reject<ArgumentException>(() => binding.ValidateCompatibility(modbus, null));
            Reject<ArgumentException>(() => new MPAProtocolConfiguration {
                Id = "bad", Kind = MPAProtocolKind.ModbusTcpDraft,
                ProcessingMode = MPAProtocolProcessingMode.Serial,
                MaxConcurrency = 2 }.Validate());
            MPAProtocolSession session = new MPAProtocolSession(binding, new MCCSubChannelAddress("tcp01"));
            Check(session.State == MPAProtocolSessionState.Created, "Session created");
            session.Start(); Check(session.State == MPAProtocolSessionState.Active, "Session start");
            Reject<InvalidOperationException>(() => session.Start());
            Task<object> pendingResponse = session.WaitResponseAsync("modbus-request-01",
                TimeSpan.FromSeconds(1), CancellationToken.None);
            Check(session.PendingRequests == 1 &&
                session.CompleteResponse("modbus-request-01", new byte[] { 3, 2, 0, 5 }) &&
                ((byte[])pendingResponse.GetAwaiter().GetResult()).SequenceEqual(new byte[] { 3, 2, 0, 5 }) &&
                session.PendingRequests == 0, "规约会话 CorrelationId 关联并返回完整响应");
            Check(!session.CompleteResponse("unknown", null), "未知请求不能伪造成功");
            Task<object> timeoutResponse = session.WaitResponseAsync("short-timeout",
                TimeSpan.FromMilliseconds(1), CancellationToken.None);
            Reject<TimeoutException>(() => timeoutResponse.GetAwaiter().GetResult());
            using (CancellationTokenSource cancelled = new CancellationTokenSource())
            {
                Task<object> cancelledResponse = session.WaitResponseAsync("cancel-request",
                    TimeSpan.FromSeconds(10), cancelled.Token);
                cancelled.Cancel();
                Reject<OperationCanceledException>(() => cancelledResponse.GetAwaiter().GetResult());
            }
            session.Stop(); session.Dispose();
            Reject<ObjectDisposedException>(() => session.Start());
            Reject<ArgumentException>(() => new MPAProtocolOperationResult(MPAProtocolOperationStage.Failed));
            Check(!new MPAProtocolOperationResult(MPAProtocolOperationStage.Sent).IsCompleted,
                "Sent must not be treated as completed");

            MPAPointTable pointTable = new MPAPointTable();
            MPAPointDefinition point = new MPAPointDefinition("temperature", "holding:100", typeof(ushort))
                { CanWrite = true, StationAddress = "1" };
            pointTable.Add(point);
            Reject<ArgumentException>(() => pointTable.Add(
                new MPAPointDefinition("temperature", "holding:200", typeof(ushort))));
            MCNConnectorConfiguration connector = new MCNConnectorConfiguration {
                DisplayName = "PLC", Channel = channel,
                Protocol = new MPAProtocolConfiguration("mod01", "modbus.tcp") {
                    Points = new List<MPAPointDefinition> { point }
                }
            };
            connector.Validate();
            Check(connector.Channel == channel && connector.Protocol.Points.Count == 1,
                "Connector has one Channel and one Protocol");
            connector.ValidateProtocolCapabilities(new Dictionary<string, MPAProtocolDescriptor>
                { { "modbus.tcp", modbus } });
            Reject<ArgumentException>(() => new MCNConnectorConfiguration { Channel = null }.Validate());
            Reject<ArgumentException>(() => new MCNConnectorConfiguration { Protocol = null }.Validate());
            MCNConnectorConfiguration empty = new MCNConnectorConfiguration();
            empty.ValidateProtocolCapabilities(new Dictionary<string, MPAProtocolDescriptor>
                { { "empty", new MPAEmptyProtocol().Descriptor } });

            MPAPointDefinition stationOne = new MPAPointDefinition("reg1", "holding:1", typeof(ushort))
                { StationAddress = "1" };
            MPAPointDefinition stationTwo = new MPAPointDefinition("reg2", "holding:1", typeof(ushort))
                { StationAddress = "2" };
            MCNConnectorConfiguration multiStation = new MCNConnectorConfiguration {
                Channel = new MCCChannelConfiguration { Id = "bus", Kind = MCCChannelKind.Serial, PortName = "COM3" },
                Protocol = new MPAProtocolConfiguration("rtu", "modbus.rtu") {
                    Points = new List<MPAPointDefinition> { stationOne, stationTwo }
                }
            };
            multiStation.Validate();
            Check(multiStation.Protocol.Points.Count == 2 &&
                  multiStation.Protocol.Points[0].StationAddress != multiStation.Protocol.Points[1].StationAddress,
                  "Multiple Modbus station addresses belong to a single Protocol PointTable");
            stationTwo.StationAddress = "256";
            Reject<ArgumentException>(() => multiStation.Validate());
            stationTwo.StationAddress = "2";

            // 实际纯传输 Virtual 不解释长度帧：由独立 Protocol 解析拆分数据。
            MCCVirtualChannel[] raw = MCCVirtualChannel.CreatePair("raw-tx", "raw-rx");
            MPALengthFrameProtocol framed = new MPALengthFrameProtocol();
            List<byte[]> parsed = new List<byte[]>();
            raw[1].Received += receivedChunk => {
                foreach (byte[] payload in framed.ProcessReceived(receivedChunk,
                    new MPAProtocolContext("length", "session")))
                    parsed.Add(payload);
            };
            raw[0].Open(); raw[1].Open();
            byte[] encoded = framed.PrepareSend(new byte[] { 10, 20, 30 }, null);
            raw[0].SendAsync(encoded.Take(3).ToArray(),
                new MCCSubChannelAddress("raw-tx"), CancellationToken.None).GetAwaiter().GetResult();
            raw[0].SendAsync(encoded.Skip(3).ToArray(),
                new MCCSubChannelAddress("raw-tx"), CancellationToken.None).GetAwaiter().GetResult();
            Check(parsed.Count == 1 && parsed[0].SequenceEqual(new byte[] { 10, 20, 30 }) &&
                  raw[1].ReadFrames().All(x => x.DecodedPayload == null),
                "Protocol owns frame parsing; Channel only transports raw chunks");
            raw[0].Close(); raw[1].Close();

            // Connector 实例与纯传输 Channel / Protocol 的实际入站闭环。
            MCNConnectorConfiguration streamConfig = new MCNConnectorConfiguration {
                DisplayName = "分帧连接",
                Channel = new MCCChannelConfiguration { Id = "raw-tx", Kind = MCCChannelKind.Virtual },
                Protocol = new MPAProtocolConfiguration { Kind = MPAProtocolKind.LengthFrame }
            };
            MCNConnectorModule streamModule = new MCNConnectorModule(
                streamConfig.Describe("stream", MFMModuleKind.Connector), streamConfig,
                raw[0], new MPALengthFrameProtocol());
            List<byte[]> assembled = new List<byte[]>();
            streamModule.PayloadReceived += x => assembled.Add(x);
            raw[1].Open();
            streamModule.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
            byte[] dataFrame = new MPALengthFrameProtocol().PrepareSend(new byte[] { 7, 8 }, null);
            raw[1].SendAsync(dataFrame, new MCCSubChannelAddress("raw-rx"),
                CancellationToken.None).GetAwaiter().GetResult();
            Check(assembled.Count == 1 && assembled[0].SequenceEqual(new byte[] { 7, 8 }),
                "Connector receives and delegates parsing to Protocol");
            streamModule.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
            raw[1].Close();

            MCCVirtualChannel[] emptyPair = MCCVirtualChannel.CreatePair("empty-tx", "empty-rx");
            MCNConnectorConfiguration emptyCfg = new MCNConnectorConfiguration {
                Channel = new MCCChannelConfiguration { Id = "empty-tx", Kind = MCCChannelKind.Virtual },
                Protocol = new MPAProtocolConfiguration { Kind = MPAProtocolKind.Empty }
            };
            MCNConnectorModule emptyModule = new MCNConnectorModule(
                emptyCfg.Describe("pass", MFMModuleKind.Connector), emptyCfg,
                emptyPair[0], new MPAEmptyProtocol());
            byte[] passthrough = null;
            emptyModule.PayloadReceived += x => passthrough = x;
            emptyPair[1].Open();
            emptyModule.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
            emptyPair[1].SendAsync(new byte[] { 3, 4, 5 }, new MCCSubChannelAddress("empty-rx"),
                CancellationToken.None).GetAwaiter().GetResult();
            Check(passthrough != null && passthrough.SequenceEqual(new byte[] { 3, 4, 5 }),
                "Empty Protocol transparently passes data, not an Empty Channel");
            emptyModule.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
            emptyPair[1].Close();
            // Modbus TCP/RTU 的实际帧校验：TCP 半包，RTU 站号和 CRC16。
            MPAProtocolContext mbCtx = new MPAProtocolContext("modbus", "session",
                new MCCSubChannelAddress("raw-rx"), protocolAddress: new MPAProtocolAddress("3"));
            MPAModbusTcpProtocol mbTcp = new MPAModbusTcpProtocol();
            byte[] tcpResponse = mbTcp.PrepareSend(new byte[] { 3, 2, 0, 5 }, mbCtx);
            byte[] headHalf = tcpResponse.Take(5).ToArray();
            byte[] tailHalf = tcpResponse.Skip(5).ToArray();
            Check(mbTcp.ProcessReceived(new MCCChannelReceiveContext(
                new MCCSubChannelAddress("raw-rx"), headHalf), mbCtx).Count == 0,
                "Modbus TCP 半包暂不输出");
            IReadOnlyList<byte[]> mbTcpFrames = mbTcp.ProcessReceived(
                new MCCChannelReceiveContext(new MCCSubChannelAddress("raw-rx"), tailHalf), mbCtx);
            Check(mbTcpFrames.Count == 1 && mbTcpFrames[0].SequenceEqual(new byte[] { 3, 2, 0, 5 }),
                "Modbus TCP MBAP 粘包解析与读寄存器响应");
            MPAModbusRtuProtocol mbRtu = new MPAModbusRtuProtocol();
            byte[] rtuResponse = mbRtu.PrepareSend(new byte[] { 3, 2, 0, 5 }, mbCtx);
            IReadOnlyList<byte[]> mbRtuFrames = mbRtu.ProcessReceived(
                new MCCChannelReceiveContext(new MCCSubChannelAddress("raw-rx"), rtuResponse), mbCtx);
            Check(mbRtuFrames.Count == 1 && mbRtuFrames[0].SequenceEqual(new byte[] { 3, 2, 0, 5 }),
                "Modbus RTU CRC16/站号/帧处理");
            byte[] badCrc = (byte[])rtuResponse.Clone(); badCrc[badCrc.Length - 1] ^= 1;
            Reject<System.IO.InvalidDataException>(() => mbRtu.ProcessReceived(
                new MCCChannelReceiveContext(new MCCSubChannelAddress("raw-rx"), badCrc), mbCtx));
            MPAProtocolConfiguration collectionConfig = new MPAProtocolConfiguration();
            collectionConfig.Points.Add(new MPAPointDefinition("sensor", "raw:1", typeof(int)));
            MPADataMappingCollection collection = new MPADataMappingCollection(collectionConfig);
            MPADataMapping mapped = new MPADataMapping {
                PointRef = "sensor", TargetRef = "scope/reading",
                TargetKind = MPADataTargetKind.Variable, Operation = MPADataOperation.WriteValue,
                Transform = MPADataTransformKind.Int32
            };
            collection.Add(mapped);
            Check(collection.Find("sensor").Count == 1 &&
                (int)MPADataValueTransform.Apply("42", MPADataTransformKind.Int32) == 42,
                "Mapping 集合与实际转换语义");
            Reject<ArgumentException>(() => collection.Add(new MPADataMapping {
                PointRef = "not-found", TargetRef = "scope/missing" }));
            Check(collection.Remove(mapped), "Mapping 删除与唯一正式配置");

            MDSVariableRegistry registry = new MDSVariableRegistry();
            MDSVariableTable variables = new MDSVariableTable("plc01");
            registry.Register(variables);
            variables.Define(new MDSVariableDefinition("temperature", typeof(double), "C"));
            Check(registry.Find("plc01") == variables, "MDS scope ownership");
            double emptyValue;
            Check(!variables.TryGet<double>("temperature", out emptyValue), "Uninitialized variables must not be successful");
            int changed = 0; variables.VariableChanged += name => { if (name == "temperature") changed++; };
            variables.Update<double>("temperature", 25.5);
            Check(changed == 1 && variables.Get<double>("temperature") == 25.5 &&
                variables.Sample<double>("temperature").HasInitialValue, "MDS update/sample/notification");
            Reject<InvalidCastException>(() => variables.Update<int>("temperature", 25));
            Reject<KeyNotFoundException>(() => variables.Update<int>("missing", 1));
            variables.Define(new MDSVariableDefinition("bytes", typeof(byte[])));
            byte[] mutable = { 5, 6 };
            variables.Update<byte[]>("bytes", mutable);
            mutable[0] = 99;
            byte[] copy = variables.Get<byte[]>("bytes"); copy[1] = 88;
            Check(variables.Get<byte[]>("bytes")[0] == 5 && variables.Get<byte[]>("bytes")[1] == 6,
                "Byte array variable isolation");
            Reject<ArgumentException>(() => registry.Register(variables));

            MPAProtocolConfiguration mapProtocol = new MPAProtocolConfiguration {
                Kind = MPAProtocolKind.Empty,
                Points = new List<MPAPointDefinition> {
                    new MPAPointDefinition("speed", "speed", typeof(double)) { CanWrite = true }
                },
                Mappings = new List<MPADataMapping> {
                    new MPADataMapping { PointRef = "speed", TargetKind = MPADataTargetKind.Variable,
                        TargetRef = "plc01/temperature", Operation = MPADataOperation.WriteValue,
                        Direction = MPADataDirection.Inbound },
                    new MPADataMapping { PointRef = "speed", TargetKind = MPADataTargetKind.Variable,
                        TargetRef = "plc01/temperature", Operation = MPADataOperation.ReadValue,
                        Direction = MPADataDirection.Outbound }
                }
            };
            MPADataMappingExecutor mapping = new MPADataMappingExecutor(
                new MDSProtocolDataTargetAccess(registry));
            mapping.ApplyInboundAsync(mapProtocol, "speed", 55.0, CancellationToken.None)
                .GetAwaiter().GetResult();
            Check(variables.Get<double>("temperature") == 55.0, "Point mapped to real MDS variable");
            MPAProtocolContext mappingContext = new MPAProtocolContext("mapping", "m-session",
                configuration: mapProtocol, dataAccess: new MDSProtocolDataTargetAccess(registry));
            MPAEmptyProtocol emptyProtocol = new MPAEmptyProtocol();
            MPAProtocolOperationResult mappedRead = emptyProtocol.ReadAsync(
                new MPAProtocolOperationRequest(MPAProtocolOperationKind.Read,
                    new MPAProtocolAddress("speed")), mappingContext, CancellationToken.None)
                .GetAwaiter().GetResult();
            Check(mappedRead.IsCompleted && (double)mappedRead.Value == 55.0,
                "IMPAProtocol.ReadAsync 读取真实 MDS 数据");
            MPAProtocolOperationResult mappedWrite = emptyProtocol.WriteAsync(
                new MPAProtocolOperationRequest(MPAProtocolOperationKind.Write,
                    new MPAProtocolAddress("speed"), 57.0),
                mappingContext, CancellationToken.None).GetAwaiter().GetResult();
            Check(mappedWrite.IsCompleted && variables.Get<double>("temperature") == 57.0,
                "IMPAProtocol.WriteAsync 写入真实 MDS 数据");
            object outbound = mapping.ReadOutboundAsync(mapProtocol, "speed", CancellationToken.None)
                .GetAwaiter().GetResult();
            Check((double)outbound == 57.0, "Outbound reads actual MDS variable");
            mapProtocol.Mappings.Add(new MPADataMapping {
                PointRef = "speed", TargetKind = MPADataTargetKind.Message,
                TargetRef = "Alarm", Operation = MPADataOperation.PublishMessage
            });
            Reject<NotSupportedException>(() => mapping.ApplyInboundAsync(mapProtocol, "speed", 56.0,
                CancellationToken.None).GetAwaiter().GetResult());

            using (MDSDataHeap heap = new MDSDataHeap())
            {
                MPAProtocolConfiguration heapProtocol = new MPAProtocolConfiguration {
                    Points = new List<MPAPointDefinition> { new MPAPointDefinition {
                        Name = "heapValue", Address = "heap:1", CanWrite = true
                    }},
                    Mappings = new List<MPADataMapping> {
                        new MPADataMapping { PointRef = "heapValue", TargetKind = MPADataTargetKind.DataHeap,
                            TargetRef = "Run.Quality", Operation = MPADataOperation.WriteValue },
                        new MPADataMapping { PointRef = "heapValue", TargetKind = MPADataTargetKind.DataHeap,
                            TargetRef = "Run.Quality", Direction = MPADataDirection.Outbound,
                            Operation = MPADataOperation.ReadValue }
                    }
                };
                MPADataMappingExecutor heapMap = new MPADataMappingExecutor(
                    new MDSProtocolDataTargetAccess(heap: heap));
                heapMap.ApplyInboundAsync(heapProtocol, "heapValue", 42, CancellationToken.None)
                    .GetAwaiter().GetResult();
                Check(heap.Get<int>("Run.Quality") == 42 &&
                    (int)heapMap.ReadOutboundAsync(heapProtocol, "heapValue", CancellationToken.None)
                       .GetAwaiter().GetResult() == 42, "MDS Heap mapping actual read/write");
            }
            using (CxVisual.Mechanism.Data.Execution.MDEExecutionDataScope scope =
                CxVisual.Mechanism.Data.Execution.MDEExecutionDataScope.CreateRoot("mapping-root"))
            {
                MDSDataContext frameContext = new MDSDataContext(scope);
                MPAProtocolConfiguration stackProtocol = new MPAProtocolConfiguration {
                    Points = new List<MPAPointDefinition> { new MPAPointDefinition {
                        Name = "stackValue", Address = "stack:1", CanWrite = true
                    }},
                    Mappings = new List<MPADataMapping> {
                        new MPADataMapping { PointRef = "stackValue", TargetKind = MPADataTargetKind.DataStack,
                            TargetRef = "Node.Speed", Operation = MPADataOperation.WriteValue },
                        new MPADataMapping { PointRef = "stackValue", TargetKind = MPADataTargetKind.DataStack,
                            TargetRef = "Node.Speed", Direction = MPADataDirection.Outbound,
                            Operation = MPADataOperation.ReadValue }
                    }
                };
                MPADataMappingExecutor stackMap = new MPADataMappingExecutor(
                    new MDSProtocolDataTargetAccess(frame: frameContext));
                stackMap.ApplyInboundAsync(stackProtocol, "stackValue", 17, CancellationToken.None)
                    .GetAwaiter().GetResult();
                Check(frameContext.Get<int>("Node.Speed") == 17 &&
                    (int)stackMap.ReadOutboundAsync(stackProtocol, "stackValue", CancellationToken.None)
                       .GetAwaiter().GetResult() == 17, "MDS DataStack frame mapping actual read/write");
            }

            MFMModuleDescriptor driverDescriptor =
                new MFMModuleDescriptor("camera01", "X-Ray Camera", MFMModuleKind.Driver);
            Check(driverDescriptor.Kind == MFMModuleKind.Driver, "Fifth Driver module kind");
            MFMDriverModuleConfiguration sdk = new MFMDriverModuleConfiguration {
                DisplayName = "采图驱动", DriverTypeId = "vision.xray.sdk",
                Backend = MFMDriverBackendKind.Sdk, DeviceRef = "camera.01"
            };
            sdk.Validate();
            Check(sdk.InternalResourceRefs.Count == 0,
                "SDK driver does not require EmptyChannel or a Connector instance");
            Reject<ArgumentException>(() => new MFMDriverModuleConfiguration
                { DriverTypeId = "" }.Validate());
            Reject<ArgumentException>(() => new MFMDriverModuleConfiguration {
                DriverTypeId = "vision.xray", InternalResourceRefs = new List<string> { "tcp", "tcp" }
            }.Validate());

            MFMModuleDescriptor computeDescriptor = new MFMModuleDescriptor("calc", "Calc", MFMModuleKind.Compute);
            PVCComputeModuleProfile compute = new PVCComputeModuleProfile(computeDescriptor);
            Check(compute.Descriptor.Kind == MFMModuleKind.Compute && new Increment().Execute(7) == 8, "Compute");
            Reject<ArgumentException>(() => new PVCComputeModuleConfiguration { MaxConcurrency = 0 }.Validate());

            MFMModuleDescriptor simulationDescriptor = new MFMModuleDescriptor("sim", "Sim", MFMModuleKind.Simulation);
            CounterModel model = new CounterModel();
            PSMSimulationModule simulation = new PSMSimulationModule(simulationDescriptor,
                new PSMSimulationModuleConfiguration { ModelId = "axis", StepMilliseconds = 1, Seed = 17 }, model);
            Reject<InvalidOperationException>(() => simulation.Advance(TimeSpan.FromMilliseconds(1)));
            simulation.Initialize();
            simulation.Advance(TimeSpan.FromMilliseconds(1));
            Check(model.Count == 18 && simulation.SimulatedTime == TimeSpan.FromMilliseconds(1), "Simulation step");
            simulation.Reset();
            Check(simulation.SimulatedTime == TimeSpan.Zero, "Simulation reset");

            MFMModuleDescriptor serviceDescriptor = new MFMModuleDescriptor("svc", "Svc", MFMModuleKind.Service);
            SimpleService service = new SimpleService(serviceDescriptor);
            service.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
            Check(service.IsReady && service.Starts == 1, "Service start");
            service.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
            Check(!service.IsReady && service.Stops == 1 && service.State == MLCServiceModuleState.Stopped,
                "Service stop");
            Console.WriteLine("FM.FOUNDATION: driver/module/channel/protocol/point/variable/compute/sim/service PASS");
        }
    }
}
