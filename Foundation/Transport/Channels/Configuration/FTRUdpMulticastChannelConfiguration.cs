using System;
using System.ComponentModel.DataAnnotations;
using System.Net;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Foundation.Transport
{
public sealed class FTRUdpMulticastChannelConfiguration : FTRChannelConfiguration
    {
        public FTRUdpMulticastChannelConfiguration() : base(FTRChannelKind.UdpMulticast) { }

        [FCMConfigItem("IPv4 组播组地址", Order = 10, Category = "组播")]
        [Required]
        public string GroupAddress { get; set; } = "239.1.1.1";

        [FCMConfigItem("组播端口", Order = 11, Category = "组播")]
        [Range(1, 65535)]
        public int Port { get; set; } = 30001;

        [FCMConfigItem("组播 TTL", Order = 12, Category = "组播")]
        [Range(1, 255)]
        public int TimeToLive { get; set; } = 1;

        [FCMConfigItem("允许本机接收自身组播", Order = 13, Category = "组播")]
        public bool Loopback { get; set; } = false;

        public override void Validate()
        {
            base.Validate();
            IPAddress group;
            if (!IPAddress.TryParse(GroupAddress, out group) ||
                group.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork ||
                group.GetAddressBytes()[0] < 224 || group.GetAddressBytes()[0] > 239 ||
                Port < 1 || Port > 65535 || TimeToLive < 1 || TimeToLive > 255)
                throw new ArgumentException("UDP 组播IPv4组、端口或TTL不合法");
        }
    }
}
