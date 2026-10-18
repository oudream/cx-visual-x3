using System;
using System.Collections.Generic;
using System.Linq;

namespace CxVisual.Mechanism.Core
{
    public sealed class MRIUsage
    {
        internal MRIUsage(Guid owner,string kind,string member){OwnerId=owner;Kind=kind;MemberId=member;}
        public Guid OwnerId {get;private set;} public string Kind {get;private set;} public string MemberId {get;private set;}
    }
    public sealed partial class MEMEngineeringSession
    {
        private readonly Dictionary<Guid,MPCPort> _ports=new Dictionary<Guid,MPCPort>();
        private readonly Dictionary<Guid,MPCConnection> _connections=new Dictionary<Guid,MPCConnection>();
        private readonly Dictionary<Guid,HashSet<object>> _declarationUses=new Dictionary<Guid,HashSet<object>>();
        private readonly Dictionary<object,Guid[]> _declarationDependencies=new Dictionary<object,Guid[]>();
        private readonly Dictionary<MPCEndpoint,HashSet<MPCConnection>> _incoming=new Dictionary<MPCEndpoint,HashSet<MPCConnection>>();
        private readonly Dictionary<MPCEndpoint,HashSet<MPCConnection>> _outgoing=new Dictionary<MPCEndpoint,HashSet<MPCConnection>>();
        internal bool IdentityExists(Guid id){return _index.ContainsKey(id)||_references.ContainsKey(id)||_ports.ContainsKey(id)||_connections.ContainsKey(id)||FindAsset(id)!=null;}
        public MPCPort FindPort(Guid id){DemandAccess(MEMAccessOperation.Read);lock(_gate){DemandOpen();MPCPort value;return _ports.TryGetValue(id,out value)?value:null;}}
        public MPCConnection FindConnection(Guid id){DemandAccess(MEMAccessOperation.Read);lock(_gate){DemandOpen();MPCConnection value;return _connections.TryGetValue(id,out value)?value:null;}}
        public MPMProperty FindProperty(MEMNode node,string member)
        {DemandAccess(MEMAccessOperation.Read);lock(_gate){DemandOpen();DemandNode(node);return MPMDefinitionState.PropertiesOf(node).FirstOrDefault(p=>p.MemberId==member);}}
        public MPMBinding ResolveParameter(MRIResourceReference reference,string member)
        {
            DemandAccess(MEMAccessOperation.Read);
            lock(_gate)
            {
                DemandReference(reference);var property=FindProperty(Find(reference.TargetId),member);
                if(property==null||!property.Descriptor.IsPublic)throw new InvalidOperationException("org.parameter.not_public");
                MPMBinding value;return reference.OverrideItems!=null&&reference.OverrideItems.TryGetValue(member,out value)?value:property.Binding;
            }
        }
        internal void DemandReference(MRIResourceReference reference)
        {DemandOpen();if(reference==null||FindReference(reference.Id)!=reference)throw new InvalidOperationException("org.reference.foreign_or_missing");}
        public MPCEndpoint Endpoint(MPCPort port)
        {DemandAccess(MEMAccessOperation.Read);lock(_gate){DemandOpen();if(port==null||FindPort(port.Id)!=port)throw new InvalidOperationException("org.port.foreign_or_missing");return new MPCEndpoint(GetAddress(Find(port.OwnerId)),port.Id);}}
        public MPCEndpoint Endpoint(MRIResourceReference reference,string publicMember)
        {
            DemandAccess(MEMAccessOperation.Read);
            lock(_gate)
            {
                DemandReference(reference);var node=Find(reference.TargetId);var port=MPMDefinitionState.PortsOf(node).FirstOrDefault(p=>p.MemberId==publicMember&&p.IsPublic);
                if(port==null)throw new InvalidOperationException("org.port.not_public");return new MPCEndpoint(GetAddress(node,new[]{reference.Id}),port.Id);
            }
        }
        internal MPCPort ResolvePort(MPCEndpoint endpoint,bool throughMapping=false)
        {
            if(endpoint==null)throw new ArgumentNullException("endpoint");var owner=Resolve(endpoint.Address);var port=FindPort(endpoint.PortId);
            if(owner==null||port==null||owner.Id!=port.OwnerId)throw new InvalidOperationException("org.endpoint.missing_or_mismatched");
            if(!throughMapping&&endpoint.Address.InstancePath.Count!=0&&(!port.IsPublic||port.OwnerId!=FindReference(endpoint.Address.InstancePath.Last()).TargetId))throw new InvalidOperationException("org.port.private");return port;
        }
        internal void CheckEndpointScope(MEMNode scope,MPCEndpoint endpoint)
        {
            ResolvePort(endpoint);
            if(endpoint.Address.InstancePath.Count==0)
            {if(!Within(Find(endpoint.Address.ObjectId),scope))throw new InvalidOperationException("org.connection.scope");}
            else
            {var first=FindReference(endpoint.Address.InstancePath[0]);if(first==null||!Within(first.Owner,scope))throw new InvalidOperationException("org.connection.instance_scope");if(endpoint.Address.InstancePath.Count>1)throw new InvalidOperationException("org.connection.must_use_public_boundary");}
        }
        internal void CheckAddressScope(MEMNode scope,MRIObjectAddress address)
        {
            var node=Resolve(address);if(node==null)throw new InvalidOperationException("org.address.missing");
            if(address.InstancePath.Count==0){if(!Within(node,scope))throw new InvalidOperationException("org.address.scope");}
            else{var first=FindReference(address.InstancePath[0]);if(first==null||!Within(first.Owner,scope)||address.InstancePath.Count>1)throw new InvalidOperationException("org.address.public_boundary");}
        }
        internal MPCEndpoint CanonicalEndpoint(MPCEndpoint endpoint)
        {
            var current=endpoint;var seen=new HashSet<MPCEndpoint>();
            while(true)
            {
                if(!seen.Add(current))throw new InvalidOperationException("org.port.mapping_cycle");var owner=Resolve(current.Address);var mapping=MPMDefinitionState.MappingsOf(owner).FirstOrDefault(m=>m.Kind==MRCPublicMappingKind.Port&&m.PublicPortId==current.PortId);
                if(mapping==null)return current;
                var path=current.Address.InstancePath.Concat(mapping.TargetAddress.InstancePath).ToArray();
                current=new MPCEndpoint(new MRIObjectAddress(Project.Id,mapping.TargetAddress.ResourceId,mapping.TargetAddress.ObjectId,path),mapping.TargetPortId.Value);
                ResolvePort(current,true);
            }
        }
        private static void AddEndpointUse(Dictionary<MPCEndpoint,HashSet<MPCConnection>> index,MPCEndpoint endpoint,MPCConnection connection)
        {HashSet<MPCConnection> values;if(!index.TryGetValue(endpoint,out values)){values=new HashSet<MPCConnection>();index.Add(endpoint,values);}values.Add(connection);}
        private static void RemoveEndpointUse(Dictionary<MPCEndpoint,HashSet<MPCConnection>> index,MPCEndpoint endpoint,MPCConnection connection)
        {var values=index[endpoint];values.Remove(connection);if(values.Count==0)index.Remove(endpoint);}
        internal bool EndpointOccupied(MPCEndpoint endpoint,bool input)
        {HashSet<MPCConnection> values;return (input?_incoming:_outgoing).TryGetValue(CanonicalEndpoint(endpoint),out values)&&values.Count!=0;}
        internal void CheckConnection(MEMNode owner,MPCEndpoint source,MPCEndpoint target,MPCConnection ignore=null)
        {
            DemandNode(owner);if(!(owner is IMPMDefinition))throw new InvalidOperationException("org.connection.owner");CheckEndpointScope(owner,source);CheckEndpointScope(owner,target);
            var from=ResolvePort(source);var to=ResolvePort(target);
            if(from.Direction==MPCPortDirection.Input||to.Direction==MPCPortDirection.Output||source.Equals(target))throw new InvalidOperationException("org.port.direction");
            if(from.Kind!=to.Kind||from.TypeId!=to.TypeId||!from.Contract.Equals(to.Contract))throw new InvalidOperationException("org.port.contract_mismatch");
            HashSet<MPCConnection> uses;var cs=CanonicalEndpoint(source);var ct=CanonicalEndpoint(target);
            if(from.Multiplicity==MPCPortMultiplicity.One&&_outgoing.TryGetValue(cs,out uses)&&uses.Any(c=>c!=ignore))throw new InvalidOperationException("org.port.output_cardinality");
            if(to.Multiplicity==MPCPortMultiplicity.One&&_incoming.TryGetValue(ct,out uses)&&uses.Any(c=>c!=ignore))throw new InvalidOperationException("org.port.input_cardinality");
            if(_outgoing.TryGetValue(cs,out uses)&&uses.Any(c=>c!=ignore&&CanonicalEndpoint(c.Target).Equals(ct)))throw new InvalidOperationException("org.connection.duplicate");
            if(target.Address.InstancePath.Count==0&&to.Multiplicity==MPCPortMultiplicity.One)
            {
                HashSet<object> records;if(_declarationUses.TryGetValue(to.Id,out records)&&records.OfType<MRCPublicMapping>().Any(m=>m.TargetPortId==to.Id))throw new InvalidOperationException("org.port.boundary_already_exposed");
            }
        }
        private IEnumerable<Guid> AddressTargets(MRIObjectAddress address)
        {yield return address.ObjectId;foreach(var id in address.InstancePath)yield return id;}
        private IEnumerable<Guid> EndpointTargets(MPCEndpoint endpoint)
        {yield return endpoint.PortId;foreach(var id in AddressTargets(endpoint.Address))yield return id;}
        internal IEnumerable<Guid> BindingTargets(MPMBinding binding)
        {
            if(binding==null)yield break;
            if(binding.Kind==MPMBindingKind.Resource){if(Project.FindResource(binding.ResourceId.Value,true)==null)throw new InvalidOperationException("org.binding.resource_missing");yield return binding.ResourceId.Value;}
            if(binding.Kind==MPMBindingKind.Member)
            {var node=Resolve(binding.Address);if(node==null||!MemberExists(node,binding.MemberId))throw new InvalidOperationException("org.binding.member_missing");if(binding.Address.InstancePath.Count!=0){var last=FindReference(binding.Address.InstancePath.Last());var property=FindProperty(node,binding.MemberId);if(node.Id!=last.TargetId||property==null||!property.Descriptor.IsPublic)throw new InvalidOperationException("org.binding.member_private");}foreach(var id in AddressTargets(binding.Address))yield return id;}
        }
        internal static bool MemberExists(MEMNode node,string member)
        {return MPMDefinitionState.PropertiesOf(node).Any(p=>p.MemberId==member)||(node is MRCVisualItem&&new[]{"x","y","width","height","rotation","scaleX","scaleY","shear","visible","selectable","locked"}.Contains(member));}
        internal void IndexUsage(object record,IEnumerable<Guid> dependencies)
        {
            var ids=dependencies.Distinct().ToArray();if(ids.Length==0)return;_declarationDependencies.Add(record,ids);
            foreach(var id in ids){HashSet<object> uses;if(!_declarationUses.TryGetValue(id,out uses)){uses=new HashSet<object>();_declarationUses.Add(id,uses);}uses.Add(record);}
        }
        internal void UnindexUsage(object record)
        {Guid[] ids;if(!_declarationDependencies.TryGetValue(record,out ids))return;foreach(var id in ids){var uses=_declarationUses[id];uses.Remove(record);if(uses.Count==0)_declarationUses.Remove(id);}_declarationDependencies.Remove(record);}
        internal void IndexPort(MPCPort port)
        {if(IdentityExists(port.Id))throw new InvalidOperationException("org.id.duplicate");_ports.Add(port.Id,port);if(port.Contract.Calibration!=null)IndexUsage(port,AddressTargets(port.Contract.Calibration));}
        internal void UnindexPort(MPCPort port){UnindexUsage(port);_ports.Remove(port.Id);}
        internal void IndexConnection(MPCConnection connection)
        {
            if(IdentityExists(connection.Id))throw new InvalidOperationException("org.id.duplicate");_connections.Add(connection.Id,connection);
            IndexUsage(connection,EndpointTargets(connection.Source).Concat(EndpointTargets(connection.Target)));
            AddEndpointUse(_outgoing,CanonicalEndpoint(connection.Source),connection);AddEndpointUse(_incoming,CanonicalEndpoint(connection.Target),connection);
        }
        internal void UnindexConnection(MPCConnection connection)
        {RemoveEndpointUse(_outgoing,CanonicalEndpoint(connection.Source),connection);RemoveEndpointUse(_incoming,CanonicalEndpoint(connection.Target),connection);UnindexUsage(connection);_connections.Remove(connection.Id);}
        internal void IndexMapping(MRCPublicMapping mapping)
        {IndexUsage(mapping,AddressTargets(mapping.TargetAddress).Concat(mapping.TargetPortId.HasValue?new[]{mapping.TargetPortId.Value,mapping.PublicPortId.Value}:new Guid[0]));}
        internal bool HasDeclarationUse(Guid id){HashSet<object> uses;return _declarationUses.TryGetValue(id,out uses)&&uses.Count!=0;}
        internal bool HasOtherDeclarationUse(Guid id,object except){HashSet<object> uses;return _declarationUses.TryGetValue(id,out uses)&&uses.Any(r=>r!=except);}
        private static Guid RecordOwner(object record)
        {var c=record as MPCConnection;if(c!=null)return c.OwnerId;var m=record as MRCPublicMapping;if(m!=null)return m.OwnerId;var p=record as MPMProperty;if(p!=null)return p.OwnerId;var port=record as MPCPort;if(port!=null)return port.OwnerId;var r=record as MRIResourceReference;if(r!=null)return r.Owner.Id;var binding=record as Tuple<Guid,string>;if(binding!=null)return binding.Item1;throw new InvalidOperationException("Unknown usage record");}
        internal void CheckDeclarationRemove(ISet<MEMNode> removed)
        {
            if(_declarationUses.Count==0)return;var owners=new HashSet<Guid>(removed.Select(n=>n.Id));var targets=new HashSet<Guid>(owners);
            foreach(var node in removed){foreach(var port in MPMDefinitionState.PortsOf(node))targets.Add(port.Id);var content=node as MEMContent;if(content!=null)foreach(var r in content.References)targets.Add(r.Id);}
            foreach(var id in targets){HashSet<object> uses;if(_declarationUses.TryGetValue(id,out uses)&&uses.Any(record=>!owners.Contains(RecordOwner(record))))throw new InvalidOperationException("org.declaration.in_use");}
        }
        internal void CheckDeclarationMove(MEMNode node,MEMNode parent)
        {
            Guid newRoot=parent is MEMProject?node.Id:parent.ResourceRootId;if(newRoot==node.ResourceRootId)return;
            foreach(var child in node.DescendantsAndSelf())if((MPMDefinitionState.For(child)!=null&&MPMDefinitionState.For(child).HasData)||HasDeclarationUse(child.Id))throw new InvalidOperationException("org.move.contract_resource_boundary: use an explicit transfer plan");
        }
        internal void IndexDeclarationTree(MEMNode root)
        {
            foreach(var node in root.DescendantsAndSelf())
            {foreach(var port in MPMDefinitionState.PortsOf(node))IndexPort(port);foreach(var property in MPMDefinitionState.PropertiesOf(node)){CheckBindingAssignment(node,property.MemberId,property.Binding,property.Descriptor.ValueKind);IndexUsage(property,BindingTargets(property.Binding));}var visual=node as MRCVisualItem;if(visual!=null&&visual.BindingItems!=null)foreach(var pair in visual.BindingItems){CheckBindingAssignment(node,pair.Key,pair.Value);IndexUsage(Tuple.Create(node.Id,pair.Key),BindingTargets(pair.Value));}}
            foreach(var node in root.DescendantsAndSelf())foreach(var mapping in MPMDefinitionState.MappingsOf(node))IndexMapping(mapping);
            foreach(var node in root.DescendantsAndSelf())foreach(var connection in MPMDefinitionState.ConnectionsOf(node)){CheckConnection(node,connection.Source,connection.Target);IndexConnection(connection);}
        }
        internal void UnindexDeclarationTree(MEMNode root)
        {
            foreach(var node in root.DescendantsAndSelf())foreach(var c in MPMDefinitionState.ConnectionsOf(node))UnindexConnection(c);
            foreach(var node in root.DescendantsAndSelf()){foreach(var m in MPMDefinitionState.MappingsOf(node))UnindexUsage(m);foreach(var p in MPMDefinitionState.PropertiesOf(node))UnindexUsage(p);foreach(var port in MPMDefinitionState.PortsOf(node))UnindexPort(port);var visual=node as MRCVisualItem;if(visual!=null&&visual.BindingItems!=null)foreach(var pair in visual.BindingItems)UnindexUsage(Tuple.Create(node.Id,pair.Key));}
        }
        internal void ValidateDeclarations(List<string> issues)
        {
            int ports=0,connections=0;
            foreach(var node in Project.DescendantsAndSelf())
            {
                foreach(var port in MPMDefinitionState.PortsOf(node)){ports++;if(port.OwnerId!=node.Id||FindPort(port.Id)!=port)issues.Add("org.port.index_inconsistent");}
                foreach(var mapping in MPMDefinitionState.MappingsOf(node)){try{CheckMapping(node,mapping);}catch(InvalidOperationException ex){issues.Add(ex.Message);}}
                foreach(var connection in MPMDefinitionState.ConnectionsOf(node)){connections++;try{if(connection.OwnerId!=node.Id||FindConnection(connection.Id)!=connection)issues.Add("org.connection.index_inconsistent");CheckConnection(node,connection.Source,connection.Target,connection);}catch(InvalidOperationException ex){issues.Add(ex.Message);}}
            }
            if(ports!=_ports.Count||connections!=_connections.Count)issues.Add("org.declaration.orphan_index");
            foreach(var reference in _references.Values)foreach(var pair in reference.Overrides)
            {try{var property=FindProperty(Find(reference.TargetId),pair.Key);if(property==null||!property.Descriptor.IsPublic)throw new InvalidOperationException("org.parameter.not_public");property.Descriptor.Validate(pair.Value);}catch(InvalidOperationException ex){issues.Add(ex.Message);}}
        }
        internal void CheckMapping(MEMNode owner,MRCPublicMapping mapping)
        {
            var target=Resolve(mapping.TargetAddress);if(target==null||target==owner)throw new InvalidOperationException("org.mapping.target_missing");
            if(mapping.Kind==MRCPublicMappingKind.Port)
            {
                var endpoint=new MPCEndpoint(mapping.TargetAddress,mapping.TargetPortId.Value);CheckEndpointScope(owner,endpoint);var inner=ResolvePort(endpoint);var outer=FindPort(mapping.PublicPortId.Value);
                if(outer==null||outer.OwnerId!=owner.Id||!outer.IsPublic||outer.MemberId!=mapping.MemberId||inner.Direction!=outer.Direction||inner.Kind!=outer.Kind||inner.TypeId!=outer.TypeId||inner.Multiplicity!=outer.Multiplicity||inner.Required!=outer.Required||!inner.Contract.Equals(outer.Contract))throw new InvalidOperationException("org.mapping.port_contract");
                CanonicalEndpoint(new MPCEndpoint(GetAddress(owner),outer.Id));
            }
            else
            {
                var property=FindProperty(owner,mapping.MemberId);
                CheckAddressScope(owner,mapping.TargetAddress);if(property==null||!property.Descriptor.IsPublic||!MemberExists(target,mapping.TargetMemberId)||MemberKind(target,mapping.TargetMemberId)!=property.Descriptor.ValueKind)throw new InvalidOperationException("org.mapping.property_contract");
            }
        }
        internal void AttachLoadedDeclarations(IDictionary<Guid,MPMDefinitionState> declarations)
        {
            foreach(var pair in declarations)
            {
                var node=Find(pair.Key);var state=MPMDefinitionState.For(node,true);state.PropertyItems=pair.Value.PropertyItems;state.PortItems=pair.Value.PortItems;state.ConnectionItems=pair.Value.ConnectionItems;state.MappingItems=pair.Value.MappingItems;state.AspectItems=pair.Value.AspectItems;
            }
            IndexDeclarationTree(Project);
            foreach(var reference in _references.Values){foreach(var pair in reference.Overrides){var property=FindProperty(Find(reference.TargetId),pair.Key);if(property==null||!property.Descriptor.IsPublic)throw new InvalidOperationException("org.parameter.not_public");CheckBindingAssignment(Find(reference.TargetId),pair.Key,pair.Value,property.Descriptor.ValueKind,reference);}IndexUsage(reference,reference.Overrides.Values.SelectMany(BindingTargets));}
        }
        public MVEValidationReport ValidateForRun(MEMNode entry,IEnumerable<string> availableProviders=null)
        {
            DemandAccess(MEMAccessOperation.PrepareRun);
            lock(_gate)
            {
                DemandOpen();DemandNode(entry);var providers=new HashSet<string>(availableProviders??new string[0],StringComparer.Ordinal);var issues=new List<string>();
                var pending=new Stack<Tuple<MEMNode,MRIResourceReference>>();pending.Push(Tuple.Create(entry,(MRIResourceReference)null));
                int expanded=0;
                while(pending.Count!=0)
                {
                    if(++expanded>MEMNode.MaximumNodeCount){issues.Add("org.run.expansion_budget");break;}
                    var next=pending.Pop();var node=next.Item1;
                    foreach(var property in MPMDefinitionState.PropertiesOf(node))
                    {var binding=next.Item2!=null&&node.Id==next.Item2.TargetId&&property.Descriptor.IsPublic?ResolveParameter(next.Item2,property.MemberId):property.Binding;if(property.Descriptor.Required&&(binding==null||(binding.Kind==MPMBindingKind.Literal&&binding.Value.Kind==MPMValueKind.Null)))issues.Add("org.run.required_property:"+node.Id+":"+property.MemberId);}
                    foreach(var aspect in MPMDefinitionState.AspectsOf(node))if(aspect.Required&&!providers.Contains(aspect.ProviderId))issues.Add("org.run.provider_missing:"+aspect.ProviderId);
                    foreach(var child in node.Children)pending.Push(Tuple.Create(child,next.Item2));
                    var content=node as MEMContent;if(content!=null)foreach(var reference in content.References)pending.Push(Tuple.Create(Find(reference.TargetId),reference));
                }
                return new MVEValidationReport(issues);
            }
        }
        public IReadOnlyList<MRIUsage> FindUsages(Guid id)
        {
            DemandAccess(MEMAccessOperation.Read);
            lock(_gate)
            {
                DemandOpen();var result=new List<MRIUsage>();HashSet<MRIResourceReference> references;if(_usages.TryGetValue(id,out references))result.AddRange(references.Select(r=>new MRIUsage(r.Owner.Id,"Reference",r.Name)));
                HashSet<object> records;if(_declarationUses.TryGetValue(id,out records))result.AddRange(records.Select(r=>new MRIUsage(RecordOwner(r),r.GetType().Name,"")));return result.AsReadOnly();
            }
        }
        public MPMProperty DefineProperty(MEMNode owner,MPMPropertyDescriptor descriptor){return Edit("声明属性",e=>e.DefineProperty(owner,descriptor)).RequireValue();}
        public void SetProperty(MEMNode owner,string member,MPMBinding binding){Edit("设置属性",e=>e.SetProperty(owner,member,binding)).RequireValue();}
        public void SetOverride(MRIResourceReference reference,string member,MPMBinding binding){Edit("设置实例参数",e=>e.SetOverride(reference,member,binding)).RequireValue();}
        public void ClearOverride(MRIResourceReference reference,string member){Edit("清除实例参数",e=>e.SetOverride(reference,member,null)).RequireValue();}
        public void SetPlacement(MRIResourceReference reference,MRCTransform transform){Edit("设置实例布局",e=>e.SetPlacement(reference,transform)).RequireValue();}
        public void Rename(MRIResourceReference reference,string name){Edit("重命名实例",e=>e.Rename(reference,name)).RequireValue();}
        public void Retarget(MRIResourceReference reference,MEMNode definition){Edit("替换资源定义",e=>e.Retarget(reference,definition)).RequireValue();}
        internal IReadOnlyList<MPCConnection> RetargetConnections(MRIResourceReference reference,MEMNode definition)
        {
            HashSet<object> records;if(!_declarationUses.TryGetValue(reference.Id,out records))return MPMEmpty<MPCConnection>.Items;
            if(records.Any(r=>!(r is MPCConnection)))throw new InvalidOperationException("org.retarget.requires_transfer_plan: mapped/member-bound instances require explicit remapping");
            Func<MPCEndpoint,MPCEndpoint> remap=endpoint=>
            {
                if(!endpoint.Address.InstancePath.Contains(reference.Id))return endpoint;
                if(endpoint.Address.InstancePath.Count!=1||endpoint.Address.ObjectId!=reference.TargetId)throw new InvalidOperationException("org.retarget.nested_endpoint");
                var old=ResolvePort(endpoint);var port=MPMDefinitionState.PortsOf(definition).FirstOrDefault(p=>p.MemberId==old.MemberId&&p.IsPublic);
                if(port==null||port.Direction!=old.Direction||port.Kind!=old.Kind||port.TypeId!=old.TypeId||port.Multiplicity!=old.Multiplicity||!port.Contract.Equals(old.Contract))throw new InvalidOperationException("org.retarget.public_port_mismatch");
                return new MPCEndpoint(new MRIObjectAddress(Project.Id,definition.ResourceRootId,definition.Id,endpoint.Address.InstancePath),port.Id);
            };
            return records.Cast<MPCConnection>().Select(c=>new MPCConnection(c.Id,c.OwnerId,remap(c.Source),remap(c.Target))).ToList().AsReadOnly();
        }
        public MPCPort DefinePort(MEMNode owner,string member,MPCPortDirection direction,string typeId,MPCPortKind kind=MPCPortKind.Data,MPCPortMultiplicity multiplicity=MPCPortMultiplicity.Many,MTUContractReference contract=null,bool isPublic=false,bool required=false)
        {return Edit("声明端口",e=>e.DefinePort(owner,member,direction,typeId,kind,multiplicity,contract,isPublic,required)).RequireValue();}
        public void RemovePort(MPCPort port){Edit("删除端口",e=>e.RemovePort(port)).RequireValue();}
        public MPCConnection Connect(MEMNode owner,MPCEndpoint source,MPCEndpoint target){return Edit("连接端口",e=>e.Connect(owner,source,target)).RequireValue();}
        public void Disconnect(MPCConnection connection){Edit("断开端口",e=>e.Disconnect(connection)).RequireValue();}
        public MPCPort ExposePort(MEMNode definition,string member,MPCEndpoint target){return Edit("公开端口",e=>e.ExposePort(definition,member,target)).RequireValue();}
        public void ExposeProperty(MEMNode definition,string member,MRIObjectAddress target,string targetMember){Edit("映射公开属性",e=>e.ExposeProperty(definition,member,target,targetMember)).RequireValue();}
        public void Unexpose(MEMNode definition,string member,MRCPublicMappingKind kind){Edit("取消公开映射",e=>e.Unexpose(definition,member,kind)).RequireValue();}
        public void SetAspect(MEMNode owner,MPMAspect aspect){Edit("设置能力声明",e=>e.SetAspect(owner,aspect)).RequireValue();}
    }
}
