using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using CxVisual.Foundation.Volume;
using CxVisual.Mechanism.Presentation.Wpf.Imaging;
using CxVisual.Mechanism.Visual;

namespace CxVisual.Mechanism.Presentation.Wpf.Volume
{
    /// <summary>
    /// 正式可嵌入四视图工作台。统一装配MPW四槽布局、三正交交互视口和导航条。
    /// 宿主提供现有Viewer与三维场景容器；只引用FVO/MVI状态，不管理原值、算法和GPU生命期。
    /// </summary>
    public sealed class MPWVolumeFourViewWorkbenchControl : Grid
    {
        private readonly Dictionary<FVOSlicePlane, MPWVolumeSliceViewportControl> _viewports =
            new Dictionary<FVOSlicePlane, MPWVolumeSliceViewportControl>();
        private readonly Dictionary<FVOSlicePlane, MPWVolumeSliceNavigationControl> _navigation =
            new Dictionary<FVOSlicePlane, MPWVolumeSliceNavigationControl>();

        public event EventHandler<MPWVolumeVoxelInspectedEventArgs> VoxelInspected;
        public event Action<string> NavigationValidationFailed;

        public MPWVolumeFourViewWorkbenchControl(MPWImageViewerControl xy,
            MPWImageViewerControl yz, MPWImageViewerControl xz, Grid volumeHost,
            Action cameraReset, Action<double> cameraZoom, Action<double,double> cameraPan)
        {
            if(xy==null)throw new ArgumentNullException("xy");
            if(yz==null)throw new ArgumentNullException("yz");
            if(xz==null)throw new ArgumentNullException("xz");
            if(volumeHost==null)throw new ArgumentNullException("volumeHost");
            if(cameraReset==null)throw new ArgumentNullException("cameraReset");
            if(cameraZoom==null)throw new ArgumentNullException("cameraZoom");
            if(cameraPan==null)throw new ArgumentNullException("cameraPan");
            if(Object.ReferenceEquals(xy,yz) || Object.ReferenceEquals(xy,xz) ||
               Object.ReferenceEquals(yz,xz))
                throw new ArgumentException("三张二维图窗必须是不同的真实Viewer实例。");

            AutomationProperties.SetAutomationId(this,"MPW.Volume.FourViewWorkbench");
            Layout=new MPWVolumeFourViewLayoutControl();
            Children.Add(Layout);
            AddSlice(FVOSlicePlane.XY,xy,"XY 主视图",0,0);
            AddSlice(FVOSlicePlane.YZ,yz,"YZ 侧视图",0,1);
            AddSlice(FVOSlicePlane.XZ,xz,"XZ 底视图",1,0);
            Grid cameraSlot=Layout.AddView(volumeHost,"3D / GPU体绘启动中",1,1);
            Border header=cameraSlot.Children[0] as Border;
            CameraTitle=header==null?null:header.Child as TextBlock;
            var cameraNavigation=new MPWVolumeCameraNavigationControl(
                cameraReset,cameraZoom,cameraPan,()=>Layout.ToggleSlot(cameraSlot));
            Grid.SetRow(cameraNavigation,0);
            cameraSlot.Children.Add(cameraNavigation);
        }

        public MPWVolumeFourViewLayoutControl Layout { get; private set; }
        public TextBlock CameraTitle { get; private set; }
        public int SliceViewportCount { get { return _viewports.Count; } }
        public int SliceNavigationCount { get { return _navigation.Count; } }
        public int VisibleCrosshairCount
        {
            get
            {
                int count=0;
                foreach(MPWVolumeSliceViewportControl v in _viewports.Values)
                    count+=v.Overlay.VisibleCrosshairCount;
                return count;
            }
        }
        public MPWVolumeSliceViewportControl GetViewport(FVOSlicePlane plane)
        {
            MPWVolumeSliceViewportControl result;
            if(!_viewports.TryGetValue(plane,out result))
                throw new ArgumentOutOfRangeException("plane");
            return result;
        }
        public MPWVolumeSliceNavigationControl GetNavigation(FVOSlicePlane plane)
        {
            MPWVolumeSliceNavigationControl result;
            if(!_navigation.TryGetValue(plane,out result))
                throw new ArgumentOutOfRangeException("plane");
            return result;
        }

        /// <summary>同时绑定或解绑三正交视口与导航条，原有FVO/MVI所有权仍由宿主持有。</summary>
        public void Bind(FVOVolumeData volume,MVIVolumeViewState state)
        {
            if((volume==null)!=(state==null))
                throw new ArgumentException("体数据与会话状态必须同时绑定或解绑。");
            foreach(MPWVolumeSliceViewportControl viewport in _viewports.Values)
                viewport.Bind(volume,state);
            foreach(MPWVolumeSliceNavigationControl navigation in _navigation.Values)
                navigation.Bind(volume,state);
        }

        public void RefreshNavigations()
        {
            foreach(MPWVolumeSliceNavigationControl nav in _navigation.Values)nav.Refresh();
        }
        public void RefreshCrosshairs()
        {
            foreach(MPWVolumeSliceViewportControl view in _viewports.Values)view.RefreshOverlay();
        }
        public void ClearCrosshairs()
        {
            foreach(MPWVolumeSliceViewportControl view in _viewports.Values)
                view.Overlay.ClearCrosshairs();
        }
        public void UpdateLegends(FVOVolumeData volume,double min,double max,bool pseudo)
        {
            if(volume==null)throw new ArgumentNullException("volume");
            foreach(MPWVolumeSliceViewportControl view in _viewports.Values)
                view.Overlay.UpdateLegend(volume,min,max,pseudo);
        }
        public void SyncVisibleViewports()
        {
            foreach(MPWVolumeSliceViewportControl view in _viewports.Values)
                view.SyncVisibleViewport();
        }

        private void AddSlice(FVOSlicePlane plane,MPWImageViewerControl viewer,
            string title,int row,int column)
        {
            var slice=new MPWVolumeSliceViewportControl(plane,viewer);
            Grid slot=Layout.AddView(slice,title,row,column);
            var nav=new MPWVolumeSliceNavigationControl(plane,viewer,
                ()=>Layout.ToggleSlot(slot));
            Grid.SetRow(nav,0);
            slot.Children.Add(nav);
            slice.VoxelInspected+=(s,e)=>{
                var listener=VoxelInspected;
                if(listener!=null)listener(this,e);
            };
            nav.ValidationFailed+=message=>{
                var listener=NavigationValidationFailed;
                if(listener!=null)listener(message);
            };
            _viewports.Add(plane,slice);
            _navigation.Add(plane,nav);
        }
    }
}
