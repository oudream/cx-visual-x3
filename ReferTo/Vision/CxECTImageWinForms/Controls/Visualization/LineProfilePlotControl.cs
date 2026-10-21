using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace CxECTImageWinForms.Controls.Visualization
{
    /// <summary>
    /// 灰度剖面曲线。测量完成后由 ImageViewerControl 注入数值序列。
    /// </summary>
    internal sealed class LineProfilePlotControl : Control
    {
        private IReadOnlyList<double> _values;

        public LineProfilePlotControl()
        {
            DoubleBuffered = true;
            BackColor = Color.White;
            MinimumSize = new Size(180, 90);
        }

        public void SetValues(IReadOnlyList<double> values)
        {
            _values = values;
            Invalidate();
        }

        public void ClearValues()
        {
            _values = null;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var plot = Rectangle.FromLTRB(28, 10, Math.Max(29, Width - 10), Math.Max(11, Height - 16));
            using (var border = new Pen(Color.FromArgb(214, 222, 232)))
            using (var curve = new Pen(Color.FromArgb(231, 91, 55), 1.5F))
            {
                e.Graphics.DrawRectangle(border, plot);
                if (_values == null || _values.Count < 2)
                {
                    TextRenderer.DrawText(e.Graphics, "灰度剖面", Font, plot, Color.DimGray, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    return;
                }

                var minimum = double.MaxValue;
                var maximum = double.MinValue;
                foreach (var value in _values)
                {
                    minimum = Math.Min(minimum, value);
                    maximum = Math.Max(maximum, value);
                }

                var range = Math.Max(0.000001, maximum - minimum);
                var previous = new PointF(plot.Left, plot.Bottom - (float)((_values[0] - minimum) * plot.Height / range));
                for (var index = 1; index < _values.Count; index++)
                {
                    var point = new PointF(plot.Left + (float)index * plot.Width / (_values.Count - 1), plot.Bottom - (float)((_values[index] - minimum) * plot.Height / range));
                    e.Graphics.DrawLine(curve, previous, point);
                    previous = point;
                }
            }
        }
    }
}
