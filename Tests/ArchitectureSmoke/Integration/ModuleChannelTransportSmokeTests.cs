using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Mechanism.Connectivity;

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
            TcpLoopback();
            UdpLoopback();
            Console.WriteLine("CHANNEL.TRANSPORT: TCP/UDP loopback MultiPeer and stale generation PASS");
        }
        private static void TcpLoopback()
        {
            int port = FreePort(false);
            MCCTcpServerChannel server = new MCCTcpServerChannel(
                new MCCChannelConfiguration { Id = "tcp-server-test", Kind = MCCChannelKind.TcpServer,
                    Host = "127.0.0.1", Port = port });
            MCCTcpClientChannel client = new MCCTcpClientChannel(
                new MCCChannelConfiguration { Id = "tcp-client-test", Kind = MCCChannelKind.TcpClient,
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
                    Check(server.RemovePeer(sub), "TCP peer remove");
                    bool rejected = false;
                    try { server.SendAsync(new byte[] { 9 }, sub, CancellationToken.None).GetAwaiter().GetResult(); }
                    catch (InvalidOperationException) { rejected = true; }
                    Check(rejected, "TCP stale SubChannel must be rejected");
                }
                finally { client.Close(); server.Close(); }
            }
        }

        private static void UdpLoopback()
        {
            int port = FreePort(true);
            MCCUdpServerChannel server = new MCCUdpServerChannel(
                new MCCChannelConfiguration { Id = "udp-server-test", Kind = MCCChannelKind.UdpServer,
                    Host = "127.0.0.1", Port = port });
            MCCUdpChannel client = new MCCUdpChannel(
                new MCCChannelConfiguration { Id = "udp-client-test", Kind = MCCChannelKind.Udp,
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
                    Check(server.RemovePeer(sub), "UDP remove endpoint");
                    bool rejected = false;
                    try { server.SendAsync(new byte[] { 55 }, sub, CancellationToken.None)
                        .GetAwaiter().GetResult(); }
                    catch (InvalidOperationException) { rejected = true; }
                    Check(rejected, "UDP stale generation cannot route");
                }
                finally { client.Close(); server.Close(); }
            }
        }
    }
}
