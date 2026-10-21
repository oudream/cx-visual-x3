using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using CxECTVolumeCore.Slicing;
using CxECTVolumeCore.Utils;
using CxECTVolumeCore.Volume;
using HalconDotNet;
using HalconDriverModule;

namespace CxECTVolumeWinForms.Views
{
    /// <summary>基于 HALCON 窗口的二维切片视图，负责切片显示及与三维视图同步的鼠标交互。</summary>
    public sealed class HalconSliceViewControl : UserControl
    {
        private readonly SlicePlane _plane;
        private readonly VolumeState _state;
        private readonly HWindowControl _hWindowControl;
        private readonly HalconWindowTools _hWindowTools;
        private readonly HWindowControlView _hWindowControlView;
        private VolumeData _volume;
        private SliceImage _sliceImage;
        private float _zoom = 1.0f;
        private bool _pseudoColor;
        private bool _draggingCrosshair;
        private bool _panning;
        private bool _fitting = true;
        private byte _windowMin = 0;
        private byte _windowMax = 255;
        private double _lastRow;
        private double _lastCol;

        public HalconSliceViewControl(SlicePlane plane, VolumeData volume, VolumeState state)
        {
            _plane = plane;
            _volume = volume;
            _state = state;

            BackColor = Color.FromArgb(13, 15, 18);
            ForeColor = Color.FromArgb(224, 230, 238);

            _hWindowControl = new HWindowControl
            {
                Dock = DockStyle.Fill,
                BackColor = BackColor,
            };
            Controls.Add(_hWindowControl);

            _hWindowTools = new HalconWindowTools(_hWindowControl, 14, 1.0, new[] { 1.0 });
            _hWindowControlView = new HWindowControlView(_hWindowTools);
            // 当前控件需要用鼠标拖十字线和滚轮切片，默认图片浏览事件会冲突，这里只复用显示封装。
            _hWindowControlView.SetHMouseDownRelease(false);
            _hWindowControlView.SetHMouseMoveRelease(false);
            _hWindowControlView.SetHMouseUpRelease(false);
            _hWindowControlView.SetHMouseWheelRelease(false);
            _hWindowControlView.SetKeyDownRelease(false);

            _hWindowControl.HMouseDown += OnHMouseDown;
            _hWindowControl.HMouseMove += OnHMouseMove;
            _hWindowControl.HMouseUp += OnHMouseUp;
            _hWindowControl.HMouseWheel += OnHMouseWheel;
            _hWindowControl.Resize += delegate { if (_fitting) FitToWindow(); };
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
            // --- 2D 路径入口：收到新体数据后，当前视图会按自己的平面 XY/XZ/YZ 重建一张切片图。
            _volume = volume;
            _fitting = true;
            _zoom = 1.0f;
            RebuildSlice();
        }

        public void FitToWindow()
        {
            _fitting = true;
            _zoom = 1.0f;
            if (_sliceImage != null)
            {
                _hWindowTools.DisplayAdaptiveSizeImage();
                UpdateViewportState();
                DrawOverlays();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _state.PropertyChanged -= OnStateChanged;
                _hWindowControl.HMouseDown -= OnHMouseDown;
                _hWindowControl.HMouseMove -= OnHMouseMove;
                _hWindowControl.HMouseUp -= OnHMouseUp;
                _hWindowControl.HMouseWheel -= OnHMouseWheel;
                if (_sliceImage != null)
                {
                    _sliceImage.Bitmap.Dispose();
                }

                _hWindowTools.Dispose();
            }

            base.Dispose(disposing);
        }

        private void OnStateChanged(object sender, PropertyChangedEventArgs e)
        {
            if ((_plane == SlicePlane.XY && e.PropertyName == "SliceZ") ||
                (_plane == SlicePlane.XZ && e.PropertyName == "SliceY") ||
                (_plane == SlicePlane.YZ && e.PropertyName == "SliceX") ||
                e.PropertyName == "All")
            {
                RebuildSlice();
            }
            else
            {
                DrawOverlays();
            }
        }

        private void RebuildSlice()
        {
            var oldSlice = _sliceImage;
            // --- SliceImageBuilder 从 VolumeData 按当前 SliceX/Y/Z 取样，生成一张 WinForms Bitmap。
            _sliceImage = SliceImageBuilder.Build(_volume, _state, _plane, _pseudoColor, _windowMin, _windowMax);
            if (oldSlice != null)
            {
                oldSlice.Bitmap.Dispose();
            }

            var image = BitmapToHObject(_sliceImage.Bitmap);
            // --- Bitmap 转成 HALCON HObject 后交给 HWindowControlView，最终显示在对应 2D 窗口里。
            _hWindowControlView.OpenImage(image, 0, 0);

            if (_fitting)
            {
                _hWindowTools.DisplayAdaptiveSizeImage();
            }

            UpdateViewportState();
            DrawOverlays();
        }

