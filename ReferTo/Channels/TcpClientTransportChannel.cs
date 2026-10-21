using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Core;
using CxVisual.Platform.Orchestration.Resources;

namespace CxVisual.Platform.Channels
{
    public sealed class TcpClientTransportChannel : IChannel
    {
        private sealed class Listener
        {
            internal readonly object Gate = new object();
            internal string Id; internal Action<byte[]> Callback; internal bool Active = true; internal int Calls;
        }
        private static readonly AsyncLocal<Listener> CurrentListener = new AsyncLocal<Listener>();
        private readonly object _gate = new object();
        private readonly SemaphoreSlim _connectGate = new SemaphoreSlim(1, 1), _sendGate = new SemaphoreSlim(1, 1), _readGate = new SemaphoreSlim(1, 1);
        private readonly Dictionary<string, Listener> _listeners = new Dictionary<string, Listener>(StringComparer.Ordinal);
        private readonly CxChannelDescriptor _descriptor;
        private readonly ICxClock _clock;
        private readonly TimeSpan _connectTimeout;
        private readonly ChannelHealth _health = new ChannelHealth();
        private readonly ChannelDiagnostics _diagnostics = new ChannelDiagnostics();
        private readonly CxChannelDiagnostics _frameDiagnostics = new CxChannelDiagnostics();
        private readonly TimeSpan _started;
        private DateTime? _lastSuccess;
        private TimeSpan _lastLatency;
        private long _reconnectCount;
        private bool _reconnecting;
        private TcpClient _client, _candidate;
        private CancellationTokenSource _openingSource;
        private NetworkStream _stream;
        private Task _pump = Task.CompletedTask, _closing = Task.CompletedTask, _notifications = Task.CompletedTask;
        private long _generation;
        private bool _disposed, _closeRequested;
        public TcpClientTransportChannel(string channelId, ChannelEndpoint endpoint, TimeSpan connectTimeout)
            : this(CxChannelDescriptor.FromLegacy(channelId, endpoint ?? throw new ArgumentNullException(nameof(endpoint))), connectTimeout, null) { }
        public TcpClientTransportChannel(CxChannelDescriptor descriptor, TimeSpan connectTimeout, ICxClock clock = null)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            _descriptor = descriptor.Clone();
            if (!new CxChannelFactory().CanCreate(_descriptor)) throw new NotSupportedException("TCP_CHANNEL_CONFIGURATION_NOT_SUPPORTED");
            _connectTimeout = connectTimeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(5) : connectTimeout;
            _clock = clock ?? CxSystemClock.Shared;
            _started = _clock.MonotonicNow;
            _health.State = ChannelHealthState.Closed; _health.ChangedUtc = _clock.UtcNow;
        }
        public string Id => _descriptor.Id;
        public string ChannelId => Id;
        public CxChannelDescriptor Descriptor => _descriptor.Clone();
        public ChannelEndpoint Endpoint
        {
            get { var result = new ChannelEndpoint("tcp", _descriptor.Endpoint.Address, _descriptor.Endpoint.Port); foreach (var pair in _descriptor.Endpoint.Parameters) result.Settings[pair.Key] = pair.Value; return result; }
        }
        public CxChannelState State
        {
            get { lock (_gate) return _closeRequested && !_closing.IsCompleted ? CxChannelState.Closing : _reconnecting ? CxChannelState.Reconnecting : Map(_health.State); }
        }
        /// <summary>Rates are bytes per second averaged over this instance's monotonic lifetime.</summary>
        public CxChannelHealth HealthSnapshot
        {
            get { lock (_gate) { var seconds = (_clock.MonotonicNow - _started).TotalSeconds; return new CxChannelHealth { State = State, LastSuccess = _lastSuccess,
                LastError = _health.LastError, Latency = _lastLatency, SendRate = seconds <= 0 ? 0 : _diagnostics.SentBytes / seconds,
                ReceiveRate = seconds <= 0 ? 0 : _diagnostics.ReceivedBytes / seconds, ReconnectCount = _reconnectCount }; } }
        }
        public CxChannelDiagnostics FrameDiagnostics => _frameDiagnostics.Snapshot();
        public ChannelHealth Health { get { lock (_gate) return CopyHealth(); } }
        public ChannelDiagnostics Diagnostics
        {
            get { lock (_gate) return new ChannelDiagnostics { ConnectCount = _diagnostics.ConnectCount, DisconnectCount = _diagnostics.DisconnectCount, SendCount = _diagnostics.SendCount,
                ReceiveCount = _diagnostics.ReceiveCount, SentBytes = _diagnostics.SentBytes, ReceivedBytes = _diagnostics.ReceivedBytes, FailureCount = _diagnostics.FailureCount,
                LastSendUtc = _diagnostics.LastSendUtc, LastReceiveUtc = _diagnostics.LastReceiveUtc, LastError = _diagnostics.LastError }; }
        }
        public event Action<ChannelHealth> HealthChanged;
        public Task OpenAsync(CancellationToken token) => ConnectAsync(token);
        public async Task ConnectAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); long generation;
            lock (_gate) { CheckUsable(); if (_stream != null) return; generation = _generation; }
            await _connectGate.WaitAsync(token).ConfigureAwait(false);
            CancellationTokenSource opening = null;
            try
            {
                lock (_gate) { CheckUsable(); opening = CancellationTokenSource.CreateLinkedTokenSource(token); _openingSource = opening; }
                token = opening.Token;
                int attempts = 0;
                while (true)
                {
                    lock (_gate) { CheckUsable(); if (generation != _generation) throw new OperationCanceledException("CHANNEL_OPEN_SUPERSEDED"); if (_stream != null) return; if (attempts > 0) _reconnectCount++; SetHealth(ChannelHealthState.Connecting, null); }
                    var candidate = new TcpClient(); Task connecting = null;
                    lock (_gate) { if (generation != _generation) { candidate.Close(); throw new OperationCanceledException("CHANNEL_OPEN_SUPERSEDED"); } _candidate = candidate; }
                    try
                    {
                        connecting = candidate.ConnectAsync(_descriptor.Endpoint.Address, _descriptor.Endpoint.Port);
                        Observe(connecting);
                        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
                        {
                            var delay = _clock.DelayAsync(_connectTimeout, timeout.Token);
                            var completed = await Task.WhenAny(connecting, delay).ConfigureAwait(false);
                            token.ThrowIfCancellationRequested();
                            if (completed != connecting) throw new TimeoutException("TCP_CONNECT_TIMEOUT");
                            timeout.Cancel(); await connecting.ConfigureAwait(false);
                        }
                        candidate.NoDelay = true; candidate.ReceiveBufferSize = _descriptor.BufferPolicy.Capacity;
                        var stream = candidate.GetStream();
                        lock (_gate)
                        {
                            CheckUsable(); if (generation != _generation) throw new OperationCanceledException("CHANNEL_OPEN_SUPERSEDED");
                            _client = candidate; _stream = stream; _candidate = null; candidate = null;
                            _diagnostics.ConnectCount++; _reconnecting = false; _lastSuccess = _clock.UtcNow; SetHealth(ChannelHealthState.Online, null); StartPump();
                        }
                        return;
                    }
                    catch (Exception error)
                    {
                        bool retry;
                        lock (_gate)
                        {
                            if (generation != _generation || _disposed || _closeRequested) throw new OperationCanceledException("CHANNEL_OPEN_SUPERSEDED", error);
                            _diagnostics.FailureCount++; _diagnostics.LastError = error.Message; SetHealth(ChannelHealthState.Faulted, error.Message);
                            retry = !(error is OperationCanceledException) && _descriptor.ReconnectPolicy.ShouldRetry(attempts);
                            _reconnecting = retry;
                        }
                        if (!retry) throw;
                        await _clock.DelayAsync(_descriptor.ReconnectPolicy.GetDelay(attempts++), token).ConfigureAwait(false);
                    }
                    finally { if (candidate != null) candidate.Close(); lock (_gate) if (ReferenceEquals(_candidate, candidate)) _candidate = null; }
                }
            }
            catch (OperationCanceledException)
            {
                lock (_gate) if (generation == _generation && !_closeRequested && !_disposed)
                { _generation++; _reconnecting = false; CloseSockets(); SetHealth(ChannelHealthState.Closed, "CHANNEL_OPEN_CANCELLED"); }
                throw;
            }
            finally { lock (_gate) if (ReferenceEquals(_openingSource, opening)) _openingSource = null; opening?.Dispose(); _connectGate.Release(); }
        }
        public Task DisconnectAsync(CancellationToken token) => CloseAsync(token);
        public Task CloseAsync(CancellationToken token)
        {
            if (CurrentListener.Value != null) throw new InvalidOperationException("CHANNEL_CALLBACK_CANNOT_WAIT_FOR_CLOSE");
            return CxResourceAwait.WaitAsync(BeginClose(false), token);
        }
        public void Close() { Observe(BeginClose(false)); }
        public void Dispose() { Observe(BeginClose(true)); }
        public Task DisposeAsync() => BeginClose(true);
        private Task BeginClose(bool dispose)
        {
            lock (_gate)
            {
                _disposed |= dispose;
                if (_closeRequested && !_closing.IsCompleted) return _closing;
                if (_stream == null && _candidate == null && _openingSource == null && _pump.IsCompleted &&
                    _connectGate.CurrentCount == 1 && _sendGate.CurrentCount == 1 && _readGate.CurrentCount == 1)
                {
                    _generation++; _reconnecting = false;
                    if (_health.State != ChannelHealthState.Closed || _health.LastError != null) SetHealth(ChannelHealthState.Closed, null);
                    return _closing = Task.CompletedTask;
                }
                _closeRequested = true; _reconnecting = false; _generation++; _openingSource?.Cancel(); CloseSockets(); SetHealth(ChannelHealthState.Closed, null);
                _closing = Task.Run(async () =>
                {
                    await _connectGate.WaitAsync().ConfigureAwait(false); _connectGate.Release();
                    Task pump; lock (_gate) pump = _pump; await pump.ConfigureAwait(false);
                    await _sendGate.WaitAsync().ConfigureAwait(false); _sendGate.Release();
                    await _readGate.WaitAsync().ConfigureAwait(false); _readGate.Release();
                    lock (_gate) _closeRequested = false;
                });
                return _closing;
            }
        }
        public async Task SendAsync(byte[] buffer, int offset, int count, CancellationToken token)
        {
            CheckBuffer(buffer, offset, count); token.ThrowIfCancellationRequested();
            if (count > _descriptor.BufferPolicy.Capacity) throw new ArgumentOutOfRangeException(nameof(count), "CHANNEL_MESSAGE_CAPACITY_REACHED");
            NetworkStream stream; Task sending;
            lock (_gate) { stream = GetStream(); sending = EnterIo(_sendGate, token); }
            await sending.ConfigureAwait(false);
            try { await Write(buffer, offset, count, stream, token).ConfigureAwait(false); }
            finally { _sendGate.Release(); }
        }
        public async Task<int> ReceiveAsync(byte[] buffer, int offset, int count, CancellationToken token)
        {
            CheckBuffer(buffer, offset, count); token.ThrowIfCancellationRequested();
            Task reading; NetworkStream stream;
            lock (_gate) { if (_listeners.Count != 0) throw new InvalidOperationException("CHANNEL_SUBSCRIPTION_OWNS_READER"); stream = GetStream(); reading = EnterIo(_readGate, token); }
            await reading.ConfigureAwait(false);
            try { return count == 0 ? 0 : await Read(buffer, offset, count, stream, token).ConfigureAwait(false); }
            finally { _readGate.Release(); }
        }
        public async Task<byte[]> RequestAsync(byte[] request, int responseLength, CancellationToken token)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.Length > _descriptor.BufferPolicy.Capacity) throw new ArgumentOutOfRangeException(nameof(request), "CHANNEL_MESSAGE_CAPACITY_REACHED");
            if (responseLength < 1 || responseLength > _descriptor.BufferPolicy.Capacity) throw new ArgumentOutOfRangeException(nameof(responseLength));
            var copy = (byte[])request.Clone();
            Task reading; NetworkStream stream;
            lock (_gate) { if (_listeners.Count != 0) throw new InvalidOperationException("CHANNEL_SUBSCRIPTION_OWNS_READER"); stream = GetStream(); reading = EnterIo(_readGate, token); }
            await reading.ConfigureAwait(false);
            try
            {
                await EnterIo(_sendGate, token).ConfigureAwait(false);
                try
                {
                    var started = _clock.MonotonicNow; var result = new byte[responseLength];
                    await Write(copy, 0, copy.Length, stream, token).ConfigureAwait(false);
                    for (int offset = 0; offset < result.Length;) offset += await Read(result, offset, result.Length - offset, stream, token).ConfigureAwait(false);
                    lock (_gate) _lastLatency = _clock.MonotonicNow - started;
                    return result;
                }
                finally { _sendGate.Release(); }
            }
            finally { _readGate.Release(); }
        }
        public string Subscribe(Action<byte[]> callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            lock (_gate)
            {
                CheckUsable();
                bool reserved = _listeners.Count == 0;
                if (reserved && !_readGate.Wait(0)) throw new InvalidOperationException("CHANNEL_READER_ALREADY_IN_USE");
                var listener = new Listener { Id = Guid.NewGuid().ToString("N"), Callback = callback }; _listeners.Add(listener.Id, listener);
                StartPump(reserved); return listener.Id;
            }
        }
        public bool Unsubscribe(string subscriptionId)
        {
            Listener listener; bool stop;
            lock (_gate) { if (subscriptionId == null || !_listeners.TryGetValue(subscriptionId, out listener)) return false; _listeners.Remove(subscriptionId); stop = _listeners.Count == 0; }
            lock (listener.Gate) { listener.Active = false; if (!ReferenceEquals(CurrentListener.Value, listener)) while (listener.Calls != 0) Monitor.Wait(listener.Gate); }
            // .NET Framework cannot cancel an outstanding NetworkStream read without closing that socket.
            if (stop) lock (_gate) { if (_listeners.Count == 0) Close(); } return true;
        }
        private void StartPump(bool reserved = false)
        {
            if (_stream == null || _listeners.Count == 0 || !_pump.IsCompleted) { if (reserved) _readGate.Release(); return; }
            if (!reserved && !_readGate.Wait(0)) throw new InvalidOperationException("CHANNEL_READER_ALREADY_IN_USE");
            var stream = _stream;
            _pump = Task.Run(async () =>
            {
                try
                {
                    var buffer = new byte[_descriptor.BufferPolicy.BatchSize];
                    while (true)
                    {
                        int count = await Read(buffer, 0, buffer.Length, stream, CancellationToken.None).ConfigureAwait(false);
                        Listener[] listeners; lock (_gate) { if (!ReferenceEquals(stream, _stream)) return; listeners = _listeners.Values.ToArray(); }
                        foreach (var listener in listeners)
                        {
                            lock (listener.Gate) { if (!listener.Active) continue; listener.Calls++; }
                            var previous = CurrentListener.Value; CurrentListener.Value = listener;
                            try { var bytes = new byte[count]; Buffer.BlockCopy(buffer, 0, bytes, 0, count); listener.Callback(bytes); }
                            catch (Exception error) { lock (_gate) { _diagnostics.FailureCount++; _diagnostics.LastError = error.Message; } }
                            finally { CurrentListener.Value = previous; lock (listener.Gate) { listener.Calls--; Monitor.PulseAll(listener.Gate); } }
                        }
                    }
                }
                catch (Exception error) { lock (_gate) { if (ReferenceEquals(stream, _stream)) Fail(stream, error, false); } }
                finally { _readGate.Release(); }
            });
            Observe(_pump.ContinueWith(_ => { lock (_gate) if (!_disposed && !_closeRequested && _stream != null && _listeners.Count > 0) StartPump(); }, TaskScheduler.Default));
        }
        private async Task Write(byte[] bytes, int offset, int count, NetworkStream stream, CancellationToken token)
        {
            try
            {
                EnsureCurrentStream(stream);
                using (token.Register(() => Abort(stream))) await stream.WriteAsync(bytes, offset, count, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                lock (_gate) { _diagnostics.SendCount++; _diagnostics.SentBytes += count; _diagnostics.LastSendUtc = _clock.UtcNow; _lastSuccess = _clock.UtcNow; }
                _frameDiagnostics.TraceSend(Envelope(bytes, offset, count));
            }
            catch (Exception error) { Fail(stream, error, token.IsCancellationRequested); if (token.IsCancellationRequested) throw new OperationCanceledException(token); throw; }
        }
        private async Task<int> Read(byte[] bytes, int offset, int count, NetworkStream stream, CancellationToken token)
        {
            try
            {
                EnsureCurrentStream(stream);
                int read; using (token.Register(() => Abort(stream))) read = await stream.ReadAsync(bytes, offset, Math.Min(count, _descriptor.BufferPolicy.BatchSize), token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested(); if (read == 0) throw new EndOfStreamException("TCP_REMOTE_CLOSED");
                lock (_gate) { _diagnostics.ReceiveCount++; _diagnostics.ReceivedBytes += read; _diagnostics.LastReceiveUtc = _clock.UtcNow; _lastSuccess = _clock.UtcNow; }
                _frameDiagnostics.TraceReceive(Envelope(bytes, offset, read)); return read;
            }
            catch (Exception error) { Fail(stream, error, token.IsCancellationRequested); if (token.IsCancellationRequested) throw new OperationCanceledException(token); throw; }
        }
        private void Abort(NetworkStream stream)
        { lock (_gate) if (ReferenceEquals(stream, _stream)) { _generation++; _reconnecting = false; CloseSockets(); SetHealth(ChannelHealthState.Closed, "CHANNEL_IO_CANCELLED"); } }
        private void Fail(NetworkStream stream, Exception error, bool cancelled)
        { lock (_gate) if (ReferenceEquals(stream, _stream)) { _generation++; _reconnecting = false; _diagnostics.FailureCount++; _diagnostics.LastError = error.Message; _frameDiagnostics.TraceError(error); CloseSockets(); SetHealth(cancelled ? ChannelHealthState.Closed : ChannelHealthState.Faulted, error.Message); } }
        private void EnsureCurrentStream(NetworkStream stream)
        { lock (_gate) { CheckUsable(); if (!ReferenceEquals(stream, _stream)) throw new InvalidOperationException("CHANNEL_IO_SUPERSEDED"); } }
        private CxMessageEnvelope Envelope(byte[] bytes, int offset, int count)
        { var payload = new byte[count]; Buffer.BlockCopy(bytes, offset, payload, 0, count); return new CxMessageEnvelope { Timestamp = _clock.UtcNow, Payload = payload }; }
        private NetworkStream GetStream() { lock (_gate) { CheckUsable(); if (_stream == null) throw new InvalidOperationException("TCP_CHANNEL_NOT_OPEN"); return _stream; } }
        private void CloseSockets()
        {
            var client = _client; var candidate = _candidate; var stream = _stream; _client = null; _candidate = null; _stream = null;
            if (stream != null) stream.Dispose(); if (client != null) { client.Close(); _diagnostics.DisconnectCount++; } candidate?.Close();
        }
        private void CheckUsable() { if (_disposed) throw new ObjectDisposedException(nameof(TcpClientTransportChannel)); if (_closeRequested) throw new InvalidOperationException("TCP_CHANNEL_CLOSING"); }
        private ChannelHealth CopyHealth() => new ChannelHealth { State = _health.State, ChangedUtc = _health.ChangedUtc, LastError = _health.LastError };
        private void SetHealth(ChannelHealthState state, string error)
        {
            _health.State = state; _health.LastError = error; _health.ChangedUtc = _clock.UtcNow;
            var handlers = HealthChanged; var snapshot = CopyHealth();
            if (handlers != null) _notifications = _notifications.ContinueWith(_ =>
            { foreach (Action<ChannelHealth> callback in handlers.GetInvocationList()) try { callback(new ChannelHealth { State = snapshot.State, ChangedUtc = snapshot.ChangedUtc, LastError = snapshot.LastError }); } catch (Exception failure) { lock (_gate) _diagnostics.LastError = failure.Message; } }, TaskScheduler.Default);
        }
        private static CxChannelState Map(ChannelHealthState state) => state == ChannelHealthState.Online ? CxChannelState.Open : state == ChannelHealthState.Connecting ? CxChannelState.Opening :
            state == ChannelHealthState.Faulted || state == ChannelHealthState.Degraded ? CxChannelState.Faulted : CxChannelState.Closed;
        private static void CheckBuffer(byte[] buffer, int offset, int count) { if (buffer == null) throw new ArgumentNullException(nameof(buffer)); if (offset < 0 || count < 0 || offset > buffer.Length - count) throw new ArgumentOutOfRangeException(nameof(count)); }
        private Task EnterIo(SemaphoreSlim semaphore, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (_descriptor.BufferPolicy.Backpressure != "Reject") return semaphore.WaitAsync(token);
            if (!semaphore.Wait(0)) throw new InvalidOperationException("CHANNEL_BACKPRESSURE_REJECTED");
            return Task.CompletedTask;
        }
        private static void Observe(Task task) { task.ContinueWith(t => { var ignored = t.Exception; }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default); }
    }
}
