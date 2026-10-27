using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace CxVisual.Mechanism.Presentation.Wpf.Volume
{
    /// <summary>
    /// 正式WPF三维体渲染参数控件。拥有唯一的界面输入与事件；
    /// 不依赖CT3D、OpenGL或FVO运行时数据，不存储第二份专业渲染参数。
    /// 配置应用、LUT烘焙及真实GPU生命周期由宿主的专业Owner负责。
    /// </summary>
    public sealed class MPWVolumeRenderingParametersControl : StackPanel
    {
        /// <summary>参数改变事件：true需要重新烘焙传递函数LUT；false只需更新uniform。</summary>
        public event Action<bool> OptionsChanged;
        public event Action<bool> GpuEnabledChanged;
        public event Action SaveFrameRequested;

        public CheckBox EnableGpu { get; private set; }
        public TextBlock StatusText { get; private set; }
        public Slider VolumeOpacity { get; private set; }
        public Slider SamplingStep { get; private set; }
        public Slider Ambient { get; private set; }
        public Slider Diffuse { get; private set; }
        public Slider Specular { get; private set; }
        public Slider SpecularPower { get; private set; }
        public CheckBox Jitter { get; private set; }
        public CheckBox AutoRange { get; private set; }
        public CheckBox UseColor { get; private set; }
        public CheckBox UseScalar { get; private set; }
        public CheckBox UseGradient { get; private set; }
        public Slider Threshold { get; private set; }
        public CheckBox UseThreshold { get; private set; }
        public CheckBox ShowVolume { get; private set; }
        public CheckBox ShowSlices { get; private set; }
        public CheckBox ShowSliceTexture { get; private set; }
        public CheckBox ShowBounds { get; private set; }
        public Slider SliceOpacity { get; private set; }
        public ComboBox Preset { get; private set; }
        public Button SaveFrameButton { get; private set; }

        public MPWVolumeRenderingParametersControl()
        {
            AutomationProperties.SetAutomationId(this,"MPW.Volume.RenderingParameters");
            EnableGpu=new CheckBox { Content="真实GPU体绘（优先启用）", IsChecked=true };
            StatusText=new TextBlock { Text="GPU准备就绪后自动启动；不支持时回退空间切面。" };
            VolumeOpacity=new Slider { Minimum=0,Maximum=3,Value=1,TickFrequency=.05 };
            SamplingStep=new Slider { Minimum=.25,Maximum=3,Value=1,TickFrequency=.05 };
            Ambient=new Slider { Minimum=0,Maximum=1,Value=.34 };
            Diffuse=new Slider { Minimum=0,Maximum=1.5,Value=.62 };
            Specular=new Slider { Minimum=0,Maximum=1.5,Value=.16 };
            SpecularPower=new Slider { Minimum=1,Maximum=96,Value=24 };
            Jitter=new CheckBox { Content="抖动采样", IsChecked=true };
            AutoRange=new CheckBox { Content="Slicer 风格自动灰阶", IsChecked=true };
            UseColor=new CheckBox { Content="启用自定义颜色曲线", IsChecked=false };
            UseScalar=new CheckBox { Content="启用标量透明曲线", IsChecked=false };
            UseGradient=new CheckBox { Content="启用梯度透明曲线", IsChecked=false };
            Threshold=new Slider { Minimum=0,Maximum=1,Value=.28 };
            UseThreshold=new CheckBox { Content="应用阈值", IsChecked=false };
            ShowVolume=new CheckBox { Content="显示体绘",IsChecked=true };
            ShowSlices=new CheckBox { Content="显示三维定位切面",IsChecked=false };
            ShowSliceTexture=new CheckBox { Content="显示切面灰度纹理",IsChecked=false };
            ShowBounds=new CheckBox { Content="显示体包围盒",IsChecked=false };
            SliceOpacity=new Slider { Minimum=0,Maximum=1,Value=.35 };
            Preset=new ComboBox { Margin=new Thickness(4),Height=29 };
            AutomationProperties.SetAutomationId(VolumeOpacity,
                "MPW.Volume.RenderingParameters.Opacity");
            AutomationProperties.SetAutomationId(SamplingStep,
                "MPW.Volume.RenderingParameters.Step");
            AutomationProperties.SetAutomationId(EnableGpu,
                "MPW.Volume.RenderingParameters.EnableGpu");
            AutomationProperties.SetAutomationId(Preset,
                "MPW.Volume.RenderingParameters.Preset");
            EnableGpu.Margin=new Thickness(4,8,4,6);
            Children.Add(EnableGpu);
            StatusText.Margin=new Thickness(4,2,4,10);
            StatusText.TextWrapping=TextWrapping.Wrap;
            StatusText.Foreground=Brushes.SteelBlue;
            Children.Add(StatusText);
            Children.Add(Preset);
            Children.Add(ShowVolume);
            Children.Add(ShowSlices);
            Children.Add(ShowSliceTexture);
            Children.Add(ShowBounds);
            AddLabeledSlider(this,"定位切面透明度",SliceOpacity);
            SaveFrameButton=new Button {
                Content="保存真实GPU帧PNG",Margin=new Thickness(4),Height=32
            };
            AutomationProperties.SetAutomationId(SaveFrameButton,"MPW.Volume.SaveGpuFrame");
            SaveFrameButton.Click+=(s,e)=>{
                var callback=SaveFrameRequested;
                if(callback!=null)callback();
            };
            Children.Add(SaveFrameButton);
            AddLabeledSlider(this,"整体体透明度",VolumeOpacity);
            AddLabeledSlider(this,"Raymarch采样步长",SamplingStep);
            AddLabeledSlider(this,"环境光",Ambient);
            AddLabeledSlider(this,"漫反射",Diffuse);
            AddLabeledSlider(this,"镜面反射",Specular);
            AddLabeledSlider(this,"高光强度",SpecularPower);
            Children.Add(Jitter);
            Children.Add(AutoRange);
            Children.Add(UseColor);
            Children.Add(UseScalar);
            Children.Add(UseGradient);
            Children.Add(UseThreshold);
            AddLabeledSlider(this,"阈值下限",Threshold);

            // 依旧先建UI后挂通知，初始化默认值不触发专业更新或GPU资源创建。
            EnableGpu.Checked+=(s,e)=>NotifyGpuEnabled(true);
            EnableGpu.Unchecked+=(s,e)=>NotifyGpuEnabled(false);
            VolumeOpacity.ValueChanged+=(s,e)=>NotifyOptionsChanged(false);
            SamplingStep.ValueChanged+=(s,e)=>NotifyOptionsChanged(false);
            Ambient.ValueChanged+=(s,e)=>NotifyOptionsChanged(false);
            Diffuse.ValueChanged+=(s,e)=>NotifyOptionsChanged(false);
            Specular.ValueChanged+=(s,e)=>NotifyOptionsChanged(false);
            SpecularPower.ValueChanged+=(s,e)=>NotifyOptionsChanged(false);
            Jitter.Checked+=(s,e)=>NotifyOptionsChanged(false);
            Jitter.Unchecked+=(s,e)=>NotifyOptionsChanged(false);
            Preset.SelectionChanged+=(s,e)=>NotifyOptionsChanged(true);
            AutoRange.Checked+=(s,e)=>NotifyOptionsChanged(true);
            AutoRange.Unchecked+=(s,e)=>NotifyOptionsChanged(true);
            UseColor.Checked+=(s,e)=>NotifyOptionsChanged(true);
            UseColor.Unchecked+=(s,e)=>NotifyOptionsChanged(true);
            UseScalar.Checked+=(s,e)=>NotifyOptionsChanged(true);
            UseScalar.Unchecked+=(s,e)=>NotifyOptionsChanged(true);
            UseGradient.Checked+=(s,e)=>NotifyOptionsChanged(true);
            UseGradient.Unchecked+=(s,e)=>NotifyOptionsChanged(true);
            UseThreshold.Checked+=(s,e)=>NotifyOptionsChanged(true);
            UseThreshold.Unchecked+=(s,e)=>NotifyOptionsChanged(true);
            Threshold.ValueChanged+=(s,e)=>NotifyOptionsChanged(true);
            SliceOpacity.ValueChanged+=(s,e)=>NotifyOptionsChanged(false);
            ShowVolume.Checked+=(s,e)=>NotifyOptionsChanged(false);
            ShowVolume.Unchecked+=(s,e)=>NotifyOptionsChanged(false);
            ShowSlices.Checked+=(s,e)=>NotifyOptionsChanged(false);
            ShowSlices.Unchecked+=(s,e)=>NotifyOptionsChanged(false);
            ShowSliceTexture.Checked+=(s,e)=>NotifyOptionsChanged(false);
            ShowSliceTexture.Unchecked+=(s,e)=>NotifyOptionsChanged(false);
            ShowBounds.Checked+=(s,e)=>NotifyOptionsChanged(false);
            ShowBounds.Unchecked+=(s,e)=>NotifyOptionsChanged(false);
        }

        private void NotifyOptionsChanged(bool transferChanged)
        {
            var handler=OptionsChanged;
            if(handler!=null)handler(transferChanged);
        }
        private void NotifyGpuEnabled(bool enabled)
        {
            var handler=GpuEnabledChanged;
            if(handler!=null)handler(enabled);
        }
        public static void AddLabeledSlider(Panel panel,string text,Slider slider)
        {
            if(panel==null)throw new ArgumentNullException("panel");
            if(slider==null)throw new ArgumentNullException("slider");
            var label=new TextBlock {
                Text=text+"："+slider.Value.ToString("0.##"),
                Margin=new Thickness(4,7,4,1),
                Foreground=new SolidColorBrush(Color.FromRgb(218,231,244))
            };
            slider.ValueChanged+=(s,e)=>label.Text=text+"："+slider.Value.ToString("0.##");
            slider.ToolTip=text;
            panel.Children.Add(label);
            slider.Margin=new Thickness(6,0,6,6);
            panel.Children.Add(slider);
        }
    }
}
