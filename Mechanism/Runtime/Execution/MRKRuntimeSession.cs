using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
namespace CxVisual.Mechanism.Runtime.Execution
{
    public enum MRKRuntimeSessionState { Created, Ready, Stopping, Stopped }
    /// <summary>Session保留已完成FlowResult的可选容量/时限策略；默认完全不保留。</summary>
    public sealed class MRKSessionResultRetentionOptions
    {
        public MRKSessionResultRetentionOptions(int maxResults, long maxPayloadBytes,
            TimeSpan timeToLive)
        {
            if (maxResults < 1) throw new ArgumentOutOfRangeException("maxResults");
            if (maxPayloadBytes < 1) throw new ArgumentOutOfRangeException("maxPayloadBytes");
            if (timeToLive <= TimeSpan.Zero || timeToLive > TimeSpan.FromDays(3650))
                throw new ArgumentOutOfRangeException("timeToLive");
            MaxResults = maxResults; MaxPayloadBytes = maxPayloadBytes; TimeToLive = timeToLive;
        }
        public int MaxResults { get; private set; }
        public long MaxPayloadBytes { get; private set; }
        public TimeSpan TimeToLive { get; private set; }
    }
    public sealed class MRKRuntimeSessionSnapshot
    {
        internal MRKRuntimeSessionSnapshot(string id, MRKRuntimeSessionState state,
            int active, long rejected, long revision,
            int retained = 0, long retainedBytes = 0, long retentionRejected = 0)
        {
            Id=id; State=state; ActiveFlows=active; Rejected=rejected; PlanRevision=revision;
            RetainedResults=retained; RetainedPayloadBytes=retainedBytes;
            RetentionRejected=retentionRejected;
        }
        public string Id {get;private set;}
        public MRKRuntimeSessionState State {get;private set;}
        public int ActiveFlows {get;private set;}
        public long Rejected {get;private set;}
        public long PlanRevision {get;private set;}
        public int RetainedResults { get; private set; }
        public long RetainedPayloadBytes { get; private set; }
        public long RetentionRejected { get; private set; }
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
        private readonly MRKSessionResultRetentionOptions _retention;
        private sealed class RetainedResult
        {
            internal MRKFlowResult Result;
            internal long Bytes;
            internal DateTime ExpiresUtc;
        }
        private readonly Dictionary<Guid, RetainedResult> _retained =
            new Dictionary<Guid, RetainedResult>();
        private long _retainedBytes;
        private long _retentionRejected;
        public MRKRuntimeSession(string id,MRKExecutionEngine engine,MRKExecutionPlanSlot slot,
            int maxInFlight=1, MRKSessionResultRetentionOptions resultRetention=null)
        {
            if(string.IsNullOrWhiteSpace(id))throw new ArgumentException("会话ID不能为空","id");
            if(engine==null)throw new ArgumentNullException("engine");
            if(slot==null)throw new ArgumentNullException("slot");
            if(maxInFlight<1)throw new ArgumentOutOfRangeException("maxInFlight");
            Id=id;_engine=engine;_slot=slot;_capacity=maxInFlight;_retention=resultRetention;
        }
        public string Id {get;private set;}
        public MRKRuntimeSessionSnapshot Snapshot()
        {
            lock (_gate)
            {
                PruneExpiredLocked();
                return new MRKRuntimeSessionSnapshot(Id,_state,_flows.Count,_rejected,
                    _slot.ActiveRevision,_retained.Count,_retainedBytes,_retentionRejected);
            }
        }

        /// <summary>领取Session保留责任：取出后Session不再持有该FlowResult的引用。</summary>
        public bool TryTakeCompletedResult(Guid flowId, out MRKFlowResult result)
        {
            lock (_gate)
            {
                PruneExpiredLocked();
                RetainedResult item;
                if (!_retained.TryGetValue(flowId, out item))
                { result=null; return false; }
                _retained.Remove(flowId);
                _retainedBytes -= item.Bytes;
                result = item.Result;
                return true;
            }
        }

