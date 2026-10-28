using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using CxVisual.Foundation.Transport;

namespace CxVisual.Tests.ArchitectureSmoke
{
    /// <summary>真实TCP长期驻留实例：多次Open/Close、原订阅继续工作，后台线程和租约有界。</summary>
    internal static class FTRPersistentChannelTests
    {
        private static void Check(bool value,string reason)
        {if(!value)throw new InvalidOperationException("F17.PERSISTENT "+reason);}
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
            var serverConfig=new FTRTcpServerChannelConfiguration {
                Id="f17-resident-server",Host="127.0.0.1",Port=port,
                ReceiveBufferBytes=4096,ReceiveByteBudget=8192,QueueCapacity=2,
                SendTimeoutMs=5000,StopTimeoutMs=5000,CapturePayloadEvidence=false
            };
            var clientConfig=new FTRTcpClientChannelConfiguration {
                Id="f17-resident-client",Host="127.0.0.1",Port=port,
                ReceiveBufferBytes=4096,ReceiveByteBudget=8192,QueueCapacity=2,
                SendTimeoutMs=5000,StopTimeoutMs=5000,ConnectTimeoutMs=5000,
                CapturePayloadEvidence=false
            };
            using(var server=new FTRTcpServerChannel(serverConfig))
            using(var client=new FTRTcpClientChannel(clientConfig))
            using(var arrived=new ManualResetEventSlim(false))
            using(var answered=new ManualResetEventSlim(false))
            {
                int received=0,responded=0;
                FTRReceiveBuffer retained=null;
                FTRSubChannelAddress source=null;
                IDisposable listener=server.SubscribeBuffers(buffer=>
                {
                    var segment=buffer.Segment;
                    if(segment.Count!=2)return;
                    source=buffer.Address;
                    Interlocked.Increment(ref received);
                    if(segment.Array[segment.Offset]==1)
                        retained=buffer.Retain(); // 回调结束后持有池块，Close也不能使其内容失效。
                    arrived.Set();
                });
                IDisposable responseListener=client.SubscribeBuffers(buffer=>
                {
                    var segment=buffer.Segment;
                    if(segment.Count==2 && segment.Array[segment.Offset]==9)
                    {
                        Interlocked.Increment(ref responded);
                        answered.Set();
                    }
                });
                try
                {
                    Guid lastServer=Guid.Empty,lastClient=Guid.Empty;
                    for(int cycle=1;cycle<=4;cycle++)
                    {
                        arrived.Reset();answered.Reset();source=null;
                        server.Open();client.Open();
                        Check(SpinWait.SpinUntil(()=>server.SnapshotSubChannels().Length==1,5000),
                            "TCP复用实例未接受本轮连接");
                        Check(server.SessionId!=lastServer && client.SessionId!=lastClient,
                            "每次Open必须有新的SessionId，不能重用旧半包或命令身份");
                        lastServer=server.SessionId;lastClient=client.SessionId;
                        client.SendBytes(new byte[]{(byte)cycle,42});
                        Check(arrived.Wait(5000) && source!=null && source.HasSubChannel &&
                            source.Generation>0 && received==cycle,
                            "长期保留的订阅未收到当前代次的BufferReceived");
                        server.SendTo(source,new byte[]{9,(byte)cycle});
                        Check(answered.Wait(5000)&&responded==cycle,
                            "原通道双向便利发送在反复打开后不再可用");
                        client.Close();server.Close();
                        Check(server.State==FTRChannelState.Closed &&
                            client.State==FTRChannelState.Closed &&
                            !server.Diagnostics.ReceiveQueue.DeliveryThreadAlive &&
                            !client.Diagnostics.ReceiveQueue.DeliveryThreadAlive,
                            "关闭后后台交付线程没有真正停止");
                        if(cycle==1)
                        {
                            Check(retained!=null && retained.Segment.Count==2 &&
                                retained.Segment.Array[retained.Segment.Offset]==1,
                                "关闭Channel不得提前释放仍由调用方Retain的缓冲");
                            retained.Dispose();retained=null;
                        }
                        Check(server.Diagnostics.ReceiveQueue.BorrowedBuffers==0 &&
                            client.Diagnostics.ReceiveQueue.BorrowedBuffers==0,
                            "全部回调/保留租约释放后接收预算必须归零");
                    }
                    listener.Dispose();listener.Dispose();
                    responseListener.Dispose();responseListener.Dispose();
                    Check(server.State==FTRChannelState.Closed && client.State==FTRChannelState.Closed,
                        "释放便利订阅不能替代Channel的生命周期Owner");
                }
                finally
                {
                    listener.Dispose();responseListener.Dispose();
                    if(retained!=null)retained.Dispose();
                    if(client.State!=FTRChannelState.Closed)client.Close();
                    if(server.State!=FTRChannelState.Closed)server.Close();
                }
                Check(received==4 && responded==4,
                    "长驻实例多代次收发校验未完成");
            }
            Console.WriteLine("F17.PERSISTENT_PASS TCP reused sessions, observable peers, long-lived listeners, pooled retain and clean close");
        }
    }
}
