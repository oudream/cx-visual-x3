using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using CxECTVolumeCore.Slicing;
using CxECTVolumeCore.Utils;
using CxECTVolumeCore.Volume;

namespace CxECTVolumeCore.Slicing
{
    /// <summary>保存二维切片位图及其在原始体素空间中的尺寸和显示比例。</summary>
    public sealed class SliceImage
    {
        public SliceImage(Bitmap bitmap, int width, int height)
        {
            Bitmap = bitmap;
            Width = width;
            Height = height;
        }

        public Bitmap Bitmap { get; private set; }

        public int Width { get; private set; }

        public int Height { get; private set; }
    }

    /// <summary>从体数据和切片状态生成 XY、XZ、YZ 平面的二维显示位图。</summary>
    public static class SliceImageBuilder
    {
        private const int MaxSliceImageAxis = 4096;

        public static SliceImage Build(
            VolumeData volume,
            VolumeState state,
            SlicePlane plane,
            bool pseudoColor,
            byte windowMin,
            byte windowMax)
        {
            // --- 2D 切片生成入口：根据平面计算输出图像尺寸，物理间距会影响 XZ/YZ 的显示比例。
            var width = GetImageWidth(volume, plane);
            var height = GetImageHeight(volume, plane);
            var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

            try
            {
                // 交互滚动必须只读内存里的显示体；整张切片从 RAW 跳读会让 UI 卡在磁盘 IO 上。
                var buffer = BuildBufferFromDisplayVolume(volume, state, plane, pseudoColor, windowMin, windowMax, Math.Abs(data.Stride), width, height);
                Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            return new SliceImage(bitmap, width, height);
        }

        private static byte[] BuildBufferFromDisplayVolume(
            VolumeData volume,
            VolumeState state,
            SlicePlane plane,
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
                case SlicePlane.XY:
                    FillDisplayXySlice(buffer, stride, volume, state, pseudoColor, windowMin, windowMax);
                    break;
                case SlicePlane.XZ:
                    FillDisplayXzSlice(buffer, stride, volume, state, pseudoColor, windowMin, windowMax);
                    break;
                case SlicePlane.YZ:
                    FillDisplayYzSlice(buffer, stride, volume, state, pseudoColor, windowMin, windowMax);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("plane", plane, null);
            }

            return buffer;
        }

