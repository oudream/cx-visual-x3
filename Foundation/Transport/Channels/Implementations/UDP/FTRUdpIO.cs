using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace CxVisual.Foundation.Transport
{
    /// <summary>UDP共用同步数据报I/O；每次接收保留边界和来源，不在通道层组帧或重发。</summary>
    internal static class FTRUdpIO
    {
        internal static void Receive(Socket socket, FTRChannelConfiguration config, CancellationToken token,
            Func<CancellationToken, FTRReceiveBuffer> rent, Action<FTRReceiveBuffer, int, IPEndPoint> deliver,
            Action<Exception> rejection, Action sweep, bool idleFault)
        {
            Stopwatch idle = Stopwatch.StartNew();
            while (!token.IsCancellationRequested)
            {
                if (sweep != null) sweep();
                if (!socket.Poll(50000, SelectMode.SelectRead))
                {
                    if (idleFault && config.ReceiveIdleTimeoutMs > 0 && idle.ElapsedMilliseconds >= config.ReceiveIdleTimeoutMs)
                        throw new TimeoutException("UDP receive idle");
                    continue;
                }
                FTRReceiveBuffer buffer = null;
                try
                {
                    buffer = rent(token);
                    EndPoint remote = socket.AddressFamily == AddressFamily.InterNetworkV6 ?
                        (EndPoint)new IPEndPoint(IPAddress.IPv6Any, 0) : new IPEndPoint(IPAddress.Any, 0);
                    int count = socket.ReceiveFrom(buffer.WritableBuffer, 0, buffer.WritableBuffer.Length, SocketFlags.None, ref remote);
                    idle.Restart();
                    deliver(buffer, count, (IPEndPoint)remote); buffer = null;
                }
                catch (SocketException error) when (error.SocketErrorCode == SocketError.MessageSize)
                {
                    // Windows已消费过大的报文；绝不能把截断片段当成完整UDP数据交付。
                    idle.Restart(); rejection(new IOException("UDP datagram exceeds configured receive block; discarded", error));
                }
                catch (SocketException error) when (!idleFault && error.SocketErrorCode == SocketError.ConnectionReset)
                { rejection(new IOException("UDP peer ICMP reset; listener remains open", error)); }
                finally { if (buffer != null) buffer.Dispose(); }
            }
        }
        internal static void Send(Socket socket, object gate, byte[] bytes, int offset, int count, EndPoint destination,
            int segmentBytes, int timeoutMs, CancellationToken token)
        {
            if (count > 65507) throw new ArgumentException("UDP单报文不得超过65507字节");
            if (segmentBytes > 0 && segmentBytes < count) throw new ArgumentException("UDP报文不能按TCP流方式分段");
            Stopwatch timer = Stopwatch.StartNew(); bool entered = false;
            try
            {
                while (!entered)
                {
                    token.ThrowIfCancellationRequested();
                    int remaining = timeoutMs - (int)Math.Min(int.MaxValue, timer.ElapsedMilliseconds);
                    if (remaining <= 0) throw new TimeoutException("UDP send gate timeout");
                    Monitor.TryEnter(gate, Math.Min(20, remaining), ref entered);
                }
                token.ThrowIfCancellationRequested();
                int writeRemaining = timeoutMs - (int)Math.Min(int.MaxValue, timer.ElapsedMilliseconds);
                if (writeRemaining <= 0) throw new TimeoutException("UDP send timeout");
                socket.SendTimeout = writeRemaining;
                using (token.Register(() => { try { socket.Close(); } catch (ObjectDisposedException) { } }))
                {
                    int sent = destination == null ? socket.Send(bytes, offset, count, SocketFlags.None) :
                        socket.SendTo(bytes, offset, count, SocketFlags.None, destination);
                    if (sent != count) throw new IOException("UDP datagram send incomplete");
                }
            }
            catch { token.ThrowIfCancellationRequested(); throw; }
            finally { if (entered) Monitor.Exit(gate); }
        }
    }
}
