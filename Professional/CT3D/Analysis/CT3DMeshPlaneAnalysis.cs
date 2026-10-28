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
            FNUPcaPlaneResult fit)
        {
            ResultId=Guid.NewGuid();
            SourceMeshResultId=source.ResultId;
            SourceRef=source.SourceRef;
            SourceRevision=source.SourceRevision;
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
        public FVOPhysicalPoint3D PointMm {get;private set;}
        public FGEVector3D Normal {get;private set;}
        public double RmsResidualMm {get;private set;}
        public double MaxAbsoluteResidualMm {get;private set;}
        public double PlanarityRatio {get;private set;}
        public double InPlaneConditionNumber {get;private set;}
        public int SampleCount {get;private set;}
        public string FitMethod {get{return "roi-mesh-vertex-orthogonal-PCA-SVD";}}
    }

    /// <summary>
    /// CT3D专业面片ROI几何拟合。复用FGE实体点和FNU SVD，不自行实现第二套求解器。
    /// 调用者必须提供选中局部面的唯一顶点索引；对整个闭合物体的所有点拟合平面不合理。
    /// </summary>
    public static class CT3DMeshPlaneAnalysis
    {
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
