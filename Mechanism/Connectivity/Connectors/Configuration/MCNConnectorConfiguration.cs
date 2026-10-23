using System;
using System.Collections.Generic;
using System.Linq;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Mechanism.Core.Modules;

namespace CxVisual.Mechanism.Connectivity
{
    // Connector 默认只描述一条独立接入链路；确实需要冗余/复合时显式使用高级组合。
    public enum MCNConnectorCompositionMode { Standard, Advanced }

    // 唯一 Connector 配置定义；FCM/MHS/MPW、验证、运行装配使用相同的类型。
    // 不再存在 EditableConfig 或只读配置镜像，ModuleId 来自 MEM/MHS 资源 Scope。
    [FCMConfigSection("CxVisual.Module.Connector", SchemaVersion = 1)]
    [FCMConfigValidator(typeof(MCNConnectorConfigurationValidator))]
    public sealed class MCNConnectorConfiguration : MFMModuleConfiguration
    {
        [FCMConfigItem("接入组合模式", Order = 5, Category = "基本信息",
            Description = "默认单通道/单规约；冗余等特殊需求才使用高级多组件组合")]
        public MCNConnectorCompositionMode CompositionMode { get; set; } = MCNConnectorCompositionMode.Standard;

        [FCMConfigItem("通道列表", Order = 10, Category = "通道")]
        public List<MCCChannelConfiguration> Channels { get; set; } = new List<MCCChannelConfiguration>();

        [FCMConfigItem("规约绑定列表", Order = 20, Category = "规约")]
        public List<MPAProtocolBinding> Protocols { get; set; } = new List<MPAProtocolBinding>();

        [FCMConfigItem("点表", Order = 30, Category = "点表")]
        public List<MCNPointDefinition> Points { get; set; } = new List<MCNPointDefinition>();

        public void Validate()
        {
            FCMConfigIssue issue = new MCNConnectorConfigurationValidator()
                .Validate(this, null).FirstOrDefault();
            if (issue != null) throw new ArgumentException(issue.Code + "：" + issue.Message);
        }

        // TypeDescriptor 是注册类型的运行元数据。尚未解析的专业类型必须拒绝，
        // 不能将配置结构校验当成真正通道/规约能力已就绪。
        public void ValidateProtocolCapabilities(IReadOnlyDictionary<string, MPAProtocolDescriptor> descriptions)
        {
            if (descriptions == null) throw new ArgumentNullException("descriptions");
            Validate();
            Dictionary<string, MCCChannelConfiguration> channels = Channels
                .ToDictionary(x => x.Id, x => x, StringComparer.Ordinal);
            foreach (MPAProtocolBinding binding in Protocols)
            {
                MPAProtocolDescriptor descriptor;
                if (!descriptions.TryGetValue(binding.ProtocolTypeId, out descriptor) || descriptor == null)
                    throw new InvalidOperationException("规约类型未注册/解析：" + binding.ProtocolTypeId);
                MCCChannelTypeDescriptor type = null;
                if (binding.RequiresChannel)
                    type = channels[binding.ChannelRef].DescribeType();
                binding.ValidateCompatibility(descriptor, type);
            }
        }
    }

