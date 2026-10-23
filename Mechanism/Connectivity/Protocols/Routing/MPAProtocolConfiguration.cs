using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Mechanism.Connectivity
{
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

    // 追加 Empty，不改变历史配置中已有枚举整数的语义。
    public enum MPAProtocolKind { LengthFrame, ModbusTcpDraft, ModbusRtuDraft, Internal, Custom, Empty }

    // 唯一规约配置：一个 Connector 持有一份；包含寻址点表、映射及运行策略。
    public sealed class MPAProtocolConfiguration
    {
        [FCMConfigItem("规约 Id", Order = 1, Category = "身份")] [Required]
        public string Id { get; set; } = "protocol01";
        [FCMConfigItem("规约类型", Order = 2, Category = "身份")]
        public MPAProtocolKind Kind { get; set; } = MPAProtocolKind.Empty;
        [FCMConfigItem("专业规约类型 Id", Order = 3, Category = "身份")]
        public string CustomTypeId { get; set; } = "";

        [FCMConfigItem("规约地址", Order = 11, Category = "寻址")]
        public string ProtocolAddress { get; set; } = "";
        [FCMConfigItem("规约点表", Order = 12, Category = "数据")]
        public List<MPAPointDefinition> Points { get; set; } = new List<MPAPointDefinition>();
        [FCMConfigItem("数据映射", Order = 13, Category = "数据")]
        public List<MPADataMapping> Mappings { get; set; } = new List<MPADataMapping>();

        [FCMConfigItem("处理模式", Order = 20, Category = "执行")]
        public MPAProtocolProcessingMode ProcessingMode { get; set; } = MPAProtocolProcessingMode.Serial;
        [FCMConfigItem("会话范围", Order = 21, Category = "执行")]
        public MPAProtocolSessionScope SessionScope { get; set; } = MPAProtocolSessionScope.Binding;
        [FCMConfigItem("最大并发数", Order = 22, Category = "执行")] [Range(1, 4096)]
        public int MaxConcurrency { get; set; } = 1;
        [FCMConfigItem("队列容量", Order = 23, Category = "执行")] [Range(1, 1000000)]
        public int QueueCapacity { get; set; } = 128;
        [FCMConfigItem("队列字节上限", Order = 24, Category = "执行")] [Range(1, int.MaxValue)]
        public int QueuedBytesLimit { get; set; } = 4 * 1024 * 1024;
        [FCMConfigItem("处理超时", Order = 25, Category = "执行", Unit = "ms")] [Range(1, 3600000)]
        public int TimeoutMs { get; set; } = 5000;
        [FCMConfigItem("F19/MRK执行组引用", Order = 26, Category = "执行")]
        public string ExecutionGroupRef { get; set; } = "";

        public MPAProtocolConfiguration() { }
        public MPAProtocolConfiguration(string id, string typeId)
        {
            Id = id;
            switch (typeId)
            {
                case "empty": Kind = MPAProtocolKind.Empty; break;
                case "length.frame": Kind = MPAProtocolKind.LengthFrame; break;
                case "modbus.tcp": Kind = MPAProtocolKind.ModbusTcpDraft; break;
                case "modbus.rtu": Kind = MPAProtocolKind.ModbusRtuDraft; break;
                case "internal": Kind = MPAProtocolKind.Internal; break;
                default: Kind = MPAProtocolKind.Custom; CustomTypeId = typeId; break;
            }
            Validate();
        }
        public string ProtocolTypeId
        {
            get
            {
                switch (Kind)
                {
                    case MPAProtocolKind.Empty: return "empty";
                    case MPAProtocolKind.LengthFrame: return "length.frame";
                    case MPAProtocolKind.ModbusTcpDraft: return "modbus.tcp";
                    case MPAProtocolKind.ModbusRtuDraft: return "modbus.rtu";
                    case MPAProtocolKind.Internal: return "internal";
                    default: return CustomTypeId;
                }
            }
        }
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Id) ||
                !Enum.IsDefined(typeof(MPAProtocolKind), Kind) ||
                !Enum.IsDefined(typeof(MPAProtocolProcessingMode), ProcessingMode) ||
                !Enum.IsDefined(typeof(MPAProtocolSessionScope), SessionScope) ||
                (Kind == MPAProtocolKind.Custom && string.IsNullOrWhiteSpace(CustomTypeId)))
                throw new ArgumentException("规约身份、类型或处理策略无效");
            if (MaxConcurrency < 1 || QueueCapacity < 1 || QueuedBytesLimit < 1 || TimeoutMs < 1 ||
                ((ProcessingMode == MPAProtocolProcessingMode.Inline ||
                  ProcessingMode == MPAProtocolProcessingMode.Serial) && MaxConcurrency != 1) ||
                (ProcessingMode == MPAProtocolProcessingMode.Runtime && string.IsNullOrWhiteSpace(ExecutionGroupRef)))
                throw new ArgumentException("规约并发、队列或执行组参数无效");
            if (Points == null || Mappings == null) throw new ArgumentException("点表及数据映射不可为 null");
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            foreach (MPAPointDefinition point in Points)
            {
                if (point == null) throw new ArgumentException("点项不可为空");
                point.Validate();
                if (!names.Add(point.Name)) throw new ArgumentException("点名称重复：" + point.Name);
                if ((Kind == MPAProtocolKind.ModbusRtuDraft || Kind == MPAProtocolKind.ModbusTcpDraft) &&
                    !string.IsNullOrWhiteSpace(point.StationAddress))
                {
                    int unit;
                    if (!int.TryParse(point.StationAddress, out unit) || unit < 1 || unit > 247)
                        throw new ArgumentException("Modbus 点表站号应为 1～247");
                }
            }
            foreach (MPADataMapping item in Mappings)
            {
                if (item == null) throw new ArgumentException("数据映射项不可为 null");
                item.Validate();
                if (!names.Contains(item.PointRef))
                    throw new ArgumentException("映射点引用不存在：" + item.PointRef);
            }
        }
        public void ValidateCompatibility(MPAProtocolDescriptor descriptor, MCCChannelTypeDescriptor channel)
        {
            Validate();
            if (descriptor == null || descriptor.Id != ProtocolTypeId)
                throw new ArgumentException("规约类型描述不匹配");
            if (channel == null) throw new ArgumentException("Connector 必须绑定一个通道");
            if (descriptor.RequiredIO != MCCIOCapabilities.None &&
                (channel.IO & descriptor.RequiredIO) != descriptor.RequiredIO)
                throw new ArgumentException("通道不满足规约所需 I/O 能力");
        }
    }

    public sealed class MPAProtocolRoute
    {
        public MPAProtocolRoute(string protocolId, MCCSubChannelAddress channelAddress, MPAProtocolAddress protocolAddress = null)
        {
            if (string.IsNullOrWhiteSpace(protocolId)) throw new ArgumentException("Protocol Id 必填");
            ProtocolId = protocolId.Trim(); ChannelAddress = channelAddress; ProtocolAddress = protocolAddress;
        }
        public string ProtocolId { get; private set; }
        public MCCSubChannelAddress ChannelAddress { get; private set; }
        public MPAProtocolAddress ProtocolAddress { get; private set; }
    }
}
