using System;
using CxVisual.Foundation.Core;
namespace CxVisual.Foundation.Geometry
{
    public struct CxRect2D
    {
        public CxRect2D(double x, double y, double width, double height)
        {
            if (width < 0 || height < 0 || double.IsNaN(x) || double.IsNaN(y))
                throw new ArgumentOutOfRangeException("Rectangle bounds must be valid");
            CxNumericGuard.RequireFinite(x, "x");
            CxNumericGuard.RequireFinite(y, "y");
            CxNumericGuard.RequireFinite(width, "width");
            CxNumericGuard.RequireFinite(height, "height");
            X = x; Y = y; Width = width; Height = height;
        }
        public double X { get; private set; }
        public double Y { get; private set; }
        public double Width { get; private set; }
        public double Height { get; private set; }
        public bool Contains(CxPoint2D point)
        {
            return point.X >= X && point.Y >= Y && point.X <= X + Width && point.Y <= Y + Height;
        }
        public bool Intersects(CxRect2D other)
        {
            return X < other.X + other.Width && other.X < X + Width
                && Y < other.Y + other.Height && other.Y < Y + Height;
        }
    }

    /// <summary>无WPF依赖的二维仿射变换；数学变换组合独立于UI。</summary>
}
