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
                input.TextChanged+=(sender,args)=>Emit(callback,item,"ValueChanged",input.Text);
                control=input;
            }
            else if(Ends(type,".checkbox"))
            {
                var input=new CheckBox{Content=item.Name};
                input.Checked+=(sender,args)=>Emit(callback,item,"ValueChanged","true");
                input.Unchecked+=(sender,args)=>Emit(callback,item,"ValueChanged","false");
                control=input;
            }
            else if(Ends(type,".slider"))
            {
                var input=new Slider{Minimum=0,Maximum=100,Value=0};
                input.ValueChanged+=(sender,args)=>Emit(callback,item,"ValueChanged",
                    input.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                control=input;
            }
            else if(Ends(type,".progressbar"))
                control=new ProgressBar{Minimum=0,Maximum=100,Value=0};
            else control=new ComboBox{ToolTip="请在属性面板配置选项（Widget定义）"};
            control.Width=Math.Max(1,item.Bounds.Width);
            control.Height=Math.Max(1,item.Bounds.Height);
            MRCVisualStyle style=item.Style;
            if(style.Fill.Kind!=MRCBrushKind.None)
                control.Background=MPWShapeRenderer.BrushOf(style.Fill,control.Background);
            if(style.Stroke.Kind!=MRCBrushKind.None)
                control.BorderBrush=MPWShapeRenderer.BrushOf(style.Stroke,control.BorderBrush);
            control.BorderThickness=new Thickness(style.StrokeWidth);
            control.Opacity=style.Opacity;
            control.FontFamily=new FontFamily(style.FontFamily);
            control.FontSize=style.FontSize;
            MRCTransform m=item.WorldTransform;
            // 真实WPF控件可以随页面Group和Symbol实例旋转、缩放、斜切。
            control.RenderTransform=new MatrixTransform(new Matrix(
                m.M11,m.M21,m.M12,m.M22,m.X,m.Y));
            return control;
        }
        private static void Emit(Action<MPWWidgetEventArgs> callback,CxSceneItem item,string name,string value)
        {
            if(callback!=null)callback(new MPWWidgetEventArgs(item.Id,name,value));
        }
    }
}
