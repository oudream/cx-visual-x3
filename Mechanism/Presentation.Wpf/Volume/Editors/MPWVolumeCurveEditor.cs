using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CxVisual.Foundation.Volume;

namespace CxVisual.Mechanism.Presentation.Wpf.Volume
{
    /// <summary>
    /// WPF中直接编辑既有FVO TransferPoint列表：左键拖节点、双击添加、右键删除。
    /// Color曲线只调X位置，RGB值在旁边三滑杆编辑；透明度曲线同时调整X/Y。
    /// 不复制数据模型、不引入适配层，编辑后直接生成真实GPU LUT。
    /// </summary>
    public sealed class MPWVolumeCurveEditor : FrameworkElement
    {
        private readonly List<FVOTransferPoint> _points;
        private readonly bool _colorMode;
        private int _selected=-1;
        private bool _dragging;
        public event EventHandler Changed;
        public event EventHandler SelectionChanged;
        public MPWVolumeCurveEditor(List<FVOTransferPoint> points,bool colorMode)
        {
            if(points==null)throw new ArgumentNullException("points");
            _points=points; _colorMode=colorMode;
            MinWidth=190;MinHeight=180;Height=230;
            SnapsToDevicePixels=true;Focusable=true;
        }

        public float[] Histogram { get; set; }
        public int PointCount { get { return _points.Count; } }
        public FVOTransferPoint SelectedPoint
        {
            get { return _selected>=0 && _selected<_points.Count ? _points[_selected] : null; }
        }

        public void SelectPoint(int index)
        {
            if(index<0 || index>=_points.Count)throw new ArgumentOutOfRangeException("index");
            _selected=index; InvalidateVisual();
            EventHandler h=SelectionChanged;if(h!=null)h(this,EventArgs.Empty);
        }

        public void AddControlPoint(float x,float y)
        {
            if(_points.Count>=32)throw new InvalidOperationException("最多32个控制点。");
            float px=Clamp(x),py=Clamp(y);
            FVOColor color=_colorMode ?
                new FVOColorTransferFunction(_points).Evaluate(px) : FVOColor.White;
            FVOTransferPoint p=new FVOTransferPoint(px,py,color);
            _points.Add(p);_points.Sort((a,b)=>a.X.CompareTo(b.X));
            SelectPoint(_points.IndexOf(p));
            NotifyChanged();
        }

        public void UpdateSelectedColor(byte red,byte green,byte blue)
        {
            if(!_colorMode||SelectedPoint==null)return;
            SelectedPoint.Color=new FVOColor(red,green,blue);
            NotifyChanged();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(double.IsInfinity(availableSize.Width)?280:Math.Max(190,availableSize.Width),230);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double w=Math.Max(100,ActualWidth),h=Math.Max(140,ActualHeight);
            Rect area=new Rect(22,30,Math.Max(20,w-38),Math.Max(20,h-49));
            dc.DrawRectangle(Brushes.White,new Pen(new SolidColorBrush(Color.FromRgb(161,174,191)),1),
                new Rect(0,0,w,h));
            for(int i=0;i<=4;i++)
            {
                double gx=area.Left+area.Width*i/4.0;
                double gy=area.Top+area.Height*i/4.0;
                Pen grid=new Pen(new SolidColorBrush(Color.FromRgb(226,233,241)),1);
                dc.DrawLine(grid,new Point(gx,area.Top),new Point(gx,area.Bottom));
                dc.DrawLine(grid,new Point(area.Left,gy),new Point(area.Right,gy));
            }
            dc.DrawRectangle(null,new Pen(Brushes.SlateGray,1),area);
            if(Histogram!=null && Histogram.Length>1)
            {
                Pen bins=new Pen(new SolidColorBrush(Color.FromRgb(176,193,211)),1);
                for(int i=1;i<Histogram.Length;i++)
                {
                    double x0=area.Left+(i-1)*area.Width/(Histogram.Length-1.0);
                    double x1=area.Left+i*area.Width/(Histogram.Length-1.0);
                    dc.DrawLine(bins,new Point(x0,area.Bottom-area.Height*Clamp(Histogram[i-1])),
                        new Point(x1,area.Bottom-area.Height*Clamp(Histogram[i])));
                }
            }
            if(_points.Count==0)return;
            if(_colorMode)
            {
                GradientStopCollection stops=new GradientStopCollection();
                foreach(FVOTransferPoint p in _points)
                    stops.Add(new GradientStop(Color.FromRgb(p.Color.R,p.Color.G,p.Color.B),Clamp(p.X)));
                if(stops.Count>=2)
                {
                    LinearGradientBrush gradient=new LinearGradientBrush(stops,0);
                    dc.DrawRectangle(gradient,null,new Rect(area.Left,area.Top,area.Width,28));
                }
            }
            Pen curve=new Pen(new SolidColorBrush(Color.FromRgb(29,71,124)),2.1);
            for(int i=1;i<_points.Count;i++)
                dc.DrawLine(curve,Project(_points[i-1],area),Project(_points[i],area));
            for(int i=0;i<_points.Count;i++)
            {
                FVOTransferPoint p=_points[i];
                Point at=Project(p,area);
                Brush dot=_colorMode ?
                    new SolidColorBrush(Color.FromRgb(p.Color.R,p.Color.G,p.Color.B)) :
                    Brushes.AliceBlue;
                dc.DrawEllipse(dot,new Pen(i==_selected ? Brushes.OrangeRed : Brushes.DarkSlateGray,
                    i==_selected ? 2.7 : 1.4),at,6.5,6.5);
            }
        }

