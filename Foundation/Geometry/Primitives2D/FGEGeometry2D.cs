using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CxVisual.Foundation.Geometry
{
    /// <summary>纯几何坐标；单位和坐标系由调用者显式约定。</summary>
    public static class FGEValidation
    {
        public static double Finite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(name, "Geometry values must be finite.");
            return value;
        }
    }

    public struct FGEPoint2D : IEquatable<FGEPoint2D>
    {
        public FGEPoint2D(double x, double y) { X = FGEValidation.Finite(x, "x"); Y = FGEValidation.Finite(y, "y"); }
        public double X { get; private set; }
        public double Y { get; private set; }
        public static FGEPoint2D operator +(FGEPoint2D p, FGEVector2D v) { return new FGEPoint2D(p.X + v.X, p.Y + v.Y); }
        public static FGEVector2D operator -(FGEPoint2D a, FGEPoint2D b) { return new FGEVector2D(a.X - b.X, a.Y - b.Y); }
        public double DistanceTo(FGEPoint2D other) { return (this - other).Length; }
        public bool Equals(FGEPoint2D other) { return X == other.X && Y == other.Y; }
        public override bool Equals(object obj) { return obj is FGEPoint2D && Equals((FGEPoint2D)obj); }
        public override int GetHashCode() { unchecked { return X.GetHashCode() * 397 ^ Y.GetHashCode(); } }
        public override string ToString() { return "(" + X.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," + Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ")"; }
    }

    public struct FGEVector2D : IEquatable<FGEVector2D>
    {
        public FGEVector2D(double x, double y) { X = FGEValidation.Finite(x, "x"); Y = FGEValidation.Finite(y, "y"); }
        public double X { get; private set; }
        public double Y { get; private set; }
        public double Length { get { return Math.Sqrt(X * X + Y * Y); } }
        public double LengthSquared { get { return X * X + Y * Y; } }
        public FGEVector2D Normalize() { var n = Length; if (n <= 0 || double.IsInfinity(n)) throw new InvalidOperationException("geometry.zero_or_overflow_vector"); return this / n; }
        public static double Dot(FGEVector2D a, FGEVector2D b) { return a.X * b.X + a.Y * b.Y; }
        public static double Cross(FGEVector2D a, FGEVector2D b) { return a.X * b.Y - a.Y * b.X; }
        public static FGEVector2D operator +(FGEVector2D a, FGEVector2D b) { return new FGEVector2D(a.X+b.X,a.Y+b.Y); }
        public static FGEVector2D operator -(FGEVector2D a, FGEVector2D b) { return new FGEVector2D(a.X-b.X,a.Y-b.Y); }
        public static FGEVector2D operator *(FGEVector2D a, double k) { return new FGEVector2D(a.X*k,a.Y*k); }
        public static FGEVector2D operator /(FGEVector2D a, double k) { if (k==0) throw new DivideByZeroException(); return new FGEVector2D(a.X/k,a.Y/k); }
        public bool Equals(FGEVector2D other) { return X == other.X && Y == other.Y; }
        public override bool Equals(object obj) { return obj is FGEVector2D && Equals((FGEVector2D)obj); }
        public override int GetHashCode() { unchecked { return X.GetHashCode()*397 ^ Y.GetHashCode(); } }
    }

    public struct FGESize2D
    {
        public FGESize2D(double width, double height) { Width=FGEValidation.Finite(width,"width"); Height=FGEValidation.Finite(height,"height"); if(width<0||height<0) throw new ArgumentOutOfRangeException("size"); }
        public double Width { get; private set; } public double Height { get; private set; }
    }

    /// <summary>连续几何矩形；Contains 包含边界，Intersects 仅在正面积重叠时为真；退化矩形有效。</summary>
    public struct FGERect2D : IEquatable<FGERect2D>
    {
        public FGERect2D(double x,double y,double width,double height)
        {
            X=FGEValidation.Finite(x,"x");Y=FGEValidation.Finite(y,"y");
            Width=FGEValidation.Finite(width,"width");Height=FGEValidation.Finite(height,"height");
            if(width<0||height<0) throw new ArgumentOutOfRangeException("size");
        }
        public double X {get;private set;} public double Y {get;private set;}
        public double Width {get;private set;} public double Height {get;private set;}
        public double Left {get{return X;}} public double Top {get{return Y;}}
        public double Right {get{return X+Width;}} public double Bottom {get{return Y+Height;}}
        public bool IsDegenerate {get{return Width==0||Height==0;}}
        public bool Contains(FGEPoint2D p) {return p.X>=Left&&p.X<=Right&&p.Y>=Top&&p.Y<=Bottom;}
        public bool Intersects(FGERect2D other) {return Left<other.Right&&other.Left<Right&&Top<other.Bottom&&other.Top<Bottom;}
        public FGERect2D Union(FGERect2D other)
        {double x=Math.Min(Left,other.Left),y=Math.Min(Top,other.Top);return new FGERect2D(x,y,Math.Max(Right,other.Right)-x,Math.Max(Bottom,other.Bottom)-y);}
        public FGERect2D Intersection(FGERect2D other)
        {double x=Math.Max(Left,other.Left),y=Math.Max(Top,other.Top),r=Math.Min(Right,other.Right),b=Math.Min(Bottom,other.Bottom);return new FGERect2D(x,y,Math.Max(0,r-x),Math.Max(0,b-y));}
        public FGERect2D Inflate(double thickness)
        {FGEValidation.Finite(thickness,"thickness");if(Width+2*thickness<0||Height+2*thickness<0)throw new ArgumentOutOfRangeException("thickness");return new FGERect2D(X-thickness,Y-thickness,Width+2*thickness,Height+2*thickness);}
        public bool Equals(FGERect2D o) {return X==o.X&&Y==o.Y&&Width==o.Width&&Height==o.Height;}
        public override bool Equals(object obj) {return obj is FGERect2D && Equals((FGERect2D)obj);}
        public override int GetHashCode() {unchecked{return X.GetHashCode()^Y.GetHashCode()*397^Width.GetHashCode()*17^Height.GetHashCode();}}
        public static FGERect2D FromPoints(FGEPoint2D a,FGEPoint2D b)
        {double x=Math.Min(a.X,b.X),y=Math.Min(a.Y,b.Y);return new FGERect2D(x,y,Math.Max(a.X,b.X)-x,Math.Max(a.Y,b.Y)-y);}
    }

    public struct FGESegment2D
    {
        public FGESegment2D(FGEPoint2D start,FGEPoint2D end){Start=start;End=end;}
        public FGEPoint2D Start{get;private set;}public FGEPoint2D End{get;private set;}
        public double Length{get{return Start.DistanceTo(End);}}
        public FGERect2D Bounds{get{return FGERect2D.FromPoints(Start,End);}}
        public FGEPoint2D PointAt(double t){FGEValidation.Finite(t,"t");return Start+(End-Start)*t;}
    }

    public struct FGECircle2D
    {
        public FGECircle2D(FGEPoint2D center,double radius){Center=center;Radius=FGEValidation.Finite(radius,"radius");if(radius<0)throw new ArgumentOutOfRangeException("radius");}
        public FGEPoint2D Center{get;private set;}public double Radius{get;private set;}
        public FGERect2D Bounds{get{return new FGERect2D(Center.X-Radius,Center.Y-Radius,2*Radius,2*Radius);}}
        public bool Contains(FGEPoint2D p){return (p-Center).LengthSquared<=Radius*Radius;}
    }

    public struct FGEEllipse2D
    {
        public FGEEllipse2D(FGEPoint2D center,double radiusX,double radiusY)
        {Center=center;RadiusX=FGEValidation.Finite(radiusX,"radiusX");RadiusY=FGEValidation.Finite(radiusY,"radiusY");if(radiusX<0||radiusY<0)throw new ArgumentOutOfRangeException("radius");}
        public FGEPoint2D Center{get;private set;}public double RadiusX{get;private set;}public double RadiusY{get;private set;}
        public FGERect2D Bounds{get{return new FGERect2D(Center.X-RadiusX,Center.Y-RadiusY,2*RadiusX,2*RadiusY);}}
        public bool Contains(FGEPoint2D p)
        {if(RadiusX==0||RadiusY==0)return Math.Abs(p.X-Center.X)<=RadiusX&&Math.Abs(p.Y-Center.Y)<=RadiusY;double x=(p.X-Center.X)/RadiusX,y=(p.Y-Center.Y)/RadiusY;return x*x+y*y<=1;}
    }

    public sealed class FGEPolyline2D
    {
        private readonly ReadOnlyCollection<FGEPoint2D> _points;
        public FGEPolyline2D(IEnumerable<FGEPoint2D> points)
        {if(points==null)throw new ArgumentNullException("points");var list=new List<FGEPoint2D>(points);if(list.Count<2||list.Count>1000000)throw new ArgumentException("Polyline requires 2..1000000 points.");_points=list.AsReadOnly();}
        public IReadOnlyList<FGEPoint2D> Points{get{return _points;}}
        public double Length{get{double v=0;for(int i=1;i<_points.Count;i++)v+=_points[i-1].DistanceTo(_points[i]);return v;}}
    }

    public sealed class FGEPolygon2D
    {
        private readonly ReadOnlyCollection<FGEPoint2D> _points;
        public FGEPolygon2D(IEnumerable<FGEPoint2D> points)
        {if(points==null)throw new ArgumentNullException("points");var list=new List<FGEPoint2D>(points);if(list.Count<3||list.Count>1000000)throw new ArgumentException("Polygon requires 3..1000000 points.");_points=list.AsReadOnly();}
        public IReadOnlyList<FGEPoint2D> Points{get{return _points;}}
        public double SignedArea{get{double a=0;for(int i=0;i<_points.Count;i++){FGEPoint2D p=_points[i],q=_points[(i+1)%_points.Count];a+=p.X*q.Y-q.X*p.Y;}return a/2;}}
        public bool Contains(FGEPoint2D p){return FGEGeometry2D.PointInPolygon(p,_points);}
    }

    public struct FGEArc2D
    {
        /// <summary>椭圆弧：角度为弧度，旋转为局部椭圆相对坐标系旋转。</summary>
        public FGEArc2D(FGEPoint2D center,double radiusX,double radiusY,double startRadians,double sweepRadians,double rotationRadians=0)
        {Center=center;RadiusX=FGEValidation.Finite(radiusX,"radiusX");RadiusY=FGEValidation.Finite(radiusY,"radiusY");
         StartRadians=FGEValidation.Finite(startRadians,"startRadians");SweepRadians=FGEValidation.Finite(sweepRadians,"sweepRadians");RotationRadians=FGEValidation.Finite(rotationRadians,"rotationRadians");
         if(radiusX<=0||radiusY<=0||Math.Abs(sweepRadians)>Math.PI*2)throw new ArgumentOutOfRangeException("arc");}
        public FGEPoint2D Center{get;private set;}public double RadiusX{get;private set;}public double RadiusY{get;private set;}
        public double StartRadians{get;private set;}public double SweepRadians{get;private set;}public double RotationRadians{get;private set;}
        public FGEPoint2D PointAt(double t)
        {double a=StartRadians+SweepRadians*t,x=RadiusX*Math.Cos(a),y=RadiusY*Math.Sin(a),c=Math.Cos(RotationRadians),s=Math.Sin(RotationRadians);return new FGEPoint2D(Center.X+x*c-y*s,Center.Y+x*s+y*c);}
    }

    public struct FGEBezier2D
    {
        public FGEBezier2D(FGEPoint2D start,FGEPoint2D control1,FGEPoint2D control2,FGEPoint2D end)
        {Start=start;Control1=control1;Control2=control2;End=end;}
        public FGEPoint2D Start{get;private set;}public FGEPoint2D Control1{get;private set;}public FGEPoint2D Control2{get;private set;}public FGEPoint2D End{get;private set;}
        public FGEPoint2D PointAt(double t)
        {FGEValidation.Finite(t,"t");double u=1-t;return new FGEPoint2D(u*u*u*Start.X+3*u*u*t*Control1.X+3*u*t*t*Control2.X+t*t*t*End.X,u*u*u*Start.Y+3*u*u*t*Control1.Y+3*u*t*t*Control2.Y+t*t*t*End.Y);}
    }
}
