using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Mechanism.Connectivity
{
    // 直接使用 .NET Framework NamedPipeStream，保留原 MCC Channel 生命周期。
    public sealed class MCCNamedPipeClientChannel : MCCSingleChannelBase
    {
        private readonly MCCNamedPipeClientChannelConfiguration _config;
        private NamedPipeClientStream _pipe;
        private CancellationTokenSource _stop;
        private Task _reader;
        private readonly SemaphoreSlim _write = new SemaphoreSlim(1, 1);
        public MCCNamedPipeClientChannel(MCCChannelConfiguration config)
            : base((config ?? throw new ArgumentNullException("config")).Id)
        {
            config.Validate();
            _config = config as MCCNamedPipeClientChannelConfiguration ??
                throw new ArgumentException("NamedPipeClient 需要专用配置");
        }
        protected override async Task OnOpenAsync(CancellationToken ct)
        {
            NamedPipeClientStream pipe = new NamedPipeClientStream(_config.ServerName,
                _config.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            _pipe = pipe;
            await pipe.ConnectAsync(_config.ConnectTimeoutMs, ct).ConfigureAwait(false);
            _stop = new CancellationTokenSource();
            _reader = Task.Run(() => ReadLoop(pipe, _stop.Token));
        }
        private async Task ReadLoop(NamedPipeClientStream pipe, CancellationToken ct)
        {
            try
            {
                byte[] buffer = new byte[8192];
                while (!ct.IsCancellationRequested)
                {
                    int n = await pipe.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false);
                    if (n == 0) throw new IOException("NamedPipe 对端断开");
                    byte[] received = new byte[n];
                    Buffer.BlockCopy(buffer, 0, received, 0, n);
                    Receive(received);
                }
            }
            catch (Exception e) { if (!ct.IsCancellationRequested) Fault(e); }
        }
        protected override async Task OnSendAsync(byte[] bytes, MCCSubChannelAddress target, CancellationToken ct)
        {
            if (target.HasSubChannel) throw new ArgumentException("NamedPipe Client 不使用子通道");
            await _write.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                NamedPipeClientStream pipe = _pipe;
                if (pipe == null || !pipe.IsConnected) throw new IOException("NamedPipe 未连接");
                await pipe.WriteAsync(bytes, 0, bytes.Length, ct).ConfigureAwait(false);
                await pipe.FlushAsync(ct).ConfigureAwait(false);
            }
            finally { _write.Release(); }
        }
        protected override async Task OnCloseAsync(CancellationToken ct)
        {
            if (_stop != null) _stop.Cancel();
            if (_pipe != null) _pipe.Dispose();
            if (_reader != null) await _reader.ConfigureAwait(false);
            if (_stop != null) _stop.Dispose();
            _stop = null; _pipe = null; _reader = null;
        }
    }

    // 一个 Server Channel + N 个真实管道实例；子通道代次由现有 MCCSubChannelDirectory 管理。
    public sealed class MCCNamedPipeServerChannel : MCCMultiChannelBase
    {
        private readonly MCCNamedPipeServerChannelConfiguration _config;
        private readonly object _gate = new object();
        private readonly Dictionary<string, NamedPipeServerStream> _clients =
            new Dictionary<string, NamedPipeServerStream>(StringComparer.Ordinal);
        private readonly List<Task> _readers = new List<Task>();
        private readonly SemaphoreSlim _write = new SemaphoreSlim(1, 1);
        private NamedPipeServerStream _pending;
        private CancellationTokenSource _stop;
        private Task _accept;
        private long _peerCounter;
        public MCCNamedPipeServerChannel(MCCChannelConfiguration config)
            : base((config ?? throw new ArgumentNullException("config")).Id)
        {
            config.Validate();
            _config = config as MCCNamedPipeServerChannelConfiguration ??
                throw new ArgumentException("NamedPipeServer 需要专用配置");
        }
        protected override Task OnOpenAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _stop = new CancellationTokenSource();
            _accept = Task.Run(() => AcceptLoop(_stop.Token));
            return Task.CompletedTask;
        }
        private async Task AcceptLoop(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    NamedPipeServerStream pipe = new NamedPipeServerStream(_config.PipeName,
                        PipeDirection.InOut, _config.MaxClients, PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);
                    lock (_gate) _pending = pipe;
                    try { await pipe.WaitForConnectionAsync(ct).ConfigureAwait(false); }
                    catch
                    {
                        pipe.Dispose();
                        if (ct.IsCancellationRequested) break;
                        throw;
                    }
                    lock (_gate) _pending = null;
                    if (ct.IsCancellationRequested) { pipe.Dispose(); break; }
                    MCCSubChannelAddress address = null;
                    lock (_gate)
                    {
                        if (_clients.Count < _config.MaxClients)
                        {
                            string peerId = Interlocked.Increment(ref _peerCounter).ToString(
                                System.Globalization.CultureInfo.InvariantCulture);
                            address = RegisterPeer(peerId, "pipe:" + peerId).Address;
                            _clients.Add(peerId, pipe);
                            _readers.Add(Task.Run(() => ReadLoop(pipe, address, ct)));
                        }
                    }
                    if (address == null) pipe.Dispose();
                }
            }
            catch (Exception e) { if (!ct.IsCancellationRequested) Fault(e); }
        }
        private async Task ReadLoop(NamedPipeServerStream pipe,
            MCCSubChannelAddress address, CancellationToken ct)
        {
            try
            {
                byte[] buffer = new byte[8192];
                while (!ct.IsCancellationRequested)
                {
                    int n = await pipe.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false);
                    if (n == 0) break;
                    byte[] payload = new byte[n];
                    Buffer.BlockCopy(buffer, 0, payload, 0, n);
                    ReceiveFrom(address, payload);
                }
            }
            catch (Exception e) { if (!ct.IsCancellationRequested) Fault(e); }
            finally
            {
                lock (_gate) _clients.Remove(address.SubChannelId);
                RemovePeer(address);
                pipe.Dispose();
            }
        }
        protected override async Task SendToPeerAsync(byte[] bytes,
            MCCSubChannelAddress address, CancellationToken ct)
        {
            await _write.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                NamedPipeServerStream pipe;
                lock (_gate)
                {
                    if (!_clients.TryGetValue(address.SubChannelId, out pipe))
                        throw new IOException("NamedPipe 对端不存在或已经断开");
                }
                if (!pipe.IsConnected) throw new IOException("NamedPipe 对端已断开");
                await pipe.WriteAsync(bytes, 0, bytes.Length, ct).ConfigureAwait(false);
                await pipe.FlushAsync(ct).ConfigureAwait(false);
            }
            finally { _write.Release(); }
        }
        protected override async Task OnCloseAsync(CancellationToken ct)
        {
            if (_stop != null) _stop.Cancel();
            NamedPipeServerStream[] active;
            NamedPipeServerStream pending;
            Task[] readers;
            lock (_gate)
            {
                active = _clients.Values.ToArray();
                pending = _pending;
                readers = _readers.ToArray();
                _pending = null;
            }
            if (pending != null) pending.Dispose();
            foreach (NamedPipeServerStream pipe in active) pipe.Dispose();
            if (_accept != null) await _accept.ConfigureAwait(false);
            if (readers.Length > 0) await Task.WhenAll(readers).ConfigureAwait(false);
            lock (_gate) { _clients.Clear(); _readers.Clear(); }
            foreach (MCCSubChannelInfo peer in SnapshotSubChannels()) RemovePeer(peer.Address);
            if (_stop != null) _stop.Dispose();
            _stop = null; _accept = null;
        }
    }
}
