using System;
using System.ComponentModel.DataAnnotations;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Foundation.Transport
{
public sealed class FTRUdpServerChannelConfiguration : FTRChannelConfiguration
    {
        public FTRUdpServerChannelConfiguration() : base(FTRChannelKind.UdpServer) { }
        [FCMConfigItem("地址/IP", Order = 10, Category = "网络")]
        public string Host { get; set; } = "127.0.0.1";
        [FCMConfigItem("端口", Order = 11, Category = "网络")]
        [Range(1, 65535)]
        public int Port { get; set; } = 502;
        [FCMConfigItem("最大客户端", Order = 13, Category = "网络")]
        [Range(1, 1000000)]
        public int MaxClients { get; set; } = 128;
        [FCMConfigItem("子通道路由策略", Order = 30, Category = "子通道")]
        public string SubChannelRule { get; set; } = "";
        public override void Validate()
        {
            base.Validate();
            if (string.IsNullOrWhiteSpace(Host) || Port < 1 || Port > 65535) throw new ArgumentException("无效的网络地址/端口"); if (MaxClients < 1) throw new ArgumentException("无效客户端数量");
        }
    }
}
