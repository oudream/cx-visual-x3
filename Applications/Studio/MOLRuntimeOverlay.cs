using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Orchestration;
using CxVisual.Mechanism.Data.RuntimeData;

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
        /// <summary>直接查询现有MRK运行投影，不复制或持久化运行状态。</summary>
        public string StateOf(Guid id)
        {
            string value;
            return _states.TryGetValue(id,out value)?value:null;
        }
        public IDictionary<Guid,string> Snapshot()
        {return new ReadOnlyDictionary<Guid,string>(new Dictionary<Guid,string>(_states));}
    }
    /// <summary>把SourceMap定位的MRK状态只读适配为现有MDS外部变量，供HMI状态绑定复用。</summary>
    public sealed class MOLVisualTraceBindingResolver : IMDSExternalBindingResolver
    {
        private const string Prefix="Flow.Block.";
        private readonly Func<Guid,string> _stateReader;
        private readonly IMDSExternalBindingResolver _existingOwner;
        public MOLVisualTraceBindingResolver(Func<Guid,string> stateReader,
            IMDSExternalBindingResolver existingOwner=null)
        {
            _stateReader=stateReader??throw new ArgumentNullException("stateReader");
            _existingOwner=existingOwner;
        }
        public static string VariableFor(Guid id,string state)
        {
            if(id==Guid.Empty)throw new ArgumentException("flow.block.empty_id","id");
            if(state!="Running"&&state!="Completed"&&state!="Failed"&&
                state!="Paused"&&state!="Cancelled")
                throw new ArgumentException("visual.flow.unsupported_state","state");
            return Prefix+id.ToString("N")+"."+state;
        }
        public object Resolve(MDSBindingSource source,string name,Type target)
        {
            if(source==MDSBindingSource.Variable&&name!=null&&
                name.StartsWith(Prefix,StringComparison.Ordinal))
            {
                if(target!=typeof(bool))
                    throw new InvalidCastException("visual.flow.bool_required");
                string tail=name.Substring(Prefix.Length);
                int index=tail.IndexOf('.');
                Guid id;
                if(index!=32||tail.LastIndexOf('.')!=index||
                   !Guid.TryParseExact(tail.Substring(0,index),"N",out id))
                    throw new ArgumentException("visual.flow.invalid_binding","name");
                string expected=VariableFor(id,tail.Substring(index+1));
                if(!string.Equals(name,expected,StringComparison.Ordinal))
                    throw new ArgumentException("visual.flow.invalid_binding","name");
                string actual=_stateReader(id);
                return actual==null?null:(object)string.Equals(actual,
                    tail.Substring(index+1),StringComparison.Ordinal);
            }
            return _existingOwner==null?null:_existingOwner.Resolve(source,name,target);
        }
    }
}
