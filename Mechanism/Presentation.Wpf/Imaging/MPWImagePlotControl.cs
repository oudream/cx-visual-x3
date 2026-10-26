using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using CxVisual.Foundation.Imaging;

namespace CxVisual.Mechanism.Presentation.Wpf.Imaging
{
    public enum MPWImagePlotKind { Histogram, LineProfile }

    /// <summary>
    /// 将旧HistogramPlotControl/LineProfilePlotControl原生移入WPF，
    /// 纯呈现FIM算出的数据，不持有图像或HALCON对象。两个实例可分别显示统计与剖面。
    /// </summary>
    public sealed class MPWImagePlotControl : FrameworkElement
    {
        private double[] _values = new double[0];
        private double _minimum, _maximum;
        private MPWImagePlotKind _kind = MPWImagePlotKind.Histogram;
        public MPWImagePlotControl()
        {
            SnapsToDevicePixels = true;
        }
        public MPWImagePlotKind Kind { get { return _kind; } }
        public int PointCount { get { return _values.Length; } }
        public void SetHistogram(FIMImageHistogram histogram)
        {
            if (histogram == null) throw new ArgumentNullException("histogram");
            _kind = MPWImagePlotKind.Histogram;
            _values = histogram.Bins.Select(n => (double)n).ToArray();
            _minimum = histogram.Minimum;
            _maximum = histogram.Maximum;
            InvalidateVisual();
        }
        public void SetLineProfile(IReadOnlyList<double> values)
        {
            if (values == null) throw new ArgumentNullException("values");
            double[] snapshot = values.ToArray();
            if (snapshot.Any(v => double.IsNaN(v) || double.IsInfinity(v)))
                throw new ArgumentException("灰度剖面存在非有限值。", "values");
            _kind = MPWImagePlotKind.LineProfile;
            _values = snapshot;
            _minimum = snapshot.Length == 0 ? 0 : snapshot.Min();
            _maximum = snapshot.Length == 0 ? 0 : snapshot.Max();
            InvalidateVisual();
        }
        public void Clear()
        {
            _values = new double[0];
            _minimum = _maximum = 0;
            InvalidateVisual();
        }
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            double width = Math.Max(1, ActualWidth);
            double height = Math.Max(1, ActualHeight);
            drawingContext.DrawRectangle(Brushes.White,
                new Pen(new SolidColorBrush(Color.FromRgb(202,214,229)), 1),
                new Rect(0.5,0.5,width - 1,height - 1));
            if (width < 100 || height < 70) return;

            const double left = 40, top = 16, rightPad = 12, bottomPad = 29;
            Rect plot = new Rect(left,top,Math.Max(1,width - left - rightPad),
                Math.Max(1,height - top - bottomPad));
            Pen grid = new Pen(new SolidColorBrush(Color.FromRgb(232,238,247)),1);
            for (int i = 0; i <= 4; i++)
            {
                double y = plot.Top + plot.Height * i / 4;
                drawingContext.DrawLine(grid,new Point(plot.Left,y),new Point(plot.Right,y));
            }
            Pen axis = new Pen(new SolidColorBrush(Color.FromRgb(124,142,164)),1);
            drawingContext.DrawLine(axis,new Point(plot.Left,plot.Top),
                new Point(plot.Left,plot.Bottom));
            drawingContext.DrawLine(axis,new Point(plot.Left,plot.Bottom),
                new Point(plot.Right,plot.Bottom));
            if (_values.Length == 0)
            {
                DrawText(drawingContext, _kind == MPWImagePlotKind.Histogram ?
                    "尚无直方图" : "尚无灰度剖面",plot.Left + 10,plot.Top + 15);
                return;
            }
            double yMin = _kind == MPWImagePlotKind.Histogram ? 0 : _values.Min();
            double yMax = Math.Max(yMin, _values.Max());
            double span = yMax - yMin;
            if (span <= 0) span = 1;
            Pen curve = new Pen(new SolidColorBrush(_kind == MPWImagePlotKind.Histogram ?
                Color.FromRgb(40,113,218) : Color.FromRgb(225,88,46)),1.6);
            Point? previous = null;
            for (int i = 0; i < _values.Length; i++)
            {
                double x = plot.Left + (_values.Length == 1 ? plot.Width/2 :
                    i * plot.Width / (_values.Length - 1));
                double y = plot.Bottom - (_values[i] - yMin) / span * plot.Height;
                Point current = new Point(x,y);
                if (previous.HasValue) drawingContext.DrawLine(curve,previous.Value,current);
                else drawingContext.DrawEllipse(curve.Brush,null,current,2.0,2.0);
                previous = current;
            }
            DrawText(drawingContext,yMax.ToString("0.##",CultureInfo.InvariantCulture),
                2,plot.Top-4);
            DrawText(drawingContext,yMin.ToString("0.##",CultureInfo.InvariantCulture),
                2,plot.Bottom-12);
            DrawText(drawingContext,_minimum.ToString("0.##",CultureInfo.InvariantCulture),
                plot.Left,plot.Bottom+7);
            DrawText(drawingContext,_maximum.ToString("0.##",CultureInfo.InvariantCulture),
                plot.Right-48,plot.Bottom+7);
            DrawText(drawingContext,_kind == MPWImagePlotKind.Histogram ?
                "Histogram" : "Line profile",plot.Left+plot.Width/3,2);
        }
        private void DrawText(DrawingContext context,string value,double x,double y)
        {
            FormattedText text = new FormattedText(value,CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,new Typeface("Segoe UI"),10.5,Brushes.SlateGray,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            context.DrawText(text,new Point(x,y));
        }
    }
}
