using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using CxECTVolumeCore.Slicing;
using CxECTVolumeCore.Utils;
using CxECTVolumeCore.Volume;

namespace CxECTVolumeWinForms.Views
{
    /// <summary>基于 WinForms 位图的二维切片视图，负责显示、缩放、平移和十字线交互。</summary>
    public sealed class SliceViewControl : UserControl
    {
        private readonly SlicePlane _plane;
        private readonly VolumeState _state;
        private VolumeData _volume;
        private SliceImage _sliceImage;
        private RectangleF _imageBounds;
        private float _zoom = 1.0f;
        private PointF _pan;
        private bool _isFitted = true;
        private bool _draggingCrosshair;
        private bool _panning;
        private Point _lastMouse;
        private bool _pseudoColor;
        private byte _windowMin = 0;
        private byte _windowMax = 255;

        public SliceViewControl(SlicePlane plane, VolumeData volume, VolumeState state)
        {
            _plane = plane;
            _volume = volume;
            _state = state;

            DoubleBuffered = true;
            BackColor = Color.FromArgb(13, 15, 18);
            ForeColor = Color.FromArgb(224, 230, 238);
            TabStop = true;

            _state.PropertyChanged += OnStateChanged;
            RebuildSlice();
        }

        public event EventHandler VoxelInfoChanged;

        public SlicePlane Plane
        {
            get { return _plane; }
        }

        public string VoxelInfoText { get; private set; }

        public bool PseudoColor
        {
            get { return _pseudoColor; }
            set
            {
                if (_pseudoColor == value)
                {
                    return;
                }

                _pseudoColor = value;
                RebuildSlice();
            }
        }

        public void SetWindowRange(byte min, byte max)
        {
            if (_windowMin == min && _windowMax == max)
            {
                return;
            }

            _windowMin = min;
            _windowMax = max;
            RebuildSlice();
        }

        public void LoadVolume(VolumeData volume)
        {
            _volume = volume;
            _isFitted = true;
            RebuildSlice();
        }

        public void FitToWindow()
        {
            _isFitted = true;
            _zoom = 1.0f;
            _pan = PointF.Empty;
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _state.PropertyChanged -= OnStateChanged;
                if (_sliceImage != null)
                {
                    _sliceImage.Bitmap.Dispose();
                }
            }

            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
            e.Graphics.Clear(BackColor);

            if (_sliceImage == null)
            {
                return;
            }

            UpdateImageBounds();
            e.Graphics.DrawImage(_sliceImage.Bitmap, _imageBounds);
            if (_state.ShowCrosshair)
            {
                DrawCrosshair(e.Graphics);
            }
            DrawGrayScaleBar(e.Graphics);
            DrawOverlayText(e.Graphics);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_isFitted)
            {
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            _lastMouse = e.Location;

            if (e.Button == MouseButtons.Left && TryScreenToImage(e.Location, out _, out _))
            {
                _draggingCrosshair = true;
                UpdateSlicesFromMouse(e.Location);
            }
            else if (e.Button == MouseButtons.Right || e.Button == MouseButtons.Middle)
            {
                _panning = true;
                _isFitted = false;
                Cursor = Cursors.SizeAll;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_draggingCrosshair)
            {
                UpdateSlicesFromMouse(e.Location);
            }
            else if (_panning)
            {
                _pan.X += e.X - _lastMouse.X;
                _pan.Y += e.Y - _lastMouse.Y;
                _lastMouse = e.Location;
                Invalidate();
            }
            else
            {
                UpdateVoxelInfo(e.Location);
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            Focus();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _draggingCrosshair = false;
            _panning = false;
            Cursor = Cursors.Default;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            if ((ModifierKeys & Keys.Control) == Keys.Control)
            {
                ZoomAt(e.Location, e.Delta > 0 ? 1.15f : 1.0f / 1.15f);
                return;
            }

            var step = e.Delta > 0 ? 1 : -1;
            switch (_plane)
            {
                case SlicePlane.XY:
                    _state.SetSliceZ(_state.SliceZ + step, _volume);
                    break;
                case SlicePlane.XZ:
                    _state.SetSliceY(_state.SliceY + step, _volume);
                    break;
                case SlicePlane.YZ:
                    _state.SetSliceX(_state.SliceX + step, _volume);
                    break;
            }
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e);
            FitToWindow();
        }

