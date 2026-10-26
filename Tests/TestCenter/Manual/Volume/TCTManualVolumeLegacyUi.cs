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
using Microsoft.Win32;

namespace CxVisual.TestCenter.UI
{
    /// <summary>
    /// 对照旧 MainForm / FourViewLayoutControl 的 WPF 工作台结构与操作。
    /// 不新建工程、不复制体数据/三刀模型；UI调用现有FVO、MVI、MPW与CT3D。
    /// </summary>
    public sealed partial class TCTManualVolumeViewerFrame
    {
        private Grid _legacyFour;
        private readonly List<Grid> _legacySlots = new List<Grid>();
        private Grid _maximizedSlot;
        private readonly Dictionary<FVOSlicePlane, Slider> _headerSliceSliders =
            new Dictionary<FVOSlicePlane, Slider>();
        private readonly Dictionary<FVOSlicePlane, TextBox> _headerSliceNumbers =
            new Dictionary<FVOSlicePlane, TextBox>();
        private bool _syncingHeaderSlices;
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
        public int LegacyTopTabCount { get { return _upperTabs == null ? 0 : _upperTabs.Items.Count; } }
        public int LegacyBottomTabCount { get { return _lowerTabs == null ? 0 : _lowerTabs.Items.Count; } }
        public bool IsLegacySingleView { get { return _maximizedSlot != null; } }

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
            _legacyFour=new Grid { Background=BrushOf(11,17,25) };
            _legacyFour.RowDefinitions.Add(new RowDefinition());
            _legacyFour.RowDefinitions.Add(new RowDefinition());
            _legacyFour.ColumnDefinitions.Add(new ColumnDefinition());
            _legacyFour.ColumnDefinitions.Add(new ColumnDefinition());
            work.Children.Add(_legacyFour);
            AddSliceViewport(_legacyFour,_xy,FVOSlicePlane.XY,"XY 主视图",0,0);
            AddSliceViewport(_legacyFour,_yz,FVOSlicePlane.YZ,"YZ 侧视图",0,1);
            AddSliceViewport(_legacyFour,_xz,FVOSlicePlane.XZ,"XZ 底视图",1,0);
            Grid gpuFrame=AddViewport(_legacyFour,_scene,"3D / GPU体绘启动中",1,1);
            Border gpuTitle=gpuFrame.Children[0] as Border;
            _gpuModeHeader=gpuTitle==null?null:gpuTitle.Child as TextBlock;
            gpuFrame.Children.Remove(_scene);
            _gpuSlot=new Grid();Grid.SetRow(_gpuSlot,1);
            gpuFrame.Children.Add(_gpuSlot);_gpuSlot.Children.Add(_scene);
            foreach(UIElement item in _legacyFour.Children)
            {
                Grid host=item as Grid;
                if(host==null)continue;
                _legacySlots.Add(host);
                int row=Grid.GetRow(host),column=Grid.GetColumn(host);
                MPWImageViewerControl viewer=(row==0 && column==0)?_xy:
                    (row==0 && column==1)?_yz:(row==1 && column==0)?_xz:null;
                AddViewportControls(host,viewer,row,column);
            }

