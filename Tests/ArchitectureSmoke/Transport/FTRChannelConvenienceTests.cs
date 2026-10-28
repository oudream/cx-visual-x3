using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Transport;

namespace CxVisual.Tests.ArchitectureSmoke
{
    /// <summary>长期驻留实例及便利调用真实路径：订阅只解除观察者、不夺取Channel所有权。</summary>
    internal static class FTRChannelConvenienceTests
    {
        private static void Check(bool okay,string message)
        {if(!okay)throw new InvalidOperationException("F17.CONVENIENCE "+message);}

        private sealed class PeerProbe : FTRMultiChannelBase
        {
            internal FTRSubChannelAddress LastDestination;
            internal byte[] LastPayload;
            internal PeerProbe():base("f17-peers"){}
            protected override Task OnOpenAsync(CancellationToken ct){return Task.CompletedTask;}
            protected override Task OnCloseAsync(CancellationToken ct){return Task.CompletedTask;}
            protected override Task SendToPeerAsync(byte[] bytes,FTRSubChannelAddress destination,CancellationToken ct)
            {
                LastDestination=destination;
                LastPayload=(byte[])bytes.Clone();
                return Task.CompletedTask;
            }
            internal void Deliver(FTRSubChannelAddress address,byte[] bytes)
            {ReceiveFrom(address,bytes);}
        }

        public static void Run()
        {
            LongLivedVirtualChannel();
            PeerReplyAndGeneration();
            Console.WriteLine("F17.CONVENIENCE_PASS long-lived subscribe, observer isolation, safe reply and generation routing");
        }

        private static void LongLivedVirtualChannel()
        {
            var pair=FTRVirtualChannel.CreatePair("f17-live-left","f17-live-right");
            using(var left=pair[0])
            using(var right=pair[1])
            {
                int delivered=0,states=0,replies=0;
                FTRChannelReceiveContext last=null;
                IDisposable good=right.SubscribeReceived(ctx=>{last=ctx;delivered++;});
                IDisposable bad=right.SubscribeReceived(ctx=>{throw new IOException("plugin failure");});
                IDisposable watch=right.SubscribeState(s=>Interlocked.Increment(ref states));
                IDisposable answered=left.SubscribeReceived(ctx=>{
                    if(ctx.Data.SequenceEqual(new byte[]{44}))Interlocked.Increment(ref replies);
                });
                try
                {
                    left.Open();right.Open();
                    left.SendBytes(new byte[]{1,2,3});
                    FTRChannelHealthSnapshot view=((IFTRChannelTransport)right).ReadHealth();
                    Check(view.ChannelId==right.Id && view.SessionId==right.SessionId &&
                        view.State==FTRChannelState.Open && view.ActivePeers==1,
                        "工厂/接口视图须直接提供正式健康状态和Session");
                    Check(delivered==1 && last!=null && last.Address.ChannelId==right.Id &&
                        last.SessionId==right.SessionId && last.SessionId!=Guid.Empty &&
                        right.Diagnostics.LastFailure!=null &&
                        right.Diagnostics.LastFailure.Stage=="Observer" &&
                        right.State==FTRChannelState.Open,
                        "一个接收插件异常不得导致其他订阅失效或关闭Virtual Channel");
                    bad.Dispose();bad.Dispose();
                    right.ReplyAsync(last,new byte[]{44}).GetAwaiter().GetResult();
                    Check(replies==1,"单端快捷ReplyAsync应回原Channel且不持有业务任务");

                    Guid session=right.SessionId;
                    FTRChannelReceiveContext staleMessage=last;
                    right.Close();
                    right.Open();
                    bool staleSessionRejected=false;
                    try { right.ReplyAsync(staleMessage,new byte[]{45}).GetAwaiter().GetResult(); }
                    catch(InvalidOperationException) { staleSessionRejected=true; }
                    Check(staleSessionRejected && replies==1,
                        "旧Session收到的请求不得跨Close/Open误回复到新物理会话");
                    Check(right.SessionId!=session && right.State==FTRChannelState.Open,
                        "同一Channel对象须支持正常Close/Open刷新Session而不是每次创建实例");
                    left.SendBytesAsync(new byte[]{5},CancellationToken.None).GetAwaiter().GetResult();
                    Check(delivered==2 && last.Data.SequenceEqual(new byte[]{5}) &&
                        last.SessionId==right.SessionId && last.SessionId!=session,
                        "订阅在Channel重开后仍可用，接收上下文必须区分新旧Session");
                    good.Dispose();good.Dispose();
                    left.SendBytes(new byte[]{6});
                    Check(delivered==2 && right.State==FTRChannelState.Open,
                        "解除订阅不得关闭长期驻留Channel，也不能再交付到已取消观察者");
                }
                finally
                {
                    good.Dispose();bad.Dispose();watch.Dispose();answered.Dispose();
                }
                Check(states>=3 && right.State==FTRChannelState.Open,
                    "订阅令牌不拥有Socket/Channel，外部Host继续维护Channel");
                right.Close();left.Close();
            }
        }

