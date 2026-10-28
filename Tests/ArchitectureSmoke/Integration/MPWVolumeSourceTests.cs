using System;
using System.Reflection;
using System.Collections.Generic;
using CxVisual.Foundation.Volume;
using System.Windows.Threading;
using CxVisual.Mechanism.Presentation.Wpf.Volume.Rendering;
using CxVisual.Mechanism.Presentation.Wpf.Imaging;

namespace CxVisual.Tests.ArchitectureSmoke
{
    /// <summary>旧GLSL算法与立方体几何被真实编译进MPW程序集；不替代GPU Shader实编译。</summary>
    internal static class MPWVolumeSourceTests
    {
        public static void Run()
        {
            Assembly assembly = typeof(MPWImageViewerControl).Assembly;
            Type shaders = assembly.GetType(
                "CxVisual.Mechanism.Presentation.Wpf.Volume.Rendering.MPWVolumeShaderSources", true);
            string raymarch = (string)ReadField(shaders, "VolumeFragment").GetRawConstantValue();
            string slice = (string)ReadField(shaders, "SliceFragment").GetRawConstantValue();
            Check(raymarch.Contains("sampler3D volumeTex") &&
                  raymarch.Contains("scalarTransferTex") &&
                  raymarch.Contains("gradientOpacityTex") &&
                  raymarch.Contains("opacityReferenceStep") &&
                  raymarch.Contains("enableJitter") &&
                  raymarch.Contains("materialSpecular"), "旧版Raymarch、采样与光照必须保留");
            Check(slice.Contains("obliqueTex") && slice.Contains("showSlicePlanes") &&
                  slice.Contains("showSliceTexture"), "正交/斜切纹理和切面开关");
            Type geometry = assembly.GetType(
                "CxVisual.Mechanism.Presentation.Wpf.Volume.Rendering.MPWVolumeGeometry", true);
            float[] cube = (float[])ReadField(geometry, "CubeVertices").GetValue(null);
            float[] edges = (float[])ReadField(geometry, "CubeLines").GetValue(null);
            Check(cube.Length >= 108 && edges.Length == 72, "原3D体包围盒三角形与12条棱");
            Type gpu = assembly.GetType(
                "CxVisual.Mechanism.Presentation.Wpf.Volume.Rendering.MPWVolumeOpenGlControl", true);
            Check(gpu.BaseType != null &&
                  gpu.BaseType.FullName == "System.Windows.Interop.HwndHost",
                  "真正的WPF HWND宿主而非WinForms GLControl");
            foreach (string api in new[] { "SetVolume", "UpdateTransfer", "SetRendering",
                     "SetSceneOptions", "SetSlicePositions", "SetObliqueTexture", "CaptureFrame", "ResetCamera" })
                Check(gpu.GetMethod(api, BindingFlags.Public | BindingFlags.Instance) != null,
                    "缺少体绘真实API："+api);
            // 验证毫米坐标→GL标准化对象空间；不依赖GPU或专业算法UI。
            FVOVolumeSpatialFrame frame=new FVOVolumeSpatialFrame(.5,1,2,10,20,30,
                new double[]{0,-1,0,1,0,0,0,0,1},true);
            var physical=new List<FVOPhysicalPoint3D>
            {
                frame.VoxelToPhysical(0,0,0),
                frame.VoxelToPhysical(5,10,15),
                frame.VoxelToPhysical(10,20,30)
            };
            var projected=MPWVolumeSurfaceProjection.Project(physical,frame,11,21,31);
            Check(projected.Count==3 && Math.Abs(projected[0].X+.5)<.000001 &&
                Math.Abs(projected[0].Y+.5)<.000001 &&
                Math.Abs(projected[1].X)<.000001 &&
                Math.Abs(projected[1].Y)<.000001 &&
                Math.Abs(projected[2].Z-.5)<.000001,
                "非等距/旋转的物理毫米Source经Frame逆变换投影到真实三维体空间");
            bool invalidFrame=false;
            try
            {
                MPWVolumeSurfaceProjection.Project(new []{
                    new FVOPhysicalPoint3D(5000,5000,5000)},frame,11,21,31);
            }
            catch(ArgumentOutOfRangeException){invalidFrame=true;}
            Check(invalidFrame,"体外专业Surface点不允许误叠加到当前Volume");
            var many=new List<FVOPhysicalPoint3D>();
            for(int i=0;i<100;i++)many.Add(physical[1]);
            Check(MPWVolumeSurfaceProjection.Project(many,frame,11,21,31,7).Count<=7,
                "大点集只进行有界显示抽样，不更改专业真值");
            Check(gpu.GetMethod("SetSurfacePoints")!=null &&
                gpu.GetMethod("ClearSurfacePoints")!=null,
                "专业Surface须进入真实HwndHost/GL framebuffer，而不是WPF伪覆盖");
            // 正式MPW调度器不需要模拟GPU成功即可验证失效合并与代次。
            Dispatcher dispatcher=Dispatcher.CurrentDispatcher;
            int count=0;
            MPWVolumeRenderDirty applied=MPWVolumeRenderDirty.None;
            using(MPWVolumeRenderScheduler scheduler=new MPWVolumeRenderScheduler(
                dispatcher,flags=>{count++;applied=flags;}))
            {
                scheduler.ResetContext(1);
                scheduler.Invalidate(MPWVolumeRenderDirty.Uniforms);
                scheduler.Invalidate(MPWVolumeRenderDirty.Scene);
                Check(count==0 && scheduler.IsQueued &&
                      scheduler.Pending==(MPWVolumeRenderDirty.Uniforms|MPWVolumeRenderDirty.Scene),
                    "单次交互的多次Uniform/Scene变更必须合并");
                scheduler.FlushNow();
                Check(count==1 && applied==(MPWVolumeRenderDirty.Uniforms|MPWVolumeRenderDirty.Scene)
                    && scheduler.Revision==2 && scheduler.AppliedRevision==2,
                    "GPU刷新合并成功后才更新已应用Revision");
                scheduler.Invalidate(MPWVolumeRenderDirty.TransferLut);
                scheduler.ResetContext(2);
                Check(scheduler.Pending==MPWVolumeRenderDirty.None && scheduler.Epoch==2,
                    "旧GPU上下文挂起的上传不得重放到新Context");
                scheduler.Invalidate(MPWVolumeRenderDirty.TransferLut);
                scheduler.FlushNow();
                Check(count==2 && applied==MPWVolumeRenderDirty.TransferLut,
                    "新Context只应用新代次LUT");
                bool bad=false;
                try {scheduler.Invalidate((MPWVolumeRenderDirty)128);}
                catch(ArgumentOutOfRangeException){bad=true;}
                Check(bad && scheduler.Pending==MPWVolumeRenderDirty.None,
                    "未知Dirty位必须拒绝且不污染调度队列");
            }
            Console.WriteLine("PASS MPW GL source and render revision/epoch batching (hardware pending)");
        }

        private static FieldInfo ReadField(Type type, string fieldName)
        {
            FieldInfo field = type.GetField(fieldName,
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null) throw new InvalidOperationException("缺少GLSL几何字段：" + fieldName);
            return field;
        }
        private static void Check(bool ok, string message)
        {
            if (!ok) throw new InvalidOperationException("MPW三维源码测试失败：" + message);
        }
    }
}
