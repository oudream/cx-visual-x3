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
using Polyline = System.Windows.Shapes.Polyline;
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
        // MVI持有单一短命编辑手势和取消快照；正式Undo仍属于MEM/MTC。
        private readonly MVIImageOverlayEditGesture _overlayEdit;
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
        private bool _editingVertex, _editingVertexSecondary;
        private bool _rotatingOverlay, _rotatingSecondary;
        private MVIImageOverlayItem _rotationStart;
        private MVIImageOverlayItem _vertexEditStart;
        private int _vertexIndex;
        private Point _startPointer;
        private int _selectionX, _selectionY, _lastSelectionX, _lastSelectionY;
        private MVIImageOverlayKind _selectionKind = MVIImageOverlayKind.RectangleRoi;
        private MVIImageToolMode _toolMode = MVIImageToolMode.Navigate;
        private readonly List<FGEPoint2D> _polygonDraft = new List<FGEPoint2D>();
        // Ls2Ls真实输入状态归UI无关MVI；此处PolygonDraft只用作画布预览快照。
        private readonly MVIImageLinePairGesture _linePairGesture=new MVIImageLinePairGesture();
        private FGEPoint2D? _draftHover;
        private string _annotationText = "标注";
        private FIMPixelRegion? _region;
        private readonly MVIImageDisplayState _displayState = new MVIImageDisplayState();
        // 仅呈现层风格预设，不另存图元或建立第二OverlayStore。
        private readonly Dictionary<MVIImageOverlayLayer,int> _overlayLayerColors =
            new Dictionary<MVIImageOverlayLayer,int>();

        public MPWImageViewerControl()
        {
            InitializeComponent();
            _overlayEdit=new MVIImageOverlayEditGesture(_overlays);
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
                CancelTransientGesture();
                _toolMode = value;
                DrawSelection();
                DrawOverlays();
            }
        }

        /// <summary>
        /// 统一终止当前WPF鼠标过程；MVI负责将同一手势写入的草稿图元回滚。
        /// Esc、失捕、切工具、换图共用；外部锁定/删除/替换不回写旧快照。
        /// </summary>
        private void CancelTransientGesture()
        {
            _overlayEdit.Cancel();
            if(_selecting)_region=null;
            _dragging=_selecting=_moving=_resizing=_editingVertex=_rotatingOverlay=false;
            _secondaryDragging=false;
            _resizingSecondary=_editingVertexSecondary=_rotatingSecondary=false;
            _movingOverlay=_resizeStart=_vertexEditStart=_rotationStart=null;
            _linePairGesture.Reset();
            _polygonDraft.Clear();
            _draftHover=null;
            if(ViewportRoot.IsMouseCaptured)ViewportRoot.ReleaseMouseCapture();
            if(SecondaryViewportRoot.IsMouseCaptured)SecondaryViewportRoot.ReleaseMouseCapture();
            DrawSelection();
            DrawOverlays();
        }

        private bool HasTransientGesture()
        {
            return _overlayEdit.IsActive || _dragging || _selecting || _moving ||
                _resizing || _editingVertex || _rotatingOverlay || _secondaryDragging ||
                _polygonDraft.Count>0;
        }

        private bool HasCapturedGesture()
        {
            // 点选多边形/两线的草稿可以跨多次MouseDown存在，不属于捕获中的拖动。
            // 仅捕获操作异常丢失时才回滚；Esc仍由HasTransientGesture取消所有草稿。
            return _overlayEdit.IsActive || _dragging || _selecting || _moving ||
                _resizing || _editingVertex || _rotatingOverlay || _secondaryDragging;
        }

        private void OnGestureLostMouseCapture(object sender,MouseEventArgs e)
        {
            // MouseUp先Complete并清理捕获态再ReleaseMouseCapture。
            // 正常释放右Pane平移不能取消另一个跨多次点击的测量草稿。
            if(HasCapturedGesture())CancelTransientGesture();
        }

        private void OnGesturePreviewKeyDown(object sender,KeyEventArgs e)
        {
            if(e.Key==Key.Escape && HasTransientGesture())
            {
                CancelTransientGesture();
                e.Handled=true;
            }
        }

        private void CaptureGestureMouse(UIElement host)
        {
            // CaptureMouse可能因视口失去可见性、焦点或其他控件抢占而失败。
            // 失败立即撤销草稿，不能保留一个永远收不到MouseUp的编辑状态。
            if(host.CaptureMouse())return;
            CancelTransientGesture();
            PixelStatus.Text="鼠标捕获失败，当前未完成编辑已取消。";
        }

        private bool ApplyOverlayEdit(MVIImageOverlayItem updated)
        {
            if(_overlayEdit.TryUpdate(updated))return true;
            CancelTransientGesture();
            PixelStatus.Text="编辑对象已被其他操作修改、锁定或移除，旧手势不再覆盖它。";
            return false;
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
            MVIImageOverlayItem resized = item.Kind==MVIImageOverlayKind.RotatedRectangleRoi ?
                item.WithResizedRotatedHandle(handle,position,
                    ActiveImage.Width,ActiveImage.Height) :
                item.WithResizedRoiHandle(handle,position,
                    ActiveImage.Width,ActiveImage.Height);
            if (!_overlays.Replace(resized))
                throw new InvalidOperationException("ROI已在编辑期间被删除。");
            return resized;
        }

        /// <summary>原图坐标下逐顶点提交，仍由唯一MVI OverlayStore更新WPF观察者。</summary>
        public MVIImageOverlayItem MoveSelectedPolygonVertex(int index,FGEPoint2D position)
        {
            VerifyAccess();
            if (!_selectedOverlayId.HasValue || ActiveImage == null)
                throw new InvalidOperationException("尚未选中多边形ROI或加载图像。");
            MVIImageOverlayItem current=_overlays.Find(_selectedOverlayId.Value);
            if (current==null || !_overlays.GetVisibleItems().Any(x=>x.Id==current.Id))
                throw new InvalidOperationException("多边形已删除或所在图层不可见。");
            MVIImageOverlayItem changed=current.WithMovedPolygonVertex(index,position,
                ActiveImage.Width,ActiveImage.Height);
            if (!_overlays.Replace(changed))
                throw new InvalidOperationException("多边形在提交时已删除。");
            return changed;
        }

        /// <summary>按边索引插入多边形顶点；WPF显示与其他编辑共用MVI快照。</summary>
        public MVIImageOverlayItem InsertSelectedPolygonVertex(int edgeIndex,FGEPoint2D point)
        {
            VerifyAccess();
            MVIImageOverlayItem current=RequireVisibleSelectedPolygon();
            MVIImageOverlayItem next=current.WithInsertedPolygonVertex(edgeIndex,point,
                ActiveImage.Width,ActiveImage.Height);
            if(!_overlays.Replace(next))
                throw new InvalidOperationException("多边形在插入时已删除。");
            return next;
        }

        /// <summary>移除顶点；至少保留有效三角形，不使用另一套覆盖对象列表。</summary>
        public MVIImageOverlayItem RemoveSelectedPolygonVertex(int vertexIndex)
        {
            VerifyAccess();
            MVIImageOverlayItem current=RequireVisibleSelectedPolygon();
            MVIImageOverlayItem next=current.WithRemovedPolygonVertex(vertexIndex);
            if(!_overlays.Replace(next))
                throw new InvalidOperationException("多边形在删除顶点时已删除。");
            return next;
        }

        private MVIImageOverlayItem RequireVisibleSelectedPolygon()
        {
            if(ActiveImage==null || !_selectedOverlayId.HasValue ||
                !OverlayMatchesActiveImage())
                throw new InvalidOperationException("请先打开原图并选中多边形ROI。");
            MVIImageOverlayItem item=_overlays.Find(_selectedOverlayId.Value);
            if(item==null || item.Kind!=MVIImageOverlayKind.PolygonRoi ||
                !_overlays.GetVisibleItems().Any(x=>x.Id==item.Id))
                throw new InvalidOperationException("多边形不存在或当前图层不可见。");
            return item;
        }

        /// <summary>在现有多边形指定边的中点插入，供区域页和专业用户调用。</summary>
        public MVIImageOverlayItem InsertSelectedPolygonEdgeMidpoint(int edgeIndex)
        {
            MVIImageOverlayItem item=RequireVisibleSelectedPolygon();
            if(edgeIndex<0 || edgeIndex>=item.Points.Count)
                throw new ArgumentOutOfRangeException("edgeIndex");
            FGEPoint2D a=item.Points[edgeIndex];
            FGEPoint2D b=item.Points[(edgeIndex+1)%item.Points.Count];
            return InsertSelectedPolygonVertex(edgeIndex,
                new FGEPoint2D((a.X+b.X)/2,(a.Y+b.Y)/2));
        }

        /// <summary>旋转矩形宽高/角度均为原图几何，经过唯一MVI Store发通知。</summary>
        public MVIImageOverlayItem RotateSelectedRectangle(double angleDegrees)
        {
            VerifyAccess();
            if(!_selectedOverlayId.HasValue || ActiveImage==null)
                throw new InvalidOperationException("尚未选中旋转矩形或加载图像。");
            MVIImageOverlayItem item=_overlays.Find(_selectedOverlayId.Value);
            if(item==null || !_overlays.GetVisibleItems().Any(x=>x.Id==item.Id))
                throw new InvalidOperationException("旋转对象不存在或图层不可见。");
            MVIImageOverlayItem changed=item.WithRotatedRectangleAngle(
                angleDegrees,ActiveImage.Width,ActiveImage.Height);
            if(!_overlays.Replace(changed))
                throw new InvalidOperationException("旋转对象已被删除。");
            return changed;
        }
        public MVIImageOverlayItem AddRotatedRectangle(FGERect2D rect,double angleDegrees)
        {
            VerifyAccess();
            if(ActiveImage==null) throw new InvalidOperationException("请先加载原图。");
            return AddOverlay(MVIImageOverlayItem.CreateRotatedRectangle(
                rect,angleDegrees,ActiveImage.Width,ActiveImage.Height));
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
        public int PolygonVertexHandleVisualCount
        { get { return CountPolygonVertexHandles(OverlayCanvas); } }
        public int SecondaryPolygonVertexHandleVisualCount
        { get { return CountPolygonVertexHandles(SecondaryOverlayCanvas); } }
        private static int CountPolygonVertexHandles(Canvas canvas)
        {
            int total=0;
            foreach(UIElement child in canvas.Children)
                if(child is Ellipse && ((Ellipse)child).Tag is int) total++;
            return total;
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
            CancelTransientGesture();
            _pendingFit = true;
            _region = null;
            _selectedOverlayId = null;
            _resizeStart = null; _resizing = false;
            _vertexEditStart = null; _editingVertex = false;
            _pixelSpacing = null; _rulerMillimeters = false;
            _displayState.Reset();
            _viewTransform.Reset();
            _comparison.Reset();
            _linePairGesture.Reset();
            _polygonDraft.Clear();
            _overlays.Clear();
            _overlayOriginal = image;
            _session.SetOriginal(image);
        }

        /// <summary>
        /// 连续体积切片替换像素帧，保留现有WPF缩放、平移与比较视口。
        /// 第一次加载仍调用ShowOriginal，新尺寸仍由RefreshDisplay自动Fit。
        /// </summary>
        public void UpdateOriginalFrame(FIMImageBuffer image)
        {
            VerifyAccess();
            if(image==null)throw new ArgumentNullException("image");
            if(_session.Original==null)
            {
                ShowOriginal(image);return;
            }
            CancelTransientGesture();
            _pendingFit=false;
            _overlayOriginal=image;
            _session.SetOriginal(image);
        }

        public void ShowResult(FIMImageBuffer image)
        {
            VerifyAccess();
            if (image == null) throw new ArgumentNullException("image");
            _session.SetResult(image);
            _session.Select(MVIImageSlot.Result);
        }

        /// <summary>
        /// 异步结果的UI提交入口。调用者应在开始计算前捕获Session.SourceVersion；
        /// 过期结果不得覆盖新图，也不得切换当前视图。
        /// </summary>
        public bool TryShowResult(long expectedVersion, FIMImageBuffer image)
        {
            VerifyAccess();
            if (image == null) throw new ArgumentNullException("image");
            if (!_session.TrySetResult(expectedVersion, image)) return false;
            _session.Select(MVIImageSlot.Result);
            return true;
        }

        public void SelectOriginal() { VerifyAccess(); _session.Select(MVIImageSlot.Original); }
        public void SelectResult() { VerifyAccess(); _session.Select(MVIImageSlot.Result); }

        public void Clear()
        {
            VerifyAccess();
            CancelTransientGesture();
            _region = null;
            _selectedOverlayId = null; _resizeStart = null; _resizing = false;
            _vertexEditStart = null; _editingVertex = false;
            _displayState.Reset();
            _viewTransform.Reset();
            _comparison.Reset();
            _linePairGesture.Reset();
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
                // SDK可直接使用公开Session切图；必须同样终止当前捕获/草稿，
                // 而不能只清Store，留下失去所属图像的鼠标编辑状态。
                CancelTransientGesture();
                _overlayOriginal = original;
                _selectedOverlayId = null;
                _region = null;
                _pixelSpacing = null; _rulerMillimeters = false;
                _linePairGesture.Reset();
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
            DrawDraftPolylineOn(DraftPolyline,DraftLinePair,_viewport,true);
            DrawDraftPolylineOn(SecondaryDraftPolyline,SecondaryDraftLinePair,
                _secondaryViewport,IsSideBySideVisible);
        }

        /// <summary>左右视图共用原图草稿点，只分别映射屏幕坐标。</summary>
        private void DrawDraftPolylineOn(Polyline first,Polyline second,
            MVIImageViewport viewport,bool visible)
        {
            first.Points.Clear();
            second.Points.Clear();
            second.Visibility=Visibility.Collapsed;
            if(!visible || _polygonDraft.Count==0 || !OverlayMatchesActiveImage())
            {
                first.Visibility=Visibility.Collapsed;
                return;
            }
            if(_toolMode==MVIImageToolMode.LinePair)
            {
                first.Points.Add(SourceToCanvas(_polygonDraft[0],viewport));
                if(_polygonDraft.Count>=2)
                    first.Points.Add(SourceToCanvas(_polygonDraft[1],viewport));
                else if(_draftHover.HasValue)
                    first.Points.Add(SourceToCanvas(_draftHover.Value,viewport));
                if(_polygonDraft.Count>=3)
                {
                    second.Points.Add(SourceToCanvas(_polygonDraft[2],viewport));
                    if(_draftHover.HasValue)
                        second.Points.Add(SourceToCanvas(_draftHover.Value,viewport));
                    second.Visibility=Visibility.Visible;
                }
            }
            else
            {
                foreach(FGEPoint2D p in _polygonDraft)
                    first.Points.Add(SourceToCanvas(p,viewport));
                if(_draftHover.HasValue)
                    first.Points.Add(SourceToCanvas(_draftHover.Value,viewport));
            }
            first.Visibility=Visibility.Visible;
        }

        /// <summary>原图坐标直接增加多边形，可供算法检测和交互工具统一调用。</summary>
        public MVIImageOverlayItem AddPolygonRoi(IReadOnlyList<FGEPoint2D> points)
        {
            VerifyAccess();
            return AddPointGeometry(MVIImageOverlayKind.PolygonRoi,
                MVIImageOverlayLayer.Roi, points, 3);
        }

        /// <summary>
        /// Ls2Ls分阶段四点输入。WPF左键与开发者API走同一条状态机；
        /// 前三点返回null，第四点提交唯一MVI Store并返回测量对象。
        /// </summary>
        public MVIImageOverlayItem AppendLinePairPoint(FGEPoint2D point)
        {
            VerifyAccess();
            FIMImageBuffer image=ActiveImage;
            if(image==null || _toolMode!=MVIImageToolMode.LinePair)
                throw new InvalidOperationException("请先加载图像并切换至两线测量模式。");
            // 共用MVI状态对象判定下一阶段：失败时不改变任何草稿或Store。
            bool complete=_linePairGesture.Append(point,image.Width,image.Height);
            _polygonDraft.Clear();
            _polygonDraft.AddRange(_linePairGesture.Points);
            _region=null;
            if(!complete)
            {
                _draftHover=null;DrawSelection();
                PixelStatus.Text="两线测量：已确定"+_linePairGesture.PointCount+
                    "/4点；"+(_linePairGesture.Stage==MVIImageLinePairStage.FirstEnd ?
                        "继续选择第一条线终点" :
                        _linePairGesture.Stage==MVIImageLinePairStage.SecondStart ?
                            "请选第二条线起点" : "请选第二条线终点");
                return null;
            }
            try
            {
                CompletePointDraft(MVIImageToolMode.LinePair);
                PixelStatus.Text="两线段测量完成，右侧对象列表可查看结果及导出CSV。";
                return _selectedOverlayId.HasValue ?
                    _overlays.Find(_selectedOverlayId.Value) : null;
            }
            catch
            {
                _linePairGesture.Reset();
                _polygonDraft.Clear();_draftHover=null;DrawSelection();
                throw;
            }
        }

        /// <summary>用于比较视图交互/测试/嵌入宿主的原图到右画布坐标映射。</summary>
        public Point SourceToSecondaryViewport(FGEPoint2D source)
        {
            VerifyAccess();
            if(!IsSideBySideVisible || ActiveImage==null)
                throw new InvalidOperationException("左右双视图未激活。");
            return SourceToCanvas(source,_secondaryViewport);
        }

        /// <summary>右视口点选经逆变换回原图，再复用同一Ls2Ls四点状态机。</summary>
        public MVIImageOverlayItem AppendLinePairAtSecondaryViewport(Point pointer)
        {
            VerifyAccess();
            if(!IsSideBySideVisible || _session.Result==null ||
                ActiveImage==null || _toolMode!=MVIImageToolMode.LinePair)
                throw new InvalidOperationException("请开启左右双视图及两线段测量模式。");
            FIMImageBuffer image=ActiveImage;
            FGEPoint2D source=_viewTransform.DisplayToSource(new FGEPoint2D(
                _secondaryViewport.ImageX(pointer.X),_secondaryViewport.ImageY(pointer.Y)),
                image.Width,image.Height);
            if(!IsFinite(source.X) || !IsFinite(source.Y) ||
                source.X<0 || source.Y<0 ||
                source.X>=image.Width || source.Y>=image.Height)
                throw new ArgumentOutOfRangeException("pointer","右侧测量点超出图像范围。");
            return AppendLinePairPoint(new FGEPoint2D(
                Math.Floor(source.X)+0.5,Math.Floor(source.Y)+0.5));
        }

        /// <summary>完整Ls2Ls四点测量入口：两条独立有限线段与结构化结果保留原图坐标。</summary>
        public MVIImageOverlayItem AddLinePairMeasurement(IReadOnlyList<FGEPoint2D> points)
        {
            VerifyAccess();
            if(points==null || points.Count!=4)
                throw new ArgumentException("两线段测量恰好需要四个原图点。","points");
            FIMImageMeasurement.MeasureLinePair(points[0],points[1],points[2],points[3],PixelSpacing);
            return AddPointGeometry(MVIImageOverlayKind.LinePairMeasurement,
                MVIImageOverlayLayer.Measurement,points,4);
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
                MVIImageOverlayItem created;
                if (mode == MVIImageToolMode.Angle)
                    created=AddAngleMeasurement(_polygonDraft.ToArray());
                else if(mode==MVIImageToolMode.LinePair)
                    created=AddLinePairMeasurement(_polygonDraft.ToArray());
                else if (mode == MVIImageToolMode.PolygonArea)
                {
                    if (FIMImageMeasurement.GetPolygonAreaPixels(_polygonDraft) <= 0)
                        throw new ArgumentException("面积测量不接受零面积多边形。");
                    created=AddPointGeometry(MVIImageOverlayKind.AreaMeasurement,
                        MVIImageOverlayLayer.Measurement,_polygonDraft.ToArray(),3);
                }
                else created=AddPolygonRoi(_polygonDraft.ToArray());
                SelectOverlay(created.Id); // 创建后自动选中，立即可编辑、统计与导出
                RaiseSelectionChanged();
            }
            _linePairGesture.Reset();
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
                case MVIImageOverlayKind.LinePairMeasurement:
                    if(item.Points.Count!=4)return string.Empty;
                    FIMLinePairResult pair=FIMImageMeasurement.MeasureLinePair(
                        item.Points[0],item.Points[1],item.Points[2],item.Points[3],spacing);
                    return "最短 "+pair.DistancePixels.ToString("0.###")+" px"+
                        (pair.DistanceMillimeters.HasValue ?
                            " / "+pair.DistanceMillimeters.Value.ToString("0.###")+" mm" : "")+
                        " · 夹角 "+pair.AngleDegrees.ToString("0.###")+"°"+
                        (pair.PhysicalAngleDegrees.HasValue ?
                            " / 物理角 "+pair.PhysicalAngleDegrees.Value.ToString("0.###")+"°" : "");
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
                         item.Kind == MVIImageOverlayKind.RotatedRectangleRoi ||
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
                else if (item.Kind==MVIImageOverlayKind.LinePairMeasurement &&
                         item.Points.Count==4)
                {
                    FIMLinePairResult pair=FIMImageMeasurement.MeasureLinePair(
                        item.Points[0],item.Points[1],item.Points[2],item.Points[3],PixelSpacing);
                    for(int i=0;i<4;i+=2)
                    {
                        Point a=SourceToCanvas(item.Points[i],viewport);
                        Point b=SourceToCanvas(item.Points[i+1],viewport);
                        DrawLine(a.X,a.Y,b.X,b.Y,stroke,width,canvas);
                    }
                    Point nearestA=SourceToCanvas(pair.ClosestOnFirst,viewport);
                    Point nearestB=SourceToCanvas(pair.ClosestOnSecond,viewport);
                    DrawLine(nearestA.X,nearestA.Y,nearestB.X,nearestB.Y,
                        Brushes.Gold,1.5,canvas);
                    DrawMeasurementLabel(item,stroke,nearestB,canvas);
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
                // 对象属性中的文字不仅属于列表，也必须呈现在实际原图坐标的画布。
                // TextAnnotation已有专门渲染路径，避免重复绘制。
                if (item.Kind != MVIImageOverlayKind.TextAnnotation &&
                    !string.IsNullOrEmpty(item.Text))
                {
                    FGEPoint2D origin = item.Points.Count > 0 ? item.Points[0] :
                        new FGEPoint2D(item.Bounds.Left,item.Bounds.Top);
                    Point anchor = SourceToCanvas(origin,viewport);
                    TextBlock customLabel = new TextBlock {
                        Text = item.Text, Foreground = stroke,
                        Background = new SolidColorBrush(Color.FromArgb(160,0,0,0)),
                        FontSize = 13, IsHitTestVisible = false };
                    Canvas.SetLeft(customLabel,anchor.X + 4);
                    Canvas.SetTop(customLabel,anchor.Y - 16);
                    canvas.Children.Add(customLabel);
                }
            }
            if ((_toolMode == MVIImageToolMode.ResizeOverlay ||
                _toolMode == MVIImageToolMode.EditPolygonVertex ||
                _toolMode == MVIImageToolMode.RotateOverlay) && _selectedOverlayId.HasValue)
            {
                MVIImageOverlayItem selected = _overlays.GetVisibleItems().FirstOrDefault(
                    item => item.Id == _selectedOverlayId.Value && item.Layer == MVIImageOverlayLayer.Roi);
                if (selected != null && !selected.IsLocked &&
                    selected.Bounds.Width > 0 && selected.Bounds.Height > 0)
                {
                    if (_toolMode == MVIImageToolMode.ResizeOverlay)
                        DrawResizeHandles(canvas,viewport,selected);
                    else if (_toolMode == MVIImageToolMode.EditPolygonVertex &&
                        selected.Kind==MVIImageOverlayKind.PolygonRoi)
                        DrawPolygonVertexHandles(canvas,viewport,selected);
                    else if (_toolMode == MVIImageToolMode.RotateOverlay &&
                        selected.Kind==MVIImageOverlayKind.RotatedRectangleRoi)
                        DrawRotationHandle(canvas,viewport,selected);
                }
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

        private static FGEPoint2D ResizeHandlePoint(MVIImageOverlayItem item,MVIImageResizeHandle h)
        {
            if(item.Kind!=MVIImageOverlayKind.RotatedRectangleRoi)
                return ResizeHandlePoint(item.Bounds,h);
            IReadOnlyList<FGEPoint2D> p=item.Points;
            int a=0,b=0;
            switch(h)
            {
                case MVIImageResizeHandle.TopLeft:return p[0];
                case MVIImageResizeHandle.TopRight:return p[1];
                case MVIImageResizeHandle.BottomRight:return p[2];
                case MVIImageResizeHandle.BottomLeft:return p[3];
                case MVIImageResizeHandle.Top:a=0;b=1;break;
                case MVIImageResizeHandle.Right:a=1;b=2;break;
                case MVIImageResizeHandle.Bottom:a=2;b=3;break;
                case MVIImageResizeHandle.Left:a=3;b=0;break;
                default:throw new ArgumentOutOfRangeException("h");
            }
            return new FGEPoint2D((p[a].X+p[b].X)/2,(p[a].Y+p[b].Y)/2);
        }

        private void DrawResizeHandles(Canvas canvas,MVIImageViewport viewport,
            MVIImageOverlayItem item)
        {
            foreach (MVIImageResizeHandle handle in Enum.GetValues(typeof(MVIImageResizeHandle)))
            {
                Point screen = SourceToCanvas(ResizeHandlePoint(item,handle),viewport);
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

        private void DrawPolygonVertexHandles(Canvas canvas,MVIImageViewport viewport,
            MVIImageOverlayItem item)
        {
            for(int i=0;i<item.Points.Count;i++)
            {
                Point screen=SourceToCanvas(item.Points[i],viewport);
                Ellipse dot=new Ellipse { Width=11,Height=11,Fill=Brushes.Gold,
                    Stroke=Brushes.DarkSlateGray,StrokeThickness=1.5,Tag=i,
                    IsHitTestVisible=false };
                System.Windows.Automation.AutomationProperties.SetAutomationId(dot,
                    "MPW.Imaging.PolygonVertex."+i);
                Canvas.SetLeft(dot,screen.X-5.5);
                Canvas.SetTop(dot,screen.Y-5.5);
                canvas.Children.Add(dot);
            }
        }

        private static FGEPoint2D RotatedRectangleCenter(MVIImageOverlayItem item)
        {
            return new FGEPoint2D((item.Points[0].X+item.Points[2].X)/2.0,
                (item.Points[0].Y+item.Points[2].Y)/2.0);
        }
        private Point RotationHandleScreenPoint(MVIImageOverlayItem item,MVIImageViewport viewport)
        {
            FGEPoint2D center=RotatedRectangleCenter(item);
            FGEPoint2D a=item.Points[0],b=item.Points[1];
            Point p=SourceToCanvas(center,viewport);
            Point edge=SourceToCanvas(new FGEPoint2D(
                (a.X+b.X)/2.0,(a.Y+b.Y)/2.0),viewport);
            // 偏移必须沿WPF已旋转/镜像后的边法线，而非原图未变换向量。
            double dx=edge.X-p.X,dy=edge.Y-p.Y;
            double length=Math.Sqrt(dx*dx+dy*dy);
            double r=length+20.0;
            return new Point(p.X+dx*r/length,p.Y+dy*r/length);
        }
        private void DrawRotationHandle(Canvas canvas,MVIImageViewport viewport,
            MVIImageOverlayItem item)
        {
            Point handlePoint=RotationHandleScreenPoint(item,viewport);
            Point center=SourceToCanvas(RotatedRectangleCenter(item),viewport);
            DrawLine(center.X,center.Y,handlePoint.X,handlePoint.Y,Brushes.Gold,1.2,canvas);
            Ellipse circle=new Ellipse {Width=12,Height=12,Fill=Brushes.Gold,
                Stroke=Brushes.DarkSlateGray,StrokeThickness=1.4,
                Tag="RotationHandle",IsHitTestVisible=false};
            System.Windows.Automation.AutomationProperties.SetAutomationId(circle,
                "MPW.Imaging.RotateOverlay.Handle");
            Canvas.SetLeft(circle,handlePoint.X-6);
            Canvas.SetTop(circle,handlePoint.Y-6);
            canvas.Children.Add(circle);
        }
        public int RotationHandleVisualCount
        { get { return OverlayCanvas.Children.OfType<Ellipse>()
            .Count(x=>string.Equals(x.Tag as string,"RotationHandle",StringComparison.Ordinal)); } }
        public int SecondaryRotationHandleVisualCount
        { get { return SecondaryOverlayCanvas.Children.OfType<Ellipse>()
            .Count(x=>string.Equals(x.Tag as string,"RotationHandle",StringComparison.Ordinal)); } }
        private bool BeginRotation(Point pointer,MVIImageViewport viewport,bool secondary)
        {
            if(ActiveImage==null || !OverlayMatchesActiveImage()) return false;
            MVIImageOverlayItem item=_selectedOverlayId.HasValue?
                _overlays.Find(_selectedOverlayId.Value):null;
            if(item!=null && item.Kind==MVIImageOverlayKind.RotatedRectangleRoi &&
                !item.IsLocked && _overlays.GetVisibleItems().Any(x=>x.Id==item.Id))
            {
                Point handle=RotationHandleScreenPoint(item,viewport);
                if(Math.Abs(pointer.X-handle.X)<=9 && Math.Abs(pointer.Y-handle.Y)<=9)
                {
                    if(!_overlayEdit.Begin(item,MVIImageOverlayEditKind.Rotate))return false;
                    _rotationStart=item;_rotatingOverlay=true;_rotatingSecondary=secondary;
                    return true;
                }
            }
            FGEPoint2D p=_viewTransform.DisplayToSource(new FGEPoint2D(
                viewport.ImageX(pointer.X),viewport.ImageY(pointer.Y)),
                ActiveImage.Width,ActiveImage.Height);
            IReadOnlyList<MVIImageOverlayItem> items=_overlays.GetVisibleItems();
            for(int i=items.Count-1;i>=0;i--)
                if(items[i].Kind==MVIImageOverlayKind.RotatedRectangleRoi &&
                    FGEGeometry2D.PointInPolygon(p,items[i].Points))
                {SelectOverlay(items[i].Id);return true;}
            SelectOverlay(null);return false;
        }
        private void UpdateRotation(Point pointer,MVIImageViewport viewport)
        {
            if(!_rotatingOverlay || _rotationStart==null || ActiveImage==null)return;
            if(!CanContinueOverlayGesture(_rotationStart))
            {
                _rotatingOverlay=false;_rotationStart=null;
                PixelStatus.Text="矩形已锁定、隐藏或删除；旋转已安全终止。";
                return;
            }
            FGEPoint2D center=RotatedRectangleCenter(_rotationStart);
            FGEPoint2D p=_viewTransform.DisplayToSource(new FGEPoint2D(
                viewport.ImageX(pointer.X),viewport.ImageY(pointer.Y)),
                ActiveImage.Width,ActiveImage.Height);
            if(p.DistanceTo(center)<1e-6)return;
            double angle=Math.Atan2(p.Y-center.Y,p.X-center.X)*180/Math.PI+90;
            try { ApplyOverlayEdit(_rotationStart.WithRotatedRectangleAngle(
                angle,ActiveImage.Width,ActiveImage.Height)); }
            catch(ArgumentOutOfRangeException)
            {PixelStatus.Text="旋转角点超出原图，保留最近一次有效矩形。";}
            catch(InvalidOperationException)
            {
                _rotatingOverlay=false;_rotationStart=null;
                PixelStatus.Text="旋转编辑被当前对象状态拒绝，已安全结束手势。";
            }
        }

        private bool TryPolygonVertexHandle(Point pointer,MVIImageViewport viewport,
            MVIImageOverlayItem item,out int index)
        {
            index=-1;
            for(int i=0;i<item.Points.Count;i++)
            {
                Point pixel=SourceToCanvas(item.Points[i],viewport);
                if(Math.Abs(pointer.X-pixel.X)<=7 && Math.Abs(pointer.Y-pixel.Y)<=7)
                { index=i;return true; }
            }
            return false;
        }

        /// <summary>逐顶点模式：Shift点边插点；Alt点金色顶点删除。屏幕拾取但保存原图坐标。</summary>
        private bool TryPolygonTopologyGesture(Point pointer,MVIImageViewport viewport)
        {
            ModifierKeys keys=Keyboard.Modifiers;
            bool insert=(keys & ModifierKeys.Shift)!=0;
            bool remove=(keys & ModifierKeys.Alt)!=0;
            if(!insert && !remove)return false;
            if(insert && remove)
            {
                PixelStatus.Text="插入和删除顶点请分别使用 Shift 或 Alt。";
                return true;
            }
            MVIImageOverlayItem item;
            try {item=RequireVisibleSelectedPolygon();}
            catch(InvalidOperationException)
            {
                PixelStatus.Text="请先选中可见的多边形ROI。";
                return true;
            }
            if(item.IsLocked)
            {
                PixelStatus.Text="多边形已锁定，请先解锁。";
                return true;
            }
            try
            {
                if(remove)
                {
                    int index;
                    if(!TryPolygonVertexHandle(pointer,viewport,item,out index))
                    {
                        PixelStatus.Text="Alt＋点击金色顶点可删除（至少保留三个点）。";
                        return true;
                    }
                    RemoveSelectedPolygonVertex(index);
                    PixelStatus.Text="已删除多边形顶点 "+index;
                }
                else
                {
                    int nearest=-1;
                    double minDistanceSquared=81;
                    FGEPoint2D projected=new FGEPoint2D(0,0);
                    for(int i=0;i<item.Points.Count;i++)
                    {
                        FGEPoint2D a=item.Points[i],b=item.Points[(i+1)%item.Points.Count];
                        Point from=SourceToCanvas(a,viewport),to=SourceToCanvas(b,viewport);
                        double dx=to.X-from.X,dy=to.Y-from.Y;
                        double len=dx*dx+dy*dy;
                        if(len<1e-10)continue;
                        double t=Math.Max(0,Math.Min(1,
                            ((pointer.X-from.X)*dx+(pointer.Y-from.Y)*dy)/len));
                        if(t<=0.005 || t>=0.995)continue;
                        double px=from.X+dx*t,py=from.Y+dy*t;
                        double distanceSquared=(pointer.X-px)*(pointer.X-px)+
                            (pointer.Y-py)*(pointer.Y-py);
                        if(distanceSquared<=minDistanceSquared)
                        {
                            minDistanceSquared=distanceSquared;
                            nearest=i;
                            projected=new FGEPoint2D(a.X+(b.X-a.X)*t,
                                a.Y+(b.Y-a.Y)*t);
                        }
                    }
                    if(nearest<0)
                    {
                        PixelStatus.Text="Shift＋点击多边形边线可插入顶点。";
                        return true;
                    }
                    InsertSelectedPolygonVertex(nearest,projected);
                    PixelStatus.Text="已在第"+nearest+"条边插入顶点";
                }
            }
            catch(ArgumentException e)
            {PixelStatus.Text="多边形拓扑无效："+e.Message;}
            catch(InvalidOperationException e)
            {PixelStatus.Text=e.Message;}
            return true;
        }

        private bool BeginPolygonVertexEdit(Point pointer,MVIImageViewport viewport,
            bool secondary)
        {
            if(ActiveImage==null || !OverlayMatchesActiveImage()) return false;
            MVIImageOverlayItem selected=_selectedOverlayId.HasValue ?
                _overlays.Find(_selectedOverlayId.Value):null;
            int index;
            if(selected!=null && selected.Kind==MVIImageOverlayKind.PolygonRoi &&
                selected.Layer==MVIImageOverlayLayer.Roi && !selected.IsLocked &&
                _overlays.GetVisibleItems().Any(x=>x.Id==selected.Id) &&
                TryPolygonVertexHandle(pointer,viewport,selected,out index))
            {
                if(!_overlayEdit.Begin(selected,MVIImageOverlayEditKind.PolygonVertex))return false;
                _vertexEditStart=selected;_vertexIndex=index;
                _editingVertex=true;_editingVertexSecondary=secondary;
                return true;
            }
            FGEPoint2D display=new FGEPoint2D(
                viewport.ImageX(pointer.X),viewport.ImageY(pointer.Y));
            FGEPoint2D source=_viewTransform.DisplayToSource(display,
                ActiveImage.Width,ActiveImage.Height);
            IReadOnlyList<MVIImageOverlayItem> visible=_overlays.GetVisibleItems();
            for(int i=visible.Count-1;i>=0;i--)
            {
                MVIImageOverlayItem item=visible[i];
                if(item.Kind==MVIImageOverlayKind.PolygonRoi &&
                    item.Layer==MVIImageOverlayLayer.Roi &&
                    FGEGeometry2D.PointInPolygon(source,item.Points))
                { SelectOverlay(item.Id);return true; }
            }
            SelectOverlay(null);
            return false;
        }

        private bool CanContinueOverlayGesture(MVIImageOverlayItem start)
        {
            if(start==null || ActiveImage==null || !OverlayMatchesActiveImage())return false;
            MVIImageOverlayItem current=_overlays.Find(start.Id);
            return current!=null && !current.IsLocked &&
                _overlays.GetVisibleItems().Any(x=>x.Id==start.Id);
        }

        private void UpdatePolygonVertexEdit(Point pointer,MVIImageViewport viewport)
        {
            if(!_editingVertex || _vertexEditStart==null || ActiveImage==null) return;
            if(!CanContinueOverlayGesture(_vertexEditStart))
            {
                _editingVertex=false;_vertexEditStart=null;
                PixelStatus.Text="多边形已锁定、隐藏或删除；顶点拖动已安全终止。";
                return;
            }
            FGEPoint2D source=_viewTransform.DisplayToSource(new FGEPoint2D(
                viewport.ImageX(pointer.X),viewport.ImageY(pointer.Y)),
                ActiveImage.Width,ActiveImage.Height);
            try
            {
                MVIImageOverlayItem updated=_vertexEditStart.WithMovedPolygonVertex(
                    _vertexIndex,source,ActiveImage.Width,ActiveImage.Height);
                ApplyOverlayEdit(updated);
            }
            catch(ArgumentException)
            {
                PixelStatus.Text="顶点位置会导致退化或自交，已保留上一个有效几何。";
            }
            catch(InvalidOperationException)
            {
                _editingVertex=false;_vertexEditStart=null;
                PixelStatus.Text="顶点修改被对象状态拒绝，已安全结束手势。";
            }
        }

        private bool TryResizeHandle(Point pointer,MVIImageViewport viewport,
            MVIImageOverlayItem item,out MVIImageResizeHandle handle)
        {
            handle = MVIImageResizeHandle.TopLeft;
            foreach (MVIImageResizeHandle candidate in Enum.GetValues(typeof(MVIImageResizeHandle)))
            {
                Point screen=SourceToCanvas(ResizeHandlePoint(item,candidate),viewport);
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
                if(!_overlayEdit.Begin(selected,MVIImageOverlayEditKind.Resize))return false;
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
            if (!CanContinueOverlayGesture(_resizeStart))
            {
                _resizing=false; _resizeStart=null;
                PixelStatus.Text="ROI已锁定、隐藏或删除；缩放已安全终止。";
                return;
            }
            try
            {
                MVIImageOverlayItem next=_resizeStart.Kind==MVIImageOverlayKind.RotatedRectangleRoi ?
                    _resizeStart.WithResizedRotatedHandle(_resizeHandle,source,
                        ActiveImage.Width,ActiveImage.Height) :
                    _resizeStart.WithResizedRoiHandle(_resizeHandle,source,
                        ActiveImage.Width,ActiveImage.Height);
                ApplyOverlayEdit(next);
            }
            catch(ArgumentOutOfRangeException)
            {PixelStatus.Text="调整后角点超出原图，保留上一有效尺寸。";}
            catch(InvalidOperationException)
            {
                _resizing=false;_resizeStart=null;
                PixelStatus.Text="ROI缩放已被当前对象状态拒绝。";
            }
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

        /// <summary>图层默认颜色属于当前Viewer外观，现有图元仍由唯一MVI Store管理。</summary>
        public int GetOverlayLayerColor(MVIImageOverlayLayer layer)
        {
            VerifyAccess();
            if (!Enum.IsDefined(typeof(MVIImageOverlayLayer),layer))
                throw new ArgumentOutOfRangeException("layer");
            int argb;
            if (_overlayLayerColors.TryGetValue(layer,out argb))return argb;
            switch(layer)
            {
                case MVIImageOverlayLayer.Roi: return unchecked((int)0xff0da6c3);
                case MVIImageOverlayLayer.Measurement: return unchecked((int)0xffe6a422);
                case MVIImageOverlayLayer.Annotation: return unchecked((int)0xffb9306e);
                case MVIImageOverlayLayer.Defect: return unchecked((int)0xffe9473d);
                default: return unchecked((int)0xffff4040);
            }
        }
        public void SetOverlayLayerColor(MVIImageOverlayLayer layer,int argb)
        {
            VerifyAccess();
            if (!Enum.IsDefined(typeof(MVIImageOverlayLayer),layer))
                throw new ArgumentOutOfRangeException("layer");
            _overlayLayerColors[layer]=argb;
            // 已锁定项不能绕过MVI编辑保护；现有其余项保留Id/坐标/层级。
            foreach(MVIImageOverlayItem item in _overlays.Items)
                if(item.Layer==layer && !item.IsLocked && item.Argb!=argb)
                    _overlays.Replace(item.WithStyle(argb,item.LineWidth));
        }
        private MVIImageOverlayItem ApplyLayerDefaultColor(MVIImageOverlayItem item)
        {
            if(item==null)throw new ArgumentNullException("item");
            // 只有使用构造函数默认ARGB的新增图元采用本Viewer的图层颜色。
            // 显式非默认颜色保持不变；未编辑色板前也与显示的默认色块一致。
            if(!item.IsLocked && item.Argb==unchecked((int)0xffff4040))
                return item.WithStyle(GetOverlayLayerColor(item.Layer),item.LineWidth);
            return item;
        }
        public MVIImageOverlayItem AddOverlay(MVIImageOverlayItem item)
        {
            VerifyAccess();
            if (ActiveImage == null) throw new InvalidOperationException("图像未加载。");
            return _overlays.Add(ApplyLayerDefaultColor(item));
        }

        /// <summary>在WPF所属线程一次写入已校验的叠加项并触发一次重绘。</summary>
        public int AddOverlays(IEnumerable<MVIImageOverlayItem> items)
        {
            VerifyAccess();
            if (ActiveImage == null) throw new InvalidOperationException("图像未加载。");
            if(items==null)throw new ArgumentNullException("items");
            return _overlays.AddRange(items.Select(ApplyLayerDefaultColor).ToArray());
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
            // 专业用户操作必须尊重锁定；但新图/关闭Viewer走会话Clear，
            // 会连同旧原图一起释放所有对象，不能残留跨源ROI。
            if (_overlays.Items.Any(x=>x.IsLocked &&
                (!layer.HasValue || x.Layer==layer.Value)))
                throw new InvalidOperationException("存在已锁定对象，请先解锁再清空。");
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
            MVIImageOverlayItem created;
            if(kind==MVIImageOverlayKind.RotatedRectangleRoi)
            {
                if(rect.Width<0.25 || rect.Height<0.25)return;
                created=AddRotatedRectangle(rect,0);
            }
            else created=AddOverlay(new MVIImageOverlayItem(kind, layer, rect, points, text));
            // 不调用SelectOverlay来清空刚画出的矩形SelectedRegion，保持已有Crop闭环。
            // 但确保刚完成的对象可立即在右侧列表操作、编辑和做ROI统计。
            _selectedOverlayId=created.Id;
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
                item.Kind == MVIImageOverlayKind.RotatedRectangleRoi ||
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
            if (item.Kind==MVIImageOverlayKind.LinePairMeasurement && item.Points.Count==4)
                return DistanceToSegment(p,item.Points[0],item.Points[1])<=tolerance ||
                    DistanceToSegment(p,item.Points[2],item.Points[3])<=tolerance;
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
            ApplyOverlayEdit(_movingOverlay.Translate(dx, dy));
        }

        private static MVIImageOverlayKind GetDrawingKind(MVIImageToolMode mode)
        {
            switch (mode)
            {
                case MVIImageToolMode.RectangleRoi: return MVIImageOverlayKind.RectangleRoi;
                case MVIImageToolMode.RotatedRectangleRoi:
                    return MVIImageOverlayKind.RotatedRectangleRoi;
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
            // 两个Pane不能同时拥有拖动；多点Polygon/LinePair在未捕获时仍可继续点选。
            if(_overlayEdit.IsActive || _dragging || _selecting || _secondaryDragging)return;
            Keyboard.Focus(SecondaryViewportRoot);
            if(_toolMode==MVIImageToolMode.LinePair)
            {
                if(e.ChangedButton!=MouseButton.Left)return;
                try { AppendLinePairAtSecondaryViewport(
                    e.GetPosition(SecondaryViewportRoot)); }
                catch(ArgumentException error)
                {PixelStatus.Text="右侧两线测量输入无效："+error.Message;}
                e.Handled=true;
                return;
            }
            if(_toolMode==MVIImageToolMode.RotateOverlay)
            {
                if(BeginRotation(e.GetPosition(SecondaryViewportRoot),_secondaryViewport,true))
                    CaptureGestureMouse(SecondaryViewportRoot);
                e.Handled=true;return;
            }
            if (_toolMode == MVIImageToolMode.EditPolygonVertex)
            {
                if(e.ChangedButton!=MouseButton.Left)return;
                Point polygonPointer=e.GetPosition(SecondaryViewportRoot);
                if(TryPolygonTopologyGesture(polygonPointer,_secondaryViewport))
                {e.Handled=true;return;}
                if (BeginPolygonVertexEdit(polygonPointer,
                    _secondaryViewport,true))
                    CaptureGestureMouse(SecondaryViewportRoot);
                e.Handled=true;return;
            }
            if (_toolMode == MVIImageToolMode.ResizeOverlay &&
                BeginResize(e.GetPosition(SecondaryViewportRoot),_secondaryViewport,true))
            {
                CaptureGestureMouse(SecondaryViewportRoot); e.Handled=true; return;
            }
            _secondaryDragging = true;
            _secondaryDragStart = e.GetPosition(SecondaryViewportRoot);
            CaptureGestureMouse(SecondaryViewportRoot);
            e.Handled = true;
        }
        private void OnSecondaryMouseUp(object sender, MouseButtonEventArgs e)
        {
            bool completeEdit=(_rotatingOverlay && _rotatingSecondary) ||
                (_editingVertex && _editingVertexSecondary) ||
                (_resizing && _resizingSecondary);
            if(_rotatingOverlay && _rotatingSecondary)
            {
                UpdateRotation(e.GetPosition(SecondaryViewportRoot),_secondaryViewport);
                _rotatingOverlay=false;_rotationStart=null;RaiseSelectionChanged();
            }
            if (_editingVertex && _editingVertexSecondary)
            {
                UpdatePolygonVertexEdit(e.GetPosition(SecondaryViewportRoot),_secondaryViewport);
                _editingVertex=false;_vertexEditStart=null;RaiseSelectionChanged();
            }
            if (_resizing && _resizingSecondary)
            {
                UpdateResize(e.GetPosition(SecondaryViewportRoot),_secondaryViewport);
                _resizing=false; _resizeStart=null;
                RaiseSelectionChanged();
            }
            _secondaryDragging = false;
            if(completeEdit)_overlayEdit.Complete();
            if (SecondaryViewportRoot.IsMouseCaptured) SecondaryViewportRoot.ReleaseMouseCapture();
            e.Handled = true;
        }
        private void OnSecondaryMouseMove(object sender, MouseEventArgs e)
        {
            if (!IsSideBySideVisible || _session.Result == null) return;
            Point pointer = e.GetPosition(SecondaryViewportRoot);
            if(_rotatingOverlay && _rotatingSecondary && e.LeftButton==MouseButtonState.Pressed)
                UpdateRotation(pointer,_secondaryViewport);
            else if (_editingVertex && _editingVertexSecondary &&
                e.LeftButton==MouseButtonState.Pressed)
                UpdatePolygonVertexEdit(pointer,_secondaryViewport);
            else if (_resizing && _resizingSecondary && e.LeftButton == MouseButtonState.Pressed)
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
                if(_toolMode==MVIImageToolMode.LinePair && _polygonDraft.Count>0)
                {
                    _draftHover=new FGEPoint2D(x+0.5,y+0.5);
                    DrawDraftPolyline();
                }
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
            // 两个Pane不能同时拥有拖动；多点Polygon/LinePair在未捕获时仍可继续点选。
            if(_overlayEdit.IsActive || _dragging || _selecting || _secondaryDragging)return;
            Keyboard.Focus(ViewportRoot);
            Point pointer = e.GetPosition(ViewportRoot);
            int x,y;
            if(_toolMode==MVIImageToolMode.RotateOverlay)
            {
                if(BeginRotation(pointer,_viewport,false))
                {CaptureGestureMouse(ViewportRoot);e.Handled=true;}
                return;
            }
            if (_toolMode == MVIImageToolMode.EditPolygonVertex)
            {
                if(e.ChangedButton!=MouseButton.Left)return;
                if(TryPolygonTopologyGesture(pointer,_viewport))
                {e.Handled=true;return;}
                if(BeginPolygonVertexEdit(pointer,_viewport,false))
                { CaptureGestureMouse(ViewportRoot);e.Handled=true; }
                return;
            }
            if (_toolMode == MVIImageToolMode.ResizeOverlay)
            {
                if (BeginResize(pointer,_viewport,false))
                { CaptureGestureMouse(ViewportRoot); e.Handled=true; }
                return;
            }
            if(_toolMode==MVIImageToolMode.LinePair)
            {
                if(e.ChangedButton!=MouseButton.Left)return;
                if(!TryImagePoint(pointer,out x,out y))return;
                try { AppendLinePairPoint(new FGEPoint2D(x+0.5,y+0.5)); }
                catch(ArgumentException error)
                { PixelStatus.Text="两线测量输入无效："+error.Message; }
                e.Handled=true;return;
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
                    (_toolMode == MVIImageToolMode.Angle && _polygonDraft.Count == 3) ||
                    (_toolMode == MVIImageToolMode.LinePair && _polygonDraft.Count == 4))
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
                    if(!_overlayEdit.Begin(hit,MVIImageOverlayEditKind.Move))
                    {PixelStatus.Text="对象状态已变化，移动未启动。";return;}
                    _moving = true;
                    _movingOverlay = hit;
                    _moveAnchor = CanvasToSource(pointer);
                    _region = null;
                    DrawSelection();
                    CaptureGestureMouse(ViewportRoot);
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
            CaptureGestureMouse(ViewportRoot);
            e.Handled = true;
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            bool completeEdit=_moving ||
                (_rotatingOverlay && !_rotatingSecondary) ||
                (_editingVertex && !_editingVertexSecondary) ||
                (_resizing && !_resizingSecondary);
            if(_rotatingOverlay && !_rotatingSecondary)
            {
                UpdateRotation(e.GetPosition(ViewportRoot),_viewport);
                _rotatingOverlay=false;_rotationStart=null;RaiseSelectionChanged();
            }
            if (_editingVertex && !_editingVertexSecondary)
            {
                UpdatePolygonVertexEdit(e.GetPosition(ViewportRoot),_viewport);
                _editingVertex=false;_vertexEditStart=null;RaiseSelectionChanged();
            }
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
            if(completeEdit)_overlayEdit.Complete();
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
            else if (_editingVertex && !_editingVertexSecondary &&
                e.LeftButton==MouseButtonState.Pressed)
                UpdatePolygonVertexEdit(pointer,_viewport);
            else if(_rotatingOverlay && !_rotatingSecondary &&
                e.LeftButton==MouseButtonState.Pressed)
                UpdateRotation(pointer,_viewport);
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
            if (!_dragging && !_selecting && !_resizing && !_editingVertex &&
                !_rotatingOverlay && ActiveImage != null)
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
