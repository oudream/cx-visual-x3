using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Mechanism.Core.Modules;

namespace CxVisual.Mechanism.Lifecycle.Modules
{
    [FCMConfigSection("CxVisual.Module.Service", SchemaVersion = 1)]
    public sealed class MLCServiceModuleConfiguration : MFMModuleConfiguration
    {
        [FCMConfigItem("Service Provider TypeId", Order = 10, Category = "服务")]
        public string ProviderTypeId { get; set; } = "";
        [FCMConfigItem("启动超时", Order = 11, Category = "生命周期", Unit = "ms")]
        [Range(1, 3600000)]
        public int StartupTimeoutMs { get; set; } = 30000;
        [FCMConfigItem("关闭超时", Order = 12, Category = "生命周期", Unit = "ms")]
        [Range(1, 3600000)]
        public int ShutdownTimeoutMs { get; set; } = 30000;
        [FCMConfigItem("最大并发请求", Order = 13, Category = "执行")]
        [Range(1, 4096)]
        public int MaxConcurrentRequests { get; set; } = 1;
        [FCMConfigItem("依赖服务资源 Id", Order = 14, Category = "依赖")]
        public List<string> DependencyRefs { get; set; } = new List<string>();

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(DisplayName) || StartupTimeoutMs < 1 || ShutdownTimeoutMs < 1 ||
                MaxConcurrentRequests < 1 || DependencyRefs == null ||
                DependencyRefs.Any(string.IsNullOrWhiteSpace) ||
                DependencyRefs.Distinct(StringComparer.Ordinal).Count() != DependencyRefs.Count)
                throw new ArgumentException("Service 配置、依赖或执行预算无效");
        }
    }
}
