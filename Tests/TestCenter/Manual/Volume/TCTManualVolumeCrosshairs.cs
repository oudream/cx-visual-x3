using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using CxVisual.Foundation.Volume;
using CxVisual.Mechanism.Presentation.Wpf.Imaging;
using CxVisual.Mechanism.Presentation.Wpf.Volume;

namespace CxVisual.TestCenter.UI
{
    /// <summary>
    /// 三维工作台的真实十字线与操作：投影位置直接使用MPW图像的WPF变换，
    /// 旧HALCON行为：左键拖动定位三刀、滚轮换层、Ctrl+滚轮缩放；Alt+左键保留MPW平移。
    /// </summary>
    public sealed partial class TCTManualVolumeViewerFrame
    {
        private readonly Dictionary<FVOSlicePlane, MPWVolumeSliceOverlayControl> _crosshairs =
            new Dictionary<FVOSlicePlane, MPWVolumeSliceOverlayControl>();

        public int SliceLegendCount { get { return _crosshairs.Count; } }

        public int VisibleCrosshairCount
        {
            get
            {
                int count = 0;
                foreach (MPWVolumeSliceOverlayControl layer in _crosshairs.Values)
                    count += layer.VisibleCrosshairCount;
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
            MPWVolumeSliceOverlayControl layer = new MPWVolumeSliceOverlayControl(plane);
            content.Children.Add(layer);
            _crosshairs.Add(plane, layer);
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

        /// <summary>图例由正式MPW公共叠加控件绘制；测试宿主只传入FVO与显示参数。</summary>
        private void UpdateSliceLegends()
        {
            if(_volume==null)return;
            foreach(MPWVolumeSliceOverlayControl layer in _crosshairs.Values)
                layer.UpdateLegend(_volume,_windowMin.Value,_windowMax.Value,
                    _pseudo.IsChecked==true);
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
            foreach (KeyValuePair<FVOSlicePlane,MPWVolumeSliceOverlayControl> pair in _crosshairs)
            {
                MPWImageViewerControl viewer = pair.Key == FVOSlicePlane.XY ? _xy :
                    pair.Key == FVOSlicePlane.XZ ? _xz : _yz;
                UpdateCrosshair(viewer,pair.Key,pair.Value);
            }
        }

        private void UpdateCrosshair(MPWImageViewerControl viewer, FVOSlicePlane plane,
            MPWVolumeSliceOverlayControl layer)
        {
            if (_volume == null || _state == null || viewer.ActiveImage == null)
                return;
            layer.UpdateCrosshair(viewer,_volume,_state.SliceX,_state.SliceY,_state.SliceZ);
        }

        private void ClearCrosshairs()
        {
            foreach (MPWVolumeSliceOverlayControl layer in _crosshairs.Values)
                layer.ClearCrosshairs();
        }
    }
}
