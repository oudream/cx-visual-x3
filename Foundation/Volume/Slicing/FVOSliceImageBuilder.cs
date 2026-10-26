using System;
using CxVisual.Foundation.Imaging;

namespace CxVisual.Foundation.Volume
{
    /// <summary>保存二维切片位图及其在原始体素空间中的尺寸和显示比例。</summary>
    public static class FVOSliceImageBuilder
    {
        private const int MaxSliceImageAxis = 4096;

        public static FIMImageBuffer Build(FVOVolumeData volume, int sliceX, int sliceY, int sliceZ,
            FVOSlicePlane plane, bool pseudoColor, byte windowMin, byte windowMax)
        {
            if (volume == null) throw new ArgumentNullException("volume");
            int width = GetImageWidth(volume, plane);
            int height = GetImageHeight(volume, plane);
            int stride = checked(width * 3);
            byte[] bytes = BuildBufferFromDisplayVolume(volume, sliceX, sliceY, sliceZ,
                plane, pseudoColor, windowMin, windowMax, stride, width, height);
            return new FIMImageBuffer(width, height, FIMPixelFormat.Bgr24, bytes);
        }

        /// <summary>直接读取16位/浮点原始标量，不采用8位窗口反算。</summary>
        public static FIMImageBuffer BuildScalar(FVOVolumeData volume, int sliceX, int sliceY, int sliceZ,
            FVOSlicePlane plane)
        {
            if (volume == null) throw new ArgumentNullException("volume");
            int width = GetImageWidth(volume, plane);
            int height = GetImageHeight(volume, plane);
            byte[] bytes = new byte[checked(width * height * sizeof(float))];
            for (int row = 0; row < height; row++)
                for (int col = 0; col < width; col++)
                {
                    int x, y, z;
                    ImageToVoxel(volume, sliceX, sliceY, sliceZ, plane, col, row, out x, out y, out z);
                    byte[] value = BitConverter.GetBytes(volume.GetScalarByOriginalIndex(x, y, z));
                    Buffer.BlockCopy(value, 0, bytes, (row * width + col) * sizeof(float), sizeof(float));
                }
            return new FIMImageBuffer(width, height, FIMPixelFormat.GrayFloat32, bytes);
        }

        private static byte[] BuildBufferFromDisplayVolume(
            FVOVolumeData volume,
            int sliceX, int sliceY, int sliceZ,
            FVOSlicePlane plane,
            bool pseudoColor,
            byte windowMin,
            byte windowMax,
            int stride,
            int width,
            int height)
        {
            var buffer = new byte[stride * height];
            // --- 按平面选择取样方向：XY 固定 Z，XZ 固定 Y，YZ 固定 X。
            switch (plane)
            {
                case FVOSlicePlane.XY:
                    FillDisplayXySlice(buffer, stride, volume, sliceX, sliceY, sliceZ, pseudoColor, windowMin, windowMax);
                    break;
                case FVOSlicePlane.XZ:
                    FillDisplayXzSlice(buffer, stride, volume, sliceX, sliceY, sliceZ, pseudoColor, windowMin, windowMax);
                    break;
                case FVOSlicePlane.YZ:
                    FillDisplayYzSlice(buffer, stride, volume, sliceX, sliceY, sliceZ, pseudoColor, windowMin, windowMax);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("plane", plane, null);
            }

            return buffer;
        }

