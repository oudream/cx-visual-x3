using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CxVisual.Foundation.Imaging;
using CxVisual.Foundation.Geometry;
using System.Collections.Generic;
using System.Linq;
using Ellipse = System.Windows.Shapes.Ellipse;
using Line = System.Windows.Shapes.Line;
using Polygon = System.Windows.Shapes.Polygon;
using Rectangle = System.Windows.Shapes.Rectangle;
using CxVisual.Mechanism.Visual;
using Microsoft.Win32;

namespace CxVisual.Mechanism.Presentation.Wpf.Imaging
{
    /// <summary>不依赖专业SDK的WPF原生二维Viewer。复用FIM图像与MVI会话及视口。</summary>
    public partial class MPWImageViewerControl : UserControl
    {
        private readonly MVIImageSession _session = new MVIImageSession();
        private readonly MVIImageViewport _viewport = new MVIImageViewport();
        private readonly MVIImageViewport _secondaryViewport = new MVIImageViewport();
        private bool _secondaryDragging;
        private Point _secondaryDragStart;
        private readonly MVIImageTransformState _viewTransform = new MVIImageTransformState();
        private readonly MVIImageComparisonState _comparison = new MVIImageComparisonState();
        private FIMPixelSpacing? _pixelSpacing;
        private bool _rulerMillimeters;
        private readonly MVIImageOverlayStore _overlays = new MVIImageOverlayStore();
        private FIMImageBuffer _overlayOriginal;
        private BitmapSource _bitmap;
        private FIMImageBuffer _shownImage;
        private bool _dragging, _selecting, _moving, _pendingFit = true;
        private MVIImageOverlayItem _movingOverlay;
        private FGEPoint2D _moveAnchor;
        private Guid? _selectedOverlayId;
        private MVIImageOverlayItem _resizeStart;
        private MVIImageResizeHandle _resizeHandle;
        private bool _resizing, _resizingSecondary;
        private Point _startPointer;
        private int _selectionX, _selectionY, _lastSelectionX, _lastSelectionY;
        private MVIImageOverlayKind _selectionKind = MVIImageOverlayKind.RectangleRoi;
        private MVIImageToolMode _toolMode = MVIImageToolMode.Navigate;
        private readonly List<FGEPoint2D> _polygonDraft = new List<FGEPoint2D>();
        private FGEPoint2D? _draftHover;
        private string _annotationText = "标注";
        private FIMPixelRegion? _region;
        private readonly MVIImageDisplayState _displayState = new MVIImageDisplayState();

        public MPWImageViewerControl()
        {
            InitializeComponent();
            _session.Changed += OnSessionChanged;
            _displayState.Changed += OnSessionChanged;
            _comparison.Changed += OnSessionChanged;
            _viewTransform.Changed += (sender, args) => { _pendingFit = true; OnSessionChanged(sender, args); };
            _overlays.Changed += (sender, args) =>
            {
                if (Dispatcher.CheckAccess()) DrawOverlays();
                else if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
                    Dispatcher.BeginInvoke(new Action(DrawOverlays));
            };
        }

        public MVIImageSession Session { get { return _session; } }
        public MVIImageDisplayState DisplayState { get { return _displayState; } }
        public MVIImageTransformState ViewTransform { get { return _viewTransform; } }
        public MVIImageComparisonState ComparisonState { get { return _comparison; } }
        public bool IsSideBySideVisible { get { return SecondaryViewportRoot.Visibility == Visibility.Visible; } }
        public BitmapSource SecondaryImageSource { get { return SecondaryPixelImage.Source as BitmapSource; } }
        public int SecondaryOverlayVisualCount { get { return SecondaryOverlayCanvas.Children.Count; } }
        public double PrimaryViewportScale { get { return _viewport.Scale; } }
        public double SecondaryViewportScale { get { return _secondaryViewport.Scale; } }
        public double PrimaryViewportCenterX { get { return _viewport.ImageX(ViewportRoot.ActualWidth / 2.0); } }
        public double SecondaryViewportCenterX
        { get { return _secondaryViewport.ImageX(SecondaryViewportRoot.ActualWidth / 2.0); } }
        public MPWImageRulerControl HorizontalRulerControl { get { return HorizontalRuler; } }
        public MPWImageRulerControl VerticalRulerControl { get { return VerticalRuler; } }
        public FIMPixelSpacing? PixelSpacing
        {
            get { return _pixelSpacing; }
            set
            {
                VerifyAccess();
                if (value.HasValue && (value.Value.MillimetersPerPixelX <= 0 ||
                    value.Value.MillimetersPerPixelY <= 0))
                    throw new ArgumentOutOfRangeException("value");
                _pixelSpacing = value;
                if (!value.HasValue) _rulerMillimeters = false;
                RefreshRulers();
                DrawOverlays();
            }
        }
        public bool ShowPhysicalRulers
        {
            get { return _rulerMillimeters; }
            set
            {
                VerifyAccess();
                if (value && !_pixelSpacing.HasValue)
                    throw new InvalidOperationException("必须先设置有效像素间距才能显示毫米标尺。");
                _rulerMillimeters = value;
                RefreshRulers();
            }
        }

        /// <summary>真实原图/结果图透明叠合，必须同尺寸、格式，原始两张图均不修改。</summary>
        public void BlendOriginalAndResult(double ratio)
        {
            VerifyAccess();
            FIMImageBuffer original = _session.Original, result = _session.Result;
            if (original == null || result == null) throw new InvalidOperationException("原图或结果图不存在。");
            if (original.Width != result.Width || original.Height != result.Height ||
                original.PixelFormat != result.PixelFormat)
                throw new InvalidOperationException("叠合要求原图与结果图尺寸和像素格式完全相同。");
            if (original.PixelFormat == FIMPixelFormat.GrayFloat32)
                throw new NotSupportedException("暂不支持GrayFloat32透明叠合。");
            _comparison.SetBlendRatio(ratio);
            _comparison.SetMode(MVIImageComparisonMode.Blend);
        }
        /// <summary>滑动对比使用同一图像坐标/视口，无需复制Viewer或同步两套Pan/Zoom。</summary>
        public void SwipeOriginalAndResult(double ratio)
        {
            VerifyAccess();
            FIMImageBuffer first = _session.Original, second = _session.Result;
            if (first == null || second == null) throw new InvalidOperationException("原图或结果图不存在。");
            if (first.Width != second.Width || first.Height != second.Height ||
                first.PixelFormat != second.PixelFormat)
                throw new InvalidOperationException("滑动比较要求两图宽高和像素格式完全相同。");
            _comparison.SetBlendRatio(ratio);
            _comparison.SetMode(MVIImageComparisonMode.Swipe);
        }
        /// <summary>原图在左、结果图在右；两个真实WPF视口共享图像会话与原图坐标Overlay Store。</summary>
        public void SideBySideOriginalAndResult(bool synchronize)
        {
            VerifyAccess();
            FIMImageBuffer first = _session.Original, second = _session.Result;
            if (first == null || second == null)
                throw new InvalidOperationException("并排比较要求原图和结果图同时存在。");
            if (first.Width != second.Width || first.Height != second.Height)
                throw new InvalidOperationException("并排同步比较要求相同的图像宽高。");
            _pendingFit = true;
            _comparison.SetSyncView(synchronize);
            _comparison.SetMode(MVIImageComparisonMode.SideBySide);
        }
        public void SetComparisonSynchronization(bool enabled)
        {
            VerifyAccess();
            _comparison.SetSyncView(enabled);
            if (enabled && IsSideBySideVisible) ApplyTransform();
        }
        /// <summary>开发者和用户导航共用同一MVI视口，不修改FIM像素。</summary>
        public void PanComparisonViewport(double dx, double dy, bool resultPane)
        {
            VerifyAccess();
            if (ActiveImage == null) throw new InvalidOperationException("未加载图像。");
            if (resultPane && !IsSideBySideVisible)
                throw new InvalidOperationException("结果视口未启用。");
            if (resultPane)
            {
                _secondaryViewport.Pan(dx,dy);
                SynchronizePrimaryFromSecondary();
            }
            else _viewport.Pan(dx,dy);
            _pendingFit = false;
            ApplyTransform();
        }
        public void DisableBlend() { VerifyAccess(); _comparison.SetMode(MVIImageComparisonMode.Single); }
        public void RotateViewClockwise() { VerifyAccess(); _viewTransform.Rotate90Clockwise(); }
        public void RotateViewCounterclockwise() { VerifyAccess(); _viewTransform.Rotate90Counterclockwise(); }
        public void FlipViewHorizontal() { VerifyAccess(); _viewTransform.FlipHorizontal(); }
        public void FlipViewVertical() { VerifyAccess(); _viewTransform.FlipVertical(); }
        public void ResetViewTransform() { VerifyAccess(); _viewTransform.Reset(); }
        public FIMImageBuffer ActiveImage { get { return _session.ActiveImage; } }
        public FIMPixelRegion? SelectedRegion { get { return _region; } }
        /// <summary>原图坐标的持久会话叠加项；调用方可直接通过MVI Store增删与更新。</summary>
        public MVIImageOverlayStore Overlays { get { return _overlays; } }

        public MVIImageToolMode ToolMode
        {
            get { return _toolMode; }
            set
            {
                VerifyAccess();
                if (!Enum.IsDefined(typeof(MVIImageToolMode), value))
                    throw new ArgumentOutOfRangeException("value");
                _toolMode = value;
                _dragging = _selecting = _moving = _resizing = false;
                _movingOverlay = null;
                _resizeStart = null;
                if (ViewportRoot.IsMouseCaptured) ViewportRoot.ReleaseMouseCapture();
                if (SecondaryViewportRoot.IsMouseCaptured) SecondaryViewportRoot.ReleaseMouseCapture();
                _polygonDraft.Clear();
                _draftHover = null;
                DrawSelection();
                DrawOverlays();
            }
        }