        private void OnStateChanged(object sender, PropertyChangedEventArgs e)
        {
            // 当前平面的固定轴变化时需要重建图像，非固定轴变化只需刷新十字线。
            if ((_plane == SlicePlane.XY && e.PropertyName == "SliceZ") ||
                (_plane == SlicePlane.XZ && e.PropertyName == "SliceY") ||
                (_plane == SlicePlane.YZ && e.PropertyName == "SliceX") ||
                e.PropertyName == "All")
            {
                RebuildSlice();
            }
            else
            {
                Invalidate();
            }
        }

        private void RebuildSlice()
        {
            var old = _sliceImage;
            _sliceImage = SliceImageBuilder.Build(_volume, _state, _plane, _pseudoColor, _windowMin, _windowMax);
            if (old != null)
            {
                old.Bitmap.Dispose();
            }

            Invalidate();
        }

        private void UpdateImageBounds()
        {
            if (_sliceImage == null)
            {
                _imageBounds = RectangleF.Empty;
                return;
            }

            var availableWidth = Math.Max(1, Width - 12);
            var availableHeight = Math.Max(1, Height - 12);
            var fitScale = Math.Min(availableWidth / (float)_sliceImage.Width, availableHeight / (float)_sliceImage.Height);
            var scale = fitScale * _zoom;
            var drawWidth = _sliceImage.Width * scale;
            var drawHeight = _sliceImage.Height * scale;
            var left = (Width - drawWidth) * 0.5f + _pan.X;
            var top = (Height - drawHeight) * 0.5f + _pan.Y;
            _imageBounds = new RectangleF(left, top, drawWidth, drawHeight);
        }

        private void DrawCrosshair(Graphics graphics)
        {
            int imageX;
            int imageY;
            GetCrosshairImagePoint(out imageX, out imageY);

            var x = _imageBounds.Left + imageX / (float)Math.Max(1, _sliceImage.Width - 1) * _imageBounds.Width;
            var y = _imageBounds.Top + imageY / (float)Math.Max(1, _sliceImage.Height - 1) * _imageBounds.Height;

            using (var xPen = new Pen(GetHorizontalAxisColor(), 1.4f))
            using (var yPen = new Pen(GetVerticalAxisColor(), 1.4f))
            {
                xPen.DashStyle = DashStyle.Dash;
                yPen.DashStyle = DashStyle.Dash;
                graphics.DrawLine(xPen, x, _imageBounds.Top, x, _imageBounds.Bottom);
                graphics.DrawLine(yPen, _imageBounds.Left, y, _imageBounds.Right, y);
            }
        }

        private void DrawOverlayText(Graphics graphics)
        {
            var text = string.Format("{0}  |  wheel: {1} slice", _plane.GetTitle(), _plane.GetWheelAxisName());
            TextRenderer.DrawText(graphics, text, Font, new Point(8, 7), ForeColor);

            if (!string.IsNullOrEmpty(VoxelInfoText))
            {
                TextRenderer.DrawText(graphics, VoxelInfoText, Font, new Point(8, Height - 24), Color.FromArgb(190, 202, 218));
            }
        }

        private void DrawGrayScaleBar(Graphics graphics)
        {
            var barHeight = Math.Min(150, Math.Max(60, Height - 60));
            var bar = new Rectangle(Width - 28, 36, 10, barHeight);
            if (bar.Left <= 0)
            {
                return;
            }

            for (var i = 0; i < bar.Height; i++)
            {
                var value = (byte)Math.Round(255.0 * (1.0 - i / (double)Math.Max(1, bar.Height - 1)));
                var displayValue = SliceImageBuilder.ApplyWindow(value, _windowMin, _windowMax);
                var color = _pseudoColor ? SliceImageBuilder.ToPseudoColor(displayValue) : Color.FromArgb(displayValue, displayValue, displayValue);
                using (var pen = new Pen(color))
                {
                    graphics.DrawLine(pen, bar.Left, bar.Top + i, bar.Right, bar.Top + i);
                }
            }

            using (var outline = new Pen(Color.FromArgb(170, 178, 190)))
            {
                graphics.DrawRectangle(outline, bar);
            }

            TextRenderer.DrawText(graphics, _volume.ScalarMax.ToString("g3"), Font, new Point(bar.Left - 6, bar.Top - 18), ForeColor);
            TextRenderer.DrawText(graphics, _volume.ScalarMin.ToString("g3"), Font, new Point(bar.Left - 6, bar.Bottom + 2), ForeColor);
        }

