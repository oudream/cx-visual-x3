using System;
using System.ComponentModel.DataAnnotations;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Foundation.Transport
{
public sealed class FTRTcpClientChannelConfiguration : FTRChannelConfiguration
    {
        public FTRTcpClientChannelConfiguration() : base(FTRChannelKind.TcpClient) { }
        [FCMConfigItem("地址/IP", Order = 10, Category = "网络")]
        public string Host { get; set; } = "127.0.0.1";
        [FCMConfigItem("端口", Order = 11, Category = "网络")]
        [Range(1, 65535)]
        public int Port { get; set; } = 502;
        [FCMConfigItem("连接超时", Order = 12, Category = "网络")]
        [Range(1, 3600000)]
        public int ConnectTimeoutMs { get; set; } = 5000;
        public override void Validate()
        {
            base.Validate();
            if (string.IsNullOrWhiteSpace(Host) || Port < 1 || Port > 65535) throw new ArgumentException("无效的网络地址/端口"); if (ConnectTimeoutMs < 1) throw new ArgumentException("无效超时");
        }
    }
}
