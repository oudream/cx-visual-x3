using System;
using System.Collections.Generic;
using System.Linq;

namespace CxVisual.Mechanism.Core
{
    public sealed partial class MEMEngineeringSession
    {
        internal Guid SessionIdentity {get{return _sessionIdentity;}}
        private void TransferFacts(out MEMProjectSnapshot snapshot,out Dictionary<Guid,MEMResourceEntry> resources,out string hash)
        {
            DemandAccess(MEMAccessOperation.Export);
            lock(_gate)
            {
                DemandOpen();if(_editing)throw new InvalidOperationException("org.transfer.uncommitted_source");snapshot=new MEMProjectSnapshot(_project,Revision);
                resources=_project.Resources.ToDictionary(r=>r.Id,r=>GetResourceEntry(r.Id));using(var xml=MEMFileProjectStore.Capture(this,System.Threading.CancellationToken.None).OpenRead())using(var sha=System.Security.Cryptography.SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(xml)).Replace("-","").ToLowerInvariant();
            }
        }
        public MTITransferPlan PreviewImport(MEMEngineeringSession source,IEnumerable<Guid> roots,MEMNode parent,MTIExternalPolicy policy=MTIExternalPolicy.IncludeClosure,IMTIAspectRemapper remapper=null)
        {
            if(source==null)throw new ArgumentNullException("source");DemandAccess(MEMAccessOperation.Edit);
            MEMProjectSnapshot snapshot;Dictionary<Guid,MEMResourceEntry> resources;string hash;source.TransferFacts(out snapshot,out resources,out hash);
            MTITransferPlan plan;
            lock(_gate){DemandOpen();DemandNode(parent);if(_editing||_collectingTypes!=0)throw new InvalidOperationException("org.transfer.busy");plan=new MTITransferPlan(snapshot,source,resources,this,parent,roots,policy,hash);_collectingTypes=1;}
            try{plan.PrepareAspects(remapper);}finally{lock(_gate)_collectingTypes=0;}
            if(plan.IsValid){var validation=PreviewDraft(plan.BaseRevision,e=>e.Import(plan));if(!validation.IsValid)plan.Reject(validation.Issues);}return plan;
        }
        public MTITransferPlan PreviewDuplicate(IEnumerable<Guid> roots,MEMNode parent){return PreviewImport(this,roots,parent,MTIExternalPolicy.Pin);}
        public MTITransferResult Import(MTITransferPlan plan)
        {
            if(plan==null)throw new ArgumentNullException("plan");if(!plan.IsValid)throw new InvalidOperationException(string.Join(";",plan.Issues));
            if(plan.TargetSessionId!=_sessionIdentity)throw new InvalidOperationException("org.transfer.foreign_plan");
            if(!plan.SourceSession.IsClosed&&plan.SourceSession.Revision!=plan.SourceRevision)throw new InvalidOperationException("org.transfer.source_changed");
            Edit("导入工程对象",plan.BaseRevision,e=>{e.Import(plan);return true;}).RequireValue();return new MTITransferResult(plan,Revision);
        }
        public MTITransferResult Duplicate(IEnumerable<Guid> roots,MEMNode parent){return Import(PreviewDuplicate(roots,parent));}

