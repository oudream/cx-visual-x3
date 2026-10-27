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
            _watch=session.Watch(change=>
            {
                // Undo分组或删除对象后不得向宿主继续发布已经离开工程的选中身份。
                if(SelectedId.HasValue&&session.Find(SelectedId.Value)==null&&
                    session.FindReference(SelectedId.Value)==null&&
                    session.FindConnection(SelectedId.Value)==null)SelectedId=null;
                RaiseChanged();
            });
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
        public bool Select(Guid? id)
        {
            Demand();
            if(id.HasValue&&!Route(MVIInteractionKind.Select,id.Value))return false;
            SelectedId=id;RaiseChanged();return true;
        }
        private bool Route(MVIInteractionKind kind,Guid target)
        {
            Demand();
            MEMNode node=Document.Find(target);
            MRIResourceReference reference=Document.FindReference(target);
            MPCConnection connector=node==null&&reference==null?
                Document.FindConnection(target):null;
            if(node==null&&reference==null&&connector==null)
                throw new InvalidOperationException("Missing interaction target");
            if(connector!=null&&!Scene.Any(x=>x.Id==connector.Id&&
                x.ElementTypeId=="core.visual.connector"&&x.IsSelectable))
                throw new InvalidOperationException("Visual connection is not in the active scene");
            MEMNode baseNode=connector==null?(node??reference.Owner):
                Document.Find(connector.OwnerId);
            var route=baseNode.Ancestors().Reverse().Select(n=>n.Id).ToList();
            route.Add(baseNode.Id);
            if(reference!=null||connector!=null)route.Add(target);
            var args=new MVIInteractionEventArgs(kind,target,route.AsReadOnly());
            for(int i=0;i<route.Count-1&&!args.PropagationStopped;i++)Dispatch(args,MVIEventPhase.Capture,route[i]);
            if(!args.PropagationStopped)Dispatch(args,MVIEventPhase.Target,target);
            for(int i=route.Count-2;i>=0&&!args.PropagationStopped;i--)Dispatch(args,MVIEventPhase.Bubble,route[i]);return !args.DefaultPrevented;
        }
        private void Dispatch(MVIInteractionEventArgs args,MVIEventPhase phase,Guid current){args.Phase=phase;args.CurrentId=current;var handler=Interaction;if(handler!=null)handler(this,args);}
        /// <summary>
        /// 从真实可视选区复制工程图元。工程身份、端口、内部视觉连线以及
        /// 一次Undo均由MEM Transfer负责；UI只提供世界中的平移意图。
        /// 当前要求同父定义项，避免把Symbol实例和定义偷偷混在一个复制事务。
        /// </summary>
        /// <summary>
        /// 复制同一父容器的真实可编辑图元。只接收父局部坐标偏移；
        /// 端口/内部MPC连接和单次Undo由Core Transfer管理。
        /// 不以Scene序号或屏幕DIP作为工程持久化坐标。
        /// </summary>
        public MTITransferResult DuplicateSelection(IReadOnlyList<Guid> ids,
            double parentOffsetX,double parentOffsetY)
        {
            Demand();
            if(ids==null||ids.Count==0)
                throw new ArgumentException("visual.duplicate.empty_selection");
            if(double.IsNaN(parentOffsetX)||double.IsInfinity(parentOffsetX)||
                double.IsNaN(parentOffsetY)||double.IsInfinity(parentOffsetY))
                throw new ArgumentException("visual.duplicate.invalid_offset");
            var nodes=new List<MRCVisualItem>();
            foreach(Guid id in ids)
            {
                MRCVisualItem node=Document.Find(id) as MRCVisualItem;
                if(node==null)throw new InvalidOperationException("visual.duplicate.invalid_item");
                // Group没有独立Shape SceneItem，但可通过工程树选中；普通图元只能复制当前定义。
                bool group=node is MRCGroup;
                MVISceneItem projected=Scene.FirstOrDefault(item=>item.Id==id&&
                    item.Address.InstancePath.Count==0);
                if(!group&&(projected==null||!projected.IsSelectable))
                    throw new InvalidOperationException("visual.duplicate.not_editable");
                if(!node.IsVisible||node.IsLocked||
                    !Route(MVIInteractionKind.Move,id))
                    throw new InvalidOperationException("visual.duplicate.forbidden");
                nodes.Add(node);
            }
            if(nodes.Select(node=>node.Id).Distinct().Count()!=nodes.Count||
                nodes.Any(node=>node.Parent!=nodes[0].Parent))
                throw new InvalidOperationException("visual.duplicate.requires_siblings");
            MEMNode parent=nodes[0].Parent;
            // 页面、Content、Symbol定义均使用相同工程Owner。不能仅依据OwnerPage
            // 限制复制，否则Symbol定义内的Group/Element永远无法使用通用Duplicate。
            MEMNode scope=CurrentResourceId.HasValue?Document.Find(CurrentResourceId.Value):null;
            if(parent==null||scope==null||nodes.Any(node=>
                !node.Ancestors().Any(ancestor=>ancestor.Id==scope.Id)))
                throw new InvalidOperationException("visual.duplicate.invalid_scope");
            return Document.DuplicateVisual(ids,parent,parentOffsetX,parentOffsetY);
        }
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

        private MPCConnection CurrentVisualConnection(Guid id)
        {
            MPCConnection connection=Document.FindConnection(id);
            if(connection==null||!Scene.Any(x=>x.Id==id&&
                x.ElementTypeId=="core.visual.connector"&&x.IsSelectable))
                return null;
            return connection;
        }

        /// <summary>直线/自动正交布线：无人工点，始终由端点当前几何重算。</summary>
        public MTCEditResult<bool> SetVisualRoute(Guid connectionId,
            MPCVisualRouteKind kind)
        {
            Demand();int revision=Document.Revision;
            MPCConnection connection=CurrentVisualConnection(connectionId);
            if(connection==null)return MTCEditResult<bool>.Rejected(revision,
                "visual.route.not_found","Connection not editable in current resource");
            if(kind!=MPCVisualRouteKind.Straight&&kind!=MPCVisualRouteKind.Orthogonal)
                return MTCEditResult<bool>.Rejected(revision,
                    "visual.route.unsupported_mode","Use waypoint editing for manual routes");
            if(!Route(MVIInteractionKind.Move,connectionId))
                return MTCEditResult<bool>.Rejected(revision,
                    "visual.default_prevented","Visual route blocked");
            return Document.Edit("设置连接线自动布线",revision,e=>
            {
                e.SetVisualRoute(connection,kind,new MPCVisualWaypoint[0]);
                return true;
            });
        }

        private FGEPoint2D[] VisualConnectionAnchors(MPCConnection connection)
        {
            var projected=Scene;
            Func<MPCEndpoint,FGEPoint2D> locate=endpoint=>
            {
                MPCPort port=Document.FindPort(endpoint.PortId);
                MVISceneItem scene=projected.FirstOrDefault(x=>
                    x.Address!=null&&x.Address.ObjectId==endpoint.Address.ObjectId&&
                    x.Address.InstancePath.Count==0);
                if(port==null||scene==null||
                    !MVIVisualAnchors.IsVisualMember(port.MemberId))
                    throw new InvalidOperationException("visual.route.missing_anchor");
                return MVIVisualAnchors.At(scene,port.MemberId.Substring(7));
            };
            return new[]{locate(connection.Source),locate(connection.Target)};
        }

        /// <summary>
        /// 当前场景显示什么路径，编辑就使用什么顶点与段索引。
        /// 自动避障分支、端口出入线和人工折点均从同一GeometryToWorld还原世界坐标；
        /// 禁止再次构造旧的中线简化路径，避免用户点击某段却编辑另一段。
        /// </summary>
        private IReadOnlyList<FGEPoint2D> VisibleConnectionPoints(Guid connectionId)
        {
            MVISceneItem line=Scene.FirstOrDefault(x=>x.Id==connectionId&&
                x.ElementTypeId=="core.visual.connector");
            if(line==null||line.Shape==null)
                throw new InvalidOperationException("visual.route.scene_not_found");
            return line.Shape.Geometry.Commands.Select(c=>
                line.GeometryToWorld.TransformPoint(c.End)).ToArray();
        }

        /// <summary>双击某线段插点；自动折线转换为等效手动点，不丢失已有路径。</summary>
        public MTCEditResult<bool> InsertVisualWaypoint(Guid connectionId,
            int segmentIndex,double x,double y)
        {
            Demand();int revision=Document.Revision;
            MPCConnection connection=CurrentVisualConnection(connectionId);
            if(connection==null)return MTCEditResult<bool>.Rejected(revision,
                "visual.route.not_found","Connection not in active scene");
            FGEValidation.Finite(x,"x");FGEValidation.Finite(y,"y");
            FGEPoint2D[] ends=VisualConnectionAnchors(connection);
            IReadOnlyList<FGEPoint2D> points=VisibleConnectionPoints(connectionId);
            int count=points.Count-2;
            if(segmentIndex<0||segmentIndex>count||count>=64)
                return MTCEditResult<bool>.Rejected(revision,
                    "visual.route.invalid_segment","Invalid segment or too many waypoints");
            if(!Route(MVIInteractionKind.Move,connectionId))
                return MTCEditResult<bool>.Rejected(revision,
                    "visual.default_prevented","Waypoint editing blocked");
            var manual=new List<MPCVisualWaypoint>();
            for(int i=0;i<count;i++)
            {
                if(connection.VisualRouteKind==MPCVisualRouteKind.Manual)
                    manual.Add(connection.VisualWaypoints[i]);
                else
                    manual.Add(MVIVisualRouting.WaypointAt(points[i+1],
                        ends[0],ends[1],(i+1.0)/(count+1)));
            }
            double prior=segmentIndex==0?0:manual[segmentIndex-1].Fraction;
            double next=segmentIndex==count?1:manual[segmentIndex].Fraction;
            manual.Insert(segmentIndex,MVIVisualRouting.WaypointAt(
                new FGEPoint2D(x,y),ends[0],ends[1],(prior+next)/2));
            return Document.Edit("插入连接线折点",revision,e=>
            {
                e.SetVisualRoute(connection,MPCVisualRouteKind.Manual,manual);
                return true;
            });
        }

        public MTCEditResult<bool> MoveVisualWaypoint(Guid connectionId,int index,
            double x,double y)
        {
            Demand();int revision=Document.Revision;
            MPCConnection connection=CurrentVisualConnection(connectionId);
            if(connection==null||index<0)
                return MTCEditResult<bool>.Rejected(revision,
                    "visual.route.invalid_waypoint","No editable manual waypoint at index");
            FGEValidation.Finite(x,"x");FGEValidation.Finite(y,"y");
            if(!Route(MVIInteractionKind.Move,connectionId))
                return MTCEditResult<bool>.Rejected(revision,
                    "visual.default_prevented","Waypoint editing blocked");
            FGEPoint2D[] ends=VisualConnectionAnchors(connection);
            IReadOnlyList<FGEPoint2D> rendered=VisibleConnectionPoints(connectionId);
            int count=rendered.Count-2;
            if(index>=count)
                return MTCEditResult<bool>.Rejected(revision,
                    "visual.route.invalid_waypoint","Waypoint index is out of range");
            var all=new List<MPCVisualWaypoint>();
            for(int i=0;i<count;i++)
            {
                if(connection.VisualRouteKind==MPCVisualRouteKind.Manual)
                    all.Add(connection.VisualWaypoints[i]);
                else all.Add(MVIVisualRouting.WaypointAt(rendered[i+1],
                    ends[0],ends[1],(i+1.0)/(count+1)));
            }
            all[index]=MVIVisualRouting.WaypointAt(new FGEPoint2D(x,y),
                ends[0],ends[1],all[index].Fraction);
            return Document.Edit("调整连接线折点",revision,e=>
            {
                e.SetVisualRoute(connection,MPCVisualRouteKind.Manual,all);
                return true;
            });
        }

        public MTCEditResult<bool> RemoveVisualWaypoint(Guid connectionId,int index)
        {
            Demand();int revision=Document.Revision;
            MPCConnection connection=CurrentVisualConnection(connectionId);
            if(connection==null||connection.VisualRouteKind!=MPCVisualRouteKind.Manual||
                index<0||index>=connection.VisualWaypoints.Count)
                return MTCEditResult<bool>.Rejected(revision,
                    "visual.route.invalid_waypoint","No manual waypoint at index");
            if(!Route(MVIInteractionKind.Delete,connectionId))
                return MTCEditResult<bool>.Rejected(revision,
                    "visual.default_prevented","Waypoint deletion blocked");
            var all=connection.VisualWaypoints.ToList();all.RemoveAt(index);
            return Document.Edit("删除连接线折点",revision,e=>
            {
                e.SetVisualRoute(connection,all.Count==0?
                    MPCVisualRouteKind.Straight:MPCVisualRouteKind.Manual,all);
                return true;
            });
        }

        /// <summary>重连保持MPCConnection.Id不变；仅改指定一端的真实Visual端口。</summary>
        public MTCEditResult<bool> ReconnectVisual(Guid connectionId,bool editSource,
            Guid newElementId,string side)
        {
            Demand();
            int revision=Document.Revision;
            MPCConnection connection=Document.FindConnection(connectionId);
            if(connection==null||!Scene.Any(x=>x.Id==connectionId&&
                x.ElementTypeId=="core.visual.connector"))
                return MTCEditResult<bool>.Rejected(revision,
                    "visual.connection.not_found","The visual connection is not in the active scene");
            MRCElement element=Document.Find(newElementId) as MRCElement;
            MVISceneItem scene=Scene.FirstOrDefault(x=>x.Id==newElementId);
            if(element==null||scene==null||!scene.IsSelectable||
                scene.Address==null||scene.Address.InstancePath.Count!=0)
                return MTCEditResult<bool>.Rejected(revision,
                    "visual.connection.target_not_editable","Target must be an editable visual definition");
            string member=MVIVisualAnchors.MemberName(side);
            MPCEndpoint kept=editSource?connection.Target:connection.Source;
            if(kept.Address.ObjectId==newElementId)
                return MTCEditResult<bool>.Rejected(revision,"visual.connection.self_link",
                    "A visual connection cannot attach to itself");
            if(!Route(MVIInteractionKind.Move,connectionId))
                return MTCEditResult<bool>.Rejected(revision,"visual.default_prevented",
                    "Reconnection rejected by the interaction policy");
            MRIObjectAddress newAddress=Document.GetAddress(element);
            return Document.Edit("调整可视连接端点",revision,e=>
            {
                MPCPort port=element.Ports.FirstOrDefault(x=>x.MemberId==member);
                if(port==null)
                    port=e.DefinePort(element,member,MPCPortDirection.InOut,
                        "core.visual.anchor",MPCPortKind.Visual);
                if(port.Kind!=MPCPortKind.Visual||port.Direction==MPCPortDirection.Input&&editSource||
                    port.Direction==MPCPortDirection.Output&&!editSource)
                    throw new InvalidOperationException("visual.connection.port_conflict");
                MPCEndpoint changed=new MPCEndpoint(newAddress,port.Id);
                e.Reconnect(connection,editSource?changed:kept,editSource?kept:changed);
                return true;
            });
        }

        /// <summary>图元新增只通过原MEM工程事务提交，一次Undo即可撤销。</summary>
        /// <summary>在世界位置放置文字框；尺寸与WPF预览由同一纯布局规则决定。</summary>
        public MTCEditResult<bool> DrawText(string name,string text,
            IReadOnlyList<FGEPoint2D> worldPoints)
        {
            Demand();
            double x,y,width,height;
            MVIShapeCatalog.TextBounds(worldPoints,out x,out y,out width,out height);
            MEMNode parent=CurrentDrawParent();
            FGEPoint2D local=DrawOriginInParent(parent,x,y);
            return Document.Edit("绘制文字框",e=>
            {
                e.AddText(parent,name,text,local.X,local.Y,width,height);
                return true;
            });
        }
        /// <summary>正式修改既有Text，保留对象身份并通过MEM事务实现一次Undo。</summary>
        public MTCEditResult<bool> SetVisualText(Guid id,string text)
        {
            Demand();
            int revision=Document.Revision;
            if(text==null||text.Length>1000000)
                return MTCEditResult<bool>.Rejected(revision,"visual.text.invalid",
                    "Text must be non-null and within the existing MRC text budget");
            MRCTextElement definition=Document.Find(id) as MRCTextElement;
            MVISceneItem visible=Scene.FirstOrDefault(item=>item.Id==id&&
                item.ElementTypeId=="core.text"&&item.IsSelectable&&
                item.Address!=null&&item.Address.InstancePath.Count==0);
            if(definition==null||visible==null)
                return MTCEditResult<bool>.Rejected(revision,"visual.text.not_editable",
                    "Text definition must be visible, editable and outside an instance projection");
            if(string.Equals(definition.Text,text,StringComparison.Ordinal))
                return MTCEditResult<bool>.Rejected(revision,"visual.text.unchanged",
                    "Text is unchanged; no engineering revision is required");
            return Document.Edit("修改图元文字",e=>{
                e.SetText(definition,text);return true;
            });
        }
        /// <summary>Widget手工单击/拖框使用同一WPF Ghost布局；现有MEM Widget唯一Owner。</summary>
        public MTCEditResult<bool> DrawWidget(string name,string typeId,
            IReadOnlyList<FGEPoint2D> worldPoints)
        {
            Demand();
            double x,y,width,height;
            MVIShapeCatalog.WidgetBounds(worldPoints,out x,out y,out width,out height);
            FGEPoint2D local=DrawOriginInParent(CurrentDrawParent(),x,y);
            return AddWidget(name,typeId,local.X,local.Y,width,height);
        }
        /// <summary>唯一绘制创建入口；与WPF Ghost共享纯几何求解，单次MEM事务。</summary>
        public MTCEditResult<bool> DrawShape(string name,MRCShapeKind kind,
            IReadOnlyList<FGEPoint2D> worldPoints,bool constrained)
        {
            Demand();
            double x,y;
            MRCShapeDefinition shape=MVIShapeCatalog.CreateDrawn(
                kind,worldPoints,constrained,out x,out y);
            // 普通Page/Content世界坐标与父局部坐标可能不同，须由当前定义的
            // 父级World矩阵变换为局部。不为绘图创建独立的WPF工程真值。
            MEMNode parent=CurrentDrawParent();
            FGEPoint2D local=DrawOriginInParent(parent,x,y);
            return AddShape(name,shape,local.X,local.Y);
        }
        /// <summary>多轮廓Path一次提交；各Move轮廓属于一个MRCShapeElement。</summary>
        public MTCEditResult<bool> DrawPathContours(string name,
            IReadOnlyList<IReadOnlyList<FGEPoint2D>> contours)
        {
            Demand();
            double x,y;
            MRCShapeDefinition shape=MVIShapeCatalog.CreateDrawnContours(
                contours,out x,out y);
            MEMNode parent=CurrentDrawParent();
            FGEPoint2D local=DrawOriginInParent(parent,x,y);
            return AddShape(name,shape,local.X,local.Y);
        }
        /// <summary>绘制起点按现有Shape语义转换到当前工程容器，避免Widget/Text另走坐标算法。</summary>
        private static FGEPoint2D DrawOriginInParent(MEMNode parent,double x,double y)
        {
            var visualParent=parent as MRCVisualItem;
            if(visualParent==null)return new FGEPoint2D(x,y);
            FGETransform2D world=FGETransform2D.Identity;
            for(MEMNode node=visualParent;node!=null;node=node.Parent)
            {
                var item=node as MRCVisualItem;
                if(item!=null)world=item.Transform.ToGeometry().Compose(world);
            }
            return world.Inverse().TransformPoint(new FGEPoint2D(x,y));
        }
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
            MVISceneItem projected=Scene.FirstOrDefault(item=>item.Id==id);
            if(projected==null||!projected.IsSelectable||projected.Address==null||
                projected.Address.InstancePath.Count!=0)
                return MTCEditResult<bool>.Rejected(Document.Revision,
                    "visual.path.not_editable","Path must be a visible unlocked definition");
            if(operation=="split"&&(double.IsNaN(fraction)||double.IsInfinity(fraction)||
                fraction<=0||fraction>=1))
                return MTCEditResult<bool>.Rejected(Document.Revision,
                    "visual.path.invalid_fraction","Split fraction must lie strictly within (0,1)");
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
        /// <summary>
        /// 所有对齐/分布基于场景世界Bounds计算，再还原各父坐标执行一次MEM事务。
        /// 保留旧distributeX“左边缘均分”语义；新增distributeGapX/Y才是真正边界等间距。
        /// 无效/负间距/零位移不产生空Undo，选区内任一对象非法则整组拒绝。
        /// </summary>
        public MTCEditResult<bool> Align(IReadOnlyList<Guid> ids,string mode)
        {
            Demand();
            int revision=Document.Revision;
            if(ids==null||ids.Count<2||ids.Distinct().Count()!=ids.Count)
                return MTCEditResult<bool>.Rejected(revision,"visual.align.selection",
                    "至少选择两个不同的可编辑图元");
            bool gapX=mode=="distributeGapX",gapY=mode=="distributeGapY";
            if(mode!="left"&&mode!="right"&&mode!="top"&&mode!="bottom"&&
                mode!="centerX"&&mode!="centerY"&&mode!="distributeX"&&
                !gapX&&!gapY)
                return MTCEditResult<bool>.Rejected(revision,"visual.align.mode",
                    "Unsupported alignment");
            if((gapX||gapY)&&ids.Count<3)
                return MTCEditResult<bool>.Rejected(revision,"visual.align.needs_three",
                    "可视等间距分布至少需要三个图元");
            var selection=Scene.Where(x=>ids.Contains(x.Id)&&x.IsSelectable)
                .GroupBy(x=>x.Id).Select(g=>g.First()).ToArray();
            if(selection.Length!=ids.Count||
                selection.Any(x=>Document.Find(x.Id) as MRCVisualItem==null))
                return MTCEditResult<bool>.Rejected(revision,"visual.align.definition",
                    "Select editable definitions, not reference instances");
            foreach(MVISceneItem item in selection)
                if(!Route(MVIInteractionKind.Move,item.Id))
                    return MTCEditResult<bool>.Rejected(revision,"visual.default_prevented",
                        "Alignment blocked by interaction policy");
            double targetLeft=selection.Min(x=>x.Bounds.Left);
            double targetRight=selection.Max(x=>x.Bounds.Right);
            double targetTop=selection.Min(x=>x.Bounds.Top);
            double targetBottom=selection.Max(x=>x.Bounds.Bottom);
            double centerX=selection.Average(x=>(x.Bounds.Left+x.Bounds.Right)/2);
            double centerY=selection.Average(x=>(x.Bounds.Top+x.Bounds.Bottom)/2);
            var orderedX=selection.OrderBy(x=>x.Bounds.Left).ThenBy(x=>x.Id).ToArray();
            var orderedY=selection.OrderBy(x=>x.Bounds.Top).ThenBy(x=>x.Id).ToArray();
            double step=(orderedX[orderedX.Length-1].Bounds.Left-
                orderedX[0].Bounds.Left)/(orderedX.Length-1);
            // 以两端最外边界为锚，减掉每个图元的真实世界宽高，剩余才是可分布空隙。
            double gapHorizontal=(orderedX[orderedX.Length-1].Bounds.Right-
                orderedX[0].Bounds.Left-selection.Sum(x=>x.Bounds.Width))/(selection.Length-1);
            double gapVertical=(orderedY[orderedY.Length-1].Bounds.Bottom-
                orderedY[0].Bounds.Top-selection.Sum(x=>x.Bounds.Height))/(selection.Length-1);
            if((gapX&&gapHorizontal<0)||(gapY&&gapVertical<0))
                return MTCEditResult<bool>.Rejected(revision,"visual.align.negative_gap",
                    "选区不足以容纳无重叠等间距布局；请先扩大选区");
            var changes=new List<Tuple<MVISceneItem,double,double>>();
            double cursorX=orderedX[0].Bounds.Left,cursorY=orderedY[0].Bounds.Top;
            var destinationsX=new Dictionary<Guid,double>();
            var destinationsY=new Dictionary<Guid,double>();
            if(gapX)foreach(MVISceneItem item in orderedX)
            {destinationsX[item.Id]=cursorX;cursorX+=item.Bounds.Width+gapHorizontal;}
            if(gapY)foreach(MVISceneItem item in orderedY)
            {destinationsY[item.Id]=cursorY;cursorY+=item.Bounds.Height+gapVertical;}
            foreach(MVISceneItem item in selection)
            {
                double dx=0,dy=0;
                if(mode=="left")dx=targetLeft-item.Bounds.Left;
                if(mode=="right")dx=targetRight-item.Bounds.Right;
                if(mode=="top")dy=targetTop-item.Bounds.Top;
                if(mode=="bottom")dy=targetBottom-item.Bounds.Bottom;
                if(mode=="centerX")dx=centerX-(item.Bounds.Left+item.Bounds.Right)/2;
                if(mode=="centerY")dy=centerY-(item.Bounds.Top+item.Bounds.Bottom)/2;
                if(mode=="distributeX")
                    dx=orderedX[0].Bounds.Left+
                        step*Array.IndexOf(orderedX,item)-item.Bounds.Left;
                if(gapX)dx=destinationsX[item.Id]-item.Bounds.Left;
                if(gapY)dy=destinationsY[item.Id]-item.Bounds.Top;
                if(Math.Abs(dx)<1e-9&&Math.Abs(dy)<1e-9)continue;
                double x,y;item.PositionAfterWorldDelta(dx,dy,out x,out y);
                changes.Add(Tuple.Create(item,x,y));
            }
            if(changes.Count==0)
                return MTCEditResult<bool>.Rejected(revision,"visual.align.no_change",
                    "对象已经处于目标位置");
            return Document.Edit("图元对齐/分布",revision,e=>
            {
                foreach(var change in changes)
                    e.Move((MRCVisualItem)Document.Find(change.Item1.Id),
                        change.Item2,change.Item3);
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
        /// <summary>
        /// 选区级旋转/八点缩放：世界空间统一计算，逐对象还原各自父级局部矩阵；
        /// 变更在同一个MTC事务内提交，Undo仅一步。拒绝父子同选、锁定和实例不可达。
        /// </summary>
        public MTCEditResult<bool> TransformSelection(IReadOnlyList<Guid> ids,
            int handleIndex,double worldX,double worldY,double zoom,
            bool keepAspect=false,bool resizeFromCenter=false,bool snapRotation=false)
        {
            Demand();
            int revision=Document.Revision;
            FGEValidation.Finite(worldX,"worldX");FGEValidation.Finite(worldY,"worldY");
            if(ids==null||ids.Count<2||ids.Count!=ids.Distinct().Count()||
                handleIndex<0||handleIndex>8||zoom<=0||
                double.IsNaN(zoom)||double.IsInfinity(zoom))
                return MTCEditResult<bool>.Rejected(revision,
                    "visual.transform.selection","Invalid multi-selection transform");
            MVISceneItem[] selection=Scene.Where(x=>ids.Contains(x.Id)&&
                x.ElementTypeId!="core.visual.connector"&&x.IsSelectable).ToArray();
            if(selection.Length!=ids.Count)
                return MTCEditResult<bool>.Rejected(revision,
                    "visual.transform.locked_or_hidden","All items must be editable and visible");
            var selectedIds=new HashSet<Guid>(ids);
            foreach(MVISceneItem item in selection)
            {
                MRCVisualItem definition=Document.Find(item.Id) as MRCVisualItem;
                MRIResourceReference reference=Document.FindReference(item.Id);
                if(definition==null&&reference==null)
                    return MTCEditResult<bool>.Rejected(revision,
                        "visual.transform.definition","Unknown edit target");
                if(definition!=null&&definition.Ancestors().Any(a=>selectedIds.Contains(a.Id)))
                    return MTCEditResult<bool>.Rejected(revision,
                        "visual.transform.ancestor_selected","Select parent or child, not both");
                if(!Route(MVIInteractionKind.Move,item.Id))
                    return MTCEditResult<bool>.Rejected(revision,
                        "visual.default_prevented","A selected element rejected the transform");
            }
            MRCTransform delta=MVISelectionTransform.Delta(
                MVISelectionTransform.Bounds(selection),handleIndex,
                new FGEPoint2D(worldX,worldY),zoom,
                keepAspect,resizeFromCenter,snapRotation);
            return Document.Edit("选区整体缩放或旋转",revision,e=>
            {
                foreach(MVISceneItem item in selection)
                {
                    MRCTransform world=delta.Compose(item.WorldTransform);
                    MRCTransform local=item.ParentWorldTransform.Inverse().Compose(world);
                    MRIResourceReference reference=Document.FindReference(item.Id);
                    if(reference!=null)e.SetPlacement(reference,local);
                    else e.SetTransform((MRCVisualItem)Document.Find(item.Id),local);
                }
                return true;
            });
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
            // API的可选网格吸附与Studio预览共用一个纯计算入口。
            // 其他调用者若已拿到吸附位移，可传snapToGrid=false防止重复取整。
            if(snapToGrid)
            {
                MVIVisualSnapResult snapped=MVIVisualSnapping.Resolve(
                    Scene,ids,worldDx,worldDy,1,true,false);
                worldDx=snapped.WorldDx;worldDy=snapped.WorldDy;
            }
            return Document.Edit("批量移动可视对象",revision,e=>
            {
                foreach(MVISceneItem scene in selected)
                {
                    double x,y;
                    scene.PositionAfterWorldDelta(worldDx,worldDy,out x,out y);
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
        {
            int revision=Document.Revision;
            MPCConnection connection=Document.FindConnection(id);
            if(connection!=null)
            {
                if(!Scene.Any(x=>x.Id==id&&x.ElementTypeId=="core.visual.connector"))
                    return MTCEditResult<bool>.Rejected(revision,
                        "visual.connection.not_in_scene","Connection is outside the active visual resource");
                if(!Route(MVIInteractionKind.Delete,id))
                    return MTCEditResult<bool>.Rejected(revision,
                        "visual.default_prevented","Delete canceled before commit");
                return Document.Edit("删除可视连接",revision,e=>
                {e.Disconnect(connection);return true;});
            }
            if(Document.Find(id)==null&&Document.FindReference(id)==null)
                return MTCEditResult<bool>.Rejected(revision,
                    "visual.target_not_found","Visual object is missing");
            if(!Route(MVIInteractionKind.Delete,id))
                return MTCEditResult<bool>.Rejected(revision,
                    "visual.default_prevented","Delete canceled before commit");
            return Document.Edit("删除可视对象",revision,e=>
            {
                var reference=Document.FindReference(id);
                if(reference==null)e.Remove(RequireVisual(id));else e.Remove(reference);
                return true;
            });
        }
        private MRCVisualItem RequireVisual(Guid id) { var item = Document.Find(id) as MRCVisualItem; if (item == null) throw new InvalidOperationException("Missing visual item"); return item; }
        public MTCEditResult<bool> Undo() { return Document.Undo(); }
        public MTCEditResult<bool> Redo() { return Document.Redo(); }
        public void Dispose(){if(_disposed)return;_disposed=true;_watch.Dispose();Changed=null;Interaction=null;}
    }
}
