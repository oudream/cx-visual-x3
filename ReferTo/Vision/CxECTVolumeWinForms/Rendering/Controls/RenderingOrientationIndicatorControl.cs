using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using OpenTK;

namespace CxECTVolumeWinForms.Rendering
{
    // 方向标记是纯 WinForms 叠加控件，不直接访问 OpenGL 上下文。
    /// <summary>显示当前三维坐标方向；双击时请求恢复默认相机。</summary>
    internal sealed class RenderingOrientationIndicatorControl : Control
    {
        private readonly Func<Matrix4> _getRotation;
        private readonly Action _resetView;

        public RenderingOrientationIndicatorControl(Func<Matrix4> getRotation, Action resetView)
        {
            _getRotation = getRotation;
            _resetView = resetView;
            DoubleBuffered = true;
            Cursor = Cursors.Hand;
            BackColor = Color.Black;
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e);
            _resetView();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(BackColor);
            PointF origin = new PointF(Width * 0.5f, Height * 0.58f);
            DrawAxis(e.Graphics, origin, Vector3.UnitZ, Color.FromArgb(51, 122, 255));
            DrawAxis(e.Graphics, origin, Vector3.UnitY, Color.FromArgb(71, 217, 82));
            DrawAxis(e.Graphics, origin, Vector3.UnitX, Color.FromArgb(255, 56, 51));
        }

        private void DrawAxis(Graphics graphics, PointF origin, Vector3 axis, Color color)
        {
            Vector3 direction = Project(axis);
            float length = 28.0f + direction.Z * 5.0f;
            PointF end = new PointF(origin.X + direction.X * length, origin.Y - direction.Y * length);
            using (var pen = new Pen(color, Math.Max(1.5f, 2.2f + direction.Z)))
            {
                pen.CustomEndCap = new AdjustableArrowCap(3, 4);
                graphics.DrawLine(pen, origin, end);
            }
        }

        private Vector3 Project(Vector3 axis)
        {
            Matrix4 rotation = _getRotation();
            Vector3 rotated = new Vector3(
                axis.X * rotation.M11 + axis.Y * rotation.M21 + axis.Z * rotation.M31,
                axis.X * rotation.M12 + axis.Y * rotation.M22 + axis.Z * rotation.M32,
                axis.X * rotation.M13 + axis.Y * rotation.M23 + axis.Z * rotation.M33);
            if (rotated.LengthSquared > 0.000001f) rotated.Normalize();
            return rotated;
        }
    }
}
