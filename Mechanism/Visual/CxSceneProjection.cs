using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CxVisual.Foundation.Geometry;
using CxVisual.Mechanism.Core;
namespace CxVisual.Mechanism.Visual
{
    public static class CxSceneProjection
    {
        public static ReadOnlyCollection<CxSceneItem> Project(MEMEngineeringSession session){return Project(session,null);}
        public static ReadOnlyCollection<CxSceneItem> Project(MEMEngineeringSession session,Guid? pageId)
        {
            if(session==null)throw new ArgumentNullException("session");var snapshot=session.GetSnapshot();var projection=new Projection(snapshot);
            foreach(var page in snapshot.Nodes.Where(n=>n.Kind==MEMNodeKind.Page&&(!pageId.HasValue||n.Id==pageId.Value)))projection.Collect(page,MRCTransform.Identity,new Guid[0],false,null,MRCTransform.Identity);
            return projection.Items.AsReadOnly();
        }
        public static Guid? HitTest(IList<CxSceneItem> items, FGEPoint2D point)
        {
            if (items == null) return null;
            for (int i = items.Count - 1; i >= 0; i--)
                if (items[i].Contains(point.X, point.Y)) return items[i].Id;
            return null;
        }
        public static ReadOnlyCollection<CxSceneItem> ProjectResource(MEMEngineeringSession session,Guid resourceId)
        {
            var snapshot=session.GetSnapshot(resourceId,true);var root=snapshot.Find(resourceId);if(root==null)throw new InvalidOperationException("Missing projection root");var projection=new Projection(snapshot);
            projection.Collect(root,MRCTransform.Identity,new Guid[0],false,null,MRCTransform.Identity);return projection.Items.AsReadOnly();
        }
        private sealed class Projection
        {
            internal readonly List<CxSceneItem> Items=new List<CxSceneItem>();
            private readonly MEMProjectSnapshot _snapshot;
            private readonly Dictionary<Guid,MRIReferenceSnapshot> _refs;
            private int _expanded;
            internal Projection(MEMProjectSnapshot snapshot){_snapshot=snapshot;_refs=snapshot.Nodes.SelectMany(n=>n.References).ToDictionary(r=>r.Id);}
            private MPMBinding Parameter(MRIReferenceSnapshot reference,string member)
            {MPMBinding value;if(reference.Overrides.TryGetValue(member,out value))return value;var property=_snapshot.Find(reference.TargetId).Properties.FirstOrDefault(p=>p.Descriptor.MemberId==member);return property==null?null:property.Binding;}
            private MPMValue Literal(MPMBinding binding,Guid[] path,HashSet<string> seen)
            {
                if(binding==null)return null;if(binding.Kind==MPMBindingKind.Literal)return binding.Value;if(binding.Kind!=MPMBindingKind.Member)return null;
                string key=binding.Address+"#"+binding.MemberId;if(!seen.Add(key))return null;var node=_snapshot.Find(binding.Address.ObjectId);if(node==null)return null;
                for(int i=path.Length-1;i>=0;i--)if(_refs[path[i]].TargetId==node.Id)return Literal(Parameter(_refs[path[i]],binding.MemberId),path,seen);
                var property=node.Properties.FirstOrDefault(p=>p.Descriptor.MemberId==binding.MemberId);return Literal(property==null?null:property.Binding,path,seen);
            }
            private MPMValue Value(MEMNodeSnapshot node,string member,Guid[] path)
            {
                for(int i=0;i<path.Length;i++)
                {
                    var reference=_refs[path[i]];var definition=_snapshot.Find(reference.TargetId);
                    var mapping=definition.PublicMappings.FirstOrDefault(m=>m.Kind==MRCPublicMappingKind.Property&&m.TargetAddress.ObjectId==node.Id&&m.TargetMemberId==member&&m.TargetAddress.InstancePath.SequenceEqual(path.Skip(i+1)));
                    if(mapping!=null)return Literal(Parameter(reference,mapping.MemberId),path,new HashSet<string>(StringComparer.Ordinal));
                }
                MPMBinding binding;if(node.Visual!=null&&node.Visual.Bindings.TryGetValue(member,out binding))return Literal(binding,path,new HashSet<string>(StringComparer.Ordinal));return null;
            }
            private double Number(MEMNodeSnapshot node,string member,double fallback,Guid[] path){var value=Value(node,member,path);return value!=null&&value.Kind==MPMValueKind.Number?value.AsNumber():fallback;}
            private bool Boolean(MEMNodeSnapshot node,string member,bool fallback,Guid[] path){var value=Value(node,member,path);return value!=null&&value.Kind==MPMValueKind.Boolean?value.AsBoolean():fallback;}
            internal void Collect(MEMNodeSnapshot node,MRCTransform parent,Guid[] path,bool locked,MRIReferenceSnapshot first,MRCTransform firstParent,int depth=0)
            {
                if(++_expanded>2000000||depth>=256)throw new InvalidOperationException("visual.instance_expansion_budget");
                var world=parent;var v=node.Visual;double width=0,height=0,x=0,y=0;
                if(v!=null)
                {
                    if(!Boolean(node,"visible",v.IsVisible,path))return;locked|=Boolean(node,"locked",v.IsLocked,path);
                    x=Number(node,"x",v.X,path);y=Number(node,"y",v.Y,path);width=Number(node,"width",v.Width,path);height=Number(node,"height",v.Height,path);
                    if(width<=0||height<=0)throw new InvalidOperationException("visual.invalid_effective_size");
                    double angle=Number(node,"rotation",v.Rotation,path)*Math.PI/180,c=Math.Cos(angle),s=Math.Sin(angle),sx=Number(node,"scaleX",v.ScaleX,path),sy=Number(node,"scaleY",v.ScaleY,path),shear=Number(node,"shear",v.Shear,path);
                    if(node.Kind==MEMNodeKind.Group){sx*=width/v.Width;shear*=height/v.Height;sy*=height/v.Height;}
                    world=parent.Compose(new MRCTransform(c*sx,c*shear-s*sy,s*sx,s*shear+c*sy,x,y));
                    if(node.Kind==MEMNodeKind.Element)
                    {
                        var address=new MRIObjectAddress(_snapshot.ProjectId,node.StorageRootId,node.Id,path);bool selectable=Boolean(node,"selectable",v.IsSelectable,path)&&!locked;
                        Items.Add(new CxSceneItem(first==null?node.Id:first.Id,first==null?node.Name:first.Name+" / "+node.Name,address,world,width,height,selectable,first==null?parent:firstParent,first==null?x:first.Placement.X,first==null?y:first.Placement.Y));
                    }
                }
                foreach(var id in node.ChildIds)Collect(_snapshot.Find(id),world,path,locked,first,firstParent,depth+1);
                foreach(var reference in node.References)
                {var next=path.Concat(new[]{reference.Id}).ToArray();Collect(_snapshot.Find(reference.TargetId),world.Compose(reference.Placement),next,locked,first??reference,first==null?world:firstParent,depth+1);}
            }
        }
    }
}
