using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using CxVisual.Mechanism.Core;
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
                Guid cadRef=scene3d.Add(MVI3DSceneKind.Cad,"cad:assembly:fixture",1,
                    "volume:physical-frame",.4);
                Guid maskRef=scene3d.Add(MVI3DSceneKind.Mask,"ct3d:mask:region",1,
                    "volume:physical-frame");
                scene3d.SetOrganization(cadRef,"reference","inspection");
                scene3d.SetOrganization(maskRef,"analysis","inspection");
                Check(scene3d.SnapshotLayer("reference").Count==1&&
                    scene3d.SnapshotLayer("analysis").Count==1&&
                    scene3d.SnapshotLayer("default").Count==2,
                    "CAD、Mask与Volume及Surface可按不同可视图层组织，共享已有来源引用");
                scene3d.Select(cadRef);
                long beforeGroup=scene3d.Revision;
                scene3d.SetGroupVisibility("inspection",false);
                Check(scene3d.Revision==beforeGroup+1&&scene3d.SelectedId==null&&
                    scene3d.SnapshotLayer("reference")[0].Opacity==.4&&
                    !scene3d.SnapshotLayer("analysis")[0].Visible,
                    "整组显隐单次通知且清理被隐藏的专业选择，保留透明度");
                scene3d.SetGroupVisibility("inspection",false);
                Check(scene3d.Revision==beforeGroup+1,"幂等整组显隐不能虚增场景Revision");
                scene3d.SetGroupVisibility("inspection",true);
                long beforeOpacity=scene3d.Revision;
                scene3d.SetGroupOpacity("inspection",.25);
                Check(scene3d.Revision==beforeOpacity+1&&
                    scene3d.SnapshotLayer("reference")[0].Opacity==.25&&
                    scene3d.SnapshotLayer("analysis")[0].Opacity==.25,
                    "多种三维对象组透明度必须批量设置且发布单次版本更新");
                scene3d.SetGroupOpacity("inspection",.25);
                Check(scene3d.Revision==beforeOpacity+1,"重复组透明度不得产生冗余场景通知");
                bool invalidGroupOpacity=false;
                try{scene3d.SetGroupOpacity("inspection",double.NaN);}
                catch(ArgumentOutOfRangeException){invalidGroupOpacity=true;}
                Check(invalidGroupOpacity&&scene3d.Revision==beforeOpacity+1,
                    "非法组透明度不得部分修改");
                bool invalidLayer=false;
                try{scene3d.SetOrganization(cadRef,"  ");}catch(ArgumentException){invalidLayer=true;}
                Check(invalidLayer&&scene3d.SnapshotLayer("reference").Count==1,
                    "非法图层更新不能损坏现有场景引用");
                Guid sourceSelection;
                Check(scene3d.TrySelectSource("cad:assembly:fixture",out sourceSelection)&&
                    sourceSelection==cadRef&&scene3d.SelectedId==cadRef,
                    "专业CAD资源引用可反向定位唯一可见Scene对象");
                Guid duplicateCad=scene3d.Add(MVI3DSceneKind.Cad,"cad:assembly:fixture",1);
                long beforeAmbiguity=scene3d.Revision;
                Check(!scene3d.TrySelectSource("cad:assembly:fixture",out sourceSelection)&&
                    scene3d.Revision==beforeAmbiguity&&scene3d.SelectedId==cadRef,
                    "同来源多实例必须明确消歧，不能误选或改变状态");
                scene3d.Remove(duplicateCad);
                scene3d.SetVisibility(cadRef,false);
                Check(!scene3d.TrySelectSource("cad:assembly:fixture",out sourceSelection),
                    "不可见CAD对象不能由资源引用选中");
                scene3d.SetVisibility(cadRef,true);
                Guid staleOne=scene3d.Add(MVI3DSceneKind.Measurement,
                    "ct3d:measurement:source",1,"frame:stable");
                Guid staleTwo=scene3d.Add(MVI3DSceneKind.Surface,
                    "ct3d:measurement:source",2,"frame:stable");
                Guid latest=scene3d.Add(MVI3DSceneKind.Measurement,
                    "ct3d:measurement:source",3,"frame:stable");
                scene3d.Select(staleTwo);
                long beforeInvalidation=scene3d.Revision;
                int invalidated=scene3d.RemoveOlderSourceRevisions(
                    "ct3d:measurement:source",3);
                MVI3DSceneItem currentItem;
                Check(invalidated==2&&scene3d.Revision==beforeInvalidation+1&&
                    scene3d.SelectedId==null&&scene3d.TryGet(latest,out currentItem)&&
                    !scene3d.TryGet(staleOne,out currentItem),
                    "来源Revision整体淘汰旧Surface/Measurement，保留最新实例且只通知一次");
                Check(scene3d.RemoveOlderSourceRevisions("ct3d:measurement:source",3)==0,
                    "重复来源版本失效不得产生冗余修改");
                scene3d.Remove(latest);
                scene3d.Remove(cadRef);
                scene3d.Remove(maskRef);
                scene3d.Remove(resultRef);
                Check(scene3d.Count==1,"移除专业结果仅解除Scene引用，不销毁资源");
                scene3d.Clear();
                Check(scene3d.Count==0 && scene3d.SelectedId==null,
                    "会话清空不能创建第二套持久工程定义");
                // B包：Scene引用/分组/显示/选择必须写入同一个MEM工程Visual Aspect。
                string layoutRoot=Path.Combine(Path.GetTempPath(),
                    "cxvisual-scene-layout-"+Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(layoutRoot);
                string projectFile=Path.Combine(layoutRoot,"visual.cxproj");
                var layout=new MVI3DSceneState();
                Guid meshVisual=layout.Add(MVI3DSceneKind.Mesh,"ct3d:mesh:verified",4,"frame:1",.6);
                Guid cadVisual=layout.Add(MVI3DSceneKind.Cad,"cad:solid:verified",3,"frame:1",.3);
                layout.SetOrganization(meshVisual,"analysis","inspection");
                layout.SetOrganization(cadVisual,"reference","inspection");
                layout.Select(cadVisual);
                MRIObjectAddress layoutAddress;
                using(var engineering=MEMEngineering.Create("三维可视工程"))
                {
                    var owner=engineering.AddPage("Scene").RootContent;
                    int before=engineering.Revision;
                    layoutAddress=MVI3DSceneLayoutPersistence.Commit(engineering,owner,
                        before,layout,CancellationToken.None);
                    Check(engineering.Revision==before+1,"Scene保存必须使用唯一MEM事务");
                    bool staleRejected=false;
                    try{MVI3DSceneLayoutPersistence.Commit(engineering,owner,before,
                        layout,CancellationToken.None);}
                    catch(InvalidOperationException){staleRejected=true;}
                    Check(staleRejected,"Scene过期工程Revision必须拒绝覆盖");
                    engineering.SaveAs(projectFile);
                }
                using(var engineering=MEMEngineering.Open(projectFile))
                {
                    var restoredScene=new MVI3DSceneState();
                    MVI3DSceneLayoutPersistence.Open(engineering,layoutAddress,restoredScene,
                        item=>item.SourceRef=="ct3d:mesh:verified"||
                            item.SourceRef=="cad:solid:verified",CancellationToken.None);
                    MVI3DSceneItem restoredMesh,restoredCad;
                    Check(restoredScene.Count==2&&restoredScene.SelectedId==cadVisual&&
                        restoredScene.TryGet(meshVisual,out restoredMesh)&&
                        restoredScene.TryGet(cadVisual,out restoredCad)&&
                        restoredMesh.LayerKey=="analysis"&&restoredMesh.GroupKey=="inspection"&&
                        restoredMesh.Opacity==.6&&restoredCad.Opacity==.3,
                        "MEM重开恢复多对象稳定ID、分组、透明度与专业选择");
                    long unchanged=restoredScene.Revision;
                    bool invalidRefRejected=false;
                    try{MVI3DSceneLayoutPersistence.Open(engineering,layoutAddress,
                        restoredScene,item=>false,CancellationToken.None);}
                    catch(InvalidDataException){invalidRefRejected=true;}
                    Check(invalidRefRejected&&restoredScene.Revision==unchanged&&
                        restoredScene.SelectedId==cadVisual,
                        "来源不可解析必须拒绝整批恢复而不污染现有Scene");
                    bool cancelled=false;
                    try{MVI3DSceneLayoutPersistence.Open(engineering,layoutAddress,
                        restoredScene,item=>true,new CancellationToken(true));}
                    catch(OperationCanceledException){cancelled=true;}
                    Check(cancelled&&restoredScene.Revision==unchanged,
                        "Scene取消恢复不能修改当前会话");
                }
                Directory.Delete(layoutRoot,true);
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
                using(FVOVolumeData calibrated=FVOVolumeData.CreateDemo(8,10,12))
                {
                    var frame=new FVOVolumeSpatialFrame(.5,.75,1.2,10,20,30,
                        new double[]{0,-1,0,1,0,0,0,0,1},true);
                    calibrated.SetSpatialFrame(frame);
                    var roi=FVOSliceImageBuilder.RectangleToPhysical(calibrated,3,4,5,
                        FVOSlicePlane.XY,0,0,
                        FVOSliceImageBuilder.GetImageWidth(calibrated,FVOSlicePlane.XY)-1,
                        FVOSliceImageBuilder.GetImageHeight(calibrated,FVOSlicePlane.XY)-1);
                    Check(roi.Unit=="mm"&&Object.ReferenceEquals(roi.SpatialFrame,frame)&&
                        Math.Abs(roi.TopLeft.X-10)<1e-9&&
                        Math.Abs(roi.TopLeft.Y-20)<1e-9&&
                        Math.Abs(roi.TopLeft.Z-36)<1e-9&&
                        Math.Abs(roi.BottomRight.Z-roi.TopLeft.Z)<1e-9,
                        "XY显示ROI须经过旋转/非各向同性Frame准确映射为毫米空间四角");
                    int vx,vy,vz;
                    FVOSliceImageBuilder.ImageToVoxel(calibrated,3,4,5,FVOSlicePlane.XY,
                        roi.Right,roi.Bottom,out vx,out vy,out vz);
                    FVOPhysicalPoint3D corner=frame.VoxelToPhysical(vx,vy,vz);
                    Check(Math.Abs(corner.X-roi.BottomRight.X)<1e-9&&
                        Math.Abs(corner.Y-roi.BottomRight.Y)<1e-9,
                        "ROI右下点须对应当前切片实际采样的原始Voxel而非屏幕近似值");
                    bool boundsRejected=false;
                    try{FVOSliceImageBuilder.RectangleToPhysical(calibrated,3,4,5,
                        FVOSlicePlane.XY,0,0,100000,5);}
                    catch(ArgumentOutOfRangeException){boundsRejected=true;}
                    Check(boundsRejected,"二维ROI超出可视像素域必须拒绝");
                }
                using(FVOVolumeData uncalibrated=FVOVolumeData.CreateDemo(8,10,12))
                {
                    bool rejected=false;
                    try{FVOSliceImageBuilder.RectangleToPhysical(uncalibrated,0,0,0,
                        FVOSlicePlane.YZ,0,0,1,1);}
                    catch(InvalidOperationException){rejected=true;}
                    Check(rejected,"未标定的二维ROI不得冒充毫米几何");
                }
                Console.WriteLine("PASS MVI single volume view state and 4-view synchronization contracts");
            }
        }
        private static void Check(bool success, string name)
        {
            if (!success) throw new InvalidOperationException("MVI体数据测试失败：" + name);
        }
    }
}
