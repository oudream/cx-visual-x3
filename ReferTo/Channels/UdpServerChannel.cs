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
    public class UdpServerChannel
    {
        public enum UdpServerModuleState
        {
            Stopping,
            Stopped,
            Started
        }

        private UdpClient _udpServer;
        private Thread _listenerThread;
        private readonly object _lockObject = new object();
        private bool _running = false;

        // 服务器的IP地址和端口
        private string _serverIp;
        private int _serverPort;

        // 状态属性
        private UdpServerModuleState state = UdpServerModuleState.Stopped;
        public UdpServerModuleState ModuleState
        {
            get { return state; }
            private set
            {
                state = value;
                OnStateChanged(state);
            }
        }

        // 状态改变事件
        public event Action<UdpServerModuleState> StateChanged;

        // 数据接收事件
        public event Action<IPEndPoint, byte[], int> DataReceived;

        // 启动服务器
        public void Start(string ip, int port)
        {
            // 检查是否已停止
            if (ModuleState != UdpServerModuleState.Stopped)
            {
                return;
            }

            _serverIp = ip;
            _serverPort = port;

            StartListenerThread();

            ModuleState = UdpServerModuleState.Started;
        }

        private void StartListenerThread()
        {
            if (_running)
            {
                return;
            }

            LogHelper.Debug($"UDP服务器{_serverIp}:{_serverPort} UDPServer监听模块已启动");
            _running = true;

            _listenerThread = new Thread(() =>
            {
                _udpServer = new UdpClient(new IPEndPoint(IPAddress.Parse(_serverIp), _serverPort));

                while (_running)
                {
                    try
                    {
                        IPEndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, 0);
                        var buffer = _udpServer.Receive(ref clientEndPoint);

                        OnDataReceived(clientEndPoint, buffer, buffer.Length);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Error($"UDP服务器{_serverIp}:{_serverPort} UDPServer监听模块异常：{ex.Message}");
                    }

                    Thread.Sleep(10); // 调整睡眠时间以减少CPU占用
                }

                _udpServer.Close();
                _udpServer = null;

                _running = false;
                ModuleState = UdpServerModuleState.Stopped;
                LogHelper.Debug($"UDP服务器{_serverIp}:{_serverPort} UDPServer监听模块已停止");
            })
            { IsBackground = true };

            _listenerThread.Start();
        }

        public void Send(IPEndPoint clientEndPoint, byte[] data)
        {
            lock (_lockObject)
            {
                if (_udpServer != null)
                {
                    try
                    {
                        _udpServer.Send(data, data.Length, clientEndPoint);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Error($"UDP服务器{_serverIp}:{_serverPort} 发送异常：{ex.Message}");
                    }
                }
            }
        }

        protected virtual void OnDataReceived(IPEndPoint clientEndPoint, byte[] data, int bytesRead)
        {
            DataReceived?.Invoke(clientEndPoint, data, bytesRead);
        }

        protected virtual void OnStateChanged(UdpServerModuleState newState)
        {
            StateChanged?.Invoke(newState);
        }

        public void Stop()
        {
            // 检查是否已启动
            if (ModuleState != UdpServerModuleState.Started)
            {
                return;
            }

            ModuleState = UdpServerModuleState.Stopping;

            // 停止读取，等待线程结束
            _running = false;
            if (_udpServer != null)
            {
                _udpServer.Close();
                _udpServer = null;
            }
        }
    }
}
