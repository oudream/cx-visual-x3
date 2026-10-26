using System;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using CxVisual.Foundation.Imaging;
using CxVisual.Foundation.Geometry;
using CxVisual.Mechanism.Presentation.Wpf.Imaging;
using CxVisual.Mechanism.Visual;
using Microsoft.Win32;

namespace CxVisual.TestCenter.UI
{
    /// <summary>
    /// 通用二维工业视觉可操作工作区：借用正式MPW控件、MVI会话和FIM算法，
    /// 不保存第二套图像对象、检测引擎、工程定义或算法实现。
    /// </summary>
    public sealed partial class TCTManualImageViewerFrame : UserControl, ITCTWorkspaceFrame
    {
        private readonly MPWImageViewerControl _viewer = new MPWImageViewerControl();
        private readonly TextBlock _status = new TextBlock { Text = "打开图片或通过开发者API传入FIMImageBuffer",
            Foreground = Brushes.SteelBlue, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(9, 5, 9, 5) };
        private readonly TextBox _info = new TextBox { IsReadOnly = true,
            FontFamily = new FontFamily("Consolas"), FontSize = 13,
            AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = Brushes.White, Margin = new Thickness(5) };
        private readonly TextBox _rawWidth = new TextBox { Width = 65, Text = "1024", Margin = new Thickness(2) };
        private readonly TextBox _rawHeight = new TextBox { Width = 65, Text = "1024", Margin = new Thickness(2) };
        private readonly ComboBox _rawFormat = new ComboBox { Width = 122, Margin = new Thickness(2) };
        private readonly CheckBox _bigEndian = new CheckBox { Content = "大端字节序", Margin = new Thickness(8, 4, 8, 4) };
        private readonly ComboBox _filterRadius = new ComboBox { Width = 66, Margin = new Thickness(3) };
        private readonly TextBox _sigma = new TextBox { Width = 58, Text = "1.0", Margin = new Thickness(3) };
        private readonly TextBox _amount = new TextBox { Width = 58, Text = "0.8", Margin = new Thickness(3) };
        private readonly TextBox _low = new TextBox { Width = 74, Text = "0", Margin = new Thickness(2) };
        private readonly TextBox _high = new TextBox { Width = 82, Text = "65535", Margin = new Thickness(2) };
        private readonly TextBox _blendRatio = new TextBox { Width = 60, Text = "0.5", Margin = new Thickness(3) };
        private readonly TextBox _spacingX = new TextBox { Width = 58, Text = "0.1", Margin = new Thickness(2) };
        private readonly TextBox _spacingY = new TextBox { Width = 58, Text = "0.1", Margin = new Thickness(2) };
        private readonly TextBox _displayGamma = new TextBox { Width = 50, Text = "1", Margin = new Thickness(2) };
        private readonly TextBox _displayBrightness = new TextBox { Width = 58, Text = "0", Margin = new Thickness(2) };
        private readonly TextBox _displayContrast = new TextBox { Width = 58, Text = "1", Margin = new Thickness(2) };
        private readonly CheckBox _displayInverted = new CheckBox { Content = "反色", Margin = new Thickness(3),
            VerticalAlignment = VerticalAlignment.Center };
        private readonly CheckBox _autoContrast = new CheckBox { Content = "自动对比度", Margin = new Thickness(3),
            VerticalAlignment = VerticalAlignment.Center };
        private readonly ComboBox _imageTool = new ComboBox {
            MinWidth = 144, Margin = new Thickness(3) };
        private readonly TextBox _annotationText = new TextBox {
            Width = 95, Text = "标注", Margin = new Thickness(2) };
        private readonly MPWImagePlotControl _histogramPlot = new MPWImagePlotControl();
        private readonly MPWImagePlotControl _profilePlot = new MPWImagePlotControl();
        private readonly TabControl _inspectTabs = new TabControl();
        private FIMImageBuffer _plottedSource;
        private CancellationTokenSource _analysisCancellation;
        private long _analysisEpoch;
        public MPWImagePlotControl HistogramPlot { get { return _histogramPlot; } }
        public MPWImagePlotControl ProfilePlot { get { return _profilePlot; } }
        private readonly ListBox _overlayList = new ListBox {
            Height = 142, Margin = new Thickness(5),
            FontSize = 12, Background = Brushes.White };
        private readonly CheckBox _withRoi = new CheckBox { Content = "ROI分析", IsChecked = true,
            Margin = new Thickness(6), VerticalAlignment = VerticalAlignment.Center };

