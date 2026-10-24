using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Mechanism.Connectivity
{
    // 真实 HTTP 请求-响应 Channel；请求体与响应体保持字节原样，业务解释归 Protocol。
    public sealed class MCCHttpClientChannel : MCCSingleChannelBase
    {
        private readonly MCCHttpClientChannelConfiguration _config;
        private readonly SemaphoreSlim _sendGate = new SemaphoreSlim(1, 1);
        private HttpClient _client;

        public MCCHttpClientChannel(MCCChannelConfiguration configuration)
            : base((configuration ?? throw new ArgumentNullException("configuration")).Id)
        {
            configuration.Validate();
            _config = configuration as MCCHttpClientChannelConfiguration ??
                throw new ArgumentException("需要 MCCHttpClientChannelConfiguration");
        }
        protected override Task OnOpenAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(_config.RequestTimeoutMs) };
            return Task.CompletedTask;
        }
        protected override async Task OnSendAsync(byte[] bytes, MCCSubChannelAddress address, CancellationToken ct)
        {
            if (address.HasSubChannel) throw new ArgumentException("HTTP Client 无子通道");
            await _sendGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                HttpClient client = _client;
                if (client == null) throw new InvalidOperationException("HTTP 通道尚未打开");
                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, _config.Url))
                {
                    request.Content = new ByteArrayContent(bytes);
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
                            Receive(content.ToArray());
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
            return Task.CompletedTask;
        }
    }
}
