using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CxVisual.Foundation.Imaging;
using CxVisual.Foundation.Volume;
using CxVisual.Mechanism.Visual;
using CxVisual.Mechanism.Presentation.Wpf.Imaging;
using CxVisual.Mechanism.Presentation.Wpf.Volume;
using Microsoft.Win32;

namespace CxVisual.TestCenter.UI
{
    /// <summary>
    /// 三维工作台可操作的WPF首个闭环：真实RAW/BMP体数据、三正交图像及3D切面同步。
    /// 3D为原生WPF空间切片，并非旧OpenGL Raymarch体绘；后续替换渲染方法不影响FVO/MVI唯一状态。
    /// </summary>
    public sealed partial class TCTManualVolumeViewerFrame : UserControl, ITCTWorkspaceFrame
    {
        private readonly MPWImageViewerControl _xy = new MPWImageViewerControl();
        private readonly MPWImageViewerControl _xz = new MPWImageViewerControl();
        private readonly MPWImageViewerControl _yz = new MPWImageViewerControl();
        private readonly MPWVolumeSliceSceneControl _scene = new MPWVolumeSliceSceneControl();
        private readonly Slider _sliderX = new Slider { Minimum = 0, TickFrequency = 1, IsSnapToTickEnabled = true };
        private readonly Slider _sliderY = new Slider { Minimum = 0, TickFrequency = 1, IsSnapToTickEnabled = true };
        private readonly Slider _sliderZ = new Slider { Minimum = 0, TickFrequency = 1, IsSnapToTickEnabled = true };
        private readonly TextBlock _sliceStatus = new TextBlock();
        private readonly TextBlock _status = new TextBlock { Text = "请选择RAW/BMP体数据或生成演示体。" };
        private readonly CheckBox _pseudo = new CheckBox { Content = "伪彩色切片" };
        private readonly Slider _windowMin = new Slider { Minimum = 0, Maximum = 255, Value = 0 };
        private readonly Slider _windowMax = new Slider { Minimum = 0, Maximum = 255, Value = 255 };
        private FVOVolumeData _volume;
        private MVIVolumeViewState _state;
        private bool _updating;

        public string WorkspaceId { get { return "manual.ct3d"; } }
        public IEnumerable<TCTWorkspaceAction> Commands
        {
            get
            {
                return new[]
                {
                    new TCTWorkspaceAction("ct3d.demo", "演示体", LoadDemo),
                    new TCTWorkspaceAction("ct3d.center", "三刀居中", Center),
                    new TCTWorkspaceAction("ct3d.camera", "3D相机复位", () => _scene.ResetCamera())
                };
            }
        }
        public event Action StateChanged;
        public FVOVolumeData Volume { get { return _volume; } }
        public MVIVolumeViewState ViewState { get { return _state; } }
        public MPWVolumeSliceSceneControl Scene { get { return _scene; } }
        public MPWImageViewerControl XyViewer { get { return _xy; } }
        public MPWImageViewerControl XzViewer { get { return _xz; } }
        public MPWImageViewerControl YzViewer { get { return _yz; } }

        public TCTManualVolumeViewerFrame()
        {
            BuildUi();
            _sliderX.ValueChanged += (s,e) => ChangeAxis(0, (int)Math.Round(_sliderX.Value));
            _sliderY.ValueChanged += (s,e) => ChangeAxis(1, (int)Math.Round(_sliderY.Value));
            _sliderZ.ValueChanged += (s,e) => ChangeAxis(2, (int)Math.Round(_sliderZ.Value));
            _pseudo.Checked += (s,e) => RefreshSlices();
            _pseudo.Unchecked += (s,e) => RefreshSlices();
            _windowMin.ValueChanged += (s,e) => RefreshSlices();
            _windowMax.ValueChanged += (s,e) => RefreshSlices();
        }

        private static Brush BrushOf(byte r, byte g, byte b)
        {
            return new SolidColorBrush(Color.FromRgb(r,g,b));
        }

        private void BuildUi()
        {
            Background = BrushOf(241, 245, 250);
            AutomationProperties.SetAutomationId(this, "TCT.Volume.Workbench");
            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(43) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(29) });
            Content = root;

