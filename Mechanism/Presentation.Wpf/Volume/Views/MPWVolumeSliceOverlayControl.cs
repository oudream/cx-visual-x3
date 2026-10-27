using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using CxVisual.Foundation.Volume;
using CxVisual.Mechanism.Presentation.Wpf.Imaging;

namespace CxVisual.Mechanism.Presentation.Wpf.Volume
{
    /// <summary>
    /// 可嵌入的体数据正交切片十字线与色标叠加层。仅负责显示；
    /// 坐标使用FVO原始体素到切片的映射，不持有第二份MVI状态。
    /// </summary>
    public sealed class MPWVolumeSliceOverlayControl : Grid
    {
        private readonly Canvas _crosshair;
        private readonly Line _first;
        private readonly Line _second;
        private readonly Border _legend;

        public MPWVolumeSliceOverlayControl(FVOSlicePlane plane)
        {
            Plane = plane;
            IsHitTestVisible = false;
            ClipToBounds = true;
            AutomationProperties.SetAutomationId(this, "MPW.Volume.SliceOverlay." + plane);

            _crosshair = new Canvas { IsHitTestVisible = false, ClipToBounds = true };
            Children.Add(_crosshair);
            _first = new Line { StrokeThickness = 1.5,
                IsHitTestVisible = false, Visibility = Visibility.Collapsed };
            _second = new Line { StrokeThickness = 1.5,
                IsHitTestVisible = false, Visibility = Visibility.Collapsed };
            switch (plane)
            {
                case FVOSlicePlane.XY:
                    _first.Stroke = Brushes.IndianRed;
                    _second.Stroke = Brushes.LimeGreen;
                    break;
                case FVOSlicePlane.XZ:
                    _first.Stroke = Brushes.IndianRed;
                    _second.Stroke = Brushes.DodgerBlue;
                    break;
                case FVOSlicePlane.YZ:
                    _first.Stroke = Brushes.LimeGreen;
                    _second.Stroke = Brushes.DodgerBlue;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("plane");
            }
            _crosshair.Children.Add(_first);
            _crosshair.Children.Add(_second);

            _legend = new Border {
                Background = new SolidColorBrush(Color.FromArgb(190, 7, 15, 26)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(7), Width = 160,
                Padding = new Thickness(5, 3, 5, 3),
                IsHitTestVisible = false
            };
            Children.Add(_legend);
        }

        public FVOSlicePlane Plane { get; private set; }
        public int VisibleCrosshairCount
        {
            get
            {
                return (_first.Visibility == Visibility.Visible ? 1 : 0) +
                    (_second.Visibility == Visibility.Visible ? 1 : 0);
            }
        }
        public bool HasLegend { get { return _legend.Child != null; } }

        /// <summary>按实际WPF图像变换绘制十字线，保留缩放/平移与非等距Spacing。</summary>
        public void UpdateCrosshair(MPWImageViewerControl viewer, FVOVolumeData volume,
            int sliceX, int sliceY, int sliceZ)
        {
            if (viewer == null || volume == null || viewer.ActiveImage == null)
                return;
            Image image = viewer.FindName("PixelImage") as Image;
            if (image == null || image.Source == null || image.Width <= 0 || image.Height <= 0 ||
                _crosshair.ActualWidth <= 0 || _crosshair.ActualHeight <= 0)
                return;

            int ix, iy;
            FVOSliceImageBuilder.VoxelToImage(volume, Plane, sliceX, sliceY, sliceZ,
                out ix, out iy);
            Point corner1 = image.TranslatePoint(new Point(0, 0), _crosshair);
            Point corner2 = image.TranslatePoint(
                new Point(image.Width, image.Height), _crosshair);
            Point cursor = image.TranslatePoint(new Point(ix + .5, iy + .5), _crosshair);
            SetLine(_first, cursor.X, corner1.Y, cursor.X, corner2.Y);
            SetLine(_second, corner1.X, cursor.Y, corner2.X, cursor.Y);
        }

        public void ClearCrosshairs()
        {
            _first.Visibility = Visibility.Collapsed;
            _second.Visibility = Visibility.Collapsed;
        }

        /// <summary>显示原始标量范围、窗口及FVO灰度/伪彩渐变，供不同宿主复用。</summary>
        public void UpdateLegend(FVOVolumeData volume, double windowMin,
            double windowMax, bool pseudoColor)
        {
            if (volume == null) throw new ArgumentNullException("volume");
            StackPanel stack = new StackPanel();
            TextBlock title = new TextBlock {
                Text = String.Format("窗 {0:0}..{1:0} / 原值 {2:0.##}..{3:0.##}",
                    windowMin, windowMax, volume.ScalarMin, volume.ScalarMax),
                Foreground = Brushes.White, FontSize = 9
            };
            stack.Children.Add(title);
            LinearGradientBrush brush = new LinearGradientBrush {
                StartPoint = new Point(0, 0), EndPoint = new Point(1, 0)
            };
            for (int i = 0; i <= 8; i++)
            {
                int value = i == 8 ? 255 : i * 32;
                Color color;
                if (pseudoColor)
                {
                    byte[] bgr = new byte[3];
                    FVOSliceImageBuilder.WritePseudoBgr(bgr, 0, (byte)value);
                    color = Color.FromRgb(bgr[2], bgr[1], bgr[0]);
                }
                else
                    color = Color.FromRgb((byte)value, (byte)value, (byte)value);
                brush.GradientStops.Add(new GradientStop(color, i / 8.0));
            }
            Border ramp = new Border {
                Background = brush, Height = 10,
                Margin = new Thickness(0, 4, 0, 0),
                BorderBrush = Brushes.LightSlateGray,
                BorderThickness = new Thickness(.5)
            };
            stack.Children.Add(ramp);
            _legend.Child = stack;
        }

        private static void SetLine(Line line, double x1, double y1, double x2, double y2)
        {
            if (line.Visibility != Visibility.Visible) line.Visibility = Visibility.Visible;
            if (Math.Abs(line.X1 - x1) > .01) line.X1 = x1;
            if (Math.Abs(line.Y1 - y1) > .01) line.Y1 = y1;
            if (Math.Abs(line.X2 - x2) > .01) line.X2 = x2;
            if (Math.Abs(line.Y2 - y2) > .01) line.Y2 = y2;
        }
    }
}
