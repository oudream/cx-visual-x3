using System;
using System.Drawing;
using System.Windows.Forms;

namespace CxECTImageWinForms.Controls.Visualization
{
    /// <summary>
    /// 图像外侧标尺。只呈现当前 HALCON 视口范围，不参与图像重绘、导出或叠加对象数据。
    /// </summary>
    public sealed class ImageRulerControl : Control
    {
        private double _minimum;
        private double _maximum = 1;
        private bool _isPhysicalUnit;
        private bool _hasCalibration;
        private double _millimetersPerPixel;

        public ImageRulerControl()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(245, 247, 250);
            ForeColor = Color.FromArgb(86, 96, 112);
            Font = new Font("Microsoft YaHei UI", 7F);
        }

        public ImageRulerOrientation Orientation { get; set; }

        /// <summary>
        /// 以当前视口像素范围刷新。物理标尺仅在完成像素标定时显示 mm 数值。
        /// </summary>
        public void SetViewport(double minimum, double maximum, bool physicalUnit, double? millimetersPerPixel)
        {
            _minimum = Math.Min(minimum, maximum);
            _maximum = Math.Max(minimum, maximum);
            if (Math.Abs(_maximum - _minimum) < 0.000001)
            {
                _maximum = _minimum + 1;
            }

            _isPhysicalUnit = physicalUnit;
            _hasCalibration = millimetersPerPixel.HasValue && millimetersPerPixel.Value > 0;
            _millimetersPerPixel = _hasCalibration ? millimetersPerPixel.Value : 1;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(BackColor);
            var length = Orientation == ImageRulerOrientation.Horizontal ? Width : Height;
            if (length < 24)
            {
                return;
            }

            using (var axis = new Pen(Color.FromArgb(181, 190, 202)))
            using (var major = new Pen(Color.FromArgb(125, 139, 157)))
            using (var minor = new Pen(Color.FromArgb(190, 198, 208)))
            using (var textBrush = new SolidBrush(ForeColor))
            {
                DrawAxis(e.Graphics, axis, length);
                var pixelSpan = _maximum - _minimum;
                var unitScale = _isPhysicalUnit && _hasCalibration ? _millimetersPerPixel : 1;
                var displayedMinimum = _minimum * unitScale;
                var displayedMaximum = _maximum * unitScale;
                var majorStep = GetNiceStep((displayedMaximum - displayedMinimum) / Math.Max(2, length / 80));
                var minorStep = majorStep / 5;
                var firstMinor = Math.Ceiling(displayedMinimum / minorStep) * minorStep;
                for (var value = firstMinor; value <= displayedMaximum + minorStep * 0.001; value += minorStep)
                {
                    var ratio = ((value / unitScale) - _minimum) / pixelSpan;
                    var position = (float)(ratio * (length - 1));
                    if (Math.Abs(value / majorStep - Math.Round(value / majorStep)) > 0.001)
                    {
                        DrawMinorTick(e.Graphics, minor, position);
                    }
                }

                var firstMajor = Math.Ceiling(displayedMinimum / majorStep) * majorStep;
                for (var value = firstMajor; value <= displayedMaximum + majorStep * 0.001; value += majorStep)
                {
                    var ratio = ((value / unitScale) - _minimum) / pixelSpan;
                    var position = (float)(ratio * (length - 1));
                    DrawMajorTick(e.Graphics, major, position);
                    DrawLabel(e.Graphics, textBrush, FormatValue(value), position);
                }

                DrawUnit(e.Graphics, textBrush);
            }
        }

        private void DrawAxis(Graphics graphics, Pen axis, int length)
        {
            if (Orientation == ImageRulerOrientation.Horizontal)
            {
                graphics.DrawLine(axis, 0, Height - 1, length, Height - 1);
            }
            else
            {
                graphics.DrawLine(axis, Width - 1, 0, Width - 1, length);
            }
        }

        private void DrawMajorTick(Graphics graphics, Pen pen, float position)
        {
            if (Orientation == ImageRulerOrientation.Horizontal)
            {
                graphics.DrawLine(pen, position, Height - 1, position, Math.Max(0, Height - 7));
            }
            else
            {
                graphics.DrawLine(pen, Width - 1, position, Math.Max(0, Width - 7), position);
            }
        }

        private void DrawMinorTick(Graphics graphics, Pen pen, float position)
        {
            if (Orientation == ImageRulerOrientation.Horizontal)
            {
                graphics.DrawLine(pen, position, Height - 1, position, Math.Max(0, Height - 4));
            }
            else
            {
                graphics.DrawLine(pen, Width - 1, position, Math.Max(0, Width - 4), position);
            }
        }

        private void DrawLabel(Graphics graphics, Brush brush, string text, float position)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var size = graphics.MeasureString(text, Font);
            if (Orientation == ImageRulerOrientation.Horizontal)
            {
                graphics.DrawString(text, Font, brush, Math.Max(0, position - size.Width / 2), 0);
            }
            else
            {
                graphics.DrawString(text, Font, brush, 1, Math.Max(0, position - size.Height / 2));
            }
        }

        private void DrawUnit(Graphics graphics, Brush brush)
        {
            var text = _isPhysicalUnit ? (_hasCalibration ? "mm" : "mm ?") : "px";
            if (Orientation == ImageRulerOrientation.Horizontal)
            {
                var size = graphics.MeasureString(text, Font);
                graphics.DrawString(text, Font, brush, Math.Max(0, Width - size.Width - 3), 0);
            }
            else
            {
                graphics.DrawString(text, Font, brush, 1, Math.Max(0, Height - Font.Height));
            }
        }

        private string FormatValue(double value)
        {
            // 未标定时不能把像素坐标伪装成毫米数值，只保留 mm ? 提示。
            if (_isPhysicalUnit && !_hasCalibration)
            {
                return string.Empty;
            }

            return Math.Abs(value) >= 100 ? value.ToString("0") : value.ToString("0.##");
        }

        private static double GetNiceStep(double roughStep)
        {
            if (roughStep <= 0 || double.IsInfinity(roughStep) || double.IsNaN(roughStep))
            {
                return 1;
            }

            var exponent = Math.Floor(Math.Log10(roughStep));
            var fraction = roughStep / Math.Pow(10, exponent);
            var niceFraction = fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10;
            return niceFraction * Math.Pow(10, exponent);
        }
    }

    public enum ImageRulerOrientation
    {
        Horizontal,
        Vertical
    }


}
