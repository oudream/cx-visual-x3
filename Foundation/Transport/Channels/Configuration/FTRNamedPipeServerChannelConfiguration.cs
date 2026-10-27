using System;
using System.ComponentModel.DataAnnotations;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Foundation.Transport
{
public sealed class FTRNamedPipeServerChannelConfiguration : FTRChannelConfiguration
    {
        public FTRNamedPipeServerChannelConfiguration() : base(FTRChannelKind.NamedPipeServer) { }
        [FCMConfigItem("本机管道名称", Order = 10, Category = "Named Pipe")]
        [Required]
        public string PipeName { get; set; } = "CxVisual";
        [FCMConfigItem("最大客户端", Order = 11, Category = "Named Pipe")]
        [Range(1, 254)]
        public int MaxClients { get; set; } = 16;
        public override void Validate()
        {
            base.Validate();
            if (string.IsNullOrWhiteSpace(PipeName) ||
                PipeName.IndexOfAny(new[] { '/', '\\', ':', '\0' }) >= 0 ||
                PipeName.Length > 120 || MaxClients < 1 || MaxClients > 254)
                throw new ArgumentException("NamedPipe服务端名称/并发上限无效");
        }
    }
}
