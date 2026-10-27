using System;
using System.Threading;

namespace CxVisual.Foundation.Numerics
{
    public static class FNUNumericalIntegration
    {
        /// <summary>复合 Simpson 1/3 法；子区间数必须为正偶数，范围可反向。</summary>
        public static double Simpson(Func<double, double> function, double start, double end,
            int intervals = 256, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (function == null) throw new ArgumentNullException("function");
            if (!Finite(start) || !Finite(end)) throw new ArgumentOutOfRangeException("start");
            if (intervals < 2 || (intervals & 1) != 0) throw new ArgumentOutOfRangeException("intervals");
            cancellationToken.ThrowIfCancellationRequested();
            if (start == end) return 0;
            double step = (end - start) / intervals;
            if (!Finite(step) || step == 0) throw new ArgumentOutOfRangeException("end");
            double sum = Checked(function(start)) + Checked(function(end));
            for (int i = 1; i < intervals; ++i)
            {
                cancellationToken.ThrowIfCancellationRequested();
                double x = start + i * step;
                sum += (i % 2 == 0 ? 2 : 4) * Checked(function(x));
            }
            double result = sum * step / 3;
            if (!Finite(result)) throw new OverflowException("Integration overflow.");
            return result;
        }
        private static bool Finite(double x) { return !double.IsNaN(x) && !double.IsInfinity(x); }
        private static double Checked(double x)
        {
            if (!Finite(x)) throw new ArithmeticException("Function returned a non-finite value.");
            return x;
        }
    }
}
