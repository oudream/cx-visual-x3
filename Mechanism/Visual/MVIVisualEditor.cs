using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using CxVisual.Mechanism.Core;
using CxVisual.Foundation.Geometry;
using CxVisual.Mechanism.Data.RuntimeData;
namespace CxVisual.Mechanism.Visual
{
    /// <summary>Presentation commands share the document session history; there is no private replay stack.</summary>
    public sealed class MVIVisualEditor : IDisposable
    {
        private readonly MEMEngineeringSession _session;
        private readonly IDisposable _watch;
        // 导航仅记录稳定资源与选择身份，不复制工程对象及历史。
        private sealed class NavigationFrame
        {
            internal readonly Guid? ResourceId;
            internal readonly Guid? SelectedId;
            internal NavigationFrame(Guid? resourceId,Guid? selectedId)
            {ResourceId=resourceId;SelectedId=selectedId;}
        }
        private readonly Stack<NavigationFrame> _contexts=new Stack<NavigationFrame>();
        public bool CanReturnToPrevious { get { Demand();return _contexts.Count>0; } }
        private Guid? _resource;
        private bool _disposed;
        private ReadOnlyCollection<MVISceneItem> _scene;
        private int _sceneRevision=-1;
        private IMDSExternalBindingResolver _runtimeSource;
        private double _runtimeElapsedMs;
        private bool _runtimeEnabled;
        public void SetRuntimeFrame(IMDSExternalBindingResolver source,double elapsedMilliseconds)
        {
            Demand();
            if(double.IsNaN(elapsedMilliseconds)||double.IsInfinity(elapsedMilliseconds)||
                elapsedMilliseconds<0)throw new ArgumentOutOfRangeException("elapsedMilliseconds");
            _runtimeSource=source;
            _runtimeElapsedMs=elapsedMilliseconds;
            _runtimeEnabled=true;
            _scene=null; // 仅重投影运行外观；调用方自主安排UI帧，不触发工程Changed和Revision
        }
        public void ClearRuntimeFrame()
        {
            Demand();
            _runtimeEnabled=false;
            _runtimeSource=null;
            _runtimeElapsedMs=0;
            _scene=null;
        }

