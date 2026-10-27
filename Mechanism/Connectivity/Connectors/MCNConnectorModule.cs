using CxVisual.Foundation.Transport;
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
            IFTRChannel channel, IMPAProtocolDefinition protocol)
            : this(descriptor, configuration, channel, protocol, null) { }

        public MCNConnectorModule(MFMModuleDescriptor descriptor,
            MCNConnectorConfiguration configuration,
            IFTRChannel channel, IMPAProtocolDefinition protocol, FTRChannelFactory channelFactory)
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
            // 创建凭据与通道类型由F17唯一Owner校验；Protocol兼容仍属于Connectivity。
            FTRChannelTypeDescriptor channelType = (channelFactory ?? new FTRChannelFactory())
                .DescribeAttached(configuration.Channel, channel);
            configuration.Protocol.ValidateCompatibility(protocol.Descriptor, channelType);
            Descriptor = descriptor; Configuration = configuration;
            Channel = channel; Protocol = protocol;
            Points = new ReadOnlyCollection<MPAPointDefinition>(configuration.Protocol.Points.ToArray());
        }
        public MFMModuleDescriptor Descriptor { get; private set; }
        public MCNConnectorConfiguration Configuration { get; private set; }
        public IFTRChannel Channel { get; private set; }
        public IMPAProtocolDefinition Protocol { get; private set; }
        public IReadOnlyList<MPAPointDefinition> Points { get; private set; }
        private readonly object _lifecycleGate = new object();
        private bool _started;
        public event Action<byte[]> PayloadReceived;
        // 一对多连接需要把来源 SubChannel 交还调用者，以便定向响应与变量作用域隔离。
        public event Action<FTRSubChannelAddress, byte[]> PayloadReceivedFrom;
        public bool IsStarted { get { lock (_lifecycleGate) return _started; } }

        // 不创建私有线程或运行队列；直接调已装配的 Channel/Protocol Owner。
        public async Task StartAsync(CancellationToken ct)
        {
            IFTRChannelTransport transport = Channel as IFTRChannelTransport;
            if (transport == null || !(Protocol is IMPAProtocol))
                throw new NotSupportedException("尚未装配可执行的 Channel/Protocol 组合");
            lock (_lifecycleGate)
            {
                if (_started) throw new InvalidOperationException("Connector 已经启动");
                _started = true;
            }
            transport.Received += HandleReceived;
            FTRMultiChannelBase multiPeer = transport as FTRMultiChannelBase;
            if (multiPeer != null) multiPeer.PeerRemoved += HandlePeerRemoved;
            try { await transport.OpenAsync(ct).ConfigureAwait(false); }
            catch
            {
                transport.Received -= HandleReceived;
                if (multiPeer != null) multiPeer.PeerRemoved -= HandlePeerRemoved;
                IMPAProtocolSessionCleanup cleanup = Protocol as IMPAProtocolSessionCleanup;
                if (cleanup != null) cleanup.ResetAll();
                lock (_lifecycleGate) _started = false;
                throw;
            }
        }

        public async Task StopAsync(CancellationToken ct)
        {
            IFTRChannelTransport transport = Channel as IFTRChannelTransport;
            if (transport == null) throw new NotSupportedException("通道不支持生命周期");
            lock (_lifecycleGate)
            {
                if (!_started) throw new InvalidOperationException("Connector 尚未启动");
                _started = false;
            }
            transport.Received -= HandleReceived;
            FTRMultiChannelBase multiPeer = transport as FTRMultiChannelBase;
            if (multiPeer != null) multiPeer.PeerRemoved -= HandlePeerRemoved;
            try { await transport.CloseAsync(ct).ConfigureAwait(false); }
            finally
            {
                // 单对端重新连接、多个客户端重连都不继承旧 Parser 和待处理报文。
                IMPAProtocolSessionCleanup cleanup = Protocol as IMPAProtocolSessionCleanup;
                if (cleanup != null) cleanup.ResetAll();
            }
        }

        private void HandlePeerRemoved(FTRSubChannelAddress address)
        {
            IMPAProtocolSessionCleanup cleanup = Protocol as IMPAProtocolSessionCleanup;
            if (cleanup != null) cleanup.Reset(address);
        }
        private void HandleReceived(FTRChannelReceiveContext received)
        {
            IMPAProtocol processor = (IMPAProtocol)Protocol;
            var context = new MPAProtocolContext(Configuration.Protocol.Id, Channel.SessionId.ToString("N"),
                received.Address, received.CorrelationId);
            foreach (byte[] payload in processor.ProcessReceived(received, context))
            {
                Action<FTRSubChannelAddress, byte[]> routed = PayloadReceivedFrom;
                if (routed != null) routed(received.Address, (byte[])payload.Clone());
                Action<byte[]> handler = PayloadReceived;
                if (handler != null) handler((byte[])payload.Clone());
            }
        }

        public Task SendPayloadAsync(byte[] payload, CancellationToken ct)
        { return SendPayloadAsync(payload, new FTRSubChannelAddress(Channel.Id), ct); }

        // MultiPeer Channel 需要明确的运行期 SubChannel 地址，绝不广播到未知客户端。
        public async Task SendPayloadAsync(byte[] payload, FTRSubChannelAddress destination, CancellationToken ct)
        {
            IMPAProtocol processor = Protocol as IMPAProtocol;
            IFTRChannelTransport channel = Channel as IFTRChannelTransport;
            if (processor == null || channel == null) throw new NotSupportedException("不支持实时规约收发");
            if (!IsStarted) throw new InvalidOperationException("Connector 尚未启动");
            if (destination == null || destination.ChannelId != Channel.Id)
                throw new ArgumentException("目标不属于本 Connector Channel");
            byte[] bytes = processor.PrepareSend(payload, new MPAProtocolContext(
                Configuration.Protocol.Id, Channel.SessionId.ToString("N"), destination));
            await channel.SendAsync(bytes, destination, ct).ConfigureAwait(false);
        }

        // Connector 唯一的物理事务入口：复用当前 Channel 接收事件驱动 Protocol，
        // 不为每个请求另订阅 Received、不创建第二个 Connector 或 Socket。
        public Task<MPAProtocolOperationResult> RequestAsync(
            MPAProtocolOperationRequest request, FTRSubChannelAddress destination, CancellationToken ct)
        {
            IMPAProtocolRequester requester = Protocol as IMPAProtocolRequester;
            IFTRChannelTransport transport = Channel as IFTRChannelTransport;
            if (requester == null || transport == null)
                throw new NotSupportedException("当前 Protocol 未实现设备级请求响应");
            if (!IsStarted || transport.State != FTRChannelState.Open)
                throw new InvalidOperationException("Connector 尚未启动或通道不可用");
            if (destination == null || destination.ChannelId != Channel.Id)
                throw new ArgumentException("请求目标不属于 Connector Channel");
            return requester.RequestAsync(request, new MPAProtocolContext(
                Configuration.Protocol.Id, Channel.SessionId.ToString("N"),
                destination, request == null ? null : request.CorrelationId,
                configuration: Configuration.Protocol, transport: transport), ct);
        }

        public Task<object> ReadDevicePointAsync(
            string pointName, IMPADataTargetAccess access, FTRSubChannelAddress destination, CancellationToken ct)
        {
            IMPAProtocolPointAccess protocol = Protocol as IMPAProtocolPointAccess;
            IFTRChannelTransport transport = Channel as IFTRChannelTransport;
            if (protocol == null || transport == null)
                throw new NotSupportedException("当前规约没有设备点表读取能力");
            if (!IsStarted || transport.State != FTRChannelState.Open)
                throw new InvalidOperationException("Connector 未启动");
            if (destination == null || destination.ChannelId != Channel.Id)
                throw new ArgumentException("点操作目标 Channel 不匹配");
            return protocol.ReadDevicePointAsync(pointName, new MPAProtocolContext(
                Configuration.Protocol.Id, Channel.SessionId.ToString("N"), destination,
                configuration: Configuration.Protocol, dataAccess: access, transport: transport), ct);
        }
        public Task<MPAProtocolOperationResult> WriteDevicePointAsync(
            string pointName, IMPADataTargetAccess access, FTRSubChannelAddress destination, CancellationToken ct)
        {
            IMPAProtocolPointAccess protocol = Protocol as IMPAProtocolPointAccess;
            IFTRChannelTransport transport = Channel as IFTRChannelTransport;
            if (protocol == null || transport == null)
                throw new NotSupportedException("当前规约没有设备点表写入能力");
            if (!IsStarted || transport.State != FTRChannelState.Open)
                throw new InvalidOperationException("Connector 未启动");
            if (destination == null || destination.ChannelId != Channel.Id)
                throw new ArgumentException("点操作目标 Channel 不匹配");
            return protocol.WriteDevicePointAsync(pointName, new MPAProtocolContext(
                Configuration.Protocol.Id, Channel.SessionId.ToString("N"), destination,
                configuration: Configuration.Protocol, dataAccess: access, transport: transport), ct);
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
                new FTRSubChannelAddress(Channel.Id), correlationId));
            Channel.Send(frame, correlationId);
        }
    }
}
