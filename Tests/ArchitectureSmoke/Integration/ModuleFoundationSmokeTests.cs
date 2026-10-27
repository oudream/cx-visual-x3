using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Mechanism.Core.Modules;
using CxVisual.Mechanism.Connectivity;
using CxVisual.Mechanism.Data.Variables;
using CxVisual.Mechanism.Data.Messages;
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

        private static void ModbusTcpTransactionSmoke()
        {
            MCCVirtualChannel[] pair = MCCVirtualChannel.CreatePair("mb-request", "mb-device");
            MPAModbusTcpProtocol protocol = new MPAModbusTcpProtocol();
            MPAProtocolConfiguration config = new MPAProtocolConfiguration("mb-request-config", "modbus.tcp");
            config.TimeoutMs = 70;
            MPAProtocolContext context = new MPAProtocolContext(config.Id, "virtual-request",
                new MCCSubChannelAddress("mb-request"), configuration: config, transport: pair[0]);
            int behavior = 0;
            int acceptedResponseCount = 0;
            pair[0].Received += received =>
                acceptedResponseCount += protocol.ProcessReceived(received, context).Count;
            pair[1].Received += received =>
            {
                byte[] frame = received.Data;
                if (behavior == 4) return; // 故意无响应，用于真实超时
                byte[] pdu = behavior == 2 ? new byte[] { 0x83, 0x02 } :
                    (frame[7] == 3 ? new byte[] { 3, 2, 0, 42 } :
                    frame.Skip(7).Take(5).ToArray());
                byte[] reply = new byte[pdu.Length + 7];
                reply[0] = frame[0]; reply[1] = frame[1]; // 保留实际 TransactionId
                reply[5] = (byte)(pdu.Length + 1);
                reply[6] = (byte)(behavior == 3 ? frame[6] + 1 : frame[6]);
                Buffer.BlockCopy(pdu, 0, reply, 7, pdu.Length);
                pair[1].SendAsync(reply, new MCCSubChannelAddress("mb-device"),
                    CancellationToken.None).GetAwaiter().GetResult();
            };
            pair[0].Open(); pair[1].Open();
            try
            {
                MPAProtocolOperationResult read = protocol.RequestAsync(
                    new MPAProtocolOperationRequest(MPAProtocolOperationKind.Request,
                        new MPAProtocolAddress("3"), MPAModbusRegisterOperations.ReadHolding(100, 1)),
                    context, CancellationToken.None).GetAwaiter().GetResult();
                Check(read.IsCompleted && ((byte[])read.Value).SequenceEqual(new byte[] { 3, 2, 0, 42 }),
                    "Modbus TCP 实际发送并关联 TransactionId 的 03 响应");
                behavior = 1;
                MPAProtocolOperationResult write = protocol.RequestAsync(
                    new MPAProtocolOperationRequest(MPAProtocolOperationKind.Request,
                        new MPAProtocolAddress("3"), MPAModbusRegisterOperations.WriteSingle(100, 42)),
                    context, CancellationToken.None).GetAwaiter().GetResult();
                Check(write.IsCompleted && ((byte[])write.Value)[0] == 6,
                    "Modbus TCP 06 写入必须等待回显确认");
                behavior = 2;
                Reject<System.IO.InvalidDataException>(() => protocol.RequestAsync(
                    new MPAProtocolOperationRequest(MPAProtocolOperationKind.Request,
                        new MPAProtocolAddress("3"), MPAModbusRegisterOperations.ReadHolding(100, 1)),
                    context, CancellationToken.None).GetAwaiter().GetResult());
                behavior = 3;
                Reject<System.IO.InvalidDataException>(() => protocol.RequestAsync(
                    new MPAProtocolOperationRequest(MPAProtocolOperationKind.Request,
                        new MPAProtocolAddress("3"), MPAModbusRegisterOperations.ReadHolding(100, 1)),
                    context, CancellationToken.None).GetAwaiter().GetResult());
                Check(acceptedResponseCount == 2,
                    "错误功能码及Unit的事务响应不可作为有效Payload发布");
                behavior = 4;
                Reject<TimeoutException>(() => protocol.RequestAsync(
                    new MPAProtocolOperationRequest(MPAProtocolOperationKind.Request,
                        new MPAProtocolAddress("3"), MPAModbusRegisterOperations.ReadHolding(100, 1)),
                    context, CancellationToken.None).GetAwaiter().GetResult());
                Reject<NotSupportedException>(() => protocol.RequestAsync(
                    new MPAProtocolOperationRequest(MPAProtocolOperationKind.Request,
                        new MPAProtocolAddress("3"), new byte[] { 16, 0, 1, 0, 1 }),
                    context, CancellationToken.None).GetAwaiter().GetResult());
                Check(pair[0].State == MCCChannelState.Open, "异常响应/超时不能错误关闭 Channel");
                protocol.ResetAll();
            }
            finally { pair[0].Close(); pair[1].Close(); }
        }


        // P0-1：x2变量对象/运行状态语义的首次真实MDS落位，兼容旧表与作用域。
        private static void VariableObjectSmoke()
        {
            MDSVariableTable table = new MDSVariableTable("leak-tester");
            MDSVariableDefinition definition = new MDSVariableDefinition(
                "Pressure", typeof(double), "MPa", true, 0.0, "pressure-001");
            table.Define(definition);
            MDSVariable<double> pressure = table.Resolve<double>("Pressure");
            MDSVariableSnapshot<double> empty = pressure.Snapshot();
            Check(empty.VariableId == "pressure-001" && empty.HasDefaultValue &&
                empty.DefaultValue == 0.0 && !empty.HasValue && !empty.HasPreviousValue &&
                empty.Quality == MDSVariableQuality.Uncertain,
                "Default is definition, not a successfully sampled current value");
            int notifications = 0;
            table.VariableChanged += name => { if (name == "Pressure") notifications++; };
            table.Update<double>("Pressure", 1.5);
            MDSVariableSnapshot<double> first = pressure.Snapshot();
            Check(first.CurrentValue == 1.5 && first.Revision == 1 &&
                first.HasValue && !first.HasPreviousValue && first.Quality == MDSVariableQuality.Good,
                "First update uses the single authoritative typed variable");
            DateTime changedAt = first.ChangeUtc;
            pressure.Update(1.5); // 同值只刷新，不能覆盖上一次不同的值。
            MDSVariableSnapshot<double> refresh = pressure.Snapshot();
            Check(refresh.Revision == 2 && refresh.ChangeUtc == changedAt &&
                !refresh.HasPreviousValue && notifications == 2, "Equal refresh semantics");
            pressure.Update(2.5);
            pressure.Update(2.5);
            MDSVariableSnapshot<double> last = pressure.Snapshot();
            Check(last.PreviousValue == 1.5 && last.CurrentValue == 2.5 &&
                last.Revision == 4 && last.HasPreviousValue && last.ChangeUtc >= changedAt &&
                table.Get<double>("Pressure") == 2.5 && notifications == 4,
                "Previous tracks last distinct value; hot handle shares the table owner");
            Reject<InvalidCastException>(() => table.Resolve<int>("Pressure"));
            Reject<ArgumentException>(() => new MDSVariableDefinition("Wrong", typeof(int),
                defaultValue: "string-not-int"));
            table.Define(new MDSVariableDefinition("raw", typeof(byte[]),
                defaultValue: new byte[] { 1, 2 }));
            MDSVariable<byte[]> raw = table.Resolve<byte[]>("raw");
            byte[] source = { 3, 4 };
            raw.Update(source); source[0] = 9;
            MDSVariableSnapshot<byte[]> snap = raw.Snapshot();
            byte[] output = snap.CurrentValue; output[1] = 7;
            byte[] originalDefault = snap.DefaultValue; originalDefault[0] = 99;
            Check(raw.Get()[0] == 3 && raw.Get()[1] == 4 &&
                raw.Snapshot().DefaultValue[0] == 1,
                "Snapshot must not leak mutable arrays");
            table.Define(new MDSVariableDefinition("Readonly", typeof(int), writable: false));
            Reject<InvalidOperationException>(() => table.Resolve<int>("Readonly").Update(1));
            MDSVariableTable another = new MDSVariableTable("leak-tester-2");
            another.Define(new MDSVariableDefinition("Pressure", typeof(double)));
            another.Update<double>("Pressure", 99.0);
            Check(table.Get<double>("Pressure") == 2.5 && another.Get<double>("Pressure") == 99.0,
                "Variable scopes must not share values");
            Check(table.Remove("Pressure"), "Removing a typed variable");
            Reject<ObjectDisposedException>(() => pressure.Update(3.0));
            Check(!table.Contains("Pressure") && notifications == 4,
                "Removed typed handles must not publish stale updates");
        }

        private static void VariableTreeSmoke()
        {
            MDSVariableTable table = new MDSVariableTable("line");
            table.Define(new MDSVariableDefinition("pressure", typeof(double), null, true, null, "pressure-id"));
            table.Define(new MDSVariableDefinition("temp", typeof(int), null, true, null, "temp-id"));
            table.Update<double>("pressure", 1.25);
            MDSVariable<double> original = table.Resolve<double>("pressure");
            MDSVariableGroup group = table.Tree.AddGroup("Line1", id: "line-id");
            MDSVariableGroup station = table.Tree.AddGroup("StationA", "line-id", "station-id");
            table.Tree.Move("pressure-id", station.VariableId);
            MDSVariableLeaf leaf = (MDSVariableLeaf)table.Tree.FindByPath("/Line1/StationA/pressure");
            Check(leaf != null && ReferenceEquals(leaf.Resolve<double>(), original), "变量树不得复制变量值");
            table.Tree.Move("station-id", null, "StationB");
            Check(table.Tree.FindByPath("/Line1/StationA/pressure") == null &&
                table.Tree.FindByPath("/StationB/pressure") == leaf &&
                table.Get<double>("pressure") == 1.25, "变量移动仅更新路径");
            table.Tree.SetOwner("pressure-id", "Device-01");
            Check(leaf.OwnerRef == "Device-01", "业务Owner独立于Parent");
            table.Tree.AddTag("pressure-id", "Critical");
            Check(leaf.HasTag("Critical") && leaf.Tags.Contains("Critical") &&
                table.Tree.RemoveTag("pressure-id", "Critical") &&
                !leaf.HasTag("Critical"), "标签增删与快照不得形成二次变量状态");
            Reject<InvalidOperationException>(() => table.Tree.Move("station-id", "station-id"));
            Reject<InvalidOperationException>(() => table.Tree.Move("temp-id", null, "StationB"));
            Reject<InvalidOperationException>(() => table.Tree.RemoveEmptyGroup("station-id"));
            Check(table.Remove("pressure") && table.Tree.FindById("pressure-id") == null, "移除变量同步移除目录");
            Reject<ObjectDisposedException>(() => original.Update(2.0));
            Check(table.Tree.RemoveEmptyGroup("station-id"), "清空后的分组可以删除");
        }

        private static void VariableDispatcherSmoke()
        {
            MDSVariableTable variables = new MDSVariableTable("dispatch");
            variables.Define(new MDSVariableDefinition("pressure", typeof(int), null, true, null, "P"));
            variables.Define(new MDSVariableDefinition("temperature", typeof(int), null, true, null, "T"));
            MDSVariableChange[] observed = null;
            int callbacks = 0;
            using (MDSVariableChangeDispatcher dispatcher = variables.CreateDispatcher(8))
            {
                using (dispatcher.Subscribe(batch => {
                    observed = batch.ToArray();
                    Interlocked.Increment(ref callbacks);
                }, new[] { "P" }))
                {
                    variables.Update<int>("pressure", 10);
                    Check(dispatcher.Drain(TimeSpan.FromSeconds(5)), "变化派发排空1");
                    Check(observed != null && observed.Length == 1 && observed[0].Revision == 1 &&
                        (int)observed[0].CurrentValue == 10, "回调获得提交时的快照");
                    variables.Update<int>("pressure", 11);
                    variables.Update<int>("temperature", 50);
                    Check(dispatcher.Drain(TimeSpan.FromSeconds(5)), "变化派发排空2");
                    Check(observed.Length == 1 && observed[0].VariableId == "P" &&
                        observed[0].Revision == 2 && (int)observed[0].PreviousValue == 10,
                        "筛选与前值都必须准确");
                }
                int completed = callbacks;
                variables.Update<int>("pressure", 12);
                Check(dispatcher.Drain(TimeSpan.FromSeconds(5)) && callbacks == completed, "退订必须生效");
                Check(dispatcher.Metrics().Received == 4 &&
                    dispatcher.Metrics().SubscriberFailures == 0, "派发统计准确");
                variables.ChangeCaptured -= dispatcher.Enqueue;
            }
        }

        private static void VariableBoundAccessSmoke()
        {
            MDSVariableRegistry registry = new MDSVariableRegistry();
            MDSVariableTable table = new MDSVariableTable("fast");
            registry.Register(table);
            table.Define(new MDSVariableDefinition("readback", typeof(int)));
            table.Resolve<int>("readback").Update(12);
            MDSProtocolDataTargetAccess access = new MDSProtocolDataTargetAccess(registry);
            object current = access.ReadAsync(MPADataTargetKind.Variable, "fast/readback",
                CancellationToken.None).GetAwaiter().GetResult();
            Check((int)current == 12, "点表读取应直接访问单份变量");
            access.WriteAsync(MPADataTargetKind.Variable, "fast/readback", 30,
                CancellationToken.None).GetAwaiter().GetResult();
            Check(table.Get<int>("readback") == 30, "点表写入应走预解析变量");
            Reject<InvalidCastException>(() => access.WriteAsync(MPADataTargetKind.Variable,
                "fast/readback", 1.0, CancellationToken.None).GetAwaiter().GetResult());
        }

        private sealed class FakeVariableProvider : IMDSVariableProvider
        {
            private Action<MDSVariableProviderSample> _push;
            public string ProviderId { get { return "device-p1"; } }
            public void Start(Action<MDSVariableProviderSample> callback) { _push = callback; }
            public void Stop() { _push = null; }
            public void Dispose() { Stop(); }
            public void Emit(string name, object value)
            {
                Action<MDSVariableProviderSample> listener = _push;
                if (listener != null) listener(new MDSVariableProviderSample(name, value));
            }
        }
        private static void VariableProviderSmoke()
        {
            MDSVariableRegistry registry = new MDSVariableRegistry();
            MDSVariableTable table = new MDSVariableTable("leak");
            table.Define(new MDSVariableDefinition("pressure", typeof(double), null, false, null, "P"));
            registry.Register(table);
            FakeVariableProvider source = new FakeVariableProvider();
            using (MDSVariableProviderHost host = new MDSVariableProviderHost(registry))
            {
                host.Register(source);
                host.Bind("device-p1", "pressure-point", "leak", "pressure");
                Reject<ArgumentException>(() => host.Bind("device-p1", "pressure-point", "leak", "pressure"));
                Reject<InvalidCastException>(() => host.ResolveTyped<int>("device-p1", "pressure-point"));
                host.Start();
                source.Emit("pressure-point", 0.18);
                source.Emit("unknown-point", 1.0);
                Check(table.Get<double>("pressure") == 0.18 && host.Health().Applied == 1 &&
                    host.Health().Rejected == 1, "Provider仅能写授权映射，允许刷新只读变量");
                MDSVariableTypedPointBinding<double> hot = host.ResolveTyped<double>("device-p1", "pressure-point");
                hot.Apply(0.22);
                Check(table.Resolve<double>("pressure").Snapshot().PreviousValue == 0.18 &&
                    table.Get<double>("pressure") == 0.22, "预绑定强类型Provider写入唯一变量");
                host.Stop();
                Reject<InvalidOperationException>(() => hot.Apply(0.3));
                Check(table.Get<double>("pressure") == 0.22 && !host.Health().Running,
                    "停止后旧世代Point句柄不可再写入");
            }
        }

        private static void VariableCasSmoke()
        {
            MDSVariableTable table = new MDSVariableTable("cas");
            table.Define(new MDSVariableDefinition("pressure", typeof(double), null, true, null, "CAS1"));
            MDSVariable<double> variable = table.Resolve<double>("pressure");
            MDSVariableUpdateResult accepted = variable.Update(1.0,
                new MDSVariableUpdateOptions("operator", expectedRevision: 0));
            Check(accepted.Revision == 1 && accepted.ValueChanged && variable.Snapshot().Source == "operator",
                "CAS第一次提交成功并保留来源");
            MDSVariableUpdateResult failed;
            Check(!variable.TryUpdate(2.0, 0, out failed) && failed == null &&
                variable.Snapshot().CurrentValue == 1.0 && variable.Snapshot().Revision == 1,
                "旧版本不得修改当前值或版本");
            Reject<MDSVariableVersionConflictException>(() => variable.Update(2.0,
                new MDSVariableUpdateOptions(expectedRevision: 0)));
            MDSVariableUpdateResult refresh = variable.Update(1.0,
                new MDSVariableUpdateOptions(expectedRevision: 1));
            Check(!refresh.ValueChanged && refresh.Revision == 2 &&
                variable.Snapshot().ChangeUtc <= refresh.RefreshUtc,
                "同值刷新提升Revision但不提升变化语义");
        }

        private static void VariableScopeSmoke()
        {
            MDSVariableRegistry registry = new MDSVariableRegistry();
            MDSVariableTable oldTable = new MDSVariableTable("machine");
            oldTable.Define(new MDSVariableDefinition("Pressure", typeof(double),
                null, true, null, "pressure-stable"));
            registry.Register(oldTable);
            MDSVariableScope original = registry.FindScope("machine");
            CxVisual.Foundation.Data.Variables.FDVVariableKey key =
                new CxVisual.Foundation.Data.Variables.FDVVariableKey("machine", "pressure-stable");
            registry.Resolve<double>(key).Update(0.5);
            Check(original.Resolve<double>(key).Get() == 0.5, "按Scope和稳定ID指向同一个正式变量");
            Reject<InvalidOperationException>(() => registry.Resolve<double>(
                new CxVisual.Foundation.Data.Variables.FDVVariableKey("wrong", "pressure-stable")));
            Check(registry.Remove("machine") && !original.IsActive, "注销旧Scope应使代次失效");
            Reject<ObjectDisposedException>(() => original.Resolve<double>(key));
            MDSVariableTable fresh = new MDSVariableTable("machine");
            fresh.Define(new MDSVariableDefinition("Pressure", typeof(double),
                null, true, null, "pressure-stable"));
            registry.Register(fresh);
            Check(registry.FindScope("machine").Generation != original.Generation,
                "重复Scope名称不复用上代Generation");
            registry.Resolve<double>(key).Update(0.7);
            Check(registry.Resolve<double>(key).Get() == 0.7 &&
                oldTable.Get<double>("Pressure") == 0.5, "新旧会话不串写");
        }

        private static void VariableMessageBridgeSmoke()
        {
            MDSVariableTable table = new MDSVariableTable("line1");
            table.Define(new MDSVariableDefinition("Pressure", typeof(double), null, true, null, "pressure"));
            using (MNMMessageCenter messages = new MNMMessageCenter())
            using (MDSVariableChangeDispatcher dispatcher = table.CreateDispatcher(32))
            {
                MDSVariableChangeNotice received = null;
                using (IDisposable receiver = messages.Subscribe<MDSVariableChangeNotice>(
                    "/machine/line1/app/CxVisual/module/Variables/message/pressure", context => received = context.Payload))
                using (MDSVariableMessageBridge bridge =
                    new MDSVariableMessageBridge(table, dispatcher, messages))
                {
                    table.Update<double>("Pressure", 1.25);
                    Check(dispatcher.Drain(TimeSpan.FromSeconds(5)), "变化通知桥应能排空");
                    Check(received != null && received.ScopeId == "line1" && received.Revision == 1 &&
                        received.VariableId == "pressure" && bridge.Published == 1 && bridge.Failed == 0,
                        "通知必须来自已有MessageCenter且只包含准确变量身份");
                    Check(CxVisual.Foundation.Data.Messages.MNMTopicFilter.ValidateTopic(
                        "/machine/line1/app/CxVisual/module/Variables/message/pressure") != null,
                        "变量消息应遵守M36既有八层Topic契约");
                }
                Reject<ArgumentException>(() => new MDSVariableMessageBridge(
                    table, dispatcher, messages, "/cxvisual/variables"));
            }
        }

        public static void Run()
        {
            VariableMessageBridgeSmoke();
            VariableScopeSmoke();
            VariableCasSmoke();
            VariableProviderSmoke();
            VariableBoundAccessSmoke();
            VariableDispatcherSmoke();
            VariableTreeSmoke();
            VariableObjectSmoke();
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

            MCCChannelConfiguration channel = new MCCTcpClientChannelConfiguration {
                Id = "tcp01", Kind = MCCChannelKind.TcpClient, Host = "127.0.0.1", Port = 502
            };
            channel.Validate();
            MCCChannelTypeDescriptor tcp = channel.DescribeType();
            Check(tcp.IO == MCCIOCapabilities.ByteStream &&
                  tcp.Topology == MCCChannelTopology.SinglePeer, "Channel metadata");
            Reject<ArgumentException>(() => new MCCTcpClientChannelConfiguration {
                Id = "bad", Kind = MCCChannelKind.TcpClient, Port = 0 }.Validate());
            Reject<ArgumentException>(() => new MCCVirtualChannelConfiguration {
                QueueCapacity = 0 }.Validate());

            MCCChannelTypeDescriptor tcpServer = new MCCTcpServerChannelConfiguration
                { Kind = MCCChannelKind.TcpServer, Port = 9000 }.DescribeType();
            MCCChannelTypeDescriptor udpServer = new MCCUdpServerChannelConfiguration
                { Kind = MCCChannelKind.UdpServer, Port = 9001 }.DescribeType();
            Check(tcpServer.Topology == MCCChannelTopology.MultiPeer &&
                  (tcpServer.IO & MCCIOCapabilities.Listener) != 0 &&
                  udpServer.Topology == MCCChannelTopology.MultiPeer &&
                  udpServer.IO == MCCIOCapabilities.Datagram,
                "TCP/UDP Server manage multiple peers inside one parent Channel");
            MCCChannelTypeDescriptor serial = new MCCSerialChannelConfiguration
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
                Channel = new MCCSerialChannelConfiguration { Id = "bus", Kind = MCCChannelKind.Serial, PortName = "COM3" },
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
                Channel = new MCCVirtualChannelConfiguration { Id = "raw-tx", Kind = MCCChannelKind.Virtual },
                Protocol = new MPAProtocolConfiguration { Kind = MPAProtocolKind.LengthFrame }
            };
            MCNConnectorModule streamModule = new MCNConnectorModule(
                streamConfig.Describe("stream", MFMModuleKind.Connector), streamConfig,
                raw[0], new MPALengthFrameProtocol());
            List<byte[]> assembled = new List<byte[]>();
            MCCSubChannelAddress lastSource = null;
            streamModule.PayloadReceived += x => assembled.Add(x);
            streamModule.PayloadReceivedFrom += (source, x) => lastSource = source;
            raw[1].Open();
            streamModule.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
            byte[] dataFrame = new MPALengthFrameProtocol().PrepareSend(new byte[] { 7, 8 }, null);
            raw[1].SendAsync(dataFrame, new MCCSubChannelAddress("raw-rx"),
                CancellationToken.None).GetAwaiter().GetResult();
            Check(lastSource != null && lastSource.ChannelId == "raw-tx",
                "Connector inbound event preserves Channel/SubChannel route");
            Check(assembled.Count == 1 && assembled[0].SequenceEqual(new byte[] { 7, 8 }),
                "Connector receives and delegates parsing to Protocol");
            streamModule.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
            raw[1].Close();

            MCCVirtualChannel[] emptyPair = MCCVirtualChannel.CreatePair("empty-tx", "empty-rx");
            MCNConnectorConfiguration emptyCfg = new MCNConnectorConfiguration {
                Channel = new MCCVirtualChannelConfiguration { Id = "empty-tx", Kind = MCCChannelKind.Virtual },
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
            ModbusTcpTransactionSmoke();
            // Modbus TCP/RTU 的实际帧校验：TCP 半包，RTU 站号和 CRC16。
            // 按代次清理半包：旧客户端遗留字节不能污染新的 ProtocolSession。
            MPALengthFrameProtocol cleanupParser = new MPALengthFrameProtocol();
            byte[] wrapped = cleanupParser.PrepareSend(new byte[] { 9, 8, 7 }, null);
            MCCSubChannelAddress former = new MCCSubChannelAddress("server", "peer", 1);
            Check(cleanupParser.ProcessReceived(new MCCChannelReceiveContext(former,
                wrapped.Take(2).ToArray()), new MPAProtocolContext("length", "one")).Count == 0,
                "单个子通道半包保留在规约解析状态");
            ((IMPAProtocolSessionCleanup)cleanupParser).Reset(former);
            Check(cleanupParser.ProcessReceived(new MCCChannelReceiveContext(former,
                wrapped), new MPAProtocolContext("length", "one")).Count == 1,
                "断开后清理半包，再收到整帧必须独立解析");
            ((IMPAProtocolSessionCleanup)cleanupParser).ResetAll();
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

            // 外部接入报文绝不能跳过正式命令授权。
            bool invokedUnsafeDeviceCommand = false;
            MDSProtocolDataTargetAccess deniedCommands = new MDSProtocolDataTargetAccess(
                invoke: (commandId, value, ct) => {
                    invokedUnsafeDeviceCommand = true;
                    return Task.CompletedTask;
                },
                authorizeCommand: (commandId, value) => false);
            Reject<UnauthorizedAccessException>(() => deniedCommands.InvokeAsync(
                "Device.Stop", 1, CancellationToken.None).GetAwaiter().GetResult());
            Check(!invokedUnsafeDeviceCommand,
                "未授权命令不得调用真实 Owner");
            MDSProtocolDataTargetAccess noCommands = new MDSProtocolDataTargetAccess();
            Reject<NotSupportedException>(() => noCommands.InvokeAsync(
                "Device.Stop", 1, CancellationToken.None).GetAwaiter().GetResult());

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
