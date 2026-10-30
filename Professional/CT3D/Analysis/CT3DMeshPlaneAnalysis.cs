using System;
using System.Collections.Generic;
using System.Threading;
using CxVisual.Foundation.Geometry;
using CxVisual.Foundation.Numerics;
using CxVisual.Foundation.Volume;

namespace CxVisual.Professional.CT3D
{
    /// <summary>由专业Mesh局部ROI的顶点拟合出的平面特征与残差证据，单位mm。</summary>
    public sealed class CT3DPlaneFitResult
    {
        internal CT3DPlaneFitResult(CT3DVoxelBoundaryMeshResult source,
            FNUPcaPlaneResult fit,Guid? resultId=null)
        {
            ResultId=resultId??Guid.NewGuid();
            SourceMeshResultId=source.ResultId;
            SourceRef=source.SourceRef;
            SourceRevision=source.SourceRevision;
            SpatialFrame=source.SpatialFrame;
            double[] center=fit.Centroid,normal=fit.Normal;
            PointMm=new FVOPhysicalPoint3D(center[0],center[1],center[2]);
            Normal=new FGEVector3D(normal[0],normal[1],normal[2]);
            RmsResidualMm=fit.RmsPerpendicularResidual;
            MaxAbsoluteResidualMm=fit.MaxAbsoluteResidual;
            PlanarityRatio=fit.PlanarityRatio;
            InPlaneConditionNumber=fit.InPlaneConditionNumber;
            SampleCount=fit.SampleCount;
        }
        public Guid ResultId {get;private set;}
        public Guid SourceMeshResultId {get;private set;}
        public string SourceRef {get;private set;}
        public long SourceRevision {get;private set;}
        /// <summary>本结果的源空间Frame，点到面偏差必须在相同Frame或显式配准后计算。</summary>
        public FVOVolumeSpatialFrame SpatialFrame {get;private set;}
        public FVOPhysicalPoint3D PointMm {get;private set;}
        public FGEVector3D Normal {get;private set;}
        public double RmsResidualMm {get;private set;}
        public double MaxAbsoluteResidualMm {get;private set;}
        public double PlanarityRatio {get;private set;}
        public double InPlaneConditionNumber {get;private set;}
        public int SampleCount {get;private set;}
        public string FitMethod {get{return "roi-mesh-vertex-orthogonal-PCA-SVD";}}

        /// <summary>仅同一物理Frame的点可直接比较；正负号按拟合法向，非CAD内外侧语义。</summary>
        public double SignedDistanceMm(FVOPhysicalPoint3D pointMm,FVOVolumeSpatialFrame pointFrame)
        {
            if(!Object.ReferenceEquals(SpatialFrame,pointFrame))
                throw new InvalidOperationException(
                    "空间Frame不一致：请先由专业配准链显式转换坐标，禁止直接报毫米偏差。");
            double result=(pointMm.X-PointMm.X)*Normal.X+
                (pointMm.Y-PointMm.Y)*Normal.Y+
                (pointMm.Z-PointMm.Z)*Normal.Z;
            if(double.IsNaN(result)||double.IsInfinity(result))
                throw new OverflowException("点到平面毫米偏差发生数值溢出。");
            return result;
        }
        /// <summary>跨工程重开的同资产物理测距：核对资产Id、Revision及Frame稳定签名。
        /// 不允许与不同资产/标定混用；本API不取代Open对RAW Hash的验证。</summary>
        public double SignedDistanceFromAssetMm(FVOPhysicalPoint3D pointMm,CT3DAssetReference source)
        {
            if(source==null)throw new ArgumentNullException("source");
            if(source.SourceRef!=SourceRef||source.Revision!=SourceRevision||
                !source.MatchesSpatialFrame(SpatialFrame))
                throw new InvalidOperationException("来源、Revision或物理Frame不一致，拒绝跨资产测距。");
            double result=(pointMm.X-PointMm.X)*Normal.X+
                (pointMm.Y-PointMm.Y)*Normal.Y+
                (pointMm.Z-PointMm.Z)*Normal.Z;
            if(double.IsNaN(result)||double.IsInfinity(result))
                throw new OverflowException("点到平面毫米偏差发生数值溢出。");
            return result;
        }
        public double AbsoluteDistanceFromAssetMm(FVOPhysicalPoint3D pointMm,CT3DAssetReference source)
        {
            return Math.Abs(SignedDistanceFromAssetMm(pointMm,source));
        }

