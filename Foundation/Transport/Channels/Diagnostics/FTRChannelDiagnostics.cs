using System;
using System.Diagnostics;

namespace CxVisual.Foundation.Transport
{
public sealed class FTRChannelDiagnostics
    {
        private readonly object _gate = new object();
        private long _sent, _received, _errors, _sentBytes, _receivedBytes;
        private DateTime? _lastSend, _lastReceive;
        private string _lastError;
        private FTRFailureInfo _lastFailure;
        private long _sampleAt=Stopwatch.GetTimestamp();
        private long _sampleSentBytes,_sampleReceivedBytes;
        private double _sendBytesPerSecond,_receiveBytesPerSecond;
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
        { RecordError(error,"Transport",FTRFaultScope.Channel); }
        public void RecordError(Exception error,string stage,FTRFaultScope scope,FTRSubChannelAddress address=null)
        {
            FTRFailureInfo failure=FTRFailureClassifier.Classify(error,stage,scope,address);
            lock (_gate)
            {
                _errors++;
                _lastError=failure.Message;
                _lastFailure=failure;
            }
        }
        public FTRChannelDiagnosticsSnapshot Snapshot()
        {
            lock (_gate)
            {
                long now=Stopwatch.GetTimestamp();
                double seconds=(now-_sampleAt)/(double)Stopwatch.Frequency;
                // 采样窗口下限避免高频UI轮询产生接近零的瞬时速率。
                if(seconds>=0.1)
                {
                    _sendBytesPerSecond=(_sentBytes-_sampleSentBytes)/seconds;
                    _receiveBytesPerSecond=(_receivedBytes-_sampleReceivedBytes)/seconds;
                    _sampleSentBytes=_sentBytes;_sampleReceivedBytes=_receivedBytes;_sampleAt=now;
                }
                var snapshot=new FTRChannelDiagnosticsSnapshot(
                    _sent,_received,_errors,_sentBytes,_receivedBytes,_lastSend,_lastReceive,_lastError);
                snapshot.LastFailure=_lastFailure;
                snapshot.SendBytesPerSecond=_sendBytesPerSecond;
                snapshot.ReceiveBytesPerSecond=_receiveBytesPerSecond;
                return snapshot;
            }
        }
    }
}
