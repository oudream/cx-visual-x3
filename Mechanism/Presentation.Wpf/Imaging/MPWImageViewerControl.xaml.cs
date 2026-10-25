using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CxVisual.Foundation.Imaging;
using CxVisual.Mechanism.Visual;
using Microsoft.Win32;

namespace CxVisual.Mechanism.Presentation.Wpf.Imaging
{
    /// <summary>不依赖专业SDK的WPF原生二维Viewer。复用FIM图像与MVI会话及视口。</summary>
    public partial class MPWImageViewerControl : UserControl
    {
        private readonly MVIImageSession _session = new MVIImageSession();
        private readonly MVIImageViewport _viewport = new MVIImageViewport();
        private BitmapSource _bitmap;
        private FIMImageBuffer _shownImage;
        private bool _dragging, _selecting, _pendingFit = true;
        private Point _startPointer;
        private int _selectionX, _selectionY;
        private FIMPixelRegion? _region;
        private double? _windowLow, _windowHigh;

        public MPWImageViewerControl()
        {
            InitializeComponent();
            _session.Changed += OnSessionChanged;
        }

        public MVIImageSession Session { get { return _session; } }
        public FIMImageBuffer ActiveImage { get { return _session.ActiveImage; } }
        public FIMPixelRegion? SelectedRegion { get { return _region; } }
        public bool IsRegionTool { get; set; }
        public event EventHandler SelectionChanged;
        public event EventHandler PixelInspected;

        public void ShowOriginal(FIMImageBuffer image)
        {
            VerifyAccess();
            if (image == null) throw new ArgumentNullException("image");
            _pendingFit = true;
            _region = null;
            _windowLow = _windowHigh = null;
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
            _windowLow = _windowHigh = null;
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
            _windowLow = low;
            _windowHigh = high;
            RefreshDisplay();
        }

        public void FitToWindow()
        {
            VerifyAccess();
            FIMImageBuffer image = ActiveImage;
            if (image == null) return;
            if (ViewportRoot.ActualWidth <= 0 || ViewportRoot.ActualHeight <= 0)
            { _pendingFit = true; return; }
            _viewport.Fit(image.Width, image.Height, ViewportRoot.ActualWidth, ViewportRoot.ActualHeight);
            _pendingFit = false;
            ApplyTransform();
        }

        public void ActualSize()
        {
            VerifyAccess();
            FIMImageBuffer image = ActiveImage;
            if (image == null) return;
            if (ViewportRoot.ActualWidth <= 0 || ViewportRoot.ActualHeight <= 0) return;
            _viewport.ActualSize(image.Width, image.Height, ViewportRoot.ActualWidth, ViewportRoot.ActualHeight);
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
                PixelStatus.Text = "未载入图像";
                return;
            }

            FIMImageBuffer display = _windowLow.HasValue ?
                FIMImageOperations.WindowToGray8(image, _windowLow.Value, _windowHigh.Value) : image;
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
            PixelImage.Width = image.Width;
            PixelImage.Height = image.Height;
            PixelImage.Source = bitmap;
            EmptyText.Visibility = Visibility.Collapsed;
            PixelStatus.Text = image.Width + "×" + image.Height + " " + image.PixelFormat;
            if (dimensionsChanged || _pendingFit) FitToWindow();
            else ApplyTransform();
        }

        private void ApplyTransform()
        {
            ImageTransform.Matrix = new Matrix(_viewport.Scale, 0, 0, _viewport.Scale,
                _viewport.OffsetX, _viewport.OffsetY);
            DrawSelection();
        }

        private void DrawSelection()
        {
            if (!_region.HasValue || ActiveImage == null)
            { SelectedRoi.Visibility = Visibility.Collapsed; return; }
            FIMPixelRegion region = _region.Value;
            double left = _viewport.DisplayX(region.Left), top = _viewport.DisplayY(region.Top);
            double right = _viewport.DisplayX(region.Right + 1.0);
            double bottom = _viewport.DisplayY(region.Bottom + 1.0);
            Canvas.SetLeft(SelectedRoi, left);
            Canvas.SetTop(SelectedRoi, top);
            SelectedRoi.Width = Math.Max(0, right - left);
            SelectedRoi.Height = Math.Max(0, bottom - top);
            SelectedRoi.Visibility = Visibility.Visible;
        }

        private bool TryImagePoint(Point display, out int x, out int y)
        {
            x = y = 0;
            FIMImageBuffer image = ActiveImage;
            if (image == null) return false;
            double xf = _viewport.ImageX(display.X), yf = _viewport.ImageY(display.Y);
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

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            Point p = e.GetPosition(ViewportRoot);
            if (ActiveImage == null) return;
            int x, y;
            if (IsRegionTool && TryImagePoint(p, out x, out y))
            {
                _selecting = true;
                _selectionX = x; _selectionY = y;
                _region = new FIMPixelRegion(x, y, x, y);
                DrawSelection();
            }
            else
            {
                _dragging = true;
                _startPointer = p;
            }
            ViewportRoot.CaptureMouse();
            e.Handled = true;
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_selecting) RaiseSelectionChanged();
            _dragging = _selecting = false;
            if (ViewportRoot.IsMouseCaptured) ViewportRoot.ReleaseMouseCapture();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            Point p = e.GetPosition(ViewportRoot);
            FIMImageBuffer image = ActiveImage;
            if (image == null) return;
            if (_dragging && e.LeftButton == MouseButtonState.Pressed)
            {
                _viewport.Pan(p.X - _startPointer.X, p.Y - _startPointer.Y);
                _startPointer = p;
                ApplyTransform();
            }
            else if (_selecting && e.LeftButton == MouseButtonState.Pressed)
            {
                int x = (int)Math.Max(0, Math.Min(image.Width - 1, Math.Floor(_viewport.ImageX(p.X))));
                int y = (int)Math.Max(0, Math.Min(image.Height - 1, Math.Floor(_viewport.ImageY(p.Y))));
                _region = new FIMPixelRegion(Math.Min(_selectionX, x), Math.Min(_selectionY, y),
                    Math.Max(_selectionX, x), Math.Max(_selectionY, y));
                DrawSelection();
            }
            int px, py;
            if (TryImagePoint(p, out px, out py))
            {
                double gray = image.GetGrayValue(px, py);
                PixelStatus.Text = string.Format("{0}×{1} · ({2}, {3}) · Gray={4:0.###} · {5:0.##}×",
                    image.Width, image.Height, px, py, gray, _viewport.Scale);
                EventHandler handler = PixelInspected;
                if (handler != null) handler(this, EventArgs.Empty);
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
