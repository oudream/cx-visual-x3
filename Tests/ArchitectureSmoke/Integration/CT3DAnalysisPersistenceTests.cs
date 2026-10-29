using System;
using System.IO;
using System.Linq;
using System.Threading;
using CxVisual.Foundation.Volume;
using CxVisual.Mechanism.Core;
using CxVisual.Professional.CT3D;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class CT3DAnalysisPersistenceTests
    {
        public static void Run()
        {
            string folder=Path.Combine(Path.GetTempPath(),"CxVisual-CT3D-V04-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try{VerifyRoundtrip(folder);VerifySphere(Path.Combine(folder,"sphere"));VerifyCylinder(Path.Combine(folder,"cylinder"));VerifySchema1Compatibility(folder);}
            finally{Directory.Delete(folder,true);}
            Console.WriteLine("PASS CT3D V04 MEM source/hash/frame, mesh/plane save reopen recompute, cancellation and corruption");
        }
        private static void VerifyRoundtrip(string folder)
        {
            const int n=12;
            var bytes=new byte[n*n*n*2];
            for(int z=5;z<=6;z++)for(int y=5;y<=6;y++)for(int x=5;x<=6;x++)
            {int i=(x+y*n+z*n*n)*2;bytes[i]=0xa4;bytes[i+1]=0x06;}
            string raw=Path.Combine(folder,"source.raw");File.WriteAllBytes(raw,bytes);
            var info=new FVORawVolumeInfo(n,n,n,1,1,1,FVORawVoxelFormat.UInt16,"source.raw");
            var frame=new FVOVolumeSpatialFrame(1,1,1,20,30,40,new double[]{1,0,0,0,1,0,0,0,1},true);
            var source=CT3DAssetReference.Capture(folder,"source.raw",info,frame,7,CancellationToken.None);
            using(var mapped=source.LoadVolume(folder,CancellationToken.None))
            {
                Reject<IOException>(()=>File.WriteAllBytes(raw,bytes));
                using(var lease=mapped.AcquireOriginalScalarLease())
                    Check(lease.ReadScalar(5,5,5)==1700,"校验读锁与只读映射共存且禁止并发改写");
            }
            var analysis=CT3DAnalysisCase.Analyze(folder,source,1000,100,
                CT3DAnalysisCase.SelectTopVoxelFace,CancellationToken.None,0.01,1000);
            Reject<ArgumentOutOfRangeException>(()=>CT3DAnalysisCase.Analyze(folder,source,
                1000,100,mesh=>new int[30001],CancellationToken.None));
            Check(analysis.Mesh.Mesh.VertexCount==26 && analysis.Plane.SampleCount==9 &&
                Math.Abs(analysis.Plane.PointMm.Z-46.5)<1e-9 && analysis.Plane.RmsResidualMm<1e-9 &&
                !analysis.HasEvaluatedMeasurementUncertainty,"真实UInt16 Mesh到局部平面质量合同");
            var planeMeasures=analysis.GetFeatureMeasurements();
            Check(planeMeasures.Count==2&&
                planeMeasures[0].Kind=="plane.rms-residual"&&
                planeMeasures[1].Kind=="plane.max-absolute-residual"&&
                Math.Abs(planeMeasures[0].ValueMm-analysis.Plane.RmsResidualMm)<1e-12&&
                Math.Abs(planeMeasures[1].ValueMm-analysis.Plane.MaxAbsoluteResidualMm)<1e-12&&
                planeMeasures.All(m=>m.FeatureResultId==analysis.Plane.ResultId&&
                    m.SourceSignature==source.Signature&&!m.HasEvaluatedUncertainty),
                "平面RMS与最大偏差必须属于质量证据而非虚构Datum尺寸");
            var copy=CT3DAssetReference.Import(folder,raw,info,frame,7,CancellationToken.None,source.Id);
            Check(copy.ContentHash==source.ContentHash && File.ReadAllBytes(raw).SequenceEqual(bytes),
                "内容寻址导入不改原始文件");
            string project=Path.Combine(folder,"analysis.cxproj");MRIObjectAddress address;
            int revision;
            using(var engineering=MEMEngineering.Create("三维分析案例"))
            {
                var page=engineering.AddPage("三维");var owner=page.RootContent;
                int before=engineering.Revision;
                address=CT3DAnalysisPersistence.Commit(engineering,owner,before,folder,analysis,CancellationToken.None);
                revision=engineering.Revision;
                Check(revision==before+1 && engineering.CanUndo,"工程由MEM统一事务提交");
                engineering.Undo().RequireValue();
                Reject<InvalidDataException>(()=>CT3DAnalysisPersistence.Open(engineering,address,folder,CancellationToken.None));
                engineering.Redo().RequireValue();
                var restored=CT3DAnalysisPersistence.Open(engineering,address,folder,CancellationToken.None);
                Check(restored.Mesh.ResultId==analysis.Mesh.ResultId,"Undo/Redo不重新生成结果身份");
                int recomputeRevision=engineering.Revision;
                CT3DAnalysisPersistence.RecomputeAndCommit(engineering,owner,recomputeRevision,folder,CancellationToken.None);
                var replaced=CT3DAnalysisPersistence.Open(engineering,address,folder,CancellationToken.None);
                Check(replaced.Mesh.ResultId!=restored.Mesh.ResultId&&
                    replaced.Plane.ResultId!=restored.Plane.ResultId&&
                    replaced.Source.Signature==restored.Source.Signature&&
                    engineering.Revision==recomputeRevision+1,
                    "原位复算生成新结果身份、保留同源并通过MEM版本事务替换");
                Reject<InvalidOperationException>(()=>CT3DAnalysisPersistence.RecomputeAndCommit(
                    engineering,owner,recomputeRevision,folder,CancellationToken.None));
                int replacementRevision=engineering.Revision;
                Reject<OperationCanceledException>(()=>CT3DAnalysisPersistence.RecomputeAndCommit(
                    engineering,owner,replacementRevision,folder,new CancellationToken(true)));
                Check(engineering.Revision==replacementRevision,"版本冲突与取消不修改专业工程");
                engineering.Undo().RequireValue();
                Check(CT3DAnalysisPersistence.Open(engineering,address,folder,CancellationToken.None)
                    .Mesh.ResultId==restored.Mesh.ResultId,"MEM Undo恢复旧专业结果身份");
                engineering.Redo().RequireValue();
                Check(CT3DAnalysisPersistence.Open(engineering,address,folder,CancellationToken.None)
                    .Mesh.ResultId==replaced.Mesh.ResultId,"MEM Redo恢复新专业结果身份");
                engineering.Undo().RequireValue();
                Reject<InvalidOperationException>(()=>CT3DAnalysisPersistence.Commit(engineering,owner,before,
                    folder,analysis,CancellationToken.None));
                int current=engineering.Revision;
                var cancelled=new CancellationToken(true);
                Reject<OperationCanceledException>(()=>CT3DAnalysisPersistence.Commit(engineering,owner,current,folder,analysis,cancelled));
                Check(engineering.Revision==current,"取消和版本冲突不修改工程");
                engineering.SaveAs(project);
            }
            using(var engineering=MEMEngineering.Open(project))
            {
                var restored=CT3DAnalysisPersistence.Open(engineering,address,folder,CancellationToken.None);
                Check(restored.Source.Id==source.Id && restored.Source.Signature==source.Signature &&
                    restored.Source.Revision==7 && restored.Mesh.ResultId==analysis.Mesh.ResultId &&
                    restored.Plane.ResultId==analysis.Plane.ResultId &&
                    restored.Plane.SourceMeshResultId==restored.Mesh.ResultId &&
                    restored.Mesh.Mesh.TriangleCount==48 && restored.SelectedVertexIndices.SequenceEqual(analysis.SelectedVertexIndices),
                    "关闭重开保留来源/空间签名/结果身份/局部ROI与Mesh");
                Check(restored.ParameterSignature==analysis.ParameterSignature&&
                    restored.ParameterSignature.Length==64,
                    "Mesh/Plane工程重开保留稳定算法参数签名");
                Check(restored.RoiSignature==analysis.RoiSignature&&
                    restored.RoiSignature!=null&&restored.RoiSignature.Length==64,
                    "原RAW和选中毫米ROI在MEM重开后保持稳定身份");
                Check(restored.Source.SpatialFrame!=frame &&
                    Math.Abs(restored.Plane.SignedDistanceMm(new FVOPhysicalPoint3D(25,35,47.5),restored.Source.SpatialFrame)-1)<1e-9,
                    "重建Frame对象后仍可在恢复的同一Frame内测量");
                var reopenedMeasures=restored.GetFeatureMeasurements();
                Check(reopenedMeasures.Count==2&&
                    reopenedMeasures.All(m=>m.FeatureResultId==restored.Plane.ResultId&&
                        m.SourceSignature==source.Signature)&&
                    Math.Abs(reopenedMeasures[0].ValueMm-planeMeasures[0].ValueMm)<1e-12,
                    "MEM恢复平面质量量值与原结果身份");
                var recomputed=restored.Recompute(folder,CancellationToken.None);
                Check(recomputed.Mesh.ResultId!=restored.Mesh.ResultId &&
                    recomputed.Plane.ResultId!=restored.Plane.ResultId &&
                    recomputed.Source.Signature==restored.Source.Signature &&
                    recomputed.RoiSignature==restored.RoiSignature &&
                    recomputed.ParameterSignature==restored.ParameterSignature &&
                    Math.Abs(recomputed.Plane.PointMm.Z-restored.Plane.PointMm.Z)<1e-10 &&
                    recomputed.Mesh.Mesh.TriangleIndices.SequenceEqual(restored.Mesh.Mesh.TriangleIndices),
                    "复算形成新结果身份且几何量值一致");
                var editedRoi=restored.RecomputeWithMeshRoi(folder,
                    restored.SelectedVertexIndices.Reverse().ToArray(),CancellationToken.None);
                Check(editedRoi.Plane.ResultId!=restored.Plane.ResultId&&
                    editedRoi.Source.Signature==restored.Source.Signature&&
                    editedRoi.RoiSignature!=restored.RoiSignature&&
                    editedRoi.ParameterSignature!=restored.ParameterSignature,
                    "ROI编辑生成新Feature及新配方指纹，旧测量不可冒充当前结果");
                Reject<InvalidOperationException>(()=>restored.RecomputeWithCylinderAxis(
                    folder,new FVOPhysicalPoint3D(0,0,1),CancellationToken.None));
                Reject<OperationCanceledException>(()=>restored.Recompute(folder,new CancellationToken(true)));
                bytes[0]=1;File.WriteAllBytes(raw,bytes);
                Reject<InvalidDataException>(()=>CT3DAnalysisPersistence.Open(engineering,address,folder,CancellationToken.None));
                Reject<InvalidDataException>(()=>restored.Recompute(folder,CancellationToken.None));
                bytes[0]=0;File.WriteAllBytes(raw,bytes);
                string missing=raw+".missing";File.Move(raw,missing);
                Reject<FileNotFoundException>(()=>CT3DAnalysisPersistence.Open(engineering,address,folder,CancellationToken.None));
                File.Move(missing,raw);
                string blob=Directory.GetFiles(Path.Combine(folder,"artifacts","ct3d"),"*.analysis").Single();
                byte[] original=File.ReadAllBytes(blob);File.WriteAllBytes(blob,new byte[]{1,2,3});
                Reject<InvalidDataException>(()=>CT3DAnalysisPersistence.Open(engineering,address,folder,CancellationToken.None));
                File.WriteAllBytes(blob,original);
                string absent=blob+".missing";File.Move(blob,absent);
                Reject<FileNotFoundException>(()=>CT3DAnalysisPersistence.Open(engineering,address,folder,CancellationToken.None));
                File.Move(absent,blob);
            }
            Reject<InvalidDataException>(()=>CT3DAssetReference.Capture(folder,"../source.raw",info,frame,1,CancellationToken.None));
            var wrong=new FVORawVolumeInfo(n,n,n+1,1,1,1,FVORawVoxelFormat.UInt16,"wrong");
            Reject<InvalidDataException>(()=>CT3DAssetReference.Capture(folder,"source.raw",wrong,frame,1,CancellationToken.None));
            var uncalibrated=CT3DAssetReference.Capture(folder,"source.raw",info,
                FVOVolumeSpatialFrame.UncalibratedAxisAligned(1,1,1),1,CancellationToken.None);
            Reject<InvalidOperationException>(()=>CT3DAnalysisCase.Analyze(folder,uncalibrated,1000,100,
                CT3DAnalysisCase.SelectTopVoxelFace,CancellationToken.None));
            Check(!Directory.GetFiles(folder,"*.tmp",SearchOption.AllDirectories).Any(),"失败不遗留临时制品");
        }
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        private static void VerifySphere(string folder)
        {
            Directory.CreateDirectory(folder);
            const int n=24;var bytes=new byte[n*n*n*4];
            for(int z=0;z<n;z++)for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                double dx=x-11.5,dy=y-11.5,dz=z-11.5;
                float scalar=dx*dx+dy*dy+dz*dz<=36?1800.25f:0;
                Array.Copy(BitConverter.GetBytes(scalar),0,bytes,4*(x+y*n+z*n*n),4);
            }
            File.WriteAllBytes(Path.Combine(folder,"sphere.raw"),bytes);
            var frame=new FVOVolumeSpatialFrame(.5,.5,.5,100,200,300,new double[]{1,0,0,0,1,0,0,0,1},true);
            var info=new FVORawVolumeInfo(n,n,n,.5f,.5f,.5f,FVORawVoxelFormat.Float32,"sphere.raw");
            var source=CT3DAssetReference.Capture(folder,"sphere.raw",info,frame,3,CancellationToken.None);
            var analysis=CT3DAnalysisCase.AnalyzeSphere(folder,source,1000,1,4000,CancellationToken.None,5000000);
            Check(analysis.Kind==CT3DAnalysisKind.SurfaceSphere&&analysis.Mesh==null&&analysis.Plane==null&&
                analysis.Surface.Count>100&&analysis.Sphere.RadiusMm>2.5&&analysis.Sphere.RadiusMm<3.25&&
                Math.Abs(analysis.Sphere.Center.X-105.75)<1e-9&&analysis.Sphere.Unit=="mm"&&
                analysis.Sphere.SourceSurfaceResultId==analysis.Surface.ResultId&&
                Object.ReferenceEquals(analysis.Sphere.SpatialFrame,source.SpatialFrame)&&
                !analysis.HasEvaluatedMeasurementUncertainty,
                "Float32原值Surface→球特征具有独立身份、毫米Frame与质量证据");
            var measurements=analysis.GetFeatureMeasurements();
            Check(measurements.Count==2&&measurements[0].Kind=="sphere.radius"&&
                measurements[1].Kind=="sphere.diameter"&&
                Math.Abs(measurements[1].ValueMm-2*measurements[0].ValueMm)<1e-12&&
                measurements.All(m=>m.FeatureResultId==analysis.Sphere.ResultId&&
                    m.SourceSignature==source.Signature&&m.SourceRevision==source.Revision&&
                    !m.HasEvaluatedUncertainty),
                "球特征测量采用毫米原值、共享Feature和完整来源签名，不冒充不确定度验收");
            string path=Path.Combine(folder,"sphere.cxproj");MRIObjectAddress address;
            using(var engineering=MEMEngineering.Create("Surface Sphere"))
            {
                var owner=engineering.AddPage("Sphere").RootContent;
                address=CT3DAnalysisPersistence.Commit(engineering,owner,engineering.Revision,folder,analysis,CancellationToken.None);
                var second=CT3DAnalysisCase.AnalyzeSphere(folder,source,1000,1,4000,CancellationToken.None,5000000);
                var secondOwner=engineering.AddPage("Sphere 2").RootContent;
                CT3DAnalysisPersistence.Commit(engineering,secondOwner,engineering.Revision,folder,second,CancellationToken.None);
                engineering.SaveAs(path);
            }
            using(var engineering=MEMEngineering.Open(path))
            {
                var allCases=CT3DAnalysisPersistence.OpenAll(engineering,folder,CancellationToken.None);
                Check(allCases.Count==2&&
                    allCases.All(c=>c.Kind==CT3DAnalysisKind.SurfaceSphere&&c.Source.Signature==source.Signature)&&
                    allCases[0].Sphere.ResultId!=allCases[1].Sphere.ResultId,
                    "同一MEM工程组合多个独立Feature且完整加载");
                Reject<OperationCanceledException>(()=>CT3DAnalysisPersistence.OpenAll(
                    engineering,folder,new CancellationToken(true)));
                var restored=CT3DAnalysisPersistence.Open(engineering,address,folder,CancellationToken.None);
                Check(restored.Kind==CT3DAnalysisKind.SurfaceSphere&&restored.Surface.ResultId==analysis.Surface.ResultId&&
                    restored.Sphere.ResultId==analysis.Sphere.ResultId&&restored.Surface.SampleStep==1&&
                    restored.Surface.MaximumPoints==4000&&restored.Sphere.MaximumConditionNumber==5000000&&
                    restored.Surface.Points.SequenceEqual(analysis.Surface.Points)&&
                    restored.Sphere.SourceSurfaceResultId==restored.Surface.ResultId&&
                    Math.Abs(restored.Sphere.RmsResidualMm-analysis.Sphere.RmsResidualMm)<1e-10,
                    "Surface/球重开保留采样参数、点集、结果依赖、半径与残差");
                Check(restored.ParameterSignature==analysis.ParameterSignature,
                    "球Feature工程重开保留算法版本和拟合参数");
                var reloadedMeasurements=restored.GetFeatureMeasurements();
                Check(reloadedMeasurements.Count==2&&
                    reloadedMeasurements[1].FeatureResultId==restored.Sphere.ResultId&&
                    Math.Abs(reloadedMeasurements[1].ValueMm-measurements[1].ValueMm)<1e-10&&
                    reloadedMeasurements[1].SourceSignature==source.Signature,
                    "MEM保存重开后从原球Feature恢复测量量值及身份");
                var recomputed=restored.Recompute(folder,CancellationToken.None);
                Check(recomputed.Surface.ResultId!=restored.Surface.ResultId&&recomputed.Sphere.ResultId!=restored.Sphere.ResultId&&
                    recomputed.Source.Signature==restored.Source.Signature&&
                    Math.Abs(recomputed.Sphere.RadiusMm-restored.Sphere.RadiusMm)<1e-10,
                    "Surface/球复算形成新身份且原值/毫米数值不漂移");
                Reject<OperationCanceledException>(()=>restored.Recompute(folder,new CancellationToken(true)));
            }
            Reject<InvalidOperationException>(()=>CT3DAnalysisCase.AnalyzeSphere(folder,source,1000,1,6,CancellationToken.None));
            Reject<ArgumentOutOfRangeException>(()=>CT3DAnalysisCase.AnalyzeSphere(folder,source,1000,0,4000,CancellationToken.None));
            Console.WriteLine("PASS CT3D Float32 surface/sphere artifact identities, physical fit quality and recompute");
        }

        private static void VerifyCylinder(string folder)
        {
            Directory.CreateDirectory(folder);
            const int n=24;
            byte[] raw=new byte[n*n*n*2];
            for(int z=5;z<=18;z++)for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                double dx=x-11.5,dy=y-11.5;
                if(dx*dx+dy*dy>25)continue;
                int i=2*(x+y*n+z*n*n);raw[i]=0xd0;raw[i+1]=0x07;
            }
            File.WriteAllBytes(Path.Combine(folder,"cylinder.raw"),raw);
            var frame=new FVOVolumeSpatialFrame(.5,.5,.5,100,200,300,
                new double[]{1,0,0,0,1,0,0,0,1},true);
            var info=new FVORawVolumeInfo(n,n,n,.5f,.5f,.5f,FVORawVoxelFormat.UInt16,"cylinder.raw");
            var source=CT3DAssetReference.Capture(folder,"cylinder.raw",info,frame,1,CancellationToken.None);
            using(var volume=source.LoadVolume(folder,CancellationToken.None))
            using(var lease=volume.AcquireOriginalScalarLease())
            {
                var samples=CT3DSurfaceSphereAnalysis.SampleBoundary(lease,source.SourceRef,
                    source.Revision,1000,1,10000,CancellationToken.None);
                var axis=new FVOPhysicalPoint3D(0,0,1);
                var result=CT3DSurfaceCylinderAnalysis.FitCylinder(samples,axis);
                Check(result.RadiusMm>2&&result.RadiusMm<3&&
                    result.DiameterMm==2*result.RadiusMm&&
                    result.SourceSurfaceResultId==samples.ResultId&&
                    result.SourceRevision==source.Revision&&
                    Object.ReferenceEquals(result.SpatialFrame,source.SpatialFrame)&&
                    !result.HasEvaluatedUncertainty,
                    "UInt16离散圆柱侧壁拟合保留来源、毫米单位与质量证据");
                var caseValue=CT3DAnalysisCase.AnalyzeCylinder(folder,source,1000,1,10000,axis,CancellationToken.None);
                Check(caseValue.GetFeatureMeasurements().Count==2,"圆柱Feature提供半径和直径测量");
                string projectFile=Path.Combine(folder,"cylinder.cxproj");MRIObjectAddress address;
                using(var engineering=MEMEngineering.Create("Cylinder"))
                {
                    var owner=engineering.AddPage("Cylinder").RootContent;
                    address=CT3DAnalysisPersistence.Commit(engineering,owner,engineering.Revision,folder,caseValue,CancellationToken.None);
                    engineering.SaveAs(projectFile);
                }
                using(var engineering=MEMEngineering.Open(projectFile))
                {
                    var restored=CT3DAnalysisPersistence.Open(engineering,address,folder,CancellationToken.None);
                    Check(restored.Kind==CT3DAnalysisKind.SurfaceCylinder&&
                        restored.ParameterSignature==caseValue.ParameterSignature&&
                        restored.Cylinder.ResultId==caseValue.Cylinder.ResultId&&
                        restored.Surface.ResultId==caseValue.Surface.ResultId&&
                        restored.Cylinder.SourceSurfaceResultId==restored.Surface.ResultId&&
                        Math.Abs(restored.Cylinder.RadiusMm-caseValue.Cylinder.RadiusMm)<1e-10&&
                        restored.GetFeatureMeasurements().All(m=>m.SourceSignature==source.Signature),
                        "MEM重开圆柱保留Feature、Surface、来源签名及毫米量值");
                    var changedAxis=restored.RecomputeWithCylinderAxis(folder,
                        new FVOPhysicalPoint3D(0,0,-1),CancellationToken.None);
                    Check(changedAxis.Cylinder.ResultId!=restored.Cylinder.ResultId&&
                        changedAxis.ParameterSignature!=restored.ParameterSignature&&
                        Math.Abs(changedAxis.Cylinder.RadiusMm-restored.Cylinder.RadiusMm)<1e-9,
                        "修改圆柱轴向约束形成新配方和独立测量身份");
                    var recomputed=restored.Recompute(folder,CancellationToken.None);
                    Check(recomputed.Cylinder.ResultId!=restored.Cylinder.ResultId&&
                        Math.Abs(recomputed.Cylinder.DiameterMm-restored.Cylinder.DiameterMm)<1e-10,
                        "固定轴向圆柱重新计算不复用旧结果身份");
                }
                Reject<ArgumentOutOfRangeException>(()=>CT3DSurfaceCylinderAnalysis.FitCylinder(
                    samples,new FVOPhysicalPoint3D(0,0,0)));
                Reject<OperationCanceledException>(()=>CT3DSurfaceCylinderAnalysis.FitCylinder(
                    samples,axis,10000000,new CancellationToken(true)));
            }
            Console.WriteLine("PASS CT3D constrained-axis UInt16 cylinder fit and provenance");
        }

        private static void VerifySchema1Compatibility(string folder)
        {
            var root=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while(root!=null&&!File.Exists(Path.Combine(root.FullName,"CxVisual.Unified.sln")))root=root.Parent;
            if(root==null)throw new DirectoryNotFoundException("Cannot locate schema1 fixture repository");
            string fixture=Path.Combine(root.FullName,"AppData","Cases","CT3D","V04Schema1");
            // 旧DLL生成的二进制fixture是外部验收资产；未入库时不能伪造或误报兼容通过。
            string rawFixture=Path.Combine(fixture,"source.raw");
            string projectFixture=Path.Combine(fixture,"schema1.cxproj");
            if(!File.Exists(rawFixture)||!File.Exists(projectFixture)||
                !Directory.Exists(Path.Combine(fixture,"artifacts","ct3d")))
            {
                Console.WriteLine("SKIP CT3D fixed schema1 compatibility: legacy fixture absent; external binary acceptance pending");
                return;
            }
            string migrated=Path.Combine(folder,"schema1-migrated");Directory.CreateDirectory(migrated);
            File.Copy(Path.Combine(fixture,"source.raw"),Path.Combine(migrated,"source.raw"));
            string output=Path.Combine(migrated,"schema2.cxproj");MRIObjectAddress address;Guid meshId,planeId;
            using(var engineering=MEMEngineering.Open(Path.Combine(fixture,"schema1.cxproj")))
            {
                var owner=engineering.Project.DescendantsAndSelf().OfType<MEMContent>()
                    .Single(c=>c.Aspects.Any(a=>a.ProviderId==CT3DAnalysisPersistence.ProviderId));
                address=engineering.GetAddress(owner);
                var legacy=CT3DAnalysisPersistence.Open(engineering,address,fixture,CancellationToken.None);
                meshId=legacy.Mesh.ResultId;planeId=legacy.Plane.ResultId;
                Check(legacy.Kind==CT3DAnalysisKind.MeshPlane&&legacy.Mesh.Mesh.TriangleCount==48,
                    "由旧Schema1正式DLL生成的固定工程/制品仍可读取");
                CT3DAnalysisPersistence.Commit(engineering,owner,engineering.Revision,migrated,legacy,CancellationToken.None);
                engineering.SaveAs(output);
            }
            using(var engineering=MEMEngineering.Open(output))
            {
                var upgraded=CT3DAnalysisPersistence.Open(engineering,address,migrated,CancellationToken.None);
                Check(upgraded.Mesh.ResultId==meshId&&upgraded.Plane.ResultId==planeId&&upgraded.Plane.SampleCount==9,
                    "旧Schema1另存为新制品后保留结果身份、ROI及毫米量值");
            }
            Console.WriteLine("PASS CT3D fixed schema1 fixture read and schema2 resave compatibility");
        }
        private static void Reject<T>(Action operation) where T:Exception
        {
            try{operation();}catch(T){return;}
            throw new InvalidOperationException("必须拒绝："+typeof(T).Name);
        }
    }
}
