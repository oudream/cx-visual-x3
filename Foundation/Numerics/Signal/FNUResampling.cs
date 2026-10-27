using System;
using System.Collections.Generic;
namespace CxVisual.Foundation.Numerics
{
    public static class FNUResampling
    {
        /// <summary>等间隔采样的端点对齐线性重采样；目标只有一个元素时取首值。</summary>
        public static double[] Linear(IReadOnlyList<double> values, int count)
        {
            if (values == null) throw new ArgumentNullException("values");
            if (values.Count == 0) throw new ArgumentException("Resampling requires samples.", "values");
            if (count <= 0) throw new ArgumentOutOfRangeException("count");
            for (int i = 0; i < values.Count; i++)
                if (double.IsNaN(values[i]) || double.IsInfinity(values[i]))
                    throw new ArgumentOutOfRangeException("values");
            var result = new double[count];
            for (int i = 0; i < count; i++)
            {
                double position = count == 1 ? 0 : (double)i * (values.Count - 1) / (count - 1);
                int left = Math.Min(values.Count - 1, (int)position);
                int right = Math.Min(values.Count - 1, left + 1);
                result[i] = FNUInterpolation.Linear(values[left], values[right], position - left);
            }
            return result;
        }
    }
}