        /// <summary>
        /// 可视编辑的完整复制：复用MTI身份映射，在一次MTC事务中导入节点、
        /// 偏移顶层图元，并复制两端都属于被选子树的MPC视觉连接。
        /// 跨选区的连接保持原状，不复制悬空端点或生成第二份图模型。
        /// </summary>
        public MTITransferResult DuplicateVisual(IEnumerable<Guid> roots,MEMNode parent,
            double deltaX,double deltaY)
        {
            if(roots==null)throw new ArgumentNullException("roots");
            if(parent==null)throw new ArgumentNullException("parent");
            if(double.IsNaN(deltaX)||double.IsInfinity(deltaX)||
                double.IsNaN(deltaY)||double.IsInfinity(deltaY))
                throw new ArgumentException("visual.duplicate.invalid_offset");
            var ids=roots.ToArray();
            if(ids.Length==0||ids.Length!=ids.Distinct().Count())
                throw new InvalidOperationException("visual.duplicate.invalid_selection");
            var items=ids.Select(id=>Find(id) as MRCVisualItem).ToArray();
            if(items.Any(item=>item==null||item.Parent!=parent))
                throw new InvalidOperationException("visual.duplicate.requires_siblings");
            // 内部视觉线的真实Owner是最近的Content/Symbol定义，
            // 即使复制对象位于多层Group中，也绝不把连线伪装成Group子对象。
            MEMNode connectionOwner=parent;
            while(connectionOwner!=null&&!(connectionOwner is IMPMDefinition))
                connectionOwner=connectionOwner.Parent;
            var definition=connectionOwner as IMPMDefinition;
            var included=new HashSet<Guid>(items
                .SelectMany(item=>item.DescendantsAndSelf()).Select(node=>node.Id));
            var internalConnections=definition==null?new MPCConnection[0]:
                definition.Connections.Where(connection=>
                    FindPort(connection.Source.PortId)!=null&&
                    FindPort(connection.Target.PortId)!=null&&
                    FindPort(connection.Source.PortId).Kind==MPCPortKind.Visual&&
                    FindPort(connection.Target.PortId).Kind==MPCPortKind.Visual&&
                    connection.Source.Address.InstancePath.Count==0&&
                    connection.Target.Address.InstancePath.Count==0&&
                    included.Contains(connection.Source.Address.ObjectId)&&
                    included.Contains(connection.Target.Address.ObjectId)).ToArray();
            // 现有预览包含依赖、类型、资产、绑定与旧版工程兼容校验，绝不跳过。
            MTITransferPlan plan=PreviewDuplicate(ids,parent);
            if(!plan.IsValid)
                throw new InvalidOperationException(string.Join(";",plan.Issues));
            var connectionMappings=new Dictionary<Guid,Guid>();
            Edit("复制可视图元",plan.BaseRevision,e=>
            {
                e.Import(plan);
                foreach(var original in items)
                {
                    var cloned=(MRCVisualItem)Find(plan.IdMap[original.Id]);
                    e.Move(cloned,original.X+deltaX,original.Y+deltaY);
                }
                foreach(MPCConnection connection in internalConnections)
                {
                    var sourceNode=Find(plan.IdMap[connection.Source.Address.ObjectId]);
                    var targetNode=Find(plan.IdMap[connection.Target.Address.ObjectId]);
                    var source=new MPCEndpoint(GetAddress(sourceNode),
                        plan.IdMap[connection.Source.PortId]);
                    var target=new MPCEndpoint(GetAddress(targetNode),
                        plan.IdMap[connection.Target.PortId]);
                    MPCConnection cloned=e.Connect(connectionOwner,source,target);
                    connectionMappings.Add(connection.Id,cloned.Id);
                    // 路由默认值来自MPC实现，不能假定默认Straight或Orthogonal。
                    // 以原连接的明确路由与人工折点为准；MTC内部负责相同值短路。
                    e.SetVisualRoute(cloned,connection.VisualRouteKind,
                        connection.VisualWaypoints);
                }
                return true;
            }).RequireValue();
            return new MTITransferResult(plan,Revision,connectionMappings);
        }

    }
    public sealed partial class MTCEditSession
    {
        internal void Import(MTITransferPlan plan)
        {
            Demand();var parent=_session.Find(plan.ParentId);if(parent==null)throw new InvalidOperationException("org.transfer.destination_missing");DemandEditable(parent);
            var selected=new HashSet<Guid>(plan.SelectedRoots);var deps=new HashSet<Guid>(plan.DependencyRoots);
            foreach(var type in plan.Source.RegisteredTypes.Where(t=>plan.Source.Nodes.Any(n=>plan.Included.Contains(n.Id)&&n.TypeId==t.TypeId)))if(_session.FindType(type.TypeId)==null)RegisterType(type);
            foreach(var asset in plan.Source.Assets.Where(a=>plan.CopiedAssets.Contains(a.Id)))AddAsset(asset.WithId(plan.Map(asset.Id)));
            foreach(var snapshot in plan.Source.Nodes.Where(n=>plan.Included.Contains(n.Id)))
            {
                MEMNode destination=selected.Contains(snapshot.Id)?parent:deps.Contains(snapshot.Id)?_session.Project:_session.Find(plan.Map(snapshot.ParentId.Value));
                var node=MTITransferPlan.CreateNode(snapshot,plan.Map(snapshot.Id));Add(destination,node);
                if(snapshot.Visual!=null)
                {
                    var v=snapshot.Visual;var item=(MRCVisualItem)node;Geometry(item,v.X,v.Y,v.Width,v.Height,v.Rotation,v.ScaleX,v.ScaleY,v.Shear);SetVisibility(item,v.IsVisible,v.IsSelectable);SetLayer(item,v.LayerId);
                }
            }
            // Declarations are staged in dependency order; every mutation uses the same journal and indices.
            foreach(var n in plan.Source.Nodes.Where(n=>plan.Included.Contains(n.Id)))
            {
                var owner=_session.Find(plan.Map(n.Id));
                foreach(var p in n.Properties)
                {var d=p.Descriptor;DefineProperty(owner,new MPMPropertyDescriptor(d.MemberId,d.ValueKind,d.Required,d.IsPublic,null,d.Minimum,d.Maximum,d.IsReadOnly));}
                foreach(var p in n.Ports)
                {
                    var contract=p.Contract;var port=new MPCPort(plan.Map(p.Id),owner.Id,p.MemberId,p.Direction,p.TypeId,p.Kind,p.Multiplicity,new MTUContractReference(contract.UnitId,contract.CoordinateFrameId,contract.TimeBaseId,contract.Calibration==null?null:plan.Map(contract.Calibration)),p.IsPublic,p.Required);
                    AddTransferredPort(owner,port);
                }
            }
            foreach(var n in plan.Source.Nodes.Where(n=>plan.Included.Contains(n.Id)))
            {
                var owner=_session.Find(plan.Map(n.Id));if(!(owner is MEMContent||owner is MEMSymbol))continue;
                foreach(var r in n.References)
                {var target=_session.Find(plan.Map(r.TargetId));_session.CheckReference(owner,target);AddReference(new MRIResourceReference(plan.Map(r.Id),r.Name,target.Id,r.Kind,owner));}
            }
            foreach(var n in plan.Source.Nodes.Where(n=>plan.Included.Contains(n.Id)))
            {
                var owner=_session.Find(plan.Map(n.Id));
                foreach(var p in n.Properties)
                {
                    var d=p.Descriptor;var property=_session.FindProperty(owner,d.MemberId);var replacement=new MPMPropertyDescriptor(d.MemberId,d.ValueKind,d.Required,d.IsPublic,plan.Map(d.DefaultBinding),d.Minimum,d.Maximum,d.IsReadOnly);
                    _session.CheckBindingAssignment(owner,d.MemberId,replacement.DefaultBinding,replacement.ValueKind);
                    var state=Definition(owner);int index=state.PropertyItems.IndexOf(property);var final=new MPMProperty(owner.Id,replacement);
                    var dependencies=_session.BindingTargets(final.Binding).ToArray();
                    Change(()=>{state.PropertyItems[index]=final;_session.IndexUsage(final,dependencies);},()=>{_session.UnindexUsage(final);state.PropertyItems[index]=property;},MTCChangeKind.Property,owner.Id);
                    if(p.HasOverride)SetProperty(owner,d.MemberId,plan.Map(p.Binding));
                }
                if(n.Visual!=null)foreach(var binding in n.Visual.Bindings)SetBinding((MRCVisualItem)owner,binding.Key,plan.Map(binding.Value));
                foreach(var aspect in n.Aspects)SetAspect(owner,plan.MappedAspects[aspect]);
                foreach(var r in n.References){var reference=_session.FindReference(plan.Map(r.Id));SetPlacement(reference,r.Placement);foreach(var value in r.Overrides)SetOverride(reference,value.Key,plan.Map(value.Value));}
            }
            foreach(var n in plan.Source.Nodes.Where(n=>plan.Included.Contains(n.Id)))foreach(var mapping in n.PublicMappings)
            {
                var owner=_session.Find(plan.Map(n.Id));var mapped=mapping.Kind==MRCPublicMappingKind.Property?new MRCPublicMapping(owner.Id,mapping.MemberId,plan.Map(mapping.TargetAddress),mapping.TargetMemberId):new MRCPublicMapping(owner.Id,mapping.MemberId,plan.Map(mapping.PublicPortId.Value),new MPCEndpoint(plan.Map(mapping.TargetAddress),plan.Map(mapping.TargetPortId.Value)));
                var state=Definition(owner);_session.CheckMapping(owner,mapped);
                Change(()=>{if(state.MappingItems==null)state.MappingItems=new List<MRCPublicMapping>();state.MappingItems.Add(mapped);_session.IndexMapping(mapped);},()=>{_session.UnindexUsage(mapped);state.MappingItems.Remove(mapped);},MTCChangeKind.Mapping,owner.Id);
            }
            foreach(var n in plan.Source.Nodes.Where(n=>plan.Included.Contains(n.Id)))foreach(var c in n.Connections)
            {
                var owner=_session.Find(plan.Map(n.Id));AddTransferredConnection(new MPCConnection(plan.Map(c.Id),owner.Id,plan.Map(c.Source),plan.Map(c.Target)).CopyRouteFrom(c));
            }
            foreach(var n in plan.Source.Nodes.Where(n=>plan.Included.Contains(n.Id)&&n.Visual!=null&&n.Visual.IsLocked).Reverse())SetLocked((MRCVisualItem)_session.Find(plan.Map(n.Id)),true);
            var report=_session.Validate();if(!report.IsValid)throw new InvalidOperationException(string.Join(";",report.Issues));
        }
        private void AddTransferredPort(MEMNode owner,MPCPort port)
        {
            var state=Definition(owner);if(MPMDefinitionState.PortsOf(owner).Any(p=>p.MemberId==port.MemberId))throw new InvalidOperationException("org.port.member_duplicate");
            if(port.Contract.Calibration!=null&&_session.Resolve(port.Contract.Calibration)==null)throw new InvalidOperationException("org.port.calibration_missing");
            Change(()=>{if(state.PortItems==null)state.PortItems=new List<MPCPort>();_session.IndexPort(port);state.PortItems.Add(port);},()=>{state.PortItems.Remove(port);_session.UnindexPort(port);},MTCChangeKind.Port,port.Id);
        }
    }
}
