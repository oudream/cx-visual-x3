using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Mechanism.Core.Modules;
using CxVisual.Mechanism.Connectivity;
using CxVisual.Mechanism.Data.Variables;
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
            MPAProtocolBinding binding = new MPAProtocolBinding("mod01", "modbus.tcp", "tcp01");
            binding.ValidateCompatibility(modbus, tcp);
            Reject<ArgumentException>(() => new MPAProtocolBinding("bad", "modbus.tcp", null, "peer01"));
            Reject<ArgumentException>(() => new MPAProtocolBinding("bad", "modbus.tcp").
                ValidateCompatibility(modbus, null));
            Reject<ArgumentException>(() => new MPAProtocolBinding {
                Id = "bad", Kind = MPAProtocolKind.ModbusTcpDraft,
                ChannelRef = "tcp01", ProcessingMode = MPAProtocolProcessingMode.Serial,
                MaxConcurrency = 2 }.Validate());
            MPAProtocolSession session = new MPAProtocolSession(binding, new MCCSubChannelAddress("tcp01"));
            Check(session.State == MPAProtocolSessionState.Created, "Session created");
            session.Start(); Check(session.State == MPAProtocolSessionState.Active, "Session start");
            Reject<InvalidOperationException>(() => session.Start());
            session.Stop(); session.Dispose();
            Reject<ObjectDisposedException>(() => session.Start());
            Reject<ArgumentException>(() => new MPAProtocolOperationResult(MPAProtocolOperationStage.Failed));
            Check(!new MPAProtocolOperationResult(MPAProtocolOperationStage.Sent).IsCompleted,
                "Sent must not be treated as completed");

            MCNPointTable table = new MCNPointTable();
            MCNPointDefinition point = new MCNPointDefinition("temperature", "holding:100", typeof(ushort)) {
                ProtocolRef = "mod01", TargetKind = MCNPointTargetKind.Variable, TargetRef = "PLC01.T"
            };
            table.Add(point);
            Reject<ArgumentException>(() => table.Add(
                new MCNPointDefinition("temperature", "holding:200", typeof(ushort))));
            MCNConnectorConfiguration connector = new MCNConnectorConfiguration {
                DisplayName = "PLC", Channels = new List<MCCChannelConfiguration> { channel },
                Protocols = new List<MPAProtocolBinding> { binding },
                Points = new List<MCNPointDefinition> { point }
            };
            connector.Validate();
            connector.ValidateProtocolCapabilities(new Dictionary<string, MPAProtocolDescriptor>
                { { "modbus.tcp", modbus } });
            Reject<ArgumentException>(() => new MCNConnectorConfiguration {
                Protocols = new List<MPAProtocolBinding> { binding } }.Validate());
            Reject<ArgumentException>(() => new MCNConnectorConfiguration {
                Channels = new List<MCCChannelConfiguration> { channel },
                Protocols = new List<MPAProtocolBinding> { binding },
                Points = new List<MCNPointDefinition> {
                    new MCNPointDefinition { Name="missing", ProtocolRef="notfound",
                        Address="holding:1",TargetRef="PLC.X" }
                }}.Validate());
            MCNConnectorConfiguration noChannel = new MCNConnectorConfiguration {
                Protocols = new List<MPAProtocolBinding> {
                    new MPAProtocolBinding("sdk-protocol", "internal")
                }
            };
            noChannel.ValidateProtocolCapabilities(new Dictionary<string, MPAProtocolDescriptor>
                { { "internal", new MPAProtocolDescriptor("internal", "Internal", 1, MPAProtocolCapabilities.Interpret) } });
            Reject<InvalidOperationException>(() => noChannel.ValidateProtocolCapabilities(
                new Dictionary<string, MPAProtocolDescriptor>()));

            MDSVariableRegistry registry = new MDSVariableRegistry();
            MDSVariableTable variables = new MDSVariableTable("plc01");
            registry.Register(variables);
            variables.Define(new MDSVariableDefinition("temperature", typeof(double), "C"));
            Check(registry.Find("plc01") == variables, "MDS scope ownership");
            double empty;
            Check(!variables.TryGet<double>("temperature", out empty), "Uninitialized variables must not be successful");
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
            Console.WriteLine("FM.FOUNDATION: module/channel/protocol/point/variable/compute/sim/service PASS");
        }
    }
}
