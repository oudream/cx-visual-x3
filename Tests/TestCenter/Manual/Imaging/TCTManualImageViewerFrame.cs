using System;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
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
    public sealed class TCTManualImageViewerFrame : UserControl, ITCTWorkspaceFrame
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
            Grid root = new Grid { Background = Brushes.White };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            WrapPanel toolbar = new WrapPanel { Background = new SolidColorBrush(Color.FromRgb(241,246,251)),
                Margin = new Thickness(3), Orientation = Orientation.Horizontal };
            AddButton(toolbar, "打开文件", "Open", () => { OpenWithDialog(); });
            AddButton(toolbar, "适应窗口", "Fit", () => _viewer.FitToWindow());
            AddButton(toolbar, "1:1", "Actual", () => _viewer.ActualSize());
            AddButton(toolbar, "观察旋转90°", "ViewRotate", () => _viewer.RotateViewClockwise());
            AddButton(toolbar, "观察水平镜像", "ViewMirrorX", () => _viewer.FlipViewHorizontal());
            AddButton(toolbar, "观察垂直镜像", "ViewMirrorY", () => _viewer.FlipViewVertical());
            AddButton(toolbar, "复位观察", "ViewReset", () => _viewer.ResetViewTransform());
            AddButton(toolbar, "放大", "ZoomIn", () => _viewer.Zoom(1.25));
            AddButton(toolbar, "缩小", "ZoomOut", () => _viewer.Zoom(0.8));
            toolbar.Children.Add(new TextBlock { Text = "交互工具", VerticalAlignment = VerticalAlignment.Center });
            foreach (MVIImageToolMode mode in Enum.GetValues(typeof(MVIImageToolMode)))
                _imageTool.Items.Add(new ComboBoxItem { Content = ToolTitle(mode), Tag = mode });
            _imageTool.SelectedIndex = 0;
            toolbar.Children.Add(_imageTool);
            toolbar.Children.Add(_annotationText);
            AddButton(toolbar, "裁剪为结果", "Crop", CropSelection);
            AddButton(toolbar, "归一化结果", "Normalize", Normalize);
            toolbar.Children.Add(new TextBlock { Text = "滤波半径", VerticalAlignment = VerticalAlignment.Center });
            _filterRadius.Items.Add(1); _filterRadius.Items.Add(2); _filterRadius.Items.Add(3);
            _filterRadius.SelectedItem = 1;
            toolbar.Children.Add(_filterRadius);
            AddButton(toolbar, "均值", "Mean", () => Filter(FIMNeighborhoodFilter.Mean));
            AddButton(toolbar, "中值", "Median", () => Filter(FIMNeighborhoodFilter.Median));
            AddButton(toolbar, "腐蚀", "Erode", () => Filter(FIMNeighborhoodFilter.Minimum));
            AddButton(toolbar, "膨胀", "Dilate", () => Filter(FIMNeighborhoodFilter.Maximum));
            toolbar.Children.Add(new TextBlock { Text = "Sigma", VerticalAlignment = VerticalAlignment.Center });
            toolbar.Children.Add(_sigma);
            AddButton(toolbar, "高斯降噪", "Gaussian", Gaussian);
            toolbar.Children.Add(new TextBlock { Text = "锐化量", VerticalAlignment = VerticalAlignment.Center });
            toolbar.Children.Add(_amount);
            AddButton(toolbar, "锐化", "Sharpen", Sharpen);
            AddButton(toolbar, "旋转90°", "Rotate", () => ProcessImage(
                image => FIMImageOperations.Rotate90Clockwise(image), "旋转90°"));
            AddButton(toolbar, "水平镜像", "FlipHorizontal", () => ProcessImage(
                image => FIMImageOperations.FlipHorizontal(image), "水平镜像"));
            AddButton(toolbar, "垂直镜像", "FlipVertical", () => ProcessImage(
                image => FIMImageOperations.FlipVertical(image), "垂直镜像"));
            AddButton(toolbar, "差分图", "Difference", Difference);
            toolbar.Children.Add(new TextBlock { Text = "叠合比例", VerticalAlignment = VerticalAlignment.Center });
            toolbar.Children.Add(_blendRatio);
            AddButton(toolbar, "透明叠合", "Blend", Blend);
            AddButton(toolbar, "退出叠合", "BlendOff", () => _viewer.DisableBlend());
            AddButton(toolbar, "原图", "Original", () => _viewer.SelectOriginal());
            AddButton(toolbar, "结果图", "Result", () => _viewer.SelectResult());
            AddButton(toolbar, "清空", "Clear", () => _viewer.Clear());
            AddButton(toolbar, "保存PNG", "Save", Save);
            AddButton(toolbar, "复制图像", "Copy", () => _viewer.CopyToClipboard());
            Grid.SetRow(toolbar, 0); root.Children.Add(toolbar);

            WrapPanel analysis = new WrapPanel { Margin = new Thickness(3) };
            analysis.Children.Add(new TextBlock { Text = "窗下限", VerticalAlignment = VerticalAlignment.Center });
            analysis.Children.Add(_low);
            analysis.Children.Add(new TextBlock { Text = "窗上限", VerticalAlignment = VerticalAlignment.Center });
            analysis.Children.Add(_high);
            AddButton(analysis, "应用窗宽窗位", "Window", Window);
            AddButton(analysis, "还原显示", "ResetWindow", ResetDisplay);
            analysis.Children.Add(new TextBlock { Text = "Gamma", VerticalAlignment = VerticalAlignment.Center });
            analysis.Children.Add(_displayGamma);
            analysis.Children.Add(new TextBlock { Text = "亮度", VerticalAlignment = VerticalAlignment.Center });
            analysis.Children.Add(_displayBrightness);
            analysis.Children.Add(new TextBlock { Text = "对比度", VerticalAlignment = VerticalAlignment.Center });
            analysis.Children.Add(_displayContrast);
            analysis.Children.Add(_displayInverted);
            analysis.Children.Add(_autoContrast);
            AddButton(analysis, "应用显示", "ApplyDisplay", ApplyDisplay);
            analysis.Children.Add(_withRoi);
            AddButton(analysis, "灰度统计/直方图", "Analyze", Calculate);
            AddButton(analysis, "阈值掩码", "Threshold", Threshold);
            AddButton(analysis, "清除ROI", "RemoveRoi", () => _viewer.ClearSelection());
            Grid.SetRow(analysis, 1); root.Children.Add(analysis);

            WrapPanel rawInput = new WrapPanel { Margin = new Thickness(3),
                Background = new SolidColorBrush(Color.FromRgb(247,249,252)) };
            rawInput.Children.Add(new TextBlock { Text = "RAW  宽", VerticalAlignment = VerticalAlignment.Center });
            rawInput.Children.Add(_rawWidth);
            rawInput.Children.Add(new TextBlock { Text = "高", VerticalAlignment = VerticalAlignment.Center });
            rawInput.Children.Add(_rawHeight);
            foreach (FIMPixelFormat format in new[] { FIMPixelFormat.Gray8,
                FIMPixelFormat.Gray16, FIMPixelFormat.GrayFloat32,
                FIMPixelFormat.Bgr24, FIMPixelFormat.Bgra32 })
                _rawFormat.Items.Add(format);
            _rawFormat.SelectedItem = FIMPixelFormat.Gray16;
            rawInput.Children.Add(_rawFormat);
            rawInput.Children.Add(_bigEndian);
            AddButton(rawInput, "打开RAW", "RawOpen", () => OpenRawWithDialog());
            rawInput.Children.Add(new TextBlock { Text = "严格按宽高/格式检查字节数（≤512MiB）",
                VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.DimGray });
            Grid.SetRow(rawInput, 2); root.Children.Add(rawInput);

            Grid body = new Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
            Border viewerFrame = new Border { BorderBrush = Brushes.SlateGray,
                BorderThickness = new Thickness(1), Child = _viewer };
            body.Children.Add(viewerFrame);
            DockPanel side = new DockPanel { Background = Brushes.WhiteSmoke };
            TextBlock summary = new TextBlock { Text = "基础图像结果 / ROI 数据",
                FontWeight = FontWeights.SemiBold, Margin = new Thickness(8),
                Foreground = Brushes.DarkSlateBlue };
            DockPanel.SetDock(summary, Dock.Top); side.Children.Add(summary);
            DockPanel.SetDock(_overlayList, Dock.Top); side.Children.Add(_overlayList);
            WrapPanel overlayButtons = new WrapPanel { Margin = new Thickness(3) };
            AddButton(overlayButtons, "删除选中", "DeleteOverlay", DeleteSelectedOverlay);
            AddButton(overlayButtons, "左移1px", "MoveOverlay", () => MoveSelectedOverlay(-1, 0));
            AddButton(overlayButtons, "隐藏/显示层", "LayerVisibility", ToggleSelectedLayer);
            AddButton(overlayButtons, "显示所有层", "ShowAllLayers", ShowAllLayers);
            AddButton(overlayButtons, "清空标注", "ClearOverlays", () => _viewer.ClearOverlays());
            DockPanel.SetDock(overlayButtons, Dock.Top); side.Children.Add(overlayButtons);
            side.Children.Add(_info);
            Grid.SetColumn(side, 1); body.Children.Add(side);
            Grid.SetRow(body, 3); root.Children.Add(body);

            Border footer = new Border { Background = Brushes.AliceBlue,
                BorderThickness = new Thickness(0,1,0,0), BorderBrush = Brushes.LightSteelBlue,
                Child = _status };
            Grid.SetRow(footer, 4); root.Children.Add(footer);
            Content = root;
            AutomationProperties.SetAutomationId(this, "TCT.Imaging.Workbench");
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
            _viewer.SelectionChanged += (s,e) =>
            {
                if (_viewer.SelectedRegion.HasValue)
                    _status.Text = "当前ROI：" + BoundsText(_viewer.SelectedRegion.Value);
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
            Show("显示状态已应用：源图不改变，原始灰度统计保持不变");
        }

        private void ResetDisplay()
        {
            _viewer.DisplayState.Reset();
            _displayGamma.Text = "1"; _displayBrightness.Text = "0"; _displayContrast.Text = "1";
            _displayInverted.IsChecked = false; _autoContrast.IsChecked = false;
            Show("窗宽窗位/反色/Gamma/亮度/对比度全部复位");
        }

        private void Calculate()
        {
            FIMImageBuffer image = _viewer.ActiveImage;
            if (image == null) throw new InvalidOperationException("未加载图像。");
            FIMPixelRegion? region = _withRoi.IsChecked == true ? _viewer.SelectedRegion : null;
            FIMImageAnalysisResult analysis = FIMImageAnalyzer.Analyze(image, 32, region);
            FIMImageStatistics stats = analysis.Statistics;
            long[] bins = analysis.Histogram.Bins.ToArray();
            _info.Text = "图像：" + image.Width + "×" + image.Height + "\n" +
                "格式：" + image.PixelFormat + "\n" +
                "ROI：" + (region.HasValue ? BoundsText(region.Value) : "全图") + "\n" +
                "像素数：" + stats.PixelCount + "\n" +
                "Min / Max：" + stats.Min.ToString("0.###") + " / " + stats.Max.ToString("0.###") + "\n" +
                "Mean：" + stats.Mean.ToString("0.###") + "\n" +
                "StdDev：" + stats.StandardDeviation.ToString("0.###") + "\n\n" +
                "32档直方图（原始灰度域）\n" +
                string.Join("\n", bins.Select((n,i) => i.ToString("D2") + "   " + n));
            Show("原始像素统计已完成，不受窗宽窗位影响");
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
            // 所有数据归当前MPW/MVI会话管理；无设备句柄或新的工程资源。
            _viewer.Clear();
        }
    }
}
