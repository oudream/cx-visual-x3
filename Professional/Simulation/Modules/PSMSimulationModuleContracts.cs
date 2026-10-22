using System;
using CxVisual.Mechanism.Core.Modules;
namespace CxVisual.Professional.Simulation.Modules
{
    // 有独立仿真时间和状态；不自行创建另一套 MRK 调度器。
    public interface IPSMSimulationModule : IMFMModule
    {
        TimeSpan SimulatedTime { get; }
        void Initialize();
        void Advance(TimeSpan simulationDelta);
        void Reset();
    }
}
