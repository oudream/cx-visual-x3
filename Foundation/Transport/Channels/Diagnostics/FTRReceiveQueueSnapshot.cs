namespace CxVisual.Foundation.Transport
{
    /// <summary>真实接收池与传输交付线程的有界快照；不代表专业算法或现场时序SLA。</summary>
    public sealed class FTRReceiveQueueSnapshot
    {
        internal FTRReceiveQueueSnapshot(int size,int capacity,int allocated,int borrowed,
            int peak,int pending,long waits,int receiveThread,int deliveryThread)
        {
            BufferSize=size;Capacity=capacity;AllocatedBuffers=allocated;BorrowedBuffers=borrowed;
            PeakBorrowedBuffers=peak;PendingReceives=pending;BackpressureWaits=waits;
            ReceiveThreadId=receiveThread;DeliveryThreadId=deliveryThread;
        }
        public int BufferSize {get;private set;}
        public int Capacity {get;private set;}
        public int AllocatedBuffers {get;private set;}
        public int BorrowedBuffers {get;private set;}
        public int PeakBorrowedBuffers {get;private set;}
        public int PendingReceives {get;private set;}
        public int PendingNotifications {get;internal set;}
        public long BackpressureWaits {get;private set;}
        public int ReceiveThreadId {get;private set;}
        public int DeliveryThreadId {get;private set;}
        public long AllocatedBytes {get{return (long)AllocatedBuffers*BufferSize;}}
        public long InUseBytes {get{return (long)BorrowedBuffers*BufferSize;}}
    }
}
