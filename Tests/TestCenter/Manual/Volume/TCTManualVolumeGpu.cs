using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using Microsoft.Win32;
using CxVisual.Foundation.Volume;
using CxVisual.Mechanism.Presentation.Wpf.Volume.Rendering;
using CxVisual.Professional.CT3D;

namespace CxVisual.TestCenter.UI
{
    /// <summary>
    /// 把正式MPW WGL Raymarch作为工作台3D视图的可选真实GPU路径。
    /// OpenGL 3.3不受支持时仍显示经过验证的WPF正交空间切面，且告知具体驱动错误。
    /// </summary>
    public sealed partial class TCTManualVolumeViewerFrame
    {
        private Grid _gpuSlot;
        private MPWVolumeOpenGlControl _gpu;
        private readonly CT3DRenderingSettings _gpuSettings = new CT3DRenderingSettings();
        private readonly CheckBox _enableGpu = new CheckBox { Content = "启用 OpenGL 3.3 真实GPU体绘" };
        private readonly TextBlock _gpuInfo = new TextBlock { Text = "未启用GPU；显示WPF空间切面。" };
        private readonly Slider _gpuOpacity = new Slider { Minimum=0,Maximum=3,Value=1,TickFrequency=.05 };
        private readonly Slider _gpuStep = new Slider { Minimum=.25,Maximum=3,Value=1,TickFrequency=.05 };
        private readonly Slider _gpuAmbient = new Slider { Minimum=0,Maximum=1,Value=.34 };
        private readonly Slider _gpuDiffuse = new Slider { Minimum=0,Maximum=1.5,Value=.62 };
        private readonly Slider _gpuSpecular = new Slider { Minimum=0,Maximum=1.5,Value=.16 };
        private readonly Slider _gpuShine = new Slider { Minimum=1,Maximum=96,Value=24 };
        private readonly CheckBox _gpuJitter = new CheckBox { Content="抖动采样", IsChecked=true };
        private readonly CheckBox _gpuAutoRange = new CheckBox { Content="Slicer 风格自动灰阶", IsChecked=true };
        private readonly CheckBox _gpuColor = new CheckBox { Content="启用自定义颜色曲线", IsChecked=false };
        private readonly CheckBox _gpuScalar = new CheckBox { Content="启用标量透明曲线", IsChecked=false };
        private readonly CheckBox _gpuGradient = new CheckBox { Content="启用梯度透明曲线", IsChecked=false };
        private readonly Slider _gpuThreshold = new Slider { Minimum=0,Maximum=1,Value=.28 };
        private readonly CheckBox _gpuUseThreshold = new CheckBox { Content="应用阈值", IsChecked=false };
        private readonly CheckBox _gpuShowVolume = new CheckBox { Content="显示体绘",IsChecked=true };
        private readonly CheckBox _gpuShowSlices = new CheckBox { Content="显示三维定位切面",IsChecked=true };
        private readonly CheckBox _gpuShowSliceTexture = new CheckBox { Content="显示切面灰度纹理",IsChecked=false };
        private readonly CheckBox _gpuBounds = new CheckBox { Content="显示体包围盒",IsChecked=false };
        private readonly Slider _gpuSliceOpacity = new Slider { Minimum=0,Maximum=1,Value=.35 };
        private readonly ComboBox _gpuPreset = new ComboBox { Margin=new Thickness(4),Height=29 };

        public bool IsGpuVolumeRendering { get { return _gpu != null && _gpu.IsAvailable; } }
        public string GpuStatusText { get { return _gpuInfo.Text; } }