        public string WorkspaceId { get { return "manual.imaging"; } }
        public IEnumerable<TCTWorkspaceAction> Commands
        {
            get
            {
                return new[] {
                    new TCTWorkspaceAction("imaging.fit", "适应窗口", () => _viewer.FitToWindow()),
                    new TCTWorkspaceAction("imaging.actual", "实际像素", () => _viewer.ActualSize()),
                    new TCTWorkspaceAction("imaging.statistics", "计算统计", Calculate)
                };
            }
        }
        public event Action StateChanged;
        public MPWImageViewerControl Viewer { get { return _viewer; } }

        public TCTManualImageViewerFrame()
        {
            BuildWorkbenchLayout();
            _imageTool.SelectionChanged += (sender,args) =>
            {
                ComboBoxItem selected = _imageTool.SelectedItem as ComboBoxItem;
                if (selected == null) return;
                _viewer.ToolMode = (MVIImageToolMode)selected.Tag;
                _viewer.AnnotationText = _annotationText.Text;
                Show("交互工具：" + selected.Content + "；鼠标左键绘制/拖动，滚轮缩放");
            };
            _annotationText.TextChanged += (sender,args) =>
                _viewer.AnnotationText = _annotationText.Text;
            _viewer.Overlays.Changed += (sender,args) => RefreshOverlayList();
            _viewer.Session.Changed += (sender,args) =>
            {
                CancelAnalysis();
                FIMImageBuffer current = _viewer.ActiveImage;
                if (!ReferenceEquals(current, _plottedSource))
                {
                    _plottedSource = current;
                    _histogramPlot.Clear();
                    _profilePlot.Clear();
                }
            };
            _viewer.SelectionChanged += (s,e) =>
            {
                CancelAnalysis();
                if (_viewer.SelectedRegion.HasValue)
                    _status.Text = "当前ROI：" + BoundsText(_viewer.SelectedRegion.Value);
                if (_viewer.ToolMode == MVIImageToolMode.LineProfile &&
                    _viewer.Overlays.Items.Any(item => item.Kind == MVIImageOverlayKind.LineProfileMeasurement))
                    ShowLineProfile();
                if (StateChanged != null) StateChanged();
            };
        }

        private static string ToolTitle(MVIImageToolMode mode)
        {
            switch (mode)
            {
                case MVIImageToolMode.Navigate: return "导航/平移";
                case MVIImageToolMode.RectangleRoi: return "矩形ROI";
                case MVIImageToolMode.CircleRoi: return "圆形ROI";
                case MVIImageToolMode.EllipseRoi: return "椭圆ROI";
                case MVIImageToolMode.Distance: return "两点距离";
                case MVIImageToolMode.Point: return "点标注";
                case MVIImageToolMode.Text: return "文字标注";
                case MVIImageToolMode.Arrow: return "箭头标注";
                case MVIImageToolMode.DefectBox: return "缺陷框";
                case MVIImageToolMode.MoveOverlay: return "移动叠加项";
                case MVIImageToolMode.DeleteOverlay: return "点击删除";
                case MVIImageToolMode.PolygonRoi: return "多边形ROI（双击结束）";
                case MVIImageToolMode.Angle: return "三点角度测量";
                case MVIImageToolMode.HorizontalDistance: return "水平距离";
                case MVIImageToolMode.VerticalDistance: return "垂直距离";
                case MVIImageToolMode.Diameter: return "圆形直径";
                case MVIImageToolMode.Area: return "矩形面积";
                case MVIImageToolMode.PolygonArea: return "多边形面积（双击结束）";
                case MVIImageToolMode.LineProfile: return "绘制灰度剖面线";
                default: return mode.ToString();
            }
        }

        private Guid? SelectedOverlayId()
        {
            ListBoxItem selected = _overlayList.SelectedItem as ListBoxItem;
            return selected == null ? (Guid?)null : (Guid)selected.Tag;
        }

