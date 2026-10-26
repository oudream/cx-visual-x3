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

        private void BuildTransferPanels(Panel parent)
        {
            Expander expander=new Expander { Header="交互传递函数（拖动 / 双击添加 / 右键删除）",
                Margin=new Thickness(3,10,3,5),IsExpanded=false };
            TabControl tabs=new TabControl { MinHeight=280,Margin=new Thickness(0,3,0,3) };
            expander.Content=tabs;
            parent.Children.Add(expander);

            _colorCurve=new TCTVolumeCurveEditor(_gpuSettings.TransferFunctions.Color.Points,true);
            _scalarCurve=new TCTVolumeCurveEditor(_gpuSettings.TransferFunctions.ScalarOpacity.Points,false);
            _gradientCurve=new TCTVolumeCurveEditor(_gpuSettings.TransferFunctions.GradientOpacity.Points,false);
            StackPanel colors=new StackPanel();
            colors.Children.Add(_colorCurve);
            AddGpuSlider(colors,"R",_red);
            AddGpuSlider(colors,"G",_green);
            AddGpuSlider(colors,"B",_blue);
            tabs.Items.Add(new TabItem { Header="颜色",Content=new ScrollViewer {
                Content=colors, VerticalScrollBarVisibility=ScrollBarVisibility.Auto } });
            tabs.Items.Add(new TabItem { Header="标量透明",Content=_scalarCurve });
            tabs.Items.Add(new TabItem { Header="梯度透明",Content=_gradientCurve });
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
