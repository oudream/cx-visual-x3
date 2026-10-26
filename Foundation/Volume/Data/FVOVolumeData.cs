using System;

namespace CxVisual.Foundation.Volume
{
    /// <summary>封装显示体、原始尺寸、物理间距和可选原始 RAW 缓存的统一体数据模型。</summary>
    public sealed class FVOVolumeData : IDisposable
    {
        public FVOVolumeData(
            int width,
            int height,
            int depth,
            byte[] voxels,
            string name,
            int? originalWidth,
            int? originalHeight,
            int? originalDepth,
            float spacingX,
            float spacingY,
            float spacingZ,
            float scalarMin,
            float scalarMax,
            string sourceFilePath,
            FVORawVoxelFormat? sourceFormat)
        {
            // --- FVOVolumeData 是加载后的统一数据包：Voxels 用于 3D 快速显示，Original* 和 Spacing* 用于真实坐标/比例。
            if (width <= 0 || height <= 0 || depth <= 0)
                throw new ArgumentOutOfRangeException("width", "体数据尺寸必须为正数。");
            long requiredBytes = checked((long)width * height * depth);
            if (voxels == null || voxels.LongLength != requiredBytes)
                throw new ArgumentException("显示体素缓冲与体积尺寸不一致。", "voxels");
            if ((originalWidth ?? width) <= 0 || (originalHeight ?? height) <= 0 || (originalDepth ?? depth) <= 0)
                throw new ArgumentOutOfRangeException("originalWidth", "原始体数据尺寸必须为正数。");
            if (spacingX <= 0 || spacingY <= 0 || spacingZ <= 0 ||
                float.IsNaN(spacingX) || float.IsNaN(spacingY) || float.IsNaN(spacingZ) ||
                float.IsInfinity(spacingX) || float.IsInfinity(spacingY) || float.IsInfinity(spacingZ))
                throw new ArgumentOutOfRangeException("spacingX", "体素间距必须是有限正数。");
            Width = width;
            Height = height;
            Depth = depth;
            Voxels = voxels;
            Name = name;
            OriginalWidth = originalWidth ?? width;
            OriginalHeight = originalHeight ?? height;
            OriginalDepth = originalDepth ?? depth;
            SpacingX = spacingX;
            SpacingY = spacingY;
            SpacingZ = spacingZ;
            ScalarMin = scalarMin;
            ScalarMax = scalarMax;
            SourceFilePath = sourceFilePath;
            SourceFormat = sourceFormat;
        }

        public FVOVolumeData(int width, int height, int depth, byte[] voxels, string name)
            : this(width, height, depth, voxels, name, null, null, null, 1.0f, 1.0f, 1.0f, 0.0f, 255.0f, null, null)
        {
        }

        // Width/Height/Depth 是当前上传到 GPU 的显示体尺寸，可能已经为了交互性能做过降采样。
        public int Width { get; private set; }

        public int Height { get; private set; }

        public int Depth { get; private set; }

        // 体素数据统一归一化为 8-bit 灰度，方便直接创建 OpenGL R8 3D 纹理。
        public byte[] Voxels { get; private set; }

        public string Name { get; private set; }

        // Original* 和 Spacing* 描述真实物理外形；渲染外框比例必须用它们，而不是降采样后的尺寸。
        public int OriginalWidth { get; private set; }

        public int OriginalHeight { get; private set; }

        public int OriginalDepth { get; private set; }

        public float SpacingX { get; private set; }

        public float SpacingY { get; private set; }

        public float SpacingZ { get; private set; }

        public float ScalarMin { get; private set; }

        public float ScalarMax { get; private set; }

        public string SourceFilePath { get; private set; }

        public FVORawVoxelFormat? SourceFormat { get; private set; }

        public FVORawVolumeLoadTiming LoadTiming { get; private set; }

        public byte[] OriginalVoxels { get; private set; }

        private FVOUnmanagedRawVolumeBuffer _rawBuffer;

        public bool HasRawSource
        {
            get { return !string.IsNullOrWhiteSpace(SourceFilePath) && SourceFormat.HasValue; }
        }

