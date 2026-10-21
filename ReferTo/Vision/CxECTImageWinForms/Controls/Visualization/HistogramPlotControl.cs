using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using CxECTImageWinForms.Services;

namespace CxECTImageWinForms.Controls.Visualization
{
    /// <summary>
    /// 轻量直方图曲线控件。只负责呈现已计算的数据，不持有图像或 HALCON 句柄。
    /// </summary>
    internal sealed class HistogramPlotControl : Control
    {
        private ImageHistogram _histogram;

        public HistogramPlotControl()
        {
            DoubleBuffered = true;
            BackColor = Color.White;
            MinimumSize = new Size(180, 120);
        }

        public void SetHistogram(ImageHistogram histogram)
        {
            _histogram = histogram;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var bounds = ClientRectangle;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            using (var border = new Pen(Color.FromArgb(214, 222, 232)))
            using (var grid = new Pen(Color.FromArgb(235, 240, 246)))
            using (var axis = new Pen(Color.FromArgb(142, 156, 175)))
            using (var curve = new Pen(Color.FromArgb(30, 111, 210), 2.2F))
            using (var fill = new SolidBrush(Color.FromArgb(38, 30, 111, 210)))
            {
                e.Graphics.DrawRectangle(border, 0, 0, Math.Max(0, bounds.Width - 1), Math.Max(0, bounds.Height - 1));
                var plot = Rectangle.FromLTRB(60, 22, Math.Max(61, bounds.Right - 16), Math.Max(23, bounds.Bottom - 44));

                if (_histogram == null || _histogram.Bins == null || _histogram.Bins.Length == 0)
                {
                    TextRenderer.DrawText(e.Graphics, "直方图将在加载图像后显示", Font, plot, Color.DimGray, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    return;
                }

                var maximum = 1;
                foreach (var count in _histogram.Bins)
                {
                    maximum = Math.Max(maximum, count);
                }

                var yMaximum = GetNiceMaximum(maximum);
                var xRange = Math.Max(0.000001, _histogram.Maximum - _histogram.Minimum);
                var xTickCount = plot.Width >= 320 ? 4 : 2;
                const int yTickCount = 4;

                DrawAxesAndScales(e.Graphics, plot, grid, axis, xTickCount, yTickCount, xRange, yMaximum);

                var points = new PointF[_histogram.Bins.Length];
                for (var index = 0; index < _histogram.Bins.Length; index++)
                {
                    var x = plot.Left + (float)index * plot.Width / Math.Max(1, _histogram.Bins.Length - 1);
                    var y = plot.Bottom - (float)(_histogram.Bins[index] * plot.Height / yMaximum);
                    points[index] = new PointF(x, y);
                }

                if (points.Length == 1)
                {
                    e.Graphics.FillEllipse(new SolidBrush(curve.Color), points[0].X - 2, points[0].Y - 2, 4, 4);
                    return;
                }

                var filledArea = new PointF[points.Length + 2];
                filledArea[0] = new PointF(points[0].X, plot.Bottom);
                Array.Copy(points, 0, filledArea, 1, points.Length);
                filledArea[filledArea.Length - 1] = new PointF(points[points.Length - 1].X, plot.Bottom);
                e.Graphics.FillPolygon(fill, filledArea);

                curve.LineJoin = LineJoin.Round;
                curve.StartCap = LineCap.Round;
                curve.EndCap = LineCap.Round;
                e.Graphics.DrawLines(curve, points);
            }
        }

        private void DrawAxesAndScales(Graphics graphics, Rectangle plot, Pen grid, Pen axis, int xTickCount, int yTickCount, double xRange, double yMaximum)
        {
            graphics.DrawLine(axis, plot.Left, plot.Top, plot.Left, plot.Bottom);
            graphics.DrawLine(axis, plot.Left, plot.Bottom, plot.Right, plot.Bottom);

            for (var index = 0; index <= yTickCount; index++)
            {
                var fraction = (double)index / yTickCount;
                var y = plot.Bottom - (int)Math.Round(plot.Height * fraction);
                var value = yMaximum * fraction;
                if (index > 0)
                {
                    graphics.DrawLine(grid, plot.Left, y, plot.Right, y);
                }

                graphics.DrawLine(axis, plot.Left - 4, y, plot.Left, y);
                TextRenderer.DrawText(
                    graphics,
                    FormatCount(value),
                    Font,
                    new Rectangle(2, y - 9, plot.Left - 7, 18),
                    Color.FromArgb(82, 95, 112),
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }

            for (var index = 0; index <= xTickCount; index++)
            {
                var fraction = (double)index / xTickCount;
                var x = plot.Left + (int)Math.Round(plot.Width * fraction);
                if (index > 0 && index < xTickCount)
                {
                    graphics.DrawLine(grid, x, plot.Top, x, plot.Bottom);
                }

                graphics.DrawLine(axis, x, plot.Bottom, x, plot.Bottom + 4);
                var value = _histogram.Minimum + xRange * fraction;
                TextRenderer.DrawText(
                    graphics,
                    FormatGrayValue(value),
                    Font,
                    new Rectangle(x - 30, plot.Bottom + 6, 60, 16),
                    Color.FromArgb(82, 95, 112),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPadding);
            }

            TextRenderer.DrawText(graphics, "计数", Font, new Point(3, 3), Color.FromArgb(63, 76, 94), TextFormatFlags.NoPadding);
            TextRenderer.DrawText(
                graphics,
                "灰度值",
                Font,
                new Rectangle(plot.Left, plot.Bottom + 22, plot.Width, 16),
                Color.FromArgb(63, 76, 94),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPadding);
        }

        private static double GetNiceMaximum(int maximum)
        {
            if (maximum <= 1)
            {
                return 1;
            }

            var scale = Math.Pow(10, Math.Floor(Math.Log10(maximum)));
            var normalized = maximum / scale;
            var multiplier = normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 5 ? 5 : 10;
            return multiplier * scale;
        }

        private static string FormatCount(double value)
        {
            if (value >= 1000000)
            {
                return string.Format("{0:0.#}M", value / 1000000);
            }

            if (value >= 1000)
            {
                return string.Format("{0:0.#}k", value / 1000);
            }

            return string.Format("{0:0}", value);
        }

        private static string FormatGrayValue(double value)
        {
            return Math.Abs(value) >= 1000
                ? string.Format("{0:0}", value)
                : string.Format("{0:0.#}", value);
        }
    }
}
