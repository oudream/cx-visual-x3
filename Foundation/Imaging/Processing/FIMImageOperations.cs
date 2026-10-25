using System;
using System.Collections.Generic;

namespace CxVisual.Foundation.Imaging
{
    /// <summary>基于原始像素的裁剪、拆分/拼接和显式窗映射；保留旧HALCON成熟算法语义。</summary>
    public static class FIMImageOperations
    {
        /// <summary>
        /// 对应旧HALCON ScaleImageMax/ConvertTo8Bit的通用线性范围归一化。
        /// 常量图生成全零8位图；不修改输入，避免旧算法除以(max-min)=0。
        /// </summary>
        public static FIMImageBuffer NormalizeToGray8(FIMImageBuffer image)
        {
            if (image == null) throw new ArgumentNullException("image");
            if (image.PixelFormat != FIMPixelFormat.Gray8 &&
                image.PixelFormat != FIMPixelFormat.Gray16 &&
                image.PixelFormat != FIMPixelFormat.GrayFloat32)
                throw new ArgumentException("只支持单通道原始灰度归一化。", "image");
            FIMImageStatistics stats = FIMImageAnalyzer.Calculate(image);
            if (stats.Max == stats.Min)
            {
                byte[] zero = new byte[checked(image.Width * image.Height)];
                return FIMImageBuffer.FromOwnedArray(image.Width, image.Height,
                    FIMPixelFormat.Gray8, zero, image.Width);
            }
            return WindowToGray8(image, stats.Min, stats.Max);
        }

        /// <summary>对应旧HALCON Compose3(Gray,Gray,Gray)，直接得到独立的BGR24像素缓冲。</summary>
        public static FIMImageBuffer Gray8ToBgr24(FIMImageBuffer image)
        {
            if (image == null) throw new ArgumentNullException("image");
            if (image.PixelFormat != FIMPixelFormat.Gray8)
                throw new ArgumentException("输入必须是Gray8。", "image");
            int stride = checked(image.Width * 3);
            byte[] pixels = new byte[checked(stride * image.Height)];
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                {
                    byte value = (byte)image.GetGrayValue(x, y);
                    int index = y * stride + x * 3;
                    pixels[index] = value;
                    pixels[index + 1] = value;
                    pixels[index + 2] = value;
                }
            return FIMImageBuffer.FromOwnedArray(image.Width, image.Height,
                FIMPixelFormat.Bgr24, pixels, stride);
        }

        /// <summary>对应旧HALCON Gray16ToRgb8基础语义：先归一化，再复制到BGR三个通道。</summary>
        public static FIMImageBuffer Gray16ToBgr24(FIMImageBuffer image)
        {
            if (image == null) throw new ArgumentNullException("image");
            if (image.PixelFormat != FIMPixelFormat.Gray16)
                throw new ArgumentException("输入必须是Gray16。", "image");
            return Gray8ToBgr24(NormalizeToGray8(image));
        }

        /// <summary>
        /// 对应旧HALCON OverlayImage2/3 的通用单通道均值融合。
        /// 保持原始Gray8/Gray16格式；每个输入都必须具有相同尺寸和格式。
        /// </summary>
        public static FIMImageBuffer AverageGray(IReadOnlyList<FIMImageBuffer> images)
        {
            if (images == null) throw new ArgumentNullException("images");
            if (images.Count == 0) throw new ArgumentException("输入图像为空。", "images");
            FIMImageBuffer first = images[0];
            if (first == null) throw new ArgumentException("输入包含空图像。", "images");
            if (first.PixelFormat != FIMPixelFormat.Gray8 && first.PixelFormat != FIMPixelFormat.Gray16)
                throw new ArgumentException("均值融合只支持Gray8/Gray16。", "images");
            foreach (FIMImageBuffer image in images)
            {
                if (image == null || image.Width != first.Width || image.Height != first.Height ||
                    image.PixelFormat != first.PixelFormat)
                    throw new ArgumentException("所有图像尺寸和像素格式必须一致。", "images");
            }
            int bpp = first.BytesPerPixel;
            int stride = checked(first.Width * bpp);
            byte[] result = new byte[checked(stride * first.Height)];
            int maxValue = bpp == 1 ? 255 : 65535;
            for (int y = 0; y < first.Height; y++)
                for (int x = 0; x < first.Width; x++)
                {
                    double total = 0;
                    foreach (FIMImageBuffer image in images) total += image.GetGrayValue(x, y);
                    int average = (int)Math.Round(total / images.Count, MidpointRounding.AwayFromZero);
                    average = Math.Max(0, Math.Min(maxValue, average));
                    int offset = y * stride + x * bpp;
                    result[offset] = (byte)(average & 255);
                    if (bpp == 2) result[offset + 1] = (byte)(average >> 8);
                }
            return FIMImageBuffer.FromOwnedArray(first.Width, first.Height, first.PixelFormat, result, stride);
        }

