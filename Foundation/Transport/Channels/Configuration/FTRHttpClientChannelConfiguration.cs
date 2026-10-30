using System;
using System.ComponentModel.DataAnnotations;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Foundation.Transport
{
public sealed class FTRHttpClientChannelConfiguration : FTRChannelConfiguration
    {
        public FTRHttpClientChannelConfiguration() : base(FTRChannelKind.HttpClient)
        {
            // HTTP消息需在响应体聚合+扩容+Received隔离快照期间同时持有内存。
            // 默认MaxResponseBytes=4MiB至少需要覆盖2份完整消息与容量增长；
            // 显式FCM保存的旧预算仍然优先，不暗改用户已配置的资源上限。
            ReceiveByteBudget=12*1024*1024;
        }
        [FCMConfigItem("请求地址", Order = 10, Category = "HTTP")]
        [Required]
        public string Url { get; set; } = "http://127.0.0.1:8080/";
        [FCMConfigItem("请求超时", Order = 11, Category = "HTTP", Unit = "ms")]
        [Range(1, 3600000)]
        public int RequestTimeoutMs { get; set; } = 5000;
        [FCMConfigItem("最大响应字节", Order = 12, Category = "HTTP", Unit = "Bytes")]
        [Range(1, int.MaxValue)]
        public int MaxResponseBytes { get; set; } = 4 * 1024 * 1024;
        [FCMConfigItem("请求内容类型", Order = 13, Category = "HTTP")]
        public string ContentType { get; set; } = "application/octet-stream";

        public override void Validate()
        {
            base.Validate();
            Uri url;
            if (!Uri.TryCreate(Url, UriKind.Absolute, out url) ||
                (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps) ||
                !string.IsNullOrEmpty(url.UserInfo) || RequestTimeoutMs < 1 ||
                MaxResponseBytes < 1 || string.IsNullOrWhiteSpace(ContentType))
                throw new ArgumentException("HTTP/HTTPS 地址、超时或响应预算无效");
        }
    }
}
