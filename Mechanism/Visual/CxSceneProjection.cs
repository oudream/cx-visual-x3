using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CxVisual.Foundation.Geometry;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Data.RuntimeData;
namespace CxVisual.Mechanism.Visual
{
    public static class CxSceneProjection
    {
        public static ReadOnlyCollection<CxSceneItem> Project(MEMEngineeringSession session){return Project(session,null);}
        public static ReadOnlyCollection<CxSceneItem> Project(MEMEngineeringSession session,Guid? pageId)
        {return ProjectCore(session,pageId,null,0,false);}
        /// <summary>可注入既有MDS/专业数据源；时间采样不修改工程Revision。</summary>
        public static ReadOnlyCollection<CxSceneItem> Project(MEMEngineeringSession session,Guid? pageId,
            IMDSExternalBindingResolver runtimeSource,double elapsedMilliseconds=0)
        {return ProjectCore(session,pageId,runtimeSource,elapsedMilliseconds,true);}
        private static ReadOnlyCollection<CxSceneItem> ProjectCore(MEMEngineeringSession session,
            Guid? pageId,IMDSExternalBindingResolver runtimeSource,double elapsedMilliseconds,bool runtimeEnabled)
        {
            if(session==null)throw new ArgumentNullException("session");var snapshot=session.GetSnapshot();var projection=new Projection(snapshot,runtimeSource,elapsedMilliseconds,runtimeEnabled);
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
            return ProjectResourceCore(session,resourceId,null,0,false);
        }
        public static ReadOnlyCollection<CxSceneItem> ProjectResource(MEMEngineeringSession session,Guid resourceId,
            IMDSExternalBindingResolver runtimeSource,double elapsedMilliseconds=0)
        {return ProjectResourceCore(session,resourceId,runtimeSource,elapsedMilliseconds,true);}
        private static ReadOnlyCollection<CxSceneItem> ProjectResourceCore(
            MEMEngineeringSession session,Guid resourceId,IMDSExternalBindingResolver runtimeSource,
            double elapsedMilliseconds,bool runtimeEnabled)
        {
            var snapshot=session.GetSnapshot(resourceId,true);var root=snapshot.Find(resourceId);if(root==null)throw new InvalidOperationException("Missing projection root");var projection=new Projection(snapshot,runtimeSource,elapsedMilliseconds,runtimeEnabled);
            projection.Collect(root,MRCTransform.Identity,new Guid[0],false,null,MRCTransform.Identity);return projection.Items.AsReadOnly();
        }
        private sealed class Projection
        {
            internal readonly List<CxSceneItem> Items=new List<CxSceneItem>();
            private readonly MEMProjectSnapshot _snapshot;
            private readonly Dictionary<Guid,MRIReferenceSnapshot> _refs;
            private readonly IMDSExternalBindingResolver _runtimeSource;
            private readonly double _elapsedMilliseconds;
            private readonly bool _runtimeEnabled;
            private int _expanded;
            internal Projection(MEMProjectSnapshot snapshot,IMDSExternalBindingResolver runtimeSource,
                double elapsedMilliseconds,bool runtimeEnabled)
            {
                if(double.IsNaN(elapsedMilliseconds)||double.IsInfinity(elapsedMilliseconds)||
                    elapsedMilliseconds<0)throw new ArgumentOutOfRangeException("elapsedMilliseconds");
                _snapshot=snapshot;_runtimeSource=runtimeSource;_elapsedMilliseconds=elapsedMilliseconds;
                _runtimeEnabled=runtimeEnabled;
                _refs=snapshot.Nodes.SelectMany(n=>n.References).ToDictionary(r=>r.Id);
            }
            private MPMBinding Parameter(MRIReferenceSnapshot reference,string member)
            {MPMBinding value;if(reference.Overrides.TryGetValue(member,out value))return value;var property=_snapshot.Find(reference.TargetId).Properties.FirstOrDefault(p=>p.Descriptor.MemberId==member);return property==null?null:property.Binding;}
            private MPMValue Literal(MPMBinding binding,Guid[] path,HashSet<string> seen,MPMValueKind target)
            {
                if(binding==null)return null;
                if(binding.Kind==MPMBindingKind.Literal)return binding.Value;
                if(binding.Kind==MPMBindingKind.Variable||binding.Kind==MPMBindingKind.Expression)
                    return MVIVisualRuntimeEvaluator.Resolve(binding,target,_runtimeSource);
                if(binding.Kind!=MPMBindingKind.Member)return null;
                string key=binding.Address+"#"+binding.MemberId;if(!seen.Add(key))return null;var node=_snapshot.Find(binding.Address.ObjectId);if(node==null)return null;
                for(int i=path.Length-1;i>=0;i--)if(_refs[path[i]].TargetId==node.Id)return Literal(Parameter(_refs[path[i]],binding.MemberId),path,seen,target);
                var property=node.Properties.FirstOrDefault(p=>p.Descriptor.MemberId==binding.MemberId);return Literal(property==null?null:property.Binding,path,seen,target);
            }
            private MPMValue Value(MEMNodeSnapshot node,string member,Guid[] path,MPMValueKind target)
            {
                for(int i=0;i<path.Length;i++)
                {
                    var reference=_refs[path[i]];var definition=_snapshot.Find(reference.TargetId);
                    var mapping=definition.PublicMappings.FirstOrDefault(m=>m.Kind==MRCPublicMappingKind.Property&&m.TargetAddress.ObjectId==node.Id&&m.TargetMemberId==member&&m.TargetAddress.InstancePath.SequenceEqual(path.Skip(i+1)));
                    if(mapping!=null)return Literal(Parameter(reference,mapping.MemberId),path,new HashSet<string>(StringComparer.Ordinal),target);
                }
                MPMBinding binding;if(node.Visual!=null&&node.Visual.Bindings.TryGetValue(member,out binding))return Literal(binding,path,new HashSet<string>(StringComparer.Ordinal),target);return null;
            }
            private double Number(MEMNodeSnapshot node,string member,double fallback,Guid[] path){var value=Value(node,member,path,MPMValueKind.Number);return value!=null&&value.Kind==MPMValueKind.Number?value.AsNumber():fallback;}
            private bool Boolean(MEMNodeSnapshot node,string member,bool fallback,Guid[] path){var value=Value(node,member,path,MPMValueKind.Boolean);return value!=null&&value.Kind==MPMValueKind.Boolean?value.AsBoolean():fallback;}
            internal void Collect(MEMNodeSnapshot node,MRCTransform parent,Guid[] path,bool locked,MRIReferenceSnapshot first,MRCTransform firstParent,int depth=0)
            {
                if(++_expanded>2000000||depth>=256)throw new InvalidOperationException("visual.instance_expansion_budget");
                var world=parent;var v=node.Visual;double width=0,height=0,x=0,y=0;
                if(v!=null)
                {
                    // 显式隐藏是强约束；动态绑定不得把已隐藏的Group/Element重新投影。
                    if(!v.IsVisible||!Boolean(node,"visible",true,path))return;locked|=Boolean(node,"locked",v.IsLocked,path);
                    x=Number(node,"x",v.X,path);y=Number(node,"y",v.Y,path);width=Number(node,"width",v.Width,path);height=Number(node,"height",v.Height,path);
                    if(width<=0||height<=0)throw new InvalidOperationException("visual.invalid_effective_size");
                    double angle=Number(node,"rotation",v.Rotation,path)*Math.PI/180,c=Math.Cos(angle),s=Math.Sin(angle),sx=Number(node,"scaleX",v.ScaleX,path),sy=Number(node,"scaleY",v.ScaleY,path),shear=Number(node,"shear",v.Shear,path);
                    if(node.Kind==MEMNodeKind.Group){sx*=width/v.Width;shear*=height/v.Height;sy*=height/v.Height;}
                    world=parent.Compose(new MRCTransform(c*sx,c*shear-s*sy,s*sx,s*shear+c*sy,x,y));
                    if(node.Kind==MEMNodeKind.Element)
                    {
                        var address=new MRIObjectAddress(_snapshot.ProjectId,node.StorageRootId,node.Id,path);bool selectable=Boolean(node,"selectable",v.IsSelectable,path)&&!locked;
                        MRCShapeDefinition shape=null;
                        if(node.TypeId=="core.shape")
                        {
                            var definition=node.Aspects.FirstOrDefault(a=>
                                a.Kind==MPMAspectKind.Visual&&a.ProviderId==MRCShapeDefinition.ProviderId);
                            if(definition==null)throw new InvalidOperationException("visual.shape.missing_definition");
                            shape=MRCShapeDefinition.FromAspect(definition);
                        }
                        string displayText=node.TypeId=="core.text"?
                            MRCTextElement.TextFromAspects(node.Aspects):"";
                        MRCVisualStyle style=MRCVisualAppearance.StyleFromAspects(node.Aspects);
                        if(_runtimeEnabled)
                            style=MVIVisualRuntimeEvaluator.EvaluateStyle(style,
                                MRCVisualAppearance.BehaviorFromAspects(node.Aspects),
                                _elapsedMilliseconds,_runtimeSource);
                        Guid? assetId=null;
                        string mediaType=null;
                        if(node.TypeId=="core.image"||node.TypeId=="core.svg")
                        {
                            MPMBinding sourceBinding;
                            if(node.Visual.Bindings.TryGetValue("source",out sourceBinding)&&
                                sourceBinding!=null&&sourceBinding.Kind==MPMBindingKind.Resource&&
                                sourceBinding.ResourceId.HasValue)
                            {
                                assetId=sourceBinding.ResourceId.Value;
                                MEMAssetReference asset=_snapshot.Assets.FirstOrDefault(a=>a.Id==assetId.Value);
                                if(asset!=null)mediaType=asset.MediaType;
                            }
                        }
                        MPMValue runtimeValue=null;
                        if(_runtimeEnabled&&(node.TypeId=="core.widget"||
                            node.TypeId.EndsWith(".textbox",StringComparison.OrdinalIgnoreCase)||
                            node.TypeId.EndsWith(".checkbox",StringComparison.OrdinalIgnoreCase)||
                            node.TypeId.EndsWith(".slider",StringComparison.OrdinalIgnoreCase)||
                            node.TypeId.EndsWith(".progressbar",StringComparison.OrdinalIgnoreCase)||
                            node.TypeId.EndsWith(".combobox",StringComparison.OrdinalIgnoreCase)))
                        {
                            MPMValueKind valueKind=node.TypeId.EndsWith(".checkbox",
                                StringComparison.OrdinalIgnoreCase)?MPMValueKind.Boolean:
                                (node.TypeId.EndsWith(".slider",StringComparison.OrdinalIgnoreCase)||
                                node.TypeId.EndsWith(".progressbar",StringComparison.OrdinalIgnoreCase))?
                                MPMValueKind.Number:MPMValueKind.String;
                            runtimeValue=Value(node,"value",path,valueKind);
                        }
                        Items.Add(new CxSceneItem(first==null?node.Id:first.Id,
                            first==null?node.Name:first.Name+" / "+node.Name,address,world,
                            width,height,selectable,first==null?parent:firstParent,
                            first==null?x:first.Placement.X,first==null?y:first.Placement.Y,
                            node.TypeId,shape,style,displayText,assetId,mediaType,runtimeValue));
                    }
                }
                foreach(var id in node.ChildIds)Collect(_snapshot.Find(id),world,path,locked,first,firstParent,depth+1);
                foreach(var reference in node.References)
                {var next=path.Concat(new[]{reference.Id}).ToArray();Collect(_snapshot.Find(reference.TargetId),world.Compose(reference.Placement),next,locked,first??reference,first==null?world:firstParent,depth+1);}
            }
        }
    }
}
