using System;

namespace CxVisual.Foundation.Transport
{
    /// <summary>通道内全部Peer共享的在途内存额度；不拥有业务消息或另建收发队列。</summary>
    internal sealed class FTRBufferedByteBudget
    {
        private readonly object _gate=new object();
        private readonly long _limit;
        private long _current,_peak,_rejected;
        internal FTRBufferedByteBudget(long limit)
        {
            if(limit<1)throw new ArgumentOutOfRangeException(nameof(limit));
            _limit=limit;
        }
        internal FTRBufferedByteLease Lease(){return new FTRBufferedByteLease(this);}
        internal void Reserve(long bytes)
        {
            if(bytes<0)throw new ArgumentOutOfRangeException(nameof(bytes));
            lock(_gate)
            {
                if(bytes>_limit-_current)
                {
                    _rejected++;
                    throw new FTRBudgetExceededException("Channel多对端在途内存预算已满");
                }
                _current+=bytes;
                if(_current>_peak)_peak=_current;
            }
        }
        internal void Release(long bytes)
        {
            if(bytes<0)throw new ArgumentOutOfRangeException(nameof(bytes));
            lock(_gate)
            {
                if(bytes>_current)throw new InvalidOperationException("FTR缓冲额度重复释放");
                _current-=bytes;
            }
        }
        internal void Snapshot(out long current,out long peak,out long rejected,out long limit)
        {
            lock(_gate){current=_current;peak=_peak;rejected=_rejected;limit=_limit;}
        }
    }

    /// <summary>额度随作用域可靠释放；增减容量不产生新队列和内存拷贝。</summary>
    internal sealed class FTRBufferedByteLease : IDisposable
    {
        private readonly FTRBufferedByteBudget _budget;
        private long _bytes;
        private bool _disposed;
        internal FTRBufferedByteLease(FTRBufferedByteBudget budget)
        {_budget=budget??throw new ArgumentNullException(nameof(budget));}
        internal void Reserve(long bytes)
        {
            if(_disposed)throw new ObjectDisposedException(nameof(FTRBufferedByteLease));
            _budget.Reserve(bytes);
            _bytes+=bytes;
        }
        internal void Release(long bytes)
        {
            if(_disposed||bytes<0||bytes>_bytes)
                throw new InvalidOperationException("释放的额度不属于此租约");
            _budget.Release(bytes);
            _bytes-=bytes;
        }
        public void Dispose()
        {
            if(_disposed)return;
            _disposed=true;
            _budget.Release(_bytes);
            _bytes=0;
        }
    }
}
