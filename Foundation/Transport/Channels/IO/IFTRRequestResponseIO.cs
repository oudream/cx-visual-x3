using System;
using System.Threading;
using System.Threading.Tasks;
namespace CxVisual.Foundation.Transport
{
    /// <summary>传输级响应提交。完成Task仅表示应答已接纳；物理交付与远端处理分离。</summary>
    public interface IFTRRequestResponseIO
    {
        Task RespondAsync(FTRSubChannelAddress address, int statusCode, byte[] payload, CancellationToken cancellation);
    }
}
