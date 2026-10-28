using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Foundation.Infrastructure.Logging;
using CxVisual.Foundation.Data.Execution;
using CxVisual.Mechanism.Data.RuntimeData;
using CxVisual.Foundation.Data.Types;

namespace CxVisual.Mechanism.Runtime.Execution
{
    public enum MRKFlowState { Running, Completed, Failed, Cancelled }
    public sealed class MRKFlowCapacityExceededException : InvalidOperationException
    {
        public MRKFlowCapacityExceededException(int limit)
            : base("运行引擎在途Flow容量已满：" + limit) { Limit = limit; }
        public int Limit { get; private set; }
    }
    public enum MRKStepState { WaitingDependencies, Scheduled, Completed, Failed, Cancelled, Queued, Running, Waiting, Paused, WaitingResources, WaitingGroup, Skipped, ResultUnknown }

    internal sealed class MRKIterationSignal
    {
        private int _exit;
        internal void Request(string kind)
        {
            int value=kind=="break"?1:kind=="continue"?2:kind=="return"?3:0;
            if(value==0)throw new ArgumentException("loop.unknown_exit");
            Interlocked.CompareExchange(ref _exit,value,0);
        }
        internal int Exit{get{return Volatile.Read(ref _exit);}}
    }
    internal sealed class MRKFlowReturnSignal
    {
        private int _requested;
        internal bool Requested{get{return Volatile.Read(ref _requested)!=0;}}
        internal void Request(){Interlocked.Exchange(ref _requested,1);}
    }

    /// <summary>同一次流程的局部快照；不对外暴露可写的全局数据字典。</summary>

    /// <summary>
    /// 同一Flow的一次循环体节点已提交的输出引用。值来自MDS安全快照，
    /// FrameKey和InvocationId与原MRK Trace/观察路径相同，不新建数据真源。
    /// </summary>
    public sealed class MRKLoopOutput<T>
    {
        internal MRKLoopOutput(Guid flowId, int iterationIndex, string frameKey,
            string invocationId, T value)
        {
            FlowId = flowId; IterationIndex = iterationIndex;
            FrameKey = frameKey; InvocationId = invocationId; Value = value;
        }
        public Guid FlowId { get; private set; }
        public int IterationIndex { get; private set; }
        public string FrameKey { get; private set; }
        public string InvocationId { get; private set; }
        public T Value { get; private set; }
    }

