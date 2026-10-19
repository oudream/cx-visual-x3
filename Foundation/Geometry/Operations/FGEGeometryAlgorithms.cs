using System;
using System.Collections.Generic;

namespace CxVisual.Foundation.Geometry
{
    /// <summary>无状态二维几何运算；几何容差由调用者显式传入，不依赖 DPI/像素。</summary>
    public static class FGEGeometry2D
    {
        public static FGEPoint2D ClosestPointOnSegment(FGEPoint2D p,FGESegment2D s)
        {
            FGEVector2D v=s.End-s.Start;double n=v.LengthSquared;
            if(n==0)return s.Start;
            double t=FGEVector2D.Dot(p-s.Start,v)/n;
            return s.PointAt(Math.Max(0,Math.Min(1,t)));
        }
        public static double DistanceToSegment(FGEPoint2D p,FGESegment2D s)
        {return p.DistanceTo(ClosestPointOnSegment(p,s));}
        public static bool PointOnSegment(FGEPoint2D p,FGESegment2D s,double tolerance=1e-9)
        {if(tolerance<0||double.IsNaN(tolerance))throw new ArgumentOutOfRangeException("tolerance");return DistanceToSegment(p,s)<=tolerance;}
        public static bool TryIntersectSegments(FGESegment2D a,FGESegment2D b,out FGEPoint2D intersection,double tolerance=1e-12)
        {
            if(tolerance<0||double.IsNaN(tolerance))throw new ArgumentOutOfRangeException("tolerance");
            FGEVector2D r=a.End-a.Start,s=b.End-b.Start,q=b.Start-a.Start;double cross=FGEVector2D.Cross(r,s);
            if(Math.Abs(cross)<=tolerance){intersection=default(FGEPoint2D);return false;} // 重叠线段应走专用区间查询
            double t=FGEVector2D.Cross(q,s)/cross,u=FGEVector2D.Cross(q,r)/cross;
            if(t<0||t>1||u<0||u>1){intersection=default(FGEPoint2D);return false;}
            intersection=a.PointAt(t);return true;
        }
        /// <summary>EvenOdd 点包含；边界视为包含，支持自交轮廓；复杂多环请分别组合。</summary>
        public static bool PointInPolygon(FGEPoint2D p,IReadOnlyList<FGEPoint2D> points)
        {
            if(points==null)throw new ArgumentNullException("points");
            if(points.Count<3)return false;
            bool inside=false;int j=points.Count-1;
            for(int i=0;i<points.Count;i++)
            {
                FGEPoint2D a=points[j],b=points[i];
                if(PointOnSegment(p,new FGESegment2D(a,b)))return true;
                if((a.Y>p.Y)!=(b.Y>p.Y))
                {
                    double x=a.X+(p.Y-a.Y)*(b.X-a.X)/(b.Y-a.Y);
                    if(p.X<x)inside=!inside;
                }
                j=i;
            }
            return inside;
        }
        public static FGERect2D Bounds(IReadOnlyList<FGEPoint2D> points)
        {
            if(points==null||points.Count==0)throw new ArgumentException("At least one point required.","points");
            double l=points[0].X,r=l,t=points[0].Y,b=t;
            for(int i=1;i<points.Count;i++){l=Math.Min(l,points[i].X);r=Math.Max(r,points[i].X);t=Math.Min(t,points[i].Y);b=Math.Max(b,points[i].Y);}
            return new FGERect2D(l,t,r-l,b-t);
        }
    }
}
