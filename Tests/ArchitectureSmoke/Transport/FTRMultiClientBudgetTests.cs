using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Transport;

namespace CxVisual.Tests.ArchitectureSmoke
{
    /// <summary>真实HTTP/WS多客户端：SubChannel+Generation定向回复，合计内存、异常隔离及额度归还。</summary>
    internal static class FTRMultiClientBudgetTests
    {
        private static void Check(bool ok,string why)
        {if(!ok)throw new InvalidOperationException("F17.MULTI_BUDGET "+why);}
        private static int Port()
        {
            var l=new TcpListener(IPAddress.Loopback,0);l.Start();
            try{return ((IPEndPoint)l.LocalEndpoint).Port;}
            finally{l.Stop();}
        }

        public static void Run()
        {
            SharedBudgetLease();
            HttpClientSegmentedPost();
            HttpConcurrentResponses();
            HttpAggregateOverload();
            WebSocketTwoClientsRouted();
            WebSocketFragmentedPeer();
            Console.WriteLine("F17.MULTI_BUDGET_PASS request/response budget, HTTP address routing, WebSocket fragment isolation, release");
        }

        private static void SharedBudgetLease()
        {
            var config=new FTRHttpServerChannelConfiguration {Id="f17-budget-model",
                TotalBufferedBytes=140,MaxRequestBytes=16,MaxResponseBytes=100};
            var codec=new FTRChannelConfigurationConverter();
            var restored=(FTRHttpServerChannelConfiguration)codec.Read(codec.Write(config));
            Check(restored.TotalBufferedBytes==140,"HTTP合计配额FCM未往返");
            var ws=new FTRWebSocketServerChannelConfiguration {Id="f17-budget-ws",
                TotalBufferedBytes=1100,MaxMessageBytes=1024};
            var restoredWs=(FTRWebSocketServerChannelConfiguration)codec.Read(codec.Write(ws));
            Check(restoredWs.TotalBufferedBytes==1100,"WS合计配额FCM未往返");

            // 真实Channel即使未打开也不能配置无效配额。
            bool rejected=false;
            try{new FTRHttpServerChannelConfiguration{TotalBufferedBytes=0}.Validate();}
            catch(ArgumentException){rejected=true;}
            Check(rejected,"HTTP 0预算必须拒绝");
        }

        private static void HttpClientSegmentedPost()
        {
            int port=Port();
            string baseUrl="http://localhost:"+port+"/segment/";
            var serverConfig=new FTRHttpServerChannelConfiguration {
                Id="f17-http-segment-server",Prefix=baseUrl,MaxClients=2,
                MaxRequestBytes=1024,MaxResponseBytes=1024,TotalBufferedBytes=65536,
                ReceiveBufferBytes=32,ResponseTimeoutMs=5000
            };
            var clientConfig=new FTRHttpClientChannelConfiguration {
                Id="f17-http-segment-client",Url=baseUrl,RequestTimeoutMs=5000,
                MaxResponseBytes=1024,StopTimeoutMs=5000
            };
            using(var server=new FTRHttpServerChannel(serverConfig))
            using(var client=new FTRHttpClientChannel(clientConfig))
            {
                int requests=0,responses=0;
                byte[] first=Enumerable.Range(0,37).Select(x=>(byte)x).ToArray();
                byte[] receivedRequest=null,receivedResponse=null;
                FTRSubChannelAddress origin=null;
                server.Received+=packet=>
                {
                    Interlocked.Increment(ref requests);
                    origin=packet.Address;
                    receivedRequest=packet.Data;
                    server.ReplyAsync(packet,new byte[]{99},CancellationToken.None)
                        .GetAwaiter().GetResult();
                };
                client.Received+=packet=>
                {
                    Interlocked.Increment(ref responses);
                    receivedResponse=packet.Data;
                };
                try
                {
                    server.Open();client.Open();
                    client.Send(first,segmentBytes:3);
                    Check(requests==1 && responses==1 && receivedRequest.SequenceEqual(first) &&
                        receivedResponse.SequenceEqual(new byte[]{99}) &&
                        origin!=null && origin.HasSubChannel && origin.Generation>0,
                        "一次同步分段发送必须只产生一个HTTP POST，正文完整且按请求子ID回复");
                    // 异步发送复用同一HTTP请求路径，并由ChannelBase一次取得源数据快照。
                    client.SendBytesAsync(first,CancellationToken.None).GetAwaiter().GetResult();
                    Check(requests==2 && responses==2 && receivedRequest.SequenceEqual(first) &&
                        receivedResponse.SequenceEqual(new byte[]{99}),
                        "HTTP Client同步/异步不得建两套独立传输或误分裂请求");
                }
                finally
                {
                    if(client.State!=FTRChannelState.Closed)client.Close();
                    if(server.State!=FTRChannelState.Closed)server.Close();
                }
            }
        }

