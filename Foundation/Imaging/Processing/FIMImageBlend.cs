using System;

namespace CxVisual.Foundation.Imaging
{
    /// <summary>原图/结果像素加权叠合，不修改输入，不预先丢失16位或BGR通道。</summary>
    public static class FIMImageBlend
    {
        /// <summary>
        /// 共坐标系滑动比较：分界左侧显示原图、右侧显示结果图。
        /// 源图像绝不修改，支持任意相同格式（含GrayFloat32）与两图各自Stride。
        /// </summary>
        public static FIMImageBuffer Swipe(FIMImageBuffer original,FIMImageBuffer result,double leftRatio)
        {
            if (original == null) throw new ArgumentNullException("original");
            if (result == null) throw new ArgumentNullException("result");
            if (double.IsNaN(leftRatio) || double.IsInfinity(leftRatio) ||
                leftRatio < 0 || leftRatio > 1)
                throw new ArgumentOutOfRangeException("leftRatio");
            if (original.Width != result.Width || original.Height != result.Height ||
                original.PixelFormat != result.PixelFormat)
                throw new ArgumentException("滑动比较必须同尺寸且像素格式一致。","result");
            int bytes = original.BytesPerPixel;
            int rowBytes = checked(original.Width * bytes);
            int cut = (int)Math.Round(original.Width * leftRatio,MidpointRounding.AwayFromZero);
            byte[] a = original.CopyPixels(), b = result.CopyPixels();
            byte[] output = new byte[checked(original.Height * rowBytes)];
            int left = checked(cut * bytes);
            for (int y = 0; y < original.Height; y++)
            {
                if (left > 0) Buffer.BlockCopy(a,checked(y * original.Stride),
                    output,y * rowBytes,left);
                if (left < rowBytes) Buffer.BlockCopy(b,checked(y * result.Stride + left),
                    output,y * rowBytes + left,rowBytes-left);
            }
            return FIMImageBuffer.FromOwnedArray(original.Width,original.Height,
                original.PixelFormat,output,rowBytes);
        }

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