    public sealed class MCNConnectorConfigurationValidator : IFCMConfigValidator<MCNConnectorConfiguration>
    {
        public IEnumerable<FCMConfigIssue> Validate(
            MCNConnectorConfiguration config, FCMConfigValidationContext context)
        {
            List<FCMConfigIssue> issues = new List<FCMConfigIssue>();
            if (config == null)
                return new[] { FCMConfigIssue.Error("", "MODULE_NULL", "模块配置为空") };
            if (string.IsNullOrWhiteSpace(config.DisplayName))
                issues.Add(FCMConfigIssue.Error("DisplayName", "MODULE_NAME_REQUIRED", "模块名称不能为空"));
            if (config.Channels == null || config.Protocols == null || config.Points == null)
            {
                issues.Add(FCMConfigIssue.Error("", "COLLECTION_NULL", "组件集合不能为空"));
                return issues;
            }
            if (!Enum.IsDefined(typeof(MCNConnectorCompositionMode), config.CompositionMode))
                issues.Add(FCMConfigIssue.Error("CompositionMode", "CONNECTOR_MODE_INVALID", "未知 Connector 组合模式"));
            if (config.CompositionMode == MCNConnectorCompositionMode.Standard &&
                (config.Channels.Count > 1 || config.Protocols.Count > 1))
                issues.Add(FCMConfigIssue.Error("CompositionMode", "CONNECTOR_ADVANCED_REQUIRED",
                    "多个父通道或多个规约必须显式启用高级组合；多客户端/多站号不应创建多个父通道或规约"));
            HashSet<string> channels = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < config.Channels.Count; i++)
            {
                MCCChannelConfiguration channel = config.Channels[i];
                string path = "Channels[" + i + "]";
                if (channel == null || string.IsNullOrWhiteSpace(channel.Id) ||
                    !channels.Add(channel.Id.Trim()))
                {
                    issues.Add(FCMConfigIssue.Error(path, "CHANNEL_DUPLICATE_OR_EMPTY", "通道 Id 必填且不得重复"));
                    continue;
                }
                try { channel.Validate(); }
                catch (ArgumentException error)
                {
                    issues.Add(FCMConfigIssue.Error(path, "CHANNEL_INVALID", error.Message));
                }
                if (channel.QueueCapacity < 1)
                    issues.Add(FCMConfigIssue.Error(path + ".QueueCapacity", "CHANNEL_CAPACITY", "接收队列容量应大于零"));
                if (channel.Kind == MCCChannelKind.Custom && string.IsNullOrWhiteSpace(channel.CustomTypeId))
                    issues.Add(FCMConfigIssue.Error(path + ".CustomTypeId", "CHANNEL_TYPE_REQUIRED", "专业通道必须指定 TypeId"));
                if ((channel.Kind == MCCChannelKind.TcpClient || channel.Kind == MCCChannelKind.TcpServer ||
                    channel.Kind == MCCChannelKind.Udp || channel.Kind == MCCChannelKind.UdpServer) &&
                    (string.IsNullOrWhiteSpace(channel.Host) || channel.Port < 1 || channel.Port > 65535))
                    issues.Add(FCMConfigIssue.Error(path + ".Port", "CHANNEL_ENDPOINT", "网络通道地址/端口无效"));
            }

            HashSet<string> protocols = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < config.Protocols.Count; i++)
            {
                MPAProtocolBinding protocol = config.Protocols[i];
                string path = "Protocols[" + i + "]";
                if (protocol == null || string.IsNullOrWhiteSpace(protocol.Id) ||
                    !protocols.Add(protocol.Id.Trim()))
                {
                    issues.Add(FCMConfigIssue.Error(path, "PROTOCOL_DUPLICATE_OR_EMPTY", "规约 Id 必填且不得重复"));
                    continue;
                }
                try { protocol.Validate(); }
                catch (ArgumentException error)
                {
                    issues.Add(FCMConfigIssue.Error(path, "PROTOCOL_INVALID", error.Message));
                }
                if (protocol.Kind == MPAProtocolKind.Custom && string.IsNullOrWhiteSpace(protocol.CustomTypeId))
                    issues.Add(FCMConfigIssue.Error(path + ".CustomTypeId", "PROTOCOL_TYPE_REQUIRED", "专业规约必须指定 TypeId"));
                if (protocol.RequiresChannel && !channels.Contains(protocol.ChannelRef ?? ""))
                    issues.Add(FCMConfigIssue.Error(path + ".ChannelRef", "CHANNEL_REFERENCE", "规约引用的通道不存在"));
                if (protocol.Kind == MPAProtocolKind.Internal && !string.IsNullOrWhiteSpace(protocol.ChannelRef))
                    issues.Add(FCMConfigIssue.Error(path + ".ChannelRef", "INTERNAL_CHANNEL_FORBIDDEN", "内部规约不使用 Channel"));
                if (protocol.MaxConcurrency < 1 ||
                    ((protocol.ProcessingMode == MPAProtocolProcessingMode.Inline ||
                      protocol.ProcessingMode == MPAProtocolProcessingMode.Serial) && protocol.MaxConcurrency != 1))
                    issues.Add(FCMConfigIssue.Error(path + ".MaxConcurrency", "PROTOCOL_CONCURRENCY", "规约并发配置无效"));
                if (protocol.ProcessingMode == MPAProtocolProcessingMode.Runtime &&
                    string.IsNullOrWhiteSpace(protocol.ExecutionGroupRef))
                    issues.Add(FCMConfigIssue.Error(path + ".ExecutionGroupRef", "EXECUTION_GROUP_REQUIRED", "需引用执行组"));
                if (protocol.Kind == MPAProtocolKind.ModbusTcpDraft ||
                    protocol.Kind == MPAProtocolKind.ModbusRtuDraft)
                {
                    MCCChannelConfiguration channel = config.Channels.FirstOrDefault(c => c != null &&
                        string.Equals(c.Id, protocol.ChannelRef, StringComparison.Ordinal));
                    bool compatible = channel != null && (
                        protocol.Kind == MPAProtocolKind.ModbusTcpDraft ?
                            channel.Kind == MCCChannelKind.TcpClient || channel.Kind == MCCChannelKind.TcpServer :
                            channel.Kind == MCCChannelKind.Serial);
                    if (!compatible)
                        issues.Add(FCMConfigIssue.Error(path + ".ChannelRef", "PROTOCOL_IO_MISMATCH", "Modbus 通道类型不兼容"));
                }
            }

