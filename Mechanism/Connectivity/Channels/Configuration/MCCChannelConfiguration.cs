using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace CxVisual.Mechanism.Connectivity
{
    // 强类型通道参数；由 MEM/现有配置服务负责保存和版本，而不是另设 ChannelConfigManager。
    public interface IMCCChannelOptions
    {
        void Validate();
    }

    public sealed class MCCTcpClientOptions : IMCCChannelOptions
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5);
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Host) || Port < 1 || Port > 65535 ||
                ConnectTimeout <= TimeSpan.Zero) throw new ArgumentException("TCP Client 配置无效");
        }
    }

    public sealed class MCCTcpServerOptions : IMCCChannelOptions
    {
        public string ListenAddress { get; set; } = "0.0.0.0";
        public int Port { get; set; }
        public int MaxClients { get; set; } = 128;
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(ListenAddress) || Port < 1 || Port > 65535 ||
                MaxClients < 1) throw new ArgumentException("TCP Server 配置无效");
        }
    }

    public sealed class MCCUdpOptions : IMCCChannelOptions
    {
        public string LocalAddress { get; set; } = "0.0.0.0";
        public int LocalPort { get; set; }
        public string RemoteEndpoint { get; set; }
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(LocalAddress) || LocalPort < 0 ||
                LocalPort > 65535) throw new ArgumentException("UDP 配置无效");
        }
    }

    public sealed class MCCSerialOptions : IMCCChannelOptions
    {
        public string PortName { get; set; }
        public int BaudRate { get; set; } = 9600;
        public int DataBits { get; set; } = 8;
        public string Parity { get; set; } = "None";
        public string StopBits { get; set; } = "One";
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(PortName) || BaudRate < 1 || DataBits < 5 ||
                DataBits > 8 || !new[] { "None", "Even", "Odd", "Mark", "Space" }.Contains(Parity) ||
                !new[] { "One", "OnePointFive", "Two" }.Contains(StopBits))
                throw new ArgumentException("串行通道配置无效");
        }
    }

    public sealed class MCCChannelExecutionOptions
    {
        public MCCChannelIOMode Mode { get; set; } = MCCChannelIOMode.AsyncIO;
        public int ReceiveQueueCapacity { get; set; } = 128;
        public int ReceiveByteBudget { get; set; } = 4 * 1024 * 1024;
        public void Validate()
        {
            if (!Enum.IsDefined(typeof(MCCChannelIOMode), Mode) || ReceiveQueueCapacity < 1 ||
                ReceiveByteBudget < 1) throw new ArgumentException("通信执行策略配置无效");
        }
    }

    public sealed class MCCChannelTypeDescriptor
    {
        public MCCChannelTypeDescriptor(string typeId, MCCChannelTopology topology, MCCIOCapabilities io)
        {
            if (string.IsNullOrWhiteSpace(typeId)) throw new ArgumentException("通道类型不能为空");
            if (!Enum.IsDefined(typeof(MCCChannelTopology), topology) || io == MCCIOCapabilities.None ||
                (io & ~(MCCIOCapabilities.ByteStream | MCCIOCapabilities.Datagram | MCCIOCapabilities.Message |
                 MCCIOCapabilities.RequestResponse | MCCIOCapabilities.SharedBuffer | MCCIOCapabilities.Listener)) != 0)
                throw new ArgumentException("通道类型能力声明无效");
            TypeId = typeId.Trim(); Topology = topology; IO = io;
        }
        public string TypeId { get; private set; }
        public MCCChannelTopology Topology { get; private set; }
        public MCCIOCapabilities IO { get; private set; }
    }

    // 是类型化配置组合，不是另一个 Channel 实现；Config Owner 仍为既有 MHS。
    public sealed class MCCChannelConfiguration
    {
        public MCCChannelConfiguration(MCCChannelProfile identity, MCCChannelTypeDescriptor type,
            IMCCChannelOptions options, MCCChannelExecutionOptions execution = null)
        {
            Identity = identity ?? throw new ArgumentNullException("identity");
            Type = type ?? throw new ArgumentNullException("type");
            Options = options ?? throw new ArgumentNullException("options");
            Execution = execution ?? new MCCChannelExecutionOptions();
            Validate();
        }
        public MCCChannelProfile Identity { get; private set; }
        public MCCChannelTypeDescriptor Type { get; private set; }
        public IMCCChannelOptions Options { get; private set; }
        public MCCChannelExecutionOptions Execution { get; private set; }
        public void Validate()
        {
            if (!string.Equals(Identity.Kind, Type.TypeId, StringComparison.Ordinal))
                throw new ArgumentException("通道类型和配置身份不一致");
            Options.Validate(); Execution.Validate();
            if (Options is MCCTcpClientOptions &&
                (Type.Topology != MCCChannelTopology.SinglePeer || (Type.IO & MCCIOCapabilities.ByteStream) == 0))
                throw new ArgumentException("TCP Client 与通道拓扑/I/O 能力不匹配");
            if (Options is MCCTcpServerOptions &&
                (Type.Topology != MCCChannelTopology.MultiPeer || (Type.IO & MCCIOCapabilities.Listener) == 0))
                throw new ArgumentException("TCP Server 与通道拓扑/I/O 能力不匹配");
            if (Options is MCCSerialOptions &&
                (Type.IO & MCCIOCapabilities.ByteStream) == 0)
                throw new ArgumentException("Serial 缺少字节流 I/O 能力");
            if (Options is MCCUdpOptions && (Type.IO & MCCIOCapabilities.Datagram) == 0)
                throw new ArgumentException("UDP 缺少数据报 I/O 能力");
        }
    }
}