        private static void FillDisplayXySlice(byte[] buffer, int stride, FVOVolumeData volume, int sliceX, int sliceY, int sliceZ, bool pseudoColor, byte windowMin, byte windowMax)
        {
            // --- XY 视图：输出图像的列映射到 X，行映射到 Y，当前 sliceZ 决定深度层。
            var sourceWidth = GetSourceWidth(volume, FVOSlicePlane.XY);
            var sourceHeight = GetSourceHeight(volume, FVOSlicePlane.XY);
            var sourceZ = volume.HasHighResolutionVoxels ? FVOFrameworkCompat.Clamp(sliceZ, 0, volume.OriginalDepth - 1) : volume.OriginalToDisplayZ(sliceZ);
            var imageWidth = GetImageWidth(volume, FVOSlicePlane.XY);
            var imageHeight = GetImageHeight(volume, FVOSlicePlane.XY);

            for (var row = 0; row < imageHeight; row++)
            {
                var sourceY = MapIndex(row, imageHeight, sourceHeight);
                var rowStart = row * stride;
                for (var col = 0; col < imageWidth; col++)
                {
                    var sourceX = MapIndex(col, imageWidth, sourceWidth);
                    var value = volume.HasHighResolutionVoxels
                        ? volume.GetVoxelByOriginalIndex(sourceX, sourceY, sourceZ)
                        : volume.GetVoxelByDisplayIndex(sourceX, sourceY, sourceZ);
                    WriteDisplayPixel(buffer, rowStart + col * 3, value, pseudoColor, windowMin, windowMax);
                }
            }
        }

        private static void FillDisplayXzSlice(byte[] buffer, int stride, FVOVolumeData volume, int sliceX, int sliceY, int sliceZ, bool pseudoColor, byte windowMin, byte windowMax)
        {
            // --- XZ 视图：输出图像的列映射到 X，行映射到 Z，当前 sliceY 决定截面位置。
            var sourceWidth = GetSourceWidth(volume, FVOSlicePlane.XZ);
            var sourceHeight = GetSourceHeight(volume, FVOSlicePlane.XZ);
            var sourceY = volume.HasHighResolutionVoxels ? FVOFrameworkCompat.Clamp(sliceY, 0, volume.OriginalHeight - 1) : volume.OriginalToDisplayY(sliceY);
            var imageWidth = GetImageWidth(volume, FVOSlicePlane.XZ);
            var imageHeight = GetImageHeight(volume, FVOSlicePlane.XZ);

            for (var row = 0; row < imageHeight; row++)
            {
                var sourceZ = MapIndex(row, imageHeight, sourceHeight);
                var rowStart = row * stride;
                for (var col = 0; col < imageWidth; col++)
                {
                    var sourceX = MapIndex(col, imageWidth, sourceWidth);
                    var value = volume.HasHighResolutionVoxels
                        ? volume.GetVoxelByOriginalIndex(sourceX, sourceY, sourceZ)
                        : volume.GetVoxelByDisplayIndex(sourceX, sourceY, sourceZ);
                    WriteDisplayPixel(buffer, rowStart + col * 3, value, pseudoColor, windowMin, windowMax);
                }
            }
        }

        private static void FillDisplayYzSlice(byte[] buffer, int stride, FVOVolumeData volume, int sliceX, int sliceY, int sliceZ, bool pseudoColor, byte windowMin, byte windowMax)
        {
            // --- YZ 视图：输出图像的列映射到 Y，行映射到 Z，当前 sliceX 决定截面位置。
            var sourceWidth = GetSourceWidth(volume, FVOSlicePlane.YZ);
            var sourceHeight = GetSourceHeight(volume, FVOSlicePlane.YZ);
            var sourceX = volume.HasHighResolutionVoxels ? FVOFrameworkCompat.Clamp(sliceX, 0, volume.OriginalWidth - 1) : volume.OriginalToDisplayX(sliceX);
            var imageWidth = GetImageWidth(volume, FVOSlicePlane.YZ);
            var imageHeight = GetImageHeight(volume, FVOSlicePlane.YZ);

            for (var row = 0; row < imageHeight; row++)
            {
                var sourceZ = MapIndex(row, imageHeight, sourceHeight);
                var rowStart = row * stride;
                for (var col = 0; col < imageWidth; col++)
                {
                    var sourceY = MapIndex(col, imageWidth, sourceWidth);
                    var value = volume.HasHighResolutionVoxels
                        ? volume.GetVoxelByOriginalIndex(sourceX, sourceY, sourceZ)
                        : volume.GetVoxelByDisplayIndex(sourceX, sourceY, sourceZ);
                    WriteDisplayPixel(buffer, rowStart + col * 3, value, pseudoColor, windowMin, windowMax);
                }
            }
        }

