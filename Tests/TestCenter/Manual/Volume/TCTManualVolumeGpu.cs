using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using Microsoft.Win32;
using CxVisual.Foundation.Volume;
using CxVisual.Mechanism.Presentation.Wpf.Volume.Rendering;
using CxVisual.Mechanism.Presentation.Wpf.Volume;
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
        private bool _gpuTransferDirty=true;
        private readonly MPWVolumeRenderingParametersControl _gpuPanel =
            new MPWVolumeRenderingParametersControl();
        // 便于在真实GPU压力验收里证明仅曲线/预设变化才重新生成LUT。
        public int GpuTransferUploadCount { get; private set; }
        private CheckBox _enableGpu { get { return _gpuPanel.EnableGpu; } }
        private TextBlock _gpuInfo { get { return _gpuPanel.StatusText; } }
        private TextBlock _gpuModeHeader;
        private void SetGpuModeLabel(string text,string detail)
        {
            if(_gpuModeHeader==null)return;
            _gpuModeHeader.Text=text;
            _gpuModeHeader.ToolTip=detail;
        }
        private Slider _gpuOpacity { get { return _gpuPanel.VolumeOpacity; } }
        private Slider _gpuStep { get { return _gpuPanel.SamplingStep; } }
        private Slider _gpuAmbient { get { return _gpuPanel.Ambient; } }
        private Slider _gpuDiffuse { get { return _gpuPanel.Diffuse; } }
        private Slider _gpuSpecular { get { return _gpuPanel.Specular; } }
        private Slider _gpuShine { get { return _gpuPanel.SpecularPower; } }
        private CheckBox _gpuJitter { get { return _gpuPanel.Jitter; } }
        private CheckBox _gpuAutoRange { get { return _gpuPanel.AutoRange; } }
        private CheckBox _gpuColor { get { return _gpuPanel.UseColor; } }
        private CheckBox _gpuScalar { get { return _gpuPanel.UseScalar; } }
        private CheckBox _gpuGradient { get { return _gpuPanel.UseGradient; } }
        private Slider _gpuThreshold { get { return _gpuPanel.Threshold; } }
        private CheckBox _gpuUseThreshold { get { return _gpuPanel.UseThreshold; } }
        private CheckBox _gpuShowVolume { get { return _gpuPanel.ShowVolume; } }
        private CheckBox _gpuShowSlices { get { return _gpuPanel.ShowSlices; } }
        private CheckBox _gpuShowSliceTexture { get { return _gpuPanel.ShowSliceTexture; } }
        private CheckBox _gpuBounds { get { return _gpuPanel.ShowBounds; } }
        private Slider _gpuSliceOpacity { get { return _gpuPanel.SliceOpacity; } }
        private ComboBox _gpuPreset { get { return _gpuPanel.Preset; } }

        public bool HasAttemptedGpuRendering { get; private set; }
        public MPWVolumeOpenGlControl GpuControlForHardwareTest { get { return _gpu; } }
        public void SetOpacityFromPanelForTest(double opacity) { _gpuOpacity.Value=opacity; }
        public void SetSliceVisibilityFromPanelForTest(bool enabled) { _gpuShowSlices.IsChecked=enabled; }
        public void SetColorCurveEnabledFromPanelForTest(bool enabled) { _gpuColor.IsChecked=enabled; }
        public float ConfiguredOpacityForTest { get { return _gpuSettings.VolumeOpacity; } }
        public bool ConfiguredSliceVisibilityForTest { get { return _gpuSettings.ShowSlices; } }
        public bool ConfiguredColorForTest { get { return _gpuSettings.TransferFunctions.UseColor; } }
        public bool IsGpuOrientationVisibleForTest
        {
            get { return _gpu!=null && _gpu.IsOrientationVisible; }
        }
        public bool IsGpuVolumeRendering { get { return _gpu != null && _gpu.IsAvailable; } }
        public string GpuStatusText { get { return _gpuInfo.Text; } }
        public BitmapSource CaptureGpuFrameForTest()
        {
            if(_gpu==null || !_gpu.IsAvailable)
                throw new InvalidOperationException("GPU驱动未验证，禁止使用WPF截图代替。");
            return _gpu.CaptureFrame();
        }

        /// <summary>TestCenter只注入专业GPU行为；MPW拥有所有渲染输入控件与事件。</summary>
        private void BuildGpuPanel(Panel sidebar)
        {
            if(sidebar==null)throw new ArgumentNullException("sidebar");
            _gpuPreset.Items.Add(CT3DRenderingPresetKind.SlicerLikeGrayscale);
            _gpuPreset.Items.Add(CT3DRenderingPresetKind.LinearGrayscale);
            _gpuPreset.SelectedIndex=0;
            _gpuPanel.GpuEnabledChanged+=enabled=>{
                if(enabled)TryEnableGpu();
                else DisableGpu();
            };
            _gpuPanel.SaveFrameRequested+=()=>SaveGpuFrame();
            // GPU参数面板只报告“发生输入变化”；是否失效LUT由专业CT3D判定。
            // FVO控制点编辑器仍通过UpdateGpuSettings(true)明确通知原始曲线变化。
            _gpuPanel.OptionsChanged+=transferChanged=>UpdateGpuSettings();
            sidebar.Children.Add(_gpuPanel);
        }

        private void UpdateGpuSettings(bool transferChanged=false)
        {
            if(transferChanged)_gpuTransferDirty=true;
            if (_volume == null) return;
            // 仅桥接现有MPW控件的原始输入；校验、写入、LUT失效均由CT3D负责。
            bool lutChanged=_gpuSettings.ApplyParameters(new CT3DRenderingParameterValues {
                Preset=(CT3DRenderingPresetKind)(_gpuPreset.SelectedItem ??
                    CT3DRenderingPresetKind.SlicerLikeGrayscale),
                VolumeOpacity=(float)_gpuOpacity.Value,
                SamplingStepScale=(float)_gpuStep.Value,
                SamplingJitter=_gpuJitter.IsChecked==true,
                Ambient=(float)_gpuAmbient.Value,
                Diffuse=(float)_gpuDiffuse.Value,
                Specular=(float)_gpuSpecular.Value,
                SpecularPower=(float)_gpuShine.Value,
                AutoRange=_gpuAutoRange.IsChecked==true,
                UseColor=_gpuColor.IsChecked==true,
                UseScalarOpacity=_gpuScalar.IsChecked==true,
                UseGradientOpacity=_gpuGradient.IsChecked==true,
                UseThreshold=_gpuUseThreshold.IsChecked==true,
                Threshold=(float)_gpuThreshold.Value,
                ShowVolume=_gpuShowVolume.IsChecked==true,
                ShowSlices=_gpuShowSlices.IsChecked==true,
                ShowSliceTexture=_gpuShowSliceTexture.IsChecked==true,
                ShowVolumeBounds=_gpuBounds.IsChecked==true,
                SliceOpacity=(float)_gpuSliceOpacity.Value
            });
            if(lutChanged)_gpuTransferDirty=true;
            if (_gpu == null || !_gpu.IsAvailable) return;
            try
            {
                if(_gpuTransferDirty)
                {
                    _gpu.UpdateTransfer(_gpuSettings.BuildScalarTransferTexture(_volume),
                        _gpuSettings.TransferFunctions.BuildGradientOpacityTexture());
                    _gpuTransferDirty=false;
                    GpuTransferUploadCount++;
                }
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
            HasAttemptedGpuRendering=true;
            if (_volume == null) LoadDemo();
            _gpuSettings.Preset=(CT3DRenderingPresetKind)(_gpuPreset.SelectedItem ??
                CT3DRenderingPresetKind.SlicerLikeGrayscale);
            try
            {
                MPWVolumeOpenGlControl gpu=new MPWVolumeOpenGlControl();
                if(_state!=null)gpu.BindCamera(_state.Camera);
                gpu.AvailabilityChanged+=OnGpuAvailabilityChanged;
                gpu.SetVolume(_volume.Voxels,_volume.Width,_volume.Height,_volume.Depth,
                    _volume.SpacingX,_volume.SpacingY,_volume.SpacingZ,
                    _gpuSettings.BuildScalarTransferTexture(_volume),
                    _gpuSettings.TransferFunctions.BuildGradientOpacityTexture());
                _gpuTransferDirty=false;
                _gpu=gpu;
                _gpuInfo.Text="正在核实显卡 OpenGL 3.3 Core Profile 与旧GLSL...";
                SetGpuModeLabel("3D / GPU体绘启动中","正在检测OpenGL 3.3与GLSL");
                _gpuSlot.Children.Add(gpu);
                _scene.Visibility=Visibility.Collapsed;
            }
            catch(Exception error) { GpuFallback("GPU初始化失败："+error.Message); }
        }

        private void OnGpuAvailabilityChanged(object sender,EventArgs e)
        {
            if (!Object.ReferenceEquals(sender,_gpu))return;
            if (!_gpu.IsAvailable) { GpuFallback(_gpu.FailureReason); return; }
            try
            {
                _gpu.SetOrientationVisible(_orientationEnabled.IsChecked==true);
                ApplyGpuUniforms();
                RefreshOblique();
                _gpuInfo.Text="真实GPU体绘已启用："+_gpu.DriverVersion;
                SetGpuModeLabel("3D / OpenGL GPU体绘",_gpuInfo.Text);
                _gpu.RenderFrame();
            }
            catch(Exception error) { GpuFallback("GPU着色失败："+error.Message); }
        }

        private void GpuFallback(string reason)
        {
            DisableGpu();
            _gpuInfo.Text="GPU不可用，已恢复WPF空间切面："+reason;
            SetGpuModeLabel("3D / WPF切面（GPU失败）",_gpuInfo.Text);
            if (_enableGpu.IsChecked==true) _enableGpu.IsChecked=false;
        }

        private void DisableGpu()
        {
            if (_gpu != null)
            {
                MPWVolumeOpenGlControl previous=_gpu; _gpu=null;
                previous.AvailabilityChanged-=OnGpuAvailabilityChanged;
                previous.BindCamera(null);
                if (_gpuSlot!=null) _gpuSlot.Children.Remove(previous);
                previous.Dispose();
            }
            _scene.Visibility=Visibility.Visible;
            if(_gpuModeHeader!=null && _gpuModeHeader.Text.IndexOf("GPU失败",StringComparison.Ordinal)<0)
                SetGpuModeLabel("3D / WPF空间切面","已手动关闭GPU体绘");
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
                _gpuTransferDirty=false;
                ApplyGpuUniforms();
            }
            catch(Exception error) { GpuFallback("GPU体数据更新失败："+error.Message); }
        }
    }
}
