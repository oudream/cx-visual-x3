using System;
using System.Collections.Generic;
using System.IO;
using CxVisual.Mechanism.Core;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Mechanism.Data.Configuration;

namespace CxVisual.Professional.Vision
{
    /// <summary>确定大小、位深与不可被调用者篡改的像素快照；像素以unsigned 16-bit内存值表达。</summary>
    public sealed class CxVisionGrayFrame
    {
        private const int MaximumPixels = 32000000;
        private readonly ushort[] _pixels;
        public int Width { get; private set; }
        public int Height { get; private set; }
        public CxVisionPixelDepth PixelDepth { get; private set; }

        public CxVisionGrayFrame(int width, int height, CxVisionPixelDepth pixelDepth, ushort[] pixels)
        {
            long length = (long)width * height;
            if (width <= 0 || height <= 0 || length > MaximumPixels)
                throw new ArgumentOutOfRangeException("width", "Frame dimensions exceed supported image size");
            if (pixelDepth != CxVisionPixelDepth.Bits8 && pixelDepth != CxVisionPixelDepth.Bits16)
                throw new ArgumentException("Unsupported unsigned grayscale bit depth", "pixelDepth");
            if (pixels == null || pixels.Length != length)
                throw new ArgumentException("Pixels must exactly match width*height", "pixels");
            if (pixelDepth == CxVisionPixelDepth.Bits8)
                for (int i = 0; i < pixels.Length; ++i)
                    if (pixels[i] > 255)
                        throw new ArgumentException("An 8-bit grayscale pixel cannot exceed 255", "pixels");
            Width = width;
            Height = height;
            PixelDepth = pixelDepth;
            _pixels = (ushort[])pixels.Clone();
        }

        internal ushort PixelAt(int index) { return _pixels[index]; }
    }

    /// <summary>四邻接连通域矩形及像素面积；矩形边界均为包含端点。</summary>
    public sealed class CxVisionDefectRegion
    {
        public int PixelArea { get; internal set; }
        public int MinX { get; internal set; }
        public int MinY { get; internal set; }
        public int MaxX { get; internal set; }
        public int MaxY { get; internal set; }
    }

    /// <summary>一次检测的可追溯结果。Passed仅指此阈值/连通域算法未检出符合面积规则的区域。</summary>
    public sealed class CxVisionInspectionResult
    {
        public long ConfigRevision { get; internal set; }
        public int Width { get; internal set; }
        public int Height { get; internal set; }
        public int ThresholdPixelCount { get; internal set; }
        public IReadOnlyList<CxVisionDefectRegion> Regions { get; internal set; }
        public bool Passed { get { return Regions.Count == 0; } }
    }

    /// <summary>
    /// 真实CPU处理：逐像素阈值比较、四邻接区域标记、像素面积上下限筛选。
    /// 不使用AI或具体硬件；对X-Ray业务还需要专业预处理、灰度校正等独立能力。
    /// </summary>
    internal sealed class CxVisionThresholdPipeline
    {
        private readonly CxVisionPixelDepth _pixelDepth;
        private readonly int _grayThreshold;
        private readonly double _minArea;
        private readonly double _maxArea;

        internal CxVisionThresholdPipeline(CxVisionInspectionOptions config)
        {
            if (config == null) throw new ArgumentNullException("config");
            string problem = CxVisionInspectionOptions.Validate(config);
            if (string.IsNullOrWhiteSpace(problem) && !FCMConfigValidationEngine.Validate(config, "vision:runtime").IsValid)
                problem = "Vision configuration failed schema validation";
            if (!string.IsNullOrWhiteSpace(problem)) throw new ArgumentException(problem, "config");
            if (config.UseAi)
                throw new NotSupportedException("AI execution is not installed in this CPU threshold provider");
            _pixelDepth = config.PixelDepth;
            _grayThreshold = config.GrayThreshold;
            _minArea = config.MinDefectArea;
            _maxArea = config.MaxDefectArea;
        }

