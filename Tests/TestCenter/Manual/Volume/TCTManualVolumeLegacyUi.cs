using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CxVisual.Foundation.Volume;
using CxVisual.Mechanism.Presentation.Wpf.Imaging;
using CxVisual.Mechanism.Presentation.Wpf.Volume;
using Microsoft.Win32;

namespace CxVisual.TestCenter.UI
{
    /// <summary>
    /// 对照旧 MainForm / FourViewLayoutControl 的 WPF 工作台结构与操作。
    /// 不新建工程、不复制体数据/三刀模型；UI调用现有FVO、MVI、MPW与CT3D。
    /// </summary>
    public sealed partial class TCTManualVolumeViewerFrame
    {
        private MPWVolumeFourViewLayoutControl _legacyFour;
        private MPWVolumeFourViewWorkbenchControl _workbench;
        private readonly Dictionary<FVOSlicePlane, MPWVolumeSliceNavigationControl> _headerNavigation =
            new Dictionary<FVOSlicePlane, MPWVolumeSliceNavigationControl>();
        private MPWVolumeSettingsDockControl _settingsDock;
        private TabControl _upperTabs, _lowerTabs;
        private readonly CheckBox _crosshairEnabled = new CheckBox { Content="显示十字线",IsChecked=true };
        private readonly CheckBox _orientationEnabled = new CheckBox { Content="显示3D方向坐标（双击复位）",IsChecked=true };
        private readonly TextBox _windowCenterText = new TextBox { Width=44,Text="128",Margin=new Thickness(3) };
        private readonly TextBox _windowWidthText = new TextBox { Width=44,Text="256",Margin=new Thickness(3) };
        private readonly Slider _presetOpacity = new Slider { Minimum=0,Maximum=200,Value=100 };
        private readonly TextBox _backgroundText = new TextBox { Width=65,Text="0",Margin=new Thickness(4) };
        private readonly TextBox _lowerPercentText = new TextBox { Width=65,Text="10.0",Margin=new Thickness(4) };
        private readonly TextBox _upperPercentText = new TextBox { Width=65,Text="99.5",Margin=new Thickness(4) };
        private readonly TextBlock _legacyDetails = new TextBlock { TextWrapping=TextWrapping.Wrap };
        private readonly CheckBox _autoSliceFit = new CheckBox { Content="每次换层自动适合视图",IsChecked=false };
        private readonly ComboBox _saveViewChoice=new ComboBox { Margin=new Thickness(4),Height=28 };
        private readonly Button _cancelLoadButton=new Button {
            Content="取消加载",Padding=new Thickness(10,4,10,4),
            Margin=new Thickness(4,5,2,5),MinWidth=90,IsEnabled=false
        };
        public int LegacyTopTabCount { get { return _upperTabs == null ? 0 : _upperTabs.Items.Count; } }
        public int LegacyBottomTabCount { get { return _lowerTabs == null ? 0 : _lowerTabs.Items.Count; } }
        public bool IsLegacySingleView { get { return _legacyFour != null && _legacyFour.IsSingleView; } }

