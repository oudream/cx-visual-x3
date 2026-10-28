using System;
using System.Collections.Generic;
using CxVisual.Foundation.Data.Types;
using CxVisual.Foundation.Imaging;

namespace CxVisual.Professional.Vision
{
    /// <summary>
    /// FIM二维灰度缓冲与现有Vision CPU帧/RAW数据流的显式安全桥接。
    /// 不持有外部指针、不改变输入像素，也不建立第二套图像或执行体系。
    /// </summary>
    public static class CxVisionImageInterop
    {
        private const long MaximumFramePixels = 32000000L;

        /// <summary>仅接受现有专业CPU检测链真正支持的Gray8和Gray16格式。</summary>
        public static CxVisionPixelDepth GetPixelDepth(FIMImageBuffer image)
        {
            if (image == null) throw new ArgumentNullException("image");
            switch (image.PixelFormat)
            {
                case FIMPixelFormat.Gray8: return CxVisionPixelDepth.Bits8;
                case FIMPixelFormat.Gray16: return CxVisionPixelDepth.Bits16;
                default:
                    throw new NotSupportedException(
                        "Vision CPU灰度检测当前仅支持FIM Gray8/Gray16；不得隐式降位深或转换彩色图像。");
            }
        }

        /// <summary>
        /// 将带Stride的FIM图像复制为紧排的专业灰度像素快照；
        /// 跳过每行Padding，16位按FIM原有小端语义解释。
        /// </summary>
        public static CxVisionGrayFrame ToGrayFrame(FIMImageBuffer image)
        {
            CxVisionPixelDepth depth = GetPixelDepth(image);
            int count = CheckPixelCount(image);
            byte[] source = image.CopyPixels();
            ushort[] pixels = new ushort[count];
            int bytesPerPixel = depth == CxVisionPixelDepth.Bits8 ? 1 : 2;
            int index = 0;
            for (int y = 0; y < image.Height; ++y)
            {
                int row = checked(y * image.Stride);
                for (int x = 0; x < image.Width; ++x)
                {
                    int offset = row + x * bytesPerPixel;
                    pixels[index++] = bytesPerPixel == 1 ? source[offset] :
                        (ushort)(source[offset] | (source[offset + 1] << 8));
                }
            }
            return new CxVisionGrayFrame(image.Width, image.Height, depth, pixels);
        }

        /// <summary>
        /// 专业帧转FIM的安全像素快照；不暴露CxVisionGrayFrame内部ushort数组。
        /// </summary>
        public static FIMImageBuffer ToImageBuffer(CxVisionGrayFrame frame)
        {
            if (frame == null) throw new ArgumentNullException("frame");
            int bytesPerPixel = frame.PixelDepth == CxVisionPixelDepth.Bits8 ? 1 : 2;
            byte[] pixels = new byte[checked(frame.Width * frame.Height * bytesPerPixel)];
            for (int i = 0; i < frame.Width * frame.Height; ++i)
            {
                ushort value = frame.PixelAt(i);
                int offset = i * bytesPerPixel;
                pixels[offset] = (byte)value;
                if (bytesPerPixel == 2) pixels[offset + 1] = (byte)(value >> 8);
            }
            return new FIMImageBuffer(frame.Width, frame.Height,
                bytesPerPixel == 1 ? FIMPixelFormat.Gray8 : FIMPixelFormat.Gray16, pixels);
        }

        /// <summary>
        /// 为现有CxVisionDataFlow.CreateCpuInspectionPlan生成严格类型的输入。
        /// 输入RawPixels由FDTReadOnlyBuffer防御复制，不会暴露FIM原像素或行Padding。
        /// 本方法只准备数据；MRK计划仍由既有Vision DataFlow创建及执行。
        /// </summary>
        public static IDictionary<string, object> ToCpuInspectionInputs(
            FIMImageBuffer image, CxVisionPixelDepth expectedDepth)
        {
            CxVisionPixelDepth depth = GetPixelDepth(image);
            if (depth != expectedDepth)
                throw new ArgumentException("图像位深与已编译CPU检测Plan配置不一致", "expectedDepth");
            CheckPixelCount(image);
            int rowBytes = checked(image.Width * (depth == CxVisionPixelDepth.Bits8 ? 1 : 2));
            byte[] source = image.CopyPixels();
            byte[] packed;
            if (image.Stride == rowBytes) packed = source;
            else
            {
                packed = new byte[checked(rowBytes * image.Height)];
                for (int y = 0; y < image.Height; ++y)
                    Buffer.BlockCopy(source, checked(y * image.Stride),
                        packed, checked(y * rowBytes), rowBytes);
            }
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "RawPixels", new FDTReadOnlyBuffer(packed) },
                { "Width", image.Width },
                { "Height", image.Height }
            };
        }

        private static int CheckPixelCount(FIMImageBuffer image)
        {
            long count = (long)image.Width * image.Height;
            if (count <= 0 || count > MaximumFramePixels)
                throw new ArgumentOutOfRangeException("image",
                    "图像尺寸超过当前CxVisionGrayFrame的3200万像素上限");
            return checked((int)count);
        }
    }
}
