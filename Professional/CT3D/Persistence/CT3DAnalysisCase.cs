using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading;
using CxVisual.Foundation.Volume;

namespace CxVisual.Professional.CT3D
{
    public enum CT3DAnalysisKind { MeshPlane=1, SurfaceSphere=2, SurfaceCylinder=3 }
    /// <summary>由已拟合专业Feature派生的只读毫米测量；共享原Feature身份与来源，
    /// 不是以显示网格估算的独立工业计量认证值。</summary>
    public sealed class CT3DFeatureMeasurement
    {
        internal CT3DFeatureMeasurement(string kind,double valueMm,double rmsMm,
            Guid featureId,string sourceRef,long revision,string sourceSignature,
            FVOVolumeSpatialFrame frame,string algorithmVersion,string parameterSignature,string roiSignature)
        {
            if(string.IsNullOrWhiteSpace(kind)||featureId==Guid.Empty||
                string.IsNullOrWhiteSpace(sourceRef)||string.IsNullOrWhiteSpace(sourceSignature)||
                revision<1||frame==null||string.IsNullOrWhiteSpace(algorithmVersion)||
                string.IsNullOrWhiteSpace(parameterSignature)||
                double.IsNaN(valueMm)||double.IsInfinity(valueMm)||
                double.IsNaN(rmsMm)||double.IsInfinity(rmsMm)||rmsMm<0)
                throw new ArgumentException("专业测量来源或数值无效。");
            Kind=kind;ValueMm=valueMm;RmsResidualMm=rmsMm;FeatureResultId=featureId;
            SourceRef=sourceRef;SourceRevision=revision;SourceSignature=sourceSignature;
            SpatialFrame=frame;AlgorithmVersion=algorithmVersion;
            ParameterSignature=parameterSignature;RoiSignature=roiSignature;
        }
        public string Kind {get;private set;}
        public double ValueMm {get;private set;}
        public string Unit {get{return "mm";}}
        public double RmsResidualMm {get;private set;}
        public Guid FeatureResultId {get;private set;}
        public string SourceRef {get;private set;}
        public long SourceRevision {get;private set;}
        public string SourceSignature {get;private set;}
        public FVOVolumeSpatialFrame SpatialFrame {get;private set;}
        public string AlgorithmVersion {get;private set;}
        public string ParameterSignature {get;private set;}
        public string RoiSignature {get;private set;}
        public bool HasEvaluatedUncertainty {get{return false;}}
    }

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
        internal CT3DAnalysisCase(CT3DAssetReference source,CT3DSurfaceSamples surface,
            CT3DCylinderFitResult cylinder)
        {
            Kind=CT3DAnalysisKind.SurfaceCylinder;Source=source;Surface=surface;Cylinder=cylinder;
            SelectedVertexIndices=Array.AsReadOnly(new int[0]);
        }
        public CT3DAnalysisKind Kind {get;private set;}
        public CT3DAssetReference Source {get;private set;}
        /// <summary>仅MeshPlane种类具有Mesh和Plane；SurfaceSphere种类具有Surface和Sphere。</summary>
        public CT3DVoxelBoundaryMeshResult Mesh {get;private set;}
        public CT3DPlaneFitResult Plane {get;private set;}
        public CT3DSurfaceSamples Surface {get;private set;}
        public CT3DSphereFitResult Sphere {get;private set;}
        public CT3DCylinderFitResult Cylinder {get;private set;}
        public ReadOnlyCollection<int> SelectedVertexIndices {get;private set;}
        /// <summary>同来源与几何ROI复算不变的稳定身份指纹；不同源或ROI变化即失效。</summary>
        public string RoiSignature
        {
            get
            {
                if(Kind!=CT3DAnalysisKind.MeshPlane)return null;
                using(var stream=new MemoryStream())
                {
                    using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
                    {
                        writer.Write(Source.Signature);
                        writer.Write(SelectedVertexIndices.Count);
                        foreach(int index in SelectedVertexIndices)
                        {
                            var point=Mesh.Mesh.Vertices[index];
                            writer.Write(index);writer.Write(point.X);writer.Write(point.Y);writer.Write(point.Z);
                        }
                    }
                    return CT3DArtifactFiles.HashBytes(stream.ToArray());
                }
            }
        }
        public double MaxPlanarityRatio {get;private set;}
        public double MaxInPlaneCondition {get;private set;}
        public int MaximumTriangles {get;private set;}
        public string AlgorithmVersion {get{return Kind==CT3DAnalysisKind.MeshPlane
            ? "ct3d.voxel-mesh-plane/1":Kind==CT3DAnalysisKind.SurfaceSphere
            ? "ct3d.surface-sphere/1":"ct3d.surface-cylinder-axis/1";}}
        /// <summary>稳定配方签名：输入版本、算法版本、阈值、预算、ROI或约束轴变化均影响结果有效性。
        /// 相同输入重算保持签名，不依赖随机ResultId。</summary>
        public string ParameterSignature
        {
            get
            {
                using(var stream=new MemoryStream())
                {
                    using(var w=new BinaryWriter(stream,Encoding.UTF8,true))
                    {
                        w.Write(Source.Signature);
                        w.Write(AlgorithmVersion);
                        w.Write((int)Kind);
                        if(Kind==CT3DAnalysisKind.MeshPlane)
                        {
                            w.Write(Mesh.Threshold);w.Write(MaximumTriangles);
                            w.Write(MaxPlanarityRatio);w.Write(MaxInPlaneCondition);
                            w.Write(RoiSignature);
                        }
                        else
                        {
                            w.Write(Surface.Threshold);w.Write(Surface.SampleStep);
                            w.Write(Surface.MaximumPoints);
                            if(Kind==CT3DAnalysisKind.SurfaceSphere)
                                w.Write(Sphere.MaximumConditionNumber);
                            else
                            {
                                w.Write(Cylinder.MaximumConditionNumber);
                                w.Write(Cylinder.AxisDirection.X);
                                w.Write(Cylinder.AxisDirection.Y);
                                w.Write(Cylinder.AxisDirection.Z);
                            }
                        }
                    }
                    return CT3DArtifactFiles.HashBytes(stream.ToArray());
                }
            }
        }
        /// <summary>同一专业结果的派生Feature测量，重开时从保存并验证的原值结果确定性恢复。
        /// ROI平面的残差是质量量，不冒充有基准的距离或GD&T。</summary>
        public IReadOnlyList<CT3DFeatureMeasurement> GetFeatureMeasurements()
        {
            var values=new List<CT3DFeatureMeasurement>();
            if(Kind==CT3DAnalysisKind.MeshPlane)
            {
                values.Add(new CT3DFeatureMeasurement("plane.rms-residual",Plane.RmsResidualMm,
                    Plane.RmsResidualMm,Plane.ResultId,Source.SourceRef,Source.Revision,Source.Signature,
                    Source.SpatialFrame,AlgorithmVersion,ParameterSignature,RoiSignature));
                values.Add(new CT3DFeatureMeasurement("plane.max-absolute-residual",Plane.MaxAbsoluteResidualMm,
                    Plane.RmsResidualMm,Plane.ResultId,Source.SourceRef,Source.Revision,Source.Signature,
                    Source.SpatialFrame,AlgorithmVersion,ParameterSignature,RoiSignature));
            }
            if(Kind==CT3DAnalysisKind.SurfaceCylinder)
            {
                values.Add(new CT3DFeatureMeasurement("cylinder.radius",Cylinder.RadiusMm,
                    Cylinder.RmsResidualMm,Cylinder.ResultId,Source.SourceRef,Source.Revision,Source.Signature,
                    Source.SpatialFrame,AlgorithmVersion,ParameterSignature,RoiSignature));
                values.Add(new CT3DFeatureMeasurement("cylinder.diameter",Cylinder.DiameterMm,
                    Cylinder.RmsResidualMm,Cylinder.ResultId,Source.SourceRef,Source.Revision,Source.Signature,
                    Source.SpatialFrame,AlgorithmVersion,ParameterSignature,RoiSignature));
            }
            if(Kind==CT3DAnalysisKind.SurfaceSphere)
            {
                values.Add(new CT3DFeatureMeasurement("sphere.radius",Sphere.RadiusMm,
                    Sphere.RmsResidualMm,Sphere.ResultId,Source.SourceRef,Source.Revision,Source.Signature,
                    Source.SpatialFrame,AlgorithmVersion,ParameterSignature,RoiSignature));
                values.Add(new CT3DFeatureMeasurement("sphere.diameter",Sphere.DiameterMm,
                    Sphere.RmsResidualMm,Sphere.ResultId,Source.SourceRef,Source.Revision,Source.Signature,
                    Source.SpatialFrame,AlgorithmVersion,ParameterSignature,RoiSignature));
            }
            return values.AsReadOnly();
        }
        public string Unit {get{return "mm";}}
        /// <summary>现有残差/拓扑证据不构成工业计量不确定度评定。</summary>
        public bool HasEvaluatedMeasurementUncertainty {get{return false;}}

