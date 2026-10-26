using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using CxVisual.Foundation.Imaging;
using CxVisual.Foundation.Volume;
using CxVisual.Mechanism.Presentation.Wpf.Imaging;

namespace CxVisual.TestCenter.UI
{
    /// <summary>原MPR任意切：FVO原始空间定义与数值采样、MVI唯一斜切状态、WPF/GPU共享四角。</summary>
    public sealed partial class TCTManualVolumeViewerFrame
    {
        private readonly MPWImageViewerControl _obliqueViewer=new MPWImageViewerControl();
        private readonly CheckBox _obliqueVisible=new CheckBox { Content="显示任意切面",IsChecked=false };
        private readonly CheckBox _obliqueFollow=new CheckBox { Content="中心跟随三刀",IsChecked=true };
        private readonly Slider _angleX=new Slider { Minimum=-180,Maximum=180,Value=0 };
        private readonly Slider _angleY=new Slider { Minimum=-180,Maximum=180,Value=0 };
        private readonly Slider _angleZ=new Slider { Minimum=-180,Maximum=180,Value=0 };
        private readonly Slider _obliquePixelStep=new Slider { Minimum=.1,Maximum=3,Value=.5 };
        private readonly TextBox _obliqueWidth=new TextBox { Text="128",Width=70,Margin=new Thickness(3) };
        private readonly TextBox _obliqueHeight=new TextBox { Text="128",Width=70,Margin=new Thickness(3) };
        public MPWImageViewerControl ObliqueViewer { get { return _obliqueViewer; } }

        private void BuildObliquePanel(Panel parent)
        {
            Expander expander=new Expander { Header="任意切面 / Oblique MPR",IsExpanded=false,
                Margin=new Thickness(2,12,2,6),Foreground=BrushOf(38,65,94) };
            StackPanel panel=new StackPanel { Margin=new Thickness(3,5,3,8) };
            expander.Content=panel; parent.Children.Add(expander);
            panel.Children.Add(_obliqueVisible);
            panel.Children.Add(_obliqueFollow);
            AddGpuSlider(panel,"X角度",_angleX);
            AddGpuSlider(panel,"Y角度",_angleY);
            AddGpuSlider(panel,"Z角度",_angleZ);
            AddGpuSlider(panel,"像素步长（原始体素）",_obliquePixelStep);
            StackPanel size=new StackPanel { Orientation=Orientation.Horizontal };
            size.Children.Add(new TextBlock { Text="宽/高",VerticalAlignment=VerticalAlignment.Center });
            size.Children.Add(_obliqueWidth); size.Children.Add(_obliqueHeight);
            panel.Children.Add(size);
            Button apply=new Button { Content="更新任意切",Height=30,Margin=new Thickness(3,5,3,5) };
            apply.Click+=(s,e)=>UpdateObliqueDefinition();
            panel.Children.Add(apply);
            _obliqueViewer.MinHeight=180;
            _obliqueViewer.Height=225;
            panel.Children.Add(_obliqueViewer);
            _obliqueVisible.Checked+=(s,e)=>UpdateObliqueDefinition();
            _obliqueVisible.Unchecked+=(s,e)=>UpdateObliqueDefinition();
            _obliqueFollow.Checked+=(s,e)=>UpdateObliqueDefinition();
            _obliqueFollow.Unchecked+=(s,e)=>UpdateObliqueDefinition();
            _angleX.ValueChanged+=(s,e)=>UpdateObliqueDefinition();
            _angleY.ValueChanged+=(s,e)=>UpdateObliqueDefinition();
            _angleZ.ValueChanged+=(s,e)=>UpdateObliqueDefinition();
            _obliquePixelStep.ValueChanged+=(s,e)=>UpdateObliqueDefinition();
        }

        private void UpdateObliqueDefinition()
        {
            if(_state==null||_volume==null||_updating)return;
            int width,height;
            if(!Int32.TryParse(_obliqueWidth.Text,out width) ||
               !Int32.TryParse(_obliqueHeight.Text,out height) ||
               width<1 || height<1 || width>2048 || height>2048)
            {
                _gpuInfo.Text="任意切宽高必须为1..2048。";
                return;
            }
            FVOObliqueSliceDefinition previous=_state.ObliqueSlice;
            FVOObliqueSliceDefinition next=previous.Clone();
            next.Visible=_obliqueVisible.IsChecked==true;
            if(_obliqueFollow.IsChecked==true)
            {
                next.CenterX=_state.SliceX; next.CenterY=_state.SliceY; next.CenterZ=_state.SliceZ;
            }
            next.AngleXDegrees=_angleX.Value;
            next.AngleYDegrees=_angleY.Value;
            next.AngleZDegrees=_angleZ.Value;
            next.PixelSpacingInVoxels=_obliquePixelStep.Value;
            next.Width=width; next.Height=height;
            _state.SetObliqueSlice(next);
        }

