using System;
using System.Collections.Generic;
using System.Threading;
using CxVisual.Foundation.Geometry;
using CxVisual.Foundation.Volume;

namespace CxVisual.Professional.CT3D
{
    /// <summary>工业RAW Float32不能将NaN/Infinity当作实心体素或空域。</summary>
    internal static class CT3DRawScalarGuard
    {
        internal static float ReadFinite(FVOOriginalScalarLease source,int x,int y,int z)
        {
            float scalar=source.ReadScalar(x,y,z);
            if(float.IsNaN(scalar)||float.IsInfinity(scalar))
                throw new InvalidOperationException(
                    "原始体素含NaN或Infinity，拒绝以非有限标量生成专业表面或Mesh。");
            return scalar;
        }
    }

    /// <summary>专业提取结果：独立双精度FGE网格 + 原值版本/空间来源，不保存重复RAW或GPU对象。</summary>
    public sealed class CT3DVoxelBoundaryMeshResult
    {
        internal CT3DVoxelBoundaryMeshResult(FGETriangleMesh3D mesh,float threshold,
            FVOVolumeSpatialFrame frame,string sourceRef,long sourceRevision,bool touchesBoundary)
        {
            ResultId=Guid.NewGuid();
            Mesh=mesh;Threshold=threshold;SpatialFrame=frame;
            SourceRef=sourceRef;SourceRevision=sourceRevision;
            TouchesVolumeBoundary=touchesBoundary;
        }
        public Guid ResultId {get;private set;}
        public FGETriangleMesh3D Mesh {get;private set;}
        public float Threshold {get;private set;}
        public string SourceRef {get;private set;}
        public long SourceRevision {get;private set;}
        public FVOVolumeSpatialFrame SpatialFrame {get;private set;}
        public bool TouchesVolumeBoundary {get;private set;}
        public string Method {get{return "binary-voxel-face-6-neighbor";}}
        /// <summary>仅表示源标定且封闭不截断；仍需不确定度/标准件验证方可用于工业量值。</summary>
        public bool HasCalibratedClosedGeometry
        {
            get{return SpatialFrame.IsCalibrated && !TouchesVolumeBoundary &&
                Mesh.IsClosedOrientedManifold && Mesh.ConnectedFaceComponentCount==1 &&
                Mesh.SignedEnclosedVolume.HasValue && Mesh.SignedEnclosedVolume.Value>0;}
        }
    }

    /// <summary>
    /// 首个可检验的CT表面三角网格：对原始标量阈值构造外露六邻域体素面，
    /// 相邻面共享网格顶点、法向和拓扑质量交FGE计算。
    /// 不是Marching Cubes/亚体素等值面，也不是工业尺寸测定。
    /// </summary>
    public static class CT3DVoxelBoundaryMeshExtractor
    {
        // 八个体素顶点局部坐标（整数角点 - 0.5 才是源体素中心坐标）。
        private static readonly int[,] Corners={
            {0,0,0},{1,0,0},{1,1,0},{0,1,0},
            {0,0,1},{1,0,1},{1,1,1},{0,1,1}
        };
        // 每面四角严格按右手体素索引朝外逆时针顺序。
        private static readonly int[,] Faces={
            {0,4,7,3}, // -X
            {1,2,6,5}, // +X
            {0,1,5,4}, // -Y
            {3,7,6,2}, // +Y
            {0,3,2,1}, // -Z
            {4,5,6,7}  // +Z
        };
        private static readonly int[] Dx={-1,1,0,0,0,0};
        private static readonly int[] Dy={0,0,-1,1,0,0};
        private static readonly int[] Dz={0,0,0,0,-1,1};

