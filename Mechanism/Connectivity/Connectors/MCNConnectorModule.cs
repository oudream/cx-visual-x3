using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Mechanism.Core.Modules;

namespace CxVisual.Mechanism.Connectivity
{
    // 严格 1 Channel : 1 Protocol。装配不等于 Open/连接/物理设备可用。
    public sealed class MCNConnectorModule : IMCNConnectorModule
    {
        public MCNConnectorModule(MFMModuleDescriptor descriptor,
            MCNConnectorConfiguration configuration,
            IMCCChannel channel, IMPAProtocolDefinition protocol)
        {
            if (descriptor == null) throw new ArgumentNullException("descriptor");
            if (configuration == null) throw new ArgumentNullException("configuration");
            if (channel == null) throw new ArgumentNullException("channel");
            if (protocol == null || protocol.Descriptor == null) throw new ArgumentNullException("protocol");
            if (descriptor.Kind != MFMModuleKind.Connector)
                throw new ArgumentException("必须是 Connector 类别");
            if (!configuration.Enabled) throw new InvalidOperationException("模块未启用");
            configuration.Validate();
            if (channel.Id != configuration.Channel.Id)
                throw new ArgumentException("Channel 实例 Id 与唯一配置不匹配");
            if (protocol.Id != configuration.Protocol.ProtocolTypeId ||
                protocol.Descriptor.Id != protocol.Id)
                throw new ArgumentException("Protocol 类型与唯一配置不匹配");
            configuration.Protocol.ValidateCompatibility(protocol.Descriptor, configuration.Channel.DescribeType());
            // 直接装配已经实现的正式 Channel 类型，严禁把配置字符串冒充硬件已连接。
            bool typeMatch = false;
            switch (configuration.Channel.Kind)
            {
                case MCCChannelKind.Virtual:
                    typeMatch = channel is MCCVirtualChannel; break;
                case MCCChannelKind.TcpClient: typeMatch = channel is MCCTcpClientChannel; break;
                case MCCChannelKind.TcpServer: typeMatch = channel is MCCTcpServerChannel; break;
                case MCCChannelKind.Udp: typeMatch = channel is MCCUdpChannel; break;
                case MCCChannelKind.UdpServer: typeMatch = channel is MCCUdpServerChannel; break;
                case MCCChannelKind.Serial: typeMatch = channel is MCCSerialChannel; break;
            }
            if (!typeMatch)
                throw new NotSupportedException("通道实例与已实现的类型不匹配或尚无真实 Provider");
            Descriptor = descriptor; Configuration = configuration;
            Channel = channel; Protocol = protocol;
            Points = new ReadOnlyCollection<MPAPointDefinition>(configuration.Protocol.Points.ToArray());
        }
        public MFMModuleDescriptor Descriptor { get; private set; }
        public MCNConnectorConfiguration Configuration { get; private set; }
        public IMCCChannel Channel { get; private set; }
        public IMPAProtocolDefinition Protocol { get; private set; }
        public IReadOnlyList<MPAPointDefinition> Points { get; private set; }
        private readonly object _lifecycleGate = new object();
        private bool _started;
        public event Action<byte[]> PayloadReceived;
        public bool IsStarted { get { lock (_lifecycleGate) return _started; } }

        // 不创建私有线程或运行队列；直接调已装配的 Channel/Protocol Owner。
        public async Task StartAsync(CancellationToken ct)
        {
            IMCCChannelTransport transport = Channel as IMCCChannelTransport;
            if (transport == null || !(Protocol is IMPAProtocol))
                throw new NotSupportedException("尚未装配可执行的 Channel/Protocol 组合");
            lock (_lifecycleGate)
            {
                if (_started) throw new InvalidOperationException("Connector 已经启动");
                _started = true;
            }
            transport.Received += HandleReceived;
            try { await transport.OpenAsync(ct).ConfigureAwait(false); }
            catch
            {
                transport.Received -= HandleReceived;
                lock (_lifecycleGate) _started = false;
                throw;
            }
        }

        public async Task StopAsync(CancellationToken ct)
        {
            IMCCChannelTransport transport = Channel as IMCCChannelTransport;
            if (transport == null) throw new NotSupportedException("通道不支持生命周期");
            lock (_lifecycleGate)
            {
                if (!_started) throw new InvalidOperationException("Connector 尚未启动");
                _started = false;
            }
            transport.Received -= HandleReceived;
            await transport.CloseAsync(ct).ConfigureAwait(false);
        }

        private void HandleReceived(MCCChannelReceiveContext received)
        {
            IMPAProtocol processor = (IMPAProtocol)Protocol;
            var context = new MPAProtocolContext(Configuration.Protocol.Id, Channel.SessionId.ToString("N"),
                received.Address, received.CorrelationId);
            foreach (byte[] payload in processor.ProcessReceived(received, context))
            {
                Action<byte[]> handler = PayloadReceived;
                if (handler != null) handler((byte[])payload.Clone());
            }
        }

        public Task SendPayloadAsync(byte[] payload, CancellationToken ct)
        { return SendPayloadAsync(payload, new MCCSubChannelAddress(Channel.Id), ct); }

        // MultiPeer Channel 需要明确的运行期 SubChannel 地址，绝不广播到未知客户端。
        public async Task SendPayloadAsync(byte[] payload, MCCSubChannelAddress destination, CancellationToken ct)
        {
            IMPAProtocol processor = Protocol as IMPAProtocol;
            IMCCChannelTransport channel = Channel as IMCCChannelTransport;
            if (processor == null || channel == null) throw new NotSupportedException("不支持实时规约收发");
            if (!IsStarted) throw new InvalidOperationException("Connector 尚未启动");
            if (destination == null || destination.ChannelId != Channel.Id)
                throw new ArgumentException("目标不属于本 Connector Channel");
            byte[] bytes = processor.PrepareSend(payload, new MPAProtocolContext(
                Configuration.Protocol.Id, Channel.SessionId.ToString("N"), destination));
            await channel.SendAsync(bytes, destination, ct).ConfigureAwait(false);
        }

        public Task ApplyPointValueAsync(string point, object value, IMPADataTargetAccess access, CancellationToken ct)
        { return new MPADataMappingExecutor(access).ApplyInboundAsync(Configuration.Protocol, point, value, ct); }

        public Task<object> ReadPointValueAsync(string point, IMPADataTargetAccess access, CancellationToken ct)
        { return new MPADataMappingExecutor(access).ReadOutboundAsync(Configuration.Protocol, point, ct); }

        public void SendPayload(byte[] payload, string correlationId = null)
        {
            IMPAProtocol processor = Protocol as IMPAProtocol;
            if (processor == null) throw new NotSupportedException("规约未提供正式 Send 处理");
            byte[] frame = processor.PrepareSend(payload, new MPAProtocolContext(
                Configuration.Protocol.Id, Channel.SessionId.ToString("N"),
                new MCCSubChannelAddress(Channel.Id), correlationId));
            Channel.Send(frame, correlationId);
        }
    }
}
