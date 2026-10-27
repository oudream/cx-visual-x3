using System;
using System.Diagnostics;
using System.Threading;

namespace CxVisual.Foundation.Transport
{
    /// <summary>仅复用传输线程的共同截止时间，不调度业务任务。</summary>
    internal static class FTRTransportThread
    {
        internal static int Remaining(Stopwatch timer, int timeout)
        {
            long remaining = timeout - timer.ElapsedMilliseconds;
            if (remaining <= 0) throw new TimeoutException("Transport deadline exceeded (including queue wait)");
            return (int)remaining;
        }
        internal static void Join(Thread thread, Stopwatch timer, int timeout, CancellationToken cancellation)
        {
            if (thread == null) return;
            while (!thread.Join(0))
            {
                cancellation.ThrowIfCancellationRequested();
                thread.Join(Math.Min(20, Remaining(timer, timeout)));
            }
        }
    }
}
