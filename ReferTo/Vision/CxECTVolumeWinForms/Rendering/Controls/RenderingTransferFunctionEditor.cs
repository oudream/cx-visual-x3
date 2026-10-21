using CxECTVolumeCore.Rendering;
using CxECTVolumeCore.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace CxECTVolumeWinForms.Rendering
{
    // 编辑器只修改核心传递函数模型并发出变更事件。
    /// <summary>传递函数编辑器当前编辑的曲线类型。</summary>
    public enum RenderingTransferEditorKind
    {
        Threshold,
        Color,
        ScalarOpacity,
        GradientOpacity,
    }

    /// <summary>显示直方图并提供传递函数控制点拖动、双击新增和编辑的图表控件。</summary>
    public sealed class RenderingTransferFunctionEditor : Control
    {
        private enum RenderingThresholdHandle
        {
            None,
            Lower,
            Upper,
        }

        private const int HandleRadius = 6;
        private readonly RenderingTransferFunctionSet _transferFunctions;
        // _activePoint 只在鼠标拖动期间有效；拖动后直接修改 RenderingTransferFunctionSet 里的控制点。
        private int _activePoint = -1;
        private RenderingThresholdHandle _activeThresholdHandle;

        public RenderingTransferFunctionEditor(RenderingTransferEditorKind kind, RenderingTransferFunctionSet transferFunctions)
        {
            Kind = kind;
            _transferFunctions = transferFunctions;
            DoubleBuffered = true;
            BackColor = Color.White;
            ForeColor = Color.FromArgb(28, 32, 38);
            MinimumSize = new Size(320, 220);
            Histogram = new float[256];
        }

        public event EventHandler TransferChanged;

        public RenderingTransferEditorKind Kind { get; private set; }

        public float[] Histogram { get; private set; }

        public void SetHistogram(float[] histogram)
        {
            Histogram = histogram;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var plot = GetPlotRect();
            DrawFrame(e.Graphics, plot);
            // 灰色直方图是参考背景，帮助判断哪些灰度/梯度区间应该被显示或压暗。
            DrawHistogram(e.Graphics, plot);

            if (Kind == RenderingTransferEditorKind.Color)
            {
                DrawColorRamp(e.Graphics, plot);
                DrawColorPoints(e.Graphics, plot);
            }
            else if (Kind == RenderingTransferEditorKind.Threshold)
            {
                DrawThreshold(e.Graphics, plot);
            }
            else
            {
                DrawOpacityCurve(e.Graphics, plot);
            }

            DrawAxisLabels(e.Graphics, plot);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            var plot = GetPlotRect();
            if (!plot.Contains(e.Location))
            {
                return;
            }

            if (Kind == RenderingTransferEditorKind.Threshold)
            {
                if (e.Button != MouseButtons.Left)
                {
                    return;
                }

                _activeThresholdHandle = FindNearestThresholdHandle(e.X, plot);
                SetThresholdFromMouse(e.X, plot, _activeThresholdHandle);
                return;
            }

            var points = GetPoints();
            _activePoint = FindPoint(e.Location, plot, points);
            if (_activePoint < 0 && e.Button == MouseButtons.Left)
            {
                // 点击空白区域新增控制点，便于快速做出多段透明度或颜色映射。
                var point = PointFromMouse(e.Location, plot);
                points.Add(new RenderingTransferPoint(point.X, Kind == RenderingTransferEditorKind.Color ? 0.5f : point.Y, Color.White));
                points.Sort(delegate(RenderingTransferPoint left, RenderingTransferPoint right) { return left.X.CompareTo(right.X); });
                _activePoint = FindPoint(e.Location, plot, points);
                RaiseChanged();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            var plot = GetPlotRect();
            if (_activeThresholdHandle != RenderingThresholdHandle.None)
            {
                SetThresholdFromMouse(e.X, plot, _activeThresholdHandle);
                return;
            }

            if (_activePoint < 0)
            {
                return;
            }

            var points = GetPoints();
            var value = PointFromMouse(e.Location, plot);
            var point = points[_activePoint];
            point.X = value.X;
            point.Y = Kind == RenderingTransferEditorKind.Color ? 0.5f : value.Y;
            points.Sort(delegate(RenderingTransferPoint left, RenderingTransferPoint right) { return left.X.CompareTo(right.X); });
            _activePoint = points.IndexOf(point);
            RaiseChanged();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _activePoint = -1;
            _activeThresholdHandle = RenderingThresholdHandle.None;
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            if (Kind != RenderingTransferEditorKind.Color)
            {
                return;
            }

            var points = GetPoints();
            var pointIndex = FindPoint(e.Location, GetPlotRect(), points);
            if (pointIndex < 0)
            {
                return;
            }

            using (var dialog = new ColorDialog
            {
                Color = points[pointIndex].Color,
                FullOpen = true,
            })
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    // 颜色图表的 Y 值固定在中线，双击点只负责改颜色。
                    points[pointIndex].Color = dialog.Color;
                    RaiseChanged();
                }
            }
        }

        private Rectangle GetPlotRect()
        {
            return new Rectangle(42, 18, Math.Max(10, Width - 56), Math.Max(10, Height - 48));
        }

        private List<RenderingTransferPoint> GetPoints()
        {
            switch (Kind)
            {
                case RenderingTransferEditorKind.Color:
                    return _transferFunctions.Color.Points;
                case RenderingTransferEditorKind.ScalarOpacity:
                    return _transferFunctions.ScalarOpacity.Points;
                case RenderingTransferEditorKind.GradientOpacity:
                    return _transferFunctions.GradientOpacity.Points;
                default:
                    throw new InvalidOperationException("Threshold does not expose editable points.");
            }
        }

        private void DrawFrame(Graphics graphics, Rectangle plot)
        {
            using (var gridPen = new Pen(Color.FromArgb(235, 238, 242)))
            {
                for (var i = 1; i < 5; i++)
                {
                    var x = plot.Left + plot.Width * i / 5;
                    var y = plot.Top + plot.Height * i / 5;
                    graphics.DrawLine(gridPen, x, plot.Top, x, plot.Bottom);
                    graphics.DrawLine(gridPen, plot.Left, y, plot.Right, y);
                }
            }

            using (var axisPen = new Pen(Color.FromArgb(120, 128, 138)))
            {
                graphics.DrawRectangle(axisPen, plot);
            }
        }

        private void DrawHistogram(Graphics graphics, Rectangle plot)
        {
            if (Histogram.Length == 0)
            {
                return;
            }

            using (var path = new GraphicsPath())
            {
                path.AddLine(plot.Left, plot.Bottom, plot.Left, plot.Bottom);

                for (var i = 0; i < Histogram.Length; i++)
                {
                    var x = plot.Left + i * plot.Width / (float)(Histogram.Length - 1);
                    var y = plot.Bottom - FrameworkCompat.Clamp(Histogram[i], 0.0f, 1.0f) * plot.Height;
                    path.AddLine(x, y, x, y);
                }

                path.AddLine(plot.Right, plot.Bottom, plot.Left, plot.Bottom);
                using (var brush = new SolidBrush(Color.FromArgb(78, 150, 156, 166)))
                {
                    graphics.FillPath(brush, path);
                }
            }
        }

        private void DrawThreshold(Graphics graphics, Rectangle plot)
        {
            var lowerX = ThresholdToScreenX(_transferFunctions.Threshold, plot);
            var upperX = ThresholdToScreenX(_transferFunctions.UpperThreshold, plot);
            using (var pen = new Pen(Color.Red, 2.0f))
            {
                graphics.DrawLine(pen, lowerX, plot.Top, lowerX, plot.Bottom);
                graphics.DrawLine(pen, upperX, plot.Top, upperX, plot.Bottom);
            }
        }

        private void DrawColorRamp(Graphics graphics, Rectangle plot)
        {
            var ramp = new Rectangle(plot.Left, plot.Top, plot.Width, Math.Min(34, plot.Height));
            for (var i = 0; i < ramp.Width; i++)
            {
                var x = i / (float)Math.Max(1, ramp.Width - 1);
                using (var pen = new Pen(_transferFunctions.Color.Evaluate(x)))
                {
                    graphics.DrawLine(pen, ramp.Left + i, ramp.Top, ramp.Left + i, ramp.Bottom);
                }
            }
        }

        private void DrawColorPoints(Graphics graphics, Rectangle plot)
        {
            foreach (var point in _transferFunctions.Color.Points)
            {
                var center = ToScreen(point, plot);
                center.Y = plot.Top + Math.Min(34, plot.Height) / 2;
                using (var brush = new SolidBrush(point.Color))
                using (var pen = new Pen(Color.FromArgb(80, 80, 80)))
                {
                    graphics.FillEllipse(brush, center.X - HandleRadius, center.Y - HandleRadius, HandleRadius * 2, HandleRadius * 2);
                    graphics.DrawEllipse(pen, center.X - HandleRadius, center.Y - HandleRadius, HandleRadius * 2, HandleRadius * 2);
                }
            }
        }

        private void DrawOpacityCurve(Graphics graphics, Rectangle plot)
        {
            var points = GetPoints();
            if (points.Count < 2)
            {
                return;
            }

            using (var pen = new Pen(Color.FromArgb(38, 45, 55), 2.0f))
            {
                var screenPoints = points.Select(point => ToScreen(point, plot)).ToArray();
                graphics.DrawLines(pen, screenPoints);

                foreach (var point in screenPoints)
                {
                    using (var brush = new SolidBrush(Color.FromArgb(232, 238, 245)))
                    using (var outline = new Pen(Color.FromArgb(70, 78, 88)))
                    {
                        graphics.FillEllipse(brush, point.X - HandleRadius, point.Y - HandleRadius, HandleRadius * 2, HandleRadius * 2);
                        graphics.DrawEllipse(outline, point.X - HandleRadius, point.Y - HandleRadius, HandleRadius * 2, HandleRadius * 2);
                    }
                }
            }
        }

        private void DrawAxisLabels(Graphics graphics, Rectangle plot)
        {
            var xLabel = Kind == RenderingTransferEditorKind.GradientOpacity ? "Gradient" : "Scalar";
            if (Kind == RenderingTransferEditorKind.Threshold)
            {
                xLabel = "Scalar Threshold Range";
            }

            TextRenderer.DrawText(graphics, xLabel, Font, new Point(plot.Left + plot.Width / 2 - 40, plot.Bottom + 8), ForeColor);
            TextRenderer.DrawText(graphics, "1.0", Font, new Point(6, plot.Top - 2), ForeColor);
            TextRenderer.DrawText(graphics, "0.0", Font, new Point(6, plot.Bottom - 14), ForeColor);
        }

        private int FindPoint(Point mouse, Rectangle plot, List<RenderingTransferPoint> points)
        {
            for (var i = 0; i < points.Count; i++)
            {
                var center = ToScreen(points[i], plot);
                if (Kind == RenderingTransferEditorKind.Color)
                {
                    center.Y = plot.Top + Math.Min(34, plot.Height) / 2;
                }

                var dx = mouse.X - center.X;
                var dy = mouse.Y - center.Y;
                if (dx * dx + dy * dy <= HandleRadius * HandleRadius * 4)
                {
                    return i;
                }
            }

            return -1;
        }

        private static PointF ToScreen(RenderingTransferPoint point, Rectangle plot)
        {
            return new PointF(
                plot.Left + point.X * plot.Width,
                plot.Bottom - point.Y * plot.Height);
        }

        private static PointF PointFromMouse(Point mouse, Rectangle plot)
        {
            return new PointF(
                FrameworkCompat.Clamp((mouse.X - plot.Left) / (float)plot.Width, 0.0f, 1.0f),
                FrameworkCompat.Clamp((plot.Bottom - mouse.Y) / (float)plot.Height, 0.0f, 1.0f));
        }

        private RenderingThresholdHandle FindNearestThresholdHandle(int mouseX, Rectangle plot)
        {
            var lowerX = ThresholdToScreenX(_transferFunctions.Threshold, plot);
            var upperX = ThresholdToScreenX(_transferFunctions.UpperThreshold, plot);
            return Math.Abs(mouseX - lowerX) <= Math.Abs(mouseX - upperX)
                ? RenderingThresholdHandle.Lower
                : RenderingThresholdHandle.Upper;
        }

        private static int ThresholdToScreenX(float threshold, Rectangle plot)
        {
            var normalized = FrameworkCompat.Clamp(threshold, 0.0f, 1.0f);
            return plot.Left + (int)Math.Round(normalized * Math.Max(0, plot.Width - 1));
        }

        private void SetThresholdFromMouse(int mouseX, Rectangle plot, RenderingThresholdHandle handle)
        {
            var value = FrameworkCompat.Clamp((mouseX - plot.Left) / (float)Math.Max(1, plot.Width - 1), 0.0f, 1.0f);
            // 至少保留一个像素间距，避免两条线重合后无法判断正在拖动哪一条。
            var minimumGap = 1.0f / Math.Max(1, plot.Width - 1);
            if (handle == RenderingThresholdHandle.Lower)
            {
                _transferFunctions.Threshold = Math.Min(value, Math.Max(0.0f, _transferFunctions.UpperThreshold - minimumGap));
            }
            else if (handle == RenderingThresholdHandle.Upper)
            {
                _transferFunctions.UpperThreshold = Math.Max(value, Math.Min(1.0f, _transferFunctions.Threshold + minimumGap));
            }
            else
            {
                return;
            }

            RaiseChanged();
        }

        private void RaiseChanged()
        {
            Invalidate();
            var handler = TransferChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }
    }
}
