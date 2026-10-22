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

    public sealed class MOLBlocksMoveRequestedEventArgs : EventArgs
    {
        internal MOLBlocksMoveRequestedEventArgs(IEnumerable<Guid> ids,double dx,double dy)
        {BlockIds=ids.ToArray();DeltaX=dx;DeltaY=dy;}
        public IReadOnlyList<Guid> BlockIds {get;private set;}
        public double DeltaX {get;private set;}
        public double DeltaY {get;private set;}
    }
    public sealed class MOLSelectionChangedEventArgs : EventArgs
    {
        internal MOLSelectionChangedEventArgs(IEnumerable<Guid> ids){BlockIds=ids.ToArray();}
        public IReadOnlyList<Guid> BlockIds {get;private set;}
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
        private MOLPortVisual _dragTarget;
        private bool _dragTargetValid;
        private Point _dragPointer;
        private Guid? _movingBlockId;
        private Point _moveOrigin,_moveBase,_moveDelta;
        private Guid[] _movingIds=new Guid[0];
        private readonly HashSet<Guid> _selected=new HashSet<Guid>();
        private bool _marqueeActive,_marqueeAdditive;
        private Point _marqueeStart,_marqueeEnd;
        private double _zoom=1;
        public event EventHandler<MOLBlockMoveRequestedEventArgs> BlockMoveRequested;
        public event EventHandler<MOLBlocksMoveRequestedEventArgs> BlocksMoveRequested;
        public event EventHandler<MOLSelectionChangedEventArgs> SelectionChanged;
        /// <summary>纯视口比例；布局使用WPF LayoutTransform，不改变Content坐标。</summary>
        public double ZoomFactor
        {
            get{return _zoom;}
            set
            {
                if(double.IsNaN(value)||double.IsInfinity(value)||value<0.25||value>3)
                    throw new ArgumentOutOfRangeException("value");
                _zoom=value;
                LayoutTransform=new ScaleTransform(value,value);
            }
        }
        public IReadOnlyList<Guid> SelectedBlockIds
        {get{return _selected.OrderBy(id=>id).ToArray();}}
        public void SetSelection(IEnumerable<Guid> ids)
        {
            if(ids==null)throw new ArgumentNullException("ids");
            var allowed=new HashSet<Guid>(WiringDefinition==null?
                Enumerable.Empty<Guid>():WiringDefinition.Blocks.Select(b=>b.Id));
            Guid[] requested=ids.Distinct().ToArray();
            if(requested.Any(id=>!allowed.Contains(id)))
                throw new InvalidOperationException("wiring.selection.unknown_block");
            if(_selected.SetEquals(requested))return;
            _selected.Clear();
            foreach(Guid id in requested)_selected.Add(id);
            InvalidateVisual();
            var callback=SelectionChanged;
            if(callback!=null)callback(this,new MOLSelectionChangedEventArgs(SelectedBlockIds));
        }
        public void RetainValidSelection()
        {
            var valid=new HashSet<Guid>(WiringDefinition==null?
                Enumerable.Empty<Guid>():WiringDefinition.Blocks.Select(b=>b.Id));
            SetSelection(_selected.Where(valid.Contains).ToArray());
        }
        public void SelectAll()
        {SetSelection(WiringDefinition==null?Enumerable.Empty<Guid>():WiringDefinition.Blocks.Select(b=>b.Id));}
        public void SelectBlock(Guid id,bool additive=false,bool toggle=false)
        {
            var next=additive?new HashSet<Guid>(_selected):new HashSet<Guid>();
            if(toggle&&!next.Add(id))next.Remove(id);
            else next.Add(id);
            SetSelection(next);
        }
        public void SelectRectangle(Rect bounds,bool additive=false)
        {
            var selected=additive?new HashSet<Guid>(_selected):new HashSet<Guid>();
            if(WiringDefinition!=null)foreach(var block in MOLFlowLayout.Build(WiringDefinition))
            {
                if(bounds.IntersectsWith(new Rect(block.X,block.Y,block.Width,block.Height)))
                    selected.Add(block.BlockId);
            }
            SetSelection(selected);
        }
        public void BeginMarquee(Point position,bool additive=false)
        {
            _marqueeActive=true;_marqueeAdditive=additive;
            _marqueeStart=position;_marqueeEnd=position;InvalidateVisual();
        }
        public void UpdateMarquee(Point position)
        {
            if(!_marqueeActive)return;
            _marqueeEnd=position;InvalidateVisual();
        }
        public void CompleteMarquee(Point position)
        {
            if(!_marqueeActive)return;
            UpdateMarquee(position);
            _marqueeActive=false;
            Rect bounds=MarqueeRect();
            if(bounds.Width>=2||bounds.Height>=2)
                SelectRectangle(bounds,_marqueeAdditive);
            else if(!_marqueeAdditive)SetSelection(new Guid[0]);
            InvalidateVisual();
        }
        private Rect MarqueeRect()
        {return new Rect(new Point(Math.Min(_marqueeStart.X,_marqueeEnd.X),
            Math.Min(_marqueeStart.Y,_marqueeEnd.Y)),
            new Point(Math.Max(_marqueeStart.X,_marqueeEnd.X),
                Math.Max(_marqueeStart.Y,_marqueeEnd.Y)));}
        public event EventHandler<MOLPortLinkRequestedEventArgs> PortLinkRequested;
        public event EventHandler<MOLPortLinkRejectedEventArgs> PortLinkRejected;
        private Guid? _sourceForLink;
        public void ResetLinkSelection()
        {
            _sourceForLink=null;_dragSource=null;_dragTarget=null;
            _dragTargetValid=false;_movingBlockId=null;
            _movingIds=new Guid[0];_marqueeActive=false;_moveDelta=new Point();
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
                bool moving=_movingBlockId.HasValue&&_movingIds.Contains(item.BlockId);
                double dx=moving?_moveDelta.X:0;
                double dy=moving?_moveDelta.Y:0;
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
            var item=MOLFlowLayout.Build(WiringDefinition).LastOrDefault(b=>
                position.X>=b.X&&position.X<=b.X+b.Width&&
                position.Y>=b.Y&&position.Y<=b.Y+b.Height);
            if(item==null)return false;
            if(!_selected.Contains(item.BlockId))SetSelection(new[]{item.BlockId});
            _movingBlockId=item.BlockId;
            _movingIds=_selected.OrderBy(id=>id).ToArray();
            _moveOrigin=position;_moveBase=new Point(item.X,item.Y);
            _moveDelta=new Point();InvalidateVisual();
            return true;
        }
        public void UpdateBlockDrag(Point position)
        {
            if(!_movingBlockId.HasValue)return;
            double dx=position.X-_moveOrigin.X,dy=position.Y-_moveOrigin.Y;
            var layout=MOLFlowLayout.Build(WiringDefinition)
                .Where(x=>_movingIds.Contains(x.BlockId)).ToArray();
            if(layout.Length==0)return;
            dx=Math.Max(-layout.Min(x=>x.X),Math.Min(100000-layout.Max(x=>x.X),dx));
            dy=Math.Max(-layout.Min(x=>x.Y),Math.Min(100000-layout.Max(x=>x.Y),dy));
            _moveDelta=new Point(dx,dy);InvalidateVisual();
        }
        public bool CompleteBlockDrag(Point position)
        {
            if(!_movingBlockId.HasValue)return false;
            UpdateBlockDrag(position);
            Guid block=_movingBlockId.Value;
            double x=_moveBase.X+_moveDelta.X,y=_moveBase.Y+_moveDelta.Y;
            bool changed=Math.Abs(_moveDelta.X)>=1||Math.Abs(_moveDelta.Y)>=1;
            var moving=_movingIds;
            _movingBlockId=null;_movingIds=new Guid[0];_moveDelta=new Point();InvalidateVisual();
            if(!changed)return false;
            if(moving.Length>1)
            {
                var many=BlocksMoveRequested;
                if(many!=null)many(this,new MOLBlocksMoveRequestedEventArgs(moving,
                    x-_moveBase.X,y-_moveBase.Y));
            }
            else
            {
                var handler=BlockMoveRequested;
                if(handler!=null)handler(this,new MOLBlockMoveRequestedEventArgs(block,x,y));
            }
            return true;
        }

        public MOLPortVisual HitTestPort(Point position)
        {
            return GetVisiblePorts().LastOrDefault(port=>
                Math.Abs(port.Center.X-position.X)<=8 &&
                Math.Abs(port.Center.Y-position.Y)<=8);
        }

        /// <summary>鼠标事件与WPF自动化共用的拖拽入口；不修改Content。</summary>
        public bool BeginPortDrag(Point position)
        {
            _dragSource=null;
            var port=HitTestPort(position);
            if(port==null||port.Direction!=MPCPortDirection.Output)return false;
            _dragSource=port;_dragTarget=null;_dragTargetValid=false;
            _dragPointer=port.Center;InvalidateVisual();
            return true;
        }

        /// <summary>悬停/拖拽只做真实Core规则预验证；不会写回MOL或MEM。</summary>
        private bool ValidateLink(MOLPortVisual source,MOLPortVisual target,out string reason)
        {
            reason=null;
            if(source==null||target==null||WiringDefinition==null)
            {reason="未命中可连接端口";return false;}
            if(source.Direction!=MPCPortDirection.Output||
                target.Direction!=MPCPortDirection.Input)
            {reason="必须从输出Port连接到输入Port";return false;}
            if(source.Kind!=target.Kind||
                source.Descriptor.TypeId!=target.Descriptor.TypeId||
                !object.Equals(source.Descriptor.Contract,target.Descriptor.Contract)||
                (source.Kind!=MPCPortKind.Control&&source.Kind!=MPCPortKind.Data))
            {reason="Port种类、数据类型或数据契约不匹配";return false;}
            try
            {
                var link=new MOLLink(Guid.NewGuid(),
                    new MOLBlockEndpoint(source.BlockId,source.MemberId),
                    new MOLBlockEndpoint(target.BlockId,target.MemberId),source.Kind);
                var schemas=WiringDefinition.Ports.ToDictionary(
                    pair=>pair.Key,pair=>(IEnumerable<MPCPortDescriptor>)pair.Value);
                new MOLWiringDefinition(WiringDefinition.EntryBlockId,
                    WiringDefinition.Blocks,schemas,WiringDefinition.Links.Concat(new[]{link}));
                return true;
            }
            catch(Exception ex){reason=ex.Message;return false;}
        }

        /// <summary>从真实鼠标Move及Studio自动化测试共用的目标端口预览入口。</summary>
        public bool UpdatePortDrag(Point position)
        {
            if(_dragSource==null)return false;
            _dragPointer=position;
            _dragTarget=HitTestPort(position);
            string problem;
            _dragTargetValid=ValidateLink(_dragSource,_dragTarget,out problem);
            InvalidateVisual();
            return _dragTargetValid;
        }

        public bool CompletePortDrag(Point position)
        {
            MOLPortVisual source=_dragSource;
            MOLPortVisual target=HitTestPort(position);
            _dragSource=null;_dragTarget=null;_dragTargetValid=false;
            InvalidateVisual();
            if(source==null||WiringDefinition==null)return false;
            if(target==null)return false;
            string reason;
            if(!ValidateLink(source,target,out reason))
            {RejectPortLink(reason);return false;}
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
                var positions=_layout.ToDictionary(b=>b.BlockId);
                foreach(var group in WiringDefinition.Groups)
                {
                    var blocks=group.BlockIds.Select(id=>positions[id]).ToArray();
                    double left=blocks.Min(b=>b.X)-12;
                    double top=blocks.Min(b=>b.Y)-12;
                    double right=blocks.Max(b=>b.X+b.Width)+12;
                    double bottom=blocks.Max(b=>b.Y+b.Height)+12;
                    dc.DrawRoundedRectangle(
                        new SolidColorBrush(Color.FromArgb(16,80,118,210)),
                        new Pen(Brushes.SlateBlue,1.2){DashStyle=DashStyles.Dash},
                        new Rect(left,top,right-left,bottom-top),9,9);
                    dc.DrawText(Text(group.Name,Brushes.SlateBlue),
                        new Point(left+9,Math.Max(0,top-15)));
                }
            }
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
                bool isPreview=_movingBlockId.HasValue&&_movingIds.Contains(item.BlockId);
                if(isPreview)dc.PushTransform(new TranslateTransform(_moveDelta.X,_moveDelta.Y));
                string state=null;
                if(RuntimeStates!=null)RuntimeStates.TryGetValue(item.BlockId,out state);
                Brush fill=state=="Completed"?Brushes.Honeydew:state=="Failed"?Brushes.MistyRose:
                    state=="Running"?Brushes.LightBlue:state=="Skipped"?Brushes.LightGray:
                    Brushes.AliceBlue;
                var rect=new Rect(item.X,item.Y,item.Width,item.Height);
                var pen=new Pen(_selected.Contains(item.BlockId)?Brushes.RoyalBlue:Brushes.SteelBlue,
                    _selected.Contains(item.BlockId)?2.8:1.4);
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
                bool candidate=_dragTarget!=null&&
                    port.BlockId==_dragTarget.BlockId&&
                    port.MemberId==_dragTarget.MemberId&&
                    port.Direction==_dragTarget.Direction;
                if(candidate)
                {
                    Brush status=_dragTargetValid?Brushes.MediumSeaGreen:Brushes.IndianRed;
                    dc.DrawEllipse(null,new Pen(status,2.5),
                        port.Center,9,9);
                    brush=status;
                }
                dc.DrawEllipse(Brushes.White,new Pen(brush,2),port.Center,5,5);
                var label=Text(port.MemberId,brush);
                double x=port.Direction==MPCPortDirection.Input?
                    port.Center.X+10:port.Center.X-10-label.Width;
                dc.DrawText(label,new Point(x,port.Center.Y-8));
            }
            if(_dragSource!=null)
                dc.DrawLine(new Pen(_dragTarget==null?Brushes.DarkSlateBlue:
                    (_dragTargetValid?Brushes.MediumSeaGreen:Brushes.IndianRed),2)
                    {DashStyle=DashStyles.Dash},
                    _dragSource.Center,_dragTargetValid&&_dragTarget!=null?
                    _dragTarget.Center:_dragPointer);
            if(_marqueeActive)
            {
                var area=MarqueeRect();
                dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(45,45,118,220)),
                    new Pen(Brushes.CornflowerBlue,1){DashStyle=DashStyles.Dash},area);
            }
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
            for(int i=_layout.Count-1;i>=0;i--)
            {
                var item=_layout[i];
                if(p.X>=item.X&&p.X<=item.X+item.Width&&
                    p.Y>=item.Y&&p.Y<=item.Y+item.Height)
                {
                    if(WiringDefinition!=null)
                    {
                        ModifierKeys modifiers=Keyboard.Modifiers;
                        bool additive=(modifiers&(ModifierKeys.Control|ModifierKeys.Shift))!=0;
                        if(!additive)
                        {
                            var group=WiringDefinition.Groups.FirstOrDefault(g=>
                                g.BlockIds.Contains(item.BlockId));
                            if(group!=null)SetSelection(group.BlockIds);
                            else SelectBlock(item.BlockId);
                        }
                        else SelectBlock(item.BlockId,true,
                            (modifiers&ModifierKeys.Control)!=0);
                    }
                    var handler=BlockSelected;
                    if(handler!=null)handler(this,new MOLBlockSelectedEventArgs(item.BlockId));
                    // Ctrl点击只切换多选状态；不应被拖动入口再次重选。
                    if((Keyboard.Modifiers&ModifierKeys.Control)!=0)
                    {e.Handled=true;return;}
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
                    return;
                }
            }
            if(WiringDefinition!=null&&!_movingBlockId.HasValue)
            {
                BeginMarquee(p,(Keyboard.Modifiers&(ModifierKeys.Control|ModifierKeys.Shift))!=0);
                CaptureMouse();e.Handled=true;
            }
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if(_dragSource!=null)
            {UpdatePortDrag(e.GetPosition(this));e.Handled=true;}
            else if(_movingBlockId.HasValue)
            {UpdateBlockDrag(e.GetPosition(this));e.Handled=true;}
            else if(_marqueeActive)
            {UpdateMarquee(e.GetPosition(this));e.Handled=true;}
        }
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if(_dragSource==null&&!_movingBlockId.HasValue&&!_marqueeActive)return;
            Point position=e.GetPosition(this);
            try
            {
                if(_dragSource!=null)CompletePortDrag(position);
                else if(_movingBlockId.HasValue)CompleteBlockDrag(position);
                else CompleteMarquee(position);
            }
            finally{if(IsMouseCaptured)ReleaseMouseCapture();}
            e.Handled=true;
        }
        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            base.OnLostMouseCapture(e);
            if(_dragSource!=null||_movingBlockId.HasValue||_marqueeActive)
            {_dragSource=null;_dragTarget=null;_dragTargetValid=false;
             _movingBlockId=null;_movingIds=new Guid[0];
             _marqueeActive=false;_moveDelta=new Point();InvalidateVisual();}
        }
    }
}
