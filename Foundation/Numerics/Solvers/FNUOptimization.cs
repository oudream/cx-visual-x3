using System;
using System.Threading;
namespace CxVisual.Foundation.Numerics
{
    /// <summary>有界单峰优化的成功结果；非单峰区间不保证全局最优。</summary>
    public sealed class FNUOptimizationResult
    {
        internal FNUOptimizationResult(double position, double value, int iterations)
        { Position = position; Value = value; Iterations = iterations; }
        public double Position { get; private set; }
        public double Value { get; private set; }
        public int Iterations { get; private set; }
    }
    public static class FNUOptimization
    {
        /// <summary>黄金分割最小化，要求连续单峰函数；容差是 x 坐标的绝对误差预算。</summary>
        public static FNUOptimizationResult GoldenSection(Func<double, double> function,
            double lower, double upper, double tolerance = 1e-9,
            int maxIterations = 512, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (function == null) throw new ArgumentNullException("function");
            if (!Finite(lower) || !Finite(upper) || lower >= upper)
                throw new ArgumentOutOfRangeException("upper");
            if (!Finite(tolerance) || tolerance <= 0)
                throw new ArgumentOutOfRangeException("tolerance");
            if (maxIterations <= 0) throw new ArgumentOutOfRangeException("maxIterations");
            cancellationToken.ThrowIfCancellationRequested();
            double phi = (Math.Sqrt(5) - 1) / 2;
            double c = upper - phi * (upper - lower);
            double d = lower + phi * (upper - lower);
            double fc = Checked(function(c)), fd = Checked(function(d));
            for (int i = 0; i < maxIterations; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (upper - lower <= tolerance)
                {
                    double x = lower / 2 + upper / 2;
                    return new FNUOptimizationResult(x, Checked(function(x)), i);
                }
                if (fc <= fd)
                {
                    upper = d; d = c; fd = fc;
                    c = upper - phi * (upper - lower);
                    fc = Checked(function(c));
                }
                else
                {
                    lower = c; c = d; fc = fd;
                    d = lower + phi * (upper - lower);
                    fd = Checked(function(d));
                }
            }
            throw new InvalidOperationException("Golden section did not converge in the iteration budget.");
        }
        private static bool Finite(double x) { return !double.IsNaN(x) && !double.IsInfinity(x); }
        private static double Checked(double x)
        {
            if (!Finite(x)) throw new ArithmeticException("Objective returned a non-finite value.");
            return x;
        }
    }
}
