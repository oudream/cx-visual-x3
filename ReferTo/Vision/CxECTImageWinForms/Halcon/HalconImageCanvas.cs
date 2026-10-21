using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using CxECTImageWinForms.Models;
using CxECTImageWinForms.Models.Overlays;
using CxECTImageWinForms.Services;
using HalconDotNet;
using HalconDriverModule;

namespace CxECTImageWinForms.Halcon
{
    public enum ImageCanvasNavigationKind
    {
        Zoom,
        Pan
    }

    public sealed class ImageCanvasNavigationEventArgs : EventArgs
    {
        public ImageCanvasNavigationEventArgs(ImageCanvasNavigationKind kind, double deltaColumn, double deltaRow, double zoomFactor)
        {
            Kind = kind;
            DeltaColumn = deltaColumn;
            DeltaRow = deltaRow;
            ZoomFactor = zoomFactor;
        }

        public ImageCanvasNavigationKind Kind { get; private set; }

        public double DeltaColumn { get; private set; }

        public double DeltaRow { get; private set; }

        public double ZoomFactor { get; private set; }
    }

    public sealed class PixelInfoChangedEventArgs : EventArgs
    {
        public PixelInfoChangedEventArgs(bool isInsideImage, ImagePoint sourcePoint, int grayValue)
        {
            IsInsideImage = isInsideImage;
            SourcePoint = sourcePoint;
            GrayValue = grayValue;
        }

        public bool IsInsideImage { get; private set; }

        public ImagePoint SourcePoint { get; private set; }

        public int GrayValue { get; private set; }
    }

    public sealed class RectangleRoiDrawnEventArgs : EventArgs
    {
        public RectangleRoiDrawnEventArgs(Guid id, ImageToolMode mode, ImageRectangle bounds, double grayValue)
        {
            Id = id;
            Mode = mode;
            Bounds = bounds;
            GrayValue = grayValue;
        }

        public Guid Id { get; private set; }

        public ImageToolMode Mode { get; private set; }

        public ImageRectangle Bounds { get; private set; }

        public double GrayValue { get; private set; }
    }

    public sealed class MeasurementDrawnEventArgs : EventArgs
    {
        public MeasurementDrawnEventArgs(ImageToolMode mode, IList<ImagePoint> points, double pixelValue)
        {
            Mode = mode;
            Points = points;
            PixelValue = pixelValue;
        }

        public ImageToolMode Mode { get; private set; }

        public IList<ImagePoint> Points { get; private set; }

        public double PixelValue { get; private set; }
    }

    public sealed class MeasurementProgressChangedEventArgs : EventArgs
    {
        public MeasurementProgressChangedEventArgs(ImageToolMode mode, int selectedPointCount, int requiredPointCount)
        {
            Mode = mode;
            SelectedPointCount = selectedPointCount;
            RequiredPointCount = requiredPointCount;
        }

        public ImageToolMode Mode { get; private set; }

        public int SelectedPointCount { get; private set; }

        public int RequiredPointCount { get; private set; }
    }

    public sealed class OverlaySelectionChangedEventArgs : EventArgs
    {
        public OverlaySelectionChangedEventArgs(Guid? overlayId)
        {
            OverlayId = overlayId;
        }

        public Guid? OverlayId { get; private set; }
    }

    public sealed class OverlayGeometryChangedEventArgs : EventArgs
    {
        public OverlayGeometryChangedEventArgs(Guid overlayId, ImageRectangle bounds, IList<ImagePoint> points)
        {
            OverlayId = overlayId;
            Bounds = bounds;
            Points = points;
        }

        public Guid OverlayId { get; private set; }

        public ImageRectangle Bounds { get; private set; }

        public IList<ImagePoint> Points { get; private set; }
    }

    public sealed class OverlayDeleteRequestedEventArgs : EventArgs
    {
        public OverlayDeleteRequestedEventArgs(Guid overlayId)
        {
            OverlayId = overlayId;
        }

        public Guid OverlayId { get; private set; }
    }

    public sealed class AnnotationDrawnEventArgs : EventArgs
    {
        public AnnotationDrawnEventArgs(ImageToolMode mode, IList<ImagePoint> points, ImageRectangle bounds, string text)
        {
            Mode = mode;
            Points = points;
            Bounds = bounds;
            Text = text;
        }

        public ImageToolMode Mode { get; private set; }

        public IList<ImagePoint> Points { get; private set; }

        public ImageRectangle Bounds { get; private set; }

        public string Text { get; private set; }
    }

    /// <summary>
    /// HALCON 图窗适配层。它复用旧工具类的显示能力，但由新控件统一管理交互状态。
    /// </summary>
    public sealed class HalconImageCanvas : IDisposable
    {
        private readonly HWindowControl _windowControl;
        private readonly HalconWindowTools _windowTools;
        private readonly HWindowControlView _windowControlView;
        private readonly ImageStatisticsService _statisticsService = new ImageStatisticsService();
        private OverlayColorPalette _overlayColors = new OverlayColorPalette();
        private bool _disposed;
        private bool _hasImage;
        private bool _supportsWindowLevel;
        private bool _isPanning;
        private double _minZoom = ImageViewportState.DefaultMinZoom;
        private double _maxZoom = ImageViewportState.DefaultMaxZoom;
        private double _lastMouseRow;
        private double _lastMouseColumn;
        private int _lastMeasurementPreviewTick;
        private int _lastOverlayEditPreviewTick;
        private ImageToolMode _toolMode;
        // 点、多边形、箭头和文字工具的完成回调由鼠标松开统一提交，避免按下即弹出输入框。
        private AnnotationDrawnEventArgs _pendingAnnotationDrawn;
        // 临时框选（如自动取窗）只传回坐标，不写入 ROI/测量库。
        private Action<ImageRectangle> _rectangleSelectionCompleted;
        private readonly List<ImagePoint> _measurementPoints = new List<ImagePoint>();
        private readonly List<ImageOverlayItem> _editableOverlays = new List<ImageOverlayItem>();
        private Guid? _selectedOverlayId;
        private OverlayEditSession _overlayEditSession;
        // 标尺仅是显示层，不进入 ROI、测量或标注的持久化模型。
        private bool _isImageRulerVisible;
        private double? _millimetersPerPixelX;
        private double? _millimetersPerPixelY;
        // 叠加模型始终保存原图坐标；图窗显示坐标由此状态统一换算。
        private ImageTransformState _coordinateTransform = new ImageTransformState();
        private ImageSize _sourceImageSize;

        public HalconImageCanvas(Control host)
        {
            if (host == null)
            {
                throw new ArgumentNullException("host");
            }

            _windowControl = new HWindowControl
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(13, 15, 18)
            };
            host.Controls.Add(_windowControl);

            _windowTools = new HalconWindowTools(_windowControl, 14, 1.0, new[] { 1.0 });
            _windowControlView = new HWindowControlView(_windowTools);
            // 旧控件默认绑定了浏览/绘制交互；新库必须由 ImageToolMode 统一分发。
            // Rectangle 等成熟绘制模式继续由 HWindowControlView 处理；仅替换滚轮和快捷键入口。
            _windowControlView.EnableLeftButtonImagePan = true;
            _windowControlView.SetHMouseWheelRelease(false);
            _windowControlView.SetKeyDownRelease(false);
            _windowControlView.RectangleDrawn += OnRectangleDrawn;
            _windowControlView.CircleDrawn += OnCircleDrawn;
            _windowControlView.PolygonDrawn += OnPolygonDrawn;
            _windowControlView.ArrowDrawn += OnArrowDrawn;
            _windowControlView.TextDrawn += OnTextDrawn;
            // 每次图窗重绘后补画标尺，保证缩放、平移和刷新时它始终跟随当前图像。
            _windowTools.ReShowDrawingALL += DrawImageRuler;
            _windowControl.HMouseDown += OnHMouseDown;
            _windowControl.HMouseMove += OnHMouseMove;
            _windowControl.HMouseUp += OnHMouseUp;
            _windowControl.HMouseWheel += OnHMouseWheel;
            _windowControl.KeyDown += OnWindowKeyDown;
        }

        public event EventHandler<PixelInfoChangedEventArgs> PixelInfoChanged;

        public event EventHandler<ImageCanvasNavigationEventArgs> NavigationChanged;

        public event EventHandler<RectangleRoiDrawnEventArgs> RectangleRoiDrawn;

        public event EventHandler<MeasurementDrawnEventArgs> MeasurementDrawn;

        public event EventHandler<MeasurementProgressChangedEventArgs> MeasurementProgressChanged;

        public event EventHandler<OverlaySelectionChangedEventArgs> OverlaySelectionChanged;

        public event EventHandler<OverlayGeometryChangedEventArgs> OverlayGeometryChanged;

        public event EventHandler<OverlayDeleteRequestedEventArgs> OverlayDeleteRequested;

        public event EventHandler<AnnotationDrawnEventArgs> AnnotationDrawn;

        public Control View
        {
            get { return _windowControl; }
        }

        public bool SupportsWindowLevel
        {
            get { return _supportsWindowLevel; }
        }

        /// <summary>
        /// 设置整图标尺显示状态。标尺只读取标定结果，不修改任何图像或测量数据。
        /// </summary>
        public void SetImageRulerVisible(bool visible, double? millimetersPerPixelX, double? millimetersPerPixelY)
        {
            ThrowIfDisposed();
            _isImageRulerVisible = visible;
            _millimetersPerPixelX = millimetersPerPixelX;
            _millimetersPerPixelY = millimetersPerPixelY;
            _windowTools.ReShowDrawALL();
        }

        public void SetCoordinateTransform(ImageTransformState transform, ImageSize sourceImageSize)
        {
            ThrowIfDisposed();
            if (transform == null || sourceImageSize.Width <= 0 || sourceImageSize.Height <= 0)
            {
                throw new ArgumentException("图像坐标变换上下文无效。", "transform");
            }

            _coordinateTransform = transform;
            _sourceImageSize = sourceImageSize;
        }

