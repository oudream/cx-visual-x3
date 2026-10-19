using System;
using System.Collections.Generic;
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
        private MVIVisualHandle _activeHandle;
        private bool _handleDragging;
        private Point _handlePointer;
        public event EventHandler<MPWVisualHandleEventArgs> HandleEditRequested;
        private readonly VisualCollection _visualChildren;
        private readonly Canvas _widgetCanvas;
        private bool _designMode=true;
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
            bool reuse=CanReuseWidgets(items);
            _items.Clear();
            if(items!=null)_items.AddRange(items);
            _selection=selected;
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
            foreach (CxSceneItem item in _items)
            {
                bool selected = _selection.HasValue && _selection.Value == item.Id;
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
                        dc.DrawEllipse(fill,new Pen(Brushes.DodgerBlue,1.5),center,5,5);
                        if(moving)
                            dc.DrawLine(new Pen(Brushes.DodgerBlue,1){DashStyle=DashStyles.Dash},
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
                        dc.DrawGeometry(null,new Pen(Brushes.OrangeRed,2)
                            {DashStyle=DashStyles.Dash},geometry);
                    }
                }
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Focus();
            Point point = e.GetPosition(this);
            if(_designMode&&_selection.HasValue)
            {
                CxSceneItem selected=_items.Find(i=>i.Id==_selection.Value);
                if(selected!=null)
                {
                    MVIVisualHandle handle=MVIVisualEditing.HitHandle(selected,point.X,point.Y);
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
            _selection = hit;
            _dragStart = point;
            _dragX = 0; _dragY = 0;
            _dragging = hit.HasValue;
            if (_dragging) CaptureMouse();
            EventHandler<CxNodeEventArgs> activated = NodeActivated;
            if (activated != null) activated(this, new CxNodeEventArgs(hit));
            InvalidateVisual();
            e.Handled = true;
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if(_handleDragging)
            {
                _handlePointer=e.GetPosition(this);
                InvalidateVisual();
                return;
            }
            if (!_dragging) return;
            Point point = e.GetPosition(this);
            _dragX = point.X - _dragStart.X;
            _dragY = point.Y - _dragStart.Y;
            InvalidateVisual();
        }
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if(_handleDragging)
            {
                MVIVisualHandle handle=_activeHandle;
                Point current=e.GetPosition(this);
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
                    moved(this, new CxMoveEventArgs(original.Id, x, y));
                }
            }
            _dragX = 0; _dragY = 0;
            InvalidateVisual();
        }
        public void CancelInteraction()
        {_dragging=false;_handleDragging=false;_activeHandle=null;_dragX=0;_dragY=0;
            if(IsMouseCaptured)ReleaseMouseCapture();InvalidateVisual();}
        protected override void OnKeyDown(KeyEventArgs e){if(e.Key==Key.Escape){CancelInteraction();e.Handled=true;}base.OnKeyDown(e);}
        protected override void OnLostMouseCapture(MouseEventArgs e){if(_dragging||_handleDragging)CancelInteraction();base.OnLostMouseCapture(e);}
    }
}