        private static void PeerReplyAndGeneration()
        {
            using(var server=new PeerProbe())
            {
                int first=0,all=0;
                var peer=server.RegisterPeer("client-17","loopback").Address;
                IDisposable filtered=server.SubscribeFrom(peer,ctx=>first++);
                IDisposable observation=server.SubscribeReceived(ctx=>all++);
                try
                {
                    server.Open();
                    bool multicastRejected=false;
                    try{server.SendBytes(new byte[]{1});}
                    catch(InvalidOperationException){multicastRejected=true;}
                    Check(multicastRejected,"MultiPeer便利发送必须禁止丢失子客户端ID导致广播");
                    server.Deliver(peer,new byte[]{7});
                    Check(((IFTRChannelTransport)server).ReadHealth().ActivePeers==1,
                        "MultiPeer健康视图应使用正式子客户端目录");
                    Check(first==1&&all==1,"订阅过滤必须按真实SubChannel ID匹配");
                    var context=new FTRChannelReceiveContext(peer,new byte[]{1});
                    server.ReplyAsync(context,new byte[]{11},CancellationToken.None).GetAwaiter().GetResult();
                    Check(peer.Equals(server.LastDestination)&&server.LastPayload.SequenceEqual(new byte[]{11}),
                        "按Received.Address回复必须保留SubChannel+Generation");
                    int peerRemovedNotifications=0;
                    server.PeerRemoved+=address=>{throw new IOException("faulty peer observer");};
                    server.PeerRemoved+=address=>Interlocked.Increment(ref peerRemovedNotifications);
                    Check(server.RemovePeer(peer) && peerRemovedNotifications==1 &&
                        server.Diagnostics.LastFailure!=null &&
                        server.Diagnostics.LastFailure.Stage=="Observer" &&
                        server.Diagnostics.LastFailure.Scope==FTRFaultScope.Operation &&
                        server.State==FTRChannelState.Open,
                        "PeerRemoved观察者异常不能截断其它订阅或误分类为物理Receive故障");
                    var replacement=server.RegisterPeer("client-17","loopback").Address;
                    Check(!peer.Equals(replacement),"重连必须更新Generation");
                    server.Deliver(replacement,new byte[]{9});
                    Check(first==1 && all==2,
                        "旧Generation过滤订阅不得误收新会话同名Peer数据");
                    bool staleRejected=false;
                    try{server.ReplyAsync(context,new byte[]{12}).GetAwaiter().GetResult();}
                    catch(InvalidOperationException){staleRejected=true;}
                    Check(staleRejected,"旧会话的Received请求不得路由到重连后的客户端");
                    server.SendToAsync(replacement,new byte[]{13}).GetAwaiter().GetResult();
                    Check(replacement.Equals(server.LastDestination) &&
                        server.LastPayload.SequenceEqual(new byte[]{13}),"新代次定向发送须工作");
                    server.Close();
                }
                finally {filtered.Dispose();observation.Dispose();}
            }
        }
    }
}
