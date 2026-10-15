using CxVisual.Foundation.Data.Configuration;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace CxVisual.Studio
{
    /// <summary>展示零样板强类型配置、默认初始化、通用属性编辑器和安全持久化。</summary>
    [FCMConfigSection("Studio.Demo")]
    public sealed class StudioDemoConfig
    {
        [FCMConfigItem("网格间距")]
        [Range(5, 200)]
        public int CanvasGridSize { get; set; } = 25;

        [FCMConfigItem("显示网格")]
        public bool ShowGrid { get; set; } = true;

        [FCMConfigItem("编辑器标题")]
        [Required]
        public string EditorTitle { get; set; } = "CxVisual Studio";

        [FCMConfigItem("自动备份")]
        public bool AutoBackup { get; set; } = true;
    }
}
