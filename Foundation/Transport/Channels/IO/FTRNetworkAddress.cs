using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Transport
{
internal static class FTRNetworkAddress
    {
        internal static IPAddress Resolve(string host)
        {
            IPAddress ip;
            if (IPAddress.TryParse(host, out ip)) return ip;
            IPAddress[] addresses = Dns.GetHostAddresses(host);
            ip = addresses.FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork);
            if (ip == null) throw new ArgumentException("无可用 IPv4 地址：" + host);
            return ip;
        }
    }
}
