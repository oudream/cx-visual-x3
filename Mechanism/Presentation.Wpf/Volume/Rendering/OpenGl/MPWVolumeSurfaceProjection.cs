using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;
using CxVisual.Foundation.Volume;
using CxVisual.Foundation.Geometry;

namespace CxVisual.Mechanism.Presentation.Wpf.Volume.Rendering
{
    /// <summary>索引三角面显示快照，独立于FGE毫米几何真值和GL显存资源。</summary>
    public sealed class MPWVolumeDisplayMesh
    {
        internal MPWVolumeDisplayMesh(List<Vector3> points,List<int> indices)
        {
            Vertices=points.AsReadOnly();
            TriangleIndices=indices.AsReadOnly();
        }
        public ReadOnlyCollection<Vector3> Vertices {get;private set;}
        public ReadOnlyCollection<int> TriangleIndices {get;private set;}
        public int TriangleCount {get{return TriangleIndices.Count/3;}}
    }

    /// <summary>仅显示的点云快照；原值索引与浮点显示位置一一对应。</summary>
    public sealed class MPWVolumeDisplayPointCloud
    {
        internal MPWVolumeDisplayPointCloud(List<Vector3> points,List<int> originalIndices,int sourceCount)
        {
            Points=points.AsReadOnly();
            OriginalIndices=originalIndices.AsReadOnly();
            SourceCount=sourceCount;
        }
        public ReadOnlyCollection<Vector3> Points {get;private set;}
        public ReadOnlyCollection<int> OriginalIndices {get;private set;}
        public int SourceCount {get;private set;}
        public int Count {get{return Points.Count;}}
    }

    /// <summary>
    /// 唯一的CT表面物理坐标→体空间显示投影桥梁。
    /// 输入mm真值来自FVO Frame；输出float仅供WPF/GL显示，绝不返回专业计量值。
    /// GL使用Y向上对应原体素正Y；WPF Scene在最终绘制时翻转Y（沿用三正交面约定）。
    /// </summary>
    public static class MPWVolumeSurfaceProjection
    {
        /// <summary>
        /// 将FGE双精度物理Mesh投影为有界索引三角面。
        /// 体素面角点使用(i+0.5)/N，不误用体素中心的i/(N-1)；
        /// 该约定与GL原始Volume物理外框及WPF三维Scene显示比例相同。
        /// </summary>
        public static MPWVolumeDisplayMesh ProjectMesh(FGETriangleMesh3D mesh,
            FVOVolumeSpatialFrame frame,int width,int height,int depth,int maxTriangles=50000)
        {
            if(mesh==null)throw new ArgumentNullException("mesh");
            if(frame==null)throw new ArgumentNullException("frame");
            if(width<2||height<2||depth<2)throw new ArgumentOutOfRangeException("width");
            if(maxTriangles<1||maxTriangles>100000)
                throw new ArgumentOutOfRangeException("maxTriangles");
            if(mesh.TriangleCount>maxTriangles)
                throw new InvalidOperationException("GPU预览三角面超出预算，须使用明确的LOD简化流程。");
            var vertices=new List<Vector3>(mesh.VertexCount);
            foreach(FGEPoint3D p in mesh.Vertices)
            {
                FVOVoxelCoordinate3D v=frame.PhysicalToVoxel(p.X,p.Y,p.Z);
                double x=(v.X+.5)/width-.5,y=(v.Y+.5)/height-.5,z=(v.Z+.5)/depth-.5;
                if(x<-.50001||x>.50001||y<-.50001||y>.50001||z<-.50001||z>.50001||
                   double.IsNaN(x)||double.IsNaN(y)||double.IsNaN(z))
                    throw new ArgumentOutOfRangeException("mesh",
                        "网格存在体外物理点，检查原始Volume尺寸、Frame和SourceRevision。");
                vertices.Add(new Vector3((float)x,(float)y,(float)z));
            }
            return new MPWVolumeDisplayMesh(vertices,new List<int>(mesh.TriangleIndices));
        }

        /// <summary>仅预览级三角抽样LOD：按稳定面片步长选取，原始FGE Mesh不改变。
        /// 可能出现开放边界，不得用于拓扑、表面积、体积或工业计量。</summary>
        public static MPWVolumeDisplayMesh ProjectMeshPreviewLod(
            FGETriangleMesh3D mesh,FVOVolumeSpatialFrame frame,
            int width,int height,int depth,int triangleBudget=50000)
        {
            if(mesh==null)throw new ArgumentNullException("mesh");
            if(triangleBudget<1||triangleBudget>50000)
                throw new ArgumentOutOfRangeException("triangleBudget");
            if(mesh.TriangleCount<=triangleBudget)
                return ProjectMesh(mesh,frame,width,height,depth,triangleBudget);
            int stride=(mesh.TriangleCount+triangleBudget-1)/triangleBudget;
            var indices=new List<int>(triangleBudget*3);
            var vertices=new List<FGEPoint3D>();
            var remap=new Dictionary<int,int>();
            for(int face=0;face<mesh.TriangleCount;face+=stride)
            {
                for(int corner=0;corner<3;corner++)
                {
                    int oldIndex=mesh.TriangleIndices[face*3+corner];
                    int newIndex;
                    if(!remap.TryGetValue(oldIndex,out newIndex))
                    {
                        newIndex=vertices.Count;
                        remap.Add(oldIndex,newIndex);
                        vertices.Add(mesh.Vertices[oldIndex]);
                    }
                    indices.Add(newIndex);
                }
            }
            return ProjectMesh(new FGETriangleMesh3D(vertices,indices),
                frame,width,height,depth,triangleBudget);
        }

