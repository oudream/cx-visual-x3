using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using CxVisual.Foundation.Volume;

namespace CxVisual.Professional.CT3D
{
    public enum CT3DAnalysisKind { MeshPlane=1, SurfaceSphere=2 }
    /// <summary>V03专业结果及其可复算配方；不拥有MEM树、WPF状态或原始数据副本。</summary>
    public sealed class CT3DAnalysisCase
    {
        internal CT3DAnalysisCase(CT3DAssetReference source,CT3DVoxelBoundaryMeshResult mesh,
            CT3DPlaneFitResult plane,IReadOnlyList<int> roi,double planarity,double condition,int budget)
        {
            Kind=CT3DAnalysisKind.MeshPlane;
            Source=source;Mesh=mesh;Plane=plane;
            var copy=new int[roi.Count];for(int i=0;i<copy.Length;i++)copy[i]=roi[i];
            SelectedVertexIndices=Array.AsReadOnly(copy);
            MaxPlanarityRatio=planarity;MaxInPlaneCondition=condition;MaximumTriangles=budget;
        }
        internal CT3DAnalysisCase(CT3DAssetReference source,CT3DSurfaceSamples surface,CT3DSphereFitResult sphere)
        {
            Kind=CT3DAnalysisKind.SurfaceSphere;Source=source;Surface=surface;Sphere=sphere;
            SelectedVertexIndices=Array.AsReadOnly(new int[0]);
        }
        public CT3DAnalysisKind Kind {get;private set;}
        public CT3DAssetReference Source {get;private set;}
        /// <summary>仅MeshPlane种类具有Mesh和Plane；SurfaceSphere种类具有Surface和Sphere。</summary>
        public CT3DVoxelBoundaryMeshResult Mesh {get;private set;}
        public CT3DPlaneFitResult Plane {get;private set;}
        public CT3DSurfaceSamples Surface {get;private set;}
        public CT3DSphereFitResult Sphere {get;private set;}
        public ReadOnlyCollection<int> SelectedVertexIndices {get;private set;}
        public double MaxPlanarityRatio {get;private set;}
        public double MaxInPlaneCondition {get;private set;}
        public int MaximumTriangles {get;private set;}
        public string AlgorithmVersion {get{return Kind==CT3DAnalysisKind.MeshPlane
            ? "ct3d.voxel-mesh-plane/1":"ct3d.surface-sphere/1";}}
        public string Unit {get{return "mm";}}
        /// <summary>现有残差/拓扑证据不构成工业计量不确定度评定。</summary>
        public bool HasEvaluatedMeasurementUncertainty {get{return false;}}

        public static CT3DAnalysisCase AnalyzeSphere(string root,CT3DAssetReference source,
            float threshold,int sampleStep,int maximumPoints,CancellationToken cancellation,
            double maximumConditionNumber=10000000)
        {
            if(source==null)throw new ArgumentNullException("source");
            if(!source.SpatialFrame.IsCalibrated)
                throw new InvalidOperationException("ct3d.precision.unavailable：未标定来源不能生成物理测量案例。");
            using(var volume=source.LoadVolume(root,cancellation))
            using(var lease=volume.AcquireOriginalScalarLease())
            {
                var surface=CT3DSurfaceSphereAnalysis.SampleBoundary(lease,source.SourceRef,
                    source.Revision,threshold,sampleStep,maximumPoints,cancellation);
                var sphere=CT3DSurfaceSphereAnalysis.FitSphere(surface,maximumConditionNumber);
                cancellation.ThrowIfCancellationRequested();
                return new CT3DAnalysisCase(source,surface,sphere);
            }
        }

        public static CT3DAnalysisCase Analyze(string root,CT3DAssetReference source,
            float threshold,int maximumTriangles,
            Func<CT3DVoxelBoundaryMeshResult,IReadOnlyList<int>> selectVertices,
            CancellationToken cancellation,double maxPlanarityRatio=0.08,double maxInPlaneCondition=1000000)
        {
            if(source==null)throw new ArgumentNullException("source");
            if(selectVertices==null)throw new ArgumentNullException("selectVertices");
            if(!source.SpatialFrame.IsCalibrated)
                throw new InvalidOperationException("ct3d.precision.unavailable：未标定来源不能生成物理测量案例。");
            // 文件读锁贯穿Hash验证、映射和计算，防止校验后原文件被改写/替换。
            using(var locked=source.OpenVerified(root,cancellation))
            {
                var f=source.SpatialFrame;
                var info=new FVORawVolumeInfo(source.Width,source.Height,source.Depth,
                    (float)f.SpacingX,(float)f.SpacingY,(float)f.SpacingZ,source.Format,source.RelativePath);
                using(var volume=FVORawVolumeLoader.Load(locked.Name,info,64,cancellation))
                {
                    volume.SetSpatialFrame(f);
                    volume.SetRawBuffer(FVORawVolumeLoader.MapRawBuffer(volume,cancellation));
                    using(var lease=volume.AcquireOriginalScalarLease())
                    {
                        var mesh=CT3DVoxelBoundaryMeshExtractor.Extract(lease,source.SourceRef,
                            source.Revision,threshold,maximumTriangles,cancellation);
                        var selection=selectVertices(mesh);
                        if(selection==null)throw new ArgumentException("ROI选择不能为空。");
                        if(selection.Count<3||selection.Count>30000)
                            throw new ArgumentOutOfRangeException("selectVertices","ROI须包含3~30000个不同顶点。");
                        var indices=new int[selection.Count];for(int i=0;i<indices.Length;i++)indices[i]=selection[i];
                        var plane=CT3DMeshPlaneAnalysis.FitSelectedVertices(mesh,indices,
                            maxPlanarityRatio,maxInPlaneCondition,cancellation);
                        cancellation.ThrowIfCancellationRequested();
                        return new CT3DAnalysisCase(source,mesh,plane,indices,
                            maxPlanarityRatio,maxInPlaneCondition,maximumTriangles);
                    }
                }
            }
        }

        /// <summary>显式体素+Z端面示例ROI，可复算；不是通用交互选面或工业Datum。</summary>
        public static IReadOnlyList<int> SelectTopVoxelFace(CT3DVoxelBoundaryMeshResult mesh)
        {
            if(mesh==null)throw new ArgumentNullException("mesh");
            var values=new double[mesh.Mesh.VertexCount];double top=double.NegativeInfinity;
            for(int i=0;i<values.Length;i++)
            {
                var p=mesh.Mesh.Vertices[i];
                values[i]=mesh.SpatialFrame.PhysicalToVoxel(p.X,p.Y,p.Z).Z;
                top=Math.Max(top,values[i]);
            }
            var roi=new List<int>();for(int i=0;i<values.Length;i++)if(Math.Abs(values[i]-top)<1e-6)roi.Add(i);
            return roi.AsReadOnly();
        }

        public CT3DAnalysisCase Recompute(string root,CancellationToken cancellation)
        {
            if(Kind==CT3DAnalysisKind.SurfaceSphere)
                return AnalyzeSphere(root,Source,Surface.Threshold,Surface.SampleStep,
                    Surface.MaximumPoints,cancellation,Sphere.MaximumConditionNumber);
            return Analyze(root,Source,Mesh.Threshold,MaximumTriangles,
                mesh=>SelectedVertexIndices,cancellation,MaxPlanarityRatio,MaxInPlaneCondition);
        }
    }
}
