using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace CxVisual.Foundation.Volume
{
    /// <summary>RAW 文件中单个体素的存储格式。</summary>
    public enum FVORawVoxelFormat
    {
        Byte,
        UInt16,
        Float32,
    }

    /// <summary>从 RAW 文件名和文件长度解析得到的尺寸、间距与格式元数据。</summary>
    public sealed class FVORawVolumeInfo
    {
        public FVORawVolumeInfo(
            int width,
            int height,
            int depth,
            float spacingX,
            float spacingY,
            float spacingZ,
            FVORawVoxelFormat format,
            string fileName)
        {
            Width = width;
            Height = height;
            Depth = depth;
            SpacingX = spacingX;
            SpacingY = spacingY;
            SpacingZ = spacingZ;
            Format = format;
            FileName = fileName;
        }

        public int Width { get; private set; }

        public int Height { get; private set; }

        public int Depth { get; private set; }

        public float SpacingX { get; private set; }

        public float SpacingY { get; private set; }

        public float SpacingZ { get; private set; }

        public FVORawVoxelFormat Format { get; private set; }

        public string FileName { get; private set; }
    }

    /// <summary>负责解析 RAW 文件、生成交互预览体，并提供原始数据缓存读取入口。</summary>
    public static class FVORawVolumeLoader
    {
        // 交互预览优先保证旋转、缩放和切面联动流畅；原始尺寸仍保存在 FVOVolumeData 里用于物理比例显示。
        private const int MaxInteractiveAxis = 512;
        private const long MaxOriginalVoxelCacheBytes = 1024L * 1024L * 1024L;

        public static FVOVolumeData Load(string filePath)
        {
            return Load(filePath, MaxInteractiveAxis, CancellationToken.None);
        }

        public static FVOVolumeData Load(string filePath, int maxInteractiveAxis)
        {
            return Load(filePath, maxInteractiveAxis, CancellationToken.None);
        }

        public static FVOVolumeData Load(string filePath, int maxInteractiveAxis, CancellationToken cancellationToken)
        {
            // --- 1. 从文件名推断 RAW 的宽高深、体素间距和数据格式，例如 2500x2500x100、sp1x1x1。
            var info = InferInfo(filePath);
            // --- 2. 3D 交互显示使用最多 512 轴长的降采样体，原始尺寸仍写入 FVOVolumeData 做坐标和物理比例。
            maxInteractiveAxis = FVOFrameworkCompat.Clamp(maxInteractiveAxis, 64, MaxInteractiveAxis);
            var targetWidth = Math.Min(info.Width, maxInteractiveAxis);
            var targetHeight = Math.Min(info.Height, maxInteractiveAxis);
            var targetDepth = Math.Min(info.Depth, maxInteractiveAxis);
            var xSamples = CreateSampleIndexes(info.Width, targetWidth);
            var ySamples = CreateSampleIndexes(info.Height, targetHeight);
            var zSamples = CreateSampleIndexes(info.Depth, targetDepth);
            var sampled = new float[checked(targetWidth * targetHeight * targetDepth)];
            var bytesPerVoxel = GetBytesPerVoxel(info.Format);
            var rowByteCount = checked(info.Width * bytesPerVoxel);
            var sliceByteCount = checked((long)info.Width * info.Height * bytesPerVoxel);
            var rowBuffer = new byte[rowByteCount];
            var min = float.PositiveInfinity;
            var max = float.NegativeInfinity;
            var timing = new FVORawVolumeLoadTiming();

            // --- 3. 文件读取阶段只抽取降采样坐标上的体素，同时统计原始标量 min/max 供归一化和 UI 显示使用。
            // 预览从原始RAW按目标索引取样，不强迫先将多GB文件整体读入。
                // 大 RAW 可能有几 GB，只按降采样索引跳读需要的行，避免把原始体一次性压进内存。
            var sampleStopwatch = System.Diagnostics.Stopwatch.StartNew();
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.RandomAccess))
            {
                for (var tz = 0; tz < targetDepth; tz++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var sourceZ = zSamples[tz];
                    var sliceOffset = sourceZ * sliceByteCount;
                    for (var ty = 0; ty < targetHeight; ty++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var sourceY = ySamples[ty];
                        stream.Position = sliceOffset + (long)sourceY * rowByteCount;
                        FVOFrameworkCompat.ReadExactly(stream, rowBuffer);
                        for (var tx = 0; tx < targetWidth; tx++)
                        {
                            var value = ReadValue(rowBuffer, xSamples[tx], info.Format);
                            var index = tx + ty * targetWidth + tz * targetWidth * targetHeight;
                            sampled[index] = value;

                            if (FVOFrameworkCompat.IsFinite(value))
                            {
                                min = Math.Min(min, value);
                                max = Math.Max(max, value);
                            }
                        }
                    }
                }
            }
            sampleStopwatch.Stop();
            timing.PreviewSamplingMilliseconds = sampleStopwatch.ElapsedMilliseconds;

            if (!FVOFrameworkCompat.IsFinite(min) || !FVOFrameworkCompat.IsFinite(max))
            {
                throw new InvalidDataException("RAW 文件没有可用的数值。");
            }

            // OpenGL 3D 纹理用 R8 存储，先保留 float 统计全局 min/max，再统一归一化到 0..255。
            var normalizeStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var voxels = NormalizeToBytes(sampled, min, max, cancellationToken);
            normalizeStopwatch.Stop();
            timing.PreviewNormalizationMilliseconds = normalizeStopwatch.ElapsedMilliseconds;
            // --- 4. 归一化后的 byte[] 会作为 FVOVolumeData.Voxels，后续直接上传为 OpenGL R8 3D texture。
            var volume = new FVOVolumeData(
                targetWidth,
                targetHeight,
                targetDepth,
                voxels,
                Path.GetFileName(filePath),
                info.Width,
                info.Height,
                info.Depth,
                info.SpacingX,
                info.SpacingY,
                info.SpacingZ,
                min,
                max,
                filePath,
                info.Format);
            volume.SetLoadTiming(timing);
            return volume;
        }

        /// <summary>
        /// 从宿主已经加载的连续 RAW 内存生成 3D 交互预览，避免为了体渲染再次打开和读取同一文件。
        /// 调用期间宿主必须保证 sourceBuffer 有效；返回的 FVOVolumeData 只拥有降采样后的 byte[]。
        /// </summary>
        public static unsafe FVOVolumeData CreatePreviewFromMemory(
            IntPtr sourceBuffer,
            long sourceBufferLength,
            FVORawVolumeInfo info,
            string sourceFilePath,
            int maxInteractiveAxis,
            CancellationToken cancellationToken)
        {
            if (sourceBuffer == IntPtr.Zero)
            {
                throw new ArgumentException("RAW memory pointer is null.", "sourceBuffer");
            }

            if (info == null)
            {
                throw new ArgumentNullException("info");
            }

            if (info.Width <= 0 || info.Height <= 0 || info.Depth <= 0)
            {
                throw new ArgumentException("RAW dimensions must be positive.", "info");
            }

            var bytesPerVoxel = GetBytesPerVoxel(info.Format);
            var expectedLength = checked((long)info.Width * info.Height * info.Depth * bytesPerVoxel);
            if (sourceBufferLength < expectedLength)
            {
                throw new InvalidDataException(string.Format(
                    "RAW memory is shorter than expected. Expected {0:N0} bytes, got {1:N0} bytes.",
                    expectedLength,
                    sourceBufferLength));
            }

            maxInteractiveAxis = FVOFrameworkCompat.Clamp(maxInteractiveAxis, 64, MaxInteractiveAxis);
            var targetWidth = Math.Min(info.Width, maxInteractiveAxis);
            var targetHeight = Math.Min(info.Height, maxInteractiveAxis);
            var targetDepth = Math.Min(info.Depth, maxInteractiveAxis);
            var xSamples = CreateSampleIndexes(info.Width, targetWidth);
            var ySamples = CreateSampleIndexes(info.Height, targetHeight);
            var zSamples = CreateSampleIndexes(info.Depth, targetDepth);
            var sampled = new float[checked(targetWidth * targetHeight * targetDepth)];
            var min = float.PositiveInfinity;
            var max = float.NegativeInfinity;
            var timing = new FVORawVolumeLoadTiming();
            byte* source = (byte*)sourceBuffer.ToPointer();

            var sampleStopwatch = System.Diagnostics.Stopwatch.StartNew();
            for (var tz = 0; tz < targetDepth; tz++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                long zOffset = checked((long)zSamples[tz] * info.Width * info.Height);
                for (var ty = 0; ty < targetHeight; ty++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    long rowOffset = checked(zOffset + (long)ySamples[ty] * info.Width);
                    int targetRowOffset = checked(tz * targetWidth * targetHeight + ty * targetWidth);
                    for (var tx = 0; tx < targetWidth; tx++)
                    {
                        float value = ReadValue(source, rowOffset + xSamples[tx], info.Format);
                        sampled[targetRowOffset + tx] = value;
                        if (FVOFrameworkCompat.IsFinite(value))
                        {
                            min = Math.Min(min, value);
                            max = Math.Max(max, value);
                        }
                    }
                }
            }

            sampleStopwatch.Stop();
            timing.PreviewSamplingMilliseconds = sampleStopwatch.ElapsedMilliseconds;
            if (!FVOFrameworkCompat.IsFinite(min) || !FVOFrameworkCompat.IsFinite(max))
            {
                throw new InvalidDataException("RAW memory contains no finite values.");
            }

            var normalizeStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var voxels = NormalizeToBytes(sampled, min, max, cancellationToken);
            normalizeStopwatch.Stop();
            timing.PreviewNormalizationMilliseconds = normalizeStopwatch.ElapsedMilliseconds;

            string name = string.IsNullOrWhiteSpace(sourceFilePath)
                ? info.FileName
                : Path.GetFileName(sourceFilePath);
            var volume = new FVOVolumeData(
                targetWidth,
                targetHeight,
                targetDepth,
                voxels,
                name,
                info.Width,
                info.Height,
                info.Depth,
                info.SpacingX,
                info.SpacingY,
                info.SpacingZ,
                min,
                max,
                sourceFilePath,
                info.Format);
            volume.SetLoadTiming(timing);
            return volume;
        }

        /// <summary>将原始 RAW 读入非托管缓存，供宿主在后台切换高分辨率切片。</summary>
        public static FVOUnmanagedRawVolumeBuffer LoadRawBuffer(FVOVolumeData volume, FVORawVolumeLoadTiming timing)
        {
            if (volume == null || !volume.HasRawSource)
            {
                throw new ArgumentException("体数据没有可读取的 RAW 源。", "volume");
            }

            return FVOUnmanagedRawVolumeBuffer.Load(volume.SourceFilePath, new FileInfo(volume.SourceFilePath).Length, timing);
        }

        public static bool TryLoadOriginalVoxelCache(FVOVolumeData volume)
        {
            if (volume == null || !volume.HasRawSource)
            {
                return false;
            }

            if (volume.HasOriginalRawData)
            {
                return false;
            }

            var voxelCount = checked((long)volume.OriginalWidth * volume.OriginalHeight * volume.OriginalDepth);
            if (voxelCount > MaxOriginalVoxelCacheBytes)
            {
                return false;
            }

            var format = volume.SourceFormat.Value;
            var bytesPerVoxel = GetBytesPerVoxel(format);
            var rowByteCount = checked(volume.OriginalWidth * bytesPerVoxel);
            var rowBuffer = new byte[rowByteCount];
            var originalVoxels = new byte[(int)voxelCount];

            // --- 5. 原始缓存按完整分辨率读入内存；2D 切片优先使用它，避免滚动时反复打开 RAW 文件。
            using (var stream = new FileStream(volume.SourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.SequentialScan))
            {
                for (var z = 0; z < volume.OriginalDepth; z++)
                {
                    for (var y = 0; y < volume.OriginalHeight; y++)
                    {
                        FVOFrameworkCompat.ReadExactly(stream, rowBuffer);
                        var rowStart = (long)z * volume.OriginalWidth * volume.OriginalHeight + (long)y * volume.OriginalWidth;
                        for (var x = 0; x < volume.OriginalWidth; x++)
                        {
                            var value = ReadValue(rowBuffer, x, format);
                            originalVoxels[checked((int)(rowStart + x))] = NormalizeValue(value, volume.ScalarMin, volume.ScalarMax);
                        }
                    }
                }
            }

            volume.SetOriginalVoxels(originalVoxels);
            return true;
        }

        public static FVORawVolumeInfo InferInfo(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            var matches = Regex.Matches(fileName, @"(?<w>\d+)x(?<h>\d+)x(?<d>\d+)", RegexOptions.IgnoreCase);
            if (matches.Count == 0)
            {
                throw new InvalidDataException("文件名中没有找到类似 2500x2500x100 的体数据尺寸。");
            }

            // 文件名里可能同时出现 sp1x1x1 这种间距字段；取体素总数最大的三元组作为数据尺寸。
            var best = matches
                .Cast<Match>()
                .Select(match => new
                {
                    Width = int.Parse(match.Groups["w"].Value),
                    Height = int.Parse(match.Groups["h"].Value),
                    Depth = int.Parse(match.Groups["d"].Value),
                })
                .OrderByDescending(item => (long)item.Width * item.Height * item.Depth)
                .First();

            var voxelCount = checked((long)best.Width * best.Height * best.Depth);
            var fileLength = new FileInfo(filePath).Length;
            FVORawVoxelFormat format;

            // 允许末尾少量附加字节，兼容某些采集程序写入的对齐或尾部信息。
            if (fileLength >= voxelCount * 4 && fileLength < voxelCount * 4 + 4096)
            {
                format = FVORawVoxelFormat.Float32;
            }
            else if (fileLength >= voxelCount * 2 && fileLength < voxelCount * 2 + 4096)
            {
                format = FVORawVoxelFormat.UInt16;
            }
            else if (fileLength >= voxelCount && fileLength < voxelCount + 4096)
            {
                format = FVORawVoxelFormat.Byte;
            }
            else
            {
                throw new InvalidDataException(string.Format(
                    "文件大小 {0:N0} 与尺寸 {1}x{2}x{3} 不匹配。",
                    fileLength,
                    best.Width,
                    best.Height,
                    best.Depth));
            }

            var spacing = ParseSpacing(fileName);
            return new FVORawVolumeInfo(best.Width, best.Height, best.Depth, spacing.X, spacing.Y, spacing.Z, format, fileName);
        }

        private static SpacingInfo ParseSpacing(string fileName)
        {
            var match = Regex.Match(
                fileName,
                @"(?:^|[_-])sp(?<x>\d+(?:[._p]\d+)?)x(?<y>\d+(?:[._p]\d+)?)x(?<z>\d+(?:[._p]\d+)?)",
                RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                return new SpacingInfo(1.0f, 1.0f, 1.0f);
            }

            return new SpacingInfo(
                ParseSpacingNumber(match.Groups["x"].Value),
                ParseSpacingNumber(match.Groups["y"].Value),
                ParseSpacingNumber(match.Groups["z"].Value));
        }

        private static float ParseSpacingNumber(string text)
        {
            var normalized = text.Replace('p', '.').Replace('_', '.');
            float value;
            return float.TryParse(normalized, out value) && value > 0 ? value : 1.0f;
        }

        private static byte[] NormalizeToBytes(float[] sampled, float min, float max, CancellationToken cancellationToken)
        {
            var voxels = new byte[sampled.Length];
            var range = max - min;
            if (range <= float.Epsilon)
            {
                return voxels;
            }

            for (var i = 0; i < sampled.Length; i++)
            {
                if ((i & 0xFFFF) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                var value = sampled[i];
                if (!FVOFrameworkCompat.IsFinite(value))
                {
                    continue;
                }

                voxels[i] = NormalizeValue(value, min, max);
            }

            return voxels;
        }

        private static byte NormalizeValue(float value, float min, float max)
        {
            if (!FVOFrameworkCompat.IsFinite(value) || max <= min)
            {
                return 0;
            }

            var normalized = FVOFrameworkCompat.Clamp((value - min) / (max - min), 0.0f, 1.0f);
            return (byte)Math.Round(normalized * 255.0f);
        }

        private static int[] CreateSampleIndexes(int sourceCount, int targetCount)
        {
            var indexes = new int[targetCount];
            if (targetCount == 1)
            {
                indexes[0] = sourceCount / 2;
                return indexes;
            }

            for (var i = 0; i < targetCount; i++)
            {
                indexes[i] = (int)Math.Round(i * (sourceCount - 1) / (float)(targetCount - 1));
            }

            return indexes;
        }

        private static int GetBytesPerVoxel(FVORawVoxelFormat format)
        {
            switch (format)
            {
                case FVORawVoxelFormat.Byte:
                    return 1;
                case FVORawVoxelFormat.UInt16:
                    return 2;
                case FVORawVoxelFormat.Float32:
                    return 4;
                default:
                    throw new ArgumentOutOfRangeException("format", format, null);
            }
        }

        private static float ReadValue(byte[] rowBuffer, int sourceX, FVORawVoxelFormat format)
        {
            switch (format)
            {
                case FVORawVoxelFormat.Byte:
                    return rowBuffer[sourceX];
                case FVORawVoxelFormat.UInt16:
                    return BitConverter.ToUInt16(rowBuffer, sourceX * 2);
                case FVORawVoxelFormat.Float32:
                    return BitConverter.ToSingle(rowBuffer, sourceX * 4);
                default:
                    throw new ArgumentOutOfRangeException("format", format, null);
            }
        }

        private static unsafe float ReadValue(byte* source, long voxelIndex, FVORawVoxelFormat format)
        {
            switch (format)
            {
                case FVORawVoxelFormat.Byte:
                    return source[voxelIndex];
                case FVORawVoxelFormat.UInt16:
                    return *((ushort*)(source + checked(voxelIndex * 2L)));
                case FVORawVoxelFormat.Float32:
                    return *((float*)(source + checked(voxelIndex * 4L)));
                default:
                    throw new ArgumentOutOfRangeException("format", format, null);
            }
        }

        private struct SpacingInfo
        {
            public SpacingInfo(float x, float y, float z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            public float X;
            public float Y;
            public float Z;
        }
    }

    /// <summary>记录预览和非托管原始缓存各阶段耗时，供性能日志输出。</summary>
    public sealed class FVORawVolumeLoadTiming
    {
        public long PreviewSamplingMilliseconds { get; internal set; }

        public long PreviewNormalizationMilliseconds { get; internal set; }

        public long UnmanagedAllocationMilliseconds { get; internal set; }

        public long UnmanagedFileReadMilliseconds { get; internal set; }

        public long UnmanagedCopyMilliseconds { get; internal set; }
    }
}
