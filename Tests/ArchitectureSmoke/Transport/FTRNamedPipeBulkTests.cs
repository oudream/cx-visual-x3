using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Transport;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class FTRNamedPipeBulkTests
    {
        private static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException("F17.PIPE_BULK " + message); }
        private static void Wait(Func<bool> condition, string message, int timeout = 10000)
        { Check(SpinWait.SpinUntil(condition, timeout), message); }
        private static string Name() { return "f17-bulk-" + Guid.NewGuid().ToString("N"); }
        private static string ReleaseEventName(string pipeName) { return @"Local\CxVisual.F17.PipeRelease." + pipeName; }
        private static FTRNamedPipeServerChannelConfiguration Config(string name)
        { return new FTRNamedPipeServerChannelConfiguration { Id = "pipe-bulk-server", PipeName = name, MaxClients = 16,
            ReceiveBufferBytes = 65536, ReceiveByteBudget = 262144, QueueCapacity = 4, CapturePayloadEvidence = false,
            SendTimeoutMs = 30000, StopTimeoutMs = 3000 }; }
        private static byte Pattern(long offset, int salt) { return (byte)((offset + salt) % 251); }
        private static void SendVolume(IFTRChannel channel, FTRSubChannelAddress address, long bytes, int salt)
        {
            byte[] block = new byte[65536 + 8];
            for (long sent = 0; sent < bytes;)
            {
                int count = (int)Math.Min(65536, bytes - sent);
                for (int i = 0; i < count; i++) block[8 + i] = Pattern(sent + i, salt);
                channel.Send(block, 8, count, address, CancellationToken.None); sent += count;
            }
        }
        private sealed class Checker
        {
            private readonly int _salt; private long _bytes; private string _error;
            internal bool Dedicated = true;
            internal Checker(int salt) { _salt = salt; }
            internal long Bytes { get { return Interlocked.Read(ref _bytes); } }
            internal string Error { get { return _error; } }
            internal void Receive(FTRReceiveBuffer buffer)
            {
                long offset = Bytes; var view = buffer.Segment;
                Dedicated &= Thread.CurrentThread.IsBackground && !Thread.CurrentThread.IsThreadPoolThread;
                for (int i = 0; i < view.Count; i++)
                    if (view.Array[view.Offset + i] != Pattern(offset + i, _salt))
                    { _error = "byte mismatch at " + (offset + i); break; }
                Interlocked.Add(ref _bytes, view.Count);
            }
        }
        public static void Run()
        {
            CrossProcessDuplex(8 * 1024 * 1024L);
            MultiplePeersAndIndependentSends();
            SlowConsumerAndTail();
            IdleAndCallbackClose();
            ReconnectSnapshotAndCancellation();
            Console.WriteLine("F17.PIPE_BULK PASS: cross-process duplex integrity, fixed multi-peer receiver, per-peer sync writes, pooled stop/leases, tail and idle");
        }
        public static void RunVolumeScale()
        {
            CrossProcessDuplex(2147483648L);
            Console.WriteLine("F17.PIPE_VOLUME_PASS bytesEachDirection=2147483648 crossProcess=true streamedWithoutWholeVolumeAllocation=true");
        }
        public static int RunChild(string pipeName, string bytesArgument)
        {
            long bytes;
            if (!long.TryParse(bytesArgument, NumberStyles.None, CultureInfo.InvariantCulture, out bytes) ||
                bytes < 1 || bytes > 2147483648L) throw new ArgumentException("Pipe volume child byte limit");
            using (var client = new FTRNamedPipeClientChannel(new FTRNamedPipeClientChannelConfiguration { Id = "pipe-bulk-child",
                PipeName = pipeName, ReceiveByteBudget = 262144, QueueCapacity = 4, CapturePayloadEvidence = false,
                SendTimeoutMs = 30000, StopTimeoutMs = 3000 }))
            {
                var checker = new Checker(19); client.BufferReceived += checker.Receive; client.Open();
                var process = Process.GetCurrentProcess(); TimeSpan cpu = process.TotalProcessorTime;
                long managed = GC.GetTotalMemory(false); int gen0 = GC.CollectionCount(0), gen2 = GC.CollectionCount(2);
                var timer = Stopwatch.StartNew();
                SendVolume(client, new FTRSubChannelAddress(client.Id), bytes, 7);
                Wait(() => checker.Bytes == bytes, "child receive incomplete", 120000);
                Check(checker.Error == null && checker.Dedicated, "child " + checker.Error);
                var snapshot = client.Diagnostics;
                Check(snapshot.ReceiveQueue.AllocatedBytes <= 262144 && snapshot.SentBytes == bytes && snapshot.ReceivedBytes == bytes &&
                    snapshot.ReceiveQueue.ReceiveThreadId != snapshot.ReceiveQueue.DeliveryThreadId, "child budgets/thread/counters");
                // 父进程确认也收齐后经stdin释放进程，避免进程退出截断尚未取走的尾部。
                client.BufferReceived -= checker.Receive;
                Console.WriteLine("PIPE_CHILD_PASS pid=" + Process.GetCurrentProcess().Id + " bytes=" + bytes +
                    " poolBytes=" + snapshot.ReceiveQueue.AllocatedBytes + " elapsedMs=" + timer.ElapsedMilliseconds +
                    " cpuMs=" + (process.TotalProcessorTime - cpu).TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture) +
                    " managedDelta=" + (GC.GetTotalMemory(false) - managed) + " workingSet=" + process.WorkingSet64 +
                    " gc0=" + (GC.CollectionCount(0) - gen0) + " gc2=" + (GC.CollectionCount(2) - gen2));
                // 控制台父进程经stdin完成同步，数据本身只走NamedPipe。
                using (var release = new EventWaitHandle(false, EventResetMode.ManualReset, ReleaseEventName(pipeName)))
                    if (!release.WaitOne(10000)) throw new IOException("Pipe child release event timed out");
                client.Close();
            }
            return 0;
        }
        private static void CrossProcessDuplex(long bytes)
        {
            string name = Name();
            using (var release = new EventWaitHandle(false, EventResetMode.ManualReset, ReleaseEventName(name)))
            using (var server = new FTRNamedPipeServerChannel(Config(name)))
            {
                var checker = new Checker(7); server.BufferReceived += checker.Receive; server.Open();
                var start = new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,
                    "--channel-pipe-child " + name + " " + bytes.ToString(CultureInfo.InvariantCulture)) {
                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true,
                    RedirectStandardError = true };
                using (var child = Process.Start(start))
                {
                    Check(child != null && child.Id != Process.GetCurrentProcess().Id, "not a real child process");
                    Task<string> childLine = child.StandardOutput.ReadLineAsync(); Task<string> errors = child.StandardError.ReadToEndAsync();
                    Stopwatch timer = Stopwatch.StartNew(); TimeSpan cpu = Process.GetCurrentProcess().TotalProcessorTime;
                    long managed = GC.GetTotalMemory(false); int gen0 = GC.CollectionCount(0), gen2 = GC.CollectionCount(2);
                    try
                    {
                        Wait(() => server.SnapshotSubChannels().Length == 1 || child.HasExited, "child did not connect");
                        if (child.HasExited) throw new IOException("pipe child startup failed: " + errors.GetAwaiter().GetResult());
                        FTRSubChannelAddress address = server.SnapshotSubChannels().Single().Address;
                        SendVolume(server, address, bytes, 19);
                        Wait(() => checker.Bytes == bytes || child.HasExited, "parent receive incomplete", 120000);
                        Check(checker.Bytes == bytes && checker.Error == null && checker.Dedicated, "parent stream integrity: " + checker.Error);
                        Check(childLine.Wait(120000) && childLine.Result.StartsWith("PIPE_CHILD_PASS", StringComparison.Ordinal),
                            "child did not verify full stream");
                        release.Set();
                        Check(child.WaitForExit(10000) && child.ExitCode == 0,
                             "child exit failure; exit=" + (child.HasExited ? child.ExitCode.ToString() : "timeout") +
                             "; stderr=" + (child.HasExited ? errors.GetAwaiter().GetResult() : "child still running"));
                        Check(errors.GetAwaiter().GetResult().Length == 0, "child stderr not empty");
                        var snapshot = server.Diagnostics;
                        Check(snapshot.ReceiveQueue.AllocatedBytes <= 262144 && snapshot.SentBytes == bytes && snapshot.ReceivedBytes == bytes &&
                            snapshot.ReceiveQueue.ReceiveThreadId != snapshot.ReceiveQueue.DeliveryThreadId, "parent budgets/thread/counters");
                        Console.WriteLine("F17.PIPE_METRIC bytesEachDirection=" + bytes + " elapsedMs=" + timer.ElapsedMilliseconds +
                            " parentCpuMs=" + (Process.GetCurrentProcess().TotalProcessorTime - cpu).TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture) +
                            " parentManagedDelta=" + (GC.GetTotalMemory(false) - managed) + " parentWorkingSet=" + Process.GetCurrentProcess().WorkingSet64 +
                            " gc0=" + (GC.CollectionCount(0) - gen0) + " gc2=" + (GC.CollectionCount(2) - gen2) +
                            " parentPoolBytes=" + snapshot.ReceiveQueue.AllocatedBytes + " backpressure=" + snapshot.ReceiveQueue.BackpressureWaits +
                            " verifiedAggregateMiBPerSecond=" + ((bytes * 2.0 / 1048576) / timer.Elapsed.TotalSeconds).ToString("F2", CultureInfo.InvariantCulture) +
                            " parentPid=" + Process.GetCurrentProcess().Id + " " + childLine.Result);
                    }
                    finally { if (!child.HasExited) { child.Kill(); child.WaitForExit(5000); } }
                }
                server.Close();
            }
        }
        private static void MultiplePeersAndIndependentSends()
        {
            string name = Name(); var config = Config(name); config.SendTimeoutMs = 1500;
            using (var server = new FTRNamedPipeServerChannel(config))
            {
                var peers = new NamedPipeClientStream[12]; var addresses = new Dictionary<byte, FTRSubChannelAddress>();
                object gate = new object(); int delivery = 0;
                server.BufferReceived += buffer => {
                    var data = buffer.Segment;
                    lock (gate) { addresses[data.Array[data.Offset]] = buffer.Address; delivery = Thread.CurrentThread.ManagedThreadId; }
                };
                server.Open();
                try
                {
                    for (byte i = 0; i < peers.Length; i++)
                    {
                        peers[i] = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
                        peers[i].Connect(3000); peers[i].WriteByte(i);
                    }
                    Wait(() => { lock (gate) return addresses.Count == 12; }, "twelve pipe sources not received");
                    FTRSubChannelAddress slow, fast; lock (gate) { slow = addresses[0]; fast = addresses[1]; }
                    Task blocked = Task.Run(() => {
                        try { server.Send(new byte[8 * 1024 * 1024], 0, 8 * 1024 * 1024, slow, CancellationToken.None); }
                        catch (TimeoutException) { return; }
                        throw new InvalidOperationException("slow peer write should reach actual deadline");
                    });
                    Wait(() => server.Diagnostics.PendingSends > 0, "blocked peer send did not enter");
                    Task<int> firstByte = Task.Run(() => peers[0].ReadByte());
                    Check(firstByte.Wait(3000) && firstByte.Result == 0, "slow peer OS write did not actually start");
                    Task<int> reply = Task.Run(() => peers[1].ReadByte());
                    server.Send(new byte[] { 73 }, 0, 1, fast, CancellationToken.None);
                    Check(reply.Wait(1000) && reply.Result == 73 && !blocked.IsCompleted, "one stalled pipe must not hold another peer's write gate");
                    Check(blocked.Wait(5000), "pipe write deadline did not stop blocked OS write");
                    FTRSubChannelAddress cancelAddress; lock (gate) cancelAddress = addresses[2];
                    using (var cancellation = new CancellationTokenSource())
                    {
                        Task cancelled = Task.Run(() => {
                            try { server.Send(new byte[8 * 1024 * 1024], 0, 8 * 1024 * 1024, cancelAddress, cancellation.Token); }
                            catch (OperationCanceledException) { return; }
                            throw new InvalidOperationException("cancelled pipe write did not report cancellation");
                        });
                        Task<int> started = Task.Run(() => peers[2].ReadByte());
                        Check(started.Wait(1000) && started.Result == 0, "cancelled OS write did not actually start");
                        cancellation.Cancel(); Check(cancelled.Wait(3000), "cancelled pipe write did not settle");
                    }
                    var snapshot = server.Diagnostics;
                    Check(snapshot.ReceiveQueue.DeliveryThreadId == delivery && snapshot.ReceiveQueue.ReceiveThreadId != delivery &&
                        !snapshot.ReceiveQueue.MultipleReceiveThreadsObserved &&
                        snapshot.ReceiveQueue.AllocatedBytes <= 262144 && server.State == FTRChannelState.Open, "fixed receiver and listener survival");
                    Console.WriteLine("F17.PIPE_MULTIPEER clients=12 receiveThread=" + snapshot.ReceiveQueue.ReceiveThreadId + " deliveryThread=" + delivery);
                }
                finally { foreach (var peer in peers) if (peer != null) peer.Dispose(); }
            }
        }
        private static void SlowConsumerAndTail()
        {
            string name = Name(); var config = Config(name); config.QueueCapacity = 2; config.ReceiveByteBudget = 131072; config.StopTimeoutMs = 100;
            using (var server = new FTRNamedPipeServerChannel(config))
            using (var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous))
            using (var entered = new ManualResetEventSlim()) using (var release = new ManualResetEventSlim())
            {
                FTRReceiveBuffer retained = null; bool first = true;
                server.BufferReceived += buffer => {
                    if (first) { first = false; retained = buffer.Retain(); entered.Set(); release.Wait(); }
                };
                server.Open(); client.Connect(3000); client.WriteByte(91);
                try
                {
                    Check(entered.Wait(3000), "slow callback did not enter");
                    client.WriteByte(92); client.WriteByte(93);
                    Wait(() => server.Diagnostics.ReceiveQueue.BorrowedBuffers == 2, "receive leases not budgeted");
                    bool timeout = false; try { server.Close(); } catch (TimeoutException) { timeout = true; }
                    Check(timeout && server.State == FTRChannelState.Faulted && server.Diagnostics.ReceiveQueue.AllocatedBytes <= 131072,
                        "unsettled callback cannot report Closed");
                    Check(!server.Diagnostics.ReceiveQueue.ReceiveThreadAlive && server.Diagnostics.ReceiveQueue.DeliveryThreadAlive,
                        "Close must stop the OS receiver even while an admitted callback is unsettled");
                }
                finally { release.Set(); }
                server.Close();
                Check(retained != null && retained.Segment.Array[0] == 91 && server.Diagnostics.ReceiveQueue.BorrowedBuffers == 1,
                    "retained pipe memory across close"); retained.Dispose();
                Check(server.Diagnostics.ReceiveQueue.AllocatedBytes == 0, "retained pipe budget not returned");
            }
            name = Name();
            using (var server = new FTRNamedPipeServerChannel(Config(name)))
            using (var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous))
            using (var entered = new ManualResetEventSlim()) using (var release = new ManualResetEventSlim())
            {
                int received = 0, removed = 0;
                server.BufferReceived += buffer => { Interlocked.Add(ref received, buffer.Count); entered.Set(); release.Wait(); };
                server.PeerRemoved += _ => Interlocked.Increment(ref removed);
                server.Open(); client.Connect(3000); client.Write(new byte[8192], 0, 8192);
                try
                {
                    Check(entered.Wait(3000), "tail receive did not enter"); client.Dispose();
                    Wait(() => server.Diagnostics.ReceiveQueue.PendingNotifications > 0, "EOF should queue after admitted tail");
                    Check(removed == 0, "Protocol reset overtook queued tail");
                }
                finally { release.Set(); }
                Wait(() => removed == 1, "tail peer cleanup missing"); Check(received == 8192, "pipe tail lost");
            }
        }
        private static void IdleAndCallbackClose()
        {
            string name = Name(); var config = Config(name); config.ReceiveIdleTimeoutMs = 80;
            using (var server = new FTRNamedPipeServerChannel(config))
            using (var raw = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous))
            {
                bool rejected = false; server.BufferReceived += _ => {
                    try { server.Close(); } catch (InvalidOperationException) { rejected = true; }
                };
                server.Open(); raw.Connect(3000); raw.WriteByte(6);
                Wait(() => rejected, "callback self-close was not rejected");
                Wait(() => server.SnapshotSubChannels().Length == 0, "idle pipe peer was not cleared");
                Check(server.State == FTRChannelState.Open, "idle peer killed pipe listener");
            }
            name = Name();
            using (var raw = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
            using (var client = new FTRNamedPipeClientChannel(new FTRNamedPipeClientChannelConfiguration { PipeName = name, ReceiveIdleTimeoutMs = 80 }))
            {
                bool selfCloseRejected = false;
                client.StateChanged += state => { if (state == FTRChannelState.Faulted)
                    try { client.Close(); } catch (InvalidOperationException) { selfCloseRejected = true; } };
                var accepting = raw.WaitForConnectionAsync(); client.Open(); accepting.GetAwaiter().GetResult();
                Wait(() => selfCloseRejected, "fault before first buffer must still identify its own receiver thread");
                client.Close();
            }
        }
        private static void ReconnectSnapshotAndCancellation()
        {
            string name = Name();
            using (var server = new FTRNamedPipeServerChannel(Config(name)))
            using (var client = new FTRNamedPipeClientChannel(new FTRNamedPipeClientChannelConfiguration { Id = "pipe-reconnect", PipeName = name,
                CapturePayloadEvidence = false }))
            using (var opening = new CancellationTokenSource()) using (var sending = new CancellationTokenSource())
            {
                long received = 0; int corrupt = 0; FTRSubChannelAddress first = null, last = null;
                server.BufferReceived += buffer => {
                    if (first == null) first = buffer.Address; last = buffer.Address;
                    var view = buffer.Segment;
                    server.Send(view.Array, view.Offset, view.Count, buffer.Address, CancellationToken.None);
                };
                client.BufferReceived += buffer => {
                    var view = buffer.Segment;
                    for (int i = 0; i < view.Count; i++) if (view.Array[view.Offset + i] != 8) Interlocked.Exchange(ref corrupt, 1);
                    Interlocked.Add(ref received, view.Count);
                };
                server.Open(); client.OpenAsync(opening.Token).GetAwaiter().GetResult(); opening.Cancel();
                client.Send(new byte[] { 8, 8 }); Wait(() => Interlocked.Read(ref received) == 2, "Open token cancelled established pipe");
                client.ReconnectAsync(CancellationToken.None).GetAwaiter().GetResult();
                client.Send(new byte[] { 8, 8, 8 }); Wait(() => Interlocked.Read(ref received) == 5, "explicit pipe reconnect failed");
                Wait(() => server.FindSubChannel(first) == null, "old pipe generation not removed");
                Check(last != null && last.Generation > first.Generation, "pipe reconnect inherited old identity");
                byte[] bytes = Enumerable.Repeat((byte)8, 65536).ToArray();
                Task write = client.SendAsync(bytes, new FTRSubChannelAddress(client.Id), sending.Token); Array.Clear(bytes, 0, bytes.Length);
                write.GetAwaiter().GetResult(); sending.Cancel();
                client.Send(new byte[] { 8 });
                Wait(() => Interlocked.Read(ref received) == 65542, "async snapshot or late write-token cancellation corrupted data");
                Check(corrupt == 0 && client.State == FTRChannelState.Open, "async pipe payload ownership or token lifetime");
                bool cancelled = false;
                try { client.Send(new byte[] { 8 }, 0, 1, new FTRSubChannelAddress(client.Id), new CancellationToken(true)); }
                catch (OperationCanceledException) { cancelled = true; }
                Check(cancelled && client.Diagnostics.SentBytes == 65542, "pre-cancelled pipe send had a side effect");
            }
            using (var missing = new FTRNamedPipeClientChannel(new FTRNamedPipeClientChannelConfiguration {
                PipeName = Name(), ConnectTimeoutMs = 100 }))
            {
                bool timeout = false; try { missing.Open(); } catch (TimeoutException) { timeout = true; }
                Check(timeout && missing.State == FTRChannelState.Faulted, "pipe connect timeout did not fail honestly");
                missing.Close(); Check(missing.State == FTRChannelState.Closed, "failed pipe open resources could not close");
            }
        }
    }
}
