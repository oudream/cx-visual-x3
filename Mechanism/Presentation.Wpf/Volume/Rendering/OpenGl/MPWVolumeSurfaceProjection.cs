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
                double x=v.X/(width-1),y=v.Y/(height-1),z=v.Z/(depth-1);
                if(double.IsNaN(x)||double.IsInfinity(x)||
                   double.IsNaN(y)||double.IsInfinity(y)||
                   double.IsNaN(z)||double.IsInfinity(z)||
                   x<0||x>1||y<0||y>1||z<0||z>1)
                    throw new ArgumentOutOfRangeException("physicalPoints",
                        "Surface点超出当前原始Volume范围；检查来源Frame/尺寸/版本。");
                vertices.Add(new Vector3((float)(x-.5),(float)(y-.5),(float)(z-.5)));
            }
            return vertices.AsReadOnly();
        }
    }
}
