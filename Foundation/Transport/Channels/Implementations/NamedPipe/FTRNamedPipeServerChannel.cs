using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Transport
{
    /// <summary>固定Accept/Receive后台线程，不为每个连接建立读者Thread/Task。</summary>
    public sealed class FTRNamedPipeServerChannel : FTRMultiChannelBase
    {
        private sealed class Peer
        {
            internal NamedPipeServerStream Pipe;
            internal FTRSubChannelAddress Address;
            internal readonly object Write = new object();
            internal long LastReceive = Stopwatch.GetTimestamp();
            internal bool Ending;
        }
        private readonly FTRNamedPipeServerChannelConfiguration _config;
        private readonly object _gate = new object();
        private readonly Dictionary<string, Peer> _clients = new Dictionary<string, Peer>(StringComparer.Ordinal);
        private SemaphoreSlim _admission;
        private NamedPipeServerStream _pending;
        private CancellationTokenSource _stop;
        private Thread _accept, _reader;
        private long _peerCounter;
        public FTRNamedPipeServerChannel(FTRChannelConfiguration config)
            : base((config ?? throw new ArgumentNullException("config")).Id)
        {
            config.Validate(); _config = config as FTRNamedPipeServerChannelConfiguration ??
                throw new ArgumentException("NamedPipeServer需要专用配置");
        }
        protected override bool IsAdditionalTransportThread { get { return Thread.CurrentThread == _accept; } }
        private NamedPipeServerStream CreatePipe()
        {
            return new NamedPipeServerStream(_config.PipeName, PipeDirection.InOut, _config.MaxClients,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous,
                Math.Min(_config.ReceiveBufferBytes, _config.ReceiveByteBudget),
                Math.Min(_config.ReceiveBufferBytes, _config.SendByteBudget));
        }
        protected override Task OnOpenAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); _stop = new CancellationTokenSource();
            _admission = new SemaphoreSlim(_config.MaxClients, _config.MaxClients);
            _pending = CreatePipe(); // Open返回前真实创建第一监听实例，错误不能伪报已打开。
            StartBufferedReceive(_config);
            _accept = new Thread(AcceptLoop) { IsBackground = true, Name = "FTR.Accept." + Id };
            _reader = new Thread(ReadLoop) { IsBackground = true, Name = "FTR.Receive." + Id };
            _accept.Start(); _reader.Start(); return Task.CompletedTask;
        }
        private void AcceptLoop()
        {
            var ct = _stop.Token;
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    _admission.Wait(ct); bool transferred = false; NamedPipeServerStream pipe = null;
                    try
                    {
                        lock (_gate) pipe = _pending;
                        if (pipe == null) pipe = CreatePipe();
                        lock (_gate) _pending = pipe;
                        pipe.WaitForConnectionAsync(ct).GetAwaiter().GetResult();
                        ct.ThrowIfCancellationRequested();
                        lock (_gate)
                        {
                            if (_peerCounter == long.MaxValue) throw new InvalidOperationException("NamedPipe peer identity exhausted");
                            string id = (++_peerCounter).ToString(System.Globalization.CultureInfo.InvariantCulture);
                            var peer = new Peer { Pipe = pipe, Address = RegisterPeer(id, "pipe:" + id).Address };
                            _clients.Add(id, peer); _pending = null; transferred = true;
                        }
                    }
                    finally
                    {
                        if (!transferred)
                        {
                            if (pipe != null) pipe.Dispose();
                            lock (_gate) if (ReferenceEquals(_pending, pipe)) _pending = null;
                            _admission.Release();
                        }
                    }
                }
            }
            catch (Exception error) { if (!ct.IsCancellationRequested) Fault(error); }
        }
        private void ReadLoop()
        {
            var ct = _stop.Token; EnterReceiveThread(); long lastProgress = Stopwatch.GetTimestamp();
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    Peer[] peers; lock (_gate) peers = _clients.Values.Where(peer => !peer.Ending).ToArray();
                    bool progressed = false;
                    foreach (Peer peer in peers)
                    {
                        if (ct.IsCancellationRequested) break;
                        FTRReceiveBuffer buffer = null;
                        try
                        {
                            int available = FTRPipeIO.Available(peer.Pipe);
                            if (available < 0) { EndPeer(peer, null); continue; }
                            if (available == 0)
                            {
                                if (FTRPipeIO.Idle(peer.LastReceive, _config.ReceiveIdleTimeoutMs))
                                    EndPeer(peer, new TimeoutException("NamedPipe peer receive idle"));
                                continue;
                            }
                            buffer = RentReceiveBuffer(ct);
                            int count = peer.Pipe.Read(buffer.WritableBuffer, 0, Math.Min(available, buffer.WritableBuffer.Length));
                            if (count == 0) { EndPeer(peer, null); continue; }
                            peer.LastReceive = Stopwatch.GetTimestamp(); progressed = true;
                            PublishReceiveBuffer(peer.Address, buffer, count, "pipe:" + peer.Address.SubChannelId); buffer = null;
                        }
                        catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                        catch (Exception error) { if (!ct.IsCancellationRequested) EndPeer(peer, error); }
                        finally { if (buffer != null) buffer.Dispose(); }
                    }
                    if (progressed) lastProgress = Stopwatch.GetTimestamp();
                    else FTRPipeIO.WaitForProgress(ct, lastProgress);
                }
            }
            catch (Exception error) { if (!ct.IsCancellationRequested) Fault(error); }
        }
        private void EndPeer(Peer peer, Exception error)
        {
            lock (_gate) { if (peer.Ending) return; peer.Ending = true; }
            if (error != null) RecordReceiveError(error);
            AfterReceived(() => RemovePeer(peer.Address),_stop.Token);
        }
        public override bool RemovePeer(FTRSubChannelAddress address)
        {
            if (address == null) return false;
            Peer peer; SemaphoreSlim slots;
            lock (_gate)
            {
                if (!RemovePeerDirectory(address)) return false;
                _clients.TryGetValue(address.SubChannelId, out peer);
                if (peer != null) { peer.Ending = true; _clients.Remove(address.SubChannelId); }
                slots = _admission;
            }
            if (peer != null) { try { peer.Pipe.Dispose(); } finally { slots.Release(); } }
            NotifyPeerRemoved(address); return true;
        }
        protected override void OnSendSynchronous(byte[] bytes, int offset, int count,
            FTRSubChannelAddress address, CancellationToken ct, int segmentBytes)
        {
            if (FindSubChannel(address) == null) throw new InvalidOperationException("NamedPipe旧代次或已断开的SubChannel");
            Peer peer; lock (_gate) if (!_clients.TryGetValue(address.SubChannelId, out peer)) throw new IOException("NamedPipe peer disconnected");
            FTRPipeIO.Write(peer.Pipe, peer.Write, bytes, offset, count, segmentBytes, _config.SendTimeoutMs, ct);
        }
        protected override Task SendToPeerAsync(byte[] bytes, FTRSubChannelAddress address, CancellationToken ct)
        { return Task.Run(() => OnSendSynchronous(bytes, 0, bytes.Length, address, ct, 0), ct); }
        protected override Task OnCloseAsync(CancellationToken ct)
        {
            return Task.Run(() => {
                var timer = Stopwatch.StartNew(); if (_stop != null) _stop.Cancel();
                NamedPipeServerStream pending; Peer[] peers;
                lock (_gate) { pending = _pending; _pending = null; peers = _clients.Values.ToArray(); }
                if (pending != null) pending.Dispose(); foreach (Peer peer in peers) peer.Pipe.Dispose();
                FTRTransportThread.Join(_accept, timer, _config.StopTimeoutMs, ct);
                // 结算最后Accept后，再关闭其可能刚转交的实例。
                lock (_gate) peers = _clients.Values.ToArray(); foreach (Peer peer in peers) peer.Pipe.Dispose();
                FTRTransportThread.Join(_reader, timer, _config.StopTimeoutMs, ct);
                WaitForPendingSends(FTRTransportThread.Remaining(timer, _config.StopTimeoutMs), ct);
                DrainBufferedReceive(FTRTransportThread.Remaining(timer, _config.StopTimeoutMs), ct);
                foreach (FTRSubChannelInfo peer in SnapshotSubChannels()) RemovePeer(peer.Address);
                if (_stop != null) _stop.Dispose(); _stop = null; _accept = null; _reader = null;
                // SemaphoreSlim从未请求AvailableWaitHandle；显式Remove可并发归还旧代额度，旧门只保留托管状态待GC。
                _admission = null;
            });
        }
    }
}
