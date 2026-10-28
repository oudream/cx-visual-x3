using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Transport;

namespace CxVisual.Tests.ArchitectureSmoke
{
    /// <summary>真实OS回环：两方向WebSocket分片、同Peer并发原子性和消息完整性。</summary>
    internal static class FTRWebSocketAtomicTests
    {
        private static void Check(bool condition,string detail)
        {if(!condition)throw new InvalidOperationException("F17.WS_ATOMIC "+detail);}

        private static int FreePort()
        {
            var listener=new TcpListener(IPAddress.Loopback,0);
            listener.Start();
            try{return ((IPEndPoint)listener.LocalEndpoint).Port;}
            finally{listener.Stop();}
        }

        public static void Run()
        {
            int port=FreePort();
            var serverConfig=new FTRWebSocketServerChannelConfiguration{
                Id="ws-atomic-server",Prefix="http://localhost:"+port+"/atomic/",
                MaxClients=2,MaxMessageBytes=131072,SendByteBudget=4096,SendQueueCapacity=1
            };
            var clientConfig=new FTRWebSocketClientChannelConfiguration{
                Id="ws-atomic-client",Url="ws://localhost:"+port+"/atomic/",
                MaxMessageBytes=131072,SendByteBudget=4096,SendQueueCapacity=1
            };
            using(var server=new FTRWebSocketServerChannel(serverConfig))
            using(var client=new FTRWebSocketClientChannel(clientConfig))
            using(var serverReceived=new ManualResetEventSlim())
            using(var clientReceived=new ManualResetEventSlim())
            {
                FTRSubChannelAddress peer=null;
                int requestMessages=0;
                byte[] received=null;
                var replies=new List<byte[]>();object gate=new object();
                server.Received+=data=>
                {
                    peer=data.Address;
                    received=data.Data;
                    if(Interlocked.Increment(ref requestMessages)==1)serverReceived.Set();
                };
                client.Received+=data=>
                {
                    lock(gate)
                    {
                        replies.Add(data.Data);
                        if(replies.Count==2)clientReceived.Set();
                    }
                };
                byte[] request=new byte[32779],a=new byte[65557],b=new byte[32789];
                for(int i=0;i<request.Length;i++)request[i]=(byte)(i%239);
                for(int i=0;i<a.Length;i++)a[i]=(byte)(31+i%181);
                for(int i=0;i<b.Length;i++)b[i]=(byte)(91+i%151);
                try
                {
                    server.Open();
                    client.Open();
                    bool clientRejected=false;
                    try{client.SendAsync(new byte[4097],new FTRSubChannelAddress(client.Id),
                        CancellationToken.None).GetAwaiter().GetResult();}
                    catch(InvalidOperationException){clientRejected=true;}
                    Check(clientRejected && client.Diagnostics.RejectedSends==1 &&
                        client.Diagnostics.LastFailure.Kind==FTRFailureKind.ResourceExhausted,
                        "无接收池的WS Client仍须在克隆前拒绝超预算异步发送");
                    // 旧FTR默认实现会将4096字节切成多条WS消息，本断言必须能抓住。
                    client.Send(request,segmentBytes:4096);
                    Check(serverReceived.Wait(8000) && Volatile.Read(ref requestMessages)==1 &&
                        received.SequenceEqual(request),
                        "Client同步分块必须只产生一条完整RFC6455消息");
                    bool serverRejected=false;
                    try{server.SendAsync(new byte[4097],peer,CancellationToken.None).GetAwaiter().GetResult();}
                    catch(InvalidOperationException){serverRejected=true;}
                    Check(serverRejected && server.Diagnostics.RejectedSends==1 &&
                        server.Diagnostics.LastFailure.Kind==FTRFailureKind.ResourceExhausted,
                        "无接收池的WS Server仍须在克隆前拒绝超预算异步发送");
                    Task sendA=Task.Run(()=>server.Send(a,0,a.Length,peer,CancellationToken.None));
                    Task sendB=Task.Run(()=>server.Send(b,0,b.Length,peer,CancellationToken.None));
                    Check(clientReceived.Wait(8000),"并发大消息未完整返回");
                    Task.WaitAll(sendA,sendB);
                    lock(gate)
                        Check(replies.Count==2 &&
                            ((replies[0].SequenceEqual(a) && replies[1].SequenceEqual(b)) ||
                             (replies[0].SequenceEqual(b) && replies[1].SequenceEqual(a))),
                            "两个Peer发送门保护下的消息不能交错或拆成多条");
                }
                finally
                {
                    if(client.State!=FTRChannelState.Closed)client.Close();
                    if(server.State!=FTRChannelState.Closed)server.Close();
                }
            }
            OversizedPeerIsolated();
            Console.WriteLine("F17.WS_ATOMIC_PASS client segmented send, server concurrent peer send, oversized peer isolation and full-message integrity");
        }

        private static void OversizedPeerIsolated()
        {
            int port=FreePort();
            string url="ws://localhost:"+port+"/peer/";
            var config=new FTRWebSocketServerChannelConfiguration{
                Id="ws-fault-isolation",Prefix="http://localhost:"+port+"/peer/",
                MaxClients=2,MaxMessageBytes=32
            };
            using(var server=new FTRWebSocketServerChannel(config))
            using(var oversized=new ClientWebSocket())
            using(var healthy=new ClientWebSocket())
            using(var received=new ManualResetEventSlim())
            using(var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(8)))
            {
                byte[] observed=null;
                server.Received+=data=>{observed=data.Data;received.Set();};
                try
                {
                    server.Open();
                    oversized.ConnectAsync(new Uri(url),timeout.Token).GetAwaiter().GetResult();
                    healthy.ConnectAsync(new Uri(url),timeout.Token).GetAwaiter().GetResult();
                    try
                    {
                        oversized.SendAsync(new ArraySegment<byte>(new byte[64]),
                            WebSocketMessageType.Binary,true,timeout.Token).GetAwaiter().GetResult();
                    }
                    catch(WebSocketException) { /* 接收端已拒绝超限客户端的连接 */ }
                    Check(SpinWait.SpinUntil(()=>
                    {
                        var failure=server.Diagnostics.LastFailure;
                        return failure!=null &&
                            failure.Kind==FTRFailureKind.ResourceExhausted &&
                            failure.Scope==FTRFaultScope.SubChannel;
                    },5000),"超大WebSocket消息应标记为Peer级资源预算故障");
                    byte[] good={5,7,9};
                    healthy.SendAsync(new ArraySegment<byte>(good),
                        WebSocketMessageType.Binary,true,timeout.Token).GetAwaiter().GetResult();
                    Check(received.Wait(5000) && observed.SequenceEqual(good) &&
                        server.State==FTRChannelState.Open,
                        "一个Peer超限退出时，监听器和其它正常Peer不得受影响");
                }
                finally
                {
                    if(server.State!=FTRChannelState.Closed)server.Close();
                }
            }
        }
    }
}
