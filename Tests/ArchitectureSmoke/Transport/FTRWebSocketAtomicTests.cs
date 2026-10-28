using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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
                MaxClients=2,MaxMessageBytes=131072
            };
            var clientConfig=new FTRWebSocketClientChannelConfiguration{
                Id="ws-atomic-client",Url="ws://localhost:"+port+"/atomic/",
                MaxMessageBytes=131072
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
                    // 旧FTR默认实现会将4096字节切成多条WS消息，本断言必须能抓住。
                    client.Send(request,segmentBytes:4096);
                    Check(serverReceived.Wait(8000) && Volatile.Read(ref requestMessages)==1 &&
                        received.SequenceEqual(request),
                        "Client同步分块必须只产生一条完整RFC6455消息");
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
            Console.WriteLine("F17.WS_ATOMIC_PASS client segmented send, server concurrent peer send and full-message integrity");
        }
    }
}
