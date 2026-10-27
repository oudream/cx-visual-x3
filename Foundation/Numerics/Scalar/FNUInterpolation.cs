using System;
namespace CxVisual.Foundation.Numerics {
    /// <summary>通用双/三线性数值插值，不管理图像/体素边界；允许有限外推。</summary>
    public static class FNUInterpolation {
        private static void Check(double x,string name) {
            if(double.IsNaN(x)||double.IsInfinity(x)) throw new ArgumentOutOfRangeException(name);
        }
        public static double Linear(double a,double b,double t) {
            Check(a,"a");Check(b,"b");Check(t,"t");
            // Preserve ordinary rounding; avoid spurious intermediate overflow
            // for convex interpolation between two finite endpoints.
            if (t == 0) return a;
            if (t == 1) return b;
            double result = a + (b - a) * t;
            if ((double.IsInfinity(result) || double.IsNaN(result)) && t > 0 && t < 1)
                result = (1 - t) * a + t * b;
            if (double.IsInfinity(result) || double.IsNaN(result))
                throw new OverflowException("Interpolation produced a non-finite value.");
            return result;
        }
        public static double Bilinear(double a00,double a10,double a01,double a11,double x,double y) {
            return Linear(Linear(a00,a10,x),Linear(a01,a11,x),y);
        }
        public static double Trilinear(double a000,double a100,double a010,double a110,
            double a001,double a101,double a011,double a111,double x,double y,double z) {
            return Linear(Bilinear(a000,a100,a010,a110,x,y),
                Bilinear(a001,a101,a011,a111,x,y),z);
        }
    }
}
