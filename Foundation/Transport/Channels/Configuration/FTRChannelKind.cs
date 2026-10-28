using System;
using System.ComponentModel.DataAnnotations;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Foundation.Transport
{
public enum FTRChannelKind { Virtual, TcpClient, TcpServer, Udp, Serial, Custom, UdpServer, HttpClient, NamedPipeClient, NamedPipeServer, WebSocketClient, Replay, UdpMulticast, HttpServer, WebSocketServer, SharedMemory }
}
