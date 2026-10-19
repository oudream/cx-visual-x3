using System;

namespace CxVisual.Foundation.Geometry
{
    /// <summary>统一列向量仿射定义：x'=M11*x+M12*y+DX; y'=M21*x+M22*y+DY。
    /// Compose(child) 表示先 child 后 this。兼容旧 MRCTransform 需按数学含义转换。
    /// </summary>
    public struct FGETransform2D
    {
        public FGETransform2D(double m11,double m12,double m21,double m22,double dx,double dy)
        {
            M11=FGEValidation.Finite(m11,"m11");M12=FGEValidation.Finite(m12,"m12");
            M21=FGEValidation.Finite(m21,"m21");M22=FGEValidation.Finite(m22,"m22");
            DX=FGEValidation.Finite(dx,"dx");DY=FGEValidation.Finite(dy,"dy");
        }
        public double M11{get;private set;}public double M12{get;private set;}
        public double M21{get;private set;}public double M22{get;private set;}
        public double DX{get;private set;}public double DY{get;private set;}
        public static FGETransform2D Identity{get{return new FGETransform2D(1,0,0,1,0,0);}}
        public static FGETransform2D Translation(double x,double y){return new FGETransform2D(1,0,0,1,x,y);}
        public static FGETransform2D Scale(double x,double y){return new FGETransform2D(x,0,0,y,0,0);}
        public static FGETransform2D Rotation(double radians){FGEValidation.Finite(radians,"radians");double c=Math.Cos(radians),s=Math.Sin(radians);return new FGETransform2D(c,-s,s,c,0,0);}
        public double Determinant{get{return M11*M22-M12*M21;}}
        public FGEPoint2D TransformPoint(FGEPoint2D p){return new FGEPoint2D(M11*p.X+M12*p.Y+DX,M21*p.X+M22*p.Y+DY);}
        public FGEVector2D TransformVector(FGEVector2D p){return new FGEVector2D(M11*p.X+M12*p.Y,M21*p.X+M22*p.Y);}
        public FGETransform2D Compose(FGETransform2D child)
        {
            return new FGETransform2D(M11*child.M11+M12*child.M21,M11*child.M12+M12*child.M22,
                M21*child.M11+M22*child.M21,M21*child.M12+M22*child.M22,
                M11*child.DX+M12*child.DY+DX,M21*child.DX+M22*child.DY+DY);
        }
        public bool TryInverse(out FGETransform2D inverse)
        {
            double d=Determinant;
            if(d==0||double.IsInfinity(d)||double.IsNaN(d)){inverse=default(FGETransform2D);return false;}
            double a=M22/d,b=-M12/d,c=-M21/d,e=M11/d;
            double x=-(a*DX+b*DY),y=-(c*DX+e*DY);
            if(double.IsNaN(a)||double.IsInfinity(a)||double.IsNaN(b)||double.IsInfinity(b)||
               double.IsNaN(c)||double.IsInfinity(c)||double.IsNaN(e)||double.IsInfinity(e)||
               double.IsNaN(x)||double.IsInfinity(x)||double.IsNaN(y)||double.IsInfinity(y)){inverse=default(FGETransform2D);return false;}
            inverse=new FGETransform2D(a,b,c,e,x,y);return true;
        }
        public FGETransform2D Inverse()
        {FGETransform2D result;if(!TryInverse(out result))throw new InvalidOperationException("geometry.singular_transform");return result;}
    }
}
