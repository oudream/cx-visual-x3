using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Windows.Media.Imaging;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;
using CxVisual.Foundation.Geometry;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Visual;

namespace CxVisual.Mechanism.Presentation.Wpf
{
    /// <summary>WPF技术适配层：只负责输入和投影绘制，不拥有工程真值或历史。</summary>
    public sealed class SceneView : FrameworkElement
    {
        private readonly List<MVISceneItem> _items = new List<MVISceneItem>();
        private Point? _paletteDropGhost;
        // 文本框只暂存WPF正在输入的草稿；正式内容仍由MRCTextElement与MTC持有。
        private readonly Canvas _inlineTextLayer=new Canvas();
        private TextBox _inlineTextBox;
        private Guid? _editingTextId;
        private string _originalInlineText;
        public bool IsInlineTextEditing{get{return _editingTextId.HasValue;}}
        public event Action<Guid,string> InlineTextCommitted;
        /// <summary>设计态原位文字编辑；Esc/取消不提交，Enter提交，Shift+Enter换行。</summary>
        public bool BeginInlineTextEdit(Guid id)
        {
            if(!_designMode||_drawTool!="Select"||IsInlineTextEditing||
                _dragging||_handleDragging||HasDrawPreview)return false;
            MVISceneItem item=_items.Find(x=>x.Id==id&&x.ElementTypeId=="core.text"&&
                x.IsSelectable&&x.Address!=null&&x.Address.InstancePath.Count==0);
            if(item==null||InlineTextCommitted==null)return false;
            CancelInteraction();
            _editingTextId=id;_originalInlineText=item.Text;
            _selection=id;_selectedItems.Clear();_selectedItems.Add(id);
            var input=new TextBox{
                Text=item.Text,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,
                MinWidth=30,MinHeight=24,
                Width=Math.Max(30,item.LocalWidth),Height=Math.Max(24,item.LocalHeight),
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
                BorderBrush=Brushes.DodgerBlue,BorderThickness=new Thickness(1.5),
                Background=Brushes.White,Foreground=Brushes.MidnightBlue,
                FontFamily=new FontFamily(item.Style.FontFamily),
                FontSize=item.Style.FontSize
            };
            System.Windows.Automation.AutomationProperties.SetAutomationId(
                input,"MPW.Visual.InlineText");
            MRCTransform tr=item.WorldTransform;
            input.RenderTransform=new MatrixTransform(
                new Matrix(tr.M11,tr.M21,tr.M12,tr.M22,tr.X,tr.Y));
            input.PreviewKeyDown+=(sender,args)=>{
                if(args.Key==Key.Escape){FinishInlineTextEdit(false);args.Handled=true;}
                else if(args.Key==Key.Enter&&
                    (Keyboard.Modifiers&ModifierKeys.Shift)==0)
                {FinishInlineTextEdit(true);args.Handled=true;}
            };
            input.LostKeyboardFocus+=(sender,args)=>{
                // WPF焦点路由可能在下一次编辑开启后才分发上一次LostFocus；
                // 只允许事件所属TextBox结束自己的会话，禁止旧框误取消新框。
                if(IsInlineTextEditing&&ReferenceEquals(_inlineTextBox,input))
                    FinishInlineTextEdit(_inputWindow==null||_inputWindow.IsActive);
            };
            _inlineTextBox=input;
            _inlineTextLayer.Children.Add(input);
            input.Focus();input.CaretIndex=input.Text.Length;
            InvalidateVisual();
            return true;
        }
        /// <summary>结束编辑先清理临时UI，再向正式MVI发出单次变更意图。</summary>
        public bool FinishInlineTextEdit(bool commit)
        {
            if(!_editingTextId.HasValue)return false;
            Guid id=_editingTextId.Value;
            string before=_originalInlineText;
            string after=_inlineTextBox==null?before:_inlineTextBox.Text;
            _editingTextId=null;_originalInlineText=null;_inlineTextBox=null;
            _inlineTextLayer.Children.Clear();
            InvalidateVisual();
            if(!commit||string.Equals(before,after,StringComparison.Ordinal))return false;
            Action<Guid,string> handler=InlineTextCommitted;
            if(handler!=null){handler(id,after);return true;}
            return false;
        }
        // 仅订阅所属Window的生命周期；不保存工程编辑状态或第二套手势。
        private Window _inputWindow;
        private void OnHostLoaded(object sender, RoutedEventArgs e)
        {
            Window host=Window.GetWindow(this);
            if(ReferenceEquals(host,_inputWindow))return;
            if(_inputWindow!=null)_inputWindow.Deactivated-=OnHostDeactivated;
            _inputWindow=host;
            if(_inputWindow!=null)_inputWindow.Deactivated+=OnHostDeactivated;
        }
        private void OnHostUnloaded(object sender, RoutedEventArgs e)
        {
            CancelInteraction();
            if(_inputWindow!=null)_inputWindow.Deactivated-=OnHostDeactivated;
            _inputWindow=null;
        }
        private void OnHostDeactivated(object sender, EventArgs e)
        {
            // 多点绘制没有鼠标捕获，窗口失活仍必须清除悬空Ghost。
            CancelInteraction();
        }
        private void OnCanvasIsVisibleChanged(object sender,
            DependencyPropertyChangedEventArgs e)
        {
            // Tab切换/隐藏工作区不触发LostCapture，但属于GE-12取消边界。
            if(!IsVisible)CancelInteraction();
        }
        private Guid? _selection;
        // Draw只保存瞬时指针和Ghost数据；工程Shape创建交回MVI/MEM唯一事务。
        private string _drawTool="Select";
        private bool _drawDragging;
        private readonly List<FGEPoint2D> _drawPoints=new List<FGEPoint2D>();
        private readonly List<int> _drawContourStarts=new List<int>();
        private bool _nextPathContour;
        /// <summary>Path点绘时N开始另一独立轮廓，未完成轮廓绝不提交工程。</summary>
        public bool BeginNextPathContour()
        {
            if(_drawTool!="Path"||!_designMode||!HasDrawPreview||_nextPathContour)return false;
            int start=_drawContourStarts.Count==0?0:
                _drawContourStarts[_drawContourStarts.Count-1];
            if(_drawPoints.Count-start<2)return false;
            _nextPathContour=true;InvalidateVisual();return true;
        }

