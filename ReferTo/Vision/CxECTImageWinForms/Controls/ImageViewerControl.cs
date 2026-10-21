using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CxECTImageWinForms.Halcon;
using CxECTImageWinForms.Models;
using CxECTImageWinForms.Controls.Visualization;
using CxECTImageWinForms.Models.Overlays;
using CxECTImageWinForms.Services;
using HalconDotNet;

namespace CxECTImageWinForms.Controls
{
    /// <summary>
    /// 二维看图工具的公开控件。设计器阶段只显示普通占位区，HALCON 窗口在运行时显式创建。
    /// </summary>
    public sealed partial class ImageViewerControl : UserControl
    {
        // Designer 中的固定工具按钮全部放在原生 FlowLayoutPanel 中。
        // 运行时仅登记按钮及其互斥模式，不再依赖自定义容器控件参与 Designer 反序列化。
        private readonly Dictionary<string, Button> _toolModeButtons =
            new Dictionary<string, Button>(StringComparer.Ordinal);
        private readonly HashSet<Button> _boundActionButtons = new HashSet<Button>();
        // 帧数据与显示处理分离：原图/结果图可反复切换，复位不会损失采集数据。
        private readonly ImageFrameStore _frames = new ImageFrameStore();
        private readonly ImagePresentationProcessor _presentationProcessor = new ImagePresentationProcessor();
        private readonly ImageStatisticsService _statisticsService = new ImageStatisticsService();
        // 原始活动帧的托管像素副本用于鼠标状态和同步查询，避免显示处理后的临时图影响分析结果。
        private HalconPixelSource _activePixelSource;
        private HalconImageCanvas _canvas;
        private HalconImageCanvas _comparisonCanvas;
        private SplitContainer _comparisonSplit;
        private long _latestLoadRequest;
        // 最近一次成功加载的来源；文件和内存图像必须分别处理，避免把对象来源误当成文件路径。
        private string _lastOpenedImagePath;
        private HObject _lastOpenedImageObject;
        private ImageSlot _lastOpenedImageMetadata;
        private bool _lastOpenedFromFile;
        // 标注文字由画布内的临时输入框确认后写入模型，避免侧栏长期占用编辑空间。
        private string _annotationText = "标注";
        private Guid? _pendingAnnotationId;
        private bool _contentSplitConfigured;
        private bool _toolPanelSplitConfigured;
        private bool _updatingDisplayControls;
        private bool _synchronizingViews;
        private long _latestAnalysisRequest;
        private CancellationTokenSource _analysisCancellation;

        public ImageViewerControl()
        {
            InitializeComponent();

            // 设计器只加载静态 Designer 布局，不执行 HALCON 和事件绑定。
            // 这样设计视图看到的就是 ImageViewerControl.Designer.cs 中可直接编辑的真实控件。
            if (IsInDesignMode())
            {
                return;
            }

            BindStaticToolbar();
            BindStaticToolPanels();
            BindStaticActionPanels();
            // 构造阶段 SplitContainer 还没有父容器尺寸；此时设置最小宽度会触发 WinForms 参数校验异常。
            _contentSplit.Layout += OnContentSplitLayout;
            _toolPanelSplit.Layout += OnToolPanelSplitLayout;
        }

