using System;
using System.Collections.Generic;
using System.Threading;
using CxECTImageWinForms.Contracts;
using CxECTImageWinForms.Models;

namespace CxECTImageWinForms.Services
{
    public sealed class ImageStatistics
    {
        public int PixelCount { get; internal set; }

        public double Min { get; internal set; }

        public double Max { get; internal set; }

        public double Mean { get; internal set; }

        public double StandardDeviation { get; internal set; }
    }

    public sealed class ImageHistogram
    {
        public ImageHistogram(double minimum, double maximum, int[] bins)
        {
            Minimum = minimum;
            Maximum = maximum;
            Bins = bins;
        }

        public double Minimum { get; private set; }

        public double Maximum { get; private set; }

        public int[] Bins { get; private set; }
    }

    /// <summary>
    /// 同一次分析请求的统计与直方图结果，避免按钮分别触发重复的全图扫描。
    /// </summary>
    public sealed class ImageAnalysisResult
    {
        public ImageAnalysisResult(ImageStatistics statistics, ImageHistogram histogram)
        {
            Statistics = statistics;
            Histogram = histogram;
        }

        public ImageStatistics Statistics { get; private set; }

        public ImageHistogram Histogram { get; private set; }
    }

    /// <summary>
    /// 统计基于原始像素源，显示窗宽窗位、反色等状态不会影响结果。
    /// </summary>
    public sealed class ImageStatisticsService
    {
        public ImageStatistics Calculate(IImagePixelSource source, ImageRectangle? roi)
        {
            return Calculate(source, roi, CancellationToken.None);
        }

        public ImageStatistics Calculate(IImagePixelSource source, ImageRectangle? roi, CancellationToken cancellationToken)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            var bounds = GetPixelBounds(source, roi);
            var statistics = new ImageStatistics { Min = double.MaxValue, Max = double.MinValue };
            var sum = 0.0;
            var squaredSum = 0.0;
            for (var y = bounds.Top; y <= bounds.Bottom; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (var x = bounds.Left; x <= bounds.Right; x++)
                {
                    var value = source.GetGrayValue(x, y);
                    statistics.Min = Math.Min(statistics.Min, value);
                    statistics.Max = Math.Max(statistics.Max, value);
                    sum += value;
                    squaredSum += value * value;
                    statistics.PixelCount++;
                }
            }

            statistics.Mean = sum / statistics.PixelCount;
            var variance = squaredSum / statistics.PixelCount - statistics.Mean * statistics.Mean;
            statistics.StandardDeviation = Math.Sqrt(Math.Max(0, variance));
            return statistics;
        }

        public ImageHistogram GetHistogram(IImagePixelSource source, ImageRectangle? roi, int binCount)
        {
            return GetHistogram(source, roi, binCount, CancellationToken.None);
        }

        public ImageHistogram GetHistogram(IImagePixelSource source, ImageRectangle? roi, int binCount, CancellationToken cancellationToken)
        {
            if (binCount <= 0)
            {
                throw new ArgumentOutOfRangeException("binCount", "直方图分箱数必须大于零。");
            }

            var statistics = Calculate(source, roi, cancellationToken);
            var bins = new int[binCount];
            var bounds = GetPixelBounds(source, roi);
            var span = statistics.Max - statistics.Min;
            for (var y = bounds.Top; y <= bounds.Bottom; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (var x = bounds.Left; x <= bounds.Right; x++)
                {
                    var value = source.GetGrayValue(x, y);
                    var index = span <= 0 ? 0 : (int)((value - statistics.Min) / span * binCount);
                    bins[Math.Min(binCount - 1, Math.Max(0, index))]++;
                }
            }

            return new ImageHistogram(statistics.Min, statistics.Max, bins);
        }

        /// <summary>
        /// 在后台一次任务内生成统计与直方图；底层仍需要两次扫描，
        /// 但两项 UI 数据会一次性回填，避免连续点击造成重复卡顿。
        /// </summary>
        public ImageAnalysisResult Analyze(IImagePixelSource source, ImageRectangle? roi, int binCount, CancellationToken cancellationToken)
        {
            var statistics = Calculate(source, roi, cancellationToken);
            var bins = new int[binCount];
            var bounds = GetPixelBounds(source, roi);
            var span = statistics.Max - statistics.Min;
            for (var y = bounds.Top; y <= bounds.Bottom; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (var x = bounds.Left; x <= bounds.Right; x++)
                {
                    var value = source.GetGrayValue(x, y);
                    var index = span <= 0 ? 0 : (int)((value - statistics.Min) / span * binCount);
                    bins[Math.Min(binCount - 1, Math.Max(0, index))]++;
                }
            }

            return new ImageAnalysisResult(statistics, new ImageHistogram(statistics.Min, statistics.Max, bins));
        }

        public IReadOnlyList<double> GetLineProfile(IImagePixelSource source, ImagePoint start, ImagePoint end)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            var deltaX = end.X - start.X;
            var deltaY = end.Y - start.Y;
            var count = Math.Max(1, (int)Math.Ceiling(Math.Max(Math.Abs(deltaX), Math.Abs(deltaY))));
            var values = new List<double>(count + 1);
            for (var index = 0; index <= count; index++)
            {
                var ratio = (double)index / count;
                var x = Clamp((int)Math.Round(start.X + deltaX * ratio), 0, source.Width - 1);
                var y = Clamp((int)Math.Round(start.Y + deltaY * ratio), 0, source.Height - 1);
                values.Add(source.GetGrayValue(x, y));
            }

            return values;
        }

        private static PixelBounds GetPixelBounds(IImagePixelSource source, ImageRectangle? roi)
        {
            if (source.Width <= 0 || source.Height <= 0)
            {
                throw new ArgumentOutOfRangeException("source", "像素源尺寸必须大于零。");
            }

            if (!roi.HasValue)
            {
                return new PixelBounds(0, 0, source.Width - 1, source.Height - 1);
            }

            var value = roi.Value;
            var left = Clamp((int)Math.Ceiling(value.Left), 0, source.Width - 1);
            var top = Clamp((int)Math.Ceiling(value.Top), 0, source.Height - 1);
            var right = Clamp((int)Math.Floor(value.Right), 0, source.Width - 1);
            var bottom = Clamp((int)Math.Floor(value.Bottom), 0, source.Height - 1);
            if (right < left || bottom < top)
            {
                throw new ArgumentOutOfRangeException("roi", "ROI 与图像没有交集。");
            }

            return new PixelBounds(left, top, right, bottom);
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private struct PixelBounds
        {
            public PixelBounds(int left, int top, int right, int bottom)
            {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }

            public int Left { get; private set; }

            public int Top { get; private set; }

            public int Right { get; private set; }

            public int Bottom { get; private set; }
        }
    }
}
