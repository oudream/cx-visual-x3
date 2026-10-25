using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using CxVisual.Foundation.Imaging;
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
        private readonly TextBox _low = new TextBox { Width = 74, Text = "0", Margin = new Thickness(2) };
        private readonly TextBox _high = new TextBox { Width = 82, Text = "65535", Margin = new Thickness(2) };
        private readonly CheckBox _roiTool = new CheckBox { Content = "ROI框选", Margin = new Thickness(10, 3, 8, 3),
            VerticalAlignment = VerticalAlignment.Center };
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
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            WrapPanel toolbar = new WrapPanel { Background = new SolidColorBrush(Color.FromRgb(241,246,251)),
                Margin = new Thickness(3), Orientation = Orientation.Horizontal };
            AddButton(toolbar, "打开文件", "Open", () => { OpenWithDialog(); });
            AddButton(toolbar, "适应窗口", "Fit", () => _viewer.FitToWindow());
            AddButton(toolbar, "1:1", "Actual", () => _viewer.ActualSize());
            AddButton(toolbar, "放大", "ZoomIn", () => _viewer.Zoom(1.25));
            AddButton(toolbar, "缩小", "ZoomOut", () => _viewer.Zoom(0.8));
            toolbar.Children.Add(_roiTool);
            AddButton(toolbar, "裁剪为结果", "Crop", CropSelection);
            AddButton(toolbar, "归一化结果", "Normalize", Normalize);
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
            AddButton(analysis, "还原显示", "ResetWindow", () => _viewer.SetDisplayWindow(null, null));
            analysis.Children.Add(_withRoi);
            AddButton(analysis, "灰度统计/直方图", "Analyze", Calculate);
            AddButton(analysis, "清除ROI", "RemoveRoi", () => _viewer.ClearSelection());
            Grid.SetRow(analysis, 1); root.Children.Add(analysis);

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
            side.Children.Add(_info);
            Grid.SetColumn(side, 1); body.Children.Add(side);
            Grid.SetRow(body, 2); root.Children.Add(body);

            Border footer = new Border { Background = Brushes.AliceBlue,
                BorderThickness = new Thickness(0,1,0,0), BorderBrush = Brushes.LightSteelBlue,
                Child = _status };
            Grid.SetRow(footer, 3); root.Children.Add(footer);
            Content = root;
            AutomationProperties.SetAutomationId(this, "TCT.Imaging.Workbench");
            _roiTool.Checked += (s,e) => { _viewer.IsRegionTool = true; Show("框选工具已启用：拖动鼠标选择像素ROI"); };
            _roiTool.Unchecked += (s,e) => { _viewer.IsRegionTool = false; Show("鼠标左键平移、滚轮缩放"); };
            _viewer.SelectionChanged += (s,e) =>
            {
                if (_viewer.SelectedRegion.HasValue)
                    _status.Text = "当前ROI：" + BoundsText(_viewer.SelectedRegion.Value);
                if (StateChanged != null) StateChanged();
            };
        }

        private static string BoundsText(FIMPixelRegion r)
        { return "(" + r.Left + "," + r.Top + ") - (" + r.Right + "," + r.Bottom + ")"; }

        private static Button AddButton(Panel host, string label, string id, Action action)
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

        public async Task<bool> OpenFileAsync(string path)
        {
            long version = _viewer.Session.BeginOriginalLoad();
            Show("正在读取：" + System.IO.Path.GetFileName(path));
            try
            {
                FIMImageBuffer image = await Task.Run(() => MPWImageViewerControl.ReadImageFile(path));
                if (!_viewer.Session.TryCompleteOriginalLoad(version, image))
                {
                    Show("新的图像加载已覆盖此任务，旧图丢弃");
                    return false;
                }
                _viewer.ClearSelection();
                _viewer.SetDisplayWindow(null, null);
                _viewer.FitToWindow();
                Show("图像已加载：" + image.Width + "×" + image.Height + " " + image.PixelFormat);
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
