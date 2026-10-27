using System;
using System.IO;
using System.Threading;
using CxVisual.Foundation.Volume;

namespace CxVisual.Tests.ArchitectureSmoke
{
    /// <summary>F12 RAW/高精度体素缓存及切片空间的正反例（无HALCON、WPF）。</summary>
    internal static class FVOVolumeTests
    {
        public static void Run()
        {
            string folder = Path.Combine(Path.GetTempPath(), "cxvisual-fvo-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string path8 = Path.Combine(folder, "sample_8x8x8_sp0p1x0p2x0p3.raw");
                byte[] pixels = new byte[512];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = (byte)(i % 256);
                File.WriteAllBytes(path8, pixels);
                FVORawVolumeInfo info = FVORawVolumeLoader.InferInfo(path8);
                Check(info.Format == FVORawVoxelFormat.Byte && info.Width == 8 &&
                    Math.Abs(info.SpacingY - 0.2f) < 0.00001, "RAW格式/大小/间距推断");
                using (FVOVolumeData data = FVORawVolumeLoader.Load(path8))
                {
                    Check(data.OriginalWidth == 8 && data.Depth == 8 &&
                        data.SourceFormat == FVORawVoxelFormat.Byte, "预览+原始尺寸");
                    Check(FVORawVolumeLoader.TryLoadOriginalVoxelCache(data), "完整分辨率缓存");
                    Check(data.GetVoxelByOriginalIndex(7, 7, 7) == 255, "高分辨率体素取值");
                    Check(data.HasHighResolutionVoxels, "缓存所有权");
                }
                string path16 = Path.Combine(folder, "sample_8x8x8_sp1x1x2.raw");
                byte[] raw16 = new byte[1024];
                for (int i = 0; i < 512; i++)
                {
                    ushort value = (ushort)(1000 + i);
                    raw16[i * 2] = (byte)value;
                    raw16[i * 2 + 1] = (byte)(value >> 8);
                }
                File.WriteAllBytes(path16, raw16);
                // 内存输入不需要虚构源RAW文件；FVO拥有私有副本，原始精度供高清MPR读取。
                FVORawVolumeInfo memory16 = new FVORawVolumeInfo(8,8,8,
                    0.1f,0.2f,0.3f,FVORawVoxelFormat.UInt16,"memory-ushort16");
                using (FVOVolumeData memoryData = FVORawVolumeLoader.LoadFromMemory(
                    raw16,memory16,64,CancellationToken.None))
                {
                    Check(memoryData.HasOriginalRawData && !memoryData.HasRawSource &&
                          memoryData.SourceFilePath==null &&
                          memoryData.GetScalarByOriginalIndex(7,7,7)==1511f,
                        "内存16位输入持有独立原始缓冲，不伪造文件来源且数值不降精度");
                    CxVisual.Foundation.Imaging.FIMImageBuffer originalSlice =
                        FVOSliceImageBuilder.BuildScalar(memoryData,0,0,7,FVOSlicePlane.XY);
                    int memorySliceX, memorySliceY;
                    FVOSliceImageBuilder.VoxelToImage(memoryData,FVOSlicePlane.XY,
                        7,7,7,out memorySliceX,out memorySliceY);
                    Check(memorySliceX>=0 && memorySliceX<originalSlice.Width &&
                          memorySliceY>=0 && memorySliceY<originalSlice.Height &&
                          originalSlice.GetGrayValue(memorySliceX,memorySliceY)==1511 &&
                          Math.Abs(memoryData.SpacingY-.2f)<.00001,
                        "内存原始体素与非等距物理Spacing映射后用于高清二维切片");
                    raw16[1022]=0;raw16[1023]=0;
                    Check(memoryData.GetScalarByOriginalIndex(7,7,7)==1511f,
                        "修改宿主托管源数组不应影响已构建FVO");
                    raw16[1022]=(byte)(1511 & 0xff);raw16[1023]=(byte)(1511 >> 8);
                }
                // 原生指针入口同样必须完整拷贝；宿主释放原内存后仍能读取真实标量。
                IntPtr hostMemory=System.Runtime.InteropServices.Marshal.AllocHGlobal(raw16.Length);
                try
                {
                    System.Runtime.InteropServices.Marshal.Copy(raw16,0,hostMemory,raw16.Length);
                    using(FVOVolumeData independent=FVORawVolumeLoader.LoadFromMemory(
                        hostMemory,raw16.LongLength,memory16,64,CancellationToken.None))
                    {
                        System.Runtime.InteropServices.Marshal.FreeHGlobal(hostMemory);
                        hostMemory=IntPtr.Zero;
                        Check(independent.GetScalarByOriginalIndex(7,7,7)==1511f &&
                              independent.HasOriginalRawData,
                            "宿主指针释放后FVO仍唯一拥有原始16位内存");
                    }
                }
                finally
                {
                    if(hostMemory!=IntPtr.Zero)
                        System.Runtime.InteropServices.Marshal.FreeHGlobal(hostMemory);
                }
                byte[] floatBytes=new byte[8*8*8*4];
                for(int index=0;index<512;index++)
                {
                    byte[] single=BitConverter.GetBytes(0.25f + index*0.125f);
                    Buffer.BlockCopy(single,0,floatBytes,index*4,4);
                }
                FVORawVolumeInfo memoryFloat = new FVORawVolumeInfo(8,8,8,
                    1,1,2,FVORawVoxelFormat.Float32,"memory-float32");
                using(FVOVolumeData floatVolume=FVORawVolumeLoader.LoadFromMemory(
                    floatBytes,memoryFloat,64,CancellationToken.None))
                {
                    Check(Math.Abs(floatVolume.GetScalarByOriginalIndex(7,7,7)-64.125f)<.00001 &&
                          floatVolume.HasOriginalRawData,
                        "Float32内存加载应保持标量数值而非从R8预览反推");
                    Check(Math.Abs(FVOSliceImageBuilder.BuildScalar(floatVolume,0,0,7,
                          FVOSlicePlane.XY).GetGrayValue(7,7)-64.125)<.00001,
                        "Float32内存高清切片不做8位量化");
                }
                Expect<InvalidDataException>(()=>FVORawVolumeLoader.LoadFromMemory(
                    new byte[1023],memory16,64,CancellationToken.None),
                    "不足量原始内存必须拒绝，不允许越界读");
                using(CancellationTokenSource canceledMemory=new CancellationTokenSource())
                {
                    canceledMemory.Cancel();
                    Expect<OperationCanceledException>(()=>FVORawVolumeLoader.LoadFromMemory(
                        raw16,memory16,64,canceledMemory.Token),
                        "取消的内存快照不得分配原始缓存");
                }
                using (FVOVolumeData data16 = FVORawVolumeLoader.Load(path16))
                {
                    Check(data16.SourceFormat == FVORawVoxelFormat.UInt16, "UInt16格式");
                    data16.SetRawBuffer(FVORawVolumeLoader.LoadRawBuffer(data16, new FVORawVolumeLoadTiming()));
                    Check(data16.GetScalarByOriginalIndex(7, 7, 7) == 1511f, "原始16bit不经8bit量化恢复");
                    Check(data16.HasOriginalRawData, "非托管原始RAW被持有");
                    FVOObliqueSliceDefinition one = new FVOObliqueSliceDefinition {
                        Width=1,Height=1,CenterX=3.25,CenterY=2,CenterZ=1,
                        PixelSpacingInVoxels=1,
                        Interpolation=FVOObliqueInterpolation.Nearest };
                    float nearest=(float)FVOObliqueSliceBuilder.BuildScalar(data16,one,
                        CancellationToken.None).GetGrayValue(0,0);
                    one.Interpolation=FVOObliqueInterpolation.Linear;
                    float linear=(float)FVOObliqueSliceBuilder.BuildScalar(data16,one,
                        CancellationToken.None).GetGrayValue(0,0);
                    Check(Math.Abs(nearest-1083f)<.001f &&
                        Math.Abs(linear-1083.25f)<.001f,
                        "任意切最近邻与三线性插值必须不同且保持16位原始数值");
                    using(System.Threading.CancellationTokenSource cts =
                        new System.Threading.CancellationTokenSource())
                    {
                        cts.Cancel();
                        Expect<OperationCanceledException>(() =>
                            FVORawVolumeLoader.LoadRawBuffer(data16,new FVORawVolumeLoadTiming(),
                                cts.Token),"取消完整原始RAW缓存必须立即停止分配");
                    }
                }
                // 小文件强制执行大RAW的只读映射路径，不在CI创建多GB文件。
                using(FVOVolumeData mapped16=FVORawVolumeLoader.Load(path16))
                {
                    mapped16.SetRawBuffer(FVORawVolumeLoader.MapRawBuffer(mapped16,
                        CancellationToken.None));
                    Check(mapped16.HasOriginalRawData &&
                        mapped16.GetScalarByOriginalIndex(7,7,7)==1511f,
                        "RAW映射应保留原始16bit标量而非8bit预览");
                    CxVisual.Foundation.Imaging.FIMImageBuffer mappedXy=
                        FVOSliceImageBuilder.BuildScalar(mapped16,0,0,7,FVOSlicePlane.XY);
                    Check(mappedXy.Width==8 && mappedXy.GetGrayValue(7,7)==1511,
                        "文件映射可用于原始分辨率切片");
                    using(CancellationTokenSource canceled=new CancellationTokenSource())
                    {
                        canceled.Cancel();
                        Expect<OperationCanceledException>(()=>
                            FVORawVolumeLoader.MapRawBuffer(mapped16,canceled.Token),
                            "取消大RAW映射读取");
                    }
                }
                // 真实切片采样与显示分离：三平面共享相同体素/物理坐标。
                using (FVOVolumeData scalarSource = FVORawVolumeLoader.Load(path16))
                {
                    scalarSource.SetRawBuffer(FVORawVolumeLoader.LoadRawBuffer(scalarSource, new FVORawVolumeLoadTiming()));
                    CxVisual.Foundation.Imaging.FIMImageBuffer xy =
                        FVOSliceImageBuilder.BuildScalar(scalarSource, 0, 0, 7, FVOSlicePlane.XY);
                    Check(xy.Width == 8 && xy.Height == 8 && xy.GetGrayValue(7, 7) == 1511,
                        "XY原始16位切片不量化");
                    CxVisual.Foundation.Imaging.FIMImageBuffer xz =
                        FVOSliceImageBuilder.Build(scalarSource, 1, 3, 2, FVOSlicePlane.XZ, true, 0, 255);
                    Check(xz.Width == 4 && xz.Height == 8, "XZ非等距物理比例");
                    int ix, iy;
                    FVOSliceImageBuilder.VoxelToImage(scalarSource, FVOSlicePlane.XY, 7, 5, 2, out ix, out iy);
                    int vx, vy, vz;
                    FVOSliceImageBuilder.ImageToVoxel(scalarSource, 3, 4, 2, FVOSlicePlane.XY,
                        ix, iy, out vx, out vy, out vz);
                    Check(vx == 7 && vy == 5 && vz == 2, "三刀与二维坐标往返");
                }
                using (FVOVolumeData demo = FVOVolumeData.CreateDemo(12, 12, 12))
                {
                    FVOVolumeHistogram hist = FVOVolumeHistogram.FromVolume(demo);
                    Check(hist.Scalar.Length == 256 && hist.Gradient.Length == 256, "标量+梯度直方图");
                    FVOIntensityRangeOptions options = new FVOIntensityRangeOptions();
                    options.AutoRange = false;
                    FVOVolumeIntensityProfile full = FVOVolumeIntensityProfile.FromVolume(demo, options);
                    Check(full.Lower == 0 && full.Upper == 1, "关闭自适应映射使用全灰阶");
                    options.AutoRange = true;
                    FVOIntensityRangeOptions previous = options;
                    FVOVolumeIntensityProfile profile = FVOVolumeIntensityProfile.FromVolume(demo, previous);
                    Check(profile.Lower >= 0 && profile.Upper <= 1 && profile.Upper > profile.Lower,
                        "预览灰阶有效区间");
                }
                FVOObliqueSliceDefinition oblique = new FVOObliqueSliceDefinition();
                oblique.CenterX = 3; oblique.CenterY = 4; oblique.CenterZ = 5;
                oblique.Width = 5; oblique.Height = 5; oblique.PixelSpacingInVoxels = 1;
                using (FVOVolumeData source = FVOVolumeData.CreateDemo(9, 10, 11))
                {
                    CxVisual.Foundation.Imaging.FIMImageBuffer obliqueImg =
                        FVOObliqueSliceBuilder.BuildPreview(source,oblique,false,0,255,
                            CancellationToken.None);
                    Check(obliqueImg.Width == 5 && obliqueImg.Height == 5 &&
                        Math.Abs(obliqueImg.GetGrayValue(2,2) -
                            source.GetVoxelByOriginalIndex(3,4,5)) <= 1,
                        "任意切中心与原体素一致");
                    CxVisual.Foundation.Imaging.FIMImageBuffer original =
                        FVOObliqueSliceBuilder.BuildScalar(source,oblique,CancellationToken.None);
                    Check(original.PixelFormat == CxVisual.Foundation.Imaging.FIMPixelFormat.GrayFloat32 &&
                        original.Width == 5, "任意切原始浮点标量与显示解耦");
                    oblique.AngleZDegrees = 90;
                    CxVisual.Foundation.Imaging.FIMImageBuffer rotated =
                        FVOObliqueSliceBuilder.BuildPreview(source,oblique,false,0,255,
                            CancellationToken.None);
                    Check(Math.Abs(rotated.GetGrayValue(2,2) -
                        obliqueImg.GetGrayValue(2,2)) <= 1, "绕中心旋转不应改变中心取样");
                    CancellationTokenSource cancellation = new CancellationTokenSource();
                    cancellation.Cancel();
                    Expect<OperationCanceledException>(() => FVOObliqueSliceBuilder.BuildPreview(
                        source,oblique,false,0,255,cancellation.Token), "任意切取消加载");
                    oblique.PixelSpacingInVoxels = 0;
                    Expect<ArgumentOutOfRangeException>(() => FVOObliqueSliceBuilder.BuildScalar(
                        source,oblique,CancellationToken.None), "任意切非法像素步长");
                    oblique.PixelSpacingInVoxels = 1;
                }
                oblique.AngleZDegrees = 0;
                FVOObliqueSliceVector u;
                FVOObliqueSliceVector v;
                FVOObliqueSliceGeometry.GetAxes(oblique, out u, out v);
                Check(u.X == 1.0 && v.Y == 1.0, "正交默认轴");
                oblique.AngleZDegrees = 90;
                FVOObliqueSliceGeometry.GetAxes(oblique, out u, out v);
                Check(Math.Abs(u.Y - 1.0) < 0.000001 && Math.Abs(v.X + 1.0) < 0.000001,
                    "任意切绕Z旋转");
                Check(FVOSlicePlane.XY.GetWheelAxisName() == "Z", "XY切片沿Z滚动");
                CancellationTokenSource cancelled = new CancellationTokenSource();
                cancelled.Cancel();
                Expect<OperationCanceledException>(() => FVORawVolumeLoader.Load(path8, 64, cancelled.Token),
                    "取消RAW加载");
                Expect<ArgumentException>(() => new FVOVolumeData(2, 2, 2, new byte[7], "bad"),
                    "不允许体素尺寸错位");
                Expect<ArgumentOutOfRangeException>(() => FVOVolumeData.CreateDemo(0, 3, 3),
                    "非法体积");
                Expect<InvalidDataException>(() => FVORawVolumeLoader.InferInfo(Path.Combine(folder, "invalid.raw")),
                    "不含尺寸的RAW路径");
                Console.WriteLine("PASS FVO RAW / lossless 16-bit / oblique geometry / negative cases");
            }
            finally
            {
                try { Directory.Delete(folder, true); } catch (IOException) { }
            }
        }

        private static void Check(bool passed, string subject)
        {
            if (!passed) throw new InvalidOperationException("FVO测试失败：" + subject);
        }
        private static void Expect<T>(Action action, string subject) where T : Exception
        {
            try { action(); } catch (T) { return; }
            throw new InvalidOperationException("FVO异常反证失败：" + subject);
        }
    }
}