        /// <summary>采用固定物理轴向约束拟合，尚不代表自动圆柱识别或工业精度。</summary>
        public static CT3DAnalysisCase AnalyzeCylinder(string root,CT3DAssetReference source,
            float threshold,int sampleStep,int maximumPoints,FVOPhysicalPoint3D axisDirection,
            CancellationToken cancellation,double maximumConditionNumber=10000000)
        {
            if(source==null)throw new ArgumentNullException("source");
            if(!source.SpatialFrame.IsCalibrated)
                throw new InvalidOperationException("ct3d.precision.unavailable");
            using(var volume=source.LoadVolume(root,cancellation))
            using(var lease=volume.AcquireOriginalScalarLease())
            {
                var surface=CT3DSurfaceSphereAnalysis.SampleBoundary(lease,source.SourceRef,
                    source.Revision,threshold,sampleStep,maximumPoints,cancellation);
                var cylinder=CT3DSurfaceCylinderAnalysis.FitCylinder(surface,axisDirection,
                    maximumConditionNumber,cancellation);
                cancellation.ThrowIfCancellationRequested();
                return new CT3DAnalysisCase(source,surface,cylinder);
            }
        }

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

        /// <summary>替换物理Mesh ROI并生成新Case；来源资产与算法质量约束保持一致。
        /// 后续工程修改由MEM Commit负责，旧ResultId不能复用。</summary>
        public CT3DAnalysisCase RecomputeWithMeshRoi(string root,IReadOnlyList<int> selectedVertexIndices,
            CancellationToken cancellation)
        {
            if(Kind!=CT3DAnalysisKind.MeshPlane)
                throw new InvalidOperationException("仅MeshPlane结果可修改顶点ROI。");
            if(selectedVertexIndices==null)throw new ArgumentNullException("selectedVertexIndices");
            return Analyze(root,Source,Mesh.Threshold,MaximumTriangles,
                mesh=>selectedVertexIndices,cancellation,MaxPlanarityRatio,MaxInPlaneCondition);
        }