        public static CT3DVoxelBoundaryMeshResult Extract(
            FVOOriginalScalarLease source,string sourceRef,long sourceRevision,
            float threshold,int maxTriangles,CancellationToken cancellation)
        {
            if(source==null)throw new ArgumentNullException("source");
            if(string.IsNullOrWhiteSpace(sourceRef))
                throw new ArgumentException("需要现有工程或会话的源引用。","sourceRef");
            if(sourceRevision<0)throw new ArgumentOutOfRangeException("sourceRevision");
            if(float.IsNaN(threshold)||float.IsInfinity(threshold))
                throw new ArgumentOutOfRangeException("threshold");
            if(maxTriangles<12||maxTriangles>2000000)
                throw new ArgumentOutOfRangeException("maxTriangles");
            // 体素索引网格的角点总数作为确定性的字典键；乘积使用long防溢出。
            long strideY=checked((long)source.Width+1);
            long strideZ=checked(strideY*(source.Height+1L));
            checked{long maxKey=strideZ*(source.Depth+1L);if(maxKey<1)throw new OverflowException();}
            var vertices=new List<FGEPoint3D>();
            var triangles=new List<int>();
            var shared=new Dictionary<long,int>();
            bool touchesBoundary=false;
            FVOVolumeSpatialFrame frame=source.SpatialFrame;
            for(int z=0;z<source.Depth;z++)
            {
                cancellation.ThrowIfCancellationRequested();
                for(int y=0;y<source.Height;y++)
                {
                    if((y&15)==0)cancellation.ThrowIfCancellationRequested();
                    for(int x=0;x<source.Width;x++)
                    {
                        if(CT3DRawScalarGuard.ReadFinite(source,x,y,z)<threshold)continue;
                        for(int face=0;face<6;face++)
                        {
                            int nx=x+Dx[face],ny=y+Dy[face],nz=z+Dz[face];
                            bool outside=nx<0||ny<0||nz<0||
                                nx>=source.Width||ny>=source.Height||nz>=source.Depth;
                            if(!outside && CT3DRawScalarGuard.ReadFinite(source,nx,ny,nz)>=threshold)continue;
                            if(triangles.Count/3>maxTriangles-2)
                                throw new InvalidOperationException(
                                    "三角网格超出最大面数预算，禁止生成部分网格或默默简化。");
                            if(outside)touchesBoundary=true;
                            int[] quad=new int[4];
                            for(int q=0;q<4;q++)
                            {
                                int corner=Faces[face,q];
                                int gx=x+Corners[corner,0],gy=y+Corners[corner,1],
                                    gz=z+Corners[corner,2];
                                long key=checked(gx+gy*strideY+gz*strideZ);
                                int index;
                                if(!shared.TryGetValue(key,out index))
                                {
                                    FVOPhysicalPoint3D physical=frame.VoxelToPhysical(
                                        gx-.5,gy-.5,gz-.5);
                                    index=vertices.Count;
                                    vertices.Add(new FGEPoint3D(
                                        physical.X,physical.Y,physical.Z));
                                    shared.Add(key,index);
                                }
                                quad[q]=index;
                            }
                            // 方向矩阵允许镜像(det -1)，翻转索引使物理法向仍朝实体外。
                            if(frame.IsRightHanded)
                            {
                                AddTriangle(triangles,quad[0],quad[1],quad[2]);
                                AddTriangle(triangles,quad[0],quad[2],quad[3]);
                            }
                            else
                            {
                                AddTriangle(triangles,quad[0],quad[2],quad[1]);
                                AddTriangle(triangles,quad[0],quad[3],quad[2]);
                            }
                        }
                    }
                }
            }
            if(triangles.Count<12)
                throw new InvalidOperationException("阈值表面不存在或不足以构成封闭三维网格。");
            FGETriangleMesh3D mesh=new FGETriangleMesh3D(vertices,triangles);
            return new CT3DVoxelBoundaryMeshResult(mesh,threshold,frame,sourceRef,
                sourceRevision,touchesBoundary);
        }

        private static void AddTriangle(List<int> indices,int a,int b,int c)
        {
            indices.Add(a);indices.Add(b);indices.Add(c);
        }
    }
}
