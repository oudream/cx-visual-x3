using System;
using System.ComponentModel.DataAnnotations;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Mechanism.Connectivity
{
    // 通道身份、专有参数、执行策略的唯一配置模型，不再拆 Profile/Options/EditableChannel。
    public enum MCCChannelKind { Virtual, TcpClient, TcpServer, Udp, Serial, Custom, UdpServer }

    public sealed class MCCChannelConfiguration
    {
        [FCMConfigItem("通道 Id", Order = 1, Category = "身份")]
        [Required]
        public string Id { get; set; } = "channel01";
        [FCMConfigItem("通道类型", Order = 2, Category = "身份")]
        public MCCChannelKind Kind { get; set; } = MCCChannelKind.Virtual;
        [FCMConfigItem("自定义通道 TypeId", Order = 3, Category = "身份")]
        public string CustomTypeId { get; set; } = "";

        [FCMConfigItem("主机/监听地址", Order = 10, Category = "网络")]
        public string Host { get; set; } = "127.0.0.1";
        [FCMConfigItem("网络端口", Order = 11, Category = "网络")]
        [Range(0, 65535)]
        public int Port { get; set; } = 502;
        [FCMConfigItem("连接超时", Order = 12, Category = "网络", Unit = "ms")]
        [Range(1, 3600000)]
        public int ConnectTimeoutMs { get; set; } = 5000;
        [FCMConfigItem("最大连接客户端", Order = 13, Category = "网络")]
        [Range(1, 1000000)]
        public int MaxClients { get; set; } = 128;

        [FCMConfigItem("串口名称", Order = 20, Category = "串口")]
        public string PortName { get; set; } = "COM1";
        [FCMConfigItem("波特率", Order = 21, Category = "串口")]
        [Range(1, 10000000)]
        public int BaudRate { get; set; } = 9600;
        [FCMConfigItem("数据位", Order = 22, Category = "串口")]
        [Range(5, 8)]
        public int DataBits { get; set; } = 8;
        [FCMConfigItem("校验位", Order = 23, Category = "串口")]
        public string Parity { get; set; } = "None";
        [FCMConfigItem("停止位", Order = 24, Category = "串口")]
        public string StopBits { get; set; } = "One";

        [FCMConfigItem("子通道路由策略", Order = 30, Category = "子通道")]
        public string SubChannelRule { get; set; } = "";
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

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Id) || !Enum.IsDefined(typeof(MCCChannelKind), Kind) ||
                !Enum.IsDefined(typeof(MCCChannelIOMode), IoMode) ||
                QueueCapacity < 1 || ReceiveByteBudget < 1 ||
                ReconnectMaxAttempts < 0 || ReconnectInitialDelayMs < 0 ||
                ReconnectMaxDelayMs < ReconnectInitialDelayMs ||
                ReconnectBackoff < 1 || double.IsNaN(ReconnectBackoff) || double.IsInfinity(ReconnectBackoff))
                throw new ArgumentException("Channel 身份/执行选项不合法");
            if (Kind == MCCChannelKind.Custom && string.IsNullOrWhiteSpace(CustomTypeId))
                throw new ArgumentException("Custom Channel 需要 TypeId");
            if (Kind == MCCChannelKind.TcpClient || Kind == MCCChannelKind.TcpServer ||
                Kind == MCCChannelKind.Udp || Kind == MCCChannelKind.UdpServer)
            {
                if (string.IsNullOrWhiteSpace(Host) || Port < 1 || Port > 65535)
                    throw new ArgumentException("网络通道地址和端口无效");
            }
            if (Kind == MCCChannelKind.TcpClient && ConnectTimeoutMs < 1)
                throw new ArgumentException("连接超时必须大于零");
            if ((Kind == MCCChannelKind.TcpServer || Kind == MCCChannelKind.UdpServer) && MaxClients < 1)
                throw new ArgumentException("最大连接数必须大于零");
            if (Kind == MCCChannelKind.Serial &&
                (string.IsNullOrWhiteSpace(PortName) || BaudRate < 1 || DataBits < 5 || DataBits > 8 ||
                 !new[] { "None", "Even", "Odd", "Mark", "Space" }.ContainsValue(Parity) ||
                 !new[] { "One", "OnePointFive", "Two" }.ContainsValue(StopBits)))
                throw new ArgumentException("串口参数无效");
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

    internal static class MCCConfigurationArrayExtensions
    {
        public static bool ContainsValue(this string[] values, string value)
        {
            return Array.IndexOf(values, value) >= 0;
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
