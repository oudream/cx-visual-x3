using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CxVisual.Foundation.Geometry;
using CxVisual.Foundation.Volume;
using CxVisual.Mechanism.Presentation.Wpf.Volume.Rendering;
using CxVisual.Mechanism.Visual;

namespace CxVisual.TestCenter.UI
{
    /// <summary>
    /// 必须在真实OpenGL 3.3驱动的Windows工作站执行；不可用时显式失败，绝不把WPF切面当体绘。
    /// 验证GPU context、真实3D纹理、旧GLSL编译、Raymarch帧非空、重复装载和释放。
    /// </summary>
    internal static class TCTVolumeGpuHardwareSmoke
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hwnd,uint message,UIntPtr wParam,IntPtr lParam);
        private static IntPtr CursorPoint(int x,int y)
        {
            return new IntPtr((y<<16)|(x&0xffff));
        }
        private static FGEPoint3D PhysicalVertex(FVOVolumeSpatialFrame frame,int x,int y,int z)
        {
            FVOPhysicalPoint3D p=frame.VoxelToPhysical(x,y,z);
            return new FGEPoint3D(p.X,p.Y,p.Z);
        }
        private static int FrameDifference(BitmapSource before,BitmapSource after)
        {
            if(before.PixelWidth!=after.PixelWidth||before.PixelHeight!=after.PixelHeight)
                throw new InvalidOperationException("GPU多对象渲染后帧尺寸被意外修改。");
            int stride=checked(before.PixelWidth*4);
            byte[] a=new byte[checked(stride*before.PixelHeight)];
            byte[] b=new byte[a.Length];
            before.CopyPixels(a,stride,0);after.CopyPixels(b,stride,0);
            int different=0;
            for(int i=0;i<a.Length;i+=4)
                if(Math.Abs(a[i]-b[i])+Math.Abs(a[i+1]-b[i+1])+
                   Math.Abs(a[i+2]-b[i+2])>=40)different++;
            return different;
        }
        public static void Run()
        {
            TCTManualVolumeViewerFrame frame=new TCTManualVolumeViewerFrame();
            Window window=new Window { Width=1400,Height=960,
                Title="CxVisual GPU real acceptance (OpenGL 3.3)",
                Content=frame, WindowStyle=WindowStyle.ToolWindow,ShowInTaskbar=false };
            Stopwatch clock=Stopwatch.StartNew();
            try
            {
                window.Show();window.UpdateLayout();
                frame.LoadDemo();
                frame.SetObliqueForTest(true,22,13,40);
                frame.TryEnableGpu();
                window.UpdateLayout();
                frame.Dispatcher.Invoke(DispatcherPriority.Background,new Action(delegate {}));
                if(!frame.IsGpuVolumeRendering)
                    throw new NotSupportedException("GPU实机验收失败（禁止回退后宣称通过）："+frame.GpuStatusText);
                // 必须给真实OpenGL子窗口发送Win32鼠标消息，而不是直接调用相机API。
                CxVisual.Mechanism.Presentation.Wpf.Volume.Rendering.MPWVolumeOpenGlControl gl=
                    frame.GpuControlForHardwareTest;
                if(gl==null||gl.NativeHandleForHardwareTest==IntPtr.Zero)
                    throw new InvalidOperationException("GPU子窗口未实际创建。");
                Vector3 rayStart,rayDirection;
                if(!gl.TryGetNormalizedPickRay(250,200,out rayStart,out rayDirection)||
                    Math.Abs(rayDirection.Length()-1)>1e-4||
                    gl.TryGetNormalizedPickRay(-1,200,out rayStart,out rayDirection))
                    throw new InvalidOperationException(
                        "真实GPU窗口像素到体空间拾取射线反投影或越界验证失败。");
                float yaw=gl.CameraYaw,distance=gl.CameraDistance;
                SendMessage(gl.NativeHandleForHardwareTest,0x0201,new UIntPtr(1),CursorPoint(60,60));
                SendMessage(gl.NativeHandleForHardwareTest,0x0200,new UIntPtr(1),CursorPoint(110,85));
                SendMessage(gl.NativeHandleForHardwareTest,0x0202,UIntPtr.Zero,CursorPoint(110,85));
                if(Math.Abs(gl.CameraYaw-yaw)<.02f)
                    throw new InvalidOperationException("真实GPU窗口左键拖动未旋转相机。");
                SendMessage(gl.NativeHandleForHardwareTest,0x0204,new UIntPtr(2),CursorPoint(65,60));
                SendMessage(gl.NativeHandleForHardwareTest,0x0200,new UIntPtr(2),CursorPoint(105,80));
                SendMessage(gl.NativeHandleForHardwareTest,0x0205,UIntPtr.Zero,CursorPoint(105,80));
                if(gl.CameraTarget.Length()<.001f)
                    throw new InvalidOperationException("真实GPU窗口右键拖动未平移相机。");
                SendMessage(gl.NativeHandleForHardwareTest,0x020A,
                    new UIntPtr((uint)(120<<16)),CursorPoint(100,100));
                if(Math.Abs(gl.CameraDistance-distance)<.05f || gl.NativePointerEvents<5)
                    throw new InvalidOperationException("真实GPU窗口滚轮缩放或原生鼠标事件未接通。");
                // 旧版操作应实时：材质、采样及切面显隐不得每次扫描体素构造新LUT。
                int oldUploads=frame.GpuTransferUploadCount;
                frame.SetOpacityFromPanelForTest(.61);
                frame.SetSliceVisibilityFromPanelForTest(false);
                if(frame.GpuTransferUploadCount!=oldUploads)
                    throw new InvalidOperationException("GPU灯光/透明度滑杆触发了不必要的RAW全体扫描/LUT上传。");
                frame.SetColorCurveEnabledFromPanelForTest(true);
                if(frame.GpuTransferUploadCount<=oldUploads)
                    throw new InvalidOperationException("GPU颜色曲线改变后必须上传真实LUT。");
                frame.SetColorCurveEnabledFromPanelForTest(false);
                string driver=frame.GpuStatusText;
                BitmapSource pixels=frame.CaptureGpuFrameForTest();
                if(pixels.PixelWidth<200 || pixels.PixelHeight<150)
                    throw new InvalidOperationException("GPU framebuffer宽高异常。");
                int stride=checked(pixels.PixelWidth*4);
                byte[] rgba=new byte[checked(stride*pixels.PixelHeight)];
                pixels.CopyPixels(rgba,stride,0);
                int nonBackground=0;
                for(int i=0;i<rgba.Length;i+=Math.Max(4,(rgba.Length/25000)&~3))
                    if(rgba[i]>35 || rgba[i+1]>35 || rgba[i+2]>35) nonBackground++;
                if(nonBackground<200)
                    throw new InvalidOperationException("GPU帧只有清屏色，没有真实Raymarch/切面像素。");
                // G4真实显卡：三角面由FGE毫米Mesh经现有MPW空间投影上传GL，
                // 截图须发生真实像素变化，而不是仅登记一个面数。
                FVOVolumeData volume=frame.Volume;
                var spatial=volume.SpatialFrame;
                int vx=volume.OriginalWidth,vy=volume.OriginalHeight,vz=volume.OriginalDepth;
                var originalMesh=new FGETriangleMesh3D(new[]{
                    PhysicalVertex(spatial,vx/3,vy/3,vz/3),
                    PhysicalVertex(spatial,2*vx/3,vy/3,vz/3),
                    PhysicalVertex(spatial,vx/3,2*vy/3,vz/3),
                    PhysicalVertex(spatial,vx/3,vy/3,2*vz/3)
                },new[]{0,2,1,0,1,3,0,3,2,1,2,3});
                MPWVolumeDisplayMesh projected=MPWVolumeSurfaceProjection.ProjectMesh(
                    originalMesh,spatial,vx,vy,vz);
                gl.SetSurfaceMesh(projected);
                BitmapSource meshImage=frame.CaptureGpuFrameForTest();
                if(gl.SurfaceMeshTriangleCount!=4||
                    FrameDifference(pixels,meshImage)<250)
                    throw new InvalidOperationException(
                        "真实GPU Mesh VBO与深度绘制未造成可观察像素变化。");
                gl.ClearSurfaceMesh();
                if(gl.SurfaceMeshTriangleCount!=0)
                    throw new InvalidOperationException("GPU Mesh清理后仍保留三角面引用。");
                Guid gpuMeshA=Guid.NewGuid(),gpuMeshB=Guid.NewGuid();
                gl.SetSceneMesh(gpuMeshA,projected,.35);
                gl.SetSceneMesh(gpuMeshB,projected,.85);
                if(gl.SceneMeshCount!=2||gl.SceneMeshTriangleCount!=8)
                    throw new InvalidOperationException("真实GPU双Mesh必须有两个独立显存资源实例。");
                BitmapSource grouped=frame.CaptureGpuFrameForTest();
                if(FrameDifference(pixels,grouped)<100)
                    throw new InvalidOperationException("GPU两个叠加Mesh未产生可观察像素差异。");
                gl.RemoveSceneMesh(gpuMeshA);
                if(gl.SceneMeshCount!=1||gl.SceneMeshTriangleCount!=4)
                    throw new InvalidOperationException("删除一个GPU Mesh不得清除另一个VBO。");
                gl.ClearSceneMeshes();
                if(gl.SceneMeshCount!=0||gl.SceneMeshTriangleCount!=0)
                    throw new InvalidOperationException("清理所有GPU Scene Mesh后仍留显示缓存。");
                var sharedScene=new MVI3DSceneState();
                Guid mviMesh=sharedScene.Add(MVI3DSceneKind.Mesh,"ct3d:test:mesh",1);
                Guid mviCad=sharedScene.Add(MVI3DSceneKind.Cad,"cad:test:mesh",1);
                gl.BindSceneMeshes(sharedScene,item=>projected);
                if(gl.SceneMeshCount!=2)
                    throw new InvalidOperationException("GPU应按MVI Scene组合加载独立Mesh/CAD VBO。");
                int uploadsBeforeStyle=gl.SceneMeshUploadCount;
                sharedScene.SetOpacity(mviMesh,.35);
                if(gl.SceneMeshUploadCount!=uploadsBeforeStyle)
                    throw new InvalidOperationException("GPU样式变化不得重复上传顶点VBO。");
                sharedScene.SetVisibility(mviCad,false);
                if(gl.SceneMeshCount!=1)
                    throw new InvalidOperationException("MVI隐藏CAD应释放GPU VBO。");
                sharedScene.Remove(mviMesh);
                if(gl.SceneMeshCount!=0)
                    throw new InvalidOperationException("MVI删除Mesh应自动卸载对应GPU资源。");
                gl.BindSceneMeshes(null,null);
                var cloudScene=new MVI3DSceneState();
                Guid cloudId=cloudScene.Add(MVI3DSceneKind.PointCloud,
                    "ct3d:test:pointcloud",1,"frame:verified",.45);
                var cloudPoints=new[]{new Vector3(-.1f,-.1f,-.1f),
                    new Vector3(.1f,.1f,.1f),new Vector3(.2f,0,.1f)};
                gl.BindScenePointClouds(cloudScene,item=>cloudPoints);
                if(gl.ScenePointCloudCount!=1||gl.ScenePointCount!=3)
                    throw new InvalidOperationException("真实GPU应上传独立点云VBO而非复用Surface点层。");
                int originalCloudUploads=gl.ScenePointCloudUploadCount;
                cloudScene.SetOpacity(cloudId,.65);
                if(gl.ScenePointCloudUploadCount!=originalCloudUploads)
                    throw new InvalidOperationException("GPU点云仅改透明度不得重传VBO。");
                cloudScene.SetVisibility(cloudId,false);
                if(gl.ScenePointCloudCount!=0)
                    throw new InvalidOperationException("隐藏点云必须回收GPU VBO。");
                cloudScene.SetVisibility(cloudId,true);
                if(gl.ScenePointCloudCount!=1||
                    gl.ScenePointCloudUploadCount<=originalCloudUploads)
                    throw new InvalidOperationException("恢复可见点云时必须重新创建GPU VBO。");
                cloudScene.Remove(cloudId);
                if(gl.ScenePointCloudCount!=0||gl.ScenePointCount!=0)
                    throw new InvalidOperationException("移除点云后仍有GPU显示资源。");
                gl.BindScenePointClouds(null,null);
                string path=Environment.GetEnvironmentVariable("CXVISUAL_GPU_UI_SCREENSHOT");
                if(string.IsNullOrEmpty(path))
                    path=Path.Combine(Path.GetTempPath(),"CxVisual-GPU-Hardware-Raymarch.png");
                string folder=Path.GetDirectoryName(Path.GetFullPath(path));
                if(!Directory.Exists(folder))Directory.CreateDirectory(folder);
                PngBitmapEncoder encoder=new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(pixels));
                using(FileStream output=File.Create(path))encoder.Save(output);
                if(new FileInfo(path).Length<4096)
                    throw new InvalidOperationException("真实GPU PNG文件长度过小。");
                for(int cycle=0;cycle<3;cycle++)
                {
                    frame.LoadDemo();
                    if(!frame.IsGpuVolumeRendering)
                        throw new InvalidOperationException("重复载入后GPU上下文丢失: "+cycle);
                    BitmapSource frameImage=frame.CaptureGpuFrameForTest();
                    if(frameImage.PixelWidth!=pixels.PixelWidth)
                        throw new InvalidOperationException("重载后GPU视口宽度异常。");
                }
                clock.Stop();
                Console.WriteLine("PASS real OpenGL hardware; "+driver+"; PNG="+path+
                    "; replay=3; elapsedMs="+clock.ElapsedMilliseconds);
            }
            finally
            {
                frame.OnWorkspaceClosed();
                window.Close();
            }
        }
    }
}
