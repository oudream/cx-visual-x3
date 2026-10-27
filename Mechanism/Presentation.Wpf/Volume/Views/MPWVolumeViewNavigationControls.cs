using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CxVisual.Foundation.Volume;
using CxVisual.Mechanism.Presentation.Wpf.Imaging;
using CxVisual.Mechanism.Visual;

namespace CxVisual.Mechanism.Presentation.Wpf.Volume
{
    /// <summary>
    /// 三维四视图顶部公共导航控件，仅呈现和调用已有Viewer、FVO/MVI。
    /// 无独立三刀、体数据或相机状态。TestCenter与Studio可使用相同实例/API。
    /// </summary>
    public sealed class MPWVolumeSliceNavigationControl : StackPanel
    {
        private readonly MPWImageViewerControl _viewer;
        private readonly Slider _slice;
        private readonly TextBox _index;
        private FVOVolumeData _volume;
        private MVIVolumeViewState _state;
        private bool _syncing;

        public event Action<string> ValidationFailed;

        public MPWVolumeSliceNavigationControl(FVOSlicePlane plane,
            MPWImageViewerControl viewer, Action toggleView)
        {
            if(viewer==null)throw new ArgumentNullException("viewer");
            if(toggleView==null)throw new ArgumentNullException("toggleView");
            if(plane!=FVOSlicePlane.XY&&plane!=FVOSlicePlane.XZ&&plane!=FVOSlicePlane.YZ)
                throw new ArgumentOutOfRangeException("plane");
            Plane=plane;
            _viewer=viewer;
            Orientation=Orientation.Horizontal;
            HorizontalAlignment=HorizontalAlignment.Right;
            Background=new SolidColorBrush(Color.FromRgb(247,250,253));
            AutomationProperties.SetAutomationId(this,"MPW.Volume.Navigation."+plane);
            AddButton(this,"适合","Fit",()=>_viewer.FitToWindow());
            AddButton(this,"1:1","Actual",()=>_viewer.ActualSize());
            AddButton(this,"+","ZoomIn",()=>_viewer.Zoom(1.2));
            AddButton(this,"-","ZoomOut",()=>_viewer.Zoom(1.0/1.2));
            Children.Add(new TextBlock {
                Text="跳转",Margin=new Thickness(7,0,2,0),
                VerticalAlignment=VerticalAlignment.Center,FontSize=10
            });
            _slice=new Slider {
                Minimum=0,Maximum=1,Width=105,
                SmallChange=1,LargeChange=1,TickFrequency=1,IsSnapToTickEnabled=true,
                VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(2,0,4,0)
            };
            AutomationProperties.SetAutomationId(_slice,"MPW.Volume.Navigation."+plane+".Slider");
            _index=new TextBox {
                Width=41,Text="0",VerticalContentAlignment=VerticalAlignment.Center,
                Height=20,Margin=new Thickness(2)
            };
            AutomationProperties.SetAutomationId(_index,"MPW.Volume.Navigation."+plane+".Index");
            _slice.ValueChanged+=(s,e)=>{
                if(_syncing || _volume==null || _state==null)return;
                SetSliceIndex((int)Math.Round(_slice.Value));
            };
            _index.KeyDown+=(s,e)=>{
                if(e.Key!=Key.Enter)return;
                int desired;
                if(!Int32.TryParse(_index.Text,out desired))
                {
                    var invalid=ValidationFailed;
                    if(invalid!=null)invalid("跳转索引必须为整数。");
                }
                else SetSliceIndex(desired);
                e.Handled=true;
            };
            Children.Add(_slice);
            Children.Add(_index);
            AddButton(this,"□","Toggle",toggleView);
            Refresh();
        }

        public FVOSlicePlane Plane { get; private set; }
        public Slider SliceSlider { get { return _slice; } }
        public TextBox SliceIndexBox { get { return _index; } }
        public FVOVolumeData Volume { get { return _volume; } }
        public MVIVolumeViewState ViewState { get { return _state; } }

        public void Bind(FVOVolumeData volume, MVIVolumeViewState state)
        {
            if((volume==null)!=(state==null))
                throw new ArgumentException("体数据与MVI状态应同时绑定/解除。");
            _volume=volume;
            _state=state;
            Refresh();
        }

