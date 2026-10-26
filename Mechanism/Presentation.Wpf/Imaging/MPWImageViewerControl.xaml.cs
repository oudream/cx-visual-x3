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
        private readonly MVIImageTransformState _viewTransform = new MVIImageTransformState();
        private readonly MVIImageComparisonState _comparison = new MVIImageComparisonState();
        private readonly MVIImageOverlayStore _overlays = new MVIImageOverlayStore();
        private FIMImageBuffer _overlayOriginal;
        private BitmapSource _bitmap;
        private FIMImageBuffer _shownImage;
        private bool _dragging, _selecting, _moving, _pendingFit = true;
        private MVIImageOverlayItem _movingOverlay;
        private FGEPoint2D _moveAnchor;
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
                _dragging = _selecting = _moving = false;
                _movingOverlay = null;
                _polygonDraft.Clear();
                _draftHover = null;
                DrawSelection();
            }
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
        public event EventHandler SelectionChanged;
        public event EventHandler PixelInspected;

        public void ShowOriginal(FIMImageBuffer image)
        {
            VerifyAccess();
            if (image == null) throw new ArgumentNullException("image");
            _pendingFit = true;
            _region = null;
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
            _displayState.Reset();
            _viewTransform.Reset();
            _comparison.Reset();
            _polygonDraft.Clear();
            _overlays.Clear();
            _overlayOriginal = null;
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
            int width = (int)Math.Ceiling(ViewportRoot.ActualWidth);
            int height = (int)Math.Ceiling(ViewportRoot.ActualHeight);
            if (width < 1 || height < 1 || (long)width * height * 4 > 512L * 1024 * 1024)
                throw new InvalidOperationException("视口尚未布置或导出尺寸过大。");
            RenderTargetBitmap snapshot = new RenderTargetBitmap(width, height, 96, 96,
                PixelFormats.Pbgra32);
            snapshot.Render(ViewportRoot);
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
                EmptyText.Visibility = Visibility.Visible;
                SelectedRoi.Visibility = Visibility.Collapsed;
                OverlayCanvas.Children.Clear();
                PixelStatus.Text = "未载入图像";
                return;
            }

            if (_comparison.Mode == MVIImageComparisonMode.Blend)
            {
                FIMImageBuffer first = _session.Original, second = _session.Result;
                if (first == null || second == null || first.Width != second.Width ||
                    first.Height != second.Height || first.PixelFormat != second.PixelFormat)
                {
                    _comparison.SetMode(MVIImageComparisonMode.Single);
                    return;
                }
                image = FIMImageBlend.Blend(first, second, _comparison.BlendRatio);
            }
            else if (_comparison.Mode == MVIImageComparisonMode.SideBySide)
                throw new NotSupportedException("并排比较尚未完成真实双视图交互，禁止当成可用模式。");
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
            PixelImage.Width = display.Width;
            PixelImage.Height = display.Height;
            PixelImage.Source = bitmap;
            EmptyText.Visibility = Visibility.Collapsed;
            PixelStatus.Text = image.Width + "×" + image.Height + " " + image.PixelFormat +
                (_comparison.Mode == MVIImageComparisonMode.Blend ?
                " · 叠合 " + _comparison.BlendRatio.ToString("0.##") : "");
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
        {
            FIMImageBuffer active = ActiveImage;
            if (active == null) return new Point(0,0);
            FGEPoint2D display = _viewTransform.SourceToDisplay(source,active.Width,active.Height);
            return new Point(_viewport.DisplayX(display.X),_viewport.DisplayY(display.Y));
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

        private void ApplyTransform()
        {
            ImageTransform.Matrix = new Matrix(_viewport.Scale, 0, 0, _viewport.Scale,
                _viewport.OffsetX, _viewport.OffsetY);
            DrawSelection();
            DrawOverlays();
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
                else AddPolygonRoi(_polygonDraft.ToArray());
                RaiseSelectionChanged();
            }
            _polygonDraft.Clear();
            _draftHover = null;
            DrawDraftPolyline();
        }

        /// <summary>所有叠加对象从原图坐标重绘；屏幕缩放/平移只改变投影，不修改ROI值。</summary>
        private void DrawOverlays()
        {
            VerifyAccess();
            OverlayCanvas.Children.Clear();
            if (!OverlayMatchesActiveImage()) return;
            foreach (MVIImageOverlayItem item in _overlays.GetVisibleItems())
            {
                Color color = Color.FromArgb((byte)((uint)item.Argb >> 24),
                    (byte)((uint)item.Argb >> 16), (byte)((uint)item.Argb >> 8),
                    (byte)item.Argb);
                Brush stroke = new SolidColorBrush(color);
                double width = item.LineWidth;
                Point cornerA = SourceToCanvas(new FGEPoint2D(item.Bounds.Left,item.Bounds.Top));
                Point cornerB = SourceToCanvas(new FGEPoint2D(item.Bounds.Right,item.Bounds.Bottom));
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
                    OverlayCanvas.Children.Add(ellipse);
                }
                else if (item.Kind == MVIImageOverlayKind.RectangleRoi ||
                         item.Kind == MVIImageOverlayKind.RectangleAnnotation ||
                         item.Kind == MVIImageOverlayKind.DefectBox ||
                         item.Kind == MVIImageOverlayKind.AreaMeasurement)
                {
                    Rectangle rectangle = new Rectangle { Width = Math.Max(0, right - left),
                        Height = Math.Max(0, bottom - top), Stroke = stroke,
                        StrokeThickness = width, Fill = Brushes.Transparent,
                        IsHitTestVisible = false };
                    Canvas.SetLeft(rectangle, left); Canvas.SetTop(rectangle, top);
                    OverlayCanvas.Children.Add(rectangle);
                }
                else if (item.Kind == MVIImageOverlayKind.PolygonRoi ||
                         item.Kind == MVIImageOverlayKind.PolygonAnnotation)
                {
                    Polygon polygon = new Polygon { Stroke = stroke, StrokeThickness = width,
                        Fill = Brushes.Transparent, IsHitTestVisible = false };
                    foreach (FGEPoint2D p in item.Points)
                        polygon.Points.Add(SourceToCanvas(p));
                    OverlayCanvas.Children.Add(polygon);
                }
                else if (item.Kind == MVIImageOverlayKind.PointAnnotation)
                {
                    FGEPoint2D point = item.Points.Count > 0 ? item.Points[0] :
                        new FGEPoint2D(item.Bounds.Left, item.Bounds.Top);
                    Point position = SourceToCanvas(point);
                    double px=position.X, py=position.Y;
                    DrawLine(px - 5, py, px + 5, py, stroke, width);
                    DrawLine(px, py - 5, px, py + 5, stroke, width);
                }
                else if (item.Kind == MVIImageOverlayKind.TextAnnotation)
                {
                    TextBlock text = new TextBlock { Text = item.Text,
                        Foreground = stroke, FontSize = 14, IsHitTestVisible = false };
                    Canvas.SetLeft(text, left); Canvas.SetTop(text, top);
                    OverlayCanvas.Children.Add(text);
                }
                else if (item.Points.Count >= 2)
                {
                    for (int i = 1; i < item.Points.Count; i++)
                    {
                        FGEPoint2D a = item.Points[i - 1], b = item.Points[i];
                        Point first=SourceToCanvas(a),last=SourceToCanvas(b);
                        DrawLine(first.X,first.Y,last.X,last.Y,stroke,width);
                    }
                    if (item.Kind == MVIImageOverlayKind.ArrowAnnotation)
                    {
                        FGEPoint2D a = item.Points[0], b = item.Points[item.Points.Count - 1];
                        Point first=SourceToCanvas(a),last=SourceToCanvas(b);
                        double x1=first.X,y1=first.Y,x2=last.X,y2=last.Y;
                        double angle = Math.Atan2(y2 - y1, x2 - x1);
                        DrawLine(x2, y2, x2 - 10 * Math.Cos(angle - Math.PI / 6),
                            y2 - 10 * Math.Sin(angle - Math.PI / 6), stroke, width);
                        DrawLine(x2, y2, x2 - 10 * Math.Cos(angle + Math.PI / 6),
                            y2 - 10 * Math.Sin(angle + Math.PI / 6), stroke, width);
                    }
                    if (item.Kind == MVIImageOverlayKind.AngleMeasurement && item.Points.Count >= 3)
                    {
                        double angle = FIMImageMeasurement.GetAngleDegrees(
                            item.Points[0],item.Points[1],item.Points[2]);
                        Point vertex = SourceToCanvas(item.Points[1]);
                        TextBlock label = new TextBlock { Text = angle.ToString("0.##") + "°",
                            Foreground = stroke, Background = Brushes.Black,
                            FontSize = 12, IsHitTestVisible = false };
                        Canvas.SetLeft(label, vertex.X + 5); Canvas.SetTop(label,vertex.Y + 5);
                        OverlayCanvas.Children.Add(label);
                    }
                    if (item.Kind == MVIImageOverlayKind.DistanceMeasurement)
                    {
                        double value = item.Points[0].DistanceTo(item.Points[1]);
                        TextBlock label = new TextBlock { Text = value.ToString("0.###") + " px",
                            Foreground = stroke, Background = Brushes.Black,
                            FontSize = 12, IsHitTestVisible = false };
                        Point where=SourceToCanvas(item.Points[1]);
                        Canvas.SetLeft(label,where.X+4);
                        Canvas.SetTop(label,where.Y+4);
                        OverlayCanvas.Children.Add(label);
                    }
                }
            }
        }

        private void DrawLine(double x1, double y1, double x2, double y2, Brush stroke, double width)
        {
            Line line = new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                Stroke = stroke, StrokeThickness = width,
                IsHitTestVisible = false };
            OverlayCanvas.Children.Add(line);
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
                kind == MVIImageOverlayKind.ArrowAnnotation)
            {
                points = new[] { new FGEPoint2D(_selectionX + 0.5, _selectionY + 0.5),
                    new FGEPoint2D(_lastSelectionX + 0.5, _lastSelectionY + 0.5) };
                layer = kind == MVIImageOverlayKind.DistanceMeasurement ?
                    MVIImageOverlayLayer.Measurement : MVIImageOverlayLayer.Annotation;
            }
            else if (kind == MVIImageOverlayKind.TextAnnotation ||
                     kind == MVIImageOverlayKind.PointAnnotation)
            {
                layer = MVIImageOverlayLayer.Annotation;
                text = kind == MVIImageOverlayKind.TextAnnotation ? _annotationText : null;
            }
            else if (kind == MVIImageOverlayKind.DefectBox) layer = MVIImageOverlayLayer.Defect;
            if (kind == MVIImageOverlayKind.CircleRoi)
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
                item.Kind == MVIImageOverlayKind.PolygonAnnotation)
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
                case MVIImageToolMode.Arrow: return MVIImageOverlayKind.ArrowAnnotation;
                case MVIImageToolMode.Point: return MVIImageOverlayKind.PointAnnotation;
                case MVIImageToolMode.Text: return MVIImageOverlayKind.TextAnnotation;
                case MVIImageToolMode.DefectBox: return MVIImageOverlayKind.DefectBox;
                default: throw new NotSupportedException("此工具尚未支持鼠标绘制：" + mode);
            }
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (ActiveImage == null) return;
            Point pointer = e.GetPosition(ViewportRoot);
            int x,y;
            if (_toolMode == MVIImageToolMode.PolygonRoi ||
                _toolMode == MVIImageToolMode.Angle)
            {
                if (!TryImagePoint(pointer, out x, out y)) return;
                _region = null;
                FGEPoint2D p = new FGEPoint2D(x + 0.5, y + 0.5);
                if (_polygonDraft.Count == 0 ||
                    !_polygonDraft[_polygonDraft.Count - 1].Equals(p))
                    _polygonDraft.Add(p);
                if ((_toolMode == MVIImageToolMode.PolygonRoi && e.ClickCount >= 2) ||
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
            if (!_dragging && !_selecting && ActiveImage != null)
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
