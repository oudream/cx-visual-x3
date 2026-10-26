using System;
using System.Collections.Generic;
using System.Linq;

namespace CxVisual.Foundation.Volume
{
    /// <summary>RGB数值；Foundation不公开WinForms/GDI或厂商颜色句柄。</summary>
    public struct FVOColor
    {
        public FVOColor(byte red, byte green, byte blue)
        {
            R = red; G = green; B = blue;
        }
        public byte R { get; private set; }
        public byte G { get; private set; }
        public byte B { get; private set; }
        public static FVOColor White { get { return new FVOColor(255, 255, 255); } }
        public static FVOColor Gray(byte value) { return new FVOColor(value, value, value); }
    }

    public sealed class FVOTransferPoint
    {
        public FVOTransferPoint(float x, float y, FVOColor color)
        {
            X = Math.Min(1f, Math.Max(0f, x));
            Y = Math.Min(1f, Math.Max(0f, y));
            Color = color;
        }
        public float X { get; set; }
        public float Y { get; set; }
        public FVOColor Color { get; set; }
    }

    /// <summary>延续旧版在控制点之间作线性插值的标量/梯度透明度。</summary>
    public sealed class FVOOpacityTransferFunction
    {
        public FVOOpacityTransferFunction(IEnumerable<FVOTransferPoint> points)
        {
            if (points == null) throw new ArgumentNullException("points");
            Points = points.OrderBy(p => p.X).ToList();
        }
        public List<FVOTransferPoint> Points { get; private set; }
        public float Evaluate(float value)
        {
            if (Points.Count == 0) return 0f;
            float x = Math.Min(1f, Math.Max(0f, value));
            if (x <= Points[0].X) return Points[0].Y;
            for (int i = 1; i < Points.Count; i++)
            {
                FVOTransferPoint left = Points[i - 1], right = Points[i];
                if (x <= right.X)
                {
                    float span = Math.Max(0.0001f, right.X - left.X);
                    return left.Y + (right.Y - left.Y) * ((x - left.X) / span);
                }
            }
            return Points[Points.Count - 1].Y;
        }
    }

    public sealed class FVOColorTransferFunction
    {
        public FVOColorTransferFunction(IEnumerable<FVOTransferPoint> points)
        {
            if (points == null) throw new ArgumentNullException("points");
            Points = points.OrderBy(p => p.X).ToList();
        }
        public List<FVOTransferPoint> Points { get; private set; }
        public FVOColor Evaluate(float value)
        {
            if (Points.Count == 0) return FVOColor.White;
            float x = Math.Min(1f, Math.Max(0f, value));
            if (x <= Points[0].X) return Points[0].Color;
            for (int i = 1; i < Points.Count; i++)
            {
                FVOTransferPoint left = Points[i - 1], right = Points[i];
                if (x <= right.X)
                {
                    float fraction = (x - left.X) / Math.Max(0.0001f, right.X - left.X);
                    return new FVOColor(
                        (byte)Math.Round(left.Color.R + (right.Color.R - left.Color.R) * fraction),
                        (byte)Math.Round(left.Color.G + (right.Color.G - left.Color.G) * fraction),
                        (byte)Math.Round(left.Color.B + (right.Color.B - left.Color.B) * fraction));
                }
            }
            return Points[Points.Count - 1].Color;
        }
    }

    /// <summary>通用传递曲线及RGBA/R8烘焙；预设算法由上层CT3D直接提供函数。</summary>
    public sealed class FVOTransferFunctionSet
    {
        public FVOTransferFunctionSet()
        {
            Threshold = 0.28f;
            UpperThreshold = 1f;
            Color = new FVOColorTransferFunction(new[]
            {
                new FVOTransferPoint(0f, 0.5f, new FVOColor(18, 36, 72)),
                new FVOTransferPoint(0.55f, 0.5f, new FVOColor(230, 194, 68)),
                new FVOTransferPoint(1f, 0.5f, FVOColor.White)
            });
            ScalarOpacity = new FVOOpacityTransferFunction(new[]
            {
                new FVOTransferPoint(0f, 0f, FVOColor.White),
                new FVOTransferPoint(0.28f, 0f, FVOColor.White),
                new FVOTransferPoint(0.55f, 0.42f, FVOColor.White),
                new FVOTransferPoint(1f, 0.92f, FVOColor.White)
            });
            GradientOpacity = new FVOOpacityTransferFunction(new[]
            {
                new FVOTransferPoint(0f, 0f, FVOColor.White),
                new FVOTransferPoint(0.16f, 0.18f, FVOColor.White),
                new FVOTransferPoint(1f, 1f, FVOColor.White)
            });
        }

        public float Threshold { get; set; }
        public float UpperThreshold { get; set; }
        public bool UseThreshold { get; set; }
        public bool UseColor { get; set; }
        public bool UseScalarOpacity { get; set; }
        public bool UseGradientOpacity { get; set; }
        public FVOColorTransferFunction Color { get; private set; }
        public FVOOpacityTransferFunction ScalarOpacity { get; private set; }
        public FVOOpacityTransferFunction GradientOpacity { get; private set; }

        /// <summary>RGBA8纹理逐档256采样；专业默认灰度/透明度通过函数参数直接组合。</summary>
        public byte[] BuildScalarTransferTexture(Func<float, float> defaultGray,
            Func<float, float> defaultOpacity, float presetOpacityScale)
        {
            if (defaultGray == null) throw new ArgumentNullException("defaultGray");
            if (defaultOpacity == null) throw new ArgumentNullException("defaultOpacity");
            float lo = Math.Min(Threshold, UpperThreshold);
            float hi = Math.Max(Threshold, UpperThreshold);
            float scale = Clamp(presetOpacityScale, 0f, 4f);
            byte[] pixels = new byte[256 * 4];
            for (int i = 0; i < 256; i++)
            {
                float x = i / 255f;
                FVOColor color = UseColor ? Color.Evaluate(x) : ToGrayColor(defaultGray(x));
                float opacity = UseScalarOpacity ? ScalarOpacity.Evaluate(x) : defaultOpacity(x) * scale;
                if (UseThreshold && (x < lo || x > hi)) opacity = 0f;
                int offset = i * 4;
                pixels[offset] = color.R;
                pixels[offset + 1] = color.G;
                pixels[offset + 2] = color.B;
                pixels[offset + 3] = (byte)Math.Round(Clamp(opacity, 0f, 1f) * 255f);
            }
            return pixels;
        }

        public byte[] BuildGradientOpacityTexture()
        {
            byte[] pixels = new byte[256];
            for (int i = 0; i < 256; i++)
            {
                float opacity = UseGradientOpacity ? GradientOpacity.Evaluate(i / 255f) : 1f;
                pixels[i] = (byte)Math.Round(Clamp(opacity, 0f, 1f) * 255f);
            }
            return pixels;
        }

        private static FVOColor ToGrayColor(float value)
        {
            byte gray = (byte)Math.Round(Clamp(value, 0f, 1f) * 255f);
            return FVOColor.Gray(gray);
        }
        private static float Clamp(float v, float lower, float upper)
        {
            return Math.Max(lower, Math.Min(upper, v));
        }
    }
}