    public sealed class MRKExecutionContext
    {
        private readonly ConcurrentDictionary<string, object> _results =
            new ConcurrentDictionary<string, object>(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, FCRWorkHandle<object>> _handles =
            new ConcurrentDictionary<string, FCRWorkHandle<object>>(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, MRKStepObservation> _observations =
            new ConcurrentDictionary<string, MRKStepObservation>(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, MRKStepState> _states =
            new ConcurrentDictionary<string, MRKStepState>(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, MRKLoopDataRetention> _loopRetention =
            new ConcurrentDictionary<string, MRKLoopDataRetention>(StringComparer.Ordinal);
        private readonly ReadOnlyDictionary<string, object> _inputs;
        private readonly string _scopePrefix;
        private readonly MRKExecutionTrace _trace;
        private readonly IMRKExecutionJournal _journal;
        private readonly IDictionary<string, object> _boundInputs;
        private readonly IFDEExecutionDataScope _dataScope;
        private readonly MDSDataStack _managedStack;
        private readonly IMDSDataContext _data;
        private readonly int _iterationIndex;
        private readonly bool _insideParallelLoop;
        private readonly MRKIterationSignal _iterationSignal;
        private readonly MRKFlowReturnSignal _returnSignal=new MRKFlowReturnSignal();
        private bool _cleanupRegion;
        private readonly MRKInvocationContext _invocation;
        private readonly ConcurrentQueue<string> _completedOrder = new ConcurrentQueue<string>();
        internal MRKExecutionContext(string planId, IDictionary<string, object> inputs,
            MRKFlowMetadata metadata = null, IFDEExecutionDataScope dataSpace = null,
            IMRKExecutionJournal journal = null, int traceCapacity = 2048,
            MDSDataBudget dataBudget = null)
        {
            FlowId = Guid.NewGuid();
            PlanId = planId;
            Metadata = metadata ?? new MRKFlowMetadata();
            _scopePrefix = string.Empty;
            _journal = journal;
            _dataScope = dataSpace == null ? null : dataSpace.CreateChild("flow-" + FlowId.ToString("N"));
            _managedStack = new MDSDataStack(_dataScope, null, FlowId.ToString("N"), dataBudget);
            _trace = new MRKExecutionTrace(traceCapacity);
            _boundInputs = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());
            _inputs = CopyByteBuffers(inputs ??
                new Dictionary<string, object>(StringComparer.Ordinal));
        }
        private MRKExecutionContext(MRKExecutionContext parent, string prefix)
        {
            FlowId = parent.FlowId;
            PlanId = parent.PlanId;
            Metadata = parent.Metadata;
            _results = parent._results;
            _states = parent._states;
            _loopRetention = parent._loopRetention;
            _observations = parent._observations;
            _handles = parent._handles;
            _inputs = parent._inputs;
            _scopePrefix = parent._scopePrefix + prefix;
            _trace = parent._trace;
            _journal = parent._journal;
            _dataScope = parent._dataScope;
            _managedStack = parent._managedStack;
            _data = parent._data;
            _invocation = parent._invocation;
            _iterationIndex=parent._iterationIndex;
            _insideParallelLoop=parent._insideParallelLoop;
            _iterationSignal=parent._iterationSignal;
            _returnSignal=parent._returnSignal;
            _boundInputs = parent._boundInputs;
            _completedOrder = parent._completedOrder;
            _cleanupRegion=parent._cleanupRegion;
        }
        internal MRKExecutionContext ForSubflow(string prefix)
        { return new MRKExecutionContext(this, prefix); }
        internal MRKExecutionContext ForCleanup(string prefix)
        {
            var result=new MRKExecutionContext(this,prefix);
            result._cleanupRegion=true;
            return result;
        }
        internal bool IsCleanupRegion{get{return _cleanupRegion;}}
        private MRKExecutionContext(MRKExecutionContext parent,string prefix,int iteration,
            bool parallel)
            : this(parent,prefix)
        {
            _iterationIndex=iteration;
            _insideParallelLoop=parent._insideParallelLoop||parallel;
            _iterationSignal=new MRKIterationSignal();
        }
        internal MRKExecutionContext ForLoopIteration(string prefix,int iteration,
            bool parallel=false)
        {
            if(string.IsNullOrWhiteSpace(prefix)||iteration<0)
                throw new ArgumentException("loop.invalid_activation");
            return new MRKExecutionContext(this,prefix,iteration,parallel);
        }
        /// <summary>当前动态循环迭代号，常规DAG默认为0。</summary>
        public int IterationIndex{get{return _iterationIndex;}}
        internal string QualifiedStepId(string id){return _scopePrefix+id;}
        internal int LoopExit{get{return _iterationSignal==null?0:_iterationSignal.Exit;}}
        internal bool FlowReturnRequested{get{return _returnSignal.Requested;}}
        internal void RegisterLoopRetention(string stepId, MRKLoopDataRetention retention)
        { _loopRetention[_scopePrefix + stepId] = retention; }
        /// <summary>由编译的Break/Continue/Return控制节点请求，不能绕过安全点。</summary>
        public void SignalControlExit(string kind)
        {
            if(_insideParallelLoop)
                throw new InvalidOperationException("loop.parallel_control_exit_unsupported");
            if(_iterationSignal==null)
            {
                if(kind!="return")
                    throw new InvalidOperationException("loop.exit_outside_region");
                _returnSignal.Request();
                return;
            }
            _iterationSignal.Request(kind);
            if(kind=="return")_returnSignal.Request();
        }
        public bool TryGetScopedResult(string stepId,out object value)
        {
            if(TryGetResult(stepId,out value))return true;
            return TryGetAbsoluteResult(stepId,out value);
        }

        private MRKExecutionContext(MRKExecutionContext parent,MRKInvocationContext invocation)
            : this(parent,string.Empty)
        { _invocation=invocation; }
        internal MRKExecutionContext WithInvocation(MRKInvocationContext invocation)
        {
            if(invocation==null||invocation.FlowId!=FlowId)
                throw new ArgumentException("invocation.flow_mismatch");
            return new MRKExecutionContext(this,invocation);
        }
        /// <summary>当前实际运行节点的Invocation；根Flow上下文为null。</summary>
        public MRKInvocationContext Invocation{get{return _invocation;}}
        private MRKExecutionContext(MRKExecutionContext parent, IDictionary<string, object> ports)
            : this(parent, string.Empty)
        { _boundInputs = ports; }
        internal MRKExecutionContext WithBoundInputs(IDictionary<string, object> ports)
        { return new MRKExecutionContext(this, ports); }
        private MRKExecutionContext(MRKExecutionContext parent, IFDEExecutionDataScope scope)
            : this(parent, string.Empty)
        { _dataScope = scope; }
        internal MRKExecutionContext WithDataScope(IFDEExecutionDataScope scope)
        { return new MRKExecutionContext(this, scope); }
        public IFDEExecutionDataScope DataScope { get { return _dataScope; } }
        public IMDSDataContext Data { get { return _data; } }
        internal MDSDataStack DataStack { get { return _managedStack; } }
        private MRKExecutionContext(MRKExecutionContext parent, IMDSDataContext data)
            : this(parent, string.Empty) { _data = data; }
        internal MRKExecutionContext WithDataContext(IMDSDataContext data)
        { return new MRKExecutionContext(this, data); }
        internal bool TryGetCommittedData(string sourceName, out object value)
        {
            int dot = sourceName.LastIndexOf('.');
            if (dot < 1 || dot == sourceName.Length - 1)
                throw new ArgumentException("具名输出必须为节点ID.输出名", "sourceName");
            return _managedStack.TryReadOutput(_scopePrefix+sourceName.Substring(0,dot),
                sourceName.Substring(dot+1),out value);
        }

        /// <summary>
        /// R07：按原循环的真实完成次数，按迭代号读取每轮已提交的具名输出。
        /// 仅能在本Flow的数据栈存活期间调用，常见场景是依赖该Loop的后继节点。
        /// 循环未完成、任一迭代Skipped/Failed/缺少输出均拒绝整个归并；
        /// 不读取Pending，不替代MDS Commit，不启动新调度器或扩散到其他Flow。
        /// </summary>
        public IReadOnlyList<MRKLoopOutput<T>> CollectLoopOutputs<T>(
            string loopStepId, string bodyStepId, string outputName)
        {
            if (string.IsNullOrWhiteSpace(loopStepId) ||
                string.IsNullOrWhiteSpace(bodyStepId) ||
                string.IsNullOrWhiteSpace(outputName))
                throw new ArgumentException("loop.outputs.identity_required");
            string loopKey = _scopePrefix + loopStepId;
            MRKStepState loopState;
            object countValue;
            if (!_states.TryGetValue(loopKey, out loopState) ||
                loopState != MRKStepState.Completed ||
                !_results.TryGetValue(loopKey, out countValue) || !(countValue is int))
                throw new InvalidOperationException("loop.outputs.loop_not_completed:" + loopKey);
            MRKLoopDataRetention declared;
            if (_loopRetention.TryGetValue(loopKey, out declared) &&
                !declared.Retains(bodyStepId))
                throw new InvalidOperationException(
                    "loop.outputs.history_not_retained:" + loopKey + "." + bodyStepId);
            int iterations = (int)countValue;
            if (iterations < 0)
                throw new InvalidOperationException("loop.outputs.invalid_iteration_count");
            List<MRKLoopOutput<T>> outputs = new List<MRKLoopOutput<T>>();
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                string frameKey = loopKey + ".i" +
                    iteration.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                    "." + bodyStepId;
                MRKStepObservation step;
                if (!_observations.TryGetValue(frameKey, out step) ||
                    step.State != MRKStepState.Completed ||
                    step.InvocationId != FlowId.ToString("N") + ":" + frameKey)
                    throw new InvalidOperationException(
                        "loop.outputs.step_not_completed:" + frameKey);
                T value;
                if (!_managedStack.TryReadOutput<T>(frameKey, outputName, out value))
                    throw new InvalidOperationException(
                        "loop.outputs.missing_committed_output:" + frameKey + "." + outputName);
                outputs.Add(new MRKLoopOutput<T>(
                    FlowId, iteration, frameKey, step.InvocationId, value));
            }
            return outputs.AsReadOnly();
        }

        internal void CloseFlowDataScope()
        {
            try { _managedStack.Dispose(); }
            finally { if (_dataScope != null) _dataScope.Dispose(); }
        }

        private static ReadOnlyDictionary<string, object> CopyByteBuffers(
            IDictionary<string, object> source)
        {
            Dictionary<string, object> copy = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> item in source)
                copy.Add(item.Key, item.Value is byte[] ? ((byte[])item.Value).Clone() : item.Value);
            return new ReadOnlyDictionary<string, object>(copy);
        }
        public IDictionary<string, object> BoundInputs { get { return CopyByteBuffers(_boundInputs); } }
        public bool TryGetBoundInput<T>(string name, out T value)
        {
            object data;
            if (_boundInputs.TryGetValue(name, out data) && data is T)
            { value = (T)(data is byte[] ? ((byte[])data).Clone() : data); return true; }
            value = default(T);
            return false;
        }
        /// <summary>跨子流程结果必须显式使用完整路径，防止名字碰撞。</summary>
        public bool TryGetAbsoluteResult(string absoluteStepId, out object result)
        {
            bool found = _results.TryGetValue(absoluteStepId, out result);
            if (result is NullResult) result = null;
            else if (result is byte[]) result = ((byte[])result).Clone();
            return found;
        }
        public Guid FlowId { get; private set; }
        public string PlanId { get; private set; }
        public MRKFlowMetadata Metadata { get; private set; }
        public MRKExecutionTrace Trace { get { return _trace; } }
        public IDictionary<string, object> Inputs { get { return CopyByteBuffers(_inputs); } }
        public bool TryGetResult(string stepId, out object result)
        {
            bool found = _results.TryGetValue(_scopePrefix + stepId, out result);
            if (result is NullResult) result = null;
            else if (result is byte[]) result = ((byte[])result).Clone();
            return found;
        }
        internal void SetResult(string stepId, object result)
        { _results[_scopePrefix + stepId] = result is byte[] ? ((byte[])result).Clone() :
                (result ?? NullResult.Instance); }
        /// <summary>用当前调用实例的完整路径读取状态，防止循环内同名节点串账。</summary>
        public bool TryGetScopedState(string stepId,out MRKStepState state)
        {
            if(string.IsNullOrWhiteSpace(stepId))
                throw new ArgumentException("step.identity_required","stepId");
            return _states.TryGetValue(_scopePrefix+stepId,out state);
        }
        internal void SetState(string stepId, MRKStepState state, string reason = null)
        {
            string qualified = _scopePrefix + stepId;
            _states[qualified] = state;
            FCRWorkHandle<object> handle;
            Guid? workId = _handles.TryGetValue(qualified, out handle)
                ? (Guid?)handle.Id : null;
            _observations[qualified] = new MRKStepObservation(qualified, state, workId,
                reason ?? string.Empty, DateTime.UtcNow,
                _invocation==null?null:_invocation.InvocationId);
            if (state == MRKStepState.Completed) _completedOrder.Enqueue(qualified);
            _trace.Record(qualified, state);
            if (_journal != null)
                _journal.Append(new MRKJournalRecord(MRKJournalKind.StepTransition,
                    FlowId, PlanId, Metadata.PlanRevision, qualified, state.ToString(),
                    Metadata.CorrelationId));
        }
        public IDictionary<string, object> ResultsSnapshot()
        {
            Dictionary<string, object> snapshot = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> item in _results)
                snapshot.Add(item.Key, item.Value is NullResult ? null :
                    item.Value is byte[] ? ((byte[])item.Value).Clone() : item.Value);
            return new ReadOnlyDictionary<string, object>(snapshot);
        }
        internal string[] CompletedOrderSnapshot()
        { return _completedOrder.ToArray(); }
        internal void TrackWork(string stepId, FCRWorkHandle<object> handle)
        {
            string id = _scopePrefix + stepId;
            _handles[id] = handle;
            MRKStepObservation current;
            if (_observations.TryGetValue(id, out current))
                _observations[id] = new MRKStepObservation(id, current.State, handle.Id,
                    current.Reason, current.ChangedUtc,current.InvocationId);
        }
        public IDictionary<string, MRKStepObservation> ObservationsSnapshot()
        {
            IDictionary<string, MRKStepState> current = StatesSnapshot();
            Dictionary<string, MRKStepObservation> output =
                new Dictionary<string, MRKStepObservation>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, MRKStepState> pair in current)
            {
                MRKStepObservation known;
                _observations.TryGetValue(pair.Key, out known);
                FCRWorkHandle<object> handle;
                Guid? workId = _handles.TryGetValue(pair.Key, out handle)
                    ? (Guid?)handle.Id : known == null ? (Guid?)null : known.WorkId;
                output.Add(pair.Key, new MRKStepObservation(pair.Key, pair.Value, workId,
                    known == null ? string.Empty : known.Reason,
                    known == null ? DateTime.UtcNow : known.ChangedUtc,
                    known == null ? null : known.InvocationId));
            }
            return new ReadOnlyDictionary<string, MRKStepObservation>(output);
        }
        /// <summary>快照可辨排队、正在执行与真正异步等待，不仅是节点已调度。</summary>
        public IDictionary<string, MRKStepState> StatesSnapshot()
        {
            Dictionary<string, MRKStepState> states =
                new Dictionary<string, MRKStepState>(_states, StringComparer.Ordinal);
            foreach (KeyValuePair<string, FCRWorkHandle<object>> pair in _handles)
            {
                MRKStepState present;
                if (!states.TryGetValue(pair.Key, out present) ||
                    present != MRKStepState.Scheduled) continue;
                FCRWorkState workState = pair.Value.State;
                if (workState == FCRWorkState.Queued) states[pair.Key] = MRKStepState.Queued;
                else if (workState == FCRWorkState.Running) states[pair.Key] = MRKStepState.Running;
                else if (workState == FCRWorkState.Waiting) states[pair.Key] = MRKStepState.Waiting;
            }
            return new ReadOnlyDictionary<string, MRKStepState>(states);
        }
        private sealed class NullResult
        {
            internal static readonly NullResult Instance = new NullResult();
        }
    }

    public sealed class MRKFlowResult
    {
        internal MRKFlowResult(MRKFlowState state, MRKExecutionContext context, Exception error,
            IDictionary<string, object> committedOutputs = null)
        {
            State = state; FlowId = context.FlowId; Metadata = context.Metadata; Error = error;
            _dataAdapters = context.DataStack.FlowHeap.Adapters;
            CompletedUtc = DateTime.UtcNow;
            Results = context.ResultsSnapshot(); StepStates = context.StatesSnapshot();
            Dictionary<string, object> safe = new Dictionary<string, object>(StringComparer.Ordinal);
            if (state == MRKFlowState.Completed && committedOutputs != null)
                foreach (KeyValuePair<string, object> item in committedOutputs)
                    safe.Add(item.Key, FDTDataValueSafety.Capture(item.Value, _dataAdapters));
            _committedOutputs = new ReadOnlyDictionary<string, object>(safe);
        }
        public MRKFlowState State { get; private set; }
        public Guid FlowId { get; private set; }
        public MRKFlowMetadata Metadata { get; private set; }
        public DateTime CompletedUtc { get; private set; }
        public Exception Error { get; private set; }
        public IDictionary<string, object> Results { get; private set; }
        private readonly IDictionary<string, object> _committedOutputs;
        private readonly FDTDataAdapterRegistry _dataAdapters;
        /// <summary>对外每次返回安全快照，避免ReadOnlyDictionary中可写数组被修改。</summary>
        public IDictionary<string, object> CommittedOutputs
        {
            get
            {
                Dictionary<string, object> safe = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, object> item in _committedOutputs)
                    safe.Add(item.Key, FDTDataValueSafety.Capture(item.Value, _dataAdapters));
                return new ReadOnlyDictionary<string, object>(safe);
            }
        }
        public T GetCommittedOutput<T>(string name)
        {
            if (State != MRKFlowState.Completed)
                throw new InvalidOperationException("流程未成功，不能访问未提交输出");
            object value;
            if (!_committedOutputs.TryGetValue(name, out value))
                throw new KeyNotFoundException("流程输出未声明或未提交：" + name);
            if (value != null && !(value is T))
                throw new InvalidCastException("流程导出类型不匹配：" + name);
            if (value == null && typeof(T).IsValueType &&
                Nullable.GetUnderlyingType(typeof(T)) == null)
                throw new InvalidCastException("非空值类型不能读取null：" + name);
            return (T)FDTDataValueSafety.Capture(value, _dataAdapters);
        }
        public IDictionary<string, MRKStepState> StepStates { get; private set; }
    }

    public sealed class MRKFlowHandle
    {
        private readonly CancellationTokenSource _cancellation;
        private readonly MRKExecutionPlan _originalPlan;
        private readonly MRKExecutionEngine _ownerEngine;
        private readonly DateTime _createdUtc = DateTime.UtcNow;
        private int _cancelRequested;
        private int _compensationStarted;
        private Task<MRKFlowResult> _completion;
        internal MRKFlowHandle(MRKExecutionContext context, CancellationTokenSource cancellation,
            MRKExecutionPlan originalPlan, MRKExecutionEngine ownerEngine)
        { Context = context; _cancellation = cancellation;
          _originalPlan = originalPlan; _ownerEngine = ownerEngine; }
        internal bool IsOriginalPlan(MRKExecutionPlan candidate)
        { return object.ReferenceEquals(_originalPlan, candidate); }
        internal bool IsOwnedBy(MRKExecutionEngine engine)
        { return object.ReferenceEquals(_ownerEngine, engine); }
        public MRKExecutionContext Context { get; private set; }
        public Guid FlowId { get { return Context.FlowId; } }
        public MRKFlowState State
        {
            get
            {
                Task<MRKFlowResult> completion = _completion;
                return completion != null && completion.Status == TaskStatus.RanToCompletion
                    ? completion.Result.State : MRKFlowState.Running;
            }
        }
        public Task<MRKFlowResult> Completion { get { return _completion; } }
        public MRKFlowSnapshot GetSnapshot()
        {
            Task<MRKFlowResult> done = _completion;
            MRKFlowState state = done != null && done.Status == TaskStatus.RanToCompletion
                ? done.Result.State : MRKFlowState.Running;
            IDictionary<string, MRKStepState> steps = Context.StatesSnapshot();
            int completed=0, waiting=0, failed=0, skipped=0;
            foreach (MRKStepState status in steps.Values)
            {
                if (status == MRKStepState.Completed) completed++;
                else if (status == MRKStepState.Failed) failed++;
                else if (status == MRKStepState.Skipped) skipped++;
                else if (status != MRKStepState.Cancelled) waiting++;
            }
            return new MRKFlowSnapshot(FlowId, Context.PlanId, Context.Metadata, state,
                Volatile.Read(ref _cancelRequested) != 0, _createdUtc,
                done != null && done.Status == TaskStatus.RanToCompletion
                    ? (DateTime?)done.Result.CompletedUtc : null,
                steps.Count, completed, waiting, failed, skipped,
                GetRegionStates(_originalPlan,steps));
        }

        private static IDictionary<string,MRKStepState> GetRegionStates(
            MRKExecutionPlan plan,IDictionary<string,MRKStepState> states)
        {
            Dictionary<string,MRKStepState> result=
                new Dictionary<string,MRKStepState>(StringComparer.Ordinal);
            foreach(MRKControlRegion region in plan.Regions)
            {
                MRKStepState[] items=region.Members.Where(states.ContainsKey)
                    .Select(id=>states[id]).ToArray();
                MRKStepState state;
                if(items.Any(s=>s==MRKStepState.Failed||s==MRKStepState.ResultUnknown))
                    state=MRKStepState.Failed;
                else if(items.Any(s=>s==MRKStepState.Cancelled))
                    state=MRKStepState.Cancelled;
                else if(items.Any(s=>s==MRKStepState.Paused))
                    state=MRKStepState.Paused;
                else if(items.Length==region.Members.Count&&
                    items.All(s=>s==MRKStepState.Completed||s==MRKStepState.Skipped))
                    state=items.All(s=>s==MRKStepState.Skipped)?
                        MRKStepState.Skipped:MRKStepState.Completed;
                else if(items.Any(s=>s==MRKStepState.Running||s==MRKStepState.Waiting||
                    s==MRKStepState.WaitingGroup||s==MRKStepState.WaitingResources))
                    state=MRKStepState.Running;
                else state=MRKStepState.WaitingDependencies;
                result.Add(region.Id,state);
            }
            return result;
        }

        internal CancellationToken Token { get { return _cancellation.Token; } }
        internal void SetCompletion(Task<MRKFlowResult> completion) { _completion = completion; }
        public bool RequestCancel()
        {
            if (_completion != null && _completion.IsCompleted) return false;
            try
            {
                Interlocked.Exchange(ref _cancelRequested, 1);
                _cancellation.Cancel();
                return true;
            }
            catch (ObjectDisposedException) { return false; }
        }
        internal bool TryStartCompensation()
        { return Interlocked.CompareExchange(ref _compensationStarted, 1, 0) == 0; }
        internal void Close() { _cancellation.Dispose(); }
    }

    public interface IMRKExecutionEngine
    {
        FCRWorkHandle<T> Submit<T>(Func<CancellationToken, Task<T>> action,
            string serialKey = null, bool droppable = false,
            CancellationToken cancellation = default(CancellationToken));
        /// <summary>等待原F19队列容量，不占执行线程；被接纳后仍通过Handle观察真实终态。</summary>
        Task<FCRWorkHandle<T>> SubmitWhenCapacityAsync<T>(
            Func<CancellationToken, Task<T>> action, string serialKey = null,
            CancellationToken cancellation = default(CancellationToken),
            int priority = 0, long estimatedBytes = 0);
        MRKFlowHandle StartFlow(MRKExecutionPlan plan, IDictionary<string, object> inputs = null,
            CancellationToken cancellation = default(CancellationToken));
        MRKFlowHandle StartFlowWithControl(MRKExecutionPlan plan, MRKExecutionDebugController control,
            IDictionary<string, object> inputs = null,
            CancellationToken cancellation = default(CancellationToken));
        MRKFlowHandle StartFlowWithMetadata(MRKExecutionPlan plan, MRKFlowMetadata metadata,
            IDictionary<string, object> inputs = null,
            CancellationToken cancellation = default(CancellationToken));
        MRKFlowHandle StartFlowWithData(MRKExecutionPlan plan, IFDEExecutionDataScope dataSpace,
            IDictionary<string, object> inputs = null,
            CancellationToken cancellation = default(CancellationToken));
        MRKFlowHandle StartFromSlot(MRKExecutionPlanSlot slot, IDictionary<string, object> inputs = null,
            CancellationToken cancellation = default(CancellationToken));
        FCRWorkerSnapshot GetSnapshot();
        Task StopAsync(bool cancelPending = false);
        Task<MRKCompensationReport> CompensateAsync(MRKExecutionPlan plan, MRKFlowHandle flow,
            CancellationToken cancellation = default(CancellationToken));
    }

    /// <summary>
    /// M30轻量执行闭环：共享F19 Worker，不依赖WPF、不重新持久化Graph、不执行专业设备安全动作。
    /// 当前计划支持DAG顺序/并行/全分支汇合；子流程、调试单步、资源租约为后续专门实现。
    /// </summary>
    public sealed class MRKExecutionEngine : IMRKExecutionEngine
    {
        private readonly FCRWorker _worker;
        private readonly MRKResourceLeaseCoordinator _resources;
        private readonly MRKExecutionGroups _groups;
        private readonly IMRKExecutionJournal _journal;
        private readonly Dictionary<string, FCRDedicatedExecutor> _dedicated;
        private readonly int _traceCapacity;
        private readonly int _maxActiveFlows;
        private readonly long _dataPayloadBudgetBytes;
        private readonly object _lifecycleGate = new object();
        private readonly Dictionary<Guid, MRKFlowHandle> _activeFlows =
            new Dictionary<Guid, MRKFlowHandle>();
        private bool _stopping;
        private bool _stopCancelPending;
        private readonly Dictionary<Guid, TaskCompletionSource<bool>> _activeCompensations =
            new Dictionary<Guid, TaskCompletionSource<bool>>();
        private Task _stopTask;

        /// <summary>
        /// 原MRK Flow内的Stage租约计数。组容量仍由MRKExecutionGroups唯一Owner维护；
        /// 一个Flow的同组Stage节点共享同一个入场Task，最后一个Stage节点结算后释放。
        /// 动态Loop内的Stage在本轮显式拒绝，避免未知迭代数错误计算引用次数。
        /// </summary>
        private sealed class StageLeaseBatch
        {
            private sealed class Slot
            {
                internal readonly object Gate=new object();
                internal int Remaining;
                internal Task<MRKExecutionGroupLease> Admission;
            }
            private readonly MRKExecutionGroups _groups;
            private readonly Dictionary<string, Slot> _slots=
                new Dictionary<string, Slot>(StringComparer.Ordinal);
            internal StageLeaseBatch(MRKExecutionPlan plan, MRKExecutionGroups groups)
            {
                _groups=groups;
                foreach (MRKExecutionStep step in plan.Steps)
                {
                    if (string.IsNullOrWhiteSpace(step.ExecutionGroup) ||
                        groups.GetScope(step.ExecutionGroup)!=MRKExecutionGroupScope.Stage)
                        continue;
                    Slot slot;
                    if (!_slots.TryGetValue(step.ExecutionGroup, out slot))
                    {
                        slot=new Slot();
                        _slots.Add(step.ExecutionGroup,slot);
                    }
                    slot.Remaining++;
                }
            }
            internal async Task EnterAsync(string id, CancellationToken cancellation)
            {
                Slot slot=_slots[id];
                Task<MRKExecutionGroupLease> admission;
                lock(slot.Gate)
                {
                    if(slot.Admission==null)
                        slot.Admission=_groups.AcquireAsync(id,cancellation);
                    admission=slot.Admission;
                }
                await admission.ConfigureAwait(false);
            }
            internal void CompleteStep(string id)
            {
                Slot slot=_slots[id];
                MRKExecutionGroupLease release=null;
                lock(slot.Gate)
                {
                    slot.Remaining--;
                    if(slot.Remaining<0)
                        throw new InvalidOperationException("stage.negative_remaining");
                    if(slot.Remaining==0 && slot.Admission!=null &&
                        slot.Admission.Status==TaskStatus.RanToCompletion)
                        release=slot.Admission.Result;
                }
                if(release!=null)release.Dispose();
            }
        }

        public int ActiveFlowCount
        { get { lock (_lifecycleGate) return _activeFlows.Count; } }
        public MRKExecutionEngine(FCRWorkerOptions options,
            MRKResourceLeaseCoordinator coordinator = null,
            IEnumerable<MRKExecutionGroupDefinition> groups = null,
            IMRKExecutionJournal journal = null,
            IDictionary<string, FCRDedicatedExecutor> dedicatedExecutors = null,
            int traceCapacity = 2048, int maxActiveFlows = int.MaxValue,
            long dataPayloadBudgetBytes = 0, FLDLogService auditLog = null)
        {
            if (maxActiveFlows < 1) throw new ArgumentOutOfRangeException("maxActiveFlows");
            if (dataPayloadBudgetBytes < 0)
                throw new ArgumentOutOfRangeException("dataPayloadBudgetBytes");
            _maxActiveFlows = maxActiveFlows;
            _dataPayloadBudgetBytes = dataPayloadBudgetBytes;
            _worker = new FCRWorker(options, auditLog);
            _resources = coordinator ?? new MRKResourceLeaseCoordinator();
            _groups = new MRKExecutionGroups(groups);
            if (traceCapacity < 1) throw new ArgumentOutOfRangeException("traceCapacity");
            _traceCapacity = traceCapacity;
            _journal = journal;
            _dedicated = new Dictionary<string, FCRDedicatedExecutor>(StringComparer.Ordinal);
            if (dedicatedExecutors != null)
                foreach (KeyValuePair<string, FCRDedicatedExecutor> item in dedicatedExecutors)
                {
                    if (string.IsNullOrWhiteSpace(item.Key) || item.Value == null)
                        throw new ArgumentException("专用执行位置配置无效", "dedicatedExecutors");
                    _dedicated.Add(item.Key, item.Value);
                }
        }
        public IList<MRKExecutionGroupSnapshot> GroupSnapshot() { return _groups.Snapshot(); }
        public Exception AuditFailure { get { return _worker.AuditFailure; } }
        public long AuditedLosses { get { return _worker.AuditedLosses; } }
        public FCRWorkHandle<T> Submit<T>(Func<CancellationToken, Task<T>> action,
            string serialKey = null, bool droppable = false,
            CancellationToken cancellation = default(CancellationToken))
        {
            lock (_lifecycleGate)
            {
                if (_stopping) throw new InvalidOperationException("运行引擎正在停止，不能提交新工作");
                return _worker.Submit(action, serialKey, droppable, cancellation);
            }
        }
        /// <summary>
        /// 开发者异步生产者入口仍依赖唯一F19 Worker，不提前预留运行态或工程数据。
        /// Stop前受理的等待请求由F19返回Accepted/Rejected，绝不越过Runtime生命周期。
        /// </summary>
        public Task<FCRWorkHandle<T>> SubmitWhenCapacityAsync<T>(
            Func<CancellationToken, Task<T>> action, string serialKey = null,
            CancellationToken cancellation = default(CancellationToken),
            int priority = 0, long estimatedBytes = 0)
        {
            lock (_lifecycleGate)
            {
                if (_stopping) throw new InvalidOperationException(
                    "运行引擎正在停止，不能提交新工作");
                return _worker.SubmitWhenCapacityAsync(action, serialKey,
                    cancellation, priority, estimatedBytes);
            }
        }

        /// <summary>
        /// 仅供操作方显式调用：失败流程所有任务完成后，按实际完成逆序补偿。
        /// 自动补偿可能改变真实设备状态，故默认不启用；对外部命令需专业确认后再授权。
        /// </summary>
        public async Task<MRKCompensationReport> CompensateAsync(
            MRKExecutionPlan plan, MRKFlowHandle flow,
            CancellationToken cancellation = default(CancellationToken))
        {
            if (plan == null) throw new ArgumentNullException("plan");
            if (flow == null) throw new ArgumentNullException("flow");
            // 补偿必须由启动Flow的同一个引擎执行，不能绕过原进程内资源仲裁。
            if (!flow.IsOwnedBy(this))
                throw new InvalidOperationException("补偿不能交给其他运行引擎，资源仲裁及任务生命周期不受保护");
            if (!flow.IsOriginalPlan(plan))
                throw new InvalidOperationException("补偿必须使用启动Flow时固定的原始Plan实例，不能使用同名不同版本计划");
            if (!flow.Completion.IsCompleted)
                throw new InvalidOperationException("所有分支结算前禁止补偿");
            MRKFlowResult terminal = await flow.Completion.ConfigureAwait(false);
            if (terminal.State == MRKFlowState.Completed)
                throw new InvalidOperationException("成功流程不允许直接补偿");
            TaskCompletionSource<bool> compensationFinished = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_lifecycleGate)
            {
                if (_stopping)
                    throw new InvalidOperationException("引擎正在停止，禁止开始补偿且不消耗补偿资格");
                if (!flow.TryStartCompensation())
                    throw new InvalidOperationException("同一流程不能重复启动补偿");
                _activeCompensations.Add(flow.FlowId, compensationFinished);
            }
            try
            {
            List<string> succeeded = new List<string>();
            Dictionary<string, Exception> failed =
                new Dictionary<string, Exception>(StringComparer.Ordinal);
            string[] completed = flow.Context.CompletedOrderSnapshot();
            for (int index = completed.Length - 1; index >= 0; index--)
            {
                cancellation.ThrowIfCancellationRequested();
                string stepId = completed[index];
                MRKExecutionStep step = plan.GetStep(stepId);
                if (step.CompensateAsync == null) continue;
                List<MRKResourceLease> leases = new List<MRKResourceLease>();
                try
                {
                    // 原Flow与节点DataScope已按正常生命周期释放，补偿不得读取悬空作用域。
                    // 通过原始Plan的类型化端口重新绑定已结算的Flow输入/节点结果。
                    MRKExecutionContext compensationContext = flow.Context
                        .WithDataScope(null)
                        .WithBoundInputs(MRKExecutionPortBinder.Bind(step, flow.Context));
                    leases.AddRange(await _resources.AcquireManyAsync(
                        step.RequiredResources,
                        flow.FlowId.ToString("N") + ":rollback:" + stepId, cancellation)
                        .ConfigureAwait(false));
                    // 已经获准的补偿必须能在Stop的排空阶段提交后续步骤。
                    // DrainAndStop会等待本次补偿完成，才真正停止底层Worker。
                    FCRWorkHandle<bool> job = _worker.Submit<bool>(async token =>
                    {
                        await step.CompensateAsync(compensationContext, token).ConfigureAwait(false);
                        return true;
                    }, step.SerialKey, false, cancellation);
                    FCRWorkResult<bool> outcome = await job.Completion.ConfigureAwait(false);
                    if (!outcome.Succeeded)
                        throw new InvalidOperationException(
                            "节点补偿未成功：" + stepId, outcome.Error);
                    succeeded.Add(stepId);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                { throw; }
                catch (Exception ex) { failed[stepId] = ex; }
                finally
                {
                    for (int j = leases.Count - 1; j >= 0; j--) leases[j].Dispose();
                }
            }
            return new MRKCompensationReport(succeeded, failed);
            }
            finally
            {
                compensationFinished.TrySetResult(true);
                lock (_lifecycleGate) _activeCompensations.Remove(flow.FlowId);
            }
        }

        public FCRWorkerSnapshot GetSnapshot() { return _worker.GetSnapshot(); }

        /// <summary>停止接单；已获准的补偿完成前不得关闭底层Worker。</summary>
        public Task StopAsync(bool cancelPending = false)
        {
            MRKFlowHandle[] flows;
            Task stop;
            lock (_lifecycleGate)
            {
                _stopping = true;
                if (cancelPending) _stopCancelPending = true;
                flows = _activeFlows.Values.ToArray();
                if (_stopTask == null)
                {
                    Task[] compensationTasks = _activeCompensations.Values
                        .Select(x => (Task)x.Task).ToArray();
                    _stopTask = DrainAndStopAsync(flows, compensationTasks);
                }
                stop = _stopTask;
            }
            if (cancelPending)
                foreach (MRKFlowHandle flow in flows) flow.RequestCancel();
            return stop;
        }
        private async Task DrainAndStopAsync(MRKFlowHandle[] flows,
            Task[] compensations)
        {
            Task[] pending = flows.Select(x => (Task)x.Completion)
                .Concat(compensations).ToArray();
            await Task.WhenAll(pending).ConfigureAwait(false);
            bool cancelPending;
            lock (_lifecycleGate) cancelPending = _stopCancelPending;
            await _worker.StopAsync(cancelPending).ConfigureAwait(false);
        }

        /// <summary>每个Flow固定本次Plan；流结束后才释放旧版本的运行引用。</summary>
        public MRKFlowHandle StartFromSlot(MRKExecutionPlanSlot slot,
            IDictionary<string, object> inputs = null,
            CancellationToken cancellation = default(CancellationToken))
        { return StartFromSlotWithMetadata(slot, null, inputs, cancellation); }

        public MRKFlowHandle StartFromSlotWithMetadata(MRKExecutionPlanSlot slot,
            MRKFlowMetadata metadata, IDictionary<string, object> inputs = null,
            CancellationToken cancellation = default(CancellationToken))
        { return StartFromSlotCore(slot, null, metadata, inputs, cancellation); }

        public MRKFlowHandle StartFromSlotWithControl(MRKExecutionPlanSlot slot,
            MRKExecutionDebugController control, MRKFlowMetadata metadata = null,
            IDictionary<string, object> inputs = null,
            CancellationToken cancellation = default(CancellationToken))
        {
            if (control == null) throw new ArgumentNullException("control");
            return StartFromSlotCore(slot, control, metadata, inputs, cancellation);
        }

        private MRKFlowHandle StartFromSlotCore(MRKExecutionPlanSlot slot,
            MRKExecutionDebugController control, MRKFlowMetadata metadata,
            IDictionary<string, object> inputs, CancellationToken cancellation)
        {
            if (slot == null) throw new ArgumentNullException("slot");
            MRKExecutionPlanLease lease = slot.Capture();
            try
            {
                MRKFlowMetadata resolved = new MRKFlowMetadata(lease.Revision,
                    metadata == null ? null : metadata.BusinessKey,
                    metadata == null ? null : metadata.CorrelationId,
                    metadata == null ? null : metadata.SessionId,
                    metadata == null ? null : metadata.ParentFlowId);
                MRKFlowHandle flow = StartFlowCore(lease.Plan, control, resolved,
                    inputs, cancellation, null);
                Task cleanup = flow.Completion.ContinueWith(
                    completed => lease.Dispose(),
                    CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
                return flow;
            }
            catch
            {
                lease.Dispose();
                throw;
            }
        }

        public MRKFlowHandle StartFlow(MRKExecutionPlan plan, IDictionary<string, object> inputs = null,
            CancellationToken cancellation = default(CancellationToken))
        { return StartFlowCore(plan, null, null, inputs, cancellation, null); }

        public MRKFlowHandle StartFlowWithMetadata(MRKExecutionPlan plan,
            MRKFlowMetadata metadata, IDictionary<string, object> inputs = null,
            CancellationToken cancellation = default(CancellationToken))
        { return StartFlowCore(plan, null, metadata, inputs, cancellation, null); }

        public MRKFlowHandle StartFlowWithControl(MRKExecutionPlan plan,
            MRKExecutionDebugController control, IDictionary<string, object> inputs = null,
            CancellationToken cancellation = default(CancellationToken))
        { return StartFlowCore(plan, control, null, inputs, cancellation, null); }
        /// <summary>运行体系借用M09数据空间；Flow与各节点创建生命周期受控的子作用域。</summary>
        public MRKFlowHandle StartFlowWithData(MRKExecutionPlan plan,
            IFDEExecutionDataScope dataSpace, IDictionary<string, object> inputs = null,
            CancellationToken cancellation = default(CancellationToken))
        {
            if (dataSpace == null) throw new ArgumentNullException("dataSpace");
            return StartFlowCore(plan, null, null, inputs, cancellation, dataSpace);
        }

        private MRKFlowHandle StartFlowCore(MRKExecutionPlan plan,
            MRKExecutionDebugController control, MRKFlowMetadata metadata,
            IDictionary<string, object> inputs, CancellationToken cancellation,
            IFDEExecutionDataScope dataSpace)
        {
            if (plan == null) throw new ArgumentNullException("plan");
            MRKExecutionPortBinder.VerifyFlowInputs(plan, inputs);
            // 包括动态循环内部节点；不能在Flow已经开始对外产生副作用后才发现未知组。
            HashSet<string> wholeFlowGroups = new HashSet<string>(StringComparer.Ordinal);
            ValidateExecutionGroups(plan, wholeFlowGroups);
            lock (_lifecycleGate)
            {
                if (_stopping) throw new InvalidOperationException("运行引擎已经停止接收新流程");
                if (_activeFlows.Count >= _maxActiveFlows)
                    throw new MRKFlowCapacityExceededException(_maxActiveFlows);
                MRKExecutionContext context = new MRKExecutionContext(plan.Id,
                    inputs, metadata, dataSpace, _journal, _traceCapacity,
                    _dataPayloadBudgetBytes == 0 ? null :
                    new MDSDataBudget(_dataPayloadBudgetBytes));
                try
                {
                    if (_journal != null)
                        _journal.Append(new MRKJournalRecord(MRKJournalKind.FlowStarted,
                            context.FlowId, plan.Id, context.Metadata.PlanRevision,
                            "", "Running", context.Metadata.CorrelationId));
                }
                catch
                {
                    context.CloseFlowDataScope();
                    throw;
                }
                CancellationTokenSource linked =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                MRKFlowHandle handle = new MRKFlowHandle(context, linked, plan, this);
                handle.SetCompletion(RunFlowAsync(plan, handle, control));
                _activeFlows.Add(handle.FlowId, handle);
                Task removal = handle.Completion.ContinueWith(
                    completed =>
                    {
                        lock (_lifecycleGate) _activeFlows.Remove(handle.FlowId);
                    }, CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                return handle;
            }
        }

        // 遍历复用的编译Plan及循环体，不构造第二份持久执行图。
        // 各WholeFlow组依字典序获取，以免两个Flow颠倒持有顺序形成等待环。
        private void ValidateExecutionGroups(MRKExecutionPlan plan,
            HashSet<string> wholeFlow, HashSet<string> enclosingStages=null)
        {
            HashSet<string> localStages=new HashSet<string>(StringComparer.Ordinal);
            // 先校验完整Plan，再递归进Loop。动态每轮使用独立Invocation的Stage计数器。
            foreach(MRKExecutionStep step in plan.Steps)
            {
                if(!string.IsNullOrEmpty(step.ExecutionGroup))
                {
                    if(!_groups.Contains(step.ExecutionGroup))
                        throw new ArgumentException("流程引用了未配置执行组：" +
                            step.ExecutionGroup, "plan");
                    MRKExecutionGroupScope scope=_groups.GetScope(step.ExecutionGroup);
                    if(scope==MRKExecutionGroupScope.WholeFlow)
                        wholeFlow.Add(step.ExecutionGroup);
                    else if(scope==MRKExecutionGroupScope.Stage)
                        localStages.Add(step.ExecutionGroup);
                }
                if(!string.IsNullOrWhiteSpace(step.DedicatedExecutorId) &&
                    !_dedicated.ContainsKey(step.DedicatedExecutorId))
                    throw new ArgumentException("流程引用了未配置专用执行位置：" +
                        step.DedicatedExecutorId, "plan");
            }
            // 外层Stage等待内层Stage（即使不同组）可能与另一Flow构成跨组反向占用环。
            // 目前保守拒绝阶段嵌套；子Loop自身没有外层Stage时可逐Invocation使用Stage。
            if(enclosingStages!=null && enclosingStages.Count>0 && localStages.Count>0)
                throw new ArgumentException(
                    "stage.nested_scope_may_deadlock", "plan");
            HashSet<string> activeStages=new HashSet<string>(
                enclosingStages??new HashSet<string>(StringComparer.Ordinal),
                StringComparer.Ordinal);
            activeStages.UnionWith(localStages);
            foreach(MRKExecutionStep step in plan.Steps)
            {
                if(step.LoopRegion!=null)
                    ValidateExecutionGroups(step.LoopRegion.Body,wholeFlow,activeStages);
                if(step.TryRegion!=null)
                {
                    ValidateExecutionGroups(step.TryRegion.Body,wholeFlow,activeStages);
                    if(step.TryRegion.CatchBody!=null)
                        ValidateExecutionGroups(step.TryRegion.CatchBody,wholeFlow,activeStages);
                    if(step.TryRegion.FinallyBody!=null)
                        ValidateExecutionGroups(step.TryRegion.FinallyBody,wholeFlow,activeStages);
                }
            }
        }

        private async Task<MRKFlowResult> RunFlowAsync(MRKExecutionPlan plan, MRKFlowHandle flow, MRKExecutionDebugController control)
        {
            MRKExecutionContext context = flow.Context;
            Dictionary<string, Task<object>> tasks =
                new Dictionary<string, Task<object>>(StringComparer.Ordinal);
            StageLeaseBatch stages=new StageLeaseBatch(plan,_groups);
            MRKCommittedOutputReclaimer reclaimer =
                plan.ReclaimCommittedWhenConsumersSettle
                    ? new MRKCommittedOutputReclaimer(plan, context.DataStack)
                    : null;
            Func<string, Task<object>> schedule = null;
            schedule = id =>
            {
                Task<object> existing;
                if (tasks.TryGetValue(id, out existing)) return existing;
                Task<object> task = RunStepAsync(plan.GetStep(id), context, flow.Token,
                    schedule, control, stages, reclaimer);
                tasks.Add(id, task);
                return task;
            };
            MRKFlowState finalState = MRKFlowState.Completed;
            Exception finalError = null;
            IDictionary<string, object> committedFlowOutputs = null;
            List<MRKExecutionGroupLease> wholeFlowLeases = new List<MRKExecutionGroupLease>();
            try
            {
                // 整个Flow（包括内部Loop）先持有指定组，避免多个Stage/Step间释放席位。
                // 等待不占F19 Worker线程；Cancel/Stop可取消排队的整条Flow。
                HashSet<string> wholeFlow = new HashSet<string>(StringComparer.Ordinal);
                ValidateExecutionGroups(plan, wholeFlow);
                foreach (string groupId in wholeFlow.OrderBy(x => x, StringComparer.Ordinal))
                    wholeFlowLeases.Add(await _groups.AcquireAsync(groupId, flow.Token)
                        .ConfigureAwait(false));
                foreach (MRKExecutionStep step in plan.Steps)
                {
                    Task<object> scheduled = schedule(step.Id);
                }
                await Task.WhenAll(tasks.Values.ToArray()).ConfigureAwait(false);
                flow.Token.ThrowIfCancellationRequested();
                Dictionary<string, object> exports =
                    new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, string> item in plan.DataExports)
                {
                    object value;
                    if (!context.TryGetCommittedData(item.Value, out value))
                        throw new InvalidOperationException("必要流程导出未提交：" + item.Value);
                    exports.Add(item.Key, FDTDataValueSafety.Capture(value, context.DataStack.FlowHeap.Adapters));
                }
                committedFlowOutputs = new ReadOnlyDictionary<string, object>(exports);
            }
            catch (Exception ex)
            {
                finalState = ex is OperationCanceledException && flow.Token.IsCancellationRequested
                    ? MRKFlowState.Cancelled : MRKFlowState.Failed;
                finalError = ex;
            }
            finally
            {
                try { context.CloseFlowDataScope(); }
                catch (Exception ex)
                {
                    finalState = MRKFlowState.Failed;
                    finalError = finalError == null ? ex :
                        new AggregateException("流程失败且数据作用域释放失败", finalError, ex);
                }
                finally { flow.Close(); }
            }
            if (_journal != null)
            {
                try
                {
                    _journal.Append(new MRKJournalRecord(MRKJournalKind.FlowFinished,
                        context.FlowId, plan.Id, context.Metadata.PlanRevision,
                        "", finalState.ToString(), context.Metadata.CorrelationId));
                }
                catch (Exception ex)
                {
                    finalState = MRKFlowState.Failed;
                    finalError = finalError == null ? ex :
                        new AggregateException("流程执行和Journal最终写入同时失败", finalError, ex);
                }
            }
            // Journal写入无论成功失败必须结算。保守做法：真实Flow终态落盘/报错后
            // 才归还WholeFlow许可；非合作底层Work仍由Task.WhenAll等待实际结算。
            try
            {
                return new MRKFlowResult(finalState, context, finalError,
                    finalState == MRKFlowState.Completed ? committedFlowOutputs : null);
            }
            finally
            {
                for (int i = wholeFlowLeases.Count - 1; i >= 0; i--)
                    wholeFlowLeases[i].Dispose();
            }
        }

        /// <summary>统一MRK执行受保护区域；先排空Try内工作，再选择Catch，Finally即使取消亦有界运行。</summary>
        private async Task ExecuteTryRegionAsync(MRKTryRegion region,
            MRKExecutionContext parent,CancellationToken cancellation,
            MRKExecutionDebugController control)
        {
            string prefix=parent.Invocation.StepId.Substring(
                parent.Invocation.StepId.LastIndexOf('.')+1)+".";
            Exception error=null;
            try
            {
                try
                {
                    await ExecuteLoopBodyAsync(region.Body,parent.ForSubflow(prefix+"try."),
                        cancellation,control).ConfigureAwait(false);
                }
                catch(Exception ex)
                {
                    if(cancellation.IsCancellationRequested||region.CatchBody==null)
                        error=ex;
                    else
                    {
                        try
                        {
                            await ExecuteLoopBodyAsync(region.CatchBody,
                                parent.ForSubflow(prefix+"catch."),cancellation,control)
                                .ConfigureAwait(false);
                        }
                        catch(Exception caught){error=caught;}
                    }
                }
            }
            finally
            {
                if(region.FinallyBody!=null)
                {
                    // 受控清理不沿用已取消的Flow令牌；10秒只发出协作取消，
                    // 不承诺强制中止无响应的设备SDK/Worker，须由专业安全层处置。
                    using(var cleanup=new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                    {
                        try
                        {
                            await ExecuteLoopBodyAsync(region.FinallyBody,
                                parent.ForCleanup(prefix+"finally."),cleanup.Token,control)
                                .ConfigureAwait(false);
                        }
                        catch(Exception ex)
                        {
                            error=error==null?ex:
                                new AggregateException("mrk.try.finally_failed",error,ex);
                        }
                    }
                }
            }
            if(error!=null)
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
            cancellation.ThrowIfCancellationRequested();
        }

        private async Task<int> ExecuteLoopRegionAsync(MRKLoopRegion loop,
            MRKExecutionContext parent,CancellationToken cancellation,
            MRKExecutionDebugController control)
        {
            // Foreach 的 MaxIterations 是安全预算，不是允许静默丢弃集合尾部。
            // 在启动任何业务Step前检查；并行与串行必须具有相同的全集语义。
            int collectionCount=-1;
            if(!string.IsNullOrEmpty(loop.CollectionInput))
            {
                object source;
                if(!parent.Inputs.TryGetValue(loop.CollectionInput,out source)||
                    !(source is System.Collections.IList))
                    throw new InvalidOperationException("moc.foreach_requires_IList:"+
                        loop.CollectionInput);
                collectionCount=((System.Collections.IList)source).Count;
                if(collectionCount>loop.MaxIterations)
                    throw new InvalidOperationException("moc.foreach_budget_exceeded:"+
                        collectionCount+"/"+loop.MaxIterations);
            }
            int count=0;
            MRKExecutionContext predicateContext=parent;
            for(int iteration=0;iteration<loop.MaxIterations;iteration++)
            {
                cancellation.ThrowIfCancellationRequested();
                if(parent.FlowReturnRequested)break;
                if(collectionCount>=0&&iteration>=collectionCount)break;
                if(!(iteration==0&&loop.ExecuteFirst)&&loop.Condition!=null&&
                   !loop.Condition(predicateContext,iteration))break;
                // 每轮只有一份编译Plan，任务/输出采用不同限定路径。
                MRKExecutionContext scope=parent.ForLoopIteration(
                    parent.Invocation.StepId.Substring(parent.Invocation.StepId.LastIndexOf('.')+1)+
                    ".i"+iteration.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)+".",iteration);
                await ExecuteLoopBodyAsync(loop.Body,scope,cancellation,control,
                    loop.DataRetention).ConfigureAwait(false);
                count++;
                predicateContext=scope;
                if(scope.LoopExit==1||scope.LoopExit==3||scope.FlowReturnRequested)break;
                // Continue 等到本轮已有Work结算并释放资源后再开始下一轮。
            }
            return count;
        }

        /// <summary>各迭代独立DAG和Frame，真正能力仍只使用原F19 Worker。</summary>
        private async Task ExecuteLoopBodyAsync(MRKExecutionPlan body,
            MRKExecutionContext scope, CancellationToken cancellation,
            MRKExecutionDebugController control,
            MRKLoopDataRetention retention=null)
        {
            Dictionary<string,Task<object>> tasks=
                new Dictionary<string,Task<object>>(StringComparer.Ordinal);
            // 每个真实Iteration/Invocation建立自己的Stage引用计数，不按MaxIterations展开Plan。
            StageLeaseBatch stages=new StageLeaseBatch(body,_groups);
            Func<string,Task<object>> schedule=null;
            schedule=id=>
            {
                Task<object> existing;
                if(tasks.TryGetValue(id,out existing))return existing;
                Task<object> current=RunStepAsync(body.GetStep(id),scope,
                    cancellation,schedule,control,stages);
                tasks.Add(id,current);
                return current;
            };
            foreach(MRKExecutionStep item in body.Steps)schedule(item.Id);
            try
            {
                await Task.WhenAll(tasks.Values.ToArray()).ConfigureAwait(false);
            }
            finally
            {
                // Task.WhenAll已结算本Invocation的全部已启动节点；其MDSDataFrame
                // 已经Dispose。只释放本轮未显式保留的输出，不影响其它轮或Join。
                if(retention!=null&&!retention.KeepAllOutputs)
                    foreach(MRKExecutionStep item in body.Steps)
                        if(!retention.Retains(item.Id))
                            scope.DataStack.ReleaseCommittedOutputs(
                                scope.QualifiedStepId(item.Id));
            }
        }

        /// <summary>有界并行Foreach：失败仅协作取消，仍排空所有实际执行的迭代。</summary>
        private async Task<int> ExecuteParallelForeachAsync(MRKLoopRegion loop,
            MRKExecutionContext parent,CancellationToken cancellation,
            MRKExecutionDebugController control)
        {
            object source;
            if(!parent.Inputs.TryGetValue(loop.CollectionInput,out source)||
                !(source is System.Collections.IList))
                throw new InvalidOperationException("moc.foreach_requires_IList:"+
                    loop.CollectionInput);
            int count=((System.Collections.IList)source).Count;
            if(count>loop.MaxIterations)
                throw new InvalidOperationException("moc.foreach_budget_exceeded:"+
                    count+"/"+loop.MaxIterations);
            string step=parent.Invocation.StepId.Substring(
                parent.Invocation.StepId.LastIndexOf('.')+1);
            using(CancellationTokenSource linked=
                CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                List<Task> active=new List<Task>();
                int next=0;
                try
                {
                    while(next<count||active.Count>0)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        while(next<count&&active.Count<loop.MaxParallelIterations)
                        {
                            MRKExecutionContext scope=parent.ForLoopIteration(
                                step+".i"+next.ToString(
                                    System.Globalization.CultureInfo.InvariantCulture)+".",
                                next,true);
                            active.Add(ExecuteLoopBodyAsync(loop.Body,scope,linked.Token,control,
                                loop.DataRetention));
                            next++;
                        }
                        Task finished=await Task.WhenAny(active).ConfigureAwait(false);
                        await finished.ConfigureAwait(false);
                        active.Remove(finished);
                    }
                    cancellation.ThrowIfCancellationRequested();
                    return count;
                }
                catch
                {
                    linked.Cancel();
                    try{await Task.WhenAll(active).ConfigureAwait(false);}
                    catch{ /* 其它分支错误保留于原节点Trace；传播第一个触发的异常。 */ }
                    throw;
                }
            }
        }

        private async Task<object> RunStepAsync(MRKExecutionStep step, MRKExecutionContext context,
            CancellationToken cancellation, Func<string, Task<object>> schedule,
            MRKExecutionDebugController control, StageLeaseBatch stages=null,
            MRKCommittedOutputReclaimer reclaimer=null)
        {
            bool dependencyFailed = false;
            try
            {
                // 运行身份与WaitingDependencies状态本身也可能写Journal失败。
                // 不能在Stage的生命周期finally之外执行它们，否则Stage席位可能泄露。
                context=context.WithInvocation(new MRKInvocationContext(context.FlowId,
                    context.QualifiedStepId(step.Id),0,
                    context.Invocation==null?null:context.Invocation.InvocationId));
                context.SetState(step.Id, MRKStepState.WaitingDependencies,
                    string.Join(",", step.Dependencies));
                List<Task<object>> dependencies = new List<Task<object>>();
                foreach (string id in step.Dependencies) dependencies.Add(schedule(id));
                try { await Task.WhenAll(dependencies).ConfigureAwait(false); }
                catch
                {
                    dependencyFailed = !cancellation.IsCancellationRequested;
                    throw;
                }
                cancellation.ThrowIfCancellationRequested();
                if(!context.IsCleanupRegion&&
                    (context.LoopExit!=0||context.FlowReturnRequested))
                {
                    context.SetState(step.Id,MRKStepState.Skipped,"loop.exit_requested");
                    return null;
                }
                if (!step.AllowSkippedDependencies)
                {
                    foreach (string dependency in step.Dependencies)
                    {
                        MRKStepState previous;
                        if (context.TryGetScopedState(dependency, out previous) &&
                            previous == MRKStepState.Skipped)
                        {
                            context.SetState(step.Id, MRKStepState.Skipped,
                                "前置分支跳过：" + dependency);
                            return null;
                        }
                    }
                }
                if (step.Condition != null && !step.Condition(context))
                {
                    context.SetState(step.Id, MRKStepState.Skipped, "条件为false");
                    return null;
                }
                if (control != null)
                {
                    Task checkpoint = control.WaitBeforeAsync(step.Id,
                        context.QualifiedStepId(step.Id), cancellation);
                    if (!checkpoint.IsCompleted) context.SetState(step.Id, MRKStepState.Paused, "breakpoint");
                    await checkpoint.ConfigureAwait(false);
                }
                if(step.TryRegion!=null)
                {
                    context.SetState(step.Id,MRKStepState.Running,"try.region");
                    await ExecuteTryRegionAsync(step.TryRegion,context,cancellation,control)
                        .ConfigureAwait(false);
                    context.SetResult(step.Id,true);
                    context.SetState(step.Id,MRKStepState.Completed,"try.completed");
                    return true;
                }
                if(step.LoopRegion!=null)
                {
                    context.RegisterLoopRetention(step.Id, step.LoopRegion.DataRetention);
                    // 控制区域自身不占Worker槽位：只让真正的Body Capability
                    // 通过同一F19 Worker运行，避免单线程Worker上的自等待死锁。
                    context.SetState(step.Id,MRKStepState.Running,"loop.region");
                    int completed=step.LoopRegion.MaxParallelIterations>1
                        ?await ExecuteParallelForeachAsync(step.LoopRegion,context,
                            cancellation,control).ConfigureAwait(false)
                        :await ExecuteLoopRegionAsync(step.LoopRegion,context,
                            cancellation,control).ConfigureAwait(false);
                    context.SetResult(step.Id,completed);
                    context.SetState(step.Id,MRKStepState.Completed,"loop.completed");
                    return completed;
                }
                // 在占用Worker槽位之前等资源，避免等锁的节点堵住真正持锁的执行任务。
                List<MRKResourceLease> leases = new List<MRKResourceLease>();
                MRKExecutionGroupLease groupLease = null;
                IFDEExecutionDataScope localData = null;
                MDSDataFrame dataFrame = null;
                try
                {
                    if (!string.IsNullOrWhiteSpace(step.ExecutionGroup) &&
                        _groups.GetScope(step.ExecutionGroup) == MRKExecutionGroupScope.Step)
                    {
                        context.SetState(step.Id, MRKStepState.WaitingGroup, step.ExecutionGroup);
                        groupLease = await _groups.AcquireAsync(step.ExecutionGroup, cancellation)
                            .ConfigureAwait(false);
                    }
                    else if (!string.IsNullOrWhiteSpace(step.ExecutionGroup) &&
                        _groups.GetScope(step.ExecutionGroup)==MRKExecutionGroupScope.Stage)
                    {
                        // 本阶段内的后续节点共用已经获取的许可证，不自等待第二次。
                        // 只在所有同Stage节点结算（含Skipped/Cancelled）后才释放。
                        if(stages==null)throw new InvalidOperationException(
                            "stage.flow_batch_required");
                        context.SetState(step.Id,MRKStepState.WaitingGroup,step.ExecutionGroup);
                        await stages.EnterAsync(step.ExecutionGroup,cancellation)
                            .ConfigureAwait(false);
                    }
                    if (step.RequiredResources.Count > 0)
                        context.SetState(step.Id, MRKStepState.WaitingResources,
                            string.Join(",", step.RequiredResources));
                    leases.AddRange(await _resources.AcquireManyAsync(
                        step.RequiredResources,
                        context.FlowId.ToString("N") + ":" + step.Id, cancellation)
                        .ConfigureAwait(false));

                    MRKExecutionContext nodeContext = context.WithBoundInputs(
                        MRKExecutionPortBinder.Bind(step, context));
                    // 运行数据Scope只建一次：新DataContext和原有DataScope共用同一实例。
                    bool useManagedData = step.EnableDataContext;
                    foreach (MRKExecutionInput input in step.Inputs)
                        if (input.Source == MRKInputSource.StepOutput) useManagedData = true;
                    if (useManagedData || context.DataScope != null)
                    {
                        localData = context.DataStack.RootScope.CreateChild(context.Invocation.FrameKey);
                        // 新Data入口不可通过旧DataScope越权读取父级Local或拿到可写别名。
                        nodeContext = nodeContext.WithDataScope(useManagedData ? null : localData);
                    }
                    if (useManagedData)
                    {
                        dataFrame = context.DataStack.OpenFrame(context.Invocation.FrameKey, localData,
                            nodeContext.BoundInputs, step.DataOutputs);
                        nodeContext = nodeContext.WithDataContext(dataFrame.Data);
                    }
                    Func<MRKExecutionContext, CancellationToken, Task<object>> operation =
                        string.IsNullOrEmpty(step.DedicatedExecutorId)
                        ? step.ExecuteAsync
                        : (Func<MRKExecutionContext, CancellationToken, Task<object>>)
                            ((data, token) => _dedicated[step.DedicatedExecutorId]
                                .InvokeAsync(inner => step.SynchronousSdk(data, inner), token));
                    FCRWorkHandle<object> work = _worker.Submit<object>(
                        ct => step.Policy == null ? operation(nodeContext, ct) :
                            step.Policy.ExecuteAsync(nodeContext, operation, ct,
                                () => context.SetState(step.Id, MRKStepState.ResultUnknown,
                                    "策略超时但原操作尚未结算，资源继续占用")),
                        step.SerialKey, false, cancellation,
                        step.Priority, step.EstimatedPendingBytes);
                    context.SetState(step.Id, MRKStepState.Scheduled);
                    context.TrackWork(step.Id, work);
                    FCRWorkResult<object> result = await work.Completion.ConfigureAwait(false);
                    if (result.State == FCRWorkState.Cancelled)
                        throw new OperationCanceledException("Work cancelled", cancellation);
                    if (!result.Succeeded)
                        throw new InvalidOperationException("STEP_" + step.Id + "_" + result.State, result.Error);
                    // 即使不合作Provider在取消后迟到返回成功，本Flow必须阻断
                    // 其Pending输出提交，不可污染同Plan的其他Flow/Invocation。
                    cancellation.ThrowIfCancellationRequested();
                    MRKExecutionPortBinder.VerifyOutput(step, result.Value);
                    // 所有具名Output先验证、整体发布，再让依赖任务观察节点Completed。
                    if (dataFrame != null) dataFrame.CommitOutputs();
                    context.SetResult(step.Id, result.Value);
                    context.SetState(step.Id, MRKStepState.Completed);
                    return result.Value;
                }
                finally
                {
                    try
                    {
                        if (dataFrame != null) dataFrame.Dispose();
                        else if (localData != null) localData.Dispose();
                    }
                    finally
                    {
                        for (int i = leases.Count - 1; i >= 0; i--) leases[i].Dispose();
                        if (groupLease != null) groupLease.Dispose();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                context.SetState(step.Id,
                    dependencyFailed ? MRKStepState.Skipped : MRKStepState.Cancelled,
                    dependencyFailed ? "依赖未成功" : "取消已确认");
                throw;
            }
            catch (Exception ex)
            {
                context.SetState(step.Id,
                    dependencyFailed ? MRKStepState.Skipped : MRKStepState.Failed,
                    dependencyFailed ? "依赖未成功" : ex.GetType().Name);
                throw;
            }
            finally
            {
                try
                {
                    if(stages!=null && !string.IsNullOrWhiteSpace(step.ExecutionGroup) &&
                        _groups.GetScope(step.ExecutionGroup)==MRKExecutionGroupScope.Stage)
                        stages.CompleteStep(step.ExecutionGroup);
                }
                finally
                {
                    // 任何结果都属于终态：成功、Skipped、Failed、Cancelled。
                    // Frame已经关闭且输入消费者业务执行结束后才结算资源。
                    if (reclaimer != null) reclaimer.StepSettled(step.Id);
                }
            }
        }
    }
}