        /// <summary>按像素变换缓冲，适用于全部已支持的位深/通道，不改变原图。</summary>
        public static FIMImageBuffer Rotate90Clockwise(FIMImageBuffer image)
        {
            if (image == null) throw new ArgumentNullException("image");
            return Remap(image, image.Height, image.Width, 1);
        }
        public static FIMImageBuffer Rotate90Counterclockwise(FIMImageBuffer image)
        {
            if (image == null) throw new ArgumentNullException("image");
            return Remap(image, image.Height, image.Width, 2);
        }
        public static FIMImageBuffer FlipHorizontal(FIMImageBuffer image)
        {
            if (image == null) throw new ArgumentNullException("image");
            return Remap(image, image.Width, image.Height, 3);
        }
        public static FIMImageBuffer FlipVertical(FIMImageBuffer image)
        {
            if (image == null) throw new ArgumentNullException("image");
            return Remap(image, image.Width, image.Height, 4);
        }

        private static FIMImageBuffer Remap(FIMImageBuffer image, int newWidth, int newHeight, int kind)
        {
            int bytes = image.BytesPerPixel;
            int stride = checked(newWidth * bytes);
            byte[] source = image.CopyPixels();
            byte[] target = new byte[checked(stride * newHeight)];
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                {
                    int nx = x, ny = y;
                    if (kind == 1) { nx = image.Height - y - 1; ny = x; }
                    else if (kind == 2) { nx = y; ny = image.Width - x - 1; }
                    else if (kind == 3) { nx = image.Width - x - 1; }
                    else if (kind == 4) { ny = image.Height - y - 1; }
                    Buffer.BlockCopy(source, y * image.Stride + x * bytes,
                        target, ny * stride + nx * bytes, bytes);
                }
            return FIMImageBuffer.FromOwnedArray(newWidth, newHeight,
                image.PixelFormat, target, stride);
        }

        /// <summary>原始灰度阈值范围，返回0/255的独立Gray8掩码。</summary>
        public static FIMImageBuffer Threshold(FIMImageBuffer image, double low, double high)
        {
            if (image == null) throw new ArgumentNullException("image");
            if (double.IsNaN(low) || double.IsInfinity(low) ||
                double.IsNaN(high) || double.IsInfinity(high) || high < low)
                throw new ArgumentOutOfRangeException("high");
            byte[] result = new byte[checked(image.Width * image.Height)];
            int index = 0;
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                {
                    double value = image.GetGrayValue(x, y);
                    result[index++] = !double.IsNaN(value) && value >= low && value <= high ?
                        (byte)255 : (byte)0;
                }
            return FIMImageBuffer.FromOwnedArray(image.Width, image.Height,
                FIMPixelFormat.Gray8, result, image.Width);
        }

        /// <summary>同位深Gray8/Gray16图像绝对差分，不丢失16位原始动态范围。</summary>
        public static FIMImageBuffer DifferenceGray(FIMImageBuffer original, FIMImageBuffer result)
        {
            if (original == null) throw new ArgumentNullException("original");
            if (result == null) throw new ArgumentNullException("result");
            if ((original.PixelFormat != FIMPixelFormat.Gray8 &&
                 original.PixelFormat != FIMPixelFormat.Gray16) ||
                original.PixelFormat != result.PixelFormat ||
                original.Width != result.Width || original.Height != result.Height)
                throw new ArgumentException("差分图必须同尺寸、同Gray8或Gray16格式。", "result");
            int bpp = original.BytesPerPixel;
            int stride = checked(original.Width * bpp);
            byte[] output = new byte[checked(stride * original.Height)];
            for (int y = 0; y < original.Height; y++)
                for (int x = 0; x < original.Width; x++)
                {
                    int delta = (int)Math.Abs(original.GetGrayValue(x,y) - result.GetGrayValue(x,y));
                    int i = y * stride + x * bpp;
                    output[i] = (byte)(delta & 255);
                    if (bpp == 2) output[i+1] = (byte)(delta >> 8);
                }
            return FIMImageBuffer.FromOwnedArray(original.Width, original.Height,
                original.PixelFormat, output, stride);
        }