        private void BuildGpuPanel(Panel sidebar)
        {
            _enableGpu.Margin = new Thickness(4,8,4,6);
            sidebar.Children.Add(_enableGpu);
            _gpuInfo.Margin = new Thickness(4,2,4,10);
            _gpuInfo.TextWrapping = TextWrapping.Wrap;
            _gpuInfo.Foreground = Brushes.SteelBlue;
            sidebar.Children.Add(_gpuInfo);
            _enableGpu.Checked += (s,e) => TryEnableGpu();
            _enableGpu.Unchecked += (s,e) => DisableGpu();
            _gpuPreset.Items.Add(CT3DRenderingPresetKind.SlicerLikeGrayscale);
            _gpuPreset.Items.Add(CT3DRenderingPresetKind.LinearGrayscale);
            _gpuPreset.SelectedIndex = 0;
            sidebar.Children.Add(_gpuPreset);
            sidebar.Children.Add(_gpuShowVolume);
            sidebar.Children.Add(_gpuShowSlices);
            sidebar.Children.Add(_gpuShowSliceTexture);
            sidebar.Children.Add(_gpuBounds);
            AddGpuSlider(sidebar,"定位切面透明度",_gpuSliceOpacity);
            Button screenshot = new Button { Content="保存真实GPU帧PNG", Margin=new Thickness(4),Height=32 };
            screenshot.Click += (s,e) => SaveGpuFrame();
            sidebar.Children.Add(screenshot);
            AddGpuSlider(sidebar,"整体体透明度",_gpuOpacity);
            AddGpuSlider(sidebar,"Raymarch采样步长",_gpuStep);
            AddGpuSlider(sidebar,"环境光",_gpuAmbient);
            AddGpuSlider(sidebar,"漫反射",_gpuDiffuse);
            AddGpuSlider(sidebar,"镜面反射",_gpuSpecular);
            AddGpuSlider(sidebar,"高光强度",_gpuShine);
            sidebar.Children.Add(_gpuJitter);
            sidebar.Children.Add(_gpuAutoRange);
            sidebar.Children.Add(_gpuColor);
            sidebar.Children.Add(_gpuScalar);
            sidebar.Children.Add(_gpuGradient);
            sidebar.Children.Add(_gpuUseThreshold);
            AddGpuSlider(sidebar,"阈值下限",_gpuThreshold);
            _gpuOpacity.ValueChanged+=(s,e)=>UpdateGpuSettings();
            _gpuStep.ValueChanged+=(s,e)=>UpdateGpuSettings();
            _gpuAmbient.ValueChanged+=(s,e)=>UpdateGpuSettings();
            _gpuDiffuse.ValueChanged+=(s,e)=>UpdateGpuSettings();
            _gpuSpecular.ValueChanged+=(s,e)=>UpdateGpuSettings();
            _gpuShine.ValueChanged+=(s,e)=>UpdateGpuSettings();
            _gpuJitter.Checked+=(s,e)=>UpdateGpuSettings();
            _gpuJitter.Unchecked+=(s,e)=>UpdateGpuSettings();
            _gpuPreset.SelectionChanged+=(s,e)=>UpdateGpuSettings();
            _gpuAutoRange.Checked+=(s,e)=>UpdateGpuSettings();
            _gpuAutoRange.Unchecked+=(s,e)=>UpdateGpuSettings();
            _gpuColor.Checked+=(s,e)=>UpdateGpuSettings();
            _gpuColor.Unchecked+=(s,e)=>UpdateGpuSettings();
            _gpuScalar.Checked+=(s,e)=>UpdateGpuSettings();
            _gpuScalar.Unchecked+=(s,e)=>UpdateGpuSettings();
            _gpuGradient.Checked+=(s,e)=>UpdateGpuSettings();
            _gpuGradient.Unchecked+=(s,e)=>UpdateGpuSettings();
            _gpuUseThreshold.Checked+=(s,e)=>UpdateGpuSettings();
            _gpuUseThreshold.Unchecked+=(s,e)=>UpdateGpuSettings();
            _gpuThreshold.ValueChanged+=(s,e)=>UpdateGpuSettings();
            _gpuSliceOpacity.ValueChanged+=(s,e)=>UpdateGpuSettings();
            _gpuShowVolume.Checked+=(s,e)=>UpdateGpuSettings();
            _gpuShowVolume.Unchecked+=(s,e)=>UpdateGpuSettings();
            _gpuShowSlices.Checked+=(s,e)=>UpdateGpuSettings();
            _gpuShowSlices.Unchecked+=(s,e)=>UpdateGpuSettings();
            _gpuShowSliceTexture.Checked+=(s,e)=>UpdateGpuSettings();
            _gpuShowSliceTexture.Unchecked+=(s,e)=>UpdateGpuSettings();
            _gpuBounds.Checked+=(s,e)=>UpdateGpuSettings();
            _gpuBounds.Unchecked+=(s,e)=>UpdateGpuSettings();
        }