        public double AbsoluteDistanceMm(FVOPhysicalPoint3D pointMm,
            FVOVolumeSpatialFrame pointFrame)
        {
            return Math.Abs(SignedDistanceMm(pointMm,pointFrame));
        }
    }

    /// <summary>
    /// CT3D专业面片ROI几何拟合。复用FGE实体点和FNU SVD，不自行实现第二套求解器。
    /// 调用者必须提供选中局部面的唯一顶点索引；对整个闭合物体的所有点拟合平面不合理。
    /// </summary>
    public static class CT3DMeshPlaneAnalysis
    {
        /// <summary>
        /// 二维切片矩形沿其索引增加方向拉伸为毫米区间，选择原值Mesh顶点。
        /// 四角是采样体素中心，横向范围扩展半个原始体素以包含这些体素的边界面。
        /// 法向偏移以选区所在切片为零点；不会重新采样或使用显示预览顶点。
        /// </summary>
        public static IReadOnlyList<int> SelectSlicePrismVertices(CT3DVoxelBoundaryMeshResult source,
            FVOPhysicalSliceRectangle rectangle,double minimumOffsetMm,double maximumOffsetMm,
            CancellationToken cancellation=default(CancellationToken))
        {
            if(source==null)throw new ArgumentNullException("source");
            if(rectangle==null)throw new ArgumentNullException("rectangle");
            if(!Object.ReferenceEquals(source.SpatialFrame,rectangle.SpatialFrame))
                throw new InvalidOperationException("ROI与Mesh不属于同一来源Frame。");
            if(rectangle.Left==rectangle.Right||rectangle.Top==rectangle.Bottom)
                throw new ArgumentException("请选择具有面积的矩形ROI。","rectangle");
            return SelectPhysicalSlicePrismVertices(source,rectangle.Plane,rectangle.TopLeft,
                rectangle.BottomRight,minimumOffsetMm,maximumOffsetMm,cancellation);
        }

        /// <summary>经资产验证的原Voxel角点所对应物理区间；无需重新创建旧预览图片。</summary>
        internal static IReadOnlyList<int> SelectPhysicalSlicePrismVertices(
            CT3DVoxelBoundaryMeshResult source,FVOSlicePlane plane,
            FVOPhysicalPoint3D topLeft,FVOPhysicalPoint3D bottomRight,
            double minimumOffsetMm,double maximumOffsetMm,CancellationToken cancellation)
        {
            if(source==null)throw new ArgumentNullException("source");
            if(!source.HasCalibratedClosedGeometry)
                throw new InvalidOperationException("ROI拟合要求已标定、非截断的单连通闭合Mesh。");
            if(double.IsNaN(minimumOffsetMm)||double.IsInfinity(minimumOffsetMm)||
                double.IsNaN(maximumOffsetMm)||double.IsInfinity(maximumOffsetMm)||
                minimumOffsetMm>maximumOffsetMm)
                throw new ArgumentOutOfRangeException("minimumOffsetMm","毫米范围必须有限且有序。");
            cancellation.ThrowIfCancellationRequested();
            var frame=source.SpatialFrame;
            var a=frame.PhysicalToVoxel(topLeft.X,topLeft.Y,topLeft.Z);
            var b=frame.PhysicalToVoxel(bottomRight.X,bottomRight.Y,bottomRight.Z);
            double au,av,aw,bu,bv,bw;
            SliceCoordinates(plane,a,out au,out av,out aw);
            SliceCoordinates(plane,b,out bu,out bv,out bw);
            double spacing=plane==FVOSlicePlane.XY?frame.SpacingZ:
                plane==FVOSlicePlane.XZ?frame.SpacingY:frame.SpacingX;
            double minimumNormal=aw+minimumOffsetMm/spacing,maximumNormal=aw+maximumOffsetMm/spacing;
            if(double.IsNaN(minimumNormal)||double.IsInfinity(minimumNormal)||
                double.IsNaN(maximumNormal)||double.IsInfinity(maximumNormal))
                throw new ArgumentOutOfRangeException("minimumOffsetMm","毫米范围超出来源Frame可表示范围。");
            double left=Math.Min(au,bu)-.5,right=Math.Max(au,bu)+.5;
            double top=Math.Min(av,bv)-.5,bottom=Math.Max(av,bv)+.5;
            var selected=new List<int>();
            for(int i=0;i<source.Mesh.VertexCount;i++)
            {
                if((i&255)==0)cancellation.ThrowIfCancellationRequested();
                var p=source.Mesh.Vertices[i];
                var voxel=frame.PhysicalToVoxel(p.X,p.Y,p.Z);
                double u,v,w;SliceCoordinates(plane,voxel,out u,out v,out w);
                // 容差在原始体素坐标下比较，不能用固定毫米容差吞掉微小Spacing的整个物体。
                const double epsilon=1e-7;
                if(u<left-epsilon||u>right+epsilon||v<top-epsilon||v>bottom+epsilon||
                    w<minimumNormal-epsilon||w>maximumNormal+epsilon)continue;
                if(selected.Count==30000)throw new InvalidOperationException("ROI超过30000个顶点预算，请缩小范围。");
                selected.Add(i);
            }
            if(selected.Count<3)throw new InvalidOperationException("ROI内有效Mesh顶点不足3个，请调整选区和法向范围。");
            cancellation.ThrowIfCancellationRequested();
            return selected.AsReadOnly();
        }

