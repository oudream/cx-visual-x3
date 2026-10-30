using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;

namespace CxVisual.Foundation.Geometry
{
    /// <summary>来自真实原值点集的双精度几何；不拥有单位Frame、GPU或MEM生命周期。</summary>
    public sealed class FGEPointCloud3D
    {
        private readonly ReadOnlyCollection<FGEPoint3D> _points;
        public ReadOnlyCollection<FGEPoint3D> Points {get{return _points;}}
        public int Count {get{return _points.Count;}}
        public FGEPoint3D BoundsMin {get;private set;}
        public FGEPoint3D BoundsMax {get;private set;}

        public FGEPointCloud3D(IReadOnlyList<FGEPoint3D> points)
        {
            if(points==null)throw new ArgumentNullException("points");
            if(points.Count==0||points.Count>20000000)
                throw new ArgumentOutOfRangeException("points","点云必须有1至2000万个原值点。");
            var copy=new FGEPoint3D[points.Count];
            double xmin=double.PositiveInfinity,ymin=double.PositiveInfinity,zmin=double.PositiveInfinity;
            double xmax=double.NegativeInfinity,ymax=double.NegativeInfinity,zmax=double.NegativeInfinity;
            for(int i=0;i<points.Count;i++)
            {
                FGEPoint3D p=points[i];
                if(double.IsNaN(p.X)||double.IsInfinity(p.X)||
                    double.IsNaN(p.Y)||double.IsInfinity(p.Y)||
                    double.IsNaN(p.Z)||double.IsInfinity(p.Z))
                    throw new ArgumentOutOfRangeException("points","点云含无效坐标。");
                copy[i]=p;
                xmin=Math.Min(xmin,p.X);ymin=Math.Min(ymin,p.Y);zmin=Math.Min(zmin,p.Z);
                xmax=Math.Max(xmax,p.X);ymax=Math.Max(ymax,p.Y);zmax=Math.Max(zmax,p.Z);
            }
            _points=Array.AsReadOnly(copy);
            BoundsMin=new FGEPoint3D(xmin,ymin,zmin);
            BoundsMax=new FGEPoint3D(xmax,ymax,zmax);
        }

        /// <summary>固定顺序显示抽样索引，保留可追踪原点号；不能用来代替专业计算。</summary>
        public ReadOnlyCollection<int> DisplaySampleIndices(int maximumPoints)
        {
            if(maximumPoints<1||maximumPoints>1000000)
                throw new ArgumentOutOfRangeException("maximumPoints");
            int stride=(int)(((long)Count+maximumPoints-1)/maximumPoints);
            var indices=new List<int>(Math.Min(Count,maximumPoints));
            for(int index=0;index<Count;index+=stride)indices.Add(index);
            return indices.AsReadOnly();
        }

        /// <summary>找离射线最近的原始物理点；选择半径必须与输入点坐标使用同一单位。</summary>
        public bool TryPickRay(FGEPoint3D origin,FGEVector3D direction,
            double maximumDistance,double radius,out FGEPointCloudRayHit hit,
            CancellationToken cancellation=default(CancellationToken))
        {
            hit=null;
            double length=direction.Length;
            if(!(length>1e-12)||double.IsNaN(length)||double.IsInfinity(length))
                throw new ArgumentOutOfRangeException("direction");
            if(double.IsNaN(maximumDistance)||double.IsInfinity(maximumDistance)||
                maximumDistance<=0)throw new ArgumentOutOfRangeException("maximumDistance");
            if(double.IsNaN(radius)||double.IsInfinity(radius)||radius<=0)
                throw new ArgumentOutOfRangeException("radius");
            double ux=direction.X/length,uy=direction.Y/length,uz=direction.Z/length;
            double radius2=radius*radius;
            double nearest=double.PositiveInfinity,nearestRadius2=double.PositiveInfinity;
            int nearestIndex=-1;
            for(int i=0;i<_points.Count;i++)
            {
                if((i&1023)==0)cancellation.ThrowIfCancellationRequested();
                FGEPoint3D p=_points[i];
                double dx=p.X-origin.X,dy=p.Y-origin.Y,dz=p.Z-origin.Z;
                double t=dx*ux+dy*uy+dz*uz;
                if(!(t>0)||t>maximumDistance)continue;
                double ex=dx-t*ux,ey=dy-t*uy,ez=dz-t*uz;
                double d2=ex*ex+ey*ey+ez*ez;
                if(d2>radius2||double.IsNaN(d2))continue;
                if(t<nearest||(t==nearest&&d2<nearestRadius2))
                {
                    nearest=t;nearestRadius2=d2;nearestIndex=i;
                }
            }
            cancellation.ThrowIfCancellationRequested();
            if(nearestIndex<0)return false;
            hit=new FGEPointCloudRayHit(nearestIndex,_points[nearestIndex],
                nearest,Math.Sqrt(nearestRadius2));
            return true;
        }
    }

    public sealed class FGEPointCloudRayHit
    {
        internal FGEPointCloudRayHit(int index,FGEPoint3D point,double distance,
            double radialDistance)
        {
            PointIndex=index;Point=point;RayDistance=distance;RadialDistance=radialDistance;
        }
        public int PointIndex {get;private set;}
        public FGEPoint3D Point {get;private set;}
        public double RayDistance {get;private set;}
        public double RadialDistance {get;private set;}
    }
}
