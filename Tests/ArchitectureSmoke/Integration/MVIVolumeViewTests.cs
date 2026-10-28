using System;
using System.ComponentModel;
using CxVisual.Foundation.Volume;
using CxVisual.Mechanism.Visual;

namespace CxVisual.Tests.ArchitectureSmoke
{
    /// <summary>四视图/三刀只使用一个会话状态；不依赖WPF或旧WinForms。</summary>
    internal static class MVIVolumeViewTests
    {
        public static void Run()
        {
            using (FVOVolumeData data = FVOVolumeData.CreateDemo(8, 10, 12))
            {
                MVIVolumeViewState state = MVIVolumeViewState.CreateCentered(data);
                Check(state.SliceX == 4 && state.SliceY == 5 && state.SliceZ == 6, "初始中心位置");
                int changes = 0;
                state.PropertyChanged += delegate(object sender, PropertyChangedEventArgs e) { changes++; };
                state.SetSlices(999, -10, 3, data);
                Check(state.SliceX == 7 && state.SliceY == 0 && state.SliceZ == 3, "越界三刀位置钳制");
                Check(changes == 1, "三刀一次更新一个事件");
                state.SetSliceViewport(FVOSlicePlane.XY, 0.7f, 0.2f, -0.5f, 1.2f);
                MVISliceViewport viewport = state.GetSliceViewport(FVOSlicePlane.XY);
                Check(Math.Abs(viewport.MinA - 0.2f) < 0.0001 && viewport.MaxB > 1, "任意视口映射不截断");
                FVOObliqueSliceDefinition oblique = new FVOObliqueSliceDefinition();
                oblique.AngleZDegrees = 45;
                state.SetObliqueSlice(oblique);
                oblique.AngleZDegrees = 0;
                Check(state.ObliqueSlice.AngleZDegrees == 45, "独立复制斜切定义");
                Check(state.GetSliceViewport(FVOSlicePlane.XZ).MinA == 0, "视口互不污染");
                Check(state.ShowCrosshair, "默认可见十字线");
                // 统一三维Scene是资产/专业结果的只读投影，不是MEM工程资源的副本。
                MVI3DSceneState scene3d=state.Scene3D;
                int sceneEvents=0;
                scene3d.Changed+=(sender,args)=>sceneEvents++;
                Guid volumeRef=scene3d.Add(MVI3DSceneKind.Volume,
                    "session:volume:one",1,"volume:physical-frame");
                Guid resultRef=scene3d.Add(MVI3DSceneKind.Surface,
                    "ct3d:surface:result-001",2,"volume:physical-frame",.7);
                Check(scene3d.Count==2 && scene3d.Revision==2 && sceneEvents==2,
                    "同一会话可引用多个专业对象但不复制数据与资源定义");
                var beforeStyle=scene3d.Snapshot();
                scene3d.SetOpacity(resultRef,.5);
                scene3d.SetOpacity(resultRef,.5);
                MVI3DSceneItem updated=default(MVI3DSceneItem);
                Check(scene3d.Revision==3 && scene3d.TryGet(resultRef,out updated) &&
                      updated.Opacity==.5 && beforeStyle[1].Opacity==.7,
                    "可视快照不可变、重复属性设置不得重复提升Revision");
                scene3d.Select(resultRef);
                scene3d.SetVisibility(resultRef,false);
                Check(scene3d.SelectedId==null && scene3d.Revision==5 &&
                      updated.Visible,
                    "隐藏被选对象必须清除选择且旧快照保持不变");
                bool hiddenRejected=false;
                try {scene3d.Select(resultRef);}
                catch(InvalidOperationException){hiddenRejected=true;}
                Check(hiddenRejected,"隐藏对象必须拒绝拾取选择");
                bool emptyRefRejected=false;
                try {scene3d.Add(MVI3DSceneKind.Cad,"",0);}
                catch(ArgumentException){emptyRefRejected=true;}
                Check(emptyRefRejected,"Scene引用不能没有现存专业资源身份");
                bool invalidOpacity=false;
                try {scene3d.SetOpacity(volumeRef,double.NaN);}
                catch(ArgumentOutOfRangeException){invalidOpacity=true;}
                Check(invalidOpacity && scene3d.Revision==5,
                    "非法透明度必须整体拒绝而不改变状态");
                scene3d.Remove(resultRef);
                Check(scene3d.Count==1,"移除专业结果仅解除Scene引用，不销毁资源");
                scene3d.Clear();
                Check(scene3d.Count==0 && scene3d.SelectedId==null,
                    "会话清空不能创建第二套持久工程定义");
                MVI3DCameraState camera=state.Camera;
                long cameraRevision=camera.Revision;
                int cameraEvents=0;
                camera.Changed+=(s,e)=>cameraEvents++;
                camera.SetPose(camera.YawRadians,camera.PitchRadians,camera.Distance,
                    camera.TargetX,camera.TargetY,camera.TargetZ);
                Check(camera.Revision==cameraRevision && cameraEvents==0,
                    "相同姿态零事件/零版本更新");
                camera.Orbit(.25,-.1);
                camera.Zoom(2);
                camera.Pan(13,-8,480);
                Check(camera.Revision==cameraRevision+3 && cameraEvents==3 &&
                    camera.Distance<2.9 && camera.TargetX!=0,
                    "MVI相机唯一Revision与轨道旋转/缩放/平移联动");
                bool rejectedCamera=false;
                try {camera.SetPose(double.NaN,0,2,0,0,0);}
                catch(ArgumentOutOfRangeException){rejectedCamera=true;}
                Check(rejectedCamera && camera.Revision==cameraRevision+3,
                    "非法相机输入必须整体拒绝且不能提升版本");
                camera.Reset();
                Check(camera.Distance==2.9 && camera.TargetX==0,
                    "统一相机复位必须恢复完整姿态");
                // MVI是滚轮/图像定位唯一Owner；不能由TestCenter窗体另写一套三刀算法。
                state.SetSlices(3,4,5,data);
                state.ScrollSlice(FVOSlicePlane.XY,100,data);
                Check(state.SliceX==3 && state.SliceY==4 && state.SliceZ==11,
                    "XY滚轮只能改变Z轴并钳制深度上界");
                state.ScrollSlice(FVOSlicePlane.XZ,-100,data);
                Check(state.SliceY==0 && state.SliceX==3 && state.SliceZ==11,
                    "XZ滚轮只能改变Y轴并钳制下界");
                state.ScrollSlice(FVOSlicePlane.YZ,2,data);
                Check(state.SliceX==5 && state.SliceY==0 && state.SliceZ==11,
                    "YZ滚轮应只移动X轴");
                int xPixel,yPixel;
                FVOSliceImageBuilder.VoxelToImage(data,FVOSlicePlane.XY,2,7,6,
                    out xPixel,out yPixel);
                state.SetSlicesFromImage(FVOSlicePlane.XY,xPixel,yPixel,data);
                Check(state.SliceX==2 && state.SliceY==7 && state.SliceZ==11,
                    "图像像素经FVO反映射定位MVI三刀且保持另一轴");
                bool invalidPlane=false;
                try { state.ScrollSlice((FVOSlicePlane)99,1,data); }
                catch(ArgumentOutOfRangeException) { invalidPlane=true; }
                Check(invalidPlane,"非法切面不能隐式修改三刀状态");
                Console.WriteLine("PASS MVI single volume view state and 4-view synchronization contracts");
            }
        }
        private static void Check(bool success, string name)
        {
            if (!success) throw new InvalidOperationException("MVI体数据测试失败：" + name);
        }
    }
}
