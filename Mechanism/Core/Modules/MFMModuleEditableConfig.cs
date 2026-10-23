using System;
using System.Collections.Generic;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Mechanism.Core.Modules
{
    // 模块配置元数据直接使用现有 FCM/MHS 的 POCO + Attribute。
    // ModuleId 来自 MHS ScopeId/MEM 工程身份，不在配置里复制第二份 Id。
    public abstract class MFMModuleEditableConfig
    {
        [FCMConfigItem("模块显示名称", Order = 1, Category = "基本信息")]
        public string DisplayName { get; set; } = "新模块";

        [FCMConfigItem("启用", Order = 2, Category = "基本信息")]
        public bool Enabled { get; set; } = true;

        [FCMConfigItem("能力 Id 列表", Order = 3, Category = "基本信息",
            Description = "引用已有 MOC Capability；不会建立第二个模块能力目录")]
        public List<string> CapabilityIds { get; set; } = new List<string>();

        public MFMModuleDescriptor Describe(string moduleId, MFMModuleKind kind)
        {
            if (string.IsNullOrWhiteSpace(DisplayName)) throw new ArgumentException("模块名称不能为空");
            return new MFMModuleDescriptor(moduleId, DisplayName, kind, 1, CapabilityIds);
        }
    }
}
