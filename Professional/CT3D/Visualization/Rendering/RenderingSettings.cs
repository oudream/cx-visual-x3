namespace CxECTVolumeCore.Rendering
{
    // 这里只定义渲染配置，不持有任何 OpenGL 或 WinForms 对象。
    /// <summary>保存体渲染、彩色切面和传递函数的可调参数。</summary>
    public sealed class RenderingSettings
    {
        public RenderingSettings()
        {
            TransferFunctions = new RenderingTransferFunctionSet();
            Material = new RenderingMaterialSettings();
            Sampling = new RenderingSamplingSettings();
            Intensity = new RenderingIntensitySettings();
            Preset = RenderingPresetKind.SlicerLikeGrayscale;
            ShowSlices = true;
            ShowSliceTexture = false;
            ShowVolume = true;
            ShowVolumeBounds = false;
            SliceOpacity = 0.35f;
            VolumeOpacity = 1.0f;
        }

        // 这些设置会被 RenderingVolumeControl 转换为 shader uniform 或 1D transfer texture。
        public RenderingTransferFunctionSet TransferFunctions { get; private set; }

        public RenderingMaterialSettings Material { get; private set; }

        public RenderingSamplingSettings Sampling { get; private set; }

        public RenderingIntensitySettings Intensity { get; private set; }

        public RenderingPresetKind Preset { get; set; }

        public bool ShowSlices { get; set; }

        // 独立控制三张平面上的体数据图案；即使隐藏红/绿/蓝定位面，图案仍可单独显示。
        public bool ShowSliceTexture { get; set; }

        public bool ShowVolume { get; set; }

        /// <summary>是否显示体数据物理范围的线框；默认关闭，避免遮挡体渲染细节。</summary>
        public bool ShowVolumeBounds { get; set; }

        public float SliceOpacity { get; set; }

        public float VolumeOpacity { get; set; }
    }

    /// <summary>控制默认灰阶预设如何从当前体数据中识别背景并展开有效灰度。</summary>
    public sealed class RenderingIntensitySettings
    {
        public RenderingIntensitySettings()
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
    public sealed class RenderingMaterialSettings
    {
        public RenderingMaterialSettings()
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
    public sealed class RenderingSamplingSettings
    {
        public RenderingSamplingSettings()
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