        /// <summary>当前MVI OverlayStore里唯一选中对象；列表选择与视口点击一致。</summary>
        public void SelectOverlay(Guid? id)
        {
            VerifyAccess();
            if (id.HasValue && _overlays.Find(id.Value) == null)
                throw new ArgumentException("选中对象已不存在。","id");
            if (_selectedOverlayId == id) return;
            _selectedOverlayId = id;
            _region = null;
            DrawSelection();
            DrawOverlays();
            RaiseSelectionChanged();
        }

        /// <summary>精确尺寸编辑API：所有几何、图像边界和原对象身份由MVI验证。</summary>
        public MVIImageOverlayItem ResizeSelectedOverlay(MVIImageResizeHandle handle,
            FGEPoint2D position)
        {
            VerifyAccess();
            if (!_selectedOverlayId.HasValue || ActiveImage == null)
                throw new InvalidOperationException("尚未选择有效ROI及图像。");
            MVIImageOverlayItem item = _overlays.Find(_selectedOverlayId.Value);
            if (item == null || !_overlays.GetVisibleItems().Any(x => x.Id == item.Id))
                throw new InvalidOperationException("不可编辑被删除或隐藏的ROI。");
            MVIImageOverlayItem resized = item.WithResizedRoiHandle(handle,position,
                ActiveImage.Width,ActiveImage.Height);
            if (!_overlays.Replace(resized))
                throw new InvalidOperationException("ROI已在编辑期间被删除。");
            return resized;
        }

        /// <summary>与第一阶段TCT矩形ROI切换代码保持兼容。</summary>
        public bool IsRegionTool
        {
            get { return _toolMode == MVIImageToolMode.RectangleRoi; }
            set { ToolMode = value ? MVIImageToolMode.RectangleRoi : MVIImageToolMode.Navigate; }
        }
        public string AnnotationText
        {
            get { return _annotationText; }
            set { VerifyAccess(); _annotationText = value ?? string.Empty; }
        }
        public int VisibleOverlayCount { get { return _overlays.GetVisibleItems().Count; } }
        public int OverlayVisualCount { get { return OverlayCanvas.Children.Count; } }
        public Guid? SelectedOverlayId { get { return _selectedOverlayId; } }
        public int ResizeHandleVisualCount
        {
            get { return CountResizeHandles(OverlayCanvas); }
        }
        public int SecondaryResizeHandleVisualCount
        {
            get { return CountResizeHandles(SecondaryOverlayCanvas); }
        }
        private static int CountResizeHandles(Canvas canvas)
        {
            int count=0;
            foreach (UIElement child in canvas.Children)
                if (child is Rectangle && ((Rectangle)child).Tag is MVIImageResizeHandle) count++;
            return count;
        }
        public event EventHandler SelectionChanged;
        public event EventHandler PixelInspected;

        public void ShowOriginal(FIMImageBuffer image)
        {
            VerifyAccess();
            if (image == null) throw new ArgumentNullException("image");
            _pendingFit = true;
            _region = null;
            _selectedOverlayId = null;
            _resizeStart = null; _resizing = false;
            _pixelSpacing = null; _rulerMillimeters = false;
            _displayState.Reset();
            _viewTransform.Reset();
            _comparison.Reset();
            _polygonDraft.Clear();
            _overlays.Clear();
            _overlayOriginal = image;
            _session.SetOriginal(image);
        }

        public void ShowResult(FIMImageBuffer image)
        {
            VerifyAccess();
            if (image == null) throw new ArgumentNullException("image");
            _session.SetResult(image);
            _session.Select(MVIImageSlot.Result);
        }

        public void SelectOriginal() { VerifyAccess(); _session.Select(MVIImageSlot.Original); }
        public void SelectResult() { VerifyAccess(); _session.Select(MVIImageSlot.Result); }

        public void Clear()
        {
            VerifyAccess();
            _region = null;
            _selectedOverlayId = null; _resizeStart = null; _resizing = false;
            _displayState.Reset();
            _viewTransform.Reset();
            _comparison.Reset();
            _polygonDraft.Clear();
            _overlays.Clear();
            _overlayOriginal = null;
            _pixelSpacing = null; _rulerMillimeters = false;
            _session.Clear();
        }

