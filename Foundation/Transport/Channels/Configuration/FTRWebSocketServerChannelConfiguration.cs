using System;
using System.ComponentModel.DataAnnotations;
using CxVisual.Foundation.Data.Configuration;
namespace CxVisual.Foundation.Transport
{
    public sealed class FTRWebSocketServerChannelConfiguration : FTRChannelConfiguration
    {
        public FTRWebSocketServerChannelConfiguration() : base(FTRChannelKind.WebSocketServer) { }
        [FCMConfigItem("WebSocket监听前缀",Order=10,Category="WebSocket Server")]
        [Required]
        public string Prefix {get;set;}="http://localhost:8080/ws/";
        [FCMConfigItem("最大连接数",Order=11,Category="WebSocket Server")]
        [Range(1,1024)]
        public int MaxClients {get;set;}=32;
        [FCMConfigItem("最大单消息字节",Order=12,Category="WebSocket Server",Unit="Bytes")]
        [Range(1,int.MaxValue)]
        public int MaxMessageBytes {get;set;}=4*1024*1024;
        [FCMConfigItem("所有客户端合计在途缓冲额度",Order=13,Category="WebSocket Server",Unit="Bytes")]
        [Range(1,int.MaxValue)]
        public int TotalBufferedBytes {get;set;}=32*1024*1024;
        public override void Validate()
        {
            base.Validate();
            Uri uri;
            if(!Uri.TryCreate(Prefix,UriKind.Absolute,out uri) || uri.Scheme!="http" ||
                (!uri.IsLoopback && uri.Host!="localhost") || !Prefix.EndsWith("/",StringComparison.Ordinal) ||
                Prefix.Contains("*") || Prefix.Contains("+") || !string.IsNullOrEmpty(uri.UserInfo) ||
                MaxClients<1 || MaxClients>1024 || MaxMessageBytes<1 || TotalBufferedBytes<1)
                throw new ArgumentException("WebSocket Server只能使用本机显式HTTP Upgrade前缀及受限容量");
        }
    }
}
