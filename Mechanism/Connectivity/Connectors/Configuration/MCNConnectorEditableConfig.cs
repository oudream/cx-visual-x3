using System;
using System.Collections.Generic;
using System.Linq;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Mechanism.Core.Modules;

namespace CxVisual.Mechanism.Connectivity
{
    // 自动 UI 使用已有 FCM Schema，实际 Channel/Protocol 执行仍归生产 Owner。
    // TypeId 以后由注册的专业 Provider 枚举；目前已知/待迁入类型以枚举显式列出，不展示“已运行”假状态。
    public enum MCNEditableChannelKind { Virtual, TcpClient, TcpServer, Udp, Serial, Custom }
    public enum MCNEditableProtocolKind { LengthFrame, ModbusTcpDraft, ModbusRtuDraft, Internal, Custom }

    public sealed class MCNEditableChannel
    {
        [FCMConfigItem("通道 Id", Order = 1, Category = "身份")]
        public string Id { get; set; } = "channel01";
        [FCMConfigItem("通道类型", Order = 2, Category = "身份",
            Description = "TCP/UDP/Serial/Modbus 的实际通信功能需待 ReferTo 正式迁移；当前仅验证结构与配置")]
        public MCNEditableChannelKind Kind { get; set; } = MCNEditableChannelKind.Virtual;
        [FCMConfigItem("专业扩展 TypeId", Order = 3, Category = "身份")]
        public string CustomTypeId { get; set; } = "";
        [FCMConfigItem("主机/监听地址", Order = 10, Category = "通信参数")]
        public string Host { get; set; } = "127.0.0.1";
        [FCMConfigItem("端口号", Order = 11, Category = "通信参数")]
        public int Port { get; set; } = 502;
        [FCMConfigItem("串口名称", Order = 12, Category = "串口参数")]
        public string PortName { get; set; } = "COM1";
        [FCMConfigItem("波特率", Order = 13, Category = "串口参数")]
        public int BaudRate { get; set; } = 9600;
        [FCMConfigItem("最大客户端", Order = 14, Category = "服务端参数")]
        public int MaxClients { get; set; } = 128;
        [FCMConfigItem("子通道选择策略", Order = 15, Category = "子通道")]
        public string SubChannelRule { get; set; } = "";
        [FCMConfigItem("I/O 专用线程", Order = 16, Category = "执行配置",
            Description = "默认异步 I/O；仅底层强制线程亲和才选择 DedicatedBlocking")]
        public MCCChannelIOMode IoMode { get; set; } = MCCChannelIOMode.AsyncIO;
        [FCMConfigItem("接收队列容量", Order = 17, Category = "执行配置")]
        public int QueueCapacity { get; set; } = 128;
    }

    public sealed class MCNEditableProtocol
    {
        [FCMConfigItem("规约绑定 Id", Order = 1)]
        public string Id { get; set; } = "protocol01";
        [FCMConfigItem("规约类型", Order = 2,
            Description = "Draft 类型仅用于编辑及兼容性验证，未接入实机驱动")]
        public MCNEditableProtocolKind Kind { get; set; } = MCNEditableProtocolKind.LengthFrame;
        [FCMConfigItem("专业扩展 TypeId", Order = 3)]
        public string CustomTypeId { get; set; } = "";
        [FCMConfigItem("通道引用 Id", Order = 4,
            Description = "Internal 可以为空，其他规约必须引用同一模块内的通道")]
        public string ChannelRef { get; set; } = "channel01";
        [FCMConfigItem("子通道选择", Order = 5)]
        public string SubChannelSelector { get; set; } = "";
        [FCMConfigItem("规约地址/站号", Order = 6)]
        public string ProtocolAddress { get; set; } = "";
        [FCMConfigItem("处理方式", Order = 7)]
        public MPAProtocolProcessingMode ProcessingMode { get; set; } = MPAProtocolProcessingMode.Serial;
        [FCMConfigItem("最大并发", Order = 8)]
        public int MaxConcurrency { get; set; } = 1;
        [FCMConfigItem("任务执行组引用", Order = 9)]
        public string ExecutionGroupRef { get; set; } = "";
    }

