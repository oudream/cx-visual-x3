using System;
using System.ComponentModel.DataAnnotations;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Foundation.Transport
{
[FCMConfigConverter(typeof(FTRChannelConfigurationConverter))]
public abstract class FTRChannelConfiguration : IFCMConfigVariantSelector
    {
        [FCMConfigItem("通道 Id", Order = 1, Category = "身份")]
        [Required]
        public string Id { get; set; } = "channel01";
        [FCMConfigItem("通道类型", Order = 2, Category = "身份")]
        public FTRChannelKind Kind { get; set; } = FTRChannelKind.Virtual;

        [FCMConfigItem("通信 I/O 方式", Order = 40, Category = "执行")]
        public FTRChannelIOMode IoMode { get; set; } = FTRChannelIOMode.AsyncIO;
        [FCMConfigItem("接收队列容量", Order = 41, Category = "执行")]
        [Range(1, 1000000)]
        public int QueueCapacity { get; set; } = 128;
        [FCMConfigItem("接收内存预算", Order = 42, Category = "执行", Unit = "Bytes")]
        [Range(1, int.MaxValue)]
        public int ReceiveByteBudget { get; set; } = 4 * 1024 * 1024;
        [FCMConfigItem("接收块大小", Order = 43, Category = "执行", Unit = "Bytes")]
        [Range(1, 1048576)]
        public int ReceiveBufferBytes { get; set; } = 64 * 1024;
        [FCMConfigItem("保留诊断载荷", Order = 44, Category = "诊断")]
        public bool CapturePayloadEvidence { get; set; } = true;
        [FCMConfigItem("发送超时", Order = 50, Category = "执行", Unit = "ms")]
        [Range(1, 3600000)]
        public int SendTimeoutMs { get; set; } = 30000;
        [FCMConfigItem("停止等待上限", Order = 51, Category = "执行", Unit = "ms")]
        [Range(1, 120000)]
        public int StopTimeoutMs { get; set; } = 5000;
        [FCMConfigItem("接收空闲超时（0=禁用）", Order = 52, Category = "执行", Unit = "ms")]
        [Range(0, 3600000)]
        public int ReceiveIdleTimeoutMs { get; set; }
        [FCMConfigItem("异步发送快照容量", Order = 53, Category = "执行")]
        [Range(1, 1000000)]
        public int SendQueueCapacity {get;set;} = 4;
        [FCMConfigItem("异步发送快照预算", Order = 54, Category = "执行", Unit = "Bytes")]
        [Range(1, int.MaxValue)]
        public int SendByteBudget {get;set;} = 64*1024*1024;

        [FCMConfigItem("最大重连次数（0=无限）", Order = 45, Category = "重连")]
        [Range(0, 1000000)]
        public int ReconnectMaxAttempts { get; set; } = 10;
        [FCMConfigItem("初始重连间隔", Order = 46, Category = "重连", Unit = "ms")]
        [Range(0, 3600000)]
        public int ReconnectInitialDelayMs { get; set; } = 250;
        [FCMConfigItem("重连退避倍数", Order = 47, Category = "重连")]
        [Range(1.0, 100.0)]
        public double ReconnectBackoff { get; set; } = 2.0;
        [FCMConfigItem("最大重连间隔", Order = 48, Category = "重连", Unit = "ms")]
        [Range(0, 3600000)]
        public int ReconnectMaxDelayMs { get; set; } = 30000;

        public bool CanReconnect(int attemptsMade)
        {
            if (attemptsMade < 0) throw new ArgumentOutOfRangeException("attemptsMade");
            Validate();
            return ReconnectMaxAttempts == 0 || attemptsMade < ReconnectMaxAttempts;
        }

        protected FTRChannelConfiguration(FTRChannelKind kind) { Kind = kind; }
        public virtual void Validate()
        {
            if (string.IsNullOrWhiteSpace(Id) || !Enum.IsDefined(typeof(FTRChannelKind), Kind) ||
                !Enum.IsDefined(typeof(FTRChannelIOMode), IoMode) ||
                QueueCapacity < 1 || QueueCapacity>1000000 || ReceiveByteBudget < 1 ||
                ReceiveBufferBytes<1||ReceiveBufferBytes>1048576||
                SendTimeoutMs<1||SendTimeoutMs>3600000||StopTimeoutMs<1||StopTimeoutMs>120000||
                ReceiveIdleTimeoutMs<0||ReceiveIdleTimeoutMs>3600000||
                SendQueueCapacity<1||SendQueueCapacity>1000000||SendByteBudget<1||
                ReconnectMaxAttempts < 0 || ReconnectInitialDelayMs < 0 ||
                ReconnectMaxDelayMs < ReconnectInitialDelayMs ||
                ReconnectBackoff < 1 || double.IsNaN(ReconnectBackoff) || double.IsInfinity(ReconnectBackoff))
                throw new ArgumentException("Channel 身份与执行预算无效");
            if (GetType() != FTRChannelConfigurations.TypeOf(Kind))
                throw new ArgumentException("Kind 与专用配置类型不一致");
        }

        public object SelectVariant(string discriminator, string value)
        {
            if (discriminator != "Kind") throw new ArgumentException("不支持的配置类型字段");
            FTRChannelKind nextKind;
            if (!Enum.TryParse(value, false, out nextKind) ||
                !Enum.IsDefined(typeof(FTRChannelKind), nextKind))
                throw new ArgumentException("通道类型非法");
            FTRChannelConfiguration next = FTRChannelConfigurations.Create(nextKind);
            next.Id = Id;
            next.IoMode = IoMode;
            next.QueueCapacity = QueueCapacity;
            next.ReceiveByteBudget = ReceiveByteBudget;
            next.ReceiveBufferBytes=ReceiveBufferBytes;
            next.CapturePayloadEvidence=CapturePayloadEvidence;
            next.SendTimeoutMs=SendTimeoutMs;next.StopTimeoutMs=StopTimeoutMs;
            next.ReceiveIdleTimeoutMs=ReceiveIdleTimeoutMs;
            next.SendQueueCapacity=SendQueueCapacity;next.SendByteBudget=SendByteBudget;
            next.ReconnectMaxAttempts = ReconnectMaxAttempts;
            next.ReconnectInitialDelayMs = ReconnectInitialDelayMs;
            next.ReconnectBackoff = ReconnectBackoff;
            next.ReconnectMaxDelayMs = ReconnectMaxDelayMs;
            next.Validate();
            return next;
        }

        public FTRChannelTypeDescriptor DescribeType()
        {
            Validate();
            switch (Kind)
            {
                case FTRChannelKind.Virtual:
                    return new FTRChannelTypeDescriptor("virtual", FTRChannelTopology.SinglePeer, FTRIOCapabilities.ByteStream);
                case FTRChannelKind.TcpClient:
                    return new FTRChannelTypeDescriptor("tcp.client", FTRChannelTopology.SinglePeer, FTRIOCapabilities.ByteStream);
                case FTRChannelKind.TcpServer:
                    return new FTRChannelTypeDescriptor("tcp.server", FTRChannelTopology.MultiPeer,
                        FTRIOCapabilities.ByteStream | FTRIOCapabilities.Listener);
                case FTRChannelKind.Udp:
                    return new FTRChannelTypeDescriptor("udp", FTRChannelTopology.SinglePeer, FTRIOCapabilities.Datagram);
                case FTRChannelKind.UdpServer:
                    return new FTRChannelTypeDescriptor("udp.server", FTRChannelTopology.MultiPeer, FTRIOCapabilities.Datagram);
                case FTRChannelKind.Serial:
                    return new FTRChannelTypeDescriptor("serial", FTRChannelTopology.SharedBus, FTRIOCapabilities.ByteStream);
                case FTRChannelKind.HttpServer:
                    return new FTRChannelTypeDescriptor("http.server", FTRChannelTopology.MultiPeer,
                        FTRIOCapabilities.Message | FTRIOCapabilities.RequestResponse | FTRIOCapabilities.Listener);
                case FTRChannelKind.HttpClient:
                    return new FTRChannelTypeDescriptor("http.client", FTRChannelTopology.SinglePeer,
                        FTRIOCapabilities.Message | FTRIOCapabilities.RequestResponse);
                case FTRChannelKind.NamedPipeClient:
                    return new FTRChannelTypeDescriptor("pipe.client", FTRChannelTopology.SinglePeer,
                        FTRIOCapabilities.ByteStream);
                case FTRChannelKind.NamedPipeServer:
                    return new FTRChannelTypeDescriptor("pipe.server", FTRChannelTopology.MultiPeer,
                        FTRIOCapabilities.ByteStream | FTRIOCapabilities.Listener);
                case FTRChannelKind.WebSocketClient:
                    return new FTRChannelTypeDescriptor("websocket.client", FTRChannelTopology.SinglePeer,
                        FTRIOCapabilities.Message);
                case FTRChannelKind.Replay:
                    return new FTRChannelTypeDescriptor("replay", FTRChannelTopology.SinglePeer,
                        FTRIOCapabilities.Message);
                case FTRChannelKind.UdpMulticast:
                    return new FTRChannelTypeDescriptor("udp.multicast", FTRChannelTopology.SharedBus,
                        FTRIOCapabilities.Datagram);
                default:
                    // 专业 Channel 尚未提供已注册的 I/O 能力元数据，不得假装兼容。
                    throw new NotSupportedException("自定义 Channel 需由专业 TypeDescriptor 提供能力");
            }
        }
    }
}
