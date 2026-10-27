using System;
using CxVisual.Foundation.Geometry;

namespace CxVisual.Foundation.Imaging
{
    /// <summary>
    /// 直接承接旧ImageMeasurementService的像素线长、面积、角度与标定间距计算。
    /// 复用已有FGEPoint2D，屏幕Zoom/Pan/旋转不影响结果；未标定返回null。
    /// </summary>
    /// <summary>两线段四点测量结果，最近点保持连续原图精度，不像旧实现舍入为整数。</summary>
    public sealed class FIMLinePairResult
    {
        internal FIMLinePairResult(FGEPoint2D onFirst,FGEPoint2D onSecond,
            double distancePixels,double? distanceMillimeters,double angleDegrees)
        {
            ClosestOnFirst=onFirst;ClosestOnSecond=onSecond;
            DistancePixels=distancePixels;DistanceMillimeters=distanceMillimeters;
            AngleDegrees=angleDegrees;
        }
        public FGEPoint2D ClosestOnFirst { get; private set; }
        public FGEPoint2D ClosestOnSecond { get; private set; }
        public double DistancePixels { get; private set; }
        public double? DistanceMillimeters { get; private set; }
        public double AngleDegrees { get; private set; }
    }

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

        /// <summary>
        /// 旧HWindowControlView的Ls2Ls语义：四点两条有限线段的最近距离、端点和0..180°方向角。
        /// 相交时最近距离为0；不把无限直线的交点误用为线段间距。
        /// </summary>
        public static FIMLinePairResult MeasureLinePair(FGEPoint2D a,FGEPoint2D b,
            FGEPoint2D c,FGEPoint2D d,FIMPixelSpacing? spacing=null)
        {
            double ux=b.X-a.X,uy=b.Y-a.Y,vx=d.X-c.X,vy=d.Y-c.Y;
            double au=ux*ux+uy*uy,bv=vx*vx+vy*vy;
            if(au<=1e-16 || bv<=1e-16 ||
                double.IsInfinity(au) || double.IsInfinity(bv))
                throw new ArgumentException("两条测量线段必须是有限的非零长度。");
            if(spacing.HasValue)ValidateSpacing(spacing.Value);
            double cos=(ux*vx+uy*vy)/Math.Sqrt(au*bv);
            double angle=Math.Acos(Math.Max(-1,Math.Min(1,cos)))*180/Math.PI;
            FGEPoint2D bestA=a,bestB=c;
            double best=double.PositiveInfinity;
            // 在二维平面中，若不相交，有限线段的最短距离必至少有一个端点。
            double cross=ux*vy-uy*vx;
            double wx=c.X-a.X,wy=c.Y-a.Y;
            double tolerance=1e-12*Math.Sqrt(au*bv);
            if(Math.Abs(cross)>tolerance)
            {
                double t=(wx*vy-wy*vx)/cross;
                double u=(wx*uy-wy*ux)/cross;
                if(t>=0 && t<=1 && u>=0 && u<=1)
                {
                    bestA=new FGEPoint2D(a.X+t*ux,a.Y+t*uy);
                    bestB=bestA;
                    best=0;
                }
            }
            if(best>0)
            {
                FGEPoint2D[] endpoints={a,b,c,d};
                for(int i=0;i<4;i++)
                {
                    bool onFirst=i<2;
                    FGEPoint2D p=endpoints[i];
                    FGEPoint2D start=onFirst?c:a;
                    double dx=onFirst?vx:ux,dy=onFirst?vy:uy;
                    double len=onFirst?bv:au;
                    double t=Math.Max(0,Math.Min(1,
                        ((p.X-start.X)*dx+(p.Y-start.Y)*dy)/len));
                    FGEPoint2D foot=new FGEPoint2D(start.X+t*dx,start.Y+t*dy);
                    double dist=GetDistancePixels(p,foot);
                    if(dist<best)
                    {
                        best=dist;
                        bestA=onFirst?p:foot;
                        bestB=onFirst?foot:p;
                    }
                }
            }
            // 非等距标定会改变线段最近点的位置，不能把像素最近点直接换算为毫米最短距离。
            double? physical=null;
            if(spacing.HasValue)
            {
                double sx=spacing.Value.MillimetersPerPixelX;
                double sy=spacing.Value.MillimetersPerPixelY;
                FIMLinePairResult physicalGeometry=MeasureLinePair(
                    new FGEPoint2D(a.X*sx,a.Y*sy),new FGEPoint2D(b.X*sx,b.Y*sy),
                    new FGEPoint2D(c.X*sx,c.Y*sy),new FGEPoint2D(d.X*sx,d.Y*sy));
                physical=physicalGeometry.DistancePixels;
            }
            return new FIMLinePairResult(bestA,bestB,best,physical,angle);
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