        public bool HasOriginalVoxels
        {
            get { return OriginalVoxels != null && OriginalVoxels.LongLength == (long)OriginalWidth * OriginalHeight * OriginalDepth; }
        }

        // 原始 RAW 已常驻非托管内存时，2D 切片可直接读取完整分辨率而无需额外缓存 byte[]。
        public bool HasOriginalRawData
        {
            get { return _rawBuffer != null && !_rawBuffer.IsClosed && !_rawBuffer.IsInvalid; }
        }

        public bool HasHighResolutionVoxels
        {
            get { return HasOriginalVoxels || HasOriginalRawData; }
        }

        public void SetOriginalVoxels(byte[] originalVoxels)
        {
            // --- 原始体素缓存加载成功后挂到同一个 FVOVolumeData 上，2D 切片会自动切到完整分辨率来源。
            if (originalVoxels == null)
            {
                OriginalVoxels = null;
                return;
            }

            var expectedLength = checked((long)OriginalWidth * OriginalHeight * OriginalDepth);
            if (originalVoxels.LongLength != expectedLength)
            {
                throw new ArgumentException("原始体素缓存尺寸与体数据尺寸不匹配。", "originalVoxels");
            }

            OriginalVoxels = originalVoxels;
        }

        /// <summary>关联完整 RAW 的非托管缓存，使切片读取优先使用原始分辨率。</summary>
        public void SetRawBuffer(FVOUnmanagedRawVolumeBuffer rawBuffer)
        {
            if (rawBuffer == null)
            {
                throw new ArgumentNullException("rawBuffer");
            }

            if (_rawBuffer != null)
            {
                _rawBuffer.Dispose();
            }

            _rawBuffer = rawBuffer;
        }

        internal void SetLoadTiming(FVORawVolumeLoadTiming timing)
        {
            LoadTiming = timing;
        }

        public int OriginalToDisplayX(int originalX)
        {
            return MapIndex(originalX, OriginalWidth, Width);
        }

        public int OriginalToDisplayY(int originalY)
        {
            return MapIndex(originalY, OriginalHeight, Height);
        }

        public int OriginalToDisplayZ(int originalZ)
        {
            return MapIndex(originalZ, OriginalDepth, Depth);
        }

        public int DisplayToOriginalX(int displayX)
        {
            return MapIndex(displayX, Width, OriginalWidth);
        }

        public int DisplayToOriginalY(int displayY)
        {
            return MapIndex(displayY, Height, OriginalHeight);
        }

        public int DisplayToOriginalZ(int displayZ)
        {
            return MapIndex(displayZ, Depth, OriginalDepth);
        }

        public float OriginalToNormalizedX(int originalX)
        {
            return FVOFrameworkCompat.Clamp(originalX / (float)Math.Max(1, OriginalWidth - 1), 0.0f, 1.0f);
        }

        public float OriginalToNormalizedY(int originalY)
        {
            return FVOFrameworkCompat.Clamp(originalY / (float)Math.Max(1, OriginalHeight - 1), 0.0f, 1.0f);
        }

        public float OriginalToNormalizedZ(int originalZ)
        {
            return FVOFrameworkCompat.Clamp(originalZ / (float)Math.Max(1, OriginalDepth - 1), 0.0f, 1.0f);
        }

        public byte GetVoxelByDisplayIndex(int x, int y, int z)
        {
            x = FVOFrameworkCompat.Clamp(x, 0, Width - 1);
            y = FVOFrameworkCompat.Clamp(y, 0, Height - 1);
            z = FVOFrameworkCompat.Clamp(z, 0, Depth - 1);
            return Voxels[x + y * Width + z * Width * Height];
        }

