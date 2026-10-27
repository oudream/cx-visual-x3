using System;
using System.Threading;
using System.IO;
using System.Globalization;
using Microsoft.Win32;
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
        private readonly ComboBox _obliqueInterpolation=new ComboBox { Margin=new Thickness(3),Height=27 };
        private readonly CheckBox _obliqueVisible=new CheckBox { Content="显示任意切面",IsChecked=false };
        private readonly CheckBox _obliqueFollow=new CheckBox { Content="中心跟随三刀",IsChecked=true };
        private readonly Slider _obliqueCenterX=new Slider { Minimum=0,Maximum=1,TickFrequency=1,IsSnapToTickEnabled=true };
        private readonly Slider _obliqueCenterY=new Slider { Minimum=0,Maximum=1,TickFrequency=1,IsSnapToTickEnabled=true };
        private readonly Slider _obliqueCenterZ=new Slider { Minimum=0,Maximum=1,TickFrequency=1,IsSnapToTickEnabled=true };
        private bool _syncingObliqueCenter;
        private readonly Slider _angleX=new Slider { Minimum=-180,Maximum=180,Value=0 };
        private readonly Slider _angleY=new Slider { Minimum=-180,Maximum=180,Value=0 };
        private readonly Slider _angleZ=new Slider { Minimum=-180,Maximum=180,Value=0 };
        private readonly Slider _obliquePixelStep=new Slider { Minimum=.1,Maximum=3,Value=.5 };
        private readonly TextBox _obliqueWidth=new TextBox { Text="128",Width=70,Margin=new Thickness(3) };
        private readonly TextBox _obliqueHeight=new TextBox { Text="128",Width=70,Margin=new Thickness(3) };
        public MPWImageViewerControl ObliqueViewer { get { return _obliqueViewer; } }

        private void BuildObliquePanel(Panel parent)
        {
            Expander expander=new Expander { Header="任意切面 / Oblique MPR",IsExpanded=true,
                Margin=new Thickness(2,12,2,6),Foreground=BrushOf(218,231,244) };
            StackPanel panel=new StackPanel { Margin=new Thickness(3,5,3,8) };
            expander.Content=panel; parent.Children.Add(expander);
            panel.Children.Add(_obliqueVisible);
            panel.Children.Add(_obliqueFollow);
            CxVisual.Mechanism.Presentation.Wpf.Volume.MPWVolumeRenderingParametersControl.AddLabeledSlider(panel,"X中心（原始体素）",_obliqueCenterX);
            CxVisual.Mechanism.Presentation.Wpf.Volume.MPWVolumeRenderingParametersControl.AddLabeledSlider(panel,"Y中心（原始体素）",_obliqueCenterY);
            CxVisual.Mechanism.Presentation.Wpf.Volume.MPWVolumeRenderingParametersControl.AddLabeledSlider(panel,"Z中心（原始体素）",_obliqueCenterZ);
            Button centerReset=new Button { Content="中心跟随当前三刀 / 复位",Height=28,
                Margin=new Thickness(3,5,3,8) };
            centerReset.Click+=(s,e)=>{
                _obliqueFollow.IsChecked=true;
                SyncObliqueCenterControls();
                UpdateObliqueDefinition();
            };
            panel.Children.Add(centerReset);
            CxVisual.Mechanism.Presentation.Wpf.Volume.MPWVolumeRenderingParametersControl.AddLabeledSlider(panel,"X角度",_angleX);
            CxVisual.Mechanism.Presentation.Wpf.Volume.MPWVolumeRenderingParametersControl.AddLabeledSlider(panel,"Y角度",_angleY);
            CxVisual.Mechanism.Presentation.Wpf.Volume.MPWVolumeRenderingParametersControl.AddLabeledSlider(panel,"Z角度",_angleZ);
            CxVisual.Mechanism.Presentation.Wpf.Volume.MPWVolumeRenderingParametersControl.AddLabeledSlider(panel,"像素步长（原始体素）",_obliquePixelStep);
            StackPanel size=new StackPanel { Orientation=Orientation.Horizontal };
            size.Children.Add(new TextBlock { Text="宽/高",VerticalAlignment=VerticalAlignment.Center });
            size.Children.Add(_obliqueWidth); size.Children.Add(_obliqueHeight);
            panel.Children.Add(size);
            panel.Children.Add(new TextBlock { Text="插值方法",Margin=new Thickness(3,8,3,2) });
            _obliqueInterpolation.Items.Add(FVOObliqueInterpolation.Nearest);
            _obliqueInterpolation.Items.Add(FVOObliqueInterpolation.Linear);
            _obliqueInterpolation.SelectedIndex=1;
            panel.Children.Add(_obliqueInterpolation);
            Button apply=new Button { Content="更新任意切",Height=30,Margin=new Thickness(3,5,3,5) };
            apply.Click+=(s,e)=>UpdateObliqueDefinition();
            panel.Children.Add(apply);
            StackPanel exports=new StackPanel { Orientation=Orientation.Horizontal };
            Button png=new Button { Content="保存预览PNG",Padding=new Thickness(7,5,7,5),
                Margin=new Thickness(2) };
            png.Click+=(s,e)=>SaveObliquePreviewDialog();
            Button raw=new Button { Content="生成高清切片 RAW(Float32)",Padding=new Thickness(7,5,7,5),
                Margin=new Thickness(2) };
            raw.Click+=(s,e)=>SaveObliqueRawDialog();
            exports.Children.Add(png);exports.Children.Add(raw);
            panel.Children.Add(exports);
            _obliqueViewer.MinHeight=180;
            _obliqueViewer.Height=225;
            panel.Children.Add(_obliqueViewer);
            _obliqueVisible.Checked+=(s,e)=>UpdateObliqueDefinition();
            _obliqueVisible.Unchecked+=(s,e)=>UpdateObliqueDefinition();
            _obliqueFollow.Checked+=(s,e)=>{SyncObliqueCenterControls();UpdateObliqueDefinition();};
            _obliqueFollow.Unchecked+=(s,e)=>{SyncObliqueCenterControls();UpdateObliqueDefinition();};
            _obliqueCenterX.ValueChanged+=(s,e)=>UpdateObliqueDefinition();
            _obliqueCenterY.ValueChanged+=(s,e)=>UpdateObliqueDefinition();
            _obliqueCenterZ.ValueChanged+=(s,e)=>UpdateObliqueDefinition();
            _angleX.ValueChanged+=(s,e)=>UpdateObliqueDefinition();
            _angleY.ValueChanged+=(s,e)=>UpdateObliqueDefinition();
            _angleZ.ValueChanged+=(s,e)=>UpdateObliqueDefinition();
            _obliquePixelStep.ValueChanged+=(s,e)=>UpdateObliqueDefinition();
            _obliqueInterpolation.SelectionChanged+=(s,e)=>UpdateObliqueDefinition();
        }

        private void UpdateObliqueDefinition()
        {
            if(_state==null||_volume==null||_updating||_syncingObliqueCenter)return;
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
            else
            {
                next.CenterX=_obliqueCenterX.Value;
                next.CenterY=_obliqueCenterY.Value;
                next.CenterZ=_obliqueCenterZ.Value;
            }
            next.AngleXDegrees=_angleX.Value;
            next.AngleYDegrees=_angleY.Value;
            next.AngleZDegrees=_angleZ.Value;
            next.PixelSpacingInVoxels=_obliquePixelStep.Value;
            next.Width=width; next.Height=height;
            next.Interpolation=(FVOObliqueInterpolation)(_obliqueInterpolation.SelectedItem ??
                FVOObliqueInterpolation.Linear);
            _state.SetObliqueSlice(next);
        }

        /// <summary>任意切中心参数只读写MVI ObliqueSlice，不创建第二套持久化状态。</summary>
        private void SyncObliqueCenterControls()
        {
            if(_volume==null||_state==null)return;
            _syncingObliqueCenter=true;
            try
            {
                _obliqueCenterX.Maximum=Math.Max(0,_volume.OriginalWidth-1);
                _obliqueCenterY.Maximum=Math.Max(0,_volume.OriginalHeight-1);
                _obliqueCenterZ.Maximum=Math.Max(0,_volume.OriginalDepth-1);
                bool follow=_obliqueFollow.IsChecked==true;
                _obliqueCenterX.IsEnabled=!follow;
                _obliqueCenterY.IsEnabled=!follow;
                _obliqueCenterZ.IsEnabled=!follow;
                FVOObliqueSliceDefinition definition=_state.ObliqueSlice;
                _obliqueCenterX.Value=follow?_state.SliceX:definition.CenterX;
                _obliqueCenterY.Value=follow?_state.SliceY:definition.CenterY;
                _obliqueCenterZ.Value=follow?_state.SliceZ:definition.CenterZ;
            }
            finally { _syncingObliqueCenter=false; }
        }

        public void SetObliqueCenterForTest(double x,double y,double z)
        {
            if(_volume==null||_state==null)throw new InvalidOperationException("请先加载体数据。");
            _obliqueFollow.IsChecked=false;
            _obliqueCenterX.Value=x;_obliqueCenterY.Value=y;_obliqueCenterZ.Value=z;
            UpdateObliqueDefinition();
        }

        private void SaveObliquePreviewDialog()
        {
            if(_volume==null || _state==null || !_state.ObliqueSlice.Visible)
            {
                MessageBox.Show("请先启用任意切面并生成预览。","导出任意切");
                return;
            }
            SaveFileDialog dialog=new SaveFileDialog { Filter="PNG 图片 (*.png)|*.png",
                FileName="CxVisual-Oblique-Preview.png" };
            if(dialog.ShowDialog()!=true)return;
            try { _obliqueViewer.SavePng(dialog.FileName); }
            catch(Exception error) { MessageBox.Show(error.Message,"PNG导出失败"); }
        }

        private void SaveObliqueRawDialog()
        {
            if(_volume==null || _state==null || !_state.ObliqueSlice.Visible)
            {
                MessageBox.Show("请先启用任意切面。","生成高清切片");
                return;
            }
            SaveFileDialog dialog=new SaveFileDialog {
                Filter="Float32 原始切片 (*.raw)|*.raw",
                FileName="CxVisual-Oblique-Float32.raw" };
            if(dialog.ShowDialog()!=true)return;
            try { SaveHighResolutionObliqueRaw(dialog.FileName); }
            catch(Exception error) { MessageBox.Show(error.Message,"高清切片失败"); }
        }

        /// <summary>保留WPF命令入口，真正的原值导出和格式校验由FVO负责。</summary>
        public void SaveHighResolutionObliqueRaw(string filename)
        {
            if(_volume==null || _state==null)
                throw new InvalidOperationException("没有体数据。");
            UpdateObliqueDefinition();
            FVOObliqueSliceExporter.SaveFloat32Raw(_volume,_state.ObliqueSlice.Clone(),
                filename,CancellationToken.None);
            _status.Text="已保存真实Float32高清切片："+Path.GetFullPath(filename);
        }

        private void RefreshOblique()
        {
            if(_volume==null || _state==null)return;
            if(_obliqueFollow.IsChecked==true)SyncObliqueCenterControls();
            FVOObliqueSliceDefinition data=_state.ObliqueSlice;
            if(!data.Visible)
            {
                _obliqueViewer.Clear();
                _scene.SetObliqueSlice(null,default(Point3D),default(Point3D),
                    default(Point3D),default(Point3D));
                if(_gpu!=null)
                {
                    _gpu.SetObliqueTexture(null,0,0);
                    _gpu.SetObliqueCorners(null);
                }
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
            // 任意切位置/角度调整是连续帧，不能重置用户的缩放和平移。
            if(_obliqueViewer.ActiveImage==null)_obliqueViewer.ShowOriginal(image);
            else _obliqueViewer.UpdateOriginalFrame(image);
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
                if(_gpu.IsAvailable)
                {
                    // 旧RenderingVolumeControl的独立斜切2D纹理：使用唯一FVO数值采样。
                    // GPU只接受灰度数组；真实RAW高位深切片另由BuildScalar导出。
                    FIMImageBuffer gray=FVOObliqueSliceBuilder.BuildPreview(_volume,data,
                        false,0,255,CancellationToken.None);
                    byte[] bgr=gray.CopyPixels();
                    byte[] pixels=new byte[checked(gray.Width*gray.Height)];
                    for(int n=0;n<pixels.Length;n++)pixels[n]=bgr[n*3];
                    _gpu.SetObliqueTexture(pixels,gray.Width,gray.Height);
                }
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
