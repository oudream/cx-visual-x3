using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AvalonDock.Layout;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Visual;
using CxVisual.Mechanism.Orchestration;
using CxVisual.Mechanism.Presentation.Wpf;

namespace CxVisual.Studio
{
    /// <summary>
    /// R06/P0-2：真正的页面、共享Content/Symbol及Content逻辑编辑Tab。
    /// 每个Tab有独立视图/选择，始终复用单一MEM工程会话、MOL定义和Undo历史。
    /// </summary>
    public partial class MainWindow
    {
        private const string PageDocumentPrefix = "doc.page.";
        private const string ContentDocumentPrefix = "doc.content.";
        private const string SymbolDocumentPrefix = "doc.symbol.";
        private const string FlowResourceDocumentPrefix = "doc.resourceflow.";
        private readonly Dictionary<string, ResourceDocumentContext> _pageDocuments =
            new Dictionary<string, ResourceDocumentContext>(StringComparer.Ordinal);
        private bool _switchingPageDocument;
        private bool _closingPageDocuments;
        private MEMEngineeringSession _pageDocumentSession;
        // 工程Dirty的唯一真源仍然是MEM；这里只保存界面级变更来源提示。
        private IDisposable _resourceDocumentChanges;
        private readonly HashSet<Guid> _resourceChangedRoots=new HashSet<Guid>();
        private readonly HashSet<Guid> _resourceAffectedRoots=new HashSet<Guid>();
        // 只缓存UI诊断所需的TargetId→MEM资源根身份，用于Undo删除对象后恢复归属。
        // 工程真源、变更与历史仍由MEM持有；切换工程释放缓存，不持有MEM节点引用。
        private readonly Dictionary<Guid,Guid> _resourceMutationOwners=new Dictionary<Guid,Guid>();
        private const int MaxStudioMutationOwners=65536;
        private void RememberStudioMutationOwner(Guid target,Guid root)
        {
            if(target==Guid.Empty||root==Guid.Empty)return;
            if(_resourceMutationOwners.Count>=MaxStudioMutationOwners &&
                !_resourceMutationOwners.ContainsKey(target))
                _resourceMutationOwners.Clear(); // 超限则退化为保守影响提示，绝不回写工程
            _resourceMutationOwners[target]=root;
        }

        private void AttachStudioResourceChangeWatch(MEMEngineeringSession session)
        {
            if(_resourceDocumentChanges!=null)
            {_resourceDocumentChanges.Dispose();_resourceDocumentChanges=null;}
            _resourceChangedRoots.Clear();
            _resourceAffectedRoots.Clear();
            _resourceMutationOwners.Clear();
            _pageDocumentSession=session;
            if(session!=null)
            {
                MEMEngineeringSession observed=session;
                _resourceDocumentChanges=session.Watch(change=>
                    OnStudioResourceMutation(observed,change));
            }
        }
        private void OnStudioResourceMutation(MEMEngineeringSession observed,MTCChangeSet change)
        {
            if(!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(()=>
                    OnStudioResourceMutation(observed,change)));
                return;
            }
            if(_viewModel==null||_closingPageDocuments||
                !ReferenceEquals(observed,_viewModel.ProjectSession))return;
            if(!observed.IsDirty)
            {
                _resourceChangedRoots.Clear();_resourceAffectedRoots.Clear();
                RefreshStudioResourceCaptions();
                return;
            }
            var changed=new HashSet<Guid>();
            bool unresolved=false;
            foreach(MTCChangeOperation operation in change.Operations)
            {
                MEMNode node=observed.Find(operation.TargetId);
                if(node!=null)
                {
                    Guid root=observed.GetAddress(node).ResourceId;
                    if(root!=Guid.Empty&&root!=observed.Project.Id)
                    {
                        changed.Add(root);
                        RememberStudioMutationOwner(operation.TargetId,root);
                    }
                    continue;
                }
                MRIResourceReference reference=observed.FindReference(operation.TargetId);
                if(reference!=null)
                {
                    Guid root=observed.GetAddress(reference.Owner).ResourceId;
                    changed.Add(root);
                    RememberStudioMutationOwner(operation.TargetId,root);
                    continue;
                }
                MPCPort port=observed.FindPort(operation.TargetId);
                if(port!=null)
                {
                    MEMNode owner=observed.Find(port.OwnerId);
                    if(owner!=null)
                    {
                        Guid root=observed.GetAddress(owner).ResourceId;
                        changed.Add(root);
                        RememberStudioMutationOwner(operation.TargetId,root);
                        continue;
                    }
                }
                // 已删除的对象失去正向索引时，宁可显示保守提示，不能宣称它们干净。
                if(observed.GetResourceEntry(operation.TargetId)!=null)
                {
                    changed.Add(operation.TargetId);
                    RememberStudioMutationOwner(operation.TargetId,operation.TargetId);
                    continue;
                }
                // Undo(Add)/Redo(Remove)之后TargetId不在现行工程索引，但已在前一次
                // 正向编辑时记录对应的真实MEM资源根。仅用于Studio脏标记的定位。
                Guid formerRoot;
                if(_resourceMutationOwners.TryGetValue(operation.TargetId,out formerRoot)&&
                    observed.Find(formerRoot)!=null)
                {
                    changed.Add(formerRoot);
                    continue;
                }
                unresolved=true;
            }
            foreach(Guid root in changed.Where(x=>x!=Guid.Empty))
            {
                _resourceChangedRoots.Add(root);
                try
                {
                    MRIResourceImpactReport report=observed.AnalyzeResourceImpact(root);
                    foreach(MRIResourceImpact item in report.Affected)
                        if(!_resourceChangedRoots.Contains(item.ResourceId))
                            _resourceAffectedRoots.Add(item.ResourceId);
                }
                catch(InvalidOperationException)
                {
                    // 当前真实资源已经删除或不适合分析时，标记所有可见Tab待核。
                    unresolved=true;
                }
            }
            if(unresolved)
                foreach(ResourceDocumentContext context in _pageDocuments.Values)
                    _resourceAffectedRoots.Add(
                        ResourceDocumentRoot(observed,context.ResourceId));
            RefreshStudioResourceCaptions();
        }
        private static Guid ResourceDocumentRoot(MEMEngineeringSession session,Guid resourceId)
        {
            MEMNode node=session.Find(resourceId);
            return node==null?resourceId:session.GetAddress(node).ResourceId;
        }
        private void RefreshStudioResourceCaptions()
        {
            if(_viewModel==null||_closingPageDocuments)return;
            bool dirty=_viewModel.ProjectSession.IsDirty;
            if(!dirty){_resourceChangedRoots.Clear();_resourceAffectedRoots.Clear();}
            foreach(KeyValuePair<string,ResourceDocumentContext> pair in _pageDocuments)
            {
                LayoutDocument document=FindStudioContent(pair.Key) as LayoutDocument;
                MEMNode node=_viewModel.ProjectSession.Find(pair.Value.ResourceId);
                if(document==null||node==null)continue;
                Guid root=_viewModel.ProjectSession.GetAddress(node).ResourceId;
                string marker=dirty&&_resourceChangedRoots.Contains(root)?" *":
                    dirty&&_resourceAffectedRoots.Contains(root)?" ~":"";
                document.Title=node.Name+marker+
                    (pair.Value.IsFlow?" [流程]":" [设计]");
            }
        }

