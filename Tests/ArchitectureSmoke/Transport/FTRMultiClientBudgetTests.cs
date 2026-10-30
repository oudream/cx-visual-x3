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
            HttpClientBoundedResponseAndReentrantSend();
            HttpClientBodyDeadline();
            HttpClientMultiMegabyteResponse();
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

        /// <summary>
        /// HTTP Client单次响应的内存峰值和重入发送：真正HttpListener回环。
        /// 不创建第二套HTTP传输，不将协议收到的200当成远端业务ACK。
        /// </summary>
        private static void HttpClientBoundedResponseAndReentrantSend()
        {
            int port=Port();
            string url="http://localhost:"+port+"/budget-client/";
            var serverConfig=new FTRHttpServerChannelConfiguration {
                Id="f17-http-client-budget-server",Prefix=url,MaxClients=2,
                MaxRequestBytes=16,MaxResponseBytes=256,TotalBufferedBytes=4096,
                ReceiveBufferBytes=16,ResponseTimeoutMs=5000
            };
            var clientConfig=new FTRHttpClientChannelConfiguration {
                Id="f17-http-client-budget",Url=url,RequestTimeoutMs=5000,
                MaxResponseBytes=256,ReceiveBufferBytes=16,ReceiveByteBudget=512,
                CapturePayloadEvidence=false,StopTimeoutMs=5000
            };
            using(var server=new FTRHttpServerChannel(serverConfig))
            using(var client=new FTRHttpClientChannel(clientConfig))
            {
                int requests=0,responses=0;
                server.Received+=packet=>
                {
                    byte[] payload=packet.Data;
                    Interlocked.Increment(ref requests);
                    byte[] reply=payload[0]==1?new byte[]{11}:
                        payload[0]==2?new byte[]{22}:new byte[256];
                    server.ReplyAsync(packet,reply,CancellationToken.None)
                        .GetAwaiter().GetResult();
                };
                client.Received+=packet=>
                {
                    byte[] payload=packet.Data;
                    Interlocked.Increment(ref responses);
                    if(payload.Length==1&&payload[0]==11)
                    {
                        // 旧客户端持有_sendGate调用接收订阅，回调内同步Send会自锁。
                        client.Send(new byte[]{2});
                    }
                };
                try
                {
                    server.Open();client.Open();
                    // 用真实进程内HTTP OS回环，若死锁则整个CI有外层超时门禁。
                    Task echo=Task.Run(()=>client.Send(new byte[]{1}));
                    Check(echo.Wait(6000),
                        "HTTP接收回调内继续同步Send发生自锁");
                    echo.GetAwaiter().GetResult();
                    Check(requests==2&&responses==2,
                        "HTTP第二次请求未通过同一Send核心且没有正确交付响应");
                    var before=client.Diagnostics;
                    Check(before.BufferedPayloadBytes==0&&
                        before.PayloadBufferByteBudget==512&&
                        before.ReceivedBytes==2,
                        "小响应预算/额度释放/真实Rx计数不正确");
                    Check(client.ReadFrames().Where(frame=>
                        frame.Direction==FTRFrameDirection.Rx||
                        frame.Direction==FTRFrameDirection.Tx).All(frame=>
                            frame.Bytes.Length==0)&&before.SentBytes==2,
                        "HTTP Client禁用诊断载荷后仍复制大消息或丢失真实字节计数");
                    bool over=false;
                    try{client.Send(new byte[]{3});}
                    catch(InvalidOperationException){over=true;}
                    Check(over&&requests==3&&responses==2&&
                        client.Diagnostics.BufferedPayloadBytes==0&&
                        client.Diagnostics.PeakBufferedPayloadBytes<=512&&
                        client.Diagnostics.RejectedPayloadBuffers>0,
                        "大响应不得在未取得内存/快照额度时隐式分配或继续交付");
                    // 拒绝大消息不污染父连接，同一Channel仍可继续正常请求。
                    client.Send(new byte[]{2});
                    Check(requests==4&&responses==3&&
                        client.State==FTRChannelState.Open,
                        "一次响应超预算不应杀死后续健康HTTP会话");
                }
                finally
                {
                    if(client.State!=FTRChannelState.Closed)client.Close();
                    if(server.State!=FTRChannelState.Closed)server.Close();
                }
            }
        }

        private static void HttpClientBodyDeadline()
        {
            int port=Port();
            string url="http://localhost:"+port+"/slow-body/";
            using(var listener=new HttpListener())
            {
                listener.Prefixes.Add(url);
                listener.Start();
                var headersSent=new ManualResetEventSlim(false);
                Task server=Task.Run(()=>
                {
                    HttpListenerContext request=listener.GetContext();
                    request.Response.StatusCode=200;
                    request.Response.SendChunked=true;
                    try
                    {
                        // 立刻发送HTTP 200和第一小块后保持连接，不再发送响应尾。
                        request.Response.OutputStream.Write(new byte[]{42},0,1);
                        request.Response.OutputStream.Flush();
                        headersSent.Set();
                        Thread.Sleep(2200);
                    }
                    catch(HttpListenerException){}
                    catch(System.IO.IOException){}
                    finally
                    {
                        try{request.Response.Close();}catch(ObjectDisposedException){}
                        catch(HttpListenerException){}
                    }
                });
                var config=new FTRHttpClientChannelConfiguration {
                    Id="f17-http-slow-response",Url=url,RequestTimeoutMs=850,
                    MaxResponseBytes=1024,ReceiveByteBudget=4096,ReceiveBufferBytes=16,
                    StopTimeoutMs=3000
                };
                using(var client=new FTRHttpClientChannel(config))
                {
                    int packets=0;
                    client.Received+=context=>Interlocked.Increment(ref packets);
                    client.Open();
                    bool timedOut=false;
                    var stopwatch=System.Diagnostics.Stopwatch.StartNew();
                    try{client.Send(new byte[]{7});}
                    catch(TimeoutException){timedOut=true;}
                    stopwatch.Stop();
                    Check(headersSent.IsSet&&timedOut&&stopwatch.ElapsedMilliseconds<2900&&
                        client.Diagnostics.BufferedPayloadBytes==0&&packets==0,
                        "收到HTTP 200响应头后慢Body必须由同一RequestTimeout及时停止、清零额度，不能交付半消息");
                    client.Close();
                }
                Check(server.Wait(5000),"HTTP慢Body服务端未正常结算");
                headersSent.Dispose();
            }
        }

        private static void HttpClientMultiMegabyteResponse()
        {
            const int bytes=4*1024*1024;
            int port=Port();
            string url="http://localhost:"+port+"/volume-response/";
            var serverConfig=new FTRHttpServerChannelConfiguration {
                Id="f17-large-http-server",Prefix=url,
                MaxClients=2,MaxRequestBytes=64,MaxResponseBytes=bytes,
                TotalBufferedBytes=16*1024*1024,ResponseTimeoutMs=15000,
                ReceiveBufferBytes=1024
            };
            var clientConfig=new FTRHttpClientChannelConfiguration {
                Id="f17-large-http-client",Url=url,RequestTimeoutMs=15000,
                MaxResponseBytes=bytes,CapturePayloadEvidence=false
            };
            Check(clientConfig.ReceiveByteBudget>=10*1024*1024,
                "HTTP Client默认4MiB响应预算不足以覆盖缓冲与Legacy快照");
            var codec=new FTRChannelConfigurationConverter();
            var saved=(FTRHttpClientChannelConfiguration)codec.Read(codec.Write(clientConfig));
            Check(saved.ReceiveByteBudget==clientConfig.ReceiveByteBudget,
                "HTTP Client独立预算没有走既有FCM序列化");
            byte[] expected=new byte[bytes];
            for(int i=0;i<expected.Length;i++)
                expected[i]=(byte)((i*31+(i>>13))&255);
            using(var server=new FTRHttpServerChannel(serverConfig))
            using(var client=new FTRHttpClientChannel(clientConfig))
            {
                int received=0;
                server.Received+=packet=>
                {
                    Check(packet.Data.Length==1,"HTTP批量响应请求身份错误");
                    server.ReplyAsync(packet,expected,CancellationToken.None)
                        .GetAwaiter().GetResult();
                };
                client.Received+=packet=>
                {
                    byte[] body=packet.Data;
                    Check(body.Length==bytes&&body.SequenceEqual(expected),
                        "HTTP 4MiB响应每字节完整性/顺序不正确");
                    Interlocked.Increment(ref received);
                };
                try
                {
                    server.Open();client.Open();
                    client.Send(new byte[]{9});
                    FTRChannelDiagnosticsSnapshot evidence=client.Diagnostics;
                    Check(received==1&&evidence.ReceivedBytes==bytes&&
                        evidence.BufferedPayloadBytes==0&&
                        evidence.PeakBufferedPayloadBytes<=clientConfig.ReceiveByteBudget&&
                        evidence.PayloadBufferByteBudget==clientConfig.ReceiveByteBudget,
                        "真实HTTP 4MiB响应没有正确使用FCM总额度或未回收");
                    Console.WriteLine("F17.HTTP_LARGE_PASS bytes="+bytes+
                        " peakBudgetBytes="+evidence.PeakBufferedPayloadBytes+
                        " budget="+evidence.PayloadBufferByteBudget);
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
