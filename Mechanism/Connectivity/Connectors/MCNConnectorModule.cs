using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CxVisual.Mechanism.Core.Modules;

namespace CxVisual.Mechanism.Connectivity
{
    /// <summary>
    /// 正式Connector Owner的实例装配：接受外部已注册的真实Channel/Protocol Provider，
    /// 校验配置身份、引用和IO兼容性，不创建第二套驱动、Runner或模块管理器。
    /// 当前实现只确认Virtual字节通道；物理网络/串口Provider未验证时明确拒绝。
    /// 装配不调用Open/Send，也不表示已运行、设备联机或点表已写入MDS。
    /// </summary>
    public sealed class MCNConnectorModule : IMCNConnectorModule
    {
        public MCNConnectorModule(MFMModuleDescriptor descriptor,
            MCNConnectorConfiguration configuration,
            IEnumerable<IMCCChannel> availableChannels,
            IEnumerable<IMPAProtocolDefinition> availableProtocols)
        {
            if (descriptor == null) throw new ArgumentNullException("descriptor");
            if (configuration == null) throw new ArgumentNullException("configuration");
            if (availableChannels == null) throw new ArgumentNullException("availableChannels");
            if (availableProtocols == null) throw new ArgumentNullException("availableProtocols");
            if (descriptor.Kind != MFMModuleKind.Connector)
                throw new ArgumentException("模块类别必须为Connector", "descriptor");
            if (!configuration.Enabled)
                throw new InvalidOperationException("模块未启用，不得装配");
            configuration.Validate();

            // 缺少物理驱动注册/类型证明前拒绝装配，不将虚拟通道冒充真实设备。
            if (configuration.Channels.Any(x => x.Kind != MCCChannelKind.Virtual))
                throw new NotSupportedException("尚无已验证的非Virtual Channel Provider装配入口");

            IMCCChannel[] channels = availableChannels.ToArray();
            IMPAProtocolDefinition[] protocols = availableProtocols.ToArray();
            if (channels.Any(x => x == null || string.IsNullOrWhiteSpace(x.Id)) ||
                protocols.Any(x => x == null || x.Descriptor == null ||
                    !string.Equals(x.Id, x.Descriptor.Id, StringComparison.Ordinal)))
                throw new ArgumentException("Provider实例/身份不合法");
            if (channels.Any(x => !(x is MCCVirtualChannel)))
                throw new NotSupportedException("Virtual配置只能装配正式MCCVirtualChannel实例");

            string[] configuredChannels = configuration.Channels.Select(x => x.Id).ToArray();
            string[] registeredChannels = channels.Select(x => x.Id).ToArray();
            if (registeredChannels.Distinct(StringComparer.Ordinal).Count() != registeredChannels.Length ||
                configuredChannels.Length != registeredChannels.Length ||
                configuredChannels.Except(registeredChannels, StringComparer.Ordinal).Any() ||
                registeredChannels.Except(configuredChannels, StringComparer.Ordinal).Any())
                throw new InvalidOperationException("正式通道实例与Connector配置的ChannelId集合不一致");

            Dictionary<string, MPAProtocolDescriptor> descriptions =
                new Dictionary<string, MPAProtocolDescriptor>(StringComparer.Ordinal);
            foreach (IMPAProtocolDefinition protocol in protocols)
            {
                if (descriptions.ContainsKey(protocol.Id))
                    throw new ArgumentException("Protocol TypeId重复：" + protocol.Id);
                descriptions.Add(protocol.Id, protocol.Descriptor);
            }
            configuration.ValidateProtocolCapabilities(descriptions);

            // 一个规约类型可在配置中有多个绑定，但只需要一个类型定义。
            string[] requiredTypes = configuration.Protocols
                .Select(x => x.ProtocolTypeId).Distinct(StringComparer.Ordinal).ToArray();
            if (requiredTypes.Except(descriptions.Keys, StringComparer.Ordinal).Any() ||
                descriptions.Keys.Except(requiredTypes, StringComparer.Ordinal).Any())
                throw new InvalidOperationException("规约类型实例与正式绑定定义不一致");

            Descriptor = descriptor;
            Channels = new ReadOnlyCollection<IMCCChannel>(channels);
            Protocols = new ReadOnlyCollection<IMPAProtocolDefinition>(protocols);
            Points = new ReadOnlyCollection<MCNPointDefinition>(configuration.Points.ToArray());
        }

        public MFMModuleDescriptor Descriptor { get; private set; }
        public IReadOnlyList<IMCCChannel> Channels { get; private set; }
        public IReadOnlyList<IMPAProtocolDefinition> Protocols { get; private set; }
        public IReadOnlyList<MCNPointDefinition> Points { get; private set; }
    }
}
