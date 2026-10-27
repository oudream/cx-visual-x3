using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Visual;
using CxVisual.Mechanism.Presentation.Wpf;
namespace CxVisual.TestCenter.UI
{
    /// <summary>
    /// 图元人工测试工作区：使用真正MEM/MVI会话和MPW画布、公共操作面板。
    /// 本页只负责ManualSession生命周期、操作转发、状态观察；不产生伪Passed。
    /// </summary>
    public sealed class TCTManualGraphicsFrame : UserControl, ITCTWorkspaceFrame
    {
        private readonly MEMEngineeringSession _session;
        private readonly MVIVisualEditor _editor;
        private readonly SceneView _canvas=new SceneView();
        private readonly MPWVisualOperationPanel _operations=new MPWVisualOperationPanel();
        private readonly TextBlock _status=new TextBlock {TextWrapping=TextWrapping.Wrap};
        private readonly TextBlock _details=new TextBlock {TextWrapping=TextWrapping.Wrap};
        private readonly ListBox _objects=new ListBox {MinHeight=120};
        private int _created;
        private bool _closed,_refreshing;
        public string WorkspaceId {get{return "manual.graphics";}}
        public Guid ManualSessionId {get;private set;}
        public SceneView Canvas {get{return _canvas;}}
        public MPWVisualOperationPanel Operations {get{return _operations;}}
        public int Revision {get{return _session.Revision;}}
        public int SceneCount {get{return _editor.Scene.Count;}}
        public string StatusMessage {get{return _status.Text;}}
        public event Action StateChanged;
        public IEnumerable<TCTWorkspaceAction> Commands
        {
            get {return new[] {
                new TCTWorkspaceAction("graphics.undo","撤销图元操作",
                    ()=>Execute("edit.undo"),()=>_editor.CanUndo),
                new TCTWorkspaceAction("graphics.redo","重做图元操作",
                    ()=>Execute("edit.redo"),()=>_editor.CanRedo),
                new TCTWorkspaceAction("graphics.inspect","观察真实工程Revision",Inspect)
            };}
        }
        public TCTManualGraphicsFrame()
        {
            ManualSessionId=Guid.NewGuid();
            _session=MEMEngineering.Create("图元测试-"+ManualSessionId.ToString("N").Substring(0,8));
            MEMPage page=_session.AddPage("图元绘制测试页");
            _editor=new MVIVisualEditor(_session);
            _editor.SetPage(page.Id);
            _canvas.IsDesignMode=true;
            _canvas.SnapToGrid=true;
            _canvas.SmartGuides=true;
            AutomationProperties.SetAutomationId(_canvas,"TCT.Graphics.Scene");
            AutomationProperties.SetAutomationId(_status,"TCT.Graphics.Status");
            AutomationProperties.SetAutomationId(_objects,"TCT.Graphics.Objects");
            _operations.OperationRequested+=Execute;
            WireCanvas();
            BuildLayout();
            _editor.Changed+=OnEditorChanged;
            Refresh();
            Report("已打开真实图元编辑会话。GE-01～15仍需逐条执行和验收。");
        }
        private void BuildLayout()
        {
            Grid layout=new Grid();
            layout.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(215)});
            layout.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
            layout.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(260)});
            layout.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});
            layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
            ScrollViewer tools=new ScrollViewer{Content=_operations,
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
                Background=new SolidColorBrush(Color.FromRgb(243,248,252)),
                Padding=new Thickness(8)};
            Grid.SetColumn(tools,0);layout.Children.Add(tools);
            Border board=new Border{Margin=new Thickness(4),BorderThickness=new Thickness(1),
                BorderBrush=Brushes.SlateGray,
                Background=new SolidColorBrush(Color.FromRgb(15,33,51)),Child=_canvas};
            Grid.SetColumn(board,1);layout.Children.Add(board);
            StackPanel inspector=new StackPanel{Margin=new Thickness(9)};
            inspector.Children.Add(new TextBlock{Text="工程对象 / 真实诊断",
                FontWeight=FontWeights.SemiBold,FontSize=16,Margin=new Thickness(0,3,0,10)});
            inspector.Children.Add(new TextBlock{Text="创建 Shape → 画布选择、拖动或连接固定锚点；"+
                "编辑控制柄与折点。操作成功仅表示MEM提交，不自动生成GE Passed。",
                TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10)});
            inspector.Children.Add(_details);
            inspector.Children.Add(new TextBlock{Text="当前场景项",FontWeight=FontWeights.SemiBold,
                Margin=new Thickness(0,12,0,4)});
            inspector.Children.Add(_objects);
            Button inspect=new Button{Content="观察工程状态",
                Padding=new Thickness(8),Margin=new Thickness(0,8,0,0)};
            AutomationProperties.SetAutomationId(inspect,"TCT.Graphics.Inspect");
            inspect.Click+=(sender,args)=>Inspect();inspector.Children.Add(inspect);
            ScrollViewer right=new ScrollViewer{Content=inspector,
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
            Grid.SetColumn(right,2);layout.Children.Add(right);
            Border bottom=new Border{Padding=new Thickness(10,7,10,7),Child=_status,
                BorderThickness=new Thickness(0,1,0,0),BorderBrush=Brushes.LightSteelBlue,
                Background=new SolidColorBrush(Color.FromRgb(231,239,247))};
            Grid.SetRow(bottom,1);Grid.SetColumnSpan(bottom,3);layout.Children.Add(bottom);
            Content=layout;
        }
        private void WireCanvas()
        {
            // 唯一工程提交Owner是MVI/MTC；SceneView负责WPF手势和世界坐标预览。
            _canvas.NodeActivated+=(sender,args)=>_editor.Select(args.Id);
            _canvas.SelectionSetRequested+=ids=>{
                _editor.Select(ids.Count==0?(Guid?)null:ids[ids.Count-1]);
                _canvas.SetSelectedVisualIds(ids);UpdateDetails();
            };
            _canvas.MoveRequested+=(sender,args)=>{
                var ids=_canvas.SelectedVisualIds;
                Commit(ids.Count>1&&ids.Contains(args.Id)?
                    _editor.MoveSelection(ids,args.WorldDeltaX,args.WorldDeltaY,false):
                    _editor.Move(args.Id,args.X,args.Y),"移动图元");
            };
            _canvas.NudgeSelectionRequested+=(ids,dx,dy)=>{
                if(ids.Count>1)Commit(_editor.MoveSelection(ids,dx,dy,false),"键盘移动");
                else if(ids.Count==1)
                {
                    MVISceneItem item=_editor.Scene.FirstOrDefault(entry=>entry.Id==ids[0]);
                    if(item==null){Report("目标已失效，拒绝移动");return;}
                    double x,y;item.PositionAfterWorldDelta(dx,dy,out x,out y);
                    Commit(_editor.Move(item.Id,x,y),"键盘移动");
                }
            };
            _canvas.ConnectRequested+=(a,aside,b,bside)=>
                Commit(_editor.ConnectVisual(a,aside,b,bside),"固定点连接");
            _canvas.ReconnectRequested+=(id,source,target,side)=>
                Commit(_editor.ReconnectVisual(id,source,target,side),"端点重附着");
            _canvas.DeleteConnectionRequested+=id=>Commit(_editor.Remove(id),"删除连线");
            _canvas.InsertWaypointRequested+=(id,index,x,y)=>
                Commit(_editor.InsertVisualWaypoint(id,index,x,y),"插入折点");
            _canvas.MoveWaypointRequested+=(id,index,x,y)=>
                Commit(_editor.MoveVisualWaypoint(id,index,x,y),"移动折点");
            _canvas.RemoveWaypointRequested+=(id,index)=>
                Commit(_editor.RemoveVisualWaypoint(id,index),"删除折点");
            _canvas.VisualRouteRequested+=(id,route)=>
                Commit(_editor.SetVisualRoute(id,route),"线路模式");
            _canvas.SelectionTransformRequested+=(ids,h,x,y,z,aspect,center,angle)=>
                Commit(_editor.TransformSelection(ids,h,x,y,z,aspect,center,angle),"多选变换");
            _canvas.HandleEditRequested+=(sender,args)=>
                Commit(_editor.EditHandle(args.Handle,args.WorldX,args.WorldY),"几何控制柄");
        }
        private void OnEditorChanged(object sender,EventArgs e){Refresh();}
        private void Refresh()
        {
            if(_closed||_refreshing)return;
            _refreshing=true;
            try
            {
                _canvas.SetScene(_editor.Scene.ToList(),_editor.SelectedId);
                _objects.ItemsSource=_editor.Scene.Select(x=>
                    x.Name+" · "+x.Id.ToString("N").Substring(0,8)).ToArray();
                _operations.SetOperationEnabled("edit.undo",_editor.CanUndo);
                _operations.SetOperationEnabled("edit.redo",_editor.CanRedo);
                UpdateDetails();
            }
            finally{_refreshing=false;}
            Action changed=StateChanged;if(changed!=null)changed();
        }
        private void UpdateDetails()
        {
            _details.Text="ManualSession "+ManualSessionId.ToString("N").Substring(0,8)+
                "\nMEM Revision "+_session.Revision+"\nScene "+_editor.Scene.Count+
                " 项\n选择 "+_canvas.SelectedVisualIds.Count+" 项"+
                "\nUndo "+(_editor.CanUndo?"可用":"无")+" / Redo "+(_editor.CanRedo?"可用":"无");
        }
        private void Report(string text)
        {
            if(_closed)return;
            _status.Text=text;UpdateDetails();
            Action changed=StateChanged;if(changed!=null)changed();
        }
        private void Commit(MTCEditResult<bool> result,string name)
        {
            Report(result.Success?name+"已提交 · Revision "+result.Revision:
                name+"被拒绝 · "+result.ErrorCode+" "+result.Message);
        }
        /// <summary>统一公共操作意图，目标不存在或格式不合法均给可见反馈而不伪造Passed。</summary>
        public void Execute(string operation)
        {
            if(_closed)throw new ObjectDisposedException("TCTManualGraphicsFrame");
            try
            {
                if(operation=="edit.undo"){Commit(_editor.Undo(),"撤销");return;}
                if(operation=="edit.redo"){Commit(_editor.Redo(),"重做");return;}
                if(operation=="edit.delete")
                {
                    if(!_editor.SelectedId.HasValue){Report("请先选择对象或视觉连接");return;}
                    Commit(_editor.Remove(_editor.SelectedId.Value),"删除");return;
                }
                if(operation.StartsWith("align.",StringComparison.Ordinal))
                {
                    Commit(_editor.Align(_canvas.SelectedVisualIds,operation.Substring(6)),"对齐");return;
                }
                if(operation.StartsWith("route.",StringComparison.Ordinal))
                {
                    if(!_editor.SelectedId.HasValue){Report("请先选择视觉连接");return;}
                    MPCVisualRouteKind route=operation=="route.straight"?
                        MPCVisualRouteKind.Straight:operation=="route.manual"?
                        MPCVisualRouteKind.Manual:MPCVisualRouteKind.Orthogonal;
                    Commit(_editor.SetVisualRoute(_editor.SelectedId.Value,route),"线路路由");return;
                }
                double offset=25+(_created%7)*34;
                MTCEditResult<bool> result;
                if(operation=="Text")result=_editor.AddText("文本"+(++_created),"文本",offset,offset);
                else if(operation=="WidgetButton"||
                    operation.StartsWith("core.widget.",StringComparison.Ordinal))
                    result=_editor.AddWidget("Widget"+(++_created),
                        operation=="WidgetButton"?"core.widget.button":operation,
                        offset,offset);
                else
                {
                    MRCShapeKind kind=(MRCShapeKind)Enum.Parse(typeof(MRCShapeKind),operation);
                    result=_editor.AddShape(kind.ToString()+(++_created),
                        MVIShapeCatalog.CreateDefault(kind),offset,offset);
                }
                Commit(result,"创建 "+operation);
            }
            catch(Exception error){Report("操作被拒绝："+error.Message);}
        }
        public void Inspect()
        {
            Refresh();
            Report("真实工程观察：Scene "+SceneCount+" 项，Revision "+Revision+
                "；尚未执行的GE用例不标记Passed。");
        }
        public void OnWorkspaceClosed()
        {
            if(_closed)return;
            _canvas.CancelInteraction();
            _operations.OperationRequested-=Execute;
            _editor.Changed-=OnEditorChanged;
            _editor.Dispose();
            _session.Close(true);
            _closed=true;
        }
    }
}