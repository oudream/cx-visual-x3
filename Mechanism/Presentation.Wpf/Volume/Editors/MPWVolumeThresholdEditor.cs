using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace CxVisual.Mechanism.Presentation.Wpf.Volume
{
    /// <summary>旧阈值图的两条可拖动边界；唯一数值最终存入FVOTransferFunctionSet。</summary>
    public sealed class MPWVolumeThresholdEditor : FrameworkElement
    {
        private float _lower=.28f, _upper=1f;
        private int _active;
        public event EventHandler RangeChanged;
        public float[] Histogram { get; set; }
        public float Lower
        {
            get { return _lower; }
            set { _lower=Clamp(value,0,_upper); InvalidateVisual(); }
        }
        public float Upper
        {
            get { return _upper; }
            set { _upper=Clamp(value,_lower,1); InvalidateVisual(); }
        }
        public MPWVolumeThresholdEditor()
        {
            MinHeight=170; Height=250; Focusable=true;
        }
        protected override Size MeasureOverride(Size offered)
        {
            return new Size(double.IsInfinity(offered.Width)?260:Math.Max(100,offered.Width),250);
        }
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            Rect rect=new Rect(30,20,Math.Max(35,ActualWidth-50),Math.Max(30,ActualHeight-46));
            dc.DrawRectangle(Brushes.White,new Pen(Brushes.LightGray,1),
                new Rect(0,0,Math.Max(1,ActualWidth),Math.Max(1,ActualHeight)));
            Pen grid=new Pen(new SolidColorBrush(Color.FromRgb(230,234,239)),1);
            for(int i=0;i<=4;i++)
            {
                double x=rect.Left+rect.Width*i/4.0;
                double y=rect.Top+rect.Height*i/4.0;
                dc.DrawLine(grid,new Point(x,rect.Top),new Point(x,rect.Bottom));
                dc.DrawLine(grid,new Point(rect.Left,y),new Point(rect.Right,y));
            }
            if(Histogram!=null&&Histogram.Length>1)
            {
                Pen graph=new Pen(Brushes.LightSteelBlue,1);
                for(int i=1;i<Histogram.Length;i++)
                {
                    double x0=rect.Left+rect.Width*(i-1)/(Histogram.Length-1.0);
                    double x1=rect.Left+rect.Width*i/(Histogram.Length-1.0);
                    double v0=Math.Max(0,Math.Min(1,Histogram[i-1]));
                    double v1=Math.Max(0,Math.Min(1,Histogram[i]));
                    dc.DrawLine(graph,new Point(x0,rect.Bottom-rect.Height*v0),
                        new Point(x1,rect.Bottom-rect.Height*v1));
                }
            }
            double left=rect.Left+_lower*rect.Width,right=rect.Left+_upper*rect.Width;
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(45,24,128,211)),null,
                new Rect(left,rect.Top,Math.Max(0,right-left),rect.Height));
            Pen line=new Pen(Brushes.Red,1.7);
            dc.DrawLine(line,new Point(left,rect.Top),new Point(left,rect.Bottom));
            dc.DrawLine(line,new Point(right,rect.Top),new Point(right,rect.Bottom));
            dc.DrawEllipse(Brushes.White,new Pen(Brushes.Red,1.5),new Point(left,rect.Top+5),5,5);
            dc.DrawEllipse(Brushes.White,new Pen(Brushes.Red,1.5),new Point(right,rect.Top+5),5,5);
        }
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Rect area=new Rect(30,20,Math.Max(35,ActualWidth-50),Math.Max(30,ActualHeight-46));
            double x=e.GetPosition(this).X;
            _active=Math.Abs(x-(area.Left+_lower*area.Width))<=
                Math.Abs(x-(area.Left+_upper*area.Width))?1:2;
            CaptureMouse();UpdatePosition(x,area);
            e.Handled=true;
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if(_active==0)return;
            Rect rect=new Rect(30,20,Math.Max(35,ActualWidth-50),Math.Max(30,ActualHeight-46));
            UpdatePosition(e.GetPosition(this).X,rect);
            e.Handled=true;
        }
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            _active=0;
            if(IsMouseCaptured)ReleaseMouseCapture();
        }
        public void SetRange(float low,float high)
        {
            if(low<0||high>1||low>high)throw new ArgumentOutOfRangeException("low");
            _lower=low;_upper=high;
            InvalidateVisual();Notify();
        }
        private void UpdatePosition(double x,Rect area)
        {
            float v=(float)Math.Max(0,Math.Min(1,(x-area.Left)/area.Width));
            if(_active==1)_lower=Math.Min(v,_upper);
            if(_active==2)_upper=Math.Max(v,_lower);
            InvalidateVisual();Notify();
        }
        private void Notify()
        {
            EventHandler handler=RangeChanged;
            if(handler!=null)handler(this,EventArgs.Empty);
        }
        private static float Clamp(float v,float low,float high)
        {return Math.Max(low,Math.Min(high,v));}
    }
}
