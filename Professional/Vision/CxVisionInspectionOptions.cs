using System;
using CxVisual.Foundation.Data.Configuration;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace CxVisual.Professional.Vision
{
    public enum CxVisionPixelDepth { Bits8 = 8, Bits16 = 16 }

    /// <summary>Vision缺陷检测配置样例（P30～P42），所有参数均是非设备实时数据。</summary>
    [FCMConfigSection("Vision.Inspection")]
    [FCMConfigValidator(typeof(CxVisionInspectionOptionsRules))]
    public sealed class CxVisionInspectionOptions
    {
        [FCMConfigItem("像素位深")]
        public CxVisionPixelDepth PixelDepth { get; set; } = CxVisionPixelDepth.Bits16;

        [FCMConfigItem("灰度阈值", Unit = "Gray", ApplyPolicy = FCMConfigApplyPolicy.NextTask)]
        [Range(0, 65535)]
        public int GrayThreshold { get; set; } = 1800;

        [FCMConfigItem("最小缺陷面积", Unit = "px²")]
        [Range(1, 10000000)]
        public double MinDefectArea { get; set; } = 10;

        [FCMConfigItem("最大缺陷面积", Unit = "px²")]
        [Range(1, 10000000)]
        public double MaxDefectArea { get; set; } = 100000;

        [FCMConfigItem("启用AI检测")]
        public bool UseAi { get; set; } = false;

        [FCMConfigItem("AI模型名称")]
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
    public sealed class CxVisionInspectionOptionsRules : IFCMConfigValidator<CxVisionInspectionOptions>
    {
        public IEnumerable<FCMConfigIssue> Validate(CxVisionInspectionOptions value, FCMConfigValidationContext ctx)
        {
            string error = CxVisionInspectionOptions.Validate(value);
            if (!string.IsNullOrWhiteSpace(error)) yield return FCMConfigIssue.Error(ctx.Path, "DOMAIN_CONFIG", error);
        }
    }
}
