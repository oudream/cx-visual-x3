using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace CxVisual.Mechanism.Core
{
    public enum MTIExternalPolicy {Reject=1,Pin=2,IncludeClosure=3}
    public interface IMTIAspectRemapper { MPMAspect Remap(MPMAspect aspect,MTITransferPlan plan); }
    public sealed class MTITransferResult
    {
        /// <summary>
        /// 返回新建工程身份的映射。普通Import/Duplicate直接保留预览映射；
        /// 可视Duplicate额外复制了所属Content中的内部MPC连接，
        /// 其新旧连接身份也必须进入同一结果，便于UI和工程诊断精确定位。
        /// </summary>
        internal MTITransferResult(MTITransferPlan plan,int revision,
            IDictionary<Guid,Guid> additionalMappings=null)
        {
            if(additionalMappings==null||additionalMappings.Count==0)
                IdMap=plan.IdMap;
            else
            {
                var merged=new Dictionary<Guid,Guid>(plan.IdMap);
                foreach(var pair in additionalMappings)
                {
                    if(merged.ContainsKey(pair.Key)||merged.ContainsValue(pair.Value))
                        throw new InvalidOperationException("org.transfer.conflicting_identity");
                    merged.Add(pair.Key,pair.Value);
                }
                IdMap=new ReadOnlyDictionary<Guid,Guid>(merged);
            }
            RootIds=Array.AsReadOnly(plan.SelectedRoots.Select(id=>plan.IdMap[id]).ToArray());
            Revision=revision;
        }
        public IReadOnlyDictionary<Guid,Guid> IdMap {get;private set;}
        public IReadOnlyList<Guid> RootIds {get;private set;}
        public int Revision {get;private set;}
    }
    /// <summary>Immutable facts and intended identities. Preview owns no mutable engineering objects.</summary>
    public sealed class MTITransferPlan
    {
        internal MEMProjectSnapshot Source;
        internal MEMEngineeringSession SourceSession;
        internal Guid TargetSessionId,ParentId,TargetProjectId;
        internal IReadOnlyList<Guid> SelectedRoots,DependencyRoots;
        internal HashSet<Guid> Included=new HashSet<Guid>();
        internal HashSet<Guid> CopiedAssets=new HashSet<Guid>();
        internal Dictionary<Guid,Guid> NewResourceRoots=new Dictionary<Guid,Guid>();
        internal readonly Dictionary<MPMAspect,MPMAspect> MappedAspects=new Dictionary<MPMAspect,MPMAspect>();
        private Dictionary<Guid,Guid> _map=new Dictionary<Guid,Guid>();
        private readonly List<string> _issues=new List<string>();
        private Dictionary<Guid,MEMNodeSnapshot> _owners=new Dictionary<Guid,MEMNodeSnapshot>();
        private Dictionary<Guid,MEMResourceEntry> _resources;
        private readonly HashSet<Guid> _pinVisited=new HashSet<Guid>();
        internal MTITransferPlan(MEMProjectSnapshot source,MEMEngineeringSession sourceSession,Dictionary<Guid,MEMResourceEntry> resources,
            MEMEngineeringSession target,MEMNode parent,IEnumerable<Guid> roots,MTIExternalPolicy policy,string hash)
        {
            Source=source;SourceSession=sourceSession;_resources=resources;SourceHash=hash;SourceRevision=source.Revision;TargetSessionId=target.SessionIdentity;TargetProjectId=target.Project.Id;ParentId=parent.Id;BaseRevision=target.Revision;Policy=policy;
            foreach(var node in source.Nodes){_owners.Add(node.Id,node);foreach(var port in node.Ports)_owners.Add(port.Id,node);foreach(var connection in node.Connections)_owners.Add(connection.Id,node);foreach(var reference in node.References)_owners.Add(reference.Id,node);}
            var selected=roots==null?source.Find(source.ProjectId).ChildIds.Concat(source.Assets.Select(a=>a.Id)).ToArray():roots.ToArray();SelectedRoots=Array.AsReadOnly(selected);var dependencies=new List<Guid>();DependencyRoots=dependencies.AsReadOnly();
            if(!Enum.IsDefined(typeof(MTIExternalPolicy),policy)||selected.Length==0||selected.Distinct().Count()!=selected.Length){_issues.Add("org.transfer.invalid_selection");Seal();return;}
            foreach(var id in selected)
            {
                var node=source.Find(id);
                if(node==null)
                {
                    var asset=source.Assets.FirstOrDefault(a=>a.Id==id);if(asset==null||!(parent is MEMProject)){_issues.Add("org.transfer.invalid_root");continue;}
                    if(target.Assets.Any(a=>a.Name==asset.Name)){_issues.Add("org.transfer.asset_name_conflict:"+asset.Name);continue;}NewId(asset.Id);CopiedAssets.Add(asset.Id);continue;
                }
                if(node.Kind==MEMNodeKind.Project){_issues.Add("org.transfer.invalid_root");continue;}
                for(var ancestor=node.ParentId;ancestor.HasValue;ancestor=source.Find(ancestor.Value).ParentId)if(selected.Contains(ancestor.Value))_issues.Add("org.transfer.overlapping_roots");
                if(!parent.Accepts(CreateNode(node,id)))_issues.Add("org.transfer.illegal_destination");Include(id,_map.ContainsKey(id)?_map[id]:NewId(id),parent is MEMProject?(Guid?)null:parent.ResourceRootId);
            }
            _map[source.ProjectId]=target.Project.Id;
            var pending=new Queue<MEMNodeSnapshot>(source.Nodes.Where(n=>Included.Contains(n.Id)));var examined=new HashSet<Guid>();
            while(pending.Count!=0)
            {
                var node=pending.Dequeue();if(!examined.Add(node.Id))continue;
                foreach(var dependency in Dependencies(node))
                {
                    if(_map.ContainsKey(dependency))continue;
                    MEMNodeSnapshot owner;
                    if(!_owners.TryGetValue(dependency,out owner))
                    {
                        var asset=source.Assets.FirstOrDefault(a=>a.Id==dependency);if(asset==null){_issues.Add("org.transfer.dependency_missing:"+dependency);continue;}
                        if(policy==MTIExternalPolicy.Pin){if(!TryPin(asset.Id,target))_issues.Add("org.transfer.pin_missing_or_changed:"+asset.Id);}
                        else if(policy==MTIExternalPolicy.Reject)_issues.Add("org.transfer.external_dependency:"+asset.Id);
                        else{var existing=target.Assets.FirstOrDefault(a=>a.Name==asset.Name);if(existing!=null){if(existing.ContentHash!=asset.ContentHash||existing.MediaType!=asset.MediaType)_issues.Add("org.transfer.asset_name_conflict:"+asset.Name);else _map[asset.Id]=existing.Id;}else{NewId(asset.Id);CopiedAssets.Add(asset.Id);}}
                        continue;
                    }
                    var root=source.Find(owner.StorageRootId);
                    if(root==null||root.Kind==MEMNodeKind.Project){_issues.Add("org.transfer.unresolved_owner:"+dependency);continue;}
                    if(policy==MTIExternalPolicy.Reject){_issues.Add("org.transfer.external_dependency:"+root.Id);continue;}
                    if(policy==MTIExternalPolicy.Pin){if(!TryPin(root.Id,target))_issues.Add("org.transfer.pin_missing_or_changed:"+root.Id);continue;}
                    if(AllNodes(root.Id).Any(n=>Included.Contains(n.Id))){_issues.Add("org.transfer.partial_resource_boundary: promote selection to owning resource");continue;}
                    Include(root.Id,NewId(root.Id),null);dependencies.Add(root.Id);foreach(var child in AllNodes(root.Id))pending.Enqueue(child);
                }
            }
            foreach(var typeId in source.Nodes.Where(n=>Included.Contains(n.Id)).Select(n=>n.TypeId).Distinct())
            {var descriptor=source.RegisteredTypes.FirstOrDefault(t=>t.TypeId==typeId);var existing=target.FindType(typeId);if(descriptor!=null&&existing!=null&&!MPMTypeDescriptor.SameSchema(descriptor,existing))_issues.Add("org.transfer.type_schema_conflict:"+typeId);}
            Seal();
        }
        private void Seal(){IdMap=new ReadOnlyDictionary<Guid,Guid>(_map);Issues=_issues.AsReadOnly();}
        internal void Reject(IEnumerable<string> issues){_issues.AddRange(issues);}
        public int BaseRevision {get;private set;}public int SourceRevision {get;private set;}public string SourceHash {get;private set;}public MTIExternalPolicy Policy {get;private set;}
        public IReadOnlyDictionary<Guid,Guid> IdMap {get;private set;}public IReadOnlyList<string> Issues {get;private set;}public bool IsValid {get{return _issues.Count==0;}}
        public Guid SourceProjectId {get{return Source.ProjectId;}}
        public Guid DestinationProjectId {get{return TargetProjectId;}}
        public IReadOnlyList<string> Warnings {get;private set;}
        internal void PrepareAspects(IMTIAspectRemapper remapper)
        {
            var warnings=new List<string>();
            foreach(var aspect in Source.Nodes.Where(n=>Included.Contains(n.Id)).SelectMany(n=>n.Aspects))
            {
                if(MappedAspects.ContainsKey(aspect))continue;
                if(aspect.PayloadBytes.Length==0&&!aspect.RequiresRemap){MappedAspects.Add(aspect,aspect);continue;}
                var mapped=remapper==null?new MPMAspect(aspect.Kind,aspect.ProviderId,aspect.SchemaVersion,aspect.Payload,aspect.Required,true):remapper.Remap(aspect,this);
                if(mapped==null||mapped.Kind!=aspect.Kind||mapped.ProviderId!=aspect.ProviderId||mapped.Required!=aspect.Required)throw new InvalidOperationException("org.transfer.invalid_aspect_remapper");
                MappedAspects.Add(aspect,mapped);if(mapped.RequiresRemap)warnings.Add("org.transfer.opaque_aspect_requires_remap:"+aspect.ProviderId);
            }
            Warnings=warnings.AsReadOnly();
        }
        private Guid NewId(Guid id){Guid value;if(!_map.TryGetValue(id,out value)){value=Guid.NewGuid();_map.Add(id,value);}return value;}
        private IEnumerable<MEMNodeSnapshot> AllNodes(Guid root)
        {var stack=new Stack<Guid>();stack.Push(root);while(stack.Count!=0){var node=Source.Find(stack.Pop());yield return node;for(int i=node.ChildIds.Count-1;i>=0;i--)stack.Push(node.ChildIds[i]);}}
        private void Include(Guid root,Guid newRoot,Guid? inherited)
        {
            foreach(var node in AllNodes(root))
            {
                if(!Included.Add(node.Id))continue;NewId(node.Id);NewResourceRoots[node.Id]=inherited??newRoot;
                foreach(var reference in node.References)NewId(reference.Id);foreach(var port in node.Ports)NewId(port.Id);foreach(var connection in node.Connections)NewId(connection.Id);
            }
        }
        private bool TryPin(Guid resourceId,MEMEngineeringSession target)
        {
            if(!_pinVisited.Add(resourceId))return _map.ContainsKey(resourceId);
            MEMResourceEntry original;if(!_resources.TryGetValue(resourceId,out original))return false;var current=target.GetResourceEntry(resourceId);
            if(current==null||current.ContentHash!=original.ContentHash||current.TypeId!=original.TypeId||current.SchemaVersion!=original.SchemaVersion)return false;
            var asset=Source.Assets.FirstOrDefault(a=>a.Id==resourceId);if(asset!=null){_map[asset.Id]=asset.Id;return true;}
            var nodes=AllNodes(resourceId).ToArray();foreach(var node in nodes){_map[node.Id]=node.Id;NewResourceRoots[node.Id]=node.StorageRootId;foreach(var port in node.Ports)_map[port.Id]=port.Id;foreach(var reference in node.References)_map[reference.Id]=reference.Id;foreach(var connection in node.Connections)_map[connection.Id]=connection.Id;}
            foreach(var dependency in nodes.SelectMany(Dependencies).Distinct())
            {
                if(_map.ContainsKey(dependency))continue;MEMNodeSnapshot owner;Guid root=_owners.TryGetValue(dependency,out owner)?owner.StorageRootId:dependency;
                if(!TryPin(root,target))return false;
            }
            return true;
        }
        private static IEnumerable<Guid> AddressIds(MRIObjectAddress address){yield return address.ObjectId;foreach(var id in address.InstancePath)yield return id;}
        private static IEnumerable<Guid> BindingIds(MPMBinding binding)
        {if(binding==null)yield break;if(binding.Kind==MPMBindingKind.Resource)yield return binding.ResourceId.Value;if(binding.Kind==MPMBindingKind.Member)foreach(var id in AddressIds(binding.Address))yield return id;}
        private static IEnumerable<Guid> Dependencies(MEMNodeSnapshot node)
        {
            foreach(var p in node.Properties)foreach(var id in BindingIds(p.Binding).Concat(BindingIds(p.Descriptor.DefaultBinding)))yield return id;
            if(node.Visual!=null)foreach(var b in node.Visual.Bindings.Values)foreach(var id in BindingIds(b))yield return id;
            foreach(var r in node.References){yield return r.TargetId;foreach(var b in r.Overrides.Values)foreach(var id in BindingIds(b))yield return id;}
            foreach(var port in node.Ports)if(port.Contract.Calibration!=null)foreach(var id in AddressIds(port.Contract.Calibration))yield return id;
            foreach(var c in node.Connections){yield return c.Source.PortId;yield return c.Target.PortId;foreach(var id in AddressIds(c.Source.Address).Concat(AddressIds(c.Target.Address)))yield return id;}
            foreach(var m in node.PublicMappings){foreach(var id in AddressIds(m.TargetAddress))yield return id;if(m.TargetPortId.HasValue)yield return m.TargetPortId.Value;}
        }
        internal Guid Map(Guid id){Guid value;if(!_map.TryGetValue(id,out value))throw new InvalidOperationException("org.transfer.unmapped_identity:"+id);return value;}
        internal MRIObjectAddress Map(MRIObjectAddress address)
        {
            if(address.ProjectId!=Source.ProjectId)throw new InvalidOperationException("org.transfer.external_project_address");Guid resource;
            if(!NewResourceRoots.TryGetValue(address.ObjectId,out resource))throw new InvalidOperationException("org.transfer.unmapped_resource");return new MRIObjectAddress(TargetProjectId,resource,Map(address.ObjectId),address.InstancePath.Select(Map));
        }
        internal MPMBinding Map(MPMBinding binding)
        {if(binding==null)return null;if(binding.Kind==MPMBindingKind.Member)return MPMBinding.Member(Map(binding.Address),binding.MemberId);if(binding.Kind==MPMBindingKind.Resource)return MPMBinding.Resource(Map(binding.ResourceId.Value));return binding;}
        internal MPCEndpoint Map(MPCEndpoint endpoint){return new MPCEndpoint(Map(endpoint.Address),Map(endpoint.PortId));}
        internal static MEMNode CreateNode(MEMNodeSnapshot node,Guid id)
        {
            MEMNode created;switch(node.Kind){case MEMNodeKind.Page:created=new MEMPage(id,node.Name,false);break;case MEMNodeKind.Content:created=new MEMContent(id,node.Name);break;case MEMNodeKind.Symbol:created=new MEMSymbol(id,node.Name,false);break;case MEMNodeKind.Group:created=new MRCGroup(id,node.Name);break;case MEMNodeKind.Element:created=new MRCRectangle(id,node.Name,0,0);break;default:throw new InvalidOperationException("org.transfer.unsupported_node");}
            if(created.TypeId!=node.TypeId)created.SetTypeId(node.TypeId);return created;
        }
    }
}
