using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;
using CxVisual.Foundation.Volume;

namespace CxVisual.Mechanism.Presentation.Wpf.Volume.Rendering
{
    /// <summary>
    /// 唯一的CT表面物理坐标→体空间显示投影桥梁。
    /// 输入mm真值来自FVO Frame；输出float仅供WPF/GL显示，绝不返回专业计量值。
    /// GL使用Y向上对应原体素正Y；WPF Scene在最终绘制时翻转Y（沿用三正交面约定）。
    /// </summary>
    public static class MPWVolumeSurfaceProjection
    {
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