        private static void SliceCoordinates(FVOSlicePlane plane,FVOVoxelCoordinate3D p,
            out double u,out double v,out double w)
        {
            switch(plane)
            {
                case FVOSlicePlane.XY:u=p.X;v=p.Y;w=p.Z;return;
                case FVOSlicePlane.XZ:u=p.X;v=p.Z;w=p.Y;return;
                case FVOSlicePlane.YZ:u=p.Y;v=p.Z;w=p.X;return;
                default:throw new ArgumentOutOfRangeException("plane");
            }
        }

        public static CT3DPlaneFitResult FitSelectedVertices(
            CT3DVoxelBoundaryMeshResult source,IReadOnlyList<int> selectedVertexIndices,
            double maxPlanarityRatio=0.08,double maxInPlaneCondition=1000000,
            CancellationToken cancellation=default(CancellationToken))
        {
            if(source==null)throw new ArgumentNullException("source");
            if(selectedVertexIndices==null)throw new ArgumentNullException("selectedVertexIndices");
            if(selectedVertexIndices.Count<3||selectedVertexIndices.Count>30000)
                throw new ArgumentOutOfRangeException("selectedVertexIndices",
                    "平面ROI须包含3~30000个不同网格顶点。");
            if(double.IsNaN(maxPlanarityRatio)||double.IsInfinity(maxPlanarityRatio)||
                maxPlanarityRatio<0||maxPlanarityRatio>1)
                throw new ArgumentOutOfRangeException("maxPlanarityRatio");
            if(!source.HasCalibratedClosedGeometry)
                throw new InvalidOperationException(
                    "专业平面拟合需要已标定、非截断的单连通闭合Mesh。");
            cancellation.ThrowIfCancellationRequested();
            var indices=new HashSet<int>();
            var xyz=new double[selectedVertexIndices.Count,3];
            for(int i=0;i<selectedVertexIndices.Count;i++)
            {
                if((i&255)==0)cancellation.ThrowIfCancellationRequested();
                int index=selectedVertexIndices[i];
                if(index<0||index>=source.Mesh.VertexCount)
                    throw new ArgumentOutOfRangeException("selectedVertexIndices",
                        "面片ROI包含越界顶点索引。");
                if(!indices.Add(index))
                    throw new ArgumentException("ROI必须选择互异的网格顶点。","selectedVertexIndices");
                FGEPoint3D vertex=source.Mesh.Vertices[index];
                xyz[i,0]=vertex.X;xyz[i,1]=vertex.Y;xyz[i,2]=vertex.Z;
            }
            FNUPcaPlaneResult fitted=FNULinearAlgebra.FitPlaneBySvd(
                xyz,maxInPlaneCondition);
            if(fitted.PlanarityRatio>maxPlanarityRatio)
                throw new InvalidOperationException(
                    "选中ROI缺乏单一平面特征，法向残差与切向尺度比超出阈值。");
            cancellation.ThrowIfCancellationRequested();
            return new CT3DPlaneFitResult(source,fitted);
        }
    }
}
