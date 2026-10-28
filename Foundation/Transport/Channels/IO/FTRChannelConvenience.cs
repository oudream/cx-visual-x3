using System;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Transport
{
    /// <summary>
    /// 长期驻留Channel的轻量便利接口：不创建收发线程、不缓存报文、不拥有Channel。
    /// 订阅可跨Close/Open继续有效；不再需要时由调用方Dispose订阅令牌。
    /// </summary>
    public static class FTRChannelConvenience
    {
        /// <summary>
        /// 从正式Transport接口读取一次健康视图；兼容Factory/Custom Provider，
        /// 无需调用方强制转换具体FTRChannelBase，也不建立监控线程。
        /// </summary>
        public static FTRChannelHealthSnapshot ReadHealth(this IFTRChannelTransport channel)
        {
            if(channel==null)throw new ArgumentNullException("channel");
            FTRChannelBase standard=channel as FTRChannelBase;
            if(standard!=null)return standard.Health;
            FTRChannelState state=channel.State;
            var multi=channel as IFTRSubChannelAware;
            int peers=state!=FTRChannelState.Open?0:
                (multi==null?1:multi.SnapshotSubChannels().Length);
            return new FTRChannelHealthSnapshot(
                channel.Id,channel.SessionId,state,peers,channel.Diagnostics);
        }

        /// <summary>订阅接收，并取得解除订阅令牌；只撤销本次观察，不关闭Channel。</summary>
        public static IDisposable SubscribeReceived(this IFTRChannelTransport channel,
            Action<FTRChannelReceiveContext> observer)
        {
            if(channel==null)throw new ArgumentNullException("channel");
            if(observer==null)throw new ArgumentNullException("observer");
            channel.Received+=observer;
            return new Subscription(()=>channel.Received-=observer);
        }

        /// <summary>只观察一个指定的SubChannelId和Generation，拒绝旧会话同名Peer混入。</summary>
        public static IDisposable SubscribeFrom(this IFTRChannelTransport channel,
            FTRSubChannelAddress address,Action<FTRChannelReceiveContext> observer)
        {
            if(channel==null)throw new ArgumentNullException("channel");
            if(address==null||address.ChannelId!=channel.Id)
                throw new ArgumentException("订阅地址与Channel不匹配","address");
            if(observer==null)throw new ArgumentNullException("observer");
            Action<FTRChannelReceiveContext> filter=message=>
            {
                if(address.Equals(message.Address))observer(message);
            };
            channel.Received+=filter;
            return new Subscription(()=>channel.Received-=filter);
        }

        /// <summary>
        /// 高速数据低复制订阅。事件提供的租约只在回调内有效；
        /// 需要长期持有或跨线程移交时请先Retain，再由持有者Dispose。
        /// 不生成第二个物理读取者或中间业务队列。
        /// </summary>
        public static IDisposable SubscribeBuffers(this IFTRChannelTransport channel,
            Action<FTRReceiveBuffer> observer)
        {
            if(channel==null)throw new ArgumentNullException("channel");
            if(observer==null)throw new ArgumentNullException("observer");
            channel.BufferReceived+=observer;
            return new Subscription(()=>channel.BufferReceived-=observer);
        }

        /// <summary>按SubChannelId及Generation过滤低复制块；旧Peer的Buffer不会误入新Peer。</summary>
        public static IDisposable SubscribeBuffersFrom(this IFTRChannelTransport channel,
            FTRSubChannelAddress address,Action<FTRReceiveBuffer> observer)
        {
            if(channel==null)throw new ArgumentNullException("channel");
            if(address==null||address.ChannelId!=channel.Id)
                throw new ArgumentException("接收地址与Channel不匹配","address");
            if(observer==null)throw new ArgumentNullException("observer");
            Action<FTRReceiveBuffer> filter=buffer=>
            {
                if(address.Equals(buffer.Address))observer(buffer);
            };
            channel.BufferReceived+=filter;
            return new Subscription(()=>channel.BufferReceived-=filter);
        }

        /// <summary>长期订阅状态变化；回调不负责业务恢复，令牌不拥有Channel。</summary>
        public static IDisposable SubscribeState(this IFTRChannelTransport channel,
            Action<FTRChannelState> observer)
        {
            if(channel==null)throw new ArgumentNullException("channel");
            if(observer==null)throw new ArgumentNullException("observer");
            channel.StateChanged+=observer;
            return new Subscription(()=>channel.StateChanged-=observer);
        }

        /// <summary>单端或共享总线的快捷发送。MultiPeer必须显式指定Address，禁止意外广播。</summary>
        public static void SendBytes(this IFTRChannelTransport channel,byte[] bytes,
            CancellationToken cancellation=default(CancellationToken))
        {
            if(channel==null)throw new ArgumentNullException("channel");
            if(bytes==null)throw new ArgumentNullException("bytes");
            if(channel.Topology==FTRChannelTopology.MultiPeer)
                throw new InvalidOperationException("多客户端Channel必须使用SendTo并指定SubChannelId和Generation");
            channel.Send(bytes,0,bytes.Length,new FTRSubChannelAddress(channel.Id),cancellation);
        }

        /// <summary>单端或共享总线的快捷异步发送；保留正式FTR快照、预算和取消语义。</summary>
        public static Task SendBytesAsync(this IFTRChannelTransport channel,byte[] bytes,
            CancellationToken cancellation=default(CancellationToken))
        {
            if(channel==null)throw new ArgumentNullException("channel");
            if(bytes==null)throw new ArgumentNullException("bytes");
            if(channel.Topology==FTRChannelTopology.MultiPeer)
                throw new InvalidOperationException("多客户端Channel必须使用SendToAsync并指定子客户端地址");
            return channel.SendAsync(bytes,new FTRSubChannelAddress(channel.Id),cancellation);
        }

        /// <summary>明确路由到指定Peer/Generation；同步调用复用原通道同步发送。</summary>
        public static void SendTo(this IFTRChannelTransport channel,FTRSubChannelAddress address,
            byte[] bytes,CancellationToken cancellation=default(CancellationToken))
        {
            if(channel==null)throw new ArgumentNullException("channel");
            if(bytes==null)throw new ArgumentNullException("bytes");
            if(address==null||address.ChannelId!=channel.Id)
                throw new ArgumentException("目标地址必须属于当前Channel","address");
            channel.Send(bytes,0,bytes.Length,address,cancellation);
        }

        /// <summary>显式目标异步发送；不创建第二套任务队列或隐式重试。</summary>
        public static Task SendToAsync(this IFTRChannelTransport channel,FTRSubChannelAddress address,
            byte[] bytes,CancellationToken cancellation=default(CancellationToken))
        {
            if(channel==null)throw new ArgumentNullException("channel");
            if(bytes==null)throw new ArgumentNullException("bytes");
            if(address==null||address.ChannelId!=channel.Id)
                throw new ArgumentException("目标地址必须属于当前Channel","address");
            return channel.SendAsync(bytes,address,cancellation);
        }

        /// <summary>
        /// 按收到的Address定向回复；HTTP调用原RespondAsync（完成仅表示接纳），
        /// 其它Channel使用原SendAsync。绝不广播或自动重放未知结果。
        /// </summary>
        public static Task ReplyAsync(this IFTRChannelTransport channel,FTRChannelReceiveContext received,
            byte[] bytes,CancellationToken cancellation=default(CancellationToken))
        {return ReplyAsync(channel,received,bytes,200,cancellation);}

        public static Task ReplyAsync(this IFTRChannelTransport channel,FTRChannelReceiveContext received,
            byte[] bytes,int statusCode,CancellationToken cancellation=default(CancellationToken))
        {
            if(channel==null)throw new ArgumentNullException("channel");
            if(received==null)throw new ArgumentNullException("received");
            if(bytes==null)throw new ArgumentNullException("bytes");
            FTRSubChannelAddress address=received.Address;
            if(address==null||address.ChannelId!=channel.Id)
                throw new ArgumentException("请求来源不属于当前Channel","received");
            // 长驻Channel重开后，同一ChannelId不能使旧Session请求投递到新物理会话。
            // Guid.Empty代表旧第三方Provider未提供会话来源，维持兼容行为。
            if(received.SessionId!=Guid.Empty && received.SessionId!=channel.SessionId)
                throw new InvalidOperationException("接收请求来自旧Session，禁止向新物理会话回复");
            cancellation.ThrowIfCancellationRequested();
            var requestResponse=channel as IFTRRequestResponseIO;
            if(requestResponse!=null)
                return requestResponse.RespondAsync(address,statusCode,bytes,cancellation);
            return channel.SendAsync(bytes,address,cancellation);
        }

        private sealed class Subscription : IDisposable
        {
            private Action _release;
            internal Subscription(Action release){_release=release;}
            public void Dispose()
            {
                Action release=Interlocked.Exchange(ref _release,null);
                if(release!=null)release();
            }
        }
    }
}