        private void RefreshOblique()
        {
            if(_volume==null || _state==null)return;
            FVOObliqueSliceDefinition data=_state.ObliqueSlice;
            if(!data.Visible)
            {
                _obliqueViewer.Clear();
                _scene.SetObliqueSlice(null,default(Point3D),default(Point3D),
                    default(Point3D),default(Point3D));
                if(_gpu!=null)_gpu.SetObliqueCorners(null);
                return;
            }
            if(_obliqueFollow.IsChecked==true &&
                (data.CenterX!=_state.SliceX || data.CenterY!=_state.SliceY || data.CenterZ!=_state.SliceZ))
            {
                data=data.Clone();
                data.CenterX=_state.SliceX;data.CenterY=_state.SliceY;data.CenterZ=_state.SliceZ;
                // 三刀本身已属于单一MVI状态，此处只更新预览与3D相交位置。
            }
            FIMImageBuffer image=FVOObliqueSliceBuilder.BuildPreview(_volume,data,
                _pseudo.IsChecked==true,(byte)Math.Round(_windowMin.Value),
                (byte)Math.Round(_windowMax.Value),CancellationToken.None);
            _obliqueViewer.ShowOriginal(image);
            FVOObliqueSliceVector u,v;
            FVOObliqueSliceGeometry.GetAxes(data,out u,out v);
            FVOObliqueSliceVector p00=FVOObliqueSliceGeometry.GetCorner(data,u,v,-1,-1);
            FVOObliqueSliceVector p10=FVOObliqueSliceGeometry.GetCorner(data,u,v,1,-1);
            FVOObliqueSliceVector p11=FVOObliqueSliceGeometry.GetCorner(data,u,v,1,1);
            FVOObliqueSliceVector p01=FVOObliqueSliceGeometry.GetCorner(data,u,v,-1,1);
            _scene.SetObliqueSlice(image,ToWorld(p00),ToWorld(p10),ToWorld(p11),ToWorld(p01));
            if(_gpu!=null)
            {
                float[] corners={
                    Tex(p00.X,_volume.OriginalWidth),Tex(p00.Y,_volume.OriginalHeight),Tex(p00.Z,_volume.OriginalDepth),
                    Tex(p10.X,_volume.OriginalWidth),Tex(p10.Y,_volume.OriginalHeight),Tex(p10.Z,_volume.OriginalDepth),
                    Tex(p11.X,_volume.OriginalWidth),Tex(p11.Y,_volume.OriginalHeight),Tex(p11.Z,_volume.OriginalDepth),
                    Tex(p01.X,_volume.OriginalWidth),Tex(p01.Y,_volume.OriginalHeight),Tex(p01.Z,_volume.OriginalDepth)
                };
                _gpu.SetObliqueCorners(corners);
            }
        }

        private static float Tex(double voxel,int count)
        { return (float)(voxel/Math.Max(1,count-1)); }

        private Point3D ToWorld(FVOObliqueSliceVector p)
        {
            double sx=_volume.OriginalWidth*_volume.SpacingX,
                sy=_volume.OriginalHeight*_volume.SpacingY,
                sz=_volume.OriginalDepth*_volume.SpacingZ;
            double scale=Math.Max(sx,Math.Max(sy,sz));
            return new Point3D(
                (p.X/Math.Max(1,_volume.OriginalWidth-1)-.5)*sx/scale,
                (.5-p.Y/Math.Max(1,_volume.OriginalHeight-1))*sy/scale,
                (p.Z/Math.Max(1,_volume.OriginalDepth-1)-.5)*sz/scale);
        }

        public void SetObliqueForTest(bool visible,double xAngle,double yAngle,double zAngle)
        {
            _obliqueVisible.IsChecked=visible;
            _angleX.Value=xAngle;_angleY.Value=yAngle;_angleZ.Value=zAngle;
            UpdateObliqueDefinition();
        }
    }
}
