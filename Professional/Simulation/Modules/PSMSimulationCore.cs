using System;
using CxVisual.Mechanism.Core.Modules;

namespace CxVisual.Professional.Simulation.Modules
{
    // Simulation 的模型状态与模拟时间归专业 Owner；外部 MRK/F19 决定推进何时发生。
    public enum PSMSimulationRunMode { Step, Realtime, Accelerated, Replay }

    public sealed class PSMSimulationDefinition
    {
        public PSMSimulationDefinition(string modelId, TimeSpan fixedStep, int seed = 1,
            PSMSimulationRunMode mode = PSMSimulationRunMode.Step)
        {
            if (string.IsNullOrWhiteSpace(modelId)) throw new ArgumentException("仿真模型 Id 必填");
            if (fixedStep <= TimeSpan.Zero || !Enum.IsDefined(typeof(PSMSimulationRunMode), mode))
                throw new ArgumentException("仿真步长/运行模式无效");
            ModelId = modelId.Trim(); FixedStep = fixedStep; Seed = seed; Mode = mode;
        }
        public string ModelId { get; private set; }
        public TimeSpan FixedStep { get; private set; }
        public int Seed { get; private set; }
        public PSMSimulationRunMode Mode { get; private set; }
    }

    public interface IPSMSimulationModel
    {
        string ModelId { get; }
        void Initialize(int seed);
        void Advance(TimeSpan delta);
        void Reset();
    }

    public sealed class PSMSimulationClock
    {
        private readonly object _gate = new object();
        private TimeSpan _elapsed;
        public TimeSpan Elapsed { get { lock (_gate) return _elapsed; } }
        public void Advance(TimeSpan delta)
        {
            if (delta <= TimeSpan.Zero) throw new ArgumentOutOfRangeException("delta");
            lock (_gate)
            {
                if (_elapsed.Ticks > TimeSpan.MaxValue.Ticks - delta.Ticks)
                    throw new OverflowException("模拟时间超出范围");
                _elapsed += delta;
            }
        }
        public void Reset() { lock (_gate) _elapsed = TimeSpan.Zero; }
    }

    // 本类是专业仿真组合基类，不创建线程或时钟调度器，不代替 MRK Runtime。
    public sealed class PSMSimulationModule : IPSMSimulationModule
    {
        private readonly object _gate = new object();
        private readonly IPSMSimulationModel _model;
        private readonly PSMSimulationClock _clock = new PSMSimulationClock();
        private bool _initialized;

        public PSMSimulationModule(MFMModuleDescriptor descriptor,
            PSMSimulationDefinition definition, IPSMSimulationModel model)
        {
            if (descriptor == null || definition == null || model == null) throw new ArgumentNullException("descriptor/definition/model");
            if (descriptor.Kind != MFMModuleKind.Simulation ||
                !string.Equals(model.ModelId, definition.ModelId, StringComparison.Ordinal))
                throw new ArgumentException("仿真类型/模型身份不匹配");
            Descriptor = descriptor; Definition = definition; _model = model;
        }
        public MFMModuleDescriptor Descriptor { get; private set; }
        public PSMSimulationDefinition Definition { get; private set; }
        public TimeSpan SimulatedTime { get { return _clock.Elapsed; } }
        public void Initialize()
        {
            lock (_gate)
            {
                _model.Initialize(Definition.Seed);
                _clock.Reset(); _initialized = true;
            }
        }
        public void Advance(TimeSpan simulationDelta)
        {
            if (simulationDelta <= TimeSpan.Zero) throw new ArgumentOutOfRangeException("simulationDelta");
            lock (_gate)
            {
                if (!_initialized) throw new InvalidOperationException("仿真模型未初始化");
                // 时间只在模型执行成功后推进，失败时不得显示虚假推进。
                _model.Advance(simulationDelta);
                _clock.Advance(simulationDelta);
            }
        }
        public void Reset()
        {
            lock (_gate)
            {
                _model.Reset(); _clock.Reset(); _initialized = false;
            }
        }
    }
}
