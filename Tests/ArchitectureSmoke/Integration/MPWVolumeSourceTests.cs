using System;
using System.Reflection;
using System.Threading;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using CxVisual.Foundation.Volume;
using CxVisual.Foundation.Geometry;
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
            Vector3 rayOrigin,rayDirection;
            Check(MPWVolumeOpenGlControl.TryUnprojectNormalizedRay(50,50,101,101,
                new Vector3(0,0,3),Vector3.Zero,Vector3.One,
                out rayOrigin,out rayDirection)&&
                Math.Abs(rayDirection.X)<1e-5&&Math.Abs(rayDirection.Y)<1e-5&&
                rayDirection.Z<-.9999&&rayOrigin.Z<3&&rayOrigin.Z>2.97,
                "OpenGL画布中央射线须与System.Numerics透视矩阵一致朝向真实3D中心");
            Check(MPWVolumeOpenGlControl.TryUnprojectNormalizedRay(75,50,101,101,
                new Vector3(0,0,3),Vector3.Zero,Vector3.One,
                out rayOrigin,out rayDirection)&&rayDirection.X>0,
                "画布右侧像素射线须指向物理右侧而不是反向");
            Check(!MPWVolumeOpenGlControl.TryUnprojectNormalizedRay(-1,50,101,101,
                new Vector3(0,0,3),Vector3.Zero,Vector3.One,
                out rayOrigin,out rayDirection)&&
                !MPWVolumeOpenGlControl.TryUnprojectNormalizedRay(50,50,101,101,
                new Vector3(0,0,3),Vector3.Zero,new Vector3(1,0,1),
                out rayOrigin,out rayDirection),
                "拾取视口越界和退化模型缩放必须拒绝，不生成伪射线");
            FVOVolumeSpatialFrame frame=new FVOVolumeSpatialFrame(.5,1,2,10,20,30,
                new double[]{0,-1,0,1,0,0,0,0,1},true);
            var physical=new List<FVOPhysicalPoint3D>
            {
                frame.VoxelToPhysical(0,0,0),
                frame.VoxelToPhysical(5,10,15),
                frame.VoxelToPhysical(10,20,30)
            };
            var projected=MPWVolumeSurfaceProjection.Project(physical,frame,11,21,31);
            Check(projected.Count==3 &&
                Math.Abs(projected[0].X-(.5/11-.5))<.000001 &&
                Math.Abs(projected[0].Y-(.5/21-.5))<.000001 &&
                Math.Abs(projected[1].X)<.000001 &&
                Math.Abs(projected[1].Y)<.000001 &&
                Math.Abs(projected[2].Z-(30.5/31-.5))<.000001,
                "非等距/旋转的物理毫米Source经Frame逆变换投影到真实三维体空间");
            var realCloud=new FGEPointCloud3D(new[]{
                new FGEPoint3D(physical[0].X,physical[0].Y,physical[0].Z),
                new FGEPoint3D(physical[1].X,physical[1].Y,physical[1].Z),
                new FGEPoint3D(physical[2].X,physical[2].Y,physical[2].Z)
            });
            MPWVolumeDisplayPointCloud projectedCloud=
                MPWVolumeSurfaceProjection.ProjectPointCloud(realCloud,frame,11,21,31);
            Check(projectedCloud.SourceCount==3&&projectedCloud.Count==3&&
                projectedCloud.OriginalIndices[1]==1&&
                Math.Abs(projectedCloud.Points[0].X-projected[0].X)<1e-7&&
                Math.Abs(projectedCloud.Points[1].Y-projected[1].Y)<1e-7&&
                Math.Abs(projectedCloud.Points[2].Z-projected[2].Z)<1e-7,
                "同一旋转/非等距Frame下PointCloud与Surface必须落在相同GL/WPF空间且保留原值索引");
            bool invalidCloud=false;
            try{MPWVolumeSurfaceProjection.ProjectPointCloud(new FGEPointCloud3D(
                new[]{new FGEPoint3D(5000,5000,5000)}),frame,11,21,31);}
            catch(ArgumentOutOfRangeException){invalidCloud=true;}
            Check(invalidCloud,"PointCloud不同Frame或体外数据禁止误叠加到当前CT Volume");
            var roiInfo=new FVORawVolumeInfo(11,21,31,.5f,1f,2f,
                FVORawVoxelFormat.UInt16,"roi-source");
            using(FVOVolumeData roiVolume=FVORawVolumeLoader.LoadFromMemory(
                new byte[11*21*31*2],roiInfo,64,CancellationToken.None))
            {
                roiVolume.SetSpatialFrame(frame);
                var physicalRoi=FVOSliceImageBuilder.RectangleToPhysical(roiVolume,
                    5,10,15,FVOSlicePlane.XY,2,3,5,6);
                MPWVolumeDisplayMesh prism=MPWVolumeSurfaceProjection.ProjectSliceRoiPrism(
                    physicalRoi,-.2,.2,frame,11,21,31);
                Check(prism.TriangleCount==12&&prism.Vertices.Count==8&&
                    Math.Abs(prism.Vertices[4].Z-prism.Vertices[0].Z)>.00001,
                    "斜置毫米Frame的二维ROI厚度必须生成12真实三角面的可视棱柱");
                bool invalidRoi=false;
                try{MPWVolumeSurfaceProjection.ProjectSliceRoiPrism(
                    physicalRoi,1,0,frame,11,21,31);}
                catch(ArgumentOutOfRangeException){invalidRoi=true;}
                Check(invalidRoi,"非法ROI法向厚度范围必须拒绝而非产生假测量几何");
            }
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
            // 同一FVO物理Frame用于真实索引三角Mesh；角点[-.5, n-.5]必须映射GL体外框。
            var meshFge=new FGETriangleMesh3D(
                new[]{
                    frame.VoxelToPhysical(-.5,-.5,-.5),
                    frame.VoxelToPhysical(10.5,-.5,-.5),
                    frame.VoxelToPhysical(-.5,20.5,-.5)
                }.Select(p=>new FGEPoint3D(p.X,p.Y,p.Z)).ToArray(),
                new[]{0,1,2});
            MPWVolumeDisplayMesh meshView=MPWVolumeSurfaceProjection.ProjectMesh(
                meshFge,frame,11,21,31);
            Check(meshView.TriangleCount==1 &&
                Math.Abs(meshView.Vertices[0].X+.5)<1e-6 &&
                Math.Abs(meshView.Vertices[1].X-.5)<1e-6 &&
                Math.Abs(meshView.Vertices[2].Y-.5)<1e-6,
                "FGE三角Mesh的体素角点投影必须匹配原始物理GL外框");
            Check(gpu.GetMethod("SetSurfaceMesh")!=null &&
                gpu.GetMethod("ClearSurfaceMesh")!=null &&
                gpu.GetMethod("SetVolumeWithOriginalDimensions")!=null,
                "正式GPU必须支持Mesh三角层及独立的原始CT物理体积比例");
            bool overBudget=false;
            try {MPWVolumeSurfaceProjection.ProjectMesh(meshFge,frame,11,21,31,0);}
            catch(ArgumentOutOfRangeException){overBudget=true;}
            Check(overBudget,"预览三角面预算不合法必须拒绝");
            var detailedVertices=new List<FGEPoint3D>();
            var detailedIndices=new List<int>();
            for(int face=0;face<40;face++)
            {
                double x=face%8+.1,y=face/8+.1;
                foreach(FVOPhysicalPoint3D p in new[]{
                    frame.VoxelToPhysical(x,y,2),frame.VoxelToPhysical(x+.6,y,2),
                    frame.VoxelToPhysical(x,y+.6,2)})
                {
                    detailedIndices.Add(detailedVertices.Count);
                    detailedVertices.Add(new FGEPoint3D(p.X,p.Y,p.Z));
                }
            }
            var detailed=new FGETriangleMesh3D(detailedVertices,detailedIndices);
            MPWVolumeDisplayMesh previewLod=MPWVolumeSurfaceProjection.ProjectMeshPreviewLod(
                detailed,frame,11,21,31,7);
            Check(previewLod.TriangleCount<=7&&previewLod.TriangleCount>0&&
                detailed.TriangleCount==40&&
                previewLod.Vertices.Count<=21&&
                MPWVolumeSurfaceProjection.ProjectMeshPreviewLod(detailed,frame,11,21,31,7)
                    .TriangleIndices.SequenceEqual(previewLod.TriangleIndices),
                "仅显示LOD必须稳定限制三角面/顶点预算并保留原FGE的40个计量面片");


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
