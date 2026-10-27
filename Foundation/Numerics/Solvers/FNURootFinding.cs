using System;
using System.Threading;

namespace CxVisual.Foundation.Numerics
{
    /// <summary>成功收敛的一维根，保留残差与迭代次数。</summary>
    public sealed class FNURootResult
    {
        internal FNURootResult(double root, double residual, int iterations)
        {
            Root = root; Residual = residual; Iterations = iterations;
        }
        public double Root { get; private set; }
        public double Residual { get; private set; }
        public int Iterations { get; private set; }
    }

    public static class FNURootFinding
    {
        /// <summary>二分法；要求有效括号或精确端点根。两端符号相同明确拒绝。</summary>
        public static FNURootResult Bisection(Func<double, double> function, double lower, double upper,
            double tolerance = 1e-10, int maxIterations = 256,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (function == null) throw new ArgumentNullException("function");
            if (!Finite(lower) || !Finite(upper) || lower >= upper)
                throw new ArgumentOutOfRangeException("upper");
            if (!Finite(tolerance) || tolerance <= 0) throw new ArgumentOutOfRangeException("tolerance");
            if (maxIterations <= 0) throw new ArgumentOutOfRangeException("maxIterations");
            cancellationToken.ThrowIfCancellationRequested();
            double left = Checked(function(lower)), right = Checked(function(upper));
            if (left == 0) return new FNURootResult(lower, left, 0);
            if (right == 0) return new FNURootResult(upper, right, 0);
            if (Math.Sign(left) == Math.Sign(right))
                throw new ArgumentException("Function values do not bracket a root.", "function");
            for (int i = 1; i <= maxIterations; ++i)
            {
                cancellationToken.ThrowIfCancellationRequested();
                double middle = lower * 0.5 + upper * 0.5;
                if (middle == lower || middle == upper)
                    throw new InvalidOperationException("Bisection cannot progress at machine precision.");
                double value = Checked(function(middle));
                if (value == 0 || Math.Abs(upper - lower) <= 2 * tolerance ||
                    Math.Abs(value) <= tolerance)
                    return new FNURootResult(middle, value, i);
                if (Math.Sign(left) == Math.Sign(value)) { lower = middle; left = value; }
                else { upper = middle; right = value; }
            }
            throw new InvalidOperationException("Bisection did not converge in the iteration budget.");
        }

        private static bool Finite(double x) { return !double.IsNaN(x) && !double.IsInfinity(x); }
        private static double Checked(double x)
        {
            if (!Finite(x)) throw new ArithmeticException("Function returned a non-finite value.");
            return x;
        }
    }
}
