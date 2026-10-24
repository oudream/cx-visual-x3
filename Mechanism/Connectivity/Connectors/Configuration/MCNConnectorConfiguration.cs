using System;
using System.Collections.Generic;
using System.Linq;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Mechanism.Core.Modules;

namespace CxVisual.Mechanism.Connectivity
{
    // 唯一工程配置：Connector 恰有一个 Channel 和一个 Protocol。
    // 多客户端归 Channel，多站号与数据映射归 Protocol；无网络的专有 SDK 归 Driver。
    [FCMConfigSection("CxVisual.Module.Connector", SchemaVersion = 2)]
    [FCMConfigValidator(typeof(MCNConnectorConfigurationValidator))]
    public sealed class MCNConnectorConfiguration : MFMModuleConfiguration
    {
        [FCMConfigItem("通道", Order = 10, Category = "通道")]
        public MCCChannelConfiguration Channel { get; set; } = new MCCVirtualChannelConfiguration();

        [FCMConfigItem("规约", Order = 20, Category = "规约")]
        public MPAProtocolConfiguration Protocol { get; set; } = new MPAProtocolConfiguration();

        public void Validate()
        {
            FCMConfigIssue issue = new MCNConnectorConfigurationValidator()
                .Validate(this, null).FirstOrDefault();
            if (issue != null) throw new ArgumentException(issue.Code + "：" + issue.Message);
        }

        public void ValidateProtocolCapabilities(IReadOnlyDictionary<string, MPAProtocolDescriptor> types)
        {
            if (types == null) throw new ArgumentNullException("types");
            Validate();
            MPAProtocolDescriptor descriptor;
            if (!types.TryGetValue(Protocol.ProtocolTypeId, out descriptor) || descriptor == null)
                throw new InvalidOperationException("规约类型未注册：" + Protocol.ProtocolTypeId);
            Protocol.ValidateCompatibility(descriptor, Channel.DescribeType());
        }
    }

    public sealed class MCNConnectorConfigurationValidator : IFCMConfigValidator<MCNConnectorConfiguration>
    {
        public IEnumerable<FCMConfigIssue> Validate(
            MCNConnectorConfiguration value, FCMConfigValidationContext context)
        {
            List<FCMConfigIssue> errors = new List<FCMConfigIssue>();
            if (value == null)
                return new[] { FCMConfigIssue.Error("", "MODULE_NULL", "Connector 配置不能为空") };
            if (string.IsNullOrWhiteSpace(value.DisplayName))
                errors.Add(FCMConfigIssue.Error("DisplayName", "MODULE_NAME_REQUIRED", "模块名称必填"));
            if (value.Channel == null)
                errors.Add(FCMConfigIssue.Error("Channel", "CHANNEL_REQUIRED", "Connector 必须有一个 Channel"));
            else
            {
                try { value.Channel.Validate(); }
                catch (ArgumentException e) { errors.Add(FCMConfigIssue.Error("Channel", "CHANNEL_INVALID", e.Message)); }
            }
            if (value.Protocol == null)
                errors.Add(FCMConfigIssue.Error("Protocol", "PROTOCOL_REQUIRED",
                    "必须有一个 Protocol；如不需要解析请选择 Empty"));
            else
            {
                try { value.Protocol.Validate(); }
                catch (ArgumentException e) { errors.Add(FCMConfigIssue.Error("Protocol", "PROTOCOL_INVALID", e.Message)); }
                if (value.Channel != null)
                {
                    if (value.Protocol.Kind == MPAProtocolKind.ModbusTcpDraft &&
                        value.Channel.Kind != MCCChannelKind.TcpClient &&
                        value.Channel.Kind != MCCChannelKind.TcpServer)
                        errors.Add(FCMConfigIssue.Error("Protocol.Kind", "PROTOCOL_IO_MISMATCH",
                            "Modbus TCP 只能使用 TCP Channel"));
                    if (value.Protocol.Kind == MPAProtocolKind.ModbusRtuDraft &&
                        value.Channel.Kind != MCCChannelKind.Serial)
                        errors.Add(FCMConfigIssue.Error("Protocol.Kind", "PROTOCOL_IO_MISMATCH",
                            "Modbus RTU 必须使用 Serial Channel"));
                    if (value.Protocol.Kind == MPAProtocolKind.LengthFrame &&
                        (value.Channel.Kind == MCCChannelKind.Udp ||
                         value.Channel.Kind == MCCChannelKind.UdpServer))
                        errors.Add(FCMConfigIssue.Error("Protocol.Kind", "PROTOCOL_IO_MISMATCH",
                            "LengthFrame 规约需要 ByteStream"));
                }
            }
            return errors;
        }
    }
}
