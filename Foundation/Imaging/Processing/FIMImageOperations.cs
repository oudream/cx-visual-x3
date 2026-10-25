using System;
using System.Collections.Generic;

namespace CxVisual.Foundation.Imaging
{
    /// <summary>基于原始像素的裁剪、拆分/拼接和显式窗映射；保留旧HALCON成熟算法语义。</summary>
    public static class FIMImageOperations
    {
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