        private static void WriteDisplayPixel(byte[] buffer, int offset, byte value, bool pseudoColor, byte windowMin, byte windowMax)
        {
            var displayValue = ApplyWindow(value, windowMin, windowMax);
            if (!pseudoColor)
            {
                // --- #加速加载-8：灰度切片直接写 BGR，避免每个像素创建 Color 对象。
                // 默认灰度切片占绝大多数交互路径，直接写 BGR 可避免逐像素构造 Color。
                buffer[offset + 0] = displayValue;
                buffer[offset + 1] = displayValue;
                buffer[offset + 2] = displayValue;
                return;
            }

            WritePseudoBgr(buffer, offset, displayValue);
        }

        public static void ImageToVoxel(
            FVOVolumeData volume,
            int sliceX, int sliceY, int sliceZ,
            FVOSlicePlane plane,
            int imageX,
            int imageY,
            out int x,
            out int y,
            out int z)
        {
            switch (plane)
            {
                case FVOSlicePlane.XY:
                    x = MapIndex(imageX, GetImageWidth(volume, plane), volume.OriginalWidth);
                    y = MapIndex(imageY, GetImageHeight(volume, plane), volume.OriginalHeight);
                    z = FVOFrameworkCompat.Clamp(sliceZ, 0, volume.OriginalDepth - 1);
                    break;
                case FVOSlicePlane.XZ:
                    x = MapIndex(imageX, GetImageWidth(volume, plane), volume.OriginalWidth);
                    y = FVOFrameworkCompat.Clamp(sliceY, 0, volume.OriginalHeight - 1);
                    z = MapIndex(imageY, GetImageHeight(volume, plane), volume.OriginalDepth);
                    break;
                case FVOSlicePlane.YZ:
                    x = FVOFrameworkCompat.Clamp(sliceX, 0, volume.OriginalWidth - 1);
                    y = MapIndex(imageX, GetImageWidth(volume, plane), volume.OriginalHeight);
                    z = MapIndex(imageY, GetImageHeight(volume, plane), volume.OriginalDepth);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("plane", plane, null);
            }
        }

        public static byte GetVoxel(FVOVolumeData volume, int x, int y, int z)
        {
            return volume.GetVoxelByOriginalIndex(x, y, z);
        }

        public static float GetScalarValue(FVOVolumeData volume, int x, int y, int z)
        {
            // 交互查看时体素值也走内存显示体，避免鼠标移动时频繁打开并读取 RAW 文件。
            return volume.GetScalarByOriginalIndex(x, y, z);
        }

        public static int GetImageWidth(FVOVolumeData volume, FVOSlicePlane plane)
        {
            return GetImageWidthHeight(volume, plane, true);
        }

        public static int GetImageHeight(FVOVolumeData volume, FVOSlicePlane plane)
        {
            return GetImageWidthHeight(volume, plane, false);
        }

        public static void VoxelToImage(FVOVolumeData volume, FVOSlicePlane plane, int x, int y, int z, out int imageX, out int imageY)
        {
            switch (plane)
            {
                case FVOSlicePlane.XY:
                    imageX = MapIndex(x, volume.OriginalWidth, GetImageWidth(volume, plane));
                    imageY = MapIndex(y, volume.OriginalHeight, GetImageHeight(volume, plane));
                    break;
                case FVOSlicePlane.XZ:
                    imageX = MapIndex(x, volume.OriginalWidth, GetImageWidth(volume, plane));
                    imageY = MapIndex(z, volume.OriginalDepth, GetImageHeight(volume, plane));
                    break;
                case FVOSlicePlane.YZ:
                    imageX = MapIndex(y, volume.OriginalHeight, GetImageWidth(volume, plane));
                    imageY = MapIndex(z, volume.OriginalDepth, GetImageHeight(volume, plane));
                    break;
                default:
                    throw new ArgumentOutOfRangeException("plane", plane, null);
            }
        }

