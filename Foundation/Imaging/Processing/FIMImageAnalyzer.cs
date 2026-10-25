using System;
using System.Collections.Generic;
using System.Threading;

namespace CxVisual.Foundation.Imaging
{
    /// <summary>原始像素统计、直方图、灰度剖面与五区域灰度值。</summary>
    public static class FIMImageAnalyzer
    {
        public static FIMImageStatistics Calculate(FIMImageBuffer image, FIMPixelRegion? roi = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (image == null) throw new ArgumentNullException("image");
            FIMPixelRegion r = roi.HasValue ? roi.Value.ClipTo(image) : FIMPixelRegion.Entire(image);
            long count = 0;
            double min = double.PositiveInfinity, max = double.NegativeInfinity, mean = 0, m2 = 0;
            for (int y = r.Top; y <= r.Bottom; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (int x = r.Left; x <= r.Right; x++)
                {
                    if ((x & 2047) == 0) cancellationToken.ThrowIfCancellationRequested();
                    double value = image.GetGrayValue(x, y);
                    if (double.IsNaN(value) || double.IsInfinity(value))
                        throw new InvalidOperationException("图像包含非有限灰度值。");
                    count++;
                    min = Math.Min(min, value);
                    max = Math.Max(max, value);
                    double delta = value - mean;
                    mean += delta / count;
                    m2 += delta * (value - mean);
                }
            }
            return new FIMImageStatistics(count, min, max, mean, Math.Sqrt(Math.Max(0, m2 / count)));
        }

        public static FIMImageAnalysisResult Analyze(FIMImageBuffer image, int binCount,
            FIMPixelRegion? roi = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (binCount < 1 || binCount > 65536) throw new ArgumentOutOfRangeException("binCount");
            FIMImageStatistics stats = Calculate(image, roi, cancellationToken);
            FIMPixelRegion r = roi.HasValue ? roi.Value.ClipTo(image) : FIMPixelRegion.Entire(image);
            long[] bins = new long[binCount];
            double span = stats.Max - stats.Min;
            for (int y = r.Top; y <= r.Bottom; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (int x = r.Left; x <= r.Right; x++)
                {
                    if ((x & 2047) == 0) cancellationToken.ThrowIfCancellationRequested();
                    double value = image.GetGrayValue(x, y);
                    int index = span <= 0 ? 0 : (int)((value - stats.Min) / span * binCount);
                    bins[Math.Max(0, Math.Min(binCount - 1, index))]++;
                }
            }
            return new FIMImageAnalysisResult(stats, new FIMImageHistogram(stats.Min, stats.Max, bins));
        }

        public static FIMImageHistogram GetHistogram(FIMImageBuffer image, int binCount,
            FIMPixelRegion? roi = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Analyze(image, binCount, roi, cancellationToken).Histogram;
        }

        public static IReadOnlyList<double> GetLineProfile(FIMImageBuffer image,
            int startX, int startY, int endX, int endY)
        {
            if (image == null) throw new ArgumentNullException("image");
            long dx = (long)endX - startX, dy = (long)endY - startY;
            long steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
            if (steps > 10000000) throw new ArgumentOutOfRangeException("endX");
            int segments = (int)Math.Max(1, steps);
            double[] values = new double[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                double ratio = (double)i / segments;
                int x = (int)Math.Max(0, Math.Min(image.Width - 1, Math.Round(startX + ratio * dx)));
                int y = (int)Math.Max(0, Math.Min(image.Height - 1, Math.Round(startY + ratio * dy)));
                values[i] = image.GetGrayValue(x, y);
            }
            return Array.AsReadOnly(values);
        }

        /// <summary>左上、右上、左下、右下、中心；继承旧HALCON五区域通用计算语义。</summary>
        public static FIMFiveRegionResult CalculateFiveRegions(FIMImageBuffer image,
            int boxWidth, int boxHeight, int margin,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (image == null) throw new ArgumentNullException("image");
            if (boxWidth <= 0 || boxWidth > image.Width) throw new ArgumentOutOfRangeException("boxWidth");
            if (boxHeight <= 0 || boxHeight > image.Height) throw new ArgumentOutOfRangeException("boxHeight");
            if (margin < 0) throw new ArgumentOutOfRangeException("margin");
            int maxX = image.Width - boxWidth, maxY = image.Height - boxHeight;
            int nearX = Math.Min(margin, maxX / 2), nearY = Math.Min(margin, maxY / 2);
            int[] xs = { nearX, maxX - nearX, nearX, maxX - nearX, maxX / 2 };
            int[] ys = { nearY, nearY, maxY - nearY, maxY - nearY, maxY / 2 };
            FIMPixelRegion[] regions = new FIMPixelRegion[5];
            double[] means = new double[5];
            for (int i = 0; i < 5; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                regions[i] = new FIMPixelRegion(xs[i], ys[i],
                    xs[i] + boxWidth - 1, ys[i] + boxHeight - 1);
                means[i] = Calculate(image, regions[i], cancellationToken).Mean;
            }
            return new FIMFiveRegionResult(regions, means);
        }
    }
}
