using System.Threading;
using System.Threading.Tasks;
using CxVisual.Mechanism.Core.Modules;
namespace CxVisual.Mechanism.Lifecycle.Modules
{
    // 可选长期服务入口；不创建第二套 Host/Scheduler。
    public interface IMLCServiceModule : IMFMModule
    {
        bool IsReady { get; }
        Task StartAsync(CancellationToken cancellationToken);
        Task StopAsync(CancellationToken cancellationToken);
    }
}