            System.Windows.Controls.GridSplitter resizer=new System.Windows.Controls.GridSplitter {
                Width=5,HorizontalAlignment=HorizontalAlignment.Stretch,
                VerticalAlignment=VerticalAlignment.Stretch,Background=BrushOf(178,192,211),
                ResizeDirection=GridResizeDirection.Columns,
                ResizeBehavior=GridResizeBehavior.PreviousAndNext };
            Grid.SetColumn(resizer,1);work.Children.Add(resizer);
            Grid side=new Grid { Background=BrushOf(9,15,24),
                Margin=new Thickness(1,0,0,0) };
            side.RowDefinitions.Add(new RowDefinition { Height=new GridLength(44,GridUnitType.Star) });
            side.RowDefinitions.Add(new RowDefinition { Height=new GridLength(56,GridUnitType.Star) });
            Grid.SetColumn(side,2);work.Children.Add(side);
            // 与成熟WinForms工作台一致：视区黑色、设置区深色，标签与曲线编辑画布明亮。
            Style checkStyle=new Style(typeof(CheckBox));
            checkStyle.Setters.Add(new Setter(Control.ForegroundProperty,BrushOf(224,234,247)));
            side.Resources.Add(typeof(CheckBox),checkStyle);
            Style textStyle=new Style(typeof(TextBlock));
            textStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty,BrushOf(221,232,246)));
            side.Resources.Add(typeof(TextBlock),textStyle);
            _upperTabs=new TabControl { Margin=new Thickness(0,0,0,2),
                Background=BrushOf(9,15,24) };
            _lowerTabs=new TabControl { Margin=new Thickness(0,2,0,0),
                Background=BrushOf(9,15,24) };
            AutomationProperties.SetAutomationId(_upperTabs,"TCT.Volume.UpperTabs");
            AutomationProperties.SetAutomationId(_lowerTabs,"TCT.Volume.LowerTabs");
            side.Children.Add(_upperTabs);Grid.SetRow(_lowerTabs,1);side.Children.Add(_lowerTabs);

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
            _sliceStatus.Foreground=BrushOf(38,66,99);
            display.Children.Add(_sliceStatus);
            display.Children.Add(_crosshairEnabled);
            _crosshairEnabled.Checked+=(s,e)=>{if(_state!=null)_state.ShowCrosshair=true;UpdateCrosshairs();};
            _crosshairEnabled.Unchecked+=(s,e)=>{if(_state!=null)_state.ShowCrosshair=false;ClearCrosshairs();};
            display.Children.Add(_orientationEnabled);
            _orientationEnabled.Checked+=(s,e)=>_scene.SetOrientationVisible(true);
            _orientationEnabled.Unchecked+=(s,e)=>_scene.SetOrientationVisible(false);
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
            BuildTransferPanels(_lowerTabs);
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
            ScrollViewer scroll=new ScrollViewer { Content=contents,
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
            _upperTabs.Items.Add(new TabItem { Header=title,Content=scroll });
        }
        private static void MoveToTab(UIElement element,Panel from,Panel to)
        {
            if(!from.Children.Contains(element))throw new InvalidOperationException("控件不是预期的唯一实例。");
            from.Children.Remove(element);to.Children.Add(element);
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
            UpdateGpuSettings();
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
            if(_thresholdCurve!=null)
            {
                _thresholdCurve.Histogram=histogram.Scalar;
                _thresholdCurve.InvalidateVisual();
            }
            if(_colorCurve!=null) { _colorCurve.Histogram=histogram.Scalar;_colorCurve.InvalidateVisual(); }
            if(_scalarCurve!=null) { _scalarCurve.Histogram=histogram.Scalar;_scalarCurve.InvalidateVisual(); }
            if(_gradientCurve!=null) { _gradientCurve.Histogram=histogram.Gradient;_gradientCurve.InvalidateVisual(); }
            _legacyDetails.Text=String.Format("已加载 {0}×{1}×{2}；直方图与体数据来自同一个FVO。",
                _volume.OriginalWidth,_volume.OriginalHeight,_volume.OriginalDepth);
        }

        public void ClearVolume()
        {
            CancelPendingRawLoads();
            DisableGpu();
            if(_state!=null)_state.PropertyChanged-=OnStateChanged;
            _state=null;
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

        private void AddViewportControls(Grid host,MPWImageViewerControl viewer,int row,int col)
        {
            StackPanel tools=new StackPanel { Orientation=Orientation.Horizontal,
                HorizontalAlignment=HorizontalAlignment.Right,
                Background=new SolidColorBrush(Color.FromRgb(247,250,253)) };
            if(viewer!=null)
            {
                FVOSlicePlane plane=row==0 && col==0?FVOSlicePlane.XY:
                    row==0 && col==1?FVOSlicePlane.YZ:FVOSlicePlane.XZ;
                TinyButton(tools,"适合",()=>viewer.FitToWindow());
                TinyButton(tools,"1:1",()=>viewer.ActualSize());
                TinyButton(tools,"+",()=>viewer.Zoom(1.2));
                TinyButton(tools,"-",()=>viewer.Zoom(1/1.2));
                tools.Children.Add(new TextBlock { Text="跳转",Margin=new Thickness(7,0,2,0),
                    VerticalAlignment=VerticalAlignment.Center,FontSize=10 });
                Slider slice=new Slider { Minimum=0,Maximum=1,Width=105,
                    SmallChange=1,LargeChange=1,TickFrequency=1,IsSnapToTickEnabled=true,
                    VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(2,0,4,0) };
                AutomationProperties.SetAutomationId(slice,"TCT.Volume.Header."+plane+".Slider");
                TextBox index=new TextBox { Width=41,Text="0",VerticalContentAlignment=VerticalAlignment.Center,
                    Height=20,Margin=new Thickness(2) };
                AutomationProperties.SetAutomationId(index,"TCT.Volume.Header."+plane+".Index");
                slice.ValueChanged+=(s,e)=>{
                    if(_syncingHeaderSlices||_updating||_state==null||_volume==null)return;
                    JumpToViewSlice(plane,(int)Math.Round(slice.Value));
                };
                index.KeyDown+=(s,e)=>{
                    if(e.Key!=System.Windows.Input.Key.Enter)return;
                    int value;
                    if(!Int32.TryParse(index.Text,out value))
                    {
                        _status.Text="跳转索引必须为整数。";e.Handled=true;return;
                    }
                    JumpToViewSlice(plane,value);
                    e.Handled=true;
                };
                tools.Children.Add(slice);tools.Children.Add(index);
                _headerSliceSliders[plane]=slice;
                _headerSliceNumbers[plane]=index;
            }
            else
            {
                TinyButton(tools,"复位",Reset3DCamera);
                TinyButton(tools,"放大",()=>Zoom3D(1));
                TinyButton(tools,"缩小",()=>Zoom3D(-1));
                TinyButton(tools,"左移",()=>Pan3D(-28,0));
                TinyButton(tools,"右移",()=>Pan3D(28,0));
                TinyButton(tools,"上移",()=>Pan3D(0,-28));
                TinyButton(tools,"下移",()=>Pan3D(0,28));
                tools.ToolTip="GPU三维：左键拖动旋转；右/中键或Shift+左键平移；滚轮缩放；双击复位";
            }
            TinyButton(tools,"□",()=>ToggleSlot(host));
            Grid.SetRow(tools,0);host.Children.Add(tools);
            AutomationProperties.SetAutomationId(host,"TCT.Volume.View."+row+col);
        }

        private void JumpToViewSlice(FVOSlicePlane plane,int index)
        {
            if(_volume==null || _state==null)return;
            if(plane==FVOSlicePlane.XY)_state.SetSliceZ(index,_volume);
            else if(plane==FVOSlicePlane.XZ)_state.SetSliceY(index,_volume);
            else if(plane==FVOSlicePlane.YZ)_state.SetSliceX(index,_volume);
            else throw new ArgumentOutOfRangeException("plane");
            SyncSliceHeaderControls();
        }

        /// <summary>各窗口只读写MVI唯一切片坐标，不创建第二套切片状态。</summary>
        private void SyncSliceHeaderControls()
        {
            if(_volume==null || _state==null)return;
            _syncingHeaderSlices=true;
            try
            {
                foreach(KeyValuePair<FVOSlicePlane,Slider> pair in _headerSliceSliders)
                {
                    int maximum=pair.Key==FVOSlicePlane.XY?_volume.OriginalDepth-1:
                        pair.Key==FVOSlicePlane.XZ?_volume.OriginalHeight-1:
                        _volume.OriginalWidth-1;
                    int position=pair.Key==FVOSlicePlane.XY?_state.SliceZ:
                        pair.Key==FVOSlicePlane.XZ?_state.SliceY:_state.SliceX;
                    pair.Value.Maximum=Math.Max(0,maximum);
                    pair.Value.Value=Math.Max(0,Math.Min(maximum,position));
                    _headerSliceNumbers[pair.Key].Text=position.ToString();
                }
            }
            finally { _syncingHeaderSlices=false; }
        }

        public int HeaderSliceControlCount { get { return _headerSliceSliders.Count; } }
        public void JumpToViewSliceForTest(FVOSlicePlane plane,int position)
        {
            if(!_headerSliceSliders.ContainsKey(plane))throw new ArgumentOutOfRangeException("plane");
            if(_volume==null||_state==null)throw new InvalidOperationException("必须先加载体数据。");
            // 真实改变WPF Slider.Value，验收时经过ValueChanged事件而非跳过UI直调MVI。
            _headerSliceSliders[plane].Value=position;
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

        private static void TinyButton(Panel tools,string caption,Action action)
        {
            Button b=new Button { Content=caption,MinWidth=26,Padding=new Thickness(2,0,2,0),
                Margin=new Thickness(2,2,0,2),FontSize=11 };
            b.Click+=(s,e)=>action();tools.Children.Add(b);
        }

        private void ToggleSlot(Grid slot)
        {
            if(_maximizedSlot==slot) { RestoreAllViews();return; }
            RestoreAllViews();
            _maximizedSlot=slot;
            foreach(Grid candidate in _legacySlots)
                if(candidate!=slot)candidate.Visibility=Visibility.Collapsed;
            Grid.SetRow(slot,0);Grid.SetColumn(slot,0);
            Grid.SetRowSpan(slot,2);Grid.SetColumnSpan(slot,2);
        }
        public void ToggleViewForTest(int index)
        {
            if(index<0||index>=_legacySlots.Count)throw new ArgumentOutOfRangeException("index");
            ToggleSlot(_legacySlots[index]);
        }
        public void RestoreAllViews()
        {
            if(_maximizedSlot==null)return;
            foreach(Grid candidate in _legacySlots)
            {
                int n=_legacySlots.IndexOf(candidate);
                Grid.SetRow(candidate,n>=2?1:0);
                Grid.SetColumn(candidate,n%2);
                Grid.SetRowSpan(candidate,1);Grid.SetColumnSpan(candidate,1);
                candidate.Visibility=Visibility.Visible;
            }
            _maximizedSlot=null;
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
