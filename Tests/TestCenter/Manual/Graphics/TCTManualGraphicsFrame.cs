using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Win32;
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
        private MEMEngineeringSession _session;
        private MVIVisualEditor _editor;
        private readonly SceneView _canvas=new SceneView();
        private readonly MPWVisualOperationPanel _operations=new MPWVisualOperationPanel();
        private readonly TextBlock _status=new TextBlock {TextWrapping=TextWrapping.Wrap};
        private readonly TextBlock _details=new TextBlock {TextWrapping=TextWrapping.Wrap};
        private readonly ListBox _objects=new ListBox {MinHeight=120,SelectionMode=SelectionMode.Extended,DisplayMemberPath="Name"};
        // 仅显示/编辑工程定义上的局部几何参数；连接线/资源引用不得伪装成矩形属性。
        private readonly TextBox _x=new TextBox(),_y=new TextBox(),_width=new TextBox(),
            _height=new TextBox(),_angle=new TextBox();
        private string _savedFile;
        private int _created;
        private bool _closed,_refreshing;
        public string WorkspaceId {get{return "manual.graphics";}}
        public Guid ManualSessionId {get;private set;}
        public SceneView Canvas {get{return _canvas;}}
        public MPWVisualOperationPanel Operations {get{return _operations;}}
        public int Revision {get{return _session.Revision;}}
        public int SceneCount {get{return _editor.Scene.Count;}}
        public string StatusMessage {get{return _status.Text;}}
        public Guid ProjectId {get{return _session.Project.Id;}}
        public bool IsDirty {get{return _session.IsDirty;}}
        public Guid? SelectedId {get{return _editor.SelectedId;}}
        public IReadOnlyList<Guid> SceneIds {get{return _editor.Scene.Select(x=>x.Id).ToArray();}}
        public event Action StateChanged;
        public IEnumerable<TCTWorkspaceAction> Commands
        {
            get {return new[] {
                new TCTWorkspaceAction("graphics.undo","撤销图元操作",
                    ()=>Execute("edit.undo"),()=>_editor.CanUndo),
                new TCTWorkspaceAction("graphics.redo","重做图元操作",
                    ()=>Execute("edit.redo"),()=>_editor.CanRedo),
                new TCTWorkspaceAction("graphics.inspect","观察真实工程Revision",Inspect),
                new TCTWorkspaceAction("graphics.save","保存测试工程",SaveWithDialog),
                new TCTWorkspaceAction("graphics.open","打开测试工程",OpenWithDialog)
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
            _objects.SelectionChanged+=OnObjectTreeSelectionChanged;
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
            inspector.Children.Add(new TextBlock {Text="局部几何参数（选择可编辑图元）",
                FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,12,0,5)});
            AddGeometryField(inspector,"X",_x);
            AddGeometryField(inspector,"Y",_y);
            AddGeometryField(inspector,"宽度",_width);
            AddGeometryField(inspector,"高度",_height);
            AddGeometryField(inspector,"旋转角度",_angle);
            Button apply=new Button {Content="应用几何属性（单事务）",
                Padding=new Thickness(8),Margin=new Thickness(0,7,0,4)};
            AutomationProperties.SetAutomationId(apply,"TCT.Graphics.ApplyGeometry");
            apply.Click+=(sender,args)=>ApplyGeometry();
            inspector.Children.Add(apply);
            StackPanel files=new StackPanel {Orientation=Orientation.Horizontal,
                Margin=new Thickness(0,8,0,5)};
            Button save=new Button {Content="保存...",Margin=new Thickness(0,0,7,0)};
            Button open=new Button {Content="打开..."};
            AutomationProperties.SetAutomationId(save,"TCT.Graphics.Save");
            AutomationProperties.SetAutomationId(open,"TCT.Graphics.Open");
            save.Click+=(sender,args)=>SaveWithDialog();
            open.Click+=(sender,args)=>OpenWithDialog();
            files.Children.Add(save);files.Children.Add(open);inspector.Children.Add(files);
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

        private static void AddGeometryField(Panel host,string label,TextBox field)
        {
            DockPanel row=new DockPanel {Margin=new Thickness(0,2,0,2)};
            TextBlock caption=new TextBlock {Text=label,Width=76,
                VerticalAlignment=VerticalAlignment.Center};
            DockPanel.SetDock(caption,Dock.Left);row.Children.Add(caption);
            AutomationProperties.SetAutomationId(field,"TCT.Graphics.Geometry."+label);
            field.MinWidth=88;
            row.Children.Add(field);host.Children.Add(row);
        }
        /// <summary>树与画布使用同一场景ID，选择动作不写Revision，不改变工程对象身份。</summary>
        private void OnObjectTreeSelectionChanged(object sender,SelectionChangedEventArgs args)
        {
            if(_refreshing||_closed)return;
            Guid[] ids=_objects.SelectedItems.Cast<MVISceneItem>().Select(x=>x.Id).ToArray();
            _editor.Select(ids.Length==0?(Guid?)null:ids[ids.Length-1]);
            _canvas.SetSelectedVisualIds(ids);
            UpdateDetails();UpdateGeometryFields();UpdateAvailability();
        }
        public void SelectObjects(params Guid[] ids)
        {
            if(ids==null)throw new ArgumentNullException("ids");
            if(ids.Any(id=>!_editor.Scene.Any(item=>item.Id==id&&item.IsSelectable)))
                throw new InvalidOperationException("只能选择当前场景中的可选对象");
            _editor.Select(ids.Length==0?(Guid?)null:ids[ids.Length-1]);
            _canvas.SetSelectedVisualIds(ids);
            // 仅同步UI选择；测试可用此入口验证树与画布的同一身份。
            _refreshing=true;
            try
            {
                _objects.SelectedItems.Clear();
                foreach(MVISceneItem item in _objects.Items.Cast<MVISceneItem>())
                    if(ids.Contains(item.Id))_objects.SelectedItems.Add(item);
            }
            finally{_refreshing=false;}
            UpdateDetails();UpdateGeometryFields();UpdateAvailability();
        }
        private void WireCanvas()
        {
            // 唯一工程提交Owner是MVI/MTC；SceneView负责WPF手势和世界坐标预览。
            _canvas.NodeActivated+=(sender,args)=>_editor.Select(args.Id);
            _canvas.SelectionSetRequested+=ids=>{
                _editor.Select(ids.Count==0?(Guid?)null:ids[ids.Count-1]);
                _canvas.SetSelectedVisualIds(ids);UpdateDetails();UpdateAvailability();
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
                MVISceneItem[] items=_editor.Scene.ToArray();
                _canvas.SetScene(items,_editor.SelectedId);
                MVISceneItem[] previous=_objects.ItemsSource as MVISceneItem[];
                // 只有场景对象身份变化才重新建列表，防止鼠标多选时丢失列表选区。
                if(previous==null||!previous.Select(x=>x.Id).SequenceEqual(items.Select(x=>x.Id)))
                {
                    _objects.ItemsSource=items;
                    if(_editor.SelectedId.HasValue)
                        _objects.SelectedItem=items.FirstOrDefault(x=>x.Id==_editor.SelectedId.Value);
                }
                UpdateDetails();UpdateGeometryFields();UpdateAvailability();
            }
            finally{_refreshing=false;}
            Action changed=StateChanged;if(changed!=null)changed();
        }

        private void UpdateAvailability()
        {
            int count=_canvas.SelectedVisualIds.Count;
            MVISceneItem selected=_editor.SelectedId.HasValue?
                _editor.Scene.FirstOrDefault(x=>x.Id==_editor.SelectedId.Value):null;
            _operations.UpdateAvailability(count,
                selected!=null&&selected.ElementTypeId=="core.visual.connector",
                _editor.CanUndo,_editor.CanRedo,_canvas.IsDesignMode,
                selected!=null&&_session.Find(selected.Id) is MRCGroup);
        }
        private void UpdateGeometryFields()
        {
            Guid? id=_editor.SelectedId;
            MRCVisualItem shape=id.HasValue?_session.Find(id.Value) as MRCVisualItem:null;
            TextBox[] fields={_x,_y,_width,_height,_angle};
            if(shape==null||_canvas.SelectedVisualIds.Count!=1)
            {
                foreach(TextBox field in fields){field.Text="";field.IsEnabled=false;}
                return;
            }
            double[] values={shape.X,shape.Y,shape.Width,shape.Height,shape.Rotation};
            for(int i=0;i<fields.Length;i++)
            {
                // 有输入焦点时不覆盖用户尚未提交的数值。
                if(!fields[i].IsKeyboardFocusWithin)
                    fields[i].Text=values[i].ToString("G17",CultureInfo.InvariantCulture);
                fields[i].IsEnabled=true;
            }
        }
        private static double ReadNumber(TextBox field,string name)
        {
            double number;
            if(!double.TryParse(field.Text,NumberStyles.Float,
                CultureInfo.InvariantCulture,out number)||
                double.IsNaN(number)||double.IsInfinity(number))
                throw new ArgumentException(name+"必须是有限数字");
            return number;
        }
        public void ApplyGeometry()
        {
            if(!_editor.SelectedId.HasValue||_canvas.SelectedVisualIds.Count!=1)
            {Report("请选择一个可编辑图元，再填写局部几何属性。");return;}
            try
            {
                Guid id=_editor.SelectedId.Value;
                if(!(_session.Find(id) is MRCVisualItem))
                {Report("连接线或实例不是可直接修改的几何定义");return;}
                double x=ReadNumber(_x,"X"),y=ReadNumber(_y,"Y");
                double w=ReadNumber(_width,"宽度"),h=ReadNumber(_height,"高度"),
                    angle=ReadNumber(_angle,"旋转");
                if(w<=0||h<=0)throw new ArgumentException("宽度和高度必须大于零");
                Commit(_editor.SetGeometry(id,x,y,w,h,angle),"修改几何属性");
            }
            catch(Exception error){Report("几何属性被拒绝："+error.Message);}
        }
        private void SaveWithDialog()
        {
            SaveFileDialog dialog=new SaveFileDialog {Title="保存图元测试工程",
                Filter="CxVisual工程|*.cxproj",DefaultExt=".cxproj",AddExtension=true,
                FileName=string.IsNullOrEmpty(_savedFile)?"图元测试.cxproj":
                    Path.GetFileName(_savedFile)};
            if(dialog.ShowDialog()==true)
                try{SaveProject(dialog.FileName);}catch(Exception ex){Report("保存失败："+ex.Message);}
        }
        private void OpenWithDialog()
        {
            OpenFileDialog dialog=new OpenFileDialog {Title="打开图元测试工程",
                Filter="CxVisual工程|*.cxproj",CheckFileExists=true};
            if(dialog.ShowDialog()==true)
                try{OpenProject(dialog.FileName);}catch(Exception ex){Report("打开失败："+ex.Message);}
        }
        public void SaveProject(string path)
        {
            if(_closed)throw new ObjectDisposedException("TCTManualGraphicsFrame");
            if(string.IsNullOrWhiteSpace(path))throw new ArgumentException("保存路径不能为空");
            _session.SaveAs(path);
            _savedFile=path;
            Report("工程已保存："+Path.GetFileName(path)+" · Revision "+_session.Revision);
        }
        /// <summary>先完整加载新工程，再原子更换当前测试会话；失败时旧工程保留。</summary>
        public void OpenProject(string path)
        {
            if(_closed)throw new ObjectDisposedException("TCTManualGraphicsFrame");
            MEMEngineeringSession loaded=MEMEngineering.Open(path);
            MVIVisualEditor next=null;
            try
            {
                if(loaded.Project.Pages.Count==0)
                    throw new InvalidOperationException("目标工程没有可编辑Page");
                next=new MVIVisualEditor(loaded);
                next.SetPage(loaded.Project.Pages[0].Id);
            }
            catch
            {
                if(next!=null)next.Dispose();
                loaded.Close(true);
                throw;
            }
            _canvas.CancelInteraction();
            MVIVisualEditor previousEditor=_editor;
            MEMEngineeringSession previousSession=_session;
            previousEditor.Changed-=OnEditorChanged;
            _editor=next;_session=loaded;_savedFile=path;
            _editor.Changed+=OnEditorChanged;
            _canvas.SetSelectedVisualIds(new Guid[0]);
            previousEditor.Dispose();previousSession.Close(true);
            Refresh();
            Report("真实工程已重开："+Path.GetFileName(path)+" · Revision "+_session.Revision);
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
                if(operation=="edit.group")
                {
                    Guid[] ids=_canvas.SelectedVisualIds.ToArray();
                    MRCVisualItem[] items=ids.Select(id=>_session.Find(id) as MRCVisualItem)
                        .ToArray();
                    if(items.Length<2||items.Any(x=>x==null))
                    {Report("请选择两个以上同父级、同图层且可编辑的图元。");return;}
                    // 组合沿用Core唯一Group事务，不能通过UI搬移成员造第二棵树。
                    MRCGroup group=_session.Group(items,"组合"+(++_created));
                    _editor.Select(group.Id);
                    _canvas.SetSelectedVisualIds(new[]{group.Id});
                    Refresh();
                    Report("已组合 "+items.Length+" 个图元（单次Undo）");
                    return;
                }
                if(operation=="edit.ungroup")
                {
                    var group=_editor.SelectedId.HasValue?
                        _session.Find(_editor.SelectedId.Value) as MRCGroup:null;
                    if(group==null){Report("请先选中一个真实Group。");return;}
                    _session.Ungroup(group);
                    _editor.Select(null);
                    _canvas.SetSelectedVisualIds(new Guid[0]);
                    Refresh();Report("解组已提交，子图元身份保留（可Undo）");
                    return;
                }
                if(operation=="layer.front"||operation=="layer.back")
                {
                    if(!_editor.SelectedId.HasValue)
                    {Report("请先选择一个图元。");return;}
                    Commit(_editor.SetZOrder(_editor.SelectedId.Value,
                        operation=="layer.front"),"图元层序");return;
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