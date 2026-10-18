using System;
using System.Collections.Generic;
using System.Linq;

namespace CxVisual.Mechanism.Core
{
    public sealed partial class MTCEditSession
    {
        private static void DemandEditable(MEMNode node)
        {for(var n=node;n!=null;n=n.Parent){var v=n as MRCVisualItem;if(v!=null&&v.IsLocked)throw new InvalidOperationException("org.visual.locked");}}
        public void SetLocked(MRCVisualItem item,bool locked)
        {Demand();_session.DemandNode(item);bool old=item.IsLocked;if(old!=locked)Change(()=>item.IsLocked=locked,()=>item.IsLocked=old,MTCChangeKind.Visibility,item.Id);}
        public void SetLayer(MRCVisualItem item,string layerId)
        {Demand();_session.DemandNode(item);DemandEditable(item);layerId=layerId==""?"":MEMNode.NormalizeName(layerId);string old=item.LayerId;if(old!=layerId)Change(()=>item.LayerId=layerId,()=>item.LayerId=old,MTCChangeKind.Visibility,item.Id);}
        public void SetTransform(MRCVisualItem item,MRCTransform transform)
        {double angle,sx,sy,shear;transform.Decompose(out angle,out sx,out sy,out shear);Geometry(item,transform.X,transform.Y,item.Width,item.Height,angle,sx,sy,shear);}
        public MRCGroup Group(IEnumerable<MRCVisualItem> selection,string name)
        {
            Demand();if(selection==null)throw new ArgumentNullException("selection");var requested=selection.ToArray();
            if(requested.Length<2||requested.Any(x=>x==null)||requested.Distinct().Count()!=requested.Length)throw new InvalidOperationException("org.group.selection");
            var parent=requested[0].Parent;_session.DemandNode(parent);DemandEditable(parent);
            if(!(parent is MEMContent||parent is MRCGroup)||requested.Any(x=>x.Parent!=parent))throw new InvalidOperationException("org.group.siblings_required");
            foreach(var item in requested){_session.DemandNode(item);DemandEditable(item);}
            if(requested.Select(x=>x.LayerId).Distinct(StringComparer.Ordinal).Count()!=1)throw new InvalidOperationException("org.group.layer_mismatch");
            var selected=new HashSet<MRCVisualItem>(requested);var ordered=parent.Children.OfType<MRCVisualItem>().Where(selected.Contains).ToArray();int index=parent.IndexOf(ordered[0]);
            double left=double.PositiveInfinity,top=double.PositiveInfinity,right=double.NegativeInfinity,bottom=double.NegativeInfinity;
            foreach(var item in ordered)foreach(var point in new[]{Tuple.Create(0.0,0.0),Tuple.Create(item.Width,0.0),Tuple.Create(0.0,item.Height),Tuple.Create(item.Width,item.Height)})
            {double x,y;item.LocalToParent(point.Item1,point.Item2,out x,out y);left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x);bottom=Math.Max(bottom,y);}
            var group=(MRCGroup)Add(parent,new MRCGroup(Guid.NewGuid(),name));Reparent(group,parent,index);
            Geometry(group,left,top,Math.Max(1,right-left),Math.Max(1,bottom-top),0,1,1);SetLayer(group,ordered[0].LayerId);
            foreach(var item in ordered){double x=item.X-left,y=item.Y-top;Reparent(item,group);Move(item,x,y);}
            return group;
        }
        public IReadOnlyList<MRCVisualItem> Ungroup(MRCGroup group)
        {
            Demand();_session.DemandNode(group);DemandEditable(group);var parent=group.Parent;
            if(!(parent is MEMContent||parent is MRCGroup))throw new InvalidOperationException("org.group.protected_or_invalid_parent");
            if(group.BindingItems!=null&&group.BindingItems.Count!=0)throw new InvalidOperationException("org.group.binding_in_use");
            var children=group.Children.ToArray();foreach(var item in children)DemandEditable(item);
            var transforms=children.Select(c=>group.Transform.Compose(c.Transform)).ToArray();
            // Decompose every result before changing ownership; shear is retained, not approximated away.
            foreach(var value in transforms){double a,sx,sy,sh;value.Decompose(out a,out sx,out sy,out sh);}
            int index=parent.IndexOf(group);
            for(int i=0;i<children.Length;i++)
            {var item=children[i];Reparent(item,parent,index+i);SetTransform(item,transforms[i]);SetLayer(item,group.LayerId);SetVisibility(item,item.IsVisible&&group.IsVisible,item.IsSelectable);}
            Remove(group);return Array.AsReadOnly(children);
        }
    }
    public sealed partial class MEMEngineeringSession
    {
        public MRCGroup Group(IEnumerable<MRCVisualItem> selection,string name) {return Edit("分组",e=>e.Group(selection,name)).RequireValue();}
        public IReadOnlyList<MRCVisualItem> Ungroup(MRCGroup group) {return Edit("解组",e=>e.Ungroup(group)).RequireValue();}
        public void SetTransform(MRCVisualItem item,MRCTransform transform) {Edit("设置仿射变换",e=>e.SetTransform(item,transform)).RequireValue();}
        public void SetLocked(MRCVisualItem item,bool locked) {Edit("设置编辑锁",e=>e.SetLocked(item,locked)).RequireValue();}
        public void SetLayer(MRCVisualItem item,string layerId) {Edit("设置图层",e=>e.SetLayer(item,layerId)).RequireValue();}
    }
}
