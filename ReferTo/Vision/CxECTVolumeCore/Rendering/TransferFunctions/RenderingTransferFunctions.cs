using CxECTVolumeCore.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace CxECTVolumeCore.Rendering
{
    // 传递函数在 CPU 侧编辑并烘焙，GPU 侧只读取一维纹理。
    /// <summary>传递函数中的一个归一化控制点。</summary>
    public sealed class RenderingTransferPoint
    {
        public RenderingTransferPoint(float x, float y, Color color)
        {
            X = FrameworkCompat.Clamp(x, 0.0f, 1.0f);
            Y = FrameworkCompat.Clamp(y, 0.0f, 1.0f);
            Color = color;
        }

        public float X { get; set; }

        public float Y { get; set; }

        public Color Color { get; set; }
    }

    /// <summary>管理标量或梯度到不透明度的分段线性映射。</summary>
    public sealed class RenderingOpacityTransferFunction
    {
        public RenderingOpacityTransferFunction(IEnumerable<RenderingTransferPoint> points)
        {
            Points = points.OrderBy(point => point.X).ToList();
        }

        public List<RenderingTransferPoint> Points { get; private set; }

        public float Evaluate(float x)
        {
            if (Points.Count == 0)
            {
                return 0.0f;
            }

            x = FrameworkCompat.Clamp(x, 0.0f, 1.0f);
            if (x <= Points[0].X)
            {
                return Points[0].Y;
            }

            for (var i = 1; i < Points.Count; i++)
            {
                var left = Points[i - 1];
                var right = Points[i];
                if (x <= right.X)
                {
                    // 控制点之间用线性插值，UI 曲线、CPU 烘焙和 shader 查表保持同一套语义。
                    var span = Math.Max(0.0001f, right.X - left.X);
                    var t = (x - left.X) / span;
                    return left.Y + (right.Y - left.Y) * t;
                }
            }

            return Points[Points.Count - 1].Y;
        }
    }

    /// <summary>管理标量到 RGB 颜色的分段线性映射。</summary>
    public sealed class RenderingColorTransferFunction
    {
        public RenderingColorTransferFunction(IEnumerable<RenderingTransferPoint> points)
        {
            Points = points.OrderBy(point => point.X).ToList();
        }

        public List<RenderingTransferPoint> Points { get; private set; }

        public Color Evaluate(float x)
        {
            if (Points.Count == 0)
            {
                return Color.White;
            }

            x = FrameworkCompat.Clamp(x, 0.0f, 1.0f);
            if (x <= Points[0].X)
            {
                return Points[0].Color;
            }

            for (var i = 1; i < Points.Count; i++)
            {
                var left = Points[i - 1];
                var right = Points[i];
                if (x <= right.X)
                {
                    var span = Math.Max(0.0001f, right.X - left.X);
                    var t = (x - left.X) / span;
                    return Color.FromArgb(
                        Lerp(left.Color.R, right.Color.R, t),
                        Lerp(left.Color.G, right.Color.G, t),
                        Lerp(left.Color.B, right.Color.B, t));
                }
            }

            return Points[Points.Count - 1].Color;
        }

        private static int Lerp(int left, int right, float t)
        {
            return (int)Math.Round(left + (right - left) * t);
        }
    }

    /// <summary>组合阈值、颜色、标量不透明度和梯度不透明度，并烘焙为 GPU 纹理数据。</summary>
    public sealed class RenderingTransferFunctionSet
    {
        public RenderingTransferFunctionSet()
        {
            Threshold = 0.28f;
            UpperThreshold = 1.0f;
            Color = new RenderingColorTransferFunction(new[]
            {
                new RenderingTransferPoint(0.0f, 0.5f, System.Drawing.Color.FromArgb(18, 36, 72)),
                new RenderingTransferPoint(0.55f, 0.5f, System.Drawing.Color.FromArgb(230, 194, 68)),
                new RenderingTransferPoint(1.0f, 0.5f, System.Drawing.Color.White),
            });
            ScalarOpacity = new RenderingOpacityTransferFunction(new[]
            {
                new RenderingTransferPoint(0.0f, 0.0f, System.Drawing.Color.White),
                new RenderingTransferPoint(0.28f, 0.0f, System.Drawing.Color.White),
                new RenderingTransferPoint(0.55f, 0.42f, System.Drawing.Color.White),
                new RenderingTransferPoint(1.0f, 0.92f, System.Drawing.Color.White),
            });
            GradientOpacity = new RenderingOpacityTransferFunction(new[]
            {
                new RenderingTransferPoint(0.0f, 0.0f, System.Drawing.Color.White),
                new RenderingTransferPoint(0.16f, 0.18f, System.Drawing.Color.White),
                new RenderingTransferPoint(1.0f, 1.0f, System.Drawing.Color.White),
            });
        }

        public float Threshold { get; set; }

        /// <summary>参与体渲染的标量上限；Threshold 为下限。</summary>
        public float UpperThreshold { get; set; }

        // 四类传递函数默认均不参与渲染；启用后才用编辑器中的曲线覆盖中性映射。
        public bool UseThreshold { get; set; }

        public bool UseColor { get; set; }

        public bool UseScalarOpacity { get; set; }

        public bool UseGradientOpacity { get; set; }

        public RenderingColorTransferFunction Color { get; private set; }

        public RenderingOpacityTransferFunction ScalarOpacity { get; private set; }

        public RenderingOpacityTransferFunction GradientOpacity { get; private set; }

        public byte[] BuildScalarTransferTexture(RenderingPresetKind preset)
        {
            return BuildScalarTransferTexture(preset, RenderingIntensityProfile.FullRange, 1.0f);
        }

        public byte[] BuildScalarTransferTexture(
            RenderingPresetKind preset,
            RenderingIntensityProfile intensityProfile)
        {
            return BuildScalarTransferTexture(preset, intensityProfile, 1.0f);
        }

        public byte[] BuildScalarTransferTexture(
            RenderingPresetKind preset,
            RenderingIntensityProfile intensityProfile,
            float presetOpacityScale)
        {
            // shader 每一步 raymarch 只做 texture lookup；复杂的颜色和透明度曲线在 CPU 侧预先烘焙成 1D RGBA 纹理。
            var pixels = new byte[256 * 4];
            // 对外调用者可能直接赋值，生成纹理时仍要容忍上下限颠倒。
            var lowerThreshold = Math.Min(Threshold, UpperThreshold);
            var upperThreshold = Math.Max(Threshold, UpperThreshold);
            var effectivePresetOpacityScale = preset == RenderingPresetKind.SlicerLikeGrayscale
                ? FrameworkCompat.Clamp(presetOpacityScale, 0.0f, 4.0f)
                : 1.0f;
            for (var i = 0; i < 256; i++)
            {
                var x = i / 255.0f;
                Color color = UseColor
                    ? Color.Evaluate(x)
                    : ToGrayColor(RenderingTransferFunctionPresets.EvaluateBaseGray(preset, x, intensityProfile));
                float opacity = UseScalarOpacity
                    ? ScalarOpacity.Evaluate(x)
                    : RenderingTransferFunctionPresets.EvaluateBaseOpacity(preset, x, intensityProfile)
                      * effectivePresetOpacityScale;
                if (UseThreshold && (x < lowerThreshold || x > upperThreshold))
                {
                    opacity = 0.0f;
                }
                var offset = i * 4;
                pixels[offset + 0] = color.R;
                pixels[offset + 1] = color.G;
                pixels[offset + 2] = color.B;
                pixels[offset + 3] = (byte)Math.Round(FrameworkCompat.Clamp(opacity, 0.0f, 1.0f) * 255.0f);
            }

            return pixels;
        }

        public byte[] BuildGradientOpacityTexture()
        {
            // 梯度透明度独立成 R8 纹理，用来强调边缘/结构，避免均匀背景把 3D 细节淹没。
            var pixels = new byte[256];
            for (var i = 0; i < 256; i++)
            {
                var opacity = UseGradientOpacity ? GradientOpacity.Evaluate(i / 255.0f) : 1.0f;
                pixels[i] = (byte)Math.Round(FrameworkCompat.Clamp(opacity, 0.0f, 1.0f) * 255.0f);
            }

            return pixels;
        }

        private static System.Drawing.Color ToGrayColor(float scalar)
        {
            int gray = (int)Math.Round(FrameworkCompat.Clamp(scalar, 0.0f, 1.0f) * 255.0f);
            return System.Drawing.Color.FromArgb(gray, gray, gray);
        }
    }
}