        private static void HttpConcurrentResponses()
        {
            int port=Port();
            var cfg=new FTRHttpServerChannelConfiguration {
                Id="http-multi-budget",Prefix="http://localhost:"+port+"/",
                MaxClients=2,ReceiveBufferBytes=8,TotalBufferedBytes=140,
                MaxRequestBytes=16,MaxResponseBytes=100,ResponseTimeoutMs=12000
            };
            using(var server=new FTRHttpServerChannel(cfg))
            using(var client=new HttpClient())
            using(var firstReady=new ManualResetEventSlim())
            using(var secondDone=new ManualResetEventSlim())
            using(var releaseFirst=new ManualResetEventSlim())
            {
                FTRSubChannelAddress firstAddress=null,secondAddress=null;
                var diagnosticAddresses=new System.Collections.Generic.List<FTRSubChannelAddress>();
                object evidenceGate=new object();
                server.FrameRecorded+=frame=>
                {
                    if(frame.Direction==FTRFrameDirection.Rx)
                        lock(evidenceGate)diagnosticAddresses.Add(frame.Address);
                };
                Exception callbackError=null;
                bool secondBudgetRejected=false;
                byte[] firstPayload=Enumerable.Repeat((byte)41,80).ToArray();
                server.Received+=packet=>
                {
                    try
                    {
                        byte[] input=packet.Data;
                        if(input.Length!=1)throw new InvalidOperationException("HTTP payload length");
                        if(input[0]==1)
                        {
                            firstAddress=packet.Address;
                            server.ReplyAsync(packet,firstPayload,CancellationToken.None)
                                .GetAwaiter().GetResult();
                            firstReady.Set();
                            if(!releaseFirst.Wait(10000))
                                throw new TimeoutException("等待另一HTTP请求验证总额度");
                        }
                        else if(input[0]==2)
                        {
                            secondAddress=packet.Address;
                            try
                            {
                                server.ReplyAsync(packet,firstPayload,CancellationToken.None)
                                    .GetAwaiter().GetResult();
                            }
                            catch(InvalidOperationException){secondBudgetRejected=true;}
                            server.ReplyAsync(packet,new byte[]{22},CancellationToken.None)
                                .GetAwaiter().GetResult();
                            secondDone.Set();
                        }
                        else throw new InvalidOperationException("HTTP unexpected input");
                    }
                    catch(Exception error)
                    {
                        callbackError=error;
                        firstReady.Set();secondDone.Set();releaseFirst.Set();
                    }
                };
                try
                {
                    server.Open();
                    client.Timeout=TimeSpan.FromSeconds(15);
                    Task<HttpResponseMessage> first=client.PostAsync(cfg.Prefix,new ByteArrayContent(new byte[]{1}));
                    Check(firstReady.Wait(6000),"第一HTTP响应没有占住预算");
                    Check(callbackError==null,"第一HTTP回调异常: "+callbackError);
                    var peak1=server.Diagnostics;
                    Check(peak1.BufferedPayloadBytes>=80 && peak1.PayloadBufferByteBudget==140,
                        "首个响应副本必须进入共享额度");
                    Task<HttpResponseMessage> second=client.PostAsync(cfg.Prefix,new ByteArrayContent(new byte[]{2}));
                    Check(secondDone.Wait(6000),"第二并发HTTP请求没有被执行");
                    Check(callbackError==null && secondBudgetRejected,"未按合计预算拒绝第二大响应");
                    releaseFirst.Set();
                    using(var firstReply=first.GetAwaiter().GetResult())
                    using(var secondReply=second.GetAwaiter().GetResult())
                    {
                        Check(firstReply.IsSuccessStatusCode && secondReply.IsSuccessStatusCode &&
                            firstReply.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult().SequenceEqual(firstPayload) &&
                            secondReply.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult().SequenceEqual(new byte[]{22}),
                            "同一HTTP Server必须分别向两个SubChannel定向响应，不得串包");
                    }
                    Check(firstAddress!=null && secondAddress!=null &&
                        firstAddress.HasSubChannel && secondAddress.HasSubChannel &&
                        !firstAddress.Equals(secondAddress) &&
                        firstAddress.Generation>=1 && secondAddress.Generation>=1,
                        "HTTP请求必须携带唯一子客户端请求ID及Generation");
                    lock(evidenceGate)
                        Check(diagnosticAddresses.Any(addr=>addr.Equals(firstAddress)) &&
                            diagnosticAddresses.Any(addr=>addr.Equals(secondAddress)),
                            "正式诊断Rx帧必须保留各HTTP请求的SubChannelId及Generation");
                    Check(SpinWait.SpinUntil(()=>server.Diagnostics.BufferedPayloadBytes==0,5000) &&
                        server.Diagnostics.RejectedPayloadBuffers>=1 &&
                        server.Diagnostics.PeakBufferedPayloadBytes<=140 &&
                        server.State==FTRChannelState.Open,
                        "HTTP响应结束必须全部归还额度，拒绝不能影响其它客户端");
                }
                finally
                {
                    releaseFirst.Set();
                    if(server.State!=FTRChannelState.Closed)server.Close();
                }
                Check(server.Diagnostics.BufferedPayloadBytes==0,"HTTP关闭后额度未清零");
            }
        }


