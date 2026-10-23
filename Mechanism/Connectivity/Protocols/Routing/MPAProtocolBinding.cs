using System;
using System.ComponentModel.DataAnnotations;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Mechanism.Connectivity
{
    // ProtocolAddress 是具体规约地址值，和 Channel 的 SubChannelId 不等价。
    public sealed class MPAProtocolAddress
    {
        public MPAProtocolAddress(string value, string scheme = null)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("规约地址必填");
            Value = value.Trim(); Scheme = scheme;
        }
        public string Value { get; private set; }
        public string Scheme { get; private set; }
        public override string ToString() { return string.IsNullOrWhiteSpace(Scheme) ? Value : Scheme + ":" + Value; }
    }

    public enum MPAProtocolKind { LengthFrame, ModbusTcpDraft, ModbusRtuDraft, Internal, Custom }

    // 唯一的规约绑定/执行参数配置；Session/Descriptor 是运行和类型元数据，不是另一个配置实体。
    public sealed class MPAProtocolBinding
    {
        [FCMConfigItem("规约绑定 Id", Order = 1, Category = "身份")]
        [Required]
        public string Id { get; set; } = "protocol01";
        [FCMConfigItem("规约类型", Order = 2, Category = "身份")]
        public MPAProtocolKind Kind { get; set; } = MPAProtocolKind.LengthFrame;
        [FCMConfigItem("扩展规约 TypeId", Order = 3, Category = "身份")]
        public string CustomTypeId { get; set; } = "";
        [FCMConfigItem("通道引用 Id", Order = 10, Category = "绑定")]
        public string ChannelRef { get; set; } = "channel01";
        [FCMConfigItem("子通道选择规则", Order = 11, Category = "绑定")]
        public string SubChannelSelector { get; set; } = "";
        [FCMConfigItem("规约地址/站号", Order = 12, Category = "绑定")]
        public string ProtocolAddress { get; set; } = "";

        [FCMConfigItem("执行模式", Order = 20, Category = "执行")]
        public MPAProtocolProcessingMode ProcessingMode { get; set; } = MPAProtocolProcessingMode.Serial;
        [FCMConfigItem("会话隔离范围", Order = 21, Category = "执行")]
        public MPAProtocolSessionScope SessionScope { get; set; } = MPAProtocolSessionScope.Binding;
        [FCMConfigItem("最大并发", Order = 22, Category = "执行")]
        [Range(1, 4096)]
        public int MaxConcurrency { get; set; } = 1;
        [FCMConfigItem("待处理任务容量", Order = 23, Category = "执行")]
        [Range(1, 1000000)]
        public int QueueCapacity { get; set; } = 128;
        [FCMConfigItem("队列字节预算", Order = 24, Category = "执行", Unit = "Bytes")]
        [Range(1, int.MaxValue)]
        public int QueuedBytesLimit { get; set; } = 4 * 1024 * 1024;
        [FCMConfigItem("单次处理超时", Order = 25, Category = "执行", Unit = "ms")]
        [Range(1, 3600000)]
        public int TimeoutMs { get; set; } = 5000;
        [FCMConfigItem("F19/MRK 执行组引用", Order = 26, Category = "执行")]
        public string ExecutionGroupRef { get; set; } = "";

        public MPAProtocolBinding() { }

        // 直接提供开发者简单用法，构造后仍使用同一份 FCM 参数；无 ToOptions() 双配置转换。
        public MPAProtocolBinding(string id, string protocolTypeId, string channelRef = null,
            string subChannelSelector = null, MPAProtocolAddress protocolAddress = null)
        {
            Id = id; ChannelRef = channelRef ?? "";
            SubChannelSelector = subChannelSelector ?? "";
            ProtocolAddress = protocolAddress == null ? "" : protocolAddress.ToString();
            switch (protocolTypeId)
            {
                case "length.frame": Kind = MPAProtocolKind.LengthFrame; break;
                case "modbus.tcp": Kind = MPAProtocolKind.ModbusTcpDraft; break;
                case "modbus.rtu": Kind = MPAProtocolKind.ModbusRtuDraft; break;
                case "internal": Kind = MPAProtocolKind.Internal; break;
                default: Kind = MPAProtocolKind.Custom; CustomTypeId = protocolTypeId; break;
            }
            Validate();
        }
        // 派生身份不进入持久化 Schema，不构成第二套类型配置。
        public string ProtocolTypeId
        {
            get
            {
                switch (Kind)
                {
                    case MPAProtocolKind.LengthFrame: return "length.frame";
                    case MPAProtocolKind.ModbusTcpDraft: return "modbus.tcp";
                    case MPAProtocolKind.ModbusRtuDraft: return "modbus.rtu";
                    case MPAProtocolKind.Internal: return "internal";
                    default: return CustomTypeId;
                }
            }
        }
        public bool RequiresChannel { get { return Kind != MPAProtocolKind.Internal; } }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Id) || !Enum.IsDefined(typeof(MPAProtocolKind), Kind) ||
                !Enum.IsDefined(typeof(MPAProtocolProcessingMode), ProcessingMode) ||
                !Enum.IsDefined(typeof(MPAProtocolSessionScope), SessionScope))
                throw new ArgumentException("规约 Id、类型或处理策略无效");
            if (Kind == MPAProtocolKind.Custom && string.IsNullOrWhiteSpace(CustomTypeId))
                throw new ArgumentException("Custom Protocol 必须指定 TypeId");
            if (RequiresChannel && string.IsNullOrWhiteSpace(ChannelRef))
                throw new ArgumentException("规约缺少 Channel 引用");
            if (!RequiresChannel && !string.IsNullOrWhiteSpace(ChannelRef))
                throw new ArgumentException("内部规约不可强制绑定 Channel");
            if (!RequiresChannel && !string.IsNullOrWhiteSpace(SubChannelSelector))
                throw new ArgumentException("无通道规约不能指定子通道");
            if (MaxConcurrency < 1 || QueueCapacity < 1 || QueuedBytesLimit < 1 || TimeoutMs < 1)
                throw new ArgumentException("规约处理预算不合法");
            if ((ProcessingMode == MPAProtocolProcessingMode.Inline || ProcessingMode == MPAProtocolProcessingMode.Serial) &&
                MaxConcurrency != 1)
                throw new ArgumentException("Inline/Serial 只能串行");
            if (ProcessingMode == MPAProtocolProcessingMode.Runtime && string.IsNullOrWhiteSpace(ExecutionGroupRef))
                throw new ArgumentException("Runtime 模式需引用已有执行组");
        }

        public void ValidateCompatibility(MPAProtocolDescriptor descriptor, MCCChannelTypeDescriptor channel)
        {
            Validate();
            if (descriptor == null || !string.Equals(descriptor.Id, ProtocolTypeId, StringComparison.Ordinal))
                throw new ArgumentException("规约类型不匹配");
            if (RequiresChannel && channel == null)
                throw new ArgumentException("找不到规约引用的 Channel");
            if (descriptor.RequiredIO != MCCIOCapabilities.None &&
                (channel == null || (channel.IO & descriptor.RequiredIO) != descriptor.RequiredIO))
                throw new ArgumentException("规约所需 I/O 能力与通道不兼容");
        }
    }

    public sealed class MPAProtocolRoute
    {
        public MPAProtocolRoute(string bindingId, MCCSubChannelAddress channelAddress, MPAProtocolAddress protocolAddress = null)
        {
            if (string.IsNullOrWhiteSpace(bindingId)) throw new ArgumentException("BindingId 不能为空");
            BindingId = bindingId.Trim(); ChannelAddress = channelAddress; ProtocolAddress = protocolAddress;
        }
        public string BindingId { get; private set; }
        public MCCSubChannelAddress ChannelAddress { get; private set; }
        public MPAProtocolAddress ProtocolAddress { get; private set; }
    }
}
