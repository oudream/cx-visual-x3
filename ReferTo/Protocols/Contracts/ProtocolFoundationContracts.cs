using System;
using System.Collections.Generic;
using CxVisual.Platform.Channels;

namespace CxVisual.Platform.Protocols
{
    public interface IProtocol
    {
        CxProtocolDescriptor Descriptor { get; }
        CxProtocolSession CreateSession(IChannel channel, bool ownsChannel = false);
        CxVisual.Core.CxValidationResult ValidateBinding(CxProtocolBinding binding, IChannel channel);
        string ProtocolId { get; }
        string DisplayName { get; }
        Version Version { get; }
        IProtocolCodec Codec { get; }
        IFrameParser FrameParser { get; }
    }

    public interface IProtocolCodec
    {
        byte[] Encode(object message);
        object Decode(byte[] frame);
    }

    public interface IFrameParser
    {
        void PushBytes(byte[] bytes, int offset, int count);
        bool TryReadFrame(out object frame);
        void Reset();
        bool TryParse(IList<byte> buffer, out object frame, out int consumed);
    }

    public sealed class ProtocolError
    {
        public string Code { get; set; }
        public string Message { get; set; }
        public bool IsTransient { get; set; }
        public DateTime TimestampUtc { get; set; }
    }

    public sealed class ProtocolDiagnostics
    {
        public long RequestCount { get; internal set; }
        public long ResponseCount { get; internal set; }
        public long DecodeFailureCount { get; internal set; }
        public ProtocolError LastError { get; internal set; }
    }

    /// <summary>协议会话只依赖公共IChannel，协议实现不再拥有Socket。</summary>
    public abstract class ProtocolSession : IDisposable
    {
        protected ProtocolSession(IProtocol protocol, IChannel channel)
        {
            Protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
            Channel = channel ?? throw new ArgumentNullException(nameof(channel));
            Diagnostics = new ProtocolDiagnostics();
        }

        public IProtocol Protocol { get; }
        public IChannel Channel { get; }
        public ProtocolDiagnostics Diagnostics { get; }
        public abstract void Dispose();

        protected void RecordError(string code, Exception error, bool transient)
        {
            Diagnostics.LastError = new ProtocolError
            {
                Code = code,
                Message = error == null ? string.Empty : error.Message,
                IsTransient = transient,
                TimestampUtc = DateTime.UtcNow
            };
        }
    }
}