        private static void HttpAggregateOverload()
        {
            int port=Port();
            var cfg=new FTRHttpServerChannelConfiguration {
                Id="http-total-overload",Prefix="http://localhost:"+port+"/",
                MaxClients=2,ReceiveBufferBytes=8,MaxRequestBytes=16,
                MaxResponseBytes=16,TotalBufferedBytes=30,ResponseTimeoutMs=12000
            };
            using(var server=new FTRHttpServerChannel(cfg))
            using(var client=new HttpClient())
            using(var firstReceived=new ManualResetEventSlim())
            using(var release=new ManualResetEventSlim())
            {
                FTRSubChannelAddress firstAddress=null;
                server.Received+=packet=>
                {
                    firstAddress=packet.Address;
                    firstReceived.Set();
                    release.Wait(10000);
                    server.RespondAsync(packet.Address,200,new byte[]{7},CancellationToken.None)
                        .GetAwaiter().GetResult();
                };
                try
                {
                    server.Open();
                    client.Timeout=TimeSpan.FromSeconds(15);
                    Task<HttpResponseMessage> first=client.PostAsync(cfg.Prefix,new ByteArrayContent(new byte[]{1}));
                    Check(firstReceived.Wait(5000),"首个HTTP请求尚未占用在途额度");
                    Task<HttpResponseMessage> second=client.PostAsync(cfg.Prefix,new ByteArrayContent(new byte[]{2}));
                    using(var overflow=second.GetAwaiter().GetResult())
                        Check(overflow.StatusCode==HttpStatusCode.ServiceUnavailable,
                            "所有请求合计额度耗尽须503，而非报告单请求体413");
                    release.Set();
                    using(var accepted=first.GetAwaiter().GetResult())
                        Check(accepted.IsSuccessStatusCode &&
                            accepted.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
                                .SequenceEqual(new byte[]{7}),
                            "被拒绝的第二请求不得破坏已接纳的第一个请求");
                    Check(server.State==FTRChannelState.Open && firstAddress!=null &&
                        firstAddress.HasSubChannel && server.Diagnostics.RejectedPayloadBuffers>=1 &&
                        SpinWait.SpinUntil(()=>server.Diagnostics.BufferedPayloadBytes==0,5000),
                        "合计额度拒绝、健康状态及资源归还不正确");
                }
                finally
                {
                    release.Set();
                    if(server.State!=FTRChannelState.Closed)server.Close();
                }
            }
        }

