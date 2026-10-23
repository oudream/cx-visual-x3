using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
            // 没有完成工业设备 Provider 的时候不允许把配置声明冒充真实连接。
            if (configuration.Channel.Kind != MCCChannelKind.Virtual)
                throw new NotSupportedException("物理 Channel 尚未迁入正式 Provider，不得冒充装配成功");
            if (!(channel is MCCVirtualChannel) && !(channel is MCCVirtualRawChannel))
                throw new NotSupportedException("Virtual 配置必须匹配已验证的 Virtual Channel");

            Descriptor = descriptor; Configuration = configuration;
            Channel = channel; Protocol = protocol;
            Points = new ReadOnlyCollection<MPAPointDefinition>(configuration.Protocol.Points.ToArray());
        }
        public MFMModuleDescriptor Descriptor { get; private set; }
        public MCNConnectorConfiguration Configuration { get; private set; }
        public IMCCChannel Channel { get; private set; }
        public IMPAProtocolDefinition Protocol { get; private set; }
        public IReadOnlyList<MPAPointDefinition> Points { get; private set; }
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
