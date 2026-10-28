using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Transport
{
    /// <summary>本机HTTP传输监听。HTTP方法、路径与载荷提供给上层，不做REST/业务路由。</summary>
    public sealed class FTRHttpServerChannel : FTRMultiChannelBase, IFTRRequestResponseIO
    {
        private sealed class Reply
        {
            internal int StatusCode;
            internal byte[] Payload;
        }
        private sealed class Pending
        {
            internal FTRSubChannelAddress Address;
            internal readonly TaskCompletionSource<Reply> Response =
                new TaskCompletionSource<Reply>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        private readonly FTRHttpServerChannelConfiguration _config;
        private readonly object _gate = new object();
        private readonly Dictionary<string,Pending> _pending = new Dictionary<string,Pending>(StringComparer.Ordinal);
        private readonly List<Task> _workers = new List<Task>();
        private HttpListener _listener;
        private CancellationTokenSource _stop;
        private SemaphoreSlim _slots;
        private Task _accept;
        private long _next;
        public event Action<FTRHttpRequestInfo> RequestReceived;
        public FTRHttpServerChannel(FTRChannelConfiguration configuration)
            : base((configuration ?? throw new ArgumentNullException("configuration")).Id)
        {
            configuration.Validate();
            _config = configuration as FTRHttpServerChannelConfiguration ??
                throw new ArgumentException("需要FTRHttpServerChannelConfiguration");
        }
        protected override Task OnOpenAsync(CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            _stop = new CancellationTokenSource();
            _slots = new SemaphoreSlim(_config.MaxClients,_config.MaxClients);
            _listener = new HttpListener();
            _listener.Prefixes.Add(_config.Prefix);
            _listener.Start();
            _accept = Task.Run(() => AcceptLoop(_stop.Token));
            return Task.CompletedTask;
        }
        private async Task AcceptLoop(CancellationToken cancellation)
        {
            try
            {
                while (!cancellation.IsCancellationRequested)
                {
                    await _slots.WaitAsync(cancellation).ConfigureAwait(false);
                    bool transferred=false;
                    try
                    {
                        HttpListenerContext context=await _listener.GetContextAsync().ConfigureAwait(false);
                        cancellation.ThrowIfCancellationRequested();
                        string id=Interlocked.Increment(ref _next).ToString(System.Globalization.CultureInfo.InvariantCulture);
                        var peer = new Pending { Address=RegisterPeer(id,context.Request.RemoteEndPoint==null?"http":context.Request.RemoteEndPoint.ToString()).Address };
                        lock(_gate) _pending.Add(id,peer);
                        Task worker=Task.Run(() => Serve(context,peer,cancellation));
                        lock(_gate){_workers.RemoveAll(task=>task.IsCompleted);_workers.Add(worker);}
                        transferred=true;
                    }
                    finally {if(!transferred)_slots.Release();}
                }
            }
            catch(OperationCanceledException) when(cancellation.IsCancellationRequested) { }
            catch(HttpListenerException) when(cancellation.IsCancellationRequested) { }
            catch(ObjectDisposedException) when(cancellation.IsCancellationRequested) { }
            catch(Exception error){if(!cancellation.IsCancellationRequested)Fault(error);}
        }
        private async Task Serve(HttpListenerContext context,Pending pending,CancellationToken cancellation)
        {
            try
            {
                if(context.Request.ContentLength64>_config.MaxRequestBytes)
                    throw new IOException("HTTP请求超过接收预算");
                byte[] payload;
                using(var memory=new MemoryStream())
                {
                    byte[] chunk=new byte[Math.Min(16384,_config.ReceiveBufferBytes)];
                    int read;
                    while((read=await context.Request.InputStream.ReadAsync(chunk,0,chunk.Length,cancellation).ConfigureAwait(false))>0)
                    {
                        if(memory.Length+read>_config.MaxRequestBytes)throw new IOException("HTTP请求超过接收预算");
                        memory.Write(chunk,0,read);
                    }
                    payload=memory.ToArray();
                }
                Action<FTRHttpRequestInfo> observer=RequestReceived;
                if(observer!=null)
                    foreach(Action<FTRHttpRequestInfo> callback in observer.GetInvocationList())
                        try{callback(new FTRHttpRequestInfo(pending.Address,context.Request.HttpMethod,
                            context.Request.Url.AbsolutePath,context.Request.ContentType,payload.Length));}
                        catch(Exception error){RecordReceiveError(error);}
                ReceiveFrom(pending.Address,payload);
                Task done=await Task.WhenAny(pending.Response.Task,Task.Delay(_config.ResponseTimeoutMs,cancellation)).ConfigureAwait(false);
                if(done!=pending.Response.Task) throw new TimeoutException("HTTP业务响应未在期限内提交");
                Reply response=await pending.Response.Task.ConfigureAwait(false);
                context.Response.StatusCode=response.StatusCode;
                context.Response.ContentType="application/octet-stream";
                context.Response.ContentLength64=response.Payload.Length;
                await context.Response.OutputStream.WriteAsync(response.Payload,0,response.Payload.Length,cancellation).ConfigureAwait(false);
            }
            catch(Exception error)
            {
                if(!cancellation.IsCancellationRequested)
                {
                    RecordReceiveError(error);
                    try{context.Response.StatusCode=error is TimeoutException?504:error is IOException?413:500;}catch{}
                }
            }
            finally
            {
                try{context.Response.Close();}catch{}
                lock(_gate)_pending.Remove(pending.Address.SubChannelId);
                RemovePeer(pending.Address);
                _slots.Release();
            }
        }
        public Task RespondAsync(FTRSubChannelAddress address,int statusCode,byte[] payload,CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if(address==null || address.ChannelId!=Id || !address.HasSubChannel)
                throw new ArgumentException("响应必须指定当前HTTP请求SubChannel");
            if(statusCode<200 || statusCode>599)throw new ArgumentOutOfRangeException("statusCode");
            if(payload==null || payload.Length>_config.MaxResponseBytes)
                throw new ArgumentException("响应超过预算或为空");
            if(FindSubChannel(address)==null)throw new InvalidOperationException("过期HTTP请求");
            Pending pending;
            lock(_gate)if(!_pending.TryGetValue(address.SubChannelId,out pending))
                throw new InvalidOperationException("HTTP请求已结束");
            if(!pending.Address.Equals(address))
                throw new InvalidOperationException("HTTP请求代次不匹配");
            if(!pending.Response.TrySetResult(new Reply { StatusCode=statusCode, Payload=(byte[])payload.Clone() }))
                throw new InvalidOperationException("HTTP请求已经响应");
            return Task.CompletedTask;
        }
        protected override Task SendToPeerAsync(byte[] bytes,FTRSubChannelAddress address,CancellationToken cancellation)
        {return RespondAsync(address,200,bytes,cancellation);}
        protected override async Task OnCloseAsync(CancellationToken cancellation)
        {
            var stop=_stop; if(stop!=null)stop.Cancel();
            var listener=_listener;if(listener!=null){listener.Stop();listener.Close();}
            Task accept=_accept; Task[] workers;
            lock(_gate)
            {
                foreach(Pending pending in _pending.Values)pending.Response.TrySetCanceled();
                workers=_workers.ToArray();
            }
            var tasks=new List<Task>();if(accept!=null)tasks.Add(accept);tasks.AddRange(workers);
            if(tasks.Count>0)
            {
                Task all=Task.WhenAll(tasks);
                Task completed=await Task.WhenAny(all,Task.Delay(_config.StopTimeoutMs,cancellation)).ConfigureAwait(false);
                if(completed!=all)throw new TimeoutException("HTTP请求/监听未在停止预算内结算");
                await all.ConfigureAwait(false);
            }
            foreach(var peer in SnapshotSubChannels())RemovePeer(peer.Address);
            lock(_gate){_pending.Clear();_workers.Clear();}
            _accept=null;_listener=null;_stop=null;_slots=null;
            if(stop!=null)stop.Dispose();
        }
    }
}