        private void RefreshOverlayList()
        {
            Guid? keep = SelectedOverlayId();
            _overlayList.Items.Clear();
            foreach (MVIImageOverlayItem item in _viewer.Overlays.Items)
            {
                string visibility = item.IsVisible &&
                    _viewer.Overlays.IsLayerVisible(item.Layer) ? "●" : "○";
                ListBoxItem row = new ListBoxItem { Tag = item.Id,
                    Content = visibility + " " + item.Layer + " / " + item.Kind +
                        (string.IsNullOrEmpty(_viewer.DescribeMeasurement(item)) ? "" :
                            " · " + _viewer.DescribeMeasurement(item)) +
                        (string.IsNullOrEmpty(item.Text) ? "" : " · " + item.Text),
                    ToolTip = "ID: " + item.Id };
                _overlayList.Items.Add(row);
                if (keep.HasValue && keep.Value == item.Id) _overlayList.SelectedItem = row;
            }
            if (StateChanged != null) StateChanged();
        }

        private MVIImageOverlayItem RequireSelectedOverlay()
        {
            Guid? id = SelectedOverlayId();
            if (!id.HasValue) throw new InvalidOperationException("请在右侧列表选中叠加项。");
            MVIImageOverlayItem item = _viewer.Overlays.Find(id.Value);
            if (item == null) throw new InvalidOperationException("叠加项不存在或已删除。");
            return item;
        }

        private void DeleteSelectedOverlay()
        {
            MVIImageOverlayItem item = RequireSelectedOverlay();
            _viewer.Overlays.Remove(item.Id);
            Show("已删除叠加项：" + item.Kind);
        }

        private void MoveSelectedOverlay(double deltaX, double deltaY)
        {
            MVIImageOverlayItem item = RequireSelectedOverlay();
            _viewer.Overlays.Replace(item.Translate(deltaX, deltaY));
            Show("叠加项已在原图像素坐标系平移：" + deltaX + "," + deltaY);
        }

        private void ToggleSelectedLayer()
        {
            MVIImageOverlayItem item = RequireSelectedOverlay();
            bool visible = !_viewer.Overlays.IsLayerVisible(item.Layer);
            _viewer.SetOverlayLayerVisible(item.Layer, visible);
            Show(item.Layer + " 图层：" + (visible ? "显示" : "隐藏"));
        }

        private void ShowAllLayers()
        {
            foreach (MVIImageOverlayLayer layer in Enum.GetValues(typeof(MVIImageOverlayLayer)))
                _viewer.SetOverlayLayerVisible(layer, true);
            Show("全部ROI/测量/标注/缺陷图层已显示");
        }

        private static string BoundsText(FIMPixelRegion r)
        { return "(" + r.Left + "," + r.Top + ") - (" + r.Right + "," + r.Bottom + ")"; }

