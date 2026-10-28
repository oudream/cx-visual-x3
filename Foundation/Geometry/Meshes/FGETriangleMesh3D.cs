using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CxVisual.Foundation.Geometry
{
    /// <summary>通用三维double几何点。单位/坐标系由来源Frame声明，不在FGE中假设为mm。</summary>
    public struct FGEPoint3D
    {
        public FGEPoint3D(double x,double y,double z)
        {
            X=FGEValidation.Finite(x,"x");Y=FGEValidation.Finite(y,"y");
            Z=FGEValidation.Finite(z,"z");
        }
        public double X {get;private set;}
        public double Y {get;private set;}
        public double Z {get;private set;}
    }

    public struct FGEVector3D
    {
        public FGEVector3D(double x,double y,double z)
        {
            X=FGEValidation.Finite(x,"x");Y=FGEValidation.Finite(y,"y");
            Z=FGEValidation.Finite(z,"z");
        }
        public double X {get;private set;}
        public double Y {get;private set;}
        public double Z {get;private set;}
        public double Length {get{return Math.Sqrt(X*X+Y*Y+Z*Z);}}
    }

    /// <summary>
    /// FGE共享的只读三角网格；拥有独立顶点/索引副本，并计算面积、体积与拓扑证据。
    /// 不管理CT阈值、CAD实体、GPU资源或MEM工程持久化。
    /// </summary>
    public sealed class FGETriangleMesh3D
    {
        private struct EdgeCount
        {
            internal int Count,DirectionSum;
        }
        private readonly ReadOnlyCollection<FGEPoint3D> _vertices;
        private readonly ReadOnlyCollection<int> _indices;
        private readonly ReadOnlyCollection<FGEVector3D> _normals;
        public ReadOnlyCollection<FGEPoint3D> Vertices {get{return _vertices;}}
        public ReadOnlyCollection<int> TriangleIndices {get{return _indices;}}
        public ReadOnlyCollection<FGEVector3D> VertexNormals {get{return _normals;}}
        public int VertexCount {get{return _vertices.Count;}}
        public int TriangleCount {get{return _indices.Count/3;}}
        public double SurfaceArea {get;private set;}
        /// <summary>仅闭合、朝向一致时有值，否则为null；单位为输入坐标单位的三次方。</summary>
        public double? EnclosedVolume {get;private set;}
        /// <summary>闭合一致朝向网格的带符号体积；正数表示外向绕序，负数提示全局法向翻转。</summary>
        public double? SignedEnclosedVolume {get;private set;}
        public int BoundaryEdgeCount {get;private set;}
        public int NonManifoldEdgeCount {get;private set;}
        public int InconsistentWindingEdgeCount {get;private set;}
        public bool IsClosedOrientedManifold
        {
            get{return BoundaryEdgeCount==0 && NonManifoldEdgeCount==0 &&
                InconsistentWindingEdgeCount==0;}
        }
        public FGEPoint3D BoundsMin {get;private set;}
        public FGEPoint3D BoundsMax {get;private set;}

        public FGETriangleMesh3D(IReadOnlyList<FGEPoint3D> vertices,
            IReadOnlyList<int> triangleIndices)
        {
            if(vertices==null)throw new ArgumentNullException("vertices");
            if(triangleIndices==null)throw new ArgumentNullException("triangleIndices");
            if(vertices.Count<3||triangleIndices.Count<3||triangleIndices.Count%3!=0)
                throw new ArgumentException("网格至少包含一个三角面，索引数量须为3的倍数。");
            var positions=new List<FGEPoint3D>(vertices);
            var indices=new List<int>(triangleIndices);
            _vertices=positions.AsReadOnly();
            _indices=indices.AsReadOnly();
            var normals=new double[checked(positions.Count*3)];
            var edges=new Dictionary<ulong,EdgeCount>();
            double minX=double.PositiveInfinity,minY=double.PositiveInfinity,minZ=double.PositiveInfinity;
            double maxX=double.NegativeInfinity,maxY=double.NegativeInfinity,maxZ=double.NegativeInfinity;
            foreach(FGEPoint3D p in positions)
            {
                minX=Math.Min(minX,p.X);minY=Math.Min(minY,p.Y);minZ=Math.Min(minZ,p.Z);
                maxX=Math.Max(maxX,p.X);maxY=Math.Max(maxY,p.Y);maxZ=Math.Max(maxZ,p.Z);
            }
            BoundsMin=new FGEPoint3D(minX,minY,minZ);
            BoundsMax=new FGEPoint3D(maxX,maxY,maxZ);
            FGEPoint3D anchor=positions[0];
            double volume6=0,area=0;
            for(int t=0;t<indices.Count;t+=3)
            {
                int a=indices[t],b=indices[t+1],c=indices[t+2];
                if(a<0||b<0||c<0||a>=positions.Count||b>=positions.Count||
                    c>=positions.Count||a==b||b==c||c==a)
                    throw new ArgumentException("网格含无效、重复或越界三角面顶点。","triangleIndices");
                FGEPoint3D p=positions[a],q=positions[b],r=positions[c];
                double ux=q.X-p.X,uy=q.Y-p.Y,uz=q.Z-p.Z;
                double vx=r.X-p.X,vy=r.Y-p.Y,vz=r.Z-p.Z;
                double nx=uy*vz-uz*vy,ny=uz*vx-ux*vz,nz=ux*vy-uy*vx;
                double magnitude=Math.Sqrt(nx*nx+ny*ny+nz*nz);
                if(double.IsInfinity(magnitude)||double.IsNaN(magnitude)||magnitude<=0)
                    throw new ArgumentException("网格含零面积或非有限三角面。","triangleIndices");
                area+=magnitude*.5;
                foreach(int i in new[]{a,b,c})
                {
                    normals[i*3]+=nx;normals[i*3+1]+=ny;normals[i*3+2]+=nz;
                }
                AddEdge(edges,a,b);AddEdge(edges,b,c);AddEdge(edges,c,a);
                // 平移不变的有向四面体体积积分，降低较大世界坐标偏置的误差。
                double ax=p.X-anchor.X,ay=p.Y-anchor.Y,az=p.Z-anchor.Z;
                volume6+=ax*((q.Y-anchor.Y)*(r.Z-anchor.Z)-(q.Z-anchor.Z)*(r.Y-anchor.Y))
                    +ay*((q.Z-anchor.Z)*(r.X-anchor.X)-(q.X-anchor.X)*(r.Z-anchor.Z))
                    +az*((q.X-anchor.X)*(r.Y-anchor.Y)-(q.Y-anchor.Y)*(r.X-anchor.X));
            }
            if(double.IsNaN(area)||double.IsInfinity(area)||double.IsNaN(volume6)||double.IsInfinity(volume6))
                throw new ArgumentOutOfRangeException("vertices","网格面积或体积发生数值溢出。");
            SurfaceArea=area;
            foreach(EdgeCount count in edges.Values)
            {
                if(count.Count==1)BoundaryEdgeCount++;
                else if(count.Count>2)NonManifoldEdgeCount++;
                else if(count.DirectionSum!=0)InconsistentWindingEdgeCount++;
            }
            if(IsClosedOrientedManifold)
            {
                SignedEnclosedVolume=volume6/6.0;
                EnclosedVolume=Math.Abs(volume6)/6.0;
            }
            var output=new List<FGEVector3D>(positions.Count);
            for(int i=0;i<positions.Count;i++)
            {
                double nx=normals[i*3],ny=normals[i*3+1],nz=normals[i*3+2];
                double magnitude=Math.Sqrt(nx*nx+ny*ny+nz*nz);
                output.Add(magnitude>0?new FGEVector3D(nx/magnitude,ny/magnitude,nz/magnitude):
                    new FGEVector3D(0,0,0));
            }
            _normals=output.AsReadOnly();
        }

        private static void AddEdge(Dictionary<ulong,EdgeCount> edges,int a,int b)
        {
            int min=Math.Min(a,b),max=Math.Max(a,b);
            ulong key=((ulong)(uint)min<<32)|(uint)max;
            EdgeCount use;
            edges.TryGetValue(key,out use);
            use.Count++;
            use.DirectionSum+=a<b?1:-1;
            edges[key]=use;
        }
    }
}