        private static void FillDisplayXySlice(byte[] buffer, int stride, VolumeData volume, VolumeState state, bool pseudoColor, byte windowMin, byte windowMax)
        {
            // --- XY 视图：输出图像的列映射到 X，行映射到 Y，当前 state.SliceZ 决定深度层。
            var sourceWidth = GetSourceWidth(volume, SlicePlane.XY);
            var sourceHeight = GetSourceHeight(volume, SlicePlane.XY);
            var sourceZ = volume.HasHighResolutionVoxels ? FrameworkCompat.Clamp(state.SliceZ, 0, volume.OriginalDepth - 1) : volume.OriginalToDisplayZ(state.SliceZ);
            var imageWidth = GetImageWidth(volume, SlicePlane.XY);
            var imageHeight = GetImageHeight(volume, SlicePlane.XY);

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

        private static void FillDisplayXzSlice(byte[] buffer, int stride, VolumeData volume, VolumeState state, bool pseudoColor, byte windowMin, byte windowMax)
        {
            // --- XZ 视图：输出图像的列映射到 X，行映射到 Z，当前 state.SliceY 决定截面位置。
            var sourceWidth = GetSourceWidth(volume, SlicePlane.XZ);
            var sourceHeight = GetSourceHeight(volume, SlicePlane.XZ);
            var sourceY = volume.HasHighResolutionVoxels ? FrameworkCompat.Clamp(state.SliceY, 0, volume.OriginalHeight - 1) : volume.OriginalToDisplayY(state.SliceY);
            var imageWidth = GetImageWidth(volume, SlicePlane.XZ);
            var imageHeight = GetImageHeight(volume, SlicePlane.XZ);

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

        private static void FillDisplayYzSlice(byte[] buffer, int stride, VolumeData volume, VolumeState state, bool pseudoColor, byte windowMin, byte windowMax)
        {
            // --- YZ 视图：输出图像的列映射到 Y，行映射到 Z，当前 state.SliceX 决定截面位置。
            var sourceWidth = GetSourceWidth(volume, SlicePlane.YZ);
            var sourceHeight = GetSourceHeight(volume, SlicePlane.YZ);
            var sourceX = volume.HasHighResolutionVoxels ? FrameworkCompat.Clamp(state.SliceX, 0, volume.OriginalWidth - 1) : volume.OriginalToDisplayX(state.SliceX);
            var imageWidth = GetImageWidth(volume, SlicePlane.YZ);
            var imageHeight = GetImageHeight(volume, SlicePlane.YZ);

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

            var color = ToPseudoColor(displayValue);
            buffer[offset + 0] = color.B;
            buffer[offset + 1] = color.G;
            buffer[offset + 2] = color.R;
        }

        public static void ImageToVoxel(
            VolumeData volume,
            VolumeState state,
            SlicePlane plane,
            int imageX,
            int imageY,
            out int x,
            out int y,
            out int z)
        {
            switch (plane)
            {
                case SlicePlane.XY:
                    x = MapIndex(imageX, GetImageWidth(volume, plane), volume.OriginalWidth);
                    y = MapIndex(imageY, GetImageHeight(volume, plane), volume.OriginalHeight);
                    z = FrameworkCompat.Clamp(state.SliceZ, 0, volume.OriginalDepth - 1);
                    break;
                case SlicePlane.XZ:
                    x = MapIndex(imageX, GetImageWidth(volume, plane), volume.OriginalWidth);
                    y = FrameworkCompat.Clamp(state.SliceY, 0, volume.OriginalHeight - 1);
                    z = MapIndex(imageY, GetImageHeight(volume, plane), volume.OriginalDepth);
                    break;
                case SlicePlane.YZ:
                    x = FrameworkCompat.Clamp(state.SliceX, 0, volume.OriginalWidth - 1);
                    y = MapIndex(imageX, GetImageWidth(volume, plane), volume.OriginalHeight);
                    z = MapIndex(imageY, GetImageHeight(volume, plane), volume.OriginalDepth);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("plane", plane, null);
            }
        }

        public static byte GetVoxel(VolumeData volume, int x, int y, int z)
        {
            return volume.GetVoxelByOriginalIndex(x, y, z);
        }

        public static float GetScalarValue(VolumeData volume, int x, int y, int z)
        {
            // 交互查看时体素值也走内存显示体，避免鼠标移动时频繁打开并读取 RAW 文件。
            return volume.GetScalarByOriginalIndex(x, y, z);
        }

        public static int GetImageWidth(VolumeData volume, SlicePlane plane)
        {
            return GetImageSize(volume, plane).Width;
        }

        public static int GetImageHeight(VolumeData volume, SlicePlane plane)
        {
            return GetImageSize(volume, plane).Height;
        }

        public static void VoxelToImage(VolumeData volume, SlicePlane plane, int x, int y, int z, out int imageX, out int imageY)
        {
            switch (plane)
            {
                case SlicePlane.XY:
                    imageX = MapIndex(x, volume.OriginalWidth, GetImageWidth(volume, plane));
                    imageY = MapIndex(y, volume.OriginalHeight, GetImageHeight(volume, plane));
                    break;
                case SlicePlane.XZ:
                    imageX = MapIndex(x, volume.OriginalWidth, GetImageWidth(volume, plane));
                    imageY = MapIndex(z, volume.OriginalDepth, GetImageHeight(volume, plane));
                    break;
                case SlicePlane.YZ:
                    imageX = MapIndex(y, volume.OriginalHeight, GetImageWidth(volume, plane));
                    imageY = MapIndex(z, volume.OriginalDepth, GetImageHeight(volume, plane));
                    break;
                default:
                    throw new ArgumentOutOfRangeException("plane", plane, null);
            }
        }

        private static Size GetImageSize(VolumeData volume, SlicePlane plane)
        {
            float physicalWidth;
            float physicalHeight;
            GetPhysicalSize(volume, plane, out physicalWidth, out physicalHeight);
            var sourceWidth = GetSourceWidth(volume, plane);
            var sourceHeight = GetSourceHeight(volume, plane);
            var sourceLongest = Math.Max(sourceWidth, sourceHeight);
            var maxPhysical = Math.Max(physicalWidth, physicalHeight);
            var pixelsPerPhysical = Math.Min(MaxSliceImageAxis, sourceLongest) / Math.Max(1.0f, maxPhysical);
            var width = FrameworkCompat.Clamp((int)Math.Round(physicalWidth * pixelsPerPhysical), 1, MaxSliceImageAxis);
            var height = FrameworkCompat.Clamp((int)Math.Round(physicalHeight * pixelsPerPhysical), 1, MaxSliceImageAxis);
            return new Size(width, height);
        }

        private static int GetSourceWidth(VolumeData volume, SlicePlane plane)
        {
            switch (plane)
            {
                case SlicePlane.XY:
                case SlicePlane.XZ:
                    return volume.HasHighResolutionVoxels ? volume.OriginalWidth : volume.Width;
                case SlicePlane.YZ:
                    return volume.HasHighResolutionVoxels ? volume.OriginalHeight : volume.Height;
                default:
                    throw new ArgumentOutOfRangeException("plane", plane, null);
            }
        }

        private static int GetSourceHeight(VolumeData volume, SlicePlane plane)
        {
            switch (plane)
            {
                case SlicePlane.XY:
                    return volume.HasHighResolutionVoxels ? volume.OriginalHeight : volume.Height;
                case SlicePlane.XZ:
                case SlicePlane.YZ:
                    return volume.HasHighResolutionVoxels ? volume.OriginalDepth : volume.Depth;
                default:
                    throw new ArgumentOutOfRangeException("plane", plane, null);
            }
        }

        private static void GetPhysicalSize(VolumeData volume, SlicePlane plane, out float width, out float height)
        {
            switch (plane)
            {
                case SlicePlane.XY:
                    width = volume.OriginalWidth * volume.SpacingX;
                    height = volume.OriginalHeight * volume.SpacingY;
                    break;
                case SlicePlane.XZ:
                    width = volume.OriginalWidth * volume.SpacingX;
                    height = volume.OriginalDepth * volume.SpacingZ;
                    break;
                case SlicePlane.YZ:
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

            var normalized = FrameworkCompat.Clamp(sourceIndex / (float)(sourceCount - 1), 0.0f, 1.0f);
            return FrameworkCompat.Clamp((int)Math.Round(normalized * (targetCount - 1)), 0, targetCount - 1);
        }

        public static Color ToPseudoColor(byte value)
        {
            var t = value / 255.0f;
            if (t < 0.33f)
            {
                var k = t / 0.33f;
                return Color.FromArgb(0, (int)Math.Round(255 * k), 255);
            }

            if (t < 0.66f)
            {
                var k = (t - 0.33f) / 0.33f;
                return Color.FromArgb((int)Math.Round(255 * k), 255, (int)Math.Round(255 * (1.0f - k)));
            }

            var high = (t - 0.66f) / 0.34f;
            return Color.FromArgb(255, (int)Math.Round(255 * (1.0f - high)), 0);
        }

        public static byte ApplyWindow(byte value, byte windowMin, byte windowMax)
        {
            if (windowMax <= windowMin)
            {
                return value >= windowMin ? (byte)255 : (byte)0;
            }

            var normalized = (value - windowMin) / (float)(windowMax - windowMin);
            return (byte)Math.Round(FrameworkCompat.Clamp(normalized, 0.0f, 1.0f) * 255.0f);
        }


    }
}
