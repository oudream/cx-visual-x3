using System;
using System.Diagnostics;

namespace CxVisual.Foundation.Transport
{
    /// <summary>一次I/O操作共享的单调截止时间；包括锁等待与全部分块，不代表远端业务确认。</summary>
    internal struct FTRDeadline
    {
        private readonly long _started;
        private readonly int _timeoutMs;

        internal FTRDeadline(int timeoutMs)
        {
            if (timeoutMs < 1) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
            _started = Stopwatch.GetTimestamp();
            _timeoutMs = timeoutMs;
        }

        internal int RemainingMs()
        {
            double elapsedMs = (Stopwatch.GetTimestamp() - _started) * 1000.0 / Stopwatch.Frequency;
            double remaining = _timeoutMs - elapsedMs;
            if (remaining <= 0) throw new TimeoutException("Transport deadline exceeded");
            return (int)Math.Max(1, Math.Ceiling(remaining));
        }
    }
}
