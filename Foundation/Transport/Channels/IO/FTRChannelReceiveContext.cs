using System;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Transport
{
public sealed class FTRChannelReceiveContext
    {
        private readonly byte[] _data;
        public FTRChannelReceiveContext(FTRSubChannelAddress address, byte[] data, string correlationId = null)
        {
            if (address == null) throw new ArgumentNullException("address");
            if (data == null) throw new ArgumentNullException("data");
            Address = address; _data = (byte[])data.Clone();
            CorrelationId = correlationId; TimestampUtc = DateTime.UtcNow;
        }
        public FTRSubChannelAddress Address { get; private set; }
        public byte[] Data { get { return (byte[])_data.Clone(); } }
        public string CorrelationId { get; private set; }
        public DateTime TimestampUtc { get; private set; }
    }
}
