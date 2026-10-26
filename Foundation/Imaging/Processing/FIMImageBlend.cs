using System;

namespace CxVisual.Foundation.Imaging
{
    /// <summary>原图/结果像素加权叠合，不修改输入，不预先丢失16位或BGR通道。</summary>
    public static class FIMImageBlend
    {
        public static FIMImageBuffer Blend(FIMImageBuffer original, FIMImageBuffer result, double resultWeight)
        {
            if (original == null) throw new ArgumentNullException("original");
            if (result == null) throw new ArgumentNullException("result");
            if (double.IsNaN(resultWeight) || double.IsInfinity(resultWeight) ||
                resultWeight < 0 || resultWeight > 1)
                throw new ArgumentOutOfRangeException("resultWeight");
            if (original.Width != result.Width || original.Height != result.Height ||
                original.PixelFormat != result.PixelFormat)
                throw new ArgumentException("叠合必须同尺寸且像素格式一致。", "result");
            FIMPixelFormat format = original.PixelFormat;
            if (format == FIMPixelFormat.GrayFloat32)
                throw new NotSupportedException("本阶段只支持整数灰度/彩色的原结果叠合。");

            int bytes = original.BytesPerPixel, rowBytes = checked(original.Width * bytes);
            byte[] a = original.CopyPixels(), b = result.CopyPixels();
            byte[] output = new byte[checked(rowBytes * original.Height)];
            double originalWeight = 1.0 - resultWeight;
            for (int y = 0; y < original.Height; y++)
                for (int x = 0; x < original.Width; x++)
                {
                    int pa = checked(y * original.Stride + x * bytes);
                    int pb = checked(y * result.Stride + x * bytes);
                    int po = y * rowBytes + x * bytes;
                    if (format == FIMPixelFormat.Gray16)
                    {
                        int valueA = a[pa] | (a[pa + 1] << 8);
                        int valueB = b[pb] | (b[pb + 1] << 8);
                        ushort mapped = (ushort)Math.Round(valueA * originalWeight +
                            valueB * resultWeight, MidpointRounding.AwayFromZero);
                        output[po] = (byte)(mapped & 255);
                        output[po + 1] = (byte)(mapped >> 8);
                    }
                    else
                        for (int c = 0; c < bytes; c++)
                            output[po + c] = (byte)Math.Round(a[pa + c] * originalWeight +
                                b[pb + c] * resultWeight, MidpointRounding.AwayFromZero);
                }
            return FIMImageBuffer.FromOwnedArray(original.Width, original.Height,
                format, output, rowBytes);
        }
    }
}