        private void BuildLegacyUi()
        {
            Background=BrushOf(237,241,247);
            AutomationProperties.SetAutomationId(this,"TCT.Volume.Workbench");
            Grid root=new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height=new GridLength(40) });
            root.RowDefinitions.Add(new RowDefinition { Height=new GridLength(1,GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height=new GridLength(26) });
            Content=root;

            StackPanel actions=new StackPanel { Orientation=Orientation.Horizontal,Background=BrushOf(248,250,253) };
            AddButton(actions,"打开 RAW","TCT.Volume.OpenRaw",OpenRaw);
            AddButton(actions,"打开 BMP 目录","TCT.Volume.OpenBmp",OpenBmpDirectory);
            AutomationProperties.SetAutomationId(_cancelLoadButton,"TCT.Volume.CancelLoad");
            _cancelLoadButton.Click+=(s,e)=>ExecuteUiAction(CancelPendingVolumeLoads);
            actions.Children.Add(_cancelLoadButton);
            AddButton(actions,"清空","TCT.Volume.Clear",ClearVolume);
            AddButton(actions,"复位视图","TCT.Volume.Fit",FitAllViews);
            AddButton(actions,"保存截图","TCT.Volume.SaveScreenshot",SaveScreenshotDialog);
            AddButton(actions,"三刀居中","TCT.Volume.Center",Center);
            AddButton(actions,"相机复位","TCT.Volume.ResetCamera",Reset3DCamera);
            AddButton(actions,"演示体","TCT.Volume.Demo",LoadDemo);
            actions.Children.Add(new TextBlock { Text="窗位",VerticalAlignment=VerticalAlignment.Center,
                Margin=new Thickness(8,0,1,0) });
            actions.Children.Add(_windowCenterText);
            actions.Children.Add(new TextBlock { Text="窗宽",VerticalAlignment=VerticalAlignment.Center });
            actions.Children.Add(_windowWidthText);
            AddButton(actions,"应用窗宽窗位","TCT.Volume.ApplyWindow",ApplyWindowCenterWidth);
            ScrollViewer scroll=new ScrollViewer { HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,Content=actions };
            Grid.SetRow(scroll,0);root.Children.Add(scroll);

            Grid work=new Grid { Margin=new Thickness(4,3,4,3) };
            work.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(1,GridUnitType.Star),
                MinWidth=450 });
            work.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(5) });
            work.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(430),MinWidth=340 });
            Grid.SetRow(work,1);root.Children.Add(work);
            // 正式MPW负责四视图/导航装配；TestCenter仅提供现有GPU容器与状态反馈。
            _gpuSlot=new Grid();
            _gpuSlot.Children.Add(_scene);
            _workbench=new MPWVolumeFourViewWorkbenchControl(
                _xy,_yz,_xz,_gpuSlot,Reset3DCamera,Zoom3D,Pan3D);
            _legacyFour=_workbench.Layout;
            _gpuModeHeader=_workbench.CameraTitle;
            _workbench.VoxelInspected+=(s,e)=>_status.Text=String.Format(
                "体素 ({0},{1},{2})  原始值 {3:0.###}  物理位置 ({4:0.###},{5:0.###},{6:0.###})",
                e.X,e.Y,e.Z,e.Value,e.PhysicalX,e.PhysicalY,e.PhysicalZ);
            _workbench.NavigationValidationFailed+=message=>_status.Text=message;
            work.Children.Add(_workbench);

            System.Windows.Controls.GridSplitter resizer=new System.Windows.Controls.GridSplitter {
                Width=5,HorizontalAlignment=HorizontalAlignment.Stretch,
                VerticalAlignment=VerticalAlignment.Stretch,Background=BrushOf(178,192,211),
                ResizeDirection=GridResizeDirection.Columns,
                ResizeBehavior=GridResizeBehavior.PreviousAndNext };
            Grid.SetColumn(resizer,1);work.Children.Add(resizer);
            // MPW持有双层参数面板的布局、主题和页签容器，宿主只注入现有编辑能力。
            _settingsDock=new MPWVolumeSettingsDockControl();
            _upperTabs=_settingsDock.UpperTabs;
            _lowerTabs=_settingsDock.LowerTabs;
            Grid.SetColumn(_settingsDock,2);work.Children.Add(_settingsDock);

            StackPanel display=CreateSettingsPanel(),gray=CreateSettingsPanel(),render=CreateSettingsPanel(),oblique=CreateSettingsPanel();
            AddUpperTab("显示设置",display);
            AddUpperTab("灰阶映射",gray);
            AddUpperTab("渲染效果",render);
            AddUpperTab("任意切",oblique);
            Label(display,"当前视图及图像保存");
            StackPanel viewActions=new StackPanel { Orientation=Orientation.Horizontal };
            _saveViewChoice.Width=125;
            _saveViewChoice.Items.Add("XY主视图");
            _saveViewChoice.Items.Add("YZ侧视图");
            _saveViewChoice.Items.Add("XZ底视图");
            _saveViewChoice.Items.Add("3D视图");
            _saveViewChoice.SelectedIndex=0;
            AutomationProperties.SetAutomationId(_saveViewChoice,"TCT.Volume.SaveViewSelection");
            viewActions.Children.Add(_saveViewChoice);
            Button saveView=new Button { Content="保存当前视图PNG",
                Margin=new Thickness(5,3,3,3),Padding=new Thickness(6,3,6,3) };
            AutomationProperties.SetAutomationId(saveView,"TCT.Volume.SaveSelectedImage");
            saveView.Click+=(s,e)=>SaveSelectedViewDialog();
            viewActions.Children.Add(saveView);
            display.Children.Add(viewActions);
            Label(display,"切片导航 / 三刀联动");
            AddAxis(display,"X",_sliderX,"TCT.Volume.SliceX");
            AddAxis(display,"Y",_sliderY,"TCT.Volume.SliceY");
            AddAxis(display,"Z",_sliderZ,"TCT.Volume.SliceZ");
            _sliceStatus.Margin=new Thickness(3,2,3,6);
            _sliceStatus.Foreground=BrushOf(220,232,248);
            display.Children.Add(_sliceStatus);
            display.Children.Add(_crosshairEnabled);
            _crosshairEnabled.Checked+=(s,e)=>{if(_state!=null)_state.ShowCrosshair=true;UpdateCrosshairs();};
            _crosshairEnabled.Unchecked+=(s,e)=>{if(_state!=null)_state.ShowCrosshair=false;ClearCrosshairs();};
            display.Children.Add(_orientationEnabled);
            _orientationEnabled.Checked+=(s,e)=>Set3DOrientationVisible(true);
            _orientationEnabled.Unchecked+=(s,e)=>Set3DOrientationVisible(false);
            display.Children.Add(_autoSliceFit);
            _autoSliceFit.Checked+=(s,e)=>{if(_volume!=null){_xy.FitToWindow();_yz.FitToWindow();_xz.FitToWindow();}};
            _pseudo.Margin=new Thickness(4,6,4,4);display.Children.Add(_pseudo);
            AddAxis(display,"切片窗下限",_windowMin,"TCT.Volume.WindowMin");
            AddAxis(display,"切片窗上限",_windowMax,"TCT.Volume.WindowMax");
            display.Children.Add(new TextBlock {
                Text="2D：左键三刀/滚轮换层/Ctrl+滚轮缩放；3D：左键旋转/右键平移/滚轮缩放。",
                TextWrapping=TextWrapping.Wrap,Margin=new Thickness(4,8,4,5) });
            Label(gray,"渲染预设与灰阶范围");
            BuildGpuPanel(render); // 将成熟渲染控件直接归类，不复制字段或事件。
            MoveToTab(_gpuPreset,render,gray);
            MoveToTab(_gpuAutoRange,render,gray);
            AddNumeric(gray,"背景灰度上限 (0-254)",_backgroundText);
            AddNumeric(gray,"低灰分位 (%)",_lowerPercentText);
            AddNumeric(gray,"高灰分位 (%)",_upperPercentText);
            AddGpuSlider(gray,"Slicer预设不透明度 (%)",_presetOpacity);
            _presetOpacity.ValueChanged+=(s,e)=>UpdateLegacyGrayMapping();
            _backgroundText.LostFocus+=(s,e)=>UpdateLegacyGrayMapping();
            _lowerPercentText.LostFocus+=(s,e)=>UpdateLegacyGrayMapping();
            _upperPercentText.LostFocus+=(s,e)=>UpdateLegacyGrayMapping();
            System.Windows.Input.KeyEventHandler grayEnter=(s,e)=>{
                if(e.Key!=System.Windows.Input.Key.Enter)return;
                UpdateLegacyGrayMapping();
                System.Windows.Input.Keyboard.ClearFocus();e.Handled=true;
            };
            _backgroundText.KeyDown+=grayEnter;
            _lowerPercentText.KeyDown+=grayEnter;
            _upperPercentText.KeyDown+=grayEnter;
            Button applyGray=new Button { Content="应用灰阶映射",
                Margin=new Thickness(4,7,4,4),Padding=new Thickness(8,5,8,5) };
            AutomationProperties.SetAutomationId(applyGray,"TCT.Volume.ApplyGrayRange");
            applyGray.Click+=(s,e)=>UpdateLegacyGrayMapping();
            gray.Children.Add(applyGray);
            BuildObliquePanel(oblique);
            Label(render,"GPU 体绘 / 光照 / 采样");
            _legacyDetails.Foreground=BrushOf(211,224,240);
            _legacyDetails.Margin=new Thickness(5,8,5,6);
            render.Children.Add(_legacyDetails);
            _legacyDetails.Text="可使用WPF三维空间切面；OpenGL体绘需真实显卡验证。";
            BuildTransferPanels(_settingsDock);
            _upperTabs.SelectedIndex=0;_lowerTabs.SelectedIndex=0;
            _status.Foreground=BrushOf(51,77,108);
            _status.Margin=new Thickness(8,3,8,3);
            _status.TextTrimming=TextTrimming.CharacterEllipsis;
            Border footer=new Border { Background=BrushOf(231,240,252),
                BorderBrush=BrushOf(201,217,233),BorderThickness=new Thickness(0,1,0,0),
                Child=_status };
            Grid.SetRow(footer,2);root.Children.Add(footer);
        }

        private static StackPanel CreateSettingsPanel()
        { return new StackPanel { Margin=new Thickness(8,7,8,12),
            Background=BrushOf(9,15,24) }; }
        private static void AddNumeric(Panel panel,string label,TextBox field)
        {
            StackPanel row=new StackPanel { Orientation=Orientation.Horizontal,Margin=new Thickness(2,5,2,5) };
            row.Children.Add(new TextBlock { Text=label, Width=177,VerticalAlignment=VerticalAlignment.Center,
                Foreground=BrushOf(223,234,246) });
            row.Children.Add(field);panel.Children.Add(row);
        }
        private void AddUpperTab(string title,StackPanel contents)
        {
            _settingsDock.AddUpperTab(title,contents);
        }
        private static void MoveToTab(UIElement element,Panel from,Panel to)
        {
            if(!from.Children.Contains(element))throw new InvalidOperationException("控件不是预期的唯一实例。");
            from.Children.Remove(element);to.Children.Add(element);
        }

        public void SetWindowWidthForTest(string text)
        {
            _windowWidthText.Text=text;
        }

        public void ApplyWindowCenterWidth()
        {
            double center,width;
            if(!Double.TryParse(_windowCenterText.Text,out center) ||
                !Double.TryParse(_windowWidthText.Text,out width) || width<=0 ||
                Double.IsNaN(center)||Double.IsInfinity(center)||Double.IsInfinity(width))
                throw new ArgumentException("窗位必须为有限数值，窗宽必须大于0。");
            _windowMin.Value=Math.Max(0,Math.Min(255,center-width/2.0));
            _windowMax.Value=Math.Max(0,Math.Min(255,center+width/2.0));
        }

        private void UpdateLegacyGrayMapping()
        {
            int background;
            double low,high;
            if(!Int32.TryParse(_backgroundText.Text,out background) ||
                !Double.TryParse(_lowerPercentText.Text,out low) ||
                !Double.TryParse(_upperPercentText.Text,out high) ||
                background<0||background>254||low<0||high>100||low>=high)
            {
                _legacyDetails.Text="灰阶参数非法：背景0..254；低灰分位需小于高灰分位（0..100）。";
                return;
            }
            _gpuSettings.Intensity.BackgroundGrayUpper=background;
            _gpuSettings.Intensity.LowerPercentile=(float)(low/100.0);
            _gpuSettings.Intensity.UpperPercentile=(float)(high/100.0);
            _gpuSettings.Intensity.PresetOpacityScale=(float)(_presetOpacity.Value/100.0);
            UpdateGpuSettings(true);
            _legacyDetails.Text=String.Format("灰阶已应用：背景≤{0}，分位 {1:0.##}%..{2:0.##}%，透明度 {3:0}%",
                background,low,high,_presetOpacity.Value);
        }

        private async void RefreshLegacyHistogram()
        {
            if(_volume==null)return;
            FVOVolumeData source=_volume;
            FVOVolumeHistogram histogram;
            try { histogram=await Task.Run(()=>FVOVolumeHistogram.FromVolume(source)); }
            catch(Exception e)
            {
                if(Object.ReferenceEquals(source,_volume))_legacyDetails.Text="直方图加载失败："+e.Message;
                return;
            }
            if(!Object.ReferenceEquals(source,_volume))return;
            if(_transferPanels!=null)
                _transferPanels.UpdateHistograms(histogram.Scalar,histogram.Gradient);
            _legacyDetails.Text=String.Format("已加载 {0}×{1}×{2}；直方图与体数据来自同一个FVO。",
                _volume.OriginalWidth,_volume.OriginalHeight,_volume.OriginalDepth);
        }

        public void ClearVolume()
        {
            CancelPendingRawLoads();
            DisableGpu();
            if(_state!=null)_state.PropertyChanged-=OnStateChanged;
            _state=null;
            _workbench.Bind(null,null);
            if(_volume!=null)_volume.Dispose();
            _volume=null;
            _xy.Clear();_xz.Clear();_yz.Clear();_obliqueViewer.Clear();
            _scene.Clear();ClearCrosshairs();
            _sliceStatus.Text="";_status.Text="未载入体数据";
        }

        public void FitAllViews()
        {
            RestoreAllViews();
            _xy.FitToWindow();_yz.FitToWindow();_xz.FitToWindow();
            Reset3DCamera();
        }

        /// <summary>宿主仅通知正式MPW控件读取FVO/MVI真源。</summary>
        private void SyncSliceHeaderControls()
        {
            if(_workbench!=null)_workbench.RefreshNavigations();
        }

        public int HeaderSliceControlCount { get { return _workbench==null?0:_workbench.SliceNavigationCount; } }
        public void JumpToViewSliceForTest(FVOSlicePlane plane,int position)
        {
            if(_volume==null || _state==null)
                throw new InvalidOperationException("必须先加载体数据。");
            _workbench.GetNavigation(plane).SliceSlider.Value=position;
        }

        private void Set3DOrientationVisible(bool visible)
        {
            _scene.SetOrientationVisible(visible);
            if(_gpu!=null)_gpu.SetOrientationVisible(visible);
        }
        public void SetOrientationVisibleForTest(bool visible)
        {
            _orientationEnabled.IsChecked=visible;
        }

        public void Zoom3D(double steps)
        {
            if(_gpu!=null && _gpu.IsAvailable)_gpu.Zoom((float)steps);
            else _scene.Zoom(steps);
        }
        public void Pan3D(double dx,double dy)
        {
            if(_gpu!=null && _gpu.IsAvailable)_gpu.Pan((float)dx,(float)dy);
            else _scene.Pan(dx,dy);
        }
        public void Orbit3D(double dx,double dy)
        {
            if(_gpu!=null && _gpu.IsAvailable)_gpu.Orbit((float)dx,(float)dy);
            else _scene.Orbit(dx,dy);
        }
        public double CameraDistanceForTest
        {
            get { return _gpu!=null && _gpu.IsAvailable?_gpu.CameraDistance:_scene.CameraDistance; }
        }
        public double CameraTargetLengthForTest
        {
            get { return _gpu!=null && _gpu.IsAvailable?_gpu.CameraTarget.Length():
                _scene.CameraTarget.Length; }
        }

        public void ToggleViewForTest(int index)
        {
            if(_legacyFour==null)throw new InvalidOperationException("四视图未初始化。");
            _legacyFour.ToggleSlot(_legacyFour.GetSlot(index));
        }
        public void RestoreAllViews()
        {
            if(_legacyFour!=null)_legacyFour.RestoreAllViews();
        }

        private void SaveSelectedViewDialog()
        {
            if(_volume==null) { MessageBox.Show("请先加载体数据。","保存当前视图");return; }
            Microsoft.Win32.SaveFileDialog dlg=new Microsoft.Win32.SaveFileDialog {
                Filter="PNG 图像 (*.png)|*.png",FileName="CxVisual-Volume-View.png" };
            if(dlg.ShowDialog()!=true)return;
            try { SaveSelectedViewPng(dlg.FileName,_saveViewChoice.SelectedIndex); }
            catch(Exception error) { MessageBox.Show(error.Message,"保存视图失败"); }
        }

        /// <summary>单视图输出复用MPW图窗，GPU直接读显卡帧；不通过四视图截图裁切。</summary>
        public void SaveSelectedViewPng(string filename,int viewIndex)
        {
            if(_volume==null)throw new InvalidOperationException("尚未加载体数据。");
            if(String.IsNullOrWhiteSpace(filename))throw new ArgumentNullException("filename");
            if(viewIndex<0||viewIndex>3)throw new ArgumentOutOfRangeException("viewIndex");
            if(viewIndex<3)
            {
                MPWImageViewerControl viewer=viewIndex==0?_xy:viewIndex==1?_yz:_xz;
                viewer.SavePng(filename);
                return;
            }
            BitmapSource pixels;
            if(IsGpuVolumeRendering)
                pixels=_gpu.CaptureFrame();
            else
            {
                int width=(int)Math.Ceiling(_scene.ActualWidth);
                int height=(int)Math.Ceiling(_scene.ActualHeight);
                if(width<=0||height<=0)throw new InvalidOperationException("3D视图还没有有效尺寸。");
                RenderTargetBitmap picture=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);
                picture.Render(_scene);
                pixels=picture;
            }
            PngBitmapEncoder encoder=new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(pixels));
            string full=Path.GetFullPath(filename);
            string folder=Path.GetDirectoryName(full);
            if(!Directory.Exists(folder))Directory.CreateDirectory(folder);
            using(FileStream file=File.Create(full))encoder.Save(file);
        }

        private void SaveScreenshotDialog()
        {
            SaveFileDialog dialog=new SaveFileDialog { Filter="PNG 图像 (*.png)|*.png",
                FileName="CxVisual-Volume-FourViews.png" };
            if(dialog.ShowDialog()!=true)return;
            SaveFourViewScreenshot(dialog.FileName);
        }

        public void SaveFourViewScreenshot(string path)
        {
            if(_volume==null || _state==null)
                throw new InvalidOperationException("尚未加载体数据，不能保存空白四视图。");
            if(String.IsNullOrWhiteSpace(path))throw new ArgumentException("缺少PNG文件名。");
            if(_legacyFour==null || _legacyFour.ActualWidth<10 || _legacyFour.ActualHeight<10)
                throw new InvalidOperationException("四视图尚未完成布局，不能保存空白截图。");
            int w=checked((int)Math.Ceiling(_legacyFour.ActualWidth));
            int h=checked((int)Math.Ceiling(_legacyFour.ActualHeight));
            RenderTargetBitmap wpf=new RenderTargetBitmap(w,h,96,96,PixelFormats.Pbgra32);
            wpf.Render(_legacyFour);
            BitmapSource combined=wpf;
            if(IsGpuVolumeRendering && _gpu!=null)
            {
                BitmapSource gpu=_gpu.CaptureFrame();
                Point origin=_gpu.TransformToAncestor(_legacyFour).Transform(new Point(0,0));
                DrawingVisual visual=new DrawingVisual();
                using(DrawingContext dc=visual.RenderOpen())
                {
                    dc.DrawImage(wpf,new Rect(0,0,w,h));
                    dc.DrawImage(gpu,new Rect(origin.X,origin.Y,
                        _gpu.ActualWidth,_gpu.ActualHeight));
                }
                RenderTargetBitmap merged=new RenderTargetBitmap(w,h,96,96,PixelFormats.Pbgra32);
                merged.Render(visual);
                combined=merged;
            }
            PngBitmapEncoder png=new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(combined));
            string directory=Path.GetDirectoryName(Path.GetFullPath(path));
            if(!Directory.Exists(directory))Directory.CreateDirectory(directory);
            using(FileStream stream=File.Create(path))png.Save(stream);
        }
    }
}
