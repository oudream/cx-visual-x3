using System;
using System.Collections.Generic;

namespace CxVisual.Mechanism.Connectivity
{
    // 协议地址与 Channel 子通道地址是不同概念。
    public sealed class MPAProtocolAddress
    {
        public MPAProtocolAddress(string value, string scheme = null)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("规约地址必填", "value");
            Value = value.Trim(); Scheme = scheme;
        }
        public string Value { get; private set; }
        public string Scheme { get; private set; }
        public override string ToString() { return string.IsNullOrEmpty(Scheme) ? Value : Scheme + ":" + Value; }
    }

    public sealed class MPAProtocolBinding
    {
        public MPAProtocolBinding(string id, string protocolTypeId, string channelRef = null,
            string subChannelSelector = null, MPAProtocolAddress protocolAddress = null,
            MPAProtocolDispatchOptions dispatch = null)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(protocolTypeId))
                throw new ArgumentException("规约绑定 Id/Type 必填");
            Id = id.Trim(); ProtocolTypeId = protocolTypeId.Trim(); ChannelRef = channelRef;
            SubChannelSelector = subChannelSelector; ProtocolAddress = protocolAddress;
            Dispatch = dispatch ?? new MPAProtocolDispatchOptions();
            Validate();
        }
        public string Id { get; private set; }
        public string ProtocolTypeId { get; private set; }
        public string ChannelRef { get; private set; }
        // 只是可持久化的选择规则，不是运行时 Generation；实际路由由 Channel 当前子通道确定。
        public string SubChannelSelector { get; private set; }
        public MPAProtocolAddress ProtocolAddress { get; private set; }
        public MPAProtocolDispatchOptions Dispatch { get; private set; }
        public bool RequiresChannel { get { return !string.IsNullOrWhiteSpace(ChannelRef); } }

        public void Validate()
        {
            Dispatch.Validate();
            if (!RequiresChannel && !string.IsNullOrWhiteSpace(SubChannelSelector))
                throw new ArgumentException("无通道绑定不能选择子通道");
        }
        public void ValidateCompatibility(MPAProtocolDescriptor protocol, MCCChannelTypeDescriptor channel)
        {
            if (protocol == null) throw new ArgumentNullException("protocol");
            if (!string.Equals(protocol.Id, ProtocolTypeId, StringComparison.Ordinal))
                throw new ArgumentException("规约类型不匹配");
            if (protocol.RequiredIO != MCCIOCapabilities.None)
            {
                if (!RequiresChannel || channel == null || (channel.IO & protocol.RequiredIO) != protocol.RequiredIO)
                    throw new ArgumentException("规约与通道 I/O 能力不兼容");
            }
            else if (RequiresChannel && channel == null)
                throw new ArgumentException("绑定通道类型缺失");
        }
    }

    public sealed class MPAProtocolRoute
    {
        public MPAProtocolRoute(string bindingId, MCCSubChannelAddress channelAddress,
            MPAProtocolAddress protocolAddress = null)
        {
            if (string.IsNullOrWhiteSpace(bindingId)) throw new ArgumentException("规约绑定身份不能为空");
            BindingId = bindingId.Trim(); ChannelAddress = channelAddress; ProtocolAddress = protocolAddress;
        }
        public string BindingId { get; private set; }
        public MCCSubChannelAddress ChannelAddress { get; private set; }
        public MPAProtocolAddress ProtocolAddress { get; private set; }
    }
}
