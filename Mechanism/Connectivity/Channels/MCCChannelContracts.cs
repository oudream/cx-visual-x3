using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Mechanism.Connectivity
{
    // 附加状态而非改动旧枚举数值，旧 Closed/Open/Faulted 配置与日志保持兼容。
    public enum MCCChannelState { Closed, Open, Faulted, Opening, Closing }
    public enum MCCFrameDirection { Tx, Rx, Error }

    /// <summary>生产机制层字节收发记录，Virtual不是Real物理设备。</summary>
    public sealed class MCCChannelFrame
    {
        private readonly byte[] _bytes;
        private readonly byte[] _payload;
        internal MCCChannelFrame(long sequence, Guid sessionId, string channelId,
            MCCFrameDirection direction, byte[] data, string correlation, string error,
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
        public MCCFrameDirection Direction { get; private set; }
        public DateTime TimestampUtc { get; private set; }
        public string CorrelationId { get; private set; }
        public string Error { get; private set; }
        public byte[] Bytes { get { return (byte[])_bytes.Clone(); } }
        public byte[] DecodedPayload { get { return _payload == null ? null : (byte[])_payload.Clone(); } }
    }

    /// <summary>Channel字节行为与协议解码分离；Real适配器必须实现独立IO及安全授权。</summary>
    public interface IMCCChannel
    {
        string Id { get; }
        Guid SessionId { get; }
        MCCChannelState State { get; }
        void Open();
        void Close();
        Task OpenAsync(CancellationToken ct);
        Task CloseAsync(CancellationToken ct);
        Task SendAsync(byte[] bytes, MCCSubChannelAddress address, CancellationToken ct);
        void Send(byte[] bytes, string correlationId = null, int segmentBytes = 0);
        IList<MCCChannelFrame> ReadFrames();
    }
}
