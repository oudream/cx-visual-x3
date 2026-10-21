using System;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Platform.Channels;
using CxVisual.Platform.Orchestration.Resources;

namespace CxVisual.Platform.Protocols
{
    public interface IProtocolProvider
    {
        string ProviderId { get; }
        Task StartAsync(CancellationToken cancellationToken);
        Task StopAsync(CancellationToken cancellationToken);
        byte[] Encode(object message);
        object Decode(byte[] frame);
        Task<object> ExecuteCommandAsync(object command, int responseLength, CancellationToken cancellationToken);
        Task<CxChannelHealth> GetHealthAsync(CancellationToken cancellationToken);
    }

    /// <summary>Uses the declared codec/session and a separately owned channel.</summary>
    public sealed class CxProtocolProvider : IProtocolProvider, IDisposable
    {
        private readonly object _gate = new object();
        private readonly IProtocol _protocol;
        private readonly IChannel _channel;
        private readonly bool _ownsChannel;
        private CxProtocolSession _session, _retiring;
        private Task _starting = Task.CompletedTask, _stopping = Task.CompletedTask, _disposing;
        private bool _disposed;
        public CxProtocolProvider(string providerId, IProtocol protocol, IChannel channel, bool ownsChannel = false)
        {
            if (string.IsNullOrWhiteSpace(providerId)) throw new ArgumentException("PROTOCOL_PROVIDER_ID_REQUIRED", nameof(providerId));
            ProviderId = providerId; _protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
            _channel = channel ?? throw new ArgumentNullException(nameof(channel)); _ownsChannel = ownsChannel;
            _protocol.Descriptor.Validate().ThrowIfInvalid();
            _protocol.ValidateBinding(new CxProtocolBinding { ProtocolId = protocol.ProtocolId, ChannelRef = channel.Id }, channel).ThrowIfInvalid();
        }
        public string ProviderId { get; private set; }
        public Task StartAsync(CancellationToken token)
        {
            lock (_gate)
            {
                token.ThrowIfCancellationRequested(); if (_disposed) throw new ObjectDisposedException(nameof(CxProtocolProvider));
                if (!_stopping.IsCompleted || _stopping.IsFaulted) throw new InvalidOperationException("PROTOCOL_PROVIDER_STOPPING_OR_CLEANUP_FAILED");
                if (_session == null) { var session = _protocol.CreateSession(_channel); _session = session; _starting = StartCore(session, token); }
                return CxResourceAwait.WaitAsync(_starting, token);
            }
        }
        private async Task StartCore(CxProtocolSession session, CancellationToken token)
        {
            try { await session.StartAsync(token).ConfigureAwait(false); }
            catch
            {
                await session.StopAsync(CancellationToken.None).ConfigureAwait(false);
                lock (_gate) if (ReferenceEquals(_session, session)) _session = null;
                throw;
            }
        }
        // Cancellation abandons this caller's wait; the provider retains and drains the stop task.
        public Task StopAsync(CancellationToken token)
        {
            lock (_gate)
            {
                if (_stopping.IsCompleted)
                {
                    if (_session != null) { _retiring = _session; _session = null; }
                    if (_retiring != null) _stopping = StopCore(_retiring, _starting);
                }
                return CxResourceAwait.WaitAsync(_stopping, token);
            }
        }
        private async Task StopCore(CxProtocolSession session, Task starting)
        {
            await session.StopAsync(CancellationToken.None).ConfigureAwait(false);
            try { await starting.ConfigureAwait(false); } catch { /* Start's caller observes failure; the session has drained. */ }
            if (_ownsChannel) await _channel.CloseAsync(CancellationToken.None).ConfigureAwait(false);
            lock (_gate) if (ReferenceEquals(_retiring, session)) _retiring = null;
        }
        public byte[] Encode(object message) => _protocol.Codec.Encode(message);
        public object Decode(byte[] frame) => _protocol.Codec.Decode(frame);
        public Task<object> ExecuteCommandAsync(object command, int responseLength, CancellationToken token)
        {
            lock (_gate)
            {
                token.ThrowIfCancellationRequested(); if (_disposed) throw new ObjectDisposedException(nameof(CxProtocolProvider));
                if (_session == null || _starting.Status != TaskStatus.RanToCompletion) throw new InvalidOperationException("PROTOCOL_PROVIDER_NOT_STARTED");
                return _session.RequestAsync(command, responseLength, token);
            }
        }
        public Task<CxChannelHealth> GetHealthAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); lock (_gate)
            {
                var health = _channel.HealthSnapshot;
                health.State = _stopping.IsFaulted ? CxChannelState.Faulted : !_stopping.IsCompleted ? CxChannelState.Closing : _session == null ? CxChannelState.Closed : _session.State;
                if (_stopping.IsFaulted) health.LastError = _stopping.Exception.GetBaseException().Message;
                if (_session?.Diagnostics.LastError != null) health.LastError = _session.Diagnostics.LastError.Message;
                return Task.FromResult(health);
            }
        }
        public Task DisposeAsync()
        { lock (_gate) { if (_disposing != null && !_disposing.IsFaulted) return _disposing; _disposed = true; return _disposing = DisposeCore(); } }
        private async Task DisposeCore()
        { await StopAsync(CancellationToken.None).ConfigureAwait(false); if (_ownsChannel) { await _channel.CloseAsync(CancellationToken.None).ConfigureAwait(false); _channel.Dispose(); } }
        public void Dispose()
        { DisposeAsync().ContinueWith(t => { var ignored = t.Exception; }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default); }
    }
}
