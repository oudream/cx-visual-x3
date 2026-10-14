using CxVisual.Mechanism.Core;

namespace CxVisual.Studio
{
    /// <summary>展示零样板强类型配置、默认初始化、通用属性编辑器和安全持久化。</summary>
    public sealed class StudioDemoConfig
    {
        [CxConfigItem("网格间距", Category = "画布", Description = "演示编辑器自动创建数值控件", Min = 5, Max = 200)]
        public int CanvasGridSize { get; set; } = 25;

        [CxConfigItem("显示网格", Category = "画布")]
        public bool ShowGrid { get; set; } = true;

        [CxConfigItem("编辑器标题", Category = "界面", Required = true)]
        public string EditorTitle { get; set; } = "CxVisual Studio";

        [CxConfigItem("自动备份", Category = "行为")]
        public bool AutoBackup { get; set; } = true;
    }
}
