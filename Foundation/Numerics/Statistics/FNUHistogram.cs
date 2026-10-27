using System;
using System.Collections.Generic;

namespace CxVisual.Foundation.Numerics
{
    /// <summary>通用区间统计。下限、上限均包含；最大值计入末箱；不解释图像语义。</summary>
    public sealed class FNUHistogram
    {
        private readonly long[] _bins;
        private FNUHistogram(double min, double max, long[] bins)
        {
            Minimum = min; Maximum = max; _bins = bins;
        }
        public double Minimum { get; private set; }
        public double Maximum { get; private set; }
        public int BinCount { get { return _bins.Length; } }
        public long[] CopyBins() { return (long[])_bins.Clone(); }
        public long GetCount(int index) { return _bins[index]; }

        public static FNUHistogram Calculate(IReadOnlyList<double> values, int bins, double min, double max)
        {
            if (values == null) throw new ArgumentNullException("values");
            if (bins < 1 || bins > 65536) throw new ArgumentOutOfRangeException("bins");
            if (!Finite(min) || !Finite(max) || max < min) throw new ArgumentOutOfRangeException("max");
            var counts = new long[bins];
            double range = max - min;
            if (!Finite(range)) throw new ArgumentOutOfRangeException("max", "Bin range must be finite.");
            for (int i = 0; i < values.Count; ++i)
            {
                double v = values[i];
                if (!Finite(v) || v < min || v > max) throw new ArgumentOutOfRangeException("values");
                int index = range == 0 ? 0 : (int)Math.Min(bins - 1, (v - min) / range * bins);
                counts[index]++;
            }
            return new FNUHistogram(min, max, counts);
        }

        private static bool Finite(double x) { return !double.IsNaN(x) && !double.IsInfinity(x); }
    }
}
