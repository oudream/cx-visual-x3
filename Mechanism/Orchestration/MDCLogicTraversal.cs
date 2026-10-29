using System;
using System.Collections.Generic;
using CxVisual.Mechanism.Core;

namespace CxVisual.Mechanism.Decision
{
    /// <summary>Single read-only traversal of Core's V1 structured MOL tree;
    /// shared by D10 impact and static source manifest. Never mutates Core.</summary>
    internal static class MDCLogicTraversal
    {
        internal static IEnumerable<MOLControlBlock> Flatten(MOLControlBlock root)
        {
            if(root==null)throw new ArgumentNullException("root");
            var stack=new Stack<MOLControlBlock>();
            var ids=new HashSet<Guid>();
            stack.Push(root);
            while(stack.Count>0)
            {
                MOLControlBlock block=stack.Pop();
                if(!ids.Add(block.Id))
                    throw new InvalidOperationException("decision.logic.duplicate_block");
                yield return block;
                foreach(var child in block.Children)stack.Push(child);
                foreach(var item in block.Cases)stack.Push(item.Body);
                if(block.Loop!=null)stack.Push(block.Loop.Body);
                if(block.TryRegion!=null)
                {
                    stack.Push(block.TryRegion.TryBody);
                    if(block.TryRegion.CatchBody!=null)stack.Push(block.TryRegion.CatchBody);
                    if(block.TryRegion.FinallyBody!=null)stack.Push(block.TryRegion.FinallyBody);
                    foreach(var c in block.TryRegion.Catches)stack.Push(c.Body);
                }
            }
        }
    }
}
