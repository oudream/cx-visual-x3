using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Mechanism.Core.Modules
{
    // 唯一模块配置基类：FCM/MHS/MPW 和运行模块读取同一配置定义。
    // ModuleId 使用 MEM/MHS Scope 身份，不在此维护第二个 Id。
    public abstract class MFMModuleConfiguration
    {
        [FCMConfigItem("模块显示名称", Order = 1, Category = "基本信息")]
        [Required]
        public string DisplayName { get; set; } = "新模块";

        [FCMConfigItem("启用", Order = 2, Category = "基本信息")]
        public bool Enabled { get; set; } = true;

        [FCMConfigItem("能力 Id 列表", Order = 3, Category = "基本信息",
            Description = "引用现有 MOC Capability；无独立 ModuleCatalog")]
        public List<string> CapabilityIds { get; set; } = new List<string>();

        public MFMModuleDescriptor Describe(string moduleId, MFMModuleKind kind)
        {
            if (string.IsNullOrWhiteSpace(DisplayName)) throw new ArgumentException("模块名称不能为空");
            return new MFMModuleDescriptor(moduleId, DisplayName, kind, 1, CapabilityIds);
        }
    }
}
