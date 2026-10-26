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
                TCTWorkspaceAction windowCommand=actions.Find(c=>c.Id=="ct3d.window");
                TCTWorkspaceAction zoomCommand=actions.Find(c=>c.Id=="ct3d.zoomIn");
                Check(clearCommand!=null && !clearCommand.CanExecute() &&
                    windowCommand!=null && !windowCommand.CanExecute(),
                    "未加载时危险的窗口操作必须禁止执行");
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
                TabControl upper=FindVisualByAutomationId<TabControl>(frame,
                    "TCT.Volume.UpperTabs");
                TabControl lower=FindVisualByAutomationId<TabControl>(frame,
                    "TCT.Volume.LowerTabs");
                Check(upper!=null && lower!=null,"新三维设置/传递函数页签必须在真实WPF可视树中");
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
                frame.JumpToViewSliceForTest(FVOSlicePlane.XY,12);
                frame.JumpToViewSliceForTest(FVOSlicePlane.XZ,13);
                frame.JumpToViewSliceForTest(FVOSlicePlane.YZ,14);
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
                frame.ToggleViewForTest(0);
                Check(frame.IsLegacySingleView,"四视图单窗最大化");
                frame.ToggleViewForTest(0);
                Check(!frame.IsLegacySingleView,"单窗可原位恢复2x2");
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
                Check(frame.Volume != null && frame.ViewState != null,"真实FVO与MVI状态绑定");
                Check(frame.XyViewer.ActiveImage != null &&
                      frame.XzViewer.ActiveImage != null &&
                      frame.YzViewer.ActiveImage != null, "三个MPW WPF图窗均须实际显示图像");
                frame.XyViewer.Zoom(1.75);
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
