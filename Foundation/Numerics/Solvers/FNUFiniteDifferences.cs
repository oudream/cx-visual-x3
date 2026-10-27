using System;
namespace CxVisual.Foundation.Numerics
{
    public static class FNUFiniteDifferences
    {
        /// <summary>中心差分一阶导数。步长由调用者明确指定，不能代替符号/自动微分。</summary>
        public static double CentralDerivative(Func<double, double> function, double x, double step)
        {
            if (function == null) throw new ArgumentNullException("function");
            if (!Finite(x) || !Finite(step) || step <= 0) throw new ArgumentOutOfRangeException("step");
            double left = x - step, right = x + step;
            if (!Finite(left) || !Finite(right) || left == x || right == x)
                throw new ArgumentOutOfRangeException("step", "Finite-difference step is not representable.");
            double a = function(left), b = function(right);
            if (!Finite(a) || !Finite(b)) throw new ArithmeticException("Derivative function returned non-finite values.");
            double derivative = (b - a) / (2 * step);
            if (!Finite(derivative)) throw new OverflowException("Numerical derivative overflow.");
            return derivative;
        }
        private static bool Finite(double x) { return !double.IsNaN(x) && !double.IsInfinity(x); }
    }
}
