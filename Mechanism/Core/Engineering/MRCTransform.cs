using System;

namespace CxVisual.Mechanism.Core
{
    /// <summary>Visual local affine value: x'=M11*x+M12*y+X, y'=M21*x+M22*y+Y. No geometry service or node storage.</summary>
    public struct MRCTransform
    {
        public MRCTransform(double m11,double m12,double m21,double m22,double x,double y)
        {MRCVisualItem.Finite(m11);MRCVisualItem.Finite(m12);MRCVisualItem.Finite(m21);MRCVisualItem.Finite(m22);MRCVisualItem.Finite(x);MRCVisualItem.Finite(y);M11=m11;M12=m12;M21=m21;M22=m22;X=x;Y=y;}
        public double M11 {get;private set;} public double M12 {get;private set;}
        public double M21 {get;private set;} public double M22 {get;private set;}
        public double X {get;private set;} public double Y {get;private set;}
        public static MRCTransform Identity {get{return new MRCTransform(1,0,0,1,0,0);}}
        public void TransformPoint(double x,double y,out double tx,out double ty) {tx=M11*x+M12*y+X;ty=M21*x+M22*y+Y;}
        public MRCTransform Compose(MRCTransform child)
        {return new MRCTransform(M11*child.M11+M12*child.M21,M11*child.M12+M12*child.M22,M21*child.M11+M22*child.M21,M21*child.M12+M22*child.M22,M11*child.X+M12*child.Y+X,M21*child.X+M22*child.Y+Y);}
        internal void Decompose(out double angle,out double sx,out double sy,out double shear)
        {
            double max=Math.Max(Math.Abs(M11),Math.Abs(M21));
            sx=max==0?0:max*Math.Sqrt((M11/max)*(M11/max)+(M21/max)*(M21/max));double determinant=M11*M22-M12*M21;
            if(sx==0||determinant==0||double.IsInfinity(determinant)||double.IsNaN(determinant))throw new InvalidOperationException("org.visual.transform.singular_or_out_of_range");
            angle=Math.Atan2(M21,M11)*180/Math.PI;sy=determinant/sx;shear=(M11/sx)*M12+(M21/sx)*M22;
            MRCVisualItem.Finite(sx);MRCVisualItem.Finite(sy);MRCVisualItem.Finite(shear);if(sy==0)throw new InvalidOperationException("org.visual.transform.singular");
        }
    }
    internal sealed class MRIPlacementState
    {
        internal readonly MRCTransform Value;
        internal MRIPlacementState(MRCTransform value){Value=value;}
    }
}