        private static int GetImageWidthHeight(FVOVolumeData volume, FVOSlicePlane plane, bool horizontal)
        {
            float physicalWidth;
            float physicalHeight;
            GetPhysicalSize(volume, plane, out physicalWidth, out physicalHeight);
            var sourceWidth = GetSourceWidth(volume, plane);
            var sourceHeight = GetSourceHeight(volume, plane);
            var sourceLongest = Math.Max(sourceWidth, sourceHeight);
            var maxPhysical = Math.Max(physicalWidth, physicalHeight);
            var pixelsPerPhysical = Math.Min(MaxSliceImageAxis, sourceLongest) / Math.Max(1.0f, maxPhysical);
            var width = FVOFrameworkCompat.Clamp((int)Math.Round(physicalWidth * pixelsPerPhysical), 1, MaxSliceImageAxis);
            var height = FVOFrameworkCompat.Clamp((int)Math.Round(physicalHeight * pixelsPerPhysical), 1, MaxSliceImageAxis);
            return horizontal ? width : height;
        }

        private static int GetSourceWidth(FVOVolumeData volume, FVOSlicePlane plane)
        {
            switch (plane)
            {
                case FVOSlicePlane.XY:
                case FVOSlicePlane.XZ:
                    return volume.HasHighResolutionVoxels ? volume.OriginalWidth : volume.Width;
                case FVOSlicePlane.YZ:
                    return volume.HasHighResolutionVoxels ? volume.OriginalHeight : volume.Height;
                default:
                    throw new ArgumentOutOfRangeException("plane", plane, null);
            }
        }

        private static int GetSourceHeight(FVOVolumeData volume, FVOSlicePlane plane)
        {
            switch (plane)
            {
                case FVOSlicePlane.XY:
                    return volume.HasHighResolutionVoxels ? volume.OriginalHeight : volume.Height;
                case FVOSlicePlane.XZ:
                case FVOSlicePlane.YZ:
                    return volume.HasHighResolutionVoxels ? volume.OriginalDepth : volume.Depth;
                default:
                    throw new ArgumentOutOfRangeException("plane", plane, null);
            }
        }

        private static void GetPhysicalSize(FVOVolumeData volume, FVOSlicePlane plane, out float width, out float height)
        {
            switch (plane)
            {
                case FVOSlicePlane.XY:
                    width = volume.OriginalWidth * volume.SpacingX;
                    height = volume.OriginalHeight * volume.SpacingY;
                    break;
                case FVOSlicePlane.XZ:
                    width = volume.OriginalWidth * volume.SpacingX;
                    height = volume.OriginalDepth * volume.SpacingZ;
                    break;
                case FVOSlicePlane.YZ:
                    width = volume.OriginalHeight * volume.SpacingY;
                    height = volume.OriginalDepth * volume.SpacingZ;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("plane", plane, null);
            }
        }

        private static int MapIndex(int sourceIndex, int sourceCount, int targetCount)
        {
            if (targetCount <= 1 || sourceCount <= 1)
            {
                return 0;
            }

            var normalized = FVOFrameworkCompat.Clamp(sourceIndex / (float)(sourceCount - 1), 0.0f, 1.0f);
            return FVOFrameworkCompat.Clamp((int)Math.Round(normalized * (targetCount - 1)), 0, targetCount - 1);
        }

        private static void WritePseudoBgr(byte[] buffer, int offset, byte value)
        {
            float t = value / 255.0f;
            byte r, g, b;
            if (t < 0.33f)
            {
                float k = t / 0.33f;
                r = 0; g = (byte)Math.Round(255 * k); b = 255;
            }
            else if (t < 0.66f)
            {
                float k = (t - 0.33f) / 0.33f;
                r = (byte)Math.Round(255 * k); g = 255; b = (byte)Math.Round(255 * (1.0f - k));
            }
            else
            {
                float high = (t - 0.66f) / 0.34f;
                r = 255; g = (byte)Math.Round(255 * (1.0f - high)); b = 0;
            }
            buffer[offset] = b; buffer[offset + 1] = g; buffer[offset + 2] = r;
        }

        public static byte ApplyWindow(byte value, byte windowMin, byte windowMax)
        {
            if (windowMax <= windowMin)
            {
                return value >= windowMin ? (byte)255 : (byte)0;
            }

            var normalized = (value - windowMin) / (float)(windowMax - windowMin);
            return (byte)Math.Round(FVOFrameworkCompat.Clamp(normalized, 0.0f, 1.0f) * 255.0f);
        }


    }
}