        private Guid? _sceneRoot;
        public MVIVisualEditor(MEMEngineeringSession session)
        {
            _session=session??throw new ArgumentNullException("session");CurrentPageId=session.Project.Pages.Count==0?(Guid?)null:session.Project.Pages[0].Id;
            _watch=session.Watch(change=>RaiseChanged());
        }
        public event EventHandler Changed;
        public event EventHandler<MVIInteractionEventArgs> Interaction;
        public Guid? CurrentPageId {get;private set;}
        public Guid? CurrentResourceId {get{return _resource??CurrentPageId;}}
        public Guid? SelectedId {get;private set;}
        private void Demand(){if(_disposed)throw new ObjectDisposedException("MVIVisualEditor");}
        private void RaiseChanged(){if(_disposed)return;var handler=Changed;if(handler!=null)handler(this,EventArgs.Empty);}
        public void SetPage(Guid id){Demand();if(!(Document.Find(id) is MEMPage))throw new InvalidOperationException("Missing page");CurrentPageId=id;_resource=null;_contexts.Clear();SelectedId=null;RaiseChanged();}
        public void OpenDefinition(Guid id)
        {
            Demand();
            MRIResourceReference reference=Document.FindReference(id);
            if(reference!=null)id=reference.TargetId;
            if(Document.Project.FindResource(id,true)==null||Document.Find(id)==null)
                throw new InvalidOperationException("Missing engineering definition");
            _contexts.Push(new NavigationFrame(_resource,SelectedId));
            _resource=id;
            SelectedId=null;
            RaiseChanged();
        }
        public bool ReturnToPrevious()
        {
            Demand();
            if(_contexts.Count==0)return false;
            NavigationFrame frame=_contexts.Peek();
            // 上一个资源已被删除时不改变当前文档及导航栈。
            Guid previous=frame.ResourceId??CurrentPageId??Guid.Empty;
            if(previous==Guid.Empty||Document.Find(previous)==null)
                throw new InvalidOperationException("Previous engineering resource no longer exists");
            _contexts.Pop();
            _resource=frame.ResourceId;
            // 被删除的实例不得恢复为悬空选中对象。
            Guid? selected=frame.SelectedId;
            SelectedId=selected.HasValue&&
                (Document.Find(selected.Value)!=null||
                 Document.FindReference(selected.Value)!=null)?selected:null;
            RaiseChanged();
            return true;
        }
        public MEMEngineeringSession Document { get { return _session; } }
        public bool CanUndo { get { return Document.CanUndo; } }
        public bool CanRedo { get { return Document.CanRedo; } }
        public ReadOnlyCollection<MVISceneItem> Scene
        {get{Demand();if(_scene==null||_sceneRevision!=Document.Revision||_sceneRoot!=CurrentResourceId){_sceneRoot=CurrentResourceId;int revision=Document.Revision;_scene=CurrentResourceId.HasValue?
                (_runtimeEnabled?
                    MVISceneProjection.ProjectResource(Document,CurrentResourceId.Value,
                        _runtimeSource,_runtimeElapsedMs):
                    MVISceneProjection.ProjectResource(Document,CurrentResourceId.Value)):
                new List<MVISceneItem>().AsReadOnly();_sceneRevision=revision;}return _scene;}}
        public bool Select(Guid? id){Demand();if(id.HasValue&&!Route(MVIInteractionKind.Select,id.Value))return false;SelectedId=id;RaiseChanged();return true;}
        private bool Route(MVIInteractionKind kind,Guid target)
        {
            Demand();var node=Document.Find(target);var reference=Document.FindReference(target);if(node==null&&reference==null)throw new InvalidOperationException("Missing interaction target");
            var route=(node??reference.Owner).Ancestors().Reverse().Select(n=>n.Id).ToList();route.Add((node??reference.Owner).Id);if(reference!=null)route.Add(target);
            var args=new MVIInteractionEventArgs(kind,target,route.AsReadOnly());
            for(int i=0;i<route.Count-1&&!args.PropagationStopped;i++)Dispatch(args,MVIEventPhase.Capture,route[i]);
            if(!args.PropagationStopped)Dispatch(args,MVIEventPhase.Target,target);
            for(int i=route.Count-2;i>=0&&!args.PropagationStopped;i--)Dispatch(args,MVIEventPhase.Bubble,route[i]);return !args.DefaultPrevented;
        }
        private void Dispatch(MVIInteractionEventArgs args,MVIEventPhase phase,Guid current){args.Phase=phase;args.CurrentId=current;var handler=Interaction;if(handler!=null)handler(this,args);}
        public MTCEditResult<bool> AddRectangle(string name, double x, double y, double width, double height)
        {
            return Document.Edit("添加矩形", e =>
            {
                if(!CurrentResourceId.HasValue)throw new InvalidOperationException("NO_PAGE");var root=Document.Find(CurrentResourceId.Value);
                MEMNode parent=root is MEMPage?((MEMPage)root).RootContent:root is MEMSymbol?(MEMNode)((MEMSymbol)root).RootGroup:root;
                foreach (var node in parent.DescendantsAndSelf())
                {
                    if (node is MRCGroup) { parent = node; break; }
                }
                e.AddRectangle(parent, name, x, y, width, height);
                return true;
            });
        }
        /// <summary>视觉连接复用Core的Visual Port与MPCConnection，不创建第二份Graph。</summary>
        public MTCEditResult<bool> ConnectVisual(Guid sourceId,string sourceSide,
            Guid targetId,string targetSide)
        {
            Demand();
            int revision=Document.Revision;
            if(sourceId==targetId||sourceId==Guid.Empty||targetId==Guid.Empty)
                return MTCEditResult<bool>.Rejected(revision,"visual.connection.invalid_pair",
                    "Distinct visual elements are required");
            string sourceMember=MVIVisualAnchors.MemberName(sourceSide);
            string targetMember=MVIVisualAnchors.MemberName(targetSide);
            MRCElement source=Document.Find(sourceId) as MRCElement;
            MRCElement target=Document.Find(targetId) as MRCElement;
            if(source==null||target==null)
                return MTCEditResult<bool>.Rejected(revision,"visual.connection.definition_required",
                    "Open the visual definition; instance connectors require explicit public ports");
            MVISceneItem sourceScene=Scene.FirstOrDefault(i=>i.Id==sourceId);
            MVISceneItem targetScene=Scene.FirstOrDefault(i=>i.Id==targetId);
            if(sourceScene==null||targetScene==null||!sourceScene.IsSelectable||
                !targetScene.IsSelectable||sourceScene.Address.InstancePath.Count!=0||
                targetScene.Address.InstancePath.Count!=0)
                return MTCEditResult<bool>.Rejected(revision,"visual.connection.not_editable",
                    "Both endpoints must be editable definitions in the current scene");
            MEMNode resource=CurrentResourceId.HasValue?Document.Find(CurrentResourceId.Value):null;
            MEMNode owner=resource is MEMPage?((MEMPage)resource).RootContent:resource;
            if(!(owner is IMPMDefinition))
                return MTCEditResult<bool>.Rejected(revision,"visual.connection.invalid_scope",
                    "Open a Page, Content or Symbol definition before connecting");
            if(!Route(MVIInteractionKind.Move,sourceId)||
                !Route(MVIInteractionKind.Move,targetId))
                return MTCEditResult<bool>.Rejected(revision,"visual.default_prevented",
                    "Visual connection prevented by engineering policy");
            MRIObjectAddress sourceAddress=Document.GetAddress(source);
            MRIObjectAddress targetAddress=Document.GetAddress(target);
            return Document.Edit("连接可视图元",revision,e=>
            {
                MPCPort first=source.Ports.FirstOrDefault(p=>p.MemberId==sourceMember);
                MPCPort last=target.Ports.FirstOrDefault(p=>p.MemberId==targetMember);
                if(first==null)first=e.DefinePort(source,sourceMember,
                    MPCPortDirection.InOut,"core.visual.anchor",MPCPortKind.Visual);
                if(last==null)last=e.DefinePort(target,targetMember,
                    MPCPortDirection.InOut,"core.visual.anchor",MPCPortKind.Visual);
                if(first.Kind!=MPCPortKind.Visual||last.Kind!=MPCPortKind.Visual||
                    first.Direction==MPCPortDirection.Input||
                    last.Direction==MPCPortDirection.Output)
                    throw new InvalidOperationException("visual.connection.port_conflict");
                e.Connect(owner,new MPCEndpoint(sourceAddress,first.Id),
                    new MPCEndpoint(targetAddress,last.Id));
                return true;
            });
        }

