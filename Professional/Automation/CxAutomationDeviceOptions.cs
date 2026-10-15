using System;
using CxVisual.Foundation.Data.Configuration;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace CxVisual.Professional.Automation
{
    /// <summary>Automation设备通信配置样例（P01/P02），不包括安全联锁或设备实时状态。</summary>
    [FCMConfigSection("Automation.Device")]
    [FCMConfigValidator(typeof(CxAutomationDeviceOptionsRules))]
    public sealed class CxAutomationDeviceOptions
    {
        [FCMConfigItem("控制器地址")]
        [Required]
        public string ControllerHost { get; set; } = "127.0.0.1";

        [FCMConfigItem("控制器端口")]
        [Range(1, 65535)]
        public int ControllerPort { get; set; } = 502;

        [FCMConfigItem("站号")]
        [Range(1, 247)]
        public int StationId { get; set; } = 1;

        [FCMConfigItem("连接超时", Unit = "ms")]
        [Range(100, 60000)]
        public int ConnectTimeoutMs { get; set; } = 2000;

        [FCMConfigItem("自动重连")]
        public bool AutoReconnect { get; set; } = true;

        [FCMConfigItem("重连等待", Unit = "ms")]
        [Range(0, 60000)]
        public int ReconnectDelayMs { get; set; } = 2000;

        public static string Validate(CxAutomationDeviceOptions value)
        {
            if (value == null) return "Device options missing";
            if (string.IsNullOrWhiteSpace(value.ControllerHost)) return "控制器地址不能为空";
            if (value.AutoReconnect && value.ReconnectDelayMs < 500)
                return "启用自动重连时，重连等待必须至少500ms";
            return null;
        }
    }
    public sealed class CxAutomationDeviceOptionsRules : IFCMConfigValidator<CxAutomationDeviceOptions>
    {
        public IEnumerable<FCMConfigIssue> Validate(CxAutomationDeviceOptions value, FCMConfigValidationContext ctx)
        {
            string error = CxAutomationDeviceOptions.Validate(value);
            if (!string.IsNullOrWhiteSpace(error)) yield return FCMConfigIssue.Error(ctx.Path, "DOMAIN_CONFIG", error);
        }
    }
}
