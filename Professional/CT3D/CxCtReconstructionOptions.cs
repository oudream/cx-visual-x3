using System;
using CxVisual.Mechanism.Core;

namespace CxVisual.Professional.CT3D
{
    public enum CxCtReconstructionMode { Fdk = 0, Sart = 1 }

    /// <summary>CT3D重建参数样例（P43～P53），不替代机台标定及采图硬件安全配置。</summary>
    public sealed class CxCtReconstructionOptions
    {
        [CxConfigItem("重建算法", Category = "重建")]
        public CxCtReconstructionMode ReconstructionMode { get; set; } = CxCtReconstructionMode.Fdk;

        [CxConfigItem("投影数量", Category = "采集数据", Min = 1, Max = 100000)]
        public int ProjectionCount { get; set; } = 720;

        [CxConfigItem("物源距FOD", Category = "几何", Unit = "mm", Min = 1, Max = 5000)]
        public double FodMm { get; set; } = 200;

        [CxConfigItem("物检距FDD", Category = "几何", Unit = "mm", Min = 1, Max = 10000)]
        public double FddMm { get; set; } = 700;

        [CxConfigItem("探测器像素尺寸", Category = "几何", Unit = "μm", Min = 1, Max = 2000)]
        public double DetectorPixelSizeUm { get; set; } = 100;

        [CxConfigItem("GPU序号", Category = "计算", Min = 0, Max = 31)]
        public int GpuIndex { get; set; } = 0;

        [CxConfigItem("迭代次数", Category = "计算", Min = 1, Max = 2000,
            Description = "迭代重建时使用；FDK模式下不使用本参数")]
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
}
