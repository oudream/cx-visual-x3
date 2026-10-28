using System.Threading;
using System.Threading.Tasks;
namespace CxVisual.Foundation.Transport
{
    /// <summary>有消息边界的传输能力。TCP字节流不实现此接口。</summary>
    public interface IFTRMessageIO
    {
        Task SendMessageAsync(byte[] bytes, FTRSubChannelAddress address, CancellationToken cancellation);
    }
}
