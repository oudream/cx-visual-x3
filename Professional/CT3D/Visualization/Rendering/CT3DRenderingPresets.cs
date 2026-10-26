using CxVisual.Foundation.Volume;
using System;

namespace CxVisual.Professional.CT3D
{
    // 预设提供稳定的基础外观，可编辑曲线通过独立开关覆盖对应通道。
    /// <summary>第一阶段提供的基础体渲染外观；可编辑传递函数仍由独立开关决定是否覆盖它。</summary>
    public enum CT3DRenderingPresetKind
    {
        LinearGrayscale,
        SlicerLikeGrayscale
    }

    public static class CT3DRenderingPresets
    {
        public static float EvaluateBaseOpacity(CT3DRenderingPresetKind preset, float scalar)
        {
            return EvaluateBaseOpacity(preset, scalar, FVOVolumeIntensityProfile.FullRange);
        }

        public static float EvaluateBaseOpacity(
            CT3DRenderingPresetKind preset,
            float scalar,
            FVOVolumeIntensityProfile intensityProfile)
        {
            scalar = Clamp(scalar, 0.0f, 1.0f);
            if (preset == CT3DRenderingPresetKind.LinearGrayscale)
            {
                return scalar;
            }

            float displayScalar = intensityProfile.Normalize(scalar);
            if (displayScalar <= 0.0f)
            {
                return 0.0f;
            }

            // RGBA8 的 alpha 最小有效步进约为 1/255；曲线不能再使用会被量化成 0 的透明度。
            if (displayScalar <= 0.12f)
            {
                return Lerp(0.0f, 0.006f, displayScalar / 0.12f);
            }

            if (displayScalar <= 0.45f)
            {
                return Lerp(0.006f, 0.020f, (displayScalar - 0.12f) / 0.33f);
            }

            if (displayScalar <= 0.75f)
            {
                return Lerp(0.020f, 0.060f, (displayScalar - 0.45f) / 0.30f);
            }

            return Lerp(0.060f, 0.14f, (displayScalar - 0.75f) / 0.25f);
        }

        public static float EvaluateBaseGray(
            CT3DRenderingPresetKind preset,
            float scalar,
            FVOVolumeIntensityProfile intensityProfile)
        {
            scalar = Clamp(scalar, 0.0f, 1.0f);
            if (preset == CT3DRenderingPresetKind.LinearGrayscale)
            {
                return scalar;
            }

            float displayScalar = intensityProfile.Normalize(scalar);
            if (displayScalar <= 0.0f)
            {
                return 0.0f;
            }

            // 提升低灰结构的可见度；透明度仍负责区分背景与实体，不再让原始低灰被光照二次压黑。
            return 0.20f + 0.80f * (float)Math.Pow(displayScalar, 0.62f);
        }

        private static float Clamp(float value, float min, float max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        private static float Lerp(float left, float right, float amount)
        {
            return left + (right - left) * Clamp(amount, 0.0f, 1.0f);
        }
    }
}
