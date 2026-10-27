using System;
using System.ComponentModel.DataAnnotations;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Foundation.Transport
{
public sealed class FTRChannelTypeDescriptor
    {
        public FTRChannelTypeDescriptor(string typeId, FTRChannelTopology topology, FTRIOCapabilities io)
        {
            if (string.IsNullOrWhiteSpace(typeId) || !Enum.IsDefined(typeof(FTRChannelTopology), topology) ||
                io == FTRIOCapabilities.None ||
                (io & ~(FTRIOCapabilities.ByteStream | FTRIOCapabilities.Datagram | FTRIOCapabilities.Message |
                        FTRIOCapabilities.RequestResponse | FTRIOCapabilities.SharedBuffer | FTRIOCapabilities.Listener)) != 0)
                throw new ArgumentException("通道类型描述无效");
            TypeId = typeId.Trim(); Topology = topology; IO = io;
        }
        public string TypeId { get; private set; }
        public FTRChannelTopology Topology { get; private set; }
        public FTRIOCapabilities IO { get; private set; }
    }
}
