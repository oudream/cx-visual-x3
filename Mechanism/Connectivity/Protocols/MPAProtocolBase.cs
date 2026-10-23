using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Mechanism.Connectivity
{
    // 一个 Connector 一个 Protocol 定义；多对端用 SessionId/SubChannel 区分运行态。
    public interface IMPAProtocol : IMPAProtocolDefinition
    {
        byte[] PrepareSend(byte[] payload, MPAProtocolContext context);
        IReadOnlyList<byte[]> ProcessReceived(MCCChannelReceiveContext data, MPAProtocolContext context);
    }

    public abstract class MPAProtocolBase : IMPAProtocol
    {
        protected MPAProtocolBase(MPAProtocolDescriptor descriptor)
        { Descriptor = descriptor ?? throw new ArgumentNullException("descriptor"); }
        public string Id { get { return Descriptor.Id; } }
        public MPAProtocolDescriptor Descriptor { get; private set; }
        public virtual byte[] PrepareSend(byte[] payload, MPAProtocolContext context)
        {
            if (payload == null) throw new ArgumentNullException("payload");
            return (byte[])payload.Clone();
        }
        public abstract IReadOnlyList<byte[]> ProcessReceived(MCCChannelReceiveContext data, MPAProtocolContext context);
    }

    // 空规约有清晰的透传语义：不解码、不丢弃、不创造传输层。
    public sealed class MPAEmptyProtocol : MPAProtocolBase
    {
        public MPAEmptyProtocol() : base(new MPAProtocolDescriptor("empty", "原始数据透传", 1,
            MPAProtocolCapabilities.Interpret | MPAProtocolCapabilities.Send | MPAProtocolCapabilities.Stream)) { }
        public override IReadOnlyList<byte[]> ProcessReceived(MCCChannelReceiveContext data, MPAProtocolContext context)
        {
            if (data == null) throw new ArgumentNullException("data");
            return new[] { data.Data };
        }
    }

    // 每个 SubChannel 维护独立长度帧解码状态；Channel 无权决定 Frame 边界。
    public sealed class MPALengthFrameProtocol : MPAProtocolBase, IMPAEncodedProtocolDefinition
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, MPALengthFrameParser> _parsers =
            new Dictionary<string, MPALengthFrameParser>(StringComparer.Ordinal);
        public MPALengthFrameProtocol() : base(new MPAProtocolDescriptor("length.frame",
            "Length Frame + CRC16", 1, MPAProtocolCapabilities.Interpret |
            MPAProtocolCapabilities.Send | MPAProtocolCapabilities.Stream, MCCIOCapabilities.ByteStream))
        { Codec = new MPALengthFrameCodec(); }
        public IMPAProtocolCodec Codec { get; private set; }
        public override byte[] PrepareSend(byte[] bytes, MPAProtocolContext context)
        { return Codec.Encode(bytes); }
        public override IReadOnlyList<byte[]> ProcessReceived(MCCChannelReceiveContext data, MPAProtocolContext context)
        {
            if (data == null || data.Address == null) throw new ArgumentNullException("data");
            string route = data.Address.ToString();
            lock (_gate)
            {
                MPALengthFrameParser parser;
                if (!_parsers.TryGetValue(route, out parser))
                {
                    parser = new MPALengthFrameParser();
                    _parsers.Add(route, parser);
                }
                parser.PushBytes(data.Data);
                List<byte[]> result = new List<byte[]>();
                byte[] decoded, rawFrame;
                while (parser.TryReadFrame(out decoded, out rawFrame)) result.Add(decoded);
                return result;
            }
        }
        public void Reset(MCCSubChannelAddress address)
        {
            if (address == null) throw new ArgumentNullException("address");
            lock (_gate) _parsers.Remove(address.ToString());
        }
    }
}
