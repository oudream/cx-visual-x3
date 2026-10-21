using System;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Core;
using CxVisual.Platform.Channels;

namespace CxVisual.Platform.Protocols
{
    public sealed class CxProtocolSession : ProtocolSession
    {
        private readonly ICxClock _clock;
        private readonly Action<object, object> _validateResponse;
        private readonly SemaphoreSlim _requests = new SemaphoreSlim(1, 1);
        private readonly object _diagnosticsGate = new object();
        public CxProtocolSession(IProtocol protocol, IChannel channel, bool ownsChannel = false, ICxClock clock = null, Action<object, object> validateResponse = null) : base(protocol, channel)
        {
            _clock = clock ?? CxSystemClock.Shared; _validateResponse = validateResponse;
            ChannelSession = new CxChannelSession(channel, ownsChannel, _clock); SessionId = Guid.NewGuid().ToString("N");
        }
        public string SessionId { get; private set; }
        public string ProtocolId => Protocol.ProtocolId;
        public CxChannelSession ChannelSession { get; private set; }
        public CxChannelState State => ChannelSession.State;
        public DateTime? LastHeartbeat { get; private set; }
        public Task StartAsync(CancellationToken token = default(CancellationToken)) => ChannelSession.OpenAsync(token);
        public Task StopAsync(CancellationToken token = default(CancellationToken)) => ChannelSession.CloseAsync(token);
        public Task SendAsync(object message, CancellationToken token = default(CancellationToken))
        { return ChannelSession.RunAsync(async ct => { await ChannelSession.SendAsync(Protocol.Codec.Encode(message), ct).ConfigureAwait(false); return true; }, token); }
        public Task<object> RequestAsync(object message, int responseLength, CancellationToken token = default(CancellationToken))
        {
            var request = Protocol.Codec.Encode(message);
            var requestSnapshot = _validateResponse == null ? null : Protocol.Codec.Decode(request);
            return ChannelSession.RunAsync(async ct =>
            {
                await _requests.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    lock (_diagnosticsGate) Diagnostics.RequestCount++;
                    var bytes = await ChannelSession.RequestAsync(request, responseLength, ct).ConfigureAwait(false);
                    var response = Decode(new CxProtocolFrame { RawBytes = bytes, Timestamp = _clock.UtcNow, Direction = "Receive" }, ct);
                    _validateResponse?.Invoke(requestSnapshot, response); ct.ThrowIfCancellationRequested();
                    LastHeartbeat = _clock.UtcNow; lock (_diagnosticsGate) { Diagnostics.ResponseCount++; Diagnostics.LastError = null; } return response;
                }
                catch (Exception error)
                {
                    // A malformed or uncorrelated response makes the stream boundary untrustworthy.
                    if (error is FormatException)
                        try { await Channel.CloseAsync(CancellationToken.None).ConfigureAwait(false); }
                        catch (Exception closeError) { error.Data["channelCloseFailure"] = closeError; }
                    ReportError("protocol.request", error, error is System.IO.IOException || error is OperationCanceledException); throw;
                }
                finally { _requests.Release(); }
            }, token);
        }
        public Task<object> HandleFrameAsync(CxProtocolFrame frame, CancellationToken token = default(CancellationToken))
        { return ChannelSession.RunAsync(ct => { var message = Decode(frame, ct); LastHeartbeat = _clock.UtcNow; return Task.FromResult(message); }, token); }
        private object Decode(CxProtocolFrame frame, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); if (State != CxChannelState.Open) throw new InvalidOperationException("PROTOCOL_SESSION_NOT_STARTED");
            if (frame == null || frame.RawBytes == null) throw new ArgumentException("PROTOCOL_FRAME_REQUIRED", nameof(frame));
            try
            {
                if (frame.Checksum != null && !frame.ValidateChecksum()) throw new FormatException("PROTOCOL_FRAME_CHECKSUM_INVALID");
                return Protocol.Codec.Decode((byte[])frame.RawBytes.Clone());
            }
            catch (Exception error) { lock (_diagnosticsGate) Diagnostics.DecodeFailureCount++; ReportError("protocol.decode", error, false); throw; }
        }
        private void ReportError(string code, Exception error, bool transient)
        { lock (_diagnosticsGate) { RecordError(code, error, transient); Diagnostics.LastError.TimestampUtc = _clock.UtcNow; } }
        public override void Dispose() { StopAsync().ContinueWith(t => { var ignored = t.Exception; }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default); }
    }
}
