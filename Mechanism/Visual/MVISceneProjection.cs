using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CxVisual.Foundation.Geometry;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Data.RuntimeData;
namespace CxVisual.Mechanism.Visual
{
    public static class MVISceneProjection
    {
        private static bool IsWidgetType(string type)
        {
            return type=="core.widget"||type.EndsWith(".button",StringComparison.OrdinalIgnoreCase)||
                type.EndsWith(".textbox",StringComparison.OrdinalIgnoreCase)||
                type.EndsWith(".checkbox",StringComparison.OrdinalIgnoreCase)||
                type.EndsWith(".slider",StringComparison.OrdinalIgnoreCase)||
                type.EndsWith(".progressbar",StringComparison.OrdinalIgnoreCase)||
                type.EndsWith(".combobox",StringComparison.OrdinalIgnoreCase);
        }
        public static ReadOnlyCollection<MVISceneItem> Project(MEMEngineeringSession session){return Project(session,null);}
        public static ReadOnlyCollection<MVISceneItem> Project(MEMEngineeringSession session,Guid? pageId)
        {return ProjectCore(session,pageId,null,0,false);}
        /// <summary>可注入既有MDS/专业数据源；时间采样不修改工程Revision。</summary>
        public static ReadOnlyCollection<MVISceneItem> Project(MEMEngineeringSession session,Guid? pageId,
            IMDSExternalBindingResolver runtimeSource,double elapsedMilliseconds=0)
        {return ProjectCore(session,pageId,runtimeSource,elapsedMilliseconds,true);}
        private static ReadOnlyCollection<MVISceneItem> ProjectCore(MEMEngineeringSession session,
            Guid? pageId,IMDSExternalBindingResolver runtimeSource,double elapsedMilliseconds,bool runtimeEnabled)
        {
            if(session==null)throw new ArgumentNullException("session");var snapshot=session.GetSnapshot();var projection=new Projection(snapshot,runtimeSource,elapsedMilliseconds,runtimeEnabled);
            foreach(var page in snapshot.Nodes.Where(n=>n.Kind==MEMNodeKind.Page&&(!pageId.HasValue||n.Id==pageId.Value)))projection.Collect(page,MRCTransform.Identity,new Guid[0],false,null,MRCTransform.Identity);
            projection.AddVisualConnections();
            return projection.Items.AsReadOnly();
        }
        public static Guid? HitTest(IList<MVISceneItem> items, FGEPoint2D point)
        {
            if (items == null) return null;
            for (int i = items.Count - 1; i >= 0; i--)
                if (items[i].Contains(point.X, point.Y)) return items[i].Id;
            return null;
        }
        public static ReadOnlyCollection<MVISceneItem> ProjectResource(MEMEngineeringSession session,Guid resourceId)
        {
            return ProjectResourceCore(session,resourceId,null,0,false);
        }
        public static ReadOnlyCollection<MVISceneItem> ProjectResource(MEMEngineeringSession session,Guid resourceId,
            IMDSExternalBindingResolver runtimeSource,double elapsedMilliseconds=0)
        {return ProjectResourceCore(session,resourceId,runtimeSource,elapsedMilliseconds,true);}
        private static ReadOnlyCollection<MVISceneItem> ProjectResourceCore(
            MEMEngineeringSession session,Guid resourceId,IMDSExternalBindingResolver runtimeSource,
            double elapsedMilliseconds,bool runtimeEnabled)
        {
            var snapshot=session.GetSnapshot(resourceId,true);var root=snapshot.Find(resourceId);if(root==null)throw new InvalidOperationException("Missing projection root");var projection=new Projection(snapshot,runtimeSource,elapsedMilliseconds,runtimeEnabled);
            projection.Collect(root,MRCTransform.Identity,new Guid[0],false,null,MRCTransform.Identity);
            projection.AddVisualConnections();
            return projection.Items.AsReadOnly();
        }
        private sealed class Projection
        {
            internal readonly List<MVISceneItem> Items=new List<MVISceneItem>();
            private readonly MEMProjectSnapshot _snapshot;
            private readonly Dictionary<Guid,MRIReferenceSnapshot> _refs;
            private readonly IMDSExternalBindingResolver _runtimeSource;
            private readonly double _elapsedMilliseconds;
            private readonly bool _runtimeEnabled;
            private int _expanded;
            /// <summary>投影现有MPC Visual连接；边不另存几何，随两端图元变换更新。</summary>
            internal void AddVisualConnections()
            {
                var links=new List<MVISceneItem>();
                foreach(MEMNodeSnapshot owner in _snapshot.Nodes)
                foreach(MPCConnection connection in owner.Connections)
                {
                    // 本阶段只处理同一编辑定义内的直接图元。实例连接走公共端口映射。
                    if(connection.Source.Address.InstancePath.Count!=0||
                        connection.Target.Address.InstancePath.Count!=0)continue;
                    MEMNodeSnapshot first=_snapshot.Find(connection.Source.Address.ObjectId);
                    MEMNodeSnapshot last=_snapshot.Find(connection.Target.Address.ObjectId);
                    if(first==null||last==null)continue;
                    MPCPort a=first.Ports.FirstOrDefault(p=>p.Id==connection.Source.PortId);
                    MPCPort b=last.Ports.FirstOrDefault(p=>p.Id==connection.Target.PortId);
                    if(a==null||b==null||a.Kind!=MPCPortKind.Visual||
                        b.Kind!=MPCPortKind.Visual||
                        !MVIVisualAnchors.IsVisualMember(a.MemberId)||
                        !MVIVisualAnchors.IsVisualMember(b.MemberId))continue;
                    MVISceneItem source=Items.FirstOrDefault(i=>i.Address!=null&&
                        i.Address.ObjectId==first.Id&&i.Address.InstancePath.Count==0);
                    MVISceneItem target=Items.FirstOrDefault(i=>i.Address!=null&&
                        i.Address.ObjectId==last.Id&&i.Address.InstancePath.Count==0);
                    if(source==null||target==null)continue;
                    // MPC只保存固定Port身份：每一帧从图元仿射矩阵反算锚点和外法线。
                    string sourceSide=a.MemberId.Substring("visual.".Length);
                    string targetSide=b.MemberId.Substring("visual.".Length);
                    FGEPoint2D start=MVIVisualAnchors.At(source,sourceSide);
                    FGEPoint2D end=MVIVisualAnchors.At(target,targetSide);
                    // 自动路由根据当前视图中真实图元的AABB计算，不保存缓存折线。
                    // Group只是容器，不以容器外框作为实体障碍；其他资源不跨页相互阻挡。
                    var obstacles=Items.Where(item=>
                        item.Id!=source.Id&&item.Id!=target.Id&&
                        item.Address!=null&&source.Address!=null&&
                        item.Address.ResourceId==source.Address.ResourceId&&
                        item.ElementTypeId!="core.visual.connector"&&
                        (item.Shape!=null||item.ElementTypeId=="core.rectangle"||
                         item.ElementTypeId=="core.text"||
                         item.ElementTypeId=="core.image"||item.ElementTypeId=="core.svg"||
                         IsWidgetType(item.ElementTypeId))&&
                        item.Bounds.Width>0&&item.Bounds.Height>0)
                        .Select(item=>item.Bounds);
                    // 自动正交线须遵守起点向外出线、终点沿外法线方向进入；
                    // 自动避障在MVI场景计算，绝不写回MPC连接或覆盖手动折点。
                    IReadOnlyList<FGEPoint2D> points=MVIVisualRouting.Points(
                        connection,start,end,obstacles,source,sourceSide,target,targetSide);
                    double x=points.Min(p=>p.X),y=points.Min(p=>p.Y);
                    var line=MRCShapeDefinition.Polyline(points.Select(p=>
                        new FGEPoint2D(p.X-x,p.Y-y)));
                    double width=points.Max(p=>p.X)-x;
                    double height=points.Max(p=>p.Y)-y;
                    var transform=new MRCTransform(1,0,0,1,x,y);
                    var style=new MRCVisualStyle(MRCBrushDefinition.None(),
                        MRCBrushDefinition.Solid(MRCColor.FromRgb(55,122,199)),1.7);
                    links.Add(new MVISceneItem(connection.Id,"Visual connector",null,
                        transform,width,height,true,MRCTransform.Identity,
                        x,y,"core.visual.connector",line,style));
                }
                Items.InsertRange(0,links);
            }

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
            private double Animated(MEMNodeSnapshot node,string member,double fallback,
                Guid[] path,MRCVisualBehaviorDefinition behavior)
            {
                double value=Number(node,member,fallback,path);
                return _runtimeEnabled?MVIVisualRuntimeEvaluator.EvaluateNumber(
                    member,value,behavior,_elapsedMilliseconds,_runtimeSource):value;
            }
            private bool Boolean(MEMNodeSnapshot node,string member,bool fallback,Guid[] path){var value=Value(node,member,path,MPMValueKind.Boolean);return value!=null&&value.Kind==MPMValueKind.Boolean?value.AsBoolean():fallback;}
            internal void Collect(MEMNodeSnapshot node,MRCTransform parent,Guid[] path,bool locked,MRIReferenceSnapshot first,MRCTransform firstParent,int depth=0)
            {
                if(++_expanded>2000000||depth>=256)throw new InvalidOperationException("visual.instance_expansion_budget");
                var world=parent;var v=node.Visual;double width=0,height=0,x=0,y=0;
                if(v!=null)
                {
                    // 显式隐藏是强约束；动态绑定不得把已隐藏的Group/Element重新投影。
                    if(!v.IsVisible||!Boolean(node,"visible",true,path))return;locked|=Boolean(node,"locked",v.IsLocked,path);
                    MRCVisualBehaviorDefinition behavior=_runtimeEnabled?
                        MRCVisualAppearance.BehaviorFromAspects(node.Aspects):null;
                    x=Animated(node,"x",v.X,path,behavior);
                    y=Animated(node,"y",v.Y,path,behavior);
                    width=Animated(node,"width",v.Width,path,behavior);
                    height=Animated(node,"height",v.Height,path,behavior);
                    if(width<=0||height<=0)throw new InvalidOperationException("visual.invalid_effective_size");
                    double angle=Animated(node,"rotation",v.Rotation,path,behavior)*Math.PI/180;
                    double c=Math.Cos(angle),s=Math.Sin(angle);
                    double sx=Animated(node,"scaleX",v.ScaleX,path,behavior),
                        sy=Animated(node,"scaleY",v.ScaleY,path,behavior),
                        shear=Animated(node,"shear",v.Shear,path,behavior);
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
                                behavior,_elapsedMilliseconds,_runtimeSource);
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
                        var widgetSettings=new Dictionary<string,MPMValue>(StringComparer.Ordinal);
                        bool widget=IsWidgetType(node.TypeId);
                        if(widget)
                        {
                            string[] textSettings={"content","placeholder","options","tooltip"};
                            foreach(string member in textSettings)
                            {
                                MPMValue value=Value(node,member,path,MPMValueKind.String);
                                if(value!=null)
                                {
                                    if(value.Kind!=MPMValueKind.String)
                                        throw new InvalidOperationException("visual.widget.string_required:"+member);
                                    widgetSettings.Add(member,value);
                                }
                            }
                            string[] numberSettings={"minimum","maximum"};
                            foreach(string member in numberSettings)
                            {
                                MPMValue value=Value(node,member,path,MPMValueKind.Number);
                                if(value!=null)
                                {
                                    if(value.Kind!=MPMValueKind.Number)
                                        throw new InvalidOperationException("visual.widget.number_required:"+member);
                                    widgetSettings.Add(member,value);
                                }
                            }
                            MPMValue enabled=Value(node,"enabled",path,MPMValueKind.Boolean);
                            if(enabled!=null)
                            {
                                if(enabled.Kind!=MPMValueKind.Boolean)
                                    throw new InvalidOperationException("visual.widget.boolean_required:enabled");
                                widgetSettings.Add("enabled",enabled);
                            }
                            MPMValueKind kind=node.TypeId.EndsWith(".checkbox",
                                StringComparison.OrdinalIgnoreCase)?MPMValueKind.Boolean:
                                (node.TypeId.EndsWith(".slider",StringComparison.OrdinalIgnoreCase)||
                                node.TypeId.EndsWith(".progressbar",StringComparison.OrdinalIgnoreCase))?
                                MPMValueKind.Number:MPMValueKind.String;
                            runtimeValue=Value(node,"value",path,kind);
                            if(runtimeValue!=null&&runtimeValue.Kind!=kind)
                                throw new InvalidOperationException("visual.widget.value_kind_mismatch");
                        }
                        Items.Add(new MVISceneItem(first==null?node.Id:first.Id,
                            first==null?node.Name:first.Name+" / "+node.Name,address,world,
                            width,height,selectable,first==null?parent:firstParent,
                            first==null?x:first.Placement.X,first==null?y:first.Placement.Y,
                            node.TypeId,shape,style,displayText,assetId,mediaType,runtimeValue,widgetSettings));
                    }
                }
                foreach(var id in node.ChildIds)Collect(_snapshot.Find(id),world,path,locked,first,firstParent,depth+1);
                foreach(var reference in node.References)
                {var next=path.Concat(new[]{reference.Id}).ToArray();Collect(_snapshot.Find(reference.TargetId),world.Compose(reference.Placement),next,locked,first??reference,first==null?world:firstParent,depth+1);}
            }
        }
    }
}