        private void DrawOverlays()
        {
            if (_sliceImage == null || _hWindowControl.HalconWindow == null)
            {
                return;
            }

            _hWindowTools.ClearAllDrawingOnly();
            if (_state.ShowCrosshair)
            {
                _hWindowTools.AddNewDrawObject(null, null, null, 0, DrawCrosshair, null);
            }

            // 灰阶/伪彩映射统一由右侧传递函数面板呈现，避免每个切片重复遮挡图像。
            _hWindowTools.ReShowDrawALL();
        }

        private void DrawCrosshair(HTuple hWindow)
        {
            int imageX;
            int imageY;
            GetCrosshairImagePoint(out imageX, out imageY);
            HOperatorSet.SetDraw(hWindow, "margin");
            HOperatorSet.SetLineWidth(hWindow, 1);
            HOperatorSet.SetColor(hWindow, GetHorizontalAxisColor());
            HOperatorSet.DispLine(hWindow, 0, imageX, _sliceImage.Height - 1, imageX);
            HOperatorSet.SetColor(hWindow, GetVerticalAxisColor());
            HOperatorSet.DispLine(hWindow, imageY, 0, imageY, _sliceImage.Width - 1);
        }

        private void OnHMouseDown(object sender, HMouseEventArgs e)
        {
            Focus();
            _hWindowControl.Focus();
            _lastRow = e.Y;
            _lastCol = e.X;

            if (e.Button == MouseButtons.Left)
            {
                _draggingCrosshair = true;
                UpdateSlicesFromImagePoint(e.X, e.Y);
            }
            else if (e.Button == MouseButtons.Right || e.Button == MouseButtons.Middle)
            {
                _panning = true;
                _fitting = false;
            }
        }

        private void OnHMouseMove(object sender, HMouseEventArgs e)
        {
            if (_draggingCrosshair)
            {
                UpdateSlicesFromImagePoint(e.X, e.Y);
            }
            else if (_panning)
            {
                Pan(e.X - _lastCol, e.Y - _lastRow);
                _lastRow = e.Y;
                _lastCol = e.X;
            }
            else
            {
                UpdateVoxelInfo(e.X, e.Y);
            }
        }

        private void OnHMouseUp(object sender, HMouseEventArgs e)
        {
            _draggingCrosshair = false;
            _panning = false;
        }

        private void OnHMouseWheel(object sender, HMouseEventArgs e)
        {
            if ((ModifierKeys & Keys.Control) == Keys.Control)
            {
                Zoom(e.Delta > 0 ? 0.85 : 1.0 / 0.85);
                return;
            }

            var direction = e.Delta > 0 ? 1 : -1;
            switch (_plane)
            {
                case SlicePlane.XY:
                    _state.SetSliceZ(_state.SliceZ + direction * GetWheelStep(_volume.OriginalDepth, _volume.Depth), _volume);
                    break;
                case SlicePlane.XZ:
                    _state.SetSliceY(_state.SliceY + direction * GetWheelStep(_volume.OriginalHeight, _volume.Height), _volume);
                    break;
                case SlicePlane.YZ:
                    _state.SetSliceX(_state.SliceX + direction * GetWheelStep(_volume.OriginalWidth, _volume.Width), _volume);
                    break;
            }
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e);
            FitToWindow();
        }

        private void Pan(double deltaCol, double deltaRow)
        {
            HTuple row1;
            HTuple col1;
            HTuple row2;
            HTuple col2;
            HOperatorSet.GetPart(_hWindowControl.HalconWindow, out row1, out col1, out row2, out col2);
            HOperatorSet.SetPart(
                _hWindowControl.HalconWindow,
                row1.D - deltaRow,
                col1.D - deltaCol,
                row2.D - deltaRow,
                col2.D - deltaCol);
            UpdateViewportState();
            _hWindowTools.ReShowDrawALL();
        }

