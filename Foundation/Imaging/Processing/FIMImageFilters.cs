using System;
using System.Threading;

namespace CxVisual.Foundation.Imaging
{
    public enum FIMNeighborhoodFilter
    {
        Mean,
        Median,
        Minimum,
        Maximum
    }

    /// <summary>
    /// 从传统视觉的去噪/形态学基础操作提炼纯标量算法。
    /// Gray8/Gray16/GrayFloat32可用；位深保持，边界采用复制最近像素；
    /// 本类是准确性CPU基线，不声称已达到高吞吐/GPU实时性能。
    /// </summary>
    public static class FIMImageFilters
    {
        public static FIMImageBuffer Apply(FIMImageBuffer image, FIMNeighborhoodFilter filter,
            int radius, CancellationToken cancellationToken = default(CancellationToken))
        {
            Validate(image, radius);
            if (!Enum.IsDefined(typeof(FIMNeighborhoodFilter), filter))
                throw new ArgumentOutOfRangeException("filter");
            int width = image.Width, height = image.Height;
            int size = checked(radius * 2 + 1), count = checked(size * size);
            int bpp = image.BytesPerPixel, stride = checked(width * bpp);
            byte[] output = new byte[checked(stride * height)];
            double[] samples = new double[count];
            for (int y = 0; y < height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (int x = 0; x < width; x++)
                {
                    int n = 0;
                    double sum = 0, minimum = double.PositiveInfinity, maximum = double.NegativeInfinity;
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        int py = Clamp(y + dy, 0, height - 1);
                        for (int dx = -radius; dx <= radius; dx++)
                        {
                            double value = image.GetGrayValue(Clamp(x + dx, 0, width - 1), py);
                            if (double.IsNaN(value) || double.IsInfinity(value))
                                throw new InvalidOperationException("滤波区域包含无效浮点像素。");
                            samples[n++] = value;
                            sum += value;
                            minimum = Math.Min(minimum, value);
                            maximum = Math.Max(maximum, value);
                        }
                    }
                    double result;
                    switch (filter)
                    {
                        case FIMNeighborhoodFilter.Mean: result = sum / count; break;
                        case FIMNeighborhoodFilter.Minimum: result = minimum; break;
                        case FIMNeighborhoodFilter.Maximum: result = maximum; break;
                        default:
                            Array.Sort(samples, 0, count);
                            result = samples[count / 2];
                            break;
                    }
                    Write(output, y * stride + x * bpp, image.PixelFormat, result);
                }
            }
            return FIMImageBuffer.FromOwnedArray(width, height, image.PixelFormat, output, stride);
        }

        /// <summary>Gaussian基于可调Sigma与半径的真实二维卷积，不改变源像素。</summary>
        public static FIMImageBuffer GaussianBlur(FIMImageBuffer image, double sigma, int radius,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Validate(image, radius);
            if (double.IsNaN(sigma) || double.IsInfinity(sigma) || sigma <= 0)
                throw new ArgumentOutOfRangeException("sigma");
            int size = radius * 2 + 1;
            double[] kernel = new double[size];
            double sum = 0;
            for (int k = -radius; k <= radius; k++)
            {
                kernel[k + radius] = Math.Exp(-(double)k * k / (2 * sigma * sigma));
                sum += kernel[k + radius];
            }
            for (int i = 0; i < size; i++) kernel[i] /= sum;

            int width = image.Width, height = image.Height, bpp = image.BytesPerPixel;
            // 可分离卷积：水平临时行与垂直卷积，成本O(WH*radius)。
            double[] horizontal = new double[checked(width * height)];
            for (int y = 0; y < height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (int x = 0; x < width; x++)
                {
                    double value = 0;
                    for (int k = -radius; k <= radius; k++)
                    {
                        double px = image.GetGrayValue(Clamp(x + k, 0, width - 1), y);
                        if (double.IsNaN(px) || double.IsInfinity(px))
                            throw new InvalidOperationException("高斯滤波输入包含非有限灰度值。");
                        value += px * kernel[k + radius];
                    }
                    horizontal[y * width + x] = value;
                }
            }
            int stride = checked(width * bpp);
            byte[] result = new byte[checked(stride * height)];
            for (int y = 0; y < height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (int x = 0; x < width; x++)
                {
                    double value = 0;
                    for (int k = -radius; k <= radius; k++)
                        value += horizontal[Clamp(y + k, 0, height - 1) * width + x] * kernel[k + radius];
                    Write(result, y * stride + x * bpp, image.PixelFormat, value);
                }
            }
            return FIMImageBuffer.FromOwnedArray(width, height, image.PixelFormat, result, stride);
        }

        /// <summary>真实反遮罩锐化：source + amount*(source - GaussianBlur(source))。</summary>
        public static FIMImageBuffer UnsharpMask(FIMImageBuffer image, double sigma, int radius,
            double amount, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (double.IsNaN(amount) || double.IsInfinity(amount) || amount < 0 || amount > 10)
                throw new ArgumentOutOfRangeException("amount");
            FIMImageBuffer blurred = GaussianBlur(image, sigma, radius, cancellationToken);
            int bpp = image.BytesPerPixel, stride = checked(image.Width * bpp);
            byte[] output = new byte[checked(stride * image.Height)];
            for (int y = 0; y < image.Height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (int x = 0; x < image.Width; x++)
                {
                    double source = image.GetGrayValue(x, y);
                    double result = source + amount * (source - blurred.GetGrayValue(x, y));
                    Write(output, y * stride + x * bpp, image.PixelFormat, result);
                }
            }
            return FIMImageBuffer.FromOwnedArray(image.Width, image.Height,
                image.PixelFormat, output, stride);
        }

        private static void Validate(FIMImageBuffer image, int radius)
        {
            if (image == null) throw new ArgumentNullException("image");
            if (radius < 1 || radius > 4) throw new ArgumentOutOfRangeException("radius");
            if (image.PixelFormat != FIMPixelFormat.Gray8 &&
                image.PixelFormat != FIMPixelFormat.Gray16 &&
                image.PixelFormat != FIMPixelFormat.GrayFloat32)
                throw new NotSupportedException("基础邻域滤波目前只支持Gray8/Gray16/GrayFloat32。");
        }

        private static int Clamp(int value, int min, int max)
        { return Math.Min(max, Math.Max(min, value)); }

        private static void Write(byte[] output, int offset, FIMPixelFormat format, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new InvalidOperationException("滤波结果不是有限数值。");
            if (format == FIMPixelFormat.Gray8)
            {
                output[offset] = (byte)Math.Round(Math.Min(255, Math.Max(0, value)),
                    MidpointRounding.AwayFromZero);
            }
            else if (format == FIMPixelFormat.Gray16)
            {
                ushort gray = (ushort)Math.Round(Math.Min(65535, Math.Max(0, value)),
                    MidpointRounding.AwayFromZero);
                output[offset] = (byte)(gray & 255);
                output[offset + 1] = (byte)(gray >> 8);
            }
            else
            {
                byte[] bits = BitConverter.GetBytes((float)value);
                if (!BitConverter.IsLittleEndian) Array.Reverse(bits);
                Buffer.BlockCopy(bits, 0, output, offset, 4);
            }
        }
    }
}
