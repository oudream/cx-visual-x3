using System;
using System.Collections.Generic;
using System.Linq;

namespace CxVisual.Mechanism.Core
{
    public sealed partial class MEMEngineeringSession
    {
        private MPMValueKind? MemberKind(MEMNode owner,string member)
        {
            var property=FindProperty(owner,member);if(property!=null)return property.Descriptor.ValueKind;
            if(!(owner is MRCVisualItem))return null;
            if(new[]{"visible","selectable","locked"}.Contains(member))return MPMValueKind.Boolean;
            return new[]{"x","y","width","height","rotation","scaleX","scaleY","shear"}.Contains(member)?(MPMValueKind?)MPMValueKind.Number:null;
        }
        internal void CheckBindingAssignment(MEMNode owner,string member,MPMBinding binding,MPMValueKind? expectedKind=null,MRIResourceReference instance=null)
        {
            if(binding==null)return;BindingTargets(binding).ToArray();
            var declared=expectedKind??MemberKind(owner,member);
            if(binding.Kind==MPMBindingKind.Literal&&binding.Value.Kind!=MPMValueKind.Null&&declared.HasValue&&declared.Value!=binding.Value.Kind)throw new InvalidOperationException("org.binding.type_mismatch");
            if(binding.Kind==MPMBindingKind.Resource&&declared.HasValue&&declared!=MPMValueKind.Resource)throw new InvalidOperationException("org.binding.type_mismatch");
            if(binding.Kind==MPMBindingKind.Member)
            {
                var target=Resolve(binding.Address);var property=FindProperty(target,binding.MemberId);
                var context=instance==null?owner:instance.Owner;bool publicRoot=target.Id==target.ResourceRootId||(target.Parent is MEMPage&&((MEMPage)target.Parent).RootContent==target);
                if(binding.Address.InstancePath.Count==0&&target.ResourceRootId!=context.ResourceRootId&&(!publicRoot||property==null||!property.Descriptor.IsPublic))throw new InvalidOperationException("org.binding.member_private");
                if(binding.Address.InstancePath.Count!=0)CheckAddressScope(Find(context.ResourceRootId),binding.Address);
                var actual=MemberKind(target,binding.MemberId);var expected=expectedKind??MemberKind(owner,member);
                if(actual.HasValue&&expected.HasValue&&actual!=expected)throw new InvalidOperationException("org.binding.type_mismatch");
            }
            var source=(instance==null?GetAddress(owner):new MRIObjectAddress(Project.Id,owner.ResourceRootId,owner.Id,new[]{instance.Id})).ToString()+"#"+member;var seen=new HashSet<string>(StringComparer.Ordinal);var current=binding;
            while(current!=null&&current.Kind==MPMBindingKind.Member)
            {
                var key=current.Address.ToString()+"#"+current.MemberId;if(key==source||!seen.Add(key))throw new InvalidOperationException("org.binding.member_cycle");
                var node=Resolve(current.Address);if(node==null)throw new InvalidOperationException("org.binding.member_missing");
                if(current.Address.InstancePath.Count!=0&&node.Id==FindReference(current.Address.InstancePath.Last()).TargetId)current=ResolveParameter(FindReference(current.Address.InstancePath.Last()),current.MemberId);
                else{var property=FindProperty(node,current.MemberId);current=property==null?(node as MRCVisualItem)?.GetBinding(current.MemberId):property.Binding;}
            }
        }
        internal bool HasMemberUse(Guid owner,string member)
        {
            HashSet<object> uses;if(!_declarationUses.TryGetValue(owner,out uses))return false;
            Func<MPMBinding,bool> refers=b=>b!=null&&b.Kind==MPMBindingKind.Member&&b.Address.ObjectId==owner&&b.MemberId==member;
            foreach(var record in uses)
            {
                var property=record as MPMProperty;if(property!=null&&refers(property.Binding))return true;
                var reference=record as MRIResourceReference;if(reference!=null&&reference.Overrides.Values.Any(refers))return true;
                var visual=record as Tuple<Guid,string>;if(visual!=null&&refers(((MRCVisualItem)Find(visual.Item1)).GetBinding(visual.Item2)))return true;
                var mapping=record as MRCPublicMapping;if(mapping!=null&&mapping.Kind==MRCPublicMappingKind.Property&&mapping.TargetAddress.ObjectId==owner&&mapping.TargetMemberId==member)return true;
            }
            return false;
        }
        public MPMBinding ResolveMappedBinding(MRIResourceReference reference,Guid targetId,string targetMember)
        {
            lock(_gate)
            {
                DemandReference(reference);var mappings=MPMDefinitionState.MappingsOf(Find(reference.TargetId)).Where(m=>m.Kind==MRCPublicMappingKind.Property&&m.TargetAddress.ObjectId==targetId&&m.TargetMemberId==targetMember).ToArray();
                if(mappings.Length>1||mappings.Any(m=>m.TargetAddress.InstancePath.Count!=0))throw new InvalidOperationException("org.mapping.requires_full_address");
                return mappings.Length==0?null:ResolveParameter(reference,mappings[0].MemberId);
            }
        }
        public MPMBinding ResolveMappedBinding(MRIResourceReference reference,MRIObjectAddress target,string targetMember)
        {
            lock(_gate)
            {
                DemandReference(reference);var mapping=MPMDefinitionState.MappingsOf(Find(reference.TargetId)).FirstOrDefault(m=>m.Kind==MRCPublicMappingKind.Property&&m.TargetAddress.Equals(target)&&m.TargetMemberId==targetMember);
                return mapping==null?null:ResolveParameter(reference,mapping.MemberId);
            }
        }
    }
}
