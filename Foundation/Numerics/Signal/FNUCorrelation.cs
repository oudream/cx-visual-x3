using System;
using System.Collections.Generic;
namespace CxVisual.Foundation.Numerics
{
    /// <summary>完整互相关，输出索引i对应滞后 lag=i-(b.Count-1)。</summary>
    public static class FNUCorrelation
    {
        public static double[] Full(IReadOnlyList<double> a, IReadOnlyList<double> b)
        {
            if (a == null) throw new ArgumentNullException("a");
            if (b == null) throw new ArgumentNullException("b");
            if (a.Count == 0 || b.Count == 0) throw new ArgumentException("Cross correlation requires two sequences.");
            int length = checked(a.Count + b.Count - 1);
            for (int i = 0; i < a.Count; i++) Check(a[i]);
            for (int i = 0; i < b.Count; i++) Check(b[i]);
            double[] result = new double[length];
            for (int lag = 1 - b.Count; lag < a.Count; lag++)
            {
                double sum = 0;
                for (int j = 0; j < b.Count; j++)
                {
                    int k = j + lag;
                    if (k >= 0 && k < a.Count) sum += a[k] * b[j];
                }
                if (double.IsNaN(sum) || double.IsInfinity(sum))
                    throw new OverflowException("Cross-correlation overflow.");
                result[lag + b.Count - 1] = sum;
            }
            return result;
        }
        private static void Check(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) throw new ArgumentOutOfRangeException("values");
        }
    }
}