        /// <summary>图元新增只通过原MEM工程事务提交，一次Undo即可撤销。</summary>
        public MTCEditResult<bool> AddShape(string name,MRCShapeDefinition shape,double x,double y)
        {
            if(shape==null)throw new ArgumentNullException("shape");
            return Document.Edit("添加Shape",e=>
            {
                e.AddShape(CurrentDrawParent(),name,shape,x,y);
                return true;
            });
        }
        public MTCEditResult<bool> AddAssetElement(Guid assetId,string name,bool svg,
            double x,double y,double width=160,double height=110)
        {
            Demand();
            MEMAssetReference asset=Document.Assets.FirstOrDefault(a=>a.Id==assetId);
            if(asset==null)
                return MTCEditResult<bool>.Rejected(Document.Revision,
                    "visual.asset.missing","Import the asset into the current project first");
            bool mediaMatch=svg?
                string.Equals(asset.MediaType,"image/svg+xml",StringComparison.OrdinalIgnoreCase):
                string.Equals(asset.MediaType,"image/png",StringComparison.OrdinalIgnoreCase)||
                string.Equals(asset.MediaType,"image/jpeg",StringComparison.OrdinalIgnoreCase)||
                string.Equals(asset.MediaType,"image/bmp",StringComparison.OrdinalIgnoreCase);
            if(!mediaMatch)
                return MTCEditResult<bool>.Rejected(Document.Revision,
                    "visual.asset.media_mismatch","Asset media type is incompatible with this element");
            return Document.Edit(svg?"添加SVG图元":"添加图片图元",e=>
            {
                MRCVisualItem element=svg?(MRCVisualItem)e.AddSvg(
                    CurrentDrawParent(),name,x,y,width,height):
                    e.AddImage(CurrentDrawParent(),name,x,y,width,height);
                e.SetBinding(element,"source",MPMBinding.Resource(assetId));
                return true;
            });
        }
        public MTCEditResult<bool> AddText(string name,string text,double x,double y)
        {
            return Document.Edit("添加文字",e=>
            {
                e.AddText(CurrentDrawParent(),name,text,x,y);
                return true;
            });
        }
        /// <summary>按钮Widget保存的是TypeId/Schema，真实WPF Button只在呈现层创建。</summary>
        public MTCEditResult<bool> AddWidget(string name,string typeId,double x,double y,
            double width=120,double height=36)
        {
            MPMTypeDescriptor registered=Document.FindType(typeId);
            if(!MRCWidgetTypes.IsBuiltIn(typeId)&&
                (registered==null||registered.Kind!=MEMNodeKind.Element))
                return MTCEditResult<bool>.Rejected(Document.Revision,"visual.widget.type_unregistered",
                    "Built-in Widget type or registered custom Element type required");
            return Document.Edit("添加UI控件",e=>
            {
                e.Add(CurrentDrawParent(),new MRCWidgetElement(
                    Guid.NewGuid(),name,typeId,x,y,width,height));
                return true;
            });
        }
        private MEMNode CurrentDrawParent()
        {
            if(!CurrentResourceId.HasValue)throw new InvalidOperationException("NO_PAGE");
            MEMNode root=Document.Find(CurrentResourceId.Value);
            MEMNode parent=root is MEMPage?((MEMPage)root).RootContent:
                root is MEMSymbol?(MEMNode)((MEMSymbol)root).RootGroup:root;
            foreach(MEMNode node in parent.DescendantsAndSelf())
                if(node is MRCGroup)return node;
            return parent;
        }
        /// <summary>开发者与WPF使用相同编辑入口，修改一次即产生一条工程Undo记录。</summary>
        public MTCEditResult<bool> EditHandle(MVIVisualHandle handle,double worldX,double worldY)
        {
            Demand();
            if(handle==null)throw new ArgumentNullException("handle");
            if(Document.Find(handle.ElementId)==null&&Document.FindReference(handle.ElementId)==null)
                return MTCEditResult<bool>.Rejected(Document.Revision,"visual.target_not_found",
                    "Handle target no longer exists");
            if(!Route(MVIInteractionKind.Move,handle.ElementId))
                return MTCEditResult<bool>.Rejected(Document.Revision,"visual.default_prevented",
                    "Handle edit prevented by capture/target/bubble interaction");
            return MVIVisualEditing.Edit(this,handle,worldX,worldY);
        }
        /// <summary>路径插入/删除/类型转换：与鼠标控制柄共用几何补偿与MEM事务。</summary>
        public MTCEditResult<bool> EditPathSegment(Guid id,int commandIndex,string operation,
            double fraction=0.5)
        {
            Demand();
            if(!Route(MVIInteractionKind.Move,id))
                return MTCEditResult<bool>.Rejected(Document.Revision,"visual.default_prevented",
                    "Path editing blocked by interaction policy");
            var shape=Document.Find(id) as MRCShapeElement;
            if(shape==null)return MTCEditResult<bool>.Rejected(Document.Revision,
                "visual.path_required","Open an editable Shape definition");
            FGEPath2D before=shape.Shape.Geometry;
            FGEPath2D after;
            switch(operation)
            {
                case "split":after=FGEPathEditing.SplitSegment(before,commandIndex,fraction);break;
                case "delete":after=FGEPathEditing.DeleteAnchor(before,commandIndex);break;
                case "line":after=FGEPathEditing.ConvertSegment(before,commandIndex,FGEPathVerb.Line);break;
                case "quadratic":after=FGEPathEditing.ConvertSegment(before,commandIndex,FGEPathVerb.Quadratic);break;
                case "cubic":after=FGEPathEditing.ConvertSegment(before,commandIndex,FGEPathVerb.Cubic);break;
                case "close":
                case "open":
                    if(shape.Shape.Kind!=MRCShapeKind.Path)
                        return MTCEditResult<bool>.Rejected(Document.Revision,
                            "visual.path.kind_required","Contour editing requires a Path definition");
                    after=FGEPathEditing.SetContourClosed(before,commandIndex,operation=="close");
                    break;
                default:throw new ArgumentException("visual.path.operation");
            }
            if(object.ReferenceEquals(before,after))
                return MTCEditResult<bool>.Rejected(Document.Revision,
                    "visual.path.unchanged","Contour already has the requested closure");
            return MVIVisualEditing.CommitGeometry(this,shape,after);
        }
        /// <summary>统一属性面板几何编辑，一次原工程事务，禁止原型式直接写字段。</summary>
        /// <summary>世界坐标对齐与水平分布；嵌套Group由场景变换回推各自父坐标。</summary>
        public MTCEditResult<bool> Align(IReadOnlyList<Guid> ids,string mode)
        {
            Demand();
            int revision=Document.Revision;
            if(ids==null||ids.Count<2)
                return MTCEditResult<bool>.Rejected(revision,"visual.align.selection",
                    "至少选择两个图元");
            if(mode!="left"&&mode!="top"&&mode!="centerX"&&
                mode!="centerY"&&mode!="distributeX")
                return MTCEditResult<bool>.Rejected(revision,"visual.align.mode",
                    "Unsupported alignment");
            var selection=Scene.Where(x=>ids.Contains(x.Id)&&x.IsSelectable)
                .GroupBy(x=>x.Id).Select(g=>g.First()).ToArray();
            if(selection.Length!=ids.Distinct().Count()||
                selection.Any(x=>Document.Find(x.Id) as MRCVisualItem==null))
                return MTCEditResult<bool>.Rejected(revision,"visual.align.definition",
                    "Select editable definitions, not reference instances");
            foreach(MVISceneItem item in selection)
                if(!Route(MVIInteractionKind.Move,item.Id))
                    return MTCEditResult<bool>.Rejected(revision,"visual.default_prevented",
                        "Alignment blocked by interaction policy");
            double targetLeft=selection.Min(x=>x.Bounds.Left);
            double targetTop=selection.Min(x=>x.Bounds.Top);
            double centerX=selection.Average(x=>(x.Bounds.Left+x.Bounds.Right)/2);
            double centerY=selection.Average(x=>(x.Bounds.Top+x.Bounds.Bottom)/2);
            var ordered=selection.OrderBy(x=>x.Bounds.Left).ToArray();
            double step=ordered.Length<2?0:
                (ordered[ordered.Length-1].Bounds.Left-ordered[0].Bounds.Left)/(ordered.Length-1);
            return Document.Edit("图元对齐/分布",revision,e=>
            {
                for(int i=0;i<selection.Length;i++)
                {
                    MVISceneItem item=selection[i];
                    double dx=0,dy=0;
                    if(mode=="left")dx=targetLeft-item.Bounds.Left;
                    if(mode=="top")dy=targetTop-item.Bounds.Top;
                    if(mode=="centerX")
                        dx=centerX-(item.Bounds.Left+item.Bounds.Right)/2;
                    if(mode=="centerY")
                        dy=centerY-(item.Bounds.Top+item.Bounds.Bottom)/2;
                    if(mode=="distributeX")
                    {
                        int order=Array.IndexOf(ordered,item);
                        dx=ordered[0].Bounds.Left+step*order-item.Bounds.Left;
                    }
                    if(Math.Abs(dx)<1e-9&&Math.Abs(dy)<1e-9)continue;
                    double x,y;
                    item.PositionAfterWorldDelta(dx,dy,out x,out y);
                    e.Move((MRCVisualItem)Document.Find(item.Id),x,y);
                }
                return true;
            });
        }
        public MTCEditResult<bool> SetGeometry(Guid id,double x,double y,double width,
            double height,double angle)
        {
            Demand();
            if(!Route(MVIInteractionKind.Move,id))
                return MTCEditResult<bool>.Rejected(Document.Revision,"visual.default_prevented",
                    "Editing prevented by policy");
            MRCVisualItem item=Document.Find(id) as MRCVisualItem;
            if(item==null)return MTCEditResult<bool>.Rejected(Document.Revision,
                "visual.definition_required","Open a visual definition");
            int revision=Document.Revision;
            return Document.Edit("编辑可视几何属性",revision,e=>
            {
                e.Move(item,x,y);
                e.Resize(item,width,height);
                e.Rotate(item,angle);
                return true;
            });
        }
        public MTCEditResult<bool> SetLayer(Guid id,string layer,bool toFront)
        {
            Demand();
            if(!Route(MVIInteractionKind.Move,id))
                return MTCEditResult<bool>.Rejected(Document.Revision,"visual.default_prevented",
                    "Layer edit prevented");
            MRCVisualItem item=Document.Find(id) as MRCVisualItem;
            if(item==null)return MTCEditResult<bool>.Rejected(Document.Revision,
                "visual.definition_required","Open a visual definition");
            int revision=Document.Revision;
            return Document.Edit("设置图层及绘制顺序",revision,e=>
            {
                e.SetLayer(item,layer??"");
                if(toFront)e.Reparent(item,item.Parent,item.Parent.Children.Count-1);
                return true;
            });
        }
        /// <summary>多选世界位移：复杂父坐标各自换算，一次MEM事务/Undo；实例只改Placement。</summary>
        /// <summary>同一父容器内调整Z序，保留MEM身份，所有改动在原事务与Undo栈内。</summary>
        public MTCEditResult<bool> SetZOrder(Guid id,bool bringToFront)
        {
            Demand();
            int revision=Document.Revision;
            MRCVisualItem item=Document.Find(id) as MRCVisualItem;
            if(item==null||item.Parent==null)
                return MTCEditResult<bool>.Rejected(revision,"visual.layer.definition_required",
                    "Open a visual definition");
            if(!Route(MVIInteractionKind.Move,id))
                return MTCEditResult<bool>.Rejected(revision,"visual.default_prevented",
                    "Z-order edit prevented");
            int index=bringToFront?item.Parent.Children.Count-1:0;
            return Document.Edit("调整图元绘制顺序",revision,e=>
            {e.Reparent(item,item.Parent,index);return true;});
        }
        public MTCEditResult<bool> MoveSelection(IReadOnlyList<Guid> ids,
            double worldDx,double worldDy,bool snapToGrid=false)
        {
            Demand();
            FGEValidation.Finite(worldDx,"worldDx");
            FGEValidation.Finite(worldDy,"worldDy");
            int revision=Document.Revision;
            if(ids==null||ids.Count<2||ids.Count!=ids.Distinct().Count())
                return MTCEditResult<bool>.Rejected(revision,"visual.move.selection",
                    "Multiple distinct visual IDs required");
            var selected=Scene.Where(x=>ids.Contains(x.Id)&&x.IsSelectable).ToArray();
            if(selected.Length!=ids.Count)
                return MTCEditResult<bool>.Rejected(revision,"visual.move.inaccessible",
                    "Selection contains hidden, locked or missing items");
            foreach(MVISceneItem item in selected)
            {
                if(Document.Find(item.Id) as MRCVisualItem==null&&
                    Document.FindReference(item.Id)==null)
                    return MTCEditResult<bool>.Rejected(revision,"visual.move.definition",
                        "Unknown selected visual definition/reference");
                if(!Route(MVIInteractionKind.Move,item.Id))
                    return MTCEditResult<bool>.Rejected(revision,"visual.default_prevented",
                        "Selected visual disallowed the move");
            }
            return Document.Edit("批量移动可视对象",revision,e=>
            {
                foreach(MVISceneItem scene in selected)
                {
                    double x,y;
                    scene.PositionAfterWorldDelta(worldDx,worldDy,out x,out y);
                    if(snapToGrid)
                    {
                        x=Math.Round(x/25)*25;
                        y=Math.Round(y/25)*25;
                    }
                    MRIResourceReference reference=Document.FindReference(scene.Id);
                    if(reference!=null)
                    {
                        MRCTransform p=reference.Placement;
                        e.SetPlacement(reference,new MRCTransform(
                            p.M11,p.M12,p.M21,p.M22,x,y));
                    }
                    else e.Move((MRCVisualItem)Document.Find(scene.Id),x,y);
                }
                return true;
            });
        }
        public MTCEditResult<bool> Move(Guid id, double x, double y)
        {
            int revision=Document.Revision;
            if(Document.Find(id)==null&&Document.FindReference(id)==null)
                return MTCEditResult<bool>.Rejected(revision,"visual.target_not_found","Move target does not exist");
            if(!Route(MVIInteractionKind.Move,id))return MTCEditResult<bool>.Rejected(Document.Revision,"visual.default_prevented","Move canceled before commit");
            return Document.Edit("移动可视对象",revision,e=>{var reference=Document.FindReference(id);if(reference==null)e.Move(RequireVisual(id),x,y);else{var p=reference.Placement;e.SetPlacement(reference,new MRCTransform(p.M11,p.M12,p.M21,p.M22,x,y));}return true;});
        }
        public MTCEditResult<bool> Remove(Guid id)
        {int revision=Document.Revision;if(!Route(MVIInteractionKind.Delete,id))return MTCEditResult<bool>.Rejected(Document.Revision,"visual.default_prevented","Delete canceled before commit");return Document.Edit("删除可视对象",revision,e=>{var reference=Document.FindReference(id);if(reference==null)e.Remove(RequireVisual(id));else e.Remove(reference);return true;});}
        private MRCVisualItem RequireVisual(Guid id) { var item = Document.Find(id) as MRCVisualItem; if (item == null) throw new InvalidOperationException("Missing visual item"); return item; }
        public MTCEditResult<bool> Undo() { return Document.Undo(); }
        public MTCEditResult<bool> Redo() { return Document.Redo(); }
        public void Dispose(){if(_disposed)return;_disposed=true;_watch.Dispose();Changed=null;Interaction=null;}
    }
}
