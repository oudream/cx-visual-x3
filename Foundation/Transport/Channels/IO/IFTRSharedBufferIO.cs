using System;
using System.Threading;
namespace CxVisual.Foundation.Transport
{
    /// <summary>有界共享缓冲传输能力；发送返回只说明写入共享槽位，不是远端业务确认。</summary>
    public interface IFTRSharedBufferIO
    {
        int SlotBytes { get; }
        int SlotCount { get; }
        void SendBuffer(byte[] bytes,int offset,int count,CancellationToken cancellation);
    }
}
