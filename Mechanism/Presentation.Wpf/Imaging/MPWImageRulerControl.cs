using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace CxVisual.Mechanism.Presentation.Wpf.Imaging
{
    public enum MPWImageRulerOrientation { Horizontal, Vertical }

    /// <summary>
    /// 旧ImageRulerControl的WPF原生迁移，随实际视口和几何旋转更新。
    /// 显示可逆起止刻度，不对倒转轴强行排序；坐标单位由调用者显式给定。
    /// </summary>
    public sealed class MPWImageRulerControl : FrameworkElement
    {
        private double _start, _end = 1;
        private string _unit = "px";
        public MPWImageRulerOrientation Orientation { get; set; }
        public double StartValue { get { return _start; } }
        public double EndValue { get { return _end; } }
        public string Unit { get { return _unit; } }
        public void SetRange(double start, double end, string unit)
        {
            if (double.IsNaN(start) || double.IsInfinity(start) ||
                double.IsNaN(end) || double.IsInfinity(end))
                throw new ArgumentOutOfRangeException("start");
            if (string.IsNullOrWhiteSpace(unit)) throw new ArgumentException("标尺单位不能为空。", "unit");
            _start = start; _end = end; _unit = unit;
            InvalidateVisual();
        }
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double length = Orientation == MPWImageRulerOrientation.Horizontal ? ActualWidth : ActualHeight;
            if (length < 24) return;
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(245,247,250)),null,
                new Rect(0,0,ActualWidth,ActualHeight));
            Pen axis = new Pen(new SolidColorBrush(Color.FromRgb(155,169,186)),1);
            Pen major = new Pen(new SolidColorBrush(Color.FromRgb(95,109,129)),1);
            Pen minor = new Pen(new SolidColorBrush(Color.FromRgb(188,197,210)),1);
            if (Orientation == MPWImageRulerOrientation.Horizontal)
                dc.DrawLine(axis,new Point(0,ActualHeight-.5),new Point(length,ActualHeight-.5));
            else dc.DrawLine(axis,new Point(ActualWidth-.5,0),new Point(ActualWidth-.5,length));

            double delta = _end-_start, span = Math.Abs(delta);
            if (span < 1e-9 || double.IsInfinity(span)) return;
            double majorStep = NiceStep(span/Math.Max(2,length/85.0));
            double minorStep = majorStep/5;
            double lo = Math.Min(_start,_end), hi = Math.Max(_start,_end);
            double first = Math.Ceiling(lo/minorStep)*minorStep;
            int limit = 0;
            for (double value = first; value <= hi + minorStep * 0.0001 && limit < 2000;
                 value += minorStep, limit++)
            {
                double pos = (value-_start)/delta*length;
                bool isMajor = Math.Abs(value/majorStep-Math.Round(value/majorStep)) < .001;
                if (Orientation == MPWImageRulerOrientation.Horizontal)
                    dc.DrawLine(isMajor?major:minor,new Point(pos,ActualHeight-1),
                        new Point(pos,ActualHeight-(isMajor?10:5)));
                else dc.DrawLine(isMajor?major:minor,new Point(ActualWidth-1,pos),
                        new Point(ActualWidth-(isMajor?10:5),pos));
                if (isMajor)
                {
                    string label = value.ToString("0.###",CultureInfo.InvariantCulture);
                    FormattedText text = new FormattedText(label,CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,new Typeface("Segoe UI"),9.5,
                        Brushes.SlateGray,VisualTreeHelper.GetDpi(this).PixelsPerDip);
                    if (Orientation == MPWImageRulerOrientation.Horizontal)
                        dc.DrawText(text,new Point(Math.Max(0,Math.Min(length-text.Width,pos+2)),0));
                    else dc.DrawText(text,new Point(0,Math.Max(0,Math.Min(length-text.Height,pos+1))));
                }
            }
        }
        private static double NiceStep(double target)
        {
            if (target <= 0 || double.IsNaN(target) || double.IsInfinity(target)) return 1;
            double magnitude = Math.Pow(10,Math.Floor(Math.Log10(target)));
            double scaled = target/magnitude;
            double unit = scaled<=1?1:scaled<=2?2:scaled<=5?5:10;
            return unit*magnitude;
        }
    }
}
