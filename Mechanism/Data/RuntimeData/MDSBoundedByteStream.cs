using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Mechanism.Data.RuntimeData
{
    /// <summary>G05b：MRK节点间的单机有界字节块传输。仅承载数据，不创建执行器。</summary>
    public sealed class MDSBoundedByteStream : IDisposable
    {
        private readonly ConcurrentQueue<byte[]> _queue=new ConcurrentQueue<byte[]>();
        private readonly SemaphoreSlim _free;
        private readonly SemaphoreSlim _ready=new SemaphoreSlim(0);
        private readonly CancellationTokenSource _shutdown=new CancellationTokenSource();
        private readonly object _gate=new object();
        private bool _completed,_disposed;
        private Exception _fault;
        public int Capacity{get;private set;}
        public int MaximumChunkBytes{get;private set;}
        public long MaximumBufferedBytes{get{return (long)Capacity*MaximumChunkBytes;}}

        public MDSBoundedByteStream(int capacity,int maximumChunkBytes)
        {
            if(capacity<1||capacity>1024||maximumChunkBytes<1||
               maximumChunkBytes>16*1024*1024||
               (long)capacity*maximumChunkBytes>512L*1024*1024)
                throw new ArgumentOutOfRangeException("capacity/maximumChunkBytes");
            Capacity=capacity;MaximumChunkBytes=maximumChunkBytes;
            _free=new SemaphoreSlim(capacity,capacity);
        }
        public async Task WriteAsync(byte[] value,
            CancellationToken cancellation=default(CancellationToken))
        {
            if(value==null||value.Length>MaximumChunkBytes)
                throw new ArgumentException("mds.stream.invalid_chunk","value");
            using(var linked=CancellationTokenSource.CreateLinkedTokenSource(
                cancellation,_shutdown.Token))
            {
                await _free.WaitAsync(linked.Token).ConfigureAwait(false);
                lock(_gate)
                {
                    if(_disposed||_completed)
                    {
                        _free.Release();
                        throw new InvalidOperationException("mds.stream.closed");
                    }
                    _queue.Enqueue((byte[])value.Clone());
                    _ready.Release();
                }
            }
        }
        /// <summary>null仅代表正常结束，故障通过异常传播；所有块做防御复制。</summary>
        public async Task<byte[]> ReadAsync(
            CancellationToken cancellation=default(CancellationToken))
        {
            using(var linked=CancellationTokenSource.CreateLinkedTokenSource(
                cancellation,_shutdown.Token))
            {
                await _ready.WaitAsync(linked.Token).ConfigureAwait(false);
                lock(_gate)
                {
                    byte[] value;
                    if(_queue.TryDequeue(out value))
                    {
                        _free.Release();
                        return (byte[])value.Clone();
                    }
                    if(_completed)
                    {
                        _ready.Release();
                        if(_fault!=null)
                            throw new InvalidOperationException("mds.stream.producer_fault",_fault);
                        return null;
                    }
                    throw new InvalidOperationException("mds.stream.signal_without_item");
                }
            }
        }
        public void Complete(Exception error=null)
        {
            lock(_gate)
            {
                if(_disposed)throw new ObjectDisposedException("MDSBoundedByteStream");
                if(_completed)throw new InvalidOperationException("mds.stream.already_completed");
                _completed=true;_fault=error;_ready.Release();
            }
        }
        public void Dispose()
        {
            lock(_gate)
            {
                if(_disposed)return;
                _disposed=true;_completed=true;
                _shutdown.Cancel();
                // 已有await可能未结算，信号量留待托管回收，避免并发Dispose异常。
            }
        }
    }
}