        public void SetZoomRange(double minimum, double maximum)
        {
            ThrowIfDisposed();
            if (minimum <= 0 || maximum < minimum)
            {
                throw new ArgumentOutOfRangeException("minimum", "缩放范围无效。");
            }

            _minZoom = minimum;
            _maxZoom = maximum;
        }

        public double GetCurrentZoom()
        {
            ThrowIfDisposed();
            if (!_hasImage || _windowControl.ClientSize.Width <= 0 || _windowControl.ClientSize.Height <= 0)
            {
                return 1.0;
            }

            _windowControl.HalconWindow.GetPart(out HTuple row1, out HTuple col1, out HTuple row2, out HTuple col2);
            var visibleWidth = Math.Abs(col2.D - col1.D);
            var visibleHeight = Math.Abs(row2.D - row1.D);
            if (visibleWidth <= 0 || visibleHeight <= 0)
            {
                return 1.0;
            }

            return Math.Min(_windowControl.ClientSize.Width / visibleWidth, _windowControl.ClientSize.Height / visibleHeight);
        }

        /// <summary>
        /// 返回当前 HALCON 视口在显示图坐标系中的范围，供控件外侧标尺读取。
        /// 标尺仅消费该状态，绝不写入或修改图像。
        /// </summary>
        public bool TryGetVisibleDisplayBounds(out ImageRectangle bounds)
        {
            bounds = new ImageRectangle();
            if (_disposed || !_hasImage)
            {
                return false;
            }

            try
            {
                _windowControl.HalconWindow.GetPart(out HTuple row1, out HTuple col1, out HTuple row2, out HTuple col2);
                bounds = new ImageRectangle(col1.D, row1.D, col2.D, row2.D);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 将原图坐标转换为当前图窗客户区坐标，供宿主在标注落点附近摆放普通 WinForms 输入控件。
        /// 该换算只读取视口状态，不修改图像或 HALCON 绘制对象。
        /// </summary>
        public bool TryGetClientPoint(ImagePoint sourcePoint, out Point clientPoint)
        {
            clientPoint = Point.Empty;
            if (_disposed || !_hasImage || _windowControl.ClientSize.Width <= 0 || _windowControl.ClientSize.Height <= 0)
            {
                return false;
            }

            try
            {
                _windowControl.HalconWindow.GetPart(out HTuple row1, out HTuple col1, out HTuple row2, out HTuple col2);
                var visibleWidth = col2.D - col1.D;
                var visibleHeight = row2.D - row1.D;
                if (Math.Abs(visibleWidth) <= double.Epsilon || Math.Abs(visibleHeight) <= double.Epsilon)
                {
                    return false;
                }

                var displayPoint = ToDisplayPoint(sourcePoint);
                clientPoint = new Point(
                    (int)Math.Round((displayPoint.X - col1.D) * _windowControl.ClientSize.Width / visibleWidth),
                    (int)Math.Round((displayPoint.Y - row1.D) * _windowControl.ClientSize.Height / visibleHeight));
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Load(HObject image)
        {
            ThrowIfDisposed();
            if (image == null)
            {
                throw new ArgumentNullException("image");
            }

            // HWindowTools 持有传入对象，因此先克隆，调用方可在返回后立即释放自己的 HObject。
            _windowTools.OpenImage(image.Clone(), 0, 0, true);
            _windowTools.DisplayAdaptiveSizeImage();
            _windowTools.GetImageInfo(out _, out _, out _, out int bitDepth);
            // 现有共用实现按 uint2 缓冲区计算。8 位图由 HALCON 原生显示，不能走该内存路径。
            _supportsWindowLevel = bitDepth == 16;
            _hasImage = true;
        }

        public void Clear()
        {
            ThrowIfDisposed();
            _windowTools.ClearAll();
            _hasImage = false;
            _supportsWindowLevel = false;
        }

        public void Refresh()
        {
            ThrowIfDisposed();
            _windowTools.ReShowDrawALL();
        }

        public void FitToWindow()
        {
            ThrowIfDisposed();
            _windowTools.DisplayAdaptiveSizeImage();
        }

        public void ActualSize()
        {
            ThrowIfDisposed();
            _windowTools.DisplayOriginalSizeImage();
        }

        public void ZoomIn()
        {
            ThrowIfDisposed();
            ZoomAtCenter(1);
        }

        public void ZoomOut()
        {
            ThrowIfDisposed();
            ZoomAtCenter(-1);
        }

        public void ResetPan()
        {
            ThrowIfDisposed();
            if (!_hasImage)
            {
                return;
            }

            _windowTools.GetImageSize(out int imageWidth, out int imageHeight);
            _windowControl.HalconWindow.GetPart(out HTuple row1, out HTuple col1, out HTuple row2, out HTuple col2);
            var currentCenterRow = (row1.D + row2.D) / 2.0;
            var currentCenterColumn = (col1.D + col2.D) / 2.0;
            var targetCenterRow = (imageHeight - 1) / 2.0;
            var targetCenterColumn = (imageWidth - 1) / 2.0;
            var deltaRow = targetCenterRow - currentCenterRow;
            var deltaColumn = targetCenterColumn - currentCenterColumn;
            _windowControl.HalconWindow.SetPart(
                row1.D + deltaRow,
                col1.D + deltaColumn,
                row2.D + deltaRow,
                col2.D + deltaColumn);
            _windowTools.ReShowDrawALL();
        }

        /// <summary>
        /// 将另一图窗的导航增量应用到当前图窗。该方法不触发事件，避免双视图同步递归。
        /// </summary>
        public void ApplyNavigation(ImageCanvasNavigationEventArgs navigation)
        {
            ThrowIfDisposed();
            if (!_hasImage || navigation == null)
            {
                return;
            }

            if (navigation.Kind == ImageCanvasNavigationKind.Pan)
            {
                PanBy(navigation.DeltaColumn, navigation.DeltaRow);
                return;
            }

            if (navigation.Kind == ImageCanvasNavigationKind.Zoom && navigation.ZoomFactor > 0)
            {
                var current = GetCurrentZoom();
                if (!CanZoom(current * navigation.ZoomFactor))
                {
                    return;
                }

                _windowControl.HalconWindow.GetPart(out HTuple row1, out HTuple col1, out HTuple row2, out HTuple col2);
                var centerRow = (row1.D + row2.D) / 2.0;
                var centerColumn = (col1.D + col2.D) / 2.0;
                var height = (row2.D - row1.D) / navigation.ZoomFactor;
                var width = (col2.D - col1.D) / navigation.ZoomFactor;
                _windowControl.HalconWindow.SetPart(centerRow - height / 2.0, centerColumn - width / 2.0, centerRow + height / 2.0, centerColumn + width / 2.0);
                _windowTools.ReShowDrawALL();
            }
        }

        public void SetToolMode(ImageToolMode mode)
        {
            ThrowIfDisposed();
            _pendingAnnotationDrawn = null;
            if (mode != ImageToolMode.RectangleRoi)
            {
                _rectangleSelectionCompleted = null;
            }
            _toolMode = mode;
            _measurementPoints.Clear();
            _windowTools.ClearTransientInteractionPreview();
            if (!IsOverlayEditMode(mode))
            {
                _selectedOverlayId = null;
                _overlayEditSession = null;
            }

            _windowControl.Cursor = IsLineMeasurementMode(mode)
                ? Cursors.Cross
                : mode == ImageToolMode.MoveRoi
                    ? Cursors.SizeAll
                    : mode == ImageToolMode.ResizeRoi
                        ? Cursors.Cross
                        : mode == ImageToolMode.DeleteOverlay
                            ? Cursors.Cross
                            : Cursors.Default;
            // 工具模式是互斥的：只在导航模式允许图像平移，避免左键绘制/点选测量
            // 被共用控件的历史“左键拖图”处理抢占。
            _windowControlView.EnableLeftButtonImagePan = mode == ImageToolMode.Navigate;
            _windowControlView.InteractionColor = GetInteractionColor(mode);
            if (mode != ImageToolMode.Navigate)
            {
                _isPanning = false;
            }
            _windowControlView.SetCurrentUserDrawMode(
                mode == ImageToolMode.RectangleRoi ||
                mode == ImageToolMode.MeasureArea ||
                mode == ImageToolMode.AddRectangleAnnotation ||
                mode == ImageToolMode.AddDefectBox
                    ? HWindowControlView.UserDrawMode.Rectangle
                    : mode == ImageToolMode.AddPointAnnotation
                        ? HWindowControlView.UserDrawMode.Point
                        : mode == ImageToolMode.AddPolygonAnnotation
                            ? HWindowControlView.UserDrawMode.Polygon
                            : mode == ImageToolMode.AddArrowAnnotation
                                ? HWindowControlView.UserDrawMode.Arrow
                    : mode == ImageToolMode.AddTextAnnotation
                                    ? HWindowControlView.UserDrawMode.Text
                                    : HWindowControlView.UserDrawMode.None);

            RenderSelectedOverlay();
        }

        /// <summary>
        /// 设置临时矩形框选模式。回调仅接收原图坐标，不会生成可编辑 ROI。
        /// </summary>
        public bool SetRectangleSelectionMode(Action<ImageRectangle> selectionCompleted)
        {
            ThrowIfDisposed();
            _rectangleSelectionCompleted = selectionCompleted;
            if (selectionCompleted == null)
            {
                SetToolMode(ImageToolMode.Navigate);
                return true;
            }

            if (!_hasImage)
            {
                _rectangleSelectionCompleted = null;
                return false;
            }

            SetToolMode(ImageToolMode.RectangleRoi);
            return true;
        }

        /// <summary>
        /// 颜色配置由宿主统一持有；图窗只读取它绘制，避免每个叠加对象复制一份颜色状态。
        /// </summary>
        public void SetOverlayColors(OverlayColorPalette colors)
        {
            ThrowIfDisposed();
            _overlayColors = colors ?? throw new ArgumentNullException("colors");
            _windowControlView.InteractionColor = GetInteractionColor(_toolMode);
        }

        public void SetAnnotationText(string text)
        {
            ThrowIfDisposed();
            // 文字标注不能走旧测量标签接口；该接口会自动追加冒号。
            _windowControlView.SetUserText(text ?? string.Empty);
        }

        /// <summary>
        /// 从持久化叠加模型重建旧图窗的临时绘制列表，保证清除、刷新与图层显示同步。
        /// </summary>
        public void RebuildOverlays(IEnumerable<ImageOverlayItem> items)
        {
            ThrowIfDisposed();
            _windowTools.ClearAllDrawingOnly();
            if (items != null)
            {
                foreach (var item in items)
                {
                    DrawOverlay(item);
                }
            }

            _windowTools.ReShowDrawALL();
            RenderSelectedOverlay();
        }

        /// <summary>
        /// 编辑交互只读取可见的叠加模型快照；真正的数据提交仍由控件宿主统一完成。
        /// </summary>
        public void SetEditableOverlays(IEnumerable<ImageOverlayItem> items)
        {
            ThrowIfDisposed();
            _editableOverlays.Clear();
            if (items != null)
            {
                foreach (var item in items)
                {
                    if (item != null && item.IsVisible && item.Layer != ImageOverlayLayer.Defect)
                    {
                        _editableOverlays.Add(item);
                    }
                }
            }

            if (_selectedOverlayId.HasValue && FindEditableOverlay(_selectedOverlayId.Value) == null)
            {
                SelectOverlay(null);
            }
        }

        public void ClearRectangleRois(IEnumerable<Guid> ids)
        {
            ThrowIfDisposed();
            if (ids == null)
            {
                return;
            }

            // 矩形面积测量也使用旧工具的矩形绘制列表，必须按稳定 ID 精确删除。
            foreach (var id in ids)
            {
                RemoveRectangleRoi(id);
            }
        }

        public void UpdateRectangleRoi(Guid id, ImageRectangle bounds, string displayText = null)
        {
            ThrowIfDisposed();
            RemoveRectangleRoi(id);
            var rectangle = ToDrawingRectangle(ToDisplayRectangle(bounds));
            var grayValue = _windowTools.GetGrayForRectangle(rectangle);
            _windowTools.AddUserRectangle(rectangle, grayValue, id.ToString("N"), null, displayText);
        }

        public bool RemoveRectangleRoi(Guid id)
        {
            ThrowIfDisposed();
            var label = id.ToString("N");
            for (var index = 0; index < _windowTools._drawUserRectangles.Count; index++)
            {
                if (_windowTools._drawUserRectangles[index].Label != label)
                {
                    continue;
                }

                _windowTools._drawUserRectangles.RemoveAt(index);
                RemoveRectangleDrawAction(index);
                _windowTools.ReShowDrawALL();
                return true;
            }

            return false;
        }

        public bool SetWindowLevel(int window, int level)
        {
            ThrowIfDisposed();
            if (!_supportsWindowLevel)
            {
                return false;
            }

            _windowTools.SetWindowLevel(window, level);
            return true;
        }

        public bool AutoWindowLevel()
        {
            ThrowIfDisposed();
            if (!_supportsWindowLevel)
            {
                return false;
            }

            var range = _windowTools.GetMinMaxGray();
            var minimum = (int)Math.Floor(range.Item1);
            var maximum = (int)Math.Ceiling(range.Item2);
            if (maximum <= minimum)
            {
                _windowTools.UpdataImageByWLWW();
                return true;
            }

            // 共用方法把 0 作为“未设置”的哨兵，实际最小灰度为 0 时以 1 代替下界。
            _windowTools.UpdataImageByWLWW(Math.Max(1, minimum), maximum);
            return true;
        }

        /// <summary>
        /// 恢复旧工具保存的原始显示图像；几何/视口由调用方分别复位。
        /// </summary>
        public void ResetDisplay()
        {
            ThrowIfDisposed();
            _windowTools.UpdataImageByWLWW();
        }

        /// <summary>
        /// 保存当前显示图像。保存的是经过窗宽窗位处理后的图像数据，不包含屏幕缩放比例。
        /// </summary>
        public bool SaveCurrentImage(string fileName)
        {
            ThrowIfDisposed();
            if (!_hasImage || string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            _windowTools.SaveShowingImage(fileName);
            return File.Exists(fileName);
        }

        /// <summary>
        /// 复制完整图像到剪贴板。导出尺寸始终等于图像原始尺寸，不受当前缩放和拖动位置影响；
        /// 可见的 ROI、测量及标注会一并绘制到导出图中。
        /// </summary>
        public bool CopyCurrentViewToClipboard()
        {
            ThrowIfDisposed();
            if (!_hasImage)
            {
                return false;
            }

            HObject exportedImage = null;
            try
            {
                // DumpImage 在离屏窗口按原始图像尺寸重绘，不能再使用屏幕抓取，否则缩放或平移后只会复制局部。
                exportedImage = _windowTools.DumpImage();
                if (exportedImage == null)
                {
                    return false;
                }

                using (var clipboardImage = CreateClipboardBitmap(exportedImage))
                {
                    Clipboard.SetImage(clipboardImage);
                }

                return true;
            }
            finally
            {
                if (exportedImage != null)
                {
                    exportedImage.Dispose();
                }
            }
        }

        private static Bitmap CreateClipboardBitmap(HObject image)
        {
            HOperatorSet.CountChannels(image, out HTuple channelCount);
            if (channelCount.I == 1)
            {
                HOperatorSet.GetImagePointer1(image, out HTuple pointer, out HTuple type, out HTuple width, out HTuple height);
                if (!string.Equals(type.S, "byte", StringComparison.OrdinalIgnoreCase))
                {
                    throw new NotSupportedException("剪贴板导出只支持 8 位离屏图像，实际类型为：" + type.S);
                }

                var pixelCount = checked(width.I * height.I);
                var gray = new byte[pixelCount];
                Marshal.Copy(pointer.IP, gray, 0, gray.Length);
                return Create24BitBitmap(width.I, height.I, gray, null, null);
            }

            if (channelCount.I == 3)
            {
                HOperatorSet.GetImagePointer3(
                    image,
                    out HTuple redPointer,
                    out HTuple greenPointer,
                    out HTuple bluePointer,
                    out HTuple type,
                    out HTuple width,
                    out HTuple height);
                if (!string.Equals(type.S, "byte", StringComparison.OrdinalIgnoreCase))
                {
                    throw new NotSupportedException("剪贴板导出只支持 8 位离屏图像，实际类型为：" + type.S);
                }

                var pixelCount = checked(width.I * height.I);
                var red = new byte[pixelCount];
                var green = new byte[pixelCount];
                var blue = new byte[pixelCount];
                Marshal.Copy(redPointer.IP, red, 0, red.Length);
                Marshal.Copy(greenPointer.IP, green, 0, green.Length);
                Marshal.Copy(bluePointer.IP, blue, 0, blue.Length);
                return Create24BitBitmap(width.I, height.I, red, green, blue);
            }

            throw new NotSupportedException("剪贴板导出不支持的图像通道数：" + channelCount.I);
        }

        private static Bitmap Create24BitBitmap(int width, int height, byte[] redOrGray, byte[] green, byte[] blue)
        {
            var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            var rectangle = new Rectangle(0, 0, width, height);
            var bitmapData = bitmap.LockBits(rectangle, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            try
            {
                var buffer = new byte[checked(bitmapData.Stride * height)];
                var isColor = green != null && blue != null;
                for (var y = 0; y < height; y++)
                {
                    var sourceOffset = y * width;
                    var destinationOffset = y * bitmapData.Stride;
                    for (var x = 0; x < width; x++)
                    {
                        var sourceIndex = sourceOffset + x;
                        var destinationIndex = destinationOffset + x * 3;
                        buffer[destinationIndex] = isColor ? blue[sourceIndex] : redOrGray[sourceIndex];
                        buffer[destinationIndex + 1] = isColor ? green[sourceIndex] : redOrGray[sourceIndex];
                        buffer[destinationIndex + 2] = redOrGray[sourceIndex];
                    }
                }

                Marshal.Copy(buffer, 0, bitmapData.Scan0, buffer.Length);
                return bitmap;
            }
            catch
            {
                bitmap.Dispose();
                throw;
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }
        }

        /// <summary>
        /// 从未受显示处理影响的原图计算整图或矩形 ROI 灰度统计。
        /// </summary>
        public ImageStatistics CalculateStatistics(ImageRectangle? roi)
        {
            ThrowIfDisposed();
            using (var source = CreatePixelSource())
            {
                return _statisticsService.Calculate(source, roi);
            }
        }

        /// <summary>
        /// 从原图计算整图或矩形 ROI 的直方图。
        /// </summary>
        public ImageHistogram GetHistogram(ImageRectangle? roi, int binCount)
        {
            ThrowIfDisposed();
            using (var source = CreatePixelSource())
            {
                return _statisticsService.GetHistogram(source, roi, binCount);
            }
        }

        /// <summary>
        /// 在 UI 线程快速复制 HALCON 像素内存。调用方可安全地在后台线程分析返回快照。
        /// </summary>
        public HalconPixelSource CreatePixelSnapshot()
        {
            ThrowIfDisposed();
            return CreatePixelSource();
        }

        /// <summary>
        /// 读取原图灰度值；坐标始终使用原图像素坐标，不受缩放、窗宽窗位和旋转影响。
        /// </summary>
        public double GetPixelValue(ImagePoint sourcePoint)
        {
            ThrowIfDisposed();
            using (var source = CreatePixelSource())
            {
                return source.GetGrayValue((int)Math.Round(sourcePoint.X), (int)Math.Round(sourcePoint.Y));
            }
        }

        /// <summary>
        /// 读取原图 RGB 分量。单通道图没有 RGB 值，因此返回 null。
        /// </summary>
        public ImagePixelColor GetPixelColor(ImagePoint sourcePoint)
        {
            ThrowIfDisposed();
            using (var source = CreatePixelSource())
            {
                return source.GetPixelColor((int)Math.Round(sourcePoint.X), (int)Math.Round(sourcePoint.Y));
            }
        }

        /// <summary>
        /// 按原图坐标获取连线上的灰度剖面，不受显示窗宽窗位影响。
        /// </summary>
        public IReadOnlyList<double> GetLineProfile(ImagePoint start, ImagePoint end)
        {
            ThrowIfDisposed();
            using (var source = CreatePixelSource())
            {
                return _statisticsService.GetLineProfile(source, start, end);
            }
        }

        private HalconPixelSource CreatePixelSource()
        {
            if (!_hasImage)
            {
                throw new InvalidOperationException("尚未加载图像。");
            }

            // 快照完成后不再需要 HALCON 克隆；必须及时释放，避免频繁统计造成句柄积累。
            var image = _windowTools.GetOrigImageClone();
            try
            {
                return new HalconPixelSource(image);
            }
            finally
            {
                image.Dispose();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _windowControlView.RectangleDrawn -= OnRectangleDrawn;
            _windowControlView.CircleDrawn -= OnCircleDrawn;
            _windowControlView.PolygonDrawn -= OnPolygonDrawn;
            _windowControlView.ArrowDrawn -= OnArrowDrawn;
            _windowControlView.TextDrawn -= OnTextDrawn;
            _windowControl.HMouseDown -= OnHMouseDown;
            _windowControl.HMouseMove -= OnHMouseMove;
            _windowControl.HMouseUp -= OnHMouseUp;
            _windowControl.HMouseWheel -= OnHMouseWheel;
            _windowControl.KeyDown -= OnWindowKeyDown;
            _windowTools.ReShowDrawingALL -= DrawImageRuler;
            _windowTools.Dispose();
        }

        /// <summary>
        /// 绘制整图坐标标尺：框内读像素，框边读标定后的毫米尺寸。
        /// 这里挂在图窗重绘回调中，而不是写入绘制对象列表，避免它被当成 ROI 或标注保存、删除。
        /// </summary>
        private void DrawImageRuler(HWindowControl hWindowControl, HObject showingImage)
        {
            if (!_isImageRulerVisible || showingImage == null)
            {
                return;
            }

            HOperatorSet.GetImageSize(showingImage, out HTuple widthTuple, out HTuple heightTuple);
            var width = widthTuple.I;
            var height = heightTuple.I;
            if (width <= 1 || height <= 1)
            {
                return;
            }

            var window = hWindowControl.HalconWindow;
            var right = width - 1;
            var bottom = height - 1;
            var horizontalTickLength = Math.Max(3, Math.Min(width / 30, height / 8));
            var verticalTickLength = Math.Max(3, Math.Min(height / 30, width / 8));

            HOperatorSet.SetDraw(window, "margin");
            HOperatorSet.SetColor(window, "cyan");
            HOperatorSet.SetLineWidth(window, 2);
            HOperatorSet.DispRectangle1(window, 0, 0, bottom, right);
            for (var index = 0; index <= 4; index++)
            {
                var column = right * index / 4;
                var row = bottom * index / 4;
                HOperatorSet.DispLine(window, 0, column, verticalTickLength, column);
                HOperatorSet.DispLine(window, bottom, column, bottom - verticalTickLength, column);
                HOperatorSet.DispLine(window, row, 0, row, horizontalTickLength);
                HOperatorSet.DispLine(window, row, right, row, right - horizontalTickLength);
            }

            // 90/270 度旋转后，显示 X/Y 分别对应原图 Y/X；翻转不影响物理尺寸。
            var axisSwapped = IsRulerAxisSwapped();
            var horizontalSpacing = axisSwapped ? _millimetersPerPixelY : _millimetersPerPixelX;
            var verticalSpacing = axisSwapped ? _millimetersPerPixelX : _millimetersPerPixelY;
            var labelRow = Math.Max(verticalTickLength + 2, Math.Min(bottom - 2, height / 12));
            var labelColumn = Math.Max(horizontalTickLength + 2, Math.Min(right - 2, width / 12));

            HOperatorSet.DispText(window, "0 px", "image", labelRow, labelColumn, "cyan", "box", "false");
            HOperatorSet.DispText(window, string.Format("{0} px", right), "image", labelRow, Math.Max(labelColumn, right * 3 / 5), "cyan", "box", "false");
            HOperatorSet.DispText(window, GetRulerDimensionLabel("X", right, horizontalSpacing), "image", Math.Max(labelRow, bottom - verticalTickLength * 3), labelColumn, "cyan", "box", "false");
            HOperatorSet.DispText(window, GetRulerDimensionLabel("Y", bottom, verticalSpacing), "image", Math.Max(labelRow + verticalTickLength * 2, bottom / 2), labelColumn, "cyan", "box", "false");
        }

        private bool IsRulerAxisSwapped()
        {
            if (!_coordinateTransform.PreservesAxisAlignedGeometry)
            {
                return false;
            }

            var quarterTurns = (int)Math.Round(_coordinateTransform.RotationDegrees / 90.0) % 4;
            return quarterTurns == 1 || quarterTurns == 3;
        }

        private static string GetRulerDimensionLabel(string axisName, int pixels, double? millimetersPerPixel)
        {
            return millimetersPerPixel.HasValue
                ? string.Format("{0}: {1:0.###} mm", axisName, pixels * millimetersPerPixel.Value)
                : string.Format("{0}: 未标定", axisName);
        }

        private bool HandleOverlayEditMouseDown(HMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || !IsOverlayEditMode(_toolMode))
            {
                return false;
            }

            var displayPoint = new ImagePoint(e.X, e.Y);
            var item = FindOverlayAt(displayPoint, _toolMode == ImageToolMode.ResizeRoi);
            if (item == null)
            {
                SelectOverlay(null);
                return true;
            }

            SelectOverlay(item.Id);
            if (_toolMode == ImageToolMode.DeleteOverlay)
            {
                SelectOverlay(null);
                RaiseOverlayDeleteRequested(item.Id);
                return true;
            }

            var handleIndex = _toolMode == ImageToolMode.ResizeRoi
                ? FindHandleIndex(item, displayPoint)
                : -1;
            if (_toolMode == ImageToolMode.ResizeRoi && HasRectangleBounds(item) && !_coordinateTransform.PreservesAxisAlignedGeometry)
            {
                // 任意角度旋转后的矩形在显示层是四边形；不把屏幕包围盒误当作原图矩形控制点。
                RenderSelectedOverlay();
                return true;
            }

            if (_toolMode == ImageToolMode.ResizeRoi && handleIndex < 0 && HasEditableGeometry(item))
            {
                // 首次点击对象只负责选中并展示控制点，避免误拖造成几何突变。
                RenderSelectedOverlay();
                return true;
            }

            _overlayEditSession = new OverlayEditSession(item, handleIndex, ToSourcePoint(displayPoint));
            return true;
        }

        private bool UpdateOverlayEditPreview(HMouseEventArgs e)
        {
            if (_overlayEditSession == null)
            {
                return false;
            }

            var now = Environment.TickCount;
            if (unchecked(now - _lastOverlayEditPreviewTick) < 33)
            {
                return true;
            }

            _lastOverlayEditPreviewTick = now;
            var geometry = CreateEditedGeometry(_overlayEditSession, ToSourcePoint(new ImagePoint(e.X, e.Y)));
            RenderOverlayEditPreview(_overlayEditSession.Item.Kind, _overlayEditSession.Item.Layer, geometry.Bounds, geometry.Points);
            return true;
        }

        private void CommitOverlayEdit(HMouseEventArgs e)
        {
            if (_overlayEditSession == null || e.Button != MouseButtons.Left)
            {
                return;
            }

            var session = _overlayEditSession;
            _overlayEditSession = null;
            var geometry = CreateEditedGeometry(session, ToSourcePoint(new ImagePoint(e.X, e.Y)));
            _windowTools.ClearTransientInteractionPreview();
            RaiseOverlayGeometryChanged(session.Item.Id, geometry.Bounds, geometry.Points);
        }

        private ImageOverlayItem FindOverlayAt(ImagePoint displayPoint, bool preferHandle)
        {
            if (preferHandle && _selectedOverlayId.HasValue)
            {
                var selected = FindEditableOverlay(_selectedOverlayId.Value);
                if (selected != null && FindHandleIndex(selected, displayPoint) >= 0)
                {
                    return selected;
                }
            }

            for (var index = _editableOverlays.Count - 1; index >= 0; index--)
            {
                var item = _editableOverlays[index];
                if (IsPointOnOverlay(item, displayPoint))
                {
                    return item;
                }
            }

            return null;
        }

        private ImageOverlayItem FindEditableOverlay(Guid id)
        {
            foreach (var item in _editableOverlays)
            {
                if (item.Id == id)
                {
                    return item;
                }
            }

            return null;
        }

        private bool IsPointOnOverlay(ImageOverlayItem item, ImagePoint displayPoint)
        {
            var tolerance = GetOverlayHitTolerance();
            if (HasRectangleBounds(item))
            {
                var bounds = ToDisplayRectangle(item.Bounds);
                return displayPoint.X >= bounds.Left - tolerance && displayPoint.X <= bounds.Right + tolerance &&
                       displayPoint.Y >= bounds.Top - tolerance && displayPoint.Y <= bounds.Bottom + tolerance;
            }

            var points = GetDisplayControlPoints(item);
            if (points.Count == 0)
            {
                return false;
            }

            if (points.Count == 1)
            {
                return GetDistance(points[0], displayPoint) <= tolerance;
            }

            for (var index = 1; index < points.Count; index++)
            {
                if (GetPointToSegmentDistance(displayPoint, points[index - 1], points[index]) <= tolerance)
                {
                    return true;
                }
            }

            if (IsPolygonOverlay(item) && GetPointToSegmentDistance(displayPoint, points[points.Count - 1], points[0]) <= tolerance)
            {
                return true;
            }

            return IsPolygonOverlay(item) && IsPointInsidePolygon(displayPoint, points);
        }

        private int FindHandleIndex(ImageOverlayItem item, ImagePoint displayPoint)
        {
            var handles = GetDisplayControlPoints(item);
            var tolerance = GetOverlayHitTolerance() * 1.5;
            for (var index = 0; index < handles.Count; index++)
            {
                if (GetDistance(handles[index], displayPoint) <= tolerance)
                {
                    return index;
                }
            }

            return -1;
        }

        private void SelectOverlay(Guid? id)
        {
            if (_selectedOverlayId == id)
            {
                RenderSelectedOverlay();
                return;
            }

            _selectedOverlayId = id;
            _overlayEditSession = null;
            RenderSelectedOverlay();
            var handler = OverlaySelectionChanged;
            if (handler != null)
            {
                handler(this, new OverlaySelectionChangedEventArgs(id));
            }
        }

        private void RenderSelectedOverlay()
        {
            if (!IsOverlayEditMode(_toolMode) || !_selectedOverlayId.HasValue)
            {
                return;
            }

            var item = FindEditableOverlay(_selectedOverlayId.Value);
            if (item != null)
            {
                RenderOverlayEditPreview(item.Kind, item.Layer, item.Bounds, new List<ImagePoint>(item.Points));
            }
        }

        private void RenderOverlayEditPreview(ImageOverlayKind kind, ImageOverlayLayer layer, ImageRectangle bounds, IList<ImagePoint> points)
        {
            var lines = GetPreviewLines(kind, bounds, points);
            var markers = GetPreviewHandles(kind, bounds, points);
            // 选中态有独立强调色，虚线由底层临时图层统一绘制。
            _windowTools.SetTransientInteractionPreview(lines, markers, OverlayColorPalette.ToHalconColor(_overlayColors.Selected));
        }

        private List<HalconWindowTools.DrawMeasureLineInfo> GetPreviewLines(ImageOverlayKind kind, ImageRectangle bounds, IList<ImagePoint> points)
        {
            var result = new List<HalconWindowTools.DrawMeasureLineInfo>();
            var displayPoints = GetDisplayControlPoints(kind, bounds, points);
            if (HasRectangleBounds(kind))
            {
                AddPreviewSegment(result, displayPoints[0], displayPoints[1]);
                AddPreviewSegment(result, displayPoints[1], displayPoints[2]);
                AddPreviewSegment(result, displayPoints[2], displayPoints[3]);
                AddPreviewSegment(result, displayPoints[3], displayPoints[0]);
                return result;
            }

            for (var index = 1; index < displayPoints.Count; index++)
            {
                AddPreviewSegment(result, displayPoints[index - 1], displayPoints[index]);
            }

            if (IsPolygonOverlay(kind) && displayPoints.Count > 2)
            {
                AddPreviewSegment(result, displayPoints[displayPoints.Count - 1], displayPoints[0]);
            }

            return result;
        }

        private void AddPreviewSegment(ICollection<HalconWindowTools.DrawMeasureLineInfo> lines, ImagePoint first, ImagePoint second)
        {
            lines.Add(new HalconWindowTools.DrawMeasureLineInfo
            {
                Start = ToDrawingPoint(first),
                End = ToDrawingPoint(second),
                Label = string.Empty
            });
        }

        private List<Point> GetPreviewHandles(ImageOverlayKind kind, ImageRectangle bounds, IList<ImagePoint> points)
        {
            var result = new List<Point>();
            foreach (var point in GetDisplayControlPoints(kind, bounds, points))
            {
                result.Add(ToDrawingPoint(point));
            }

            return result;
        }

        private List<ImagePoint> GetDisplayControlPoints(ImageOverlayItem item)
        {
            return GetDisplayControlPoints(item.Kind, item.Bounds, item.Points);
        }

        private List<ImagePoint> GetDisplayControlPoints(ImageOverlayKind kind, ImageRectangle bounds, IList<ImagePoint> points)
        {
            var result = new List<ImagePoint>();
            if (HasRectangleBounds(kind))
            {
                var displayBounds = ToDisplayRectangle(bounds);
                result.Add(new ImagePoint(displayBounds.Left, displayBounds.Top));
                result.Add(new ImagePoint(displayBounds.Right, displayBounds.Top));
                result.Add(new ImagePoint(displayBounds.Right, displayBounds.Bottom));
                result.Add(new ImagePoint(displayBounds.Left, displayBounds.Bottom));
                return result;
            }

            if (points != null)
            {
                foreach (var point in points)
                {
                    result.Add(ToDisplayPoint(point));
                }
            }

            return result;
        }

        private OverlayGeometry CreateEditedGeometry(OverlayEditSession session, ImagePoint currentPoint)
        {
            if (session.HandleIndex < 0)
            {
                var deltaX = currentPoint.X - session.StartPoint.X;
                var deltaY = currentPoint.Y - session.StartPoint.Y;
                var points = OffsetPoints(session.OriginalPoints, deltaX, deltaY);
                return new OverlayGeometry(session.OriginalBounds.Offset(deltaX, deltaY), points);
            }

            if (HasRectangleBounds(session.Item.Kind))
            {
                return new OverlayGeometry(ResizeRectangle(session.OriginalBounds, session.HandleIndex, currentPoint), session.OriginalPoints);
            }

            var updatedPoints = ReplaceControlPoint(session.OriginalPoints, session.HandleIndex, currentPoint, IsPolygonOverlay(session.Item));
            ConstrainMeasurementPoints(session.Item.Kind, updatedPoints);
            return new OverlayGeometry(session.OriginalBounds, updatedPoints);
        }

        private static ImageRectangle ResizeRectangle(ImageRectangle bounds, int handleIndex, ImagePoint currentPoint)
        {
            switch (handleIndex)
            {
                case 0: return new ImageRectangle(currentPoint.X, currentPoint.Y, bounds.Right, bounds.Bottom);
                case 1: return new ImageRectangle(bounds.Left, currentPoint.Y, currentPoint.X, bounds.Bottom);
                case 2: return new ImageRectangle(bounds.Left, bounds.Top, currentPoint.X, currentPoint.Y);
                case 3: return new ImageRectangle(currentPoint.X, bounds.Top, bounds.Right, currentPoint.Y);
                default: return bounds;
            }
        }

        private static List<ImagePoint> OffsetPoints(IList<ImagePoint> points, double deltaX, double deltaY)
        {
            var result = new List<ImagePoint>();
            foreach (var point in points)
            {
                result.Add(new ImagePoint(point.X + deltaX, point.Y + deltaY));
            }

            return result;
        }

        private static List<ImagePoint> ReplaceControlPoint(IList<ImagePoint> points, int index, ImagePoint value, bool polygon)
        {
            var result = new List<ImagePoint>(points);
            if (index < 0 || index >= result.Count)
            {
                return result;
            }

            result[index] = value;
            if (polygon && result.Count > 2 && IsSamePoint(result[0], result[result.Count - 1]))
            {
                if (index == 0)
                {
                    result[result.Count - 1] = value;
                }
                else if (index == result.Count - 1)
                {
                    result[0] = value;
                }
            }

            return result;
        }

        private static void ConstrainMeasurementPoints(ImageOverlayKind kind, IList<ImagePoint> points)
        {
            if (points.Count < 2)
            {
                return;
            }

            if (kind == ImageOverlayKind.HorizontalMeasurement)
            {
                points[1] = new ImagePoint(points[1].X, points[0].Y);
            }
            else if (kind == ImageOverlayKind.VerticalMeasurement)
            {
                points[1] = new ImagePoint(points[0].X, points[1].Y);
            }
        }

        private static bool IsSamePoint(ImagePoint first, ImagePoint second)
        {
            return Math.Abs(first.X - second.X) < 0.001 && Math.Abs(first.Y - second.Y) < 0.001;
        }

        private static bool HasRectangleBounds(ImageOverlayItem item)
        {
            return HasRectangleBounds(item.Kind);
        }

        private static bool HasRectangleBounds(ImageOverlayKind kind)
        {
            return kind == ImageOverlayKind.RectangleRoi ||
                   kind == ImageOverlayKind.AreaMeasurement ||
                   kind == ImageOverlayKind.RectangleAnnotation ||
                   kind == ImageOverlayKind.DefectBox;
        }

        private static bool IsPolygonOverlay(ImageOverlayItem item)
        {
            return IsPolygonOverlay(item.Kind);
        }

        private static bool IsPolygonOverlay(ImageOverlayKind kind)
        {
            return kind == ImageOverlayKind.PolygonAnnotation || kind == ImageOverlayKind.PolygonRoi;
        }

        private static bool HasEditableGeometry(ImageOverlayItem item)
        {
            return HasRectangleBounds(item) || item.Points.Count > 0;
        }

        private static bool IsOverlayEditMode(ImageToolMode mode)
        {
            return mode == ImageToolMode.MoveRoi || mode == ImageToolMode.ResizeRoi || mode == ImageToolMode.DeleteOverlay;
        }

        private double GetOverlayHitTolerance()
        {
            return Math.Max(2.0, 10.0 / Math.Max(0.01, GetCurrentZoom()));
        }

        private static double GetPointToSegmentDistance(ImagePoint point, ImagePoint first, ImagePoint second)
        {
            var deltaX = second.X - first.X;
            var deltaY = second.Y - first.Y;
            var lengthSquared = deltaX * deltaX + deltaY * deltaY;
            if (lengthSquared < 0.000001)
            {
                return GetDistance(point, first);
            }

            var ratio = ((point.X - first.X) * deltaX + (point.Y - first.Y) * deltaY) / lengthSquared;
            ratio = Math.Max(0, Math.Min(1, ratio));
            return GetDistance(point, new ImagePoint(first.X + ratio * deltaX, first.Y + ratio * deltaY));
        }

        private static bool IsPointInsidePolygon(ImagePoint point, IList<ImagePoint> polygon)
        {
            var inside = false;
            for (int current = 0, previous = polygon.Count - 1; current < polygon.Count; previous = current++)
            {
                var currentPoint = polygon[current];
                var previousPoint = polygon[previous];
                var crosses = (currentPoint.Y > point.Y) != (previousPoint.Y > point.Y);
                if (crosses && point.X < (previousPoint.X - currentPoint.X) * (point.Y - currentPoint.Y) /
                    (previousPoint.Y - currentPoint.Y) + currentPoint.X)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private void RaiseOverlayGeometryChanged(Guid id, ImageRectangle bounds, IList<ImagePoint> points)
        {
            var handler = OverlayGeometryChanged;
            if (handler != null)
            {
                handler(this, new OverlayGeometryChangedEventArgs(id, bounds, new List<ImagePoint>(points)));
            }
        }

        private void RaiseOverlayDeleteRequested(Guid id)
        {
            var handler = OverlayDeleteRequested;
            if (handler != null)
            {
                handler(this, new OverlayDeleteRequestedEventArgs(id));
            }
        }

        private sealed class OverlayEditSession
        {
            public OverlayEditSession(ImageOverlayItem item, int handleIndex, ImagePoint startPoint)
            {
                Item = item;
                HandleIndex = handleIndex;
                StartPoint = startPoint;
                OriginalBounds = item.Bounds;
                OriginalPoints = new List<ImagePoint>(item.Points);
            }

            public ImageOverlayItem Item { get; private set; }

            public int HandleIndex { get; private set; }

            public ImagePoint StartPoint { get; private set; }

            public ImageRectangle OriginalBounds { get; private set; }

            public IList<ImagePoint> OriginalPoints { get; private set; }
        }

        private sealed class OverlayGeometry
        {
            public OverlayGeometry(ImageRectangle bounds, IList<ImagePoint> points)
            {
                Bounds = bounds;
                Points = points;
            }

            public ImageRectangle Bounds { get; private set; }

            public IList<ImagePoint> Points { get; private set; }
        }

        private void OnHMouseDown(object sender, HMouseEventArgs e)
        {
            if (HandleOverlayEditMouseDown(e))
            {
                return;
            }

            if (e.Button == MouseButtons.Left && IsLineMeasurementMode(_toolMode))
            {
                CollectMeasurementPoint(ToSourcePoint(new ImagePoint(e.X, e.Y)));
                return;
            }

            // ROI、标注等工具状态下不允许备用鼠标键拖动图像；切回“导航”才恢复平移。
            if (_toolMode != ImageToolMode.Navigate)
            {
                return;
            }

            if (e.Button != MouseButtons.Right && e.Button != MouseButtons.Middle)
            {
                return;
            }

            _isPanning = true;
            _lastMouseRow = e.Y;
            _lastMouseColumn = e.X;
        }

        private void OnRectangleDrawn(Rectangle rectangle)
        {
            var sourceBounds = ToSourceRectangle(rectangle);
            var selectionCompleted = _rectangleSelectionCompleted;
            if (selectionCompleted != null)
            {
                // 旧控件会在事件返回后决定是否持久化矩形；临时框选必须明确阻止入库。
                _windowControlView.IsRectangleDrawingHandledExternally = true;
                selectionCompleted(sourceBounds);
                return;
            }

            // 回调发生在旧控件自动入库前。由新库分配稳定 ID 后再写入旧显示层，二者可对应。
            _windowControlView.IsRectangleDrawingHandledExternally = true;
            var id = Guid.NewGuid();
            var grayValue = _windowTools.GetGrayForRectangle(rectangle);
            _windowTools.AddUserRectangle(rectangle, grayValue, id.ToString("N"));
            var handler = RectangleRoiDrawn;
            if (handler != null)
            {
                handler(this, new RectangleRoiDrawnEventArgs(
                    id,
                    _toolMode,
                    sourceBounds,
                    grayValue));
            }
        }

        private void OnCircleDrawn(Point center, double radius)
        {
            if (_toolMode != ImageToolMode.AddPointAnnotation)
            {
                return;
            }

            QueueAnnotationDrawn(
                _toolMode,
                new[] { ToSourcePoint(new ImagePoint(center.X, center.Y)) },
                ToSourceRectangle(Rectangle.FromLTRB((int)(center.X - radius), (int)(center.Y - radius), (int)(center.X + radius), (int)(center.Y + radius))),
                null);
        }

        private void OnPolygonDrawn(Point[] points)
        {
            if (_toolMode != ImageToolMode.AddPolygonAnnotation || points == null || points.Length < 3)
            {
                return;
            }

            var imagePoints = new List<ImagePoint>();
            foreach (var point in points)
            {
                imagePoints.Add(ToSourcePoint(new ImagePoint(point.X, point.Y)));
            }

            QueueAnnotationDrawn(_toolMode, imagePoints, new ImageRectangle(), null);
        }

        private void OnArrowDrawn(Point head, Point tail)
        {
            if (_toolMode != ImageToolMode.AddArrowAnnotation)
            {
                return;
            }

            QueueAnnotationDrawn(
                _toolMode,
                new[] { ToSourcePoint(new ImagePoint(head.X, head.Y)), ToSourcePoint(new ImagePoint(tail.X, tail.Y)) },
                new ImageRectangle(),
                null);
        }

        private void OnTextDrawn(Point point, string text)
        {
            if (_toolMode != ImageToolMode.AddTextAnnotation)
            {
                return;
            }

            QueueAnnotationDrawn(
                _toolMode,
                new[] { ToSourcePoint(new ImagePoint(point.X, point.Y)) },
                new ImageRectangle(),
                text);
        }

        private void RaiseAnnotationDrawn(ImageToolMode mode, IList<ImagePoint> points, ImageRectangle bounds, string text)
        {
            var handler = AnnotationDrawn;
            if (handler != null)
            {
                handler(this, new AnnotationDrawnEventArgs(mode, points, bounds, text));
            }
        }

        private void QueueAnnotationDrawn(ImageToolMode mode, IList<ImagePoint> points, ImageRectangle bounds, string text)
        {
            _pendingAnnotationDrawn = new AnnotationDrawnEventArgs(mode, points, bounds, text);
        }

        private void DrawOverlay(ImageOverlayItem item)
        {
            if (item == null || !item.IsVisible)
            {
                return;
            }

            switch (item.Kind)
            {
                case ImageOverlayKind.RectangleRoi:
                case ImageOverlayKind.AreaMeasurement:
                case ImageOverlayKind.RectangleAnnotation:
                case ImageOverlayKind.DefectBox:
                    DrawRectangleOverlay(item);
                    break;
                case ImageOverlayKind.PointAnnotation:
                    if (item.Points.Count > 0)
                    {
                        var point = ToDisplayPoint(item.Points[0]);
                        _windowTools.AddUserCircle(new HalconWindowTools.DrawCircleInfo
                        {
                            Center = new Point((int)Math.Round(point.X), (int)Math.Round(point.Y)),
                            Radius = 3,
                            GrayValue = 0,
                            Label = item.Id.ToString("N"),
                            // GUID 仅供旧绘制列表内部识别；用户确认的标注文字才显示。
                            DisplayText = item.Text ?? string.Empty,
                            Color = GetOverlayColor(item.Layer)
                        });
                    }
                    break;
                case ImageOverlayKind.PolygonAnnotation:
                    if (item.Points.Count >= 3)
                    {
                        var points = new List<Point>();
                        foreach (var point in item.Points)
                        {
                            var displayPoint = ToDisplayPoint(point);
                            points.Add(new Point((int)Math.Round(displayPoint.X), (int)Math.Round(displayPoint.Y)));
                        }

                        _windowTools.AddUserPolygon(new HalconWindowTools.DrawPolygonInfo
                        {
                            Points = points,
                            GrayValue = 0,
                            Label = item.Id.ToString("N"),
                            // 旧绘制器内部标识不显示，只显示用户确认的标注文字。
                            DisplayText = item.Text ?? string.Empty,
                            Color = GetOverlayColor(item.Layer)
                        });
                    }
                    break;
                case ImageOverlayKind.ArrowAnnotation:
                    if (item.Points.Count >= 2)
                    {
                        _windowTools.AddUserArrow(
                            ToDrawingPoint(ToDisplayPoint(item.Points[0])),
                            ToDrawingPoint(ToDisplayPoint(item.Points[1])),
                            GetOverlayColor(item.Layer),
                            item.Text ?? string.Empty);
                    }
                    break;
                case ImageOverlayKind.TextAnnotation:
                    if (item.Points.Count > 0)
                    {
                        var point = ToDisplayPoint(item.Points[0]);
                        _windowTools.AddNewDrawFont(item.Text ?? string.Empty, GetOverlayColor(item.Layer), (int)Math.Round(point.Y), (int)Math.Round(point.X));
                    }
                    break;
                case ImageOverlayKind.DistanceMeasurement:
                case ImageOverlayKind.HorizontalMeasurement:
                case ImageOverlayKind.VerticalMeasurement:
                case ImageOverlayKind.DiameterMeasurement:
                case ImageOverlayKind.LineProfileMeasurement:
                    DrawOverlayLine(item, 0, 1, item.Text);
                    break;
                case ImageOverlayKind.AngleMeasurement:
                    // 角度由两条边组成，第一条仅绘制几何，业务数值只显示一次。
                    DrawOverlayLine(item, 1, 0, string.Empty);
                    DrawOverlayLine(item, 1, 2, item.Text);
                    break;
            }
        }

        private void DrawOverlayLine(ImageOverlayItem item, int firstIndex, int secondIndex, string label)
        {
            if (item.Points.Count <= Math.Max(firstIndex, secondIndex))
            {
                return;
            }

            var sourceFirst = item.Points[firstIndex];
            var sourceSecond = item.Points[secondIndex];
            var first = ToDisplayPoint(sourceFirst);
            var second = ToDisplayPoint(sourceSecond);
            _windowTools.AddUserMeasureLine(
                ToDrawingPoint(first),
                ToDrawingPoint(second),
                GetDistance(sourceFirst, sourceSecond),
                label ?? string.Empty,
                GetOverlayColor(item.Layer),
                label ?? string.Empty);
        }

        private static string GetOverlayLabel(ImageOverlayItem item)
        {
            return string.IsNullOrWhiteSpace(item.ExternalId) ? item.Id.ToString("N") : item.ExternalId;
        }

        private string GetOverlayColor(ImageOverlayLayer layer)
        {
            return _overlayColors.GetHalconColor(layer);
        }

        private string GetInteractionColor(ImageToolMode mode)
        {
            if (mode == ImageToolMode.MoveRoi || mode == ImageToolMode.ResizeRoi || mode == ImageToolMode.DeleteOverlay)
            {
                return OverlayColorPalette.ToHalconColor(_overlayColors.Selected);
            }

            if (mode == ImageToolMode.RectangleRoi)
            {
                return GetOverlayColor(ImageOverlayLayer.Roi);
            }

            if (IsLineMeasurementMode(mode) || mode == ImageToolMode.MeasureArea)
            {
                return GetOverlayColor(ImageOverlayLayer.Measurement);
            }

            if (mode == ImageToolMode.AddDefectBox)
            {
                return GetOverlayColor(ImageOverlayLayer.Defect);
            }

            return GetOverlayColor(ImageOverlayLayer.Annotation);
        }

        private void DrawRectangleOverlay(ImageOverlayItem item)
        {
            if (_coordinateTransform.PreservesAxisAlignedGeometry)
            {
                _windowTools.AddUserRectangle(
                    ToDrawingRectangle(ToDisplayRectangle(item.Bounds)),
                    0,
                    GetOverlayLabel(item),
                    GetOverlayColor(item.Layer),
                    GetOverlayDisplayText(item));
                return;
            }

            // 任意角度旋转后原图矩形在显示图中是四边形，使用多边形保持视觉与像素一致。
            _windowTools.AddUserPolygon(new HalconWindowTools.DrawPolygonInfo
            {
                Points = ToDisplayPolygon(item.Bounds),
                GrayValue = 0,
                Label = GetOverlayLabel(item),
                DisplayText = GetOverlayDisplayText(item),
                Color = GetOverlayColor(item.Layer)
            });
        }

        private static string GetOverlayDisplayText(ImageOverlayItem item)
        {
            // 图窗上只保留用户可读的业务信息；对象 GUID 继续留在 Label/ExternalId 中用于更新和删除。
            if (item.Layer == ImageOverlayLayer.Defect)
            {
                return string.Empty;
            }

            return item.Text ?? string.Empty;
        }

        private ImagePoint ToSourcePoint(ImagePoint displayPoint)
        {
            if (_sourceImageSize.Width <= 0 || _sourceImageSize.Height <= 0)
            {
                return displayPoint;
            }

            var source = _coordinateTransform.DisplayToSource(displayPoint, _sourceImageSize.Width, _sourceImageSize.Height);
            return new ImagePoint(
                Math.Max(0, Math.Min(_sourceImageSize.Width - 1, source.X)),
                Math.Max(0, Math.Min(_sourceImageSize.Height - 1, source.Y)));
        }

        private ImagePoint ToDisplayPoint(ImagePoint sourcePoint)
        {
            return _sourceImageSize.Width <= 0 || _sourceImageSize.Height <= 0
                ? sourcePoint
                : _coordinateTransform.SourceToDisplay(sourcePoint, _sourceImageSize.Width, _sourceImageSize.Height);
        }

        private ImageRectangle ToSourceRectangle(Rectangle displayRectangle)
        {
            // Rectangle.Right/Bottom 为排他边界；模型保存最后一个实际像素。
            return BoundsFromPoints(
                ToSourcePoint(new ImagePoint(displayRectangle.Left, displayRectangle.Top)),
                ToSourcePoint(new ImagePoint(displayRectangle.Right - 1, displayRectangle.Top)),
                ToSourcePoint(new ImagePoint(displayRectangle.Left, displayRectangle.Bottom - 1)),
                ToSourcePoint(new ImagePoint(displayRectangle.Right - 1, displayRectangle.Bottom - 1)));
        }

        private ImageRectangle ToDisplayRectangle(ImageRectangle sourceBounds)
        {
            return BoundsFromPoints(
                ToDisplayPoint(new ImagePoint(sourceBounds.Left, sourceBounds.Top)),
                ToDisplayPoint(new ImagePoint(sourceBounds.Right, sourceBounds.Top)),
                ToDisplayPoint(new ImagePoint(sourceBounds.Left, sourceBounds.Bottom)),
                ToDisplayPoint(new ImagePoint(sourceBounds.Right, sourceBounds.Bottom)));
        }

        private List<Point> ToDisplayPolygon(ImageRectangle sourceBounds)
        {
            return new List<Point>
            {
                ToDrawingPoint(ToDisplayPoint(new ImagePoint(sourceBounds.Left, sourceBounds.Top))),
                ToDrawingPoint(ToDisplayPoint(new ImagePoint(sourceBounds.Right, sourceBounds.Top))),
                ToDrawingPoint(ToDisplayPoint(new ImagePoint(sourceBounds.Right, sourceBounds.Bottom))),
                ToDrawingPoint(ToDisplayPoint(new ImagePoint(sourceBounds.Left, sourceBounds.Bottom)))
            };
        }

        private static ImageRectangle BoundsFromPoints(params ImagePoint[] points)
        {
            var left = double.MaxValue;
            var top = double.MaxValue;
            var right = double.MinValue;
            var bottom = double.MinValue;
            foreach (var point in points)
            {
                left = Math.Min(left, point.X);
                top = Math.Min(top, point.Y);
                right = Math.Max(right, point.X);
                bottom = Math.Max(bottom, point.Y);
            }

            return new ImageRectangle(left, top, right, bottom);
        }

        private static Point ToDrawingPoint(ImagePoint point)
        {
            return new Point((int)Math.Round(point.X), (int)Math.Round(point.Y));
        }

        private void RemoveRectangleDrawAction(int rectangleIndex)
        {
            var foundRectangleCount = 0;
            for (var index = 0; index < _windowTools._drawActionTypes.Count; index++)
            {
                if (_windowTools._drawActionTypes[index] != HalconWindowTools.DrawActionType.AddRectangle)
                {
                    continue;
                }

                if (foundRectangleCount == rectangleIndex)
                {
                    _windowTools._drawActionTypes.RemoveAt(index);
                    return;
                }

                foundRectangleCount++;
            }
        }

        private static Rectangle ToDrawingRectangle(ImageRectangle bounds)
        {
            var left = (int)Math.Floor(bounds.Left);
            var top = (int)Math.Floor(bounds.Top);
            var right = (int)Math.Ceiling(bounds.Right);
            var bottom = (int)Math.Ceiling(bounds.Bottom);
            return Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
        }

        private void OnHMouseMove(object sender, HMouseEventArgs e)
        {
            if (_isPanning && _toolMode == ImageToolMode.Navigate)
            {
                var deltaColumn = e.X - _lastMouseColumn;
                var deltaRow = e.Y - _lastMouseRow;
                PanBy(deltaColumn, deltaRow);
                _lastMouseRow = e.Y;
                _lastMouseColumn = e.X;
                RaiseNavigation(ImageCanvasNavigationKind.Pan, deltaColumn, deltaRow, 1.0);
                return;
            }

            if (UpdateOverlayEditPreview(e))
            {
                return;
            }

            UpdateMeasurementPreview(e);

            var pixel = _windowTools.GetCurrentMousePosAndGrayval();
            var handler = PixelInfoChanged;
            if (handler != null)
            {
                // HWindowTools 返回 Row/Column；对外统一成 X/Y。
                handler(this, new PixelInfoChangedEventArgs(pixel.Item1, ToSourcePoint(new ImagePoint(pixel.Item3, pixel.Item2)), pixel.Item4));
            }
        }

        private void OnHMouseUp(object sender, HMouseEventArgs e)
        {
            _isPanning = false;
            CommitOverlayEdit(e);

            if (e.Button != MouseButtons.Left || _pendingAnnotationDrawn == null)
            {
                return;
            }

            var pending = _pendingAnnotationDrawn;
            _pendingAnnotationDrawn = null;
            RaiseAnnotationDrawn(pending.Mode, pending.Points, pending.Bounds, pending.Text);
        }

        private void OnWindowKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete && _selectedOverlayId.HasValue && IsOverlayEditMode(_toolMode))
            {
                var id = _selectedOverlayId.Value;
                SelectOverlay(null);
                RaiseOverlayDeleteRequested(id);
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode != Keys.Escape)
            {
                return;
            }

            if (_overlayEditSession != null)
            {
                // 编辑过程只存在临时预览层；Esc 丢弃预览即可恢复原几何。
                _overlayEditSession = null;
                RenderSelectedOverlay();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (IsLineMeasurementMode(_toolMode) && _measurementPoints.Count > 0)
            {
                // Esc 只撤销尚未完成的本次测量，已固化的结果不会被误删。
                _measurementPoints.Clear();
                _windowTools.ClearTransientInteractionPreview();
                RaiseMeasurementProgressChanged(0);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void CollectMeasurementPoint(ImagePoint point)
        {
            _measurementPoints.Add(point);
            var requiredPointCount = _toolMode == ImageToolMode.MeasureAngle ? 3 : 2;
            if (_measurementPoints.Count < requiredPointCount)
            {
                ShowMeasurementPreview(point);
                RaiseMeasurementProgressChanged(_measurementPoints.Count);
                return;
            }

            _windowTools.ClearTransientInteractionPreview();

            var first = _measurementPoints[0];
            var second = _measurementPoints[1];
            var third = _measurementPoints.Count > 2 ? _measurementPoints[2] : new ImagePoint();
            var pixelValue = 0.0;
            switch (_toolMode)
            {
                case ImageToolMode.MeasureHorizontal:
                    second = new ImagePoint(second.X, first.Y);
                    _measurementPoints[1] = second;
                    pixelValue = Math.Abs(second.X - first.X);
                    DrawMeasurementLine(first, second, pixelValue, "H");
                    break;
                case ImageToolMode.MeasureVertical:
                    second = new ImagePoint(first.X, second.Y);
                    _measurementPoints[1] = second;
                    pixelValue = Math.Abs(second.Y - first.Y);
                    DrawMeasurementLine(first, second, pixelValue, "V");
                    break;
                case ImageToolMode.MeasureAngle:
                    pixelValue = GetAngleDegrees(first, second, third);
                    DrawMeasurementLine(second, first, GetDistance(first, second), "Angle");
                    DrawMeasurementLine(second, third, GetDistance(second, third), string.Format("{0:0.##}°", pixelValue));
                    break;
                case ImageToolMode.MeasureDiameter:
                    pixelValue = GetDistance(first, second);
                    DrawMeasurementLine(first, second, pixelValue, "Diameter");
                    break;
                case ImageToolMode.MeasureLineProfile:
                    pixelValue = GetDistance(first, second);
                    DrawMeasurementLine(first, second, pixelValue, "Profile");
                    break;
                default:
                    pixelValue = GetDistance(first, second);
                    DrawMeasurementLine(first, second, pixelValue, "Distance");
                    break;
            }

            var handler = MeasurementDrawn;
            if (handler != null)
            {
                handler(this, new MeasurementDrawnEventArgs(_toolMode, new List<ImagePoint>(_measurementPoints), pixelValue));
            }

            _measurementPoints.Clear();
        }

        private void UpdateMeasurementPreview(HMouseEventArgs e)
        {
            if (!IsLineMeasurementMode(_toolMode) || _measurementPoints.Count == 0)
            {
                return;
            }

            // 图窗会在刷新时重绘整张图，限制到约 30 fps，保证拖动预览清晰且不拖慢 UI。
            var now = Environment.TickCount;
            if (unchecked(now - _lastMeasurementPreviewTick) < 33)
            {
                return;
            }

            _lastMeasurementPreviewTick = now;
            ShowMeasurementPreview(ToSourcePoint(new ImagePoint(e.X, e.Y)));
        }

        private void ShowMeasurementPreview(ImagePoint pointer)
        {
            var markers = new List<Point>();
            foreach (var selected in _measurementPoints)
            {
                markers.Add(ToDrawingPoint(ToDisplayPoint(selected)));
            }

            var lines = new List<HalconWindowTools.DrawMeasureLineInfo>();
            var first = _measurementPoints[0];

            if (_toolMode == ImageToolMode.MeasureAngle && _measurementPoints.Count >= 2)
            {
                var vertex = _measurementPoints[1];
                markers.Add(ToDrawingPoint(ToDisplayPoint(pointer)));
                lines.Add(CreatePreviewLine(first, vertex, "第 2 点：顶点"));
                lines.Add(CreatePreviewLine(vertex, pointer, string.Format("当前角度：{0:0.##}°", GetAngleDegrees(first, vertex, pointer))));
            }
            else
            {
                var end = GetConstrainedMeasurementEnd(first, pointer);
                markers.Add(ToDrawingPoint(ToDisplayPoint(end)));
                lines.Add(CreatePreviewLine(first, end, GetMeasurementPreviewLabel(first, end)));
            }

            _windowTools.SetTransientInteractionPreview(lines, markers, GetInteractionColor(_toolMode));
        }

        private HalconWindowTools.DrawMeasureLineInfo CreatePreviewLine(ImagePoint first, ImagePoint second, string label)
        {
            return new HalconWindowTools.DrawMeasureLineInfo
            {
                Start = ToDrawingPoint(ToDisplayPoint(first)),
                End = ToDrawingPoint(ToDisplayPoint(second)),
                Length = GetDistance(first, second),
                Label = label
            };
        }

        private ImagePoint GetConstrainedMeasurementEnd(ImagePoint first, ImagePoint candidate)
        {
            if (_toolMode == ImageToolMode.MeasureHorizontal)
            {
                return new ImagePoint(candidate.X, first.Y);
            }

            if (_toolMode == ImageToolMode.MeasureVertical)
            {
                return new ImagePoint(first.X, candidate.Y);
            }

            return candidate;
        }

        private string GetMeasurementPreviewLabel(ImagePoint first, ImagePoint second)
        {
            var length = GetDistance(first, second);
            switch (_toolMode)
            {
                case ImageToolMode.MeasureHorizontal:
                    return string.Format("当前水平距离：{0:0.##} px", Math.Abs(second.X - first.X));
                case ImageToolMode.MeasureVertical:
                    return string.Format("当前垂直距离：{0:0.##} px", Math.Abs(second.Y - first.Y));
                case ImageToolMode.MeasureDiameter:
                    return string.Format("当前直径：{0:0.##} px", length);
                case ImageToolMode.MeasureLineProfile:
                    return string.Format("剖面长度：{0:0.##} px", length);
                default:
                    return string.Format("当前距离：{0:0.##} px", length);
            }
        }

        private void RaiseMeasurementProgressChanged(int selectedPointCount)
        {
            var handler = MeasurementProgressChanged;
            if (handler != null)
            {
                var requiredPointCount = _toolMode == ImageToolMode.MeasureAngle ? 3 : 2;
                handler(this, new MeasurementProgressChangedEventArgs(_toolMode, selectedPointCount, requiredPointCount));
            }
        }

        private void DrawMeasurementLine(ImagePoint first, ImagePoint second, double length, string label)
        {
            var displayFirst = ToDisplayPoint(first);
            var displaySecond = ToDisplayPoint(second);
            _windowTools.AddUserMeasureLine(
                ToDrawingPoint(displayFirst),
                ToDrawingPoint(displaySecond),
                length,
                label);
        }

        private static bool IsLineMeasurementMode(ImageToolMode mode)
        {
            return mode == ImageToolMode.MeasureDistance ||
                   mode == ImageToolMode.MeasureHorizontal ||
                   mode == ImageToolMode.MeasureVertical ||
                   mode == ImageToolMode.MeasureAngle ||
                   mode == ImageToolMode.MeasureDiameter ||
                   mode == ImageToolMode.MeasureLineProfile;
        }

        private static double GetDistance(ImagePoint first, ImagePoint second)
        {
            var deltaX = second.X - first.X;
            var deltaY = second.Y - first.Y;
            return Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        }

        private static double GetAngleDegrees(ImagePoint first, ImagePoint vertex, ImagePoint third)
        {
            var firstX = first.X - vertex.X;
            var firstY = first.Y - vertex.Y;
            var secondX = third.X - vertex.X;
            var secondY = third.Y - vertex.Y;
            var firstLength = Math.Sqrt(firstX * firstX + firstY * firstY);
            var secondLength = Math.Sqrt(secondX * secondX + secondY * secondY);
            if (firstLength == 0 || secondLength == 0)
            {
                return 0;
            }

            var cosine = (firstX * secondX + firstY * secondY) / (firstLength * secondLength);
            return Math.Acos(Math.Max(-1, Math.Min(1, cosine))) * 180.0 / Math.PI;
        }

        private void OnHMouseWheel(object sender, HMouseEventArgs e)
        {
            if (e.Delta == 0)
            {
                return;
            }

            var factor = e.Delta > 0 ? 1.0 / 0.7 : 0.7;
            var before = GetCurrentZoom();
            if (!CanZoom(before * factor))
            {
                return;
            }

            // DispImageZoom 内部用当前 HALCON 鼠标位置计算视口，因此就是鼠标中心缩放。
            _windowTools.DispImageZoom(e.Delta);
            RaiseZoomIfChanged(before);
        }

        private void ZoomAtCenter(int mode)
        {
            if (!_hasImage)
            {
                return;
            }

            var factor = mode > 0 ? 1.0 / 0.7 : 0.7;
            var before = GetCurrentZoom();
            if (!CanZoom(before * factor))
            {
                return;
            }

            _windowTools.DispImageZoomAtCenter(mode);
            RaiseZoomIfChanged(before);
        }

        private bool CanZoom(double targetZoom)
        {
            return targetZoom >= _minZoom && targetZoom <= _maxZoom;
        }

        private void RaiseZoomIfChanged(double before)
        {
            var after = GetCurrentZoom();
            if (before <= 0 || Math.Abs(after - before) < 0.000001)
            {
                return;
            }

            RaiseNavigation(ImageCanvasNavigationKind.Zoom, 0, 0, after / before);
        }

        private void PanBy(double deltaColumn, double deltaRow)
        {
            _windowControl.HalconWindow.GetPart(out HTuple row1, out HTuple col1, out HTuple row2, out HTuple col2);
            _windowControl.HalconWindow.SetPart(
                row1.D - deltaRow,
                col1.D - deltaColumn,
                row2.D - deltaRow,
                col2.D - deltaColumn);
            _windowTools.ReShowDrawALL();
        }

        private void RaiseNavigation(ImageCanvasNavigationKind kind, double deltaColumn, double deltaRow, double zoomFactor)
        {
            var handler = NavigationChanged;
            if (handler != null)
            {
                handler(this, new ImageCanvasNavigationEventArgs(kind, deltaColumn, deltaRow, zoomFactor));
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("HalconImageCanvas");
            }
        }
    }
}