        /// <summary>
        /// 编辑模式属于整个看图器，不属于某个 ROI/测量/标注面板；因此在控件级统一处理快捷键。
        /// </summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Control | Keys.E:
                    ActivateTopToolMode(ImageToolMode.ResizeRoi);
                    return true;
                case Keys.Control | Keys.M:
                    ActivateTopToolMode(ImageToolMode.MoveRoi);
                    return true;
                case Keys.Control | Keys.D:
                    ActivateTopToolMode(ImageToolMode.DeleteOverlay);
                    return true;
                case Keys.Control | Keys.R:
                    RestoreNavigateMode();
                    return true;
                default:
                    return base.ProcessCmdKey(ref msg, keyData);
            }
        }

        [Browsable(false)]
        public ImageDocument Document { get; private set; } = new ImageDocument();

        [Browsable(false)]
        public ImageDisplayState DisplayState { get; private set; } = new ImageDisplayState();

        [Browsable(false)]
        public ImageViewportState ViewportState { get; private set; } = new ImageViewportState();

        [Browsable(false)]
        public ImageTransformState TransformState { get; private set; } = new ImageTransformState();

        [Browsable(false)]
        public CalibrationState Calibration { get; private set; } = new CalibrationState();

        [Browsable(false)]
        public OverlayStore Overlays { get; private set; } = new OverlayStore();

        /// <summary>
        /// ROI、测量、标注、缺陷与选中态共用的显示颜色；修改后立即重绘全部叠加层。
        /// </summary>
        [Browsable(false)]
        public OverlayColorPalette OverlayColors { get; private set; } = new OverlayColorPalette();

        [Browsable(false)]
        public ImageToolMode ToolMode { get; private set; } = ImageToolMode.Navigate;

        [Browsable(false)]
        public ImageComparisonState ComparisonState { get; private set; } = new ImageComparisonState();

        /// <summary>
        /// 标注输入框默认带出的内容。宿主可在进入标注工具前赋值。
        /// </summary>
        public string AnnotationText
        {
            get { return _annotationText; }
            set { _annotationText = value ?? string.Empty; }
        }

        public event EventHandler<ImageLoadCompletedEventArgs> ImageLoadCompleted;

        /// <summary>
        /// 鼠标进入图像时输出原图像素坐标与灰度值，宿主无需依赖内部 HALCON 画布。
        /// </summary>
        public event EventHandler<PixelInfoChangedEventArgs> PixelInfoChanged;

        public void EnsureRuntimeViewer()
        {
            if (_canvas != null || IsInDesignMode())
            {
                return;
            }

            _viewerHost.Controls.Remove(_designPlaceholder);
            _canvas = new HalconImageCanvas(_viewerHost);
            _canvas.SetZoomRange(ViewportState.MinZoom, ViewportState.MaxZoom);
            _canvas.SetOverlayColors(OverlayColors);
            _canvas.PixelInfoChanged += OnPixelInfoChanged;
            _canvas.NavigationChanged += OnNavigationChanged;
            _canvas.RectangleRoiDrawn += OnRectangleRoiDrawn;
            _canvas.MeasurementDrawn += OnMeasurementDrawn;
            _canvas.MeasurementProgressChanged += OnMeasurementProgressChanged;
            _canvas.OverlaySelectionChanged += OnOverlaySelectionChanged;
            _canvas.OverlayGeometryChanged += OnOverlayGeometryChanged;
            _canvas.OverlayDeleteRequested += OnOverlayDeleteRequested;
            _canvas.AnnotationDrawn += OnAnnotationDrawn;
            UpdateImageRulerVisibility();
        }

        public void LoadImage(HObject image, ImageSlot metadata)
        {
            if (metadata == null)
            {
                throw new ArgumentNullException("metadata");
            }

            // 同步装载也必须废弃已在后台读取的旧请求，保证“最后一次意图”获胜。
            Interlocked.Increment(ref _latestLoadRequest);

            EnsureRuntimeViewer();
            if (_canvas == null)
            {
                throw new InvalidOperationException("HALCON 图窗尚未初始化。");
            }

            // 新原图是一个新的工作文档，不能保留上一张图的 ROI、测量和旧 HALCON 绘制对象。
            _canvas.Clear();
            Overlays.Clear(null);
            SetToolMode(ImageToolMode.Navigate);
            _presentationProcessor.ClearCache();
            _frames.SetOriginal(image);
            Document.SetOriginal(metadata);
            UpdateActivePixelSource();
            ViewportState.Reset();
            TransformState.Reset();
            UpdateTransformControls();
            DisplayState.Reset();
            var maximum = GetDisplayMaximum(metadata.BitDepth);
            SetDisplayControls(0, maximum, 1, maximum, maximum, maximum / 2, _canvas.SupportsWindowLevel);
            ResetDisplayEffectControls();
            RenderActiveImage(true);
            if (_canvas.SupportsWindowLevel)
            {
                RefreshWindowLevelFromImage();
            }
            RememberOpenedImageObject(image, metadata);
            UpdateImageStatus(metadata);
            _zoomStatus.Text = "Zoom: Fit";
            RefreshAnalysisAsync(false);
        }

        /// <summary>
        /// 装载与原图尺寸一致的结果图，用于原图/结果切换、双图与融合显示。
        /// </summary>
        public void LoadResultImage(HObject image, ImageSlot metadata)
        {
            if (image == null)
            {
                throw new ArgumentNullException("image");
            }

            if (metadata == null)
            {
                throw new ArgumentNullException("metadata");
            }

            if (!Document.HasImage)
            {
                throw new InvalidOperationException("请先加载原图，再加载结果图。");
            }

            _frames.SetResult(image);
            Document.SetResult(metadata);
            _presentationProcessor.ClearCache();
            SetOperationStatus("结果图已加载，可使用原图/结果或图像融合。");
        }

        /// <summary>
        /// 在已装载的原图和结果图之间切换当前显示帧。
        /// </summary>
        public void ToggleImage()
        {
            if (!Document.HasResult)
            {
                throw new InvalidOperationException("尚未加载结果图。");
            }

            var next = Document.ActiveSlot == ImageSlotKind.Original ? ImageSlotKind.Result : ImageSlotKind.Original;
            Document.Select(next);
            UpdateActivePixelSource();
            RenderActiveImage(true);
            SetOperationStatus(next == ImageSlotKind.Original ? "当前显示：原图" : "当前显示：结果图");
        }

        /// <summary>
        /// 以结果图比例融合原图和结果图。比例为 0 时为原图，1 时为结果图。
        /// </summary>
        public void BlendImages(double resultRatio)
        {
            if (!Document.HasResult)
            {
                throw new InvalidOperationException("尚未加载结果图。");
            }

            ComparisonState.SetBlendRatio(resultRatio);
            ComparisonState.SetMode(ImageComparisonMode.Blend);
            ConfigureCanvasCoordinateTransform(_canvas, Document.ActiveImage);
            using (var blend = _frames.CreateBlend(resultRatio))
            using (var presentation = _presentationProcessor.CreatePresentation(
                blend,
                TransformState,
                DisplayState,
                CreatePresentationCacheKey("blend:" + resultRatio.ToString("R"))))
            {
                _canvas.Load(presentation);
            }

            RebuildOverlays();
            SetOperationStatus(string.Format("图像融合：结果 {0:P0}", resultRatio));
        }

        /// <summary>
        /// 开启时左右同时显示原图和结果图，并同步平移与缩放；关闭后恢复当前单图。
        /// </summary>
        public void SetSyncView(bool enabled)
        {
            if (enabled && !Document.HasResult)
            {
                throw new InvalidOperationException("双图同步需要先加载结果图。");
            }

            if (enabled == ComparisonState.IsSyncViewEnabled)
            {
                return;
            }

            ComparisonState.SetSyncView(enabled);
            if (enabled)
            {
                EnsureComparisonViewer();
                ComparisonState.SetMode(ImageComparisonMode.SideBySide);
                RenderComparisonImages();
                SetOperationStatus("双图同步：左原图，右结果图");
            }
            else
            {
                RemoveComparisonViewer();
                ComparisonState.SetMode(ImageComparisonMode.Single);
                RenderActiveImage(true);
                SetOperationStatus("已关闭双图同步");
            }
        }

        public void Rotate90Clockwise()
        {
            TransformState.Rotate90Clockwise();
            EnsureToolModeCompatibleWithTransform();
            RenderActiveImage(true);
            UpdateTransformControls();
            SetOperationStatus(string.Format("图像旋转: {0:0.#}°", TransformState.RotationDegrees));
        }

        public void Rotate90Counterclockwise()
        {
            TransformState.Rotate90Counterclockwise();
            EnsureToolModeCompatibleWithTransform();
            RenderActiveImage(true);
            UpdateTransformControls();
            SetOperationStatus(string.Format("图像旋转: {0:0.#}°", TransformState.RotationDegrees));
        }

        public void SetRotation(double degrees)
        {
            TransformState.SetRotation(degrees);
            EnsureToolModeCompatibleWithTransform();
            RenderActiveImage(true);
            UpdateTransformControls();
            SetOperationStatus(string.Format("图像旋转: {0:0.#}°", TransformState.RotationDegrees));
        }

        public void FlipHorizontal()
        {
            TransformState.FlipHorizontal();
            EnsureToolModeCompatibleWithTransform();
            RenderActiveImage(true);
            UpdateTransformControls();
            SetOperationStatus(TransformState.IsFlippedHorizontally ? "已水平翻转" : "已取消水平翻转");
        }

        public void FlipVertical()
        {
            TransformState.FlipVertical();
            EnsureToolModeCompatibleWithTransform();
            RenderActiveImage(true);
            UpdateTransformControls();
            SetOperationStatus(TransformState.IsFlippedVertically ? "已垂直翻转" : "已取消垂直翻转");
        }

        public void ResetTransform()
        {
            TransformState.Reset();
            ViewportState.Reset();
            RenderActiveImage(true);
            UpdateTransformControls();
            SetOperationStatus("已重置旋转、翻转与视图位置");
        }

        /// <summary>
        /// 按最近一次矩形 ROI 裁剪当前图像，并把裁剪结果作为新的工作原图。
        /// 新工作图不能保留旧结果图和叠加层，否则尺寸改变后会出现坐标错位。
        /// </summary>
        public void CropToLastRectangleRoi()
        {
            if (!Document.HasImage)
            {
                throw new InvalidOperationException("请先加载图像，再裁剪 ROI。");
            }

            var roi = GetLastRectangleRoi();
            if (!roi.HasValue)
            {
                throw new InvalidOperationException("请先使用“矩形 ROI”框选裁剪区域。");
            }

            var image = Document.ActiveImage;
            var crop = GetCropRectangle(roi.Value, image.Size);
            HObject croppedImage = null;
            try
            {
                using (var source = _frames.GetClone(Document.ActiveSlot))
                {
                    HOperatorSet.CropPart(source, out croppedImage, crop.Top, crop.Left, crop.Width, crop.Height);
                }

                var metadata = new ImageSlot(
                    string.Format("{0}_crop", image.Name),
                    crop.Width,
                    crop.Height,
                    image.ChannelCount,
                    image.BitDepth,
                    image.PixelType);
                LoadImage(croppedImage, metadata);
                SetOperationStatus(string.Format("已裁剪 ROI：{0} × {1}", crop.Width, crop.Height));
            }
            finally
            {
                if (croppedImage != null)
                {
                    croppedImage.Dispose();
                }
            }
        }

        public void SetAutoContrast(bool enabled)
        {
            DisplayState.SetAutoContrast(enabled);
            RenderActiveImage(false);
        }

        /// <summary>
        /// 设置 16 位图的窗宽窗位。8 位或不支持的图像返回 false，且不伪造显示成功。
        /// </summary>
        public bool SetWindowLevel(int windowWidth, int windowCenter)
        {
            if (windowWidth <= 0)
            {
                throw new ArgumentOutOfRangeException("windowWidth", "窗宽必须大于零。");
            }

            var levelMinimum = Math.Min(_levelTrackBar.Minimum, windowCenter - windowWidth);
            var levelMaximum = Math.Max(_levelTrackBar.Maximum, windowCenter + windowWidth);
            var widthMaximum = Math.Max(_windowTrackBar.Maximum, windowWidth);
            SetDisplayControls(levelMinimum, levelMaximum, 1, widthMaximum, windowWidth, windowCenter, true);
            return ApplyWindowLevelFromControls();
        }

        /// <summary>
        /// 根据 16 位原图灰度范围自动设置窗宽窗位；不支持窗宽窗位的图像返回 false。
        /// </summary>
        public bool AutoWindowLevel()
        {
            return RefreshWindowLevelFromImage();
        }

        /// <summary>
        /// 按当前二维原图的灰度范围重新计算并应用窗宽窗位。
        /// </summary>
        public bool RefreshWindowLevelFromImage()
        {
            return RefreshWindowLevelFromRectangle(null, "已按整图灰度范围刷新窗宽窗位");
        }

        public void SetInverted(bool inverted)
        {
            DisplayState.SetInverted(inverted);
            RenderActiveImage(false);
        }

        public void SetGamma(double gamma)
        {
            DisplayState.SetGamma(gamma);
            RenderActiveImage(false);
        }

        public void SetBrightness(double brightness)
        {
            DisplayState.SetBrightness(brightness);
            RenderActiveImage(false);
        }

        public void SetContrast(double contrast)
        {
            DisplayState.SetContrast(contrast);
            RenderActiveImage(false);
        }

        /// <summary>
        /// 异步从文件读取图像。只有最后一次请求可提交到图窗，较早请求的 HObject 会被释放。
        /// </summary>
        public void LoadImageAsync(string fileName, ImageSlot metadata)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("图像文件路径不能为空。", "fileName");
            }

            if (metadata == null)
            {
                throw new ArgumentNullException("metadata");
            }

            EnsureRuntimeViewer();
            if (!IsHandleCreated)
            {
                throw new InvalidOperationException("ImageViewerControl 尚未创建窗口句柄，不能开始异步加载。");
            }

            var request = Interlocked.Increment(ref _latestLoadRequest);
            SetOperationStatus(string.Format("正在异步加载: {0}", fileName));
            Task.Factory.StartNew(delegate
            {
                HOperatorSet.ReadImage(out HObject image, fileName);
                return image;
            }).ContinueWith(delegate(Task<HObject> task)
            {
                if (IsDisposed || Disposing || !IsHandleCreated)
                {
                    DisposeLoadedImage(task);
                    return;
                }

                try
                {
                    BeginInvoke((Action)delegate
                    {
                        CompleteAsyncLoad(request, fileName, metadata, task);
                    });
                }
                catch (InvalidOperationException)
                {
                    // 控件正在销毁，后台创建的图像不能交给已失效的窗口。
                    DisposeLoadedImage(task);
                }
            });
        }

        public void ClearImage()
        {
            // 清空是一次新的用户意图，不能被已经开始的异步读取反向覆盖。
            Interlocked.Increment(ref _latestLoadRequest);
            Interlocked.Increment(ref _latestAnalysisRequest);
            CancelAnalysis();
            CancelPendingAnnotationTextInput(false);
            if (_canvas != null)
            {
                _canvas.Clear();
            }

            Document.Clear();
            _frames.Clear();
            _presentationProcessor.ClearCache();
            DisposeActivePixelSource();
            Overlays.Clear(null);
            // 图像关闭后不能保留临时框选意图；新图加载时必须从普通浏览模式开始。
            _autoWindowLevelRoiCheckBox.Checked = false;
            SetToolMode(ImageToolMode.Navigate);
            DisplayState.Reset();
            TransformState.Reset();
            UpdateTransformControls();
            ViewportState.Reset();
            SetDisplayControls(0, 16383, 1, 16383, 16383, 8191, false);
            ResetDisplayEffectControls();
            _imageStatus.Text = "图像: --";
            _pixelStatus.Text = "X/Y: --  Gray: --";
            _zoomStatus.Text = "Zoom: 100%";
            _statisticsSummaryLabel.Text = "统计：等待图像";
        }

        /// <summary>
        /// 切回原图并恢复刚打开时的显示状态。ROI、测量和标注属于用户数据，不能由普通重置静默清除。
        /// </summary>
        public void ResetImage()
        {
            if (Document.Original == null)
            {
                SetOperationStatus("重置失败：尚未加载原图");
                return;
            }

            if (ComparisonState.IsSyncViewEnabled)
            {
                SetSyncView(false);
            }

            Document.Select(ImageSlotKind.Original);
            ComparisonState.SetMode(ImageComparisonMode.Single);
            UpdateActivePixelSource();
            SetToolMode(ImageToolMode.Navigate);
            ResetView();
            SetOperationStatus("已恢复原图的初始显示状态");
        }

        public void RefreshImage()
        {
            if (_canvas != null)
            {
                _canvas.Refresh();
            }
        }

        public void FitToWindow()
        {
            if (_canvas == null)
            {
                return;
            }

            _canvas.FitToWindow();
            ViewportState.SetFit(_canvas.GetCurrentZoom(), new ImagePoint(0, 0));
            _zoomStatus.Text = "Zoom: Fit";
        }

        public void ActualSize()
        {
            if (_canvas == null)
            {
                return;
            }

            _canvas.ActualSize();
            ViewportState.SetZoom(_canvas.GetCurrentZoom());
            _zoomStatus.Text = "Zoom: 100%";
        }

        public void ResetPan()
        {
            if (_canvas == null || !Document.HasImage)
            {
                // 工具栏在未打开图像时仍可见；不能把空 HALCON 对象交给位置复位逻辑。
                SetOperationStatus("位置复位失败：当前没有打开的图像");
                return;
            }

            _canvas.ResetPan();
            ViewportState.ResetPan();
        }

        /// <summary>
        /// 设置按钮与鼠标滚轮共用的缩放边界，防止视口被无限放大或缩小。
        /// </summary>
        public void SetZoomRange(double minimum, double maximum)
        {
            ViewportState.SetZoomRange(minimum, maximum);
            if (_canvas != null)
            {
                _canvas.SetZoomRange(minimum, maximum);
            }
        }

        /// <summary>
        /// 保存当前显示图像，供宿主在不使用内置面板时直接调用。
        /// </summary>
        public bool SaveCurrentImage(string fileName)
        {
            return _canvas != null && _canvas.SaveCurrentImage(fileName);
        }

        /// <summary>
        /// 复制完整图像（包含当前 ROI 与标注）到剪贴板。
        /// </summary>
        public bool CopyCurrentViewToClipboard()
        {
            return _canvas != null && _canvas.CopyCurrentViewToClipboard();
        }

        /// <summary>
        /// 计算整图或最后一个矩形 ROI 的灰度统计。结果始终基于原始像素。
        /// </summary>
        public ImageStatistics CalculateStatistics(bool useLastRectangleRoi)
        {
            return _statisticsService.Calculate(GetActivePixelSource(), useLastRectangleRoi ? GetLastRectangleRoi() : null);
        }

        /// <summary>
        /// 计算整图或最后一个矩形 ROI 的直方图。默认使用 256 个分箱。
        /// </summary>
        public ImageHistogram GetHistogram(bool useLastRectangleRoi, int binCount)
        {
            return _statisticsService.GetHistogram(GetActivePixelSource(), useLastRectangleRoi ? GetLastRectangleRoi() : null, binCount);
        }

        /// <summary>
        /// 获取原图坐标中两点之间的灰度剖面。
        /// </summary>
        public IReadOnlyList<double> GetLineProfile(ImagePoint start, ImagePoint end)
        {
            return _statisticsService.GetLineProfile(GetActivePixelSource(), start, end);
        }

        /// <summary>
        /// 获取原图指定位置的灰度值，不受当前显示效果影响。
        /// </summary>
        public double GetPixelValue(ImagePoint sourcePoint)
        {
            return GetActivePixelSource().GetGrayValue((int)Math.Round(sourcePoint.X), (int)Math.Round(sourcePoint.Y));
        }

        /// <summary>
        /// 获取彩色原图指定位置的 RGB 分量；单通道图返回 null。
        /// </summary>
        public ImagePixelColor GetPixelColor(ImagePoint sourcePoint)
        {
            return GetActivePixelSource().GetPixelColor((int)Math.Round(sourcePoint.X), (int)Math.Round(sourcePoint.Y));
        }

        /// <summary>
        /// 将像素位移换算为物理距离；未标定时返回 null，避免把像素值伪装成毫米值。
        /// </summary>
        public double? PixelToWorld(double pixelX, double pixelY)
        {
            return Calibration.PixelToWorld(pixelX, pixelY);
        }

        private HalconPixelSource GetActivePixelSource()
        {
            if (_activePixelSource == null)
            {
                throw new InvalidOperationException("尚未加载图像。");
            }

            return _activePixelSource;
        }

        private HalconPixelSource CreateActivePixelSnapshot()
        {
            if (!Document.HasImage)
            {
                throw new InvalidOperationException("尚未加载图像。");
            }

            var image = _frames.GetClone(Document.ActiveSlot);
            try
            {
                return new HalconPixelSource(image);
            }
            finally
            {
                image.Dispose();
            }
        }

        private void UpdateActivePixelSource()
        {
            var source = CreateActivePixelSnapshot();
            DisposeActivePixelSource();
            _activePixelSource = source;
        }

        private void DisposeActivePixelSource()
        {
            if (_activePixelSource != null)
            {
                _activePixelSource.Dispose();
                _activePixelSource = null;
            }
        }

        public bool DeleteRoi(Guid id)
        {
            var item = Overlays.Find(id);
            if (item == null || item.Kind != ImageOverlayKind.RectangleRoi)
            {
                return false;
            }

            if (_canvas != null && !string.IsNullOrWhiteSpace(item.ExternalId))
            {
                _canvas.RemoveRectangleRoi(Guid.Parse(item.ExternalId));
            }

            return Overlays.Remove(id);
        }

        /// <summary>
        /// 删除指定人工标注。调用方可从 <see cref="Overlays"/> 中取得标注 ID 后精确删除。
        /// </summary>
        public bool RemoveAnnotation(Guid id)
        {
            var item = Overlays.Find(id);
            if (item == null || item.Layer != ImageOverlayLayer.Annotation)
            {
                return false;
            }

            var removed = Overlays.Remove(id);
            if (removed)
            {
                RebuildOverlays();
            }

            return removed;
        }

        /// <summary>
        /// 清除全部人工标注，不影响 ROI、测量和检测缺陷框。
        /// </summary>
        public void ClearAnnotations()
        {
            CancelPendingAnnotationTextInput(false);
            Overlays.Clear(ImageOverlayLayer.Annotation);
            RebuildOverlays();
        }

        /// <summary>
        /// 清除全部人工测量，不影响 ROI、标注和检测缺陷框。
        /// </summary>
        public void ClearMeasurements()
        {
            // 正在绘制的测量没有进入叠加集合，先退出测量模式以清掉画布临时预览。
            if (IsMeasurementToolMode(ToolMode))
            {
                SetToolMode(ImageToolMode.Navigate);
            }

            Overlays.Clear(ImageOverlayLayer.Measurement);
            _lineProfilePlot.ClearValues();
            RebuildOverlays();
            SetOperationStatus("已清除全部测量");
        }

        public bool MoveRoi(Guid id, double deltaX, double deltaY)
        {
            var item = Overlays.Find(id);
            if (item == null || item.Kind != ImageOverlayKind.RectangleRoi)
            {
                return false;
            }

            item.Bounds = item.Bounds.Offset(deltaX, deltaY);
            RefreshRectangleRoi(item);
            return true;
        }

        public bool ResizeRoi(Guid id, ImageRectangle bounds)
        {
            var item = Overlays.Find(id);
            if (item == null || item.Kind != ImageOverlayKind.RectangleRoi)
            {
                return false;
            }

            item.Bounds = bounds;
            RefreshRectangleRoi(item);
            return true;
        }

        /// <summary>
        /// 显示检测结果框。传入新结果会替换旧的缺陷层，不影响 ROI、测量及人工标注。
        /// </summary>
        public void ShowDefects(IEnumerable<ImageRectangle> defects)
        {
            Overlays.Clear(ImageOverlayLayer.Defect);
            if (defects != null)
            {
                foreach (var bounds in defects)
                {
                    Overlays.Add(new ImageOverlayItem(ImageOverlayKind.DefectBox, ImageOverlayLayer.Defect)
                    {
                        Bounds = bounds,
                        Text = "Defect"
                    });
                }
            }

            RebuildOverlays();
        }

        /// <summary>
        /// 显示或隐藏一个叠加图层；隐藏不会删除其数据。
        /// </summary>
        public void SetLayerVisible(ImageOverlayLayer layer, bool visible)
        {
            Overlays.SetLayerVisible(layer, visible);
            RebuildOverlays();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                RemoveComparisonViewer();
            }

            if (disposing && _canvas != null)
            {
                _canvas.PixelInfoChanged -= OnPixelInfoChanged;
                _canvas.NavigationChanged -= OnNavigationChanged;
                _canvas.RectangleRoiDrawn -= OnRectangleRoiDrawn;
                _canvas.MeasurementDrawn -= OnMeasurementDrawn;
                _canvas.MeasurementProgressChanged -= OnMeasurementProgressChanged;
                _canvas.OverlaySelectionChanged -= OnOverlaySelectionChanged;
                _canvas.OverlayGeometryChanged -= OnOverlayGeometryChanged;
                _canvas.OverlayDeleteRequested -= OnOverlayDeleteRequested;
                _canvas.AnnotationDrawn -= OnAnnotationDrawn;
                _canvas.Dispose();
                _canvas = null;
            }

            if (disposing)
            {
                CancelAnalysis();
                DisposeActivePixelSource();
                _presentationProcessor.Dispose();
                _frames.Dispose();
                DisposeLastOpenedImageObject();
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// 工具栏控件由 Designer 固定声明；这里只绑定行为，避免静态布局散落在业务代码中。
        /// </summary>
        private void BindStaticToolbar()
        {
            _openImageButton.Click += delegate { OpenImageFromToolbar(); };
            _reopenImageButton.Click += delegate { ReopenImageFromToolbar(); };
            _fitButton.Click += delegate { FitToWindow(); };
            _actualSizeButton.Click += delegate { ActualSize(); };
            _zoomInButton.Click += delegate { Zoom(1); };
            _zoomOutButton.Click += delegate { Zoom(-1); };
            _resetPanButton.Click += delegate { ResetPan(); };
            _closeImageButton.Click += delegate { CloseImageFromToolbar(); };
            _editGeometryToolButton.Click += delegate { ActivateTopToolMode(ImageToolMode.ResizeRoi); };
            _moveObjectToolButton.Click += delegate { ActivateTopToolMode(ImageToolMode.MoveRoi); };
            _deleteObjectToolButton.Click += delegate { ActivateTopToolMode(ImageToolMode.DeleteOverlay); };
            _navigateToolButton.Click += delegate { RestoreNavigateMode(); };
            _resetImageButton.Click += delegate { ResetImage(); };
            UpdateToolbarToolSelection(ToolMode);
        }

        private void CloseImageFromToolbar()
        {
            if (!Document.HasImage)
            {
                SetOperationStatus("关闭失败：当前没有打开的图像");
                return;
            }

            // 关闭会连同用户绘制的数据一起清除，必须由用户明确确认。
            DialogResult result = MessageBox.Show(
                FindForm(),
                "关闭当前图像后，ROI、测量、标注和显示缓存也会被清除。是否继续？",
                "关闭图像",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (result != DialogResult.Yes)
            {
                SetOperationStatus("已取消关闭图像");
                return;
            }

            ClearImage();
            SetOperationStatus("已关闭当前图像");
        }

        private void ActivateTopToolMode(ImageToolMode mode)
        {
            SetToolMode(mode);
            SetOperationStatus(GetToolInstruction(ToolMode, 0));
        }

        private void RestoreNavigateMode()
        {
            if (_autoWindowLevelRoiCheckBox.Checked)
            {
                _autoWindowLevelRoiCheckBox.Checked = false;
            }

            SetToolMode(ImageToolMode.Navigate);
            SetOperationStatus("浏览模式：已退出绘制与编辑，可拖动图像；快捷键 Ctrl+R");
        }

        /// <summary>
        /// 由工具栏直接打开图片。图像读取和元数据构造在控件内完成，宿主无需重复 HALCON 读取逻辑。
        /// </summary>
        private void OpenImageFromToolbar()
        {
            using (var dialog = new OpenFileDialog
            {
                Filter = "图像文件|*.bmp;*.png;*.jpg;*.jpeg;*.tif;*.tiff|所有文件|*.*",
                Title = "打开图片"
            })
            {
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
                {
                    return;
                }

                LoadImageFileFromToolbar(dialog.FileName, "已打开图片");
            }
        }

        private void ReopenImageFromToolbar()
        {
            if (_lastOpenedFromFile)
            {
                if (string.IsNullOrWhiteSpace(_lastOpenedImagePath) || !File.Exists(_lastOpenedImagePath))
                {
                    _reopenImageButton.Enabled = false;
                    SetOperationStatus("重新打开失败：上次图像文件不存在");
                    return;
                }

                // 文件来源重新读取磁盘内容，外部程序覆盖文件后也能得到最新图像。
                LoadImageFileFromToolbar(_lastOpenedImagePath, "已重新打开图像文件");
                return;
            }

            if (_lastOpenedImageObject == null || _lastOpenedImageMetadata == null)
            {
                _reopenImageButton.Enabled = false;
                SetOperationStatus("重新打开失败：没有可恢复的图像来源");
                return;
            }

            LoadImage(_lastOpenedImageObject, _lastOpenedImageMetadata);
            SetOperationStatus("已重新打开图像对象：" + _lastOpenedImageMetadata.Name);
        }

        private void LoadImageFileFromToolbar(string fileName, string successStatus)
        {
            HObject image = null;
            try
            {
                HOperatorSet.ReadImage(out image, fileName);
                HOperatorSet.GetImageSize(image, out HTuple width, out HTuple height);
                HOperatorSet.CountChannels(image, out HTuple channels);
                HOperatorSet.GetImageType(image, out HTuple type);
                LoadImage(image, new ImageSlot(
                    Path.GetFileName(fileName),
                    width.I,
                    height.I,
                    channels.I,
                    GetImageBitDepth(type.S),
                    type.S));
                RememberOpenedImageFile(fileName);
                SetOperationStatus(string.Format("{0}：{1}", successStatus, Path.GetFileName(fileName)));
            }
            catch (Exception exception)
            {
                MessageBox.Show(FindForm(), exception.Message, "打开图片失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetOperationStatus(string.Format("打开图片失败：{0}", exception.Message));
            }
            finally
            {
                if (image != null)
                {
                    image.Dispose();
                }
            }
        }

        private void RememberOpenedImageFile(string fileName)
        {
            DisposeLastOpenedImageObject();
            _lastOpenedImagePath = Path.GetFullPath(fileName);
            _lastOpenedImageMetadata = null;
            _lastOpenedFromFile = true;
            _reopenImageButton.Enabled = true;
        }

        private void RememberOpenedImageObject(HObject image, ImageSlot metadata)
        {
            var clone = image.Clone();
            DisposeLastOpenedImageObject();
            _lastOpenedImageObject = clone;
            _lastOpenedImageMetadata = metadata;
            _lastOpenedImagePath = null;
            _lastOpenedFromFile = false;
            _reopenImageButton.Enabled = true;
        }

        private void DisposeLastOpenedImageObject()
        {
            if (_lastOpenedImageObject != null)
            {
                _lastOpenedImageObject.Dispose();
                _lastOpenedImageObject = null;
            }
        }

        private void UpdateImageStatus(ImageSlot metadata)
        {
            var pixelType = string.IsNullOrWhiteSpace(metadata.PixelType)
                ? string.Format("{0}位", metadata.BitDepth)
                : GetPixelTypeDisplayName(metadata.PixelType);
            _imageStatus.Text = string.Format(
                "图像: {0} × {1}  通道: {2}  类型: {3}",
                metadata.Size.Width,
                metadata.Size.Height,
                metadata.ChannelCount,
                pixelType);
        }

        private static string GetPixelTypeDisplayName(string pixelType)
        {
            switch (pixelType)
            {
                case "byte": return "8U";
                case "int1": return "1S";
                case "int2": return "2S";
                case "uint2": return "2U";
                case "int4": return "4S";
                case "real": return "浮点";
                default: return pixelType;
            }
        }

        private static int GetImageBitDepth(string imageType)
        {
            switch (imageType)
            {
                case "byte":
                    return 8;
                case "int1":
                case "int2":
                case "uint2":
                    return 16;
                case "int4":
                case "real":
                    return 32;
                default:
                    return 8;
            }
        }

        /// <summary>
        /// 固定小面板均由 ImageViewerControl.Designer.cs 声明；这里仅绑定行为，避免布局与业务状态交叉。
        /// </summary>
        private void BindStaticToolPanels()
        {
            _displaySettingsTabButton.Click += delegate { SelectDisplayPage(_displaySettingsPage, _displaySettingsTabButton); };
            _windowLevelTabButton.Click += delegate { SelectDisplayPage(_windowLevelPage, _windowLevelTabButton); };
            _toneTabButton.Click += delegate { SelectDisplayPage(_tonePage, _toneTabButton); };
            _pixelAnalysisTabButton.Click += delegate { SelectOperationPage(_pixelAnalysisPage, _pixelAnalysisTabButton); };
            _profileAnalysisTabButton.Click += delegate { SelectOperationPage(_profileAnalysisPage, _profileAnalysisTabButton); };
            _roiTabButton.Click += delegate { SelectOperationPage(_roiPage, _roiTabButton); };
            _transformTabButton.Click += delegate { SelectOperationPage(_transformPanel, _transformTabButton); };
            _measurementTabButton.Click += delegate { SelectDisplayPage(_measurementPage, _measurementTabButton); };
            _annotationTabButton.Click += delegate { SelectDisplayPage(_annotationPage, _annotationTabButton); };
            _outputTabButton.Click += delegate { SelectOperationPage(_outputPage, _outputTabButton); };

            _applyWindowLevelCheckBox.CheckedChanged += delegate { ApplyWindowLevelFromControls(); };
            _autoWindowLevelRoiCheckBox.CheckedChanged += delegate { ToggleAutoWindowLevelRoiSelection(); };
            _refreshWindowLevelButton.Click += delegate { RefreshWindowLevelFromImage(); };
            _windowValueEditor.ValueChanged += delegate { SynchronizeWindowWidthFromEditor(); };
            _windowTrackBar.ValueChanged += delegate { SynchronizeWindowWidthFromTrackBar(); };
            _levelValueEditor.ValueChanged += delegate { SynchronizeWindowLevelFromEditor(); };
            _levelTrackBar.ValueChanged += delegate { SynchronizeWindowLevelFromTrackBar(); };
            _autoContrastCheckBox.CheckedChanged += delegate { SetAutoContrast(_autoContrastCheckBox.Checked); };
            _invertCheckBox.CheckedChanged += delegate { SetInverted(_invertCheckBox.Checked); };
            _gammaTrackBar.ValueChanged += delegate { SetGamma(_gammaTrackBar.Value / 100.0); UpdateDisplayEffectLabels(); };
            _brightnessTrackBar.ValueChanged += delegate { SetBrightness(_brightnessTrackBar.Value); UpdateDisplayEffectLabels(); };
            _contrastTrackBar.ValueChanged += delegate { SetContrast(_contrastTrackBar.Value / 100.0); UpdateDisplayEffectLabels(); };
            _rotate90Button.Click += delegate { Rotate90Clockwise(); };
            _rotateCounterClockwiseButton.Click += delegate { Rotate90Counterclockwise(); };
            _applyRotationButton.Click += delegate { SetRotation((double)_rotationAngleInput.Value); };
            _flipHorizontalButton.Click += delegate { FlipHorizontal(); };
            _flipVerticalButton.Click += delegate { FlipVertical(); };
            _resetTransformButton.Click += delegate { ResetTransform(); };
            _cropToRoiButton.Click += delegate { CropToRoiFromPanel(); };
            _annotationInputBox.KeyDown += OnAnnotationInputBoxKeyDown;
            _annotationInputBox.LostFocus += delegate { CancelPendingAnnotationTextInput(false); };
            _showImageRulerCheckBox.CheckedChanged += delegate { UpdateImageRulerVisibility(); };
            BindOverlayColorButton(_roiColorButton, ImageOverlayLayer.Roi, "ROI 颜色");
            BindOverlayColorButton(_measurementColorButton, ImageOverlayLayer.Measurement, "测量颜色");
            BindOverlayColorButton(_annotationColorButton, ImageOverlayLayer.Annotation, "标注颜色");
            BindOverlayColorButton(_defectColorButton, ImageOverlayLayer.Defect, "缺陷颜色");
            BindSelectedColorButton();
            UpdateOverlayColorButtons();
            BindLayerVisibility(_showRoiCheckBox, ImageOverlayLayer.Roi);
            BindLayerVisibility(_showMeasurementCheckBox, ImageOverlayLayer.Measurement);
            BindLayerVisibility(_showAnnotationCheckBox, ImageOverlayLayer.Annotation);
            BindLayerVisibility(_showDefectCheckBox, ImageOverlayLayer.Defect);

            SelectDisplayPage(_displaySettingsPage, _displaySettingsTabButton);
            SelectOperationPage(_roiPage, _roiTabButton);
            UpdateWindowLevelLabels();
            UpdateWindowLevelControls();
            UpdateDisplayEffectLabels();
            UpdateTransformControls();
        }

        /// <summary>
        /// 所有变换入口均回写此面板，避免按钮状态与实际渲染状态不一致。
        /// </summary>
        private void UpdateTransformControls()
        {
            _rotationAngleInput.Value = (decimal)TransformState.RotationDegrees;
            SetTransformToggleAppearance(_flipHorizontalButton, TransformState.IsFlippedHorizontally);
            SetTransformToggleAppearance(_flipVerticalButton, TransformState.IsFlippedVertically);
        }

        private static void SetTransformToggleAppearance(Button button, bool selected)
        {
            button.BackColor = selected ? Color.FromArgb(225, 238, 255) : Color.White;
            button.ForeColor = selected ? Color.FromArgb(35, 120, 220) : Color.FromArgb(54, 62, 76);
            button.FlatAppearance.BorderColor = selected ? Color.FromArgb(110, 170, 235) : Color.FromArgb(210, 216, 224);
        }

        /// <summary>
        /// 固定命令面板在设计器内可见；这里统一接入命令事件和互斥状态广播。
        /// </summary>
        private void BindStaticActionPanels()
        {
            RegisterActionPanel(_pixelAnalysisActionPanel);
            RegisterActionPanel(_profileAnalysisPanel);
            RegisterActionPanel(_roiActionPanel);
            RegisterActionPanel(_measurementActionPanel);
            RegisterActionPanel(_annotationActionPanel);
            RegisterActionPanel(_outputActionPanel);
        }

        private void SelectDisplayPage(Control selectedPage, Button selectedButton)
        {
            SelectPage(selectedPage, selectedButton,
                _displaySettingsPage, _displaySettingsTabButton,
                _windowLevelPage, _windowLevelTabButton,
                _tonePage, _toneTabButton,
                _measurementPage, _measurementTabButton,
                _annotationPage, _annotationTabButton);
        }

        private void SelectOperationPage(Control selectedPage, Button selectedButton)
        {
            SelectPage(selectedPage, selectedButton,
                _roiPage, _roiTabButton,
                _pixelAnalysisPage, _pixelAnalysisTabButton,
                _profileAnalysisPage, _profileAnalysisTabButton,
                _transformPanel, _transformTabButton,
                _outputPage, _outputTabButton);
        }

        private static void SelectPage(Control selectedPage, Button selectedButton, params object[] pagesAndButtons)
        {
            for (var index = 0; index < pagesAndButtons.Length; index += 2)
            {
                var page = (Control)pagesAndButtons[index];
                var button = (Button)pagesAndButtons[index + 1];
                var selected = page == selectedPage;
                page.Visible = selected;
                if (selected)
                {
                    page.BringToFront();
                }

                button.BackColor = selected ? Color.FromArgb(234, 243, 255) : Color.FromArgb(249, 250, 252);
                button.ForeColor = selected ? Color.FromArgb(45, 127, 230) : Color.FromArgb(42, 50, 64);
            }
        }

        private void BindLayerVisibility(CheckBox checkBox, ImageOverlayLayer layer)
        {
            checkBox.CheckedChanged += delegate { SetLayerVisible(layer, checkBox.Checked); };
        }

        private void BindOverlayColorButton(Button button, ImageOverlayLayer layer, string title)
        {
            button.Click += delegate
            {
                var color = SelectOverlayColor(OverlayColors.GetLayerColor(layer), title);
                if (!color.HasValue)
                {
                    return;
                }

                switch (layer)
                {
                    case ImageOverlayLayer.Roi: OverlayColors.Roi = color.Value; break;
                    case ImageOverlayLayer.Measurement: OverlayColors.Measurement = color.Value; break;
                    case ImageOverlayLayer.Annotation: OverlayColors.Annotation = color.Value; break;
                    case ImageOverlayLayer.Defect: OverlayColors.Defect = color.Value; break;
                }

                ApplyOverlayColors();
                SetOperationStatus(title + "已更新");
            };
        }

        private void BindSelectedColorButton()
        {
            _selectedColorButton.Click += delegate
            {
                var color = SelectOverlayColor(OverlayColors.Selected, "当前选中颜色");
                if (color.HasValue)
                {
                    OverlayColors.Selected = color.Value;
                    ApplyOverlayColors();
                }
            };
        }

        private Color? SelectOverlayColor(Color initialColor, string title)
        {
            using (var dialog = new ColorDialog { Color = initialColor, FullOpen = true })
            {
                dialog.AnyColor = true;
                dialog.SolidColorOnly = true;
                dialog.AllowFullOpen = true;
                return dialog.ShowDialog(FindForm()) == DialogResult.OK ? (Color?)dialog.Color : null;
            }
        }

        private void ApplyOverlayColors()
        {
            UpdateOverlayColorButtons();
            if (_canvas != null)
            {
                _canvas.SetOverlayColors(OverlayColors);
            }

            if (_comparisonCanvas != null)
            {
                _comparisonCanvas.SetOverlayColors(OverlayColors);
            }

            RebuildOverlays();
        }

        private void UpdateOverlayColorButtons()
        {
            UpdateOverlayColorButton(_roiColorButton, OverlayColors.Roi);
            UpdateOverlayColorButton(_measurementColorButton, OverlayColors.Measurement);
            UpdateOverlayColorButton(_annotationColorButton, OverlayColors.Annotation);
            UpdateOverlayColorButton(_defectColorButton, OverlayColors.Defect);
            UpdateOverlayColorButton(_selectedColorButton, OverlayColors.Selected);
        }

        private static void UpdateOverlayColorButton(Button button, Color color)
        {
            button.BackColor = color;
            button.ForeColor = color.GetBrightness() < 0.6F ? Color.White : Color.Black;
        }

        private void Zoom(int direction)
        {
            if (_canvas == null)
            {
                return;
            }

            if (direction > 0)
            {
                _canvas.ZoomIn();
            }
            else
            {
                _canvas.ZoomOut();
            }
        }

        private void ResetView()
        {
            TransformState.Reset();
            UpdateTransformControls();
            DisplayState.Reset();
            if (Document.ActiveImage != null)
            {
                var maximum = GetDisplayMaximum(Document.ActiveImage.BitDepth);
                // 内部显示状态与右侧滑块必须同步复位，避免画面和控件显示两套窗宽窗位。
                SetDisplayControls(0, maximum, 1, maximum, maximum, maximum / 2, _canvas != null && _canvas.SupportsWindowLevel);
            }
            ViewportState.Reset();
            ResetDisplayEffectControls();
            RenderActiveImage(true);
            if (_canvas != null && _canvas.SupportsWindowLevel)
            {
                RefreshWindowLevelFromImage();
            }
        }

        /// <summary>
        /// 将当前帧和显示状态提交给 HALCON。这里是唯一的图像重绘入口，
        /// 所有变换、灰度处理及原图/结果切换均经由此处，避免状态与画面不一致。
        /// </summary>
        private void RenderActiveImage(bool fitToWindow)
        {
            if (!Document.HasImage || _canvas == null)
            {
                return;
            }

            if (ComparisonState.IsSyncViewEnabled)
            {
                RenderComparisonImages();
                return;
            }

            ConfigureCanvasCoordinateTransform(_canvas, Document.ActiveImage);
            using (var source = _frames.GetClone(Document.ActiveSlot))
            using (var presentation = _presentationProcessor.CreatePresentation(
                source,
                TransformState,
                DisplayState,
                CreatePresentationCacheKey("active:" + Document.ActiveSlot)))
            {
                _canvas.Load(presentation);
            }

            RebuildOverlays();
            if (fitToWindow)
            {
                FitToWindow();
            }

            UpdateExternalImageRulers();

            if (_applyWindowLevelCheckBox.Checked)
            {
                ApplyWindowLevelFromControls();
            }
        }

        private void EnsureComparisonViewer()
        {
            if (_comparisonCanvas != null)
            {
                return;
            }

            if (_canvas == null)
            {
                EnsureRuntimeViewer();
            }

            _comparisonSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 4,
                BackColor = Color.FromArgb(222, 229, 238)
            };
            _viewerHost.Controls.Remove(_canvas.View);
            _comparisonSplit.Panel1.Controls.Add(_canvas.View);
            _viewerHost.Controls.Add(_comparisonSplit);
            _comparisonCanvas = new HalconImageCanvas(_comparisonSplit.Panel2);
            _comparisonCanvas.SetZoomRange(ViewportState.MinZoom, ViewportState.MaxZoom);
            _comparisonCanvas.NavigationChanged += OnNavigationChanged;
        }

        private void RemoveComparisonViewer()
        {
            if (_comparisonCanvas == null)
            {
                return;
            }

            _comparisonCanvas.NavigationChanged -= OnNavigationChanged;
            _comparisonCanvas.Dispose();
            _comparisonCanvas = null;
            if (_comparisonSplit != null)
            {
                _comparisonSplit.Panel1.Controls.Remove(_canvas.View);
                _viewerHost.Controls.Remove(_comparisonSplit);
                _viewerHost.Controls.Add(_canvas.View);
                _comparisonSplit.Dispose();
                _comparisonSplit = null;
            }
        }

        private void RenderComparisonImages()
        {
            if (_canvas == null || _comparisonCanvas == null || !_frames.HasOriginal || !_frames.HasResult)
            {
                return;
            }

            RenderSlot(ImageSlotKind.Original, _canvas);
            RenderSlot(ImageSlotKind.Result, _comparisonCanvas);
            _canvas.FitToWindow();
            _comparisonCanvas.FitToWindow();
            RebuildOverlays();
            UpdateExternalImageRulers();
        }

        private void RenderSlot(ImageSlotKind slot, HalconImageCanvas target)
        {
            ConfigureCanvasCoordinateTransform(target, slot == ImageSlotKind.Original ? Document.Original : Document.Result);
            using (var source = _frames.GetClone(slot))
            using (var presentation = _presentationProcessor.CreatePresentation(
                source,
                TransformState,
                DisplayState,
                CreatePresentationCacheKey("comparison:" + slot)))
            {
                target.Load(presentation);
            }
        }

        private string CreatePresentationCacheKey(string frameKey)
        {
            return string.Format(
                "{0}|{1}|{2:R}|{3}|{4}|{5}|{6:R}|{7:R}|{8:R}",
                Document.SourceVersion,
                frameKey,
                TransformState.RotationDegrees,
                TransformState.IsFlippedHorizontally,
                TransformState.IsFlippedVertically,
                DisplayState.IsAutoContrast,
                DisplayState.Gamma,
                DisplayState.Brightness,
                DisplayState.Contrast) + "|" + DisplayState.IsInverted;
        }

        private void ConfigureCanvasCoordinateTransform(HalconImageCanvas canvas, ImageSlot slot)
        {
            if (canvas != null && slot != null)
            {
                canvas.SetCoordinateTransform(TransformState, slot.Size);
                canvas.SetOverlayColors(OverlayColors);
                // 标尺改为图像区外侧的普通 WinForms 控件；HALCON 图窗内不再叠画标尺。
                canvas.SetImageRulerVisible(false, null, null);
            }
        }

        private void RegisterActionPanel(Control panel)
        {
            if (panel == null)
            {
                return;
            }

            BindActionButtonsRecursive(panel);
        }

        private void BindActionButtonsRecursive(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                var button = control as Button;
                if (button != null)
                {
                    BindActionButton(button);
                }

                if (control.HasChildren)
                {
                    BindActionButtonsRecursive(control);
                }
            }
        }

        private void BindActionButton(Button button)
        {
            if (button == null || _boundActionButtons.Contains(button))
            {
                return;
            }

            _boundActionButtons.Add(button);

            var action = button.Text ?? string.Empty;
            var tagText = button.Tag == null ? string.Empty : Convert.ToString(button.Tag);
            var kind = string.Equals(tagText, "ExclusiveMode", StringComparison.OrdinalIgnoreCase)
                ? ToolActionKind.ExclusiveMode
                : ToolActionKind.Command;

            if (kind == ToolActionKind.ExclusiveMode && !_toolModeButtons.ContainsKey(action))
            {
                _toolModeButtons.Add(action, button);
            }

            button.Click += delegate
            {
                if (kind == ToolActionKind.ExclusiveMode)
                {
                    UpdateToolButtonSelection(action);
                }

                OnToolActionRequested(button, new ToolActionEventArgs(action, kind));
            };
        }

        private void UpdateToolButtonSelection(string selectedAction)
        {
            foreach (var item in _toolModeButtons)
            {
                var selected = string.Equals(item.Key, selectedAction, StringComparison.Ordinal);
                var button = item.Value;
                button.BackColor = selected ? Color.FromArgb(234, 243, 255) : Color.White;
                button.ForeColor = selected ? Color.FromArgb(45, 127, 230) : Color.FromArgb(42, 50, 64);
                button.FlatAppearance.BorderColor = selected
                    ? Color.FromArgb(112, 170, 235)
                    : Color.FromArgb(214, 222, 232);
            }
        }

        private void UpdateToolbarToolSelection(ImageToolMode mode)
        {
            // ToolStrip 仅呈现全局编辑模式；其它绘制工具不占用这组按钮的选中状态。
            _editGeometryToolButton.Checked = mode == ImageToolMode.ResizeRoi;
            _moveObjectToolButton.Checked = mode == ImageToolMode.MoveRoi;
            _deleteObjectToolButton.Checked = mode == ImageToolMode.DeleteOverlay;
            _navigateToolButton.Checked = mode == ImageToolMode.Navigate;
        }

        private void SetDisplayControls(
            int levelMinimum,
            int levelMaximum,
            int widthMinimum,
            int widthMaximum,
            int window,
            int level,
            bool applied)
        {
            _updatingDisplayControls = true;
            try
            {
                ConfigureWindowLevelRange(levelMinimum, levelMaximum, widthMinimum, widthMaximum);
                SetNumericValue(_windowValueEditor, window);
                SetNumericValue(_levelValueEditor, level);
                _windowTrackBar.Value = Clamp(window, _windowTrackBar.Minimum, _windowTrackBar.Maximum);
                _levelTrackBar.Value = Clamp(level, _levelTrackBar.Minimum, _levelTrackBar.Maximum);
                _applyWindowLevelCheckBox.Checked = applied;
            }
            finally
            {
                _updatingDisplayControls = false;
            }

            UpdateWindowLevelLabels();
            UpdateWindowLevelControls();
        }

        private bool ApplyWindowLevelFromControls()
        {
            if (_updatingDisplayControls)
            {
                return false;
            }

            UpdateWindowLevelLabels();
            UpdateWindowLevelControls();
            if (_canvas == null || !Document.HasImage)
            {
                return false;
            }

            if (!_applyWindowLevelCheckBox.Checked)
            {
                if (_autoWindowLevelRoiCheckBox.Checked)
                {
                    _autoWindowLevelRoiCheckBox.Checked = false;
                }

                DisplayState.SetAutoWindowLevel(false);
                _canvas.ResetDisplay();
                return true;
            }

            var width = Decimal.ToInt32(_windowValueEditor.Value);
            var level = Decimal.ToInt32(_levelValueEditor.Value);
            DisplayState.SetWindowLevel(level, width);
            if (!_canvas.SetWindowLevel(width, level))
            {
                SetOperationStatus("当前图像不是 16 位，窗宽窗位不适用");
                return false;
            }

            return true;
        }

        private bool RefreshWindowLevelFromRectangle(ImageRectangle? rectangle, string successMessage)
        {
            if (_canvas == null || !Document.HasImage)
            {
                SetOperationStatus("刷新窗值失败：当前没有打开的图像");
                return false;
            }

            if (!_canvas.SupportsWindowLevel)
            {
                SetOperationStatus("当前图像不是 16 位，窗宽窗位不适用");
                return false;
            }

            try
            {
                var statistics = _statisticsService.Calculate(GetActivePixelSource(), rectangle);
                var minimum = (int)Math.Floor(statistics.Min);
                var maximum = (int)Math.Ceiling(statistics.Max);
                var width = Math.Max(1, maximum - minimum);
                var level = minimum + width / 2;
                SetDisplayControls(minimum, maximum, 1, Math.Max(1, width), width, level, true);
                if (!ApplyWindowLevelFromControls())
                {
                    return false;
                }

                // 刷新/框选得到的是自动计算的窗值，复位时应保留这种显示语义。
                DisplayState.SetAutoWindowLevel(true);
                SetOperationStatus(successMessage);
                return true;
            }
            catch (Exception exception)
            {
                SetOperationStatus("刷新窗值失败：" + exception.Message);
                return false;
            }
        }

        private void ToggleAutoWindowLevelRoiSelection()
        {
            if (_updatingDisplayControls)
            {
                return;
            }

            if (!_autoWindowLevelRoiCheckBox.Checked)
            {
                if (_canvas != null)
                {
                    _canvas.SetRectangleSelectionMode(null);
                }

                SetToolMode(ImageToolMode.Navigate);
                return;
            }

            if (_canvas == null || !Document.HasImage || !_canvas.SupportsWindowLevel)
            {
                _updatingDisplayControls = true;
                _autoWindowLevelRoiCheckBox.Checked = false;
                _updatingDisplayControls = false;
                SetOperationStatus("框选自动取窗不可用：请先打开 16 位图像");
                return;
            }

            if (!_applyWindowLevelCheckBox.Checked)
            {
                _applyWindowLevelCheckBox.Checked = true;
            }

            SetToolMode(ImageToolMode.RectangleRoi);
            if (!_canvas.SetRectangleSelectionMode(bounds =>
                RefreshWindowLevelFromRectangle(bounds, "已按框选区域刷新窗宽窗位")))
            {
                _updatingDisplayControls = true;
                _autoWindowLevelRoiCheckBox.Checked = false;
                _updatingDisplayControls = false;
                SetOperationStatus("框选自动取窗不可用：当前图像未就绪");
                return;
            }

            SetOperationStatus("框选自动取窗：请在图像上拖拽矩形；框选不会生成 ROI");
        }

        private void SynchronizeWindowWidthFromEditor()
        {
            if (_updatingDisplayControls)
            {
                return;
            }

            _updatingDisplayControls = true;
            _windowTrackBar.Value = Clamp(Decimal.ToInt32(_windowValueEditor.Value), _windowTrackBar.Minimum, _windowTrackBar.Maximum);
            _updatingDisplayControls = false;
            ApplyWindowLevelFromControls();
        }

        private void SynchronizeWindowWidthFromTrackBar()
        {
            if (_updatingDisplayControls)
            {
                return;
            }

            _updatingDisplayControls = true;
            SetNumericValue(_windowValueEditor, _windowTrackBar.Value);
            _updatingDisplayControls = false;
            ApplyWindowLevelFromControls();
        }

        private void SynchronizeWindowLevelFromEditor()
        {
            if (_updatingDisplayControls)
            {
                return;
            }

            _updatingDisplayControls = true;
            _levelTrackBar.Value = Clamp(Decimal.ToInt32(_levelValueEditor.Value), _levelTrackBar.Minimum, _levelTrackBar.Maximum);
            _updatingDisplayControls = false;
            ApplyWindowLevelFromControls();
        }

        private void SynchronizeWindowLevelFromTrackBar()
        {
            if (_updatingDisplayControls)
            {
                return;
            }

            _updatingDisplayControls = true;
            SetNumericValue(_levelValueEditor, _levelTrackBar.Value);
            _updatingDisplayControls = false;
            ApplyWindowLevelFromControls();
        }

        private void ConfigureWindowLevelRange(int levelMinimum, int levelMaximum, int widthMinimum, int widthMaximum)
        {
            levelMaximum = Math.Max(levelMinimum, levelMaximum);
            widthMinimum = Math.Max(1, widthMinimum);
            widthMaximum = Math.Max(widthMinimum, widthMaximum);
            ConfigureTrackBarRange(_levelTrackBar, levelMinimum, levelMaximum);
            ConfigureTrackBarRange(_windowTrackBar, widthMinimum, widthMaximum);
            ConfigureNumericRange(_levelValueEditor, levelMinimum, levelMaximum);
            ConfigureNumericRange(_windowValueEditor, widthMinimum, widthMaximum);
        }

        private void UpdateWindowLevelLabels()
        {
            _windowMinimumLabel.Text = _windowTrackBar.Minimum.ToString();
            _windowMaximumLabel.Text = _windowTrackBar.Maximum.ToString();
            _levelMinimumLabel.Text = _levelTrackBar.Minimum.ToString();
            _levelMaximumLabel.Text = _levelTrackBar.Maximum.ToString();
        }

        private void UpdateWindowLevelControls()
        {
            var available = _canvas != null && Document.HasImage && _canvas.SupportsWindowLevel;
            _applyWindowLevelCheckBox.Enabled = available;
            var canEdit = available && _applyWindowLevelCheckBox.Checked;
            _windowValueEditor.Enabled = canEdit;
            _windowTrackBar.Enabled = canEdit;
            _levelValueEditor.Enabled = canEdit;
            _levelTrackBar.Enabled = canEdit;
            _autoWindowLevelRoiCheckBox.Enabled = available;
            _refreshWindowLevelButton.Enabled = available;
        }

        private static void ConfigureTrackBarRange(TrackBar trackBar, int minimum, int maximum)
        {
            trackBar.Minimum = Math.Min(trackBar.Minimum, minimum);
            trackBar.Maximum = Math.Max(trackBar.Maximum, maximum);
            trackBar.Value = Clamp(trackBar.Value, minimum, maximum);
            trackBar.Minimum = minimum;
            trackBar.Maximum = maximum;
        }

        private static void ConfigureNumericRange(NumericUpDown editor, int minimum, int maximum)
        {
            editor.Minimum = Math.Min(editor.Minimum, minimum);
            editor.Maximum = Math.Max(editor.Maximum, maximum);
            SetNumericValue(editor, Clamp(Decimal.ToInt32(editor.Value), minimum, maximum));
            editor.Minimum = minimum;
            editor.Maximum = maximum;
        }

        private static void SetNumericValue(NumericUpDown editor, int value)
        {
            editor.Value = Math.Max(editor.Minimum, Math.Min(editor.Maximum, value));
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private void UpdateDisplayEffectLabels()
        {
            _gammaValueLabel.Text = string.Format("Gamma {0:0.00}", _gammaTrackBar.Value / 100.0);
            _brightnessValueLabel.Text = string.Format("亮度 {0}", _brightnessTrackBar.Value);
            _contrastValueLabel.Text = string.Format("对比度 {0:0.00}", _contrastTrackBar.Value / 100.0);
        }

        private void ResetDisplayEffectControls()
        {
            _autoContrastCheckBox.Checked = false;
            _invertCheckBox.Checked = false;
            _gammaTrackBar.Value = 100;
            _brightnessTrackBar.Value = 0;
            _contrastTrackBar.Value = 100;
            UpdateDisplayEffectLabels();
        }

        private void OnToolActionRequested(object sender, ToolActionEventArgs e)
        {
            if (e.Action == "清空 ROI")
            {
                var roiIds = GetRectangleIds(ImageOverlayLayer.Roi);
                Overlays.Clear(ImageOverlayLayer.Roi);
                if (_canvas != null)
                {
                    _canvas.ClearRectangleRois(roiIds);
                }
                return;
            }

            if (e.Action == "清空标注")
            {
                ClearAnnotations();
                return;
            }

            if (e.Action == "清除测量")
            {
                ClearMeasurements();
                return;
            }

            if (e.Action == "保存图像")
            {
                SaveImageFromPanel();
                return;
            }

            if (e.Action == "复制到剪贴板")
            {
                SetOperationStatus(CopyCurrentViewToClipboard()
                    ? "已复制完整图像到剪贴板"
                    : "复制失败：尚未加载图像");
                return;
            }

            if (e.Action == "原图/结果")
            {
                try
                {
                    ToggleImage();
                }
                catch (Exception exception)
                {
                    SetOperationStatus(exception.Message);
                }
                return;
            }

            if (e.Action == "双图同步")
            {
                try
                {
                    SetSyncView(!ComparisonState.IsSyncViewEnabled);
                }
                catch (Exception exception)
                {
                    SetOperationStatus(exception.Message);
                }
                return;
            }

            if (e.Action == "图像融合")
            {
                try
                {
                    BlendImages(ComparisonState.BlendRatio);
                }
                catch (Exception exception)
                {
                    SetOperationStatus(exception.Message);
                }
                return;
            }

            if (e.Action == "Min/Max/Mean")
            {
                SelectOperationPage(_pixelAnalysisPage, _pixelAnalysisTabButton);
                RefreshAnalysisAsync(true);
                return;
            }

            if (e.Action == "直方图")
            {
                SelectOperationPage(_pixelAnalysisPage, _pixelAnalysisTabButton);
                RefreshAnalysisAsync(true);
                return;
            }

            if (e.Kind != ToolActionKind.ExclusiveMode)
            {
                // 命令不改变当前图窗模式，避免保存或统计后意外退出 ROI/测量。
                SetOperationStatus(string.Format("命令: {0}", e.Action));
                return;
            }

            // 从功能面板切到任意常规绘制/编辑工具时，退出“框选自动取窗”临时模式。
            if (_autoWindowLevelRoiCheckBox.Checked)
            {
                _autoWindowLevelRoiCheckBox.Checked = false;
            }

            SetToolMode(GetToolMode(e.Action));
            SetOperationStatus(GetToolInstruction(ToolMode, 0));
        }

        private static ImageToolMode GetToolMode(string action)
        {
            switch (action)
            {
                case "矩形 ROI": return ImageToolMode.RectangleRoi;
                case "移动对象": return ImageToolMode.MoveRoi;
                case "编辑几何": return ImageToolMode.ResizeRoi;
                case "删除对象": return ImageToolMode.DeleteOverlay;
                case "距离": return ImageToolMode.MeasureDistance;
                case "水平距离": return ImageToolMode.MeasureHorizontal;
                case "垂直距离": return ImageToolMode.MeasureVertical;
                case "角度": return ImageToolMode.MeasureAngle;
                case "直径": return ImageToolMode.MeasureDiameter;
                case "面积": return ImageToolMode.MeasureArea;
                case "灰度剖面": return ImageToolMode.MeasureLineProfile;
                case "点标注": return ImageToolMode.AddPointAnnotation;
                case "矩形标注": return ImageToolMode.AddRectangleAnnotation;
                case "多边形标注": return ImageToolMode.AddPolygonAnnotation;
                case "文字标注": return ImageToolMode.AddTextAnnotation;
                case "箭头": return ImageToolMode.AddArrowAnnotation;
                case "缺陷框": return ImageToolMode.AddDefectBox;
                default: return ImageToolMode.Navigate;
            }
        }

        private static bool IsMeasurementToolMode(ImageToolMode mode)
        {
            return mode == ImageToolMode.MeasureDistance ||
                   mode == ImageToolMode.MeasureHorizontal ||
                   mode == ImageToolMode.MeasureVertical ||
                   mode == ImageToolMode.MeasureAngle ||
                   mode == ImageToolMode.MeasureDiameter ||
                   mode == ImageToolMode.MeasureArea ||
                   mode == ImageToolMode.MeasureLineProfile;
        }

        private void SetToolMode(ImageToolMode mode)
        {
            // 切换工具时丢弃尚未按 Enter 确认的临时标注，避免残留半成品对象。
            CancelPendingAnnotationTextInput(false);
            if (mode != ImageToolMode.RectangleRoi && _autoWindowLevelRoiCheckBox.Checked)
            {
                _autoWindowLevelRoiCheckBox.Checked = false;
            }

            if (!TransformState.PreservesAxisAlignedGeometry && RequiresAxisAlignedRectangle(mode))
            {
                ToolMode = ImageToolMode.Navigate;
                UpdateToolButtonSelection(string.Empty);
                UpdateToolbarToolSelection(ImageToolMode.Navigate);
                if (_canvas != null)
                {
                    _canvas.SetToolMode(ImageToolMode.Navigate);
                }

                SetOperationStatus("任意角度旋转下不能绘制矩形 ROI；请先重置旋转或使用点、线、多边形工具。");
                return;
            }

            ToolMode = mode;
            var selectedAction = GetToolAction(mode);
            UpdateToolButtonSelection(selectedAction);
            UpdateToolbarToolSelection(mode);

            if (_canvas != null)
            {
                if (mode == ImageToolMode.AddTextAnnotation)
                {
                    // 旧图窗会即时写出临时文字；真正内容只在画布输入框回车后写入模型。
                    _canvas.SetAnnotationText(string.Empty);
                }
                _canvas.SetToolMode(mode);
            }
        }

        private static bool RequiresAxisAlignedRectangle(ImageToolMode mode)
        {
            return mode == ImageToolMode.RectangleRoi ||
                   mode == ImageToolMode.MeasureArea ||
                   mode == ImageToolMode.AddRectangleAnnotation ||
                   mode == ImageToolMode.AddDefectBox;
        }

        private void EnsureToolModeCompatibleWithTransform()
        {
            if (!TransformState.PreservesAxisAlignedGeometry && RequiresAxisAlignedRectangle(ToolMode))
            {
                SetToolMode(ImageToolMode.Navigate);
            }
        }

        private static string GetToolAction(ImageToolMode mode)
        {
            switch (mode)
            {
                case ImageToolMode.Navigate: return string.Empty;
                case ImageToolMode.RectangleRoi: return "矩形 ROI";
                // 枚举沿用历史名称以保持外部兼容，实际动作已统一覆盖 ROI、测量和标注。
                case ImageToolMode.MoveRoi: return "移动对象";
                case ImageToolMode.ResizeRoi: return "编辑几何";
                case ImageToolMode.DeleteOverlay: return "删除对象";
                case ImageToolMode.MeasureDistance: return "距离";
                case ImageToolMode.MeasureHorizontal: return "水平距离";
                case ImageToolMode.MeasureVertical: return "垂直距离";
                case ImageToolMode.MeasureAngle: return "角度";
                case ImageToolMode.MeasureDiameter: return "直径";
                case ImageToolMode.MeasureArea: return "面积";
                case ImageToolMode.MeasureLineProfile: return "灰度剖面";
                case ImageToolMode.AddPointAnnotation: return "点标注";
                case ImageToolMode.AddRectangleAnnotation: return "矩形标注";
                case ImageToolMode.AddPolygonAnnotation: return "多边形标注";
                case ImageToolMode.AddTextAnnotation: return "文字标注";
                case ImageToolMode.AddArrowAnnotation: return "箭头";
                case ImageToolMode.AddDefectBox: return "缺陷框";
                default: return string.Empty;
            }
        }

        private static int GetDisplayMaximum(int bitDepth)
        {
            return bitDepth <= 8 ? 255 : 16383;
        }

        private void OnPixelInfoChanged(object sender, PixelInfoChangedEventArgs e)
        {
            if (e.IsInsideImage && _activePixelSource != null)
            {
                var gray = GetPixelValue(e.SourcePoint);
                var color = GetPixelColor(e.SourcePoint);
                _pixelStatus.Text = color == null
                    ? string.Format("X/Y: {0:0}/{1:0}  Gray: {2:0.##}", e.SourcePoint.X, e.SourcePoint.Y, gray)
                    : string.Format(
                        "X/Y: {0:0}/{1:0}  RGB: {2:0.##}/{3:0.##}/{4:0.##}",
                        e.SourcePoint.X,
                        e.SourcePoint.Y,
                        color.Red,
                        color.Green,
                        color.Blue);
            }
            else
            {
                _pixelStatus.Text = "X/Y: --  Gray: --";
            }

            var handler = PixelInfoChanged;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        private void OnNavigationChanged(object sender, ImageCanvasNavigationEventArgs e)
        {
            if (ComparisonState.IsSyncViewEnabled && !_synchronizingViews && _comparisonCanvas != null)
            {
                var source = sender as HalconImageCanvas;
                var target = source == _canvas ? _comparisonCanvas : _canvas;
                _synchronizingViews = true;
                try
                {
                    target.ApplyNavigation(e);
                }
                finally
                {
                    _synchronizingViews = false;
                }
            }

            if (e.Kind == ImageCanvasNavigationKind.Zoom)
            {
                ViewportState.SetZoom(ViewportState.Zoom * e.ZoomFactor);
                _zoomStatus.Text = string.Format("Zoom: {0:P0}", ViewportState.Zoom);
                UpdateExternalImageRulers();
                return;
            }

            ViewportState.Pan(e.DeltaColumn, e.DeltaRow);
            UpdateExternalImageRulers();
        }

        private void OnRectangleRoiDrawn(object sender, RectangleRoiDrawnEventArgs e)
        {
            if (e.Mode == ImageToolMode.MeasureArea)
            {
                AddAreaMeasurement(e);
                return;
            }

            if (e.Mode == ImageToolMode.AddRectangleAnnotation)
            {
                BeginAnnotationTextInput(new ImageOverlayItem(ImageOverlayKind.RectangleAnnotation, ImageOverlayLayer.Annotation)
                {
                    Bounds = e.Bounds
                });
                return;
            }

            if (e.Mode == ImageToolMode.AddDefectBox)
            {
                AddRectangleOverlay(ImageOverlayKind.DefectBox, ImageOverlayLayer.Defect, e.Bounds, "缺陷框");
                return;
            }

            var roi = new ImageOverlayItem(ImageOverlayKind.RectangleRoi, ImageOverlayLayer.Roi)
            {
                Bounds = e.Bounds,
                ExternalId = e.Id.ToString("N"),
                // ROI 在图上只显示核心几何尺寸；灰度可由状态栏的鼠标像素值查看，避免叠加层拥挤。
                Text = string.Format("ROI: {0:0} × {1:0} px", e.Bounds.Width, e.Bounds.Height)
            };
            Overlays.Add(roi);
            RebuildOverlays();
            SetOperationStatus(string.Format("矩形 ROI: {0:0} × {1:0}", e.Bounds.Width, e.Bounds.Height));
        }

        private void OnAnnotationDrawn(object sender, AnnotationDrawnEventArgs e)
        {
            var item = new ImageOverlayItem(GetAnnotationKind(e.Mode), ImageOverlayLayer.Annotation)
            {
                Bounds = e.Bounds,
                // 旧图窗可能为文字工具产生默认文本，但未回车前不能视为用户确认。
                Text = null
            };
            foreach (var point in e.Points)
            {
                item.Points.Add(point);
            }

            BeginAnnotationTextInput(item);
        }

        private void AddRectangleOverlay(ImageOverlayKind kind, ImageOverlayLayer layer, ImageRectangle bounds, string text)
        {
            Overlays.Add(new ImageOverlayItem(kind, layer)
            {
                Bounds = bounds,
                Text = text
            });
            RebuildOverlays();
            SetOperationStatus(string.Format("已添加{0}", text));
        }

        private void AddAreaMeasurement(RectangleRoiDrawnEventArgs e)
        {
            var pixelArea = e.Bounds.Width * e.Bounds.Height;
            var physicalArea = Calibration.IsCalibrated
                ? pixelArea * Calibration.MillimetersPerPixelX.Value * Calibration.MillimetersPerPixelY.Value
                : (double?)null;
            var item = new ImageOverlayItem(ImageOverlayKind.AreaMeasurement, ImageOverlayLayer.Measurement)
            {
                Bounds = e.Bounds,
                ExternalId = e.Id.ToString("N"),
                Text = physicalArea.HasValue
                    ? string.Format("面积: {0:0.##} px² / {1:0.###} mm²", pixelArea, physicalArea.Value)
                    : string.Format("面积: {0:0.##} px²（未标定）", pixelArea)
            };
            Overlays.Add(item);
            RebuildOverlays();
            SetOperationStatus(item.Text);
        }

        /// <summary>
        /// “显示标尺”是纯显示选项；它读取当前标定值，不参与 ROI、测量和标注数据。
        /// </summary>
        private void UpdateImageRulerVisibility()
        {
            var visible = _showImageRulerCheckBox.Checked;
            _topPixelRuler.Visible = visible;
            _leftPixelRuler.Visible = visible;
            _bottomMillimeterRuler.Visible = visible;
            _rightMillimeterRuler.Visible = visible;

            // 关闭时同时收起外围行列，让图像宿主恢复完整可用区域。
            _viewerRulerLayout.ColumnStyles[0].Width = visible ? 22F : 0F;
            _viewerRulerLayout.ColumnStyles[2].Width = visible ? 22F : 0F;
            _viewerRulerLayout.RowStyles[0].Height = visible ? 22F : 0F;
            _viewerRulerLayout.RowStyles[2].Height = visible ? 22F : 0F;
            _viewerRulerLayout.PerformLayout();

            UpdateImageRulerVisibility(_canvas);
            UpdateImageRulerVisibility(_comparisonCanvas);
            UpdateExternalImageRulers();
        }

        private void UpdateImageRulerVisibility(HalconImageCanvas canvas)
        {
            if (canvas == null)
            {
                return;
            }

            canvas.SetImageRulerVisible(false, null, null);
        }

        private void UpdateExternalImageRulers()
        {
            if (!_showImageRulerCheckBox.Checked || _canvas == null)
            {
                return;
            }

            ImageRectangle bounds;
            if (!_canvas.TryGetVisibleDisplayBounds(out bounds))
            {
                return;
            }

            var horizontalSpacing = Calibration.MillimetersPerPixelX;
            var verticalSpacing = Calibration.MillimetersPerPixelY;
            if (IsRulerAxisSwapped())
            {
                var temporary = horizontalSpacing;
                horizontalSpacing = verticalSpacing;
                verticalSpacing = temporary;
            }

            // 任意角度旋转后的显示坐标不再等同 X/Y 物理轴，保留 px 标尺并明确不给出误导性的 mm 数值。
            if (!TransformState.PreservesAxisAlignedGeometry)
            {
                horizontalSpacing = null;
                verticalSpacing = null;
            }

            _topPixelRuler.SetViewport(bounds.Left, bounds.Right, false, null);
            _leftPixelRuler.SetViewport(bounds.Top, bounds.Bottom, false, null);
            _bottomMillimeterRuler.SetViewport(bounds.Left, bounds.Right, true, horizontalSpacing);
            _rightMillimeterRuler.SetViewport(bounds.Top, bounds.Bottom, true, verticalSpacing);
        }

        private bool IsRulerAxisSwapped()
        {
            var rotation = ((TransformState.RotationDegrees % 360) + 360) % 360;
            return Math.Abs(rotation - 90) < 0.001 || Math.Abs(rotation - 270) < 0.001;
        }

        /// <summary>
        /// 标注几何先以临时对象显示；只有用户在画布对应位置按 Enter 后才写入文字并保留该对象。
        /// </summary>
        private void BeginAnnotationTextInput(ImageOverlayItem item)
        {
            if (item == null)
            {
                return;
            }

            CancelPendingAnnotationTextInput(false);
            Overlays.Add(item);
            RebuildOverlays();
            _pendingAnnotationId = item.Id;

            Point clientPoint;
            if (_canvas == null || !_canvas.TryGetClientPoint(GetAnnotationInputAnchor(item), out clientPoint))
            {
                CancelPendingAnnotationTextInput(false);
                SetOperationStatus("标注输入失败：无法定位图像位置");
                return;
            }

            _annotationInputBox.Text = AnnotationText;
            _annotationInputBox.Location = ClampAnnotationInputLocation(clientPoint);
            _annotationInputBox.Visible = true;
            _annotationInputBox.BringToFront();
            _annotationInputBox.Focus();
            _annotationInputBox.SelectAll();
            SetOperationStatus("请输入标注文字，按 Enter 确认；Esc 取消");
        }

        private void OnAnnotationInputBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                CommitPendingAnnotationTextInput();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                CancelPendingAnnotationTextInput(true);
            }
        }

        private void CommitPendingAnnotationTextInput()
        {
            if (!_pendingAnnotationId.HasValue)
            {
                return;
            }

            var text = _annotationInputBox.Text.Trim();
            if (text.Length == 0)
            {
                SetOperationStatus("标注文字不能为空；请输入文字后按 Enter 确认");
                _annotationInputBox.Focus();
                return;
            }

            var item = Overlays.Find(_pendingAnnotationId.Value);
            if (item == null)
            {
                CancelPendingAnnotationTextInput(false);
                return;
            }

            item.Text = text;
            AnnotationText = text;
            _pendingAnnotationId = null;
            _annotationInputBox.Visible = false;
            RebuildOverlays();
            SetOperationStatus("已添加标注");
        }

        private void CancelPendingAnnotationTextInput(bool showStatus)
        {
            if (!_pendingAnnotationId.HasValue)
            {
                _annotationInputBox.Visible = false;
                return;
            }

            var id = _pendingAnnotationId.Value;
            _pendingAnnotationId = null;
            _annotationInputBox.Visible = false;
            Overlays.Remove(id);
            RebuildOverlays();
            if (showStatus)
            {
                SetOperationStatus("已取消未确认的标注");
            }
        }

        private Point ClampAnnotationInputLocation(Point anchor)
        {
            var maximumX = Math.Max(0, _viewerHost.ClientSize.Width - _annotationInputBox.Width);
            var maximumY = Math.Max(0, _viewerHost.ClientSize.Height - _annotationInputBox.Height);
            return new Point(
                Math.Max(0, Math.Min(maximumX, anchor.X + 6)),
                Math.Max(0, Math.Min(maximumY, anchor.Y + 6)));
        }

        private static ImagePoint GetAnnotationInputAnchor(ImageOverlayItem item)
        {
            if (item.Points.Count > 0 &&
                (item.Kind == ImageOverlayKind.PointAnnotation ||
                 item.Kind == ImageOverlayKind.TextAnnotation ||
                 item.Kind == ImageOverlayKind.ArrowAnnotation))
            {
                return item.Points[0];
            }

            if (item.Bounds.Width > 0 || item.Bounds.Height > 0)
            {
                return new ImagePoint(item.Bounds.Left, item.Bounds.Top);
            }

            if (item.Points.Count > 0)
            {
                var left = item.Points[0].X;
                var top = item.Points[0].Y;
                for (var index = 1; index < item.Points.Count; index++)
                {
                    left = Math.Min(left, item.Points[index].X);
                    top = Math.Min(top, item.Points[index].Y);
                }

                return new ImagePoint(left, top);
            }

            return new ImagePoint(0, 0);
        }

        private void OnMeasurementDrawn(object sender, MeasurementDrawnEventArgs e)
        {
            var kind = GetMeasurementKind(e.Mode);
            var item = new ImageOverlayItem(kind, ImageOverlayLayer.Measurement)
            {
                Text = GetMeasurementText(e)
            };
            foreach (var point in e.Points)
            {
                item.Points.Add(point);
            }

            Overlays.Add(item);
            RebuildOverlays();
            SetOperationStatus(item.Text);
        }

        private void OnMeasurementProgressChanged(object sender, MeasurementProgressChangedEventArgs e)
        {
            SetOperationStatus(GetToolInstruction(e.Mode, e.SelectedPointCount));
        }

        private void OnOverlaySelectionChanged(object sender, OverlaySelectionChangedEventArgs e)
        {
            if (!e.OverlayId.HasValue)
            {
                SetOperationStatus("未选中对象");
                return;
            }

            var item = Overlays.Find(e.OverlayId.Value);
            if (item != null)
            {
                if (ToolMode == ImageToolMode.ResizeRoi && !TransformState.PreservesAxisAlignedGeometry &&
                    (item.Kind == ImageOverlayKind.RectangleRoi || item.Kind == ImageOverlayKind.AreaMeasurement ||
                     item.Kind == ImageOverlayKind.RectangleAnnotation))
                {
                    SetOperationStatus("任意角度旋转下不能修改矩形几何；请先重置旋转后编辑。");
                    return;
                }

                SetOperationStatus(string.Format("已选中{0}：可拖动或编辑控制点，Delete 删除", GetOverlayDisplayName(item)));
            }
        }

        private void OnOverlayGeometryChanged(object sender, OverlayGeometryChangedEventArgs e)
        {
            var item = Overlays.Find(e.OverlayId);
            if (item == null || item.Layer == ImageOverlayLayer.Defect)
            {
                return;
            }

            item.Bounds = e.Bounds;
            item.Points.Clear();
            foreach (var point in e.Points)
            {
                item.Points.Add(point);
            }

            RefreshEditedOverlayText(item);
            RebuildOverlays();
            SetOperationStatus(string.Format("已更新{0}", GetOverlayDisplayName(item)));
        }

        private void OnOverlayDeleteRequested(object sender, OverlayDeleteRequestedEventArgs e)
        {
            var item = Overlays.Find(e.OverlayId);
            if (item == null || item.Layer == ImageOverlayLayer.Defect)
            {
                return;
            }

            Overlays.Remove(item.Id);
            RebuildOverlays();
            SetOperationStatus(string.Format("已删除{0}", GetOverlayDisplayName(item)));
        }

        private void RefreshEditedOverlayText(ImageOverlayItem item)
        {
            if (item.Kind == ImageOverlayKind.AreaMeasurement)
            {
                var pixelArea = item.Bounds.Width * item.Bounds.Height;
                var physicalArea = Calibration.IsCalibrated
                    ? pixelArea * Calibration.MillimetersPerPixelX.Value * Calibration.MillimetersPerPixelY.Value
                    : (double?)null;
                item.Text = physicalArea.HasValue
                    ? string.Format("面积: {0:0.##} px² / {1:0.###} mm²", pixelArea, physicalArea.Value)
                    : string.Format("面积: {0:0.##} px²（未标定）", pixelArea);
                return;
            }

            var mode = GetMeasurementMode(item.Kind);
            if (!mode.HasValue || item.Points.Count < 2)
            {
                return;
            }

            var value = GetEditedMeasurementValue(mode.Value, item.Points);
            item.Text = GetMeasurementText(new MeasurementDrawnEventArgs(mode.Value, item.Points, value));
        }

        private static ImageToolMode? GetMeasurementMode(ImageOverlayKind kind)
        {
            switch (kind)
            {
                case ImageOverlayKind.DistanceMeasurement: return ImageToolMode.MeasureDistance;
                case ImageOverlayKind.HorizontalMeasurement: return ImageToolMode.MeasureHorizontal;
                case ImageOverlayKind.VerticalMeasurement: return ImageToolMode.MeasureVertical;
                case ImageOverlayKind.AngleMeasurement: return ImageToolMode.MeasureAngle;
                case ImageOverlayKind.DiameterMeasurement: return ImageToolMode.MeasureDiameter;
                case ImageOverlayKind.LineProfileMeasurement: return ImageToolMode.MeasureLineProfile;
                default: return null;
            }
        }

        private static double GetEditedMeasurementValue(ImageToolMode mode, IList<ImagePoint> points)
        {
            var first = points[0];
            var second = points[1];
            if (mode == ImageToolMode.MeasureHorizontal)
            {
                return Math.Abs(second.X - first.X);
            }

            if (mode == ImageToolMode.MeasureVertical)
            {
                return Math.Abs(second.Y - first.Y);
            }

            if (mode == ImageToolMode.MeasureAngle && points.Count >= 3)
            {
                var leftX = first.X - second.X;
                var leftY = first.Y - second.Y;
                var rightX = points[2].X - second.X;
                var rightY = points[2].Y - second.Y;
                var leftLength = Math.Sqrt(leftX * leftX + leftY * leftY);
                var rightLength = Math.Sqrt(rightX * rightX + rightY * rightY);
                if (leftLength <= 0 || rightLength <= 0)
                {
                    return 0;
                }

                var cosine = (leftX * rightX + leftY * rightY) / (leftLength * rightLength);
                return Math.Acos(Math.Max(-1, Math.Min(1, cosine))) * 180.0 / Math.PI;
            }

            var deltaX = second.X - first.X;
            var deltaY = second.Y - first.Y;
            return Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        }

        private static string GetOverlayDisplayName(ImageOverlayItem item)
        {
            switch (item.Kind)
            {
                case ImageOverlayKind.RectangleRoi: return "矩形 ROI";
                case ImageOverlayKind.AreaMeasurement: return "面积测量";
                case ImageOverlayKind.DistanceMeasurement: return "距离测量";
                case ImageOverlayKind.HorizontalMeasurement: return "水平距离测量";
                case ImageOverlayKind.VerticalMeasurement: return "垂直距离测量";
                case ImageOverlayKind.AngleMeasurement: return "角度测量";
                case ImageOverlayKind.DiameterMeasurement: return "直径测量";
                case ImageOverlayKind.LineProfileMeasurement: return "灰度剖面";
                case ImageOverlayKind.PointAnnotation: return "点标注";
                case ImageOverlayKind.RectangleAnnotation: return "矩形标注";
                case ImageOverlayKind.PolygonAnnotation: return "多边形标注";
                case ImageOverlayKind.TextAnnotation: return "文字标注";
                case ImageOverlayKind.ArrowAnnotation: return "箭头标注";
                default: return "图像对象";
            }
        }

        private static string GetToolInstruction(ImageToolMode mode, int selectedPointCount)
        {
            switch (mode)
            {
                case ImageToolMode.Navigate:
                    return "浏览模式：可拖动图像，滚轮缩放；Ctrl+R 可随时退出绘制或编辑";
                case ImageToolMode.MoveRoi:
                    return "移动对象：单击对象后直接拖动；支持 ROI、测量和标注，检测结果不可编辑";
                case ImageToolMode.ResizeRoi:
                    return "编辑几何：先单击对象显示控制点，再拖动角点或端点；Esc 取消本次编辑";
                case ImageToolMode.DeleteOverlay:
                    return "删除对象：单击 ROI、测量或标注删除；也可先选中对象后按 Delete";
                case ImageToolMode.MeasureDistance:
                    return selectedPointCount == 0
                        ? "距离测量：单击起点，再移动鼠标预览并单击终点；Esc 取消本次测量"
                        : "距离测量：已选起点，移动鼠标查看虚线预览后单击终点；Esc 取消";
                case ImageToolMode.MeasureHorizontal:
                    return selectedPointCount == 0
                        ? "水平距离：单击起点，再单击终点；过程会锁定为水平线，Esc 取消"
                        : "水平距离：已选起点，移动鼠标查看预览后单击终点；Esc 取消";
                case ImageToolMode.MeasureVertical:
                    return selectedPointCount == 0
                        ? "垂直距离：单击起点，再单击终点；过程会锁定为垂直线，Esc 取消"
                        : "垂直距离：已选起点，移动鼠标查看预览后单击终点；Esc 取消";
                case ImageToolMode.MeasureAngle:
                    if (selectedPointCount == 0)
                    {
                        return "角度测量：依次单击第一点、顶点、第三点；Esc 取消本次测量";
                    }

                    return selectedPointCount == 1
                        ? "角度测量：已选第一点，请单击顶点；Esc 取消"
                        : "角度测量：已选顶点，移动鼠标查看实时角度后单击第三点；Esc 取消";
                case ImageToolMode.MeasureDiameter:
                    return selectedPointCount == 0
                        ? "直径测量：单击直径两端，鼠标移动时显示当前直径；Esc 取消"
                        : "直径测量：已选第一端，移动鼠标查看预览后单击第二端；Esc 取消";
                case ImageToolMode.MeasureLineProfile:
                    return selectedPointCount == 0
                        ? "灰度剖面：单击起点和终点，完成后自动更新曲线；Esc 取消"
                        : "灰度剖面：已选起点，移动鼠标查看剖面线后单击终点；Esc 取消";
                default:
                    return string.IsNullOrWhiteSpace(GetToolAction(mode))
                        ? "导航：拖动查看图像"
                        : string.Format("工具: {0}", GetToolAction(mode));
            }
        }

        private void RefreshRectangleRoi(ImageOverlayItem item)
        {
            if (_canvas == null || string.IsNullOrWhiteSpace(item.ExternalId))
            {
                return;
            }

            if (!item.IsVisible || !Overlays.IsLayerVisible(item.Layer))
            {
                RebuildOverlays();
                return;
            }

            _canvas.UpdateRectangleRoi(Guid.Parse(item.ExternalId), item.Bounds, item.Text ?? string.Empty);
        }

        private void RebuildOverlays()
        {
            if (_canvas != null)
            {
                var visibleItems = Overlays.GetVisibleItems();
                _canvas.SetEditableOverlays(visibleItems);
                _canvas.RebuildOverlays(visibleItems);
            }
        }

        private void SaveImageFromPanel()
        {
            using (var dialog = new SaveFileDialog
            {
                Filter = "TIFF 图像|*.tif;*.tiff|PNG 图像|*.png|JPEG 图像|*.jpg;*.jpeg|BMP 图像|*.bmp|所有文件|*.*",
                DefaultExt = "png",
                AddExtension = true,
                FileName = "image.png"
            })
            {
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
                {
                    return;
                }

                SetOperationStatus(SaveCurrentImage(dialog.FileName)
                    ? string.Format("图像已保存：{0}", dialog.FileName)
                    : "保存失败：尚未加载图像或目标路径不可写");
            }
        }

        /// <summary>
        /// 复制当前图像的像素快照后在后台计算。每次新请求都会取消旧请求，
        /// 防止连续点击统计/直方图时多个全图扫描同时占用 CPU 或回写过期数据。
        /// </summary>
        private async void RefreshAnalysisAsync(bool useLastRectangleRoi)
        {
            if (_canvas == null || !Document.HasImage || IsDisposed)
            {
                return;
            }

            var request = Interlocked.Increment(ref _latestAnalysisRequest);
            CancelAnalysis();
            var cancellation = new CancellationTokenSource();
            _analysisCancellation = cancellation;
            var roi = useLastRectangleRoi ? GetLastRectangleRoi() : null;
            HalconPixelSource snapshot = null;
            try
            {
                // HALCON 句柄只在 UI 线程读取；后台任务只接触活动原图的托管像素快照。
                // 不能从画布取图，否则窗宽窗位、反色等显示效果会污染统计结果。
                snapshot = CreateActivePixelSnapshot();
                _statisticsSummaryLabel.Text = "统计与直方图正在计算…";
                SetOperationStatus("正在后台计算直方图与灰度统计…");
                var result = await Task.Run(
                    () => _statisticsService.Analyze(snapshot, roi, 256, cancellation.Token),
                    cancellation.Token);

                if (request != Interlocked.Read(ref _latestAnalysisRequest) || IsDisposed)
                {
                    return;
                }

                ApplyAnalysisResult(result, roi.HasValue);
            }
            catch (OperationCanceledException)
            {
                // 新图或新请求到来时取消旧分析，这是预期分支，不覆盖当前状态栏。
            }
            catch (Exception exception)
            {
                if (request == Interlocked.Read(ref _latestAnalysisRequest) && !IsDisposed)
                {
                    _statisticsSummaryLabel.Text = "统计计算失败";
                    SetOperationStatus(string.Format("统计失败: {0}", exception.Message));
                }
            }
            finally
            {
                if (snapshot != null)
                {
                    snapshot.Dispose();
                }

                if (ReferenceEquals(_analysisCancellation, cancellation))
                {
                    _analysisCancellation = null;
                }

                cancellation.Dispose();
            }
        }

        private void ApplyAnalysisResult(ImageAnalysisResult result, bool basedOnRoi)
        {
            var statistics = result.Statistics;
            var histogram = result.Histogram;
            var pixelCount = 0L;
            foreach (var count in histogram.Bins)
            {
                pixelCount += count;
            }

            _histogramPlot.SetHistogram(histogram);
            _grayLevelScale.Minimum = histogram.Minimum;
            _grayLevelScale.Maximum = histogram.Maximum;
            _grayLevelScale.Invalidate();
            _statisticsSummaryLabel.Text = string.Format(
                "{0}统计：Min {1:0.##}  Max {2:0.##}  Mean {3:0.##}  Std {4:0.##}",
                basedOnRoi ? "ROI " : "整图 ",
                statistics.Min,
                statistics.Max,
                statistics.Mean,
                statistics.StandardDeviation);
            SetOperationStatus(string.Format(
                "直方图已刷新：{0} bins，{1} 像素",
                histogram.Bins.Length,
                pixelCount));
        }

        private void CancelAnalysis()
        {
            if (_analysisCancellation == null)
            {
                return;
            }

            _analysisCancellation.Cancel();
            _analysisCancellation = null;
        }

        private ImageRectangle? GetLastRectangleRoi()
        {
            ImageOverlayItem last = null;
            foreach (var item in Overlays.Items)
            {
                if (item.Kind == ImageOverlayKind.RectangleRoi && item.IsVisible)
                {
                    last = item;
                }
            }

            return last == null ? (ImageRectangle?)null : last.Bounds;
        }

        private void CropToRoiFromPanel()
        {
            try
            {
                CropToLastRectangleRoi();
            }
            catch (Exception exception)
            {
                SetOperationStatus(exception.Message);
            }
        }

        private static CropRectangle GetCropRectangle(ImageRectangle roi, ImageSize imageSize)
        {
            var left = Math.Max(0, Math.Min(imageSize.Width - 1, (int)Math.Floor(roi.Left)));
            var top = Math.Max(0, Math.Min(imageSize.Height - 1, (int)Math.Floor(roi.Top)));
            var right = Math.Max(0, Math.Min(imageSize.Width - 1, (int)Math.Ceiling(roi.Right)));
            var bottom = Math.Max(0, Math.Min(imageSize.Height - 1, (int)Math.Ceiling(roi.Bottom)));
            if (right < left || bottom < top)
            {
                throw new InvalidOperationException("裁剪 ROI 不在当前图像范围内。");
            }

            return new CropRectangle(left, top, right - left + 1, bottom - top + 1);
        }

        private IList<Guid> GetRectangleIds(ImageOverlayLayer layer)
        {
            var ids = new List<Guid>();
            foreach (var item in Overlays.Items)
            {
                Guid id;
                if (item.Layer == layer && !string.IsNullOrWhiteSpace(item.ExternalId) && Guid.TryParse(item.ExternalId, out id))
                {
                    ids.Add(id);
                }
            }

            return ids;
        }

        private static ImageOverlayKind GetMeasurementKind(ImageToolMode mode)
        {
            switch (mode)
            {
                case ImageToolMode.MeasureHorizontal: return ImageOverlayKind.HorizontalMeasurement;
                case ImageToolMode.MeasureVertical: return ImageOverlayKind.VerticalMeasurement;
                case ImageToolMode.MeasureAngle: return ImageOverlayKind.AngleMeasurement;
                case ImageToolMode.MeasureDiameter: return ImageOverlayKind.DiameterMeasurement;
                case ImageToolMode.MeasureLineProfile: return ImageOverlayKind.LineProfileMeasurement;
                default: return ImageOverlayKind.DistanceMeasurement;
            }
        }

        private static ImageOverlayKind GetAnnotationKind(ImageToolMode mode)
        {
            switch (mode)
            {
                case ImageToolMode.AddPointAnnotation: return ImageOverlayKind.PointAnnotation;
                case ImageToolMode.AddPolygonAnnotation: return ImageOverlayKind.PolygonAnnotation;
                case ImageToolMode.AddTextAnnotation: return ImageOverlayKind.TextAnnotation;
                case ImageToolMode.AddArrowAnnotation: return ImageOverlayKind.ArrowAnnotation;
                default: throw new ArgumentOutOfRangeException("mode", "当前工具不是标注工具。");
            }
        }

        private static string GetAnnotationDisplayName(ImageToolMode mode)
        {
            switch (mode)
            {
                case ImageToolMode.AddPointAnnotation: return "点标注";
                case ImageToolMode.AddPolygonAnnotation: return "多边形标注";
                case ImageToolMode.AddTextAnnotation: return "文字标注";
                case ImageToolMode.AddArrowAnnotation: return "箭头标注";
                default: return "标注";
            }
        }

        private string GetMeasurementText(MeasurementDrawnEventArgs e)
        {
            if (e.Mode == ImageToolMode.MeasureLineProfile)
            {
                var values = GetLineProfile(e.Points[0], e.Points[1]);
                _lineProfilePlot.SetValues(values);
                var minimum = double.MaxValue;
                var maximum = double.MinValue;
                var sum = 0.0;
                foreach (var value in values)
                {
                    minimum = Math.Min(minimum, value);
                    maximum = Math.Max(maximum, value);
                    sum += value;
                }

                return string.Format(
                    "灰度剖面: {0} 点  Min {1:0.##}  Max {2:0.##}  Mean {3:0.##}",
                    values.Count,
                    minimum,
                    maximum,
                    sum / values.Count);
            }

            if (e.Mode == ImageToolMode.MeasureAngle)
            {
                return string.Format("角度: {0:0.##}°", e.PixelValue);
            }

            var first = e.Points[0];
            var second = e.Points[1];
            var millimeters = PixelToWorld(second.X - first.X, second.Y - first.Y);
            return millimeters.HasValue
                ? string.Format("测量: {0:0.##} px / {1:0.###} mm", e.PixelValue, millimeters.Value)
                : string.Format("测量: {0:0.##} px（未标定）", e.PixelValue);
        }

        private void CompleteAsyncLoad(long request, string fileName, ImageSlot metadata, Task<HObject> task)
        {
            if (task.IsFaulted)
            {
                var error = task.Exception == null ? null : task.Exception.GetBaseException();
                SetOperationStatus(string.Format("加载失败: {0}", error == null ? "未知错误" : error.Message));
                RaiseImageLoadCompleted(fileName, false, error);
                return;
            }

            var image = task.Result;
            if (request != Interlocked.Read(ref _latestLoadRequest))
            {
                // 新请求已发出，旧图绝不能覆盖当前图；此处由本控件释放后台创建的句柄。
                image.Dispose();
                RaiseImageLoadCompleted(fileName, false, null);
                return;
            }

            try
            {
                LoadImage(image, metadata);
                RememberOpenedImageFile(fileName);
                RaiseImageLoadCompleted(fileName, true, null);
            }
            catch (Exception exception)
            {
                SetOperationStatus(string.Format("加载失败: {0}", exception.Message));
                RaiseImageLoadCompleted(fileName, false, exception);
            }
            finally
            {
                // LoadImage 内部会克隆图像，后台加载对象的生命周期仍由这里结束。
                image.Dispose();
            }
        }

        private static void DisposeLoadedImage(Task<HObject> task)
        {
            if (task.Status == TaskStatus.RanToCompletion && task.Result != null)
            {
                task.Result.Dispose();
            }
        }

        private void RaiseImageLoadCompleted(string fileName, bool applied, Exception error)
        {
            var handler = ImageLoadCompleted;
            if (handler != null)
            {
                handler(this, new ImageLoadCompletedEventArgs(fileName, applied, error));
            }
        }

        private void SetOperationStatus(string text)
        {
            _operationStatus.Text = text;
        }

        private void OnContentSplitLayout(object sender, LayoutEventArgs e)
        {
            if (_contentSplitConfigured || _contentSplit.ClientSize.Width <= 0)
            {
                return;
            }

            const int rightPanelMinimum = 320;
            const int preferredRightPanelWidth = 380;
            var width = _contentSplit.ClientSize.Width;
            if (width <= rightPanelMinimum + _contentSplit.SplitterWidth)
            {
                return;
            }

            // 首次获得有效尺寸后再固定右侧工具区，保证主看图区和小面板均有可用空间。
            // 下列属性会同步触发 Layout；必须先置位，避免构造期递归布局导致窗口无法创建。
            _contentSplitConfigured = true;
            _contentSplit.Panel2MinSize = rightPanelMinimum;
            var maximumSplitterDistance = width - rightPanelMinimum - _contentSplit.SplitterWidth;
            _contentSplit.SplitterDistance = Math.Max(0, Math.Min(width - preferredRightPanelWidth, maximumSplitterDistance));
            _contentSplit.Layout -= OnContentSplitLayout;
        }

        private void OnToolPanelSplitLayout(object sender, LayoutEventArgs e)
        {
            if (_toolPanelSplitConfigured)
            {
                return;
            }

            var split = sender as SplitContainer;
            if (split == null || split.ClientSize.Height <= 0)
            {
                return;
            }

            const int minimumPanelHeight = 150;
            const int preferredDisplayPanelHeight = 510;
            var availableHeight = split.ClientSize.Height;
            if (availableHeight <= minimumPanelHeight * 2 + split.SplitterWidth)
            {
                return;
            }

            // 设置前先置标记，防止 SplitterDistance 触发布局重入。
            _toolPanelSplitConfigured = true;
            split.Panel1MinSize = minimumPanelHeight;
            split.Panel2MinSize = minimumPanelHeight;
            var maximumSplitterDistance = availableHeight - minimumPanelHeight - split.SplitterWidth;
            // 显示区包含窗宽窗位与图像变换两组控件，优先完整显示；空间不足时由显示区滚动。
            split.SplitterDistance = Math.Max(minimumPanelHeight, Math.Min(preferredDisplayPanelHeight, maximumSplitterDistance));
            split.Layout -= OnToolPanelSplitLayout;
        }

        private bool IsInDesignMode()
        {
            return LicenseManager.UsageMode == LicenseUsageMode.Designtime || DesignMode;
        }

        private struct CropRectangle
        {
            public CropRectangle(int left, int top, int width, int height)
            {
                Left = left;
                Top = top;
                Width = width;
                Height = height;
            }

            public int Left { get; private set; }

            public int Top { get; private set; }

            public int Width { get; private set; }

            public int Height { get; private set; }
        }
    }

    public sealed class ImageLoadCompletedEventArgs : EventArgs
    {
        public ImageLoadCompletedEventArgs(string fileName, bool applied, Exception error)
        {
            FileName = fileName;
            Applied = applied;
            Error = error;
        }

        public string FileName { get; private set; }

        public bool Applied { get; private set; }

        public Exception Error { get; private set; }
    }


}
