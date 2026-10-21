using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CxVisual.Mechanism.Connectivity
{
    public enum MCCChannelState { Closed, Open, Faulted }
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
        void Send(byte[] bytes, string correlationId = null, int segmentBytes = 0);
        IList<MCCChannelFrame> ReadFrames();
    }

    /// <summary>
    /// 真实生产Virtual字节通道：点对点、发送分片、接收缓冲、断线与事件。
    /// 无Socket/PLC操作；不提供Real驱动；测试中心只观察而不持有收发逻辑。
    /// </summary>
    public sealed class MCCVirtualChannel : IMCCChannel
    {
        private readonly object _gate = new object();
        private readonly List<MCCChannelFrame> _events = new List<MCCChannelFrame>();
        private readonly MPALengthFrameParser _parser;
        private MCCVirtualChannel _peer;
        private long _sequence;
        private MCCChannelState _state;
        public MCCVirtualChannel(string id, int maximumFrame = 4096)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("ChannelId required");
            Id = id; SessionId = Guid.NewGuid();
            _parser = new MPALengthFrameParser(maximumFrame);
        }
        public string Id { get; private set; }
        public Guid SessionId { get; private set; }
        public MCCChannelState State { get { lock (_gate) return _state; } }
        public event Action<MCCChannelFrame> FrameRecorded;

        public static MCCVirtualChannel[] CreatePair(string leftId, string rightId)
        {
            if (leftId == rightId) throw new ArgumentException("Virtual peer IDs must differ");
            var left = new MCCVirtualChannel(leftId);
            var right = new MCCVirtualChannel(rightId);
            left._peer = right; right._peer = left;
            return new[] { left, right };
        }
        public void Open()
        {
            lock (_gate)
            {
                if (_peer == null) throw new InvalidOperationException("Virtual peer not assigned");
                _parser.Reset();
                _state = MCCChannelState.Open;
            }
        }
        public void Close() { lock (_gate) _state = MCCChannelState.Closed; }
        public IList<MCCChannelFrame> ReadFrames()
        { lock (_gate) return _events.ToArray(); }

        private void Record(MCCFrameDirection direction, byte[] frame,
            string correlation, string error = null, byte[] payload = null)
        {
            MCCChannelFrame record;
            lock (_gate)
            {
                if (_events.Count >= 10000) _events.RemoveAt(0);
                record = new MCCChannelFrame(++_sequence, SessionId, Id, direction,
                    frame, correlation, error, payload);
                _events.Add(record);
            }
            Action<MCCChannelFrame> listeners = FrameRecorded;
            if (listeners != null) listeners(record);
        }

        public void Send(byte[] bytes, string correlationId = null, int segmentBytes = 0)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length > 65536)
                throw new ArgumentException("Invalid raw frame", "bytes");
            if (segmentBytes < 0) throw new ArgumentOutOfRangeException("segmentBytes");
            MCCVirtualChannel peer;
            lock (_gate)
            {
                if (_state != MCCChannelState.Open)
                    throw new InvalidOperationException("Channel is not Open");
                peer = _peer;
            }
            // Capture immutable outbound data before dispatch. No callback executes under _gate.
            byte[] copy = (byte[])bytes.Clone();
            Record(MCCFrameDirection.Tx, copy, correlationId);
            int chunk = segmentBytes == 0 ? copy.Length : segmentBytes;
            for (int offset = 0; offset < copy.Length; offset += chunk)
            {
                int take = Math.Min(chunk, copy.Length - offset);
                byte[] partial = new byte[take];
                Buffer.BlockCopy(copy, offset, partial, 0, take);
                peer.Receive(partial, correlationId);
            }
        }

        private void Receive(byte[] chunk, string correlation)
        {
            lock (_gate)
                if (_state != MCCChannelState.Open)
                    throw new IOException("Virtual receiver is disconnected");
            try
            {
                _parser.PushBytes(chunk);
                byte[] decoded, rawFrame;
                while (_parser.TryReadFrame(out decoded, out rawFrame))
                    Record(MCCFrameDirection.Rx, rawFrame, correlation, null, decoded);
            }
            catch (Exception error) when (error is InvalidDataException ||
                error is InvalidOperationException || error is IOException)
            {
                lock (_gate) _state = MCCChannelState.Faulted;
                Record(MCCFrameDirection.Error, chunk, correlation, error.Message);
                throw;
            }
        }
    }

    /// <summary>封装通道生命周期的真实Virtual会话；不会创建设备现场连接。</summary>
    public sealed class MCCCxChannelSession
    {
        private readonly MCCVirtualChannel _channel;
        private readonly IMPAProtocolCodec _codec;
        public MCCCxChannelSession(MCCVirtualChannel channel, IMPAProtocolCodec codec)
        {
            _channel = channel ?? throw new ArgumentNullException("channel");
            _codec = codec ?? throw new ArgumentNullException("codec");
        }
        public Guid SessionId { get { return _channel.SessionId; } }
        public MCCChannelState State { get { return _channel.State; } }
        public void Open() { _channel.Open(); }
        public void Close() { _channel.Close(); }
        public void SendPayload(byte[] data, string correlation = null, int segmentBytes = 0)
        { _channel.Send(_codec.Encode(data), correlation, segmentBytes); }
        public IList<MCCChannelFrame> Snapshot() { return _channel.ReadFrames(); }
    }
}