        internal bool Matches(CxVisionInspectionOptions options)
        {
            return options != null && !options.UseAi &&
                options.PixelDepth == _pixelDepth && options.GrayThreshold == _grayThreshold &&
                options.MinDefectArea == _minArea && options.MaxDefectArea == _maxArea;
        }

        internal CxVisionInspectionResult Inspect(CxVisionGrayFrame frame, long revision,
            System.Threading.CancellationToken cancellation = default(System.Threading.CancellationToken))
        {
            if (frame == null) throw new ArgumentNullException("frame");
            if (frame.PixelDepth != _pixelDepth)
                throw new ArgumentException("Frame bit depth differs from active inspection configuration", "frame");
            int width = frame.Width;
            int size = checked(width * frame.Height);
            byte[] visited = new byte[size];
            int[] queue = new int[size];
            List<CxVisionDefectRegion> accepted = new List<CxVisionDefectRegion>();
            int thresholdPixelCount = 0;
            // Pixel count is independent of accepted defect area; do not count a pixel twice.
            for (int index = 0; index < size; ++index)
            {
                if ((index & 4095) == 0) cancellation.ThrowIfCancellationRequested();
                if (frame.PixelAt(index) >= _grayThreshold) thresholdPixelCount++;
            }

            for (int index = 0; index < size; ++index)
            {
                if ((index & 4095) == 0) cancellation.ThrowIfCancellationRequested();
                if (visited[index] != 0 || frame.PixelAt(index) < _grayThreshold) continue;
                int head = 0, tail = 1, area = 0;
                queue[0] = index;
                visited[index] = 1;
                int minX = width, maxX = 0, minY = frame.Height, maxY = 0;
                while (head < tail)
                {
                    if ((area & 4095) == 0) cancellation.ThrowIfCancellationRequested();
                    int cursor = queue[head++];
                    int x = cursor % width;
                    int y = cursor / width;
                    area++;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                    if (x > 0) Enqueue(cursor - 1, frame, visited, queue, ref tail);
                    if (x + 1 < width) Enqueue(cursor + 1, frame, visited, queue, ref tail);
                    if (y > 0) Enqueue(cursor - width, frame, visited, queue, ref tail);
                    if (y + 1 < frame.Height) Enqueue(cursor + width, frame, visited, queue, ref tail);
                }
                if (area >= _minArea && area <= _maxArea)
                    accepted.Add(new CxVisionDefectRegion
                    {
                        PixelArea = area, MinX = minX, MinY = minY, MaxX = maxX, MaxY = maxY
                    });
            }
            return new CxVisionInspectionResult
            {
                Width = width, Height = frame.Height,
                ConfigRevision = revision, ThresholdPixelCount = thresholdPixelCount,
                Regions = accepted.AsReadOnly()
            };
        }

        private void Enqueue(int index, CxVisionGrayFrame frame, byte[] visited, int[] queue, ref int tail)
        {
            if (visited[index] != 0 || frame.PixelAt(index) < _grayThreshold) return;
            visited[index] = 1;
            queue[tail++] = index;
        }
    }