        private void GetCrosshairImagePoint(out int imageX, out int imageY)
        {
            switch (_plane)
            {
                case SlicePlane.XY:
                    imageX = _state.SliceX;
                    imageY = _state.SliceY;
                    break;
                case SlicePlane.XZ:
                    imageX = _state.SliceX;
                    imageY = _state.SliceZ;
                    break;
                case SlicePlane.YZ:
                    imageX = _state.SliceY;
                    imageY = _state.SliceZ;
                    break;
                default:
                    imageX = 0;
                    imageY = 0;
                    break;
            }
        }

        private Color GetHorizontalAxisColor()
        {
            switch (_plane)
            {
                case SlicePlane.XY:
                case SlicePlane.XZ:
                    return Color.FromArgb(245, 88, 88);
                case SlicePlane.YZ:
                    return Color.FromArgb(85, 210, 120);
                default:
                    return Color.White;
            }
        }

        private Color GetVerticalAxisColor()
        {
            switch (_plane)
            {
                case SlicePlane.XY:
                    return Color.FromArgb(85, 210, 120);
                case SlicePlane.XZ:
                case SlicePlane.YZ:
                    return Color.FromArgb(80, 150, 255);
                default:
                    return Color.White;
            }
        }

        private bool TryScreenToImage(Point point, out int imageX, out int imageY)
        {
            UpdateImageBounds();
            if (_sliceImage == null || !_imageBounds.Contains(point))
            {
                imageX = 0;
                imageY = 0;
                return false;
            }

            imageX = (int)Math.Round((point.X - _imageBounds.Left) / Math.Max(1.0f, _imageBounds.Width) * (_sliceImage.Width - 1));
            imageY = (int)Math.Round((point.Y - _imageBounds.Top) / Math.Max(1.0f, _imageBounds.Height) * (_sliceImage.Height - 1));
            imageX = FrameworkCompat.Clamp(imageX, 0, _sliceImage.Width - 1);
            imageY = FrameworkCompat.Clamp(imageY, 0, _sliceImage.Height - 1);
            return true;
        }

        private void UpdateSlicesFromMouse(Point point)
        {
            int imageX;
            int imageY;
            if (!TryScreenToImage(point, out imageX, out imageY))
            {
                return;
            }

            switch (_plane)
            {
                case SlicePlane.XY:
                    _state.SetSlices(imageX, imageY, _state.SliceZ, _volume);
                    break;
                case SlicePlane.XZ:
                    _state.SetSlices(imageX, _state.SliceY, imageY, _volume);
                    break;
                case SlicePlane.YZ:
                    _state.SetSlices(_state.SliceX, imageX, imageY, _volume);
                    break;
            }

            UpdateVoxelInfo(point);
        }

        private void UpdateVoxelInfo(Point point)
        {
            int imageX;
            int imageY;
            if (!TryScreenToImage(point, out imageX, out imageY))
            {
                return;
            }

            int x;
            int y;
            int z;
            SliceImageBuilder.ImageToVoxel(_volume, _state, _plane, imageX, imageY, out x, out y, out z);
            var value = SliceImageBuilder.GetVoxel(_volume, x, y, z);
            var scalar = SliceImageBuilder.GetScalarValue(_volume, x, y, z);
            VoxelInfoText = string.Format(
                "X:{0}  Y:{1}  Z:{2}  Value:{3:g}  P:{4:g},{5:g},{6:g}",
                x,
                y,
                z,
                scalar,
                x * _volume.SpacingX,
                y * _volume.SpacingY,
                z * _volume.SpacingZ);
            var handler = VoxelInfoChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }

            Invalidate();
        }

        private void ZoomAt(Point location, float factor)
        {
            var oldZoom = _zoom;
            _zoom = FrameworkCompat.Clamp(_zoom * factor, 0.25f, 12.0f);
            if (Math.Abs(_zoom - oldZoom) < 0.0001f)
            {
                return;
            }

            _isFitted = false;
            _pan.X = location.X - (location.X - _pan.X - Width * 0.5f) * (_zoom / oldZoom) - Width * 0.5f;
            _pan.Y = location.Y - (location.Y - _pan.Y - Height * 0.5f) * (_zoom / oldZoom) - Height * 0.5f;
            Invalidate();
        }
    }
}
