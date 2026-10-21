using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Platform.Channels
{

    public class TcpClientConnection
    {
        private readonly TcpClient _tcpClient;
        private readonly TcpServerService _server;
        public Guid ClientId { get; }
        public NetworkStream Stream { get; }
        private readonly CancellationTokenSource _clientCancellationTokenSource = new CancellationTokenSource();
        private readonly CancellationToken _parentCancellationToken; // 用于响应服务器的停止信号

        public bool IsConnected => _tcpClient?.Connected ?? false;

        public TcpClientConnection(TcpClient tcpClient, Guid clientId, TcpServerService server)
        {
            _tcpClient = tcpClient;
            ClientId = clientId;
            _server = server;
            Stream = _tcpClient.GetStream();
            _parentCancellationToken = server._cancellationTokenSource.Token; // 监听服务器的全局取消信号
        }

        public async Task ProcessAsync()
        {
            Console.WriteLine($"开始处理客户端 {ClientId} 的数据...");
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_clientCancellationTokenSource.Token, _parentCancellationToken);
            var buffer = new byte[4096]; // 缓冲区大小

            try
            {
                while (!_clientCancellationTokenSource.IsCancellationRequested && !_parentCancellationToken.IsCancellationRequested && IsConnected)
                {
                    int bytesRead = await Stream.ReadAsync(buffer, 0, buffer.Length, linkedCts.Token);
                    if (bytesRead == 0)
                    {
                        // 优雅关闭或连接被对方关闭
                        Console.WriteLine($"客户端 {ClientId} 主动关闭连接或连接已断开 (ReadAsync 返回 0)。");
                        break;
                    }

                    string receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine($"收到来自 {ClientId} 的数据: {receivedData}");

                    // ****** 协议解析与业务分发 ******
                    // 这里需要根据你的应用协议来解析数据，并判断业务类型
                    // 示例：假设接收到的数据格式为 "业务类型:具体数据"
                    string[] parts = receivedData.Split(new[] { ':' }, 2);
                    if (parts.Length == 2)
                    {
                        TcpBusinessType type;
                        TcpBusinessTask task;
                        switch (parts[0].Trim().ToUpper())
                        {
                            case "A":
                                type = TcpBusinessType.TypeA;
                                task = new TcpBusinessTask(ClientId, type, parts[1].Trim());
                                _server.EnqueueBusinessTask(task);
                                break;
                            case "B":
                                type = TcpBusinessType.TypeB;
                                task = new TcpBusinessTask(ClientId, type, parts[1].Trim());
                                _server.EnqueueBusinessTask(task);
                                break;
                            default:
                                Console.WriteLine($"来自 {ClientId} 的未知业务指令: {parts[0]}");
                                // 可以选择回复错误信息或忽略
                                // await _server.SendToClientAsync(ClientId, "错误：未知的业务类型");
                                break;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"来自 {ClientId} 的数据格式不正确: {receivedData}");
                        // await _server.SendToClientAsync(ClientId, "错误：数据格式不正确");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                //  通常是服务器停止或客户端主动取消时触发
                if (_parentCancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine($"服务器停止，正在关闭客户端 {ClientId} 的处理。");
                }
                else
                {
                    Console.WriteLine($"客户端 {ClientId} 的操作被取消。");
                }
            }
            catch (IOException ex) when (ex.InnerException is SocketException sex &&
                                        (sex.SocketErrorCode == SocketError.ConnectionReset ||
                                         sex.SocketErrorCode == SocketError.ConnectionAborted ||
                                         sex.SocketErrorCode == SocketError.TimedOut ||
                                         sex.SocketErrorCode == SocketError.NetworkReset))
            {
                // 这些是常见的连接断开的SocketException
                Console.WriteLine($"客户端 {ClientId} 连接丢失 (IOException -> SocketException: {sex.SocketErrorCode}): {sex.Message}");
            }
            catch (Exception ex)
            {
                // 其他所有未预料到的异常
                Console.WriteLine($"处理客户端 {ClientId} 数据时发生错误: {ex.Message}");
                // 考虑记录详细的堆栈信息 ex.StackTrace
            }
            finally
            {
                Console.WriteLine($"结束处理客户端 {ClientId}。");
                _server.RemoveClient(ClientId); // 确保从服务器列表中移除
            }
        }

        public void Disconnect()
        {
            if (!_clientCancellationTokenSource.IsCancellationRequested)
            {
                _clientCancellationTokenSource.Cancel();
            }
            Stream?.Close();
            _tcpClient?.Close();
            Console.WriteLine($"客户端 {ClientId} 的资源已释放。");
        }
    }

    // 定义业务任务的结构
    public class TcpBusinessTask
    {
        public Guid ClientId { get; }
        public TcpBusinessType Type { get; }
        public string Data { get; }

        public TcpBusinessTask(Guid clientId, TcpBusinessType type, string data)
        {
            ClientId = clientId;
            Type = type;
            Data = data;
        }
    }

    // 定义业务类型枚举
    public enum TcpBusinessType
    {
        TypeA,
        TypeB
        // 可以根据需要添加更多类型
    }

}
