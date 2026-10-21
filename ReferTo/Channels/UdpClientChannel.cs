using System;
using System.Collections.Generic;
using CommonInterfaces;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Platform.Channels
{
    public class UdpClientChannel
    {
        public enum UdpClientModuleState
        {
            Stopping,
            Stopped,
            Started
        }

        private UdpClient _udpClient;
        private Thread _clientThread;
        private readonly object _lockObject = new object();
        private bool _running = false;

        // 服务器的IP地址和端口
        private string _serverIp;
        private int _serverPort;

        // 状态属性
        private UdpClientModuleState state = UdpClientModuleState.Stopped;
        public UdpClientModuleState ModuleState
        {
            get { return state; }
            private set
            {
                state = value;
                OnStateChanged(state);
            }
        }

        // 状态改变事件
        public event Action<UdpClientModuleState> StateChanged;

        private byte[] buffer = new byte[8192];

        // 数据接收事件
        public event Action<byte[], int> DataReceived;

        // 启动读取模块，启动UDPClient通道
        public void Start(string ip, int port)
        {
            // 检查是否已停止
            if (ModuleState != UdpClientModuleState.Stopped)
            {
                return;
            }

            _serverIp = ip;
            _serverPort = port;

            StartClientThread();

            ModuleState = UdpClientModuleState.Started;
        }

        private void StartClientThread()
        {
            if (_running)
            {
                return;
            }

            LogHelper.Debug($"UDP客户端{_serverIp}:{_serverPort} UDPClient读取模块已启动");
            _running = true;

            _clientThread = new Thread(() =>
            {
                _udpClient = new UdpClient();
                var endPoint = new IPEndPoint(IPAddress.Parse(_serverIp), _serverPort);

                while (_running)
                {
                    try
                    {
                        // 接收数据
                        if (_udpClient.Available > 0)
                        {
                            var receivedData = _udpClient.Receive(ref endPoint);
                            OnDataReceived(receivedData, receivedData.Length);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Error($"UDP客户端{_serverIp}:{_serverPort} UDPClient通道异常：{ex.Message}");
                    }

                    Thread.Sleep(10); // 调整睡眠时间以减少CPU占用
                }

                _udpClient.Close();
                _udpClient = null;

                _running = false;
                ModuleState = UdpClientModuleState.Stopped;
                LogHelper.Debug($"UDP客户端{_serverIp}:{_serverPort} UDPClient通道维护模块已停止");
            })
            { IsBackground = true };

            _clientThread.Start();
        }

        public void Send(byte[] data)
        {
            lock (_lockObject)
            {
                if (_udpClient != null)
                {
                    try
                    {
                        var endPoint = new IPEndPoint(IPAddress.Parse(_serverIp), _serverPort);
                        _udpClient.Send(data, data.Length, endPoint);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Error($"UDP客户端{_serverIp}:{_serverPort} 发送异常：{ex.Message}");
                    }
                }
            }
        }

        protected virtual void OnDataReceived(byte[] data, int bytesRead)
        {
            DataReceived?.Invoke(data, bytesRead);
        }

        protected virtual void OnStateChanged(UdpClientModuleState newState)
        {
            StateChanged?.Invoke(newState);
        }

        public void Stop()
        {
            // 检查是否已启动
            if (ModuleState != UdpClientModuleState.Started)
            {
                return;
            }

            ModuleState = UdpClientModuleState.Stopping;

            // 停止读取，等待线程结束
            _running = false;
            if (_udpClient != null)
            {
                _udpClient.Close();
                _udpClient = null;
            }
        }

        public bool IsConnected()
        {
            lock (_lockObject)
            {
                // UDP 是无连接的协议，因此我们始终返回 true
                return true;
            }
        }
    }
}
