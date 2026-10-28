using System;
using System.Reflection;
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
