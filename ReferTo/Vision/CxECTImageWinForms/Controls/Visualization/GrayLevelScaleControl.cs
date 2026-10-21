using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CxECTImageWinForms.Controls.Visualization
{
    /// <summary>
    /// 灰阶梯级图，作为当前分析范围的视觉刻度，不参与任何图像处理。
    /// </summary>
    internal sealed class GrayLevelScaleControl : Control
    {
        public GrayLevelScaleControl()
        {
            DoubleBuffered = true;
            BackColor = Color.White;
            Height = 38;
        }

        public double Minimum { get; set; }

        public double Maximum { get; set; } = 255;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var bar = Rectangle.FromLTRB(8, 8, Math.Max(9, Width - 8), Math.Max(9, Height - 18));
            using (var gradient = new LinearGradientBrush(bar, Color.Black, Color.White, LinearGradientMode.Horizontal))
            using (var border = new Pen(Color.FromArgb(175, 185, 198)))
            {
                e.Graphics.FillRectangle(gradient, bar);
                e.Graphics.DrawRectangle(border, bar);
            }

            TextRenderer.DrawText(e.Graphics, string.Format("{0:0}", Minimum), Font, new Point(8, Height - 17), Color.DimGray);
            var maximumText = string.Format("{0:0}", Maximum);
            var maximumSize = TextRenderer.MeasureText(maximumText, Font);
            TextRenderer.DrawText(e.Graphics, maximumText, Font, new Point(Math.Max(8, Width - maximumSize.Width - 8), Height - 17), Color.DimGray);
        }
    }
}
