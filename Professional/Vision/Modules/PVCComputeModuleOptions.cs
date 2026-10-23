using System;
using CxVisual.Mechanism.Core.Modules;

namespace CxVisual.Professional.Vision.Modules
{
    // 资源选择枚举与 ModuleProfile 是运行侧元数据，不是重复配置。
    public enum PVCComputeDevicePreference { Auto, Cpu, Gpu }

    public sealed class PVCComputeModuleProfile
    {
        public PVCComputeModuleProfile(MFMModuleDescriptor descriptor, PVCComputeModuleConfiguration configuration = null)
        {
            if (descriptor == null) throw new ArgumentNullException("descriptor");
            if (descriptor.Kind != MFMModuleKind.Compute) throw new ArgumentException("不是 Compute 模块");
            Configuration = configuration ?? new PVCComputeModuleConfiguration();
            Configuration.Validate();
            Descriptor = descriptor;
        }
        public MFMModuleDescriptor Descriptor { get; private set; }
        // 真实配置由 FCM/MHS 持久化，该运行对象引用同一个类型，不再接受另一份 ExecutionOptions。
        public PVCComputeModuleConfiguration Configuration { get; private set; }
    }
}