    /// <summary>每项检测任务保持自己的配置快照与算法实例；V1.4 软件配置不代表硬件确认。</summary>
    public sealed class CxVisionInspectionTask : IDisposable
    {
        private MHSConfigSnapshot<CxVisionInspectionOptions> _snapshot;
        private Action _onDispose;
        private readonly CxVisionThresholdPipeline _pipeline;
        internal CxVisionInspectionTask(MHSConfigSnapshot<CxVisionInspectionOptions> snapshot,
            CxVisionThresholdPipeline pipeline, Action onDispose)
        { _snapshot = snapshot; _pipeline = pipeline; _onDispose = onDispose; }
        public long ConfigRevision
        {
            get
            {
                if (_snapshot == null) throw new ObjectDisposedException("CxVisionInspectionTask");
                return long.Parse(_snapshot.Version.Substring(1), System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        public CxVisionInspectionResult Inspect(CxVisionGrayFrame frame)
        {
            if (_snapshot == null) throw new ObjectDisposedException("CxVisionInspectionTask");
            return _pipeline.Inspect(frame, ConfigRevision);
        }
        public void Dispose()
        {
            MHSConfigSnapshot<CxVisionInspectionOptions> ticket = System.Threading.Interlocked.Exchange(ref _snapshot, null);
            if (ticket != null) ticket.Dispose();
            Action release = System.Threading.Interlocked.Exchange(ref _onDispose, null);
            if (release != null) release();
        }
    }

    /// <summary>以V1.4配置服务为唯一真源；任务边界内切换，新任务生成新CPU检测实例。</summary>
    public sealed class CxVisionInspectionRuntime
    {
        private readonly object _gate = new object();
        private readonly MHSConfigSection<CxVisionInspectionOptions> _config;
        private int _running;
        public CxVisionInspectionRuntime(MHSConfigSection<CxVisionInspectionOptions> configuration)
        {
            _config = configuration ?? throw new ArgumentNullException("configuration");
            // The actual CPU pipeline validates unsupported AI modes before any task can run.
            new CxVisionThresholdPipeline(_config.Get());
        }
        public long ActiveRevision { get { return ParseRevision(_config.EffectiveVersion); } }
        public long? PendingRevision { get { return _config.HasPendingApply ? (long?)ParseRevision(_config.SavedVersion) : null; } }
        private static long ParseRevision(string version)
        { return long.Parse(version.Substring(1), System.Globalization.CultureInfo.InvariantCulture); }
        public bool RequestApply(FCMConfigApplyPolicy policy)
        {
            // For NextTask the committed candidate is already staged by the config store.
            // Applying to a CPU pipeline happens only at the BeginInspection boundary.
            return policy == FCMConfigApplyPolicy.NextTask && _config.HasPendingApply;
        }
        public CxVisionInspectionTask BeginInspection()
        {
            lock (_gate)
            {
                if (_running == 0) _config.ApplyPendingAtTaskBoundary();
                MHSConfigSnapshot<CxVisionInspectionOptions> snapshot =
                    _config.CaptureForTask("vision-" + Guid.NewGuid().ToString("N"));
                try
                {
                    CxVisionThresholdPipeline pipeline = new CxVisionThresholdPipeline(snapshot.Value(x => x));
                    _running++;
                    return new CxVisionInspectionTask(snapshot, pipeline, () =>
                    {
                        lock (_gate) { _running--; }
                    });
                }
                catch { snapshot.Dispose(); throw; }
            }
        }
    }

    /// <summary>载入无头RAW灰度图，8位为1字节/像素，16位为little-endian双字节/像素。</summary>
    public static class CxVisionRawFrameReader
    {
        public static CxVisionGrayFrame Read(string filename, int width, int height, CxVisionPixelDepth depth)
        {
            if (string.IsNullOrWhiteSpace(filename)) throw new ArgumentException("Image filename required");
            // 先使用Frame验证维度范围，避免外部未受控RAW尺寸导致超大内存分配。
            long length = (long)width * height;
            if (width <= 0 || height <= 0 || length > 32000000)
                throw new ArgumentOutOfRangeException("width", "Unsupported RAW dimensions");
            if (depth != CxVisionPixelDepth.Bits8 && depth != CxVisionPixelDepth.Bits16)
                throw new ArgumentException("Unsupported RAW depth", "depth");
            long expected = length * (depth == CxVisionPixelDepth.Bits8 ? 1 : 2);
            FileInfo info = new FileInfo(filename);
            if (info.Length != expected)
                throw new InvalidDataException("RAW byte count does not match width/height/bit depth");
            byte[] data = File.ReadAllBytes(filename);
            ushort[] pixels = new ushort[checked((int)length)];
            if (depth == CxVisionPixelDepth.Bits8)
                for (int i = 0; i < pixels.Length; i++) pixels[i] = data[i];
            else
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = (ushort)(data[2 * i] | (data[2 * i + 1] << 8));
            return new CxVisionGrayFrame(width, height, depth, pixels);
        }
    }
}