        /// <summary>更新圆柱轴向约束并进行原值重算，返回新Feature/Measurement。</summary>
        public CT3DAnalysisCase RecomputeWithCylinderAxis(string root,
            FVOPhysicalPoint3D axisDirection,CancellationToken cancellation)
        {
            if(Kind!=CT3DAnalysisKind.SurfaceCylinder)
                throw new InvalidOperationException("仅圆柱结果可更新轴向约束。");
            return AnalyzeCylinder(root,Source,Surface.Threshold,Surface.SampleStep,
                Surface.MaximumPoints,axisDirection,cancellation,Cylinder.MaximumConditionNumber);
        }

        public CT3DAnalysisCase Recompute(string root,CancellationToken cancellation)
        {
            if(Kind==CT3DAnalysisKind.SurfaceCylinder)
                return AnalyzeCylinder(root,Source,Surface.Threshold,Surface.SampleStep,
                    Surface.MaximumPoints,Cylinder.AxisDirection,cancellation,Cylinder.MaximumConditionNumber);
            if(Kind==CT3DAnalysisKind.SurfaceSphere)
                return AnalyzeSphere(root,Source,Surface.Threshold,Surface.SampleStep,
                    Surface.MaximumPoints,cancellation,Sphere.MaximumConditionNumber);
            return Analyze(root,Source,Mesh.Threshold,MaximumTriangles,
                mesh=>SelectedVertexIndices,cancellation,MaxPlanarityRatio,MaxInPlaneCondition);
        }
    }
}
