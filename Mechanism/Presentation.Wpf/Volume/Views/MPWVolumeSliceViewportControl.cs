using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using CxVisual.Foundation.Volume;
using CxVisual.Mechanism.Presentation.Wpf.Imaging;
using CxVisual.Mechanism.Visual;

namespace CxVisual.Mechanism.Presentation.Wpf.Volume
{
    /// <summary>真实原值悬停事件，坐标单位与数值均来源于当前FVO。</summary>
    public sealed class MPWVolumeVoxelInspectedEventArgs : EventArgs
    {
        public MPWVolumeVoxelInspectedEventArgs(int x,int y,int z,float value,
            double physicalX,double physicalY,double physicalZ)
        {
            X=x;Y=y;Z=z;Value=value;
            PhysicalX=physicalX;PhysicalY=physicalY;PhysicalZ=physicalZ;
        }
        public int X { get; private set; }
        public int Y { get; private set; }
        public int Z { get; private set; }
        public float Value { get; private set; }
        public double PhysicalX { get; private set; }
        public double PhysicalY { get; private set; }
        public double PhysicalZ { get; private set; }
    }

    /// <summary>
    /// 可嵌入的正交体切片交互视口：组合已有二维Viewer/三维Overlay，
    /// 鼠标拖动定位、滚轮换层、Ctrl滚轮缩放均修改唯一MVI会话；不持有第二套切片索引。
    /// FVO和MVI由宿主提供并保持所有权，关闭或更换体数据时须解除绑定。
    /// </summary>
    public sealed class MPWVolumeSliceViewportControl : Grid
    {
        private FVOVolumeData _volume;
        private MVIVolumeViewState _state;
        private bool _positioning;
        public event EventHandler<MPWVolumeVoxelInspectedEventArgs> VoxelInspected;

        public MPWVolumeSliceViewportControl(FVOSlicePlane plane,MPWImageViewerControl viewer)
        {
            if(viewer==null)throw new ArgumentNullException("viewer");
            Viewer=viewer;
            Overlay=new MPWVolumeSliceOverlayControl(plane);
            AutomationProperties.SetAutomationId(this,"MPW.Volume.SliceViewport."+plane);
            Children.Add(viewer);
            Children.Add(Overlay);
            SizeChanged+=(s,e)=>RefreshOverlay();
            viewer.LayoutUpdated+=(s,e)=>{RefreshOverlay();SyncVisibleViewport();};
            // 二维Viewer可以先处理平移；仍需同步真实WPF可视范围。
            AddHandler(UIElement.MouseMoveEvent,new MouseEventHandler(
                (s,e)=>SyncVisibleViewport()),true);
            PreviewMouseLeftButtonDown+=OnPointerDown;
            PreviewMouseLeftButtonUp+=OnPointerUp;
            LostMouseCapture+=(s,e)=>_positioning=false;
            PreviewMouseMove+=OnPointerMove;
            PreviewMouseWheel+=(s,e)=>{
                if(HandleWheel(e.Delta,Keyboard.Modifiers))e.Handled=true;
            };
        }

        public MPWImageViewerControl Viewer { get; private set; }
        public MPWVolumeSliceOverlayControl Overlay { get; private set; }
        public FVOSlicePlane Plane { get { return Overlay.Plane; } }
        public FVOVolumeData Volume { get { return _volume; } }
        public MVIVolumeViewState ViewState { get { return _state; } }
        public bool IsPositioning { get { return _positioning; } }

        public void Bind(FVOVolumeData volume,MVIVolumeViewState state)
        {
            if((volume==null)!=(state==null))
                throw new ArgumentException("体数据与会话状态必须同时绑定或解绑。");
            if(IsMouseCaptured)ReleaseMouseCapture();
            _positioning=false;
            _volume=volume;
            _state=state;
            RefreshOverlay();
        }

        public void RefreshOverlay()
        {
            if(_volume==null||_state==null||!_state.ShowCrosshair)
            {
                Overlay.ClearCrosshairs();
                return;
            }
            Overlay.UpdateCrosshair(Viewer,_volume,
                _state.SliceX,_state.SliceY,_state.SliceZ);
        }

