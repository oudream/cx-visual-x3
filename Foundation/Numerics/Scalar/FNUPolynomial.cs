using System;
using System.Collections.Generic;

namespace CxVisual.Foundation.Numerics
{
    /// <summary>多项式系数按常数项到最高次项排序。纯数值算法，不绑定几何曲线。</summary>
    public static class FNUPolynomial
    {
        public static double Evaluate(IReadOnlyList<double> coefficients, double x)
        {
            if (coefficients == null) throw new ArgumentNullException("coefficients");
            if (coefficients.Count == 0) throw new ArgumentException("Polynomial must have coefficients.", "coefficients");
            RequireFinite(x, "x");
            double result = 0;
            for (int i = coefficients.Count - 1; i >= 0; --i)
            {
                RequireFinite(coefficients[i], "coefficients");
                result = result * x + coefficients[i];
            }
            if (double.IsNaN(result) || double.IsInfinity(result))
                throw new OverflowException("Polynomial evaluation overflow.");
            return result;
        }

        public static double[] Derivative(IReadOnlyList<double> coefficients)
        {
            if (coefficients == null) throw new ArgumentNullException("coefficients");
            if (coefficients.Count == 0) throw new ArgumentException("Polynomial must have coefficients.", "coefficients");
            var result = new double[Math.Max(1, coefficients.Count - 1)];
            for (int i = 0; i < coefficients.Count; ++i)
            {
                RequireFinite(coefficients[i], "coefficients");
                if (i > 0)
                {
                    result[i - 1] = i * coefficients[i];
                    if (double.IsNaN(result[i - 1]) || double.IsInfinity(result[i - 1]))
                        throw new OverflowException("Polynomial derivative overflow.");
                }
            }
            return result;
        }

        private static void RequireFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(name);
        }
    }
}
