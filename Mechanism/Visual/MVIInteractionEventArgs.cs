using System;
using System.Collections.Generic;
namespace CxVisual.Mechanism.Visual
{
    public enum MVIEventPhase {Capture=1,Target=2,Bubble=3}
    public enum MVIInteractionKind {Select=1,Move=2,Delete=3,Place=4}
    public sealed class MVIInteractionEventArgs : EventArgs
    {
        internal MVIInteractionEventArgs(MVIInteractionKind kind,Guid target,IReadOnlyList<Guid> route){Kind=kind;TargetId=target;Route=route;}
        public MVIInteractionKind Kind {get;private set;}public Guid TargetId {get;private set;}public Guid CurrentId {get;internal set;}
        public MVIEventPhase Phase {get;internal set;}public IReadOnlyList<Guid> Route {get;private set;}
        public bool DefaultPrevented {get;private set;}public bool PropagationStopped {get;private set;}
        public void PreventDefault(){DefaultPrevented=true;}public void StopPropagation(){PropagationStopped=true;}
    }
}
