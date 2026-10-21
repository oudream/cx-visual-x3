using System;
using System.Drawing;
using System.Windows.Forms;
using CxECTVolumeCore.Slicing;
using CxECTVolumeCore.Volume;
using CxECTVolumeWinForms.Rendering;

namespace CxECTVolumeWinForms.Views
{
    /// <summary>组织三个正交二维切片与一个三维体渲染视图的四视图布局。</summary>
    public sealed class FourViewLayoutControl : UserControl
    {
        private readonly TableLayoutPanel _grid;
        private readonly ViewSlotControl _xySlot;
        private readonly ViewSlotControl _xzSlot;
        private readonly ViewSlotControl _yzSlot;
        private readonly ViewSlotControl _volumeSlot;
        private readonly HalconSliceViewControl _xyView;
        private readonly HalconSliceViewControl _xzView;
        private readonly HalconSliceViewControl _yzView;
        private ViewSlotControl _maximizedSlot;

        public FourViewLayoutControl(VolumeData volume, VolumeState state)
        {
            BackColor = Color.FromArgb(18, 20, 24);

            _xyView = new HalconSliceViewControl(SlicePlane.XY, volume, state) { Dock = DockStyle.Fill };
            _xzView = new HalconSliceViewControl(SlicePlane.XZ, volume, state) { Dock = DockStyle.Fill };
            _yzView = new HalconSliceViewControl(SlicePlane.YZ, volume, state) { Dock = DockStyle.Fill };
            Renderer = new RenderingVolumeControl(volume, state) { Dock = DockStyle.Fill };

            _grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = BackColor,
                Padding = new Padding(4),
            };
            _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            _grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            _grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            Controls.Add(_grid);

            _xySlot = CreateSlot(_xyView, _xyView.Plane.GetTitle());
            _xzSlot = CreateSlot(_xzView, _xzView.Plane.GetTitle());
            _yzSlot = CreateSlot(_yzView, _yzView.Plane.GetTitle());
            _volumeSlot = CreateSlot(Renderer, "3D Volume");

            _grid.Controls.Add(_xzSlot, 0, 0);
            _grid.Controls.Add(_yzSlot, 1, 0);
            _grid.Controls.Add(_xySlot, 0, 1);
            _grid.Controls.Add(_volumeSlot, 1, 1);
        }

        public RenderingVolumeControl Renderer { get; private set; }

        public string ActiveVoxelInfo { get; private set; }

        public event EventHandler ActiveVoxelInfoChanged;

        public void LoadVolume(VolumeData volume)
        {
            // --- 四视图分发点：同一份 VolumeData 同时进入三张 2D 切片和右下角 3D 体渲染。
            LoadSliceViews(volume);
            Renderer.LoadVolume(volume);
        }

        public void LoadSliceViews(VolumeData volume)
        {
            _xyView.LoadVolume(volume);
            _xzView.LoadVolume(volume);
            _yzView.LoadVolume(volume);
        }

        public void LoadHighResolutionSlices(VolumeData volume)
        {
            LoadSliceViews(volume);
        }

        public void FitSliceViews()
        {
            _xyView.FitToWindow();
            _xzView.FitToWindow();
            _yzView.FitToWindow();
        }

        public void SetSlicePseudoColor(bool enabled)
        {
            _xyView.PseudoColor = enabled;
            _xzView.PseudoColor = enabled;
            _yzView.PseudoColor = enabled;
        }

        public void SetSliceWindow(byte min, byte max)
        {
            _xyView.SetWindowRange(min, max);
            _xzView.SetWindowRange(min, max);
            _yzView.SetWindowRange(min, max);
        }

