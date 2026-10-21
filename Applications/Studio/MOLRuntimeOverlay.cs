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

    /// <summary>仅用于Studio观察的限定执行步骤快照，保留Flow/Content/Step身份。</summary>
    public sealed class MOLRunStepSnapshot
    {
        internal MOLRunStepSnapshot(Guid flowId,MOCObservedStep eventItem)
        {
            FlowId=flowId;StepId=eventItem.Transition.StepId;
            BlockId=eventItem.Origin.BlockId;
            ContentAddress=eventItem.Origin.ContentAddress;
            CallPath=eventItem.Origin.CallPath;
            State=eventItem.Transition.State.ToString();
            Sequence=eventItem.Transition.Sequence;
        }
        public Guid FlowId{get;private set;}
        public string StepId{get;private set;}
        public Guid BlockId{get;private set;}
        public MRIObjectAddress ContentAddress{get;private set;}
        public IReadOnlyList<Guid> CallPath{get;private set;}
        public string State{get;private set;}
        public long Sequence{get;private set;}
    }

    public sealed class MOLRuntimeOverlay
    {
        private const int MaxObservedSteps=4096;
        // 老HMI绑定仍使用按Block聚合的状态；新调试索引用Flow+限定执行路径，
        // 其内容只是MRK Trace的有界只读投影，不作为另一份运行真源。
        private readonly Dictionary<Guid,string> _states=new Dictionary<Guid,string>();
        private readonly Dictionary<string,MOLRunStepSnapshot> _observed=
            new Dictionary<string,MOLRunStepSnapshot>(StringComparer.Ordinal);
        private readonly Queue<string> _observedOrder=new Queue<string>();
        private static string Key(Guid flowId,string stepId)
        {return flowId.ToString("N")+":"+stepId;}
        public void Clear()
        {
            _states.Clear();_observed.Clear();_observedOrder.Clear();
        }
        /// <summary>旧BlockId视图兼容，未提供FlowId时不得冒充可定位某次Flow。</summary>
        public void Apply(MOCTraceView batch){Apply(Guid.Empty,batch);}
        /// <summary>接收具体Flow的有序MRK变化；发生Gap即拒绝增量覆盖。</summary>
        public void Apply(Guid flowId,MOCTraceView batch)
        {
            if(batch==null)throw new ArgumentNullException("batch");
            if(batch.LostCount>0)throw new InvalidOperationException("moc.trace_gap_refresh_required");
            foreach(MOCObservedStep ev in batch.Events)
            {
                _states[ev.Origin.BlockId]=ev.Transition.State.ToString();
                if(flowId==Guid.Empty)continue;
                string key=Key(flowId,ev.Transition.StepId);
                if(!_observed.ContainsKey(key))
                {
                    while(_observedOrder.Count>=MaxObservedSteps)
                        _observed.Remove(_observedOrder.Dequeue());
                    _observedOrder.Enqueue(key);
                }
                _observed[key]=new MOLRunStepSnapshot(flowId,ev);
            }
        }
        /// <summary>历史可视图元状态（多个Flow时仅为聚合显示，不用于调试定位）。</summary>
        public string StateOf(Guid id)
        {
            string value;
            return _states.TryGetValue(id,out value)?value:null;
        }
        /// <summary>查询指定Flow下某次真实执行路径，如loop.i37.work。</summary>
        public MOLRunStepSnapshot Find(Guid flowId,string qualifiedStepId)
        {
            if(flowId==Guid.Empty||string.IsNullOrWhiteSpace(qualifiedStepId))return null;
            MOLRunStepSnapshot item;
            return _observed.TryGetValue(Key(flowId,qualifiedStepId),out item)?item:null;
        }
        /// <summary>只读的Flow实例快照，不更改MEM/MRK，也不保留大对象负载。</summary>
        public IReadOnlyList<MOLRunStepSnapshot> Snapshot(Guid flowId)
        {
            var result=new List<MOLRunStepSnapshot>();
            foreach(MOLRunStepSnapshot item in _observed.Values)
                if(item.FlowId==flowId)result.Add(item);
            result.Sort((a,b)=>a.Sequence.CompareTo(b.Sequence));
            return new ReadOnlyCollection<MOLRunStepSnapshot>(result);
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
