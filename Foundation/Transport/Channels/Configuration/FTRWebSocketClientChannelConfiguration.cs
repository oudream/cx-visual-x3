using System;
using System.ComponentModel.DataAnnotations;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Foundation.Transport
{
public sealed class FTRWebSocketClientChannelConfiguration : FTRChannelConfiguration
    {
        public FTRWebSocketClientChannelConfiguration() : base(FTRChannelKind.WebSocketClient) { }

        [FCMConfigItem("WebSocket 地址", Order = 10, Category = "WebSocket")]
        [Required]
        public string Url { get; set; } = "ws://127.0.0.1:8080/ws";

        [FCMConfigItem("建立连接超时", Order = 11, Category = "WebSocket", Unit = "ms")]
        [Range(1, 3600000)]
        public int ConnectTimeoutMs { get; set; } = 5000;

        [FCMConfigItem("单个消息最大字节数", Order = 12, Category = "WebSocket", Unit = "Bytes")]
        [Range(1, int.MaxValue)]
        public int MaxMessageBytes { get; set; } = 4 * 1024 * 1024;

        public override void Validate()
        {
            base.Validate();
            Uri uri;
            if (!Uri.TryCreate(Url, UriKind.Absolute, out uri) ||
                (uri.Scheme != "ws" && uri.Scheme != "wss") ||
                !string.IsNullOrEmpty(uri.UserInfo) || ConnectTimeoutMs < 1 ||
                MaxMessageBytes < 1)
                throw new ArgumentException("WebSocket 地址、超时或接收预算不合法");
        }
    }
}