        private Button AddButton(Panel host, string label, string id, Action action)
        {
            Button button = new Button { Content = label, Margin = new Thickness(3),
                Padding = new Thickness(9, 5, 9, 5), MinHeight = 29,
                Background = Brushes.White, BorderBrush = Brushes.LightSteelBlue };
            AutomationProperties.SetAutomationId(button, "TCT.Imaging." + id);
            button.Click += (s,e) =>
            {
                try { action(); }
                catch (Exception error)
                {
                    Show("操作失败 [" + id + "]：" + error.Message);
                    MessageBox.Show(error.Message, "图像操作失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };
            host.Children.Add(button);
            return button;
        }

        public void SetOriginal(FIMImageBuffer image)
        {
            _viewer.ShowOriginal(image);
            Show("图像已从内存加载：" + image.Width + "×" + image.Height + " · " + image.PixelFormat);
        }

        private async void OpenWithDialog()
        {
            OpenFileDialog dialog = new OpenFileDialog {
                Filter = "常用图像|*.bmp;*.png;*.jpg;*.jpeg;*.tif;*.tiff|全部文件|*.*",
                Title = "打开图像（保留原始位深）" };
            if (dialog.ShowDialog() != true) return;
            await OpenFileAsync(dialog.FileName);
        }

        public Task<bool> OpenFileAsync(string path)
        {
            return LoadAsync(path, () => MPWImageViewerControl.ReadImageFile(path));
        }

        public Task<bool> OpenRawAsync(string path, int width, int height,
            FIMPixelFormat format, bool bigEndian)
        {
            return LoadAsync(path, () => FIMRawImageReader.LoadFile(path, width, height, format, 0, bigEndian));
        }

        private void OpenRawWithDialog()
        {
            int width, height;
            if (!int.TryParse(_rawWidth.Text, out width) || width <= 0 ||
                !int.TryParse(_rawHeight.Text, out height) || height <= 0)
                throw new ArgumentException("RAW宽高必须是正整数。");
            FIMPixelFormat format = (FIMPixelFormat)_rawFormat.SelectedItem;
            OpenFileDialog dialog = new OpenFileDialog {
                Filter = "RAW图像|*.raw;*.bin;*.dat|所有文件|*.*",
                Title = "打开原始图像（需已指定宽高和格式）" };
            if (dialog.ShowDialog() == true)
                OpenRawAsync(dialog.FileName, width, height, format, _bigEndian.IsChecked == true);
        }

        private async Task<bool> LoadAsync(string path, Func<FIMImageBuffer> read)
        {
            long version = _viewer.Session.BeginOriginalLoad();
            Stopwatch elapsed = Stopwatch.StartNew();
            Show("正在读取：" + System.IO.Path.GetFileName(path));
            try
            {
                FIMImageBuffer image = await Task.Run(read);
                if (!_viewer.Session.TryCompleteOriginalLoad(version, image))
                {
                    Show("新任务已取代该次加载，过期图像不会覆盖当前显示");
                    return false;
                }
                _viewer.ClearSelection();
                _viewer.SetDisplayWindow(null, null);
                _viewer.FitToWindow();
                elapsed.Stop();
                Show("已加载：" + image.Width + "×" + image.Height + " " + image.PixelFormat +
                    "；读取耗时 " + elapsed.ElapsedMilliseconds + " ms");
                return true;
            }
            catch (Exception error)
            {
                Show("载入失败：" + error.Message);
                return false;
            }
        }

        private void CropSelection()
        {
            FIMImageBuffer image = _viewer.ActiveImage;
            if (image == null) throw new InvalidOperationException("未加载图像。");
            if (!_viewer.SelectedRegion.HasValue) throw new InvalidOperationException("请先框选ROI。");
            FIMImageBuffer cropped = FIMImageOperations.Crop(image, _viewer.SelectedRegion.Value);
            _viewer.ClearSelection();
            _viewer.ShowResult(cropped);
            _viewer.FitToWindow();
            Show("裁剪结果：" + cropped.Width + "×" + cropped.Height + "；原图仍保留");
        }

        private int FilterRadius()
        {
            return _filterRadius.SelectedItem == null ? 1 : (int)_filterRadius.SelectedItem;
        }

        private double FloatParameter(TextBox field, string name)
        {
            double value;
            if (!double.TryParse(field.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                throw new ArgumentException(name + "必须是有限数值。");
            return value;
        }

        private void Filter(FIMNeighborhoodFilter kind)
        {
            ProcessImage(image => FIMImageFilters.Apply(image, kind, FilterRadius()), kind.ToString());
        }

        private void Gaussian()
        {
            double sigma = FloatParameter(_sigma, "Sigma");
            ProcessImage(image => FIMImageFilters.GaussianBlur(image, sigma, FilterRadius()), "高斯降噪");
        }

        private void Sharpen()
        {
            double sigma = FloatParameter(_sigma, "Sigma");
            double amount = FloatParameter(_amount, "锐化量");
            ProcessImage(image => FIMImageFilters.UnsharpMask(image, sigma, FilterRadius(), amount), "锐化");
        }

        private void ProcessImage(Func<FIMImageBuffer, FIMImageBuffer> operation, string title)
        {
            if (_viewer.ActiveImage == null) throw new InvalidOperationException("未加载图像。");
            Stopwatch elapsed = Stopwatch.StartNew();
            FIMImageBuffer result = operation(_viewer.ActiveImage);
            elapsed.Stop();
            _viewer.ClearSelection();
            _viewer.ShowResult(result);
            _viewer.FitToWindow();
            Show(title + "处理结果已显示，原图仍保持不变；CPU耗时 " +
                elapsed.ElapsedMilliseconds + " ms");
        }

        private void Blend()
        {
            double ratio = FloatParameter(_blendRatio, "叠合比例");
            _viewer.BlendOriginalAndResult(ratio);
            Show("原图/结果图透明叠合：" + ratio.ToString("0.##", CultureInfo.InvariantCulture) +
                "（源图不变）");
        }

        private void Swipe()
        {
            double ratio = FloatParameter(_blendRatio, "滑动比较比例");
            _viewer.SwipeOriginalAndResult(ratio);
            Show("原图/结果滑动比较：左侧原图占比 " +
                ratio.ToString("0.##",CultureInfo.InvariantCulture) +
                "；共享同一坐标视口与缩放，原始图不变");
        }

        private void Difference()
        {
            FIMImageBuffer original = _viewer.Session.Original;
            FIMImageBuffer result = _viewer.Session.Result;
            if (original == null || result == null)
                throw new InvalidOperationException("差分需要同时加载原图和结果图。");
            FIMImageBuffer difference = FIMImageOperations.DifferenceGray(original, result);
            _viewer.ShowResult(difference);
            Show("已生成原图与结果图的绝对差分（保留原图）。");
        }

        private void Threshold()
        {
            double low, high;
            if (!double.TryParse(_low.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out low) ||
                !double.TryParse(_high.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out high))
                throw new ArgumentException("阈值范围必须是有效数字。");
            ProcessImage(image => FIMImageOperations.Threshold(image, low, high), "阈值");
        }

        private void Normalize()
        {
            if (_viewer.ActiveImage == null) throw new InvalidOperationException("未加载图像。");
            FIMImageBuffer result = FIMImageOperations.NormalizeToGray8(_viewer.ActiveImage);
            _viewer.ShowResult(result);
            Show("归一化结果已生成（原图保留）");
        }

        private void Window()
        {
            double low, high;
            if (!double.TryParse(_low.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out low) ||
                !double.TryParse(_high.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out high))
                throw new ArgumentException("窗上下限请输入有限数字，如 0 与 65535。");
            _viewer.SetDisplayWindow(low, high);
            Show("显示窗范围：" + low + " - " + high + "（原图不变）");
        }

        private void ApplyPixelSpacing()
        {
            if (_viewer.ActiveImage == null)
                throw new InvalidOperationException("请先加载图像，再应用其真实标定间距。");
            double sx = FloatParameter(_spacingX, "X像素间距");
            double sy = FloatParameter(_spacingY, "Y像素间距");
            _viewer.PixelSpacing = new FIMPixelSpacing(sx,sy);
            _viewer.ShowPhysicalRulers = true;
            Show("标尺切换为毫米：X=" + sx + " mm/px，Y=" + sy +
                " mm/px（必须以真实标定值为准）");
        }

        private void ResetPixelSpacing()
        {
            _viewer.PixelSpacing = null;
            _viewer.ShowPhysicalRulers = false;
            Show("恢复像素标尺；未标定不显示毫米尺寸");
        }

        private void ApplyDisplay()
        {
            double gamma = FloatParameter(_displayGamma, "Gamma");
            double brightness = FloatParameter(_displayBrightness, "亮度");
            double contrast = FloatParameter(_displayContrast, "对比度");
            MVIImageDisplayState display = _viewer.DisplayState;
            // 所有参数先验证，避免半途异常导致部分值已经生效。
            if (double.IsNaN(gamma) || double.IsInfinity(gamma) || gamma <= 0 || gamma > 20 ||
                double.IsNaN(brightness) || double.IsInfinity(brightness) ||
                brightness < -1 || brightness > 1 ||
                double.IsNaN(contrast) || double.IsInfinity(contrast) ||
                contrast < 0 || contrast > 16)
                throw new ArgumentOutOfRangeException("显示参数超出范围。");
            display.SetGamma(gamma);
            display.SetBrightness(brightness);
            display.SetContrast(contrast);
            display.SetInverted(_displayInverted.IsChecked == true);
            display.SetAutoContrast(_autoContrast.IsChecked == true);
            SyncGraySliders();
            Show("显示状态已应用：源图不改变，原始灰度统计保持不变");
        }

        private void ResetDisplay()
        {
            _viewer.DisplayState.Reset();
            _displayGamma.Text = "1"; _displayBrightness.Text = "0"; _displayContrast.Text = "1";
            _displayInverted.IsChecked = false; _autoContrast.IsChecked = false;
            SyncGraySliders();
            Show("窗宽窗位/反色/Gamma/亮度/对比度全部复位");
        }

        private void CancelAnalysis()
        {
            Interlocked.Increment(ref _analysisEpoch);
            if (_analysisCancellation != null)
                _analysisCancellation.Cancel();
        }

        /// <summary>
        /// 大图计算在后台执行；只有当前图像、ROI和请求仍匹配才允许更新图表。
        /// 旧ImageViewerControl的取消及latest-request语义直接在唯一工作台落地。
        /// </summary>
        private async void Calculate()
        {
            FIMImageBuffer image = _viewer.ActiveImage;
            if (image == null)
            {
                Show("统计失败：请先加载图像。");
                return;
            }
            FIMPixelRegion? region = _withRoi.IsChecked == true ? _viewer.SelectedRegion : null;
            CancelAnalysis();
            CancellationTokenSource cancellation = new CancellationTokenSource();
            _analysisCancellation = cancellation;
            long epoch = Interlocked.Read(ref _analysisEpoch);
            Stopwatch timer = Stopwatch.StartNew();
            Show("正在分析原始灰度：" + image.Width + "×" + image.Height +
                "；界面仍可操作，切图/变更ROI会取消旧请求");
            try
            {
                FIMImageAnalysisResult result = await Task.Run(() =>
                    FIMImageAnalyzer.Analyze(image, 256, region, cancellation.Token),
                    cancellation.Token);
                if (cancellation.IsCancellationRequested ||
                    Interlocked.Read(ref _analysisEpoch) != epoch ||
                    !ReferenceEquals(_viewer.ActiveImage,image))
                    return;
                timer.Stop();
                _histogramPlot.SetHistogram(result.Histogram);
                _plottedSource = image;
                _inspectTabs.SelectedIndex = 1;
                FIMImageStatistics stats = result.Statistics;
                _info.Text = "图像：" + image.Width + "×" + image.Height + "\n" +
                    "格式：" + image.PixelFormat + "\n" +
                    "ROI：" + (region.HasValue ? BoundsText(region.Value) : "全图") + "\n" +
                    "像素数：" + stats.PixelCount + "\n" +
                    "Min / Max：" + stats.Min.ToString("0.###") + " / " + stats.Max.ToString("0.###") + "\n" +
                    "Mean：" + stats.Mean.ToString("0.###") + "\n" +
                    "StdDev：" + stats.StandardDeviation.ToString("0.###") + "\n" +
                    "256档直方图（原始灰度域） · 耗时 " +
                    timer.Elapsed.TotalMilliseconds.ToString("0.#") + "ms";
                Show("灰度统计完成：" + stats.PixelCount + "像素 / " +
                    timer.Elapsed.TotalMilliseconds.ToString("0.#") +
                    "ms；原图不变、显示窗不参与计算");
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                if (Interlocked.Read(ref _analysisEpoch) == epoch)
                    Show("统计失败：" + error.Message);
            }
            finally
            {
                if (ReferenceEquals(_analysisCancellation,cancellation))
                    _analysisCancellation = null;
                cancellation.Dispose();
            }
        }

        private void ShowLineProfile()
        {
            FIMImageBuffer image = _viewer.ActiveImage;
            if (image == null) throw new InvalidOperationException("未加载图像。");
            int x0 = 0, y0 = image.Height / 2, x1 = image.Width - 1, y1 = y0;
            Guid? id = SelectedOverlayId();
            MVIImageOverlayItem item = id.HasValue ? _viewer.Overlays.Find(id.Value) : null;
            if (item == null || item.Points.Count < 2)
                item = _viewer.Overlays.Items.LastOrDefault(x =>
                    x.Kind == MVIImageOverlayKind.LineProfileMeasurement && x.Points.Count >= 2);
            if (item != null && item.Points.Count >= 2)
            {
                x0 = (int)Math.Round(item.Points[0].X);
                y0 = (int)Math.Round(item.Points[0].Y);
                x1 = (int)Math.Round(item.Points[1].X);
                y1 = (int)Math.Round(item.Points[1].Y);
            }
            else if (_viewer.SelectedRegion.HasValue)
            {
                FIMPixelRegion roi = _viewer.SelectedRegion.Value;
                x0 = roi.Left; y0 = roi.Top; x1 = roi.Right; y1 = roi.Bottom;
            }
            x0 = Math.Min(image.Width - 1, Math.Max(0, x0));
            x1 = Math.Min(image.Width - 1, Math.Max(0, x1));
            y0 = Math.Min(image.Height - 1, Math.Max(0, y0));
            y1 = Math.Min(image.Height - 1, Math.Max(0, y1));
            _profilePlot.SetLineProfile(FIMImageAnalyzer.GetLineProfile(image,x0,y0,x1,y1));
            _plottedSource = image;
            _inspectTabs.SelectedIndex = 2;
            Show("原始灰度剖面：" + "(" + x0 + "," + y0 + ") → (" + x1 + "," + y1 +
                ")，不受显示Gamma和窗宽窗位影响");
        }

        /// <summary>导出当前真实MVI对象；保留稳定ID与原图几何，不伪造物理标定。</summary>
        public void ExportMeasurementsCsv(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("导出路径不能为空。","path");
            if (_viewer.Session.Original == null) throw new InvalidOperationException("尚未加载原图。");
            using (System.IO.StreamWriter output = new System.IO.StreamWriter(
                path,false,new System.Text.UTF8Encoding(true)))
            {
                output.WriteLine("Id,Layer,Kind,Visible,X,Y,Width,Height,PointCount,Value,Text,Vertices");
                foreach (MVIImageOverlayItem item in _viewer.Overlays.Items)
                {
                    string[] fields = {
                        item.Id.ToString("D"),item.Layer.ToString(),item.Kind.ToString(),
                        item.IsVisible.ToString(),item.Bounds.X.ToString("R",CultureInfo.InvariantCulture),
                        item.Bounds.Y.ToString("R",CultureInfo.InvariantCulture),
                        item.Bounds.Width.ToString("R",CultureInfo.InvariantCulture),
                        item.Bounds.Height.ToString("R",CultureInfo.InvariantCulture),
                        item.Points.Count.ToString(CultureInfo.InvariantCulture),
                        _viewer.DescribeMeasurement(item), item.Text,
                        string.Join(";",item.Points.Select(point =>
                            point.X.ToString("R",CultureInfo.InvariantCulture) + ":" +
                            point.Y.ToString("R",CultureInfo.InvariantCulture)))
                    };
                    output.WriteLine(string.Join(",",fields.Select(CsvField)));
                }
            }
        }
        private static string CsvField(string s)
        {
            string text = s ?? string.Empty;
            // 防止通过Excel打开缺陷文字触发CSV公式注入。
            if (text.Length > 0 && "=+-@\t\r".IndexOf(text[0]) >= 0) text = "'" + text;
            return "\"" + text.Replace("\"","\"\"") + "\"";
        }
        private void ExportMeasurements()
        {
            SaveFileDialog dialog = new SaveFileDialog {
                Filter = "测量与标注CSV|*.csv", DefaultExt = ".csv",
                Title = "导出当前图像ROI/测量/标注" };
            if (dialog.ShowDialog() != true) return;
            ExportMeasurementsCsv(dialog.FileName);
            Show("已导出原图坐标、测量数值、物理标定与稳定ID：" + dialog.FileName);
        }

        private void Save()
        {
            SaveFileDialog dialog = new SaveFileDialog {
                Filter = "PNG图像|*.png", DefaultExt = ".png" };
            if (dialog.ShowDialog() != true) return;
            _viewer.SavePng(dialog.FileName);
            Show("PNG已保存：" + dialog.FileName);
        }

        private void Show(string text)
        {
            _status.Text = text;
            if (StateChanged != null) StateChanged();
        }

        public void OnWorkspaceClosed()
        {
            CancelAnalysis();
            // 所有数据归当前MPW/MVI会话管理；无设备句柄或新的工程资源。
            _viewer.Clear();
        }
    }
}
