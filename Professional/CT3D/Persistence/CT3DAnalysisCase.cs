using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading;
using CxVisual.Foundation.Volume;
using CxVisual.Foundation.Geometry;

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

    /// <summary>局部Mesh ROI顶点的稳定引用；同源同物理几何复算可重新解析。</summary>
    public sealed class CT3DRoiVertexReference
    {
        internal CT3DRoiVertexReference(string sourceSignature,string roiSignature,
            int vertexIndex,FGEPoint3D pointMm)
        {
            if(string.IsNullOrWhiteSpace(sourceSignature)||string.IsNullOrWhiteSpace(roiSignature)||
                vertexIndex<0)throw new ArgumentException("ROI顶点身份无效。");
            SourceSignature=sourceSignature;RoiSignature=roiSignature;
            VertexIndex=vertexIndex;PointMm=pointMm;
            using(var stream=new MemoryStream())
            {
                using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
                {
                    writer.Write(sourceSignature);writer.Write(roiSignature);writer.Write(vertexIndex);
                    writer.Write(pointMm.X);writer.Write(pointMm.Y);writer.Write(pointMm.Z);
                }
                StableId=CT3DArtifactFiles.HashBytes(stream.ToArray());
            }
        }
        public string StableId {get;private set;}
        public string SourceSignature {get;private set;}
        public string RoiSignature {get;private set;}
        public int VertexIndex {get;private set;}
        public FGEPoint3D PointMm {get;private set;}
        public string Unit {get{return "mm";}}
    }

    /// <summary>专业结果的数值质量证据，不等于计量不确定度或工厂合格判定。</summary>
    public sealed class CT3DQualityMetric
    {
        internal CT3DQualityMetric(string kind,double value,string unit,Guid featureId,
            string sourceSignature,string parameterSignature)
        {
            if(string.IsNullOrWhiteSpace(kind)||string.IsNullOrWhiteSpace(unit)||
                featureId==Guid.Empty||string.IsNullOrWhiteSpace(sourceSignature)||
                string.IsNullOrWhiteSpace(parameterSignature)||double.IsNaN(value)||double.IsInfinity(value))
                throw new ArgumentException("三维质量数值或来源无效。");
            Kind=kind;Value=value;Unit=unit;FeatureResultId=featureId;
            SourceSignature=sourceSignature;ParameterSignature=parameterSignature;
        }
        public string Kind {get;private set;}
        public double Value {get;private set;}
        public string Unit {get;private set;}
        public Guid FeatureResultId {get;private set;}
        public string SourceSignature {get;private set;}
        public string ParameterSignature {get;private set;}
        public bool IsIndustrialAccuracyCertified {get{return false;}}
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
        /// <summary>原体素物理切片配方；旧案例没有配方时保持null。</summary>
        public CT3DSliceRoiRecipe SliceRoiRecipe {get;private set;}
        /// <summary>同来源与几何ROI复算不变的稳定身份指纹；不同源或ROI变化即失效。</summary>
        public string RoiSignature
        {
            get
            {
                if(Kind==CT3DAnalysisKind.SurfaceSphere)return null;
                if(Kind==CT3DAnalysisKind.SurfaceCylinder&&
                    Cylinder.SelectedPointIndices.Count==0)return null;
                using(var stream=new MemoryStream())
                {
                    using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
                    {
                        writer.Write(Source.Signature);
                        if(Kind==CT3DAnalysisKind.MeshPlane)
                        {
                            writer.Write(SelectedVertexIndices.Count);
                            foreach(int index in SelectedVertexIndices)
                            {
                                var point=Mesh.Mesh.Vertices[index];
                                writer.Write(index);writer.Write(point.X);writer.Write(point.Y);writer.Write(point.Z);
                            }
                        }
                        else
                        {
                            writer.Write(Cylinder.SelectedPointIndices.Count);
                            foreach(int index in Cylinder.SelectedPointIndices)
                            {
                                var point=Surface.Points[index];
                                writer.Write(index);writer.Write(point.X);writer.Write(point.Y);writer.Write(point.Z);
                            }
                        }
                    }
                    return CT3DArtifactFiles.HashBytes(stream.ToArray());
                }
            }
        }
        /// <summary>把ROI逐顶点解析成稳定可追溯子元素引用，禁止使用屏幕或GPU坐标。</summary>
        public IReadOnlyList<CT3DRoiVertexReference> GetSelectedVertexReferences()
        {
            var values=new List<CT3DRoiVertexReference>();
            if(Kind!=CT3DAnalysisKind.MeshPlane)return values.AsReadOnly();
            string signature=RoiSignature;
            foreach(int index in SelectedVertexIndices)
                values.Add(new CT3DRoiVertexReference(Source.Signature,signature,
                    index,Mesh.Mesh.Vertices[index]));
            return values.AsReadOnly();
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
                            if(SliceRoiRecipe!=null)w.Write(SliceRoiRecipe.Signature);
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
                            w.Write(Cylinder.SelectedPointIndices.Count);
                            foreach(int index in Cylinder.SelectedPointIndices)w.Write(index);
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
        /// <summary>统一质量证据；维度与毫米误差分离，不以无量纲条件数冒充尺寸。</summary>
        public IReadOnlyList<CT3DQualityMetric> GetQualityEvidence()
        {
            var metrics=new List<CT3DQualityMetric>();
            Guid id=Kind==CT3DAnalysisKind.MeshPlane?Plane.ResultId:
                Kind==CT3DAnalysisKind.SurfaceSphere?Sphere.ResultId:Cylinder.ResultId;
            if(Kind==CT3DAnalysisKind.MeshPlane)
            {
                metrics.Add(new CT3DQualityMetric("plane.rms-residual",Plane.RmsResidualMm,
                    "mm",id,Source.Signature,ParameterSignature));
                metrics.Add(new CT3DQualityMetric("plane.max-absolute-residual",Plane.MaxAbsoluteResidualMm,
                    "mm",id,Source.Signature,ParameterSignature));
                metrics.Add(new CT3DQualityMetric("plane.planarity-ratio",Plane.PlanarityRatio,
                    "ratio",id,Source.Signature,ParameterSignature));
                metrics.Add(new CT3DQualityMetric("plane.in-plane-condition",Plane.InPlaneConditionNumber,
                    "ratio",id,Source.Signature,ParameterSignature));
            }
            else
            {
                double rms=Kind==CT3DAnalysisKind.SurfaceSphere?Sphere.RmsResidualMm:Cylinder.RmsResidualMm;
                double maximum=Kind==CT3DAnalysisKind.SurfaceSphere?Sphere.MaxAbsoluteResidualMm:
                    Cylinder.MaxAbsoluteResidualMm;
                double condition=Kind==CT3DAnalysisKind.SurfaceSphere?Sphere.ConditionNumber:
                    Cylinder.ConditionNumber;
                metrics.Add(new CT3DQualityMetric("surface.rms-residual",rms,
                    "mm",id,Source.Signature,ParameterSignature));
                metrics.Add(new CT3DQualityMetric("surface.max-absolute-residual",maximum,
                    "mm",id,Source.Signature,ParameterSignature));
                metrics.Add(new CT3DQualityMetric("surface.fit-condition",condition,
                    "ratio",id,Source.Signature,ParameterSignature));
            }
            return metrics.AsReadOnly();
        }
        /// <summary>现有残差/拓扑证据不构成工业计量不确定度评定。</summary>
        public bool HasEvaluatedMeasurementUncertainty {get{return false;}}

        /// <summary>采用固定物理轴向约束拟合，尚不代表自动圆柱识别或工业精度。</summary>
        public static CT3DAnalysisCase AnalyzeCylinder(string root,CT3DAssetReference source,
            float threshold,int sampleStep,int maximumPoints,FVOPhysicalPoint3D axisDirection,
            CancellationToken cancellation,double maximumConditionNumber=10000000,
            IReadOnlyList<int> selectedPointIndices=null)
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
                    selectedPointIndices,maximumConditionNumber,cancellation);
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

        /// <summary>跨MEM重开继续编辑二维切片ROI：先验证同一资产Frame，
        /// 从保存的物理四角重建原Voxel切片，不依赖旧WPF或旧FVO对象引用。</summary>
        public CT3DAnalysisCase RecomputeWithSliceRectangle(string root,
            FVOPhysicalSliceRectangle rectangle,double minimumOffsetMm,double maximumOffsetMm,
            CancellationToken cancellation)
        {
            if(Kind!=CT3DAnalysisKind.MeshPlane)
                throw new InvalidOperationException("只有MeshPlane案例可从切片ROI重新拟合。");
            var recipe=CT3DSliceRoiRecipe.Capture(Source,rectangle,minimumOffsetMm,maximumOffsetMm);
            return RecomputeWithSliceRoiRecipe(root,recipe,cancellation);
        }

        /// <summary>使用工程中保存的原体素四角重算；不反推旧预览像素。</summary>
        public CT3DAnalysisCase RecomputeWithSavedSliceRoi(string root,CancellationToken cancellation)
        {
            if(SliceRoiRecipe==null)
                throw new InvalidOperationException("历史案例没有已保存的切片ROI编辑配方。");
            return RecomputeWithSliceRoiRecipe(root,SliceRoiRecipe,cancellation);
        }

        public CT3DAnalysisCase RecomputeWithSliceRoiRecipe(string root,
            CT3DSliceRoiRecipe recipe,CancellationToken cancellation)
        {
            if(Kind!=CT3DAnalysisKind.MeshPlane)
                throw new InvalidOperationException("只有MeshPlane结果可编辑切片ROI。");
            if(recipe==null)throw new ArgumentNullException("recipe");
            recipe.RequireSource(Source);
            cancellation.ThrowIfCancellationRequested();
            var next=Analyze(root,Source,Mesh.Threshold,MaximumTriangles,
                mesh=>recipe.SelectVertices(mesh,Source,cancellation),
                cancellation,MaxPlanarityRatio,MaxInPlaneCondition);
            return next.WithSliceRoiRecipe(recipe);
        }

        internal CT3DAnalysisCase WithSliceRoiRecipe(CT3DSliceRoiRecipe recipe)
        {
            if(Kind!=CT3DAnalysisKind.MeshPlane)throw new InvalidOperationException("只有MeshPlane支持切片配方。");
            if(recipe==null)throw new ArgumentNullException("recipe");
            recipe.RequireSource(Source);
            SliceRoiRecipe=recipe;
            return this;
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

        /// <summary>真实Mesh三角面子元素组合ROI；重新提取Mesh后验证资产及顶点，
        /// 复用已有原值拟合/Result合同，MEM持久化仍由原工程Owner负责。</summary>
        public CT3DAnalysisCase RecomputeWithMeshFaces(string root,
            IReadOnlyList<CT3DMeshFaceReference> faces,CancellationToken cancellation)
        {
            if(Kind!=CT3DAnalysisKind.MeshPlane)
                throw new InvalidOperationException("非MeshPlane案例不能使用三角面ROI。");
            if(faces==null)throw new ArgumentNullException("faces");
            return Analyze(root,Source,Mesh.Threshold,MaximumTriangles,
                mesh=>CT3DMeshFaceSelection.ResolveVertices(Source,mesh,faces,cancellation),
                cancellation,MaxPlanarityRatio,MaxInPlaneCondition);
        }

        /// <summary>更新圆柱轴向约束并进行原值重算，返回新Feature/Measurement。</summary>
        public CT3DAnalysisCase RecomputeWithCylinderAxis(string root,
            FVOPhysicalPoint3D axisDirection,CancellationToken cancellation)
        {
            if(Kind!=CT3DAnalysisKind.SurfaceCylinder)
                throw new InvalidOperationException("仅圆柱结果可更新轴向约束。");
            return AnalyzeCylinder(root,Source,Surface.Threshold,Surface.SampleStep,
                Surface.MaximumPoints,axisDirection,cancellation,Cylinder.MaximumConditionNumber,
                Cylinder.SelectedPointIndices.Count==0?null:Cylinder.SelectedPointIndices);
        }

        /// <summary>替换选定圆柱侧壁点集并使依赖Feature/Measurement重新计算。</summary>
        public CT3DAnalysisCase RecomputeWithCylinderRoi(string root,
            IReadOnlyList<int> selectedPointIndices,CancellationToken cancellation)
        {
            if(Kind!=CT3DAnalysisKind.SurfaceCylinder)
                throw new InvalidOperationException("仅圆柱Case可修改侧壁ROI。");
            if(selectedPointIndices==null)throw new ArgumentNullException("selectedPointIndices");
            return AnalyzeCylinder(root,Source,Surface.Threshold,Surface.SampleStep,
                Surface.MaximumPoints,Cylinder.AxisDirection,cancellation,
                Cylinder.MaximumConditionNumber,selectedPointIndices);
        }

        public CT3DAnalysisCase Recompute(string root,CancellationToken cancellation)
        {
            if(Kind==CT3DAnalysisKind.SurfaceCylinder)
                return AnalyzeCylinder(root,Source,Surface.Threshold,Surface.SampleStep,
                    Surface.MaximumPoints,Cylinder.AxisDirection,cancellation,Cylinder.MaximumConditionNumber,
                    Cylinder.SelectedPointIndices.Count==0?null:Cylinder.SelectedPointIndices);
            if(Kind==CT3DAnalysisKind.SurfaceSphere)
                return AnalyzeSphere(root,Source,Surface.Threshold,Surface.SampleStep,
                    Surface.MaximumPoints,cancellation,Sphere.MaximumConditionNumber);
            if(SliceRoiRecipe!=null)return RecomputeWithSavedSliceRoi(root,cancellation);
            return Analyze(root,Source,Mesh.Threshold,MaximumTriangles,
                mesh=>SelectedVertexIndices,cancellation,MaxPlanarityRatio,MaxInPlaneCondition);
        }
    }
}
