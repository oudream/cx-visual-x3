using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace CxVisual.Foundation.Transport
{
    /// <summary>通道专属的快速交付FIFO。池额度覆盖接收中、排队、回调中及外部Retain的块。</summary>
    internal sealed class FTRReceivePipeline
    {
        private readonly object _gate=new object();
        private readonly Queue<Action> _pending=new Queue<Action>();
        private readonly FTRReceiveBufferPool _pool;
        private readonly Action<FTRReceiveBuffer> _deliver;
        private readonly Action<Exception> _error;
        private readonly Thread _delivery;
        private bool _complete;
        private int _receiveThread;
        private int _queuedBuffers;
        internal bool IsDeliveryThread {get{return Thread.CurrentThread==_delivery;}}
        internal bool IsReceiveThread
        {get{return Thread.CurrentThread.ManagedThreadId==Volatile.Read(ref _receiveThread);}}
        internal FTRReceivePipeline(string id,FTRChannelConfiguration configuration,
            Action<FTRReceiveBuffer> deliver,Action<Exception> error)
        {
            int size=Math.Min(configuration.ReceiveBufferBytes,configuration.ReceiveByteBudget);
            int capacity=Math.Min(configuration.QueueCapacity,configuration.ReceiveByteBudget/size);
            _pool=new FTRReceiveBufferPool(size,capacity);_deliver=deliver;_error=error;
            _delivery=new Thread(Deliver){IsBackground=true,Name="FTR.Deliver."+id};
            _delivery.Start();
        }
        internal FTRReceiveBuffer Rent(CancellationToken cancellation)
        {
            Volatile.Write(ref _receiveThread,Thread.CurrentThread.ManagedThreadId);
            return _pool.Rent(cancellation);
        }
        internal void Publish(FTRSubChannelAddress address,FTRReceiveBuffer buffer,int count)
        {
            buffer.Seal(address,count);
            lock(_gate)
            {
                if(_complete)throw new InvalidOperationException("Receive pipeline completed");
                _queuedBuffers++;
                _pending.Enqueue(()=>{lock(_gate)_queuedBuffers--;using(buffer)_deliver(buffer);});
                Monitor.PulseAll(_gate);
            }
        }
        // 每个存活Peer最多排入一个终止通知；直到FIFO处理该通知才释放其MaxClients额度。
        internal void AfterReceived(Action action)
        {lock(_gate){if(_complete)throw new InvalidOperationException("Receive pipeline completed");
            _pending.Enqueue(action);Monitor.PulseAll(_gate);}}
        private void Deliver()
        {
            while(true)
            {
                Action action;
                lock(_gate)
                {
                    while(_pending.Count==0&&!_complete)Monitor.Wait(_gate);
                    if(_pending.Count==0)return;
                    action=_pending.Dequeue();
                }
                try{action();}catch(Exception error){_error(error);}
            }
        }
        internal FTRReceiveQueueSnapshot Snapshot()
        {
            lock(_gate)
            {
                var snapshot=_pool.Snapshot(_queuedBuffers,Volatile.Read(ref _receiveThread),_delivery.ManagedThreadId);
                snapshot.PendingNotifications=Math.Max(0,_pending.Count-_queuedBuffers);return snapshot;
            }
        }
        internal void CompleteAndDrain(int timeoutMilliseconds,CancellationToken cancellation)
        {
            if(Thread.CurrentThread==_delivery)
                throw new InvalidOperationException("Receive callback cannot synchronously close its own delivery thread");
            lock(_gate){_complete=true;Monitor.PulseAll(_gate);}
            Stopwatch timer=Stopwatch.StartNew();
            while(!_delivery.Join(20))
            {
                cancellation.ThrowIfCancellationRequested();
                if(timer.ElapsedMilliseconds>=timeoutMilliseconds)
                    throw new TimeoutException("Receive callback has not settled; transport buffers remain leased");
            }
            _pool.ReleaseUnused();
        }
    }
}
