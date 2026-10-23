using System.Collections.Generic;
using CxVisual.Mechanism.Core.Modules;

namespace CxVisual.Mechanism.Connectivity
{
    // 公共模块显式一对一，运行期 SubChannel 属于 Channel，点表属于 Protocol。
    public interface IMCNConnectorModule : IMFMModule
    {
        IMCCChannel Channel { get; }
        IMPAProtocolDefinition Protocol { get; }
        IReadOnlyList<MPAPointDefinition> Points { get; }
    }
}
