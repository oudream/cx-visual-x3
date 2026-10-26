using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using CxVisual.Foundation.Volume;
using CxVisual.Mechanism.Presentation.Wpf.Imaging;

namespace CxVisual.TestCenter.UI
{
    /// <summary>
    /// 三维工作台的真实十字线与操作：投影位置直接使用MPW图像的WPF变换，
    /// Ctrl+点击定位其他两刀，Shift+滚轮移动当前平面，普通拖动/滚轮仍归原MPW浏览器。
    /// </summary>
    public sealed partial class TCTManualVolumeViewerFrame
    {
        private readonly Dictionary<FVOSlicePlane, Canvas> _crosshairs =
            new Dictionary<FVOSlicePlane, Canvas>();

        public int VisibleCrosshairCount
        {
            get
            {
                int count = 0;
                foreach (Canvas canvas in _crosshairs.Values)
                    foreach (UIElement item in canvas.Children)
                        if (item.Visibility == Visibility.Visible) count++;
                return count;
            }
        }

        private void AddSliceViewport(Grid host, MPWImageViewerControl viewer,
            FVOSlicePlane plane, string title, int row, int column)
        {
            Grid section = AddViewport(host, viewer, title, row, column);
            section.Children.Remove(viewer);
            Grid content = new Grid();
            Grid.SetRow(content,1);
            section.Children.Add(content);
            Grid.SetRow(viewer,0);
            content.Children.Add(viewer);
            Canvas layer = new Canvas { IsHitTestVisible = false, ClipToBounds = true };
            content.Children.Add(layer);
            _crosshairs.Add(plane, layer);
            Line first = new Line { StrokeThickness = 1.5, IsHitTestVisible = false,
                Visibility = Visibility.Collapsed };
            Line second = new Line { StrokeThickness = 1.5, IsHitTestVisible = false,
                Visibility = Visibility.Collapsed };
            switch (plane)
            {
                case FVOSlicePlane.XY:
                    first.Stroke = Brushes.IndianRed; second.Stroke = Brushes.LimeGreen;
                    break;
                case FVOSlicePlane.XZ:
                    first.Stroke = Brushes.IndianRed; second.Stroke = Brushes.DodgerBlue;
                    break;
                default:
                    first.Stroke = Brushes.LimeGreen; second.Stroke = Brushes.DodgerBlue;
                    break;
            }
            layer.Children.Add(first);
            layer.Children.Add(second);
            content.SizeChanged += (s,e) => UpdateCrosshairs();
            viewer.LayoutUpdated += (s,e) => UpdateCrosshair(viewer,plane,layer);
            content.PreviewMouseLeftButtonDown += (s,e) =>
            {
                if ((Keyboard.Modifiers & ModifierKeys.Control) == 0 || _volume == null)
                    return;
                Image image = viewer.FindName("PixelImage") as Image;
                if (image == null || image.Source == null) return;
                Point position = e.GetPosition(image);
                if (position.X < 0 || position.Y < 0 ||
                    position.X >= image.Width || position.Y >= image.Height) return;
                SetCrosshairFromImage(plane,(int)Math.Round(position.X),(int)Math.Round(position.Y));
                e.Handled = true;
            };
            content.PreviewMouseMove += (s,e) =>
            {
                if(_volume==null || _state==null)return;
                Image image=viewer.FindName("PixelImage") as Image;
                if(image==null||image.Source==null)return;
                Point loc=e.GetPosition(image);
                if(loc.X<0||loc.Y<0||loc.X>=image.Width||loc.Y>=image.Height)return;
                int x,y,z;
                FVOSliceImageBuilder.ImageToVoxel(_volume,
                    _state.SliceX,_state.SliceY,_state.SliceZ,plane,
                    (int)Math.Round(loc.X),(int)Math.Round(loc.Y),out x,out y,out z);
                float value=_volume.GetScalarByOriginalIndex(x,y,z);
                _status.Text=String.Format("体素 ({0},{1},{2})  原始值 {3:0.###}  物理位置 ({4:0.###},{5:0.###},{6:0.###})",
                    x,y,z,value,x*_volume.SpacingX,y*_volume.SpacingY,z*_volume.SpacingZ);
            };
            content.PreviewMouseWheel += (s,e) =>
            {
                if ((Keyboard.Modifiers & ModifierKeys.Shift) == 0 ||
                    _volume == null || _state == null) return;
                int step = e.Delta > 0 ? 1 : -1;
                if (plane == FVOSlicePlane.XY) _state.SetSliceZ(_state.SliceZ + step,_volume);
                else if (plane == FVOSlicePlane.XZ) _state.SetSliceY(_state.SliceY + step,_volume);
                else _state.SetSliceX(_state.SliceX + step,_volume);
                e.Handled = true;
            };
        }

