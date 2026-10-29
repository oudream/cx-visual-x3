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

    /// <summary>统一三维场景可视引用面板；仅操作MVI显隐、选择与透明度，不改专业实体。</summary>
    public sealed class MPWVolumeSceneObjectListControl : StackPanel
    {
        private MVI3DSceneState _scene;
        private readonly StackPanel _rows=new StackPanel();
        private readonly TextBox _layerInput=new TextBox {Text="default",Width=92,
            Margin=new Thickness(1)};
        private readonly TextBox _groupInput=new TextBox {Text="inspection",Width=92,
            Margin=new Thickness(1)};
        private readonly TextBlock _feedback=new TextBlock {TextWrapping=TextWrapping.Wrap};
        public int DisplayedObjectCount {get;private set;}
        public MPWVolumeSceneObjectListControl()
        {
            AutomationProperties.SetAutomationId(this,"MPW.Volume.SceneObjects");
            var editor=new StackPanel {Orientation=Orientation.Horizontal};
            AutomationProperties.SetAutomationId(_layerInput,"MPW.Scene.Layer");
            AutomationProperties.SetAutomationId(_groupInput,"MPW.Scene.Group");
            editor.Children.Add(_layerInput);editor.Children.Add(_groupInput);
            var apply=new Button {Content="分组",Margin=new Thickness(2)};
            AutomationProperties.SetAutomationId(apply,"MPW.Scene.ApplyGroup");
            apply.Click+=(sender,args)=>{
                if(_scene==null||!_scene.SelectedId.HasValue)
                {_feedback.Text="请选择一个可见对象。";return;}
                try
                {
                    _scene.SetOrganization(_scene.SelectedId.Value,
                        _layerInput.Text,_groupInput.Text.Length==0?null:_groupInput.Text);
                    _feedback.Text="";
                }
                catch(ArgumentException ex){_feedback.Text=ex.Message;}
            };
            editor.Children.Add(apply);
            var toggle=new Button {Content="整组显隐",Margin=new Thickness(2)};
            AutomationProperties.SetAutomationId(toggle,"MPW.Scene.ToggleGroup");
            toggle.Click+=(sender,args)=>{
                if(_scene==null||string.IsNullOrWhiteSpace(_groupInput.Text))
                {_feedback.Text="请填写组标识。";return;}
                bool anyVisible=false;
                foreach(MVI3DSceneItem item in _scene.Snapshot())
                    if(item.GroupKey==_groupInput.Text&&item.Visible)anyVisible=true;
                _scene.SetGroupVisibility(_groupInput.Text,!anyVisible);
            };
            editor.Children.Add(toggle);
            var groupOpacity=new Slider {Minimum=0,Maximum=100,Value=100,
                Width=72,Margin=new Thickness(3,0,2,0)};
            AutomationProperties.SetAutomationId(groupOpacity,"MPW.Scene.GroupOpacity");
            ToolTipService.SetToolTip(groupOpacity,"整组透明度（0–100%）");
            Action applyGroupOpacity=()=>{
                if(_scene==null||string.IsNullOrWhiteSpace(_groupInput.Text))return;
                _scene.SetGroupOpacity(_groupInput.Text,groupOpacity.Value/100);
            };
            groupOpacity.LostMouseCapture+=(sender,args)=>applyGroupOpacity();
            groupOpacity.KeyUp+=(sender,args)=>applyGroupOpacity();
            editor.Children.Add(groupOpacity);
            Children.Add(editor);
            Children.Add(_feedback);
            Children.Add(_rows);
        }
        public void Bind(MVI3DSceneState scene)
        {
            if(Object.ReferenceEquals(scene,_scene))return;
            if(_scene!=null)_scene.Changed-=SceneChanged;
            _scene=scene;
            if(_scene!=null)_scene.Changed+=SceneChanged;
            Refresh();
        }
        private void SceneChanged(object sender,EventArgs e)
        {
            if(Dispatcher.CheckAccess())Refresh();
            else Dispatcher.BeginInvoke(new Action(Refresh));
        }
        private void Refresh()
        {
            _rows.Children.Clear();
            DisplayedObjectCount=0;
            if(_scene==null)return;
            foreach(MVI3DSceneItem item in _scene.Snapshot())
            {
                if(DisplayedObjectCount>=256)break;
                DisplayedObjectCount++;
                var row=new StackPanel {Orientation=Orientation.Horizontal,
                    Margin=new Thickness(2)};
                Guid id=item.Id;
                var visible=new CheckBox {IsChecked=item.Visible,
                    VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(1,0,4,0)};
                AutomationProperties.SetAutomationId(visible,"MPW.Scene.Visible."+id.ToString("N"));
                visible.Checked+=(sender,args)=>{if(_scene!=null)_scene.SetVisibility(id,true);};
                visible.Unchecked+=(sender,args)=>{if(_scene!=null)_scene.SetVisibility(id,false);};
                row.Children.Add(visible);
                var select=new Button {
                    Content=item.Kind+" · "+item.LayerKey+
                        (string.IsNullOrEmpty(item.GroupKey)?"":" / "+item.GroupKey),
                    MinWidth=130,MaxWidth=225,Margin=new Thickness(1),Padding=new Thickness(3,1,3,1),
                    IsEnabled=item.Visible,
                    FontWeight=_scene.SelectedId==id?FontWeights.Bold:FontWeights.Normal
                };
                AutomationProperties.SetAutomationId(select,"MPW.Scene.Select."+id.ToString("N"));
                select.Click+=(sender,args)=>{if(_scene!=null)_scene.Select(id);};
                ToolTipService.SetToolTip(select,item.SourceRef);
                row.Children.Add(select);
                var opacity=new Slider {Minimum=0,Maximum=100,Value=item.Opacity*100,
                    Width=80,Margin=new Thickness(5,0,2,0),
                    VerticalAlignment=VerticalAlignment.Center};
                AutomationProperties.SetAutomationId(opacity,"MPW.Scene.Opacity."+id.ToString("N"));
                Action applyOpacity=()=>{
                    MVI3DSceneItem current;
                    if(_scene!=null&&_scene.TryGet(id,out current))
                        _scene.SetOpacity(id,opacity.Value/100);
                };
                opacity.LostMouseCapture+=(sender,args)=>applyOpacity();
                opacity.KeyUp+=(sender,args)=>applyOpacity();
                row.Children.Add(opacity);
                _rows.Children.Add(row);
            }
        }
    }
}
