using System;
using System.Collections.Generic;
namespace CxVisual.Mechanism.Connectivity
{
    // 由 ReferTo/Channels/CxChannelModels.cs 直接收敛的通道配置，不是第二通道运行层。
    public sealed class MCCChannelProfile
    {
        public MCCChannelProfile(string id, string kind)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(kind)) throw new ArgumentException("通道 Id/Kind 必填");
            Id = id; Kind = kind;
        }
        public string Id { get; private set; }
        public string Kind { get; private set; }
        public string Address { get; set; }
        public int? Port { get; set; }
        public IDictionary<string,string> Settings { get; private set; } = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
    }
}
