using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Platform.Channels
{
    // 使用C#写一个tcp server，能支持上千个连接，并发性要好，某一种类业务放各类线程中处理，要维护好连接，
    // 支持种类业务处理完能正常发送（连接不在就发不出去）

    public class TcpServerService
    {
        private TcpListener _listener;
        private readonly int _port;
        private readonly ConcurrentDictionary<Guid, TcpClientConnection> _clients = new ConcurrentDictionary<Guid, TcpClientConnection>();
        public readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        // 用于分发不同业务逻辑的队列
        private readonly ConcurrentQueue<TcpBusinessTask> _businessTypeAQueue = new ConcurrentQueue<TcpBusinessTask>();
        private readonly ConcurrentQueue<TcpBusinessTask> _businessTypeBQueue = new ConcurrentQueue<TcpBusinessTask>();

        // 处理不同业务逻辑的线程
        private readonly List<Task> _businessProcessors = new List<Task>();

        public TcpServerService(int port)
        {
            _port = port;
        }

        public async Task StartAsync()
        {
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            Console.WriteLine($"服务器已启动，监听端口: {_port}");

            // 启动业务处理线程
            StartBusinessProcessors();

            try
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    //TcpClient tcpClient = await _listener.AcceptTcpClientAsync(_cancellationTokenSource.Token);
                    TcpClient tcpClient = await _listener.AcceptTcpClientAsync();
                    // 配置TCP KeepAlive，以便及早检测到断开的连接
                    tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                    // 可以进一步配置KeepAlive参数，例如KeepAliveTime和KeepAliveInterval，但这依赖于操作系统和.NET版本
                    // 例如: tcpClient.Client.IOControl(IOControlCode.KeepAliveValues, KeepAlivePacket(1, 30000, 5000) , null); // .NET Core/5+

                    Guid clientId = Guid.NewGuid();
                    TcpClientConnection clientConnection = new TcpClientConnection(tcpClient, clientId, this);
                    _clients.TryAdd(clientId, clientConnection);

                    Console.WriteLine($"新客户端连接: {clientId} from {((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address}");
                    _ = clientConnection.ProcessAsync(); // 启动客户端处理，不等待完成
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("服务器正在停止...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"服务器发生错误: {ex.Message}");
                // 在实际应用中，这里应该有更完善的错误处理和日志记录
            }
            finally
            {
                Stop();
            }
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
            _listener?.Stop();

            Console.WriteLine("正在关闭所有客户端连接...");
            foreach (var clientId in _clients.Keys)
            {
                if (_clients.TryRemove(clientId, out var client))
                {
                    client.Disconnect();
                }
            }

            Console.WriteLine("正在停止业务处理线程...");
            Task.WaitAll(_businessProcessors.ToArray(), TimeSpan.FromSeconds(5)); // 等待业务处理器完成

            Console.WriteLine("服务器已停止。");
        }

        internal void RemoveClient(Guid clientId)
        {
            if (_clients.TryRemove(clientId, out var client))
            {
                Console.WriteLine($"客户端已断开: {clientId}");
                client.Disconnect(); // 确保资源释放
            }
        }

        // 根据业务类型分发任务
        public void EnqueueBusinessTask(TcpBusinessTask task)
        {
            switch (task.Type)
            {
                case TcpBusinessType.TypeA:
                    _businessTypeAQueue.Enqueue(task);
                    break;
                case TcpBusinessType.TypeB:
                    _businessTypeBQueue.Enqueue(task);
                    break;
                default:
                    Console.WriteLine($"未知的业务类型: {task.Type}");
                    break;
            }
        }

        // 尝试发送数据到指定客户端
        public async Task<bool> SendToClientAsync(Guid clientId, string message)
        {
            if (_clients.TryGetValue(clientId, out TcpClientConnection client) && client.IsConnected)
            {
                try
                {
                    byte[] data = Encoding.UTF8.GetBytes(message);
                    await client.Stream.WriteAsync(data, 0, data.Length);
                    await client.Stream.FlushAsync();
                    Console.WriteLine($"已发送消息给 {clientId}: {message}");
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"发送消息给 {clientId} 失败: {ex.Message}");
                    // 连接可能在此处出现问题，可以考虑移除客户端
                    // RemoveClient(clientId); // 可以选择在此处或在 ClientConnection 的读取循环中处理断开
                    return false;
                }
            }
            else
            {
                Console.WriteLine($"无法发送消息：客户端 {clientId} 未连接或不存在。");
                return false;
            }
        }

        private void StartBusinessProcessors()
        {
            _businessProcessors.Add(Task.Run(() => ProcessBusinessTypeAQueueAsync(_cancellationTokenSource.Token)));
            _businessProcessors.Add(Task.Run(() => ProcessBusinessTypeBQueueAsync(_cancellationTokenSource.Token)));
            // 可以根据需要添加更多业务类型的处理器
            Console.WriteLine("业务处理线程已启动。");
        }

        private async Task ProcessBusinessTypeAQueueAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("业务类型 A 处理器已启动。");
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_businessTypeAQueue.TryDequeue(out TcpBusinessTask task))
                {
                    try
                    {
                        Console.WriteLine($"处理业务 A: 客户端 {task.ClientId}, 数据: {task.Data}");
                        // 模拟业务处理
                        await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken); // 模拟耗时操作
                        string responseMessage = $"业务A处理完成: {task.Data.ToUpper()}";
                        // 处理完成后尝试发送回执
                        await SendToClientAsync(task.ClientId, responseMessage);
                    }
                    catch (OperationCanceledException) { break; } // 响应取消请求
                    catch (Exception ex)
                    {
                        Console.WriteLine($"处理业务 A 失败 (客户端 {task.ClientId}): {ex.Message}");
                    }
                }
                else
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken); // 队列为空时稍作等待
                }
            }
            Console.WriteLine("业务类型 A 处理器已停止。");
        }

        private async Task ProcessBusinessTypeBQueueAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("业务类型 B 处理器已启动。");
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_businessTypeBQueue.TryDequeue(out TcpBusinessTask task))
                {
                    try
                    {
                        Console.WriteLine($"处理业务 B: 客户端 {task.ClientId}, 数据: {task.Data}");
                        // 模拟业务处理
                        await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken); // 模拟耗时操作
                        string responseMessage = $"业务B处理完成: {task.Data.ToLower()}";
                        await SendToClientAsync(task.ClientId, responseMessage);
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"处理业务 B 失败 (客户端 {task.ClientId}): {ex.Message}");
                    }
                }
                else
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
                }
            }
            Console.WriteLine("业务类型 B 处理器已停止。");
        }

        // 用于演示的KeepAlive配置方法 (仅适用于 .NET Core/5+ 且需要管理员权限)
        // 参考: https://docs.microsoft.com/en-us/dotnet/api/system.net.sockets.socket.iocontrol?view=net-6.0
        // public static byte[] KeepAlivePacket(int onOff, int keepAliveTime, int keepAliveInterval)
        // {
        //     byte[] buffer = new byte[12];
        //     BitConverter.GetBytes(onOff).CopyTo(buffer, 0);
        //     BitConverter.GetBytes(keepAliveTime).CopyTo(buffer, 4); // The number of milliseconds to wait for a keep-alive response before sending another keep-alive probe.
        //     BitConverter.GetBytes(keepAliveInterval).CopyTo(buffer, 8); // The number of milliseconds between keep-alive probes.
        //     return buffer;
        // }

        public static async Task MainTest(string[] args)
        {
            TcpServerService server = new TcpServerService(8888);
            var serverTask = server.StartAsync(); // 启动服务器

            Console.WriteLine("按任意键停止服务器...");
            Console.ReadKey();

            server.Stop();
            await serverTask; // 等待服务器完全停止
        }
    }

}