        private sealed class ResourceDocumentContext : IDisposable
        {
            private readonly MainWindow _owner;
            private bool _disposed;
            internal readonly Guid ResourceId;
            internal readonly string Key;
            internal readonly bool IsFlow;
            internal readonly MEMEngineeringSession Session;
            internal readonly CxVisualEditor Editor;
            internal readonly SceneView Scene;
            internal readonly MOLFlowCanvas Flow;
            internal readonly DockPanel Panel;
            internal readonly TextBlock Status;
            private readonly IDisposable _watch;

            internal ResourceDocumentContext(MainWindow owner, MEMEngineeringSession session,
                MEMNode resource,string key,bool flow)
            {
                _owner=owner;Session=session;ResourceId=resource.Id;Key=key;IsFlow=flow;
                Panel=new DockPanel();
                Status=new TextBlock{Margin=new Thickness(6,4,6,3),TextWrapping=TextWrapping.Wrap};
                StackPanel top=new StackPanel{Orientation=Orientation.Vertical};
                WrapPanel actions=new WrapPanel();
                top.Children.Add(actions);top.Children.Add(Status);
                DockPanel.SetDock(top,Dock.Top);
                Panel.Children.Add(top);
                if(flow)
                {
                    Flow=new MOLFlowCanvas{Focusable=true,MinHeight=410};
                    Panel.Children.Add(Flow);
                    actions.Children.Add(Button("撤销",()=>Undo(false)));
                    actions.Children.Add(Button("重做",()=>Undo(true)));
                    actions.Children.Add(Button("打开图元",()=>
                        _owner.OpenStudioResourceDocument(ResourceId,false)));
                    Flow.BlockMoveRequested+=(s,e)=>ApplyFlow(
                        "移动流程节点", b=>b.Move(e.BlockId,e.X,e.Y));
                    Flow.BlocksMoveRequested+=(s,e)=>ApplyFlow(
                        "批量移动流程节点",b=>b.MoveMany(e.BlockIds,e.DeltaX,e.DeltaY));
                    Flow.PortLinkRequested+=(s,e)=>ApplyFlow(
                        "连接类型化端口",b=>b.Connect(e.SourceBlockId,e.SourcePort,
                            e.TargetBlockId,e.TargetPort,e.Kind));
                    Flow.PortLinkRejected+=(s,e)=>Status.Text="端口连线被拒绝："+e.Reason;
                    _watch=Session.Watch(change=>OnChanged(this,EventArgs.Empty));
                }
                else
                {
                    Editor=new CxVisualEditor(session);
                    MEMPage page=resource as MEMPage;
                    if(page!=null)Editor.SetPage(page.Id);
                    else Editor.OpenDefinition(resource.Id);
                    Scene=new SceneView{Focusable=true};
                    Scene.SetAssetResolver(id=>Session.OpenAsset(id));
                    Panel.Children.Add(Scene);
                    actions.Children.Add(Button("添加矩形",()=>
                    {
                        int count=Editor.Scene.Count+1;
                        MTCEditResult<bool> result=Editor.AddRectangle(
                            "矩形 "+count,60+count*15,55+count*12,110,65);
                        Status.Text=result.Success?"已通过MEM事务添加图元":
                            "添加失败："+result.ErrorCode+" "+result.Message;
                    }));
                    actions.Children.Add(Button("撤销",()=>Undo(false)));
                    actions.Children.Add(Button("重做",()=>Undo(true)));
                    MEMContent content=resource as MEMContent;
                    if(content!=null)
                        actions.Children.Add(Button("打开流程",()=>
                            _owner.OpenStudioResourceDocument(ResourceId,true)));
                    if(page!=null)
                    {
                        actions.Children.Add(Button("打开本页流程",()=>
                            _owner.OpenStudioResourceDocument(page.RootContent.Id,true)));
                        actions.Children.Add(Button("完整图元编辑",()=>
                        {
                            MEMPage actual=Session.Find(ResourceId) as MEMPage;
                            if(actual==null)return;
                            _owner._viewModel.ActivePage=actual;
                            _owner.OpenStudioDocument("doc.visual");
                        }));
                    }
                    Scene.NodeActivated+=(s,e)=>Select(e.Id);
                    Scene.SelectionSetRequested+=ids=>
                    {
                        if(_disposed||!_owner.IsStudioDocumentActive(Key))return;
                        Guid? selected=ids.Count==1?(Guid?)ids[0]:null;
                        Select(selected);
                        Scene.SetSelectedVisualIds(ids);
                    };
                    Scene.MoveRequested+=(s,e)=>
                    {
                        if(!CanEditVisual())return;
                        IReadOnlyList<Guid> ids=Scene.SelectedVisualIds;
                        MTCEditResult<bool> result=ids.Count>1&&ids.Contains(e.Id)?
                            Editor.MoveSelection(ids,e.WorldDeltaX,e.WorldDeltaY,Scene.SnapToGrid):
                            Editor.Move(e.Id,e.X,e.Y);
                        if(!result.Success)Status.Text="拖动失败："+result.ErrorCode+" "+result.Message;
                    };
                    Scene.HandleEditRequested+=(s,e)=>
                    {
                        if(!CanEditVisual())return;
                        MTCEditResult<bool> result=Editor.EditHandle(
                            e.Handle,e.WorldX,e.WorldY);
                        Status.Text=result.Success?"控制柄修改已提交MEM事务":
                            "控制柄修改拒绝："+result.ErrorCode+" "+result.Message;
                    };
                    Editor.Changed+=OnChanged;
                }
                Refresh();
            }