        public Bitmap CaptureScreenshot()
        {
            var bitmap = new Bitmap(Math.Max(1, Width), Math.Max(1, Height));
            if (Visible && IsHandleCreated)
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(PointToScreen(Point.Empty), Point.Empty, Size);
                }
            }
            else
            {
                DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                using (var rendererImage = Renderer.CaptureImage())
                {
                    if (rendererImage != null)
                    {
                        var rendererPoint = PointToClient(Renderer.PointToScreen(Point.Empty));
                        using (var graphics = Graphics.FromImage(bitmap))
                        {
                            graphics.DrawImage(rendererImage, new Rectangle(rendererPoint, Renderer.Size));
                        }
                    }
                }
            }
            return bitmap;
        }

        private ViewSlotControl CreateSlot(Control content, string title)
        {
            var slot = new ViewSlotControl(title, content)
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(4),
            };
            slot.MaximizeRequested += delegate { ToggleMaximize(slot); };

            var sliceView = content as HalconSliceViewControl;
            if (sliceView != null)
            {
                sliceView.VoxelInfoChanged += OnSliceVoxelInfoChanged;
            }

            return slot;
        }

        private void OnSliceVoxelInfoChanged(object sender, EventArgs e)
        {
            var view = sender as HalconSliceViewControl;
            if (view == null)
            {
                return;
            }

            ActiveVoxelInfo = view.VoxelInfoText;
            var handler = ActiveVoxelInfoChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void ToggleMaximize(ViewSlotControl slot)
        {
            if (_maximizedSlot == slot)
            {
                _maximizedSlot = null;
                RestoreGridLayout();
                return;
            }

            _maximizedSlot = slot;
            foreach (Control control in _grid.Controls)
            {
                control.Visible = ReferenceEquals(control, slot);
            }

            _grid.SetCellPosition(slot, new TableLayoutPanelCellPosition(0, 0));
            _grid.SetColumnSpan(slot, 2);
            _grid.SetRowSpan(slot, 2);
            slot.BringToFront();
        }

        private void RestoreGridLayout()
        {
            _grid.SetColumnSpan(_xzSlot, 1);
            _grid.SetColumnSpan(_yzSlot, 1);
            _grid.SetColumnSpan(_xySlot, 1);
            _grid.SetColumnSpan(_volumeSlot, 1);
            _grid.SetRowSpan(_xzSlot, 1);
            _grid.SetRowSpan(_yzSlot, 1);
            _grid.SetRowSpan(_xySlot, 1);
            _grid.SetRowSpan(_volumeSlot, 1);

            _grid.SetCellPosition(_xzSlot, new TableLayoutPanelCellPosition(0, 0));
            _grid.SetCellPosition(_yzSlot, new TableLayoutPanelCellPosition(1, 0));
            _grid.SetCellPosition(_xySlot, new TableLayoutPanelCellPosition(0, 1));
            _grid.SetCellPosition(_volumeSlot, new TableLayoutPanelCellPosition(1, 1));

            foreach (Control control in _grid.Controls)
            {
                control.Visible = true;
            }
        }

        /// <summary>承载单个视图并提供标题栏和最大化切换的布局容器。</summary>
        private sealed class ViewSlotControl : UserControl
        {
            private readonly Button _maximizeButton;

            public ViewSlotControl(string title, Control content)
            {
                BackColor = Color.FromArgb(36, 40, 47);
                Padding = new Padding(1);

                var root = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 2,
                    BackColor = BackColor,
                };
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                Controls.Add(root);

                var header = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.FromArgb(44, 49, 58),
                };
                root.Controls.Add(header, 0, 0);

                var label = new Label
                {
                    Text = title,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    ForeColor = Color.FromArgb(220, 228, 238),
                    Padding = new Padding(8, 0, 0, 0),
                };
                header.Controls.Add(label);

                _maximizeButton = new Button
                {
                    Text = "□",
                    Dock = DockStyle.Right,
                    Width = 32,
                    FlatStyle = FlatStyle.Flat,
                    ForeColor = Color.FromArgb(220, 228, 238),
                    BackColor = Color.FromArgb(54, 60, 70),
                    TabStop = false,
                };
                _maximizeButton.FlatAppearance.BorderSize = 0;
                _maximizeButton.Click += delegate
                {
                    var handler = MaximizeRequested;
                    if (handler != null)
                    {
                        handler(this, EventArgs.Empty);
                    }
                };
                header.Controls.Add(_maximizeButton);
                _maximizeButton.BringToFront();

                content.Dock = DockStyle.Fill;
                root.Controls.Add(content, 0, 1);
            }

            public event EventHandler MaximizeRequested;
        }
    }
}
