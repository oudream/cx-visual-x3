using System;
using System.Collections.Generic;
using System.Linq;

namespace CxVisual.Mechanism.Core
{
    public sealed partial class MTCEditSession
    {
        private MPMDefinitionState Definition(MEMNode owner)
        {Demand();_session.DemandNode(owner);DemandEditable(owner);return MPMDefinitionState.For(owner,true);}
        public MPMProperty DefineProperty(MEMNode owner,MPMPropertyDescriptor descriptor)
        {
            if(descriptor==null)throw new ArgumentNullException("descriptor");var state=Definition(owner);
            if(_session.FindProperty(owner,descriptor.MemberId)!=null)throw new InvalidOperationException("org.property.duplicate");
            _session.CheckBindingAssignment(owner,descriptor.MemberId,descriptor.DefaultBinding,descriptor.ValueKind);
            var property=new MPMProperty(owner.Id,descriptor);var dependencies=_session.BindingTargets(property.Binding).ToArray();
            Change(()=>{if(state.PropertyItems==null)state.PropertyItems=new List<MPMProperty>();state.PropertyItems.Add(property);_session.IndexUsage(property,dependencies);},()=>{_session.UnindexUsage(property);state.PropertyItems.Remove(property);},MTCChangeKind.Property,owner.Id);return property;
        }
        public void SetProperty(MEMNode owner,string member,MPMBinding binding)
        {
            Definition(owner);var property=_session.FindProperty(owner,member);if(property==null)throw new InvalidOperationException("org.property.missing");property.Descriptor.Validate(binding);
            var old=property.ExplicitBinding;if(object.Equals(old,binding))return;var before=_session.BindingTargets(property.Binding).ToArray();var after=_session.BindingTargets(binding??property.Descriptor.DefaultBinding).ToArray();
            _session.CheckBindingAssignment(owner,member,binding??property.Descriptor.DefaultBinding,property.Descriptor.ValueKind);
            Change(()=>{_session.UnindexUsage(property);property.ExplicitBinding=binding;_session.IndexUsage(property,after);},()=>{_session.UnindexUsage(property);property.ExplicitBinding=old;_session.IndexUsage(property,before);},MTCChangeKind.Property,owner.Id);
        }
        public void RemoveProperty(MEMNode owner,string member)
        {
            var state=Definition(owner);var property=_session.FindProperty(owner,member);if(property==null)throw new InvalidOperationException("org.property.missing");
            if(MPMDefinitionState.MappingsOf(owner).Any(m=>m.Kind==MRCPublicMappingKind.Property&&m.MemberId==member)||_session.HasMemberUse(owner.Id,member)||(property.Descriptor.IsPublic&&_session.HasUsages(owner.Id)))throw new InvalidOperationException("org.property.in_use");
            int index=state.PropertyItems.IndexOf(property);var dependencies=_session.BindingTargets(property.Binding).ToArray();
            Change(()=>{_session.UnindexUsage(property);state.PropertyItems.RemoveAt(index);},()=>{state.PropertyItems.Insert(index,property);_session.IndexUsage(property,dependencies);},MTCChangeKind.Property,owner.Id);
        }
        public void SetOverride(MRIResourceReference reference,string member,MPMBinding binding)
        {
            Demand();_session.DemandReference(reference);DemandEditable(reference.Owner);var target=_session.Find(reference.TargetId);var property=_session.FindProperty(target,member);
            if(property==null||!property.Descriptor.IsPublic)throw new InvalidOperationException("org.parameter.not_public");property.Descriptor.Validate(binding);
            _session.CheckBindingAssignment(target,member,binding??property.Binding,property.Descriptor.ValueKind,reference);
            MPMBinding old=null;if(reference.OverrideItems!=null)reference.OverrideItems.TryGetValue(member,out old);if(object.Equals(old,binding))return;
            var before=reference.Overrides.Values.SelectMany(_session.BindingTargets).ToArray();var pending=reference.Overrides.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal);if(binding==null)pending.Remove(member);else pending[member]=binding;
            var after=pending.Values.SelectMany(_session.BindingTargets).ToArray();
            Action<MPMBinding,Guid[]> assign=(value,dependencies)=>{_session.UnindexUsage(reference);if(reference.OverrideItems==null)reference.OverrideItems=new Dictionary<string,MPMBinding>(StringComparer.Ordinal);if(value==null)reference.OverrideItems.Remove(member);else reference.OverrideItems[member]=value;_session.IndexUsage(reference,dependencies);};
            Change(()=>assign(binding,after),()=>assign(old,before),MTCChangeKind.ReferenceValue,reference.Id);
        }
        public void SetPlacement(MRIResourceReference reference,MRCTransform transform)
        {
            Demand();_session.DemandReference(reference);DemandEditable(reference.Owner);double a,sx,sy,sh;transform.Decompose(out a,out sx,out sy,out sh);
            if(reference.Placement.Equals(transform))return;var old=reference.PlacementValue;var value=transform.Equals(MRCTransform.Identity)?null:new MRIPlacementState(transform);
            Change(()=>reference.PlacementValue=value,()=>reference.PlacementValue=old,MTCChangeKind.ReferenceValue,reference.Id);
        }
        public MPCPort DefinePort(MEMNode owner,string member,MPCPortDirection direction,string typeId,MPCPortKind kind=MPCPortKind.Data,MPCPortMultiplicity multiplicity=MPCPortMultiplicity.Many,MTUContractReference contract=null,bool isPublic=false,bool required=false)
        {
            var state=Definition(owner);if(MPMDefinitionState.PortsOf(owner).Any(p=>p.MemberId==member))throw new InvalidOperationException("org.port.member_duplicate");
            var port=new MPCPort(Guid.NewGuid(),owner.Id,member,direction,typeId,kind,multiplicity,contract,isPublic,required);
            if(port.Contract.Calibration!=null&&_session.Resolve(port.Contract.Calibration)==null)throw new InvalidOperationException("org.port.calibration_missing");
            Change(()=>{if(state.PortItems==null)state.PortItems=new List<MPCPort>();_session.IndexPort(port);state.PortItems.Add(port);},()=>{state.PortItems.Remove(port);_session.UnindexPort(port);},MTCChangeKind.Port,port.Id);return port;
        }
        public void RemovePort(MPCPort port)
        {
            Demand();if(port==null||_session.FindPort(port.Id)!=port)throw new InvalidOperationException("org.port.foreign_or_missing");var owner=_session.Find(port.OwnerId);var state=Definition(owner);
            if(_session.HasDeclarationUse(port.Id)||(port.IsPublic&&_session.HasUsages(owner.Id)))throw new InvalidOperationException("org.port.in_use");int index=state.PortItems.IndexOf(port);
            Change(()=>{state.PortItems.RemoveAt(index);_session.UnindexPort(port);},()=>{_session.IndexPort(port);state.PortItems.Insert(index,port);},MTCChangeKind.Port,port.Id);
        }
        public MPCConnection Connect(MEMNode owner,MPCEndpoint source,MPCEndpoint target)
        {
            var state=Definition(owner);_session.CheckConnection(owner,source,target);var connection=new MPCConnection(Guid.NewGuid(),owner.Id,source,target);
            Change(()=>{if(state.ConnectionItems==null)state.ConnectionItems=new List<MPCConnection>();_session.IndexConnection(connection);state.ConnectionItems.Add(connection);},()=>{state.ConnectionItems.Remove(connection);_session.UnindexConnection(connection);},MTCChangeKind.Connection,connection.Id);return connection;
        }
        public void Disconnect(MPCConnection connection)
        {
            Demand();if(connection==null||_session.FindConnection(connection.Id)!=connection)throw new InvalidOperationException("org.connection.foreign_or_missing");var state=Definition(_session.Find(connection.OwnerId));int index=state.ConnectionItems.IndexOf(connection);
            Change(()=>{_session.UnindexConnection(connection);state.ConnectionItems.RemoveAt(index);},()=>{_session.IndexConnection(connection);state.ConnectionItems.Insert(index,connection);},MTCChangeKind.Connection,connection.Id);
        }
        public MPCPort ExposePort(MEMNode definition,string member,MPCEndpoint target)
        {
            var state=Definition(definition);_session.CheckEndpointScope(definition,target);var inner=_session.ResolvePort(target);
            if(inner.OwnerId==definition.Id)throw new InvalidOperationException("org.mapping.requires_owned_target");
            if(MPMDefinitionState.MappingsOf(definition).Any(m=>m.Kind==MRCPublicMappingKind.Port&&m.MemberId==member))throw new InvalidOperationException("org.mapping.duplicate");
            if(inner.Multiplicity==MPCPortMultiplicity.One&&((inner.Direction!=MPCPortDirection.Output&&_session.EndpointOccupied(target,true))||(inner.Direction!=MPCPortDirection.Input&&_session.EndpointOccupied(target,false))))throw new InvalidOperationException("org.mapping.port_already_connected");
            var port=DefinePort(definition,member,inner.Direction,inner.TypeId,inner.Kind,inner.Multiplicity,inner.Contract,true,inner.Required);var mapping=new MRCPublicMapping(definition.Id,member,port.Id,target);
            Change(()=>{if(state.MappingItems==null)state.MappingItems=new List<MRCPublicMapping>();state.MappingItems.Add(mapping);_session.IndexMapping(mapping);},()=>{_session.UnindexUsage(mapping);state.MappingItems.Remove(mapping);},MTCChangeKind.Mapping,definition.Id);return port;
        }
        public void ExposeProperty(MEMNode definition,string member,MRIObjectAddress target,string targetMember)
        {
            var state=Definition(definition);var property=_session.FindProperty(definition,member);var node=_session.Resolve(target);
            if(property==null||!property.Descriptor.IsPublic||node==null||node==definition||!MEMEngineeringSession.MemberExists(node,targetMember))throw new InvalidOperationException("org.mapping.property_target");_session.CheckAddressScope(definition,target);
            _session.BindingTargets(MPMBinding.Member(target,targetMember)).ToArray();
            if(MPMDefinitionState.MappingsOf(definition).Any(m=>m.Kind==MRCPublicMappingKind.Property&&m.MemberId==member))throw new InvalidOperationException("org.mapping.duplicate");
            var targetProperty=_session.FindProperty(node,targetMember);
            var expected=targetProperty==null?(new[]{"visible","selectable","locked"}.Contains(targetMember)?MPMValueKind.Boolean:MPMValueKind.Number):targetProperty.Descriptor.ValueKind;
            if(expected!=property.Descriptor.ValueKind)throw new InvalidOperationException("org.mapping.property_type");
            var mapping=new MRCPublicMapping(definition.Id,member,target,targetMember);
            Change(()=>{if(state.MappingItems==null)state.MappingItems=new List<MRCPublicMapping>();state.MappingItems.Add(mapping);_session.IndexMapping(mapping);},()=>{_session.UnindexUsage(mapping);state.MappingItems.Remove(mapping);},MTCChangeKind.Mapping,definition.Id);
        }
        private static bool WithinDefinition(MEMNode node,MEMNode definition){for(var n=node;n!=null;n=n.Parent)if(n==definition)return true;return false;}
        public void Unexpose(MEMNode definition,string member,MRCPublicMappingKind kind)
        {
            var state=Definition(definition);var mapping=MPMDefinitionState.MappingsOf(definition).FirstOrDefault(m=>m.Kind==kind&&m.MemberId==member);
            if(mapping==null)throw new InvalidOperationException("org.mapping.missing");if(_session.HasUsages(definition.Id))throw new InvalidOperationException("org.mapping.in_use");
            if(kind==MRCPublicMappingKind.Port&&_session.HasOtherDeclarationUse(mapping.PublicPortId.Value,mapping))throw new InvalidOperationException("org.mapping.in_use");
            int index=state.MappingItems.IndexOf(mapping);Change(()=>{_session.UnindexUsage(mapping);state.MappingItems.RemoveAt(index);},()=>{state.MappingItems.Insert(index,mapping);_session.IndexMapping(mapping);},MTCChangeKind.Mapping,definition.Id);
            if(kind==MRCPublicMappingKind.Port)RemovePort(_session.FindPort(mapping.PublicPortId.Value));
        }
        public void SetAspect(MEMNode owner,MPMAspect aspect)
        {
            if(aspect==null)throw new ArgumentNullException("aspect");var state=Definition(owner);var old=MPMDefinitionState.AspectsOf(owner).FirstOrDefault(a=>a.Kind==aspect.Kind&&a.ProviderId==aspect.ProviderId);
            if(old!=null&&old.Required==aspect.Required&&old.SchemaVersion==aspect.SchemaVersion&&old.PayloadBytes.SequenceEqual(aspect.PayloadBytes))return;
            int index=old==null?-1:state.AspectItems.IndexOf(old);
            Change(()=>{if(state.AspectItems==null)state.AspectItems=new List<MPMAspect>();if(index<0)state.AspectItems.Add(aspect);else state.AspectItems[index]=aspect;},()=>{if(index<0)state.AspectItems.Remove(aspect);else state.AspectItems[index]=old;},MTCChangeKind.Aspect,owner.Id);
        }
    }
}
