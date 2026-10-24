using System;
using System.ComponentModel.DataAnnotations;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Mechanism.Connectivity
{
    // 通道身份、专有参数、执行策略的唯一配置模型，不再拆 Profile/Options/EditableChannel。
    public enum MCCChannelKind { Virtual, TcpClient, TcpServer, Udp, Serial, Custom, UdpServer }

    [FCMConfigConverter(typeof(MCCChannelConfigurationConverter))]
    public abstract class MCCChannelConfiguration
    {
        [FCMConfigItem("通道 Id", Order = 1, Category = "身份")]
        [Required]
        public string Id { get; set; } = "channel01";
        [FCMConfigItem("通道类型", Order = 2, Category = "身份")]
        public MCCChannelKind Kind { get; set; } = MCCChannelKind.Virtual;

        [FCMConfigItem("通信 I/O 方式", Order = 40, Category = "执行")]
        public MCCChannelIOMode IoMode { get; set; } = MCCChannelIOMode.AsyncIO;
        [FCMConfigItem("接收队列容量", Order = 41, Category = "执行")]
        [Range(1, 1000000)]
        public int QueueCapacity { get; set; } = 128;
        [FCMConfigItem("接收内存预算", Order = 42, Category = "执行", Unit = "Bytes")]
        [Range(1, int.MaxValue)]
        public int ReceiveByteBudget { get; set; } = 4 * 1024 * 1024;

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

        protected MCCChannelConfiguration(MCCChannelKind kind) { Kind = kind; }
        public virtual void Validate()
        {
            if (string.IsNullOrWhiteSpace(Id) || !Enum.IsDefined(typeof(MCCChannelKind), Kind) ||
                !Enum.IsDefined(typeof(MCCChannelIOMode), IoMode) ||
                QueueCapacity < 1 || ReceiveByteBudget < 1 ||
                ReconnectMaxAttempts < 0 || ReconnectInitialDelayMs < 0 ||
                ReconnectMaxDelayMs < ReconnectInitialDelayMs ||
                ReconnectBackoff < 1 || double.IsNaN(ReconnectBackoff) || double.IsInfinity(ReconnectBackoff))
                throw new ArgumentException("Channel 身份与执行预算无效");
            if (GetType() != MCCChannelConfigurations.TypeOf(Kind))
                throw new ArgumentException("Kind 与专用配置类型不一致");
        }

        public MCCChannelTypeDescriptor DescribeType()
        {
            Validate();
            switch (Kind)
            {
                case MCCChannelKind.Virtual:
                    return new MCCChannelTypeDescriptor("virtual", MCCChannelTopology.SinglePeer, MCCIOCapabilities.ByteStream);
                case MCCChannelKind.TcpClient:
                    return new MCCChannelTypeDescriptor("tcp.client", MCCChannelTopology.SinglePeer, MCCIOCapabilities.ByteStream);
                case MCCChannelKind.TcpServer:
                    return new MCCChannelTypeDescriptor("tcp.server", MCCChannelTopology.MultiPeer,
                        MCCIOCapabilities.ByteStream | MCCIOCapabilities.Listener);
                case MCCChannelKind.Udp:
                    return new MCCChannelTypeDescriptor("udp", MCCChannelTopology.SinglePeer, MCCIOCapabilities.Datagram);
                case MCCChannelKind.UdpServer:
                    return new MCCChannelTypeDescriptor("udp.server", MCCChannelTopology.MultiPeer, MCCIOCapabilities.Datagram);
                case MCCChannelKind.Serial:
                    return new MCCChannelTypeDescriptor("serial", MCCChannelTopology.SharedBus, MCCIOCapabilities.ByteStream);
                default:
                    // 专业 Channel 尚未提供已注册的 I/O 能力元数据，不得假装兼容。
                    throw new NotSupportedException("自定义 Channel 需由专业 TypeDescriptor 提供能力");
            }
        }
    }

    // 仅白名单中的正式 Channel 配置可加载；不使用任意 CLR 类型名。
    public static class MCCChannelConfigurations
    {
        public static Type TypeOf(MCCChannelKind kind)
        {
            switch (kind)
            {
                case MCCChannelKind.Virtual: return typeof(MCCVirtualChannelConfiguration);
                case MCCChannelKind.TcpClient: return typeof(MCCTcpClientChannelConfiguration);
                case MCCChannelKind.TcpServer: return typeof(MCCTcpServerChannelConfiguration);
                case MCCChannelKind.Udp: return typeof(MCCUdpChannelConfiguration);
                case MCCChannelKind.UdpServer: return typeof(MCCUdpServerChannelConfiguration);
                case MCCChannelKind.Serial: return typeof(MCCSerialChannelConfiguration);
                case MCCChannelKind.Custom: return typeof(MCCCustomChannelConfiguration);
                default: throw new ArgumentOutOfRangeException("kind");
            }
        }
        public static MCCChannelConfiguration Create(MCCChannelKind kind)
        { return (MCCChannelConfiguration)Activator.CreateInstance(TypeOf(kind)); }
    }

    public sealed class MCCChannelConfigurationConverter : IFCMConfigValueConverter<MCCChannelConfiguration>
    {
        public sealed class KindProbe
        {
            public KindProbe() { }
            public MCCChannelKind Kind { get; set; } = MCCChannelKind.Virtual; }
        public string Write(MCCChannelConfiguration value)
        {
            if (value == null) return "null";
            value.Validate();
            return FCMConfigValueCodec.Serialize(value);
        }
        public MCCChannelConfiguration Read(string json)
        {
            MCCChannelKind kind = FCMConfigValueCodec.Deserialize<KindProbe>(json).Kind;
            Type type = MCCChannelConfigurations.TypeOf(kind);
            var allowed = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (FCMConfigMemberDescriptor member in FCMConfigSchemaBuilder.Members(type))
                allowed.Add(member.Name);
            foreach (string key in FCMConfigValueCodec.GetObjectPropertyNames(json))
                if (!allowed.Contains(key))
                    throw new FormatException("CHANNEL_LEGACY_MIGRATION_REQUIRED: " + key);
            MCCChannelConfiguration value = (MCCChannelConfiguration)FCMConfigValueCodec.Deserialize(json, type);
            value.Validate();
            return value;
        }
    }

    // 类型描述是运行/发现元数据，不是另一份配置。
    public sealed class MCCChannelTypeDescriptor
    {
        public MCCChannelTypeDescriptor(string typeId, MCCChannelTopology topology, MCCIOCapabilities io)
        {
            if (string.IsNullOrWhiteSpace(typeId) || !Enum.IsDefined(typeof(MCCChannelTopology), topology) ||
                io == MCCIOCapabilities.None ||
                (io & ~(MCCIOCapabilities.ByteStream | MCCIOCapabilities.Datagram | MCCIOCapabilities.Message |
                        MCCIOCapabilities.RequestResponse | MCCIOCapabilities.SharedBuffer | MCCIOCapabilities.Listener)) != 0)
                throw new ArgumentException("通道类型描述无效");
            TypeId = typeId.Trim(); Topology = topology; IO = io;
        }
        public string TypeId { get; private set; }
        public MCCChannelTopology Topology { get; private set; }
        public MCCIOCapabilities IO { get; private set; }
    }
}
