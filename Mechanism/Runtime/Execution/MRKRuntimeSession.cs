using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
namespace CxVisual.Mechanism.Runtime.Execution
{
    public enum MRKRuntimeSessionState { Created, Ready, Stopping, Stopped }
    public sealed class MRKRuntimeSessionSnapshot
    {
        internal MRKRuntimeSessionSnapshot(string id, MRKRuntimeSessionState state,
            int active, long rejected, long revision)
        { Id=id; State=state; ActiveFlows=active; Rejected=rejected; PlanRevision=revision; }
        public string Id {get;private set;}
        public MRKRuntimeSessionState State {get;private set;}
        public int ActiveFlows {get;private set;}
        public long Rejected {get;private set;}
        public long PlanRevision {get;private set;}
    }
    /// <summary>一个Runtime会话承载多个Flow；不创建或关闭共享Worker。</summary>
    public sealed class MRKRuntimeSession
    {
        private readonly object _gate=new object();
        private readonly MRKExecutionEngine _engine;
        private readonly MRKExecutionPlanSlot _slot;
        private readonly int _capacity;
        private readonly Dictionary<Guid,MRKFlowHandle> _flows=new Dictionary<Guid,MRKFlowHandle>();
        private MRKRuntimeSessionState _state=MRKRuntimeSessionState.Created;
        private Task _stopTask;
        private long _rejected;
        public MRKRuntimeSession(string id,MRKExecutionEngine engine,MRKExecutionPlanSlot slot,int maxInFlight=1)
        {
            if(string.IsNullOrWhiteSpace(id))throw new ArgumentException("会话ID不能为空","id");
            if(engine==null)throw new ArgumentNullException("engine");
            if(slot==null)throw new ArgumentNullException("slot");
            if(maxInFlight<1)throw new ArgumentOutOfRangeException("maxInFlight");
            Id=id;_engine=engine;_slot=slot;_capacity=maxInFlight;
        }
        public string Id {get;private set;}
        public MRKRuntimeSessionSnapshot Snapshot()
        { lock(_gate)return new MRKRuntimeSessionSnapshot(Id,_state,_flows.Count,_rejected,_slot.ActiveRevision); }
        public void Start()
        {
            lock(_gate)
            {
                if(_state!=MRKRuntimeSessionState.Created)
                    throw new InvalidOperationException("会话只允许启动一次");
                _state=MRKRuntimeSessionState.Ready;
            }
        }
        public MRKFlowHandle TryStartFlow(IDictionary<string,object> inputs=null,
            string businessKey=null,string correlationId=null,
            CancellationToken cancellation=default(CancellationToken))
        {
            lock(_gate)
            {
                if(_state!=MRKRuntimeSessionState.Ready || _flows.Count>=_capacity)
                { _rejected++;return null; }
                MRKFlowHandle flow=_engine.StartFromSlotWithMetadata(_slot,
                    new MRKFlowMetadata(businessKey:businessKey,
                        correlationId:correlationId,sessionId:Id),inputs,cancellation);
                _flows.Add(flow.FlowId,flow);
                Task cleanup=flow.Completion.ContinueWith(done=>
                {lock(_gate)_flows.Remove(flow.FlowId);},
                    CancellationToken.None,TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);
                return flow;
            }
        }
        public bool TryActivatePendingPlan()
        { lock(_gate)return _state==MRKRuntimeSessionState.Ready && _flows.Count==0 && _slot.TryActivatePending(); }
        public Task StopAsync(bool cancel=false)
        {
            MRKFlowHandle[] active;
            lock(_gate)
            {
                if(_stopTask!=null)return _stopTask;
                _state=MRKRuntimeSessionState.Stopping;
                active=new List<MRKFlowHandle>(_flows.Values).ToArray();
                _stopTask=DrainAsync(active);
            }
            if(cancel)foreach(MRKFlowHandle flow in active)flow.RequestCancel();
            return _stopTask;
        }
        private async Task DrainAsync(MRKFlowHandle[] active)
        {
            try
            {
                List<Task> pending=new List<Task>();
                foreach(MRKFlowHandle flow in active)pending.Add(flow.Completion);
                await Task.WhenAll(pending).ConfigureAwait(false);
            }
            finally {lock(_gate)_state=MRKRuntimeSessionState.Stopped;}
        }
    }
}
