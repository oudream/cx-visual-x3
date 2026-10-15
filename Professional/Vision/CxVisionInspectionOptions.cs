using System;
using CxVisual.Mechanism.Core;

namespace CxVisual.Professional.Vision
{
    public enum CxVisionPixelDepth { Bits8 = 8, Bits16 = 16 }

    /// <summary>Vision缺陷检测配置样例（P30～P42），所有参数均是非设备实时数据。</summary>
    public sealed class CxVisionInspectionOptions
    {
        [MPMConfigItem("像素位深", Category = "图像")]
        public CxVisionPixelDepth PixelDepth { get; set; } = CxVisionPixelDepth.Bits16;

        [MPMConfigItem("灰度阈值", Category = "图像", Unit = "Gray", Min = 0, Max = 65535)]
        public int GrayThreshold { get; set; } = 1800;

        [MPMConfigItem("最小缺陷面积", Category = "检测", Unit = "px²", Min = 1, Max = 10000000)]
        public double MinDefectArea { get; set; } = 10;

        [MPMConfigItem("最大缺陷面积", Category = "检测", Unit = "px²", Min = 1, Max = 10000000)]
        public double MaxDefectArea { get; set; } = 100000;

        [MPMConfigItem("启用AI检测", Category = "AI")]
        public bool UseAi { get; set; } = false;

        [MPMConfigItem("AI模型名称", Category = "AI")]
        public string AiModelName { get; set; } = "";

        public static string Validate(CxVisionInspectionOptions value)
        {
            if (value == null) return "Vision options missing";
            if (!Enum.IsDefined(typeof(CxVisionPixelDepth), value.PixelDepth))
                return "不支持的像素位深";
            if (value.PixelDepth == CxVisionPixelDepth.Bits8 && value.GrayThreshold > 255)
                return "8位图像的灰度阈值必须不超过255";
            if (value.MinDefectArea > value.MaxDefectArea)
                return "最小缺陷面积不能超过最大缺陷面积";
            if (value.UseAi && string.IsNullOrWhiteSpace(value.AiModelName))
                return "启用AI检测时必须指定模型名称";
            return null;
        }
    }
}
