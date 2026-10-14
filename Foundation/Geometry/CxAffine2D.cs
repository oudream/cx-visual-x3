using System;
using CxVisual.Foundation.Core;
namespace CxVisual.Foundation.Geometry
{
    public struct CxAffine2D
    {
        public CxAffine2D(double m11, double m12, double m21, double m22, double dx, double dy)
        { M11 = m11; M12 = m12; M21 = m21; M22 = m22; DX = dx; DY = dy; }
        public double M11 { get; private set; }
        public double M12 { get; private set; }
        public double M21 { get; private set; }
        public double M22 { get; private set; }
        public double DX { get; private set; }
        public double DY { get; private set; }
        public static CxAffine2D Identity { get { return new CxAffine2D(1, 0, 0, 1, 0, 0); } }
        public CxPoint2D Transform(CxPoint2D p)
        { return new CxPoint2D(M11 * p.X + M21 * p.Y + DX, M12 * p.X + M22 * p.Y + DY); }
    }
}
