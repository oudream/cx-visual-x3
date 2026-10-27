using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Transport
{
public sealed class FTRUdpMulticastChannel : FTRChannelBase
    {
        private readonly FTRUdpMulticastChannelConfiguration _config;
        private UdpClient _udp;
        private CancellationTokenSource _stop;
        private Task _reader;

        public FTRUdpMulticastChannel(FTRChannelConfiguration config)
            : base((config ?? throw new ArgumentNullException("config")).Id, FTRChannelTopology.SharedBus)
        {
            config.Validate();
            _config = config as FTRUdpMulticastChannelConfiguration ??
                throw new ArgumentException("必须使用 FTRUdpMulticastChannelConfiguration");
        }

        protected override Task OnOpenAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            UdpClient udp = new UdpClient(AddressFamily.InterNetwork);
            try
            {
                udp.ExclusiveAddressUse = false;
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, _config.Port));
                udp.MulticastLoopback = _config.Loopback;
                udp.Ttl = (short)_config.TimeToLive;
                udp.JoinMulticastGroup(IPAddress.Parse(_config.GroupAddress));
            }
            catch { udp.Close(); throw; }
            _udp = udp;
            _stop = new CancellationTokenSource();
            _reader = Task.Run(() => ReceiveLoop(udp, _stop.Token));
            return Task.CompletedTask;
        }

        private async Task ReceiveLoop(UdpClient udp, CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    UdpReceiveResult packet = await udp.ReceiveAsync().ConfigureAwait(false);
                    ReceiveBytes(new FTRSubChannelAddress(Id), packet.Buffer);
                }
            }
            catch (Exception error) { if (!ct.IsCancellationRequested) Fault(error); }
        }

        protected override async Task OnSendAsync(byte[] bytes,
            FTRSubChannelAddress address, CancellationToken ct)
        {
            if (address.HasSubChannel)
                throw new ArgumentException("组播目的地址由组播配置决定，不能使用子通道");
            ct.ThrowIfCancellationRequested();
            UdpClient udp = _udp;
            if (udp == null) throw new IOException("UDP 组播尚未启动");
            int sent = await udp.SendAsync(bytes, bytes.Length,
                new IPEndPoint(IPAddress.Parse(_config.GroupAddress), _config.Port))
                .ConfigureAwait(false);
            if (sent != bytes.Length) throw new IOException("组播报文发送长度不完整");
        }

        protected override async Task OnCloseAsync(CancellationToken ct)
        {
            if (_stop != null) _stop.Cancel();
            UdpClient udp = _udp;
            if (udp != null)
            {
                try { udp.DropMulticastGroup(IPAddress.Parse(_config.GroupAddress)); }
                catch (SocketException) { }
                udp.Close();
            }
            if (_reader != null) await _reader.ConfigureAwait(false);
            if (_stop != null) _stop.Dispose();
            _udp = null; _reader = null; _stop = null;
        }
    }
}