        /// <summary>
        /// Backspace仅撤销当前Polyline/Polygon/Path草稿顶点，不进入MEM Undo。
        /// Path的N待建轮廓先撤销等待；新轮廓退回到零点时移除分界，
        /// 后续Backspace仍可回退前一轮廓，避免形成不可完成的孤立Move。
        /// </summary>
        public bool RemoveLastDrawPoint()
        {
            if(!_designMode||!IsPointDrawTool(_drawTool)||!HasDrawPreview||
                IsInlineTextEditing)return false;
            if(_nextPathContour)_nextPathContour=false;
            else
            {
                _drawPoints.RemoveAt(_drawPoints.Count-1);
                if(_drawContourStarts.Count>0&&
                    _drawPoints.Count==_drawContourStarts[_drawContourStarts.Count-1])
                {
                    _drawContourStarts.RemoveAt(_drawContourStarts.Count-1);
                    _nextPathContour=true;
                }
            }
            if(_drawPoints.Count==0)
            {
                _drawContourStarts.Clear();
                _nextPathContour=false;
            }
            InvalidateVisual();
            return true;
        }
        private List<IReadOnlyList<FGEPoint2D>> DrawContours(IReadOnlyList<FGEPoint2D> points)
        {
            var contours=new List<IReadOnlyList<FGEPoint2D>>();
            int start=0;
            foreach(int next in _drawContourStarts)
            {
                contours.Add(points.Skip(start).Take(next-start).ToArray());
                start=next;
            }
            contours.Add(points.Skip(start).ToArray());
            return contours;
        }
        private Point _drawPointer;
        public string ActiveDrawTool {get{return _drawTool;}}
        public bool HasDrawPreview {get{return _drawDragging||_drawPoints.Count>0;}}
        public event Action<string> DrawToolChanged;
        public event Action<MRCShapeKind,IReadOnlyList<FGEPoint2D>,bool> DrawShapeRequested;
        public event Action<IReadOnlyList<IReadOnlyList<FGEPoint2D>>> DrawPathContoursRequested;
        public event Action<IReadOnlyList<FGEPoint2D>> DrawTextRequested;
        public event Action<string,IReadOnlyList<FGEPoint2D>> DrawWidgetRequested;
        private static bool IsWidgetDrawTool(string tool)
        {return tool=="WidgetButton"||MRCWidgetTypes.IsBuiltIn(tool);}
        private static bool IsPointDrawTool(string tool)
        {return tool=="Polyline"||tool=="Polygon"||tool=="Path";}
        public void SetDrawTool(string tool)
        {
            if(tool!="Select"&&tool!="Text"&&!IsWidgetDrawTool(tool))
            {
                MRCShapeKind kind;
                if(!Enum.TryParse(tool,false,out kind)||!Enum.IsDefined(typeof(MRCShapeKind),kind))
                    throw new ArgumentException("visual.draw.unsupported_tool","tool");
            }
            CancelInteraction();
            _geometryEditMode=false;
            if(!_designMode&&tool!="Select")
                throw new InvalidOperationException("visual.draw.requires_design_mode");
            _drawTool=tool;
            Cursor=tool=="Select"?Cursors.Arrow:Cursors.Cross;
            InvalidateVisual();
            Action<string> handler=DrawToolChanged;if(handler!=null)handler(tool);
        }
        private Point SnapDrawing(Point p)
        {return _gridSnap?new Point(Math.Round(p.X/25)*25,Math.Round(p.Y/25)*25):p;}
        /// <summary>WPF鼠标和可替换的触笔/触控输入共同使用的绘制手势入口。</summary>
        public void BeginDrawGesture(double worldX,double worldY)
        {
            if(!_designMode||_drawTool=="Select")
                throw new InvalidOperationException("visual.draw.tool_not_active");
            Point p=SnapDrawing(new Point(worldX,worldY));
            _drawPointer=p;
            if(IsPointDrawTool(_drawTool))
            {
                FGEPoint2D point=new FGEPoint2D(p.X,p.Y);
                if(_drawTool=="Path"&&_nextPathContour)
                {
                    _drawContourStarts.Add(_drawPoints.Count);
                    _nextPathContour=false;
                    _drawPoints.Add(point);
                }
                else if(_drawPoints.Count==0||
                    Math.Abs(_drawPoints[_drawPoints.Count-1].X-point.X)>1e-9||
                    Math.Abs(_drawPoints[_drawPoints.Count-1].Y-point.Y)>1e-9)
                    _drawPoints.Add(point);
            }
            else
            {
                _drawPoints.Clear();
                _drawPoints.Add(new FGEPoint2D(p.X,p.Y));
                _drawDragging=true;
            }
            InvalidateVisual();
        }
        public void UpdateDrawGesture(double worldX,double worldY)
        {
            if(!HasDrawPreview)return;
            _drawPointer=SnapDrawing(new Point(worldX,worldY));
            InvalidateVisual();
        }
        /// <summary>释放矩形/线鼠标或双击/Enter结束多点绘制；无效几何不发创建事件。</summary>
        public bool CompleteDrawGesture(double worldX,double worldY,bool constrain=false)
        {
            if(_drawTool=="Select"||!HasDrawPreview)return false;
            UpdateDrawGesture(worldX,worldY);
            bool pointsMode=IsPointDrawTool(_drawTool);
            var points=new List<FGEPoint2D>(_drawPoints);
            if(!pointsMode)points.Add(new FGEPoint2D(_drawPointer.X,_drawPointer.Y));
            // N只表示等待下一条轮廓的首点；如果直接按Enter或双击，
            // 应完成此前合法轮廓，而不是强迫用户再画一条空轮廓。
            if(pointsMode&&points.Count<(_drawTool=="Polygon"?3:2))return false;
            bool multipleContours=pointsMode&&_drawTool=="Path"&&
                _drawContourStarts.Count>0;
            List<IReadOnlyList<FGEPoint2D>> contours=multipleContours?
                DrawContours(points):null;
            if(!pointsMode&&_drawTool!="Text"&&!IsWidgetDrawTool(_drawTool)&&
                points[0].DistanceTo(points[1])<3/_zoom)
            {
                _drawDragging=false;_drawPoints.Clear();InvalidateVisual();
                return false;
            }
            MRCShapeKind kind=MRCShapeKind.Rect;
            try
            {
                double x,y,w,h;
                if(_drawTool=="Text")MVIShapeCatalog.TextBounds(points,out x,out y,out w,out h);
                else if(IsWidgetDrawTool(_drawTool))
                    MVIShapeCatalog.WidgetBounds(points,out x,out y,out w,out h);
                else
                {
                    kind=(MRCShapeKind)Enum.Parse(typeof(MRCShapeKind),_drawTool);
                    if(multipleContours)MVIShapeCatalog.CreateDrawnContours(
                        contours,out x,out y);
                    else MVIShapeCatalog.CreateDrawn(kind,points,constrain,out x,out y);
                }
            }
            catch(ArgumentException)
            {
                if(!pointsMode){_drawDragging=false;_drawPoints.Clear();InvalidateVisual();}
                return false;
            }
            _drawDragging=false;_drawPoints.Clear();
            _drawContourStarts.Clear();_nextPathContour=false;
            if(IsMouseCaptured)ReleaseMouseCapture();
            InvalidateVisual();
            if(_drawTool=="Text")
            {
                var placed=DrawTextRequested;
                if(placed!=null)placed(points.AsReadOnly());
                return placed!=null;
            }
            if(IsWidgetDrawTool(_drawTool))
            {
                var placed=DrawWidgetRequested;
                if(placed!=null)
                    placed(_drawTool=="WidgetButton"?"core.widget":_drawTool,
                        points.AsReadOnly());
                return placed!=null;
            }
            if(multipleContours)
            {
                var placed=DrawPathContoursRequested;
                if(placed!=null)placed(contours.AsReadOnly());
                return placed!=null;
            }
            var handler=DrawShapeRequested;
            if(handler!=null)handler(kind,points.AsReadOnly(),constrain);
            return handler!=null;
        }
        private Point _dragStart;
        private double _dragX;
        private double _dragY;
        private bool _dragging;
        private readonly HashSet<Guid> _selectedItems=new HashSet<Guid>();
        public IReadOnlyList<Guid> SelectedVisualIds
        {get{return new List<Guid>(_selectedItems).AsReadOnly();}}
        /// <summary>由Studio组织选择结果回写画布，解决双向通知时只剩最后一个的多选丢失。</summary>
        public void SetSelectedVisualIds(IEnumerable<Guid> ids)
        {
            if(ids==null)throw new ArgumentNullException("ids");
            _selectedItems.Clear();
            Guid? last=null;
            foreach(Guid id in ids)
            {
                if(!_items.Exists(x=>x.Id==id&&x.IsSelectable))continue;
                if(_selectedItems.Add(id))last=id;
            }
            _selection=last;
            if(_selection.HasValue&&_selection==_hoverConnectId)_hoverConnectId=null;
            InvalidateVisual();
        }
        private bool _marquee;
        private Point _marqueeStart,_marqueeEnd;
        public event Action<IReadOnlyList<Guid>> SelectionSetRequested;
        /// <summary>方向键请求仅携带稳定选区Id与世界位移；MVI决定实际MEM提交和拒绝。</summary>
        public event Action<IReadOnlyList<Guid>,double,double> NudgeSelectionRequested;
        // 几何节点编辑与对象外框变换互斥，双击Shape进入；Esc退出。
        private bool _geometryEditMode;
        private Guid? _hoverConnectId,_connectSourceId,_connectTargetId;
        private string _connectSourceSide,_connectTargetSide;
        private bool _connectDragging;
        private Point _connectPointer;
        /// <summary>可视锚点拖线完成事件，由宿主调用既有MEM/MPC事务。</summary>
        public event Action<Guid,string,Guid,string> ConnectRequested;
        /// <summary>拖动可视连接端点；原MPC连接Id保持不变。</summary>
        public event Action<Guid,bool,Guid,string> ReconnectRequested;
        public event Action<Guid> DeleteConnectionRequested;
        public event Action<Guid,int,double,double> InsertWaypointRequested;
        public event Action<Guid,int,double,double> MoveWaypointRequested;
        public event Action<Guid,int> RemoveWaypointRequested;
        /// <summary>Ctrl+双击几何编辑曲线段，仅提交稳定Id、命令索引和原参数t。</summary>
        public event Action<Guid,int,double> PathSegmentSplitRequested;
        public event Action<Guid,int,string> PathSegmentOperationRequested;
        public event Action<Guid,MPCVisualRouteKind> VisualRouteRequested;
        /// <summary>多对象控制柄只提交一次MEM事务；拖动仅产生画布预览。</summary>
        public event Action<IReadOnlyList<Guid>,int,double,double,double,bool,bool,bool>
            SelectionTransformRequested;
        private bool _selectionTransformDragging;
        private int _selectionHandleIndex;
        private Point _selectionHandleStart,_selectionHandlePointer;
        private bool _waypointDragging;
        private Guid? _waypointConnectionId;
        private int _waypointIndex;
        private Point _waypointStart,_waypointPointer;
        private bool _rewireDragging,_rewireSource;
        private Guid? _rewireConnectionId;
        private Point _rewirePointer;
        public bool IsGeometryEditMode { get { return _geometryEditMode; } }
        /// <summary>与双击切换同一个几何编辑状态，便于WPF工具和操作测试共用。</summary>
        public bool EnterGeometryEditModeForSelection()
        {
            if(!_designMode||_drawTool!="Select"||!_selection.HasValue||
                _selectedItems.Count!=1||!_selectedItems.Contains(_selection.Value))
                return false;
            MVISceneItem item=_items.Find(x=>x.Id==_selection.Value);
            if(item==null||item.Shape==null||!item.IsSelectable||item.Address==null||
                item.Address.InstancePath.Count!=0)return false;
            _geometryEditMode=true;InvalidateVisual();return true;
        }
        /// <summary>WPF手势与STA回归共用的世界坐标分段请求。未命中不修改工程。</summary>
        public bool RequestSelectedPathSplitAt(double worldX,double worldY)
        {
            if(!_designMode||_drawTool!="Select"||!_geometryEditMode||
                !_selection.HasValue||_selectedItems.Count!=1||
                ! _selectedItems.Contains(_selection.Value)||
                PathSegmentSplitRequested==null||_handleDragging||_dragging||
                HasDrawPreview)return false;
            MVISceneItem item=_items.Find(x=>x.Id==_selection.Value);
            if(item==null||item.Address==null||item.Address.InstancePath.Count!=0)
                return false;
            int index;double t;
            if(!MVIVisualEditing.TryHitPathSplitSegment(item,worldX,worldY,
                8/_zoom,out index,out t))return false;
            // 精细锚点和控制点附近绝不把双击误解释为插点。
            if(MVIVisualEditing.HitHandle(item,worldX,worldY,8/_zoom,true,_zoom)!=null)
                return false;
            PathSegmentSplitRequested(item.Id,index,t);
            return true;
        }
        /// <summary>
        /// 几何右键菜单不保存工程对象；每次执行前校验当前选区/模式/命令，复用MVI事务。
        /// </summary>
        private static bool CanApplyPathOperation(MVISceneItem item,int index,string operation)
        {
            if(item==null||item.Shape==null||index<0||
                index>=item.Shape.Geometry.Commands.Count)return false;
            FGEPath2D path=item.Shape.Geometry;
            try
            {
                switch(operation)
                {
                    case "delete":FGEPathEditing.DeleteAnchor(path,index);return true;
                    case "line":return path.Commands[index].Verb!=FGEPathVerb.Line&&
                        FGEPathEditing.ConvertSegment(path,index,FGEPathVerb.Line)!=null;
                    case "quadratic":return path.Commands[index].Verb!=FGEPathVerb.Quadratic&&
                        FGEPathEditing.ConvertSegment(path,index,FGEPathVerb.Quadratic)!=null;
                    case "cubic":return path.Commands[index].Verb!=FGEPathVerb.Cubic&&
                        FGEPathEditing.ConvertSegment(path,index,FGEPathVerb.Cubic)!=null;
                    case "open":
                    case "close":
                        if(item.Shape.Kind!=MRCShapeKind.Path)return false;
                        return !object.ReferenceEquals(path,FGEPathEditing.SetContourClosed(
                            path,index,operation=="close"));
                    default:return false;
                }
            }
            catch(ArgumentException){return false;}
        }
        public bool RequestSelectedPathOperation(Guid id,int commandIndex,string operation)
        {
            if(!_designMode||_drawTool!="Select"||!_geometryEditMode||
                !_selection.HasValue||_selection.Value!=id||_selectedItems.Count!=1||
                !_selectedItems.Contains(id)||PathSegmentOperationRequested==null||
                _dragging||_handleDragging||HasDrawPreview)return false;
            MVISceneItem item=_items.Find(x=>x.Id==id);
            if(item==null||item.Shape==null||!item.IsSelectable||item.Address==null||
                item.Address.InstancePath.Count!=0||commandIndex<0||
                commandIndex>=item.Shape.Geometry.Commands.Count)return false;
            FGEPathCommand cmd=item.Shape.Geometry.Commands[commandIndex];
            if(operation=="delete"&&cmd.Verb!=FGEPathVerb.Line)return false;
            if(operation=="line"||operation=="quadratic"||operation=="cubic")
            {
                if(cmd.Verb!=FGEPathVerb.Line&&cmd.Verb!=FGEPathVerb.Quadratic&&
                    cmd.Verb!=FGEPathVerb.Cubic)return false;
                if(operation=="line"&&cmd.Verb==FGEPathVerb.Line||
                    operation=="quadratic"&&cmd.Verb==FGEPathVerb.Quadratic||
                    operation=="cubic"&&cmd.Verb==FGEPathVerb.Cubic)return false;
            }
            else if(operation=="close"||operation=="open")
            {
                if(item.Shape.Kind!=MRCShapeKind.Path||cmd.Verb==FGEPathVerb.Close)
                    return false;
            }
            else if(operation!="delete")return false;
            // 无效闭合/最小顶点删除在WPF层提前阻止，MVI/MEM依旧负责最终鉴权。
            if(!CanApplyPathOperation(item,commandIndex,operation))return false;
            PathSegmentOperationRequested(id,commandIndex,operation);
            return true;
        }
        /// <summary>当前轮廓是否已有Close，不能因多子路径搜索误关闭相邻轮廓。</summary>
        private static bool IsContourClosed(IReadOnlyList<FGEPathCommand> commands,int index)
        {
            for(int i=index+1;i<commands.Count;i++)
            {
                if(commands[i].Verb==FGEPathVerb.Close)return true;
                if(commands[i].Verb==FGEPathVerb.Move)return false;
            }
            return false;
        }
        private static MenuItem GeometryMenuItem(string title,Action action)
        {
            var menu=new MenuItem{Header=title};
            menu.Click+=(sender,args)=>action();
            return menu;
        }
        /// <summary>画布坐标下构建H5右键操作；传入世界坐标便于WPF STA验证。</summary>
        public ContextMenu CreateSelectedGeometryMenuAt(double x,double y)
        {
            if(!_designMode||_drawTool!="Select"||!_geometryEditMode||
                !_selection.HasValue||_selectedItems.Count!=1||
                !_selectedItems.Contains(_selection.Value))return null;
            Guid id=_selection.Value;
            MVISceneItem item=_items.Find(v=>v.Id==id);
            if(item==null||item.Shape==null||!item.IsSelectable||
                item.Address==null||item.Address.InstancePath.Count!=0)return null;
            MVIVisualHandle handle=MVIVisualEditing.HitHandle(
                item,x,y,8/_zoom,true,_zoom);
            FGEPathCommand cmd;int index;double fraction;
            var menu=new ContextMenu();
            if(handle!=null&&handle.Kind==MVIVisualHandleKind.Anchor)
            {
                index=handle.CommandIndex;
                if(CanApplyPathOperation(item,index,"delete"))
                    menu.Items.Add(GeometryMenuItem("删除顶点",()=>
                        RequestSelectedPathOperation(id,index,"delete")));
            }
            else if(MVIVisualEditing.TryHitPathSplitSegment(item,x,y,8/_zoom,
                out index,out fraction))
            {
                if(PathSegmentSplitRequested!=null)
                    menu.Items.Add(GeometryMenuItem("精确插入锚点",()=>
                    {
                        if(_selection==id&&_geometryEditMode)
                            PathSegmentSplitRequested(id,index,fraction);
                    }));
                cmd=item.Shape.Geometry.Commands[index];
                if(PathSegmentOperationRequested!=null)
                {
                    if(CanApplyPathOperation(item,index,"line"))
                        menu.Items.Add(GeometryMenuItem("转为直线段",()=>
                            RequestSelectedPathOperation(id,index,"line")));
                    if(CanApplyPathOperation(item,index,"quadratic"))
                        menu.Items.Add(GeometryMenuItem("转为二次曲线",()=>
                            RequestSelectedPathOperation(id,index,"quadratic")));
                    if(CanApplyPathOperation(item,index,"cubic"))
                        menu.Items.Add(GeometryMenuItem("转为三次贝塞尔",()=>
                            RequestSelectedPathOperation(id,index,"cubic")));
                    if(item.Shape.Kind==MRCShapeKind.Path)
                    {
                        bool closed=IsContourClosed(item.Shape.Geometry.Commands,index);
                        string operation=closed?"open":"close";
                        if(CanApplyPathOperation(item,index,operation))
                            menu.Items.Add(GeometryMenuItem(
                                closed?"断开此轮廓":"闭合此轮廓",()=>
                                RequestSelectedPathOperation(id,index,operation)));
                    }
                }
            }
            return menu.Items.Count==0?null:menu;
        }
        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonUp(e);
            Point p=WorldPoint(e.GetPosition(this));
            ContextMenu menu=CreateSelectedGeometryMenuAt(p.X,p.Y);
            if(menu==null)return;
            // 由WPF正式Popup负责右键交互；失焦不会提交几何草稿。
            menu.PlacementTarget=this;
            menu.Placement=System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.IsOpen=true;
            e.Handled=true;
        }
        private MVIVisualHandle _activeHandle;
        private bool _handleDragging;
        private Point _handlePointer;
        public event EventHandler<MPWVisualHandleEventArgs> HandleEditRequested;
        private readonly VisualCollection _visualChildren;
        private readonly Canvas _widgetCanvas;
        private Func<Guid,Stream> _assetResolver;
        private readonly Dictionary<Guid,ImageSource> _assetCache=new Dictionary<Guid,ImageSource>();
        private readonly Dictionary<Guid,string> _assetErrors=new Dictionary<Guid,string>();
        public IReadOnlyDictionary<Guid,string> AssetErrors{get{return _assetErrors;}}
        public void SetAssetResolver(Func<Guid,Stream> resolver)
        {
            _assetResolver=resolver;
            _assetCache.Clear();_assetErrors.Clear();InvalidateVisual();
        }
        private ImageSource AssetImage(MVISceneItem item)
        {
            if(!item.AssetId.HasValue||_assetResolver==null)return null;
            Guid id=item.AssetId.Value;
            ImageSource found;if(_assetCache.TryGetValue(id,out found))return found;
            if(_assetErrors.ContainsKey(id))return null;
            try
            {
                using(Stream stream=_assetResolver(id))
                {
                    if(stream==null)throw new InvalidDataException("visual.asset.unavailable");
                    found=item.ElementTypeId=="core.svg"?
                        (ImageSource)MPWVisualAssetRenderer.DecodeSvg(stream):
                        MPWVisualAssetRenderer.DecodeBitmap(stream,item.MediaType);
                }
                _assetCache.Add(id,found);
                return found;
            }
            catch(Exception ex)
            {
                _assetErrors[id]=ex.GetType().Name+": "+ex.Message;
                return null; // 画面显示资源错误而不是使整个WPF Designer崩溃
            }
        }
        private bool _designMode=true;
        private bool _resetWidgetLayerOnNextScene;
        private double _zoom=1,_panX,_panY;
        private bool _panning;private Point _panPointer;
        private bool _gridSnap;
        private bool _smartGuides=true;
        private MVIVisualSnapResult _dragSnap;
        /// <summary>智能参考线仅在当前拖动中计算，不构成任何MEM工程状态。</summary>
        public bool SmartGuides {get{return _smartGuides;}set{_smartGuides=value;InvalidateVisual();}}
        public double Zoom{get{return _zoom;}}
        public bool SnapToGrid{get{return _gridSnap;}set{_gridSnap=value;}}
        /// <summary>资源画刷仅从宿主WPF主题或授权的本地Provider获取，绝不从ResourceKey加载外部地址。</summary>
        public Func<string,Brush> ResourceBrushResolver{get;set;}
        private Brush ResolveStyleBrush(string key)
        {
            if(string.IsNullOrWhiteSpace(key))return null;
            if(ResourceBrushResolver!=null)return ResourceBrushResolver(key);
            object value=TryFindResource(key);
            return value as Brush;
        }
        public void SetViewport(double zoom,double panX,double panY)
        {
            if(double.IsNaN(zoom)||double.IsInfinity(zoom)||zoom<0.05||zoom>16||
                double.IsNaN(panX)||double.IsNaN(panY)||double.IsInfinity(panX)||double.IsInfinity(panY))
                throw new ArgumentOutOfRangeException("zoom");
            _zoom=zoom;_panX=panX;_panY=panY;
            _widgetCanvas.RenderTransform=new MatrixTransform(
                new Matrix(zoom,0,0,zoom,panX,panY));
            _inlineTextLayer.RenderTransform=new MatrixTransform(
                new Matrix(zoom,0,0,zoom,panX,panY));
            InvalidateVisual();
        }
        private Point WorldPoint(Point screen)
        {return new Point((screen.X-_panX)/_zoom,(screen.Y-_panY)/_zoom);}
        private Point ScreenPoint(Point world)
        {return new Point(world.X*_zoom+_panX,world.Y*_zoom+_panY);}
        /// <summary>预览和鼠标松开的提交共用同一世界位移，避免吸附后图元跳动。</summary>
        private MVIVisualSnapResult ResolveDrag(double dx,double dy)
        {
            MVISceneItem[] selected=EditableSelection();
            if(selected.Length==0||selected.Length!=_selectedItems.Count)
                return new MVIVisualSnapResult(dx,dy,null,null);
            return MVIVisualSnapping.Resolve(_items,
                selected.Select(item=>item.Id),dx,dy,_zoom,_gridSnap,_smartGuides);
        }
        private static readonly string[] ConnectionSides=
            {"north","east","south","west"};
        private MVISceneItem Connectable(Guid id)
        {
            MVISceneItem item=_items.Find(x=>x.Id==id);
            return item!=null&&item.IsSelectable&&item.Address!=null&&
                item.Address.InstancePath.Count==0&&
                item.ElementTypeId!="core.visual.connector"?item:null;
        }
        /// <summary>端口位置与核心MVIVisualAnchors使用同一几何计算；偏移采用DIP。</summary>
        private Point QuickArrowPoint(MVISceneItem item,string side)
        {
            FGEPoint2D anchor=MVIVisualAnchors.At(item,side);
            FGEPoint2D middle=item.WorldTransform.ToGeometry().TransformPoint(
                new FGEPoint2D(item.LocalWidth/2,item.LocalHeight/2));
            double dx=anchor.X-middle.X,dy=anchor.Y-middle.Y;
            double length=Math.Sqrt(dx*dx+dy*dy);
            if(length<1e-8){dx=1;dy=0;length=1;}
            return new Point(anchor.X+dx*22/(_zoom*length),
                anchor.Y+dy*22/(_zoom*length));
        }
        private MVISceneItem[] EditableSelection()
        {
            var result=new List<MVISceneItem>();
            foreach(MVISceneItem item in _items)
                if(_selectedItems.Contains(item.Id)&&item.IsSelectable&&
                    item.ElementTypeId!="core.visual.connector")result.Add(item);
            return result.ToArray();
        }
        private bool TryHitSelectionHandle(Point point,out int index)
        {
            index=-1;
            MVISceneItem[] selected=EditableSelection();
            if(selected.Length<2||SelectionTransformRequested==null)return false;
            FGERect2D bounds=MVISelectionTransform.Bounds(selected);
            double radius=9/_zoom,dist=radius*radius;
            for(int i=0;i<9;i++)
            {
                FGEPoint2D p=MVISelectionTransform.Handle(bounds,i,_zoom);
                double dx=p.X-point.X,dy=p.Y-point.Y;
                double d=dx*dx+dy*dy;
                if(d<=dist){dist=d;index=i;}
            }
            return index>=0;
        }

