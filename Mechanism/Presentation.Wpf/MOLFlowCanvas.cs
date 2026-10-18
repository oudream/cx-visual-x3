using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Visual;

namespace CxVisual.Mechanism.Presentation.Wpf
{
    public sealed class MOLBlockSelectedEventArgs : EventArgs
    {
        public MOLBlockSelectedEventArgs(Guid id) { BlockId=id; }
        public Guid BlockId {get;private set;}
    }

    public static class MOLBlockTemplateSelector
    {
        public static bool IsDecision(MOLControlKind kind)
        { return kind == MOLControlKind.Branch; }
        public static bool IsContainer(MOLControlKind kind)
        { return kind == MOLControlKind.Sequence || kind == MOLControlKind.Loop ||
            kind == MOLControlKind.ForEach || kind == MOLControlKind.Parallel; }
    }

    /// <summary>
    /// One reusable engineering logic view. The MRK engine never references WPF.
    /// </summary>
    public sealed class MOLFlowCanvas : FrameworkElement
    {
        public static readonly DependencyProperty DefinitionProperty =
            DependencyProperty.Register("Definition",typeof(MOLLogicDefinition),
                typeof(MOLFlowCanvas),new FrameworkPropertyMetadata(null,
                    FrameworkPropertyMetadataOptions.AffectsMeasure |
                    FrameworkPropertyMetadataOptions.AffectsRender));
        public MOLLogicDefinition Definition
        {get{return (MOLLogicDefinition)GetValue(DefinitionProperty);}set{SetValue(DefinitionProperty,value);}}

        public static readonly DependencyProperty RuntimeStatesProperty =
            DependencyProperty.Register("RuntimeStates",typeof(IDictionary<Guid,string>),
                typeof(MOLFlowCanvas),new FrameworkPropertyMetadata(null,
                    FrameworkPropertyMetadataOptions.AffectsRender));
        public IDictionary<Guid,string> RuntimeStates
        {get{return (IDictionary<Guid,string>)GetValue(RuntimeStatesProperty);}
         set{SetValue(RuntimeStatesProperty,value);}}

        private IReadOnlyList<MOLBlockLayout> _layout=Array.AsReadOnly(new MOLBlockLayout[0]);
        public event EventHandler<MOLBlockSelectedEventArgs> BlockSelected;
        protected override Size MeasureOverride(Size availableSize)
        {
            _layout=MOLFlowLayout.Build(Definition);
            double width=_layout.Count==0?520:Math.Max(520,_layout.Max(b=>b.X+b.Width+20));
            double height=_layout.Count==0?180:_layout.Max(b=>b.Y+b.Height+24);
            return new Size(width,height);
        }
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            dc.DrawRectangle(Brushes.White,null,new Rect(RenderSize));
            if(_layout.Count==0)
            {
                var hint=Text("当前Content尚无逻辑编排；可创建示例或通过工程API设置Logic Aspect。",
                    Brushes.SlateGray);
                dc.DrawText(hint,new Point(20,25));return;
            }
            for(int i=0;i<_layout.Count;i++)
            {
                var item=_layout[i];
                string state=null;
                if(RuntimeStates!=null)RuntimeStates.TryGetValue(item.BlockId,out state);
                Brush fill=state=="Completed"?Brushes.Honeydew:state=="Failed"?Brushes.MistyRose:
                    state=="Running"?Brushes.LightBlue:state=="Skipped"?Brushes.LightGray:
                    Brushes.AliceBlue;
                var rect=new Rect(item.X,item.Y,item.Width,item.Height);
                var pen=new Pen(Brushes.SteelBlue,1.4);
                if(MOLBlockTemplateSelector.IsDecision(item.Kind))
                {
                    var geo=new StreamGeometry();
                    using(var c=geo.Open())
                    {
                        c.BeginFigure(new Point(rect.Left,rect.Top+rect.Height/2),true,true);
                        c.LineTo(new Point(rect.Left+22,rect.Top),true,false);
                        c.LineTo(new Point(rect.Right-22,rect.Top),true,false);
                        c.LineTo(new Point(rect.Right,rect.Top+rect.Height/2),true,false);
                        c.LineTo(new Point(rect.Right-22,rect.Bottom),true,false);
                        c.LineTo(new Point(rect.Left+22,rect.Bottom),true,false);
                    }
                    geo.Freeze();dc.DrawGeometry(fill,pen,geo);
                }
                else dc.DrawRoundedRectangle(fill,pen,rect,7,7);
                dc.DrawText(Text(item.Label+(state==null?"":" · "+state),Brushes.MidnightBlue),
                    new Point(item.X+26,item.Y+11));
                if(i!=0)
                {
                    var previous=_layout[i-1];
                    dc.DrawLine(new Pen(Brushes.LightSteelBlue,1),
                        new Point(previous.X+previous.Width/2,previous.Y+previous.Height),
                        new Point(item.X+item.Width/2,item.Y));
                }
            }
        }
        private static FormattedText Text(string value,Brush brush)
        {return new FormattedText(value,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),12,brush);}
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Point p=e.GetPosition(this);
            foreach(var item in _layout)
            {
                if(p.X>=item.X&&p.X<=item.X+item.Width&&
                    p.Y>=item.Y&&p.Y<=item.Y+item.Height)
                {
                    var handler=BlockSelected;
                    if(handler!=null)handler(this,new MOLBlockSelectedEventArgs(item.BlockId));
                    break;
                }
            }
        }
    }
}