        /// <summary>主动释放Session对已完成结果的引用，不影响用户已有Flow.Completion结果。</summary>
        public bool ReleaseRetainedResult(Guid flowId)
        {
            lock (_gate)
            {
                PruneExpiredLocked();
                RetainedResult item;
                if (!_retained.TryGetValue(flowId, out item)) return false;
                _retained.Remove(flowId);
                _retainedBytes -= item.Bytes;
                return true;
            }
        }

        public int SweepExpiredResults()
        {
            lock (_gate) return PruneExpiredLocked();
        }
        private int PruneExpiredLocked()
        {
            int count = 0;
            DateTime now = DateTime.UtcNow;
            foreach (Guid key in new List<Guid>(_retained.Keys))
                if (_retained[key].ExpiresUtc <= now)
                {
                    _retainedBytes -= _retained[key].Bytes;
                    _retained.Remove(key);
                    count++;
                }
            return count;
        }
        private void OnFlowCompleted(MRKFlowHandle flow, Task<MRKFlowResult> done)
        {
            MRKFlowResult result = done.Status == TaskStatus.RanToCompletion ? done.Result : null;
            long bytes = 0;
            bool measurable = false;
            if (result != null && result.State == MRKFlowState.Completed && _retention != null)
                try { measurable = result.TryEstimateCommittedBytes(out bytes); }
                catch { measurable = false; }
            lock (_gate)
            {
                _flows.Remove(flow.FlowId);
                if (_retention == null || _state != MRKRuntimeSessionState.Ready ||
                    result == null || result.State != MRKFlowState.Completed) return;
                PruneExpiredLocked();
                if (!measurable || _retained.Count >= _retention.MaxResults ||
                    bytes > _retention.MaxPayloadBytes - _retainedBytes)
                {
                    _retentionRejected++;
                    return;
                }
                _retained.Add(flow.FlowId, new RetainedResult {
                    Result=result, Bytes=bytes,
                    ExpiresUtc=DateTime.UtcNow.Add(_retention.TimeToLive)
                });
                _retainedBytes += bytes;
            }
        }
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
        { return TryStartFlowCore(null,inputs,businessKey,correlationId,cancellation); }

        public MRKFlowHandle TryStartFlowWithControl(MRKExecutionDebugController control,
            IDictionary<string,object> inputs=null,
            string businessKey=null,string correlationId=null,
            CancellationToken cancellation=default(CancellationToken))
        {
            if(control==null)throw new ArgumentNullException("control");
            return TryStartFlowCore(control,inputs,businessKey,correlationId,cancellation);
        }

        private MRKFlowHandle TryStartFlowCore(MRKExecutionDebugController control,
            IDictionary<string,object> inputs,string businessKey,string correlationId,
            CancellationToken cancellation)
        {
            lock(_gate)
            {
                if(_state!=MRKRuntimeSessionState.Ready || _flows.Count>=_capacity)
                { _rejected++;return null; }
                MRKFlowMetadata metadata=new MRKFlowMetadata(
                    businessKey:businessKey,correlationId:correlationId,sessionId:Id);
                MRKFlowHandle flow=control==null
                    ? _engine.StartFromSlotWithMetadata(_slot,metadata,inputs,cancellation)
                    : _engine.StartFromSlotWithControl(_slot,control,metadata,inputs,cancellation);
                _flows.Add(flow.FlowId,flow);
                Task cleanup=flow.Completion.ContinueWith(done=>
                    OnFlowCompleted(flow,done),
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
                if (_stopTask == null)
                {
                    _state = MRKRuntimeSessionState.Stopping;
                    _stopTask = DrainAsync(new List<MRKFlowHandle>(_flows.Values).ToArray());
                }
                active = new List<MRKFlowHandle>(_flows.Values).ToArray();
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
            finally
            {
                lock (_gate)
                {
                    _state=MRKRuntimeSessionState.Stopped;
                    _retained.Clear();
                    _retainedBytes=0;
                }
            }
        }
    }
}
