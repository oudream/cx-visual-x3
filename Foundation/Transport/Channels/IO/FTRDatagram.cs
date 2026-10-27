using System;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Transport
{
public sealed class FTRDatagram
    {
        private readonly byte[] _data;
        public FTRDatagram(string remoteEndpoint, byte[] data)
        {
            if (string.IsNullOrWhiteSpace(remoteEndpoint)) throw new ArgumentException("数据报目标不能为空");
            if (data == null) throw new ArgumentNullException("data");
            RemoteEndpoint = remoteEndpoint.Trim(); _data = (byte[])data.Clone();
        }
        public string RemoteEndpoint { get; private set; }
        public byte[] Data { get { return (byte[])_data.Clone(); } }
    }
}
