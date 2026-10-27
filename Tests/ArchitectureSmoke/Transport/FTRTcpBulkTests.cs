using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Transport;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class FTRTcpBulkTests
    {
        private static void Check(bool value,string message)
        {if(!value)throw new InvalidOperationException("F17.BULK "+message);}
        private static void Wait(Func<bool> condition,string message)
        {Check(SpinWait.SpinUntil(condition,10000),message);}
        private static int Port()
        {var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
            int port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();return port;}
        private static FTRTcpServerChannelConfiguration ServerConfig(int port,int slots=4)
        {return new FTRTcpServerChannelConfiguration{Id="bulk-server",Host="127.0.0.1",Port=port,
            ReceiveBufferBytes=65536,QueueCapacity=slots,ReceiveByteBudget=65536*slots,
            CapturePayloadEvidence=false,SendTimeoutMs=10000,StopTimeoutMs=2000};}
        private static FTRTcpClientChannelConfiguration ClientConfig(int port)
        {return new FTRTcpClientChannelConfiguration{Id="bulk-client",Host="127.0.0.1",Port=port,
            ReceiveBufferBytes=65536,QueueCapacity=4,ReceiveByteBudget=262144,
            CapturePayloadEvidence=false,SendTimeoutMs=10000,StopTimeoutMs=2000};}
        private static void Close(FTRChannelBase channel)
        {if(channel.State!=FTRChannelState.Closed)channel.Close();}
        public static void Run()
        {
            ConfigRoundtrip();AsyncSendAdmission();DuplexBulk();RetainedBackpressure();MultiPeerThreadBudget();
            StopAndObserverIsolation();SynchronousOrderAndAsyncSnapshot();LifecycleAndCallbackClose();IdleTimeouts();HalfCloseTailDelivery();
            Console.WriteLine("F17.BULK PASS: 40MiB full-duplex integrity, pooled leases, bounded slow consumer, fixed multi-peer threads, stop and config");
        }
        public static void RunVolumeScale()
        {
            DuplexBulk(32768,300000);
            Console.WriteLine("F17.BULK_VOLUME_PASS bytesEachDirection=2147483648 streamedWithoutWholeVolumeAllocation=true");
        }
        private static void ConfigRoundtrip()
        {
            var cfg=ServerConfig(Port(),3);cfg.ReceiveBufferBytes=32768;cfg.ReceiveIdleTimeoutMs=1111;
            cfg.StopTimeoutMs=777;cfg.SendTimeoutMs=999;
            var converter=new FTRChannelConfigurationConverter();
            var read=(FTRTcpServerChannelConfiguration)converter.Read(converter.Write(cfg));
            Check(read.ReceiveBufferBytes==32768&&read.ReceiveByteBudget==196608&&
                !read.CapturePayloadEvidence&&read.SendTimeoutMs==999&&read.StopTimeoutMs==777&&
                read.ReceiveIdleTimeoutMs==1111,"FCM roundtrip lost active receive/send limits");
            var variant=(FTRChannelConfiguration)cfg.SelectVariant("Kind","TcpClient");
            Check(variant.ReceiveBufferBytes==cfg.ReceiveBufferBytes&&variant.StopTimeoutMs==777&&
                variant.ReceiveIdleTimeoutMs==1111&&!variant.CapturePayloadEvidence,"Kind switch lost common limits");
            cfg.ReceiveBufferBytes=0;bool invalid=false;
            try{cfg.Validate();}catch(ArgumentException){invalid=true;}
            Check(invalid,"zero receive block accepted");
        }
        private sealed class AdmissionChannel : FTRSingleChannelBase
        {
            internal readonly TaskCompletionSource<bool> Release=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            internal byte[] Captured;internal int Calls;
            internal AdmissionChannel():base("admission"){ }
            protected override Task OnOpenAsync(CancellationToken ct)
            {StartBufferedReceive(new FTRVirtualChannelConfiguration{SendQueueCapacity=1,SendByteBudget=8});return Task.CompletedTask;}
            protected override Task OnSendAsync(byte[] bytes,FTRSubChannelAddress address,CancellationToken ct)
            {Captured=bytes;Calls++;return Release.Task;}
            protected override Task OnCloseAsync(CancellationToken ct)
            {DrainBufferedReceive(1000,ct);return Task.CompletedTask;}
        }
        private static void AsyncSendAdmission()
        {
            using(var channel=new AdmissionChannel())
            {
                channel.Open();var address=new FTRSubChannelAddress(channel.Id);
                byte[] payload=Enumerable.Repeat((byte)17,8).ToArray();
                Task first=channel.SendAsync(payload,address,CancellationToken.None);Array.Clear(payload,0,8);
                Check(channel.Diagnostics.PendingSendBytes==8&&channel.Diagnostics.PendingSendSnapshots==1&&
                    channel.Captured.All(b=>b==17),"admitted async send did not own a bounded snapshot");
                bool full=false;
                try{channel.SendAsync(new byte[]{2},address,CancellationToken.None).GetAwaiter().GetResult();}
                catch(InvalidOperationException){full=true;}
                Check(full&&channel.Calls==1&&channel.Diagnostics.RejectedSends==1,
                    "full async send queue allocated or invoked unadmitted transport work");
                channel.Release.SetResult(true);first.GetAwaiter().GetResult();
                bool oversized=false;
                try{channel.SendAsync(new byte[9],address,CancellationToken.None).GetAwaiter().GetResult();}
                catch(InvalidOperationException){oversized=true;}
                Check(oversized&&channel.Calls==1&&channel.Diagnostics.PendingSends==0&&
                    channel.Diagnostics.PendingSendBytes==0&&channel.Diagnostics.RejectedSends==2,
                    "oversized async snapshot escaped budget or released credits incorrectly");
            }
        }
        private static void DuplexBulk(int blocks=640,int timeoutMilliseconds=20000)
        {
            const int block=65536;long total=(long)block*blocks;
            int port=Port();var server=new FTRTcpServerChannel(ServerConfig(port));
            var client=new FTRTcpClientChannel(ClientConfig(port));
            long atServer=0,atClient=0;Exception validation=null;
            var serverDone=new ManualResetEventSlim();var clientDone=new ManualResetEventSlim();
            Action<FTRReceiveBuffer,bool> inspect=(buffer,isServer)=>
            {
                try
                {
                    Check(Thread.CurrentThread.IsBackground&&!Thread.CurrentThread.IsThreadPoolThread,
                        "delivery is not a dedicated background thread");
                    ArraySegment<byte> segment=buffer.Segment;
                    long offset=isServer?atServer:atClient;
                    for(int i=0;i<segment.Count;i++)
                        if(segment.Array[segment.Offset+i]!=(byte)((offset+i+(isServer?7:19))%251))
                            throw new InvalidOperationException("byte integrity/order mismatch at "+(offset+i));
                    if(isServer){atServer+=segment.Count;if(atServer>=total)serverDone.Set();}
                    else {atClient+=segment.Count;if(atClient>=total)clientDone.Set();}
                }
                catch(Exception error){Interlocked.CompareExchange(ref validation,error,null);
                    serverDone.Set();clientDone.Set();}
            };
            server.BufferReceived+=buffer=>inspect(buffer,true);
            client.BufferReceived+=buffer=>inspect(buffer,false);
            var process=Process.GetCurrentProcess();TimeSpan cpu=process.TotalProcessorTime;
            long memory=GC.GetTotalMemory(false);int gc0=GC.CollectionCount(0),gc2=GC.CollectionCount(2);
            Stopwatch timer=Stopwatch.StartNew();
            Console.WriteLine("F17.BULK_ENV clrReportedOs="+Environment.OSVersion+" clr="+Environment.Version+
                " x64="+Environment.Is64BitProcess+" cpus="+Environment.ProcessorCount);
            using(var cancellation=new CancellationTokenSource(timeoutMilliseconds))
            try
            {
                server.Open();client.Open();Wait(()=>server.SnapshotSubChannels().Length==1,"bulk peer accept");
                var peer=server.SnapshotSubChannels()[0].Address;
                Action<FTRChannelBase,FTRSubChannelAddress,int> send=(channel,destination,seed)=>
                {
                    byte[] payload=new byte[block+16];
                    for(int part=0;part<blocks;part++)
                    {
                        long start=(long)part*block;
                        for(int i=0;i<block;i++)payload[i+8]=(byte)((start+i+seed)%251);
                        channel.Send(payload,8,block,destination,cancellation.Token);
                    }
                };
                Task up=Task.Run(()=>send(client,new FTRSubChannelAddress(client.Id),7));
                Task down=Task.Run(()=>send(server,peer,19));
                Check(Task.WaitAll(new[]{up,down},timeoutMilliseconds),"duplex send timed out");
                Check(serverDone.Wait(10000)&&clientDone.Wait(10000),"duplex receive timed out");
                if(validation!=null)throw validation;
                Check(atServer==total&&atClient==total,"received byte totals differ");
                foreach(FTRChannelBase channel in new FTRChannelBase[]{server,client})
                {
                    var queue=channel.Diagnostics.ReceiveQueue;
                    Check(queue.ReceiveThreadId>0&&queue.ReceiveThreadId!=queue.DeliveryThreadId,
                        "receive and delivery must use independent dedicated threads");
                    Check(queue.AllocatedBytes<=262144&&queue.PeakBorrowedBuffers<=4,
                        "bulk receive exceeded configured byte/count budget");
                    Check(channel.Diagnostics.SentBytes==total&&channel.Diagnostics.ReceivedBytes==total,
                        "diagnostics must count full data when evidence payload is disabled");
                    Check(channel.ReadFrames().All(frame=>frame.Bytes.Length==0),"bulk evidence copied payload");
                }
                timer.Stop();process.Refresh();
                Console.WriteLine("F17.BULK_METRIC bytesEachDirection="+total+" elapsedMs="+timer.ElapsedMilliseconds+
                    " cpuMs="+(process.TotalProcessorTime-cpu).TotalMilliseconds.ToString("F1")+
                    " managedDelta="+(GC.GetTotalMemory(false)-memory)+" workingSet="+process.WorkingSet64+
                    " gc0="+(GC.CollectionCount(0)-gc0)+" gc2="+(GC.CollectionCount(2)-gc2)+
                    " serverPoolBytes="+server.Diagnostics.ReceiveQueue.AllocatedBytes+
                    " clientPoolBytes="+client.Diagnostics.ReceiveQueue.AllocatedBytes+
                    " serverPeakBuffers="+server.Diagnostics.ReceiveQueue.PeakBorrowedBuffers+
                    " serverBackpressure="+server.Diagnostics.ReceiveQueue.BackpressureWaits+
                    " clientBackpressure="+client.Diagnostics.ReceiveQueue.BackpressureWaits+
                    " verifiedAggregateMiBPerSecond="+((total*2.0/1048576)/timer.Elapsed.TotalSeconds).ToString("F2"));
            }
            finally{cancellation.Cancel();Close(client);Close(server);serverDone.Dispose();clientDone.Dispose();}
            Check(server.Diagnostics.ReceiveQueue.BorrowedBuffers==0&&
                client.Diagnostics.ReceiveQueue.BorrowedBuffers==0,"close leaked receive buffers");
        }
        private static void RetainedBackpressure()
        {
            int port=Port();var server=new FTRTcpServerChannel(ServerConfig(port,2));
            var client=new FTRTcpClientChannel(ClientConfig(port));
            var entered=new ManualResetEventSlim();var release=new ManualResetEventSlim();
            var ack=new ManualResetEventSlim();FTRReceiveBuffer retained=null;long received=0;
            server.BufferReceived+=buffer=>
            {
                Interlocked.Add(ref received,buffer.Count);
                if(retained==null){retained=buffer.Retain();entered.Set();release.Wait();}
            };
            client.BufferReceived+=buffer=>{if(buffer.Segment.Array[0]==91)ack.Set();};
            Task sending=null;
            try
            {
                server.Open();client.Open();Wait(()=>server.SnapshotSubChannels().Length==1,"slow peer accept");
                byte[] payload=Enumerable.Repeat((byte)37,1024*1024).ToArray();
                sending=Task.Run(()=>client.Send(payload));
                Check(entered.Wait(5000),"slow consumer never entered");
                Wait(()=>server.Diagnostics.ReceiveQueue.BackpressureWaits>0,"byte budget never applied backpressure");
                var queue=server.Diagnostics.ReceiveQueue;
                Check(queue.AllocatedBytes<=131072&&queue.BorrowedBuffers==2,
                    "blocked consumer escaped configured receive budget");
                server.Send(new byte[]{91},0,1,server.SnapshotSubChannels()[0].Address,CancellationToken.None);
                Check(ack.Wait(5000),"blocked receive consumer also blocked independent send");
                release.Set();Check(sending.Wait(10000),"slow producer did not recover");
                Wait(()=>Interlocked.Read(ref received)==payload.Length,"slow consumer lost reliable bytes");
                Check(retained.Segment.Array[0]==37,"retained buffer was reused before final release");
                Close(client);Close(server);
                Check(server.Diagnostics.ReceiveQueue.BorrowedBuffers==1,"external retained lease missing from budget");
                Check(retained.Segment.Array[0]==37,"close invalidated externally retained bytes");
                retained.Dispose();retained.Dispose();
                bool disposed=false;try{var ignored=retained.Segment;}catch(ObjectDisposedException){disposed=true;}
                Check(disposed&&server.Diagnostics.ReceiveQueue.BorrowedBuffers==0,"final lease was not released safely");
            }
            finally{release.Set();if(retained!=null)retained.Dispose();Close(client);Close(server);
                if(sending!=null)sending.GetAwaiter().GetResult();entered.Dispose();release.Dispose();ack.Dispose();}
        }
        private static void MultiPeerThreadBudget()
        {
            int port=Port();var server=new FTRTcpServerChannel(ServerConfig(port));
            var clients=new List<TcpClient>();var counts=new Dictionary<string,int>();
            Exception validation=null;
            server.BufferReceived+=buffer=>
            {
                try
                {
                    string key=buffer.Address.ToString();int previous;
                    int current;lock(counts)
                    {counts.TryGetValue(key,out previous);current=counts[key]=previous+buffer.Count;}
                    if(current==4096)
                        server.Send(new byte[]{buffer.Segment.Array[0]},0,1,buffer.Address,CancellationToken.None);
                }
                catch(Exception error){validation=error;}
            };
            try
            {
                server.Open();
                for(int i=0;i<12;i++){var client=new TcpClient();client.Connect(IPAddress.Loopback,port);
                    client.ReceiveTimeout=5000;clients.Add(client);}
                Wait(()=>server.SnapshotSubChannels().Length==12,"12 peers not accepted");
                foreach(var pair in clients.Select((client,index)=>new{client,index}))
                {
                    byte[] payload=Enumerable.Repeat((byte)(pair.index+1),4096).ToArray();
                    pair.client.GetStream().Write(payload,0,payload.Length);
                }
                for(int i=0;i<clients.Count;i++)Check(clients[i].GetStream().ReadByte()==i+1,"multi-peer reply was misrouted");
                if(validation!=null)throw validation;
                lock(counts)Check(counts.Count==12&&counts.Values.All(n=>n==4096),"multi-peer stream integrity");
                var queue=server.Diagnostics.ReceiveQueue;
                Check(queue.ReceiveThreadId>0&&queue.DeliveryThreadId>0&&
                    queue.ReceiveThreadId!=queue.DeliveryThreadId,"server did not share bounded receive threads");
                Console.WriteLine("F17.BULK_MULTIPEER clients=12 receiveThread="+queue.ReceiveThreadId+
                    " deliveryThread="+queue.DeliveryThreadId+" allocatedBytes="+queue.AllocatedBytes);
            }
            finally{foreach(TcpClient client in clients)client.Close();Close(server);}
        }
        private static void StopAndObserverIsolation()
        {
            int port=Port();var cfg=ServerConfig(port,2);cfg.StopTimeoutMs=100;
            var server=new FTRTcpServerChannel(cfg);var client=new FTRTcpClientChannel(ClientConfig(port));
            var entered=new ManualResetEventSlim();var release=new ManualResetEventSlim();int deliveries=0;
            server.BufferReceived+=buffer=>{throw new InvalidOperationException("test subscriber rejection");};
            server.BufferReceived+=buffer=>{Interlocked.Increment(ref deliveries);entered.Set();release.Wait();};
            try
            {
                server.Open();client.Open();client.Send(new byte[]{1,2,3});
                Check(entered.Wait(5000),"one throwing subscriber prevented later subscriber delivery");
                bool timeout=false;try{server.Close();}catch(TimeoutException){timeout=true;}
                Check(timeout&&server.State==FTRChannelState.Faulted&&
                    server.Diagnostics.ReceiveQueue.BorrowedBuffers>0,"unsettled callback falsely reported Closed");
                release.Set();Close(server);Close(client);
                Check(deliveries==1&&server.Diagnostics.Errors>=1&&
                    server.Diagnostics.ReceiveQueue.BorrowedBuffers==0,"retry close leaked buffers or lost subscriber diagnostic");
            }
            finally{release.Set();Close(client);Close(server);entered.Dispose();release.Dispose();}
        }
        private static void SynchronousOrderAndAsyncSnapshot()
        {
            const int size=1024*1024;
            var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
            int port=((IPEndPoint)listener.LocalEndpoint).Port;
            var client=new FTRTcpClientChannel(ClientConfig(port));TcpClient remote=null;
            try
            {
                client.Open();remote=listener.AcceptTcpClient();remote.ReceiveTimeout=10000;
                byte[] received=new byte[size*2+65536];
                Task reader=Task.Run(()=>
                {
                    int at=0;while(at<received.Length)
                    {int n=remote.GetStream().Read(received,at,received.Length-at);
                        if(n==0)throw new InvalidOperationException("ordered sender closed early");at+=n;}
                });
                byte[] first=Enumerable.Repeat((byte)17,size).ToArray();
                byte[] second=Enumerable.Repeat((byte)34,size).ToArray();
                Task a=Task.Run(()=>client.Send(first,segmentBytes:8191));
                Task b=Task.Run(()=>client.Send(second,segmentBytes:4001));
                Check(Task.WaitAll(new[]{a,b},10000),"concurrent synchronous writes did not settle");
                byte[] snapshot=Enumerable.Repeat((byte)51,65536).ToArray();
                Task sending=client.SendAsync(snapshot,new FTRSubChannelAddress(client.Id),CancellationToken.None);
                Array.Clear(snapshot,0,snapshot.Length);sending.GetAwaiter().GetResult();
                Check(reader.Wait(10000),"ordered reader did not settle");
                byte left=received[0],right=received[size];
                Check((left==17&&right==34)||(left==34&&right==17),"synchronous write order was corrupted");
                Check(received.Take(size).All(v=>v==left)&&received.Skip(size).Take(size).All(v=>v==right),
                    "segmented writes from concurrent callers interleaved");
                Check(received.Skip(size*2).All(v=>v==51),"async send did not own its input snapshot");
            }
            finally{if(remote!=null)remote.Close();Close(client);listener.Stop();}
        }
        private static void LifecycleAndCallbackClose()
        {
            int port=Port();var server=new FTRTcpServerChannel(ServerConfig(port));
            var client=new FTRTcpClientChannel(ClientConfig(port));
            var states=new List<FTRChannelState>();var received=new ManualResetEventSlim();bool refused=false;
            client.StateChanged+=state=>{throw new InvalidOperationException("test state observer rejection");};
            client.StateChanged+=state=>{lock(states)states.Add(state);};
            server.BufferReceived+=buffer=>
            {
                try{server.Close();}catch(InvalidOperationException){refused=true;}
                received.Set();
            };
            try
            {
                server.Open();using(var opening=new CancellationTokenSource())
                {client.OpenAsync(opening.Token).GetAwaiter().GetResult();opening.Cancel();}
                client.Send(new byte[]{5});Check(received.Wait(5000)&&refused&&server.State==FTRChannelState.Open,
                    "callback Close self-wait was not rejected before changing lifecycle state");
                Close(client);client.ReconnectAsync(CancellationToken.None).GetAwaiter().GetResult();
                lock(states)Check(client.State==FTRChannelState.Open&&states.Contains(FTRChannelState.Opening)&&
                    states.Contains(FTRChannelState.Closing),"reconnect/state notifications lost");
                client.Dispose();client.Dispose();
                bool disposed=false;try{client.Open();}catch(ObjectDisposedException){disposed=true;}
                Check(disposed&&client.State==FTRChannelState.Closed,"Dispose did not prevent reopen");
                Check(client.Diagnostics.Errors>0,"state observer rejection was not diagnosed");
            }
            finally{Close(client);Close(server);received.Dispose();}
        }
        private static void IdleTimeouts()
        {
            var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
            var cfg=ClientConfig(((IPEndPoint)listener.LocalEndpoint).Port);cfg.ReceiveIdleTimeoutMs=80;
            var client=new FTRTcpClientChannel(cfg);TcpClient remote=null;
            try
            {
                client.Open();remote=listener.AcceptTcpClient();
                Wait(()=>client.State==FTRChannelState.Faulted,"configured client receive idle deadline ignored");
                Check(client.Diagnostics.Errors>0,"idle receiver failure lacks diagnostic");
            }
            finally{if(remote!=null)remote.Close();Close(client);listener.Stop();}
            int port=Port();var serverConfig=ServerConfig(port);serverConfig.ReceiveIdleTimeoutMs=80;
            var server=new FTRTcpServerChannel(serverConfig);client=new FTRTcpClientChannel(ClientConfig(port));
            try
            {
                server.Open();client.Open();
                Wait(()=>server.SnapshotSubChannels().Length==1,"idle test peer was not accepted");
                Wait(()=>server.SnapshotSubChannels().Length==0,"configured server peer idle deadline ignored");
                Check(server.State==FTRChannelState.Open&&server.Diagnostics.Errors>0,
                    "one idle peer faulted shared listener or escaped diagnostics");
            }
            finally{Close(client);Close(server);}
        }
        private static void HalfCloseTailDelivery()
        {
            int port=Port();var server=new FTRTcpServerChannel(ServerConfig(port));
            var entered=new ManualResetEventSlim();var release=new ManualResetEventSlim();
            var removed=new ManualResetEventSlim();var remote=new TcpClient();Exception failure=null;
            server.BufferReceived+=buffer=>
            {
                entered.Set();release.Wait();
                try{server.Send(buffer.Segment.Array,buffer.Segment.Offset,buffer.Count,buffer.Address,CancellationToken.None);}
                catch(Exception error){failure=error;}
            };
            server.PeerRemoved+=address=>removed.Set();
            try
            {
                server.Open();remote.Connect(IPAddress.Loopback,port);remote.ReceiveTimeout=5000;
                var stream=remote.GetStream();stream.Write(new byte[]{11,22,33},0,3);
                Check(entered.Wait(5000),"tail data not accepted before FIN");
                remote.Client.Shutdown(SocketShutdown.Send);
                Wait(()=>server.Diagnostics.ReceiveQueue.PendingNotifications>0,"EOF terminal notification was not queued");
                Check(!removed.IsSet,"Protocol peer was reset before accepted tail data settled");
                release.Set();byte[] reply=new byte[3];int at=0;
                while(at<reply.Length){int n=stream.Read(reply,at,reply.Length-at);
                    Check(n>0,"FIN peer closed before final response");at+=n;}
                if(failure!=null)throw failure;
                Check(reply.SequenceEqual(new byte[]{11,22,33})&&removed.Wait(5000),
                    "half-close response/ordered peer termination failed");
            }
            finally{release.Set();remote.Close();Close(server);entered.Dispose();release.Dispose();removed.Dispose();}
        }
    }
}
