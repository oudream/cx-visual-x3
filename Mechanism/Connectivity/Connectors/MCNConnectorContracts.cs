using System;
using System.ComponentModel.DataAnnotations;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Mechanism.Core.Modules;

namespace CxVisual.Mechanism.Connectivity
{
    // 设备点的唯一配置：地址、类型、规约引用和 MDS/消息/命令目标一起定义。
    public sealed class MCNPointDefinition
    {
        [FCMConfigItem("点名称", Order = 1, Category = "点身份")]
        [Required]
        public string Name { get; set; } = "temperature";

        [FCMConfigItem("规约绑定 Id", Order = 2, Category = "点身份")]
        public string ProtocolRef { get; set; } = "protocol01";

        [FCMConfigItem("外部地址", Order = 3, Category = "地址")]
        [Required]
        public string Address { get; set; } = "holding:100";

        [FCMConfigItem("数据类型 Id", Order = 4, Category = "地址")]
        [Required]
        public string ValueType { get; set; } = "UInt16";

        [FCMConfigItem("目标类型", Order = 5, Category = "数据映射")]
        public MCNPointTargetKind TargetKind { get; set; } = MCNPointTargetKind.Variable;

        [FCMConfigItem("数据目标引用", Order = 6, Category = "数据映射")]
        [Required]
        public string TargetRef { get; set; } = "PLC01.Temperature";

        [FCMConfigItem("站号/规约设备地址", Order = 6, Category = "地址",
            Description = "适用于共享 RS-485 等多站号点表；不创建第二个 Serial/Protocol 或 SubChannel")]
        public string StationAddress { get; set; } = "";

        [FCMConfigItem("允许读取", Order = 7, Category = "权限")]
        public bool CanRead { get; set; } = true;

        [FCMConfigItem("允许写入", Order = 8, Category = "权限")]
        public bool CanWrite { get; set; }

        public MCNPointDefinition() { }

        public MCNPointDefinition(string name, string address, Type valueType)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(address) ||
                valueType == null || valueType == typeof(void))
                throw new ArgumentException("点名称、地址或类型无效");
            Name = name.Trim(); Address = address.Trim(); ValueType = valueType.Name;
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Address) ||
                string.IsNullOrWhiteSpace(ValueType) || string.IsNullOrWhiteSpace(TargetRef) ||
                !Enum.IsDefined(typeof(MCNPointTargetKind), TargetKind))
                throw new ArgumentException("Point 定义/目标不合法");
        }
    }

    public interface IMCNConnectorModule : IMFMModule
    {
        System.Collections.Generic.IReadOnlyList<IMCCChannel> Channels { get; }
        System.Collections.Generic.IReadOnlyList<IMPAProtocolDefinition> Protocols { get; }
        System.Collections.Generic.IReadOnlyList<MCNPointDefinition> Points { get; }
    }
}
