using System;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Core;
using CxVisual.Platform.Orchestration.Resources;

namespace CxVisual.Platform.Channels
{
    public interface IChannelFactory
    {
        bool CanCreate(CxChannelDescriptor descriptor);
        IChannel Create(CxChannelDescriptor descriptor);
    }
    public sealed class CxChannelFactory : IChannelFactory
    {
        private readonly ICxClock _clock;
        public CxChannelFactory(ICxClock clock = null) { _clock = clock ?? CxSystemClock.Shared; }
        public bool CanCreate(CxChannelDescriptor descriptor) => descriptor != null && descriptor.Validate().IsValid &&
            string.Equals(descriptor.Kind, "TCP", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(descriptor.Endpoint.Path) &&
            (string.IsNullOrEmpty(descriptor.SecurityProfile) || descriptor.SecurityProfile == "None") && descriptor.BufferPolicy.DropPolicy == "None";
        public IChannel Create(CxChannelDescriptor descriptor)
        {
            if (!CanCreate(descriptor)) throw new NotSupportedException("CHANNEL_FACTORY_CONFIGURATION_NOT_SUPPORTED");
            return new TcpClientTransportChannel(descriptor, TimeSpan.FromSeconds(5), _clock);
        }
    }
    /// <summary>Runtime session; borrows the channel unless ownership was explicitly transferred.</summary>
    public sealed class CxChannelSession
    {
        private readonly IChannel _channel;
        private readonly bool _owns;
        private readonly ICxClock _clock;
        private readonly object _gate = new object();
        private readonly CancellationTokenSource _stop = new CancellationTokenSource();
        private readonly TaskCompletionSource<bool> _drained = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private Task _closing;
        private int _active;
        private bool _closed;
        public CxChannelSession(IChannel channel, bool ownsChannel = false, ICxClock clock = null)
        { _channel = channel ?? throw new ArgumentNullException(nameof(channel)); _owns = ownsChannel; _clock = clock ?? CxSystemClock.Shared; SessionId = Guid.NewGuid().ToString("N"); }
        public string SessionId { get; private set; }
        public string ChannelId => _channel.Id;
        public DateTime? OpenedAt { get; private set; }
        public CxChannelState State
        { get { lock (_gate) return !_closed ? _channel.State : _closing != null && _closing.IsFaulted ? CxChannelState.Faulted :
                _active != 0 || _closing == null || !_closing.IsCompleted ? CxChannelState.Closing : CxChannelState.Closed; } }
        public System.Collections.Generic.IDictionary<string, string> CorrelationContext { get; private set; } = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);
        public Task OpenAsync(CancellationToken token = default(CancellationToken)) => Execute(async ct => { await _channel.OpenAsync(ct).ConfigureAwait(false); OpenedAt = _clock.UtcNow; return true; }, token);
        public Task CloseAsync(CancellationToken token = default(CancellationToken))
        {
            Task closing;
            lock (_gate)
            {
                if (_closing == null || _closing.IsFaulted)
                {
                    _closed = true; if (!_stop.IsCancellationRequested) _stop.Cancel(); if (_active == 0) _drained.TrySetResult(true);
                    _closing = CloseCore();
                }
                closing = _closing;
            }
            return CxResourceAwait.WaitAsync(closing, token);
        }
        private async Task CloseCore()
        { await _drained.Task.ConfigureAwait(false); if (_owns) { await _channel.CloseAsync(CancellationToken.None).ConfigureAwait(false); _channel.Dispose(); } _stop.Dispose(); }
        public Task SendAsync(byte[] bytes, CancellationToken token = default(CancellationToken))
        { if (bytes == null) throw new ArgumentNullException(nameof(bytes)); return Execute(async ct => { await _channel.SendAsync(bytes, 0, bytes.Length, ct).ConfigureAwait(false); return true; }, token); }
        public Task<byte[]> RequestAsync(byte[] bytes, int responseLength, CancellationToken token = default(CancellationToken))
        { return Execute(ct => _channel.RequestAsync(bytes, responseLength, ct), token); }
        internal Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken token) => Execute(action, token);
        private async Task<T> Execute<T>(Func<CancellationToken, Task<T>> action, CancellationToken token)
        {
            CancellationTokenSource cancellation;
            lock (_gate) { if (_closed) throw new ObjectDisposedException(nameof(CxChannelSession)); token.ThrowIfCancellationRequested(); cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, _stop.Token); _active++; }
            try { return await action(cancellation.Token).ConfigureAwait(false); }
            finally { cancellation.Dispose(); lock (_gate) { _active--; if (_closed && _active == 0) _drained.TrySetResult(true); } }
        }
    }
}