    public sealed class MCNEditablePoint
    {
        [FCMConfigItem("点名称", Order = 1)]
        public string Name { get; set; } = "temperature";
        [FCMConfigItem("所属规约绑定", Order = 2)]
        public string ProtocolRef { get; set; } = "protocol01";
        [FCMConfigItem("点地址", Order = 3)]
        public string Address { get; set; } = "holding:100";
        [FCMConfigItem("值类型", Order = 4,
            Description = "类型 Id 将通过 Foundation.Data 类型目录进一步校验")]
        public string ValueType { get; set; } = "UInt16";
        [FCMConfigItem("映射目标类别", Order = 5)]
        public MCNPointTargetKind TargetKind { get; set; } = MCNPointTargetKind.Variable;
        [FCMConfigItem("MDS / Message / Command 引用", Order = 6)]
        public string TargetRef { get; set; } = "PLC01.Temperature";
        [FCMConfigItem("允许读取", Order = 7)]
        public bool CanRead { get; set; } = true;
        [FCMConfigItem("允许写入", Order = 8)]
        public bool CanWrite { get; set; }
    }

    [FCMConfigSection("CxVisual.Module.Connector", SchemaVersion = 1)]
    [FCMConfigValidator(typeof(MCNConnectorEditableValidator))]
    public sealed class MCNConnectorEditableConfig : MFMModuleEditableConfig
    {
        [FCMConfigItem("通道列表", Order = 10, Category = "通道")]
        public List<MCNEditableChannel> Channels { get; set; } = new List<MCNEditableChannel>();

        [FCMConfigItem("规约绑定列表", Order = 20, Category = "规约")]
        public List<MCNEditableProtocol> Protocols { get; set; } = new List<MCNEditableProtocol>();

        [FCMConfigItem("点表", Order = 30, Category = "点表")]
        public List<MCNEditablePoint> Points { get; set; } = new List<MCNEditablePoint>();
    }

