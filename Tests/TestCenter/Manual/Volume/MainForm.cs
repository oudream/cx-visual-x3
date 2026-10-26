using CxECTVolumeCore.Rendering;
using CxECTVolumeCore.Utils;
using CxECTVolumeCore.Volume;
using CxECTVolumeWinForms.Rendering;
using CxECTVolumeWinForms.Views;
using CommonInterfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CxECT3DViewer
{
    public partial class MainForm : Form
    {
        // MainForm 只做应用层编排：加载体数据、刷新参数图表、同步切面状态。
        // 真正的 OpenGL 资源和 shader 渲染都封装在 RenderingVolumeControl 内。
        private VolumeData _volume;
        private RenderingHistogram _histogram;
        private readonly VolumeState _state;
        private readonly FourViewLayoutControl _fourView;
        private readonly RenderingVolumeControl _renderer;
        private readonly Label _volumeInfoLabel;
        private readonly Label _sliceXValue;
        private readonly Label _sliceYValue;
        private readonly Label _sliceZValue;
        private readonly TrackBar _sliceXSlider;
        private readonly TrackBar _sliceYSlider;
        private readonly TrackBar _sliceZSlider;
        private readonly Button _loadRawButton;
        private readonly RenderingTransferFunctionEditor _thresholdEditor;
        private readonly RenderingTransferFunctionEditor _colorEditor;
        private readonly RenderingTransferFunctionEditor _scalarOpacityEditor;
        private readonly RenderingTransferFunctionEditor _gradientOpacityEditor;
        private int _sliceWindowCenter = 128;
        private int _sliceWindowWidth = 256;
        // --- #加速加载-1：大文件首屏使用 128³ 预览体；完整 RAW 读取移到后台，避免首屏等待数 GB 顺序读盘。
        private const int LargeRawPreviewAxis = 128;
        private const long LargeRawPreviewThresholdBytes = 512L * 1024L * 1024L;

        public MainForm()
        {
            InitializeComponent();

            Text = "CxECT 3D Viewer";
            Width = 1500;
            Height = 900;
            MinimumSize = new Size(1100, 720);
            StartPosition = FormStartPosition.CenterScreen;

            _volume = VolumeData.CreateDemo(128, 128, 128);
            _histogram = RenderingHistogram.FromVolume(_volume);
            _state = VolumeState.CreateCentered(_volume);
            FormClosed += delegate { _volume.Dispose(); };

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.FromArgb(18, 20, 24),
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 470));
            Controls.Add(root);

            _fourView = new FourViewLayoutControl(_volume, _state)
            {
                Dock = DockStyle.Fill,
            };
            _fourView.ActiveVoxelInfoChanged += delegate { UpdateVolumeInfo(); };
            _renderer = _fourView.Renderer;
            root.Controls.Add(_fourView, 0, 0);

            var panel = BuildSidePanel();
            root.Controls.Add(panel, 1, 0);

            _loadRawButton = new Button
            {
                Text = "加载 RAW",
                Width = 410,
                Height = 34,
                Margin = new Padding(0, 4, 0, 8),
            };
            _loadRawButton.Click += OnLoadRawClicked;
            panel.Controls.Add(_loadRawButton);

            var screenshotButton = new Button
            {
                Text = "保存四视图截图",
                Width = 410,
                Height = 32,
                Margin = new Padding(0, 0, 0, 8),
            };
            screenshotButton.Click += OnSaveScreenshotClicked;
            panel.Controls.Add(screenshotButton);

            var resetViewsButton = new Button
            {
                Text = "重置 / 适应四视图",
                Width = 410,
                Height = 32,
                Margin = new Padding(0, 0, 0, 8),
            };
            resetViewsButton.Click += delegate
            {
                _state.ResetCentered(_volume);
                _fourView.FitSliceViews();
                _renderer.ResetCameraView();
            };
            panel.Controls.Add(resetViewsButton);

            _volumeInfoLabel = new Label
            {
                Width = 410,
                Height = 112,
                ForeColor = Color.FromArgb(190, 199, 212),
                Margin = new Padding(0, 0, 0, 8),
            };
            panel.Controls.Add(_volumeInfoLabel);

            AddQuickRenderControls(panel);

            var transferFunctions = _renderer.TransferFunctions;
            _thresholdEditor = new RenderingTransferFunctionEditor(RenderingTransferEditorKind.Threshold, transferFunctions);
            _colorEditor = new RenderingTransferFunctionEditor(RenderingTransferEditorKind.Color, transferFunctions);
            _scalarOpacityEditor = new RenderingTransferFunctionEditor(RenderingTransferEditorKind.ScalarOpacity, transferFunctions);
            _gradientOpacityEditor = new RenderingTransferFunctionEditor(RenderingTransferEditorKind.GradientOpacity, transferFunctions);
            AddTransferTabs(panel);

            AddSliceSlider(panel, "X 切面", delegate (int value) { _state.SetSliceX(value, _volume); }, out _sliceXValue, out _sliceXSlider);
            AddSliceSlider(panel, "Y 切面", delegate (int value) { _state.SetSliceY(value, _volume); }, out _sliceYValue, out _sliceYSlider);
            AddSliceSlider(panel, "Z 切面", delegate (int value) { _state.SetSliceZ(value, _volume); }, out _sliceZValue, out _sliceZSlider);

            ApplyVolumeToUi();
            _state.PropertyChanged += delegate
            {
                UpdateSliceLabels();
                UpdateSliceSliderValues();
            };
        }

        private static FlowLayoutPanel BuildSidePanel()
        {
            return new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(18),
                BackColor = Color.FromArgb(28, 31, 36),
            };
        }

        private void AddQuickRenderControls(FlowLayoutPanel panel)
        {
            var showSlices = new CheckBox
            {
                Text = "显示切面",
                Checked = true,
                Width = 410,
                Height = 28,
                ForeColor = Color.FromArgb(232, 236, 242),
                Margin = new Padding(0, 4, 0, 0),
            };
            showSlices.CheckedChanged += delegate { _renderer.UpdateRenderSettings(delegate (RenderingSettings settings) { settings.ShowSlices = showSlices.Checked; }); };
            panel.Controls.Add(showSlices);

            var showSliceTexture = new CheckBox
            {
                Text = "显示 3D 切片图案",
                Checked = false,
                Width = 410,
                Height = 28,
                ForeColor = Color.FromArgb(232, 236, 242),
                Margin = new Padding(0, 4, 0, 0),
            };
            // 这个开关独立控制三张平面上的体数据图案，可在隐藏红/绿/蓝定位面时单独开启。
            showSliceTexture.CheckedChanged += delegate { _renderer.UpdateRenderSettings(delegate (RenderingSettings settings) { settings.ShowSliceTexture = showSliceTexture.Checked; }); };
            panel.Controls.Add(showSliceTexture);

            var showVolume = new CheckBox
            {
                Text = "显示体渲染",
                Checked = true,
                Width = 410,
                Height = 28,
                ForeColor = Color.FromArgb(232, 236, 242),
                Margin = new Padding(0, 4, 0, 0),
            };
            showVolume.CheckedChanged += delegate { _renderer.UpdateRenderSettings(delegate (RenderingSettings settings) { settings.ShowVolume = showVolume.Checked; }); };
            panel.Controls.Add(showVolume);

            var showVolumeBounds = new CheckBox
            {
                Text = "显示体积外接框",
                Checked = false,
                Width = 410,
                Height = 28,
                ForeColor = Color.FromArgb(232, 236, 242),
                Margin = new Padding(0, 4, 0, 0),
            };
            showVolumeBounds.CheckedChanged += delegate { _renderer.UpdateRenderSettings(delegate (RenderingSettings settings) { settings.ShowVolumeBounds = showVolumeBounds.Checked; }); };
            panel.Controls.Add(showVolumeBounds);

            var showCrosshair = new CheckBox
            {
                Text = "显示十字线",
                Checked = true,
                Width = 410,
                Height = 28,
                ForeColor = Color.FromArgb(232, 236, 242),
                Margin = new Padding(0, 4, 0, 0),
            };
            showCrosshair.CheckedChanged += delegate { _state.ShowCrosshair = showCrosshair.Checked; };
            panel.Controls.Add(showCrosshair);

            var pseudoColor = new CheckBox
            {
                Text = "2D 伪彩色",
                Checked = false,
                Width = 410,
                Height = 28,
                ForeColor = Color.FromArgb(232, 236, 242),
                Margin = new Padding(0, 4, 0, 0),
            };
            pseudoColor.CheckedChanged += delegate { _fourView.SetSlicePseudoColor(pseudoColor.Checked); };
            panel.Controls.Add(pseudoColor);

            AddSettingSlider(panel, "切面透明度", 35, delegate (int value)
            {
                _renderer.UpdateRenderSettings(delegate (RenderingSettings settings) { settings.SliceOpacity = value / 100.0f; });
            });

            AddSettingSlider(panel, "体渲染强度", 80, delegate (int value)
            {
                _renderer.UpdateRenderSettings(delegate (RenderingSettings settings) { settings.VolumeOpacity = value / 100.0f; });
            });

            AddSettingSlider(panel, "2D 窗位", _sliceWindowCenter * 100 / 255, delegate (int value)
            {
                _sliceWindowCenter = FrameworkCompat.Clamp((int)Math.Round(value / 100.0 * 255.0), 0, 255);
                UpdateSliceWindow();
            });

            AddSettingSlider(panel, "2D 窗宽", 100, delegate (int value)
            {
                _sliceWindowWidth = FrameworkCompat.Clamp((int)Math.Round(Math.Max(1, value) / 100.0 * 256.0), 1, 256);
                UpdateSliceWindow();
            });
        }

        private void AddTransferTabs(FlowLayoutPanel panel)
        {
            var tabs = new TabControl
            {
                Width = 410,
                Height = 360,
                Margin = new Padding(0, 12, 0, 8),
            };

            // 四张图表共用同一套 RenderingTransferFunctionSet，拖动控制点后会重新烘焙 1D 纹理给 shader。
            AddTab(tabs, "Threshold", _thresholdEditor);
            AddTab(tabs, "Color", _colorEditor);
            AddTab(tabs, "Scalar Opacity", _scalarOpacityEditor);
            AddTab(tabs, "Gradient Opacity", _gradientOpacityEditor);
            panel.Controls.Add(tabs);

            _thresholdEditor.TransferChanged += OnTransferChanged;
            _colorEditor.TransferChanged += OnTransferChanged;
            _scalarOpacityEditor.TransferChanged += OnTransferChanged;
            _gradientOpacityEditor.TransferChanged += OnTransferChanged;
        }

        private static void AddTab(TabControl tabs, string title, RenderingTransferFunctionEditor editor)
        {
            var page = new TabPage(title)
            {
                BackColor = Color.White,
                Padding = new Padding(6),
            };
            editor.Dock = DockStyle.Fill;
            page.Controls.Add(editor);
            tabs.TabPages.Add(page);
        }

        private void AddSliceSlider(FlowLayoutPanel panel, string title, Action<int> onChanged, out Label valueLabel, out TrackBar slider)
        {
            var titleLabel = new Label
            {
                Text = title,
                Width = 410,
                Height = 24,
                ForeColor = Color.FromArgb(232, 236, 242),
                Font = new Font(Font, FontStyle.Bold),
                Margin = new Padding(0, 12, 0, 0),
            };
            panel.Controls.Add(titleLabel);

            valueLabel = new Label
            {
                Width = 410,
                Height = 22,
                ForeColor = Color.FromArgb(165, 174, 188),
            };
            panel.Controls.Add(valueLabel);

            var createdSlider = new TrackBar
            {
                Width = 410,
                Minimum = 0,
                Maximum = 1,
                TickFrequency = 1,
                SmallChange = 1,
                LargeChange = 1,
            };
            createdSlider.Scroll += delegate { onChanged(createdSlider.Value); };
            panel.Controls.Add(createdSlider);
            slider = createdSlider;
        }

        private void AddSettingSlider(FlowLayoutPanel panel, string title, int value, Action<int> onChanged)
        {
            var valueLabel = new Label
            {
                Text = string.Format("{0}: {1}%", title, value),
                Width = 410,
                Height = 22,
                ForeColor = Color.FromArgb(190, 199, 212),
                Margin = new Padding(0, 6, 0, 0),
            };
            panel.Controls.Add(valueLabel);

            var slider = new TrackBar
            {
                Width = 410,
                Minimum = 0,
                Maximum = 100,
                Value = value,
                TickFrequency = 20,
                SmallChange = 1,
                LargeChange = 10,
            };
            slider.Scroll += delegate
            {
                valueLabel.Text = string.Format("{0}: {1}%", title, slider.Value);
                onChanged(slider.Value);
            };
            panel.Controls.Add(slider);
        }

        private async void OnLoadRawClicked(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog
            {
                Title = "选择体数据 RAW 文件",
                Filter = "RAW volume (*.raw)|*.raw|All files (*.*)|*.*",
                CheckFileExists = true,
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                SetLoadingState(true, "正在加载 RAW...");
                var totalStopwatch = Stopwatch.StartNew();

                try
                {
                    var fileSizeBytes = new System.IO.FileInfo(dialog.FileName).Length;
                    // --- #加速加载-2：仅大文件降低 3D 预览分辨率，小文件仍使用原有 512³ 上限。
                    var previewAxis = fileSizeBytes >= LargeRawPreviewThresholdBytes ? LargeRawPreviewAxis : 512;
                    LogHelper.Information(string.Format("开始加载 RAW：文件={0}，大小={1:N2} GB", dialog.FileName, fileSizeBytes / 1024.0 / 1024.0 / 1024.0), "CxECT3DViewer.Performance");
                    // --- 1. 用户选择 RAW 后，后台线程把磁盘文件解析成 VolumeData，避免阻塞 WinForms UI 线程。
                    var previewLoadStopwatch = Stopwatch.StartNew();
                    var loadedVolume = await Task.Run(delegate { return RawVolumeLoader.Load(dialog.FileName, previewAxis); });
                    previewLoadStopwatch.Stop();
                    LogHelper.Information(string.Format("RAW 预览抽样完成：{0}x{1}x{2}，耗时={3}ms", loadedVolume.Width, loadedVolume.Height, loadedVolume.Depth, previewLoadStopwatch.ElapsedMilliseconds), "CxECT3DViewer.Performance");
                    LogHelper.Information(string.Format("RAW 预览明细：目标轴={0}，抽样读盘={1}ms，归一化={2}ms", previewAxis, loadedVolume.LoadTiming.PreviewSamplingMilliseconds, loadedVolume.LoadTiming.PreviewNormalizationMilliseconds), "CxECT3DViewer.Performance");
                    // --- 2. MainForm 只保存当前体数据引用；真正显示由四视图控件继续分发给 2D 和 3D 子控件。
                    var previousVolume = _volume;
                    _volume = loadedVolume;
                    // 直方图只用于 UI 背景和参数调节参考，加载完成后异步计算，避免阻塞界面绘制。
                    var histogramStopwatch = Stopwatch.StartNew();
                    _histogram = await Task.Run(delegate { return RenderingHistogram.FromVolume(_volume); });
                    histogramStopwatch.Stop();
                    LogHelper.Information(string.Format("直方图计算完成，耗时={0}ms", histogramStopwatch.ElapsedMilliseconds), "CxECT3DViewer.Performance");
                    // --- 3. 切片状态使用原始体素坐标，滑条、2D 十字线、3D 彩色切面共用这一份状态。
                    _state.ResetCentered(_volume);
                    // 大文件先构建低分辨率切片；原始切片在后台 RAW 常驻后再替换，避免首次显示被 2.5GB 读盘阻塞。
                    var sliceLoadStopwatch = Stopwatch.StartNew();
                    _fourView.LoadSliceViews(_volume);
                    sliceLoadStopwatch.Stop();
                    var gpuLoadStopwatch = Stopwatch.StartNew();
                    _renderer.LoadVolume(_volume);
                    gpuLoadStopwatch.Stop();
                    var transferFunctionStopwatch = Stopwatch.StartNew();
                    ApplyVolumeToUi();
                    PushTransferFunctionsToGpu();
                    transferFunctionStopwatch.Stop();
                    LogHelper.Information(string.Format("首次显示明细：预览 2D 切片={0}ms，3D 纹理上传={1}ms，传递函数上传/UI={2}ms", sliceLoadStopwatch.ElapsedMilliseconds, gpuLoadStopwatch.ElapsedMilliseconds, transferFunctionStopwatch.ElapsedMilliseconds), "CxECT3DViewer.Performance");
                    // --- 5. 3D 先用降采样体快速显示；如果原始体不太大，再后台缓存原始体素供 2D 切面使用。
                    // --- #加速加载-5：首屏完成后才启动完整 RAW 的后台常驻；不 await，保证此处立即恢复交互。
                    StartOriginalSliceCache(_volume);
                    previousVolume.Dispose();
                    totalStopwatch.Stop();
                    LogHelper.Information(string.Format("RAW 首次可交互显示完成，总耗时={0}ms", totalStopwatch.ElapsedMilliseconds), "CxECT3DViewer.Performance");
                }
                catch (Exception ex)
                {
                    LogHelper.Exception(ex, "加载 RAW 失败", "CxECT3DViewer.Performance");
                    MessageBox.Show(this, ex.Message, "加载 RAW 失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    UpdateVolumeInfo();
                }
                finally
                {
                    SetLoadingState(false, null);
                }
            }
        }

        private void OnSaveScreenshotClicked(object sender, EventArgs e)
        {
            using (var dialog = new SaveFileDialog
            {
                Title = "保存四视图截图",
                Filter = "PNG image (*.png)|*.png",
                FileName = "CxECT3DViewer.png",
                OverwritePrompt = true,
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                using (var bitmap = _fourView.CaptureScreenshot())
                {
                    bitmap.Save(dialog.FileName, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
        }

        private void ApplyVolumeToUi()
        {
            ConfigureSlider(_sliceXSlider, _volume.OriginalWidth - 1, _state.SliceX);
            ConfigureSlider(_sliceYSlider, _volume.OriginalHeight - 1, _state.SliceY);
            ConfigureSlider(_sliceZSlider, _volume.OriginalDepth - 1, _state.SliceZ);
            UpdateSliceLabels();
            UpdateSliceSliderValues();
            UpdateVolumeInfo();
            UpdateHistograms();
        }

        private void UpdateHistograms()
        {
            _thresholdEditor.SetHistogram(_histogram.Scalar);
            _colorEditor.SetHistogram(_histogram.Scalar);
            _scalarOpacityEditor.SetHistogram(_histogram.Scalar);
            _gradientOpacityEditor.SetHistogram(_histogram.Gradient);
        }

        private static void ConfigureSlider(TrackBar slider, int maximum, int value)
        {
            slider.Maximum = Math.Max(1, maximum);
            slider.TickFrequency = Math.Max(1, slider.Maximum / 8);
            slider.SmallChange = 1;
            slider.LargeChange = Math.Max(1, slider.Maximum / 16);
            slider.Value = FrameworkCompat.Clamp(value, slider.Minimum, slider.Maximum);
        }

        private void UpdateSliceLabels()
        {
            _sliceXValue.Text = string.Format("{0} / {1}", _state.SliceX, _volume.OriginalWidth - 1);
            _sliceYValue.Text = string.Format("{0} / {1}", _state.SliceY, _volume.OriginalHeight - 1);
            _sliceZValue.Text = string.Format("{0} / {1}", _state.SliceZ, _volume.OriginalDepth - 1);
        }

        private void UpdateSliceSliderValues()
        {
            _sliceXSlider.Value = FrameworkCompat.Clamp(_state.SliceX, _sliceXSlider.Minimum, _sliceXSlider.Maximum);
            _sliceYSlider.Value = FrameworkCompat.Clamp(_state.SliceY, _sliceYSlider.Minimum, _sliceYSlider.Maximum);
            _sliceZSlider.Value = FrameworkCompat.Clamp(_state.SliceZ, _sliceZSlider.Minimum, _sliceZSlider.Maximum);
        }

        private void UpdateVolumeInfo()
        {
            _volumeInfoLabel.Text =
                _volume.Name + "\r\n" +
                string.Format("原始: {0}x{1}x{2}\r\n", _volume.OriginalWidth, _volume.OriginalHeight, _volume.OriginalDepth) +
                string.Format("显示: {0}x{1}x{2}\r\n", _volume.Width, _volume.Height, _volume.Depth) +
                string.Format("间距: {0:g}x{1:g}x{2:g}\r\n", _volume.SpacingX, _volume.SpacingY, _volume.SpacingZ) +
                string.Format("灰度: {0:g}..{1:g}\r\n", _volume.ScalarMin, _volume.ScalarMax) +
                _fourView.ActiveVoxelInfo;
        }

        private void UpdateSliceWindow()
        {
            var half = _sliceWindowWidth * 0.5;
            var min = (byte)FrameworkCompat.Clamp((int)Math.Round(_sliceWindowCenter - half), 0, 255);
            var max = (byte)FrameworkCompat.Clamp((int)Math.Round(_sliceWindowCenter + half), 0, 255);
            _fourView.SetSliceWindow(min, max);
        }

        private void OnTransferChanged(object sender, EventArgs e)
        {
            PushTransferFunctionsToGpu();
        }

        private void PushTransferFunctionsToGpu()
        {
            // RenderingTransferFunctionSet 是引用类型，曲线点已在编辑器里原地修改；这里触发渲染器重新烘焙并上传纹理。
            _renderer.UpdateRenderSettings(delegate { });
        }

        private async void StartOriginalSliceCache(VolumeData volume)
        {
            if (volume == null || !volume.HasRawSource)
            {
                return;
            }

            if (volume.HasOriginalRawData)
            {
                LogHelper.Information("原始 RAW 已常驻非托管内存，跳过重复的原始切片缓存读取。", "CxECT3DViewer.Performance");
                return;
            }

            try
            {
                // --- #加速加载-6：完整 RAW 只顺序读取一次到非托管内存，后续高分辨率 2D 切片直接从内存取值。
                var cacheStopwatch = Stopwatch.StartNew();
                var timing = new RawVolumeLoadTiming();
                LogHelper.Information(string.Format("开始顺序载入非托管原始 RAW：{0}x{1}x{2}", volume.OriginalWidth, volume.OriginalHeight, volume.OriginalDepth), "CxECT3DViewer.Performance");
                var rawBuffer = await Task.Run(delegate { return RawVolumeLoader.LoadRawBuffer(volume, timing); });
                cacheStopwatch.Stop();
                if (!ReferenceEquals(_volume, volume) || IsDisposed)
                {
                    rawBuffer.Dispose();
                    return;
                }

                volume.SetRawBuffer(rawBuffer);
                LogHelper.Information(string.Format("非托管 RAW 完成：总耗时={0}ms，分配={1}ms，文件读取={2}ms，内存复制={3}ms", cacheStopwatch.ElapsedMilliseconds, timing.UnmanagedAllocationMilliseconds, timing.UnmanagedFileReadMilliseconds, timing.UnmanagedCopyMilliseconds), "CxECT3DViewer.Performance");
                var highResolutionSliceStopwatch = Stopwatch.StartNew();
                // --- #加速加载-7：原始缓存就绪时只重建三张 2D 切片，不重复上传已经可交互的 3D 预览纹理。
                _fourView.LoadHighResolutionSlices(_volume);
                highResolutionSliceStopwatch.Stop();
                LogHelper.Information(string.Format("高分辨率 2D 切片切换完成，耗时={0}ms", highResolutionSliceStopwatch.ElapsedMilliseconds), "CxECT3DViewer.Performance");
                UpdateVolumeInfo();
            }
            catch (Exception ex)
            {
                LogHelper.Exception(ex, "原始切片缓存失败", "CxECT3DViewer.Performance");
                // 原始缓存只是提升 2D 切片精度；失败时保留降采样切片，避免影响主查看流程。
            }
        }

        private void SetLoadingState(bool isLoading, string message)
        {
            _loadRawButton.Enabled = !isLoading;
            UseWaitCursor = isLoading;

            if (!string.IsNullOrWhiteSpace(message))
            {
                _volumeInfoLabel.Text = message;
                _volumeInfoLabel.Refresh();
            }
        }

    }
}
