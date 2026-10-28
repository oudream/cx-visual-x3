using System;
using CxVisual.Foundation.Volume;
using CxVisual.Professional.CT3D;

namespace CxVisual.Tests.ArchitectureSmoke
{
    /// <summary>沿用旧版灰阶、透明度、阈值与梯度传递算法；不冒充GPU实机。</summary>
    internal static class CT3DRenderingTests
    {
        public static void Run()
        {
            using (FVOVolumeData data = FVOVolumeData.CreateDemo(14, 12, 10))
            {
                CT3DRenderingSettings config = new CT3DRenderingSettings();
                Check(config.Preset == CT3DRenderingPresetKind.SlicerLikeGrayscale &&
                    config.Sampling.EnableJitter, "Slicer预设与GPU参数默认值");
                byte[] preset = config.BuildScalarTransferTexture(data);
                byte[] gradients = config.TransferFunctions.BuildGradientOpacityTexture();
                Check(preset.Length == 1024 && gradients.Length == 256, "RGBA8与梯度R8纹理长度");
                Check(preset[0] == 0 && preset[1] == 0 && preset[2] == 0, "初始黑色背景");
                CT3DRenderingParameterValues input=config.CaptureParameters();
                Check(input.Preset==config.Preset &&
                    input.VolumeOpacity==config.VolumeOpacity &&
                    input.SamplingStepScale==config.Sampling.StepScale,
                    "CT3D专业参数快照必须直接读取唯一配置");
                Check(!config.ApplyParameters(input),"重复相同参数不得使GPU LUT失效");
                input.VolumeOpacity=.45f;
                input.SamplingStepScale=.7f;
                input.Ambient=.12f;
                input.ShowSlices=false;
                Check(!config.ApplyParameters(input) &&
                    Math.Abs(config.VolumeOpacity-.45f)<.001f &&
                    Math.Abs(config.Sampling.StepScale-.7f)<.001f &&
                    Math.Abs(config.Material.Ambient-.12f)<.001f && !config.ShowSlices,
                    "CT3D采样/光照/切面更改只能更新uniform");
                input.UseColor=true;
                Check(config.ApplyParameters(input) &&
                    config.TransferFunctions.UseColor,
                    "颜色曲线开关必须触发LUT失效且写入FVO唯一真源");
                Check(!config.ApplyParameters(input),"幂等重复应用不得重复上传LUT");
                input.Threshold=.31f;
                Check(config.ApplyParameters(input) &&
                    Math.Abs(config.TransferFunctions.Threshold-.31f)<.001f,
                    "阈值变更应更新唯一FVO阈值并触发LUT失效");
                CT3DRenderingParameterValues invalid=config.CaptureParameters();
                invalid.VolumeOpacity=.9f;
                invalid.Specular=float.NaN;
                bool rejected=false;
                try {config.ApplyParameters(invalid);}
                catch(ArgumentOutOfRangeException){rejected=true;}
                Check(rejected && Math.Abs(config.VolumeOpacity-.45f)<.001f &&
                    Math.Abs(config.Material.Specular-input.Specular)<.001f,
                    "NaN非法参数不得部分污染CT3D设置");
                invalid=config.CaptureParameters();
                invalid.Preset=(CT3DRenderingPresetKind)1234;
                rejected=false;
                try {config.ApplyParameters(invalid);}
                catch(ArgumentOutOfRangeException){rejected=true;}
                Check(rejected && config.Preset==CT3DRenderingPresetKind.SlicerLikeGrayscale,
                    "非法枚举预设必须失败关闭");
                config.TransferFunctions.UseThreshold = true;
                config.TransferFunctions.Threshold = 0.3f;
                config.TransferFunctions.UpperThreshold = 0.9f;
                byte[] thresholded = config.BuildScalarTransferTexture(data);
                Check(thresholded[3] == 0 && thresholded[255 * 4 + 3] == 0,
                    "上下阈值必须剔除范围外体素");
                config.TransferFunctions.UseColor = true;
                FVOColor mid = config.TransferFunctions.Color.Evaluate(0.55f);
                Check(mid.R == 230 && mid.G == 194 && mid.B == 68, "旧色标中间控制点");
                config.TransferFunctions.UseThreshold = false;
                config.TransferFunctions.UseScalarOpacity = true;
                byte[] scale1 = config.BuildScalarTransferTexture(data);
                config.Intensity.PresetOpacityScale = 4f;
                byte[] scale4 = config.BuildScalarTransferTexture(data);
                Check(scale1[4 * 255 + 3] == scale4[4 * 255 + 3],
                    "自定义标量透明曲线不能叠加Slicer预设倍率");
                config.TransferFunctions.UseGradientOpacity = true;
                Check(config.TransferFunctions.BuildGradientOpacityTexture()[0] == 0,
                    "梯度透明曲线首点");
                config.Preset = CT3DRenderingPresetKind.LinearGrayscale;
                config.TransferFunctions.UseScalarOpacity = false;
                byte[] linear = config.BuildScalarTransferTexture(data);
                Check(linear[255 * 4 + 3] == 255, "线性灰阶末端不透明度");
                Console.WriteLine("PASS FVO transfer LUT and CT3D Slicer/linear preset composition");
            }
        }
        private static void Check(bool ok, string message)
        {
            if (!ok) throw new InvalidOperationException("CT3D传递函数测试失败：" + message);
        }
    }
}
