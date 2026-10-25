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

        public static long GetAreaPixels(FIMPixelRegion region)
        {
            return checked((long)region.Width * region.Height);
        }

        public static double? GetAreaSquareMillimeters(FIMPixelRegion region, FIMPixelSpacing? spacing)
        {
            if (!spacing.HasValue) return null;
            return GetAreaPixels(region) *
                spacing.Value.MillimetersPerPixelX * spacing.Value.MillimetersPerPixelY;
        }
    }
}
