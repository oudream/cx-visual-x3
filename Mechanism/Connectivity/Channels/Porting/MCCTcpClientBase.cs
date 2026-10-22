// PORTING REVIEW: ReferTo source copied and type-renamed; NOT INCLUDED IN PROJECT.
// Original: ReferTo/Channels/TcpClientBase.cs; requires dependency cleanup, no compatibility Adapter will be retained.
using Serilog;
using System;
using CommonInterfaces;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Mechanism.Connectivity.Porting
{
    public class MCCTcpClientBase
    {
        private TcpClient _tcpClient;
        private Thread _clientThread;
        private readonly object _lockObject = new object();
        private bool _running = false;
        private bool _tcpClientError = false;
        private long _tryConnectTime = 0;

        // 服务器的IP地址和端口
        private string _serverIp;
        private ushort _serverPort;

        // 状态属性
        private TcpClientModuleState _state = TcpClientModuleState.Stopped;
        public TcpClientModuleState ModuleState
        {
            get { return _state; }
            private set
            {
                if (_state != value)
                {
                    //LogHelper.Debug($"TCP客户端{_serverIp}:{_serverPort} 状态改变:{_state} -> {value}");
                    _state = value;
                    StateChanged?.Invoke(value);
                }
            }
        }

        // 状态改变事件
        public event Action<TcpClientModuleState> StateChanged;

        // 日志记录
        protected ILogger _logger = null;

        // 需要关闭的通道
        private volatile bool _needClose = false;

        protected bool _enableLog = false;

        public MCCTcpClientBase(bool enableLog)
        {
            _enableLog = enableLog; ;            
        }

        // 启动读取模块，启动TCPClient通道
        public void Start(string ip, ushort port)
        {
            // 检查是否已停止
            if (ModuleState != TcpClientModuleState.Stopped)
            {
                return;
            }

            _serverIp = ip;
            _serverPort = port;

            if (_enableLog  && _logger == null)
            {
                // 启动时检测Logs文件夹是否存在，创建失败会退出程序
                string logPath = LogHelper.GetLogPath($"log-tcpclient-{_serverIp}-{_serverPort}.txt");
                _logger = new LoggerConfiguration()
                  .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
                  .MinimumLevel.Verbose()
                  .CreateLogger();
            }

            StartClientThread();
        }

        private void StartClientThread()
        {
            if (_running)
            {
                return;
            }

            ModuleState = TcpClientModuleState.Started;
            LogHelper.Debug($"TCP客户端{_serverIp}:{_serverPort} TCPClient读取模板已启动");
            _running = true;
            bool errLog = false, connectLog = false;
            Stopwatch sw_err = new Stopwatch(), sw_connect = new Stopwatch();

            StartClientInit();

            _clientThread = new Thread(() =>
            {
                while (_running)
                {
                    try
                    {
                        lock (_lockObject)
                        {
                            // 关闭已存在问题的TCPClient
                            if (_tcpClientError)
                            {
                                if (_tcpClient != null)
                                {
                                    _tcpClient.Close();
                                    _tcpClient = null;
                                }
                                _tcpClientError = false;
                                Thread.Sleep(100);
                            }
                            // 创建TCPClient
                            if (_tcpClient == null)
                            {
                                _tcpClient = new TcpClient();
                            }
                        }

                        // 检查TCPClient是否连接，如果没有连接，则尝试重新连接
                        if (_tcpClient.Connected == false)
                        {
                            try
                            {
                                // 如果连接时间超过1秒，则尝试重新连接。防止连接过快
                                if (TimeHelper.GetNow() - _tryConnectTime < 1000)
                                {
                                    var waitCount = 300;
                                    while (_running && waitCount > 0)
                                    {
                                        waitCount -= 1;
                                        Thread.Sleep(10); // 等待时间可以根据实际需要调整
                                    }
                                }
                                if (!connectLog)
                                {
                                    LogHelper.Debug($"TCP客户端{_serverIp}:{_serverPort} 连接中...");
                                    connectLog = true;
                                    sw_connect.Restart();
                                }
                                else
                                {
                                    if (sw_connect.ElapsedMilliseconds > 300000)
                                    {
                                        connectLog = false; //不要重复记录日志，5分钟记录一次
                                    }
                                }
                                _tryConnectTime = TimeHelper.GetNow();
                                _tcpClient.Connect(_serverIp, _serverPort);
                                if (_tcpClient.Connected)
                                {
                                    // 连接成功后数据初始化
                                    ConnectedInit(_tcpClient);
                                    LogHelper.Debug($"TCP客户端{_serverIp}:{_serverPort} 连接成功");
                                    Thread.Sleep(2);
                                    ModuleState = TcpClientModuleState.Connected;
                                }
                                else
                                {
                                    ModuleState = TcpClientModuleState.Started;
                                }
                            }
                            catch (Exception e)
                            {
                                lock (_lockObject)
                                {
                                    _tcpClient.Close();
                                    _tcpClient = null;
                                }
                                if (!errLog)
                                {
                                    LogHelper.Error($"TCP客户端{_serverIp}:{_serverPort} 连接异常：{e.Message}");
                                    errLog = true;
                                    sw_err.Restart();
                                }
                                else
                                {
                                    if (sw_err.ElapsedMilliseconds > 300000)
                                    {
                                        errLog = false; // 不要重复记录日志，5分钟记录一次
                                    }
                                }
                                continue;
                            }
                            if (!_tcpClient.Connected)
                            {
                                lock (_lockObject)
                                {
                                    _tcpClient.Close();
                                    _tcpClient = null;
                                }
                                LogHelper.Error($"TCP客户端{_serverIp}:{_serverPort} 无法连接到服务端：{_serverIp}");
                            }
                        }

                        if (_tcpClient != null && _tcpClient.Connected == true)
                        {
                            TcpClientLoop(_tcpClient);
                        }

                        if (_needClose)
                        {
                            _needClose = false;
                            Close();
                        }
                    }
                    catch (IOException ex) when (ex.InnerException is SocketException socketEx && (socketEx.SocketErrorCode == SocketError.TimedOut))
                    {
                        //Console.WriteLine("Read timed out.");
                    }
                    catch (Exception ex)
                    {
                        // 除了超时异常，其他都认为是异常，则尝试重新连接
                        _tcpClientError = true;
                        ModuleState = TcpClientModuleState.Started; //断开连接后将状态改为started
                        LogHelper.Debug($"TCP客户端{_serverIp}:{_serverPort} TCPClient通道异常，尝试重新连接：{ex.Message}");
                    }
                }

                _running = false;
                ModuleState = TcpClientModuleState.Stopped;
                _tcpClient = null;
                LogHelper.Debug($"TCP客户端{_serverIp}:{_serverPort} TCPClient通道维护模块已停止");
            })
            { IsBackground = true };

            _clientThread.Start();
        }

        // 虚拟方法
        protected virtual void StartClientInit() { }

        // 虚拟方法
        protected virtual void ConnectedInit(TcpClient _tcpClient) { }

        // 虚拟方法
        protected virtual void TcpClientLoop(TcpClient tcpClient) { }

        public bool Send(byte[] data, bool loggingEnabled = true, bool logSTR = true)
        {
            lock (_lockObject)
            {
                if (_tcpClient != null && _tcpClient.Connected)
                {
                    try
                    {
                        NetworkStream stream = _tcpClient.GetStream();
                        stream.Write(data, 0, data.Length);
                        // 记录日志
                        if (loggingEnabled && _logger != null)
                        {
                            try
                            {
                                // 先记录 十六进制
                                {
                                    var sendBufString = StringHelper.BytesToHexString(data, 0, data.Length);
                                    _logger.Debug($"发送数据0X--[{data.Length}]：{sendBufString}");
                                }

                                // 再记录字符串
                                if (logSTR)
                                {
                                    var sendBufString = Encoding.UTF8.GetString(data, 0, data.Length);
                                    _logger.Debug($"发送数据STR-[{data.Length}]：{sendBufString}");
                                }
                            }
                            catch (Exception)
                            {
                            }
                        }
                        return true;
                    }
                    catch (Exception ex)
                    {
                        _tcpClientError = true;
                        LogHelper.Debug($"TCP客户端{_serverIp}:{_serverPort} 发送异常，尝试重新连接：{ex.Message}");
                    }
                }
            }
            return false;
        }

        public void PushClose()
        {
            _needClose = true;
        }

        protected void Close()
        {
            lock (_lockObject)
            {
                if (_tcpClient == null) return;
                _tcpClient.Close();
                _tcpClient = null;
            }
            ModuleState = TcpClientModuleState.Started; //断开连接后将状态改为started
            LogHelper.Debug($"TCP客户端{_serverIp}:{_serverPort} TCPClient通道已断开");
            Thread.Sleep(100);
        }

        public void Stop()
        {
            // 检查是否已启动
            if (ModuleState != TcpClientModuleState.Started && ModuleState != TcpClientModuleState.Connected)
            {
                return;
            }

            ModuleState = TcpClientModuleState.Stopping;

            // 停止读取，等待线程结束
            _running = false;
            lock (_lockObject)
            {
                if (_tcpClient?.Connected == true)
                {
                    _tcpClient?.Close();
                }
            }
        }

        public bool IsConnected()
        {
            lock (_lockObject)
            {
                if (_tcpClient?.Connected == true)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
