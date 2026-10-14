using System;
using CxVisual.Mechanism.Core;

namespace CxVisual.Professional.Automation
{
    /// <summary>Automation设备通信配置样例（P01/P02），不包括安全联锁或设备实时状态。</summary>
    public sealed class CxAutomationDeviceOptions
    {
        [CxConfigItem("控制器地址", Category = "通信", Required = true, Description = "IP地址或可解析主机名")]
        public string ControllerHost { get; set; } = "127.0.0.1";

        [CxConfigItem("控制器端口", Category = "通信", Min = 1, Max = 65535)]
        public int ControllerPort { get; set; } = 502;

        [CxConfigItem("站号", Category = "规约", Min = 1, Max = 247)]
        public int StationId { get; set; } = 1;

        [CxConfigItem("连接超时", Category = "通信", Unit = "ms", Min = 100, Max = 60000)]
        public int ConnectTimeoutMs { get; set; } = 2000;

        [CxConfigItem("自动重连", Category = "通信")]
        public bool AutoReconnect { get; set; } = true;

        [CxConfigItem("重连等待", Category = "通信", Unit = "ms", Min = 0, Max = 60000,
            Description = "启用自动重连时至少500ms，防止故障期间反复重连")]
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
}