            private static Button Button(string title,Action callback)
            {
                Button button=new Button{Content=title,Padding=new Thickness(9,5,9,5),
                    Margin=new Thickness(2)};
                button.Click+=(s,e)=>callback();
                return button;
            }
            // 独立资源的绘图命令只使用此Tab自己的Editor与MEM会话。
            internal bool InsertVisual(string kind)
            {
                if(_disposed||Editor==null||!_owner.IsStudioDocumentActive(Key)||
                    !ReferenceEquals(Session,_owner._viewModel.ProjectSession)||
                    Session.Find(ResourceId)==null)
                {
                    Status.Text="图元资源不可编辑，已拒绝插入";
                    return false;
                }
                int index=Editor.Scene.Count+1;
                MTCEditResult<bool> result;
                if(kind=="Text")
                    result=Editor.AddText("文字 "+index,"双击属性面板修改",
                        45+index*14,40+index*10);
                else if(kind=="WidgetButton")
                    result=Editor.AddWidget("按钮 "+index,"core.widget",
                        45+index*16,55+index*12);
                else if(kind!=null&&kind.StartsWith("core.widget.",StringComparison.Ordinal))
                    result=Editor.AddWidget(kind.Substring(kind.LastIndexOf('.')+1)+" "+index,
                        kind,45+index*16,55+index*12);
                else
                {
                    MRCShapeKind shapeKind;
                    if(!Enum.TryParse(kind,out shapeKind)||
                        !Enum.IsDefined(typeof(MRCShapeKind),shapeKind))
                    {
                        Status.Text="不支持的图元类型："+kind;
                        return false;
                    }
                    result=Editor.AddShape(shapeKind+" "+index,
                        MVIShapeCatalog.CreateDefault(shapeKind),
                        45+(index%7)*24,55+(index%7)*20);
                }
                Status.Text=result.Success?"已通过MEM事务插入："+kind:
                    "插入失败："+result.ErrorCode+" "+result.Message;
                return result.Success;
            }
            // 使用真实活动Tab的多选结果，而不是旧固定页面ViewModel的_selection。
            // 真正的几何坐标变换、可选择权限与事务全部归CxVisualEditor/MEM。
            private bool CanEditVisual()
            {
                return !_disposed&&Editor!=null&&_owner.IsStudioDocumentActive(Key)&&
                    ReferenceEquals(Session,_owner._viewModel.ProjectSession)&&
                    Session.Find(ResourceId)!=null;
            }
            private Guid? SelectedDefinition()
            {
                if(!CanEditVisual()||Scene.SelectedVisualIds.Count!=1)return null;
                Guid id=Scene.SelectedVisualIds[0];
                return Editor.Scene.Any(x=>x.Id==id&&x.IsSelectable)&&
                    Session.Find(id) is MRCVisualItem?(Guid?)id:null;
            }
            internal bool EditGeometry(double x,double y,double width,double height,double rotation)
            {
                Guid? id=SelectedDefinition();
                if(!id.HasValue){Status.Text="请选择当前图元定义，禁止跨资源或多选修改几何";return false;}
                MTCEditResult<bool> result=Editor.SetGeometry(id.Value,x,y,width,height,rotation);
                Status.Text=result.Success?"几何属性已提交MEM事务":
                    "几何属性拒绝："+result.ErrorCode+" "+result.Message;
                return result.Success;
            }
            internal bool EditPath(int index,string operation)
            {
                Guid? id=SelectedDefinition();
                if(!id.HasValue){Status.Text="请选择当前路径定义";return false;}
                MTCEditResult<bool> result=Editor.EditPathSegment(id.Value,index,operation);
                Status.Text=result.Success?"路径操作已提交MEM事务":
                    "路径修改拒绝："+result.ErrorCode+" "+result.Message;
                return result.Success;
            }
            internal bool ChangeLayer(string layer,bool toFront)
            {
                Guid? id=SelectedDefinition();
                if(!id.HasValue){Status.Text="图层修改只允许选择一个当前资源定义";return false;}
                MTCEditResult<bool> result=Editor.SetLayer(id.Value,layer,toFront);
                Status.Text=result.Success?"图层属性已提交MEM事务":
                    "图层属性拒绝："+result.ErrorCode+" "+result.Message;
                return result.Success;
            }
            internal bool DeleteVisualSelection()
            {
                Guid? id=SelectedDefinition();
                if(!id.HasValue){Status.Text="删除只允许选择当前可编辑图元定义";return false;}
                MTCEditResult<bool> result=Editor.Remove(id.Value);
                Status.Text=result.Success?"已通过MEM事务删除当前图元":
                    "删除拒绝："+result.ErrorCode+" "+result.Message;
                if(result.Success)
                {
                    Scene.SetSelectedVisualIds(new Guid[0]);
                    Editor.Select(null);
                    _owner._viewModel.Select(null);
                    _owner.RefreshStudioGraphicsTree();
                }
                return result.Success;
            }
            internal bool ApplyHistory(bool redo)
            {
                if(_disposed||!_owner.IsStudioDocumentActive(Key)||
                    !ReferenceEquals(Session,_owner._viewModel.ProjectSession))return false;
                MTCEditResult<bool> result=redo?Session.Redo():Session.Undo();
                Status.Text=result.Success?(redo?"已重做MEM工程事务":"已撤销MEM工程事务"):
                    "工程历史操作拒绝："+result.ErrorCode+" "+result.Message;
                return result.Success;
            }
            internal void ClearSelection()
            {
                if(!CanEditVisual())return;
                Scene.SetSelectedVisualIds(new Guid[0]);
                Editor.Select(null);
                _owner._viewModel.Select(null);
                _owner.RefreshStudioGraphicsTree();
            }
            internal bool AlignVisuals(string mode)
            {
                if(_disposed||Editor==null||!_owner.IsStudioDocumentActive(Key)||
                    !ReferenceEquals(Session,_owner._viewModel.ProjectSession)||
                    Session.Find(ResourceId)==null)return false;
                IReadOnlyList<Guid> selected=Scene.SelectedVisualIds;
                MTCEditResult<bool> result=Editor.Align(selected,mode);
                Status.Text=result.Success?"当前资源已原子对齐/分布："+mode:
                    "对齐未执行："+result.ErrorCode+" "+result.Message;
                return result.Success;
            }
            internal bool ChangeVisualZOrder(bool toFront)
            {
                if(_disposed||Editor==null||!_owner.IsStudioDocumentActive(Key)||
                    !ReferenceEquals(Session,_owner._viewModel.ProjectSession)||
                    Session.Find(ResourceId)==null)return false;
                IReadOnlyList<Guid> selected=Scene.SelectedVisualIds;
                if(selected.Count!=1)
                {
                    Status.Text="图层顺序需要当前资源恰好一个可编辑图元";
                    return false;
                }
                MTCEditResult<bool> result=Editor.SetZOrder(selected[0],toFront);
                Status.Text=result.Success?"当前资源已调整图层顺序":
                    "图层顺序未修改："+result.ErrorCode+" "+result.Message;
                return result.Success;
            }
            internal void Select(Guid? id)
            {
                if(!CanEditVisual())return;
                if(id.HasValue&&!Editor.Scene.Any(x=>x.Id==id.Value&&x.IsSelectable))
                {
                    Status.Text="选择拒绝：图元不属于当前可编辑资源";
                    return;
                }
                if(!Editor.Select(id))return;
                _owner._viewModel.Select(id);
                _owner.RefreshVisualInspector();
                _owner.RefreshStudioGraphicsTree();
            }
            private void Undo(bool redo)
            {
                MTCEditResult<bool> result=redo?Session.Redo():Session.Undo();
                Status.Text=result.Success?(redo?"已重做MEM事务":"已撤销MEM事务"):
                    (redo?"重做":"撤销")+"失败："+result.Message;
                Refresh();
            }
            private void ApplyFlow(string operation,Action<MOCFlowBuilder> edit)
            {
                if(_disposed||!_owner.IsStudioDocumentActive(Key))return;
                try
                {
                    MEMContent content=Session.Find(ResourceId) as MEMContent;
                    if(content==null)throw new InvalidOperationException("流程Content已不存在");
                    MOCFlowBuilder.Edit(Session,content,edit);
                    Refresh();
                    Status.Text=operation+"：已提交MEM，支持统一Undo/Redo";
                }
                catch(Exception ex){Status.Text=operation+"失败："+ex.Message;}
            }
            private void OnChanged(object sender,EventArgs args)
            {
                if(_disposed)return;
                if(!_owner.Dispatcher.CheckAccess())
                {
                    _owner.Dispatcher.BeginInvoke(new Action(Refresh));
                    return;
                }
                Refresh();
                if(_owner.IsStudioDocumentActive(Key)&&Editor!=null)
                    _owner.RefreshStudioGraphicsTree();
            }
            internal void Refresh()
            {
                if(_disposed)return;
                MEMNode resource=Session.Find(ResourceId);
                if(resource==null)
                {
                    Status.Text="工程资源已删除，请关闭此文档";
                    if(Scene!=null)Scene.SetScene(new List<CxSceneItem>(),null);
                    if(Flow!=null){Flow.Definition=null;Flow.WiringDefinition=null;}
                    return;
                }
                if(IsFlow)
                {
                    try
                    {
                        MEMNodeSnapshot snapshot=Session.GetSnapshot().Find(ResourceId);
                        Flow.WiringDefinition=MOLWiringCodec.Get(snapshot);
                        Flow.Definition=Flow.WiringDefinition==null?MOLLogicCodec.Get(snapshot):null;
                        Flow.RetainValidSelection();
                        Status.Text=resource.Name+" | MEM Revision "+Session.Revision+
                            " | 独立流程画布，节点/Port操作走MOC+MEM事务";
                    }
                    catch(Exception ex)
                    {
                        Flow.WiringDefinition=null;Flow.Definition=null;
                        Status.Text="流程无法呈现："+ex.Message;
                    }
                }
                else
                {
                    Guid[] selected=Scene.SelectedVisualIds.ToArray();
                    Scene.SetScene(Editor.Scene,Editor.SelectedId);
                    // MEM重投影不丢活动资源自己的单选/多选；显式Editor导航另行优先。
                    // SetSelectedVisualIds自动排除不再可见/已删除的ID。
                    if(selected.Length>0&&!Editor.SelectedId.HasValue)
                        Scene.SetSelectedVisualIds(selected);
                    Status.Text=resource.Name+" | MEM Revision "+Session.Revision+
                        " | 独立视口与选择，统一Undo/Redo";
                }
            }
            public void Dispose()
            {
                if(_disposed)return;_disposed=true;
                if(Editor!=null){Editor.Changed-=OnChanged;Editor.Dispose();}
                if(_watch!=null)_watch.Dispose();
                if(Scene!=null)Scene.SetAssetResolver(null);
                if(Flow!=null){Flow.WiringDefinition=null;Flow.Definition=null;}
            }
        }

