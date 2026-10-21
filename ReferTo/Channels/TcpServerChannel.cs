using System;
using System.Collections.Generic;
using CommonInterfaces;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Platform.Channels
{
    public class TcpServerChannel
    {
        public enum TcpServerModuleState
        {
            Stopping,
            Stopped,
            Started
        }

        private TcpListener _tcpListener;
        private Thread _listenerThread;
        private readonly object _lockObject = new object();
        private bool _running = false;

        private List<TcpClient> _clients = new List<TcpClient>();

        // 服务器的IP地址和端口
        private string _serverIp;
        private int _serverPort;

        // 状态属性
        private TcpServerModuleState state = TcpServerModuleState.Stopped;
        public TcpServerModuleState ModuleState
        {
            get { return state; }
            private set
            {
                state = value;
                OnStateChanged(state);
            }
        }

        // 状态改变事件
        public event Action<TcpServerModuleState> StateChanged;
        public event Action<string> ClientConnected;
        public event Action<string> ClientDisconnected;

        // 数据接收事件
        public event Action<TcpClient, byte[], int> DataReceived;

        // 启动服务器
        public void Start(string ip, int port)
        {
            // 检查是否已停止
            if (ModuleState != TcpServerModuleState.Stopped)
            {
                return;
            }

            _serverIp = ip;
            _serverPort = port;

            StartListenerThread();

            ModuleState = TcpServerModuleState.Started;
        }

        private void StartListenerThread()
        {
            if (_running)
            {
                return;
            }

            LogHelper.Debug($"TCP服务器{_serverIp}:{_serverPort} TCPServer监听模块已启动");
            _running = true;

            _listenerThread = new Thread(() =>
            {
                _tcpListener = new TcpListener(IPAddress.Parse(_serverIp), _serverPort);
                _tcpListener.Start();

                while (_running)
                {
                    try
                    {
                        // 接受客户端连接
                        if (_tcpListener.Pending())
                        {
                            var client = _tcpListener.AcceptTcpClient();
                            string clientId = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
                            lock (_lockObject)
                            {
                                _clients.Add(client);
                            }
                            StartClientThread(client);
                            ClientConnected?.Invoke(clientId);
                        }
                        Thread.Sleep(10); // 调整睡眠时间以减少CPU占用
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Error($"TCP服务器{_serverIp}:{_serverPort} TCPServer监听模块异常：{ex.Message}");
                    }
                }

                _tcpListener.Stop();
                _tcpListener = null;

                _running = false;
                ModuleState = TcpServerModuleState.Stopped;
                LogHelper.Debug($"TCP服务器{_serverIp}:{_serverPort} TCPServer监听模块已停止");
            })
            { IsBackground = true };

            _listenerThread.Start();
        }

        private void StartClientThread(TcpClient client)
        {
            new Thread(() =>
            {
                var buffer = new byte[8192];
                var stream = client.GetStream();

                string clientId = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
                while (_running)
                {
                    try
                    {
                        //if (client.Available > 0)
                        //{

                        //}
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            OnDataReceived(client, buffer, bytesRead);
                        }
                        else
                        {
                            LogHelper.Error($"TCP服务器{_serverIp}:{_serverPort} 客户端正常关闭连接[{clientId}]！");
                            lock (_lockObject)
                            {
                                _clients.Remove(client);
                            }
                            client.Close();
                            ClientDisconnected?.Invoke(clientId);
                            return;
                        }
                        //Thread.Sleep(10); // 调整睡眠时间以减少CPU占用
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Error($"TCP服务器{_serverIp}:{_serverPort} 客户端处理模块异常：{ex.Message}");
                        lock (_lockObject)
                        {
                            _clients.Remove(client);
                        }
                        client.Close();
                        ClientDisconnected?.Invoke(clientId);
                        return;
                    }
                }
            })
            { IsBackground = true }.Start();
        }

        public bool Send(TcpClient client, byte[] data)
        {
            lock (_lockObject)
            {
                if (client != null && client.Connected)
                {
                    try
                    {
                        NetworkStream stream = client.GetStream();
                        stream.Write(data, 0, data.Length);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Error($"TCP服务器{_serverIp}:{_serverPort} 发送异常：{ex.Message}");
                    }
                }
            }
            return false;
        }

        public bool Send(byte[] data)
        {
            bool r = false;
            lock (_lockObject)
            {
                foreach (var client in _clients)
                {
                    if (client != null && client.Connected)
                    {
                        try
                        {
                            NetworkStream stream = client.GetStream();
                            stream.Write(data, 0, data.Length);
                            r = true;
                        }
                        catch (Exception ex)
                        {
                            LogHelper.Error($"TCP服务器{_serverIp}:{_serverPort} 发送异常：{ex.Message}");
                        }
                    }
                }
            }
            return r;
        }

        protected virtual void OnDataReceived(TcpClient client, byte[] data, int bytesRead)
        {
            DataReceived?.Invoke(client, data, bytesRead);
        }

        protected virtual void OnStateChanged(TcpServerModuleState newState)
        {
            StateChanged?.Invoke(newState);
        }

        public void Stop()
        {
            // 检查是否已启动
            if (ModuleState != TcpServerModuleState.Started)
            {
                return;
            }

            ModuleState = TcpServerModuleState.Stopping;

            // 停止读取，等待线程结束
            _running = false;
            lock (_lockObject)
            {
                foreach (var client in _clients)
                {
                    if (client.Connected)
                    {
                        client.Close();
                    }
                }
                _clients.Clear();
            }
        }

        public bool IsConnected(TcpClient client)
        {
            lock (_lockObject)
            {
                return client?.Connected == true;
            }
        }
    }
}
