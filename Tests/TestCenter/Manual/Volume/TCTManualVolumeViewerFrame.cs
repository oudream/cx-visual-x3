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
        /// <summary>旧版MainForm的可见操作必须与工作区命令使用相同函数入口。</summary>
        public IEnumerable<TCTWorkspaceAction> Commands
        {
            get
            {
                Func<bool> loaded=()=>_volume!=null;
                return new[]
                {
                    WorkspaceAction("ct3d.openRaw","打开 RAW",OpenRaw),
                    WorkspaceAction("ct3d.openBmp","打开 BMP 目录",OpenBmpDirectory),
                    WorkspaceAction("ct3d.load.cancel","取消后台加载",CancelPendingVolumeLoads,
                        ()=>HasPendingVolumeLoads),
                    WorkspaceAction("ct3d.demo","演示体",LoadDemo),
                    WorkspaceAction("ct3d.clear","清空数据",ClearVolume,loaded),
                    WorkspaceAction("ct3d.fit","复位四视图",FitAllViews,loaded),
                    WorkspaceAction("ct3d.center","三刀居中",Center,loaded),
                    WorkspaceAction("ct3d.camera","3D相机复位",Reset3DCamera,loaded),
                    WorkspaceAction("ct3d.window","应用窗宽窗位",ApplyWindowCenterWidth,loaded),
                    WorkspaceAction("ct3d.gray","应用灰阶映射",UpdateLegacyGrayMapping,loaded),
                    WorkspaceAction("ct3d.screenshot","保存四视图PNG",SaveScreenshotDialog,loaded),
                    WorkspaceAction("ct3d.saveView","保存当前视图PNG",SaveSelectedViewDialog,loaded),
                    WorkspaceAction("ct3d.gpu.toggle","开启/关闭GPU",()=>
                        _enableGpu.IsChecked=_enableGpu.IsChecked!=true,loaded),
                    WorkspaceAction("ct3d.gpu.save","保存GPU画面PNG",SaveGpuFrame,()=>IsGpuVolumeRendering),
                    WorkspaceAction("ct3d.zoomIn","3D放大",()=>Zoom3D(1),loaded),
                    WorkspaceAction("ct3d.zoomOut","3D缩小",()=>Zoom3D(-1),loaded),
                    WorkspaceAction("ct3d.panLeft","3D左移",()=>Pan3D(-28,0),loaded),
                    WorkspaceAction("ct3d.panRight","3D右移",()=>Pan3D(28,0),loaded),
                    WorkspaceAction("ct3d.oblique.update","更新任意切",UpdateObliqueDefinition,loaded),
                    WorkspaceAction("ct3d.oblique.png","保存任意切PNG",SaveObliquePreviewDialog,
                        ()=>_state!=null && _state.ObliqueSlice.Visible),
                    WorkspaceAction("ct3d.oblique.raw","导出任意切Float32",SaveObliqueRawDialog,
                        ()=>_state!=null && _state.ObliqueSlice.Visible)
                };
            }
        }
        public string LastUiError { get; private set; }
        private TCTWorkspaceAction WorkspaceAction(string id,string label,Action action,
            Func<bool> canExecute=null)
        {
            return new TCTWorkspaceAction(id,label,delegate
            {
                if(canExecute!=null && !canExecute())
                {
                    LastUiError="当前没有满足命令 "+label+" 的操作条件。";
                    _status.Text=LastUiError;
                    RaiseStateChanged();
                    return;
                }
                ExecuteUiAction(action);
            },canExecute);
        }
        private void ExecuteUiAction(Action action)
        {
            try
            {
                LastUiError=null;
                action();
            }
            catch(Exception error)
            {
                LastUiError=error.Message;
                _status.Text="操作失败："+error.Message;
                RaiseStateChanged();
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
            BuildLegacyUi();
        }

        private static void Label(Panel panel, string text)
        {
            panel.Children.Add(new TextBlock { Text = text,
                FontWeight = FontWeights.SemiBold, FontSize = 14,
                Foreground = BrushOf(229,239,250), Margin = new Thickness(2,4,2,8) });
        }
        private void AddButton(Panel bar, string caption, string id, Action action)
        {
            Button b = new Button { Content = caption, Padding = new Thickness(10,4,10,4),
                Margin = new Thickness(4,5,2,5), MinWidth = 90 };
            AutomationProperties.SetAutomationId(b,id);
            b.Click += (s,e)=>ExecuteUiAction(action);
            bar.Children.Add(b);
        }
        private static void AddAxis(Panel panel, string name, Slider slider, string id)
        {
            panel.Children.Add(new TextBlock { Text = name, Foreground = BrushOf(218,231,244),
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

        public void ResetCameraForTest() { Reset3DCamera(); }

        private void Reset3DCamera()
        {
            _scene.ResetCamera();
            if (_gpu != null && _gpu.IsAvailable) _gpu.ResetCamera();
        }

        public void LoadDemo()
        {
            CancelPendingRawLoads();
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
                SyncSliceHeaderControls();
                SyncObliqueCenterControls();
            }
            finally { _updating = false; }
            if (previous != null && !Object.ReferenceEquals(previous,volume)) previous.Dispose();
            RefreshSlices();
            RefreshLegacyHistogram();
            UpdateGpuSettings(); // 控件状态先同步；下面UpdateGpuVolume用新体素一次生成新LUT
            UpdateGpuVolume();
            _status.Text = String.Format("{0} · {1}×{2}×{3} · Spacing {4:0.###}/{5:0.###}/{6:0.###}",
                volume.Name, volume.OriginalWidth, volume.OriginalHeight, volume.OriginalDepth,
                volume.SpacingX,volume.SpacingY,volume.SpacingZ);
            RaiseStateChanged();
            // 默认优先真正GPU体绘；等WPF视图完成布局后再构建HwndHost/WGL，
            // 显卡驱动不支持时由GpuFallback明确报因并保留WPF空间切面。
            if(_enableGpu.IsChecked==true && _gpu==null)
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,
                    new Action(delegate
                    {
                        if(Object.ReferenceEquals(_volume,volume) &&
                           _enableGpu.IsChecked==true && _gpu==null)
                            TryEnableGpu();
                    }));
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
            if(!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(()=>OnStateChanged(sender,e)));
                return;
            }
            if(e.PropertyName=="SliceViewports")
            {
                UpdateRenderedSliceViewports();
                return;
            }
            RefreshSlices();
        }

        private void UpdateRenderedSliceViewports()
        {
            if(_volume==null || _state==null)return;
            CxVisual.Mechanism.Visual.MVISliceViewport xy=_state.GetSliceViewport(FVOSlicePlane.XY);
            CxVisual.Mechanism.Visual.MVISliceViewport xz=_state.GetSliceViewport(FVOSlicePlane.XZ);
            CxVisual.Mechanism.Visual.MVISliceViewport yz=_state.GetSliceViewport(FVOSlicePlane.YZ);
            _scene.SetSliceViewports(xy,xz,yz,_state.SliceX,_state.SliceY,_state.SliceZ,
                _volume.OriginalWidth,_volume.OriginalHeight,_volume.OriginalDepth,
                _volume.SpacingX,_volume.SpacingY,_volume.SpacingZ);
            if(_gpu!=null && _gpu.IsAvailable)
                _gpu.SetSliceViewports(xy,xz,yz);
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
                SyncSliceHeaderControls();
                byte low = (byte)Math.Round(Math.Min(_windowMin.Value,_windowMax.Value));
                byte high = (byte)Math.Round(Math.Max(_windowMin.Value,_windowMax.Value));
                bool pseudo = _pseudo.IsChecked == true;
                FIMImageBuffer xy = FVOSliceImageBuilder.Build(_volume,
                    _state.SliceX,_state.SliceY,_state.SliceZ,FVOSlicePlane.XY,pseudo,low,high);
                FIMImageBuffer xz = FVOSliceImageBuilder.Build(_volume,
                    _state.SliceX,_state.SliceY,_state.SliceZ,FVOSlicePlane.XZ,pseudo,low,high);
                FIMImageBuffer yz = FVOSliceImageBuilder.Build(_volume,
                    _state.SliceX,_state.SliceY,_state.SliceZ,FVOSlicePlane.YZ,pseudo,low,high);
                if(_autoSliceFit.IsChecked==true)
                {
                    _xy.ShowOriginal(xy);
                    _xz.ShowOriginal(xz);
                    _yz.ShowOriginal(yz);
                }
                else
                {
                    _xy.UpdateOriginalFrame(xy);
                    _xz.UpdateOriginalFrame(xz);
                    _yz.UpdateOriginalFrame(yz);
                }
                UpdateSliceLegends();
                _scene.SetSlices(xy,xz,yz,_state.SliceX,_state.SliceY,_state.SliceZ,
                    _volume.OriginalWidth,_volume.OriginalHeight,_volume.OriginalDepth,
                    _volume.SpacingX,_volume.SpacingY,_volume.SpacingZ);
                UpdateRenderedSliceViewports();
                UpdateCrosshairs();
                UpdateGpuSlicePositions();
                RefreshOblique();
                _sliceStatus.Text = String.Format("位置：X {0} / Y {1} / Z {2}",
                    _state.SliceX,_state.SliceY,_state.SliceZ);
            }
            finally { _updating = false; }
            RaiseStateChanged();
        }

        private async void OpenRaw()
        {
            OpenFileDialog dialog = new OpenFileDialog {
                Filter = "RAW体数据 (*.raw)|*.raw|所有文件 (*.*)|*.*", Title = "打开RAW体积文件" };
            if (dialog.ShowDialog() != true) return;
            try { await LoadRawFileAsync(dialog.FileName,System.Threading.CancellationToken.None); }
            catch (System.OperationCanceledException) { _status.Text="RAW读取已取消。"; }
            catch (Exception ex) { MessageBox.Show(ex.Message,"RAW载入失败"); }
        }

        private async void OpenBmpDirectory()
        {
            OpenFileDialog dialog = new OpenFileDialog {
                Filter = "BMP切片 (*.bmp)|*.bmp", Title = "从BMP目录选择任一张图像" };
            if (dialog.ShowDialog() != true) return;
            try
            {
                await LoadBmpDirectoryAsync(Path.GetDirectoryName(dialog.FileName),
                    System.Threading.CancellationToken.None);
            }
            catch (System.OperationCanceledException) { _status.Text="BMP读取已取消。"; }
            catch (Exception ex) { _status.Text="BMP载入失败："+ex.Message; MessageBox.Show(ex.Message,"BMP目录载入失败"); }
        }

        public static FVOVolumeData LoadBmpStack(string directory)
        {
            return LoadBmpStack(directory,System.Threading.CancellationToken.None);
        }

        /// <summary>兼容现有WPF工作台入口；BMP像素解析由正式MPW图像服务实现。</summary>
        public static FVOVolumeData LoadBmpStack(string directory,
            System.Threading.CancellationToken cancellation)
        {
            int width, height, depth;
            byte[] pixels = MPWImageSequenceReader.ReadBmpGray8Stack(directory,
                cancellation, out width, out height, out depth);
            return new FVOVolumeData(width,height,depth,pixels,
                "BMP序列 ("+depth+"张)");
        }

        public void OnWorkspaceClosed()
        {
            CancelPendingRawLoads();
            DisableGpu(); // 先释放WGL上下文和全部GL资源，再释放其可能引用的FVO像素。
            if(_enableGpu.IsChecked==true)_enableGpu.IsChecked=false;
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
            _obliqueViewer.Clear();
            RestoreAllViews();
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
