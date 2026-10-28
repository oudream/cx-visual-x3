using System;
using System.ComponentModel.DataAnnotations;
using CxVisual.Foundation.Data.Configuration;
namespace CxVisual.Foundation.Transport
{
    public sealed class FTRHttpServerChannelConfiguration : FTRChannelConfiguration
    {
        public FTRHttpServerChannelConfiguration() : base(FTRChannelKind.HttpServer) { }
        [FCMConfigItem("HTTP监听地址",Order=10,Category="HTTP Server")]
        [Required]
        public string Prefix { get; set; } = "http://localhost:8080/";
        [FCMConfigItem("最大同时请求",Order=11,Category="HTTP Server")]
        [Range(1,1024)]
        public int MaxClients { get; set; } = 32;
        [FCMConfigItem("最大请求体字节",Order=12,Category="HTTP Server",Unit="Bytes")]
        [Range(1,int.MaxValue)]
        public int MaxRequestBytes { get; set; } = 4*1024*1024;
        [FCMConfigItem("最大响应字节",Order=13,Category="HTTP Server",Unit="Bytes")]
        [Range(1,int.MaxValue)]
        public int MaxResponseBytes { get; set; } = 4*1024*1024;
        [FCMConfigItem("响应等待上限",Order=14,Category="HTTP Server",Unit="ms")]
        [Range(1,3600000)]
        public int ResponseTimeoutMs { get; set; } = 30000;
        public override void Validate()
        {
            base.Validate();
            Uri uri;
            if (!Uri.TryCreate(Prefix,UriKind.Absolute,out uri) || uri.Scheme!="http" ||
                (!uri.IsLoopback && uri.Host!="localhost") || !Prefix.EndsWith("/",StringComparison.Ordinal) ||
                Prefix.Contains("*") || Prefix.Contains("+") || !string.IsNullOrEmpty(uri.UserInfo) ||
                MaxClients<1 || MaxClients>1024 || MaxRequestBytes<1 ||
                MaxResponseBytes<1 || ResponseTimeoutMs<1 || ResponseTimeoutMs>3600000)
                throw new ArgumentException("HTTP Server须显式使用安全的本机http前缀及有限预算");
        }
    }
}
