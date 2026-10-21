using Serilog;
using Serilog.Core;
using CommonInterfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace CxVisual.Platform.Channels
{
    public class TcpClientChannel : TcpClientBase
    {
        // 数据缓冲区
        private byte[] buffer = new byte[256 * 1024];

        // 数据接收事件
        public event Action<byte[], int> DataReceivedEvent;

        // 网络流
        private NetworkStream _networkStream = null;

        // 数据接收
        protected virtual void OnDataReceived(byte[] data, int bytesRead)
        {
            DataReceivedEvent?.Invoke(data, bytesRead);
        }

        public TcpClientChannel(bool enableLog) : base(enableLog)
        {

        }

        // 继承TcpClientBase，在启动线程前运行了
        // 清空网络流
        protected override void StartClientInit()
        {
            _networkStream = null;
            base.StartClientInit();
        }

        // 继承TcpClientBase，在线程的循环(while)中运行
        protected override void TcpClientLoop(TcpClient tcpClient)
        {
            NetworkStream stream = tcpClient.GetStream();
            if (stream != _networkStream)
            {
                _networkStream = stream;
                _networkStream.ReadTimeout = 1000; // 1秒
            }

            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            if (bytesRead > 0)
            {
                OnDataReceived(buffer, bytesRead);

                // 记录日志
                if (_logger != null)
                {
                    try
                    {
                        // 先记录 十六进制
                        {
                            var recvBufString = StringHelper.BytesToHexString(buffer, 0, bytesRead);
                            _logger.Debug($"接收数据0X--[{bytesRead}]：{recvBufString}");
                        }

                        // 再记录字符串
                        {
                            var recvBufString = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            _logger.Debug($"接收数据STR-[{bytesRead}]：{recvBufString}");
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            else if (bytesRead == 0)
            {
                Close();
            }
        }
    }

}
