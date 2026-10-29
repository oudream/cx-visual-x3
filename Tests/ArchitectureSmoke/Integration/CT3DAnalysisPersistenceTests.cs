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
            try{VerifyRoundtrip(folder);VerifySphere(Path.Combine(folder,"sphere"));VerifySchema1Compatibility(folder);}
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
                Check(restored.Source.SpatialFrame!=frame &&
                    Math.Abs(restored.Plane.SignedDistanceMm(new FVOPhysicalPoint3D(25,35,47.5),restored.Source.SpatialFrame)-1)<1e-9,
                    "重建Frame对象后仍可在恢复的同一Frame内测量");
                var recomputed=restored.Recompute(folder,CancellationToken.None);
                Check(recomputed.Mesh.ResultId!=restored.Mesh.ResultId &&
                    recomputed.Plane.ResultId!=restored.Plane.ResultId &&
                    recomputed.Source.Signature==restored.Source.Signature &&
                    Math.Abs(recomputed.Plane.PointMm.Z-restored.Plane.PointMm.Z)<1e-10 &&
                    recomputed.Mesh.Mesh.TriangleIndices.SequenceEqual(restored.Mesh.Mesh.TriangleIndices),
                    "复算形成新结果身份且几何量值一致");
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
            string path=Path.Combine(folder,"sphere.cxproj");MRIObjectAddress address;
            using(var engineering=MEMEngineering.Create("Surface Sphere"))
            {
                var owner=engineering.AddPage("Sphere").RootContent;
                address=CT3DAnalysisPersistence.Commit(engineering,owner,engineering.Revision,folder,analysis,CancellationToken.None);
                engineering.SaveAs(path);
            }
            using(var engineering=MEMEngineering.Open(path))
            {
                var restored=CT3DAnalysisPersistence.Open(engineering,address,folder,CancellationToken.None);
                Check(restored.Kind==CT3DAnalysisKind.SurfaceSphere&&restored.Surface.ResultId==analysis.Surface.ResultId&&
                    restored.Sphere.ResultId==analysis.Sphere.ResultId&&restored.Surface.SampleStep==1&&
                    restored.Surface.MaximumPoints==4000&&restored.Sphere.MaximumConditionNumber==5000000&&
                    restored.Surface.Points.SequenceEqual(analysis.Surface.Points)&&
                    restored.Sphere.SourceSurfaceResultId==restored.Surface.ResultId&&
                    Math.Abs(restored.Sphere.RmsResidualMm-analysis.Sphere.RmsResidualMm)<1e-10,
                    "Surface/球重开保留采样参数、点集、结果依赖、半径与残差");
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
