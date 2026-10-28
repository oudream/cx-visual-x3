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
using CxVisual.Foundation.Infrastructure.Logging;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Runtime.Execution;
using CxVisual.Professional.Vision;
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
        private readonly TextBox _thresholdLow=new TextBox {
            Width=82,Text="0",Margin=new Thickness(3) };
        private readonly TextBox _thresholdHigh=new TextBox {
            Width=82,Text="65535",Margin=new Thickness(3) };
        private readonly TextBox _growTolerance=new TextBox {
            Width=60,Text="1",Margin=new Thickness(3) };
        private readonly TextBox _cpuThreshold = new TextBox { Width = 82,
            Text = "10", Margin = new Thickness(3) };
        private readonly TextBox _cpuMinArea = new TextBox { Width = 70,
            Text = "2", Margin = new Thickness(3) };
        private CancellationTokenSource _cpuRunCancellation;
        private FIMImageBuffer _cpuSourceImage;
        private string _cpuSourceId;
        private CxVisionRunSummary _cpuEvidence;
        public CxVisionRunSummary CpuEvidence { get { return _cpuEvidence; } }

        private readonly CheckBox _growEightConnected=new CheckBox {
            Content="八邻域",IsChecked=false,Margin=new Thickness(5) };

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
        private readonly TextBox _polygonVertexIndex = new TextBox {
            Width = 54, Text = "0", Margin = new Thickness(3) };
        private readonly TextBox _overlayArgb = new TextBox {
            Width=94, Text="FFFF4040", Margin=new Thickness(3) };
        private readonly TextBox _overlayLineWidth = new TextBox {
            Width=54, Text="1.5", Margin=new Thickness(3) };
        private readonly TextBox _overlayLabel = new TextBox {
            MinWidth=110, Width=130, Margin=new Thickness(3) };
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
                UpdateRegionToolHighlight();
                Show("当前工具：" + selected.Content +
                    "；请在左侧图像上操作（绘制结束后自动选中结果），滚轮缩放。");
            };
            _annotationText.TextChanged += (sender,args) =>
                _viewer.AnnotationText = _annotationText.Text;
            _overlayList.SelectionChanged += (sender,args) =>
            {
                if (!_syncingOverlayList)
                {
                    _viewer.SelectOverlay(SelectedOverlayId());
                    SyncOverlayPropertyEditors();
                }
            };
            _viewer.Overlays.Changed += (sender,args) =>
            {
                CancelAnalysis();
                _histogramPlot.Clear();
                _info.Text="ROI或图层已变更，请重新计算统计。";
                RefreshOverlayList();
            };
            _viewer.Session.Changed += (sender,args) =>
            {
                if (!ReferenceEquals(_cpuSourceImage, _viewer.Session.Original))
                {
                    CancelCpuVision();
                    _cpuSourceImage = _viewer.Session.Original;
                    _cpuSourceId = _cpuSourceImage == null ? null :
                        "testcenter.imaging/" + Guid.NewGuid().ToString("N");
                    _cpuEvidence = null;
                }
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
                if (_viewer.SelectedOverlayId.HasValue)
                    SelectOverlayRow(_viewer.SelectedOverlayId);
                CancelAnalysis();
                _histogramPlot.Clear();
                _info.Text="选择区域已变化，请点击“计算灰度统计 / 直方图”。";
                if (_viewer.SelectedRegion.HasValue)
                    _status.Text = "当前ROI：" + BoundsText(_viewer.SelectedRegion.Value);
                if (_viewer.ToolMode == MVIImageToolMode.LineProfile &&
                    _viewer.Overlays.Items.Any(item => item.Kind == MVIImageOverlayKind.LineProfileMeasurement))
                    ShowLineProfile();
                if (StateChanged != null) StateChanged();
            };
        }

        private void CancelCpuVision()
        {
            // 当前运行会在自身finally中释放CTS，取消函数不提前Dispose。
            CancellationTokenSource old = _cpuRunCancellation;
            _cpuRunCancellation = null;
            if (old != null) old.Cancel();
        }

        /// <summary>
        /// 真实FIM输入→既有MRK CPU专业检测→正式Image/Mask结果→同一MPW/MVI。
        /// 不额外建立图像会话、结果历史、数据堆或调度器。
        /// </summary>
        private async void RunCpuVision()
        {
            CancellationTokenSource request = null;
            try
            {
                FIMImageBuffer original = _viewer.Session.Original;
                if (original == null) throw new InvalidOperationException("请先打开原图。");
                CxVisionPixelDepth depth = CxVisionImageInterop.GetPixelDepth(original);
                int threshold, minArea;
                if (!int.TryParse(_cpuThreshold.Text, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out threshold) || threshold < 0 ||
                    threshold > (depth == CxVisionPixelDepth.Bits8 ? 255 : 65535))
                    throw new ArgumentException("检测阈值必须在当前位深的合法范围内。");
                if (!int.TryParse(_cpuMinArea.Text, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out minArea) || minArea < 1 ||
                    minArea > 10000000)
                    throw new ArgumentException("最小区域面积须在1到10000000像素之间。");
                var options = new CxVisionInspectionOptions {
                    PixelDepth = depth, GrayThreshold = threshold,
                    MinDefectArea = minArea, MaxDefectArea = 10000000,
                    UseAi = false
                };
                string issue = CxVisionInspectionOptions.Validate(options);
                if (!string.IsNullOrEmpty(issue)) throw new ArgumentException(issue);
                CancelCpuVision();
                _cpuEvidence = null;
                request = new CancellationTokenSource();
                _cpuRunCancellation = request;
                string sourceId = _cpuSourceId;
                long version = _viewer.Session.SourceVersion;
                CancellationToken token = request.Token;
                Show("正在使用现有MRK运行CPU视觉检测（真实处理图、阈值Mask与区域）…");
                CxVisionRunSummary evidence = await Task.Run(async () =>
                {
                    token.ThrowIfCancellationRequested();
                    var inputs = CxVisionImageInterop.ToCpuInspectionInputs(original, depth);
                    var plan = CxVisionDataFlow.CreateCpuInspectionPlanWithArtifacts(options);
                    var engine = new MRKExecutionEngine(new FCRWorkerOptions(3, 10));
                    try
                    {
                        MRKFlowHandle handle = engine.StartFlow(plan, inputs);
                        using (token.Register(() => handle.RequestCancel()))
                        {
                            MRKFlowResult result = await handle.Completion.ConfigureAwait(false);
                            token.ThrowIfCancellationRequested();
                            if (result.State != MRKFlowState.Completed)
                                throw new InvalidOperationException("MRK运行失败：" + result.State +
                                    (result.Error == null ? "" : " / " + result.Error.Message));
                            return CxVisionRunSummary.FromCpuFlow(result, sourceId);
                        }
                    }
                    finally { await engine.StopAsync().ConfigureAwait(false); }
                }, token);
                if (token.IsCancellationRequested ||
                    !ReferenceEquals(request, _cpuRunCancellation) ||
                    !ReferenceEquals(original, _viewer.Session.Original) ||
                    sourceId != _cpuSourceId || version != _viewer.Session.SourceVersion)
                    return;
                CxVisionResultItem mask = evidence.Items.Single(i =>
                    i.Kind == CxVisionResultKind.Mask);
                if (mask.SourceId != sourceId || mask.RunId != evidence.RunId ||
                    mask.Image == null || mask.Image.PixelFormat != FIMPixelFormat.Gray8 ||
                    mask.Image.Width != original.Width || mask.Image.Height != original.Height)
                    throw new InvalidOperationException("运行结果来源或掩码坐标与当前原图不匹配。");
                _viewer.ShowResult(mask.Image);
                _cpuEvidence = evidence;
                var accepted = evidence.Items.Where(i => i.Kind == CxVisionResultKind.Region).ToArray();
                Show("真实CPU检测完成：阈值命中=" +
                    evidence.Items.Single(i => i.Name == "阈值命中像素数").IntegerValue +
                    " px；面积规则保留=" + accepted.Length + "区域；Run=" +
                    evidence.RunId.ToString("N") + "。结果图是阈值Mask，原图未修改。");
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { Show("CPU视觉检测失败：" + error.Message); }
            finally
            {
                if (request != null)
                {
                    if (ReferenceEquals(_cpuRunCancellation, request)) _cpuRunCancellation = null;
                    request.Dispose();
                }
            }
        }

        private static string ToolTitle(MVIImageToolMode mode)
        {
            switch (mode)
            {
                case MVIImageToolMode.Navigate: return "导航/平移";
                case MVIImageToolMode.RectangleRoi: return "矩形ROI";
                case MVIImageToolMode.RotatedRectangleRoi: return "旋转矩形ROI";
                case MVIImageToolMode.RotateOverlay: return "旋转角柄";
                case MVIImageToolMode.CircleRoi: return "圆形ROI";
                case MVIImageToolMode.EllipseRoi: return "椭圆ROI";
                case MVIImageToolMode.Distance: return "两点距离";
                case MVIImageToolMode.Point: return "点标注";
                case MVIImageToolMode.Text: return "文字标注";
                case MVIImageToolMode.Arrow: return "箭头标注";
                case MVIImageToolMode.DefectBox: return "缺陷框";
                case MVIImageToolMode.MoveOverlay: return "移动叠加项";
                case MVIImageToolMode.ResizeOverlay: return "ROI八柄编辑";
                case MVIImageToolMode.EditPolygonVertex: return "多边形逐顶点编辑";
                case MVIImageToolMode.DeleteOverlay: return "点击删除";
                case MVIImageToolMode.PolygonRoi: return "多边形ROI（双击结束）";
                case MVIImageToolMode.Angle: return "三点角度测量";
                case MVIImageToolMode.LinePair: return "两线段最短距离与夹角（四点）";
                case MVIImageToolMode.HorizontalDistance: return "水平距离";
                case MVIImageToolMode.VerticalDistance: return "垂直距离";
                case MVIImageToolMode.Diameter: return "圆形直径";
                case MVIImageToolMode.Area: return "矩形面积";
                case MVIImageToolMode.PolygonArea: return "多边形面积（双击结束）";
                case MVIImageToolMode.LineProfile: return "绘制灰度剖面线";
                default: return mode.ToString();
            }
        }

        private bool _syncingOverlayList;

        private void SelectOverlayRow(Guid? id)
        {
            if (_syncingOverlayList) return;
            _syncingOverlayList = true;
            try
            {
                ListBoxItem row = _overlayList.Items.OfType<ListBoxItem>()
                    .FirstOrDefault(x=>id.HasValue && (Guid)x.Tag==id.Value);
                _overlayList.SelectedItem=row;
            }
            finally { _syncingOverlayList=false; }
            SyncOverlayPropertyEditors();
        }

        private Guid? SelectedOverlayId()
        {
            ListBoxItem selected = _overlayList.SelectedItem as ListBoxItem;
            return selected == null ? (Guid?)null : (Guid)selected.Tag;
        }

        private void RefreshOverlayList()
        {
            Guid? keep = SelectedOverlayId();
            _syncingOverlayList = true;
            try
            {
            _overlayList.Items.Clear();
            foreach (MVIImageOverlayItem item in _viewer.Overlays.Items)
            {
                string visibility = item.IsVisible &&
                    _viewer.Overlays.IsLayerVisible(item.Layer) ? "●" : "○";
                if (item.IsLocked) visibility += "🔒";
                ListBoxItem row = new ListBoxItem { Tag = item.Id,
                    Content = visibility + " " + item.Layer + " / " + item.Kind +
                        (string.IsNullOrEmpty(_viewer.DescribeMeasurement(item)) ? "" :
                            " · " + _viewer.DescribeMeasurement(item)) +
                        (item.Kind==MVIImageOverlayKind.RotatedRectangleRoi ?
                            " · " + item.RotatedRectangleAngleDegrees.ToString("0.##") +
                            "° · " + item.RotatedRectangleWidth.ToString("0.##") + "×" +
                            item.RotatedRectangleHeight.ToString("0.##") + "px" : "") +
                        (string.IsNullOrEmpty(item.Text) ? "" : " · " + item.Text),
                    ToolTip = "ID: " + item.Id };
                _overlayList.Items.Add(row);
                if (keep.HasValue && keep.Value == item.Id) _overlayList.SelectedItem = row;
            }
            }
            finally { _syncingOverlayList=false; }
            SyncOverlayPropertyEditors();
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

        private int GetPolygonVertexIndex()
        {
            int index;
            if(!int.TryParse(_polygonVertexIndex.Text,NumberStyles.Integer,
                CultureInfo.InvariantCulture,out index))
                throw new ArgumentException("请输入有效的零基顶点或边索引。");
            return index;
        }

        private void InsertPolygonVertexAtIndex()
        {
            MVIImageOverlayItem item=RequireSelectedOverlay();
            if(item.Kind!=MVIImageOverlayKind.PolygonRoi)
                throw new InvalidOperationException("请先选择多边形ROI。");
            int index=GetPolygonVertexIndex();
            MVIImageOverlayItem updated=_viewer.InsertSelectedPolygonEdgeMidpoint(index);
            Show("多边形已在第"+index+"条边中点插入；当前"+
                updated.Points.Count+"个顶点。");
        }

        private void RemovePolygonVertexAtIndex()
        {
            MVIImageOverlayItem item=RequireSelectedOverlay();
            if(item.Kind!=MVIImageOverlayKind.PolygonRoi)
                throw new InvalidOperationException("请先选择多边形ROI。");
            int index=GetPolygonVertexIndex();
            MVIImageOverlayItem updated=_viewer.RemoveSelectedPolygonVertex(index);
            Show("多边形已删除第"+index+"个顶点；当前"+
                updated.Points.Count+"个顶点。");
        }

        private void SyncOverlayPropertyEditors()
        {
            Guid? id=SelectedOverlayId();
            MVIImageOverlayItem item=id.HasValue ? _viewer.Overlays.Find(id.Value) : null;
            if(item==null)
            {
                _overlayArgb.Text=string.Empty;
                _overlayLineWidth.Text=string.Empty;
                _overlayLabel.Text=string.Empty;
                return;
            }
            _overlayArgb.Text=((uint)item.Argb).ToString("X8",CultureInfo.InvariantCulture);
            _overlayLineWidth.Text=item.LineWidth.ToString("0.###",CultureInfo.InvariantCulture);
            _overlayLabel.Text=item.Text;
        }

        private void ApplyOverlayProperties()
        {
            MVIImageOverlayItem item=RequireSelectedOverlay();
            if(item.IsLocked)
                throw new InvalidOperationException("所选对象已锁定，无法修改属性。");
            string raw=(_overlayArgb.Text??string.Empty).Trim().TrimStart('#');
            uint argb;
            if(raw.Length!=8 || !uint.TryParse(raw,NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,out argb))
                throw new ArgumentException("颜色请输入8位ARGB十六进制，例如FF0DA6C3。");
            double width;
            if(!double.TryParse(_overlayLineWidth.Text,NumberStyles.Float,
                CultureInfo.InvariantCulture,out width) ||
                double.IsNaN(width) || double.IsInfinity(width) || width<=0 || width>100)
                throw new ArgumentOutOfRangeException("线宽须大于0且不超过100像素。");
            MVIImageOverlayItem changed=item.WithStyle(unchecked((int)argb),width)
                .WithText(_overlayLabel.Text);
            if(!_viewer.Overlays.Replace(changed))
                throw new InvalidOperationException("所选对象已被删除。");
            Show("对象属性已写入唯一MVI Store："+changed.Id);
        }

        private void SetSelectedOverlayColor(Color color)
        {
            MVIImageOverlayItem item=RequireSelectedOverlay();
            if(item.IsLocked)
                throw new InvalidOperationException("对象已锁定，请先解锁再修改颜色。");
            int argb=unchecked((int)((uint)color.A<<24 |
                (uint)color.R<<16 | (uint)color.G<<8 | color.B));
            MVIImageOverlayItem changed=item.WithStyle(argb,item.LineWidth);
            if(!_viewer.Overlays.Replace(changed))
                throw new InvalidOperationException("所选对象已删除。");
            Show("已修改选中对象颜色："+item.Kind+" · "+item.Id);
        }

        private void ToggleSelectedOverlayLock()
        {
            MVIImageOverlayItem item=RequireSelectedOverlay();
            _viewer.Overlays.SetLocked(item.Id,!item.IsLocked);
            Show(!item.IsLocked ? "ROI/标注已锁定："+item.Id : "对象已解锁："+item.Id);
        }

        private void MoveSelectedZOrder(int direction)
        {
            MVIImageOverlayItem item=RequireSelectedOverlay();
            if (_viewer.Overlays.MoveZOrder(item.Id,direction))
                Show("对象层叠顺序已更新："+item.Id);
            else Show("对象已在当前边界位置，无需调整层叠顺序。");
        }

        private void DeleteSelectedOverlay()
        {
            MVIImageOverlayItem item = RequireSelectedOverlay();
            if (item.IsLocked) throw new InvalidOperationException("对象已锁定，禁止删除。");
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
                try
                {
                    // 先清除上一次失败样式，再执行命令；不能在成功后覆盖工具选中高亮。
                    button.BorderBrush=Brushes.LightSteelBlue;
                    button.BorderThickness=new Thickness(1);
                    button.ToolTip=null;
                    AutomationProperties.SetHelpText(button,string.Empty);
                    action();
                }
                catch (Exception error)
                {
                    // 不在UI线程使用MessageBox堵塞工业图窗与无人值守WPF验收。
                    // 操作失败可见、可查询，但不修改图像Store及其它操作状态。
                    string failure="操作失败 ["+id+"]："+error.Message;
                    Show(failure);
                    button.BorderBrush=Brushes.OrangeRed;
                    button.BorderThickness=new Thickness(2);
                    button.ToolTip=failure;
                    AutomationProperties.SetHelpText(button,failure);
                }
            };
            host.Children.Add(button);
            return button;
        }

        public void SetOriginal(FIMImageBuffer image)
        {
            // 外部重新加载同一个FIM引用也代表新的Viewer源代次。
            // 不允许上一Run绑定相同引用而绕过新原图的证据清理。
            CancelCpuVision();
            _cpuSourceImage = null;
            _cpuEvidence = null;
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

        private void SideBySide()
        {
            _viewer.SideBySideOriginalAndResult(_comparisonSync.IsChecked == true);
            Show("原图/结果图真实左右双视口，默认同步缩放、平移和原图坐标ROI。");
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

        private void ReadThresholdRange(out double low,out double high)
        {
            if(!double.TryParse(_thresholdLow.Text,NumberStyles.Float,
                    CultureInfo.InvariantCulture,out low) ||
               !double.TryParse(_thresholdHigh.Text,NumberStyles.Float,
                    CultureInfo.InvariantCulture,out high) ||
               double.IsNaN(low) || double.IsInfinity(low) ||
               double.IsNaN(high) || double.IsInfinity(high) || high<low)
                throw new ArgumentException("阈值必须是有限数值，且下限不能大于上限。");
        }

        private void Threshold()
        {
            double low,high;
            ReadThresholdRange(out low,out high);
            ProcessImage(image=>FIMImageOperations.Threshold(image,low,high),"全图阈值");
        }

        /// <summary>
        /// 旧ThresholdSeg的交互语义：原图+当前用户所选精确ROI+阈值，
        /// 异步返回独立掩码及实际像素计数；不得将ROI外接框冒充真正分割区域。
        /// </summary>
        private async void ThresholdSelectedRoi()
        {
            FIMImageBuffer original=_viewer.Session.Original;
            if(original==null)
            { Show("ROI阈值失败：请先加载原图。");return; }
            double low,high;
            try { ReadThresholdRange(out low,out high); }
            catch(ArgumentException error)
            { Show("ROI阈值失败："+error.Message);return; }

            FIMPixelRegion? selected=_viewer.SelectedRegion;
            MVIImageOverlayItem area=null;
            if(_viewer.SelectedOverlayId.HasValue)
            {
                MVIImageOverlayItem chosen=_viewer.Overlays.Find(
                    _viewer.SelectedOverlayId.Value);
                if(chosen!=null && chosen.IsAreaRoi)
                {
                    if(!chosen.IsVisible || !_viewer.Overlays.IsLayerVisible(chosen.Layer))
                    { Show("ROI阈值失败：选中的形状ROI当前不可见。");return; }
                    area=chosen;
                    int left=Math.Max(0,(int)Math.Floor(chosen.Bounds.Left));
                    int top=Math.Max(0,(int)Math.Floor(chosen.Bounds.Top));
                    int right=Math.Min(original.Width-1,(int)Math.Ceiling(chosen.Bounds.Right)-1);
                    int bottom=Math.Min(original.Height-1,(int)Math.Ceiling(chosen.Bounds.Bottom)-1);
                    if(left>right || top>bottom)
                    { Show("ROI阈值失败：形状在原图没有有效像素。");return; }
                    selected=new FIMPixelRegion(left,top,right,bottom);
                }
            }
            if(!selected.HasValue)
            { Show("ROI阈值失败：先在图像中画选区或选中可见的ROI对象。");return; }
            FIMPixelRegion region;
            try { region=selected.Value.ClipTo(original); }
            catch(ArgumentOutOfRangeException error)
            { Show("ROI阈值失败："+error.Message);return; }

            MVIImageOverlayItem snapshot=area;
            CancelAnalysis();
            CancellationTokenSource cancellation=new CancellationTokenSource();
            _analysisCancellation=cancellation;
            long epoch=Interlocked.Read(ref _analysisEpoch);
            Stopwatch timer=Stopwatch.StartNew();
            Show("正在按原图精确ROI阈值分割，可切图、改ROI或发起新请求取消旧任务。");
            try
            {
                FIMThresholdRegionResult segmented=await Task.Run(()=>
                    FIMImageOperations.ThresholdInRegion(original,low,high,region,
                        snapshot==null ? (Func<int,int,bool>)null :
                            (x,y)=>snapshot.ContainsRoiPixelCenter(x,y),
                        cancellation.Token),cancellation.Token);
                if(cancellation.IsCancellationRequested ||
                   epoch!=Interlocked.Read(ref _analysisEpoch) ||
                   !ReferenceEquals(original,_viewer.Session.Original))return;
                timer.Stop();
                _viewer.ShowResult(segmented.Mask);
                _viewer.SelectResult();
                Show("ROI阈值分割完成："+segmented.PixelCount+
                    "/"+segmented.SampledPixelCount+"像素；"+
                    (snapshot==null ? BoundsText(region) : snapshot.Kind+" "+snapshot.Id)+
                    "；命中范围 "+(segmented.Bounds.HasValue ?
                        BoundsText(segmented.Bounds.Value) : "无命中")+
                    "；独立Gray8掩码在结果图，原图不变；耗时 "+
                    timer.Elapsed.TotalMilliseconds.ToString("0.#",CultureInfo.InvariantCulture)+"ms。");
            }
            catch(OperationCanceledException) { }
            catch(Exception error)
            {
                if(epoch==Interlocked.Read(ref _analysisEpoch))
                    Show("ROI阈值分割失败："+error.Message);
            }
            finally
            {
                if(ReferenceEquals(_analysisCancellation,cancellation))
                    _analysisCancellation=null;
                cancellation.Dispose();
            }
        }

        /// <summary>承接旧RegionGrowing框选语义，用已选矩形ROI中心作为种子。</summary>
        private async void RegionGrow()
        {
            FIMImageBuffer original=_viewer.Session.Original;
            if(original==null)
            { Show("区域生长失败：请先打开原图。");return; }
            FIMPixelRegion? selected=_viewer.SelectedRegion;
            if(!selected.HasValue && _viewer.SelectedOverlayId.HasValue)
            {
                MVIImageOverlayItem overlay=_viewer.Overlays.Find(
                    _viewer.SelectedOverlayId.Value);
                if(overlay!=null && overlay.Kind==MVIImageOverlayKind.RectangleRoi &&
                    overlay.IsVisible && _viewer.Overlays.IsLayerVisible(overlay.Layer))
                {
                    int left=Math.Max(0,(int)Math.Floor(overlay.Bounds.Left));
                    int top=Math.Max(0,(int)Math.Floor(overlay.Bounds.Top));
                    int right=Math.Min(original.Width-1,(int)Math.Ceiling(overlay.Bounds.Right)-1);
                    int bottom=Math.Min(original.Height-1,(int)Math.Ceiling(overlay.Bounds.Bottom)-1);
                    if(right>=left && bottom>=top)
                        selected=new FIMPixelRegion(left,top,right,bottom);
                }
            }
            if(!selected.HasValue)
            {
                Show("区域生长失败：请先绘制或选中一个矩形ROI，作为生长限定范围。");
                return;
            }
            double tolerance;
            if(!double.TryParse(_growTolerance.Text,NumberStyles.Float,
                CultureInfo.InvariantCulture,out tolerance) ||
                double.IsNaN(tolerance) || double.IsInfinity(tolerance) || tolerance<0)
            {
                Show("区域生长失败：容差需为非负有限数字。");
                return;
            }
            FIMPixelRegion roi=selected.Value.ClipTo(original);
            int seedX=roi.Left+(roi.Right-roi.Left)/2;
            int seedY=roi.Top+(roi.Bottom-roi.Top)/2;
            bool eight=_growEightConnected.IsChecked==true;
            CancelAnalysis();
            CancellationTokenSource cancellation=new CancellationTokenSource();
            _analysisCancellation=cancellation;
            long epoch=Interlocked.Read(ref _analysisEpoch);
            Stopwatch timer=Stopwatch.StartNew();
            Show("区域生长正在分析原图ROI：种子("+seedX+","+seedY+")，容差 "+
                tolerance.ToString("0.###",CultureInfo.InvariantCulture)+"，支持取消。");
            try
            {
                FIMRegionGrowingResult grown=await Task.Run(()=>
                    FIMImageOperations.RegionGrow(original,roi,seedX,seedY,
                        tolerance,eight,cancellation.Token),cancellation.Token);
                if(cancellation.IsCancellationRequested ||
                    epoch!=Interlocked.Read(ref _analysisEpoch) ||
                    !ReferenceEquals(original,_viewer.Session.Original))return;
                timer.Stop();
                _viewer.ShowResult(grown.Mask);
                _viewer.SelectResult();
                Show("区域生长完成："+grown.PixelCount+"像素，种子灰度 "+
                    grown.SeedGrayValue.ToString("0.###",CultureInfo.InvariantCulture)+
                    "，范围 "+BoundsText(grown.Bounds)+
                    "；掩码在结果图，可通过输出页保存PNG；耗时 "+
                    timer.Elapsed.TotalMilliseconds.ToString("0.#")+"ms。");
            }
            catch(OperationCanceledException) { }
            catch(Exception error)
            {
                if(epoch==Interlocked.Read(ref _analysisEpoch))
                    Show("区域生长失败："+error.Message);
            }
            finally
            {
                if(ReferenceEquals(_analysisCancellation,cancellation))
                    _analysisCancellation=null;
                cancellation.Dispose();
            }
        }

        /// <summary>
        /// 将真实0/255结果掩码中的每个外环和孔洞分别生成MVI轮廓标注。
        /// 轮廓保持原图格点坐标，不把孔洞填充为多边形面积ROI。
        /// </summary>
        private async void ExtractResultMaskContours()
        {
            FIMImageBuffer original=_viewer.Session.Original;
            FIMImageBuffer mask=_viewer.Session.Result;
            if(original==null || mask==null || mask.PixelFormat!=FIMPixelFormat.Gray8 ||
               original.Width!=mask.Width || original.Height!=mask.Height)
            {
                Show("轮廓提取失败：需要与原图同尺寸的Gray8二值分割结果。");
                return;
            }
            CancelAnalysis();
            CancellationTokenSource cancellation=new CancellationTokenSource();
            _analysisCancellation=cancellation;
            long epoch=Interlocked.Read(ref _analysisEpoch);
            long sourceVersion=_viewer.Session.SourceVersion;
            FLDLogScope log=new FLDLogScope("TCT.Imaging.MaskContours",
                Guid.NewGuid().ToString("N"));
            Stopwatch timer=Stopwatch.StartNew();
            log.Info("开始二值轮廓提取；图像="+mask.Width+"x"+mask.Height+
                "；源版本="+sourceVersion+"；最大边数=100000。");
            Show("正在追踪结果掩码的外环与孔洞，可切图取消。");
            try
            {
                IReadOnlyList<IReadOnlyList<FGEPoint2D>> rings=await Task.Run(()=>
                    FIMImageMaskContours.Trace(mask,100000,cancellation.Token),
                    cancellation.Token);
                if(cancellation.IsCancellationRequested ||
                   epoch!=Interlocked.Read(ref _analysisEpoch) ||
                   !ReferenceEquals(original,_viewer.Session.Original) ||
                   !ReferenceEquals(mask,_viewer.Session.Result))return;
                if(rings.Count>256)
                    throw new InvalidOperationException(
                        "轮廓数量超过256个，请缩小ROI；掩码仍可保留和导出。");
                if(rings.Count==0)
                {
                    Show("当前二值结果没有非零像素，也没有可生成的轮廓。");
                    return;
                }
                // 先完成数值和对象校验，再写入唯一Store，避免部分无效轮廓。
                List<MVIImageOverlayItem> objects=new List<MVIImageOverlayItem>();
                int outer=0,holes=0;
                foreach(IReadOnlyList<FGEPoint2D> points in rings)
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    bool hole=FIMImageMaskContours.SignedArea(points)<0;
                    if(hole)holes++;else outer++;
                    double left=points.Min(p=>p.X),right=points.Max(p=>p.X);
                    double top=points.Min(p=>p.Y),bottom=points.Max(p=>p.Y);
                    objects.Add(new MVIImageOverlayItem(
                        MVIImageOverlayKind.PolygonAnnotation,MVIImageOverlayLayer.Annotation,
                        new FGERect2D(left,top,right-left,bottom-top),points,
                        (hole?"孔洞轮廓 ":"分割外轮廓 ")+rings.Count+"-"+objects.Count,
                        null,unchecked((int)(hole?0xFF00D4C7u:0xFFFFD157u)),1.5));
                }
                if(cancellation.IsCancellationRequested ||
                   epoch!=Interlocked.Read(ref _analysisEpoch) ||
                   !ReferenceEquals(original,_viewer.Session.Original) ||
                   !ReferenceEquals(mask,_viewer.Session.Result))return;
                timer.Stop();
                _viewer.AddOverlays(objects);
                _viewer.SelectOriginal();
                _viewer.SelectOverlay(objects[0].Id);
                log.Info("轮廓完成；外环="+outer+"；孔洞="+holes+
                    "；耗时毫秒="+timer.Elapsed.TotalMilliseconds.ToString("0.###",CultureInfo.InvariantCulture)+
                    "；源版本="+sourceVersion+"；叠加数量="+objects.Count+"。");
                Show("结果掩码轮廓已转成"+outer+"条外环、"+holes+
                    "条孔洞标注，原图坐标/点序列已写入OverlayStore，可在输出页导出CSV；"+
                    "保留独立Gray8掩码。耗时 "+
                    timer.Elapsed.TotalMilliseconds.ToString("0.#",CultureInfo.InvariantCulture)+"ms。");
            }
            catch(OperationCanceledException)
            {
                timer.Stop();
                log.Info("轮廓任务取消；耗时毫秒="+
                    timer.Elapsed.TotalMilliseconds.ToString("0.###",CultureInfo.InvariantCulture)+"。");
            }
            catch(Exception error)
            {
                timer.Stop();
                log.Error("轮廓提取失败；源版本="+sourceVersion+
                    "；耗时毫秒="+timer.Elapsed.TotalMilliseconds.ToString("0.###",CultureInfo.InvariantCulture),error);
                if(epoch==Interlocked.Read(ref _analysisEpoch))
                    Show("轮廓提取失败："+error.Message);
            }
            finally
            {
                if(ReferenceEquals(_analysisCancellation,cancellation))
                    _analysisCancellation=null;
                cancellation.Dispose();
            }
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
            MVIImageOverlayItem areaRoi = null;
            if (_withRoi.IsChecked == true && _viewer.SelectedOverlayId.HasValue)
            {
                MVIImageOverlayItem chosen=_viewer.Overlays.Find(
                    _viewer.SelectedOverlayId.Value);
                if(chosen!=null && chosen.IsAreaRoi)
                {
                    if(!chosen.IsVisible || !_viewer.Overlays.IsLayerVisible(chosen.Layer))
                    {
                        Show("统计失败：所选ROI图层已隐藏。");
                        return;
                    }
                    areaRoi=chosen;
                    int l=Math.Max(0,(int)Math.Floor(chosen.Bounds.Left));
                    int t=Math.Max(0,(int)Math.Floor(chosen.Bounds.Top));
                    int r=Math.Min(image.Width-1,(int)Math.Ceiling(chosen.Bounds.Right)-1);
                    int b=Math.Min(image.Height-1,(int)Math.Ceiling(chosen.Bounds.Bottom)-1);
                    if(l>r || t>b)
                    {
                        Show("统计失败：所选ROI在图像内没有有效像素。");
                        return;
                    }
                    region=new FIMPixelRegion(l,t,r,b);
                }
            }
            MVIImageOverlayItem selectedRoi=areaRoi; // 线程只消费不可变MVI快照
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
                    selectedRoi!=null ?
                        FIMImageAnalyzer.AnalyzeMasked(image,256,region.Value,
                            (x,y)=>selectedRoi.ContainsRoiPixelCenter(x,y),cancellation.Token) :
                        FIMImageAnalyzer.Analyze(image,256,region,cancellation.Token),
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
                    "ROI：" + (selectedRoi!=null ?
                        selectedRoi.Kind+" · "+selectedRoi.Id.ToString("D")+"（精确形状）" :
                        region.HasValue ? BoundsText(region.Value) : "全图") + "\n" +
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
            MVIImageOverlayItem item = _viewer.ToolMode == MVIImageToolMode.LineProfile ?
                _viewer.Overlays.Items.LastOrDefault(x =>
                    x.Kind == MVIImageOverlayKind.LineProfileMeasurement && x.Points.Count >= 2) :
                id.HasValue ? _viewer.Overlays.Find(id.Value) : null;
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
                // 前12列保持兼容；追加样式、锁定及Ls2Ls数值列，避免CSV只有人类可读描述。
                output.WriteLine("Id,Layer,Kind,Visible,X,Y,Width,Height,PointCount,Value,Text,Vertices,"+
                    "Argb,LineWidthPx,Locked,LinePairDistancePixels,LinePairDistanceMillimeters,"+
                    "LinePairAngleDegrees,LinePairPhysicalAngleDegrees,ClosestOnFirstX,ClosestOnFirstY,"+
                    "ClosestOnSecondX,ClosestOnSecondY");
                foreach (MVIImageOverlayItem item in _viewer.Overlays.Items)
                {
                    FIMLinePairResult pair=item.Kind==MVIImageOverlayKind.LinePairMeasurement &&
                        item.Points.Count==4 ?
                        FIMImageMeasurement.MeasureLinePair(item.Points[0],item.Points[1],
                            item.Points[2],item.Points[3],_viewer.PixelSpacing) : null;
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
                            point.Y.ToString("R",CultureInfo.InvariantCulture))),
                        ((uint)item.Argb).ToString("X8",CultureInfo.InvariantCulture),
                        item.LineWidth.ToString("R",CultureInfo.InvariantCulture),
                        item.IsLocked.ToString(),
                        pair==null ? string.Empty :
                            pair.DistancePixels.ToString("R",CultureInfo.InvariantCulture),
                        pair==null || !pair.DistanceMillimeters.HasValue ? string.Empty :
                            pair.DistanceMillimeters.Value.ToString("R",CultureInfo.InvariantCulture),
                        pair==null ? string.Empty :
                            pair.AngleDegrees.ToString("R",CultureInfo.InvariantCulture),
                        pair==null || !pair.PhysicalAngleDegrees.HasValue ? string.Empty :
                            pair.PhysicalAngleDegrees.Value.ToString("R",CultureInfo.InvariantCulture),
                        pair==null ? string.Empty :
                            pair.ClosestOnFirst.X.ToString("R",CultureInfo.InvariantCulture),
                        pair==null ? string.Empty :
                            pair.ClosestOnFirst.Y.ToString("R",CultureInfo.InvariantCulture),
                        pair==null ? string.Empty :
                            pair.ClosestOnSecond.X.ToString("R",CultureInfo.InvariantCulture),
                        pair==null ? string.Empty :
                            pair.ClosestOnSecond.Y.ToString("R",CultureInfo.InvariantCulture)
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
