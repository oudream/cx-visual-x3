using System;
using System.ComponentModel.DataAnnotations;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Mechanism.Core.Modules;

namespace CxVisual.Professional.Vision.Modules
{
    [FCMConfigSection("CxVisual.Module.Compute", SchemaVersion = 1)]
    public sealed class PVCComputeEditableConfig : MFMModuleEditableConfig
    {
        [FCMConfigItem("执行设备", Order = 10, Category = "资源")]
        public PVCComputeDevicePreference Device { get; set; } = PVCComputeDevicePreference.Auto;
        [FCMConfigItem("最大并发", Order = 11, Category = "资源")]
        [Range(1, 4096)]
        public int MaxConcurrency { get; set; } = 1;
        [FCMConfigItem("内存预算", Order = 12, Category = "资源", Unit = "MB")]
        [Range(1, 1048576)]
        public int MemoryBudgetMb { get; set; } = 256;
        [FCMConfigItem("执行超时", Order = 13, Category = "执行", Unit = "ms")]
        [Range(1, 86400000)]
        public int TimeoutMs { get; set; } = 60000;
        [FCMConfigItem("已有 F19/MRK 执行组引用", Order = 14, Category = "执行")]
        public string ExecutionGroupRef { get; set; } = "";
        [FCMConfigItem("算法提供者 TypeId", Order = 15, Category = "专业")]
        public string ProviderTypeId { get; set; } = "";

        public PVCComputeExecutionOptions ToOptions()
        {
            PVCComputeExecutionOptions options = new PVCComputeExecutionOptions {
                Device = Device, MaxConcurrency = MaxConcurrency,
                MemoryBudgetBytes = (long)MemoryBudgetMb * 1024L * 1024L,
                Timeout = TimeSpan.FromMilliseconds(TimeoutMs),
                ExecutionGroupRef = ExecutionGroupRef
            };
            options.Validate();
            return options;
        }
    }
}