        /// <summary>读共享MVI索引并刷新界面，不引入自己的导航状态。</summary>
        public void Refresh()
        {
            _syncing=true;
            try
            {
                int maximum=0,position=0;
                if(_volume!=null && _state!=null)
                {
                    maximum=Plane==FVOSlicePlane.XY?_volume.OriginalDepth-1:
                        Plane==FVOSlicePlane.XZ?_volume.OriginalHeight-1:_volume.OriginalWidth-1;
                    position=Plane==FVOSlicePlane.XY?_state.SliceZ:
                        Plane==FVOSlicePlane.XZ?_state.SliceY:_state.SliceX;
                }
                _slice.Maximum=Math.Max(0,maximum);
                _slice.Value=Math.Max(0,Math.Min(maximum,position));
                _index.Text=position.ToString();
                _slice.IsEnabled=_volume!=null;
                _index.IsEnabled=_volume!=null;
            }
            finally{_syncing=false;}
        }

        public void SetSliceIndex(int index)
        {
            if(_volume==null || _state==null)
                throw new InvalidOperationException("必须先绑定体数据。");
            switch(Plane)
            {
                case FVOSlicePlane.XY:_state.SetSliceZ(index,_volume);break;
                case FVOSlicePlane.XZ:_state.SetSliceY(index,_volume);break;
                case FVOSlicePlane.YZ:_state.SetSliceX(index,_volume);break;
                default:throw new ArgumentOutOfRangeException("Plane");
            }
            Refresh();
        }

        internal static Button AddButton(Panel panel,string caption,string action,Action handler)
        {
            if(panel==null)throw new ArgumentNullException("panel");
            if(handler==null)throw new ArgumentNullException("handler");
            var button=new Button {
                Content=caption,MinWidth=26,Padding=new Thickness(2,0,2,0),
                Margin=new Thickness(2,2,0,2),FontSize=11
            };
            AutomationProperties.SetAutomationId(button,"MPW.Volume.Navigation."+action);
            button.Click+=(s,e)=>handler();
            panel.Children.Add(button);
            return button;
        }
    }

    /// <summary>三维相机导航，命令回调复用宿主现有MPW场景/GPU实例，无相机副本。</summary>
    public sealed class MPWVolumeCameraNavigationControl : StackPanel
    {
        public MPWVolumeCameraNavigationControl(Action reset,
            Action<double> zoom,Action<double,double> pan,Action toggleView)
        {
            if(reset==null)throw new ArgumentNullException("reset");
            if(zoom==null)throw new ArgumentNullException("zoom");
            if(pan==null)throw new ArgumentNullException("pan");
            if(toggleView==null)throw new ArgumentNullException("toggleView");
            Orientation=Orientation.Horizontal;
            HorizontalAlignment=HorizontalAlignment.Right;
            Background=new SolidColorBrush(Color.FromRgb(247,250,253));
            AutomationProperties.SetAutomationId(this,"MPW.Volume.Navigation.Camera");
            MPWVolumeSliceNavigationControl.AddButton(this,"复位","Camera.Reset",reset);
            MPWVolumeSliceNavigationControl.AddButton(this,"放大","Camera.ZoomIn",()=>zoom(1));
            MPWVolumeSliceNavigationControl.AddButton(this,"缩小","Camera.ZoomOut",()=>zoom(-1));
            MPWVolumeSliceNavigationControl.AddButton(this,"左移","Camera.PanLeft",()=>pan(-28,0));
            MPWVolumeSliceNavigationControl.AddButton(this,"右移","Camera.PanRight",()=>pan(28,0));
            MPWVolumeSliceNavigationControl.AddButton(this,"上移","Camera.PanUp",()=>pan(0,-28));
            MPWVolumeSliceNavigationControl.AddButton(this,"下移","Camera.PanDown",()=>pan(0,28));
            MPWVolumeSliceNavigationControl.AddButton(this,"□","Camera.Toggle",toggleView);
            ToolTip="GPU三维：左键拖动旋转；右/中键或Shift+左键平移；滚轮缩放；双击复位";
        }
    }
}
