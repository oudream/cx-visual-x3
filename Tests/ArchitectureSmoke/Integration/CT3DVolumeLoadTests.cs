using System;
using System.IO;
using System.Threading;
using CxVisual.Foundation.Volume;
using CxVisual.Professional.CT3D;
using CxVisual.Mechanism.Visual;

namespace CxVisual.Tests.ArchitectureSmoke
{
    /// <summary>专业CT3D加载策略的独立正反例，不依赖TestCenter或WPF窗口。</summary>
    internal static class CT3DVolumeLoadTests
    {
        public static void Run()
        {
            string folder = Path.Combine(Path.GetTempPath(),
                "cxvisual-ct3d-load-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string raw = Path.Combine(folder, "volume_8x8x8_sp1x1x1.raw");
                byte[] voxels = new byte[512];
                for (int i = 0; i < voxels.Length; i++) voxels[i] = (byte)(i % 256);
                File.WriteAllBytes(raw, voxels);
                using (FVOVolumeData preview = CT3DVolumeLoadService
                    .LoadRawPreviewAsync(raw, CancellationToken.None)
                    .GetAwaiter().GetResult())
                {
                    Check(preview.OriginalDepth == 8 &&
                        preview.GetVoxelByOriginalIndex(7,7,7)==255,
                        "RAW后台预览应保留数据及原始尺寸");
                    Check(!CT3DVolumeLoadService.NeedsOriginalCache(preview),
                        "完整8位RAW不能被重复分配高清缓存");
                }

                string high = Path.Combine(folder, "volume_8x8x8_sp1x1x1_uint16.raw");
                byte[] data = new byte[1024];
                for (int i = 0; i < 512; i++)
                {
                    ushort value = (ushort)(1000 + i);
                    data[2*i] = (byte)(value & 255);
                    data[2*i + 1] = (byte)(value >> 8);
                }
                File.WriteAllBytes(high,data);
                using (FVOVolumeData preview = CT3DVolumeLoadService
                    .LoadRawPreviewAsync(high,CancellationToken.None).GetAwaiter().GetResult())
                {
                    Check(CT3DVolumeLoadService.NeedsOriginalCache(preview) &&
                        !CT3DVolumeLoadService.UsesMappedOriginalCache(preview),
                        "UInt16小RAW应按专业策略建立完整非托管原始缓冲");
                    FVOUnmanagedRawVolumeBuffer original = CT3DVolumeLoadService
                        .LoadOriginalCacheAsync(preview,CancellationToken.None)
                        .GetAwaiter().GetResult();
                    preview.SetRawBuffer(original);
                    Check(preview.HasOriginalRawData &&
                        preview.GetScalarByOriginalIndex(7,7,7)==1511f &&
                        !CT3DVolumeLoadService.NeedsOriginalCache(preview),
                        "CT3D返回原始缓冲交FVO持有，16位原值不经R8量化");
                    bool duplicateRejected = false;
                    try
                    {
                        CT3DVolumeLoadService.LoadOriginalCacheAsync(preview,
                            CancellationToken.None).GetAwaiter().GetResult();
                    }
                    catch (InvalidOperationException) { duplicateRejected=true; }
                    Check(duplicateRejected,"重复建立原始缓存应明确拒绝");
                }

                // V01→V02→V03：仅通过FVO真实RAW租约、空间mm与FNU SVD形成Headless数值闭环。
                string sphereRaw=Path.Combine(folder,"sphere_32x32x32_sp1x1x1_uint16.raw");
                byte[] sphereVoxels=new byte[32*32*32*2];
                for(int z=0;z<32;z++)
                for(int y=0;y<32;y++)
                for(int x=0;x<32;x++)
                {
                    double dx=x-15.5,dy=y-15.5,dz=z-15.5;
                    ushort value=(ushort)(dx*dx+dy*dy+dz*dz<=81?1511:0);
                    int offset=2*(x+y*32+z*32*32);
                    sphereVoxels[offset]=(byte)value;
                    sphereVoxels[offset+1]=(byte)(value>>8);
                }
                File.WriteAllBytes(sphereRaw,sphereVoxels);
                using(FVOVolumeData sphere=FVORawVolumeLoader.Load(sphereRaw))
                {
                    sphere.SetRawBuffer(FVORawVolumeLoader.LoadRawBuffer(
                        sphere,new FVORawVolumeLoadTiming()));
                    using(FVOOriginalScalarLease uncalibrated=sphere.AcquireOriginalScalarLease())
                    {
                        CT3DSurfaceSamples preview=CT3DSurfaceSphereAnalysis.SampleBoundary(
                            uncalibrated,"fixture:synthetic-sphere",7,1000f,1,5000,CancellationToken.None);
                        bool refused=false;
                        try {CT3DSurfaceSphereAnalysis.FitSphere(preview);}
                        catch(InvalidOperationException){refused=true;}
                        Check(refused,"原始体虽完整但空间未标定，禁止返回专业毫米拟合");
                    }
                    sphere.SetSpatialFrame(new FVOVolumeSpatialFrame(
                        sphere.SpacingX,sphere.SpacingY,sphere.SpacingZ,0,0,0,
                        new double[]{1,0,0,0,1,0,0,0,1},true));
                    using(FVOOriginalScalarLease original=sphere.AcquireOriginalScalarLease())
                    {
                        CT3DSurfaceSamples points=CT3DSurfaceSphereAnalysis.SampleBoundary(
                            original,"fixture:synthetic-sphere",7,1000f,1,5000,CancellationToken.None);
                        Check(points.Count>300 && original.Format==FVORawVoxelFormat.UInt16 &&
                              original.ReadScalar(15,15,15)==1511 && !points.TouchesVolumeBoundary &&
                            points.SurfaceMethod=="binary-voxel-center-6-neighbor",
                            "原值边界采样保留方法和截断证据");
                        CT3DSphereFitResult fit=CT3DSurfaceSphereAnalysis.FitSphere(points);
                        Check(Math.Abs(fit.Center.X-15.5)<.2 &&
                              Math.Abs(fit.Center.Y-15.5)<.2 &&
                              Math.Abs(fit.Center.Z-15.5)<.2 &&
                              Math.Abs(fit.RadiusMm-9)<.8 &&
                              fit.RmsResidualMm<.7 && fit.SampleCount==points.Count,
                            "球体拟合必须使用原始体、真实毫米Frame与FNU SVD残差");
                        Check(fit.SourceRef=="fixture:synthetic-sphere" &&
                              fit.SourceRevision==7 && fit.ResultId!=Guid.Empty,
                            "专业结果必须保留原值来源、版本和临时结果身份");
                        MVI3DSceneState sceneResult=new MVI3DSceneState();
                        Guid surfaceReference=sceneResult.Add(MVI3DSceneKind.Surface,
                            "ct3d:result:"+fit.ResultId.ToString("N"),fit.SourceRevision,
                            "fixture:physical-frame");
                        sceneResult.Select(surfaceReference);
                        Check(sceneResult.SelectedId==surfaceReference &&
                              sceneResult.Snapshot()[0].Kind==MVI3DSceneKind.Surface,
                            "CT3D结果通过MVI场景资源引用显示，不把专业算法或结果复制进MVI");
                        bool limit=false;
                        try {CT3DSurfaceSphereAnalysis.SampleBoundary(original,"fixture:synthetic-sphere",7,1000,1,8,
                            CancellationToken.None);}
                        catch(InvalidOperationException){limit=true;}
                        Check(limit,"超预算必须报错，不得只取一侧点伪报球半径");
                        CT3DSurfaceSamples cut=CT3DSurfaceSphereAnalysis.SampleBoundary(
                            original,"fixture:synthetic-sphere",7,0f,1,10000,CancellationToken.None);
                        Check(cut.TouchesVolumeBoundary,"接触边缘的体必须保留截断诊断");
                        bool cutRefused=false;
                        try {CT3DSurfaceSphereAnalysis.FitSphere(cut);}
                        catch(InvalidOperationException){cutRefused=true;}
                        Check(cutRefused,"截断对象禁止冒充完整球体拟合");
                        using(CancellationTokenSource cancelled=new CancellationTokenSource())
                        {
                            cancelled.Cancel();
                            bool cancellationObserved=false;
                            try {CT3DSurfaceSphereAnalysis.SampleBoundary(original,"fixture:synthetic-sphere",7,1000,1,5000,
                                cancelled.Token);}
                            catch(OperationCanceledException){cancellationObserved=true;}
                            Check(cancellationObserved,"专业表面采样须响应平台取消信号");
                        }
                    }
                }

                using(CancellationTokenSource stopped=new CancellationTokenSource())
                {
                    stopped.Cancel();
                    bool refused=false;
                    try
                    {
                        CT3DVolumeLoadService.LoadRawPreviewAsync(raw,stopped.Token)
                            .GetAwaiter().GetResult();
                    }
                    catch(OperationCanceledException) { refused=true; }
                    Check(refused,"已取消的后台RAW任务必须拒绝开始");
                }
                bool missingRejected=false;
                try
                {
                    CT3DVolumeLoadService.LoadRawPreviewAsync(
                        Path.Combine(folder,"missing.raw"),CancellationToken.None)
                        .GetAwaiter().GetResult();
                }
                catch(FileNotFoundException) { missingRejected=true; }
                Check(missingRejected,"不存在的RAW源必须给明确错误");
                Console.WriteLine("PASS CT3D RAW preview/cache policy and independent tests");
            }
            finally { Directory.Delete(folder,true); }
        }

        private static void Check(bool result,string description)
        {
            if(!result)throw new InvalidOperationException(
                "CT3D加载验收失败："+description);
        }
    }
}
