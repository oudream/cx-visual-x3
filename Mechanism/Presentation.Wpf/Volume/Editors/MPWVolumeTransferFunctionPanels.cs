using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CxVisual.Foundation.Volume;

namespace CxVisual.Mechanism.Presentation.Wpf.Volume
{
    /// <summary>
    /// 五类传递函数WPF面板的唯一公共组合Owner。
    /// 直接持有宿主传来的FVOTransferFunctionSet控制点，复用既有启用开关和阈值滑杆。
    /// 不知道CT3D专业渲染设置，也不制造第二套传递函数；变更通过回调交还宿主刷新GPU LUT。
    /// </summary>
    public sealed class MPWVolumeTransferFunctionPanels
    {
        private readonly FVOTransferFunctionSet _functions;
        private readonly CheckBox _useThreshold;
        private readonly Slider _thresholdLower;
        private readonly CheckBox _useColor;
        private readonly CheckBox _useScalar;
        private readonly CheckBox _useGradient;
        private readonly Action _transferChanged;
        private bool _settingRgb;

        public MPWVolumeTransferFunctionPanels(
            MPWVolumeSettingsDockControl dock,FVOTransferFunctionSet functions,
            CheckBox useThreshold,Slider thresholdLower,
            CheckBox useColor,CheckBox useScalar,CheckBox useGradient,
            Action transferChanged,Action openOblique)
        {
            if(dock==null)throw new ArgumentNullException("dock");
            if(functions==null)throw new ArgumentNullException("functions");
            if(useThreshold==null)throw new ArgumentNullException("useThreshold");
            if(thresholdLower==null)throw new ArgumentNullException("thresholdLower");
            if(useColor==null)throw new ArgumentNullException("useColor");
            if(useScalar==null)throw new ArgumentNullException("useScalar");
            if(useGradient==null)throw new ArgumentNullException("useGradient");
            if(transferChanged==null)throw new ArgumentNullException("transferChanged");
            if(openOblique==null)throw new ArgumentNullException("openOblique");
            _functions=functions;
            _useThreshold=useThreshold;
            _thresholdLower=thresholdLower;
            _useColor=useColor;
            _useScalar=useScalar;
            _useGradient=useGradient;
            _transferChanged=transferChanged;

            ColorCurve=new MPWVolumeCurveEditor(functions.Color.Points,true);
            ScalarCurve=new MPWVolumeCurveEditor(functions.ScalarOpacity.Points,false);
            GradientCurve=new MPWVolumeCurveEditor(functions.GradientOpacity.Points,false);
            ThresholdCurve=new MPWVolumeThresholdEditor {
                Lower=functions.Threshold,Upper=functions.UpperThreshold
            };
            RedSlider=NewColorSlider(230);
            GreenSlider=NewColorSlider(194);
            BlueSlider=NewColorSlider(68);

            var threshold=new StackPanel { Margin=new Thickness(7) };
            MoveControl(_useThreshold,threshold);
            // 原滑杆的文字标签和滑杆是独立WPF元素，迁移时保持原对象身份。
            var oldParent=_thresholdLower.Parent as Panel;
            if(oldParent!=null)
            {
                int index=oldParent.Children.IndexOf(_thresholdLower);
                if(index>0)MoveControl(oldParent.Children[index-1],threshold);
            }
            MoveControl(_thresholdLower,threshold);
            threshold.Children.Add(ThresholdCurve);
            dock.AddLowerTab("阈值",threshold);

            var color=new StackPanel { Margin=new Thickness(4) };
            MoveControl(_useColor,color);
            color.Children.Add(ColorCurve);
            AddColorSlider(color,"R",RedSlider);
            AddColorSlider(color,"G",GreenSlider);
            AddColorSlider(color,"B",BlueSlider);
            dock.AddLowerTab("颜色",Scroll(color));

            var scalar=new StackPanel { Margin=new Thickness(4) };
            MoveControl(_useScalar,scalar);
            scalar.Children.Add(ScalarCurve);
            dock.AddLowerTab("标量透明",Scroll(scalar));

            var gradient=new StackPanel { Margin=new Thickness(4) };
            MoveControl(_useGradient,gradient);
            gradient.Children.Add(GradientCurve);
            dock.AddLowerTab("梯度透明",Scroll(gradient));

            var oblique=new StackPanel { Margin=new Thickness(10) };
            oblique.Children.Add(new TextBlock {
                Text="任意切的角度、位置、插值、尺寸和实时预览在上方“任意切”页签中；共用同一FVO/MVI切面状态。",
                TextWrapping=TextWrapping.Wrap,Margin=new Thickness(5,8,5,12)
            });
            var jump=new Button { Content="打开任意切设置",
                Padding=new Thickness(8,5,8,5) };
            jump.Click+=(s,e)=>openOblique();
            oblique.Children.Add(jump);
            dock.AddLowerTab("任意切面",oblique);
            dock.LowerTabs.SelectedIndex=0;

            _thresholdLower.ValueChanged+=(s,e)=>{
                ThresholdCurve.Lower=(float)_thresholdLower.Value;
            };
            ThresholdCurve.RangeChanged+=(s,e)=>{
                _functions.Threshold=ThresholdCurve.Lower;
                _functions.UpperThreshold=ThresholdCurve.Upper;
                _thresholdLower.Value=ThresholdCurve.Lower;
                _useThreshold.IsChecked=true;
                _transferChanged();
            };
            ColorCurve.SelectionChanged+=(s,e)=>ReadSelectedColor();
            RedSlider.ValueChanged+=(s,e)=>WriteSelectedColor();
            GreenSlider.ValueChanged+=(s,e)=>WriteSelectedColor();
            BlueSlider.ValueChanged+=(s,e)=>WriteSelectedColor();
            ColorCurve.Changed+=(s,e)=>{_useColor.IsChecked=true;_transferChanged();};
            ScalarCurve.Changed+=(s,e)=>{_useScalar.IsChecked=true;_transferChanged();};
            GradientCurve.Changed+=(s,e)=>{_useGradient.IsChecked=true;_transferChanged();};
            if(ColorCurve.PointCount>0)
                ColorCurve.SelectPoint(Math.Min(1,ColorCurve.PointCount-1));
        }

