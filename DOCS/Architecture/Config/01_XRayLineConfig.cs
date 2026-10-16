// CxVisual 配置开发体验的候选 API 演示；并非 V7.8 已有接口。
// 使用时需先由平台实现文中 ConfigSection/ConfigItem/CxConfig 等契约。
using System;
using System.Collections.Generic;
using CxVisual.Configuration; // 候选门面命名空间，待与现有类型清单确认

namespace CxVisual.Examples.Configuration
{
    [ConfigSection("Inspection.XRayLine", DisplayName = "X-Ray 检测线")]
    public sealed class XRayLineConfig
    {
        [ConfigItem("整线启用")]
        public bool Enabled { get; set; } = true;

        public DeviceGroupConfig Devices { get; set; } = new DeviceGroupConfig();
        public VisionGroupConfig Vision { get; set; } = new VisionGroupConfig();
        public RuntimeGroupConfig Runtime { get; set; } = new RuntimeGroupConfig();

        [ConfigItem("自定义标签")]
        public Dictionary<string, string> Tags { get; set; }
            = new Dictionary<string, string> { { "Factory", "Foshan" } };
    }

    public sealed class DeviceGroupConfig
    {
        [ConfigItem("探测器集合")]
        public Dictionary<string, DetectorConfig> Detectors { get; set; }
            = new Dictionary<string, DetectorConfig>
            {
                { "Detector-01", new DetectorConfig() },
                { "Detector-02", new DetectorConfig() }
            };

        [ConfigItem("运动轴")]
        public List<AxisConfig> Axes { get; set; }
            = new List<AxisConfig> { new AxisConfig { Id = "Z1" } };
    }

    public sealed class DetectorConfig
    {
        public CaptureConfig Capture { get; set; } = new CaptureConfig();
        public DetectorCalibrationConfig Calibration { get; set; }
            = new DetectorCalibrationConfig();
    }

    public enum TriggerMode { Software, Hardware, Encoder }
    public enum PixelFormat { Mono8, Mono16 }

    public sealed class CaptureConfig
    {
        [ConfigItem("曝光时间", Unit = "ms", Min = 1, Max = 2000,
            Apply = ConfigApplyMode.NextTask)]
        public int ExposureMs { get; set; } = 80;

        [ConfigItem("增益", Min = 0.1, Max = 20)]
        public double Gain { get; set; } = 1.0;

        [ConfigItem("触发方式")]
        public TriggerMode Trigger { get; set; } = TriggerMode.Hardware;

        [ConfigItem("像素格式")]
        public PixelFormat Format { get; set; } = PixelFormat.Mono16;

        [ConfigItem("采图超时")]
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

        public RoiConfig Crop { get; set; } = new RoiConfig();
    }

    public sealed class RoiConfig
    {
        [ConfigItem("X", Unit = "px")]
        public int X { get; set; }
        [ConfigItem("Y", Unit = "px")]
        public int Y { get; set; }
        [ConfigItem("宽度", Unit = "px", Min = 1)]
        public int Width { get; set; } = 3072;
        [ConfigItem("高度", Unit = "px", Min = 1)]
        public int Height { get; set; } = 1536;
    }

    public sealed class DetectorCalibrationConfig
    {
        [ConfigItem("标定资源引用", Editor = "ResourcePicker")]
        public string CalibrationResourceId { get; set; } = "Calibration/Detector-01";

        [ConfigItem("最后标定日期")]
        public DateTimeOffset? CalibratedAt { get; set; }

        [ConfigItem("像素修正系数")]
        public double[] Correction { get; set; } = new double[] { 1.0, 0.0, 0.0 };
    }

    public sealed class AxisConfig
    {
        [ConfigKey]
        public string Id { get; set; } = string.Empty;

        [ConfigItem("最大速度", Unit = "mm/s", Min = 0.01)]
        public double MaxVelocity { get; set; } = 20;

        [ConfigItem("软限位")]
        public NumericRange SoftLimit { get; set; } = new NumericRange { Min = -50, Max = 50 };
    }

    public sealed class NumericRange
    {
        public double Min { get; set; }
        public double Max { get; set; }
    }

    public sealed class VisionGroupConfig
    {
        [ConfigItem("算法实例集合")]
        public Dictionary<string, AlgorithmConfig> Algorithms { get; set; }
            = new Dictionary<string, AlgorithmConfig>
            {
                { "VoidDetection", new AlgorithmConfig() }
            };
    }

    public sealed class AlgorithmConfig
    {
        [ConfigItem("算法启用")]
        public bool Enabled { get; set; } = true;

        [ConfigItem("灰度阈值", Min = 0, Max = 65535)]
        public int Threshold { get; set; } = 1800;

        [ConfigItem("最小缺陷面积", Unit = "mm²", Min = 0)]
        public decimal MinAreaMm2 { get; set; } = 0.25m;

        [ConfigItem("ROI 规则")]
        public List<DefectRoiConfig> Regions { get; set; }
            = new List<DefectRoiConfig>
            {
                new DefectRoiConfig { Id = "R1", Bounds = new RoiConfig() }
            };

        [ConfigItem("按缺陷类型设定上下限")]
        public Dictionary<string, NumericRange> DefectLimits { get; set; }
            = new Dictionary<string, NumericRange>
            {
                { "VoidArea", new NumericRange { Min = 0, Max = 0.8 } }
            };
    }

    public sealed class DefectRoiConfig
    {
        [ConfigKey]
        public string Id { get; set; } = string.Empty;
        public RoiConfig Bounds { get; set; } = new RoiConfig();
        public bool Enabled { get; set; } = true;
    }

    public sealed class RuntimeGroupConfig
    {
        [ConfigItem("处理线程数", Min = 1, Max = 128)]
        public int WorkerCount { get; set; } = 4;

        [ConfigItem("结果回调地址")]
        public Uri ResultEndpoint { get; set; }

        [ConfigItem("报警邮箱")]
        public List<string> AlarmRecipients { get; set; } = new List<string>();
    }
}