        public static FIMImageBuffer ReadImageFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("图像文件路径为空。", "path");
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension != ".bmp" && extension != ".png" && extension != ".jpg" &&
                extension != ".jpeg" && extension != ".tif" && extension != ".tiff")
                throw new NotSupportedException("本阶段支持BMP/PNG/JPG/TIFF；RAW需指定尺寸和位深。");
            BitmapSource source;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                BitmapDecoder decoder = BitmapDecoder.Create(stream,
                    BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                if (decoder.Frames.Count == 0) throw new InvalidDataException("图像帧为空。");
                BitmapSource frame = decoder.Frames[0];
                PixelFormat format = frame.Format;
                if (format != PixelFormats.Gray8 && format != PixelFormats.Gray16 &&
                    format != PixelFormats.Gray32Float && format != PixelFormats.Bgr24 &&
                    format != PixelFormats.Bgra32)
                    source = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
                else source = frame;
                source.Freeze();
            }
            FIMPixelFormat pixelFormat = GetPixelFormat(source.Format);
            int stride = checked(source.PixelWidth * FIMImageBuffer.GetBytesPerPixel(pixelFormat));
            long byteSize = (long)stride * source.PixelHeight;
            if (byteSize <= 0 || byteSize > 512L * 1024 * 1024)
                throw new InvalidDataException("图像解码快照超过512MiB安全限制。");
            byte[] pixels = new byte[(int)byteSize];
            source.CopyPixels(pixels, stride, 0);
            return new FIMImageBuffer(source.PixelWidth, source.PixelHeight, pixelFormat, pixels, stride);
        }

        public void OpenFile(string path) { VerifyAccess(); ShowOriginal(ReadImageFile(path)); }

        public void SavePng(string path)
        {
            VerifyAccess();
            if (_bitmap == null) throw new InvalidOperationException("尚无可导出图像。");
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(_bitmap));
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write))
                encoder.Save(stream);
        }

        /// <summary>导出当前WPF视口截图，包含ROI、测量、标注与缺陷图层；不同于仅原像素的SavePng。</summary>
        public void SaveViewPng(string path)
        {
            VerifyAccess();
            if (ActiveImage == null) throw new InvalidOperationException("没有可导出的图像视口。");
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("输出路径不能为空。", "path");
            // 单视图只截当前画面；双视图必须将左原/右结果、共同ROI/标注、
            // 分隔条都写入同一PNG，而不是只导出左侧ViewportRoot。
            FrameworkElement rendered = IsSideBySideVisible ?
                (FrameworkElement)ComparisonLayout : ViewportRoot;
            int width = (int)Math.Ceiling(rendered.ActualWidth);
            int height = (int)Math.Ceiling(rendered.ActualHeight);
            if (width < 1 || height < 1 || (long)width * height * 4 > 512L * 1024 * 1024)
                throw new InvalidOperationException("视口尚未布置或导出尺寸过大。");
            RenderTargetBitmap snapshot = new RenderTargetBitmap(width, height, 96, 96,
                PixelFormats.Pbgra32);
            snapshot.Render(rendered);
            snapshot.Freeze();
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(snapshot));
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write))
                encoder.Save(stream);
        }

        public void CopyToClipboard()
        {
            VerifyAccess();
            if (_bitmap == null) throw new InvalidOperationException("尚无图像。");
            Clipboard.SetImage(_bitmap);
        }

        public void SetDisplayWindow(double? low, double? high)
        {
            VerifyAccess();
            if (low.HasValue != high.HasValue)
                throw new ArgumentException("窗口下限与上限必须一起指定或一起清除。");
            if (low.HasValue && (!IsFinite(low.Value) || !IsFinite(high.Value) || high.Value <= low.Value))
                throw new ArgumentOutOfRangeException("high");
            FIMImageBuffer current = ActiveImage;
            if (low.HasValue && current != null &&
                (current.PixelFormat == FIMPixelFormat.Bgr24 || current.PixelFormat == FIMPixelFormat.Bgra32))
                throw new NotSupportedException("彩色图不能使用单通道窗宽窗位；请使用亮度、Gamma、对比度。");
            if (!low.HasValue) _displayState.SetAutoWindowLevel(true);
            else _displayState.SetWindowLevel((low.Value + high.Value) * 0.5, high.Value - low.Value);
        }

        public void FitToWindow()
        {
            VerifyAccess();
            FIMImageBuffer image = ActiveImage;
            if (image == null) return;
            if (ViewportRoot.ActualWidth <= 0 || ViewportRoot.ActualHeight <= 0)
            { _pendingFit = true; return; }
            FGESize2D size = _viewTransform.GetDisplaySize(image.Width,image.Height);
            _viewport.Fit((int)size.Width,(int)size.Height,ViewportRoot.ActualWidth,ViewportRoot.ActualHeight);
            if (IsSideBySideVisible && SecondaryViewportRoot.ActualWidth > 0 &&
                SecondaryViewportRoot.ActualHeight > 0)
            {
                _secondaryViewport.Fit((int)size.Width,(int)size.Height,
                    SecondaryViewportRoot.ActualWidth,SecondaryViewportRoot.ActualHeight);
                SynchronizeSecondaryFromPrimary();
            }
            _pendingFit = false;
            ApplyTransform();
        }

        public void ActualSize()
        {
            VerifyAccess();
            FIMImageBuffer image = ActiveImage;
            if (image == null) return;
            if (ViewportRoot.ActualWidth <= 0 || ViewportRoot.ActualHeight <= 0) return;
            FGESize2D size = _viewTransform.GetDisplaySize(image.Width,image.Height);
            _viewport.ActualSize((int)size.Width,(int)size.Height,ViewportRoot.ActualWidth,ViewportRoot.ActualHeight);
            if (IsSideBySideVisible && SecondaryViewportRoot.ActualWidth > 0 &&
                SecondaryViewportRoot.ActualHeight > 0)
            {
                _secondaryViewport.ActualSize((int)size.Width,(int)size.Height,
                    SecondaryViewportRoot.ActualWidth,SecondaryViewportRoot.ActualHeight);
                SynchronizeSecondaryFromPrimary();
            }
            _pendingFit = false;
            ApplyTransform();
        }

        public void Zoom(double factor)
        {
            VerifyAccess();
            if (ActiveImage == null) return;
            _viewport.ZoomAt(factor, ViewportRoot.ActualWidth / 2, ViewportRoot.ActualHeight / 2);
            _pendingFit = false;
            ApplyTransform();
        }

        public void SelectRegion(FIMPixelRegion region)
        {
            VerifyAccess();
            FIMImageBuffer image = ActiveImage;
            if (image == null) throw new InvalidOperationException("尚未加载图像。");
            if (region.Right >= image.Width || region.Bottom >= image.Height)
                throw new ArgumentOutOfRangeException("region");
            _region = region;
            DrawSelection();
            RaiseSelectionChanged();
        }

        public void ClearSelection()
        {
            VerifyAccess();
            _region = null;
            DrawSelection();
            RaiseSelectionChanged();
        }

        private void VerifyAccess()
        {
            if (!Dispatcher.CheckAccess())
                throw new InvalidOperationException("WPF ImageViewer 必须在所属UI线程操作。");
        }

        private static bool IsFinite(double value)
        { return !double.IsNaN(value) && !double.IsInfinity(value); }

        private static FIMPixelFormat GetPixelFormat(PixelFormat format)
        {
            if (format == PixelFormats.Gray8) return FIMPixelFormat.Gray8;
            if (format == PixelFormats.Gray16) return FIMPixelFormat.Gray16;
            if (format == PixelFormats.Gray32Float) return FIMPixelFormat.GrayFloat32;
            if (format == PixelFormats.Bgr24) return FIMPixelFormat.Bgr24;
            if (format == PixelFormats.Bgra32) return FIMPixelFormat.Bgra32;
            throw new NotSupportedException("像素格式尚不支持。");
        }

        private static PixelFormat GetWpfFormat(FIMPixelFormat format)
        {
            switch (format)
            {
                case FIMPixelFormat.Gray8: return PixelFormats.Gray8;
                case FIMPixelFormat.Gray16: return PixelFormats.Gray16;
                case FIMPixelFormat.GrayFloat32: return PixelFormats.Gray32Float;
                case FIMPixelFormat.Bgr24: return PixelFormats.Bgr24;
                case FIMPixelFormat.Bgra32: return PixelFormats.Bgra32;
                default: throw new NotSupportedException("暂不支持此像素格式。");
            }
        }

        private void OnSessionChanged(object sender, EventArgs e)
        {
            if (Dispatcher.CheckAccess()) RefreshDisplay();
            else if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
                Dispatcher.BeginInvoke(new Action(RefreshDisplay));
        }

        private void RefreshDisplay()
        {
            VerifyAccess();
            FIMImageBuffer original = _session.Original;
            if (!ReferenceEquals(original, _overlayOriginal))
            {
                _overlayOriginal = original;
                _region = null;
                _pixelSpacing = null; _rulerMillimeters = false;
                _polygonDraft.Clear();
                _overlays.Clear();
            }
            FIMImageBuffer image = ActiveImage;
            bool dimensionsChanged = _shownImage == null || image == null ||
                _shownImage.Width != image.Width || _shownImage.Height != image.Height;
            _shownImage = image;
            if (image == null)
            {
                _bitmap = null;
                PixelImage.Source = null;
                SecondaryPixelImage.Source = null;
                SetComparisonLayout(false);
                EmptyText.Visibility = Visibility.Visible;
                SelectedRoi.Visibility = Visibility.Collapsed;
                OverlayCanvas.Children.Clear();
                PixelStatus.Text = "未载入图像";
                return;
            }

            if (_comparison.Mode == MVIImageComparisonMode.Blend ||
                _comparison.Mode == MVIImageComparisonMode.Swipe)
            {
                FIMImageBuffer first = _session.Original, second = _session.Result;
                if (first == null || second == null || first.Width != second.Width ||
                    first.Height != second.Height || first.PixelFormat != second.PixelFormat)
                {
                    _comparison.SetMode(MVIImageComparisonMode.Single);
                    return;
                }
                image = _comparison.Mode == MVIImageComparisonMode.Swipe ?
                    FIMImageBlend.Swipe(first,second,_comparison.BlendRatio) :
                    FIMImageBlend.Blend(first, second, _comparison.BlendRatio);
            }
            bool sideBySide = _comparison.Mode == MVIImageComparisonMode.SideBySide;
            FIMImageBuffer secondImage = null;
            if (sideBySide)
            {
                FIMImageBuffer first = _session.Original, second = _session.Result;
                if (first == null || second == null || first.Width != second.Width ||
                    first.Height != second.Height)
                {
                    _comparison.SetMode(MVIImageComparisonMode.Single);
                    return;
                }
                image = first;
                secondImage = second;
            }
            SetComparisonLayout(sideBySide);
            FIMImageBuffer display = ApplyViewTransform(CreateDisplayBuffer(image));
            int stride = checked(display.Width * display.BytesPerPixel);
            long memory = (long)stride * display.Height;
            if (memory > 512L * 1024 * 1024)
                throw new InvalidOperationException("显示像素超过512MiB快照限制。");
            byte[] data = display.CopyPixels();
            // Preserve Stride for padded rows; WPF creates its own presentation snapshot.
            BitmapSource bitmap = BitmapSource.Create(display.Width, display.Height, 96, 96,
                GetWpfFormat(display.PixelFormat), null, data, display.Stride);
            bitmap.Freeze();
            _bitmap = bitmap;
            if (sideBySide)
            {
                FIMImageBuffer right = ApplyViewTransform(CreateDisplayBuffer(secondImage));
                long bytes = (long)right.Stride * right.Height;
                if (bytes > 512L * 1024 * 1024)
                    throw new InvalidOperationException("结果视图显示缓冲超过512MiB。");
                BitmapSource rightBitmap = BitmapSource.Create(right.Width,right.Height,96,96,
                    GetWpfFormat(right.PixelFormat),null,right.CopyPixels(),right.Stride);
                rightBitmap.Freeze();
                SecondaryPixelImage.Width = right.Width;
                SecondaryPixelImage.Height = right.Height;
                SecondaryPixelImage.Source = rightBitmap;
            }
            else SecondaryPixelImage.Source = null;
            PixelImage.Width = display.Width;
            PixelImage.Height = display.Height;
            PixelImage.Source = bitmap;
            EmptyText.Visibility = Visibility.Collapsed;
            PixelStatus.Text = image.Width + "×" + image.Height + " " + image.PixelFormat +
                (_comparison.Mode == MVIImageComparisonMode.Blend ?
                    " · 叠合 " + _comparison.BlendRatio.ToString("0.##") :
                 _comparison.Mode == MVIImageComparisonMode.Swipe ?
                    " · 滑动比较 " + _comparison.BlendRatio.ToString("0.##") :
                 sideBySide ? " · 并排原图/结果图" +
                    (_comparison.IsSyncViewEnabled ? " · 同步" : " · 独立导航") : "");
            if (dimensionsChanged || _pendingFit) FitToWindow();
            else ApplyTransform();
        }

        /// <summary>只有呈现缓冲经调整，原始FIM图像和统计完全不变。</summary>
        private FIMImageBuffer CreateDisplayBuffer(FIMImageBuffer image)
        {
            if (_displayState.IsIdentity) return image;

            double low = 0, high = 255;
            bool isColor = image.PixelFormat == FIMPixelFormat.Bgr24 ||
                image.PixelFormat == FIMPixelFormat.Bgra32;
            if (isColor)
            {
                if (!_displayState.IsAutoWindowLevel)
                    throw new NotSupportedException("手动窗宽窗位要求灰度图像，彩色图请使用亮度/Gamma/对比度。");
                if (_displayState.IsAutoContrast)
                {
                    FIMImageStatistics stats = FIMImageAnalyzer.Calculate(image);
                    low = stats.Min;
                    high = stats.Max > stats.Min ? stats.Max : stats.Min + 1;
                }
                return FIMImageToneMapping.MapColorRange(image, low, high,
                    _displayState.Gamma, _displayState.Brightness,
                    _displayState.Contrast, _displayState.IsInverted);
            }

            if (!_displayState.IsAutoWindowLevel)
            {
                low = _displayState.WindowCenter - _displayState.WindowWidth / 2;
                high = _displayState.WindowCenter + _displayState.WindowWidth / 2;
            }
            else if (_displayState.IsAutoContrast || image.PixelFormat == FIMPixelFormat.GrayFloat32)
            {
                FIMImageStatistics stats = FIMImageAnalyzer.Calculate(image);
                low = stats.Min;
                high = stats.Max > stats.Min ? stats.Max : stats.Min + 1;
            }
            else if (image.PixelFormat == FIMPixelFormat.Gray16)
                high = 65535;
            return FIMImageToneMapping.MapGray(image, low, high,
                _displayState.Gamma, _displayState.Brightness,
                _displayState.Contrast, _displayState.IsInverted);
        }

        private FIMImageBuffer ApplyViewTransform(FIMImageBuffer image)
        {
            switch (_viewTransform.QuarterTurns)
            {
                case 1: image = FIMImageOperations.Rotate90Clockwise(image); break;
                case 2: image = FIMImageOperations.Rotate180(image); break;
                case 3: image = FIMImageOperations.Rotate90Counterclockwise(image); break;
            }
            if (_viewTransform.IsFlippedHorizontally) image = FIMImageOperations.FlipHorizontal(image);
            if (_viewTransform.IsFlippedVertically) image = FIMImageOperations.FlipVertical(image);
            return image;
        }

        private Point SourceToCanvas(FGEPoint2D source)
        { return SourceToCanvas(source, _viewport); }
        private Point SourceToCanvas(FGEPoint2D source, MVIImageViewport viewport)
        {
            FIMImageBuffer active = ActiveImage;
            if (active == null) return new Point(0,0);
            FGEPoint2D display = _viewTransform.SourceToDisplay(source,active.Width,active.Height);
            return new Point(viewport.DisplayX(display.X),viewport.DisplayY(display.Y));
        }
        private FGEPoint2D CanvasToSource(Point point)
        {
            FIMImageBuffer active = ActiveImage;
            if (active == null) throw new InvalidOperationException("尚无活动图像。");
            return _viewTransform.DisplayToSource(
                new FGEPoint2D(_viewport.ImageX(point.X),_viewport.ImageY(point.Y)),
                active.Width,active.Height);
        }
        private bool OverlayMatchesActiveImage()
        {
            FIMImageBuffer original = _session.Original, active = ActiveImage;
            return original != null && active != null &&
                original.Width == active.Width && original.Height == active.Height;
        }

        private void SetComparisonLayout(bool enabled)
        {
            bool wasVisible = IsSideBySideVisible;
            if (enabled == wasVisible) return;
            SecondaryColumn.Width = enabled ? new GridLength(1,GridUnitType.Star) : new GridLength(0);
            ComparisonDividerColumn.Width = enabled ? new GridLength(5) : new GridLength(0);
            ComparisonRulerColumn.Width = enabled ? new GridLength(1,GridUnitType.Star) : new GridLength(0);
            ComparisonRulerDivider.Width = enabled ? new GridLength(5) : new GridLength(0);
            SecondaryViewportRoot.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
            SecondaryHorizontalRuler.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
            ComparisonDivider.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
            OriginalPaneLabel.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
            if (enabled) _pendingFit = true;
        }

        private void SynchronizeSecondaryFromPrimary()
        {
            if (!IsSideBySideVisible || !_comparison.IsSyncViewEnabled ||
                ViewportRoot.ActualWidth <= 0 || ViewportRoot.ActualHeight <= 0 ||
                SecondaryViewportRoot.ActualWidth <= 0 || SecondaryViewportRoot.ActualHeight <= 0)
                return;
            _secondaryViewport.SynchronizeFrom(_viewport,
                ViewportRoot.ActualWidth, ViewportRoot.ActualHeight,
                SecondaryViewportRoot.ActualWidth, SecondaryViewportRoot.ActualHeight);
        }

        private void SynchronizePrimaryFromSecondary()
        {
            if (!IsSideBySideVisible || !_comparison.IsSyncViewEnabled ||
                ViewportRoot.ActualWidth <= 0 || ViewportRoot.ActualHeight <= 0 ||
                SecondaryViewportRoot.ActualWidth <= 0 || SecondaryViewportRoot.ActualHeight <= 0)
                return;
            _viewport.SynchronizeFrom(_secondaryViewport,
                SecondaryViewportRoot.ActualWidth, SecondaryViewportRoot.ActualHeight,
                ViewportRoot.ActualWidth, ViewportRoot.ActualHeight);
        }

        private void ApplyTransform()
        {
            SynchronizeSecondaryFromPrimary();
            SecondaryImageTransform.Matrix = new Matrix(_secondaryViewport.Scale,0,0,
                _secondaryViewport.Scale,_secondaryViewport.OffsetX,_secondaryViewport.OffsetY);
            ImageTransform.Matrix = new Matrix(_viewport.Scale, 0, 0, _viewport.Scale,
                _viewport.OffsetX, _viewport.OffsetY);
            DrawSelection();
            DrawOverlays();
            RefreshRulers();
        }

        private void RefreshRulers()
        {
            FIMImageBuffer image = ActiveImage;
            if (image == null || ViewportRoot.ActualWidth <= 0 || ViewportRoot.ActualHeight <= 0)
            {
                HorizontalRuler.SetRange(0,1,"px");
                VerticalRuler.SetRange(0,1,"px");
                return;
            }
            FGEPoint2D corner = _viewTransform.DisplayToSource(new FGEPoint2D(
                _viewport.ImageX(0),_viewport.ImageY(0)),image.Width,image.Height);
            FGEPoint2D across = _viewTransform.DisplayToSource(new FGEPoint2D(
                _viewport.ImageX(ViewportRoot.ActualWidth),_viewport.ImageY(0)),
                image.Width,image.Height);
            FGEPoint2D down = _viewTransform.DisplayToSource(new FGEPoint2D(
                _viewport.ImageX(0),_viewport.ImageY(ViewportRoot.ActualHeight)),
                image.Width,image.Height);
            bool swapped = _viewTransform.QuarterTurns % 2 == 1;
            double x0 = swapped ? corner.Y : corner.X;
            double x1 = swapped ? across.Y : across.X;
            double y0 = swapped ? corner.X : corner.Y;
            double y1 = swapped ? down.X : down.Y;
            bool useMm = _rulerMillimeters && _pixelSpacing.HasValue;
            double mmX = useMm ? (swapped ? _pixelSpacing.Value.MillimetersPerPixelY :
                _pixelSpacing.Value.MillimetersPerPixelX) : 1;
            double mmY = useMm ? (swapped ? _pixelSpacing.Value.MillimetersPerPixelX :
                _pixelSpacing.Value.MillimetersPerPixelY) : 1;
            string unit = useMm ? "mm" : "px";
            HorizontalRuler.SetRange(x0*mmX,x1*mmX,unit);
            VerticalRuler.SetRange(y0*mmY,y1*mmY,unit);
            if (IsSideBySideVisible && SecondaryViewportRoot.ActualWidth > 0)
            {
                FGEPoint2D rightStart = _viewTransform.DisplayToSource(new FGEPoint2D(
                    _secondaryViewport.ImageX(0),_secondaryViewport.ImageY(0)),
                    image.Width,image.Height);
                FGEPoint2D rightEnd = _viewTransform.DisplayToSource(new FGEPoint2D(
                    _secondaryViewport.ImageX(SecondaryViewportRoot.ActualWidth),
                    _secondaryViewport.ImageY(0)),image.Width,image.Height);
                SecondaryHorizontalRuler.SetRange((swapped ? rightStart.Y : rightStart.X)*mmX,
                    (swapped ? rightEnd.Y : rightEnd.X)*mmX,unit);
            }
        }

        private void DrawSelection()
        {
            DrawDraftPolyline();
            if (!_region.HasValue || !OverlayMatchesActiveImage())
            { SelectedRoi.Visibility = Visibility.Collapsed; return; }
            FIMPixelRegion roi = _region.Value;
            Point a = SourceToCanvas(new FGEPoint2D(roi.Left,roi.Top));
            Point b = SourceToCanvas(new FGEPoint2D(roi.Right+1.0,roi.Bottom+1.0));
            Canvas.SetLeft(SelectedRoi,Math.Min(a.X,b.X));
            Canvas.SetTop(SelectedRoi,Math.Min(a.Y,b.Y));
            SelectedRoi.Width = Math.Abs(a.X-b.X);
            SelectedRoi.Height = Math.Abs(a.Y-b.Y);
            SelectedRoi.Visibility = Visibility.Visible;
        }

        private void DrawDraftPolyline()
        {
            DraftPolyline.Points.Clear();
            if (_polygonDraft.Count == 0 || !OverlayMatchesActiveImage())
            { DraftPolyline.Visibility = Visibility.Collapsed; return; }
            foreach (FGEPoint2D p in _polygonDraft)
                DraftPolyline.Points.Add(SourceToCanvas(p));
            if (_draftHover.HasValue)
                DraftPolyline.Points.Add(SourceToCanvas(_draftHover.Value));
            DraftPolyline.Visibility = Visibility.Visible;
        }

        /// <summary>原图坐标直接增加多边形，可供算法检测和交互工具统一调用。</summary>
        public MVIImageOverlayItem AddPolygonRoi(IReadOnlyList<FGEPoint2D> points)
        {
            VerifyAccess();
            return AddPointGeometry(MVIImageOverlayKind.PolygonRoi,
                MVIImageOverlayLayer.Roi, points, 3);
        }

        public MVIImageOverlayItem AddAngleMeasurement(IReadOnlyList<FGEPoint2D> points)
        {
            VerifyAccess();
            if (points == null || points.Count != 3)
                throw new ArgumentException("角度测量要求恰好三个点。", "points");
            // 若退化点不先反证，图窗会在绘制结果时得到无法显示的角度。
            FIMImageMeasurement.GetAngleDegrees(points[0], points[1], points[2]);
            return AddPointGeometry(MVIImageOverlayKind.AngleMeasurement,
                MVIImageOverlayLayer.Measurement, points, 3);
        }

        private MVIImageOverlayItem AddPointGeometry(MVIImageOverlayKind kind,
            MVIImageOverlayLayer layer, IReadOnlyList<FGEPoint2D> points, int minCount)
        {
            FIMImageBuffer image = ActiveImage;
            if (image == null) throw new InvalidOperationException("未加载图像。");
            if (points == null || points.Count < minCount)
                throw new ArgumentException("点数不足。", "points");
            double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
            foreach (FGEPoint2D p in points)
            {
                if (p.X < 0 || p.Y < 0 || p.X > image.Width || p.Y > image.Height)
                    throw new ArgumentOutOfRangeException("points", "点必须在当前原图边界内。");
                minX = Math.Min(minX,p.X); minY = Math.Min(minY,p.Y);
                maxX = Math.Max(maxX,p.X); maxY = Math.Max(maxY,p.Y);
            }
            return AddOverlay(new MVIImageOverlayItem(kind,layer,
                new FGERect2D(minX,minY,maxX-minX,maxY-minY), points));
        }

        private void CompletePointDraft(MVIImageToolMode mode)
        {
            if (_polygonDraft.Count >= 3)
            {
                if (mode == MVIImageToolMode.Angle)
                    AddAngleMeasurement(_polygonDraft.ToArray());
                else if (mode == MVIImageToolMode.PolygonArea)
                {
                    if (FIMImageMeasurement.GetPolygonAreaPixels(_polygonDraft) <= 0)
                        throw new ArgumentException("面积测量不接受零面积多边形。");
                    AddPointGeometry(MVIImageOverlayKind.AreaMeasurement,
                        MVIImageOverlayLayer.Measurement,_polygonDraft.ToArray(),3);
                }
                else AddPolygonRoi(_polygonDraft.ToArray());
                RaiseSelectionChanged();
            }
            _polygonDraft.Clear();
            _draftHover = null;
            DrawDraftPolyline();
        }

        /// <summary>所有叠加对象从原图坐标重绘；屏幕缩放/平移只改变投影，不修改ROI值。</summary>
        /// <summary>
        /// 用原始像素几何计算完整测量结果，同时服务画布标签、对象列表与CSV。
        /// 物理量只在明确标定时输出；非等距像素下圆的物理X/Y直径分别报告。
        /// </summary>
        public string DescribeMeasurement(MVIImageOverlayItem item)
        {
            VerifyAccess();
            if (item == null) throw new ArgumentNullException("item");
            FIMPixelSpacing? spacing = PixelSpacing;
            FGEPoint2D a = item.Points.Count >= 2 ? item.Points[0] : new FGEPoint2D(0,0);
            FGEPoint2D b = item.Points.Count >= 2 ? item.Points[1] : new FGEPoint2D(0,0);
            double pixels;
            double? physical;
            string suffix;
            switch (item.Kind)
            {
                case MVIImageOverlayKind.DistanceMeasurement:
                    pixels = FIMImageMeasurement.GetDistancePixels(a,b);
                    physical = FIMImageMeasurement.GetDistanceMillimeters(a,b,spacing);
                    suffix = "mm"; break;
                case MVIImageOverlayKind.HorizontalMeasurement:
                    pixels = FIMImageMeasurement.GetHorizontalDistancePixels(a,b);
                    physical = FIMImageMeasurement.GetHorizontalDistanceMillimeters(a,b,spacing);
                    suffix = "mm"; break;
                case MVIImageOverlayKind.VerticalMeasurement:
                    pixels = FIMImageMeasurement.GetVerticalDistancePixels(a,b);
                    physical = FIMImageMeasurement.GetVerticalDistanceMillimeters(a,b,spacing);
                    suffix = "mm"; break;
                case MVIImageOverlayKind.AngleMeasurement:
                    if (item.Points.Count != 3) return string.Empty;
                    return FIMImageMeasurement.GetAngleDegrees(
                        item.Points[0],item.Points[1],item.Points[2]).ToString("0.###") + "°";
                case MVIImageOverlayKind.DiameterMeasurement:
                    pixels = item.Bounds.Width;
                    if (spacing.HasValue)
                    {
                        double x = pixels * spacing.Value.MillimetersPerPixelX;
                        double y = pixels * spacing.Value.MillimetersPerPixelY;
                        return pixels.ToString("0.###") + " px · X " + x.ToString("0.###") +
                            " mm / Y " + y.ToString("0.###") + " mm";
                    }
                    return pixels.ToString("0.###") + " px";
                case MVIImageOverlayKind.AreaMeasurement:
                    pixels = item.Points.Count >= 3 ?
                        FIMImageMeasurement.GetPolygonAreaPixels(item.Points) :
                        FIMImageMeasurement.GetRectangleAreaPixels(item.Bounds);
                    physical = item.Points.Count >= 3 ?
                        FIMImageMeasurement.GetPolygonAreaSquareMillimeters(item.Points,spacing) :
                        FIMImageMeasurement.GetRectangleAreaSquareMillimeters(item.Bounds,spacing);
                    suffix = "mm²";
                    return pixels.ToString("0.###") + " px²" +
                        (physical.HasValue ? " / " + physical.Value.ToString("0.###") + " " + suffix : "");
                default: return string.Empty;
            }
            return pixels.ToString("0.###") + " px" +
                (physical.HasValue ? " / " + physical.Value.ToString("0.###") + " " + suffix : "");
        }

        private void DrawOverlays()
        {
            VerifyAccess();
            OverlayCanvas.Children.Clear();
            SecondaryOverlayCanvas.Children.Clear();
            if (_selectedOverlayId.HasValue && _overlays.Find(_selectedOverlayId.Value) == null)
                _selectedOverlayId = null;
            if (!OverlayMatchesActiveImage()) return;
            DrawOverlaysOn(OverlayCanvas,_viewport);
            if (IsSideBySideVisible) DrawOverlaysOn(SecondaryOverlayCanvas,_secondaryViewport);
        }

        private void DrawOverlaysOn(Canvas canvas,MVIImageViewport viewport)
        {
            foreach (MVIImageOverlayItem item in _overlays.GetVisibleItems())
            {
                Color color = Color.FromArgb((byte)((uint)item.Argb >> 24),
                    (byte)((uint)item.Argb >> 16), (byte)((uint)item.Argb >> 8),
                    (byte)item.Argb);
                Brush stroke = new SolidColorBrush(color);
                double width = item.LineWidth;
                Point cornerA = SourceToCanvas(new FGEPoint2D(item.Bounds.Left,item.Bounds.Top),viewport);
                Point cornerB = SourceToCanvas(new FGEPoint2D(item.Bounds.Right,item.Bounds.Bottom),viewport);
                double left=Math.Min(cornerA.X,cornerB.X),top=Math.Min(cornerA.Y,cornerB.Y);
                double right=Math.Max(cornerA.X,cornerB.X),bottom=Math.Max(cornerA.Y,cornerB.Y);
                if (item.Kind == MVIImageOverlayKind.CircleRoi ||
                    item.Kind == MVIImageOverlayKind.EllipseRoi ||
                    item.Kind == MVIImageOverlayKind.DiameterMeasurement)
                {
                    Ellipse ellipse = new Ellipse { Width = Math.Max(0, right - left),
                        Height = Math.Max(0, bottom - top), Stroke = stroke,
                        StrokeThickness = width, Fill = Brushes.Transparent,
                        IsHitTestVisible = false };
                    Canvas.SetLeft(ellipse, left); Canvas.SetTop(ellipse, top);
                    canvas.Children.Add(ellipse);
                }
                else if (item.Kind == MVIImageOverlayKind.RectangleRoi ||
                         item.Kind == MVIImageOverlayKind.RectangleAnnotation ||
                         item.Kind == MVIImageOverlayKind.DefectBox ||
                         (item.Kind == MVIImageOverlayKind.AreaMeasurement && item.Points.Count == 0))
                {
                    Rectangle rectangle = new Rectangle { Width = Math.Max(0, right - left),
                        Height = Math.Max(0, bottom - top), Stroke = stroke,
                        StrokeThickness = width, Fill = Brushes.Transparent,
                        IsHitTestVisible = false };
                    Canvas.SetLeft(rectangle, left); Canvas.SetTop(rectangle, top);
                    canvas.Children.Add(rectangle);
                }
                else if (item.Kind == MVIImageOverlayKind.PolygonRoi ||
                         item.Kind == MVIImageOverlayKind.PolygonAnnotation ||
                         (item.Kind == MVIImageOverlayKind.AreaMeasurement && item.Points.Count >= 3))
                {
                    Polygon polygon = new Polygon { Stroke = stroke, StrokeThickness = width,
                        Fill = Brushes.Transparent, IsHitTestVisible = false };
                    foreach (FGEPoint2D p in item.Points)
                        polygon.Points.Add(SourceToCanvas(p,viewport));
                    canvas.Children.Add(polygon);
                }
                else if (item.Kind == MVIImageOverlayKind.PointAnnotation)
                {
                    FGEPoint2D point = item.Points.Count > 0 ? item.Points[0] :
                        new FGEPoint2D(item.Bounds.Left, item.Bounds.Top);
                    Point position = SourceToCanvas(point,viewport);
                    double px=position.X, py=position.Y;
                    DrawLine(px - 5, py, px + 5, py, stroke, width,canvas);
                    DrawLine(px, py - 5, px, py + 5, stroke, width,canvas);
                }
                else if (item.Kind == MVIImageOverlayKind.TextAnnotation)
                {
                    TextBlock text = new TextBlock { Text = item.Text,
                        Foreground = stroke, FontSize = 14, IsHitTestVisible = false };
                    Canvas.SetLeft(text, left); Canvas.SetTop(text, top);
                    canvas.Children.Add(text);
                }
                else if (item.Points.Count >= 2)
                {
                    for (int i = 1; i < item.Points.Count; i++)
                    {
                        FGEPoint2D a = item.Points[i - 1], b = item.Points[i];
                        Point first=SourceToCanvas(a,viewport),last=SourceToCanvas(b,viewport);
                        DrawLine(first.X,first.Y,last.X,last.Y,stroke,width,canvas);
                    }
                    if (item.Kind == MVIImageOverlayKind.ArrowAnnotation)
                    {
                        FGEPoint2D a = item.Points[0], b = item.Points[item.Points.Count - 1];
                        Point first=SourceToCanvas(a,viewport),last=SourceToCanvas(b,viewport);
                        double x1=first.X,y1=first.Y,x2=last.X,y2=last.Y;
                        double angle = Math.Atan2(y2 - y1, x2 - x1);
                        DrawLine(x2, y2, x2 - 10 * Math.Cos(angle - Math.PI / 6),
                            y2 - 10 * Math.Sin(angle - Math.PI / 6), stroke, width,canvas);
                        DrawLine(x2, y2, x2 - 10 * Math.Cos(angle + Math.PI / 6),
                            y2 - 10 * Math.Sin(angle + Math.PI / 6), stroke, width,canvas);
                    }
                    if (item.Kind == MVIImageOverlayKind.AngleMeasurement && item.Points.Count >= 3)
                    {
                        double angle = FIMImageMeasurement.GetAngleDegrees(
                            item.Points[0],item.Points[1],item.Points[2]);
                        Point vertex = SourceToCanvas(item.Points[1],viewport);
                        TextBlock label = new TextBlock { Text = angle.ToString("0.##") + "°",
                            Foreground = stroke, Background = Brushes.Black,
                            FontSize = 12, IsHitTestVisible = false };
                        Canvas.SetLeft(label, vertex.X + 5); Canvas.SetTop(label,vertex.Y + 5);
                        canvas.Children.Add(label);
                    }
                    if (item.Kind == MVIImageOverlayKind.DistanceMeasurement ||
                        item.Kind == MVIImageOverlayKind.HorizontalMeasurement ||
                        item.Kind == MVIImageOverlayKind.VerticalMeasurement)
                        DrawMeasurementLabel(item, stroke, SourceToCanvas(item.Points[item.Points.Count-1],viewport),canvas);
                }
                if (item.Kind == MVIImageOverlayKind.DiameterMeasurement ||
                    item.Kind == MVIImageOverlayKind.AreaMeasurement)
                    DrawMeasurementLabel(item,stroke,new Point(right,top),canvas);
            }
            if (_toolMode == MVIImageToolMode.ResizeOverlay && _selectedOverlayId.HasValue)
            {
                MVIImageOverlayItem selected = _overlays.GetVisibleItems().FirstOrDefault(
                    item => item.Id == _selectedOverlayId.Value && item.Layer == MVIImageOverlayLayer.Roi);
                if (selected != null && !selected.IsLocked &&
                    selected.Bounds.Width > 0 && selected.Bounds.Height > 0)
                    DrawResizeHandles(canvas,viewport,selected);
            }
        }

        private static FGEPoint2D ResizeHandlePoint(FGERect2D bounds, MVIImageResizeHandle handle)
        {
            double cx=(bounds.Left+bounds.Right)/2.0, cy=(bounds.Top+bounds.Bottom)/2.0;
            switch (handle)
            {
                case MVIImageResizeHandle.TopLeft: return new FGEPoint2D(bounds.Left,bounds.Top);
                case MVIImageResizeHandle.Top: return new FGEPoint2D(cx,bounds.Top);
                case MVIImageResizeHandle.TopRight: return new FGEPoint2D(bounds.Right,bounds.Top);
                case MVIImageResizeHandle.Right: return new FGEPoint2D(bounds.Right,cy);
                case MVIImageResizeHandle.BottomRight: return new FGEPoint2D(bounds.Right,bounds.Bottom);
                case MVIImageResizeHandle.Bottom: return new FGEPoint2D(cx,bounds.Bottom);
                case MVIImageResizeHandle.BottomLeft: return new FGEPoint2D(bounds.Left,bounds.Bottom);
                case MVIImageResizeHandle.Left: return new FGEPoint2D(bounds.Left,cy);
                default: throw new ArgumentOutOfRangeException("handle");
            }
        }

        private void DrawResizeHandles(Canvas canvas,MVIImageViewport viewport,
            MVIImageOverlayItem item)
        {
            foreach (MVIImageResizeHandle handle in Enum.GetValues(typeof(MVIImageResizeHandle)))
            {
                Point screen = SourceToCanvas(ResizeHandlePoint(item.Bounds,handle),viewport);
                Rectangle square = new Rectangle {
                    Width=9,Height=9,Stroke=Brushes.DarkSlateGray,StrokeThickness=1.2,
                    Fill=Brushes.LightCyan,Tag=handle,IsHitTestVisible=false };
                System.Windows.Automation.AutomationProperties.SetAutomationId(square,
                    "MPW.Imaging.Resize." + handle);
                Canvas.SetLeft(square,screen.X-4.5);
                Canvas.SetTop(square,screen.Y-4.5);
                canvas.Children.Add(square);
            }
        }

        private bool TryResizeHandle(Point pointer,MVIImageViewport viewport,
            MVIImageOverlayItem item,out MVIImageResizeHandle handle)
        {
            handle = MVIImageResizeHandle.TopLeft;
            foreach (MVIImageResizeHandle candidate in Enum.GetValues(typeof(MVIImageResizeHandle)))
            {
                Point screen=SourceToCanvas(ResizeHandlePoint(item.Bounds,candidate),viewport);
                if (Math.Abs(pointer.X-screen.X)<=7 && Math.Abs(pointer.Y-screen.Y)<=7)
                { handle=candidate;return true; }
            }
            return false;
        }

        private bool BeginResize(Point pointer,MVIImageViewport viewport,bool secondary)
        {
            if (ActiveImage == null || !OverlayMatchesActiveImage()) return false;
            MVIImageOverlayItem selected = _selectedOverlayId.HasValue ?
                _overlays.Find(_selectedOverlayId.Value) : null;
            MVIImageResizeHandle handle;
            if (selected != null && !selected.IsLocked &&
                selected.Layer == MVIImageOverlayLayer.Roi &&
                _overlays.GetVisibleItems().Any(x=>x.Id==selected.Id) &&
                TryResizeHandle(pointer,viewport,selected,out handle))
            {
                _resizeStart=selected; _resizeHandle=handle;
                _resizing=true; _resizingSecondary=secondary;
                return true;
            }
            FGEPoint2D point = _viewTransform.DisplayToSource(new FGEPoint2D(
                viewport.ImageX(pointer.X),viewport.ImageY(pointer.Y)),
                ActiveImage.Width,ActiveImage.Height);
            if (point.X<0 || point.Y<0 ||
                point.X>=ActiveImage.Width || point.Y>=ActiveImage.Height)
                return false;
            IReadOnlyList<MVIImageOverlayItem> items=_overlays.GetVisibleItems();
            for (int i=items.Count-1;i>=0;i--)
            {
                MVIImageOverlayItem item=items[i];
                if (item.Layer==MVIImageOverlayLayer.Roi &&
                    IsHit(item,point,Math.Max(0.4,6.0/Math.Max(0.01,viewport.Scale))))
                {
                    SelectOverlay(item.Id);
                    return true;
                }
            }
            SelectOverlay(null);
            return false;
        }

        private void UpdateResize(Point pointer,MVIImageViewport viewport)
        {
            if (!_resizing || _resizeStart==null || ActiveImage==null) return;
            FGEPoint2D source = _viewTransform.DisplayToSource(new FGEPoint2D(
                viewport.ImageX(pointer.X),viewport.ImageY(pointer.Y)),
                ActiveImage.Width,ActiveImage.Height);
            MVIImageOverlayItem next=_resizeStart.WithResizedRoiHandle(
                _resizeHandle,source,ActiveImage.Width,ActiveImage.Height);
            if (_overlays.Find(_resizeStart.Id)==null)
            { _resizing=false; _resizeStart=null; return; }
            _overlays.Replace(next);
        }

        private void DrawMeasurementLabel(MVIImageOverlayItem item, Brush stroke, Point anchor, Canvas canvas)
        {
            string value = DescribeMeasurement(item);
            if (string.IsNullOrEmpty(value)) return;
            TextBlock label = new TextBlock { Text = value, Foreground = stroke,
                Background = Brushes.Black, FontSize = 12, IsHitTestVisible = false };
            Canvas.SetLeft(label,anchor.X + 4);
            Canvas.SetTop(label,anchor.Y + 4);
            canvas.Children.Add(label);
        }

        private void DrawLine(double x1, double y1, double x2, double y2, Brush stroke, double width, Canvas canvas)
        {
            Line line = new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                Stroke = stroke, StrokeThickness = width,
                IsHitTestVisible = false };
            canvas.Children.Add(line);
        }

        public MVIImageOverlayItem AddOverlay(MVIImageOverlayItem item)
        {
            VerifyAccess();
            if (ActiveImage == null) throw new InvalidOperationException("图像未加载。");
            return _overlays.Add(item);
        }

        /// <summary>用于API层创建多边形/检测缺陷/算法结果，无需将标注坐标转成WPF类型。</summary>
        public void SetOverlayLayerVisible(MVIImageOverlayLayer layer, bool visible)
        {
            VerifyAccess();
            _overlays.SetLayerVisible(layer, visible);
        }

        public void ClearOverlays(MVIImageOverlayLayer? layer = null)
        {
            VerifyAccess();
            _overlays.Clear(layer);
        }

        private FGERect2D PixelBounds(FIMPixelRegion region)
        {
            return new FGERect2D(region.Left, region.Top, region.Width, region.Height);
        }

        private void CompleteDraw()
        {
            if (!_region.HasValue || ActiveImage == null) return;
            FIMPixelRegion r = _region.Value;
            FGERect2D rect = PixelBounds(r);
            MVIImageOverlayKind kind = _selectionKind;
            MVIImageOverlayLayer layer = MVIImageOverlayLayer.Roi;
            FGEPoint2D[] points = null;
            string text = null;
            if (kind == MVIImageOverlayKind.DistanceMeasurement ||
                kind == MVIImageOverlayKind.HorizontalMeasurement ||
                kind == MVIImageOverlayKind.VerticalMeasurement ||
                kind == MVIImageOverlayKind.LineProfileMeasurement ||
                kind == MVIImageOverlayKind.ArrowAnnotation)
            {
                points = new[] { new FGEPoint2D(_selectionX + 0.5, _selectionY + 0.5),
                    new FGEPoint2D(_lastSelectionX + 0.5, _lastSelectionY + 0.5) };
                if (kind == MVIImageOverlayKind.HorizontalMeasurement)
                    points[1] = new FGEPoint2D(points[1].X,points[0].Y);
                if (kind == MVIImageOverlayKind.VerticalMeasurement)
                    points[1] = new FGEPoint2D(points[0].X,points[1].Y);
                if (kind == MVIImageOverlayKind.HorizontalMeasurement ||
                    kind == MVIImageOverlayKind.VerticalMeasurement)
                {
                    rect = new FGERect2D(Math.Min(points[0].X,points[1].X),
                        Math.Min(points[0].Y,points[1].Y),
                        Math.Abs(points[1].X-points[0].X),
                        Math.Abs(points[1].Y-points[0].Y));
                }
                layer = kind == MVIImageOverlayKind.ArrowAnnotation ?
                    MVIImageOverlayLayer.Annotation : MVIImageOverlayLayer.Measurement;
            }
            else if (kind == MVIImageOverlayKind.TextAnnotation ||
                     kind == MVIImageOverlayKind.PointAnnotation)
            {
                layer = MVIImageOverlayLayer.Annotation;
                text = kind == MVIImageOverlayKind.TextAnnotation ? _annotationText : null;
            }
            else if (kind == MVIImageOverlayKind.DefectBox) layer = MVIImageOverlayLayer.Defect;
            else if (kind == MVIImageOverlayKind.DiameterMeasurement ||
                kind == MVIImageOverlayKind.AreaMeasurement)
                layer = MVIImageOverlayLayer.Measurement;
            if (kind == MVIImageOverlayKind.CircleRoi ||
                kind == MVIImageOverlayKind.DiameterMeasurement)
            {
                double side = Math.Min(rect.Width, rect.Height);
                rect = new FGERect2D(rect.X + (rect.Width - side) / 2.0,
                    rect.Y + (rect.Height - side) / 2.0, side, side);
            }
            if (kind == MVIImageOverlayKind.RectangleRoi)
                _region = r;
            else _region = null;
            _overlays.Add(new MVIImageOverlayItem(kind, layer, rect, points, text));
            DrawSelection();
            RaiseSelectionChanged();
        }

        private bool TryImagePoint(Point display, out int x, out int y)
        {
            x = y = 0;
            FIMImageBuffer image = ActiveImage;
            if (image == null) return false;
            FGEPoint2D source=CanvasToSource(display);
            double xf=source.X,yf=source.Y;
            if (!IsFinite(xf) || !IsFinite(yf) || xf < 0 || yf < 0 ||
                xf >= image.Width || yf >= image.Height) return false;
            x = (int)Math.Floor(xf);
            y = (int)Math.Floor(yf);
            return true;
        }

        private void OnViewportSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ActiveImage != null && (_pendingFit || _viewport.IsFit)) FitToWindow();
            else ApplyTransform();
        }
        private void OnSecondaryViewportSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!IsSideBySideVisible || ActiveImage == null ||
                SecondaryViewportRoot.ActualWidth <= 0 || SecondaryViewportRoot.ActualHeight <= 0) return;
            if (_pendingFit || _viewport.IsFit) FitToWindow();
            else if (_secondaryViewport.IsFit && !_comparison.IsSyncViewEnabled)
            {
                FGESize2D size = _viewTransform.GetDisplaySize(ActiveImage.Width,ActiveImage.Height);
                _secondaryViewport.Fit((int)size.Width,(int)size.Height,
                    SecondaryViewportRoot.ActualWidth,SecondaryViewportRoot.ActualHeight);
                ApplyTransform();
            }
            else ApplyTransform();
        }

        /// <summary>在原图像素坐标执行形状命中，反向迭代保证顶层覆盖项优先。</summary>
        public MVIImageOverlayItem HitTestOverlay(double imageX, double imageY)
        {
            VerifyAccess();
            FGEPoint2D p = new FGEPoint2D(imageX, imageY);
            if (!OverlayMatchesActiveImage()) return null;
            IReadOnlyList<MVIImageOverlayItem> items = _overlays.GetVisibleItems();
            double tolerance = Math.Max(0.4, 6.0 / Math.Max(0.01, _viewport.Scale));
            for (int i = items.Count - 1; i >= 0; i--)
            {
                MVIImageOverlayItem item = items[i];
                if (IsHit(item, p, tolerance)) return item;
            }
            return null;
        }

        private static bool IsHit(MVIImageOverlayItem item, FGEPoint2D p, double tolerance)
        {
            if (item.Kind == MVIImageOverlayKind.PointAnnotation)
            {
                FGEPoint2D center = item.Points.Count > 0 ? item.Points[0] :
                    new FGEPoint2D(item.Bounds.X, item.Bounds.Y);
                return p.DistanceTo(center) <= tolerance;
            }
            if (item.Kind == MVIImageOverlayKind.PolygonRoi ||
                item.Kind == MVIImageOverlayKind.PolygonAnnotation ||
                (item.Kind == MVIImageOverlayKind.AreaMeasurement && item.Points.Count >= 3))
                return item.Points.Count >= 3 && FGEGeometry2D.PointInPolygon(p, item.Points);
            if (item.Kind == MVIImageOverlayKind.EllipseRoi ||
                item.Kind == MVIImageOverlayKind.CircleRoi ||
                item.Kind == MVIImageOverlayKind.DiameterMeasurement)
            {
                if (item.Bounds.Width <= 0 || item.Bounds.Height <= 0) return false;
                double cx = item.Bounds.X + item.Bounds.Width / 2.0;
                double cy = item.Bounds.Y + item.Bounds.Height / 2.0;
                double dx = (p.X - cx) / (item.Bounds.Width / 2.0);
                double dy = (p.Y - cy) / (item.Bounds.Height / 2.0);
                return dx * dx + dy * dy <= 1.0;
            }
            if (item.Points.Count >= 2)
            {
                for (int i = 1; i < item.Points.Count; i++)
                {
                    if (DistanceToSegment(p, item.Points[i-1], item.Points[i]) <= tolerance)
                        return true;
                }
                return false;
            }
            return item.Bounds.Contains(p);
        }

        private static double DistanceToSegment(FGEPoint2D p, FGEPoint2D a, FGEPoint2D b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double lengthSquared = dx * dx + dy * dy;
            if (lengthSquared <= 0) return p.DistanceTo(a);
            double t = Math.Max(0, Math.Min(1,
                ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lengthSquared));
            double x = a.X + t * dx, y = a.Y + t * dy;
            double px = p.X - x, py = p.Y - y;
            return Math.Sqrt(px * px + py * py);
        }

        private void UpdateMovedOverlay(Point pointer)
        {
            if (!_moving || _movingOverlay == null || ActiveImage == null) return;
            FGEPoint2D current = CanvasToSource(pointer);
            double dx = current.X - _moveAnchor.X,dy = current.Y - _moveAnchor.Y;
            // 移动鼠标不能把图中标注悄悄移入无限画布；保持当前Bounds在图像内。
            dx = Math.Max(-_movingOverlay.Bounds.Left,
                Math.Min(ActiveImage.Width - _movingOverlay.Bounds.Right, dx));
            dy = Math.Max(-_movingOverlay.Bounds.Top,
                Math.Min(ActiveImage.Height - _movingOverlay.Bounds.Bottom, dy));
            _overlays.Replace(_movingOverlay.Translate(dx, dy));
        }

        private static MVIImageOverlayKind GetDrawingKind(MVIImageToolMode mode)
        {
            switch (mode)
            {
                case MVIImageToolMode.RectangleRoi: return MVIImageOverlayKind.RectangleRoi;
                case MVIImageToolMode.CircleRoi: return MVIImageOverlayKind.CircleRoi;
                case MVIImageToolMode.EllipseRoi: return MVIImageOverlayKind.EllipseRoi;
                case MVIImageToolMode.Distance: return MVIImageOverlayKind.DistanceMeasurement;
                case MVIImageToolMode.HorizontalDistance: return MVIImageOverlayKind.HorizontalMeasurement;
                case MVIImageToolMode.VerticalDistance: return MVIImageOverlayKind.VerticalMeasurement;
                case MVIImageToolMode.Diameter: return MVIImageOverlayKind.DiameterMeasurement;
                case MVIImageToolMode.Area: return MVIImageOverlayKind.AreaMeasurement;
                case MVIImageToolMode.LineProfile: return MVIImageOverlayKind.LineProfileMeasurement;
                case MVIImageToolMode.Arrow: return MVIImageOverlayKind.ArrowAnnotation;
                case MVIImageToolMode.Point: return MVIImageOverlayKind.PointAnnotation;
                case MVIImageToolMode.Text: return MVIImageOverlayKind.TextAnnotation;
                case MVIImageToolMode.DefectBox: return MVIImageOverlayKind.DefectBox;
                default: throw new NotSupportedException("此工具尚未支持鼠标绘制：" + mode);
            }
        }

        private void OnSecondaryMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsSideBySideVisible || _session.Result == null) return;
            if (_toolMode == MVIImageToolMode.ResizeOverlay &&
                BeginResize(e.GetPosition(SecondaryViewportRoot),_secondaryViewport,true))
            {
                SecondaryViewportRoot.CaptureMouse(); e.Handled=true; return;
            }
            _secondaryDragging = true;
            _secondaryDragStart = e.GetPosition(SecondaryViewportRoot);
            SecondaryViewportRoot.CaptureMouse();
            e.Handled = true;
        }
        private void OnSecondaryMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_resizing && _resizingSecondary)
            {
                UpdateResize(e.GetPosition(SecondaryViewportRoot),_secondaryViewport);
                _resizing=false; _resizeStart=null;
                RaiseSelectionChanged();
            }
            _secondaryDragging = false;
            if (SecondaryViewportRoot.IsMouseCaptured) SecondaryViewportRoot.ReleaseMouseCapture();
            e.Handled = true;
        }
        private void OnSecondaryMouseMove(object sender, MouseEventArgs e)
        {
            if (!IsSideBySideVisible || _session.Result == null) return;
            Point pointer = e.GetPosition(SecondaryViewportRoot);
            if (_resizing && _resizingSecondary && e.LeftButton == MouseButtonState.Pressed)
                UpdateResize(pointer,_secondaryViewport);
            else if (_secondaryDragging && e.LeftButton == MouseButtonState.Pressed)
            {
                _secondaryViewport.Pan(pointer.X-_secondaryDragStart.X,
                    pointer.Y-_secondaryDragStart.Y);
                _secondaryDragStart = pointer;
                SynchronizePrimaryFromSecondary();
                ApplyTransform();
            }
            FIMImageBuffer result = _session.Result;
            FGEPoint2D source = _viewTransform.DisplayToSource(new FGEPoint2D(
                _secondaryViewport.ImageX(pointer.X),_secondaryViewport.ImageY(pointer.Y)),
                result.Width,result.Height);
            if (source.X >= 0 && source.Y >= 0 &&
                source.X < result.Width && source.Y < result.Height)
            {
                int x = (int)Math.Floor(source.X), y = (int)Math.Floor(source.Y);
                PixelStatus.Text = "结果图 · (" + x + "," + y + ") · Gray=" +
                    result.GetGrayValue(x,y).ToString("0.###") +
                    " · " + _secondaryViewport.Scale.ToString("0.##") + "×";
                EventHandler handler = PixelInspected;
                if (handler != null) handler(this,EventArgs.Empty);
            }
        }
        private void OnSecondaryMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!IsSideBySideVisible || _session.Result == null) return;
            Point pointer = e.GetPosition(SecondaryViewportRoot);
            _secondaryViewport.ZoomAt(e.Delta>0?1.2:1.0/1.2,pointer.X,pointer.Y);
            SynchronizePrimaryFromSecondary();
            ApplyTransform();
            e.Handled=true;
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (ActiveImage == null) return;
            Point pointer = e.GetPosition(ViewportRoot);
            int x,y;
            if (_toolMode == MVIImageToolMode.ResizeOverlay)
            {
                if (BeginResize(pointer,_viewport,false))
                { ViewportRoot.CaptureMouse(); e.Handled=true; }
                return;
            }
            if (_toolMode == MVIImageToolMode.PolygonRoi ||
                _toolMode == MVIImageToolMode.PolygonArea ||
                _toolMode == MVIImageToolMode.Angle)
            {
                if (!TryImagePoint(pointer, out x, out y)) return;
                _region = null;
                FGEPoint2D p = new FGEPoint2D(x + 0.5, y + 0.5);
                if (_polygonDraft.Count == 0 ||
                    !_polygonDraft[_polygonDraft.Count - 1].Equals(p))
                    _polygonDraft.Add(p);
                if (((_toolMode == MVIImageToolMode.PolygonRoi ||
                       _toolMode == MVIImageToolMode.PolygonArea) && e.ClickCount >= 2) ||
                    (_toolMode == MVIImageToolMode.Angle && _polygonDraft.Count == 3))
                    CompletePointDraft(_toolMode);
                else { _draftHover = null; DrawSelection(); }
                e.Handled = true;
                return;
            }
            if (_toolMode == MVIImageToolMode.MoveOverlay ||
                _toolMode == MVIImageToolMode.DeleteOverlay)
            {
                if (!TryImagePoint(pointer,out x,out y)) return;
                MVIImageOverlayItem hit = HitTestOverlay(x + 0.5, y + 0.5);
                if (hit == null) return;
                if (hit.IsLocked)
                {
                    SelectOverlay(hit.Id);
                    PixelStatus.Text="此对象已锁定；请先在区域页解锁。";
                    e.Handled=true; return;
                }
                if (_toolMode == MVIImageToolMode.DeleteOverlay)
                {
                    _overlays.Remove(hit.Id);
                    _region = null;
                    DrawSelection();
                }
                else
                {
                    _moving = true;
                    _movingOverlay = hit;
                    _moveAnchor = CanvasToSource(pointer);
                    _region = null;
                    DrawSelection();
                    ViewportRoot.CaptureMouse();
                }
                RaiseSelectionChanged();
                e.Handled = true;
                return;
            }
            if (_toolMode != MVIImageToolMode.Navigate)
            {
                if (!TryImagePoint(pointer, out x, out y)) return;
                _selectionKind = GetDrawingKind(_toolMode);
                _selectionX = _lastSelectionX = x;
                _selectionY = _lastSelectionY = y;
                _region = new FIMPixelRegion(x, y, x, y);
                _selecting = true;
                DrawSelection();
            }
            else
            {
                _dragging = true;
                _startPointer = pointer;
            }
            ViewportRoot.CaptureMouse();
            e.Handled = true;
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_resizing && !_resizingSecondary)
            {
                UpdateResize(e.GetPosition(ViewportRoot),_viewport);
                _resizing=false; _resizeStart=null;
                RaiseSelectionChanged();
            }
            if (_selecting)
            {
                Point pointer = e.GetPosition(ViewportRoot);
                UpdateDraftSelection(pointer);
                CompleteDraw();
            }
            if (_moving) UpdateMovedOverlay(e.GetPosition(ViewportRoot));
            _dragging = _selecting = _moving = false;
            _movingOverlay = null;
            if (ViewportRoot.IsMouseCaptured) ViewportRoot.ReleaseMouseCapture();
        }

        private void UpdateDraftSelection(Point pointer)
        {
            FIMImageBuffer image = ActiveImage;
            if (image == null) return;
            FGEPoint2D source=CanvasToSource(pointer);
            double imageX=source.X,imageY=source.Y;
            if (!IsFinite(imageX) || !IsFinite(imageY)) return;
            int x = (int)Math.Max(0, Math.Min(image.Width - 1, Math.Floor(imageX)));
            int y = (int)Math.Max(0, Math.Min(image.Height - 1, Math.Floor(imageY)));
            _lastSelectionX = x; _lastSelectionY = y;
            _region = new FIMPixelRegion(Math.Min(_selectionX,x), Math.Min(_selectionY,y),
                Math.Max(_selectionX,x), Math.Max(_selectionY,y));
            DrawSelection();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            Point pointer = e.GetPosition(ViewportRoot);
            FIMImageBuffer image = ActiveImage;
            if (image == null) return;
            if (_dragging && e.LeftButton == MouseButtonState.Pressed)
            {
                _viewport.Pan(pointer.X - _startPointer.X, pointer.Y - _startPointer.Y);
                _startPointer = pointer;
                ApplyTransform();
            }
            else if (_selecting && e.LeftButton == MouseButtonState.Pressed)
                UpdateDraftSelection(pointer);
            else if (_moving && e.LeftButton == MouseButtonState.Pressed)
                UpdateMovedOverlay(pointer);
            else if (_resizing && !_resizingSecondary && e.LeftButton == MouseButtonState.Pressed)
                UpdateResize(pointer,_viewport);
            int px,py;
            if (TryImagePoint(pointer,out px,out py))
            {
                double gray = image.GetGrayValue(px,py);
                PixelStatus.Text = string.Format("{0}×{1} · ({2},{3}) · Gray={4:0.###} · {5:0.##}×",
                    image.Width,image.Height,px,py,gray,_viewport.Scale);
                if (_polygonDraft.Count > 0)
                {
                    _draftHover = new FGEPoint2D(px + 0.5, py + 0.5);
                    DrawDraftPolyline();
                }
                EventHandler handler = PixelInspected;
                if (handler != null) handler(this,EventArgs.Empty);
            }
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (ActiveImage == null) return;
            Point p = e.GetPosition(ViewportRoot);
            _viewport.ZoomAt(e.Delta > 0 ? 1.2 : 1.0 / 1.2, p.X, p.Y);
            ApplyTransform();
            e.Handled = true;
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (!_dragging && !_selecting && !_resizing && ActiveImage != null)
                PixelStatus.Text = ActiveImage.Width + "×" + ActiveImage.Height +
                    " · " + ActiveImage.PixelFormat + " · " + _viewport.Scale.ToString("0.##") + "×";
        }

        private void RaiseSelectionChanged()
        {
            EventHandler handler = SelectionChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }
}
