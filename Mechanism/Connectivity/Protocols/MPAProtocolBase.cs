using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Mechanism.Connectivity
{
    // 一个 Connector 一个 Protocol 定义；多对端用 SessionId/SubChannel 区分运行态。
    public interface IMPAProtocol : IMPAProtocolDefinition, IMPAProtocolReader, IMPAProtocolWriter, IMPAProtocolSender
    {
        byte[] PrepareSend(byte[] payload, MPAProtocolContext context);
        IReadOnlyList<byte[]> ProcessReceived(MCCChannelReceiveContext data, MPAProtocolContext context);
        // 完整操作面：Read/Write 为 CxVisual 数据映射的读写，不代表设备已响应。
        // 设备级请求/响应由专业规约会话明确实现。
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
        public virtual async Task<MPAProtocolOperationResult> ReadAsync(
            MPAProtocolOperationRequest request, MPAProtocolContext context, CancellationToken ct)
        {
            if (request == null || request.Kind != MPAProtocolOperationKind.Read || request.Target == null)
                throw new ArgumentException("Read 要求有效目标点");
            EnsureMappingContext(context);
            object value = await new MPADataMappingExecutor(context.DataAccess)
                .ReadOutboundAsync(context.Configuration, request.Target.Value, ct).ConfigureAwait(false);
            return new MPAProtocolOperationResult(MPAProtocolOperationStage.Completed, value);
        }
        public virtual async Task<MPAProtocolOperationResult> WriteAsync(
            MPAProtocolOperationRequest request, MPAProtocolContext context, CancellationToken ct)
        {
            if (request == null || request.Kind != MPAProtocolOperationKind.Write ||
                request.Target == null) throw new ArgumentException("Write 要求有效目标点");
            EnsureMappingContext(context);
            await new MPADataMappingExecutor(context.DataAccess)
                .ApplyInboundAsync(context.Configuration, request.Target.Value, request.Value, ct).ConfigureAwait(false);
            return new MPAProtocolOperationResult(MPAProtocolOperationStage.Completed);
        }
        public virtual async Task<MPAProtocolOperationResult> SendAsync(
            MPAProtocolOperationRequest request, MPAProtocolContext context, CancellationToken ct)
        {
            if (request == null || request.Kind != MPAProtocolOperationKind.Send ||
                !(request.Value is byte[])) throw new ArgumentException("Send 需要字节数据");
            if (context == null || context.Transport == null || context.ChannelAddress == null)
                throw new NotSupportedException("必须提供有效的 Channel/子通道发送上下文");
            ct.ThrowIfCancellationRequested();
            byte[] packet = PrepareSend((byte[])request.Value, context);
            await context.Transport.SendAsync(packet, context.ChannelAddress, ct).ConfigureAwait(false);
            // 网络发送不代表接收者处理完毕，更不代表命令完成。
            return new MPAProtocolOperationResult(MPAProtocolOperationStage.Sent);
        }
        private static void EnsureMappingContext(MPAProtocolContext context)
        {
            if (context == null || context.Configuration == null || context.DataAccess == null)
                throw new NotSupportedException("映射读写必须引用正式配置及 MDS/授权 Owner");
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
