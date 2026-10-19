using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;
using CxVisual.Foundation.Geometry;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Visual;

namespace CxVisual.Mechanism.Presentation.Wpf
{
    /// <summary>WPF技术适配层：只负责输入和投影绘制，不拥有工程真值或历史。</summary>
    public sealed class SceneView : FrameworkElement
    {
        private readonly List<CxSceneItem> _items = new List<CxSceneItem>();
        private Guid? _selection;
        private Point _dragStart;
        private double _dragX;
        private double _dragY;
        private bool _dragging;
        private readonly HashSet<Guid> _selectedItems=new HashSet<Guid>();
        public IReadOnlyList<Guid> SelectedVisualIds
        {get{return new List<Guid>(_selectedItems).AsReadOnly();}}
        /// <summary>由Studio组织选择结果回写画布，解决双向通知时只剩最后一个的多选丢失。</summary>
        public void SetSelectedVisualIds(IEnumerable<Guid> ids)
        {
            if(ids==null)throw new ArgumentNullException("ids");
            _selectedItems.Clear();
            Guid? last=null;
            foreach(Guid id in ids)
            {
                if(!_items.Exists(x=>x.Id==id&&x.IsSelectable))continue;
                if(_selectedItems.Add(id))last=id;
            }
            _selection=last;
            InvalidateVisual();
        }
        private bool _marquee;
        private Point _marqueeStart,_marqueeEnd;
        public event Action<IReadOnlyList<Guid>> SelectionSetRequested;
        private MVIVisualHandle _activeHandle;
        private bool _handleDragging;
        private Point _handlePointer;
        public event EventHandler<MPWVisualHandleEventArgs> HandleEditRequested;
        private readonly VisualCollection _visualChildren;
        private readonly Canvas _widgetCanvas;
        private Func<Guid,Stream> _assetResolver;
        private readonly Dictionary<Guid,ImageSource> _assetCache=new Dictionary<Guid,ImageSource>();
        private readonly Dictionary<Guid,string> _assetErrors=new Dictionary<Guid,string>();
        public IReadOnlyDictionary<Guid,string> AssetErrors{get{return _assetErrors;}}
        public void SetAssetResolver(Func<Guid,Stream> resolver)
        {
            _assetResolver=resolver;
            _assetCache.Clear();_assetErrors.Clear();InvalidateVisual();
        }
        private ImageSource AssetImage(CxSceneItem item)
        {
            if(!item.AssetId.HasValue||_assetResolver==null)return null;
            Guid id=item.AssetId.Value;
            ImageSource found;if(_assetCache.TryGetValue(id,out found))return found;
            if(_assetErrors.ContainsKey(id))return null;
            try
            {
                using(Stream stream=_assetResolver(id))
                {
                    if(stream==null)throw new InvalidDataException("visual.asset.unavailable");
                    found=item.ElementTypeId=="core.svg"?
                        (ImageSource)MPWVisualAssetRenderer.DecodeSvg(stream):
                        MPWVisualAssetRenderer.DecodeBitmap(stream,item.MediaType);
                }
                _assetCache.Add(id,found);
                return found;
            }
            catch(Exception ex)
            {
                _assetErrors[id]=ex.GetType().Name+": "+ex.Message;
                return null; // 画面显示资源错误而不是使整个WPF Designer崩溃
            }
        }
        private bool _designMode=true;
        private bool _resetWidgetLayerOnNextScene;
        private double _zoom=1,_panX,_panY;
        private bool _panning;private Point _panPointer;
        private bool _gridSnap;
        public double Zoom{get{return _zoom;}}
        public bool SnapToGrid{get{return _gridSnap;}set{_gridSnap=value;}}
        public void SetViewport(double zoom,double panX,double panY)
        {
            if(double.IsNaN(zoom)||double.IsInfinity(zoom)||zoom<0.05||zoom>16||
                double.IsNaN(panX)||double.IsNaN(panY)||double.IsInfinity(panX)||double.IsInfinity(panY))
                throw new ArgumentOutOfRangeException("zoom");
            _zoom=zoom;_panX=panX;_panY=panY;
            _widgetCanvas.RenderTransform=new MatrixTransform(
                new Matrix(zoom,0,0,zoom,panX,panY));
            InvalidateVisual();
        }
        private Point WorldPoint(Point screen)
        {return new Point((screen.X-_panX)/_zoom,(screen.Y-_panY)/_zoom);}
        private Point ScreenPoint(Point world)
        {return new Point(world.X*_zoom+_panX,world.Y*_zoom+_panY);}
        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            if(!IsDesignMode){base.OnMouseWheel(e);return;}
            Point center=e.GetPosition(this);double before=_zoom;
            double next=Math.Max(0.05,Math.Min(16,_zoom*(e.Delta>0?1.15:1/1.15)));
            Point target=WorldPoint(center);
            SetViewport(next,center.X-target.X*next,center.Y-target.Y*next);
            e.Handled=true;
        }
        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            if(_designMode&&e.ChangedButton==MouseButton.Middle)
            {_panning=true;_panPointer=e.GetPosition(this);CaptureMouse();e.Handled=true;return;}
            base.OnMouseDown(e);
        }
        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            if(_panning&&e.ChangedButton==MouseButton.Middle)
            {_panning=false;if(IsMouseCaptured)ReleaseMouseCapture();e.Handled=true;return;}
            base.OnMouseUp(e);
        }
        public SceneView()
        {
            _widgetCanvas=new Canvas();
            _visualChildren=new VisualCollection(this);
            _visualChildren.Add(_widgetCanvas);
            Focusable=true;
        }
        protected override int VisualChildrenCount{get{return _visualChildren.Count;}}
        protected override System.Windows.Media.Visual GetVisualChild(int index)
        {
            if(index<0||index>=_visualChildren.Count)throw new ArgumentOutOfRangeException("index");
            return _visualChildren[index];
        }
        protected override Size MeasureOverride(Size availableSize)
        {
            double w=double.IsInfinity(availableSize.Width)?720:availableSize.Width;
            double h=double.IsInfinity(availableSize.Height)?520:availableSize.Height;
            _widgetCanvas.Measure(new Size(w,h));
            return new Size(w,h);
        }
        protected override Size ArrangeOverride(Size finalSize)
        {
            _widgetCanvas.Arrange(new Rect(new Point(0,0),finalSize));
            return finalSize;
        }
        /// <summary>设计模式只允许画布选择移动；运行预览才允许真实控件接收输入。</summary>
        public bool IsDesignMode
        {
            get{return _designMode;}
            set
            {
                // 退出预览后，下次投影应重新按工程定义物化Widget。
                // 否则变量失联返回null时旧的运行态值会遗留在重用的TextBox/CheckBox中。
                if(!_designMode&&value)_resetWidgetLayerOnNextScene=true;
                _designMode=value;
                foreach(UIElement element in _widgetCanvas.Children)
                    element.IsHitTestVisible=!_designMode;
            }
        }
        public event EventHandler<MPWWidgetEventArgs> WidgetEventRequested;
        private void BuildWidgetLayer()
        {
            _widgetCanvas.Children.Clear();
            foreach(CxSceneItem item in _items)
            {
                FrameworkElement widget=MPWWidgetPresenter.Create(item,OnWidgetEvent);
                if(widget==null)continue;
                widget.IsHitTestVisible=!_designMode;
                _widgetCanvas.Children.Add(widget);
            }
            InvalidateMeasure();
            InvalidateArrange();
        }
        private void OnWidgetEvent(MPWWidgetEventArgs args)
        {
            if(_designMode)return;
            EventHandler<MPWWidgetEventArgs> callback=WidgetEventRequested;
            if(callback!=null)callback(this,args);
        }

        public event EventHandler<CxNodeEventArgs> NodeActivated;
        public event EventHandler<CxMoveEventArgs> MoveRequested;
        private bool CanReuseWidgets(IList<CxSceneItem> next)
        {
            if(next==null||next.Count!=_items.Count)return false;
            int widgetCount=0;
            for(int i=0;i<next.Count;i++)
            {
                CxSceneItem before=_items[i],after=next[i];
                if(before.Id!=after.Id||before.ElementTypeId!=after.ElementTypeId)
                    return false;
                if(MPWWidgetPresenter.Supports(after.ElementTypeId))widgetCount++;
            }
            return widgetCount==_widgetCanvas.Children.Count;
        }
        public void SetScene(IList<CxSceneItem> items, Guid? selected)
        {
            bool reuse=!_resetWidgetLayerOnNextScene&&CanReuseWidgets(items);
            _resetWidgetLayerOnNextScene=false;
            _items.Clear();
            if(items!=null)_items.AddRange(items);
            // 外部切换选择时清除旧高亮；鼠标本地维护多选时不被场景重投影覆盖。
            bool changedSelection=_selection!=selected;
            if(changedSelection)_selectedItems.Clear();
            _selection=selected;
            if(changedSelection&&selected.HasValue)_selectedItems.Add(selected.Value);
            _selectedItems.RemoveWhere(id=>!_items.Exists(item=>item.Id==id));
            if(!reuse)BuildWidgetLayer();
            else
            {
                int index=0;
                foreach(CxSceneItem item in _items)
                    if(MPWWidgetPresenter.Supports(item.ElementTypeId))
                        MPWWidgetPresenter.UpdateAppearance(
                            (FrameworkElement)_widgetCanvas.Children[index++],item);
            }
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            dc.DrawRectangle(Brushes.White, null, new Rect(new Point(0, 0), RenderSize));
            Pen gridPen = new Pen(new SolidColorBrush(Color.FromRgb(237, 241, 247)), 1);
            for (double x = 0; x < ActualWidth; x += 25) dc.DrawLine(gridPen, new Point(x, 0), new Point(x, ActualHeight));
            for (double y = 0; y < ActualHeight; y += 25) dc.DrawLine(gridPen, new Point(0, y), new Point(ActualWidth, y));
            dc.PushTransform(new MatrixTransform(new Matrix(_zoom,0,0,_zoom,_panX,_panY)));
            foreach (CxSceneItem item in _items)
            {
                bool selected = _selectedItems.Contains(item.Id)||
                    (_selection.HasValue&&_selection.Value==item.Id);
                if(item.Shape!=null)
                {
                    MPWShapeRenderer.Draw(dc,item,selected&&_dragging?_dragX:0,
                        selected&&_dragging?_dragY:0);
                    if(selected)
                    {
                        Rect highlight=new Rect(item.Bounds.X+( _dragging?_dragX:0),
                            item.Bounds.Y+(_dragging?_dragY:0),
                            Math.Max(1,item.Bounds.Width),Math.Max(1,item.Bounds.Height));
                        dc.DrawRectangle(null,new Pen(Brushes.DodgerBlue,0.8){DashStyle=DashStyles.Dash},highlight);
                    }
                    continue;
                }
                if(item.ElementTypeId=="core.image"||item.ElementTypeId=="core.svg")
                {
                    ImageSource image=AssetImage(item);
                    MRCTransform transform=item.WorldTransform;
                    dc.PushTransform(new MatrixTransform(new Matrix(
                        transform.M11,transform.M21,transform.M12,transform.M22,
                        transform.X+(_dragging&&selected?_dragX:0),
                        transform.Y+(_dragging&&selected?_dragY:0))));
                    dc.PushOpacity(item.Style.Opacity);
                    if(image!=null)
                        dc.DrawImage(image,new Rect(0,0,item.LocalWidth,item.LocalHeight));
                    else
                    {
                        dc.DrawRectangle(Brushes.MistyRose,new Pen(Brushes.IndianRed,1),
                            new Rect(0,0,item.LocalWidth,item.LocalHeight));
                        var label=new FormattedText(item.AssetId.HasValue?"资源不可用":"未绑定资源",
                            CultureInfo.CurrentCulture,FlowDirection.LeftToRight,
                            new Typeface("Segoe UI"),10,Brushes.DarkRed,1.0);
                        dc.DrawText(label,new Point(4,4));
                    }
                    dc.Pop();dc.Pop();
                    continue;
                }
                if(MPWWidgetPresenter.Supports(item.ElementTypeId))continue;
                if(item.ElementTypeId=="core.text")
                {
                    MRCVisualStyle textStyle=item.Style;
                    var matrix=item.WorldTransform;
                    dc.PushTransform(new MatrixTransform(new Matrix(matrix.M11,matrix.M21,
                        matrix.M12,matrix.M22,matrix.X+(_dragging&&selected?_dragX:0),
                        matrix.Y+(_dragging&&selected?_dragY:0))));
                    var textBrush=MPWShapeRenderer.BrushOf(textStyle.Fill,Brushes.MidnightBlue);
                    if(textStyle.Fill.Kind==MRCBrushKind.None)textBrush=Brushes.MidnightBlue;
                    var formatted=new FormattedText(item.Text,CultureInfo.CurrentUICulture,
                        FlowDirection.LeftToRight,new Typeface(textStyle.FontFamily),
                        textStyle.FontSize,textBrush,1.0);
                    formatted.MaxTextWidth=Math.Max(1,item.Bounds.Width);
                    dc.PushOpacity(textStyle.Opacity);
                    dc.DrawText(formatted,new Point(0,0));
                    dc.Pop();
                    dc.Pop();
                    continue;
                }

                double dx = selected && _dragging ? _dragX : 0;
                double dy = selected && _dragging ? _dragY : 0;
                Rect bounds = new Rect(item.Bounds.X + dx, item.Bounds.Y + dy, item.Bounds.Width, item.Bounds.Height);
                Brush fill = selected ? new SolidColorBrush(Color.FromRgb(206, 232, 253)) :
                    new SolidColorBrush(Color.FromRgb(229, 239, 252));
                Pen pen = new Pen(selected ? Brushes.DodgerBlue : Brushes.SteelBlue, selected ? 2.5 : 1.2);
                var geometry = new StreamGeometry();
                using (var shape = geometry.Open())
                {
                    shape.BeginFigure(new Point(item.TopLeft.X + dx, item.TopLeft.Y + dy), true, true);
                    shape.LineTo(new Point(item.TopRight.X + dx, item.TopRight.Y + dy), true, false);
                    shape.LineTo(new Point(item.BottomRight.X + dx, item.BottomRight.Y + dy), true, false);
                    shape.LineTo(new Point(item.BottomLeft.X + dx, item.BottomLeft.Y + dy), true, false);
                }
                geometry.Freeze(); dc.DrawGeometry(fill, pen, geometry);
                FormattedText name = new FormattedText(item.Name, CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, new Typeface("Segoe UI"), 13, Brushes.MidnightBlue, 1.0);
                dc.DrawText(name, new Point(bounds.Left + 12, bounds.Top + 12));
            }
            if(IsDesignMode&&_selection.HasValue)
            {
                CxSceneItem edited=_items.Find(i=>i.Id==_selection.Value);
                if(edited!=null)
                {
                    foreach(MVIVisualHandle handle in MVIVisualEditing.Handles(edited))
                    {
                        bool moving=_handleDragging&&_activeHandle!=null&&
                            handle.Kind==_activeHandle.Kind&&
                            handle.CommandIndex==_activeHandle.CommandIndex;
                        Point center=moving?_handlePointer:
                            new Point(handle.WorldPoint.X,handle.WorldPoint.Y);
                        Brush fill=handle.Kind==MVIVisualHandleKind.Rotate?Brushes.Orange:
                            handle.Kind==MVIVisualHandleKind.Resize?Brushes.MediumPurple:
                            handle.Kind==MVIVisualHandleKind.Anchor?Brushes.White:Brushes.Gold;
                        dc.DrawEllipse(fill,new Pen(Brushes.DodgerBlue,1.5/_zoom),center,5/_zoom,5/_zoom);
                        if(moving)
                            dc.DrawLine(new Pen(Brushes.DodgerBlue,1/_zoom){DashStyle=DashStyles.Dash},
                                new Point(handle.WorldPoint.X,handle.WorldPoint.Y),center);
                    }
                    // 鼠标按住时直接预览整条Bezier/Path，不污染MEM Revision；
                    // 松开后由统一EditHandle事务一次性提交精确曲线。
                    if(_handleDragging&&_activeHandle!=null&&edited.Shape!=null&&
                        (_activeHandle.Kind==MVIVisualHandleKind.Anchor||
                         _activeHandle.Kind==MVIVisualHandleKind.Control1||
                         _activeHandle.Kind==MVIVisualHandleKind.Control2))
                    {
                        FGEPathHandleKind kind=_activeHandle.Kind==MVIVisualHandleKind.Anchor?
                            FGEPathHandleKind.Anchor:_activeHandle.Kind==MVIVisualHandleKind.Control1?
                            FGEPathHandleKind.Control1:FGEPathHandleKind.Control2;
                        FGEPoint2D point=edited.GeometryToWorld.Inverse().TransformPoint(
                            new FGEPoint2D(_handlePointer.X,_handlePointer.Y));
                        FGEPath2D preview=FGEPathEditing.MoveHandle(
                            edited.Shape.Geometry,_activeHandle.CommandIndex,kind,point);
                        Geometry geometry=MPWShapeRenderer.BuildGeometry(preview,edited.GeometryToWorld);
                        dc.DrawGeometry(null,new Pen(Brushes.OrangeRed,2/_zoom)
                            {DashStyle=DashStyles.Dash},geometry);
                    }
                }
            }
            if(_marquee)
            {
                double x=Math.Min(_marqueeStart.X,_marqueeEnd.X);
                double y=Math.Min(_marqueeStart.Y,_marqueeEnd.Y);
                var rect=new Rect(x,y,Math.Abs(_marqueeEnd.X-_marqueeStart.X),
                    Math.Abs(_marqueeEnd.Y-_marqueeStart.Y));
                dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(42,35,117,218)),
                    new Pen(Brushes.DodgerBlue,1){DashStyle=DashStyles.Dash},rect);
            }
            dc.Pop();
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Focus();
            Point point = WorldPoint(e.GetPosition(this));
            if(!_designMode)return;
            if(_designMode&&_selection.HasValue)
            {
                CxSceneItem selected=_items.Find(i=>i.Id==_selection.Value);
                if(selected!=null)
                {
                    MVIVisualHandle handle=MVIVisualEditing.HitHandle(selected,point.X,point.Y,8/_zoom);
                    if(handle!=null)
                    {
                        _activeHandle=handle;
                        _handleDragging=true;
                        _handlePointer=point;
                        _dragging=false;
                        CaptureMouse();
                        InvalidateVisual();
                        e.Handled=true;
                        return;
                    }
                }
            }
            Guid? hit = null;
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                CxSceneItem item = _items[i];
                if (item.Contains(point.X, point.Y))
                { hit = item.Id; break; }
            }
            bool extend=(Keyboard.Modifiers&ModifierKeys.Control)!=0||
                (Keyboard.Modifiers&ModifierKeys.Shift)!=0;
            if(!extend)_selectedItems.Clear();
            if(hit.HasValue)
            {
                if(extend&&!_selectedItems.Add(hit.Value))_selectedItems.Remove(hit.Value);
                else _selectedItems.Add(hit.Value);
            }
            _selection=hit;
            if(hit==null)
            {
                _marquee=true;_marqueeStart=point;_marqueeEnd=point;
                CaptureMouse();
            }
            _dragStart = point;
            _dragX = 0; _dragY = 0;
            _dragging = hit.HasValue;
            if (_dragging) CaptureMouse();
            EventHandler<CxNodeEventArgs> activated = NodeActivated;
            if (activated != null) activated(this, new CxNodeEventArgs(hit));
            if(hit.HasValue&&SelectionSetRequested!=null)
                SelectionSetRequested(new List<Guid>(_selectedItems).AsReadOnly());
            InvalidateVisual();
            e.Handled = true;
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if(_marquee)
            {
                _marqueeEnd=WorldPoint(e.GetPosition(this));
                InvalidateVisual();return;
            }
            if(_panning)
            {
                Point position=e.GetPosition(this);
                SetViewport(_zoom,_panX+position.X-_panPointer.X,
                    _panY+position.Y-_panPointer.Y);
                _panPointer=position;
                return;
            }
            if(_handleDragging)
            {
                _handlePointer=WorldPoint(e.GetPosition(this));
                InvalidateVisual();
                return;
            }
            if (!_dragging) return;
            Point point = WorldPoint(e.GetPosition(this));
            _dragX = point.X - _dragStart.X;
            _dragY = point.Y - _dragStart.Y;
            InvalidateVisual();
        }
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if(_marquee)
            {
                _marquee=false;
                if(IsMouseCaptured)ReleaseMouseCapture();
                FGEPoint2D a=new FGEPoint2D(_marqueeStart.X,_marqueeStart.Y);
                FGEPoint2D b=new FGEPoint2D(_marqueeEnd.X,_marqueeEnd.Y);
                FGERect2D selection=FGERect2D.FromPoints(a,b);
                foreach(CxSceneItem item in _items)
                    if(item.IsSelectable&&(selection.Intersects(item.Bounds)||
                        selection.Contains(new FGEPoint2D(item.Bounds.X,item.Bounds.Y))))
                        _selectedItems.Add(item.Id);
                if(_selectedItems.Count!=0)
                {
                    foreach(Guid id in _selectedItems){_selection=id;break;}
                    if(SelectionSetRequested!=null)
                        SelectionSetRequested(new List<Guid>(_selectedItems).AsReadOnly());
                }
                InvalidateVisual();e.Handled=true;return;
            }
            if(_handleDragging)
            {
                MVIVisualHandle handle=_activeHandle;
                Point current=WorldPoint(e.GetPosition(this));
                _activeHandle=null;
                _handleDragging=false;
                if(IsMouseCaptured)ReleaseMouseCapture();
                if(handle!=null&&(Math.Abs(current.X-handle.WorldPoint.X)>0.8||
                    Math.Abs(current.Y-handle.WorldPoint.Y)>0.8))
                {
                    EventHandler<MPWVisualHandleEventArgs> edited=HandleEditRequested;
                    if(edited!=null)edited(this,
                        new MPWVisualHandleEventArgs(handle,current.X,current.Y));
                }
                InvalidateVisual();
                e.Handled=true;
                return;
            }
            if (!_dragging) return;
            _dragging = false;
            if (IsMouseCaptured) ReleaseMouseCapture();
            if (_selection.HasValue && (Math.Abs(_dragX) > 1 || Math.Abs(_dragY) > 1))
            {
                CxSceneItem original = _items.Find(x => x.Id == _selection.Value);
                EventHandler<CxMoveEventArgs> moved = MoveRequested;
                if (original != null && moved != null)
                {
                    double x, y; original.PositionAfterWorldDelta(_dragX, _dragY, out x, out y);
                    if(_gridSnap){x=Math.Round(x/25)*25;y=Math.Round(y/25)*25;}
                    moved(this, new CxMoveEventArgs(original.Id, x, y));
                }
            }
            _dragX = 0; _dragY = 0;
            InvalidateVisual();
        }
        public void CancelInteraction()
        {_dragging=false;_marquee=false;_handleDragging=false;_activeHandle=null;_panning=false;_dragX=0;_dragY=0;
            if(IsMouseCaptured)ReleaseMouseCapture();InvalidateVisual();}
        protected override void OnKeyDown(KeyEventArgs e){if(e.Key==Key.Escape){CancelInteraction();e.Handled=true;}base.OnKeyDown(e);}
        protected override void OnLostMouseCapture(MouseEventArgs e){if(_dragging||_handleDragging)CancelInteraction();base.OnLostMouseCapture(e);}
    }
}