        public void SetCrosshairFromImage(FVOSlicePlane plane, int pixelX, int pixelY)
        {
            if (_volume == null || _state == null) return;
            int x,y,z;
            FVOSliceImageBuilder.ImageToVoxel(_volume,
                _state.SliceX,_state.SliceY,_state.SliceZ,
                plane,pixelX,pixelY,out x,out y,out z);
            _state.SetSlices(x,y,z,_volume);
        }

        public void UpdateCrosshairs()
        {
            if (_volume == null || _state == null)return;
            if (_crosshairEnabled.IsChecked!=true || !_state.ShowCrosshair)
            {
                ClearCrosshairs();
                return;
            }
            foreach (KeyValuePair<FVOSlicePlane,Canvas> pair in _crosshairs)
            {
                MPWImageViewerControl viewer = pair.Key == FVOSlicePlane.XY ? _xy :
                    pair.Key == FVOSlicePlane.XZ ? _xz : _yz;
                UpdateCrosshair(viewer,pair.Key,pair.Value);
            }
        }

        private void UpdateCrosshair(MPWImageViewerControl viewer, FVOSlicePlane plane, Canvas canvas)
        {
            if (_volume == null || _state == null || viewer.ActiveImage == null)
                return;
            Image image = viewer.FindName("PixelImage") as Image;
            if (image == null || image.Source == null || image.Width <= 0 || image.Height <= 0 ||
                canvas.ActualWidth <= 0 || canvas.ActualHeight <= 0) return;
            double fx = plane == FVOSlicePlane.YZ ?
                _state.SliceY / (double)Math.Max(1,_volume.OriginalHeight-1) :
                _state.SliceX / (double)Math.Max(1,_volume.OriginalWidth-1);
            double fy = plane == FVOSlicePlane.XY ?
                _state.SliceY / (double)Math.Max(1,_volume.OriginalHeight-1) :
                _state.SliceZ / (double)Math.Max(1,_volume.OriginalDepth-1);
            Point upperLeft = image.TranslatePoint(new Point(0,0),canvas);
            Point lowerRight = image.TranslatePoint(new Point(image.Width,image.Height),canvas);
            Point cursor = image.TranslatePoint(
                new Point(fx * image.Width, fy * image.Height),canvas);
            SetLine((Line)canvas.Children[0],cursor.X,upperLeft.Y,cursor.X,lowerRight.Y);
            SetLine((Line)canvas.Children[1],upperLeft.X,cursor.Y,lowerRight.X,cursor.Y);
        }

        private void ClearCrosshairs()
        {
            foreach (Canvas canvas in _crosshairs.Values)
                foreach (UIElement line in canvas.Children)
                    line.Visibility = Visibility.Collapsed;
        }

        private static void SetLine(Line line,double x1,double y1,double x2,double y2)
        {
            if (line.Visibility != Visibility.Visible) line.Visibility = Visibility.Visible;
            if (Math.Abs(line.X1-x1) > 0.01) line.X1=x1;
            if (Math.Abs(line.Y1-y1) > 0.01) line.Y1=y1;
            if (Math.Abs(line.X2-x2) > 0.01) line.X2=x2;
            if (Math.Abs(line.Y2-y2) > 0.01) line.Y2=y2;
        }
    }
}
