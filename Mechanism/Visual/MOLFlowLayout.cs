using System;
using System.Collections.Generic;
using System.Linq;
using CxVisual.Mechanism.Core;

namespace CxVisual.Mechanism.Visual
{
    public sealed class MOLBlockLayout
    {
        internal MOLBlockLayout(Guid id,MOLControlKind kind,string label,int depth,double x,double y,double height=40)
        { BlockId=id;Kind=kind;Label=label;Depth=depth;X=x;Y=y;Width=260;Height=height; }
        public Guid BlockId {get;private set;}
        public MOLControlKind Kind {get;private set;}
        public string Label {get;private set;}
        public int Depth {get;private set;}
        public double X {get;private set;}
        public double Y {get;private set;}
        public double Width {get;private set;}
        public double Height {get;private set;}
    }

    /// <summary>Only computes presentation positions; it is not another workflow or engineering tree.</summary>
    public static class MOLFlowLayout
    {

        public static IReadOnlyList<MOLBlockLayout> Build(MOLWiringDefinition wiring)
        {
            if(wiring==null)return Array.AsReadOnly(new MOLBlockLayout[0]);
            wiring.Validate();
            var items=new List<MOLBlockLayout>();
            double top=20;
            // V2端口高度由真实声明决定；布局只在Visual计算，不改变Logic真源。
            foreach(var block in wiring.Blocks)
            {
                var ports=wiring.Ports[block.Id];
                int inputs=ports.Count(p=>p.Direction!=MPCPortDirection.Output);
                int outputs=ports.Count(p=>p.Direction!=MPCPortDirection.Input);
                double height=Math.Max(58,32+Math.Max(inputs,outputs)*22);
                items.Add(new MOLBlockLayout(block.Id,block.Kind,block.CapabilityId,0,20,top,height));
                top+=height+24;
            }
            return items.AsReadOnly();
        }
        public static IReadOnlyList<MOLBlockLayout> Build(MOLLogicDefinition definition)
        {
            if(definition==null)return Array.AsReadOnly(new MOLBlockLayout[0]);
            var rows=new List<MOLBlockLayout>();
            Action<MOLControlBlock,int> emit=null;
            emit=(block,depth)=>
            {
                if(depth>32||rows.Count>4096)throw new InvalidOperationException("moc.layout_budget");
                string label=block.Kind==MOLControlKind.Action?
                    block.CapabilityId:block.Kind==MOLControlKind.Invoke?
                    "Subflow "+block.TargetFlow:block.Kind==MOLControlKind.Loop?
                    "Loop "+block.Loop.Mode+" × "+block.Loop.MaxIterations:block.Kind==MOLControlKind.ForEach?
                    "Foreach "+block.Loop.CollectionInput:block.Kind.ToString();
                rows.Add(new MOLBlockLayout(block.Id,block.Kind,label,depth,
                    20+depth*28,20+rows.Count*58));
                foreach(var child in block.Children)emit(child,depth+1);
                foreach(var branch in block.Cases)emit(branch.Body,depth+1);
                if(block.Loop!=null)emit(block.Loop.Body,depth+1);
            };
            emit(definition.Root,0);
            return rows.AsReadOnly();
        }
    }
}
