using System;
using System.Reflection;
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
            Console.WriteLine("PASS MPW legacy GLSL and cube geometry compiled contracts (GPU runtime pending)");
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
