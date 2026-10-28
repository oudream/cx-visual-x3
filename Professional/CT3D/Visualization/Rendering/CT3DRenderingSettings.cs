using System;
using CxVisual.Foundation.Volume;

namespace CxVisual.Professional.CT3D
{
    // 这里只定义渲染配置，不持有任何 OpenGL 或 WinForms 对象。
    /// <summary>保存体渲染、彩色切面和传递函数的可调参数。</summary>
    public sealed class CT3DRenderingSettings
    {
        public CT3DRenderingSettings()
        {
            TransferFunctions = new FVOTransferFunctionSet();
            Material = new CT3DMaterialSettings();
            Sampling = new CT3DSamplingSettings();
            Intensity = new CT3DIntensitySettings();
            Preset = CT3DRenderingPresetKind.SlicerLikeGrayscale;
            ShowSlices = true;
            ShowSliceTexture = false;
            ShowVolume = true;
            ShowVolumeBounds = false;
            SliceOpacity = 0.35f;
            VolumeOpacity = 1.0f;
        }

        // 这些设置会被 RenderingVolumeControl 转换为 shader uniform 或 1D transfer texture。
        public FVOTransferFunctionSet TransferFunctions { get; private set; }

        public CT3DMaterialSettings Material { get; private set; }

        public CT3DSamplingSettings Sampling { get; private set; }

        public CT3DIntensitySettings Intensity { get; private set; }

        public CT3DRenderingPresetKind Preset { get; set; }

        public bool ShowSlices { get; set; }

        // 独立控制三张平面上的体数据图案；即使隐藏红/绿/蓝定位面，图案仍可单独显示。
        public bool ShowSliceTexture { get; set; }

        public bool ShowVolume { get; set; }

        /// <summary>是否显示体数据物理范围的线框；默认关闭，避免遮挡体渲染细节。</summary>
        public bool ShowVolumeBounds { get; set; }

        public float SliceOpacity { get; set; }

        public float VolumeOpacity { get; set; }

        /// <summary>
        /// 先整批验证，再应用到唯一CT3D专业配置。
        /// 返回是否需要重新生成传递函数LUT；材质/采样/显隐只需更新uniform。
        /// 外部FVO控制点直接变化时，宿主另传曲线Dirty事件即可。
        /// </summary>
        public bool ApplyParameters(CT3DRenderingParameterValues values)
        {
            if(values==null)throw new ArgumentNullException("values");
            values.Validate();
            bool lutChanged =
                Preset!=values.Preset ||
                Intensity.AutoRange!=values.AutoRange ||
                TransferFunctions.UseColor!=values.UseColor ||
                TransferFunctions.UseScalarOpacity!=values.UseScalarOpacity ||
                TransferFunctions.UseGradientOpacity!=values.UseGradientOpacity ||
                TransferFunctions.UseThreshold!=values.UseThreshold ||
                TransferFunctions.Threshold!=values.Threshold;

            Preset=values.Preset;
            VolumeOpacity=values.VolumeOpacity;
            Sampling.StepScale=values.SamplingStepScale;
            Sampling.EnableJitter=values.SamplingJitter;
            Material.Ambient=values.Ambient;
            Material.Diffuse=values.Diffuse;
            Material.Specular=values.Specular;
            Material.SpecularPower=values.SpecularPower;
            Intensity.AutoRange=values.AutoRange;
            TransferFunctions.UseColor=values.UseColor;
            TransferFunctions.UseScalarOpacity=values.UseScalarOpacity;
            TransferFunctions.UseGradientOpacity=values.UseGradientOpacity;
            TransferFunctions.UseThreshold=values.UseThreshold;
            TransferFunctions.Threshold=values.Threshold;
            ShowVolume=values.ShowVolume;
            ShowSlices=values.ShowSlices;
            ShowSliceTexture=values.ShowSliceTexture;
            ShowVolumeBounds=values.ShowVolumeBounds;
            SliceOpacity=values.SliceOpacity;
            return lutChanged;
        }

        /// <summary>返回当前专业参数值快照，供纯数值测试与非WPF宿主使用。</summary>
        public CT3DRenderingParameterValues CaptureParameters()
        {
            return new CT3DRenderingParameterValues {
                Preset=Preset,VolumeOpacity=VolumeOpacity,
                SamplingStepScale=Sampling.StepScale,SamplingJitter=Sampling.EnableJitter,
                Ambient=Material.Ambient,Diffuse=Material.Diffuse,
                Specular=Material.Specular,SpecularPower=Material.SpecularPower,
                AutoRange=Intensity.AutoRange,
                UseColor=TransferFunctions.UseColor,
                UseScalarOpacity=TransferFunctions.UseScalarOpacity,
                UseGradientOpacity=TransferFunctions.UseGradientOpacity,
                UseThreshold=TransferFunctions.UseThreshold,
                Threshold=TransferFunctions.Threshold,
                ShowVolume=ShowVolume,ShowSlices=ShowSlices,
                ShowSliceTexture=ShowSliceTexture,ShowVolumeBounds=ShowVolumeBounds,
                SliceOpacity=SliceOpacity
            };
        }

