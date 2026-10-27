using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace CxVisual.Mechanism.Presentation.Wpf.Volume
{
    /// <summary>
    /// 通用三维工作台的可嵌入双区域设置面板。仅拥有WPF布局与页签；
    /// 外部传入现有FVO/MVI/CT3D设置编辑控件，不复制专业设置或GPU状态。
    /// </summary>
    public sealed class MPWVolumeSettingsDockControl : Grid
    {
        private readonly TabControl _upper;
        private readonly TabControl _lower;

        public MPWVolumeSettingsDockControl()
        {
            Background=new SolidColorBrush(Color.FromRgb(9,15,24));
            Margin=new Thickness(1,0,0,0);
            AutomationProperties.SetAutomationId(this,"MPW.Volume.SettingsDock");
            RowDefinitions.Add(new RowDefinition { Height=new GridLength(44,GridUnitType.Star) });
            RowDefinitions.Add(new RowDefinition { Height=new GridLength(56,GridUnitType.Star) });

            // 保留旧版深色设置面板对所有继承控件的统一文字主题。
            Style checks=new Style(typeof(CheckBox));
            checks.Setters.Add(new Setter(Control.ForegroundProperty,
                new SolidColorBrush(Color.FromRgb(224,234,247))));
            Resources.Add(typeof(CheckBox),checks);
            Style labels=new Style(typeof(TextBlock));
            labels.Setters.Add(new Setter(TextBlock.ForegroundProperty,
                new SolidColorBrush(Color.FromRgb(221,232,246))));
            Resources.Add(typeof(TextBlock),labels);

            _upper=new TabControl {
                Margin=new Thickness(0,0,0,2), Background=Background
            };
            _lower=new TabControl {
                Margin=new Thickness(0,2,0,0), Background=Background
            };
            AutomationProperties.SetAutomationId(_upper,"MPW.Volume.Settings.UpperTabs");
            AutomationProperties.SetAutomationId(_lower,"MPW.Volume.Settings.LowerTabs");
            Children.Add(_upper);
            Grid.SetRow(_lower,1);
            Children.Add(_lower);
        }

        public TabControl UpperTabs { get { return _upper; } }
        public TabControl LowerTabs { get { return _lower; } }
        public int UpperCount { get { return _upper.Items.Count; } }
        public int LowerCount { get { return _lower.Items.Count; } }

        /// <summary>创建滚动设置页。宿主持有传入编辑控件的业务逻辑。</summary>
        public TabItem AddUpperTab(string title,UIElement editor)
        {
            Validate(title,editor);
            var scroll=new ScrollViewer {
                Content=editor,VerticalScrollBarVisibility=ScrollBarVisibility.Auto
            };
            var tab=new TabItem { Header=title,Content=scroll };
            _upper.Items.Add(tab);
            if(_upper.SelectedIndex<0)_upper.SelectedIndex=0;
            return tab;
        }

        /// <summary>注册FVO传递函数等编辑页，直接保留原控件实例及事件。</summary>
        public TabItem AddLowerTab(string title,UIElement editor)
        {
            Validate(title,editor);
            var tab=new TabItem { Header=title,Content=editor };
            _lower.Items.Add(tab);
            if(_lower.SelectedIndex<0)_lower.SelectedIndex=0;
            return tab;
        }

        private static void Validate(string title,UIElement editor)
        {
            if(String.IsNullOrWhiteSpace(title))
                throw new ArgumentException("设置页标题不能为空。","title");
            if(editor==null)throw new ArgumentNullException("editor");
            FrameworkElement element=editor as FrameworkElement;
            if(element!=null && element.Parent!=null)
                throw new InvalidOperationException("同一个设置控件不能同时添加到多个容器。");
        }
    }
}
