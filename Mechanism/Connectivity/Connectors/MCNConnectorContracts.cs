using CxVisual.Foundation.Transport;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Mechanism.Core.Modules;

namespace CxVisual.Mechanism.Connectivity
{
    // 公共模块显式一对一，运行期 SubChannel 属于 Channel，点表属于 Protocol。
    public interface IMCNConnectorModule : IMFMModule
    {
        IFTRChannel Channel { get; }
        IMPAProtocolDefinition Protocol { get; }
        IReadOnlyList<MPAPointDefinition> Points { get; }
        Task<MPAProtocolOperationResult> RequestAsync(
            MPAProtocolOperationRequest request, FTRSubChannelAddress destination, CancellationToken ct);
        Task<object> ReadDevicePointAsync(
            string pointName, IMPADataTargetAccess access, FTRSubChannelAddress destination, CancellationToken ct);
        Task<MPAProtocolOperationResult> WriteDevicePointAsync(
            string pointName, IMPADataTargetAccess access, FTRSubChannelAddress destination, CancellationToken ct);
    }
}
