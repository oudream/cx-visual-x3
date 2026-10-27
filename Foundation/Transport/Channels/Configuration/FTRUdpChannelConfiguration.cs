using System;
using System.ComponentModel.DataAnnotations;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Foundation.Transport
{
public sealed class FTRUdpChannelConfiguration : FTRChannelConfiguration
    {
        public FTRUdpChannelConfiguration() : base(FTRChannelKind.Udp) { }
        [FCMConfigItem("地址/IP", Order = 10, Category = "网络")]
        public string Host { get; set; } = "127.0.0.1";
        [FCMConfigItem("端口", Order = 11, Category = "网络")]
        [Range(1, 65535)]
        public int Port { get; set; } = 502;
        public override void Validate()
        {
            base.Validate();
            if (string.IsNullOrWhiteSpace(Host) || Port < 1 || Port > 65535) throw new ArgumentException("无效的网络地址/端口");
        }
    }
}
