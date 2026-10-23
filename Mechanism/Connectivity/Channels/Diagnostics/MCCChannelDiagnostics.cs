using System;

namespace CxVisual.Mechanism.Connectivity
{
    // 直接吸收 ReferTo/CxChannelDiagnostics 的有意义指标；统计实例由真实 Channel 持有。
    public sealed class MCCChannelDiagnostics
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
        public MCCChannelDiagnosticsSnapshot Snapshot()
        {
            lock (_gate) return new MCCChannelDiagnosticsSnapshot(
                _sent, _received, _errors, _sentBytes, _receivedBytes, _lastSend, _lastReceive, _lastError);
        }
    }

    public sealed class MCCChannelDiagnosticsSnapshot
    {
        public MCCChannelDiagnosticsSnapshot(long sent, long received, long errors,
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

    public sealed class MCCReconnectPolicy
    {
        public int MaxAttempts { get; set; } = 10;
        public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(250);
        public double Backoff { get; set; } = 2.0;
        public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);
        public void Validate()
        {
            if (MaxAttempts < 0 || InitialDelay < TimeSpan.Zero || MaxDelay < InitialDelay ||
                Backoff < 1 || double.IsNaN(Backoff) || double.IsInfinity(Backoff))
                throw new ArgumentException("重连策略无效");
        }
        public bool ShouldRetry(int attemptsMade)
        {
            if (attemptsMade < 0) throw new ArgumentOutOfRangeException("attemptsMade");
            return MaxAttempts == 0 || attemptsMade < MaxAttempts;
        }
    }
}
