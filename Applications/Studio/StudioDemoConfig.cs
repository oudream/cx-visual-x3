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

    public enum StudioQuality { Fast, Balanced, Precise }

    public sealed class StudioDetectorSetting
    {
        [FCMConfigItem("探测器增益")]
        [Range(1, 20)]
        public int Gain { get; set; } = 3;
        [FCMConfigItem("启用")]
        public bool Enabled { get; set; } = true;
    }
    public sealed class StudioWorkflowStep
    {
        [FCMConfigItem("步骤名称")]
        public string Name { get; set; } = "扫描";
        [FCMConfigItem("超时 ms")]
        [Range(1, 10000)]
        public int TimeoutMs { get; set; } = 500;
    }
    public sealed class StudioCalibration
    {
        [FCMConfigItem("校准矩阵版本")]
        public string Revision { get; set; } = "CAL-A";
    }
    /// <summary>V2.4 自动 UI 的动态集合/嵌套模型演示，不影响旧 Studio.Demo Schema。</summary>
    [FCMConfigSection("Studio.RichDemo")]
    public sealed class StudioRichConfig
    {
        [FCMConfigItem("曝光时间", Unit = "ms", Category = "采图", ApplyPolicy = FCMConfigApplyPolicy.NextTask)]
        [Range(1, 1000)]
        public int ExposureMs { get; set; } = 80;
        [FCMConfigItem("启用实时预览")]
        public bool PreviewEnabled { get; set; } = true;
        [FCMConfigItem("质量等级")]
        public StudioQuality Quality { get; set; } = StudioQuality.Balanced;
        [FCMConfigItem("可选重试次数")]
        public int? Retries { get; set; } = 2;
        [FCMConfigItem("探测器表")]
        public Dictionary<string, StudioDetectorSetting> Detectors { get; set; } =
            new Dictionary<string, StudioDetectorSetting> { { "D-01", new StudioDetectorSetting() } };
        [FCMConfigItem("流程步骤")]
        public List<StudioWorkflowStep> Steps { get; set; } =
            new List<StudioWorkflowStep> { new StudioWorkflowStep() };
        [FCMConfigItem("工艺标签")]
        public HashSet<string> Tags { get; set; } =
            new HashSet<string> { "XRay" };
        [FCMConfigItem("标定")]
        public StudioCalibration Calibration { get; set; } = new StudioCalibration();
    }
}
