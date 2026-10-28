using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Transport
{
    /// <summary>
    /// 独立物理UDP接收者的有界数据报视图。保留来源Endpoint与消息边界；
    /// 队列只保存FTR原始池块租约，消费后转为安全快照，不抢读Socket。
    /// </summary>
    public sealed class FTRDatagramAdapter : IFTRDatagramIO, IDisposable
    {
        private readonly IFTRChannelTransport _channel;
        private readonly BlockingCollection<FTRReceiveBuffer> _pending;
        private readonly CancellationTokenSource _stop=new CancellationTokenSource();
        private int _disposed;
        public FTRDatagramAdapter(IFTRChannelTransport channel, FTRChannelTypeDescriptor descriptor,
            int queueCapacity=2)
        {
            if(channel==null)throw new ArgumentNullException("channel");
            if(descriptor==null || (descriptor.IO & FTRIOCapabilities.Datagram)==0)
                throw new ArgumentException("仅可接入Datagram能力的原始传输通道");
            if(queueCapacity<1 || queueCapacity>1024)throw new ArgumentOutOfRangeException("queueCapacity");
            _channel=channel;
            _pending=new BlockingCollection<FTRReceiveBuffer>(
                new ConcurrentQueue<FTRReceiveBuffer>(),queueCapacity);
            _channel.BufferReceived+=Received;
            _channel.StateChanged+=StateChanged;
        }
        private void Received(FTRReceiveBuffer buffer)
        {
            if(Volatile.Read(ref _disposed)!=0)return;
            FTRReceiveBuffer lease=buffer.Retain();
            try
            {
                // 队列满时阻塞上层交付，背压仍由既有池化接收预算控制。
                _pending.Add(lease,_stop.Token);
                lease=null;
            }
            catch(OperationCanceledException) when(Volatile.Read(ref _disposed)!=0){}
            catch(InvalidOperationException) when(Volatile.Read(ref _disposed)!=0 || _pending.IsAddingCompleted){}
            finally{if(lease!=null)lease.Dispose();}
        }
        private void StateChanged(FTRChannelState state)
        {
            if(state==FTRChannelState.Closed || state==FTRChannelState.Faulted)
                _pending.CompleteAdding();
        }
        public async Task<FTRDatagram> ReceiveFromAsync(CancellationToken cancellationToken)
        {
            if(Volatile.Read(ref _disposed)!=0)throw new ObjectDisposedException("FTRDatagramAdapter");
            using(var linked=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,_stop.Token))
            {
                // 本易用接口阻塞的是队列等待，不是第二个UDP Socket读取者。
                FTRReceiveBuffer lease;
                try{lease=await Task.Run(()=>_pending.Take(linked.Token),linked.Token).ConfigureAwait(false);}
                catch(InvalidOperationException) when(_pending.IsCompleted){return null;}
                using(lease)
                    return new FTRDatagram(string.IsNullOrWhiteSpace(lease.RemoteEndpoint)
                        ? lease.Address.ToString():lease.RemoteEndpoint,lease.ToArray());
            }
        }
        public Task SendToAsync(FTRDatagram datagram,CancellationToken cancellationToken)
        {
            if(datagram==null)throw new ArgumentNullException("datagram");
            if(Volatile.Read(ref _disposed)!=0)throw new ObjectDisposedException("FTRDatagramAdapter");
            FTRSubChannelAddress address=new FTRSubChannelAddress(_channel.Id);
            if(_channel.Topology==FTRChannelTopology.MultiPeer)
            {
                var peers=_channel as IFTRMultiPeerChannel;
                if(peers==null)throw new NotSupportedException("多对端Datagram缺少真实SubChannel管理者");
                var matches=peers.SnapshotSubChannels()
                    .Where(x=>string.Equals(x.RemoteEndpoint,datagram.RemoteEndpoint,StringComparison.Ordinal))
                    .ToArray();
                if(matches.Length!=1)throw new InvalidOperationException("目标Endpoint未连接或存在不唯一代次");
                address=matches[0].Address;
            }
            return _channel.SendAsync(datagram.Data,address,cancellationToken);
        }
        public void Dispose()
        {
            if(Interlocked.Exchange(ref _disposed,1)!=0)return;
            _channel.BufferReceived-=Received;_channel.StateChanged-=StateChanged;
            _stop.Cancel();_pending.CompleteAdding();
            FTRReceiveBuffer lease;
            while(_pending.TryTake(out lease))lease.Dispose();
            // 不夺取通道或Socket所有权，外部还需关闭Channel。
            _pending.Dispose();_stop.Dispose();
        }
    }
}
