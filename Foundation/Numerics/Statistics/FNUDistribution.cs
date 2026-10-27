using System;
using System.Collections.Generic;

namespace CxVisual.Foundation.Numerics
{
    /// <summary>经验分位数，排序拷贝；p∈[0,1]，相邻秩线性插值，不修改调用者数据。</summary>
    public static class FNUDistribution
    {
        public static double Quantile(IReadOnlyList<double> samples, double probability)
        {
            if (samples == null) throw new ArgumentNullException("samples");
            if (samples.Count == 0) throw new ArgumentException("No observations.", "samples");
            if (double.IsNaN(probability) || probability < 0 || probability > 1)
                throw new ArgumentOutOfRangeException("probability");
            var sorted = new double[samples.Count];
            for (int i = 0; i < samples.Count; i++)
            {
                sorted[i] = samples[i];
                if (double.IsNaN(sorted[i]) || double.IsInfinity(sorted[i]))
                    throw new ArgumentOutOfRangeException("samples");
            }
            Array.Sort(sorted);
            double rank = (sorted.Length - 1) * probability;
            int lower = (int)rank, upper = Math.Min(sorted.Length - 1, lower + 1);
            return FNUInterpolation.Linear(sorted[lower], sorted[upper], rank - lower);
        }
        public static double Median(IReadOnlyList<double> samples) { return Quantile(samples, 0.5); }
    }
}
