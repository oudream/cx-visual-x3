using System;
using System.Collections.Generic;
using System.Threading;

namespace CxVisual.Foundation.Transport
{
    /// <summary>
    /// 一个有界接收块的只读租约。Segment仅在租约存活时有效，调用者不得修改数组。
    /// 回调结束后需要继续处理时必须Retain，并在处理完成后Dispose；预算包含所有保留租约。
    /// 跨线程参与者各持有独立Retain租约；同一租约不能与读取并发Dispose。
    /// </summary>
    public sealed class FTRReceiveBuffer : IDisposable
    {
        internal sealed class Storage
        {
            internal readonly byte[] Bytes;
            internal readonly Action<byte[]> Return;
            internal int References=1;
            internal Storage(byte[] bytes,Action<byte[]> release){Bytes=bytes;Return=release;}
        }
        private Storage _storage;
        private FTRSubChannelAddress _address;
        private int _count;
        private string _remoteEndpoint;
        internal FTRReceiveBuffer(Storage storage){_storage=storage;}
        private FTRReceiveBuffer(Storage storage,FTRSubChannelAddress address,int count,string remoteEndpoint)
        {_storage=storage;_address=address;_count=count;_remoteEndpoint=remoteEndpoint;}
        public FTRSubChannelAddress Address {get{return _address;}}
        public string RemoteEndpoint { get { return _remoteEndpoint; } }
        public int Count {get{Demand();return _count;}}
        public ArraySegment<byte> Segment {get{return new ArraySegment<byte>(Demand().Bytes,0,_count);}}
        internal byte[] WritableBuffer {get{return Demand().Bytes;}}
        internal void Seal(FTRSubChannelAddress address,int count,string remoteEndpoint)
        {
            if(address==null)throw new ArgumentNullException("address");
            if(count<0||count>Demand().Bytes.Length)throw new ArgumentOutOfRangeException("count");
            if(_address!=null)throw new InvalidOperationException("Receive buffer already published");
            _address=address;_count=count;_remoteEndpoint=remoteEndpoint;
        }
        private Storage Demand()
        {
            Storage storage=Volatile.Read(ref _storage);
            if(storage==null)throw new ObjectDisposedException("FTRReceiveBuffer");
            return storage;
        }
        /// <summary>与Dispose并发时，要么成功取得独立租约，要么明确拒绝，不复活已归还的池块。</summary>
        public FTRReceiveBuffer Retain()
        {
            Storage storage=Demand();
            int references=Volatile.Read(ref storage.References);
            while(references>0)
            {
                if(references==int.MaxValue)throw new InvalidOperationException("Receive lease limit");
                int observed=Interlocked.CompareExchange(ref storage.References,references+1,references);
                if(observed==references)return new FTRReceiveBuffer(storage,_address,_count,_remoteEndpoint);
                references=observed;
            }
            throw new ObjectDisposedException("FTRReceiveBuffer");
        }
        public byte[] ToArray()
        {
            Storage storage=Demand();byte[] result=new byte[_count];
            Buffer.BlockCopy(storage.Bytes,0,result,0,_count);return result;
        }
        public void Dispose()
        {
            Storage storage=Interlocked.Exchange(ref _storage,null);
            if(storage!=null&&Interlocked.Decrement(ref storage.References)==0)
                storage.Return(storage.Bytes);
        }
    }

    // 仅管理通道接收内存；不是业务队列、任务调度器或第二套MRK。
    internal sealed class FTRReceiveBufferPool
    {
        private readonly object _gate=new object();
        private readonly Stack<byte[]> _free=new Stack<byte[]>();
        private readonly SemaphoreSlim _slots;
        internal readonly int BufferSize,Capacity;
        private int _allocated,_borrowed,_peak;
        private bool _closed;
        private long _waits;
        internal FTRReceiveBufferPool(int bufferSize,int capacity)
        {BufferSize=bufferSize;Capacity=capacity;_slots=new SemaphoreSlim(capacity,capacity);}
        internal FTRReceiveBuffer Rent(CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if(!_slots.Wait(0))
            {Interlocked.Increment(ref _waits);_slots.Wait(cancellation);}
            try
            {
                lock(_gate)
                {
                    if(_closed)throw new ObjectDisposedException("FTRReceiveBufferPool");
                    byte[] bytes=_free.Count>0?_free.Pop():new byte[BufferSize];
                    _borrowed++;_peak=Math.Max(_peak,_borrowed);
                    _allocated=Math.Max(_allocated,_borrowed+_free.Count);
                    return new FTRReceiveBuffer(new FTRReceiveBuffer.Storage(bytes,Return));
                }
            }
            catch{_slots.Release();throw;}
        }
        private void Return(byte[] bytes)
        {lock(_gate){if(!_closed)_free.Push(bytes);else _allocated--;_borrowed--;}_slots.Release();}
        internal void ReleaseUnused()
        {lock(_gate){_closed=true;_free.Clear();_allocated=_borrowed;}}
        internal FTRReceiveQueueSnapshot Snapshot(int pending,int receiveThread,int deliveryThread)
        {
            lock(_gate)return new FTRReceiveQueueSnapshot(BufferSize,Capacity,_allocated,
                _borrowed,_peak,pending,Interlocked.Read(ref _waits),receiveThread,deliveryThread);
        }
    }
}
