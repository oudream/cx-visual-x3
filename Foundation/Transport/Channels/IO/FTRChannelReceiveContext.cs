using System;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Transport
{
public sealed class FTRChannelReceiveContext
    {
        private readonly byte[] _data;
        public FTRChannelReceiveContext(FTRSubChannelAddress address, byte[] data, string correlationId = null)
            :this(address,data,data==null?0:data.Length,correlationId){ }
        internal FTRChannelReceiveContext(FTRSubChannelAddress address,byte[] data,int count,string correlationId)
        {
            if (address == null) throw new ArgumentNullException("address");
            if (data == null) throw new ArgumentNullException("data");
            if(count<0||count>data.Length)throw new ArgumentOutOfRangeException("count");
            Address = address; _data = new byte[count];Buffer.BlockCopy(data,0,_data,0,count);
            CorrelationId = correlationId; TimestampUtc = DateTime.UtcNow;
        }
        public FTRSubChannelAddress Address { get; private set; }
        public byte[] Data { get { return (byte[])_data.Clone(); } }
        public string CorrelationId { get; private set; }
        public DateTime TimestampUtc { get; private set; }
    }
}