            StackPanel bar = new StackPanel { Orientation = Orientation.Horizontal,
                Background = BrushOf(249, 251, 255), Margin = new Thickness(0,0,0,2) };
            AddButton(bar, "打开 RAW", "TCT.Volume.OpenRaw", OpenRaw);
            AddButton(bar, "打开 BMP 目录", "TCT.Volume.OpenBmp", OpenBmpDirectory);
            AddButton(bar, "生成演示体", "TCT.Volume.Demo", LoadDemo);
            AddButton(bar, "三刀居中", "TCT.Volume.Center", Center);
            AddButton(bar, "重置相机", "TCT.Volume.ResetCamera", () => _scene.ResetCamera());
            root.Children.Add(bar);

            Grid work = new Grid { Margin = new Thickness(5,3,5,3) };
            work.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            work.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300), MinWidth = 260 });
            Grid.SetRow(work,1); root.Children.Add(work);
            Grid four = new Grid { Background = BrushOf(21, 29, 40) };
            four.RowDefinitions.Add(new RowDefinition());
            four.RowDefinitions.Add(new RowDefinition());
            four.ColumnDefinitions.Add(new ColumnDefinition());
            four.ColumnDefinitions.Add(new ColumnDefinition());
            work.Children.Add(four);
            AddSliceViewport(four, _xy, FVOSlicePlane.XY, "XY / 轴向", 0,0);
            AddSliceViewport(four, _yz, FVOSlicePlane.YZ, "YZ / 矢状", 0,1);
            AddSliceViewport(four, _xz, FVOSlicePlane.XZ, "XZ / 冠状", 1,0);
            AddViewport(four, _scene, "3D / 空间切片", 1,1);

            ScrollViewer rightScroll = new ScrollViewer {
                Background = BrushOf(249,251,255),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Grid.SetColumn(rightScroll,1); work.Children.Add(rightScroll);
            StackPanel settings = new StackPanel { Margin = new Thickness(10,8,10,8) };
            rightScroll.Content = settings;
            Label(settings, "切片导航（三刀联动）");
            AddAxis(settings, "X", _sliderX, "TCT.Volume.SliceX");
            AddAxis(settings, "Y", _sliderY, "TCT.Volume.SliceY");
            AddAxis(settings, "Z", _sliderZ, "TCT.Volume.SliceZ");
            _sliceStatus.Margin = new Thickness(4,4,4,8);
            _sliceStatus.Foreground = BrushOf(42,71,105);
            settings.Children.Add(_sliceStatus);
            settings.Children.Add(new Separator { Margin = new Thickness(2,8,2,10) });
            Label(settings,"二维显示设置");
            _pseudo.Margin = new Thickness(5,5,5,8);
            settings.Children.Add(_pseudo);
            AddAxis(settings, "窗下限（0-255）", _windowMin, "TCT.Volume.WindowMin");
            AddAxis(settings, "窗上限（0-255）", _windowMax, "TCT.Volume.WindowMax");
            settings.Children.Add(new Separator { Margin = new Thickness(2,10,2,10) });
            settings.Children.Add(new TextBlock { Text = "Ctrl+点击切片：移动十字线    Shift+滚轮：切换切片", FontSize = 11, TextWrapping=TextWrapping.Wrap, Foreground=BrushOf(58,98,150), Margin=new Thickness(4,8,4,7) });
            Label(settings, "三维可视化");
            settings.Children.Add(new TextBlock {
                Text = "已实现：三正交物理切面、共享三刀、相机旋转和缩放。\n" +
                       "待接入：旧OpenGL体绘、Shader、材质光照和传递函数GPU编辑。",
                Foreground = BrushOf(77,91,115), TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(4,6,4,8) });
            _status.Foreground = BrushOf(56,83,113);
            _status.Margin = new Thickness(8,4,8,4);
            _status.TextTrimming = TextTrimming.CharacterEllipsis;
            Border footer = new Border { Background = BrushOf(235,243,253),
                BorderBrush = BrushOf(204,216,233), BorderThickness = new Thickness(0,1,0,0),
                Child = _status };
            Grid.SetRow(footer,2); root.Children.Add(footer);
        }

        private static void Label(Panel panel, string text)
        {
            panel.Children.Add(new TextBlock { Text = text,
                FontWeight = FontWeights.SemiBold, FontSize = 14,
                Foreground = BrushOf(38,65,94), Margin = new Thickness(2,4,2,8) });
        }
        private static void AddButton(Panel bar, string caption, string id, Action action)
        {
            Button b = new Button { Content = caption, Padding = new Thickness(10,4,10,4),
                Margin = new Thickness(4,5,2,5), MinWidth = 90 };
            AutomationProperties.SetAutomationId(b,id);
            b.Click += (s,e)=>action();
            bar.Children.Add(b);
        }
        private static void AddAxis(Panel panel, string name, Slider slider, string id)
        {
            panel.Children.Add(new TextBlock { Text = name, Foreground = BrushOf(60,78,105),
                Margin = new Thickness(3,7,3,1) });
            slider.Margin = new Thickness(5,0,5,8);
            AutomationProperties.SetAutomationId(slider,id);
            panel.Children.Add(slider);
        }
        private static Grid AddViewport(Grid grid, UIElement control, string caption, int row, int column)
        {
            Grid host = new Grid { Margin = new Thickness(2), Background = BrushOf(8,14,22) };
            host.RowDefinitions.Add(new RowDefinition { Height = new GridLength(24) });
            host.RowDefinitions.Add(new RowDefinition());
            Border title = new Border { Background = BrushOf(245,248,252),
                Padding = new Thickness(6,2,4,2), Child = new TextBlock {
                    Text = caption, Foreground = BrushOf(36,65,105),
                    FontSize = 12, FontWeight = FontWeights.SemiBold } };
            host.Children.Add(title);
            Grid.SetRow(control,1); host.Children.Add(control);
            Grid.SetRow(host,row); Grid.SetColumn(host,column); grid.Children.Add(host);
            return host;
        }

        public void LoadDemo()
        {
            LoadVolume(FVOVolumeData.CreateDemo(72,68,60));
        }

        public void LoadVolume(FVOVolumeData volume)
        {
            if (volume == null) throw new ArgumentNullException("volume");
            FVOVolumeData previous = _volume;
            if (_state != null) _state.PropertyChanged -= OnStateChanged;
            _updating = true;
            try
            {
                _volume = volume;
                _state = MVIVolumeViewState.CreateCentered(volume);
                _state.PropertyChanged += OnStateChanged;
                _sliderX.Maximum = volume.OriginalWidth - 1;
                _sliderY.Maximum = volume.OriginalHeight - 1;
                _sliderZ.Maximum = volume.OriginalDepth - 1;
                _sliderX.Value = _state.SliceX;
                _sliderY.Value = _state.SliceY;
                _sliderZ.Value = _state.SliceZ;
            }
            finally { _updating = false; }
            if (previous != null && !Object.ReferenceEquals(previous,volume)) previous.Dispose();
            RefreshSlices();
            _status.Text = String.Format("{0} · {1}×{2}×{3} · Spacing {4:0.###}/{5:0.###}/{6:0.###}",
                volume.Name, volume.OriginalWidth, volume.OriginalHeight, volume.OriginalDepth,
                volume.SpacingX,volume.SpacingY,volume.SpacingZ);
            RaiseStateChanged();
        }

        public void MoveSlices(int x, int y, int z)
        {
            if (_state == null) return;
            _state.SetSlices(x, y, z, _volume);
        }
        public void Center()
        {
            if (_volume != null) _state.ResetCentered(_volume);
        }
        private void ChangeAxis(int axis, int value)
        {
            if (_updating || _volume == null) return;
            if (axis == 0) _state.SetSliceX(value, _volume);
            else if (axis == 1) _state.SetSliceY(value, _volume);
            else _state.SetSliceZ(value, _volume);
        }
        private void OnStateChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(RefreshSlices)); return; }
            RefreshSlices();
        }
        private void RefreshSlices()
        {
            if (_updating || _volume == null || _state == null) return;
            _updating = true;
            try
            {
                _sliderX.Value = _state.SliceX;
                _sliderY.Value = _state.SliceY;
                _sliderZ.Value = _state.SliceZ;
                byte low = (byte)Math.Round(Math.Min(_windowMin.Value,_windowMax.Value));
                byte high = (byte)Math.Round(Math.Max(_windowMin.Value,_windowMax.Value));
                bool pseudo = _pseudo.IsChecked == true;
                FIMImageBuffer xy = FVOSliceImageBuilder.Build(_volume,
                    _state.SliceX,_state.SliceY,_state.SliceZ,FVOSlicePlane.XY,pseudo,low,high);
                FIMImageBuffer xz = FVOSliceImageBuilder.Build(_volume,
                    _state.SliceX,_state.SliceY,_state.SliceZ,FVOSlicePlane.XZ,pseudo,low,high);
                FIMImageBuffer yz = FVOSliceImageBuilder.Build(_volume,
                    _state.SliceX,_state.SliceY,_state.SliceZ,FVOSlicePlane.YZ,pseudo,low,high);
                _xy.ShowOriginal(xy);
                _xz.ShowOriginal(xz);
                _yz.ShowOriginal(yz);
                _scene.SetSlices(xy,xz,yz,_state.SliceX,_state.SliceY,_state.SliceZ,
                    _volume.OriginalWidth,_volume.OriginalHeight,_volume.OriginalDepth,
                    _volume.SpacingX,_volume.SpacingY,_volume.SpacingZ);
                UpdateCrosshairs();
                _sliceStatus.Text = String.Format("位置：X {0} / Y {1} / Z {2}",
                    _state.SliceX,_state.SliceY,_state.SliceZ);
            }
            finally { _updating = false; }
            RaiseStateChanged();
        }

        private void OpenRaw()
        {
            OpenFileDialog dialog = new OpenFileDialog {
                Filter = "RAW体数据 (*.raw)|*.raw|所有文件 (*.*)|*.*", Title = "打开RAW体积文件" };
            if (dialog.ShowDialog() != true) return;
            try { LoadVolume(FVORawVolumeLoader.Load(dialog.FileName)); }
            catch (Exception ex) { MessageBox.Show(ex.Message,"RAW载入失败"); }
        }

        private void OpenBmpDirectory()
        {
            OpenFileDialog dialog = new OpenFileDialog {
                Filter = "BMP切片 (*.bmp)|*.bmp", Title = "从BMP目录选择任一张图像" };
            if (dialog.ShowDialog() != true) return;
            try { LoadVolume(LoadBmpStack(Path.GetDirectoryName(dialog.FileName))); }
            catch (Exception ex) { MessageBox.Show(ex.Message,"BMP目录载入失败"); }
        }

        public static FVOVolumeData LoadBmpStack(string directory)
        {
            if (String.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException("directory");
            string[] files = Directory.GetFiles(directory, "*.bmp")
                .OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).ToArray();
            if (files.Length == 0) throw new InvalidDataException("目录内没有BMP切片。");
            int width = 0, height = 0;
            byte[] pixels = null;
            for (int z=0;z<files.Length;z++)
            {
                BitmapFrame source = BitmapFrame.Create(new Uri(files[z]),BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad);
                FormatConvertedBitmap mono = new FormatConvertedBitmap(source, PixelFormats.Gray8, null, 0);
                int w = mono.PixelWidth, h = mono.PixelHeight;
                if (z == 0)
                {
                    width = w; height = h;
                    long count = checked((long)w * h * files.Length);
                    if (count > 512L*1024*1024) throw new InvalidDataException("BMP目录超过512MiB上限，请改用RAW。");
                    pixels = new byte[checked((int)count)];
                }
                else if (w != width || h != height)
                    throw new InvalidDataException("BMP序列包含尺寸不一致的切片：" + files[z]);
                mono.CopyPixels(pixels, checked(width * z * height), width);
            }
            return new FVOVolumeData(width,height,files.Length,pixels,
                "BMP序列 ("+files.Length+"张)");
        }

        public void OnWorkspaceClosed()
        {
            _updating = true;
            if (_state != null) _state.PropertyChanged -= OnStateChanged;
            if (_volume != null) _volume.Dispose();
            _volume = null;
            _state = null;
            _xy.Clear();
            _xz.Clear();
            _yz.Clear();
            _scene.Clear();
            ClearCrosshairs();
            _updating = false;
            _status.Text = "三维工作区已关闭，体数据与三维切面已释放。";
        }

        private void RaiseStateChanged()
        {
            Action action = StateChanged;
            if (action != null) action();
        }
    }
}
