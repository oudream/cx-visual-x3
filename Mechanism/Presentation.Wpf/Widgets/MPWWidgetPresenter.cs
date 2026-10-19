using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Visual;

namespace CxVisual.Mechanism.Presentation.Wpf
{
    public sealed class MPWWidgetEventArgs : EventArgs
    {
        public MPWWidgetEventArgs(Guid id,string eventId,string value)
        {ElementId=id;EventId=eventId;Value=value;}
        public Guid ElementId{get;private set;}
        public string EventId{get;private set;}
        public string Value{get;private set;}
    }
    /// <summary>Widget工程定义到真实WPF控件的适配；不执行设备命令也不修改MEM工程Revision。</summary>
    public static class MPWWidgetPresenter
    {
        private static bool Ends(string text,string suffix)
        {return text!=null&&text.EndsWith(suffix,StringComparison.OrdinalIgnoreCase);}
        public static bool Supports(string typeId)
        {
            return typeId=="core.widget"||Ends(typeId,".button")||Ends(typeId,".textbox")||
                Ends(typeId,".checkbox")||Ends(typeId,".slider")||Ends(typeId,".progressbar")||
                Ends(typeId,".combobox");
        }
        public static FrameworkElement Create(CxSceneItem item,Action<MPWWidgetEventArgs> callback)
        {
            if(item==null)throw new ArgumentNullException("item");
            if(!Supports(item.ElementTypeId))return null;
            string type=item.ElementTypeId;
            Control control;
            if(type=="core.widget"||Ends(type,".button"))
            {
                var b=new Button{Content=item.Name};
                b.Click+=(sender,args)=>Emit(callback,item,"Click",null);
                control=b;
            }
            else if(Ends(type,".textbox"))
            {
                var input=new TextBox{Text=item.Name};
                input.TextChanged+=(sender,args)=>{
                    if(!object.Equals(input.Tag,"visual.update"))
                        Emit(callback,item,"ValueChanged",input.Text);
                };
                control=input;
            }
            else if(Ends(type,".checkbox"))
            {
                var input=new CheckBox{Content=item.Name};
                input.Checked+=(sender,args)=>{
                    if(!object.Equals(input.Tag,"visual.update"))
                        Emit(callback,item,"ValueChanged","true");
                };
                input.Unchecked+=(sender,args)=>{
                    if(!object.Equals(input.Tag,"visual.update"))
                        Emit(callback,item,"ValueChanged","false");
                };
                control=input;
            }
            else if(Ends(type,".slider"))
            {
                var input=new Slider{Minimum=0,Maximum=100,Value=0};
                input.ValueChanged+=(sender,args)=>{
                    if(!object.Equals(input.Tag,"visual.update"))
                        Emit(callback,item,"ValueChanged",
                    input.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                };
                control=input;
            }
            else if(Ends(type,".progressbar"))
                control=new ProgressBar{Minimum=0,Maximum=100,Value=0};
            else
            {
                var combo=new ComboBox{ToolTip="请在属性面板配置选项"};
                combo.SelectionChanged+=(sender,args)=>{
                    if(!object.Equals(combo.Tag,"visual.update"))
                        Emit(callback,item,"ValueChanged",combo.SelectedItem as string);
                };
                control=combo;
            }
            UpdateAppearance(control,item);
            return control;
        }
        private static MPMValue Setting(CxSceneItem item,string name)
        {
            MPMValue value;
            return item.WidgetSettings.TryGetValue(name,out value)?value:null;
        }
        private static string Text(CxSceneItem item,string name,string fallback)
        {
            MPMValue value=Setting(item,name);
            return value==null?fallback:value.AsString();
        }
        private static double Number(CxSceneItem item,string name,double fallback)
        {
            MPMValue value=Setting(item,name);
            return value==null?fallback:value.AsNumber();
        }
        /// <summary>帧刷新复用活动控件：不重建Button/TextBox，保护焦点和文本输入态。</summary>
        public static void UpdateAppearance(FrameworkElement widget,CxSceneItem item)
        {
            if(widget==null||item==null)throw new ArgumentNullException(
                widget==null?"widget":"item");
            Control control=widget as Control;
            if(control==null)throw new ArgumentException("WPF Control required","widget");
            // 整次外观刷新都是程序操作：范围收缩、选项重建也可能触发ValueChanged。
            // 必须在设置任何WPF属性前加保护，而不只保护最后的运行值赋值。
            object appearanceTag=control.Tag;
            control.Tag="visual.update";
            try
            {
            control.Width=Math.Max(1,item.LocalWidth);
            control.Height=Math.Max(1,item.LocalHeight);
            MRCVisualStyle style=item.Style;
            if(style.Fill.Kind!=MRCBrushKind.None)
                control.Background=MPWShapeRenderer.BrushOf(style.Fill,control.Background);
            if(style.Stroke.Kind!=MRCBrushKind.None)
                control.BorderBrush=MPWShapeRenderer.BrushOf(style.Stroke,control.BorderBrush);
            control.BorderThickness=new Thickness(style.StrokeWidth);
            control.Opacity=style.Opacity;
            control.FontFamily=new FontFamily(style.FontFamily);
            control.FontSize=style.FontSize;
            control.ToolTip=Text(item,"tooltip",null);
            MPMValue enabled=Setting(item,"enabled");
            control.IsEnabled=enabled==null||enabled.AsBoolean();
            var button=control as Button;
            if(button!=null)button.Content=Text(item,"content",item.Name);
            var checkbox=control as CheckBox;
            if(checkbox!=null)checkbox.Content=Text(item,"content",item.Name);
            var inputBox=control as TextBox;
            if(inputBox!=null)
            {
                string placeholder=Text(item,"placeholder","");
                if(!string.IsNullOrEmpty(placeholder))inputBox.ToolTip=placeholder;
            }
            double minimum=Number(item,"minimum",0),maximum=Number(item,"maximum",100);
            if(minimum>maximum)throw new InvalidOperationException("visual.widget.range_invalid");
            var sliderRange=control as Slider;
            if(sliderRange!=null){sliderRange.Minimum=minimum;sliderRange.Maximum=maximum;}
            var progressRange=control as ProgressBar;
            if(progressRange!=null){progressRange.Minimum=minimum;progressRange.Maximum=maximum;}
            var combo=control as ComboBox;
            if(combo!=null)
            {
                string options=Text(item,"options","");
                string[] values=options.Split(new[]{'|'},StringSplitOptions.RemoveEmptyEntries);
                if(values.Length>128||options.Length>8192)
                    throw new InvalidOperationException("visual.widget.options_budget");
                bool changed=values.Length!=combo.Items.Count;
                if(!changed)for(int i=0;i<values.Length;i++)
                    if(!string.Equals(combo.Items[i] as string,values[i],StringComparison.Ordinal))
                    {changed=true;break;}
                if(changed)
                {
                    object prior=combo.Tag;combo.Tag="visual.update";
                    try
                    {
                        combo.Items.Clear();
                        foreach(string value in values)combo.Items.Add(value);
                    }
                    finally{combo.Tag=prior;}
                }
            }
            if(item.RuntimeValue!=null)
            {
                // 控件值来自运行态数据：从工程里只读投影，不写回工程，不触发用户事件。
                object prior=control.Tag;
                control.Tag="visual.update";
                try
                {
                    var comboValue=control as ComboBox;
                    if(comboValue!=null&&item.RuntimeValue.Kind==MPMValueKind.String)
                    {
                        string value=item.RuntimeValue.AsString();
                        if(comboValue.Items.Contains(value)&&
                            !object.Equals(comboValue.SelectedItem,value))
                            comboValue.SelectedItem=value;
                    }
                    var box=control as TextBox;
                    if(box!=null&&item.RuntimeValue.Kind==MPMValueKind.String)
                    {
                        string value=item.RuntimeValue.AsString();
                        if(box.Text!=value)box.Text=value;
                    }
                    var check=control as CheckBox;
                    if(check!=null&&item.RuntimeValue.Kind==MPMValueKind.Boolean)
                        check.IsChecked=item.RuntimeValue.AsBoolean();
                    var slider=control as Slider;
                    if(slider!=null&&item.RuntimeValue.Kind==MPMValueKind.Number)
                        slider.Value=item.RuntimeValue.AsNumber();
                    var progress=control as ProgressBar;
                    if(progress!=null&&item.RuntimeValue.Kind==MPMValueKind.Number)
                        progress.Value=item.RuntimeValue.AsNumber();
                }
                finally{control.Tag=prior;}
            }
            MRCTransform m=item.WorldTransform;
            control.RenderTransform=new MatrixTransform(new Matrix(
                m.M11,m.M21,m.M12,m.M22,m.X,m.Y));
            }
            finally{control.Tag=appearanceTag;}
        }
        private static void Emit(Action<MPWWidgetEventArgs> callback,CxSceneItem item,string name,string value)
        {
            if(callback!=null)callback(new MPWWidgetEventArgs(item.Id,name,value));
        }
    }
}
