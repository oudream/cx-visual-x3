using System.Threading;
using System.Threading.Tasks;
using CxVisual.Mechanism.Core.Modules;
using CxVisual.Mechanism.Data.RuntimeData;
namespace CxVisual.Professional.Vision.Modules
{
    // 可选复杂计算接口；普通函数不强制实现。执行依托已有 MOC/MRK/MDS。
    public interface IPVCComputeModule : IMFMModule
    {
        Task ExecuteAsync(IMDSDataContext data, CancellationToken cancellationToken);
    }
}