        /// <summary>中间顶点命中；首尾端点属于重连，不属于折点。</summary>
        private bool TryHitConnectionWaypoint(Point point,out Guid id,out int index)
        {
            id=Guid.Empty;index=-1;
            if(!_selection.HasValue)return false;
            MVISceneItem item=_items.Find(x=>x.Id==_selection.Value&&
                x.ElementTypeId=="core.visual.connector"&&x.IsSelectable);
            if(item==null||item.Shape==null)return false;
            var commands=item.Shape.Geometry.Commands;
            double radius2=64/(_zoom*_zoom);
            for(int i=1;i<commands.Count-1;i++)
            {
                FGEPoint2D p=item.GeometryToWorld.TransformPoint(commands[i].End);
                double dx=p.X-point.X,dy=p.Y-point.Y;
                if(dx*dx+dy*dy<=radius2)
                {id=item.Id;index=i-1;return true;}
            }
            return false;
        }
        /// <summary>真实路径段命中；不使用外框冒充连接线路径。</summary>
        private bool TryHitConnectorSegment(Point point,out Guid id,out int index)
        {
            id=Guid.Empty;index=-1;
            if(!_selection.HasValue)return false;
            MVISceneItem item=_items.Find(x=>x.Id==_selection.Value&&
                x.ElementTypeId=="core.visual.connector"&&x.IsSelectable);
            if(item==null||item.Shape==null)return false;
            var commands=item.Shape.Geometry.Commands;
            double best=49/(_zoom*_zoom);
            for(int i=0;i<commands.Count-1;i++)
            {
                FGEPoint2D a=item.GeometryToWorld.TransformPoint(commands[i].End);
                FGEPoint2D b=item.GeometryToWorld.TransformPoint(commands[i+1].End);
                double dx=b.X-a.X,dy=b.Y-a.Y;
                double length2=dx*dx+dy*dy;
                double t=length2<=1e-16?0:Math.Max(0,Math.Min(1,
                    ((point.X-a.X)*dx+(point.Y-a.Y)*dy)/length2));
                double ex=a.X+t*dx-point.X,ey=a.Y+t*dy-point.Y;
                double d=ex*ex+ey*ey;
                if(d<=best){best=d;id=item.Id;index=i;}
            }
            return index>=0;
        }
        private bool TryHitConnectionEnd(Point p,out Guid connector,out bool source)
        {
            connector=Guid.Empty;source=false;
            if(!_selection.HasValue)return false;
            MVISceneItem current=_items.Find(x=>x.Id==_selection.Value&&
                x.ElementTypeId=="core.visual.connector"&&x.IsSelectable);
            if(current==null||current.Shape==null||
                current.Shape.Geometry.Commands.Count<2)return false;
            FGEPoint2D first=current.GeometryToWorld.TransformPoint(
                current.Shape.Geometry.Commands[0].End);
            FGEPoint2D last=current.GeometryToWorld.TransformPoint(
                current.Shape.Geometry.Commands[
                    current.Shape.Geometry.Commands.Count-1].End);
            double threshold=9/_zoom;
            double a=Math.Pow(first.X-p.X,2)+Math.Pow(first.Y-p.Y,2);
            double b=Math.Pow(last.X-p.X,2)+Math.Pow(last.Y-p.Y,2);
            if(Math.Min(a,b)>threshold*threshold)return false;
            connector=current.Id;source=a<=b;return true;
        }
        private bool TryHitQuickArrow(Point point,out Guid id,out string side)
        {
            id=Guid.Empty;side=null;
            if(!_designMode||ConnectRequested==null||!_hoverConnectId.HasValue||
                _selectedItems.Contains(_hoverConnectId.Value))return false;
            MVISceneItem item=Connectable(_hoverConnectId.Value);
            if(item==null)return false;
            foreach(string candidate in ConnectionSides)
            {
                Point arrow=QuickArrowPoint(item,candidate);
                double dx=point.X-arrow.X,dy=point.Y-arrow.Y;
                if(dx*dx+dy*dy<=100/(_zoom*_zoom))
                {id=item.Id;side=candidate;return true;}
            }
            return false;
        }
        private void UpdateConnectionHover(Point point)
        {
            Guid? next=null;
            if(_designMode&&ConnectRequested!=null&&!_geometryEditMode)
            {
                for(int i=_items.Count-1;i>=0;i--)
                {
                    MVISceneItem item=_items[i];
                    if(Connectable(item.Id)!=null&&
                        !_selectedItems.Contains(item.Id)&&
                        item.Bounds.Contains(new FGEPoint2D(point.X,point.Y)))
                    {next=item.Id;break;}
                }
                if(!next.HasValue&&_hoverConnectId.HasValue)
                {
                    MVISceneItem existing=Connectable(_hoverConnectId.Value);
                    if(existing!=null&&!_selectedItems.Contains(existing.Id))
                        foreach(string candidate in ConnectionSides)
                        {
                            FGEPoint2D anchor=MVIVisualAnchors.At(existing,candidate);
                            double dx=point.X-anchor.X,dy=point.Y-anchor.Y;
                            if(dx*dx+dy*dy<=(34/_zoom)*(34/_zoom))
                            {next=existing.Id;break;}
                        }
                }
            }
            if(next!=_hoverConnectId){_hoverConnectId=next;InvalidateVisual();}
        }
        private void UpdateConnectionDrag(Point pointer)
        {
            _connectPointer=pointer;
            _connectTargetId=null;_connectTargetSide=null;
            for(int i=_items.Count-1;i>=0;i--)
            {
                MVISceneItem item=_items[i];
                if(Connectable(item.Id)==null||item.Id==_connectSourceId||
                    !item.Bounds.Contains(new FGEPoint2D(pointer.X,pointer.Y)))continue;
                double best=double.PositiveInfinity;
                foreach(string side in ConnectionSides)
                {
                    FGEPoint2D anchor=MVIVisualAnchors.At(item,side);
                    double dx=pointer.X-anchor.X,dy=pointer.Y-anchor.Y;
                    double dist=dx*dx+dy*dy;
                    if(dist<best)
                    {best=dist;_connectTargetId=item.Id;_connectTargetSide=side;}
                }
                break;
            }
            InvalidateVisual();
        }
        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            if(!IsDesignMode){base.OnMouseWheel(e);return;}
            Point center=e.GetPosition(this);double before=_zoom;
            double next=Math.Max(0.05,Math.Min(16,_zoom*(e.Delta>0?1.15:1/1.15)));
            Point target=WorldPoint(center);
            SetViewport(next,center.X-target.X*next,center.Y-target.Y*next);
            e.Handled=true;
        }
        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            if(_designMode&&e.ChangedButton==MouseButton.Middle)
            {_panning=true;_panPointer=e.GetPosition(this);CaptureMouse();e.Handled=true;return;}
            base.OnMouseDown(e);
        }
        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            if(_panning&&e.ChangedButton==MouseButton.Middle)
            {_panning=false;if(IsMouseCaptured)ReleaseMouseCapture();e.Handled=true;return;}
            base.OnMouseUp(e);
        }
        public SceneView()
        {
            _widgetCanvas=new Canvas();
            _visualChildren=new VisualCollection(this);
            _visualChildren.Add(_widgetCanvas);
            _visualChildren.Add(_inlineTextLayer);
            Focusable=true;
            Loaded+=OnHostLoaded;
            Unloaded+=OnHostUnloaded;
            IsVisibleChanged+=OnCanvasIsVisibleChanged;
            AllowDrop=true;
        }
        protected override int VisualChildrenCount{get{return _visualChildren.Count;}}
        protected override System.Windows.Media.Visual GetVisualChild(int index)
        {
            if(index<0||index>=_visualChildren.Count)throw new ArgumentOutOfRangeException("index");
            return _visualChildren[index];
        }
        protected override Size MeasureOverride(Size availableSize)
        {
            double w=double.IsInfinity(availableSize.Width)?720:availableSize.Width;
            double h=double.IsInfinity(availableSize.Height)?520:availableSize.Height;
            _widgetCanvas.Measure(new Size(w,h));
            _inlineTextLayer.Measure(new Size(w,h));
            return new Size(w,h);
        }
        protected override Size ArrangeOverride(Size finalSize)
        {
            _widgetCanvas.Arrange(new Rect(new Point(0,0),finalSize));
            _inlineTextLayer.Arrange(new Rect(new Point(0,0),finalSize));
            return finalSize;
        }
        /// <summary>设计模式只允许画布选择移动；运行预览才允许真实控件接收输入。</summary>
        public bool IsDesignMode
        {
            get{return _designMode;}
            set
            {
                // 退出预览后，下次投影应重新按工程定义物化Widget。
                // 否则变量失联返回null时旧的运行态值会遗留在重用的TextBox/CheckBox中。
                if(!_designMode&&value)_resetWidgetLayerOnNextScene=true;
                _designMode=value;
                if(!value)
                {
                    CancelInteraction();_geometryEditMode=false;
                    _drawTool="Select";Cursor=Cursors.Arrow;
                    Action<string> changed=DrawToolChanged;if(changed!=null)changed(_drawTool);
                }
                foreach(UIElement element in _widgetCanvas.Children)
                    element.IsHitTestVisible=!_designMode;
            }
        }
        public event EventHandler<MPWWidgetEventArgs> WidgetEventRequested;
        private void BuildWidgetLayer()
        {
            _widgetCanvas.Children.Clear();
            foreach(MVISceneItem item in _items)
            {
                FrameworkElement widget=MPWWidgetPresenter.Create(item,OnWidgetEvent);
                if(widget==null)continue;
                widget.IsHitTestVisible=!_designMode;
                _widgetCanvas.Children.Add(widget);
            }
            InvalidateMeasure();
            InvalidateArrange();
        }
        /// <summary>仅预览本次画布拖动：真实WPF Widget跟随Shape整体移动，不提交工程事务。</summary>
        private void UpdateWidgetDragPreview()
        {
            if(!_designMode)return;
            int index=0;
            foreach(MVISceneItem item in _items)
            {
                if(!MPWWidgetPresenter.Supports(item.ElementTypeId))continue;
                if(index>=_widgetCanvas.Children.Count)break;
                var widget=_widgetCanvas.Children[index++] as FrameworkElement;
                if(widget==null)continue;
                MRCTransform m=item.WorldTransform;
                bool selected=_dragging&&_selectedItems.Contains(item.Id);
                widget.RenderTransform=new MatrixTransform(new Matrix(
                    m.M11,m.M21,m.M12,m.M22,
                    m.X+(selected?_dragX:0),m.Y+(selected?_dragY:0)));
            }
        }
        private void OnWidgetEvent(MPWWidgetEventArgs args)
        {
            if(_designMode)return;
            EventHandler<MPWWidgetEventArgs> callback=WidgetEventRequested;
            if(callback!=null)callback(this,args);
        }

        public event EventHandler<MPWNodeEventArgs> NodeActivated;
        public event EventHandler<MPWMoveEventArgs> MoveRequested;
        private bool CanReuseWidgets(IList<MVISceneItem> next)
        {
            if(next==null||next.Count!=_items.Count)return false;
            int widgetCount=0;
            for(int i=0;i<next.Count;i++)
            {
                MVISceneItem before=_items[i],after=next[i];
                if(before.Id!=after.Id||before.ElementTypeId!=after.ElementTypeId)
                    return false;
                if(MPWWidgetPresenter.Supports(after.ElementTypeId))widgetCount++;
            }
            return widgetCount==_widgetCanvas.Children.Count;
        }
        public void SetScene(IList<MVISceneItem> items, Guid? selected)
        {
            bool reuse=!_resetWidgetLayerOnNextScene&&CanReuseWidgets(items);
            _resetWidgetLayerOnNextScene=false;
            _items.Clear();
            if(items!=null)_items.AddRange(items);
            // 外部切换选择时清除旧高亮；鼠标本地维护多选时不被场景重投影覆盖。
            bool changedSelection=_selection!=selected;
            if(changedSelection){_selectedItems.Clear();_geometryEditMode=false;_hoverConnectId=null;}
            _selection=selected;
            if(changedSelection&&selected.HasValue)_selectedItems.Add(selected.Value);
            _selectedItems.RemoveWhere(id=>!_items.Exists(item=>item.Id==id));
            if(_editingTextId.HasValue&&(!_items.Exists(item=>
                item.Id==_editingTextId.Value&&item.ElementTypeId=="core.text"&&
                item.IsSelectable)||selected!=_editingTextId.Value))
                FinishInlineTextEdit(false);
            if(!reuse)BuildWidgetLayer();
            else
            {
                int index=0;
                foreach(MVISceneItem item in _items)
                    if(MPWWidgetPresenter.Supports(item.ElementTypeId))
                        MPWWidgetPresenter.UpdateAppearance(
                            (FrameworkElement)_widgetCanvas.Children[index++],item);
            }
            InvalidateVisual();
        }

        private static string PaletteType(IDataObject data)
        {
            if(data==null||!data.GetDataPresent(MPWVisualOperationPanel.WidgetDragFormat))return null;
            string type=data.GetData(MPWVisualOperationPanel.WidgetDragFormat) as string;
            return MRCWidgetTypes.IsBuiltIn(type)?type:null;
        }
        /// <summary>拖放使用已有Widget绘制事件、世界坐标和MEM单事务。</summary>
        public bool DropWidgetFromPalette(string type,double x,double y)
        {
            if(!_designMode||!MRCWidgetTypes.IsBuiltIn(type)||DrawWidgetRequested==null||
                double.IsNaN(x)||double.IsNaN(y)||double.IsInfinity(x)||double.IsInfinity(y))
                return false;
            CancelInteraction();
            Point p=SnapDrawing(new Point(x,y));
            FGEPoint2D at=new FGEPoint2D(p.X,p.Y);
            DrawWidgetRequested(type,new[]{at,at});
            return true;
        }
        protected override void OnDragOver(DragEventArgs e)
        {
            base.OnDragOver(e);
            bool allowed=_designMode&&DrawWidgetRequested!=null&&PaletteType(e.Data)!=null;
            e.Effects=allowed?DragDropEffects.Copy:DragDropEffects.None;
            _paletteDropGhost=allowed?(Point?)SnapDrawing(WorldPoint(e.GetPosition(this))):null;
            InvalidateVisual();e.Handled=true;
        }
        protected override void OnDragLeave(DragEventArgs e)
        {
            base.OnDragLeave(e);_paletteDropGhost=null;InvalidateVisual();e.Handled=true;
        }
        protected override void OnDrop(DragEventArgs e)
        {
            base.OnDrop(e);
            string type=PaletteType(e.Data);
            Point p=WorldPoint(e.GetPosition(this));
            bool accepted=type!=null&&DropWidgetFromPalette(type,p.X,p.Y);
            _paletteDropGhost=null;InvalidateVisual();
            e.Effects=accepted?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;
        }
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            dc.DrawRectangle(Brushes.White, null, new Rect(new Point(0, 0), RenderSize));
            // 网格与吸附共用世界原点；显示平移、Zoom时仍然严格重合。
            // 很小Zoom隐藏过密网格，避免在高数量级视野内逐线绘制。
            double gridStep=25*_zoom;
            if(gridStep>=6)
            {
                Pen gridPen=new Pen(new SolidColorBrush(Color.FromRgb(237,241,247)),1);
                double startX=((_panX%gridStep)+gridStep)%gridStep;
                double startY=((_panY%gridStep)+gridStep)%gridStep;
                for(double x=startX;x<ActualWidth;x+=gridStep)
                    dc.DrawLine(gridPen,new Point(x,0),new Point(x,ActualHeight));
                for(double y=startY;y<ActualHeight;y+=gridStep)
                    dc.DrawLine(gridPen,new Point(0,y),new Point(ActualWidth,y));
            }
            dc.PushTransform(new MatrixTransform(new Matrix(_zoom,0,0,_zoom,_panX,_panY)));
            MVISceneItem transformPreview=null;
            if(_handleDragging&&_activeHandle!=null&&
                (_activeHandle.Kind==MVIVisualHandleKind.Resize||
                 _activeHandle.Kind==MVIVisualHandleKind.Rotate))
            {
                MVISceneItem current=_items.Find(x=>x.Id==_activeHandle.ElementId);
                if(current!=null)
                {
                    try
                    {
                        transformPreview=MVIVisualEditing.PreviewTransform(current,_activeHandle,
                            _handlePointer.X,_handlePointer.Y);
                    }
                    catch(InvalidOperationException){/* 退化矩阵时不显示无效预览 */}
                    catch(ArgumentException){/* 非法控制柄不触发工程修改 */}
                }
            }
            foreach (MVISceneItem item in _items)
            {
                bool selected = _selectedItems.Contains(item.Id)||
                    (_selection.HasValue&&_selection.Value==item.Id);
                if(item.Shape!=null)
                {
                    MVISceneItem shown=transformPreview!=null&&transformPreview.Id==item.Id?
                        transformPreview:item;
                    MPWShapeRenderer.Draw(dc,shown,selected&&_dragging?_dragX:0,
                        selected&&_dragging?_dragY:0,ResolveStyleBrush);
                    // 自动布线退化必须可见；不许用“画出折线”掩盖避障失败。
                    if(item.ElementTypeId=="core.visual.connector"&&
                        !string.IsNullOrEmpty(item.VisualRouteDiagnostic))
                    {
                        dc.DrawGeometry(null,new Pen(Brushes.OrangeRed,2.5/_zoom)
                            {DashStyle=DashStyles.Dash},
                            MPWShapeRenderer.BuildGeometry(item.Shape.Geometry,
                                item.GeometryToWorld));
                        var flag=new Point(item.Bounds.Right+4/_zoom,
                            item.Bounds.Top-12/_zoom);
                        dc.DrawEllipse(Brushes.OrangeRed,null,flag,7/_zoom,7/_zoom);
                        // 感叹号标记随缩放调整，便于直接发现需要人工调整的连接。
                        dc.DrawLine(new Pen(Brushes.White,1.4/_zoom),
                            new Point(flag.X,flag.Y-3/_zoom),
                            new Point(flag.X,flag.Y+1/_zoom));
                    }
                    if(selected)
                    {
                        if(item.ElementTypeId=="core.visual.connector")
                            dc.DrawGeometry(null,new Pen(Brushes.DodgerBlue,3/_zoom),
                                MPWShapeRenderer.BuildGeometry(item.Shape.Geometry,
                                    item.GeometryToWorld));
                        else
                        {
                            Rect highlight=new Rect(shown.Bounds.X+(_dragging?_dragX:0),
                                shown.Bounds.Y+(_dragging?_dragY:0),
                                Math.Max(1,shown.Bounds.Width),Math.Max(1,shown.Bounds.Height));
                            dc.DrawRectangle(null,new Pen(Brushes.DodgerBlue,0.8)
                                {DashStyle=DashStyles.Dash},highlight);
                        }
                    }
                    continue;
                }
                if(item.ElementTypeId=="core.image"||item.ElementTypeId=="core.svg")
                {
                    ImageSource image=AssetImage(item);
                    MRCTransform transform=item.WorldTransform;
                    dc.PushTransform(new MatrixTransform(new Matrix(
                        transform.M11,transform.M21,transform.M12,transform.M22,
                        transform.X+(_dragging&&_selectedItems.Contains(item.Id)?_dragX:0),
                        transform.Y+(_dragging&&_selectedItems.Contains(item.Id)?_dragY:0))));
                    dc.PushOpacity(item.Style.Opacity);
                    if(image!=null)
                        dc.DrawImage(image,new Rect(0,0,item.LocalWidth,item.LocalHeight));
                    else
                    {
                        dc.DrawRectangle(Brushes.MistyRose,new Pen(Brushes.IndianRed,1),
                            new Rect(0,0,item.LocalWidth,item.LocalHeight));
                        var label=new FormattedText(item.AssetId.HasValue?"资源不可用":"未绑定资源",
                            CultureInfo.CurrentCulture,FlowDirection.LeftToRight,
                            new Typeface("Segoe UI"),10,Brushes.DarkRed,1.0);
                        dc.DrawText(label,new Point(4,4));
                    }
                    dc.Pop();dc.Pop();
                    continue;
                }
                if(MPWWidgetPresenter.Supports(item.ElementTypeId))continue;
                if(item.ElementTypeId=="core.text")
                {
                    if(_editingTextId.HasValue&&_editingTextId.Value==item.Id)continue;
                    MRCVisualStyle textStyle=item.Style;
                    var matrix=item.WorldTransform;
                    dc.PushTransform(new MatrixTransform(new Matrix(matrix.M11,matrix.M21,
                        matrix.M12,matrix.M22,matrix.X+(_dragging&&selected?_dragX:0),
                        matrix.Y+(_dragging&&selected?_dragY:0))));
                    var textBrush=MPWShapeRenderer.BrushOf(textStyle.Fill,
                        Brushes.MidnightBlue,ResolveStyleBrush);
                    if(textStyle.Fill.Kind==MRCBrushKind.None)textBrush=Brushes.MidnightBlue;
                    var formatted=new FormattedText(item.Text,CultureInfo.CurrentUICulture,
                        FlowDirection.LeftToRight,new Typeface(textStyle.FontFamily),
                        textStyle.FontSize,textBrush,1.0);
                    formatted.MaxTextWidth=Math.Max(1,item.Bounds.Width);
                    dc.PushOpacity(textStyle.Opacity);
                    dc.DrawText(formatted,new Point(0,0));
                    dc.Pop();
                    dc.Pop();
                    continue;
                }

                double dx = selected && _dragging ? _dragX : 0;
                double dy = selected && _dragging ? _dragY : 0;
                Rect bounds = new Rect(item.Bounds.X + dx, item.Bounds.Y + dy, item.Bounds.Width, item.Bounds.Height);
                Brush fill = selected ? new SolidColorBrush(Color.FromRgb(206, 232, 253)) :
                    new SolidColorBrush(Color.FromRgb(229, 239, 252));
                Pen pen = new Pen(selected ? Brushes.DodgerBlue : Brushes.SteelBlue, selected ? 2.5 : 1.2);
                var geometry = new StreamGeometry();
                using (var shape = geometry.Open())
                {
                    shape.BeginFigure(new Point(item.TopLeft.X + dx, item.TopLeft.Y + dy), true, true);
                    shape.LineTo(new Point(item.TopRight.X + dx, item.TopRight.Y + dy), true, false);
                    shape.LineTo(new Point(item.BottomRight.X + dx, item.BottomRight.Y + dy), true, false);
                    shape.LineTo(new Point(item.BottomLeft.X + dx, item.BottomLeft.Y + dy), true, false);
                }
                geometry.Freeze(); dc.DrawGeometry(fill, pen, geometry);
                FormattedText name = new FormattedText(item.Name, CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, new Typeface("Segoe UI"), 13, Brushes.MidnightBlue, 1.0);
                dc.DrawText(name, new Point(bounds.Left + 12, bounds.Top + 12));
            }
            // draw.io式图元快捷连接提示：悬停态只显示固定锚点与四向箭头。
            if(_designMode&&_drawTool=="Select"&&!_connectDragging&&ConnectRequested!=null&&
                _hoverConnectId.HasValue)
            {
                MVISceneItem hover=Connectable(_hoverConnectId.Value);
                if(hover!=null&&!_selectedItems.Contains(hover.Id))
                    foreach(string side in ConnectionSides)
                    {
                        FGEPoint2D anchor=MVIVisualAnchors.At(hover,side);
                        Point quick=QuickArrowPoint(hover,side);
                        double vx=quick.X-anchor.X,vy=quick.Y-anchor.Y;
                        double length=Math.Sqrt(vx*vx+vy*vy);
                        if(length<1e-8)continue;
                        vx/=length;vy/=length;
                        Point fixedPort=new Point(anchor.X,anchor.Y);
                        double d=3/_zoom;
                        var pin=new Pen(Brushes.DeepSkyBlue,1.4/_zoom);
                        dc.DrawLine(pin,new Point(fixedPort.X-d,fixedPort.Y-d),
                            new Point(fixedPort.X+d,fixedPort.Y+d));
                        dc.DrawLine(pin,new Point(fixedPort.X+d,fixedPort.Y-d),
                            new Point(fixedPort.X-d,fixedPort.Y+d));
                        dc.DrawLine(new Pen(Brushes.SteelBlue,1.3/_zoom),fixedPort,
                            new Point(quick.X-vx*6/_zoom,quick.Y-vy*6/_zoom));
                        double tip=7/_zoom,half=5/_zoom;
                        var arrow=new StreamGeometry();
                        using(StreamGeometryContext g=arrow.Open())
                        {
                            g.BeginFigure(new Point(quick.X+vx*tip,quick.Y+vy*tip),
                                true,true);
                            g.LineTo(new Point(quick.X-vx*half-vy*half,
                                quick.Y-vy*half+vx*half),true,false);
                            g.LineTo(new Point(quick.X-vx*half+vy*half,
                                quick.Y-vy*half-vx*half),true,false);
                        }
                        arrow.Freeze();
                        dc.DrawGeometry(Brushes.SteelBlue,
                            new Pen(Brushes.White,1/_zoom),arrow);
                    }
            }
            if(_connectDragging&&_connectSourceId.HasValue)
            {
                MVISceneItem source=Connectable(_connectSourceId.Value);
                if(source!=null)
                {
                    FGEPoint2D anchor=MVIVisualAnchors.At(source,_connectSourceSide);
                    Point target=_connectPointer;
                    MVISceneItem candidate=_connectTargetId.HasValue?
                        Connectable(_connectTargetId.Value):null;
                    if(candidate!=null)
                    {
                        FGEPoint2D point=MVIVisualAnchors.At(candidate,_connectTargetSide);
                        target=new Point(point.X,point.Y);
                        dc.DrawEllipse(null,new Pen(Brushes.DodgerBlue,2/_zoom),
                            target,8/_zoom,8/_zoom);
                    }
                    dc.DrawLine(new Pen(Brushes.DodgerBlue,2/_zoom)
                        {DashStyle=DashStyles.Dash},new Point(anchor.X,anchor.Y),target);
                }
            }
            if(IsDesignMode&&_drawTool=="Select"&&_selection.HasValue&&EditableSelection().Length<2)
            {
                MVISceneItem edited=_items.Find(i=>i.Id==_selection.Value);
                if(edited!=null&&edited.ElementTypeId=="core.visual.connector"&&
                    edited.Shape!=null)
                {
                    FGEPoint2D start=edited.GeometryToWorld.TransformPoint(
                        edited.Shape.Geometry.Commands[0].End);
                    FGEPoint2D end=edited.GeometryToWorld.TransformPoint(
                        edited.Shape.Geometry.Commands[
                            edited.Shape.Geometry.Commands.Count-1].End);
                    FGEPoint2D[] points={start,end};
                    for(int i=0;i<points.Length;i++)
                    {
                        Point center=new Point(points[i].X,points[i].Y);
                        dc.DrawEllipse(Brushes.White,
                            new Pen(Brushes.DodgerBlue,2/_zoom),
                            center,5/_zoom,5/_zoom);
                    }
                    var commands=edited.Shape.Geometry.Commands;
                    for(int index=1;index<commands.Count-1;index++)
                    {
                        FGEPoint2D p=edited.GeometryToWorld.TransformPoint(commands[index].End);
                        Point center=new Point(p.X,p.Y);
                        if(_waypointDragging&&_waypointConnectionId==edited.Id&&
                            _waypointIndex==index-1)center=_waypointPointer;
                        double radius=5/_zoom;
                        dc.DrawRectangle(Brushes.Gold,new Pen(Brushes.DodgerBlue,1.5/_zoom),
                            new Rect(center.X-radius,center.Y-radius,radius*2,radius*2));
                    }
                    if(_waypointDragging&&_waypointConnectionId==edited.Id)
                    {
                        FGEPoint2D previous=edited.GeometryToWorld.TransformPoint(
                            commands[_waypointIndex].End);
                        FGEPoint2D next=edited.GeometryToWorld.TransformPoint(
                            commands[_waypointIndex+2].End);
                        Pen dashed=new Pen(Brushes.DodgerBlue,1.5/_zoom)
                            {DashStyle=DashStyles.Dash};
                        dc.DrawLine(dashed,new Point(previous.X,previous.Y),_waypointPointer);
                        dc.DrawLine(dashed,_waypointPointer,new Point(next.X,next.Y));
                    }
                    if(_rewireDragging&&_rewireConnectionId==edited.Id)
                    {
                        FGEPoint2D opposite=_rewireSource?end:start;
                        Point target=_rewirePointer;
                        MVISceneItem candidate=_connectTargetId.HasValue?
                            Connectable(_connectTargetId.Value):null;
                        if(candidate!=null&&_connectTargetSide!=null)
                        {
                            FGEPoint2D point=MVIVisualAnchors.At(candidate,
                                _connectTargetSide);
                            target=new Point(point.X,point.Y);
                            dc.DrawEllipse(null,new Pen(Brushes.MediumSeaGreen,2/_zoom),
                                target,8/_zoom,8/_zoom);
                        }
                        dc.DrawLine(new Pen(Brushes.DodgerBlue,2/_zoom)
                            {DashStyle=DashStyles.Dash},
                            new Point(opposite.X,opposite.Y),target);
                    }
                }
                else if(edited!=null)
                {
                    MVISceneItem shown=transformPreview!=null&&transformPreview.Id==edited.Id?
                        transformPreview:edited;
                    // 全部变换柄与绘制图元使用同一预览矩阵，曲线柄独立预览。
                    foreach(MVIVisualHandle handle in MVIVisualEditing.Handles(shown,_geometryEditMode,_zoom))
                    {
                        bool moving=_handleDragging&&_activeHandle!=null&&
                            handle.Kind==_activeHandle.Kind&&
                            handle.CommandIndex==_activeHandle.CommandIndex;
                        Point center=moving&&_geometryEditMode?_handlePointer:
                            new Point(handle.WorldPoint.X,handle.WorldPoint.Y);
                        if(handle.Kind==MVIVisualHandleKind.Rotate)
                        {
                            // 单独的右上旋转图标，不与缩放圆点和路径锚点混用。
                            dc.DrawEllipse(null,new Pen(Brushes.DodgerBlue,2/_zoom),
                                center,7/_zoom,7/_zoom);
                            Point tip=new Point(center.X+7/_zoom,center.Y-3/_zoom);
                            dc.DrawLine(new Pen(Brushes.DodgerBlue,2/_zoom),tip,
                                new Point(tip.X-5/_zoom,tip.Y-2/_zoom));
                            dc.DrawLine(new Pen(Brushes.DodgerBlue,2/_zoom),tip,
                                new Point(tip.X-1/_zoom,tip.Y+5/_zoom));
                        }
                        else
                        {
                            Brush fill=handle.Kind==MVIVisualHandleKind.Resize?
                                Brushes.DodgerBlue:
                                handle.Kind==MVIVisualHandleKind.Anchor?Brushes.White:Brushes.Gold;
                            Pen pen=handle.Kind==MVIVisualHandleKind.Resize?
                                new Pen(Brushes.White,1.5/_zoom):
                                new Pen(Brushes.DodgerBlue,1.5/_zoom);
                            dc.DrawEllipse(fill,pen,center,5/_zoom,5/_zoom);
                        }
                        if(moving)
                            dc.DrawLine(new Pen(Brushes.DodgerBlue,1/_zoom){DashStyle=DashStyles.Dash},
                                new Point(handle.WorldPoint.X,handle.WorldPoint.Y),center);
                    }
                    // 非Shape先预览变换外框；后续由控件适配器提供内容级同步预览。
                    if(transformPreview!=null&&transformPreview.Id==edited.Id&&edited.Shape==null)
                    {
                        FGEPoint2D[] corners={shown.TopLeft,shown.TopRight,
                            shown.BottomRight,shown.BottomLeft,shown.TopLeft};
                        Pen dashed=new Pen(Brushes.DodgerBlue,1.5/_zoom){DashStyle=DashStyles.Dash};
                        for(int i=0;i<4;i++)
                            dc.DrawLine(dashed,new Point(corners[i].X,corners[i].Y),
                                new Point(corners[i+1].X,corners[i+1].Y));
                    }
                    // 鼠标按住时直接预览整条Bezier/Path，不污染MEM Revision；
                    // 松开后由统一EditHandle事务一次性提交精确曲线。
                    if(_handleDragging&&_activeHandle!=null&&edited.Shape!=null&&
                        (_activeHandle.Kind==MVIVisualHandleKind.Anchor||
                         _activeHandle.Kind==MVIVisualHandleKind.Control1||
                         _activeHandle.Kind==MVIVisualHandleKind.Control2))
                    {
                        FGEPathHandleKind kind=_activeHandle.Kind==MVIVisualHandleKind.Anchor?
                            FGEPathHandleKind.Anchor:_activeHandle.Kind==MVIVisualHandleKind.Control1?
                            FGEPathHandleKind.Control1:FGEPathHandleKind.Control2;
                        FGEPoint2D point=edited.GeometryToWorld.Inverse().TransformPoint(
                            new FGEPoint2D(_handlePointer.X,_handlePointer.Y));
                        FGEPath2D preview=FGEPathEditing.MoveHandle(
                            edited.Shape.Geometry,_activeHandle.CommandIndex,kind,point);
                        Geometry geometry=MPWShapeRenderer.BuildGeometry(preview,edited.GeometryToWorld);
                        dc.DrawGeometry(null,new Pen(Brushes.OrangeRed,2/_zoom)
                            {DashStyle=DashStyles.Dash},geometry);
                    }
                }
            }
            // 参考线来自同一只读吸附结果，既不保存，也不单独实现对齐算法。
            if(_dragging&&_dragSnap!=null)
            {
                Pen guidePen=new Pen(Brushes.DeepSkyBlue,1/_zoom)
                    {DashStyle=DashStyles.Dash};
                if(_dragSnap.GuideX.HasValue)
                {
                    double x=_dragSnap.GuideX.Value;
                    dc.DrawLine(guidePen,new Point(x,(-_panY)/_zoom),
                        new Point(x,(ActualHeight-_panY)/_zoom));
                }
                if(_dragSnap.GuideY.HasValue)
                {
                    double y=_dragSnap.GuideY.Value;
                    dc.DrawLine(guidePen,new Point((-_panX)/_zoom,y),
                        new Point((ActualWidth-_panX)/_zoom,y));
                }
            }
            // 共性选区使用一个外框与九个变换控制柄，避免每个对象各出现九个重叠控制柄。
            MVISceneItem[] multi=EditableSelection();
            if(_drawTool=="Select"&&multi.Length>1&&SelectionTransformRequested!=null)
            {
                FGERect2D bounds=MVISelectionTransform.Bounds(multi);
                MRCTransform preview=MRCTransform.Identity;
                if(_selectionTransformDragging)
                    preview=MVISelectionTransform.Delta(bounds,_selectionHandleIndex,
                        new FGEPoint2D(_selectionHandlePointer.X,_selectionHandlePointer.Y),
                        _zoom,
                        (Keyboard.Modifiers&ModifierKeys.Shift)!=0,
                        (Keyboard.Modifiers&ModifierKeys.Alt)!=0,
                        (Keyboard.Modifiers&ModifierKeys.Shift)!=0);
                FGEPoint2D[] corners={
                    new FGEPoint2D(bounds.Left,bounds.Top),
                    new FGEPoint2D(bounds.Right,bounds.Top),
                    new FGEPoint2D(bounds.Right,bounds.Bottom),
                    new FGEPoint2D(bounds.Left,bounds.Bottom)};
                var outline=new StreamGeometry();
                using(StreamGeometryContext context=outline.Open())
                {
                    FGEPoint2D start=preview.ToGeometry().TransformPoint(corners[0]);
                    context.BeginFigure(new Point(start.X,start.Y),false,true);
                    for(int k=1;k<4;k++)
                    {
                        FGEPoint2D point=preview.ToGeometry().TransformPoint(corners[k]);
                        context.LineTo(new Point(point.X,point.Y),true,false);
                    }
                }
                dc.DrawGeometry(null,new Pen(Brushes.DodgerBlue,1.2/_zoom)
                    {DashStyle=DashStyles.Dash},outline);
                for(int k=0;k<9;k++)
                {
                    FGEPoint2D world=MVISelectionTransform.Handle(bounds,k,_zoom);
                    world=preview.ToGeometry().TransformPoint(world);
                    dc.DrawEllipse(k==8?Brushes.AliceBlue:Brushes.White,
                        new Pen(Brushes.DodgerBlue,1.4/_zoom),
                        new Point(world.X,world.Y),5/_zoom,5/_zoom);
                }
            }
            // 创建预览与最终工程使用相同MVIShapeCatalog求解；仅WPF临时Ghost。
            if(HasDrawPreview)
            {
                var points=new List<FGEPoint2D>(_drawPoints);
                if(!IsPointDrawTool(_drawTool))
                    points.Add(new FGEPoint2D(_drawPointer.X,_drawPointer.Y));
                if(IsPointDrawTool(_drawTool))
                {
                    if(points.Count>0)
                    {
                        for(int i=1;i<points.Count;i++)
                        {
                            if(_drawContourStarts.BinarySearch(i)>=0)continue;
                            dc.DrawLine(new Pen(Brushes.DodgerBlue,1.5/_zoom),
                                new Point(points[i-1].X,points[i-1].Y),
                                new Point(points[i].X,points[i].Y));
                        }
                        if(!_nextPathContour)
                        {
                            FGEPoint2D last=points[points.Count-1];
                            dc.DrawLine(new Pen(Brushes.DodgerBlue,1.5/_zoom)
                                {DashStyle=DashStyles.Dash},
                                new Point(last.X,last.Y),_drawPointer);
                        }
                        foreach(FGEPoint2D pt in points)
                            dc.DrawEllipse(Brushes.White,new Pen(Brushes.DodgerBlue,1/_zoom),
                                new Point(pt.X,pt.Y),3.5/_zoom,3.5/_zoom);
                        if(_drawTool=="Path")
                        {
                            // 各轮廓起点用方形标记，与普通顶点区分；等待N后首点时
                            // 仅显示指针提示，不与前一条轮廓生成任何Ghost连接线。
                            var starts=new List<int>{0};starts.AddRange(_drawContourStarts);
                            foreach(int start in starts)
                            {
                                FGEPoint2D origin=points[start];
                                double radius=4.5/_zoom;
                                dc.DrawRectangle(Brushes.AliceBlue,
                                    new Pen(Brushes.DodgerBlue,1.5/_zoom),
                                    new Rect(origin.X-radius,origin.Y-radius,
                                        radius*2,radius*2));
                            }
                            if(_nextPathContour)
                                dc.DrawEllipse(null,new Pen(Brushes.DeepSkyBlue,1.5/_zoom)
                                    {DashStyle=DashStyles.Dash},_drawPointer,
                                    5/_zoom,5/_zoom);
                        }
                    }
                }
                else if(points.Count>=2)
                {
                    if(_drawTool=="Text"||IsWidgetDrawTool(_drawTool))
                    {
                        double x,y,w,h;
                        try
                        {
                            if(_drawTool=="Text")MVIShapeCatalog.TextBounds(points,out x,out y,out w,out h);
                            else MVIShapeCatalog.WidgetBounds(points,out x,out y,out w,out h);
                            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(32,32,117,218)),
                                new Pen(Brushes.DodgerBlue,1.5/_zoom)
                                {DashStyle=DashStyles.Dash},new Rect(x,y,w,h));
                        }
                        catch(ArgumentException){/*非法文本尺寸不落工程*/}
                    }
                    else try
                    {
                        MRCShapeKind type=(MRCShapeKind)Enum.Parse(typeof(MRCShapeKind),_drawTool);
                        double x,y;
                        MRCShapeDefinition ghost=MVIShapeCatalog.CreateDrawn(type,points,
                            (Keyboard.Modifiers&ModifierKeys.Shift)!=0,out x,out y);
                        Geometry outline=MPWShapeRenderer.BuildGeometry(ghost.Geometry,
                            FGETransform2D.Translation(x,y));
                        dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(28,32,117,218)),
                            new Pen(Brushes.DodgerBlue,1.6/_zoom)
                            {DashStyle=DashStyles.Dash},outline);
                    }
                    catch(ArgumentException){/* 零长度指针移动仅显示工具十字，不生成工程 */}
                }
            }
            if(_paletteDropGhost.HasValue)
            {
                Point p=_paletteDropGhost.Value;
                FGEPoint2D w=new FGEPoint2D(p.X,p.Y);
                double x,y,width,height;
                MVIShapeCatalog.WidgetBounds(new[]{w,w},out x,out y,out width,out height);
                dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(35,32,117,218)),
                    new Pen(Brushes.DodgerBlue,1.5/_zoom){DashStyle=DashStyles.Dash},
                    new Rect(x,y,width,height));
            }
            if(_marquee)
            {
                double x=Math.Min(_marqueeStart.X,_marqueeEnd.X);
                double y=Math.Min(_marqueeStart.Y,_marqueeEnd.Y);
                var rect=new Rect(x,y,Math.Abs(_marqueeEnd.X-_marqueeStart.X),
                    Math.Abs(_marqueeEnd.Y-_marqueeStart.Y));
                dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(42,35,117,218)),
                    new Pen(Brushes.DodgerBlue,1){DashStyle=DashStyles.Dash},rect);
            }
            dc.Pop();
        }

        /// <summary>真实鼠标选择共用的集合运算：Ctrl/Shift切换选中不依赖上一次单选事件回写。</summary>
        private IReadOnlyList<Guid> ApplyPointerSelection(Guid? hit,bool extend)
        {
            if(!extend)_selectedItems.Clear();
            if(hit.HasValue)
            {
                if(extend&&!_selectedItems.Add(hit.Value))_selectedItems.Remove(hit.Value);
                else _selectedItems.Add(hit.Value);
            }
            _selection=hit.HasValue&&_selectedItems.Contains(hit.Value)?hit:null;
            if(!_selection.HasValue)
                foreach(Guid id in _selectedItems){_selection=id;break;}
            return new List<Guid>(_selectedItems).AsReadOnly();
        }
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Focus();
            Point point = WorldPoint(e.GetPosition(this));
            if(!_designMode)return;
            if(_drawTool!="Select")
            {
                Point world=WorldPoint(e.GetPosition(this));
                if(IsPointDrawTool(_drawTool)&&e.ClickCount>=2)
                    CompleteDrawGesture(world.X,world.Y);
                else
                {
                    BeginDrawGesture(world.X,world.Y);
                    if(_drawDragging)CaptureMouse();
                }
                e.Handled=true;return;
            }
            if(e.ClickCount>=2&&_geometryEditMode&&
                (Keyboard.Modifiers&ModifierKeys.Control)!=0&&
                RequestSelectedPathSplitAt(point.X,point.Y))
            {e.Handled=true;return;}
            int selectionHandle;
            if(TryHitSelectionHandle(point,out selectionHandle))
            {
                _selectionTransformDragging=true;_selectionHandleIndex=selectionHandle;
                _selectionHandleStart=point;_selectionHandlePointer=point;
                CaptureMouse();InvalidateVisual();e.Handled=true;return;
            }
            Guid waypointLink;int waypointIndex;
            if(e.ClickCount>=2&&InsertWaypointRequested!=null&&
                TryHitConnectorSegment(point,out waypointLink,out waypointIndex))
            {
                InsertWaypointRequested(waypointLink,waypointIndex,point.X,point.Y);
                e.Handled=true;return;
            }
            if(TryHitConnectionWaypoint(point,out waypointLink,out waypointIndex))
            {
                if((Keyboard.Modifiers&ModifierKeys.Shift)!=0&&
                    RemoveWaypointRequested!=null)
                {
                    RemoveWaypointRequested(waypointLink,waypointIndex);
                    e.Handled=true;return;
                }
                if(MoveWaypointRequested!=null)
                {
                    _waypointDragging=true;_waypointConnectionId=waypointLink;
                    _waypointIndex=waypointIndex;_waypointStart=point;
                    _waypointPointer=point;CaptureMouse();
                    e.Handled=true;return;
                }
            }
            Guid link;bool sourceEnd;
            if(ReconnectRequested!=null&&
                TryHitConnectionEnd(point,out link,out sourceEnd))
            {
                _rewireDragging=true;_rewireConnectionId=link;
                _rewireSource=sourceEnd;_rewirePointer=point;
                _connectSourceId=null;_connectTargetId=null;_connectTargetSide=null;
                _hoverConnectId=null;CaptureMouse();
                InvalidateVisual();e.Handled=true;return;
            }
            Guid quickId;string quickSide;
            if(TryHitQuickArrow(point,out quickId,out quickSide))
            {
                _connectDragging=true;_connectSourceId=quickId;_connectSourceSide=quickSide;
                _connectTargetId=null;_connectTargetSide=null;
                _connectPointer=point;_hoverConnectId=null;
                CaptureMouse();InvalidateVisual();e.Handled=true;return;
            }
            if(e.ClickCount>=2&&
                (Keyboard.Modifiers&(ModifierKeys.Control|ModifierKeys.Shift))==0)
            {
                // 文字优先进入原位输入；不误触发H1/H5或普通Shape几何编辑。
                MVISceneItem textItem=_items.LastOrDefault(item=>
                    item.ElementTypeId=="core.text"&&item.IsSelectable&&
                    item.Contains(point.X,point.Y));
                if(textItem!=null&&BeginInlineTextEdit(textItem.Id))
                {e.Handled=true;return;}
            }
            if(e.ClickCount>=2&&_selection.HasValue&&
                (Keyboard.Modifiers&(ModifierKeys.Control|ModifierKeys.Shift))==0&&
                !_items.Exists(x=>x.Id==_selection.Value&&
                    x.ElementTypeId=="core.visual.connector"))
            {
                MVISceneItem current=_items.Find(i=>i.Id==_selection.Value);
                if(current!=null&&current.Shape!=null&&current.Contains(point.X,point.Y))
                {
                    _geometryEditMode=!_geometryEditMode;
                    _dragging=false;
                    InvalidateVisual();e.Handled=true;return;
                }
            }
            if(_designMode&&_selection.HasValue&&EditableSelection().Length<2&&
                (Keyboard.Modifiers&(ModifierKeys.Control|ModifierKeys.Shift))==0)
            {
                MVISceneItem selected=_items.Find(i=>i.Id==_selection.Value);
                if(selected!=null&&selected.ElementTypeId!="core.visual.connector")
                {
                    MVIVisualHandle handle=MVIVisualEditing.HitHandle(selected,point.X,point.Y,8/_zoom,_geometryEditMode,_zoom);
                    if(handle!=null)
                    {
                        _activeHandle=handle;
                        _handleDragging=true;
                        _handlePointer=point;
                        _dragging=false;
                        CaptureMouse();
                        InvalidateVisual();
                        e.Handled=true;
                        return;
                    }
                }
            }
            Guid? hit = null;
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                MVISceneItem item = _items[i];
                if (item.Contains(point.X, point.Y))
                { hit = item.Id; break; }
            }
            Guid? previousSelection=_selection;
            bool extend=(Keyboard.Modifiers&ModifierKeys.Control)!=0||
                (Keyboard.Modifiers&ModifierKeys.Shift)!=0;
            // 选中的多个对象拖动时保留选区；点击其他对象才清除原选区。
            bool moveGroup=hit.HasValue&&!extend&&_selectedItems.Count>1&&
                _selectedItems.Contains(hit.Value);
            IReadOnlyList<Guid> intendedSelection;
            if(moveGroup)
            {
                _selection=hit;
                intendedSelection=SelectedVisualIds;
            }
            else intendedSelection=ApplyPointerSelection(hit,extend);
            if(extend||!hit.HasValue||!previousSelection.HasValue||
                hit.Value!=previousSelection.Value)
                _geometryEditMode=false;
            if(hit==null)
            {
                _marquee=true;_marqueeStart=point;_marqueeEnd=point;
                CaptureMouse();
            }
            _dragStart = point;
            _dragX = 0; _dragY = 0;_dragSnap=null;
            // 增减选择不能再触发单选NodeActivated，否则Studio回写会抹掉集合。
            _dragging = hit.HasValue&&!extend&&_selectedItems.Contains(hit.Value)&&
                !_items.Exists(x=>x.Id==hit.Value&&x.ElementTypeId=="core.visual.connector");
            if (_dragging) CaptureMouse();
            EventHandler<MPWNodeEventArgs> activated = NodeActivated;
            if (!extend&&!moveGroup&&activated != null) activated(this, new MPWNodeEventArgs(hit));
            if(hit.HasValue&&!moveGroup&&SelectionSetRequested!=null)
                SelectionSetRequested(intendedSelection);
            InvalidateVisual();
            e.Handled = true;
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if(HasDrawPreview)
            {
                Point p=WorldPoint(e.GetPosition(this));
                UpdateDrawGesture(p.X,p.Y);
                e.Handled=true;return;
            }
            if(_selectionTransformDragging)
            {
                Point pointer=WorldPoint(e.GetPosition(this));
                _selectionHandlePointer=_gridSnap&&_selectionHandleIndex!=8?
                    new Point(Math.Round(pointer.X/25)*25,
                        Math.Round(pointer.Y/25)*25):pointer;
                InvalidateVisual();e.Handled=true;return;
            }
            if(_waypointDragging)
            {
                Point pointerWorld=WorldPoint(e.GetPosition(this));
                _waypointPointer=_gridSnap?
                    new Point(Math.Round(pointerWorld.X/25)*25,
                        Math.Round(pointerWorld.Y/25)*25):pointerWorld;
                InvalidateVisual();e.Handled=true;return;
            }
            if(_rewireDragging)
            {
                _rewirePointer=WorldPoint(e.GetPosition(this));
                UpdateConnectionDrag(_rewirePointer);
                e.Handled=true;return;
            }
            if(_connectDragging)
            {
                UpdateConnectionDrag(WorldPoint(e.GetPosition(this)));
                e.Handled=true;return;
            }
            if(_marquee)
            {
                _marqueeEnd=WorldPoint(e.GetPosition(this));
                InvalidateVisual();return;
            }
            if(_panning)
            {
                Point position=e.GetPosition(this);
                SetViewport(_zoom,_panX+position.X-_panPointer.X,
                    _panY+position.Y-_panPointer.Y);
                _panPointer=position;
                return;
            }
            if(_handleDragging)
            {
                Point current=WorldPoint(e.GetPosition(this));
                if(_gridSnap&&_activeHandle!=null&&
                    _activeHandle.Kind==MVIVisualHandleKind.Resize)
                    current=new Point(Math.Round(current.X/25)*25,
                        Math.Round(current.Y/25)*25);
                _handlePointer=current;
                InvalidateVisual();
                return;
            }
            if (!_dragging)
            {
                UpdateConnectionHover(WorldPoint(e.GetPosition(this)));
                return;
            }
            Point point = WorldPoint(e.GetPosition(this));
            _dragSnap=ResolveDrag(point.X-_dragStart.X,point.Y-_dragStart.Y);
            _dragX=_dragSnap.WorldDx;
            _dragY=_dragSnap.WorldDy;
            UpdateWidgetDragPreview();
            InvalidateVisual();
        }
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if(_drawTool!="Select")
            {
                if(_drawDragging)
                {
                    Point p=WorldPoint(e.GetPosition(this));
                    CompleteDrawGesture(p.X,p.Y,
                        (Keyboard.Modifiers&ModifierKeys.Shift)!=0);
                    _drawDragging=false;
                    if(IsMouseCaptured)ReleaseMouseCapture();
                }
                e.Handled=true;return;
            }
            if(_selectionTransformDragging)
            {
                Point pointer=WorldPoint(e.GetPosition(this));
                Point target=_gridSnap&&_selectionHandleIndex!=8?
                    new Point(Math.Round(pointer.X/25)*25,
                        Math.Round(pointer.Y/25)*25):pointer;
                bool moved=Math.Abs(pointer.X-_selectionHandleStart.X)>1/_zoom||
                    Math.Abs(pointer.Y-_selectionHandleStart.Y)>1/_zoom;
                int handle=_selectionHandleIndex;
                IReadOnlyList<Guid> ids=SelectedVisualIds;
                _selectionTransformDragging=false;
                if(IsMouseCaptured)ReleaseMouseCapture();
                if(moved&&SelectionTransformRequested!=null)
                    SelectionTransformRequested(ids,handle,target.X,target.Y,_zoom,
                        (Keyboard.Modifiers&ModifierKeys.Shift)!=0,
                        (Keyboard.Modifiers&ModifierKeys.Alt)!=0,
                        (Keyboard.Modifiers&ModifierKeys.Shift)!=0);
                InvalidateVisual();e.Handled=true;return;
            }
            if(_waypointDragging)
            {
                Point raw=WorldPoint(e.GetPosition(this));
                Point target=_gridSnap?
                    new Point(Math.Round(raw.X/25)*25,
                        Math.Round(raw.Y/25)*25):raw;
                Guid link=_waypointConnectionId??Guid.Empty;
                int index=_waypointIndex;
                bool moved=Math.Abs(raw.X-_waypointStart.X)>2/_zoom||
                    Math.Abs(raw.Y-_waypointStart.Y)>2/_zoom;
                _waypointDragging=false;_waypointConnectionId=null;
                if(IsMouseCaptured)ReleaseMouseCapture();
                if(moved&&link!=Guid.Empty&&MoveWaypointRequested!=null)
                    MoveWaypointRequested(link,index,target.X,target.Y);
                InvalidateVisual();e.Handled=true;return;
            }
            if(_rewireDragging)
            {
                UpdateConnectionDrag(WorldPoint(e.GetPosition(this)));
                Guid connection=_rewireConnectionId??Guid.Empty;
                Guid target=_connectTargetId??Guid.Empty;
                bool source=_rewireSource;string side=_connectTargetSide;
                _rewireDragging=false;_rewireConnectionId=null;
                _connectTargetId=null;_connectTargetSide=null;
                if(IsMouseCaptured)ReleaseMouseCapture();
                if(connection!=Guid.Empty&&target!=Guid.Empty&&
                    ReconnectRequested!=null)
                    ReconnectRequested(connection,source,target,side);
                InvalidateVisual();e.Handled=true;return;
            }
            if(_connectDragging)
            {
                UpdateConnectionDrag(WorldPoint(e.GetPosition(this)));
                Guid source=_connectSourceId??Guid.Empty,target=_connectTargetId??Guid.Empty;
                string sourceSide=_connectSourceSide,targetSide=_connectTargetSide;
                _connectDragging=false;_connectSourceId=null;_connectTargetId=null;
                _connectSourceSide=null;_connectTargetSide=null;_hoverConnectId=null;
                if(IsMouseCaptured)ReleaseMouseCapture();
                if(source!=Guid.Empty&&target!=Guid.Empty&&ConnectRequested!=null)
                    ConnectRequested(source,sourceSide,target,targetSide);
                InvalidateVisual();e.Handled=true;return;
            }
            if(_marquee)
            {
                _marquee=false;
                if(IsMouseCaptured)ReleaseMouseCapture();
                FGEPoint2D a=new FGEPoint2D(_marqueeStart.X,_marqueeStart.Y);
                FGEPoint2D b=new FGEPoint2D(_marqueeEnd.X,_marqueeEnd.Y);
                FGERect2D selection=FGERect2D.FromPoints(a,b);
                foreach(MVISceneItem item in _items)
                    if(item.IsSelectable&&(selection.Intersects(item.Bounds)||
                        selection.Contains(new FGEPoint2D(item.Bounds.X,item.Bounds.Y))))
                        _selectedItems.Add(item.Id);
                if(_selectedItems.Count!=0)
                {
                    foreach(Guid id in _selectedItems){_selection=id;break;}
                    if(SelectionSetRequested!=null)
                        SelectionSetRequested(new List<Guid>(_selectedItems).AsReadOnly());
                }
                InvalidateVisual();e.Handled=true;return;
            }
            if(_handleDragging)
            {
                MVIVisualHandle handle=_activeHandle;
                Point raw=WorldPoint(e.GetPosition(this));
                bool changed=handle!=null&&(Math.Abs(raw.X-handle.WorldPoint.X)>0.8||
                    Math.Abs(raw.Y-handle.WorldPoint.Y)>0.8);
                Point current=raw;
                if(changed&&_gridSnap&&handle!=null&&
                    handle.Kind==MVIVisualHandleKind.Resize)
                    current=new Point(Math.Round(raw.X/25)*25,
                        Math.Round(raw.Y/25)*25);
                _activeHandle=null;
                _handleDragging=false;
                if(IsMouseCaptured)ReleaseMouseCapture();
                if(changed)
                {
                    EventHandler<MPWVisualHandleEventArgs> edited=HandleEditRequested;
                    if(edited!=null)edited(this,
                        new MPWVisualHandleEventArgs(handle,current.X,current.Y));
                }
                InvalidateVisual();
                e.Handled=true;
                return;
            }
            if (!_dragging) return;
            Point releasePointer=WorldPoint(e.GetPosition(this));
            double rawDx=releasePointer.X-_dragStart.X;
            double rawDy=releasePointer.Y-_dragStart.Y;
            MVIVisualSnapResult snapped=ResolveDrag(rawDx,rawDy);
            _dragging = false;
            if (IsMouseCaptured) ReleaseMouseCapture();
            // 判断是否拖动要用实际鼠标距离，而不是吸附后位移；
            // 用户移动不足1 DIP或吸附回原位时，都不创建无意义的MEM事务。
            if (_selection.HasValue&&
                (Math.Abs(rawDx)>1/_zoom||Math.Abs(rawDy)>1/_zoom)&&
                (Math.Abs(snapped.WorldDx)>1e-9||Math.Abs(snapped.WorldDy)>1e-9))
            {
                MVISceneItem original=_items.Find(item=>item.Id==_selection.Value);
                EventHandler<MPWMoveEventArgs> moved=MoveRequested;
                if(original!=null&&moved!=null)
                {
                    double x,y;
                    original.PositionAfterWorldDelta(snapped.WorldDx,snapped.WorldDy,out x,out y);
                    moved(this,new MPWMoveEventArgs(original.Id,x,y,
                        snapped.WorldDx,snapped.WorldDy));
                }
            }
            _dragX=0;_dragY=0;_dragSnap=null;
            UpdateWidgetDragPreview();
            InvalidateVisual();
        }
        public void CancelInteraction()
        {
            if(IsInlineTextEditing)FinishInlineTextEdit(false);
            _paletteDropGhost=null;
            _drawDragging=false;_drawPoints.Clear();
            _drawContourStarts.Clear();_nextPathContour=false;
            _dragging=false;_marquee=false;_handleDragging=false;_activeHandle=null;_panning=false;_dragX=0;_dragY=0;_dragSnap=null;
            _waypointDragging=false;_waypointConnectionId=null;
            _selectionTransformDragging=false;
            _connectDragging=false;_connectSourceId=null;_connectTargetId=null;
            _connectSourceSide=null;_connectTargetSide=null;_hoverConnectId=null;
            _rewireDragging=false;_rewireConnectionId=null;
            if(IsMouseCaptured)ReleaseMouseCapture();
            UpdateWidgetDragPreview();InvalidateVisual();}
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if(_selectionTransformDragging&&
                (e.Key==Key.LeftShift||e.Key==Key.RightShift||
                 e.Key==Key.LeftAlt||e.Key==Key.RightAlt||e.Key==Key.System))
                InvalidateVisual();
            if(e.Key==Key.Escape)
            {
                if(HasDrawPreview)CancelInteraction();
                else if(_drawTool!="Select")SetDrawTool("Select");
                else if(_geometryEditMode&&!_dragging&&!_handleDragging)
                {_geometryEditMode=false;InvalidateVisual();}
                else CancelInteraction();
                e.Handled=true;
            }
            else if(_designMode&&e.Key==Key.Back&&
                Keyboard.Modifiers==ModifierKeys.None&&
                object.ReferenceEquals(e.OriginalSource,this)&&
                RemoveLastDrawPoint())
            {
                // WPF内嵌TextBox的Backspace交给文本编辑器，不误删除草稿点。
                e.Handled=true;
            }
            else if(_designMode&&e.Key==Key.N&&
                _drawTool=="Path"&&HasDrawPreview&&
                Keyboard.Modifiers==ModifierKeys.None)
            {
                BeginNextPathContour();
                e.Handled=true;
            }
            else if(_designMode&&e.Key==Key.Enter&&
                IsPointDrawTool(_drawTool)&&HasDrawPreview)
            {
                FGEPoint2D pt=_drawPoints[_drawPoints.Count-1];
                CompleteDrawGesture(pt.X,pt.Y);
                e.Handled=true;
            }
            else if(_designMode&&!_geometryEditMode&&!_dragging&&!_handleDragging&&
                !_selectionTransformDragging&&!_waypointDragging&&!_connectDragging&&
                !_rewireDragging&&!_marquee&&NudgeSelectionRequested!=null&&
                (e.Key==Key.Left||e.Key==Key.Right||e.Key==Key.Up||e.Key==Key.Down)&&
                (Keyboard.Modifiers&(ModifierKeys.Control|ModifierKeys.Alt))==0&&
                object.ReferenceEquals(e.OriginalSource,this))
            {
                // Ctrl/Alt保留给Studio与专业输入；Shift将单次微调由1扩至10世界单位。
                // 只要选区含连接线、隐藏或锁定元素，就整体拒绝，不移动剩余部分。
                MVISceneItem[] editable=EditableSelection();
                if(editable.Length>0&&editable.Length==_selectedItems.Count)
                {
                    double step=(Keyboard.Modifiers&ModifierKeys.Shift)!=0?10:1;
                    double dx=e.Key==Key.Left?-step:e.Key==Key.Right?step:0;
                    double dy=e.Key==Key.Up?-step:e.Key==Key.Down?step:0;
                    NudgeSelectionRequested(new List<Guid>(_selectedItems).AsReadOnly(),dx,dy);
                    e.Handled=true;
                }
            }
            else if(_designMode&&_selection.HasValue&&VisualRouteRequested!=null&&
                _items.Exists(x=>x.Id==_selection.Value&&
                    x.ElementTypeId=="core.visual.connector")&&
                (e.Key==Key.O||e.Key==Key.L)&&
                Keyboard.Modifiers==ModifierKeys.None)
            {
                VisualRouteRequested(_selection.Value,
                    e.Key==Key.O?MPCVisualRouteKind.Orthogonal:MPCVisualRouteKind.Straight);
                e.Handled=true;
            }
            else if(_designMode&&e.Key==Key.Delete&&_selection.HasValue&&
                _items.Exists(x=>x.Id==_selection.Value&&
                    x.ElementTypeId=="core.visual.connector")&&
                DeleteConnectionRequested!=null)
            {
                DeleteConnectionRequested(_selection.Value);
                e.Handled=true;
            }
            base.OnKeyDown(e);
        }
        /// <summary>释放修饰键须立即刷新选区预览，但不能影响MEM Revision。</summary>
        protected override void OnKeyUp(KeyEventArgs e)
        {
            if(_selectionTransformDragging)InvalidateVisual();
            base.OnKeyUp(e);
        }
        protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            // 鼠标未捕获的Polyline/Polygon/Path同样必须在输入焦点离开时取消。
            if(HasDrawPreview||_dragging||_marquee||_handleDragging||
                _connectDragging||_rewireDragging||_waypointDragging||
                _selectionTransformDragging||_panning)
                CancelInteraction();
            base.OnLostKeyboardFocus(e);
        }
        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            if(HasDrawPreview||_dragging||_marquee||_handleDragging||
                _connectDragging||_rewireDragging||_waypointDragging||
                _selectionTransformDragging||_panning)
                CancelInteraction();
            base.OnLostMouseCapture(e);
        }
        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if(!_connectDragging&&!_rewireDragging&&_hoverConnectId.HasValue&&!IsMouseCaptured)
            {_hoverConnectId=null;InvalidateVisual();}
        }
    }
}
