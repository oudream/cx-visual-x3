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
            _client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(_config.RequestTimeoutMs) };
            ConfigureSendAdmission(_config);
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
            await _sendGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                HttpClient client = _client;
                if (client == null) throw new InvalidOperationException("HTTP 通道尚未打开");
                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, _config.Url))
                {
                    request.Content = new ByteArrayContent(bytes,offset,count);
                    request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(_config.ContentType);
                    using (HttpResponseMessage response = await client.SendAsync(
                        request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
                    {
                        response.EnsureSuccessStatusCode();
                        long? size = response.Content.Headers.ContentLength;
                        if (size.HasValue && size.Value > _config.MaxResponseBytes)
                            throw new IOException("HTTP 响应超过允许字节预算");
                        using (Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        using (MemoryStream content = new MemoryStream())
                        {
                            byte[] buffer = new byte[8192];
                            int n;
                            while ((n = await stream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
                            {
                                if (content.Length + n > _config.MaxResponseBytes)
                                    throw new IOException("HTTP 响应超过允许字节预算");
                                content.Write(buffer, 0, n);
                            }
                            _inReceiveCallback.Value=true;
                            try{Receive(content.ToArray());}
                            finally{_inReceiveCallback.Value=false;}
                        }
                    }
                }
            }
            finally { _sendGate.Release(); }
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