        private Point Project(FVOTransferPoint point,Rect area)
        {
            return new Point(area.Left+Clamp(point.X)*area.Width,
                area.Top+(1-Clamp(point.Y))*area.Height);
        }
        private Rect PlotBounds()
        {
            return new Rect(22,30,Math.Max(20,ActualWidth-38),Math.Max(20,ActualHeight-49));
        }
        private int Hit(Point point)
        {
            int index=-1;double distance=100;
            Rect plot=PlotBounds();
            for(int i=0;i<_points.Count;i++)
            {
                Point cursor=Project(_points[i],plot);
                double d=(cursor-point).Length;
                if(d<distance && d<13) { index=i;distance=d; }
            }
            return index;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Point at=e.GetPosition(this);
            if(e.ClickCount==2)
            {
                Rect plot=PlotBounds();
                AddControlPoint((float)((at.X-plot.Left)/plot.Width),
                    (float)(1-(at.Y-plot.Top)/plot.Height));
                e.Handled=true;return;
            }
            int hit=Hit(at);
            if(hit>=0)
            {
                SelectPoint(hit);
                _dragging=true;CaptureMouse();e.Handled=true;
            }
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if(!_dragging || SelectedPoint==null)return;
            Rect plot=PlotBounds();
            Point at=e.GetPosition(this);
            int selected=_selected;
            float x=Clamp((float)((at.X-plot.Left)/plot.Width));
            if(selected==0)x=0;
            else if(selected==_points.Count-1)x=1;
            else
            {
                x=Math.Max(_points[selected-1].X+.0005f,
                    Math.Min(_points[selected+1].X-.0005f,x));
            }
            SelectedPoint.X=x;
            if(!_colorMode)SelectedPoint.Y=Clamp((float)(1-(at.Y-plot.Top)/plot.Height));
            NotifyChanged();e.Handled=true;
        }
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            _dragging=false;if(IsMouseCaptured)ReleaseMouseCapture();
        }
        protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonDown(e);
            int index=Hit(e.GetPosition(this));
            if(index>0 && index<_points.Count-1 && _points.Count>2)
            {
                _points.RemoveAt(index);_selected=-1;
                NotifyChanged();e.Handled=true;
            }
        }

        private void NotifyChanged()
        {
            InvalidateVisual();
            EventHandler changed=Changed;if(changed!=null)changed(this,EventArgs.Empty);
        }
        private static float Clamp(float x) { return Math.Max(0,Math.Min(1,x)); }
    }
}
