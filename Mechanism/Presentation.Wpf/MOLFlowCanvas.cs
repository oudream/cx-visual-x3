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

    public sealed class MOLBlockMoveRequestedEventArgs : EventArgs
    {
        internal MOLBlockMoveRequestedEventArgs(Guid blockId,double x,double y)
        {BlockId=blockId;X=x;Y=y;}
        public Guid BlockId {get;private set;}
        public double X {get;private set;}
        public double Y {get;private set;}
    }

    /// <summary>类型化端口在画布上的投影，不是另一份工程定义。</summary>
    public sealed class MOLPortVisual
    {
        internal MOLPortVisual(Guid blockId,MPCPortDescriptor descriptor,
            MPCPortDirection direction,Point center)
        {BlockId=blockId;Descriptor=descriptor;Direction=direction;Center=center;}
        internal MPCPortDescriptor Descriptor {get;private set;}
        public Guid BlockId {get;private set;}
        public string MemberId {get{return Descriptor.MemberId;}}
        public MPCPortKind Kind {get{return Descriptor.Kind;}}
        public MPCPortDirection Direction {get;private set;}
        public Point Center {get;private set;}
    }

    public sealed class MOLPortLinkRequestedEventArgs : EventArgs
    {
        internal MOLPortLinkRequestedEventArgs(MOLPortVisual from,MOLPortVisual to)
        {SourceBlockId=from.BlockId;SourcePort=from.MemberId;
         TargetBlockId=to.BlockId;TargetPort=to.MemberId;Kind=from.Kind;}
        public Guid SourceBlockId {get;private set;}
        public string SourcePort {get;private set;}
        public Guid TargetBlockId {get;private set;}
        public string TargetPort {get;private set;}
        public MPCPortKind Kind {get;private set;}
    }

    public sealed class MOLPortLinkRejectedEventArgs : EventArgs
    {
        internal MOLPortLinkRejectedEventArgs(string reason){Reason=reason;}
        public string Reason {get;private set;}
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
        private MOLPortVisual _dragSource;
        private Point _dragPointer;
        private Guid? _movingBlockId;
        private Point _moveOrigin,_moveBase,_moveDelta;
        public event EventHandler<MOLBlockMoveRequestedEventArgs> BlockMoveRequested;
        public event EventHandler<MOLPortLinkRequestedEventArgs> PortLinkRequested;
        public event EventHandler<MOLPortLinkRejectedEventArgs> PortLinkRejected;
        private Guid? _sourceForLink;
        public void ResetLinkSelection()
        {
            _sourceForLink=null;_dragSource=null;_movingBlockId=null;
            _moveDelta=new Point();
            if(IsMouseCaptured)ReleaseMouseCapture();
            InvalidateVisual();
        }

        /// <summary>供WPF命中测试、辅助访问和自动化验收使用；坐标在画布局部空间。</summary>
        public IReadOnlyList<MOLPortVisual> GetVisiblePorts()
        {
            if(WiringDefinition==null)return Array.AsReadOnly(new MOLPortVisual[0]);
            var layout=MOLFlowLayout.Build(WiringDefinition);
            var result=new List<MOLPortVisual>();
            foreach(var item in layout)
            {
                var ports=WiringDefinition.Ports[item.BlockId];
                int left=0,right=0;
                double dx=_movingBlockId==item.BlockId?_moveDelta.X:0;
                double dy=_movingBlockId==item.BlockId?_moveDelta.Y:0;
                foreach(var descriptor in ports)
                {
                    if(descriptor.Direction!=MPCPortDirection.Output)
                        result.Add(new MOLPortVisual(item.BlockId,descriptor,
                            MPCPortDirection.Input,new Point(item.X+dx+2,item.Y+dy+36+22*left++)));
                    if(descriptor.Direction!=MPCPortDirection.Input)
                        result.Add(new MOLPortVisual(item.BlockId,descriptor,
                            MPCPortDirection.Output,new Point(item.X+dx+item.Width-2,item.Y+dy+36+22*right++)));
                }
            }
            return result.AsReadOnly();
        }

        /// <summary>块拖动与鼠标事件共用此入口；结束时才提交工程事务。</summary>
        public bool BeginBlockDrag(Point position)
        {
            if(WiringDefinition==null||HitTestPort(position)!=null)return false;
            var item=MOLFlowLayout.Build(WiringDefinition).FirstOrDefault(b=>
                position.X>=b.X&&position.X<=b.X+b.Width&&
                position.Y>=b.Y&&position.Y<=b.Y+b.Height);
            if(item==null)return false;
            _movingBlockId=item.BlockId;
            _moveOrigin=position;_moveBase=new Point(item.X,item.Y);
            _moveDelta=new Point();InvalidateVisual();
            return true;
        }
        public void UpdateBlockDrag(Point position)
        {
            if(!_movingBlockId.HasValue)return;
            _moveDelta=new Point(
                Math.Max(-_moveBase.X,Math.Min(100000-_moveBase.X,position.X-_moveOrigin.X)),
                Math.Max(-_moveBase.Y,Math.Min(100000-_moveBase.Y,position.Y-_moveOrigin.Y)));
            InvalidateVisual();
        }
        public bool CompleteBlockDrag(Point position)
        {
            if(!_movingBlockId.HasValue)return false;
            UpdateBlockDrag(position);
            Guid block=_movingBlockId.Value;
            double x=_moveBase.X+_moveDelta.X,y=_moveBase.Y+_moveDelta.Y;
            bool changed=Math.Abs(_moveDelta.X)>=1||Math.Abs(_moveDelta.Y)>=1;
            _movingBlockId=null;_moveDelta=new Point();InvalidateVisual();
            if(!changed)return false;
            var handler=BlockMoveRequested;
            if(handler!=null)handler(this,new MOLBlockMoveRequestedEventArgs(block,x,y));
            return true;
        }

        public MOLPortVisual HitTestPort(Point position)
        {
            return GetVisiblePorts().FirstOrDefault(port=>
                Math.Abs(port.Center.X-position.X)<=8 &&
                Math.Abs(port.Center.Y-position.Y)<=8);
        }

        /// <summary>鼠标事件与WPF自动化共用的拖拽入口；不修改Content。</summary>
        public bool BeginPortDrag(Point position)
        {
            _dragSource=null;
            var port=HitTestPort(position);
            if(port==null||port.Direction!=MPCPortDirection.Output)return false;
            _dragSource=port;_dragPointer=port.Center;InvalidateVisual();
            return true;
        }

        public bool CompletePortDrag(Point position)
        {
            var source=_dragSource;
            _dragSource=null;InvalidateVisual();
            if(source==null||WiringDefinition==null)return false;
            var target=HitTestPort(position);
            if(target==null)return false;
            if(target.Direction!=MPCPortDirection.Input)
            {RejectPortLink("目标必须是输入Port");return false;}
            if(source.Kind!=target.Kind||source.Descriptor.TypeId!=target.Descriptor.TypeId||
                !object.Equals(source.Descriptor.Contract,target.Descriptor.Contract)||
                (source.Kind!=MPCPortKind.Control&&source.Kind!=MPCPortKind.Data))
            {RejectPortLink("Port种类、数据类型或数据契约不匹配");return false;}
            try
            {
                // 复用Core唯一的方向、重边、多重度、环路规则，不添加WPF独立校验真源。
                var link=new MOLLink(Guid.NewGuid(),
                    new MOLBlockEndpoint(source.BlockId,source.MemberId),
                    new MOLBlockEndpoint(target.BlockId,target.MemberId),source.Kind);
                var schemas=WiringDefinition.Ports.ToDictionary(
                    pair=>pair.Key,pair=>(IEnumerable<MPCPortDescriptor>)pair.Value);
                new MOLWiringDefinition(WiringDefinition.EntryBlockId,
                    WiringDefinition.Blocks,schemas,WiringDefinition.Links.Concat(new[]{link}));
            }
            catch(Exception ex){RejectPortLink(ex.Message);return false;}
            var requested=PortLinkRequested;
            if(requested!=null)requested(this,new MOLPortLinkRequestedEventArgs(source,target));
            return true;
        }

        private void RejectPortLink(string reason)
        {
            var handler=PortLinkRejected;
            if(handler!=null)handler(this,new MOLPortLinkRejectedEventArgs(reason));
        }
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

            var visiblePorts=GetVisiblePorts();
            if(WiringDefinition!=null)
            {
                var sources=visiblePorts.Where(p=>p.Direction==MPCPortDirection.Output)
                    .ToDictionary(p=>new MOLBlockEndpoint(p.BlockId,p.MemberId),p=>p.Center);
                var targets=visiblePorts.Where(p=>p.Direction==MPCPortDirection.Input)
                    .ToDictionary(p=>new MOLBlockEndpoint(p.BlockId,p.MemberId),p=>p.Center);
                foreach(var edge in WiringDefinition.Links)
                {
                    Point start,end;
                    if(!sources.TryGetValue(edge.Source,out start)||
                       !targets.TryGetValue(edge.Target,out end))continue;
                    var color=edge.Kind==MPCPortKind.Data?Brushes.DarkOrange:Brushes.SteelBlue;
                    dc.DrawLine(new Pen(color,edge.Kind==MPCPortKind.Data?1.8:1.4),start,end);
                }
            }

            for(int i=0;i<_layout.Count;i++)
            {
                var item=_layout[i];
                bool isPreview=_movingBlockId==item.BlockId;
                if(isPreview)dc.PushTransform(new TranslateTransform(_moveDelta.X,_moveDelta.Y));
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
                if(isPreview)dc.Pop();
            }
            foreach(var port in visiblePorts)
            {
                Brush brush=port.Kind==MPCPortKind.Data?Brushes.DarkOrange:Brushes.SteelBlue;
                dc.DrawEllipse(Brushes.White,new Pen(brush,2),port.Center,5,5);
                var label=Text(port.MemberId,brush);
                double x=port.Direction==MPCPortDirection.Input?
                    port.Center.X+10:port.Center.X-10-label.Width;
                dc.DrawText(label,new Point(x,port.Center.Y-8));
            }
            if(_dragSource!=null)
                dc.DrawLine(new Pen(Brushes.DarkSlateBlue,2){DashStyle=DashStyles.Dash},
                    _dragSource.Center,_dragPointer);
        }
        private static FormattedText Text(string value,Brush brush)
        {return new FormattedText(value,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),12,brush);}
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Point p=e.GetPosition(this);
            if(BeginPortDrag(p))
            {
                CaptureMouse();e.Handled=true;return;
            }
            foreach(var item in _layout)
            {
                if(p.X>=item.X&&p.X<=item.X+item.Width&&
                    p.Y>=item.Y&&p.Y<=item.Y+item.Height)
                {
                    var handler=BlockSelected;
                    if(handler!=null)handler(this,new MOLBlockSelectedEventArgs(item.BlockId));
                    if(!IsConnectMode&&BeginBlockDrag(p))
                    {
                        CaptureMouse();e.Handled=true;return;
                    }
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
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if(_dragSource!=null)
            {_dragPointer=e.GetPosition(this);InvalidateVisual();e.Handled=true;}
            else if(_movingBlockId.HasValue)
            {UpdateBlockDrag(e.GetPosition(this));e.Handled=true;}
        }
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if(_dragSource==null&&!_movingBlockId.HasValue)return;
            Point position=e.GetPosition(this);
            try
            {
                if(_dragSource!=null)CompletePortDrag(position);
                else CompleteBlockDrag(position);
            }
            finally{if(IsMouseCaptured)ReleaseMouseCapture();}
            e.Handled=true;
        }
        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            base.OnLostMouseCapture(e);
            if(_dragSource!=null||_movingBlockId.HasValue)
            {_dragSource=null;_movingBlockId=null;_moveDelta=new Point();InvalidateVisual();}
        }
    }
}