    // 配置体系的正式 Validator，同一规则被自动编辑、保存和 TestCenter 调用。
    public sealed class MCNConnectorEditableValidator : IFCMConfigValidator<MCNConnectorEditableConfig>
    {
        public IEnumerable<FCMConfigIssue> Validate(MCNConnectorEditableConfig config, FCMConfigValidationContext context)
        {
            List<FCMConfigIssue> issues = new List<FCMConfigIssue>();
            if (config == null) return new[] { FCMConfigIssue.Error("", "MODULE_NULL", "模块配置为空") };
            if (string.IsNullOrWhiteSpace(config.DisplayName))
                issues.Add(FCMConfigIssue.Error("DisplayName", "MODULE_NAME_REQUIRED", "模块显示名称不能为空"));
            if (config.Channels == null || config.Protocols == null || config.Points == null)
            {
                issues.Add(FCMConfigIssue.Error("", "COLLECTION_NULL", "组件集合不能为空"));
                return issues;
            }
            HashSet<string> channelIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < config.Channels.Count; i++)
            {
                MCNEditableChannel channel = config.Channels[i];
                string path = "Channels[" + i + "]";
                if (channel == null || string.IsNullOrWhiteSpace(channel.Id) || !channelIds.Add(channel.Id.Trim()))
                { issues.Add(FCMConfigIssue.Error(path, "CHANNEL_DUPLICATE_OR_EMPTY", "通道 Id 必填且不可重复")); continue; }
                if (channel.QueueCapacity < 1)
                    issues.Add(FCMConfigIssue.Error(path + ".QueueCapacity", "CHANNEL_CAPACITY", "接收队列容量必须为正数"));
                if (channel.Kind == MCNEditableChannelKind.Custom && string.IsNullOrWhiteSpace(channel.CustomTypeId))
                    issues.Add(FCMConfigIssue.Error(path + ".CustomTypeId", "CHANNEL_TYPE_REQUIRED", "专业通道需要 TypeId"));
                if (channel.Kind == MCNEditableChannelKind.TcpClient ||
                    channel.Kind == MCNEditableChannelKind.TcpServer || channel.Kind == MCNEditableChannelKind.Udp)
                {
                    if (string.IsNullOrWhiteSpace(channel.Host) || channel.Port < 1 || channel.Port > 65535)
                        issues.Add(FCMConfigIssue.Error(path + ".Port", "CHANNEL_ENDPOINT", "网络通道地址和端口无效"));
                }
                if (channel.Kind == MCNEditableChannelKind.Serial &&
                    (string.IsNullOrWhiteSpace(channel.PortName) || channel.BaudRate < 1))
                    issues.Add(FCMConfigIssue.Error(path + ".PortName", "CHANNEL_SERIAL", "串口参数无效"));
                if (channel.Kind == MCNEditableChannelKind.TcpServer && channel.MaxClients < 1)
                    issues.Add(FCMConfigIssue.Error(path + ".MaxClients", "CHANNEL_CLIENTS", "最大客户端数必须为正数"));
            }
            HashSet<string> protocolIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < config.Protocols.Count; i++)
            {
                MCNEditableProtocol protocol = config.Protocols[i];
                string path = "Protocols[" + i + "]";
                if (protocol == null || string.IsNullOrWhiteSpace(protocol.Id) || !protocolIds.Add(protocol.Id.Trim()))
                { issues.Add(FCMConfigIssue.Error(path, "PROTOCOL_DUPLICATE_OR_EMPTY", "规约绑定 Id 必填且不可重复")); continue; }
                bool internalOnly = protocol.Kind == MCNEditableProtocolKind.Internal;
                if (!internalOnly && (string.IsNullOrWhiteSpace(protocol.ChannelRef) || !channelIds.Contains(protocol.ChannelRef)))
                    issues.Add(FCMConfigIssue.Error(path + ".ChannelRef", "CHANNEL_REFERENCE", "规约引用的通道不存在"));
                if (internalOnly && !string.IsNullOrWhiteSpace(protocol.ChannelRef))
                    issues.Add(FCMConfigIssue.Error(path + ".ChannelRef", "INTERNAL_CHANNEL_FORBIDDEN", "内部规约不需通道引用"));
                if (protocol.Kind == MCNEditableProtocolKind.Custom && string.IsNullOrWhiteSpace(protocol.CustomTypeId))
                    issues.Add(FCMConfigIssue.Error(path + ".CustomTypeId", "PROTOCOL_TYPE_REQUIRED", "专业规约需要 TypeId"));
                if (protocol.MaxConcurrency < 1 || ((protocol.ProcessingMode == MPAProtocolProcessingMode.Inline ||
                    protocol.ProcessingMode == MPAProtocolProcessingMode.Serial) && protocol.MaxConcurrency != 1))
                    issues.Add(FCMConfigIssue.Error(path + ".MaxConcurrency", "PROTOCOL_CONCURRENCY", "规约并发配置不合法"));
                if (protocol.ProcessingMode == MPAProtocolProcessingMode.Runtime &&
                    string.IsNullOrWhiteSpace(protocol.ExecutionGroupRef))
                    issues.Add(FCMConfigIssue.Error(path + ".ExecutionGroupRef", "EXECUTION_GROUP_REQUIRED", "Runtime 模式需引用执行组"));
                if (protocol.Kind == MCNEditableProtocolKind.ModbusTcpDraft ||
                    protocol.Kind == MCNEditableProtocolKind.ModbusRtuDraft)
                {
                    MCNEditableChannel channel = config.Channels.FirstOrDefault(x => x != null &&
                        string.Equals(x.Id, protocol.ChannelRef, StringComparison.Ordinal));
                    bool compatible = channel != null &&
                        (protocol.Kind == MCNEditableProtocolKind.ModbusTcpDraft ?
                         (channel.Kind == MCNEditableChannelKind.TcpClient || channel.Kind == MCNEditableChannelKind.TcpServer) :
                         channel.Kind == MCNEditableChannelKind.Serial);
                    if (!compatible)
                        issues.Add(FCMConfigIssue.Error(path + ".ChannelRef", "PROTOCOL_IO_MISMATCH", "Modbus 通道类型不兼容"));
                }
            }
            HashSet<string> pointNames = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < config.Points.Count; i++)
            {
                MCNEditablePoint point = config.Points[i];
                string path = "Points[" + i + "]";
                if (point == null || string.IsNullOrWhiteSpace(point.Name) || !pointNames.Add(point.Name.Trim()))
                { issues.Add(FCMConfigIssue.Error(path, "POINT_DUPLICATE_OR_EMPTY", "点名称必填且不可重复")); continue; }
                if (!protocolIds.Contains(point.ProtocolRef ?? ""))
                    issues.Add(FCMConfigIssue.Error(path + ".ProtocolRef", "POINT_PROTOCOL_MISSING", "点表引用的规约不存在"));
                if (string.IsNullOrWhiteSpace(point.Address) || string.IsNullOrWhiteSpace(point.TargetRef))
                    issues.Add(FCMConfigIssue.Error(path, "POINT_TARGET_MISSING", "点地址和数据目标不能为空"));
            }
            return issues;
        }
    }
}
