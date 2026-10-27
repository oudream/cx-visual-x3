using System;

namespace CxVisual.Foundation.Transport
{
public sealed class FTRChannelDiagnosticsSnapshot
    {
        public FTRChannelDiagnosticsSnapshot(long sent, long received, long errors,
            long sentBytes, long receivedBytes, DateTime? lastSendUtc, DateTime? lastReceiveUtc, string lastError)
        {
            Sent = sent; Received = received; Errors = errors; SentBytes = sentBytes; ReceivedBytes = receivedBytes;
            LastSendUtc = lastSendUtc; LastReceiveUtc = lastReceiveUtc; LastError = lastError;
        }
        public long Sent { get; private set; }
        public long Received { get; private set; }
        public long Errors { get; private set; }
        public long SentBytes { get; private set; }
        public long ReceivedBytes { get; private set; }
        public DateTime? LastSendUtc { get; private set; }
        public DateTime? LastReceiveUtc { get; private set; }
        public string LastError { get; private set; }
    }
}
