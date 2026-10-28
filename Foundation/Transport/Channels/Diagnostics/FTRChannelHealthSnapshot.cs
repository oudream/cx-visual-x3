using System;

namespace CxVisual.Foundation.Transport
{
    /// <summary>按需读取的通道运行视图；仅表示本机可观测状态，不证明远端业务成功。</summary>
    public sealed class FTRChannelHealthSnapshot
    {
        internal FTRChannelHealthSnapshot(string channelId,Guid sessionId,FTRChannelState state,
            int activePeers,FTRChannelDiagnosticsSnapshot diagnostics)
        {
            ChannelId=channelId;SessionId=sessionId;State=state;ActivePeers=activePeers;
            Diagnostics=diagnostics;TimestampUtc=DateTime.UtcNow;
        }

        public string ChannelId {get;private set;}
        public Guid SessionId {get;private set;}
        public FTRChannelState State {get;private set;}
        public int ActivePeers {get;private set;}
        public FTRChannelDiagnosticsSnapshot Diagnostics {get;private set;}
        public DateTime TimestampUtc {get;private set;}
    }
}