        private static void WebSocketTwoClientsRouted()
        {
            int port=Port();
            string url="ws://localhost:"+port+"/routes/";
            var cfg=new FTRWebSocketServerChannelConfiguration {
                Id="ws-routed",Prefix="http://localhost:"+port+"/routes/",
                MaxClients=2,MaxMessageBytes=128,ReceiveBufferBytes=16,TotalBufferedBytes=2048
            };
            using(var server=new FTRWebSocketServerChannel(cfg))
            using(var a=new ClientWebSocket())
            using(var b=new ClientWebSocket())
            using(var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(12)))
            {
                FTRSubChannelAddress first=null,second=null;
                server.Received+=packet=>
                {
                    byte[] bytes=packet.Data;
                    if(bytes[0]==1)first=packet.Address;
                    else if(bytes[0]==2)second=packet.Address;
                    else throw new InvalidOperationException("unexpected WS packet");
                    server.Send(new byte[]{(byte)(bytes[0]+10)},0,1,
                        packet.Address,CancellationToken.None);
                };
                try
                {
                    server.Open();
                    a.ConnectAsync(new Uri(url),deadline.Token).GetAwaiter().GetResult();
                    b.ConnectAsync(new Uri(url),deadline.Token).GetAwaiter().GetResult();
                    a.SendAsync(new ArraySegment<byte>(new byte[]{1}),
                        WebSocketMessageType.Binary,true,deadline.Token).GetAwaiter().GetResult();
                    b.SendAsync(new ArraySegment<byte>(new byte[]{2}),
                        WebSocketMessageType.Binary,true,deadline.Token).GetAwaiter().GetResult();
                    byte[] ra=new byte[8],rb=new byte[8];
                    var ar=a.ReceiveAsync(new ArraySegment<byte>(ra),deadline.Token).GetAwaiter().GetResult();
                    var br=b.ReceiveAsync(new ArraySegment<byte>(rb),deadline.Token).GetAwaiter().GetResult();
                    Check(ar.Count==1 && br.Count==1 && ra[0]==11 && rb[0]==12 &&
                        first!=null && second!=null && first.HasSubChannel && second.HasSubChannel &&
                        first.SubChannelId!=second.SubChannelId && first.Generation>0 &&
                        second.Generation>0 && server.State==FTRChannelState.Open,
                        "两条WebSocket连接必须各有独立SubChannelId/Generation与定向回应，禁止广播");
                }
                finally
                {
                    if(server.State!=FTRChannelState.Closed)server.Close();
                }
                Check(server.Diagnostics.BufferedPayloadBytes==0,
                    "正常双WebSocket关闭应归还共享缓冲额度");
            }
        }

        private static void WebSocketFragmentedPeer()
        {
            int port=Port();
            string url="ws://localhost:"+port+"/shared/";
            var cfg=new FTRWebSocketServerChannelConfiguration{
                Id="ws-multi-budget",Prefix="http://localhost:"+port+"/shared/",
                MaxClients=2,MaxMessageBytes=1024,ReceiveBufferBytes=16,
                TotalBufferedBytes=1600
            };
            using(var server=new FTRWebSocketServerChannel(cfg))
            using(var holding=new ClientWebSocket())
            using(var competing=new ClientWebSocket())
            using(var received=new ManualResetEventSlim())
            using(var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                byte[] observed=null;FTRSubChannelAddress source=null;
                server.Received+=packet=>{observed=packet.Data;source=packet.Address;received.Set();};
                try
                {
                    server.Open();
                    holding.ConnectAsync(new Uri(url),deadline.Token).GetAwaiter().GetResult();
                    competing.ConnectAsync(new Uri(url),deadline.Token).GetAwaiter().GetResult();
                    byte[] first=Enumerable.Repeat((byte)77,512).ToArray();
                    holding.SendAsync(new ArraySegment<byte>(first),WebSocketMessageType.Binary,false,
                        deadline.Token).GetAwaiter().GetResult();
                    Check(SpinWait.SpinUntil(()=>server.Diagnostics.BufferedPayloadBytes>=1040,5000),
                        "第一WS客户端未保持跨帧缓冲额度");
                    try
                    {
                        competing.SendAsync(new ArraySegment<byte>(new byte[80]),WebSocketMessageType.Binary,true,
                            deadline.Token).GetAwaiter().GetResult();
                    }
                    catch(WebSocketException){/* 资源拒绝的Peer可能已被关闭 */}
                    Check(SpinWait.SpinUntil(()=>server.Diagnostics.RejectedPayloadBuffers>=1,5000),
                        "多个Peer合计超过额度必须拒绝，而非仅校验各消息大小");
                    holding.SendAsync(new ArraySegment<byte>(new byte[]{88}),WebSocketMessageType.Binary,true,
                        deadline.Token).GetAwaiter().GetResult();
                    Check(received.Wait(5000) && source!=null && source.HasSubChannel &&
                        source.Generation>=1 && observed.Length==513 &&
                        observed.Take(512).All(x=>x==77) && observed[512]==88 &&
                        server.State==FTRChannelState.Open,
                        "超限Peer不得破坏持有客户端的消息边界/身份/服务状态");
                    Check(server.Diagnostics.PeakBufferedPayloadBytes<=1600,
                        "WS所有Peer合计预算水位不能超过限制");
                }
                finally
                {
                    if(server.State!=FTRChannelState.Closed)server.Close();
                }
                Check(server.Diagnostics.BufferedPayloadBytes==0,
                    "WS关闭后在途消息与读缓冲额度应全部释放");
            }
        }
    }
}
