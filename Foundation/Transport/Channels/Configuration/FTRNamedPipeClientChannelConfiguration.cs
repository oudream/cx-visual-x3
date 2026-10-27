using System;
using System.ComponentModel.DataAnnotations;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Foundation.Transport
{
public sealed class FTRNamedPipeClientChannelConfiguration : FTRChannelConfiguration
    {
        public FTRNamedPipeClientChannelConfiguration() : base(FTRChannelKind.NamedPipeClient) { }
        [FCMConfigItem("服务器名称（本机为 .）", Order = 10, Category = "Named Pipe")]
        [Required]
        public string ServerName { get; set; } = ".";
        [FCMConfigItem("管道名称", Order = 11, Category = "Named Pipe")]
        [Required]
        public string PipeName { get; set; } = "CxVisual";
        [FCMConfigItem("连接超时", Order = 12, Category = "Named Pipe", Unit = "ms")]
        [Range(1, 3600000)]
        public int ConnectTimeoutMs { get; set; } = 5000;
        public override void Validate()
        {
            base.Validate();
            if (string.IsNullOrWhiteSpace(ServerName) || string.IsNullOrWhiteSpace(PipeName) ||
                PipeName.IndexOfAny(new[] { '/', '\\', ':', '\0' }) >= 0 ||
                PipeName.Length > 120 || ConnectTimeoutMs < 1)
                throw new ArgumentException("NamedPipe客户端地址/超时无效");
        }
    }
}
