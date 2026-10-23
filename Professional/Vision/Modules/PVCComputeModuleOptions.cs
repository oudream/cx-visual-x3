using System;
using CxVisual.Mechanism.Core.Modules;

namespace CxVisual.Professional.Vision.Modules
{
    // 只描述专业计算资源意图；实际并发、取消、执行与结果由已有 F19/MRK/MDS 实现。
    public enum PVCComputeDevicePreference { Auto, Cpu, Gpu }

    public sealed class PVCComputeExecutionOptions
    {
        public PVCComputeDevicePreference Device { get; set; } = PVCComputeDevicePreference.Auto;
        public int MaxConcurrency { get; set; } = 1;
        public long MemoryBudgetBytes { get; set; } = 256L * 1024L * 1024L;
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(1);
        public string ExecutionGroupRef { get; set; }
        public void Validate()
        {
            if (!Enum.IsDefined(typeof(PVCComputeDevicePreference), Device) ||
                MaxConcurrency < 1 || MemoryBudgetBytes < 1 || Timeout <= TimeSpan.Zero)
                throw new ArgumentException("计算资源配置无效");
        }
    }

    public sealed class PVCComputeModuleProfile
    {
        public PVCComputeModuleProfile(MFMModuleDescriptor descriptor, PVCComputeExecutionOptions options = null)
        {
            if (descriptor == null) throw new ArgumentNullException("descriptor");
            if (descriptor.Kind != MFMModuleKind.Compute) throw new ArgumentException("不是 Compute 模块");
            Descriptor = descriptor; Options = options ?? new PVCComputeExecutionOptions();
            Options.Validate();
        }
        public MFMModuleDescriptor Descriptor { get; private set; }
        public PVCComputeExecutionOptions Options { get; private set; }
    }
}
