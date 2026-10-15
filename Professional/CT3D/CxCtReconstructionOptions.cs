using System;
using CxVisual.Foundation.Data.Configuration;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace CxVisual.Professional.CT3D
{
    public enum CxCtReconstructionMode { Fdk = 0, Sart = 1 }

    /// <summary>CT3D重建参数样例（P43～P53），不替代机台标定及采图硬件安全配置。</summary>
    [FCMConfigSection("CT3D.Reconstruction")]
    [FCMConfigValidator(typeof(CxCtReconstructionOptionsRules))]
    public sealed class CxCtReconstructionOptions
    {
        [FCMConfigItem("重建算法")]
        public CxCtReconstructionMode ReconstructionMode { get; set; } = CxCtReconstructionMode.Fdk;

        [FCMConfigItem("投影数量")]
        [Range(1, 100000)]
        public int ProjectionCount { get; set; } = 720;

        [FCMConfigItem("物源距FOD", Unit = "mm")]
        [Range(1, 5000)]
        public double FodMm { get; set; } = 200;

        [FCMConfigItem("物检距FDD", Unit = "mm")]
        [Range(1, 10000)]
        public double FddMm { get; set; } = 700;

        [FCMConfigItem("探测器像素尺寸", Unit = "μm")]
        [Range(1, 2000)]
        public double DetectorPixelSizeUm { get; set; } = 100;

        [FCMConfigItem("GPU序号")]
        [Range(0, 31)]
        public int GpuIndex { get; set; } = 0;

        [FCMConfigItem("迭代次数")]
        [Range(1, 2000)]
        public int Iterations { get; set; } = 40;

        public static string Validate(CxCtReconstructionOptions value)
        {
            if (value == null) return "CT reconstruction options missing";
            if (!Enum.IsDefined(typeof(CxCtReconstructionMode), value.ReconstructionMode))
                return "不支持的CT重建算法";
            if (value.FddMm <= value.FodMm)
                return "FDD必须大于FOD，否则几何放大比无效";
            return null;
        }
    }
    public sealed class CxCtReconstructionOptionsRules : IFCMConfigValidator<CxCtReconstructionOptions>
    {
        public IEnumerable<FCMConfigIssue> Validate(CxCtReconstructionOptions value, FCMConfigValidationContext ctx)
        {
            string error = CxCtReconstructionOptions.Validate(value);
            if (!string.IsNullOrWhiteSpace(error)) yield return FCMConfigIssue.Error(ctx.Path, "DOMAIN_CONFIG", error);
        }
    }
}
