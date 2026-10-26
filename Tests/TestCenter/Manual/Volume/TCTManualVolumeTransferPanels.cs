using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CxVisual.Foundation.Volume;

namespace CxVisual.TestCenter.UI
{
    /// <summary>沿用唯一FVO传递曲线，真实拖动节点后立即刷新CT3D LUT与GPU。</summary>
    public sealed partial class TCTManualVolumeViewerFrame
    {
        private TCTVolumeCurveEditor _colorCurve,_scalarCurve,_gradientCurve;
        private readonly Slider _red=new Slider { Minimum=0,Maximum=255,Value=230 };
        private readonly Slider _green=new Slider { Minimum=0,Maximum=255,Value=194 };
        private readonly Slider _blue=new Slider { Minimum=0,Maximum=255,Value=68 };
        private bool _settingRgb;
        public int ColorStopCount { get { return _gpuSettings.TransferFunctions.Color.Points.Count; } }
        public int ScalarStopCount { get { return _gpuSettings.TransferFunctions.ScalarOpacity.Points.Count; } }
        public int GradientStopCount { get { return _gpuSettings.TransferFunctions.GradientOpacity.Points.Count; } }

        private void BuildTransferPanels(TabControl tabs)
        {
            // 与旧 WinForms 同样为独立五标签页，所有点直接修改唯一 FVOTransferFunctionSet。
            _colorCurve=new TCTVolumeCurveEditor(_gpuSettings.TransferFunctions.Color.Points,true);
            _scalarCurve=new TCTVolumeCurveEditor(_gpuSettings.TransferFunctions.ScalarOpacity.Points,false);
            _gradientCurve=new TCTVolumeCurveEditor(_gpuSettings.TransferFunctions.GradientOpacity.Points,false);
            StackPanel threshold=new StackPanel { Margin=new Thickness(7) };
            MoveToTab(_gpuUseThreshold, (Panel)_gpuUseThreshold.Parent, threshold);
            MoveToTab(_gpuThreshold, (Panel)_gpuThreshold.Parent, threshold);
            _thresholdCurve=new TCTVolumeThresholdEditor();
            _thresholdCurve.Lower=_gpuSettings.TransferFunctions.Threshold;
            _thresholdCurve.Upper=_gpuSettings.TransferFunctions.UpperThreshold;
            threshold.Children.Add(_thresholdCurve);
            _thresholdCurve.RangeChanged+=(s,e)=>{
                _gpuSettings.TransferFunctions.Threshold=_thresholdCurve.Lower;
                _gpuSettings.TransferFunctions.UpperThreshold=_thresholdCurve.Upper;
                _gpuThreshold.Value=_thresholdCurve.Lower;
                _gpuUseThreshold.IsChecked=true;
                UpdateGpuSettings();
            };
            tabs.Items.Add(new TabItem { Header="阈值",Content=threshold });
            StackPanel colors=new StackPanel { Margin=new Thickness(4) };
            MoveToTab(_gpuColor,(Panel)_gpuColor.Parent,colors);
            colors.Children.Add(_colorCurve);
            AddGpuSlider(colors,"R",_red);
            AddGpuSlider(colors,"G",_green);
            AddGpuSlider(colors,"B",_blue);
            tabs.Items.Add(new TabItem { Header="颜色",Content=new ScrollViewer { Content=colors,
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto } });
            StackPanel scalar=new StackPanel { Margin=new Thickness(4) };
            MoveToTab(_gpuScalar,(Panel)_gpuScalar.Parent,scalar);
            scalar.Children.Add(_scalarCurve);
            tabs.Items.Add(new TabItem { Header="标量透明",Content=new ScrollViewer { Content=scalar,
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto } });
            StackPanel gradient=new StackPanel { Margin=new Thickness(4) };
            MoveToTab(_gpuGradient,(Panel)_gpuGradient.Parent,gradient);
            gradient.Children.Add(_gradientCurve);
            tabs.Items.Add(new TabItem { Header="梯度透明",Content=new ScrollViewer { Content=gradient,
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto } });
            StackPanel arbitrary=new StackPanel { Margin=new Thickness(10) };
            arbitrary.Children.Add(new TextBlock {
                Text="任意切的角度、位置、插值、尺寸和实时预览在上方“任意切”页签中；共用同一FVO/MVI切面状态。",
                TextWrapping=TextWrapping.Wrap,Margin=new Thickness(5,8,5,12) });
            Button jump=new Button { Content="打开任意切设置",Padding=new Thickness(8,5,8,5) };
            jump.Click+=(s,e)=>_upperTabs.SelectedIndex=3;
            arbitrary.Children.Add(jump);
            tabs.Items.Add(new TabItem { Header="任意切面",Content=arbitrary });
            tabs.SelectedIndex=0;
            _colorCurve.SelectionChanged+=(s,e)=>ReadSelectedColor();
            _red.ValueChanged+=(s,e)=>WriteSelectedColor();
            _green.ValueChanged+=(s,e)=>WriteSelectedColor();
            _blue.ValueChanged+=(s,e)=>WriteSelectedColor();
            _colorCurve.Changed+=(s,e)=>{ _gpuColor.IsChecked=true; UpdateGpuSettings(); };
            _scalarCurve.Changed+=(s,e)=>{ _gpuScalar.IsChecked=true; UpdateGpuSettings(); };
            _gradientCurve.Changed+=(s,e)=>{ _gpuGradient.IsChecked=true; UpdateGpuSettings(); };
            _colorCurve.SelectPoint(Math.Min(1,_colorCurve.PointCount-1));
        }

        private TCTVolumeThresholdEditor _thresholdCurve;
        private void ReadSelectedColor()
        {
            FVOTransferPoint point=_colorCurve.SelectedPoint;
            if(point==null)return;
            _settingRgb=true;
            try { _red.Value=point.Color.R;_green.Value=point.Color.G;_blue.Value=point.Color.B; }
            finally { _settingRgb=false; }
        }

        private void WriteSelectedColor()
        {
            if(_settingRgb || _colorCurve==null || _colorCurve.SelectedPoint==null)return;
            _colorCurve.UpdateSelectedColor((byte)Math.Round(_red.Value),
                (byte)Math.Round(_green.Value),(byte)Math.Round(_blue.Value));
        }

        public void AddScalarStopForTest(float x,float y)
        {
            if(_scalarCurve==null)throw new InvalidOperationException("曲线控件未初始化。");
            _scalarCurve.AddControlPoint(x,y);
        }
        public void AddColorStopForTest(float x)
        {
            if(_colorCurve==null)throw new InvalidOperationException("曲线控件未初始化。");
            _colorCurve.AddControlPoint(x,.5f);
        }
    }
}