        public static FIMImageBuffer Crop(FIMImageBuffer image, FIMPixelRegion region)
        {
            if (image == null) throw new ArgumentNullException("image");
            if (region.Right >= image.Width || region.Bottom >= image.Height)
                throw new ArgumentOutOfRangeException("region");
            int rowBytes = checked(region.Width * image.BytesPerPixel);
            byte[] pixels = new byte[checked(rowBytes * region.Height)];
            for (int y = 0; y < region.Height; y++)
                image.CopyRow(region.Top + y, region.Left, region.Width, pixels, y * rowBytes);
            return FIMImageBuffer.FromOwnedArray(region.Width, region.Height, image.PixelFormat, pixels, rowBytes);
        }

        /// <summary>通过整数边界分配高度余数，保证不丢失末尾行。</summary>
        public static IReadOnlyList<FIMImageBuffer> SplitVertically(FIMImageBuffer image, int parts)
        {
            if (image == null) throw new ArgumentNullException("image");
            if (parts < 1 || parts > image.Height) throw new ArgumentOutOfRangeException("parts");
            List<FIMImageBuffer> result = new List<FIMImageBuffer>(parts);
            for (int i = 0; i < parts; i++)
            {
                int top = (int)((long)i * image.Height / parts);
                int bottom = (int)((long)(i + 1) * image.Height / parts) - 1;
                result.Add(Crop(image, new FIMPixelRegion(0, top, image.Width - 1, bottom)));
            }
            return result.AsReadOnly();
        }

        public static FIMImageBuffer MergeVertically(IReadOnlyList<FIMImageBuffer> images)
        {
            if (images == null) throw new ArgumentNullException("images");
            if (images.Count == 0) throw new ArgumentException("图像列表为空。", "images");
            FIMImageBuffer first = images[0];
            if (first == null) throw new ArgumentException("存在空图。", "images");
            int height = 0;
            foreach (FIMImageBuffer image in images)
            {
                if (image == null || image.Width != first.Width || image.PixelFormat != first.PixelFormat)
                    throw new ArgumentException("宽度或像素格式不一致。", "images");
                height = checked(height + image.Height);
            }
            int rowBytes = checked(first.Width * first.BytesPerPixel);
            byte[] pixels = new byte[checked(rowBytes * height)];
            int row = 0;
            foreach (FIMImageBuffer image in images)
                for (int y = 0; y < image.Height; y++)
                    image.CopyRow(y, 0, image.Width, pixels, row++ * rowBytes);
            return FIMImageBuffer.FromOwnedArray(first.Width, height, first.PixelFormat, pixels, rowBytes);
        }

        /// <summary>仅明确显示时生成8位副本，原始统计不会被显示窗宽窗位影响。</summary>
        public static FIMImageBuffer WindowToGray8(FIMImageBuffer image, double low, double high)
        {
            if (image == null) throw new ArgumentNullException("image");
            if (double.IsNaN(low) || double.IsInfinity(low) || double.IsNaN(high) ||
                double.IsInfinity(high) || high <= low)
                throw new ArgumentOutOfRangeException("high");
            byte[] pixels = new byte[checked(image.Width * image.Height)];
            int index = 0;
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                {
                    double gray = image.GetGrayValue(x, y);
                    if (double.IsNaN(gray) || double.IsInfinity(gray))
                        throw new InvalidOperationException("图像包含非有限像素。");
                    double scaled = (gray - low) / (high - low) * 255.0;
                    pixels[index++] = scaled <= 0 ? (byte)0 : scaled >= 255 ? (byte)255 :
                        (byte)Math.Round(scaled, MidpointRounding.AwayFromZero);
                }
            return FIMImageBuffer.FromOwnedArray(image.Width, image.Height, FIMPixelFormat.Gray8,
                pixels, image.Width);
        }
    }
}
