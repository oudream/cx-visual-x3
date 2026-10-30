using System;
using System.IO;
using System.Linq;
using System.Threading;
using CxVisual.Foundation.Volume;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Visual;
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
            FVOPhysicalSliceRectangle sliceRoi;
            using(var mapped=source.LoadVolume(folder,CancellationToken.None))
            {
                Reject<IOException>(()=>File.WriteAllBytes(raw,bytes));
                using(var lease=mapped.AcquireOriginalScalarLease())
                    Check(lease.ReadScalar(5,5,5)==1700,"校验读锁与只读映射共存且禁止并发改写");
                sliceRoi=FVOSliceImageBuilder.RectangleToPhysical(mapped,6,6,6,FVOSlicePlane.XY,5,5,6,6);
            }
            var analysis=CT3DAnalysisCase.Analyze(folder,source,1000,100,
                mesh=>CT3DMeshPlaneAnalysis.SelectSlicePrismVertices(mesh,sliceRoi,.49,.51),CancellationToken.None,0.01,1000);
            Reject<ArgumentOutOfRangeException>(()=>CT3DAnalysisCase.Analyze(folder,source,
                1000,100,mesh=>new int[30001],CancellationToken.None));
            Check(analysis.Mesh.Mesh.VertexCount==26 && analysis.Plane.SampleCount==9 &&
                Math.Abs(analysis.Plane.PointMm.Z-46.5)<1e-9 && analysis.Plane.RmsResidualMm<1e-9 &&
                !analysis.HasEvaluatedMeasurementUncertainty,"真实UInt16 Mesh到局部平面质量合同");
            var planeQuality=analysis.GetQualityEvidence();
            Check(planeQuality.Count==4&&
                planeQuality.Any(m=>m.Kind=="plane.planarity-ratio"&&m.Unit=="ratio")&&
                planeQuality.All(m=>m.FeatureResultId==analysis.Plane.ResultId&&
                    m.ParameterSignature==analysis.ParameterSignature&&
                    !m.IsIndustrialAccuracyCertified),
                "平面质量证据应区分毫米残差与无量纲退化指标");
            var planeMeasures=analysis.GetFeatureMeasurements();
            Check(planeMeasures.Count==2&&
                planeMeasures[0].Kind=="plane.rms-residual"&&
                planeMeasures[1].Kind=="plane.max-absolute-residual"&&
                Math.Abs(planeMeasures[0].ValueMm-analysis.Plane.RmsResidualMm)<1e-12&&
                Math.Abs(planeMeasures[1].ValueMm-analysis.Plane.MaxAbsoluteResidualMm)<1e-12&&
                planeMeasures.All(m=>m.FeatureResultId==analysis.Plane.ResultId&&
                    m.SourceSignature==source.Signature&&
                    m.ParameterSignature==analysis.ParameterSignature&&
                    m.RoiSignature==analysis.RoiSignature&&
                    m.AlgorithmVersion==analysis.AlgorithmVersion&&
                    Object.ReferenceEquals(m.SpatialFrame,source.SpatialFrame)&&
                    !m.HasEvaluatedUncertainty),
                "平面RMS与最大偏差必须属于质量证据而非虚构Datum尺寸");
            var copy=CT3DAssetReference.Import(folder,raw,info,frame,7,CancellationToken.None,source.Id);
            Check(copy.FrameSignature==source.FrameSignature&&copy.SpatialFrameRef==source.SpatialFrameRef,
                "同资产同物理Frame在内容寻址RAW导入后保持稳定空间引用");
            Check(copy.ContentHash==source.ContentHash && File.ReadAllBytes(raw).SequenceEqual(bytes),
                "内容寻址导入不改原始文件");
            string project=Path.Combine(folder,"analysis.cxproj");MRIObjectAddress address;
            string originalArtifactPath;
            int revision;
            using(var engineering=MEMEngineering.Create("三维分析案例"))
            {
                var page=engineering.AddPage("三维");var owner=page.RootContent;
                int before=engineering.Revision;
                address=CT3DAnalysisPersistence.Commit(engineering,owner,before,folder,analysis,CancellationToken.None);
                originalArtifactPath=Directory.GetFiles(Path.Combine(folder,"artifacts","ct3d"),"*.analysis").Single();
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
                Check(restored.Source.FrameSignature==source.FrameSignature&&
                    restored.Source.SpatialFrameRef==source.SpatialFrameRef,
                    "MEM重建Frame对象后仍保留资产级稳定物理空间引用");
                Check(restored.Source.Id==source.Id && restored.Source.Signature==source.Signature &&
                    restored.Source.Revision==7 && restored.Mesh.ResultId==analysis.Mesh.ResultId &&
                    restored.Plane.ResultId==analysis.Plane.ResultId &&
                    restored.Plane.SourceMeshResultId==restored.Mesh.ResultId &&
                    restored.Mesh.Mesh.TriangleCount==48 && restored.SelectedVertexIndices.SequenceEqual(analysis.SelectedVertexIndices),
                    "关闭重开保留来源/空间签名/结果身份/局部ROI与Mesh");
                Check(restored.ParameterSignature==analysis.ParameterSignature&&
                    restored.ParameterSignature.Length==64,
                    "Mesh/Plane工程重开保留稳定算法参数签名");
                var originalVertices=analysis.GetSelectedVertexReferences();
                var reopenedVertices=restored.GetSelectedVertexReferences();
                Check(originalVertices.Count==9&&reopenedVertices.Count==originalVertices.Count&&
                    originalVertices.Select(v=>v.StableId).SequenceEqual(
                        reopenedVertices.Select(v=>v.StableId))&&
                    reopenedVertices.All(v=>v.SourceSignature==source.Signature&&
                        v.RoiSignature==restored.RoiSignature&&v.Unit=="mm"),
                    "ROI每个物理顶点在MEM重开后仍具有确定性稳定引用");
                Check(restored.RoiSignature==analysis.RoiSignature&&
                    restored.RoiSignature!=null&&restored.RoiSignature.Length==64,
                    "原RAW和选中毫米ROI在MEM重开后保持稳定身份");
                Check(restored.Source.SpatialFrame!=frame &&
                    Math.Abs(restored.Plane.SignedDistanceMm(new FVOPhysicalPoint3D(25,35,47.5),restored.Source.SpatialFrame)-1)<1e-9,
                    "重建Frame对象后仍可在恢复的同一Frame内测量");
                Check(Math.Abs(restored.Plane.SignedDistanceFromAssetMm(
                    new FVOPhysicalPoint3D(25,35,47.5),source)-1)<1e-9,
                    "跨MEM重开Frame实例不同，但来源身份和物理标定一致允许毫米测距");
                var shiftedFrame=new FVOVolumeSpatialFrame(1,1,1,21,30,40,
                    new double[]{1,0,0,0,1,0,0,0,1},true);
                var shiftedSource=CT3DAssetReference.Capture(folder,"source.raw",info,
                    shiftedFrame,7,CancellationToken.None,source.Id);
                Reject<InvalidOperationException>(()=>restored.Plane.SignedDistanceFromAssetMm(
                    new FVOPhysicalPoint3D(25,35,47.5),shiftedSource));
                var unrelatedSource=CT3DAssetReference.Capture(folder,"source.raw",info,
                    frame,7,CancellationToken.None);
                Reject<InvalidOperationException>(()=>restored.Plane.SignedDistanceFromAssetMm(
                    new FVOPhysicalPoint3D(25,35,47.5),unrelatedSource));
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
                var continued=restored.RecomputeWithSliceRectangle(folder,sliceRoi,.49,.51,
                    CancellationToken.None);
                Check(continued.Plane.ResultId!=restored.Plane.ResultId&&
                    continued.Source.Signature==restored.Source.Signature&&
                    continued.RoiSignature==restored.RoiSignature&&
                    continued.ParameterSignature==restored.ParameterSignature&&
                    continued.Plane.SampleCount==9,
                    "跨MEM工程Frame对象重开后仍能从原物理切片ROI继续拟合并保留原值参数");
                var wrongFrame=new FVOVolumeSpatialFrame(1,1,1,21,30,40,
                    new double[]{1,0,0,0,1,0,0,0,1},true);
                using(var wrongVolume=source.LoadVolume(folder,CancellationToken.None))
                {
                    wrongVolume.SetSpatialFrame(wrongFrame);
                    var wrongRoi=FVOSliceImageBuilder.RectangleToPhysical(wrongVolume,6,6,6,
                        FVOSlicePlane.XY,5,5,6,6);
                    Reject<InvalidOperationException>(()=>restored.RecomputeWithSliceRectangle(
                        folder,wrongRoi,.49,.51,CancellationToken.None));
                }
                Reject<OperationCanceledException>(()=>restored.RecomputeWithSliceRectangle(
                    folder,sliceRoi,.49,.51,new CancellationToken(true)));
                var owner=engineering.Resolve(address) as MEMContent;
                Check(owner!=null,"重开MEM地址必须能解析当前Content Owner");
                var visualLayout=new MVI3DSceneState();
                Guid visualMesh=visualLayout.Add(MVI3DSceneKind.Mesh,
                    "ct3d:mesh:"+restored.Mesh.ResultId.ToString("N"),
                    restored.Source.Revision,restored.Source.Signature,.4);
                visualLayout.SetOrganization(visualMesh,"inspection","measurements");
                visualLayout.Select(visualMesh);
                var visualAspect=MVI3DSceneLayoutPersistence.CreateAspect(visualLayout,
                    CancellationToken.None);
                int combinedRevision=engineering.Revision;
                CT3DAnalysisPersistence.Commit(engineering,owner,combinedRevision,folder,
                    restored,visualAspect,CancellationToken.None);
                Check(engineering.Revision==combinedRevision+1,
                    "专业Data和Scene Visual只能增加一次MEM工程Revision");
                engineering.Undo().RequireValue();
                Reject<InvalidDataException>(()=>MVI3DSceneLayoutPersistence.Open(
                    engineering,address,new MVI3DSceneState(),item=>true,CancellationToken.None));
                engineering.Redo().RequireValue();
                var loadedLayout=new MVI3DSceneState();
                MVI3DSceneLayoutPersistence.Open(engineering,address,loadedLayout,
                    item=>item.SourceRef=="ct3d:mesh:"+restored.Mesh.ResultId.ToString("N"),
                    CancellationToken.None);
                Check(loadedLayout.SelectedId.HasValue&&
                    loadedLayout.SnapshotLayer("inspection").Count==1&&
                    Math.Abs(loadedLayout.SnapshotLayer("inspection")[0].Opacity-.4)<1e-12,
                    "同一MEM事务Undo/Redo完整恢复专业结果及Scene可视引用");
                int editRevision=engineering.Revision;
                Reject<InvalidOperationException>(()=>CT3DAnalysisPersistence.RecomputeSliceRoiAndCommit(
                    engineering,owner,editRevision,folder,sliceRoi,.49,.51,CancellationToken.None));
                Check(engineering.Revision==editRevision,
                    "已有Scene Visual却未提供结果重绑定器时不得提交孤立Data");
                CT3DAnalysisPersistence.RecomputeSliceRoiAndCommit(engineering,owner,
                    editRevision,folder,sliceRoi,.49,.51,
                    (previous,next)=>MVI3DSceneLayoutPersistence.CreateReboundAspect(
                        loadedLayout,item=>
                        {
                            if(item.Kind!=MVI3DSceneKind.Mesh||
                                item.SourceRef!="ct3d:mesh:"+previous.Mesh.ResultId.ToString("N"))
                                throw new InvalidDataException("旧Scene对象来源不属于本次专业Case。");
                            return "ct3d:mesh:"+next.Mesh.ResultId.ToString("N");
                        },CancellationToken.None),CancellationToken.None);
                var persistedEdited=CT3DAnalysisPersistence.Open(engineering,address,
                    folder,CancellationToken.None);
                var revisedLayout=new MVI3DSceneState();
                MVI3DSceneLayoutPersistence.Open(engineering,address,revisedLayout,
                    item=>item.SourceRef=="ct3d:mesh:"+persistedEdited.Mesh.ResultId.ToString("N"),
                    CancellationToken.None);
                Check(revisedLayout.SelectedId==loadedLayout.SelectedId&&
                    revisedLayout.SnapshotLayer("inspection").Count==1&&
                    revisedLayout.SnapshotLayer("inspection")[0].Id==visualMesh&&
                    Math.Abs(revisedLayout.SnapshotLayer("inspection")[0].Opacity-.4)<1e-12,
                    "ROI重算原子更新Visual来源并保持Scene实例Id、选择、分组及透明度");
                Check(engineering.Revision==editRevision+1&&
                    persistedEdited.Plane.ResultId!=restored.Plane.ResultId&&
                    persistedEdited.RoiSignature==restored.RoiSignature&&
                    persistedEdited.Source.Signature==restored.Source.Signature,
                    "二维ROI编辑经过正式MEM事务形成新专业结果，旧来源/ROI身份稳定");
                Reject<InvalidOperationException>(()=>CT3DAnalysisPersistence.RecomputeSliceRoiAndCommit(
                    engineering,owner,editRevision,folder,sliceRoi,.49,.51,CancellationToken.None));
                int revised=engineering.Revision;
                Reject<OperationCanceledException>(()=>CT3DAnalysisPersistence.RecomputeSliceRoiAndCommit(
                    engineering,owner,revised,folder,sliceRoi,.49,.51,new CancellationToken(true)));
                Check(engineering.Revision==revised,
                    "ROI修改的版本冲突及取消不得改变MEM工程");
                engineering.Undo().RequireValue();
                var oldLayoutAfterUndo=new MVI3DSceneState();
                MVI3DSceneLayoutPersistence.Open(engineering,address,oldLayoutAfterUndo,
                    item=>item.SourceRef=="ct3d:mesh:"+restored.Mesh.ResultId.ToString("N"),
                    CancellationToken.None);
                Check(oldLayoutAfterUndo.SelectedId==visualMesh,
                    "ROI Undo必须同时恢复旧Scene来源和选中实例");
                Check(CT3DAnalysisPersistence.Open(engineering,address,folder,CancellationToken.None)
                    .Plane.ResultId==restored.Plane.ResultId,
                    "ROI编辑Undo应完整恢复旧Feature身份");
                engineering.Redo().RequireValue();
                var newLayoutAfterRedo=new MVI3DSceneState();
                MVI3DSceneLayoutPersistence.Open(engineering,address,newLayoutAfterRedo,
                    item=>item.SourceRef=="ct3d:mesh:"+persistedEdited.Mesh.ResultId.ToString("N"),
                    CancellationToken.None);
                Check(newLayoutAfterRedo.SelectedId==visualMesh,
                    "ROI Redo必须同时恢复新Scene来源与原实例选择");
                Check(CT3DAnalysisPersistence.Open(engineering,address,folder,CancellationToken.None)
                    .Plane.ResultId==persistedEdited.Plane.ResultId,
                    "ROI编辑Redo应完整恢复新Feature身份");
                engineering.Undo().RequireValue();
                var editedRoi=restored.RecomputeWithMeshRoi(folder,
                    restored.SelectedVertexIndices.Reverse().ToArray(),CancellationToken.None);
                Check(editedRoi.Plane.ResultId!=restored.Plane.ResultId&&
                    editedRoi.Source.Signature==restored.Source.Signature&&
                    editedRoi.RoiSignature!=restored.RoiSignature&&
                    !editedRoi.GetSelectedVertexReferences().First().StableId.Equals(
                        restored.GetSelectedVertexReferences().First().StableId)&&
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
                string blob=originalArtifactPath;
                byte[] original=File.ReadAllBytes(blob);File.WriteAllBytes(blob,new byte[]{1,2,3});
                Reject<InvalidDataException>(()=>CT3DAnalysisPersistence.Open(engineering,address,folder,CancellationToken.None));
                File.WriteAllBytes(blob,original);
                string absent=blob+".missing";File.Move(blob,absent);
                Reject<FileNotFoundException>(()=>CT3DAnalysisPersistence.Open(engineering,address,folder,CancellationToken.None));
                File.Move(absent,blob);
                engineering.Close(true); // 故意Undo后仍为Dirty，必须显式丢弃测试会话。
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
            var sphereQuality=analysis.GetQualityEvidence();
            Check(sphereQuality.Count==3&&
                sphereQuality.Any(m=>m.Kind=="surface.fit-condition"&&m.Unit=="ratio")&&
                sphereQuality.All(m=>m.FeatureResultId==analysis.Sphere.ResultId&&
                    m.SourceSignature==source.Signature),
                "球体拟合残差和数值条件数属于可追溯质量证据");
            var measurements=analysis.GetFeatureMeasurements();
            Check(measurements.Count==2&&measurements[0].Kind=="sphere.radius"&&
                measurements[1].Kind=="sphere.diameter"&&
                Math.Abs(measurements[1].ValueMm-2*measurements[0].ValueMm)<1e-12&&
                measurements.All(m=>m.FeatureResultId==analysis.Sphere.ResultId&&
                    m.SourceSignature==source.Signature&&m.SourceRevision==source.Revision&&
                    m.ParameterSignature==analysis.ParameterSignature&&
                    m.AlgorithmVersion==analysis.AlgorithmVersion&&m.RoiSignature==null&&
                    Object.ReferenceEquals(m.SpatialFrame,source.SpatialFrame)&&
                    !m.HasEvaluatedUncertainty),
                "球特征测量采用毫米原值、共享Feature和完整来源签名，不冒充不确定度验收");
            string path=Path.Combine(folder,"sphere.cxproj");MRIObjectAddress address;
            using(var engineering=MEMEngineering.Create("Surface Sphere"))
            {
                var owner=engineering.AddPage("Sphere").RootContent;
                var second=CT3DAnalysisCase.AnalyzeSphere(folder,source,1000,1,4000,CancellationToken.None,5000000);
                var secondOwner=engineering.AddPage("Sphere 2").RootContent;
                int beforeBatch=engineering.Revision;
                var committed=CT3DAnalysisPersistence.CommitAll(engineering,
                    new[]{owner,secondOwner},beforeBatch,folder,
                    new[]{analysis,second},CancellationToken.None);
                address=committed[0];
                Check(committed.Count==2&&engineering.Revision==beforeBatch+1,
                    "同一MEM事务提交两个独立Feature仅增加一次工程Revision");
                Reject<InvalidOperationException>(()=>CT3DAnalysisPersistence.CommitAll(engineering,
                    new[]{owner,secondOwner},beforeBatch,folder,
                    new[]{analysis,second},CancellationToken.None));
                Reject<ArgumentException>(()=>CT3DAnalysisPersistence.CommitAll(engineering,
                    new[]{owner,owner},engineering.Revision,folder,
                    new[]{analysis,second},CancellationToken.None));
                int afterBatch=engineering.Revision;
                Reject<OperationCanceledException>(()=>CT3DAnalysisPersistence.CommitAll(engineering,
                    new[]{owner,secondOwner},afterBatch,folder,
                    new[]{analysis,second},new CancellationToken(true)));
                Check(engineering.Revision==afterBatch,
                    "批量提交在取消时不能产生部分MEM结果或工程修订");

                engineering.Undo().RequireValue();
                Reject<InvalidDataException>(()=>CT3DAnalysisPersistence.Open(engineering,address,folder,CancellationToken.None));
                engineering.Redo().RequireValue();
                Check(CT3DAnalysisPersistence.OpenAll(engineering,folder,CancellationToken.None).Count==2,
                    "MEM Undo/Redo必须同时移除及恢复同一批结果");
                engineering.SaveAs(path);
            }
            using(var engineering=MEMEngineering.Open(path))
            {
                var allCases=CT3DAnalysisPersistence.OpenAll(engineering,folder,CancellationToken.None);
                Check(allCases.Count==2&&
                    allCases.All(c=>c.Kind==CT3DAnalysisKind.SurfaceSphere&&c.Source.Signature==source.Signature)&&
                    allCases[0].Sphere.ResultId!=allCases[1].Sphere.ResultId,
                    "同一MEM工程组合多个独立Feature且完整加载");
                var batchOwners=engineering.Project.DescendantsAndSelf().OfType<MEMContent>()
                    .Where(c=>c.Aspects.Any(a=>a.ProviderId==CT3DAnalysisPersistence.ProviderId)).ToArray();
                int beforeRevision=engineering.Revision;
                var before=CT3DAnalysisPersistence.OpenAll(engineering,folder,CancellationToken.None);
                CT3DAnalysisPersistence.RecomputeAllAndCommit(engineering,batchOwners,
                    beforeRevision,folder,CancellationToken.None);
                var after=CT3DAnalysisPersistence.OpenAll(engineering,folder,CancellationToken.None);
                Check(engineering.Revision==beforeRevision+1&&after.Count==before.Count&&
                    after.Zip(before,(next,prev)=>
                        next.Sphere.ResultId!=prev.Sphere.ResultId&&
                        next.ParameterSignature==prev.ParameterSignature).All(v=>v),
                    "两个Case通过单MEM修订原值复算且更新Feature身份");
                Reject<InvalidOperationException>(()=>CT3DAnalysisPersistence.RecomputeAllAndCommit(
                    engineering,batchOwners,beforeRevision,folder,CancellationToken.None));
                engineering.Undo().RequireValue();
                Check(CT3DAnalysisPersistence.OpenAll(engineering,folder,CancellationToken.None)
                    .Select(c=>c.Sphere.ResultId).SequenceEqual(before.Select(c=>c.Sphere.ResultId)),
                    "批量复算Undo整体恢复旧专业结果");
                engineering.Redo().RequireValue();
                Check(CT3DAnalysisPersistence.OpenAll(engineering,folder,CancellationToken.None)
                    .Select(c=>c.Sphere.ResultId).SequenceEqual(after.Select(c=>c.Sphere.ResultId)),
                    "批量复算Redo整体恢复新专业结果");
                engineering.Undo().RequireValue();
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
                var visualOwners=engineering.Project.DescendantsAndSelf().OfType<MEMContent>()
                    .Where(c=>c.Aspects.Any(a=>a.ProviderId==CT3DAnalysisPersistence.ProviderId)).ToArray();
                var originalCases=CT3DAnalysisPersistence.OpenAll(engineering,folder,CancellationToken.None);
                var priorLayouts=new System.Collections.Generic.Dictionary<Guid,MVI3DSceneState>();
                var visualAspects=new System.Collections.Generic.List<MPMAspect>();
                foreach(var value in originalCases)
                {
                    var layout=new MVI3DSceneState();
                    Guid surfaceItem=layout.Add(MVI3DSceneKind.Surface,
                        "ct3d:surface:"+value.Surface.ResultId.ToString("N"),
                        value.Source.Revision,value.Source.Signature,.55);
                    Guid measurementItem=layout.Add(MVI3DSceneKind.Measurement,
                        "ct3d:sphere:"+value.Sphere.ResultId.ToString("N"),
                        value.Source.Revision,value.Source.Signature,.75);
                    layout.SetOrganization(measurementItem,"measurement","batch");
                    layout.Select(measurementItem);
                    priorLayouts.Add(value.Sphere.ResultId,layout);
                    visualAspects.Add(MVI3DSceneLayoutPersistence.CreateAspect(layout,CancellationToken.None));
                }
                int initialBatchRevision=engineering.Revision;
                CT3DAnalysisPersistence.CommitAll(engineering,visualOwners,initialBatchRevision,
                    folder,originalCases,visualAspects,CancellationToken.None);
                Check(engineering.Revision==initialBatchRevision+1,
                    "批量专业结果与两个Scene Visual应在一次MEM事务中同步保存");
                int beforeVisualRecompute=engineering.Revision;
                Reject<InvalidOperationException>(()=>CT3DAnalysisPersistence.RecomputeAllAndCommit(
                    engineering,visualOwners,beforeVisualRecompute,folder,CancellationToken.None));
                Check(engineering.Revision==beforeVisualRecompute,
                    "批量复算不提供Scene重绑定器必须拒绝写入旧可视来源");
                CT3DAnalysisPersistence.RecomputeAllAndCommit(engineering,visualOwners,
                    beforeVisualRecompute,folder,(previous,next)=>
                        MVI3DSceneLayoutPersistence.CreateReboundAspect(
                            priorLayouts[previous.Sphere.ResultId],item=>
                            {
                                if(item.Kind==MVI3DSceneKind.Surface&&
                                    item.SourceRef=="ct3d:surface:"+previous.Surface.ResultId.ToString("N"))
                                    return "ct3d:surface:"+next.Surface.ResultId.ToString("N");
                                if(item.Kind==MVI3DSceneKind.Measurement&&
                                    item.SourceRef=="ct3d:sphere:"+previous.Sphere.ResultId.ToString("N"))
                                    return "ct3d:sphere:"+next.Sphere.ResultId.ToString("N");
                                throw new InvalidDataException("批量Scene旧专业来源不匹配。");
                            },CancellationToken.None),CancellationToken.None);
                var updatedCases=CT3DAnalysisPersistence.OpenAll(engineering,folder,CancellationToken.None);
                Check(updatedCases.Count==2&&engineering.Revision==beforeVisualRecompute+1&&
                    updatedCases.All(c=>!priorLayouts.ContainsKey(c.Sphere.ResultId)),
                    "两份Sphere/Surface经单事务更新专业结果身份");
                for(int idx=0;idx<visualOwners.Length;idx++)
                {
                    var freshScene=new MVI3DSceneState();
                    CT3DAnalysisCase current=updatedCases[idx];
                    MVI3DSceneLayoutPersistence.Open(engineering,
                        engineering.GetAddress(visualOwners[idx]),freshScene,
                        item=>item.Kind==MVI3DSceneKind.Surface
                            ?item.SourceRef=="ct3d:surface:"+current.Surface.ResultId.ToString("N")
                            :item.Kind==MVI3DSceneKind.Measurement&&
                                item.SourceRef=="ct3d:sphere:"+current.Sphere.ResultId.ToString("N"),
                        CancellationToken.None);
                    Check(freshScene.SnapshotLayer("measurement").Count==1&&
                        freshScene.SelectedId.HasValue&&
                        Math.Abs(freshScene.SnapshotLayer("measurement")[0].Opacity-.75)<1e-12,
                        "批量复算后Scene仍保留测量选择、组和透明度且专业ResultId已更新");
                }
                engineering.Undo().RequireValue();
                Check(CT3DAnalysisPersistence.OpenAll(engineering,folder,CancellationToken.None)
                    .Select(c=>c.Sphere.ResultId).SequenceEqual(originalCases.Select(c=>c.Sphere.ResultId)),
                    "批量Data/Visual事务Undo应整体恢复两份旧Sphere");
                engineering.Redo().RequireValue();
                Check(CT3DAnalysisPersistence.OpenAll(engineering,folder,CancellationToken.None)
                    .Select(c=>c.Sphere.ResultId).SequenceEqual(updatedCases.Select(c=>c.Sphere.ResultId)),
                    "批量Data/Visual事务Redo应整体恢复两份新Sphere");
                engineering.Close(true); // 测试撤销批量复算后显式丢弃未保存会话，不更改MEM保存合同。
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
                var sidewall=Enumerable.Range(0,samples.Count)
                    .Where(i=>samples.Points[i].Z>302.5&&samples.Points[i].Z<309).ToArray();
                Check(sidewall.Length>=6&&sidewall.Length<samples.Count,
                    "侧壁点ROI必须实际排除圆柱两端端盖");
                var sidewallFit=CT3DSurfaceCylinderAnalysis.FitCylinder(samples,axis,sidewall);
                Check(sidewallFit.HasExplicitSidewallRoi&&
                    sidewallFit.SelectedPointIndices.SequenceEqual(sidewall)&&
                    sidewallFit.RadiusMm>2&&sidewallFit.RadiusMm<3,
                    "显式侧壁ROI保留选择身份并输出毫米半径");
                Reject<ArgumentException>(()=>CT3DSurfaceCylinderAnalysis.FitCylinder(samples,axis,
                    new[]{sidewall[0],sidewall[0],sidewall[0],sidewall[0],sidewall[0],sidewall[0]}));
                var caseValue=CT3DAnalysisCase.AnalyzeCylinder(folder,source,1000,1,10000,axis,
                    CancellationToken.None,10000000,sidewall);
                Check(caseValue.GetFeatureMeasurements().Count==2&&
                    caseValue.GetQualityEvidence().Count==3&&
                    caseValue.GetQualityEvidence().All(m=>
                        m.FeatureResultId==caseValue.Cylinder.ResultId&&
                        m.ParameterSignature==caseValue.ParameterSignature&&
                        !m.IsIndustrialAccuracyCertified),
                    "圆柱Feature/Measurement和质量证据归属相同专业结果");
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
                        restored.RoiSignature==caseValue.RoiSignature&&
                        restored.RoiSignature!=null&&restored.RoiSignature.Length==64&&
                        restored.GetFeatureMeasurements().All(m=>m.RoiSignature==restored.RoiSignature)&&
                        restored.Cylinder.SelectedPointIndices.SequenceEqual(sidewall)&&
                        restored.Cylinder.HasExplicitSidewallRoi&&
                        restored.Cylinder.ResultId==caseValue.Cylinder.ResultId&&
                        restored.Surface.ResultId==caseValue.Surface.ResultId&&
                        restored.Cylinder.SourceSurfaceResultId==restored.Surface.ResultId&&
                        Math.Abs(restored.Cylinder.RadiusMm-caseValue.Cylinder.RadiusMm)<1e-10&&
                        restored.GetFeatureMeasurements().All(m=>
                            m.SourceSignature==source.Signature&&
                            m.ParameterSignature==restored.ParameterSignature&&
                            Object.ReferenceEquals(m.SpatialFrame,restored.Source.SpatialFrame)),
                        "MEM重开圆柱保留Feature、Surface、来源签名及毫米量值");
                    var changedRoi=restored.RecomputeWithCylinderRoi(folder,
                        sidewall.Reverse().ToArray(),CancellationToken.None);
                    Check(changedRoi.Cylinder.ResultId!=restored.Cylinder.ResultId&&
                        changedRoi.RoiSignature!=restored.RoiSignature&&
                        changedRoi.ParameterSignature!=restored.ParameterSignature&&
                        changedRoi.Cylinder.SelectedPointIndices.Count==sidewall.Length,
                        "侧壁ROI修改须使圆柱Feature与配方指纹更新");
                    var changedAxis=restored.RecomputeWithCylinderAxis(folder,
                        new FVOPhysicalPoint3D(0,0,-1),CancellationToken.None);
                    Check(changedAxis.Cylinder.ResultId!=restored.Cylinder.ResultId&&
                        changedAxis.ParameterSignature!=restored.ParameterSignature&&
                        Math.Abs(changedAxis.Cylinder.RadiusMm-restored.Cylinder.RadiusMm)<1e-9,
                        "修改圆柱轴向约束形成新配方和独立测量身份");
                    var recomputed=restored.Recompute(folder,CancellationToken.None);
                    Check(recomputed.Cylinder.SelectedPointIndices.SequenceEqual(sidewall)&&
                        recomputed.Cylinder.ResultId!=restored.Cylinder.ResultId&&
                        Math.Abs(recomputed.Cylinder.DiameterMm-restored.Cylinder.DiameterMm)<1e-10,
                        "固定轴向圆柱重新计算不复用旧结果身份");
                }
                Reject<ArgumentOutOfRangeException>(()=>CT3DSurfaceCylinderAnalysis.FitCylinder(
                    samples,new FVOPhysicalPoint3D(0,0,0)));
                Reject<OperationCanceledException>(()=>CT3DSurfaceCylinderAnalysis.FitCylinder(
                    samples,axis,10000000,new CancellationToken(true)));
            }
            string cylinderProject=Path.Combine(folder,"cylinder.cxproj");
            string cylinderRaw=Path.Combine(folder,"cylinder.raw");
            byte[] altered=(byte[])raw.Clone();altered[0]=1;
            File.WriteAllBytes(cylinderRaw,altered);
            using(var engineering=MEMEngineering.Open(cylinderProject))
            {
                Reject<InvalidDataException>(()=>CT3DAnalysisPersistence.OpenAll(
                    engineering,folder,CancellationToken.None));
                var owner=engineering.Project.DescendantsAndSelf().OfType<MEMContent>()
                    .Single(c=>c.Aspects.Any(a=>a.ProviderId==CT3DAnalysisPersistence.ProviderId));
                int revision=engineering.Revision;
                Reject<InvalidDataException>(()=>CT3DAnalysisPersistence.RecomputeAllAndCommit(
                    engineering,new[]{owner},revision,folder,CancellationToken.None));
                Check(engineering.Revision==revision,"圆柱RAW被改动时必须拒绝整体复算且不改变MEM工程");
            }
            File.WriteAllBytes(cylinderRaw,raw);
            Console.WriteLine("PASS CT3D constrained-axis UInt16 cylinder fit and provenance");
        }

        private static void VerifySchema1Compatibility(string folder)
        {
            var root=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while(root!=null&&!File.Exists(Path.Combine(root.FullName,"CxVisual.Unified.sln")))root=root.Parent;
            if(root==null)throw new DirectoryNotFoundException("Cannot locate schema1 fixture repository");
            string fixture=Path.Combine(root.FullName,"AppData","Cases","CT3D","V04Schema1");
            // 固定样本由Schema1正式DLL生成；不能用当前Schema写入器重新生成冒充旧版本。
            string rawFixture=Path.Combine(fixture,"source.raw");
            string projectFixture=Path.Combine(fixture,"schema1.cxproj");
            if(!File.Exists(rawFixture)||!File.Exists(projectFixture)||
                !Directory.Exists(Path.Combine(fixture,"artifacts","ct3d")))
            {
                // 正式兼容性门禁必须失败，不能把外部历史资产缺失视为通过。
                if(String.Equals(Environment.GetEnvironmentVariable("CXVISUAL_CT3D_REQUIRE_LEGACY_FIXTURE"),
                    "1",StringComparison.Ordinal))
                    throw new FileNotFoundException(
                        "A包严格Schema1验收要求真实旧版DLL生成的RAW、工程与Artifact；当前fixture不完整。",
                        fixture);
                Console.WriteLine("SKIP CT3D fixed schema1 compatibility: legacy fixture absent; set CXVISUAL_CT3D_REQUIRE_LEGACY_FIXTURE=1 for strict acceptance");
                return;
            }
            string migrated=Path.Combine(folder,"schema1-migrated");Directory.CreateDirectory(migrated);
            string legacyArtifact=Directory.GetFiles(Path.Combine(fixture,"artifacts","ct3d"),"*.analysis").Single();
            using(var input=File.OpenRead(legacyArtifact))
            using(var reader=new BinaryReader(input))
                Check(reader.ReadInt32()==0x33444358&&reader.ReadInt32()==1,
                    "兼容样本必须具有真实旧V1制品头，不能由新Schema样本替代");
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
            Console.WriteLine("PASS CT3D fixed schema1 fixture read and current schema resave compatibility");
        }
        private static void Reject<T>(Action operation) where T:Exception
        {
            try{operation();}catch(T){return;}
            throw new InvalidOperationException("必须拒绝："+typeof(T).Name);
        }
    }
}
