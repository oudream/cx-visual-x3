using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace CxVisual.Foundation.Transport
{
    /// <summary>Windows字节管道共用I/O；异步句柄避免Peek与同步写入竞争同一内核等待。</summary>
    internal static class FTRPipeIO
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PeekNamedPipe(SafePipeHandle handle, IntPtr buffer, uint length,
            IntPtr read, out uint available, IntPtr left);

        internal static int Available(PipeStream pipe)
        {
            uint available;
            if (PeekNamedPipe(pipe.SafePipeHandle, IntPtr.Zero, 0, IntPtr.Zero, out available, IntPtr.Zero))
                return (int)Math.Min(int.MaxValue, available);
            int error = Marshal.GetLastWin32Error();
            if (error == 109 || error == 232 || error == 233) return -1;
            throw new IOException("NamedPipe readiness failed", new Win32Exception(error));
        }
        internal static bool Idle(long lastReceive, int timeout)
        { return timeout > 0 && (Stopwatch.GetTimestamp() - lastReceive) * 1000.0 / Stopwatch.Frequency >= timeout; }
        internal static void WaitForProgress(CancellationToken token, long lastProgress)
        {
            // 连续流的短空隙不能每64KiB强制Sleep；短暂让出CPU，真正空闲后再低频等待。
            if (Idle(lastProgress, 2)) token.WaitHandle.WaitOne(5);
            else Thread.Yield();
        }

        internal static void Write(PipeStream pipe, object gate, byte[] bytes, int offset, int count,
            int segmentBytes, int timeout, CancellationToken cancellation)
        {
            Stopwatch timer = Stopwatch.StartNew(); bool taken = false;
            try
            {
                while (!taken)
                {
                    cancellation.ThrowIfCancellationRequested();
                    Monitor.TryEnter(gate, Math.Min(20, FTRTransportThread.Remaining(timer, timeout)), ref taken);
                }
                cancellation.ThrowIfCancellationRequested();
                using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
                {
                    deadline.CancelAfter(FTRTransportThread.Remaining(timer, timeout));
                    try
                    {
                        using (deadline.Token.Register(() => { try { pipe.Dispose(); } catch (IOException) { } }))
                        {
                            int chunk = segmentBytes == 0 ? count : segmentBytes;
                            for (int done = 0; done < count;)
                            {
                                deadline.Token.ThrowIfCancellationRequested();
                                int n = Math.Min(chunk, count - done);
                                pipe.Write(bytes, offset + done, n); done += n;
                            }
                            // 注册先结算；返回后取消原Token不得再关闭本次已完成的连接。
                            deadline.CancelAfter(Timeout.Infinite);
                        }
                        cancellation.ThrowIfCancellationRequested();
                        if (deadline.IsCancellationRequested || timer.ElapsedMilliseconds >= timeout)
                            throw new TimeoutException("NamedPipe write deadline exceeded; remote receipt is unknown");
                    }
                    catch
                    {
                        cancellation.ThrowIfCancellationRequested();
                        if (deadline.IsCancellationRequested)
                            throw new TimeoutException("NamedPipe write deadline exceeded; remote receipt is unknown");
                        throw;
                    }
                }
            }
            finally { if (taken) Monitor.Exit(gate); }
        }
    }
}