            // 高级组合时规约不可争抢同一个接收路径，必须给出可区分的子通道选择规则。
            HashSet<string> activeRoutes = new HashSet<string>(StringComparer.Ordinal);
            foreach (MPAProtocolBinding protocol in config.Protocols)
            {
                if (protocol == null || !protocol.RequiresChannel ||
                    string.IsNullOrWhiteSpace(protocol.ChannelRef)) continue;
                string route = protocol.ChannelRef + "/" + (protocol.SubChannelSelector ?? "");
                if (!activeRoutes.Add(route))
                    issues.Add(FCMConfigIssue.Error("Protocols", "PROTOCOL_ROUTE_CONFLICT",
                        "多个规约不能抢读同一 Channel/SubChannel；需要唯一绑定或由专业 Driver 自行处理分流"));
            }
            HashSet<string> points = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < config.Points.Count; i++)
            {
                MCNPointDefinition point = config.Points[i];
                string path = "Points[" + i + "]";
                if (point == null || string.IsNullOrWhiteSpace(point.Name) ||
                    !points.Add(point.Name.Trim()))
                {
                    issues.Add(FCMConfigIssue.Error(path, "POINT_DUPLICATE_OR_EMPTY", "点名称必填且不得重复"));
                    continue;
                }
                if (!protocols.Contains(point.ProtocolRef ?? ""))
                    issues.Add(FCMConfigIssue.Error(path + ".ProtocolRef", "POINT_PROTOCOL_MISSING", "点引用的规约不存在"));
                if (string.IsNullOrWhiteSpace(point.Address) || string.IsNullOrWhiteSpace(point.TargetRef))
                    issues.Add(FCMConfigIssue.Error(path, "POINT_TARGET_MISSING", "外部地址与数据目标必须指定"));
                MPAProtocolBinding pointProtocol = config.Protocols.FirstOrDefault(p => p != null &&
                    string.Equals(p.Id, point.ProtocolRef, StringComparison.Ordinal));
                if (pointProtocol != null &&
                    (pointProtocol.Kind == MPAProtocolKind.ModbusRtuDraft ||
                     pointProtocol.Kind == MPAProtocolKind.ModbusTcpDraft) &&
                    !string.IsNullOrWhiteSpace(point.StationAddress))
                {
                    int station;
                    if (!int.TryParse(point.StationAddress, out station) || station < 1 || station > 247)
                        issues.Add(FCMConfigIssue.Error(path + ".StationAddress", "POINT_STATION_INVALID",
                            "Modbus 站号应在 1～247 范围；它属于规约点表，不是 SubChannel"));
                }
                try { point.Validate(); }
                catch (ArgumentException error)
                {
                    issues.Add(FCMConfigIssue.Error(path, "POINT_INVALID", error.Message));
                }
            }
            return issues;
        }
    }
}
