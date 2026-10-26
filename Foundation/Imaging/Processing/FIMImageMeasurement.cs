using System;
using CxVisual.Foundation.Geometry;

namespace CxVisual.Foundation.Imaging
{
    /// <summary>
    /// 直接承接旧ImageMeasurementService的像素线长、面积、角度与标定间距计算。
    /// 复用已有FGEPoint2D，屏幕Zoom/Pan/旋转不影响结果；未标定返回null。
    /// </summary>
    public static class FIMImageMeasurement
    {
        private static void ValidateSpacing(FIMPixelSpacing spacing)
        {
            if (double.IsNaN(spacing.MillimetersPerPixelX) ||
                double.IsInfinity(spacing.MillimetersPerPixelX) ||
                spacing.MillimetersPerPixelX <= 0 ||
                double.IsNaN(spacing.MillimetersPerPixelY) ||
                double.IsInfinity(spacing.MillimetersPerPixelY) ||
                spacing.MillimetersPerPixelY <= 0)
                throw new ArgumentOutOfRangeException("spacing", "默认或无效像素间距不能用于物理测量。");
        }

        public static double GetDistancePixels(FGEPoint2D first, FGEPoint2D second)
        {
            double dx = second.X - first.X;
            double dy = second.Y - first.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public static double? GetDistanceMillimeters(FGEPoint2D first, FGEPoint2D second,
            FIMPixelSpacing? spacing)
        {
            if (!spacing.HasValue) return null;
            ValidateSpacing(spacing.Value);
            double dx = (second.X - first.X) * spacing.Value.MillimetersPerPixelX;
            double dy = (second.Y - first.Y) * spacing.Value.MillimetersPerPixelY;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public static double GetAngleDegrees(FGEPoint2D first, FGEPoint2D vertex, FGEPoint2D third)
        {
            double ax = first.X - vertex.X, ay = first.Y - vertex.Y;
            double bx = third.X - vertex.X, by = third.Y - vertex.Y;
            double lengthA = Math.Sqrt(ax * ax + ay * ay);
            double lengthB = Math.Sqrt(bx * bx + by * by);
            if (lengthA <= 0 || lengthB <= 0)
                throw new ArgumentException("角度测量的两条边必须具有非零长度。");
            double cosine = (ax * bx + ay * by) / (lengthA * lengthB);
            cosine = Math.Max(-1, Math.Min(1, cosine));
            return Math.Acos(cosine) * 180.0 / Math.PI;
        }

        /// <summary>横/纵距离保留原图坐标，物理量使用各轴独立标定，避免显示缩放污染。</summary>
        public static double GetHorizontalDistancePixels(FGEPoint2D a, FGEPoint2D b)
        { return Math.Abs(b.X - a.X); }
        public static double GetVerticalDistancePixels(FGEPoint2D a, FGEPoint2D b)
        { return Math.Abs(b.Y - a.Y); }
        public static double? GetHorizontalDistanceMillimeters(FGEPoint2D a, FGEPoint2D b,
            FIMPixelSpacing? spacing)
        {
            if (!spacing.HasValue) return null;
            ValidateSpacing(spacing.Value);
            return GetHorizontalDistancePixels(a,b) * spacing.Value.MillimetersPerPixelX;
        }
        public static double? GetVerticalDistanceMillimeters(FGEPoint2D a, FGEPoint2D b,
            FIMPixelSpacing? spacing)
        {
            if (!spacing.HasValue) return null;
            ValidateSpacing(spacing.Value);
            return GetVerticalDistancePixels(a,b) * spacing.Value.MillimetersPerPixelY;
        }
        /// <summary>连续几何面积与离散ROI像素计数严格区分。</summary>
        public static double GetRectangleAreaPixels(FGERect2D bounds)
        { return bounds.Width * bounds.Height; }
        public static double? GetRectangleAreaSquareMillimeters(FGERect2D bounds,
            FIMPixelSpacing? spacing)
        {
            if (!spacing.HasValue) return null;
            ValidateSpacing(spacing.Value);
            return GetRectangleAreaPixels(bounds) *
                spacing.Value.MillimetersPerPixelX * spacing.Value.MillimetersPerPixelY;
        }
        /// <summary>闭合多边形鞋带公式；面积与显示旋转、镜像无关。</summary>
        public static double GetPolygonAreaPixels(System.Collections.Generic.IReadOnlyList<FGEPoint2D> points)
        {
            if (points == null) throw new ArgumentNullException("points");
            if (points.Count < 3) throw new ArgumentException("多边形至少需要三个顶点。", "points");
            double sum = 0;
            FGEPoint2D origin = points[0];
            for (int i = 1; i < points.Count - 1; i++)
            {
                double ax = points[i].X - origin.X, ay = points[i].Y - origin.Y;
                double bx = points[i+1].X - origin.X, by = points[i+1].Y - origin.Y;
                sum += ax * by - bx * ay;
            }
            if (double.IsNaN(sum) || double.IsInfinity(sum))
                throw new OverflowException("多边形面积超出有限数值。");
            return Math.Abs(sum) * 0.5;
        }
        public static double? GetPolygonAreaSquareMillimeters(
            System.Collections.Generic.IReadOnlyList<FGEPoint2D> points, FIMPixelSpacing? spacing)
        {
            if (!spacing.HasValue) return null;
            ValidateSpacing(spacing.Value);
            return GetPolygonAreaPixels(points) *
                spacing.Value.MillimetersPerPixelX * spacing.Value.MillimetersPerPixelY;
        }

        public static long GetAreaPixels(FIMPixelRegion region)
        {
            return checked((long)region.Width * region.Height);
        }

        public static double? GetAreaSquareMillimeters(FIMPixelRegion region, FIMPixelSpacing? spacing)
        {
            if (!spacing.HasValue) return null;
            ValidateSpacing(spacing.Value);
            return GetAreaPixels(region) *
                spacing.Value.MillimetersPerPixelX * spacing.Value.MillimetersPerPixelY;
        }
    }
}
