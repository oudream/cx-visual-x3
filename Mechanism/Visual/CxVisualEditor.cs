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
    public sealed class CxVisualEditor : IDisposable
    {
        private readonly MEMEngineeringSession _session;
        private readonly IDisposable _watch;
        private readonly Stack<Guid?> _contexts=new Stack<Guid?>();
        private Guid? _resource;
        private bool _disposed;
        private ReadOnlyCollection<CxSceneItem> _scene;
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
        public CxVisualEditor(MEMEngineeringSession session)
        {
            _session=session??throw new ArgumentNullException("session");CurrentPageId=session.Project.Pages.Count==0?(Guid?)null:session.Project.Pages[0].Id;
            _watch=session.Watch(change=>RaiseChanged());
        }
        public event EventHandler Changed;
        public event EventHandler<MVIInteractionEventArgs> Interaction;
        public Guid? CurrentPageId {get;private set;}
        public Guid? CurrentResourceId {get{return _resource??CurrentPageId;}}
        public Guid? SelectedId {get;private set;}
        private void Demand(){if(_disposed)throw new ObjectDisposedException("CxVisualEditor");}
        private void RaiseChanged(){if(_disposed)return;var handler=Changed;if(handler!=null)handler(this,EventArgs.Empty);}
        public void SetPage(Guid id){Demand();if(!(Document.Find(id) is MEMPage))throw new InvalidOperationException("Missing page");CurrentPageId=id;_resource=null;_contexts.Clear();SelectedId=null;RaiseChanged();}
        public void OpenDefinition(Guid id)
        {Demand();var reference=Document.FindReference(id);if(reference!=null)id=reference.TargetId;if(Document.Project.FindResource(id,true)==null||Document.Find(id)==null)throw new InvalidOperationException("Missing engineering definition");_contexts.Push(CurrentResourceId);_resource=id;SelectedId=null;RaiseChanged();}
        public bool ReturnToPrevious(){Demand();if(_contexts.Count==0)return false;_resource=_contexts.Pop();SelectedId=null;RaiseChanged();return true;}
        public MEMEngineeringSession Document { get { return _session; } }
        public bool CanUndo { get { return Document.CanUndo; } }
        public bool CanRedo { get { return Document.CanRedo; } }
        public ReadOnlyCollection<CxSceneItem> Scene
        {get{Demand();if(_scene==null||_sceneRevision!=Document.Revision||_sceneRoot!=CurrentResourceId){_sceneRoot=CurrentResourceId;int revision=Document.Revision;_scene=CurrentResourceId.HasValue?
                (_runtimeEnabled?
                    CxSceneProjection.ProjectResource(Document,CurrentResourceId.Value,
                        _runtimeSource,_runtimeElapsedMs):
                    CxSceneProjection.ProjectResource(Document,CurrentResourceId.Value)):
                new List<CxSceneItem>().AsReadOnly();_sceneRevision=revision;}return _scene;}}
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
                default:throw new ArgumentException("visual.path.operation");
            }
            return MVIVisualEditing.CommitGeometry(this,shape,after);
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