        private static void AddGpuSlider(Panel panel,string text,Slider slider)
        {
            panel.Children.Add(new TextBlock { Text=text, Margin=new Thickness(4,7,4,1),
                Foreground=BrushOf(55,76,104) });
            slider.Margin=new Thickness(6,0,6,6);
            panel.Children.Add(slider);
        }

        private void UpdateGpuSettings()
        {
            if (_volume == null) return;
            _gpuSettings.Preset=(CT3DRenderingPresetKind)(_gpuPreset.SelectedItem ??
                CT3DRenderingPresetKind.SlicerLikeGrayscale);
            _gpuSettings.VolumeOpacity=(float)_gpuOpacity.Value;
            _gpuSettings.Sampling.StepScale=(float)_gpuStep.Value;
            _gpuSettings.Sampling.EnableJitter=_gpuJitter.IsChecked==true;
            _gpuSettings.Material.Ambient=(float)_gpuAmbient.Value;
            _gpuSettings.Material.Diffuse=(float)_gpuDiffuse.Value;
            _gpuSettings.Material.Specular=(float)_gpuSpecular.Value;
            _gpuSettings.Material.SpecularPower=(float)_gpuShine.Value;
            _gpuSettings.Intensity.AutoRange=_gpuAutoRange.IsChecked==true;
            _gpuSettings.TransferFunctions.UseColor=_gpuColor.IsChecked==true;
            _gpuSettings.TransferFunctions.UseScalarOpacity=_gpuScalar.IsChecked==true;
            _gpuSettings.TransferFunctions.UseGradientOpacity=_gpuGradient.IsChecked==true;
            _gpuSettings.TransferFunctions.UseThreshold=_gpuUseThreshold.IsChecked==true;
            _gpuSettings.TransferFunctions.Threshold=(float)_gpuThreshold.Value;
            _gpuSettings.ShowVolume = _gpuShowVolume.IsChecked==true;
            _gpuSettings.ShowSlices = _gpuShowSlices.IsChecked==true;
            _gpuSettings.ShowSliceTexture = _gpuShowSliceTexture.IsChecked==true;
            _gpuSettings.ShowVolumeBounds = _gpuBounds.IsChecked==true;
            _gpuSettings.SliceOpacity = (float)_gpuSliceOpacity.Value;
            if (_gpu == null || !_gpu.IsAvailable) return;
            try
            {
                _gpu.UpdateTransfer(_gpuSettings.BuildScalarTransferTexture(_volume),
                    _gpuSettings.TransferFunctions.BuildGradientOpacityTexture());
                ApplyGpuUniforms();
            }
            catch(Exception error) { GpuFallback("GPU曲线/参数上传失败："+error.Message); }
        }

        private void ApplyGpuUniforms()
        {
            if (_gpu==null || !_gpu.IsAvailable)return;
            _gpu.SetSceneOptions(_gpuSettings.ShowVolume,_gpuSettings.ShowSlices,
                _gpuSettings.ShowSliceTexture,_gpuSettings.ShowVolumeBounds,_gpuSettings.SliceOpacity);
            UpdateGpuSlicePositions();
            _gpu.SetRendering(_gpuSettings.VolumeOpacity,_gpuSettings.Sampling.StepScale,
                _gpuSettings.Sampling.ReferenceStep,_gpuSettings.Sampling.EnableJitter,
                _gpuSettings.Material.Ambient,_gpuSettings.Material.Diffuse,
                _gpuSettings.Material.Specular,_gpuSettings.Material.SpecularPower);
        }

