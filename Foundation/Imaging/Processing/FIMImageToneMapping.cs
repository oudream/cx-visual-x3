using System;

namespace CxVisual.Foundation.Imaging
{
    /// <summary>
    /// 纯像素显示映射。输入只读、输出独立；不含图窗/UI/HALCON依赖。
    /// 原灰度窗映射后依次执行Contrast/Brightness、Gamma、Invert。
    /// </summary>
    public static class FIMImageToneMapping
    {
        public static FIMImageBuffer MapGray(FIMImageBuffer image, double low, double high,
            double gamma, double brightness, double contrast, bool invert)
        {
            Validate(image, low, high, gamma, brightness, contrast);
            if (image.PixelFormat != FIMPixelFormat.Gray8 &&
                image.PixelFormat != FIMPixelFormat.Gray16 &&
                image.PixelFormat != FIMPixelFormat.GrayFloat32)
                throw new ArgumentException("灰阶显示只允许单通道。", "image");
            byte[] output = new byte[checked(image.Width * image.Height)];
            int index = 0;
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                {
                    double value = image.GetGrayValue(x, y);
                    if (double.IsNaN(value) || double.IsInfinity(value))
                        throw new InvalidOperationException("包含非有限灰度数据。");
                    output[index++] = Adjust(value, low, high, gamma, brightness, contrast, invert);
                }
            return FIMImageBuffer.FromOwnedArray(image.Width, image.Height,
                FIMPixelFormat.Gray8, output, image.Width);
        }

        public static FIMImageBuffer MapColor(FIMImageBuffer image, double gamma,
            double brightness, double contrast, bool invert)
        {
            return MapColorRange(image, 0, 255, gamma, brightness, contrast, invert);
        }

        /// <summary>使用共同的RGB动态范围映射色彩，透明通道始终保留。</summary>
        public static FIMImageBuffer MapColorRange(FIMImageBuffer image, double low, double high,
            double gamma, double brightness, double contrast, bool invert)
        {
            Validate(image, low, high, gamma, brightness, contrast);
            if (image.PixelFormat != FIMPixelFormat.Bgr24 && image.PixelFormat != FIMPixelFormat.Bgra32)
                throw new ArgumentException("颜色增强要求Bgr24或Bgra32。", "image");
            byte[] input = image.CopyPixels();
            byte[] output = new byte[checked(image.Width * image.Height * 4)];
            int srcBpp = image.BytesPerPixel;
            int outputStride = checked(image.Width * 4);
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                {
                    int from = y * image.Stride + x * srcBpp;
                    int to = y * outputStride + x * 4;
                    output[to] = Adjust(input[from], low, high, gamma, brightness, contrast, invert);
                    output[to + 1] = Adjust(input[from + 1], low, high, gamma, brightness, contrast, invert);
                    output[to + 2] = Adjust(input[from + 2], low, high, gamma, brightness, contrast, invert);
                    output[to + 3] = srcBpp == 4 ? input[from + 3] : (byte)255;
                }
            return FIMImageBuffer.FromOwnedArray(image.Width, image.Height,
                FIMPixelFormat.Bgra32, output, outputStride);
        }

        private static void Validate(FIMImageBuffer image, double low, double high,
            double gamma, double brightness, double contrast)
        {
            if (image == null) throw new ArgumentNullException("image");
            if (!Finite(low) || !Finite(high) || high <= low)
                throw new ArgumentOutOfRangeException("high");
            if (!Finite(gamma) || gamma <= 0 || gamma > 20)
                throw new ArgumentOutOfRangeException("gamma");
            if (!Finite(brightness) || brightness < -1 || brightness > 1)
                throw new ArgumentOutOfRangeException("brightness");
            if (!Finite(contrast) || contrast < 0 || contrast > 16)
                throw new ArgumentOutOfRangeException("contrast");
        }
        private static bool Finite(double x) { return !double.IsNaN(x) && !double.IsInfinity(x); }
        private static byte Adjust(double value, double low, double high, double gamma,
            double brightness, double contrast, bool invert)
        {
            double normalized = (value - low) / (high - low);
            normalized = (normalized - 0.5) * contrast + 0.5 + brightness;
            normalized = Math.Max(0, Math.Min(1, normalized));
            normalized = Math.Pow(normalized, 1.0 / gamma);
            if (invert) normalized = 1.0 - normalized;
            return (byte)Math.Round(normalized * 255, MidpointRounding.AwayFromZero);
        }
    }
}
