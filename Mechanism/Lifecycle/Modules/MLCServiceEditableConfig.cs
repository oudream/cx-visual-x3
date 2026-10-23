using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Mechanism.Core.Modules;

namespace CxVisual.Mechanism.Lifecycle.Modules
{
    [FCMConfigSection("CxVisual.Module.Service", SchemaVersion = 1)]
    public sealed class MLCServiceEditableConfig : MFMModuleEditableConfig
    {
        [FCMConfigItem("服务 Provider TypeId", Order = 10, Category = "服务")]
        public string ProviderTypeId { get; set; } = "";
        [FCMConfigItem("启动超时", Order = 11, Unit = "ms", Category = "生命周期")]
        [Range(1, 3600000)]
        public int StartupTimeoutMs { get; set; } = 30000;
        [FCMConfigItem("停止超时", Order = 12, Unit = "ms", Category = "生命周期")]
        [Range(1, 3600000)]
        public int ShutdownTimeoutMs { get; set; } = 30000;
        [FCMConfigItem("最大并发请求", Order = 13, Category = "执行")]
        [Range(1, 4096)]
        public int MaxConcurrentRequests { get; set; } = 1;
        [FCMConfigItem("依赖的服务资源 Id", Order = 14, Category = "依赖")]
        public List<string> DependencyRefs { get; set; } = new List<string>();

        public MLCServiceModuleOptions ToOptions()
        {
            MLCServiceModuleOptions options = new MLCServiceModuleOptions {
                StartupTimeout = TimeSpan.FromMilliseconds(StartupTimeoutMs),
                ShutdownTimeout = TimeSpan.FromMilliseconds(ShutdownTimeoutMs),
                MaxConcurrentRequests = MaxConcurrentRequests,
                DependencyRefs = DependencyRefs.ToArray()
            };
            options.Validate();
            return options;
        }
    }
}