        public bool SyncVisibleViewport()
        {
            return _volume!=null&&_state!=null &&
                Overlay.SyncVisibleViewport(Viewer,_state);
        }

        /// <summary>供鼠标操作、自动测试与正式开发者调用；用FVO转换像素，不另存三刀。</summary>
        public bool PositionFromImagePixel(int pixelX,int pixelY)
        {
            if(_volume==null||_state==null||!_state.ShowCrosshair||
                Viewer.ActiveImage==null)return false;
            _state.SetSlicesFromImage(Plane,pixelX,pixelY,_volume);
            return true;
        }

        /// <summary>实际滚轮和开发者API使用同一函数，不依赖测试窗体。</summary>
        public bool HandleWheel(int delta,ModifierKeys modifiers)
        {
            if(_volume==null||_state==null||delta==0)return false;
            if((modifiers&ModifierKeys.Control)!=0)
                Viewer.Zoom(delta>0?1.2:1.0/1.2);
            else
            {
                int step=(modifiers&ModifierKeys.Shift)!=0?5:1;
                _state.ScrollSlice(Plane,delta>0?step:-step,_volume);
            }
            return true;
        }

        private void OnPointerDown(object sender,MouseButtonEventArgs e)
        {
            if((Keyboard.Modifiers&ModifierKeys.Alt)!=0)return; // 沿用MPW图像平移。
            Image image=GetPixelImage();
            if(image==null||image.Source==null||!TryPosition(e,image))return;
            _positioning=true;
            CaptureMouse();
            e.Handled=true;
        }

        private void OnPointerUp(object sender,MouseButtonEventArgs e)
        {
            if(!_positioning)return;
            _positioning=false;
            if(IsMouseCaptured)ReleaseMouseCapture();
            e.Handled=true;
        }

        private void OnPointerMove(object sender,MouseEventArgs e)
        {
            if(_volume==null||_state==null)return;
            Image image=GetPixelImage();
            if(_positioning)
            {
                if(e.LeftButton!=MouseButtonState.Pressed)
                {
                    _positioning=false;
                    if(IsMouseCaptured)ReleaseMouseCapture();
                }
                else if(image!=null&&image.Source!=null)
                {
                    TryPosition(e,image);
                    e.Handled=true;
                }
            }
            if(image==null||image.Source==null)return;
            Point point=e.GetPosition(image);
            if(!IsFinite(point.X)||!IsFinite(point.Y)||
                point.X<0||point.Y<0||point.X>=image.Width||point.Y>=image.Height)return;
            int x,y,z;
            FVOSliceImageBuilder.ImageToVoxel(_volume,
                _state.SliceX,_state.SliceY,_state.SliceZ,Plane,
                (int)Math.Round(point.X),(int)Math.Round(point.Y),out x,out y,out z);
            var handler=VoxelInspected;
            if(handler!=null)handler(this,new MPWVolumeVoxelInspectedEventArgs(x,y,z,
                _volume.GetScalarByOriginalIndex(x,y,z),
                x*_volume.SpacingX,y*_volume.SpacingY,z*_volume.SpacingZ));
        }

        private bool TryPosition(MouseEventArgs e,Image image)
        {
            if(image.Width<=0||image.Height<=0)return false;
            Point at=e.GetPosition(image);
            if(!IsFinite(at.X)||!IsFinite(at.Y))return false;
            int ix=(int)Math.Max(0,Math.Min(image.Width-1,Math.Floor(at.X)));
            int iy=(int)Math.Max(0,Math.Min(image.Height-1,Math.Floor(at.Y)));
            return PositionFromImagePixel(ix,iy);
        }

        private Image GetPixelImage()
        {
            return Viewer.FindName("PixelImage") as Image;
        }
        private static bool IsFinite(double x)
        {
            return !Double.IsNaN(x)&&!Double.IsInfinity(x);
        }
    }
}
