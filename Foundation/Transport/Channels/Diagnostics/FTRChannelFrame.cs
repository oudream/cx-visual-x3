using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Transport
{
public sealed class FTRChannelFrame
    {
        private readonly byte[] _bytes;
        private readonly byte[] _payload;
        internal FTRChannelFrame(long sequence, Guid sessionId, string channelId,
            FTRFrameDirection direction, byte[] data, string correlation, string error,
            byte[] decodedPayload)
        {
            Sequence = sequence; SessionId = sessionId; ChannelId = channelId;
            Direction = direction; TimestampUtc = DateTime.UtcNow;
            _bytes = (byte[])(data ?? new byte[0]).Clone();
            _payload = decodedPayload == null ? null : (byte[])decodedPayload.Clone();
            CorrelationId = correlation ?? ""; Error = error ?? "";
        }
        public long Sequence { get; private set; }
        public Guid SessionId { get; private set; }
        public string ChannelId { get; private set; }
        public FTRFrameDirection Direction { get; private set; }
        public DateTime TimestampUtc { get; private set; }
        public string CorrelationId { get; private set; }
        public string Error { get; private set; }
        // 内部统计不应通过 Bytes 公共防护副本再分配大数组。
        internal int StoredByteCount { get { return _bytes.Length; } }
        public byte[] Bytes { get { return (byte[])_bytes.Clone(); } }
        public byte[] DecodedPayload { get { return _payload == null ? null : (byte[])_payload.Clone(); } }
    }
}
