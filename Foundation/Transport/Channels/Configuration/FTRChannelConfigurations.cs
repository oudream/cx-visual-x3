using System;
using System.ComponentModel.DataAnnotations;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Foundation.Transport
{
public static class FTRChannelConfigurations
    {
        public static Type TypeOf(FTRChannelKind kind)
        {
            switch (kind)
            {
                case FTRChannelKind.Virtual: return typeof(FTRVirtualChannelConfiguration);
                case FTRChannelKind.TcpClient: return typeof(FTRTcpClientChannelConfiguration);
                case FTRChannelKind.TcpServer: return typeof(FTRTcpServerChannelConfiguration);
                case FTRChannelKind.Udp: return typeof(FTRUdpChannelConfiguration);
                case FTRChannelKind.UdpServer: return typeof(FTRUdpServerChannelConfiguration);
                case FTRChannelKind.Serial: return typeof(FTRSerialChannelConfiguration);
                case FTRChannelKind.Custom: return typeof(FTRCustomChannelConfiguration);
                case FTRChannelKind.HttpClient: return typeof(FTRHttpClientChannelConfiguration);
                case FTRChannelKind.HttpServer: return typeof(FTRHttpServerChannelConfiguration);
                case FTRChannelKind.NamedPipeClient: return typeof(FTRNamedPipeClientChannelConfiguration);
                case FTRChannelKind.NamedPipeServer: return typeof(FTRNamedPipeServerChannelConfiguration);
                case FTRChannelKind.WebSocketClient: return typeof(FTRWebSocketClientChannelConfiguration);
                case FTRChannelKind.WebSocketServer: return typeof(FTRWebSocketServerChannelConfiguration);
                case FTRChannelKind.Replay: return typeof(FTRReplayChannelConfiguration);
                case FTRChannelKind.UdpMulticast: return typeof(FTRUdpMulticastChannelConfiguration);
                default: throw new ArgumentOutOfRangeException("kind");
            }
        }
        public static FTRChannelConfiguration Create(FTRChannelKind kind)
        { return (FTRChannelConfiguration)Activator.CreateInstance(TypeOf(kind)); }
    }
}
