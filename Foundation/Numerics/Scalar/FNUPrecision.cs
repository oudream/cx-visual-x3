using System;

namespace CxVisual.Foundation.Numerics
{
    /// <summary>包含绝对与相对容差的有限数近似判断。无 NaN/Infinity 特殊相等语义。</summary>
    public static class FNUPrecision
    {
        public static bool NearlyEqual(double a, double b, double absolute = 1e-12,
            double relative = 1e-9)
        {
            if (!Finite(absolute) || !Finite(relative) || absolute < 0 || relative < 0)
                throw new ArgumentOutOfRangeException("absolute");
            if (!Finite(a) || !Finite(b)) return false;
            if (a == b) return true;
            double delta = Math.Abs(a - b);
            if (delta <= absolute) return true;
            double scale = Math.Max(Math.Abs(a), Math.Abs(b));
            return delta / scale <= relative;
        }
        private static bool Finite(double x) { return !double.IsNaN(x) && !double.IsInfinity(x); }
    }
}
