using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CxVisual.Foundation.Volume;
using CxVisual.Mechanism.Visual;

namespace CxVisual.TestCenter.UI
{
    /// <summary>在真实Windows WPF窗口中执行四视图/三刀/三维切片及PNG可视回归，不冒充GPU体绘。</summary>
    internal static class TCTVolumeViewerSmoke
    {
        public static void Run()
        {
            TCTManualVolumeViewerFrame frame = new TCTManualVolumeViewerFrame();
            Window window = new Window {
                Title = "CxVisual 3D Volume Workbench Acceptance",
                Width = 1280, Height = 850,
                WindowStyle = WindowStyle.ToolWindow, ShowInTaskbar = false,
                Content = frame
            };
            try
            {
                window.Show();
                window.UpdateLayout();
                // 命令中心/顶部按钮使用同一操作入口，不能只注册原来的3个命令。
                List<TCTWorkspaceAction> actions=new List<TCTWorkspaceAction>(frame.Commands);
                HashSet<string> unique=new HashSet<string>();
                foreach(TCTWorkspaceAction command in actions)
                    Check(unique.Add(command.Id) && command.Execute!=null &&
                        command.CanExecute!=null,"CT3D工作区命令须有唯一ID及可执行入口");
                Check(actions.Count>=20,
                    "旧版RAW/BMP/四视图/渲染/任意切操作均需公开到工作区命令");
                TCTWorkspaceAction clearCommand=actions.Find(c=>c.Id=="ct3d.clear");
                TCTWorkspaceAction cancelCommand=actions.Find(c=>c.Id=="ct3d.load.cancel");
                Button cancelButton=FindVisualByAutomationId<Button>(frame,"TCT.Volume.CancelLoad");
                Check(cancelCommand!=null && !cancelCommand.CanExecute() &&
                      cancelButton!=null && !cancelButton.IsEnabled,
                    "空闲时取消加载按钮与工作区命令必须禁用");
                TCTWorkspaceAction windowCommand=actions.Find(c=>c.Id=="ct3d.window");
                TCTWorkspaceAction zoomCommand=actions.Find(c=>c.Id=="ct3d.zoomIn");
                Check(clearCommand!=null && !clearCommand.CanExecute() &&
                    windowCommand!=null && !windowCommand.CanExecute(),
                    "未加载时危险的窗口操作必须禁止执行");
                string emptyImage=Path.Combine(Path.GetTempPath(),
                    "CxVisual-Reject-Empty-"+Guid.NewGuid().ToString("N")+".png");
                bool refusedEmpty=false;
                try { frame.SaveFourViewScreenshot(emptyImage); }
                catch(InvalidOperationException) { refusedEmpty=true; }
                Check(refusedEmpty && !File.Exists(emptyImage),
                    "未载入体数据时必须拒绝四视图PNG，不能输出空白截图");
                frame.LoadDemo();
                window.UpdateLayout();
                frame.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                    new Action(delegate { }));
                Check(clearCommand.CanExecute() && windowCommand.CanExecute() &&
                    zoomCommand.CanExecute(),"装载体数据后工作区命令应该可执行");
                double beforeCommandZoom=frame.CameraDistanceForTest;
                zoomCommand.Execute();
                Check(frame.CameraDistanceForTest<beforeCommandZoom,
                    "CT3D工作区缩放命令必须调用真实GPU/WPF相机");
                frame.ResetCameraForTest();
                Check(frame.HasAttemptedGpuRendering,
                    "默认加载必须真实尝试GPU Raymarch而不能直接停留WPF切面");
                Check(frame.IsGpuVolumeRendering || frame.GpuStatusText.Contains("GPU不可用"),
                    "GPU成功或带具体原因的WPF回退，不能静默伪报GPU成功");
                Check(frame.LegacyTopTabCount==4 && frame.LegacyBottomTabCount==5,
                    "原版上下4/5页签完整恢复");
                var settingsDock=FindVisualByAutomationId<
                    CxVisual.Mechanism.Presentation.Wpf.Volume.MPWVolumeSettingsDockControl>(
                    frame,"MPW.Volume.SettingsDock");
                TabControl upper=FindVisualByAutomationId<TabControl>(frame,
                    "MPW.Volume.Settings.UpperTabs");
                TabControl lower=FindVisualByAutomationId<TabControl>(frame,
                    "MPW.Volume.Settings.LowerTabs");
                Check(settingsDock!=null && upper!=null && lower!=null &&
                      Object.ReferenceEquals(settingsDock.UpperTabs,upper) &&
                      Object.ReferenceEquals(settingsDock.LowerTabs,lower) &&
                      settingsDock.UpperCount==4 && settingsDock.LowerCount==5,
                    "正式MPW双层设置面板必须承载真实4/5页签，不保留TestCenter布局副本");
                for(int i=0;i<upper.Items.Count;i++)
                {
                    upper.SelectedIndex=i;
                    Check(upper.SelectedContent!=null,
                        "上层显示/灰阶/渲染/任意切各页必须有真实可显示内容");
                }
                for(int i=0;i<lower.Items.Count;i++)
                {
                    lower.SelectedIndex=i;
                    Check(lower.SelectedContent!=null,
                        "阈值/颜色/标量/梯度/任意切各页不能是空占位");
                }
                upper.SelectedIndex=0;lower.SelectedIndex=0;
                // 独立公共设置面板不依赖TestCenter或CT3D渲染器。
                var standaloneDock=new CxVisual.Mechanism.Presentation.Wpf.Volume.
                    MPWVolumeSettingsDockControl();
                var sharedEditor=new StackPanel();
                standaloneDock.AddUpperTab("显示",sharedEditor);
                var transferEditor=new StackPanel();
                standaloneDock.AddLowerTab("透明度",transferEditor);
                bool duplicateDockRejected=false;
                try { standaloneDock.AddUpperTab("重复",sharedEditor); }
                catch(InvalidOperationException) { duplicateDockRejected=true; }
                Check(standaloneDock.UpperCount==1 && standaloneDock.LowerCount==1 &&
                      standaloneDock.UpperTabs.SelectedIndex==0 &&
                      standaloneDock.LowerTabs.SelectedIndex==0 && duplicateDockRejected,
                    "MPW公共参数面板独立创建、默认选中并拒绝重复父级，保持编辑器身份");

                double initialCameraDistance=frame.CameraDistanceForTest;
                frame.Zoom3D(2);
                Check(frame.CameraDistanceForTest<initialCameraDistance,
                    "3D放大按钮必须真正改变GPU/WPF相机而非空动作");
                frame.Zoom3D(-2);
                Check(Math.Abs(frame.CameraDistanceForTest-initialCameraDistance)<.001,
                    "3D缩小必须恢复到原相机距离");
                frame.Pan3D(50,25);
                Check(frame.CameraTargetLengthForTest>.001,
                    "3D平移按钮必须真正改变相机注视点");
                frame.Orbit3D(25,15);
                frame.ResetCameraForTest();
                Check(frame.CameraTargetLengthForTest<.0001,
                    "3D相机复位必须清除平移");
                Check(frame.HeaderSliceControlCount==3,
                    "旧版XY/YZ/XZ三图窗均有独立跳转滑杆和索引输入");
                foreach(FVOSlicePlane plane in new[]{
                    FVOSlicePlane.XY,FVOSlicePlane.XZ,FVOSlicePlane.YZ})
                {
                    var navigation=FindVisualByAutomationId<
                        CxVisual.Mechanism.Presentation.Wpf.Volume.MPWVolumeSliceNavigationControl>(
                            frame,"MPW.Volume.Navigation."+plane);
                    Check(navigation!=null &&
                          Object.ReferenceEquals(navigation.Volume,frame.Volume) &&
                          Object.ReferenceEquals(navigation.ViewState,frame.ViewState) &&
                          navigation.SliceSlider.IsEnabled &&
                          navigation.SliceIndexBox.IsEnabled,
                        "正式MPW切片导航必须直接绑定唯一FVO/MVI且三个视口均可调用");
                }
                Check(FindVisualByAutomationId<
                    CxVisual.Mechanism.Presentation.Wpf.Volume.MPWVolumeCameraNavigationControl>(
                    frame,"MPW.Volume.Navigation.Camera")!=null,
                    "三维场景相机导航须由MPW正式公共控件实现");
                frame.JumpToViewSliceForTest(FVOSlicePlane.XY,12);
                frame.JumpToViewSliceForTest(FVOSlicePlane.XZ,13);
                frame.JumpToViewSliceForTest(FVOSlicePlane.YZ,14);
                var xyNavigation=FindVisualByAutomationId<
                    CxVisual.Mechanism.Presentation.Wpf.Volume.MPWVolumeSliceNavigationControl>(
                        frame,"MPW.Volume.Navigation.XY");
                Check(xyNavigation!=null && xyNavigation.SliceIndexBox.Text=="12",
                    "导航数值由MVI真实三刀同步，不能保留独立副本");
                xyNavigation.SliceIndexBox.Text="17";
                xyNavigation.SliceIndexBox.RaiseEvent(new System.Windows.Input.KeyEventArgs(
                    System.Windows.Input.Keyboard.PrimaryDevice,
                    System.Windows.PresentationSource.FromVisual(xyNavigation),
                    0,System.Windows.Input.Key.Enter) {
                    RoutedEvent=System.Windows.Input.Keyboard.KeyDownEvent
                });
                Check(frame.ViewState.SliceZ==17,
                    "MPW索引输入回车应通过正式导航API写回MVI");
                xyNavigation.SliceIndexBox.Text="not-an-index";
                xyNavigation.SliceIndexBox.RaiseEvent(new System.Windows.Input.KeyEventArgs(
                    System.Windows.Input.Keyboard.PrimaryDevice,
                    System.Windows.PresentationSource.FromVisual(xyNavigation),
                    0,System.Windows.Input.Key.Enter) {
                    RoutedEvent=System.Windows.Input.Keyboard.KeyDownEvent
                });
                Check(frame.ViewState.SliceZ==17 &&
                      xyNavigation.LastValidationError!=null &&
                      xyNavigation.LastValidationError.Contains("整数"),
                    "非法索引输入必须拒绝修改原始MVI状态并给出反馈");
                xyNavigation.SliceSlider.Value=12;
                Check(frame.ViewState.SliceZ==12,
                    "滑杆恢复须写回共享三刀状态");
                Check(frame.ViewState.SliceZ==12 && frame.ViewState.SliceY==13 &&
                    frame.ViewState.SliceX==14,
                    "三图窗跳转必须直接修改共享MVI三刀而不是独立索引");
                Button centerButton=FindVisualByAutomationId<Button>(frame,
                    "TCT.Volume.Center");
                Button resetButton=FindVisualByAutomationId<Button>(frame,
                    "TCT.Volume.ResetCamera");
                Button applyWindowButton=FindVisualByAutomationId<Button>(frame,
                    "TCT.Volume.ApplyWindow");
                Check(centerButton!=null && resetButton!=null && applyWindowButton!=null,
                    "真实TopBar必须有三刀居中、相机复位、窗宽窗位动作控件");
                centerButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent,centerButton));
                Check(frame.ViewState.SliceX==frame.Volume.OriginalWidth/2 &&
                    frame.ViewState.SliceY==frame.Volume.OriginalHeight/2 &&
                    frame.ViewState.SliceZ==frame.Volume.OriginalDepth/2,
                    "顶部三刀按钮Click必须执行真正FVO/MVI居中操作");
                frame.Pan3D(35,20);
                resetButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent,resetButton));
                Check(frame.CameraTargetLengthForTest<.0001,
                    "顶部相机复位按钮Click必须恢复真实3D相机");
                var workbench=FindVisualByAutomationId<
                    CxVisual.Mechanism.Presentation.Wpf.Volume.MPWVolumeFourViewWorkbenchControl>(
                    frame,"MPW.Volume.FourViewWorkbench");
                Check(workbench!=null && workbench.SliceViewportCount==3 &&
                      workbench.SliceNavigationCount==3 &&
                      Object.ReferenceEquals(workbench.GetViewport(FVOSlicePlane.XY).Viewer,
                          frame.XyViewer) &&
                      Object.ReferenceEquals(workbench.GetViewport(FVOSlicePlane.YZ).Viewer,
                          frame.YzViewer) &&
                      Object.ReferenceEquals(workbench.GetViewport(FVOSlicePlane.XZ).Viewer,
                          frame.XzViewer) &&
                      Object.ReferenceEquals(workbench.GetViewport(FVOSlicePlane.XY).Volume,
                          frame.Volume) &&
                      Object.ReferenceEquals(workbench.GetNavigation(FVOSlicePlane.XZ).ViewState,
                          frame.ViewState),
                    "正式MPW四视图工作区必须统一装配既有Viewer并绑定FVO/MVI唯一真源");
                var fourView=FindVisualByAutomationId<
                    CxVisual.Mechanism.Presentation.Wpf.Volume.MPWVolumeFourViewLayoutControl>(
                    frame,"MPW.Volume.FourView");
                Check(fourView!=null && fourView.ViewCount==4 &&
                      fourView.GetSlot(0).Children.Count>=2,
                    "四视图布局须归MPW，直接复用已有Viewer实例");
                var slots=new System.Windows.Controls.Grid[4];
                for(int n=0;n<4;n++)slots[n]=fourView.GetSlot(n);
                frame.ToggleViewForTest(0);
                Check(frame.IsLegacySingleView && fourView.IsSingleView &&
                      System.Windows.Controls.Grid.GetRowSpan(slots[0])==2 &&
                      slots[1].Visibility==Visibility.Collapsed,
                    "MPW左上角最大化应仅操作现有布局");
                frame.ToggleViewForTest(0);
                Check(!frame.IsLegacySingleView &&
                      slots[1].Visibility==Visibility.Visible,
                    "MPW单窗可原位恢复2x2");
                for(int n=0;n<4;n++)
                {
                    Check(Object.ReferenceEquals(slots[n],fourView.GetSlot(n)) &&
                          System.Windows.Controls.Grid.GetRow(slots[n])==n/2 &&
                          System.Windows.Controls.Grid.GetColumn(slots[n])==n%2,
                        "恢复后四个原控件位置和实例必须保持");
                }
                for(int n=1;n<4;n++)
                {
                    frame.ToggleViewForTest(n);
                    Check(fourView.IsSingleView &&
                          Object.ReferenceEquals(fourView.MaximizedSlot,slots[n]),
                        "四视图每个视口均支持最大化");
                    frame.ToggleViewForTest(n);
                    Check(!fourView.IsSingleView &&
                          Object.ReferenceEquals(fourView.GetSlot(n),slots[n]),
                        "四视图还原不能重建二维/三维控件");
                }
                var independent=new CxVisual.Mechanism.Presentation.Wpf.Volume.
                    MPWVolumeFourViewLayoutControl();
                independent.AddView(new System.Windows.Controls.Border(),"测试",0,0);
                // 单独创建MPW四视图工作区，不依赖TestCenter frame或专业CT3D。
                var reusable=new CxVisual.Mechanism.Presentation.Wpf.Volume.
                    MPWVolumeFourViewWorkbenchControl(
                    new CxVisual.Mechanism.Presentation.Wpf.Imaging.MPWImageViewerControl(),
                    new CxVisual.Mechanism.Presentation.Wpf.Imaging.MPWImageViewerControl(),
                    new CxVisual.Mechanism.Presentation.Wpf.Imaging.MPWImageViewerControl(),
                    new System.Windows.Controls.Grid(),()=>{},step=>{},(dx,dy)=>{});
                Check(reusable.Layout.ViewCount==4 &&
                      reusable.GetNavigation(FVOSlicePlane.XY).Volume==null,
                    "MPW独立创建完整2x2四视图并默认无数据，不依赖测试页面");
                using(var example=FVOVolumeData.CreateDemo(8,8,8))
                {
                    var shared=CxVisual.Mechanism.Visual.MVIVolumeViewState.CreateCentered(example);
                    reusable.Bind(example,shared);
                    Check(Object.ReferenceEquals(reusable.GetViewport(FVOSlicePlane.YZ).ViewState,
                          reusable.GetNavigation(FVOSlicePlane.XY).ViewState) &&
                          Object.ReferenceEquals(reusable.GetViewport(FVOSlicePlane.XZ).Volume,example),
                        "MPW单次绑定应同步贯穿所有导航和视口");
                    reusable.Bind(null,null);
                    Check(reusable.GetViewport(FVOSlicePlane.YZ).Volume==null &&
                          reusable.GetNavigation(FVOSlicePlane.XY).ViewState==null &&
                          !reusable.GetNavigation(FVOSlicePlane.XY).SliceSlider.IsEnabled,
                        "MPW解绑必须清除所有子控件的已释放体数据引用");
                }
                bool duplicateRejected=false;
                try { independent.AddView(new System.Windows.Controls.Border(),"重复",0,0); }
                catch(InvalidOperationException) { duplicateRejected=true; }
                Check(duplicateRejected && independent.ViewCount==1,
                    "MPW开发者API应拒绝重复占用同一位置");
                frame.SetWindowWidthForTest("非法数值");
                applyWindowButton.RaiseEvent(new RoutedEventArgs(
                    Button.ClickEvent,applyWindowButton));
                Check(!String.IsNullOrEmpty(frame.LastUiError),
                    "真实顶部窗宽按钮Click应捕获非法输入并给用户错误反馈");
                windowCommand.Execute();
                Check(!String.IsNullOrEmpty(frame.LastUiError),
                    "命令输入非法时不得造成未处理UI异常，须回报操作失败");
                frame.SetWindowWidthForTest("256");
                windowCommand.Execute();
                Check(String.IsNullOrEmpty(frame.LastUiError),
                    "修改有效窗宽后工作区命令必须恢复可用");
                frame.ApplyWindowCenterWidth();
                frame.SetOpacityFromPanelForTest(.45);
                Check(Math.Abs(frame.ConfiguredOpacityForTest-.45f)<.001,
                    "体透明度滑杆ValueChanged须更新实际CT3D渲染配置");
                frame.SetSliceVisibilityFromPanelForTest(true);
                Check(frame.ConfiguredSliceVisibilityForTest,
                    "三维定位切面勾选必须更新真实渲染配置");
                frame.SetSliceVisibilityFromPanelForTest(false);
                Check(!frame.ConfiguredSliceVisibilityForTest,
                    "三维定位切面取消勾选必须真实隐藏");
                frame.SetColorCurveEnabledFromPanelForTest(true);
                Check(frame.ConfiguredColorForTest,
                    "颜色曲线勾选必须更新FVO传递函数状态");
                frame.SetColorCurveEnabledFromPanelForTest(false);
                frame.SetThresholdRangeForTest(.22f,.78f);
                Check(Math.Abs(frame.CurrentThresholdLower-.22f)<.001 &&
                    Math.Abs(frame.CurrentThresholdUpper-.78f)<.001,
                    "旧版阈值上下界拖拽必须回写FVO并触发CT3D LUT");
                Check(typeof(CxVisual.Mechanism.Presentation.Wpf.Volume.
                    MPWVolumeThresholdEditor).Assembly ==
                    typeof(CxVisual.Mechanism.Presentation.Wpf.Volume.
                    MPWVolumeSliceSceneControl).Assembly,
                    "阈值双端WPF控件的真实Owner必须为MPW公共控件库而非TestCenter");
                Check(frame.Volume != null && frame.ViewState != null,"真实FVO与MVI状态绑定");
                Check(frame.XyViewer.ActiveImage != null &&
                      frame.XzViewer.ActiveImage != null &&
                      frame.YzViewer.ActiveImage != null, "三个MPW WPF图窗均须实际显示图像");
                frame.XyViewer.Zoom(1.75);
                // MPW公共叠加控件负责真实视口反算，MVI唯一持有三刀与可视区状态。
                var visibleOverlay=FindVisualByAutomationId<
                    CxVisual.Mechanism.Presentation.Wpf.Volume.MPWVolumeSliceOverlayControl>(
                    frame,"MPW.Volume.SliceOverlay.XY");
                Check(visibleOverlay!=null && visibleOverlay.SyncVisibleViewport(
                        frame.XyViewer,frame.ViewState),
                    "MPW必须在实际WPF视口中完成坐标反算并写入MVI");
                var visibleRange=frame.ViewState.GetSliceViewport(FVOSlicePlane.XY);
                Check(visibleRange.MaxA>visibleRange.MinA &&
                      visibleRange.MaxB>visibleRange.MinB &&
                      visibleRange.MaxA-visibleRange.MinA<1.01f,
                    "缩放后可视范围不应继续使用原始全视口伪数据");
                double beforeSliceScale=frame.XyViewer.PrimaryViewportScale;
                frame.ScrollSlice(FVOSlicePlane.XY,1);
                Check(Math.Abs(frame.XyViewer.PrimaryViewportScale-beforeSliceScale)<.001,
                    "真实WPF二维换层不得丢失用户缩放与平移状态");
                frame.XyViewer.FitToWindow();
                for(int view=0;view<4;view++)
                {
                    string viewPath=Path.Combine(Path.GetTempPath(),
                        "CxVisual-Volume-Selected-"+Guid.NewGuid().ToString("N")+".png");
                    try
                    {
                        frame.SaveSelectedViewPng(viewPath,view);
                        Check(File.Exists(viewPath)&&new FileInfo(viewPath).Length>512,
                            "旧版当前视图XY/YZ/XZ/3D独立PNG输出应有效");
                    }
                    finally {if(File.Exists(viewPath))File.Delete(viewPath);}
                }
                frame.UpdateCrosshairs();
                Check(frame.VisibleCrosshairCount == 6, "三个二维图窗必须显示六条十字线");
                var mpwViewport=FindVisualByAutomationId<
                    CxVisual.Mechanism.Presentation.Wpf.Volume.MPWVolumeSliceViewportControl>(
                    frame,"MPW.Volume.SliceViewport.XY");
                Check(mpwViewport!=null &&
                      Object.ReferenceEquals(mpwViewport.Volume,frame.Volume) &&
                      Object.ReferenceEquals(mpwViewport.ViewState,frame.ViewState) &&
                      Object.ReferenceEquals(mpwViewport.Viewer,frame.XyViewer),
                    "正式MPW交互视口必须绑定FVO/MVI与既有二维Viewer，而非TestCenter状态副本");
                int beforeInputZ=frame.ViewState.SliceZ;
                Check(mpwViewport.HandleWheel(120,System.Windows.Input.ModifierKeys.Shift) &&
                      frame.ViewState.SliceZ==Math.Min(frame.Volume.OriginalDepth-1,beforeInputZ+5),
                    "MPW正交交互控件真实滚轮Shift应调用MVI唯一三刀状态");
                Check(mpwViewport.PositionFromImagePixel(6,9) &&
                      frame.ViewState.SliceX==6 && frame.ViewState.SliceY==9,
                    "MPW点击坐标应经FVO真实像素反映射更新MVI");
                frame.Center();
                // 叠加图元与空间映射实现已归MPW；测试宿主只绑定FVO/MVI和操作。
                foreach(FVOSlicePlane plane in new[] {
                    FVOSlicePlane.XY,FVOSlicePlane.XZ,FVOSlicePlane.YZ })
                {
                    var overlay=FindVisualByAutomationId<
                        CxVisual.Mechanism.Presentation.Wpf.Volume.MPWVolumeSliceOverlayControl>(
                            frame,"MPW.Volume.SliceOverlay."+plane);
                    Check(overlay!=null && overlay.Plane==plane &&
                          overlay.VisibleCrosshairCount==2 && overlay.HasLegend &&
                          overlay.IsHitTestVisible==false,
                        "正交切片须使用正式MPW十字线色标叠加层，直接响应真实视图与FVO参数");
                }
                Check(frame.Scene.SlicePlaneCount == 3, "三张WPF空间贴图切面");
                Check(frame.SliceLegendCount == 3, "旧HALCON三二维图窗均显示灰阶/伪彩色标");
                Check(frame.Scene.CameraDistance > 0 && frame.Scene.OrientationAxisCount==3,
                    "原版三维相机和红绿蓝方向标记应正常初始化");
                frame.ViewState.SetSliceViewport(FVOSlicePlane.XY,0,1,0,1);
                double fullPlaneSpan=frame.Scene.GetPlaneHorizontalSpan(0);
                frame.ViewState.SetSliceViewport(FVOSlicePlane.XY,.2f,.8f,-.1f,1.1f);
                double zoomedSpan=frame.Scene.GetPlaneHorizontalSpan(0);
                Check(fullPlaneSpan>0 &&
                    Math.Abs(zoomedSpan/fullPlaneSpan-.6)<.001,
                    "二维缩放可视窗口必须同步改变WPF三维切面尺寸");
                frame.ViewState.SetSliceViewport(FVOSlicePlane.XY,0,1,0,1);
                int zBeforeWheel=frame.ViewState.SliceZ;
                frame.ScrollSlice(FVOSlicePlane.XY,2);
                Check(frame.ViewState.SliceZ==Math.Min(frame.Volume.OriginalDepth-1,zBeforeWheel+2),
                    "旧HALCON普通鼠标滚轮应切换当前二维平面，而非误缩放");
                frame.SetCrosshairFromImage(FVOSlicePlane.XY,6,9);
                Check(frame.ViewState.SliceX == 6 && frame.ViewState.SliceY == 9,
                    "二维切片坐标必须反算为MVI三刀索引");
                frame.MoveSlices(11,19,23);
                Check(frame.ViewState.SliceX == 11 && frame.ViewState.SliceY == 19 &&
                      frame.ViewState.SliceZ == 23, "三刀共用MVI状态");
                Check(frame.Scene.SlicePlaneCount == 3 &&
                      frame.XyViewer.ActiveImage != null, "移动三刀后四视图联动更新");
                frame.Center();
                Check(frame.ViewState.SliceX == frame.Volume.OriginalWidth/2 &&
                      frame.ViewState.SliceZ == frame.Volume.OriginalDepth/2,
                    "三刀中心复位");
                FVOVolumeData before = frame.Volume;
                frame.LoadDemo();
                Check(before != frame.Volume && !Object.ReferenceEquals(before,frame.Volume),
                    "更换体数据必须更新FVO对象与纹理贴图");
                // 公共WPF曲线组件由MPW持有，不再在TestCenter维护第二份实现。
                Check(typeof(CxVisual.Mechanism.Presentation.Wpf.Volume.
                    MPWVolumeCurveEditor).Assembly ==
                    typeof(CxVisual.Mechanism.Presentation.Wpf.Volume.
                    MPWVolumeThresholdEditor).Assembly,
                    "颜色/透明度曲线交互编辑器必须与阈值编辑器同属MPW程序集");
                var standalonePoints=new List<FVOTransferPoint> {
                    new FVOTransferPoint(0f,0f,FVOColor.White),
                    new FVOTransferPoint(1f,1f,FVOColor.White)
                };
                var editor=new CxVisual.Mechanism.Presentation.Wpf.Volume.
                    MPWVolumeCurveEditor(standalonePoints,false);
                int changedEvents=0;
                editor.Changed+=(sender,args)=>changedEvents++;
                editor.AddControlPoint(.45f,.55f);
                Check(editor.PointCount==3 && changedEvents==1 &&
                    Object.ReferenceEquals(editor.SelectedPoint,standalonePoints[1]) &&
                    Math.Abs(standalonePoints[1].X-.45f)<.0001 &&
                    Math.Abs(standalonePoints[1].Y-.55f)<.0001,
                    "正式MPW曲线编辑器新增节点应直接写回唯一FVO控制点并发出变化事件");
                // 正式MPW组件独立实例须直接编辑外部FVO列表，而不依赖专业CT3D程序集。
                var transferSet=new FVOTransferFunctionSet();
                var transferDock=new CxVisual.Mechanism.Presentation.Wpf.Volume.
                    MPWVolumeSettingsDockControl();
                var enableThreshold=new CheckBox();
                var thresholdLower=new Slider{Minimum=0,Maximum=1,Value=transferSet.Threshold};
                var enableColor=new CheckBox();
                var enableScalar=new CheckBox();
                var enableGradient=new CheckBox();
                int transferNotifications=0,obliqueJumpCount=0;
                var transferUi=new CxVisual.Mechanism.Presentation.Wpf.Volume.
                    MPWVolumeTransferFunctionPanels(
                    transferDock,transferSet,enableThreshold,thresholdLower,
                    enableColor,enableScalar,enableGradient,
                    ()=>transferNotifications++,()=>obliqueJumpCount++);
                Check(Object.ReferenceEquals(transferUi.TransferFunctions,transferSet) &&
                      transferDock.LowerCount==5 &&
                      transferUi.ColorCurve.PointCount==transferSet.Color.Points.Count,
                    "MPW五页传递函数面板必须独立创建并直接持有唯一FVO曲线");
                transferUi.ColorCurve.SelectPoint(1);
                int redBefore=transferSet.Color.Points[1].Color.R;
                transferUi.RedSlider.Value=redBefore==255?254:redBefore+1;
                Check(transferSet.Color.Points[1].Color.R==(byte)transferUi.RedSlider.Value &&
                      enableColor.IsChecked==true && transferNotifications>0,
                    "MPW RGB滑杆必须直接改变所选FVO颜色节点并通知渲染宿主");
                transferUi.ThresholdCurve.SetRange(.2f,.8f);
                Check(Math.Abs(transferSet.Threshold-.2f)<.001 &&
                      Math.Abs(transferSet.UpperThreshold-.8f)<.001 &&
                      Math.Abs(thresholdLower.Value-.2)<.001 &&
                      enableThreshold.IsChecked==true,
                    "MPW双端阈值编辑必须更新唯一FVO范围及既有阈值滑杆");
                var scalarHistogram=new float[]{0f,.5f,1f};
                var gradientHistogram=new float[]{1f,.3f,0f};
                transferUi.UpdateHistograms(scalarHistogram,gradientHistogram);
                Check(Object.ReferenceEquals(transferUi.ColorCurve.Histogram,scalarHistogram) &&
                      Object.ReferenceEquals(transferUi.ScalarCurve.Histogram,scalarHistogram) &&
                      Object.ReferenceEquals(transferUi.GradientCurve.Histogram,gradientHistogram) &&
                      Object.ReferenceEquals(transferUi.ThresholdCurve.Histogram,scalarHistogram),
                    "MPW统一直方图更新要同时覆盖颜色、标量、梯度与阈值而不复制采样");
                var obliqueTab=transferDock.LowerTabs.Items[4] as TabItem;
                var obliquePanel=obliqueTab==null?null:obliqueTab.Content as StackPanel;
                Button openOblique=null;
                if(obliquePanel!=null)
                    foreach(UIElement element in obliquePanel.Children)
                        if((openOblique=element as Button)!=null)break;
                Check(openOblique!=null,"MPW任意切导航应保留真实按钮");
                openOblique.RaiseEvent(new RoutedEventArgs(Button.ClickEvent,openOblique));
                Check(obliqueJumpCount==1,"MPW任意切导航必须调用宿主回调");

                int scalarCount=frame.ScalarStopCount,colorCount=frame.ColorStopCount;
                frame.AddScalarStopForTest(.72f,.6f);
                frame.AddColorStopForTest(.38f);
                Check(frame.ScalarStopCount==scalarCount+1 &&
                      frame.ColorStopCount==colorCount+1,
                    "WPF交互曲线须直接修改唯一FVO传递函数控制点");
                frame.SetObliqueForTest(true,20,15,45);
                Check(frame.Scene.SlicePlaneCount==4 &&
                      frame.ObliqueViewer.ActiveImage != null &&
                      frame.ViewState.ObliqueSlice.Visible,
                    "任意切FVO三线性结果/3D空间平面与MVI状态必须联动");
                frame.ObliqueViewer.Zoom(1.6);
                double obliqueZoom=frame.ObliqueViewer.PrimaryViewportScale;
                frame.SetObliqueForTest(true,24,15,45);
                Check(Math.Abs(frame.ObliqueViewer.PrimaryViewportScale-obliqueZoom)<.001,
                    "任意切角度调整应保持WPF预览缩放而非重新适合窗口");
                frame.ObliqueViewer.FitToWindow();
                int sharedX=frame.ViewState.SliceX, sharedY=frame.ViewState.SliceY,
                    sharedZ=frame.ViewState.SliceZ;
                frame.SetObliqueCenterForTest(8,12,15);
                Check(frame.ViewState.ObliqueSlice.CenterX==8 &&
                      frame.ViewState.ObliqueSlice.CenterY==12 &&
                      frame.ViewState.ObliqueSlice.CenterZ==15 &&
                      frame.ViewState.SliceX==sharedX &&
                      frame.ViewState.SliceY==sharedY &&
                      frame.ViewState.SliceZ==sharedZ &&
                      frame.Scene.SlicePlaneCount==4,
                    "关闭跟随三刀后任意切X/Y/Z中心可独立滑动且不能移动共享三刀");
                string obliqueRaw=Path.Combine(Path.GetTempPath(),
                    "CxVisual-Oblique-"+Guid.NewGuid().ToString("N")+".raw");
                try
                {
                    frame.SaveHighResolutionObliqueRaw(obliqueRaw);
                    Check(File.Exists(obliqueRaw) && File.Exists(obliqueRaw+".txt") &&
                        new FileInfo(obliqueRaw).Length ==
                        frame.ViewState.ObliqueSlice.Width *
                        frame.ViewState.ObliqueSlice.Height * sizeof(float),
                        "高清任意切原始Float32尺寸与几何元数据必须完整");
                }
                finally
                {
                    if(File.Exists(obliqueRaw))File.Delete(obliqueRaw);
                    if(File.Exists(obliqueRaw+".txt"))File.Delete(obliqueRaw+".txt");
                }
                frame.SetObliqueForTest(false,0,0,0);
                Check(frame.Scene.SlicePlaneCount==3,
                    "关闭任意切不能残留3D空间贴图");
                string file = Environment.GetEnvironmentVariable("CXVISUAL_VOLUME_UI_SCREENSHOT");
                if (!String.IsNullOrWhiteSpace(file))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(file));
                    window.UpdateLayout();
                    string legacyPng=Path.Combine(Path.GetDirectoryName(file),"four-views.png");
                    frame.SaveFourViewScreenshot(legacyPng);
                    Check(new FileInfo(legacyPng).Length>5000, "旧版四视图完整PNG导出");
                    RenderTargetBitmap bitmap = new RenderTargetBitmap(
                        (int)Math.Ceiling(window.ActualWidth),(int)Math.Ceiling(window.ActualHeight),
                        96,96,PixelFormats.Pbgra32);
                    bitmap.Render(window);
                    PngBitmapEncoder png = new PngBitmapEncoder();
                    png.Frames.Add(BitmapFrame.Create(bitmap));
                    using (FileStream stream = File.Create(file)) png.Save(stream);
                    Check(new FileInfo(file).Length > 8000, "输出实际WPF窗口截图不能为空");
                }
                string empty = Path.Combine(Path.GetTempPath(),
                    "cxvisual-volume-empty-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(empty);
                try
                {
                    bool rejected = false;
                    try { TCTManualVolumeViewerFrame.LoadBmpStack(empty); }
                    catch (InvalidDataException) { rejected = true; }
                    Check(rejected, "无BMP切片目录必须明确拒绝");
                }
                finally { Directory.Delete(empty); }
                // 真实文件从后台FVO加载并回到UI线程；验证不依赖文件对话框。
                string rawFolder=Path.Combine(Path.GetTempPath(),"cxvisual-volume-async-"+
                    Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(rawFolder);
                try
                {
                    string rawPath=Path.Combine(rawFolder,"sample_8x8x8_sp1x1x1.raw");
                    byte[] samples=new byte[512];
                    for(int v=0;v<samples.Length;v++)samples[v]=(byte)(v%255);
                    File.WriteAllBytes(rawPath,samples);
                    System.Threading.Tasks.Task task=frame.LoadRawFileAsync(rawPath,
                        System.Threading.CancellationToken.None);
                    if(!task.IsCompleted)
                    {
                        System.Windows.Threading.DispatcherFrame pump=
                            new System.Windows.Threading.DispatcherFrame();
                        task.ContinueWith(t=>frame.Dispatcher.BeginInvoke(
                            new Action(delegate {pump.Continue=false;})));
                        System.Windows.Threading.Dispatcher.PushFrame(pump);
                    }
                    task.GetAwaiter().GetResult();
                    Check(frame.Volume.OriginalWidth==8 && !frame.IsRawLoading,
                        "旧版RAW异步预览与界面状态回收");
                    frame.LoadDemo();
                }
                finally { Directory.Delete(rawFolder,true); }
                // BMP序列同样必须在后台加载，且旧版静态接口继续可调用。
                string bmpFolder=Path.Combine(Path.GetTempPath(),
                    "cxvisual-volume-bmp-"+Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(bmpFolder);
                try
                {
                    for(int z=0;z<3;z++)
                    {
                        byte[] gray=new byte[64];
                        for(int k=0;k<gray.Length;k++)gray[k]=(byte)(40+z*20);
                        BitmapSource source=BitmapSource.Create(8,8,96,96,
                            PixelFormats.Gray8,null,gray,8);
                        BmpBitmapEncoder encoder=new BmpBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(source));
                        using(FileStream bmpOutputStream=File.Create(Path.Combine(bmpFolder,
                            "slice"+z.ToString("D2")+".bmp")))encoder.Save(bmpOutputStream);
                    }
                    // 单独调用正式MPW BMP解码API，TestCenter不拥有BMP像素算法。
                    int bmpWidth, bmpHeight, bmpDepth;
                    byte[] decoded=CxVisual.Mechanism.Presentation.Wpf.Imaging.
                        MPWImageSequenceReader.ReadBmpGray8Stack(
                            bmpFolder,System.Threading.CancellationToken.None,
                            out bmpWidth,out bmpHeight,out bmpDepth);
                    Check(bmpWidth==8 && bmpHeight==8 && bmpDepth==3 &&
                          decoded.Length==8*8*3 && decoded[0]==40 &&
                          decoded[64]==60 && decoded[128]==80,
                        "正式MPW BMP解码器输出逐层Gray8，TestCenter仅调用其公共API");
                    using(System.Threading.CancellationTokenSource canceledReader=
                        new System.Threading.CancellationTokenSource())
                    {
                        canceledReader.Cancel();
                        bool refused=false;
                        try
                        {
                            int a,b,c;
                            CxVisual.Mechanism.Presentation.Wpf.Imaging.
                                MPWImageSequenceReader.ReadBmpGray8Stack(
                                    bmpFolder,canceledReader.Token,out a,out b,out c);
                        }
                        catch(System.OperationCanceledException) { refused=true; }
                        Check(refused,"MPW BMP解码器取消必须先于读取和内存分配");
                    }
                    string different=Path.Combine(bmpFolder,"slice03.bmp");
                    try
                    {
                        byte[] smallGray=new byte[4*4];
                        BitmapSource bad=BitmapSource.Create(4,4,96,96,
                            PixelFormats.Gray8,null,smallGray,4);
                        BmpBitmapEncoder badEncoder=new BmpBitmapEncoder();
                        badEncoder.Frames.Add(BitmapFrame.Create(bad));
                        using(FileStream output=File.Create(different))badEncoder.Save(output);
                        bool rejectedDimensions=false;
                        try
                        {
                            int a,b,c;
                            CxVisual.Mechanism.Presentation.Wpf.Imaging.
                                MPWImageSequenceReader.ReadBmpGray8Stack(
                                    bmpFolder,System.Threading.CancellationToken.None,
                                    out a,out b,out c);
                        }
                        catch(InvalidDataException) { rejectedDimensions=true; }
                        Check(rejectedDimensions,
                            "MPW BMP解码器拒绝混合尺寸，不能产生错位体数据");
                    }
                    finally {if(File.Exists(different))File.Delete(different);}
                    System.Threading.Tasks.Task bmpTask=frame.LoadBmpDirectoryAsync(
                        bmpFolder,System.Threading.CancellationToken.None);
                    if(!bmpTask.IsCompleted)
                    {
                        System.Windows.Threading.DispatcherFrame pump=
                            new System.Windows.Threading.DispatcherFrame();
                        bmpTask.ContinueWith(t=>frame.Dispatcher.BeginInvoke(
                            new Action(delegate {pump.Continue=false;})));
                        System.Windows.Threading.Dispatcher.PushFrame(pump);
                    }
                    bmpTask.GetAwaiter().GetResult();
                    Check(frame.Volume!=null && frame.Volume.OriginalWidth==8 &&
                          frame.Volume.OriginalDepth==3 && !frame.IsBmpLoading &&
                          Math.Abs(frame.Volume.GetScalarByOriginalIndex(0,0,2)-80)<1,
                        "BMP序列按文件顺序后台解码、转移FVO所有权并回收加载状态");

                    // 已取消的BMP请求不得清除或覆盖前一次成功加载的数据。
                    FVOVolumeData accepted=frame.Volume;
                    using(System.Threading.CancellationTokenSource canceled=
                        new System.Threading.CancellationTokenSource())
                    {
                        canceled.Cancel();
                        System.Threading.Tasks.Task rejected=frame.LoadBmpDirectoryAsync(
                            bmpFolder,canceled.Token);
                        if(!rejected.IsCompleted)
                        {
                            System.Windows.Threading.DispatcherFrame pump=
                                new System.Windows.Threading.DispatcherFrame();
                            rejected.ContinueWith(t=>frame.Dispatcher.BeginInvoke(
                                new Action(delegate {pump.Continue=false;})));
                            System.Windows.Threading.Dispatcher.PushFrame(pump);
                        }
                        bool wasCanceled=false;
                        try { rejected.GetAwaiter().GetResult(); }
                        catch(System.OperationCanceledException) { wasCanceled=true; }
                        Check(wasCanceled && Object.ReferenceEquals(accepted,frame.Volume) &&
                              !frame.IsBmpLoading,
                            "被取消的BMP加载不能覆盖当前体数据且必须回收异步状态");
                    }
                    // 真实点击WPF顶部按钮取消正在执行的BMP解码，不得清空当前体数据。
                    FVOVolumeData beforeCancel=frame.Volume;
                    System.Threading.Tasks.Task pending=frame.LoadBmpDirectoryAsync(
                        bmpFolder,System.Threading.CancellationToken.None);
                    Check(cancelCommand.CanExecute() && cancelButton.IsEnabled &&
                          frame.IsBmpLoading,
                        "BMP开始异步读取后应立即启用取消按钮与命令");
                    cancelButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent,cancelButton));
                    if(!pending.IsCompleted)
                    {
                        System.Windows.Threading.DispatcherFrame pump=
                            new System.Windows.Threading.DispatcherFrame();
                        pending.ContinueWith(t=>frame.Dispatcher.BeginInvoke(
                            new Action(delegate {pump.Continue=false;})));
                        System.Windows.Threading.Dispatcher.PushFrame(pump);
                    }
                    bool canceledByButton=false;
                    try { pending.GetAwaiter().GetResult(); }
                    catch(System.OperationCanceledException) { canceledByButton=true; }
                    Check(canceledByButton && Object.ReferenceEquals(beforeCancel,frame.Volume) &&
                          !frame.IsBmpLoading && !cancelCommand.CanExecute() &&
                          !cancelButton.IsEnabled,
                        "WPF按钮取消BMP后台读取后须保留原体并恢复空闲操作状态");
                    frame.MoveSlices(2,3,1);
                    Check(frame.ViewState.SliceX==2 && frame.ViewState.SliceY==3,
                        "取消加载后当前FVO/MVI切片仍可交互");
                    frame.LoadDemo();
                }
                finally {Directory.Delete(bmpFolder,true);}
                // 真实开发者内存API与WPF Viewer组合：UInt16原值不得被GPU预览R8替代。
                byte[] highPrecisionMemory=new byte[8*8*8*2];
                for(int voxel=0;voxel<512;voxel++)
                {
                    ushort original=(ushort)(2300+voxel);
                    highPrecisionMemory[voxel*2]=(byte)(original & 255);
                    highPrecisionMemory[voxel*2+1]=(byte)(original >> 8);
                }
                FVORawVolumeInfo memoryInfo=new FVORawVolumeInfo(8,8,8,
                    1,1,1,FVORawVoxelFormat.UInt16,"wpf-memory-uint16");
                FVOVolumeData memoryVolume=FVORawVolumeLoader.LoadFromMemory(
                    highPrecisionMemory,memoryInfo,64,System.Threading.CancellationToken.None);
                try
                {
                    frame.LoadVolume(memoryVolume);
                    memoryVolume=null; // 工作台成为唯一FVO Owner。
                    frame.MoveSlices(3,4,7);
                    int selectedIndex=3+4*8+7*64;
                    Check(frame.Volume.HasOriginalRawData &&
                          !frame.Volume.HasRawSource &&
                          frame.Volume.GetScalarByOriginalIndex(3,4,7)==2300+selectedIndex &&
                          frame.XyViewer.ActiveImage!=null &&
                          frame.XzViewer.ActiveImage!=null &&
                          frame.YzViewer.ActiveImage!=null,
                        "16位内存数据通过开发者API进入三维WPF四视图，三刀/原值保持一致");
                    highPrecisionMemory[selectedIndex*2]=0;
                    highPrecisionMemory[selectedIndex*2+1]=0;
                    Check(frame.Volume.GetScalarByOriginalIndex(3,4,7)==2300+selectedIndex,
                        "宿主内存变更后WPF仍访问FVO独立持有的原始数据");
                }
                finally
                {
                    if(memoryVolume!=null)memoryVolume.Dispose();
                    frame.LoadDemo();
                }
                // 真正尝试 WGL Context。CI若只有OpenGL1.1，必须回退而不是伪报GPU成功。
                frame.TryEnableGpu();
                window.UpdateLayout();
                frame.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background,
                    new Action(delegate { }));
                if (!frame.IsGpuVolumeRendering &&
                    !frame.GpuStatusText.Contains("GPU不可用"))
                    throw new InvalidOperationException("GPU驱动不足时没有给出显式失败原因");
                frame.OnWorkspaceClosed();
                Check(frame.Volume == null && frame.Scene.SlicePlaneCount == 0 &&
                      frame.VisibleCrosshairCount == 0, "关闭时应解除MVI状态、释放体数据及切面可视对象");
                Console.WriteLine("PASS TCT Volume native WPF 4-view, FVO/MVI, crosshair, slice scene and screenshot");
            }
            finally
            {
                window.Close();
            }
        }
        private static T FindVisualByAutomationId<T>(DependencyObject root,string id)
            where T:DependencyObject
        {
            if(root==null)return null;
            T item=root as T;
            if(item!=null && AutomationProperties.GetAutomationId(item)==id)return item;
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
            {
                T found=FindVisualByAutomationId<T>(
                    VisualTreeHelper.GetChild(root,i),id);
                if(found!=null)return found;
            }
            return null;
        }

        private static void Check(bool ok, string subject)
        {
            if (!ok) throw new InvalidOperationException("三维WPF验收失败：" + subject);
        }
    }
}
