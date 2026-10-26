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
    /// 旧HALCON行为：左键拖动定位三刀、滚轮换层、Ctrl+滚轮缩放；Alt+左键保留MPW平移。
    /// </summary>
    public sealed partial class TCTManualVolumeViewerFrame
    {
        private readonly Dictionary<FVOSlicePlane, Canvas> _crosshairs =
            new Dictionary<FVOSlicePlane, Canvas>();
        private readonly Dictionary<FVOSlicePlane, Border> _sliceLegends =
            new Dictionary<FVOSlicePlane, Border>();

        public int SliceLegendCount { get { return _sliceLegends.Count; } }

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
            Border legend=new Border {
                Background=new SolidColorBrush(Color.FromArgb(190,7,15,26)),
                HorizontalAlignment=HorizontalAlignment.Right,
                VerticalAlignment=VerticalAlignment.Bottom,
                Margin=new Thickness(7),Width=160,Padding=new Thickness(5,3,5,3),
                IsHitTestVisible=false };
            content.Children.Add(legend);
            _sliceLegends.Add(plane,legend);
            content.SizeChanged += (s,e) => UpdateCrosshairs();
            viewer.LayoutUpdated += (s,e) =>
            {
                UpdateCrosshair(viewer,plane,layer);
                UpdateSliceViewportFromViewer(viewer,plane);
            };
            // MPW的平移事件已被其自身处理，handledEventsToo保证投影仍能同步。
            content.AddHandler(UIElement.MouseMoveEvent,new MouseEventHandler((s,e)=>
                UpdateSliceViewportFromViewer(viewer,plane)),true);
            bool positioning=false;
            content.PreviewMouseLeftButtonDown += (s,e) =>
            {
                if (_volume==null || _state==null || !_state.ShowCrosshair ||
                    _crosshairEnabled.IsChecked!=true ||
                    (Keyboard.Modifiers & ModifierKeys.Alt)!=0) return;
                Image pixel=viewer.FindName("PixelImage") as Image;
                if(pixel==null || pixel.Source==null || !TryPositionFromMouse(e,pixel,plane))
                    return;
                positioning=true;
                content.CaptureMouse();
                e.Handled=true;
            };
            content.PreviewMouseLeftButtonUp += (s,e) =>
            {
                if(!positioning)return;
                positioning=false;
                if(content.IsMouseCaptured)content.ReleaseMouseCapture();
                e.Handled=true;
            };
            content.LostMouseCapture += (s,e) => { positioning=false; };
            content.PreviewMouseMove += (s,e) =>
            {
                if(_volume==null || _state==null)return;
                Image image=viewer.FindName("PixelImage") as Image;
                if(positioning)
                {
                    if(e.LeftButton!=MouseButtonState.Pressed)
                    {
                        positioning=false;
                        if(content.IsMouseCaptured)content.ReleaseMouseCapture();
                    }
                    else if(image!=null && image.Source!=null)
                    {
                        TryPositionFromMouse(e,image,plane);
                        e.Handled=true;
                    }
                }
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
                if (_volume==null || _state==null)return;
                if((Keyboard.Modifiers & ModifierKeys.Control)!=0)
                    viewer.Zoom(e.Delta>0?1.2:1.0/1.2);
                else
                {
                    int step=(Keyboard.Modifiers & ModifierKeys.Shift)!=0?5:1;
                    ScrollSlice(plane,e.Delta>0?step:-step);
                }
                e.Handled=true;
            };
        }

        /// <summary>通过WPF实际图像变换反算可见区，写入已有MVI视口；允许超出体外框。</summary>
        private void UpdateSliceViewportFromViewer(MPWImageViewerControl viewer,FVOSlicePlane plane)
        {
            if(_volume==null || _state==null || _updating)return;
            Image image=viewer.FindName("PixelImage") as Image;
            FrameworkElement viewport=viewer.FindName("ViewportRoot") as FrameworkElement;
            if(image==null || image.Source==null || viewport==null ||
                image.Width<=0 || image.Height<=0 ||
                viewport.ActualWidth<=1 || viewport.ActualHeight<=1)return;
            Point a=viewport.TranslatePoint(new Point(0,0),image);
            Point b=viewport.TranslatePoint(new Point(viewport.ActualWidth,
                viewport.ActualHeight),image);
            if(Double.IsNaN(a.X) || Double.IsNaN(a.Y) ||
               Double.IsNaN(b.X) || Double.IsNaN(b.Y))return;
            const double limit=8.0;
            float minA=(float)Math.Max(-limit,Math.Min(limit,a.X/image.Width));
            float maxA=(float)Math.Max(-limit,Math.Min(limit,b.X/image.Width));
            float minB=(float)Math.Max(-limit,Math.Min(limit,a.Y/image.Height));
            float maxB=(float)Math.Max(-limit,Math.Min(limit,b.Y/image.Height));
            _state.SetSliceViewport(plane,minA,maxA,minB,maxB);
        }

        public void UpdateAllSliceViewportRanges()
        {
            UpdateSliceViewportFromViewer(_xy,FVOSlicePlane.XY);
            UpdateSliceViewportFromViewer(_xz,FVOSlicePlane.XZ);
            UpdateSliceViewportFromViewer(_yz,FVOSlicePlane.YZ);
        }

        private bool TryPositionFromMouse(MouseEventArgs args,Image image,FVOSlicePlane plane)
        {
            if(_volume==null || _state==null || image==null || image.Source==null ||
                image.Width<=0 || image.Height<=0)return false;
            Point location=args.GetPosition(image);
            if(Double.IsNaN(location.X)||Double.IsNaN(location.Y))return false;
            int x=(int)Math.Max(0,Math.Min(image.Width-1,Math.Floor(location.X)));
            int y=(int)Math.Max(0,Math.Min(image.Height-1,Math.Floor(location.Y)));
            SetCrosshairFromImage(plane,x,y);
            return true;
        }

        /// <summary>复用唯一MVI切片索引，普通滚轮换层、Ctrl+滚轮由MPW缩放。</summary>
        public void ScrollSlice(FVOSlicePlane plane,int steps)
        {
            if(_volume==null || _state==null || steps==0)return;
            if(plane==FVOSlicePlane.XY)_state.SetSliceZ(_state.SliceZ+steps,_volume);
            else if(plane==FVOSlicePlane.XZ)_state.SetSliceY(_state.SliceY+steps,_volume);
            else if(plane==FVOSlicePlane.YZ)_state.SetSliceX(_state.SliceX+steps,_volume);
            else throw new ArgumentOutOfRangeException("plane");
        }

        /// <summary>旧HALCON 2D色标；伪彩映射直接复用FVO唯一数值函数，不复制调色算法。</summary>
        private void UpdateSliceLegends()
        {
            if(_volume==null)return;
            foreach(Border border in _sliceLegends.Values)
            {
                StackPanel stack=new StackPanel();
                TextBlock title=new TextBlock {
                    Text=String.Format("窗 {0:0}..{1:0} / 原值 {2:0.##}..{3:0.##}",
                        _windowMin.Value,_windowMax.Value,_volume.ScalarMin,_volume.ScalarMax),
                    Foreground=Brushes.White,FontSize=9 };
                stack.Children.Add(title);
                LinearGradientBrush brush=new LinearGradientBrush {
                    StartPoint=new Point(0,0),EndPoint=new Point(1,0) };
                for(int i=0;i<=8;i++)
                {
                    int value=i==8?255:i*32;
                    Color color;
                    if(_pseudo.IsChecked==true)
                    {
                        byte[] bgr=new byte[3];
                        FVOSliceImageBuilder.WritePseudoBgr(bgr,0,(byte)value);
                        color=Color.FromRgb(bgr[2],bgr[1],bgr[0]);
                    }
                    else color=Color.FromRgb((byte)value,(byte)value,(byte)value);
                    brush.GradientStops.Add(new GradientStop(color,i/8.0));
                }
                Border ramp=new Border {
                    Background=brush,Height=10,Margin=new Thickness(0,4,0,0),
                    BorderBrush=Brushes.LightSlateGray,BorderThickness=new Thickness(.5) };
                stack.Children.Add(ramp);
                border.Child=stack;
            }
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
            // 非等距Spacing和降采样都会改变切片图像长宽，禁止按原始索引比例估算。
            int ix,iy;
            FVOSliceImageBuilder.VoxelToImage(_volume,plane,
                _state.SliceX,_state.SliceY,_state.SliceZ,out ix,out iy);
            Point upperLeft = image.TranslatePoint(new Point(0,0),canvas);
            Point lowerRight = image.TranslatePoint(new Point(image.Width,image.Height),canvas);
            Point cursor = image.TranslatePoint(new Point(ix+.5,iy+.5),canvas);
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
