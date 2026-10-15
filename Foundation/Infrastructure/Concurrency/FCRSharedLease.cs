using System;
using System.Threading;
namespace CxVisual.Foundation.Infrastructure.Concurrency
{
    /// <summary>同一数据供多异步分支共享。最后一个引用释放时，恰好执行一次清理回调。</summary>
    public sealed class FCRSharedLease<T> : IDisposable where T : class
    {
        private sealed class Holder
        {
            internal readonly T Data;
            internal readonly Action<T> Release;
            internal int Count = 1;
            internal Holder(T data, Action<T> release) { Data = data; Release = release; }
        }
        private Holder _holder;
        public FCRSharedLease(T data, Action<T> release)
        {
            if (data == null) throw new ArgumentNullException("data");
            _holder = new Holder(data, release);
        }
        private FCRSharedLease(Holder holder) { _holder = holder; }
        public T Value
        {
            get
            {
                Holder holder = Volatile.Read(ref _holder);
                if (holder == null) throw new ObjectDisposedException("FCRSharedLease");
                return holder.Data;
            }
        }
        public FCRSharedLease<T> Retain()
        {
            Holder holder = Volatile.Read(ref _holder);
            if (holder == null) throw new ObjectDisposedException("FCRSharedLease");
            while (true)
            {
                int count = Volatile.Read(ref holder.Count);
                if (count <= 0) throw new ObjectDisposedException("FCRSharedLease");
                if (count == int.MaxValue) throw new InvalidOperationException("引用计数溢出");
                if (Interlocked.CompareExchange(ref holder.Count, count + 1, count) == count)
                    return new FCRSharedLease<T>(holder);
            }
        }
        public void Dispose()
        {
            Holder holder = Interlocked.Exchange(ref _holder, null);
            if (holder == null) return;
            if (Interlocked.Decrement(ref holder.Count) == 0 && holder.Release != null)
                holder.Release(holder.Data);
        }
    }
}
