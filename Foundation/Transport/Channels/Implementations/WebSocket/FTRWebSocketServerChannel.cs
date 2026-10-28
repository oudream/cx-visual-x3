using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Transport
{
    /// <summary>真正RFC6455 Server，保留帧边界、多对端与受控停止；不自建Protocol。</summary>
    public sealed class FTRWebSocketServerChannel : FTRMultiChannelBase, IFTRMessageIO
    {
        private sealed class Peer
        {
            internal WebSocket Socket;
            internal FTRSubChannelAddress Address;
            internal readonly SemaphoreSlim SendGate=new SemaphoreSlim(1,1);
        }
        private readonly AsyncLocal<bool> _inReceiveCallback=new AsyncLocal<bool>();
        protected override bool IsAdditionalTransportThread {get{return _inReceiveCallback.Value;}}
        private readonly FTRWebSocketServerChannelConfiguration _config;
        private readonly object _gate=new object();
        private readonly Dictionary<string,Peer> _clients=new Dictionary<string,Peer>(StringComparer.Ordinal);
        private readonly List<Task> _workers=new List<Task>();
        private HttpListener _listener;
        private CancellationTokenSource _stop;
        private SemaphoreSlim _slots;
        private Task _accept;
        private long _peerId;
        public FTRWebSocketServerChannel(FTRChannelConfiguration config)
            : base((config??throw new ArgumentNullException("config")).Id)
        {
            config.Validate();
            _config=config as FTRWebSocketServerChannelConfiguration ??
                throw new ArgumentException("需要FTRWebSocketServerChannelConfiguration");
        }
        protected override Task OnOpenAsync(CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            _stop=new CancellationTokenSource();
            _slots=new SemaphoreSlim(_config.MaxClients,_config.MaxClients);
            _listener=new HttpListener();
            _listener.Prefixes.Add(_config.Prefix);
            _listener.Start();
            ConfigureSendAdmission(_config);
            ConfigureInFlightBudget(_config.TotalBufferedBytes);
            _accept=Task.Run(()=>AcceptLoop(_stop.Token));
            return Task.CompletedTask;
        }
        private async Task AcceptLoop(CancellationToken cancellation)
        {
            try
            {
                while(!cancellation.IsCancellationRequested)
                {
                    await _slots.WaitAsync(cancellation).ConfigureAwait(false);
                    bool transferred=false;
                    try
                    {
                        HttpListenerContext context=await _listener.GetContextAsync().ConfigureAwait(false);
                        cancellation.ThrowIfCancellationRequested();
                        if(!context.Request.IsWebSocketRequest)
                        {
                            context.Response.StatusCode=400;context.Response.Close();
                            continue;
                        }
                        HttpListenerWebSocketContext accepted=await context.AcceptWebSocketAsync(null).ConfigureAwait(false);
                        string id=Interlocked.Increment(ref _peerId).ToString(System.Globalization.CultureInfo.InvariantCulture);
                        var peer=new Peer {Socket=accepted.WebSocket,
                            Address=RegisterPeer(id,context.Request.RemoteEndPoint==null?"ws":context.Request.RemoteEndPoint.ToString()).Address};
                        lock(_gate)_clients.Add(id,peer);
                        Task worker=Task.Run(()=>ReceiveLoop(peer,cancellation));
                        lock(_gate){_workers.RemoveAll(t=>t.IsCompleted);_workers.Add(worker);}
                        transferred=true;
                    }
                    finally{if(!transferred)_slots.Release();}
                }
            }
            catch(OperationCanceledException) when(cancellation.IsCancellationRequested){}
            catch(HttpListenerException) when(cancellation.IsCancellationRequested){}
            catch(ObjectDisposedException) when(cancellation.IsCancellationRequested){}
            catch(Exception error){if(!cancellation.IsCancellationRequested)Fault(error);}
        }
        private async Task ReceiveLoop(Peer peer,CancellationToken cancellation)
        {
            try
            {
                // 各Peer共享同一个内存额度；慢端只能占用自身已登记的份额。
                using(var lease=InFlightBudget.Lease())
                {
                    int scratchBytes=Math.Min(16384,_config.ReceiveBufferBytes);
                    lease.Reserve(scratchBytes);
                    byte[] buffer=new byte[scratchBytes];
                    using(var incoming=new MemoryStream())
                    {
                        while(!cancellation.IsCancellationRequested)
                        {
                            WebSocketReceiveResult result=await peer.Socket.ReceiveAsync(
                                new ArraySegment<byte>(buffer),cancellation).ConfigureAwait(false);
                            if(result.MessageType==WebSocketMessageType.Close)break;
                            if(result.MessageType!=WebSocketMessageType.Binary)
                                throw new NotSupportedException("WebSocket Server目前只提供二进制消息传输");
                            long required=incoming.Length+result.Count;
                            if(required>_config.MaxMessageBytes)
                                throw new FTRBudgetExceededException("WebSocket消息超出资源预算");
                            if(required>incoming.Capacity)
                            {
                                int oldCapacity=incoming.Capacity;
                                int next=(int)Math.Min((long)_config.MaxMessageBytes,
                                    Math.Max(required,Math.Max(4096L,(long)oldCapacity*2)));
                                // Capacity扩容时GC可能同时保留新旧数组，因此预留临时峰值。
                                lease.Reserve(next);
                                try{incoming.Capacity=next;}
                                catch{lease.Release(next);throw;}
                                lease.Release(oldCapacity);
                            }
                            incoming.Write(buffer,0,result.Count);
                            if(result.EndOfMessage)
                            {
                                // 不再ToArray整消息；ReceiveFrom切片会创建有明确额度的独立上下文。
                                _inReceiveCallback.Value=true;
                                try{ReceiveFrom(peer.Address,incoming.GetBuffer(),(int)incoming.Length);}
                                finally{_inReceiveCallback.Value=false;}
                                int previous=incoming.Capacity;
                                incoming.SetLength(0);
                                incoming.Capacity=0; // 消息结束立即还给其它Peer，不长期占住大Buffer。
                                lease.Release(previous);
                            }
                        }
                    }
                }
            }
            catch(Exception error){if(!cancellation.IsCancellationRequested)RecordReceiveError(error,FTRFaultScope.SubChannel);}
            finally
            {
                RemovePeer(peer.Address);
                try{peer.Socket.Dispose();}catch{}
                peer.SendGate.Dispose();
                _slots.Release();
            }
        }
        public Task SendMessageAsync(byte[] bytes,FTRSubChannelAddress address,CancellationToken cancellation)
        {return SendAsync(bytes,address,cancellation);}
        // 同Peer发送门贯穿整条逻辑Message；多分片不允许与并发Message交错。
        protected override void OnSendSynchronous(byte[] bytes,int offset,int count,
            FTRSubChannelAddress address,CancellationToken cancellation,int segmentBytes)
        {
            WriteMessageAsync(bytes,offset,count,address,segmentBytes,cancellation).GetAwaiter().GetResult();
        }
        protected override Task SendToPeerAsync(byte[] bytes,FTRSubChannelAddress address,CancellationToken cancellation)
        {return WriteMessageAsync(bytes,0,bytes.Length,address,0,cancellation);}
        private async Task WriteMessageAsync(byte[] bytes,int offset,int count,
            FTRSubChannelAddress address,int segmentBytes,CancellationToken cancellation)
        {
            if(address==null || FindSubChannel(address)==null)
                throw new InvalidOperationException("已失效的WebSocket对端地址");
            Peer peer;
            lock(_gate)if(!_clients.TryGetValue(address.SubChannelId,out peer) || !peer.Address.Equals(address))
                throw new InvalidOperationException("WebSocket会话代次不匹配");
            await peer.SendGate.WaitAsync(cancellation).ConfigureAwait(false);
            try
            {
                if(peer.Socket.State!=WebSocketState.Open)throw new IOException("WebSocket对端已关闭");
                int chunk=segmentBytes>0?segmentBytes:16384;
                for(int sent=0;sent<count;)
                {
                    cancellation.ThrowIfCancellationRequested();
                    int n=Math.Min(chunk,count-sent);
                    await peer.Socket.SendAsync(new ArraySegment<byte>(bytes,offset+sent,n),
                        WebSocketMessageType.Binary,sent+n==count,cancellation).ConfigureAwait(false);
                    sent+=n;
                }
            }
            finally{peer.SendGate.Release();}
        }
        public override bool RemovePeer(FTRSubChannelAddress address)
        {
            if(address==null)return false;
            Peer peer=null;
            lock(_gate)
            {
                if(!RemovePeerDirectory(address))return false;
                _clients.TryGetValue(address.SubChannelId,out peer);
                if(peer!=null)_clients.Remove(address.SubChannelId);
            }
            NotifyPeerRemoved(address);
            return true;
        }
        protected override async Task OnCloseAsync(CancellationToken cancellation)
        {
            var stop=_stop;if(stop!=null)stop.Cancel();
            var listener=_listener;if(listener!=null){listener.Stop();listener.Close();}
            Task accept=_accept;Task[] workers;Peer[] peers;
            lock(_gate){workers=_workers.ToArray();peers=new List<Peer>(_clients.Values).ToArray();}
            foreach(Peer peer in peers)try{peer.Socket.Abort();}catch{}
            var tasks=new List<Task>();if(accept!=null)tasks.Add(accept);tasks.AddRange(workers);
            if(tasks.Count>0)
            {
                Task all=Task.WhenAll(tasks);
                if(await Task.WhenAny(all,Task.Delay(_config.StopTimeoutMs,cancellation)).ConfigureAwait(false)!=all)
                    throw new TimeoutException("WebSocket会话在停止预算内未结算");
                await all.ConfigureAwait(false);
            }
            foreach(var peer in SnapshotSubChannels())RemovePeer(peer.Address);
            lock(_gate){_clients.Clear();_workers.Clear();}
            _accept=null;_listener=null;_stop=null;_slots=null;
            if(stop!=null)stop.Dispose();
        }
    }
}
