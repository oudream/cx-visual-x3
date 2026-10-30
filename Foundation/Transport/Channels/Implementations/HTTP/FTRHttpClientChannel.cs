using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Transport
{
public sealed class FTRHttpClientChannel : FTRSingleChannelBase
    {
        private readonly FTRHttpClientChannelConfiguration _config;
        private readonly SemaphoreSlim _sendGate = new SemaphoreSlim(1, 1);
        private HttpClient _client;
        private readonly AsyncLocal<bool> _inReceiveCallback=new AsyncLocal<bool>();
        protected override bool IsAdditionalTransportThread {get{return _inReceiveCallback.Value;}}

        public FTRHttpClientChannel(FTRChannelConfiguration configuration)
            : base((configuration ?? throw new ArgumentNullException("configuration")).Id)
        {
            configuration.Validate();
            _config = configuration as FTRHttpClientChannelConfiguration ??
                throw new ArgumentException("需要 FTRHttpClientChannelConfiguration");
        }
        protected override Task OnOpenAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            ConfigureSendAdmission(_config);
            ConfigureInFlightBudget(_config.ReceiveByteBudget);
            return Task.CompletedTask;
        }
        // segmentBytes只是底层传输提示，绝不能把一条HTTP消息拆成多次POST。
        // 同步Send等待同一个HTTP请求结束，异步Send使用完全相同的物理调用。
        protected override void OnSendSynchronous(byte[] bytes,int offset,int count,
            FTRSubChannelAddress address,CancellationToken ct,int segmentBytes)
        {
            if(address.HasSubChannel)throw new ArgumentException("HTTP Client 无子通道");
            PostAsync(bytes,offset,count,ct).GetAwaiter().GetResult();
        }
        protected override Task OnSendAsync(byte[] bytes,FTRSubChannelAddress address,CancellationToken ct)
        {
            if(address.HasSubChannel)throw new ArgumentException("HTTP Client 无子通道");
            return PostAsync(bytes,0,bytes.Length,ct);
        }
        private async Task PostAsync(byte[] bytes,int offset,int count,CancellationToken ct)
        {
            // HttpClient.Timeout+ResponseHeadersRead不保证响应体持续读取截止。
            // 一个请求从发送门排队、HTTP响应头、完整Body到交付之前共用同一截止。
            using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                deadline.CancelAfter(_config.RequestTimeoutMs);
                CancellationToken operation=deadline.Token;
                bool acquired=false;
                try
                {
                    await _sendGate.WaitAsync(operation).ConfigureAwait(false);
                    acquired=true;
                    HttpClient client=_client;
                    if(client==null)throw new InvalidOperationException("HTTP 通道尚未打开");
                    using(HttpRequestMessage request=new HttpRequestMessage(HttpMethod.Post,_config.Url))
                    {
                        request.Content=new ByteArrayContent(bytes,offset,count);
                        request.Content.Headers.ContentType=MediaTypeHeaderValue.Parse(_config.ContentType);
                        using(HttpResponseMessage response=await client.SendAsync(
                            request,HttpCompletionOption.ResponseHeadersRead,operation).ConfigureAwait(false))
                        {
                            response.EnsureSuccessStatusCode();
                            long? length=response.Content.Headers.ContentLength;
                            if(length.HasValue&&length.Value>_config.MaxResponseBytes)
                                throw new FTRBudgetExceededException("HTTP响应超过允许字节预算");
                            // scratch、MemoryStream扩容和Received隔离副本都用同一FTR额度。
                            using(FTRBufferedByteLease lease=InFlightBudget.Lease())
                            {
                                int scratch=Math.Min(_config.ReceiveBufferBytes,_config.ReceiveByteBudget);
                                lease.Reserve(scratch);
                                byte[] buffer=new byte[scratch];
                                using(Stream stream=await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                                using(MemoryStream content=new MemoryStream())
                                // 某些.NET48 HTTP响应流不及时响应ReadAsync取消；停止时
                                // 只关闭本次响应流，不CancelPendingRequests影响其它请求。
                                using(operation.Register(()=>
                                {
                                    // Token回调可能由Timer线程触发。单流清理异常绝不可逃逸杀Host；
                                    // 实际请求仍由下层Read/Deadline异常报告给调用者。
                                    try{stream.Dispose();}
                                    catch(Exception){}
                                }))
                                {
                                    int n;
                                    while((n=await stream.ReadAsync(buffer,0,buffer.Length,operation)
                                        .ConfigureAwait(false))>0)
                                    {
                                        long required=content.Length+n;
                                        if(required>_config.MaxResponseBytes)
                                            throw new FTRBudgetExceededException("HTTP响应超过允许字节预算");
                                        if(required>content.Capacity)
                                        {
                                            int previous=content.Capacity;
                                            int next=(int)Math.Min((long)_config.MaxResponseBytes,
                                                Math.Max(required,Math.Max((long)scratch,(long)previous*2)));
                                            lease.Reserve(next);
                                            try{content.Capacity=next;}
                                            catch{lease.Release(next);throw;}
                                            lease.Release(previous);
                                        }
                                        content.Write(buffer,0,n);
                                    }
                                    operation.ThrowIfCancellationRequested();
                                    // 真正Receive观察者不在发送锁内运行，允许安全同步再Send。
                                    _sendGate.Release();
                                    acquired=false;
                                    _inReceiveCallback.Value=true;
                                    try
                                    {
                                        ReceiveBytes(new FTRSubChannelAddress(Id),content.GetBuffer(),
                                            (int)content.Length);
                                    }
                                    finally{_inReceiveCallback.Value=false;}
                                }
                            }
                        }
                    }
                }
                catch(Exception error) when(deadline.IsCancellationRequested)
                {
                    ct.ThrowIfCancellationRequested();
                    throw new TimeoutException("HTTP request including response body deadline",error);
                }
                finally{if(acquired)_sendGate.Release();}
            }
        }
        protected override Task OnCloseAsync(CancellationToken ct)
        {
            HttpClient client = _client;
            _client = null;
            if (client != null) client.Dispose();
            // 不把已接纳但尚未终止的HTTP Send报告为已安全停止。
            WaitForPendingSends(_config.StopTimeoutMs,ct);
            return Task.CompletedTask;
        }
    }
}