        private void Zoom(double factor)
        {
            _fitting = false;
            _zoom = FrameworkCompat.Clamp((float)(_zoom * factor), 0.05f, 20.0f);
            HTuple row1;
            HTuple col1;
            HTuple row2;
            HTuple col2;
            HOperatorSet.GetPart(_hWindowControl.HalconWindow, out row1, out col1, out row2, out col2);
            var centerRow = (row1.D + row2.D) * 0.5;
            var centerCol = (col1.D + col2.D) * 0.5;
            var halfHeight = (row2.D - row1.D) * factor * 0.5;
            var halfWidth = (col2.D - col1.D) * factor * 0.5;
            HOperatorSet.SetPart(_hWindowControl.HalconWindow, centerRow - halfHeight, centerCol - halfWidth, centerRow + halfHeight, centerCol + halfWidth);
            UpdateViewportState();
            _hWindowTools.ReShowDrawALL();
        }

        private void UpdateViewportState()
        {
            if (_sliceImage == null || _hWindowControl.HalconWindow == null)
            {
                return;
            }

            HTuple row1;
            HTuple col1;
            HTuple row2;
            HTuple col2;
            HOperatorSet.GetPart(_hWindowControl.HalconWindow, out row1, out col1, out row2, out col2);

            var minCol = Math.Min(col1.D, col2.D);
            var maxCol = Math.Max(col1.D, col2.D);
            var minRow = Math.Min(row1.D, row2.D);
            var maxRow = Math.Max(row1.D, row2.D);

            // --- 当前 HALCON 窗口的 GetPart 就是 2D 可视范围；写入 VolumeState 后，3D 彩色平面同步大小和位置。
            _state.SetSliceViewport(
                _plane,
                ImageAxisToNormalized(minCol, _sliceImage.Width),
                ImageAxisToNormalized(maxCol, _sliceImage.Width),
                ImageAxisToNormalized(minRow, _sliceImage.Height),
                ImageAxisToNormalized(maxRow, _sliceImage.Height));
        }

        private static float ImageAxisToNormalized(double coordinate, int imageSize)
        {
            if (imageSize <= 1)
            {
                return 0.0f;
            }

            return (float)(coordinate / (imageSize - 1));
        }

        private void UpdateSlicesFromImagePoint(double col, double row)
        {
            var imageX = FrameworkCompat.Clamp((int)Math.Round(col), 0, _sliceImage.Width - 1);
            var imageY = FrameworkCompat.Clamp((int)Math.Round(row), 0, _sliceImage.Height - 1);
            int x;
            int y;
            int z;
            SliceImageBuilder.ImageToVoxel(_volume, _state, _plane, imageX, imageY, out x, out y, out z);
            _state.SetSlices(x, y, z, _volume);

            UpdateVoxelInfo(col, row);
        }

        private void UpdateVoxelInfo(double col, double row)
        {
            var imageX = FrameworkCompat.Clamp((int)Math.Round(col), 0, _sliceImage.Width - 1);
            var imageY = FrameworkCompat.Clamp((int)Math.Round(row), 0, _sliceImage.Height - 1);
            int x;
            int y;
            int z;
            SliceImageBuilder.ImageToVoxel(_volume, _state, _plane, imageX, imageY, out x, out y, out z);
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
        }

        private void GetCrosshairImagePoint(out int imageX, out int imageY)
        {
            SliceImageBuilder.VoxelToImage(_volume, _plane, _state.SliceX, _state.SliceY, _state.SliceZ, out imageX, out imageY);
        }

        private static int GetWheelStep(int originalCount, int displayCount)
        {
            return Math.Max(1, (int)Math.Ceiling(originalCount / (double)Math.Max(1, displayCount)));
        }

        private string GetHorizontalAxisColor()
        {
            switch (_plane)
            {
                case SlicePlane.XY:
                case SlicePlane.XZ:
                    return "red";
                case SlicePlane.YZ:
                    return "green";
                default:
                    return "white";
            }
        }

        private string GetVerticalAxisColor()
        {
            switch (_plane)
            {
                case SlicePlane.XY:
                    return "green";
                case SlicePlane.XZ:
                case SlicePlane.YZ:
                    return "blue";
                default:
                    return "white";
            }
        }

        private static string ToHalconColor(Color color)
        {
            return string.Format("#{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B);
        }

        private static HObject BitmapToHObject(Bitmap bitmap)
        {
            // SliceImageBuilder 已创建 Format24bppRgb 位图，无需再复制一整张图后再交给 HALCON。
            var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                HObject image;
                HOperatorSet.GenImageInterleaved(
                    out image,
                    data.Scan0,
                    "bgr",
                    bitmap.Width,
                    bitmap.Height,
                    0,
                    "byte",
                    bitmap.Width,
                    bitmap.Height,
                    0,
                    0,
                    -1,
                    0);
                return image;
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }
    }
}
