using System;
using System.Collections.Generic;

namespace CxVisual.Foundation.Numerics
{
    /// <summary>数值多项式拟合；几何空间拟合仍由 Geometry 所有。</summary>
    public static class FNUCurveFitting
    {
        public static FNULeastSquaresResult Polynomial(IReadOnlyList<double> xs,
            IReadOnlyList<double> ys, int degree)
        {
            if (xs == null) throw new ArgumentNullException("xs");
            if (ys == null) throw new ArgumentNullException("ys");
            if (degree < 0 || degree == int.MaxValue) throw new ArgumentOutOfRangeException("degree");
            if (xs.Count != ys.Count || xs.Count < degree + 1)
                throw new ArgumentException("Polynomial fit needs matching observations and sufficient samples.");
            var design = new double[xs.Count, degree + 1];
            var observations = new double[ys.Count];
            for (int i = 0; i < xs.Count; ++i)
            {
                double x = xs[i], y = ys[i];
                if (!Finite(x) || !Finite(y)) throw new ArgumentOutOfRangeException("xs");
                observations[i] = y;
                double power = 1;
                for (int j = 0; j <= degree; ++j)
                {
                    if (!Finite(power)) throw new OverflowException("Polynomial basis overflow.");
                    design[i, j] = power;
                    if (j < degree) power *= x;
                }
            }
            return FNULinearAlgebra.LeastSquares(design, observations);
        }
        private static bool Finite(double x) { return !double.IsNaN(x) && !double.IsInfinity(x); }
    }
}
