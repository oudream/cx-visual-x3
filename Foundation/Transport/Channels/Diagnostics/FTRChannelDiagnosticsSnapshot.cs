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
        /// <summary>最近一次故障的类型、作用域和发生阶段；不包含Payload。</summary>
        public FTRFailureInfo LastFailure {get;internal set;}
        /// <summary>按诊断快照采样的本机平均发送字节率，初次采样前为0。</summary>
        public double SendBytesPerSecond {get;internal set;}
        /// <summary>按诊断快照采样的本机平均接收字节率，初次采样前为0。</summary>
        public double ReceiveBytesPerSecond {get;internal set;}
        public FTRReceiveQueueSnapshot ReceiveQueue {get;internal set;}
        public int PendingSends {get;internal set;}
        public int PendingSendSnapshots {get;internal set;}
        public long PendingSendBytes {get;internal set;}
        public long RejectedSends {get;internal set;}
        /// <summary>受FTR在途额度管理的HTTP/WS临时接收聚合及回复副本字节数。</summary>
        public long BufferedPayloadBytes {get;internal set;}
        public long PeakBufferedPayloadBytes {get;internal set;}
        public long RejectedPayloadBuffers {get;internal set;}
        public long PayloadBufferByteBudget {get;internal set;}
    }
}
