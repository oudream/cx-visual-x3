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


    public sealed class MOLLinkRequestedEventArgs : EventArgs
    {
        public MOLLinkRequestedEventArgs(Guid source,Guid target)
        {SourceBlockId=source;TargetBlockId=target;}
        public Guid SourceBlockId{get;private set;}
        public Guid TargetBlockId{get;private set;}
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


        public static readonly DependencyProperty WiringDefinitionProperty=
            DependencyProperty.Register("WiringDefinition",typeof(MOLWiringDefinition),
                typeof(MOLFlowCanvas),new FrameworkPropertyMetadata(null,
                    FrameworkPropertyMetadataOptions.AffectsMeasure |
                    FrameworkPropertyMetadataOptions.AffectsRender));
        public MOLWiringDefinition WiringDefinition
        {get{return (MOLWiringDefinition)GetValue(WiringDefinitionProperty);}
         set{SetValue(WiringDefinitionProperty,value);}}
        private Guid? _sourceForLink;
        public void ResetLinkSelection(){_sourceForLink=null;}
        public bool IsConnectMode {get;set;}
        public event EventHandler<MOLLinkRequestedEventArgs> LinkRequested;

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
            _layout=WiringDefinition!=null?MOLFlowLayout.Build(WiringDefinition):MOLFlowLayout.Build(Definition);
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

            if(WiringDefinition!=null)
            {
                var byId=_layout.ToDictionary(x=>x.BlockId);
                foreach(var edge in WiringDefinition.Links)
                {
                    MOLBlockLayout from,to;
                    if(!byId.TryGetValue(edge.Source.BlockId,out from)||
                       !byId.TryGetValue(edge.Target.BlockId,out to))continue;
                    var color=edge.Kind==MPCPortKind.Data?Brushes.DarkOrange:Brushes.SteelBlue;
                    var pen=new Pen(color,edge.Kind==MPCPortKind.Data?1.8:1.4);
                    var start=new Point(from.X+from.Width/2,from.Y+from.Height);
                    var end=new Point(to.X+to.Width/2,to.Y);
                    dc.DrawLine(pen,start,end);
                }
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
                if(i!=0 && WiringDefinition==null)
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
                    if(IsConnectMode&&WiringDefinition!=null)
                    {
                        if(!_sourceForLink.HasValue)_sourceForLink=item.BlockId;
                        else
                        {
                            Guid source=_sourceForLink.Value;_sourceForLink=null;
                            if(source!=item.BlockId)
                            {
                                var link=LinkRequested;
                                if(link!=null)link(this,new MOLLinkRequestedEventArgs(source,item.BlockId));
                            }
                        }
                    }
                    break;
                }
            }
        }
    }
}