        private static string PageDocumentId(Guid id){return PageDocumentPrefix+id.ToString("N");}
        private static bool TryParsePageDocumentId(string key,out Guid id)
        {
            string kind;
            return TryParseResourceDocumentId(key,out id,out kind)&&kind=="page";
        }
        private static bool TryParseResourceDocumentId(string key,out Guid id,out string kind)
        {
            id=Guid.Empty;kind=null;
            if(key==null)return false;
            string prefix;
            if(key.StartsWith(PageDocumentPrefix,StringComparison.Ordinal))
            {prefix=PageDocumentPrefix;kind="page";}
            else if(key.StartsWith(ContentDocumentPrefix,StringComparison.Ordinal))
            {prefix=ContentDocumentPrefix;kind="content";}
            else if(key.StartsWith(SymbolDocumentPrefix,StringComparison.Ordinal))
            {prefix=SymbolDocumentPrefix;kind="symbol";}
            else if(key.StartsWith(FlowResourceDocumentPrefix,StringComparison.Ordinal))
            {prefix=FlowResourceDocumentPrefix;kind="flow";}
            else return false;
            if(!Guid.TryParseExact(key.Substring(prefix.Length),"N",out id)||id==Guid.Empty)
            {kind=null;return false;}
            return true;
        }
        private bool ResourceDocumentExists(string key)
        {
            Guid id;string kind;
            if(!TryParseResourceDocumentId(key,out id,out kind))return false;
            MEMNode node=_viewModel.ProjectSession.Find(id);
            if(kind=="flow")return node is MEMContent;
            if(kind=="page")return node is MEMPage;
            IMEMResource standalone=_viewModel.ProjectSession.Project.FindResource(id);
            return kind=="content"?standalone is MEMContent:standalone is MEMSymbol;
        }
        private static string ResourceDocumentId(MEMNode resource,bool flow)
        {
            if(flow)return FlowResourceDocumentPrefix+resource.Id.ToString("N");
            if(resource is MEMPage)return PageDocumentId(resource.Id);
            if(resource is MEMSymbol)return SymbolDocumentPrefix+resource.Id.ToString("N");
            if(resource is MEMContent)return ContentDocumentPrefix+resource.Id.ToString("N");
            throw new InvalidOperationException("仅支持真实MEM Page/Content/Symbol");
        }
        private ResourceDocumentContext CreateStudioPageContext(string key)
        {
            if(!ResourceDocumentExists(key))
                throw new InvalidDataException("失效或跨工程资源文档："+key);
            ResourceDocumentContext existing;
            if(_pageDocuments.TryGetValue(key,out existing))return existing;
            Guid id;string kind;TryParseResourceDocumentId(key,out id,out kind);
            ResourceDocumentContext ctx=new ResourceDocumentContext(this,
                _viewModel.ProjectSession,_viewModel.ProjectSession.Find(id),key,kind=="flow");
            _pageDocuments.Add(key,ctx);_studioViews[key]=ctx.Panel;
            return ctx;
        }
        private void RegisterStudioPageClosing(LayoutDocument document)
        {
            document.Closed+=(s,e)=>
            {
                ResourceDocumentContext context;
                if(_pageDocuments.TryGetValue(document.ContentId,out context))
                {
                    _pageDocuments.Remove(document.ContentId);
                    _studioViews.Remove(document.ContentId);
                    context.Dispose();
                }
                if(_activeStudioDocumentId==document.ContentId&&!_closingPageDocuments)
                {
                    LayoutDocument fallback=StudioDock.Layout.ActiveContent as LayoutDocument;
                    SelectStudioContext(fallback==null?"doc.visual":fallback.ContentId);
                    RefreshStudioMenus();
                }
            };
        }
        public SceneView GetStudioPageScene(Guid pageId)
        {return GetStudioResourceScene(pageId);}
        public SceneView GetStudioResourceScene(Guid resourceId)
        {
            MEMNode resource=_viewModel.ProjectSession.Find(resourceId);
            if(resource==null)return null;
            ResourceDocumentContext ctx;
            return _pageDocuments.TryGetValue(ResourceDocumentId(resource,false),out ctx)?ctx.Scene:null;
        }
        public MOLFlowCanvas GetStudioResourceFlow(Guid contentId)
        {
            ResourceDocumentContext ctx;
            return _pageDocuments.TryGetValue(FlowResourceDocumentPrefix+contentId.ToString("N"),
                out ctx)?ctx.Flow:null;
        }
        /// <summary>真实MRK轨迹的ContentId/BlockId下钻原MOL V2定义，失败不切换Tab。</summary>
        public bool LocateStudioFlowSource(Guid contentId,Guid blockId)
        {
            if(contentId==Guid.Empty||blockId==Guid.Empty)return false;
            MEMEngineeringSession session=_viewModel.ProjectSession;
            MEMContent content=session.Find(contentId) as MEMContent;
            if(content==null)return false;
            MEMNodeSnapshot snapshot=session.GetSnapshot().Find(contentId);
            if(snapshot==null)return false;
            MOLWiringDefinition wiring=MOLWiringCodec.Get(snapshot);
            if(wiring==null||!wiring.Blocks.Any(block=>block.Id==blockId))return false;
            OpenStudioResourceDocument(contentId,true);
            MOLFlowCanvas flow=GetStudioResourceFlow(contentId);
            if(flow==null||flow.WiringDefinition==null)
                throw new InvalidOperationException("真实MOL画布没有装载源节点");
            flow.SetSelection(new[]{blockId});
            return true;
        }
        /// <summary>
        /// MOC计划Revision/工程身份必须与当前MEM真源一致，才允许按旧MRK来源导航。
        /// 只校验与投影，不持有Plan、不更改工程、不维护第二套版本仓库。
        /// </summary>
        public bool LocateStudioFlowSource(Guid projectId,int planRevision,
            Guid contentId,Guid blockId)
        {
            MEMEngineeringSession session=_viewModel.ProjectSession;
            if(session==null||projectId==Guid.Empty||
                session.Project.Id!=projectId||session.Revision!=planRevision)return false;
            return LocateStudioFlowSource(contentId,blockId);
        }
        private ResourceDocumentContext ActiveStudioResourceContext()
        {
            ResourceDocumentContext ctx;
            return _pageDocuments.TryGetValue(_activeStudioDocumentId,out ctx)?ctx:null;
        }
        // Ribbon和旧工具箱共用真实的活动DocumentId；错误资源不可落到固定页面。
        private bool RouteStudioResourceVisualInsertion(string kind)
        {
            Guid id;string resourceKind;
            if(!TryParseResourceDocumentId(_activeStudioDocumentId,out id,out resourceKind))
                return false;
            ResourceDocumentContext context=ActiveStudioResourceContext();
            if(context==null||!ResourceDocumentExists(_activeStudioDocumentId)||
                !ReferenceEquals(context.Session,_viewModel.ProjectSession))
            {
                _viewModel.ReportStudioShellWarning("资源文档已失效，拒绝绘图命令。");
                return true;
            }
            if(resourceKind=="flow"||context.IsFlow)
            {
                _viewModel.ReportStudioShellWarning("流程画布不能插入图元。");
                return true;
            }
            // 保留R06旧页面编辑器上下文契约：Ribbon目标来自真实活动Tab，
            // 页面级VM也同步此Page；Content/Symbol绝不可强行切回旧Page。
            if(resourceKind=="page")
            {
                MEMPage activePage=_viewModel.ProjectSession.Find(id) as MEMPage;
                if(activePage==null)
                {
                    _viewModel.ReportStudioShellWarning("页面已失效，拒绝绘图命令。");
                    return true;
                }
                if(_viewModel.ActivePage==null||_viewModel.ActivePage.Id!=activePage.Id)
                    _viewModel.ActivePage=activePage;
            }
            context.InsertVisual(kind);
            return true;
        }
        // 返回true表示已由资源Tab负责命令（包括非法选择/Flow的拒绝）。
        // 普通固定doc.visual继续走原StudioViewModel命令，不创建第二套Command表。
        private bool RouteStudioResourceVisualAlignment(string mode)
        {
            Guid id;string kind;
            if(!TryParseResourceDocumentId(_activeStudioDocumentId,out id,out kind))return false;
            ResourceDocumentContext context=ActiveStudioResourceContext();
            if(context==null||!ResourceDocumentExists(_activeStudioDocumentId)||
                !ReferenceEquals(context.Session,_viewModel.ProjectSession)||context.IsFlow)
            {
                _viewModel.ReportStudioShellWarning("当前不是可编辑图元资源，已拒绝对齐/分布。");
                return true;
            }
            context.AlignVisuals(mode);
            return true;
        }
        private bool RouteStudioResourceVisualZOrder(bool toFront)
        {
            Guid id;string kind;
            if(!TryParseResourceDocumentId(_activeStudioDocumentId,out id,out kind))return false;
            ResourceDocumentContext context=ActiveStudioResourceContext();
            if(context==null||!ResourceDocumentExists(_activeStudioDocumentId)||
                !ReferenceEquals(context.Session,_viewModel.ProjectSession)||context.IsFlow)
            {
                _viewModel.ReportStudioShellWarning("当前不是可编辑图元资源，已拒绝图层命令。");
                return true;
            }
            context.ChangeVisualZOrder(toFront);
            return true;
        }
        // 右侧属性、画布路径工具和快捷键只路由当前资源Tab，
        // 固定doc.visual继续沿原有ViewModel路径；非法Flow/过期资源拒绝。
        private bool RouteStudioResourceVisualEdit(
            Func<ResourceDocumentContext,bool> execute,string description)
        {
            Guid id;string kind;
            if(!TryParseResourceDocumentId(_activeStudioDocumentId,out id,out kind))return false;
            ResourceDocumentContext context=ActiveStudioResourceContext();
            if(context==null||context.IsFlow||
                !ResourceDocumentExists(_activeStudioDocumentId)||
                !ReferenceEquals(context.Session,_viewModel.ProjectSession))
            {
                _viewModel.ReportStudioShellWarning(description+"：活动资源无效或不是图元编辑器");
                return true;
            }
            execute(context);
            return true;
        }
        private bool RouteStudioResourceGeometry(double x,double y,double width,
            double height,double rotation)
        {return RouteStudioResourceVisualEdit(c=>c.EditGeometry(x,y,width,height,rotation),"几何修改");}
        private bool RouteStudioResourcePath(int index,string operation)
        {return RouteStudioResourceVisualEdit(c=>c.EditPath(index,operation),"路径修改");}
        private bool RouteStudioResourceLayer(string layer,bool toFront)
        {return RouteStudioResourceVisualEdit(c=>c.ChangeLayer(layer,toFront),"图层属性");}
        // 可在WPF STA中调用的实际键盘命令路径，只有活动文档能消费。
        internal bool HandleStudioResourceShortcut(Key key,ModifierKeys modifiers)
        {
            Guid id;string kind;
            if(!TryParseResourceDocumentId(_activeStudioDocumentId,out id,out kind))
                return false;
            ResourceDocumentContext context=ActiveStudioResourceContext();
            if(context==null||!ResourceDocumentExists(_activeStudioDocumentId)||
                !ReferenceEquals(context.Session,_viewModel.ProjectSession))return true;
            bool control=(modifiers&ModifierKeys.Control)!=0;
            if(control&&key==Key.Z){context.ApplyHistory(false);return true;}
            if(control&&key==Key.Y){context.ApplyHistory(true);return true;}
            if(context.IsFlow)return false; // 保留现有MOL流程键鼠Owner，不交旧固定流程编辑器
            if(modifiers==ModifierKeys.None&&key==Key.Delete)
            {context.DeleteVisualSelection();return true;}
            if(modifiers==ModifierKeys.None&&key==Key.Escape)
            {context.ClearSelection();return true;}
            return false;
        }
        public string OpenStudioPageDocument(Guid pageId)
        {return OpenStudioResourceDocument(pageId,false);}
        public string OpenStudioResourceDocument(Guid resourceId,bool flow=false)
        {
            MEMNode resource=_viewModel.ProjectSession.Find(resourceId);
            if(resource==null)
                throw new InvalidOperationException("工程资源已不存在或来自其他工程");
            string key=ResourceDocumentId(resource,flow);
            if(!ResourceDocumentExists(key))
                throw new InvalidOperationException("该资源类型不支持此编辑器或不是独立资源");
            LayoutDocument document=FindStudioContent(key) as LayoutDocument;
            if(document==null)
            {
                ResourceDocumentContext context=CreateStudioPageContext(key);
                LayoutDocumentPane pane=StudioDock.Layout.Descendents().OfType<LayoutDocumentPane>()
                    .FirstOrDefault();
                if(pane==null)throw new InvalidOperationException("中央工作区已丢失");
                document=new LayoutDocument{ContentId=key,Title=resource.Name+
                    (flow?" [流程]":" [设计]"),Content=context.Panel,
                    CanClose=true,CanFloat=false};
                RegisterStudioPageClosing(document);pane.Children.Add(document);
            }
            document.IsActive=true;
            SelectStudioContext(key);
            // 新打开的同资源另一种编辑器，必须立即继承已有MEM Dirty/影响状态。
            RefreshStudioResourceCaptions();
            RefreshStudioMenus();
            return key;
        }
        private void OnStudioPageSourceChanged(object sender,EventArgs args)
        {
            if(_viewModel==null||_closingPageDocuments)return;
            if(!ReferenceEquals(_pageDocumentSession,_viewModel.ProjectSession))
            {
                // Docking关闭资源Tab过程中会改变ActiveContent；必须在关闭前保留活动身份。
                string previouslyActive=_activeStudioDocumentId;
                _closingPageDocuments=true;
                try
                {
                    foreach(string key in _pageDocuments.Keys.ToArray())
                    {
                        LayoutDocument document=FindStudioContent(key) as LayoutDocument;
                        if(document!=null){document.Content=null;document.Close();}
                        ResourceDocumentContext ctx;
                        if(_pageDocuments.TryGetValue(key,out ctx))
                        {ctx.Dispose();_pageDocuments.Remove(key);}
                        _studioViews.Remove(key);
                    }
                    AttachStudioResourceChangeWatch(_viewModel.ProjectSession);
                }
                finally{_closingPageDocuments=false;}
                Guid id;string kind;
                if(TryParseResourceDocumentId(previouslyActive,out id,out kind))
                {
                    LayoutDocument fallback=FindStudioContent("doc.visual") as LayoutDocument;
                    if(fallback!=null)fallback.IsActive=true;
                    SelectStudioContext("doc.visual");RefreshStudioMenus();
                }
            }
            foreach(string key in _pageDocuments.Keys.ToArray())
            {
                ResourceDocumentContext ctx=_pageDocuments[key];
                MEMNode resource=_viewModel.ProjectSession.Find(ctx.ResourceId);
                LayoutDocument document=FindStudioContent(key) as LayoutDocument;
                if(resource==null||!ResourceDocumentExists(key))
                    if(document!=null){document.Content=null;document.Close();}
            }
            RefreshStudioResourceCaptions();
        }
    }
}