        /// <summary>FVO曲线与CT3D渲染预设的直接组合；产生GPU RGBA8纹理。</summary>
        public byte[] BuildScalarTransferTexture(FVOVolumeData volume)
        {
            if (volume == null) throw new ArgumentNullException("volume");
            FVOIntensityRangeOptions options = new FVOIntensityRangeOptions
            {
                AutoRange = Intensity.AutoRange,
                BackgroundGrayUpper = Intensity.BackgroundGrayUpper,
                LowerPercentile = Intensity.LowerPercentile,
                UpperPercentile = Intensity.UpperPercentile
            };
            FVOVolumeIntensityProfile profile = FVOVolumeIntensityProfile.FromVolume(volume, options);
            float scale = Preset == CT3DRenderingPresetKind.SlicerLikeGrayscale
                ? Intensity.PresetOpacityScale : 1.0f;
            return TransferFunctions.BuildScalarTransferTexture(
                x => CT3DRenderingPresets.EvaluateBaseGray(Preset, x, profile),
                x => CT3DRenderingPresets.EvaluateBaseOpacity(Preset, x, profile),
                scale);
        }
    }

    /// <summary>
    /// 纯专业参数输入。WPF宿主仅提供数值，CT3D拥有验证与配置应用；
    /// FVO控制点列表继续归CT3DRenderingSettings.TransferFunctions，绝不复制。
    /// </summary>
    public sealed class CT3DRenderingParameterValues
    {
        public CT3DRenderingPresetKind Preset { get; set; }
        public float VolumeOpacity { get; set; }
        public float SamplingStepScale { get; set; }
        public bool SamplingJitter { get; set; }
        public float Ambient { get; set; }
        public float Diffuse { get; set; }
        public float Specular { get; set; }
        public float SpecularPower { get; set; }
        public bool AutoRange { get; set; }
        public bool UseColor { get; set; }
        public bool UseScalarOpacity { get; set; }
        public bool UseGradientOpacity { get; set; }
        public bool UseThreshold { get; set; }
        public float Threshold { get; set; }
        public bool ShowVolume { get; set; }
        public bool ShowSlices { get; set; }
        public bool ShowSliceTexture { get; set; }
        public bool ShowVolumeBounds { get; set; }
        public float SliceOpacity { get; set; }

        public void Validate()
        {
            if (Preset != CT3DRenderingPresetKind.SlicerLikeGrayscale &&
                Preset != CT3DRenderingPresetKind.LinearGrayscale)
                throw new ArgumentOutOfRangeException("Preset");
            InRange(VolumeOpacity, 0f, 3f, "VolumeOpacity");
            InRange(SamplingStepScale, .25f, 3f, "SamplingStepScale");
            InRange(Ambient, 0f, 1f, "Ambient");
            InRange(Diffuse, 0f, 1.5f, "Diffuse");
            InRange(Specular, 0f, 1.5f, "Specular");
            InRange(SpecularPower, 1f, 96f, "SpecularPower");
            InRange(Threshold, 0f, 1f, "Threshold");
            InRange(SliceOpacity, 0f, 1f, "SliceOpacity");
        }

        private static void InRange(float value,float min,float max,string name)
        {
            if(Single.IsNaN(value)||Single.IsInfinity(value)||value<min||value>max)
                throw new ArgumentOutOfRangeException(name,
                    "CT3D参数必须为界面范围内的有限数值。");
        }
    }

    /// <summary>控制默认灰阶预设如何从当前体数据中识别背景并展开有效灰度。</summary>
    public sealed class CT3DIntensitySettings
    {
        public CT3DIntensitySettings()
        {
            AutoRange = true;
            BackgroundGrayUpper = 0;
            LowerPercentile = 0.10f;
            UpperPercentile = 0.995f;
            PresetOpacityScale = 1.0f;
        }

        public bool AutoRange { get; set; }

        /// <summary>小于或等于该 8 位灰度的体素视为背景；默认仅排除纯黑。</summary>
        public int BackgroundGrayUpper { get; set; }

        public float LowerPercentile { get; set; }

        public float UpperPercentile { get; set; }

        /// <summary>
        /// 内建预设不透明度曲线的倍率。仅在未启用自定义“标量透明度”时生效，
        /// 避免预设参数与用户曲线重复叠加。
        /// </summary>
        public float PresetOpacityScale { get; set; }
    }

    /// <summary>体渲染的光照材质参数；数值会直接映射到体渲染 Shader。</summary>
    public sealed class CT3DMaterialSettings
    {
        public CT3DMaterialSettings()
        {
            Ambient = 0.34f;
            Diffuse = 0.62f;
            Specular = 0.16f;
            SpecularPower = 24.0f;
        }

        public float Ambient { get; set; }

        public float Diffuse { get; set; }

        public float Specular { get; set; }

        public float SpecularPower { get; set; }
    }

    /// <summary>控制射线步进质量和采样稳定性，不包含任何 UI 状态。</summary>
    public sealed class CT3DSamplingSettings
    {
        public CT3DSamplingSettings()
        {
            // 线性纹理过滤下按最长轴约一体素采样；抖动用于抑制固定步长产生的层纹。
            StepScale = 1.0f;
            ReferenceStep = 1.0f / 256.0f;
            EnableJitter = true;
        }

        public float StepScale { get; set; }

        public float ReferenceStep { get; set; }

        public bool EnableJitter { get; set; }
    }
}