        public void TryEnableGpu()
        {
            if (_gpu != null || _gpuSlot == null) return;
            if (_volume == null) LoadDemo();
            _gpuSettings.Preset=(CT3DRenderingPresetKind)(_gpuPreset.SelectedItem ??
                CT3DRenderingPresetKind.SlicerLikeGrayscale);
            try
            {
                MPWVolumeOpenGlControl gpu=new MPWVolumeOpenGlControl();
                gpu.AvailabilityChanged+=OnGpuAvailabilityChanged;
                gpu.SetVolume(_volume.Voxels,_volume.Width,_volume.Height,_volume.Depth,
                    _volume.SpacingX,_volume.SpacingY,_volume.SpacingZ,
                    _gpuSettings.BuildScalarTransferTexture(_volume),
                    _gpuSettings.TransferFunctions.BuildGradientOpacityTexture());
                _gpu=gpu;
                _gpuSlot.Children.Add(gpu);
                _scene.Visibility=Visibility.Collapsed;
                _gpuInfo.Text="正在核实显卡 OpenGL 3.3 Core Profile 与旧GLSL...";
            }
            catch(Exception error) { GpuFallback("GPU初始化失败："+error.Message); }
        }

        private void OnGpuAvailabilityChanged(object sender,EventArgs e)
        {
            if (!Object.ReferenceEquals(sender,_gpu))return;
            if (!_gpu.IsAvailable) { GpuFallback(_gpu.FailureReason); return; }
            try
            {
                ApplyGpuUniforms();
                RefreshOblique();
                _gpuInfo.Text="真实GPU体绘已启用："+_gpu.DriverVersion;
                _gpu.RenderFrame();
            }
            catch(Exception error) { GpuFallback("GPU着色失败："+error.Message); }
        }

        private void GpuFallback(string reason)
        {
            DisableGpu();
            _gpuInfo.Text="GPU不可用，已恢复WPF空间切面："+reason;
            if (_enableGpu.IsChecked==true) _enableGpu.IsChecked=false;
        }

        private void DisableGpu()
        {
            if (_gpu != null)
            {
                MPWVolumeOpenGlControl previous=_gpu; _gpu=null;
                previous.AvailabilityChanged-=OnGpuAvailabilityChanged;
                if (_gpuSlot!=null) _gpuSlot.Children.Remove(previous);
                previous.Dispose();
            }
            _scene.Visibility=Visibility.Visible;
        }

        private void UpdateGpuSlicePositions()
        {
            if (_gpu==null || _state==null || _volume==null || !_gpu.IsAvailable)return;
            _gpu.SetSlicePositions(
                _state.SliceX/(float)Math.Max(1,_volume.OriginalWidth-1),
                _state.SliceY/(float)Math.Max(1,_volume.OriginalHeight-1),
                _state.SliceZ/(float)Math.Max(1,_volume.OriginalDepth-1));
        }

        private void SaveGpuFrame()
        {
            if (_gpu==null || !_gpu.IsAvailable)
            {
                MessageBox.Show("当前未启用真实GPU体绘，不能冒用WPF切面截图。","GPU帧截图");
                return;
            }
            SaveFileDialog dialog=new SaveFileDialog { Filter="PNG文件 (*.png)|*.png",
                FileName="CxVisual-GPU-Volume.png" };
            if(dialog.ShowDialog()!=true)return;
            try
            {
                BitmapSource pixels=_gpu.CaptureFrame();
                PngBitmapEncoder encoder=new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(pixels));
                using(FileStream file=File.Create(dialog.FileName))encoder.Save(file);
            }
            catch(Exception error) { MessageBox.Show(error.Message,"GPU帧截图失败"); }
        }

        private void UpdateGpuVolume()
        {
            if (_gpu==null || _volume==null)return;
            try
            {
                _gpu.SetVolume(_volume.Voxels,_volume.Width,_volume.Height,_volume.Depth,
                    _volume.SpacingX,_volume.SpacingY,_volume.SpacingZ,
                    _gpuSettings.BuildScalarTransferTexture(_volume),
                    _gpuSettings.TransferFunctions.BuildGradientOpacityTexture());
                ApplyGpuUniforms();
            }
            catch(Exception error) { GpuFallback("GPU体数据更新失败："+error.Message); }
        }
    }
}
