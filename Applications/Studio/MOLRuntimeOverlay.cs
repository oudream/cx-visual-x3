using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Orchestration;

namespace CxVisual.Studio
{
    /// <summary>Only WPF-facing projection; never owns the engineering definition or worker.</summary>
    public sealed class MOLBlockViewModel
    {
        public MOLBlockViewModel(Guid id, string label, MOLControlKind kind)
        { Id=id;Label=label;Kind=kind; }
        public Guid Id {get;private set;}
        public string Label {get;private set;}
        public MOLControlKind Kind {get;private set;}
    }

    public sealed class MOLRuntimeOverlay
    {
        private readonly Dictionary<Guid,string> _states = new Dictionary<Guid,string>();
        public void Clear(){_states.Clear();}
        public void Apply(MOCTraceView batch)
        {
            if(batch==null)throw new ArgumentNullException("batch");
            if(batch.LostCount>0)throw new InvalidOperationException("moc.trace_gap_refresh_required");
            foreach(var ev in batch.Events)
                _states[ev.Origin.BlockId]=ev.Transition.State.ToString();
        }
        public IDictionary<Guid,string> Snapshot()
        {return new ReadOnlyDictionary<Guid,string>(new Dictionary<Guid,string>(_states));}
    }
}
