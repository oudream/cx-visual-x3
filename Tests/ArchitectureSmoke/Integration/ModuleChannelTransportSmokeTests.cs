using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Mechanism.Connectivity;
using CxVisual.Mechanism.Core.Modules;
using CxVisual.Mechanism.Data;
using CxVisual.Mechanism.Data.Variables;

namespace CxVisual.Tests.ArchitectureSmoke
{
    // OS loopback 真网络 I/O：与 Virtual 区分，不能冒称外部 PLC/HIL 现场验收。
    internal static class ModuleChannelTransportSmokeTests
    {
        private static void Check(bool condition, string why)
        {
            if (!condition) throw new InvalidOperationException("CHANNEL.TRANSPORT " + why);
        }
        private sealed class StartupReceiveChannel : MCCSingleChannelBase
        {
            internal bool ClosedResources;
            internal StartupReceiveChannel(string id) : base(id) { }
            internal void InjectReceived(byte[] data) { Receive(data); }
            protected override Task OnOpenAsync(CancellationToken ct)
            {
                // 用确定性时序覆盖 Opening 状态中的第一次收包。
                Receive(new byte[] { 88 });
                return Task.CompletedTask;
            }
            protected override Task OnCloseAsync(CancellationToken ct)
            {
                ClosedResources = true;
                return Task.CompletedTask;
            }
            protected override Task OnSendAsync(byte[] payload, MCCSubChannelAddress address, CancellationToken ct)
            { return Task.CompletedTask; }
        }
        private sealed class FailingOpenChannel : MCCSingleChannelBase
        {
            internal bool CleanupCalled;
            internal FailingOpenChannel() : base("failure") { }
            protected override Task OnOpenAsync(CancellationToken ct)
            { throw new IOException("打开设备失败"); }
            protected override Task OnCloseAsync(CancellationToken ct)
            { CleanupCalled = true; return Task.CompletedTask; }
            protected override Task OnSendAsync(byte[] data, MCCSubChannelAddress target, CancellationToken ct)
            { return Task.CompletedTask; }
        }
        private static void LifecycleStateMachine()
        {
            StartupReceiveChannel early = new StartupReceiveChannel("early");
            byte[] first = null;
            early.Received += data => first = data.Data;
            early.Open();
            Check(early.State == MCCChannelState.Open && first != null &&
                first.SequenceEqual(new byte[] { 88 }),
                "OpenAsync 中的第一包应接纳，不能因后台接收快于状态切换而失败");
            early.Close();
            Check(early.ClosedResources && early.State == MCCChannelState.Closed,
                "关闭必须释放资源");
            early.Close();
            FailingOpenChannel fail = new FailingOpenChannel();
            bool rejected = false;
            try { fail.Open(); } catch (IOException) { rejected = true; }
            Check(rejected && fail.CleanupCalled && fail.State == MCCChannelState.Faulted,
                "Open 失败要清理已创建资源并报告 Faulted");
            fail.Close();
            Check(fail.State == MCCChannelState.Closed,
                "Faulted 关闭后可回到 Closed");
        }
        private static int FreePort(bool udp)
        {
            if (udp)
            {
                using (UdpClient candidate = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
                    return ((IPEndPoint)candidate.Client.LocalEndPoint).Port;
            }
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try { return ((IPEndPoint)listener.LocalEndpoint).Port; }
            finally { listener.Stop(); }
        }

        public static void Run()
        {
            LifecycleStateMachine();
            BoundedEvidence();
            TcpLoopback();
            ConnectorModbusTcpLoopback();
            UdpLoopback();
            HttpConfigurationSmoke();
            Console.WriteLine("CHANNEL.TRANSPORT: TCP/UDP loopback MultiPeer and stale generation PASS");
        }
        // 大数据仍应完整传给上层；证据只能有界留存，并明确记录省略原因。
        private static void HttpConfigurationSmoke()
        {
            MCCHttpClientChannelConfiguration config = new MCCHttpClientChannelConfiguration {
                Id = "http-client-test", Url = "http://127.0.0.1:8080/"
            };
            Check(config.DescribeType().IO == (MCCIOCapabilities.Message | MCCIOCapabilities.RequestResponse),
                "HTTP Client capabilities");
            MCCHttpClientChannel client = new MCCHttpClientChannel(config);
            Check(client.State == MCCChannelState.Closed, "HTTP Client constructed without network side effects");
            bool rejected = false;
            try { new MCCHttpClientChannelConfiguration { Url = "ftp://localhost/a" }.Validate(); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, "Reject non-http channel URL");
        }

        private static void BoundedEvidence()
        {
            StartupReceiveChannel channel = new StartupReceiveChannel("evidence-budget");
            int lastReceivedLength = 0;
            channel.Received += data => lastReceivedLength = data.Data.Length;
            try
            {
                channel.Open();
                byte[] oversized = new byte[5 * 1024 * 1024];
                oversized[0] = 0x5a;
                channel.InjectReceived(oversized);
                MCCChannelFrame metadata = channel.ReadFrames().Last();
                Check(lastReceivedLength == oversized.Length,
                    "超大接收数据不得被诊断证据预算截断");
                Check(metadata.Direction == MCCFrameDirection.Rx && metadata.Bytes.Length == 0 &&
                    metadata.Error.Contains("原始字节数=" + oversized.Length),
                    "超大报文诊断副本必须省略并保留长度与原因，不可静默丢字节");

                byte[] chunk = new byte[128 * 1024];
                for (int i = 0; i < 40; i++) channel.InjectReceived(chunk);
                System.Collections.Generic.IList<MCCChannelFrame> frames = channel.ReadFrames();
                long total = frames.Sum(frame => (long)frame.Bytes.Length);
                Check(total <= 4L * 1024 * 1024 && frames.Count < 42 &&
                    frames.Last().Bytes.Length == chunk.Length,
                    "诊断证据累计字节数必须有界且保留最新收包");
            }
            finally { channel.Close(); }
        }

        private static void TcpLoopback()
        {
            int port = FreePort(false);
            MCCTcpServerChannel server = new MCCTcpServerChannel(
                new MCCTcpServerChannelConfiguration { Id = "tcp-server-test", Kind = MCCChannelKind.TcpServer,
                    Host = "127.0.0.1", Port = port });
            MCCTcpClientChannel client = new MCCTcpClientChannel(
                new MCCTcpClientChannelConfiguration { Id = "tcp-client-test", Kind = MCCChannelKind.TcpClient,
                    Host = "127.0.0.1", Port = port });
            byte[] inbound = null, outbound = null;
            MCCSubChannelAddress sub = null;
            using (ManualResetEventSlim arrived = new ManualResetEventSlim(false))
            using (ManualResetEventSlim replied = new ManualResetEventSlim(false))
            {
                server.Received += message => {
                    inbound = message.Data; sub = message.Address; arrived.Set();
                };
                client.Received += message => { outbound = message.Data; replied.Set(); };
                try
                {
                    server.Open(); client.Open();
                    client.Send(new byte[] { 1, 2, 3 });
                    Check(arrived.Wait(3000) && inbound.SequenceEqual(new byte[] { 1, 2, 3 }) &&
                        sub != null && sub.HasSubChannel && sub.Generation > 0 &&
                        server.SnapshotSubChannels().Length == 1,
                        "TCP Server must receive from one SubChannel without creating another parent Channel");
                    server.SendAsync(new byte[] { 4, 5 }, sub, CancellationToken.None)
                        .GetAwaiter().GetResult();
                    Check(replied.Wait(3000) && outbound.SequenceEqual(new byte[] { 4, 5 }),
                        "TCP Server reply must use selected SubChannel");
                    MCCSubChannelAddress originalPeer = sub;
                    MCCTcpClientChannel second = new MCCTcpClientChannel(
                        new MCCTcpClientChannelConfiguration { Id = "tcp-client-test2",
                            Kind = MCCChannelKind.TcpClient, Host = "127.0.0.1", Port = port });
                    using (ManualResetEventSlim secondArrived = new ManualResetEventSlim(false))
                    using (ManualResetEventSlim secondReplied = new ManualResetEventSlim(false))
                    {
                        MCCSubChannelAddress secondAddress = null;
                        byte[] secondReply = null;
                        server.Received += msg => {
                            if (msg.Data.Length == 1 && msg.Data[0] == 6)
                            {
                                secondAddress = msg.Address;
                                secondArrived.Set();
                            }
                        };
                        second.Received += msg => { secondReply = msg.Data; secondReplied.Set(); };
                        try
                        {
                            second.Open(); second.Send(new byte[] { 6 });
                            Check(secondArrived.Wait(3000) && secondAddress != null &&
                                !secondAddress.Equals(originalPeer) &&
                                server.SnapshotSubChannels().Length == 2,
                                "TCP Server 一个父通道应同时接受两个独立 SubChannel");
                            server.SendAsync(new byte[] { 7 }, secondAddress, CancellationToken.None)
                                .GetAwaiter().GetResult();
                            Check(secondReplied.Wait(3000) &&
                                secondReply.SequenceEqual(new byte[] { 7 }),
                                "TCP Server 第二对端定向响应");
                        }
                        finally { second.Close(); }
                    }
                    Check(server.RemovePeer(originalPeer), "TCP peer remove");
                    bool rejected = false;
                    try { server.SendAsync(new byte[] { 9 }, originalPeer, CancellationToken.None).GetAwaiter().GetResult(); }
                    catch (InvalidOperationException) { rejected = true; }
                    Check(rejected, "TCP stale SubChannel must be rejected");
                }
                finally { client.Close(); server.Close(); }
            }
        }

        // 与纯 Virtual 请求测试互补：Connector + 正式 TCP Client / Server + Modbus Protocol。
        // 服务端为本机可控的模拟端点，不能当成 PLC 硬件 HIL。
        private static void ConnectorModbusTcpLoopback()
        {
            int port = FreePort(false);
            MCCTcpServerChannel server = new MCCTcpServerChannel(new MCCTcpServerChannelConfiguration {
                Id = "mb-loopback-server", Kind = MCCChannelKind.TcpServer,
                Host = "127.0.0.1", Port = port
            });
            MCNConnectorConfiguration config = new MCNConnectorConfiguration {
                DisplayName = "Modbus TCP Loopback",
                Channel = new MCCTcpClientChannelConfiguration {
                    Id = "mb-loopback-client", Kind = MCCChannelKind.TcpClient,
                    Host = "127.0.0.1", Port = port
                },
                Protocol = new MPAProtocolConfiguration {
                    Id = "mb-loopback-protocol", Kind = MPAProtocolKind.ModbusTcpDraft, TimeoutMs = 3000,
                    Points = new List<MPAPointDefinition> {
                        new MPAPointDefinition("temperature", "holding:100", typeof(ushort)) {
                            StationAddress = "3", CanRead = true, CanWrite = true
                        }
                    },
                    Mappings = new List<MPADataMapping> {
                        new MPADataMapping { PointRef = "temperature",
                            TargetKind = MPADataTargetKind.Variable, TargetRef = "plc/temperature",
                            Direction = MPADataDirection.Inbound, Operation = MPADataOperation.WriteValue },
                        new MPADataMapping { PointRef = "temperature",
                            TargetKind = MPADataTargetKind.Variable, TargetRef = "plc/mirror",
                            Direction = MPADataDirection.Inbound, Operation = MPADataOperation.WriteValue },
                        new MPADataMapping { PointRef = "temperature",
                            TargetKind = MPADataTargetKind.Variable, TargetRef = "plc/temperature",
                            Direction = MPADataDirection.Outbound, Operation = MPADataOperation.ReadValue }
                    }
                }
            };
            MCNConnectorModule connector = new MCNConnectorModule(
                config.Describe("mb-loopback", MFMModuleKind.Connector), config,
                new MCCTcpClientChannel(config.Channel), new MPAModbusTcpProtocol());
            MDSVariableRegistry registry = new MDSVariableRegistry();
            MDSVariableTable variables = new MDSVariableTable("plc");
            registry.Register(variables);
            variables.Define(new MDSVariableDefinition("temperature", typeof(ushort)));
            variables.Define(new MDSVariableDefinition("mirror", typeof(ushort)));
            MDSProtocolDataTargetAccess mds = new MDSProtocolDataTargetAccess(registry);
            ushort lastWritten = 0;
            List<byte> requestBuffer = new List<byte>();
            server.Received += received =>
            {
                lock (requestBuffer)
                {
                    requestBuffer.AddRange(received.Data);
                    if (requestBuffer.Count < 7) return;
                    int size = 6 + (requestBuffer[4] << 8 | requestBuffer[5]);
                    if (requestBuffer.Count < size) return;
                    // 回送同一 MBAP TransactionId/Unit；03 返回42，06 回显请求值。
                    byte[] response;
                    if (requestBuffer[7] == 6)
                    {
                        lastWritten = (ushort)((requestBuffer[10] << 8) | requestBuffer[11]);
                        response = requestBuffer.Take(size).ToArray();
                    }
                    else
                        response = new byte[] { requestBuffer[0], requestBuffer[1], 0, 0, 0, 5,
                            requestBuffer[6], 3, 2, 0, 42 };
                    requestBuffer.RemoveRange(0, size);
                    server.SendAsync(response, received.Address, CancellationToken.None)
                        .GetAwaiter().GetResult();
                }
            };
            try
            {
                server.Open();
                connector.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
                MPAProtocolOperationRequest request = new MPAProtocolOperationRequest(
                    MPAProtocolOperationKind.Request, new MPAProtocolAddress("3"),
                    MPAModbusRegisterOperations.ReadHolding(100, 1));
                MPAProtocolOperationResult result = connector.RequestAsync(
                    request, new MCCSubChannelAddress(config.Channel.Id), CancellationToken.None)
                    .GetAwaiter().GetResult();
                Check(result.IsCompleted && ((byte[])result.Value).SequenceEqual(new byte[] { 3, 2, 0, 42 }),
                    "Connector RequestAsync 必须通过真实 TCP 收发并完成 03 事务确认");
                MCCSubChannelAddress destination = new MCCSubChannelAddress(config.Channel.Id);
                object pointValue = connector.ReadDevicePointAsync("temperature", mds, destination,
                    CancellationToken.None).GetAwaiter().GetResult();
                Check((ushort)pointValue == 42 && variables.Get<ushort>("temperature") == 42 &&
                    variables.Get<ushort>("mirror") == 42,
                    "PointTable holding:100 读值必须一对多写入真实 MDS Variable");
                variables.Update<ushort>("temperature", 55);
                MPAProtocolOperationResult confirmed = connector.WriteDevicePointAsync(
                    "temperature", mds, destination, CancellationToken.None).GetAwaiter().GetResult();
                Check(confirmed.IsCompleted && lastWritten == 55,
                    "MDS Outbound 变量必须经 PointTable 发送06并等设备确认");
                config.Protocol.Points[0].CanWrite = false;
                bool denied = false;
                try { connector.WriteDevicePointAsync("temperature", mds, destination,
                    CancellationToken.None).GetAwaiter().GetResult(); }
                catch (InvalidOperationException) { denied = true; }
                Check(denied, "PointTable 禁止写入的点不得发出设备命令");
            }
            finally
            {
                if (connector.IsStarted)
                    connector.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
                server.Close();
            }
        }

        private static void UdpLoopback()
        {
            int port = FreePort(true);
            MCCUdpServerChannel server = new MCCUdpServerChannel(
                new MCCUdpServerChannelConfiguration { Id = "udp-server-test", Kind = MCCChannelKind.UdpServer,
                    Host = "127.0.0.1", Port = port });
            MCCUdpChannel client = new MCCUdpChannel(
                new MCCUdpChannelConfiguration { Id = "udp-client-test", Kind = MCCChannelKind.Udp,
                    Host = "127.0.0.1", Port = port });
            MCCSubChannelAddress sub = null;
            byte[] outbound = null;
            using (ManualResetEventSlim arrived = new ManualResetEventSlim(false))
            using (ManualResetEventSlim replied = new ManualResetEventSlim(false))
            {
                server.Received += msg => { sub = msg.Address; arrived.Set(); };
                client.Received += msg => { outbound = msg.Data; replied.Set(); };
                try
                {
                    server.Open(); client.Open();
                    client.Send(new byte[] { 33 });
                    Check(arrived.Wait(3000) && sub != null && sub.HasSubChannel &&
                        server.SnapshotSubChannels().Length == 1,
                        "UDP Server endpoint must become a logical SubChannel, not TCP Accept");
                    server.SendAsync(new byte[] { 44 }, sub, CancellationToken.None).GetAwaiter().GetResult();
                    Check(replied.Wait(3000) && outbound.SequenceEqual(new byte[] { 44 }),
                        "UDP Server reply to exact endpoint");
                    MCCSubChannelAddress originalPeer = sub;
                    MCCUdpChannel second = new MCCUdpChannel(
                        new MCCUdpChannelConfiguration { Id = "udp-client-test2",
                            Kind = MCCChannelKind.Udp, Host = "127.0.0.1", Port = port });
                    using (ManualResetEventSlim secondArrived = new ManualResetEventSlim(false))
                    using (ManualResetEventSlim secondReplied = new ManualResetEventSlim(false))
                    {
                        MCCSubChannelAddress secondAddress = null;
                        byte[] secondReply = null;
                        server.Received += msg => {
                            if (msg.Data.Length == 1 && msg.Data[0] == 66)
                            { secondAddress = msg.Address; secondArrived.Set(); }
                        };
                        second.Received += msg => { secondReply = msg.Data; secondReplied.Set(); };
                        try
                        {
                            second.Open(); second.Send(new byte[] { 66 });
                            Check(secondArrived.Wait(3000) && secondAddress != null &&
                                !secondAddress.Equals(originalPeer) &&
                                server.SnapshotSubChannels().Length == 2,
                                "UDP Server 一个父通道应区分两个来源 Endpoint");
                            server.SendAsync(new byte[] { 77 }, secondAddress, CancellationToken.None)
                                .GetAwaiter().GetResult();
                            Check(secondReplied.Wait(3000) &&
                                secondReply.SequenceEqual(new byte[] { 77 }),
                                "UDP Server 第二逻辑对端定向响应");
                        }
                        finally { second.Close(); }
                    }
                    Check(server.RemovePeer(originalPeer), "UDP remove endpoint");
                    bool rejected = false;
                    try { server.SendAsync(new byte[] { 55 }, originalPeer, CancellationToken.None)
                        .GetAwaiter().GetResult(); }
                    catch (InvalidOperationException) { rejected = true; }
                    Check(rejected, "UDP stale generation cannot route");
                }
                finally { client.Close(); server.Close(); }
            }
        }
    }
}
