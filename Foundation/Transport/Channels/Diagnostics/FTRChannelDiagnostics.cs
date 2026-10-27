using System;

namespace CxVisual.Foundation.Transport
{
public sealed class FTRChannelDiagnostics
    {
        private readonly object _gate = new object();
        private long _sent, _received, _errors, _sentBytes, _receivedBytes;
        private DateTime? _lastSend, _lastReceive;
        private string _lastError;
        public void RecordSend(int byteCount)
        {
            if (byteCount < 0) throw new ArgumentOutOfRangeException("byteCount");
            lock (_gate) { _sent++; _sentBytes += byteCount; _lastSend = DateTime.UtcNow; }
        }
        public void RecordReceive(int byteCount)
        {
            if (byteCount < 0) throw new ArgumentOutOfRangeException("byteCount");
            lock (_gate) { _received++; _receivedBytes += byteCount; _lastReceive = DateTime.UtcNow; }
        }
        public void RecordError(Exception error)
        {
            if (error == null) throw new ArgumentNullException("error");
            lock (_gate) { _errors++; _lastError = error.Message; }
        }
        public FTRChannelDiagnosticsSnapshot Snapshot()
        {
            lock (_gate) return new FTRChannelDiagnosticsSnapshot(
                _sent, _received, _errors, _sentBytes, _receivedBytes, _lastSend, _lastReceive, _lastError);
        }
    }
}