        public FVOTransferFunctionSet TransferFunctions {get{return _functions;}}
        public MPWVolumeCurveEditor ColorCurve {get;private set;}
        public MPWVolumeCurveEditor ScalarCurve {get;private set;}
        public MPWVolumeCurveEditor GradientCurve {get;private set;}
        public MPWVolumeThresholdEditor ThresholdCurve {get;private set;}
        public Slider RedSlider {get;private set;}
        public Slider GreenSlider {get;private set;}
        public Slider BlueSlider {get;private set;}

        public void UpdateHistograms(float[] scalar,float[] gradient)
        {
            ThresholdCurve.Histogram=scalar;
            ThresholdCurve.InvalidateVisual();
            ColorCurve.Histogram=scalar;
            ColorCurve.InvalidateVisual();
            ScalarCurve.Histogram=scalar;
            ScalarCurve.InvalidateVisual();
            GradientCurve.Histogram=gradient;
            GradientCurve.InvalidateVisual();
        }

        private void ReadSelectedColor()
        {
            FVOTransferPoint point=ColorCurve.SelectedPoint;
            if(point==null)return;
            _settingRgb=true;
            try {
                RedSlider.Value=point.Color.R;
                GreenSlider.Value=point.Color.G;
                BlueSlider.Value=point.Color.B;
            }
            finally {_settingRgb=false;}
        }
        private void WriteSelectedColor()
        {
            if(_settingRgb || ColorCurve.SelectedPoint==null)return;
            ColorCurve.UpdateSelectedColor(
                (byte)Math.Round(RedSlider.Value),
                (byte)Math.Round(GreenSlider.Value),
                (byte)Math.Round(BlueSlider.Value));
        }

        private static Slider NewColorSlider(double value)
        {
            return new Slider {Minimum=0,Maximum=255,Value=value};
        }
        private static ScrollViewer Scroll(UIElement content)
        {
            return new ScrollViewer {Content=content,
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        }
        private static void MoveControl(UIElement child,Panel target)
        {
            Panel current=child.Parent as Panel;
            if(current!=null)current.Children.Remove(child);
            else if(child.Parent!=null)
                throw new InvalidOperationException("传入控件有不支持的原始WPF父容器。");
            target.Children.Add(child);
        }
        private static void AddColorSlider(Panel target,string name,Slider slider)
        {
            var label=new TextBlock { Text=name+"："+slider.Value.ToString("0.##"),
                Margin=new Thickness(4,7,4,1),
                Foreground=new SolidColorBrush(Color.FromRgb(218,231,244)) };
            slider.ValueChanged+=(s,e)=>label.Text=name+"："+slider.Value.ToString("0.##");
            slider.ToolTip=name;
            slider.Margin=new Thickness(6,0,6,6);
            target.Children.Add(label);
            target.Children.Add(slider);
        }
    }
}