        /// <summary>FGE原值PointCloud→有界MVI/MPW显示，保留原始点索引以便回到double物理量。</summary>
        public static MPWVolumeDisplayPointCloud ProjectPointCloud(
            FGEPointCloud3D cloud,FVOVolumeSpatialFrame frame,
            int width,int height,int depth,int maximumPoints=12000)
        {
            if(cloud==null)throw new ArgumentNullException("cloud");
            if(frame==null)throw new ArgumentNullException("frame");
            if(width<2||height<2||depth<2)throw new ArgumentOutOfRangeException("width");
            if(maximumPoints<1||maximumPoints>12000)
                throw new ArgumentOutOfRangeException("maximumPoints");
            ReadOnlyCollection<int> indices=cloud.DisplaySampleIndices(maximumPoints);
            var points=new List<Vector3>(indices.Count);
            var selected=new List<int>(indices.Count);
            foreach(int index in indices)
            {
                FGEPoint3D p=cloud.Points[index];
                FVOVoxelCoordinate3D v=frame.PhysicalToVoxel(p.X,p.Y,p.Z);
                double x=(v.X+.5)/width-.5;
                double y=(v.Y+.5)/height-.5;
                double z=(v.Z+.5)/depth-.5;
                if(double.IsNaN(x)||double.IsInfinity(x)||
                    double.IsNaN(y)||double.IsInfinity(y)||
                    double.IsNaN(z)||double.IsInfinity(z)||
                    v.X<-.00001||v.X>width-1+.00001||
                    v.Y<-.00001||v.Y>height-1+.00001||
                    v.Z<-.00001||v.Z>depth-1+.00001)
                    throw new ArgumentOutOfRangeException("cloud",
                        "PointCloud物理点超出当前Volume；检查资产Frame、尺寸和来源版本。");
                points.Add(new Vector3((float)x,(float)y,(float)z));
                selected.Add(index);
            }
            return new MPWVolumeDisplayPointCloud(points,selected,cloud.Count);
        }

        public static ReadOnlyCollection<Vector3> Project(
            IReadOnlyList<FVOPhysicalPoint3D> physicalPoints,FVOVolumeSpatialFrame frame,
            int width,int height,int depth,int maxPreviewPoints=12000)
        {
            if(physicalPoints==null)throw new ArgumentNullException("physicalPoints");
            if(frame==null)throw new ArgumentNullException("frame");
            if(width<2 || height<2 || depth<2)
                throw new ArgumentOutOfRangeException("width","三维预览须每轴至少两个原始体素。");
            if(maxPreviewPoints<1 || maxPreviewPoints>100000)
                throw new ArgumentOutOfRangeException("maxPreviewPoints");
            var vertices=new List<Vector3>(Math.Min(physicalPoints.Count,maxPreviewPoints));
            int count=physicalPoints.Count;
            // 有界、确定性的等间距抽样；只影响GPU预览，不改变原始Surface结果。
            int step=count<=maxPreviewPoints?1:checked((count+maxPreviewPoints-1)/maxPreviewPoints);
            for(int i=0;i<count;i+=step)
            {
                FVOPhysicalPoint3D p=physicalPoints[i];
                FVOVoxelCoordinate3D v=frame.PhysicalToVoxel(p.X,p.Y,p.Z);
                // 与ProjectMesh、体素面外框和GL体绘使用同一体素中心约定。
                double x=(v.X+.5)/width-.5,y=(v.Y+.5)/height-.5,z=(v.Z+.5)/depth-.5;
                if(double.IsNaN(x)||double.IsInfinity(x)||
                   double.IsNaN(y)||double.IsInfinity(y)||
                   double.IsNaN(z)||double.IsInfinity(z)||
                   v.X<-.00001||v.X>width-1+.00001||
                   v.Y<-.00001||v.Y>height-1+.00001||
                   v.Z<-.00001||v.Z>depth-1+.00001)
                    throw new ArgumentOutOfRangeException("physicalPoints",
                        "Surface点超出当前原始Volume范围；检查来源Frame/尺寸/版本。");
                vertices.Add(new Vector3((float)x,(float)y,(float)z));
            }
            return vertices.AsReadOnly();
        }
    }
}