        public byte GetVoxelByOriginalIndex(int x, int y, int z)
        {
            if (HasOriginalVoxels)
            {
                x = FVOFrameworkCompat.Clamp(x, 0, OriginalWidth - 1);
                y = FVOFrameworkCompat.Clamp(y, 0, OriginalHeight - 1);
                z = FVOFrameworkCompat.Clamp(z, 0, OriginalDepth - 1);
                return OriginalVoxels[x + y * OriginalWidth + z * OriginalWidth * OriginalHeight];
            }

            if (HasOriginalRawData)
            {
                x = FVOFrameworkCompat.Clamp(x, 0, OriginalWidth - 1);
                y = FVOFrameworkCompat.Clamp(y, 0, OriginalHeight - 1);
                z = FVOFrameworkCompat.Clamp(z, 0, OriginalDepth - 1);
                var voxelIndex = x + (long)y * OriginalWidth + (long)z * OriginalWidth * OriginalHeight;
                var value = _rawBuffer.ReadValue(voxelIndex, SourceFormat.Value);
                return NormalizeRawValue(value);
            }

            return GetVoxelByDisplayIndex(OriginalToDisplayX(x), OriginalToDisplayY(y), OriginalToDisplayZ(z));
        }

        public void Dispose()
        {
            if (_rawBuffer != null)
            {
                _rawBuffer.Dispose();
                _rawBuffer = null;
            }
        }

        public float GetScalarByOriginalIndex(int x, int y, int z)
        {
            // 保留原始16bit/float标量：不通过8bit显示数据恢复，从而避免量化损失。
            if (HasOriginalRawData && SourceFormat.HasValue)
            {
                x = FVOFrameworkCompat.Clamp(x, 0, OriginalWidth - 1);
                y = FVOFrameworkCompat.Clamp(y, 0, OriginalHeight - 1);
                z = FVOFrameworkCompat.Clamp(z, 0, OriginalDepth - 1);
                long index = x + (long)y * OriginalWidth + (long)z * OriginalWidth * OriginalHeight;
                return _rawBuffer.ReadValue(index, SourceFormat.Value);
            }
            var value = GetVoxelByOriginalIndex(x, y, z);
            if (ScalarMax <= ScalarMin)
            {
                return value;
            }

            return ScalarMin + value / 255.0f * (ScalarMax - ScalarMin);
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

        private byte NormalizeRawValue(float value)
        {
            if (!FVOFrameworkCompat.IsFinite(value) || ScalarMax <= ScalarMin)
            {
                return 0;
            }

            var normalized = FVOFrameworkCompat.Clamp((value - ScalarMin) / (ScalarMax - ScalarMin), 0.0f, 1.0f);
            return (byte)Math.Round(normalized * 255.0f);
        }

        public static FVOVolumeData CreateDemo(int width, int height, int depth)
        {
            if (width <= 0 || height <= 0 || depth <= 0) throw new ArgumentOutOfRangeException("width");
            var voxels = new byte[checked(width * height * depth)];
            var centerX = (width - 1) * 0.5f;
            var centerY = (height - 1) * 0.5f;
            var centerZ = (depth - 1) * 0.5f;
            var maxRadius = Math.Min(width, Math.Min(height, depth)) * 0.42f;

            for (var z = 0; z < depth; z++)
            {
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var dx = (x - centerX) / maxRadius;
                        var dy = (y - centerY) / maxRadius;
                        var dz = (z - centerZ) / maxRadius;
                        var radius = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);

                        // Demo 数据刻意做成“外壳 + 内部热点 + 轻微波纹”，便于验证体渲染、光照和切面联动。
                        var shell = (float)Math.Exp(-Math.Pow((radius - 0.72f) * 9.0f, 2.0));
                        var hotSpot = (float)Math.Exp(-(Math.Pow(dx + 0.28f, 2.0) + Math.Pow(dy - 0.18f, 2.0) + Math.Pow(dz, 2.0)) * 16.0);
                        var wave = 0.12f * ((float)Math.Sin(x * 0.17f) + (float)Math.Cos(y * 0.13f) + (float)Math.Sin(z * 0.11f));
                        var value = FVOFrameworkCompat.Clamp(shell * 0.75f + hotSpot * 0.9f + wave, 0.0f, 1.0f);

                        voxels[x + y * width + z * width * height] = (byte)Math.Round(value * 255.0f);
                    }
                }
            }

            return new FVOVolumeData(width, height, depth, voxels, "Demo volume");
        }
    }
}
