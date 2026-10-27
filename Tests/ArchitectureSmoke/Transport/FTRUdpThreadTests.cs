using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using CxVisual.Foundation.Transport;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class FTRUdpThreadTests
    {
        private static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException("F17.UDP " + message); }
        private static void Wait(Func<bool> condition, string message)
        { Check(SpinWait.SpinUntil(condition, 4000), message); }
        private static int Port()
        { using (var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0))) return ((IPEndPoint)udp.Client.LocalEndPoint).Port; }
        internal static void Run()
        {
            DatagramBoundariesAndSource();
            TruncationAndPeerBudget();
            SlowConsumerStopAndLeases();
            IdleGenerationAndObserver();
            MulticastLoopback();
            Console.WriteLine("F17.UDP PASS: dedicated pooled receive, sync/async datagrams, zero-length/source, truncation/peer rejection, idle generation, stop and real IPv4 multicast");
        }
        private static void DatagramBoundariesAndSource()
        {
            int port = Port();
            using (var server = new FTRUdpServerChannel(new FTRUdpServerChannelConfiguration { Id = "udp-boundaries", Port = port,
                ReceiveBufferBytes = 65536, ReceiveByteBudget = 131072, QueueCapacity = 2, CapturePayloadEvidence = false }))
            using (var client = new FTRUdpChannel(new FTRUdpChannelConfiguration { Id = "udp-sender", Port = port,
                ReceiveByteBudget = 131072, QueueCapacity = 2, CapturePayloadEvidence = false }))
            {
                var seen = new List<int>(); var replies = new List<int>(); object gate = new object();
                string source = null; string legacySource = null; bool receiverThread = false; int corrupt = 0;
                server.BufferReceived += buffer => {
                    lock (gate) { seen.Add(buffer.Count); source = buffer.RemoteEndpoint; }
                    receiverThread = Thread.CurrentThread.IsBackground && !Thread.CurrentThread.IsThreadPoolThread;
                    byte[] data = buffer.ToArray();
                    server.Send(data, 0, data.Length, buffer.Address, CancellationToken.None);
                };
                server.Received += context => legacySource = context.RemoteEndpoint;
                client.BufferReceived += buffer => {
                    var segment = buffer.Segment;
                    for (int i = 0; i < segment.Count; i++)
                        if (segment.Array[segment.Offset + i] != (segment.Count == 2 ? (byte)(i + 3) : (byte)(i % 251)))
                            Interlocked.Exchange(ref corrupt, 1);
                    lock (gate) replies.Add(buffer.Count);
                };
                server.Open(); client.Open();
                int[] sizes = { 0, 1, 17, 4096, 65507 };
                foreach (int size in sizes)
                {
                    byte[] data = Enumerable.Range(0, size).Select(n => (byte)(n % 251)).ToArray();
                    client.Send(data);
                    Wait(() => { lock (gate) return replies.Count == Array.IndexOf(sizes, size) + 1; }, "未保留数据报边界/空报文");
                }
                client.SendAsync(new byte[] { 3, 4 }, new FTRSubChannelAddress(client.Id), CancellationToken.None).GetAwaiter().GetResult();
                Wait(() => { lock (gate) return replies.Count == 6; }, "异步UDP同底层收发");
                lock (gate) Check(seen.Take(5).SequenceEqual(sizes) && replies.Take(5).SequenceEqual(sizes), "UDP不可按流合并或拆报文");
                Check(Volatile.Read(ref corrupt) == 0, "UDP报文往返字节完整性失败");
                Wait(() => legacySource != null, "旧上下文来源缺失");
                Check(!string.IsNullOrEmpty(source) && source == legacySource && receiverThread, "来源或独立交付线程错误");
                var snapshot = server.Diagnostics;
                Check(snapshot.ReceiveQueue.ReceiveThreadId != snapshot.ReceiveQueue.DeliveryThreadId &&
                    snapshot.ReceiveQueue.AllocatedBytes <= 131072 && snapshot.Received == 6 && snapshot.Sent == 6,
                    "接收线程、内存或诊断报文数错误");
                bool tooLarge = false, split = false;
                try { client.Send(new byte[65508]); } catch (ArgumentException) { tooLarge = true; }
                try { client.Send(new byte[10], segmentBytes: 3); } catch (ArgumentException) { split = true; }
                Check(tooLarge && split, "超大报文和流式拆包必须拒绝");
                client.Close(); server.Close();
                Check(server.Diagnostics.ReceiveQueue.AllocatedBytes == 0, "关闭应清空无外部租约的接收池");
            }
        }
        private static void TruncationAndPeerBudget()
        {
            int port = Port();
            using (var server = new FTRUdpServerChannel(new FTRUdpServerChannelConfiguration { Id = "udp-rejections", Port = port,
                ReceiveBufferBytes = 8, ReceiveByteBudget = 16, QueueCapacity = 2, MaxClients = 1 }))
            using (var first = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
            using (var second = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
            {
                int packets = 0; server.BufferReceived += _ => Interlocked.Increment(ref packets); server.Open();
                var target = new IPEndPoint(IPAddress.Loopback, port);
                first.Send(new byte[100], 100, target);
                Wait(() => server.Diagnostics.Errors >= 1, "过大报文必须产生截断诊断");
                Check(packets == 0 && server.SnapshotSubChannels().Length == 0, "截断报文不得交付或注册来源");
                first.Send(new byte[] { 9 }, 1, target); Wait(() => Volatile.Read(ref packets) == 1, "截断后应继续接收完整报文");
                long before = server.Diagnostics.Errors;
                second.Send(new byte[] { 4 }, 1, target);
                Wait(() => server.Diagnostics.Errors > before, "逻辑对端满额必须明确诊断");
                Check(packets == 1 && server.SnapshotSubChannels().Length == 1 && server.State == FTRChannelState.Open,
                    "对端拒绝不能撑大目录或杀死监听");
            }
        }
        private static void SlowConsumerStopAndLeases()
        {
            int port = Port();
            using (var server = new FTRUdpServerChannel(new FTRUdpServerChannelConfiguration { Id = "udp-stop", Port = port,
                ReceiveBufferBytes = 16, ReceiveByteBudget = 32, QueueCapacity = 2, StopTimeoutMs = 100 }))
            using (var sender = new UdpClient()) using (var entered = new ManualResetEventSlim()) using (var release = new ManualResetEventSlim())
            {
                FTRReceiveBuffer retained = null; int count = 0;
                server.BufferReceived += buffer => {
                    if (Interlocked.Increment(ref count) == 1) { retained = buffer.Retain(); entered.Set(); release.Wait(); }
                };
                server.Open(); var target = new IPEndPoint(IPAddress.Loopback, port);
                try
                {
                    sender.Send(new byte[] { 71 }, 1, target); Check(entered.Wait(3000), "慢回调未进入");
                    for (int i = 0; i < 10; i++) sender.Send(new byte[] { 72 }, 1, target);
                    Wait(() => server.Diagnostics.ReceiveQueue.BackpressureWaits > 0, "有界池耗尽应可观察");
                    bool timeout = false;
                    try { server.Close(); } catch (TimeoutException) { timeout = true; }
                    Check(timeout && server.State == FTRChannelState.Faulted && server.Diagnostics.ReceiveQueue.AllocatedBytes <= 32,
                        "慢回调未结算不得假报Closed或超预算");
                }
                finally { release.Set(); }
                server.Close();
                Check(retained != null && retained.Segment.Array[0] == 71 && retained.RemoteEndpoint != null &&
                    server.Diagnostics.ReceiveQueue.BorrowedBuffers == 1, "跨Close租约及来源必须有效");
                retained.Dispose(); Check(server.Diagnostics.ReceiveQueue.AllocatedBytes == 0, "外部租约最终释放");
            }
        }
        private static void IdleGenerationAndObserver()
        {
            int port = Port();
            using (var server = new FTRUdpServerChannel(new FTRUdpServerChannelConfiguration { Id = "udp-idle", Port = port,
                ReceiveIdleTimeoutMs = 100, QueueCapacity = 2 }))
            using (var sender = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
            using (var entered = new ManualResetEventSlim()) using (var release = new ManualResetEventSlim())
            {
                FTRSubChannelAddress old = null, next = null; int packets = 0, removals = 0;
                server.PeerRemoved += _ => { throw new InvalidOperationException("故意的Peer观察器异常"); };
                server.PeerRemoved += _ => Interlocked.Increment(ref removals);
                server.BufferReceived += buffer => {
                    if (Interlocked.Increment(ref packets) == 1) { old = buffer.Address; entered.Set(); release.Wait(); }
                    else next = buffer.Address;
                };
                server.Open(); var target = new IPEndPoint(IPAddress.Loopback, port);
                try
                {
                    sender.Send(new byte[] { 1 }, 1, target); Check(entered.Wait(3000), "空闲测试收包未进入");
                    Wait(() => server.Diagnostics.ReceiveQueue.PendingNotifications > 0, "空闲移除通知应排在旧数据之后");
                    Check(removals == 0 && server.FindSubChannel(old) != null, "不得越过旧回调提前移除代次");
                }
                finally { release.Set(); }
                Wait(() => Volatile.Read(ref removals) == 1, "观察器异常不能阻断后继清理");
                sender.Send(new byte[] { 2 }, 1, target); Wait(() => next != null, "同Endpoint需可重新注册");
                Check(next.Generation > old.Generation && server.FindSubChannel(old) == null && server.State == FTRChannelState.Open,
                    "UDP重新出现的来源不能继承旧代次");
            }
            using (var quiet = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
            using (var client = new FTRUdpChannel(new FTRUdpChannelConfiguration { Id = "udp-client-idle",
                Port = ((IPEndPoint)quiet.Client.LocalEndPoint).Port, ReceiveIdleTimeoutMs = 60 }))
            { client.Open(); Wait(() => client.State == FTRChannelState.Faulted, "UDP Client空闲超时配置未生效"); }
        }
        private static void MulticastLoopback()
        {
            int port = Port();
            var config = new FTRUdpMulticastChannelConfiguration { Id = "udp-multicast-real", GroupAddress = "239.255.42.17",
                LocalInterface = "127.0.0.1", Port = port, Loopback = true, ReceiveByteBudget = 131072, QueueCapacity = 2 };
            var codec = new FTRChannelConfigurationConverter();
            Check(((FTRUdpMulticastChannelConfiguration)codec.Read(codec.Write(config))).LocalInterface == "127.0.0.1", "组播接口FCM往返");
            using (var multicast = new FTRUdpMulticastChannel(config)) using (var received = new ManualResetEventSlim())
            {
                bool valid = false;
                multicast.BufferReceived += buffer => { valid = buffer.ToArray().SequenceEqual(new byte[] { 19, 20 }) &&
                    buffer.RemoteEndpoint != null && Thread.CurrentThread.IsBackground && !Thread.CurrentThread.IsThreadPoolThread; received.Set(); };
                multicast.Open(); multicast.Send(new byte[] { 19, 20 });
                Check(received.Wait(4000) && valid, "真实IPv4组播本机回送、来源或接收线程失败");
                multicast.Close(); multicast.Open(); multicast.Close();
            }
        }
    }
}
