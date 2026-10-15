using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Infrastructure.Concurrency;

namespace CxVisual.Mechanism.Runtime.Execution
{
    public enum MRKFlowState { Running, Completed, Failed, Cancelled }
    public enum MRKStepState { WaitingDependencies, Scheduled, Completed, Failed, Cancelled, Queued, Running, Waiting, Paused, WaitingResources, WaitingGroup }

    /// <summary>同一次流程的局部快照；不对外暴露可写的全局数据字典。</summary>
    public sealed class MRKExecutionContext
    {
        private readonly ConcurrentDictionary<string, object> _results =
            new ConcurrentDictionary<string, object>(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, FCRWorkHandle<object>> _handles =
            new ConcurrentDictionary<string, FCRWorkHandle<object>>(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, MRKStepState> _states =
            new ConcurrentDictionary<string, MRKStepState>(StringComparer.Ordinal);
        private readonly ReadOnlyDictionary<string, object> _inputs;
        private readonly string _scopePrefix;
        private readonly MRKExecutionTrace _trace;
        private readonly IDictionary<string, object> _boundInputs;
        private readonly ConcurrentQueue<string> _completedOrder = new ConcurrentQueue<string>();
        internal MRKExecutionContext(string planId, IDictionary<string, object> inputs)
        {
            FlowId = Guid.NewGuid();
            PlanId = planId;
            _scopePrefix = string.Empty;
            _trace = new MRKExecutionTrace();
            _boundInputs = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());
            _inputs = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>(
                inputs ?? new Dictionary<string, object>(), StringComparer.Ordinal));
        }
        private MRKExecutionContext(MRKExecutionContext parent, string prefix)
        {
            FlowId = parent.FlowId;
            PlanId = parent.PlanId;
            _results = parent._results;
            _states = parent._states;
            _handles = parent._handles;
            _inputs = parent._inputs;
            _scopePrefix = parent._scopePrefix + prefix;
            _trace = parent._trace;
            _boundInputs = parent._boundInputs;
            _completedOrder = parent._completedOrder;
        }
        internal MRKExecutionContext ForSubflow(string prefix)
        { return new MRKExecutionContext(this, prefix); }
        private MRKExecutionContext(MRKExecutionContext parent, IDictionary<string, object> ports)
            : this(parent, string.Empty)
        { _boundInputs = ports; }
        internal MRKExecutionContext WithBoundInputs(IDictionary<string, object> ports)
        { return new MRKExecutionContext(this, ports); }
        public IDictionary<string, object> BoundInputs { get { return _boundInputs; } }
        public bool TryGetBoundInput<T>(string name, out T value)
        {
            object data;
            if (_boundInputs.TryGetValue(name, out data) && data is T)
            { value = (T)data; return true; }
            value = default(T);
            return false;
        }
        /// <summary>跨子流程结果必须显式使用完整路径，防止名字碰撞。</summary>
        public bool TryGetAbsoluteResult(string absoluteStepId, out object result)
        {
            bool found = _results.TryGetValue(absoluteStepId, out result);
            if (result is NullResult) result = null;
            return found;
        }
        public Guid FlowId { get; private set; }
        public string PlanId { get; private set; }
        public MRKExecutionTrace Trace { get { return _trace; } }
        public IDictionary<string, object> Inputs { get { return _inputs; } }
        public bool TryGetResult(string stepId, out object result)
        {
            bool found = _results.TryGetValue(_scopePrefix + stepId, out result);
            if (result is NullResult) result = null;
            return found;
        }
        internal void SetResult(string stepId, object result)
        { _results[_scopePrefix + stepId] = result ?? NullResult.Instance; }
        internal void SetState(string stepId, MRKStepState state)
        {
            string qualified = _scopePrefix + stepId;
            _states[qualified] = state;
            if (state == MRKStepState.Completed) _completedOrder.Enqueue(qualified);
            _trace.Record(qualified, state);
        }
        public IDictionary<string, object> ResultsSnapshot()
        {
            Dictionary<string, object> snapshot = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> item in _results)
                snapshot.Add(item.Key, item.Value is NullResult ? null : item.Value);
            return new ReadOnlyDictionary<string, object>(snapshot);
        }
        internal string[] CompletedOrderSnapshot()
        { return _completedOrder.ToArray(); }
        internal void TrackWork(string stepId, FCRWorkHandle<object> handle)
        { _handles[_scopePrefix + stepId] = handle; }
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
        internal MRKFlowResult(MRKFlowState state, MRKExecutionContext context, Exception error)
        { State = state; FlowId = context.FlowId; Error = error;
          Results = context.ResultsSnapshot(); StepStates = context.StatesSnapshot(); }
        public MRKFlowState State { get; private set; }
        public Guid FlowId { get; private set; }
        public Exception Error { get; private set; }
        public IDictionary<string, object> Results { get; private set; }
        public IDictionary<string, MRKStepState> StepStates { get; private set; }
    }

    public sealed class MRKFlowHandle
    {
        private readonly CancellationTokenSource _cancellation;
        private int _compensationStarted;
        private Task<MRKFlowResult> _completion;
        internal MRKFlowHandle(MRKExecutionContext context, CancellationTokenSource cancellation)
        { Context = context; _cancellation = cancellation; }
        public MRKExecutionContext Context { get; private set; }
        public Guid FlowId { get { return Context.FlowId; } }
        public Task<MRKFlowResult> Completion { get { return _completion; } }
        internal CancellationToken Token { get { return _cancellation.Token; } }
        internal void SetCompletion(Task<MRKFlowResult> completion) { _completion = completion; }
        public bool RequestCancel()
        {
            if (_completion != null && _completion.IsCompleted) return false;
            try { _cancellation.Cancel(); return true; }
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
        MRKFlowHandle StartFlow(MRKExecutionPlan plan, IDictionary<string, object> inputs = null,
            CancellationToken cancellation = default(CancellationToken));
        MRKFlowHandle StartFlowWithControl(MRKExecutionPlan plan, MRKExecutionDebugController control,
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
        private readonly object _lifecycleGate = new object();
        private readonly Dictionary<Guid, MRKFlowHandle> _activeFlows =
            new Dictionary<Guid, MRKFlowHandle>();
        private bool _stopping;
        private Task _stopTask;
        public int ActiveFlowCount
        { get { lock (_lifecycleGate) return _activeFlows.Count; } }
        public MRKExecutionEngine(FCRWorkerOptions options,
            MRKResourceLeaseCoordinator coordinator = null,
            IEnumerable<MRKExecutionGroupDefinition> groups = null)
        {
            _worker = new FCRWorker(options);
            _resources = coordinator ?? new MRKResourceLeaseCoordinator();
            _groups = new MRKExecutionGroups(groups);
        }
        public IList<MRKExecutionGroupSnapshot> GroupSnapshot() { return _groups.Snapshot(); }
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
        /// 仅供操作方显式调用：失败流程所有任务完成后，按实际完成逆序补偿。
        /// 自动补偿可能改变真实设备状态，故默认不启用；对外部命令需专业确认后再授权。
        /// </summary>
        public async Task<MRKCompensationReport> CompensateAsync(
            MRKExecutionPlan plan, MRKFlowHandle flow,
            CancellationToken cancellation = default(CancellationToken))
        {
            if (plan == null) throw new ArgumentNullException("plan");
            if (flow == null) throw new ArgumentNullException("flow");
            if (!string.Equals(plan.Id, flow.Context.PlanId, StringComparison.Ordinal))
                throw new InvalidOperationException("补偿计划与当前Flow不匹配");
            if (!flow.Completion.IsCompleted)
                throw new InvalidOperationException("所有分支结算前禁止补偿");
            MRKFlowResult terminal = await flow.Completion.ConfigureAwait(false);
            if (terminal.State == MRKFlowState.Completed)
                throw new InvalidOperationException("成功流程不允许直接补偿");
            if (!flow.TryStartCompensation())
                throw new InvalidOperationException("同一流程不能重复启动补偿");

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
                    foreach (string resourceId in step.RequiredResources)
                        leases.Add(await _resources.AcquireAsync(resourceId,
                            flow.FlowId.ToString("N") + ":rollback:" + stepId, cancellation)
                            .ConfigureAwait(false));
                    FCRWorkHandle<bool> job = Submit<bool>(async token =>
                    {
                        await step.CompensateAsync(flow.Context, token).ConfigureAwait(false);
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

        public FCRWorkerSnapshot GetSnapshot() { return _worker.GetSnapshot(); }

        /// <summary>停止接单，等待所有流程结算；cancelPending=true时请求协作取消并清空待执行工作。</summary>
        public Task StopAsync(bool cancelPending = false)
        {
            MRKFlowHandle[] flows;
            lock (_lifecycleGate)
            {
                _stopping = true;
                flows = _activeFlows.Values.ToArray();
                if (_stopTask == null) _stopTask = DrainAndStopAsync(flows, cancelPending);
            }
            if (cancelPending)
            {
                foreach (MRKFlowHandle flow in flows) flow.RequestCancel();
                Task ignored = _worker.StopAsync(true);
            }
            return _stopTask;
        }
        private async Task DrainAndStopAsync(MRKFlowHandle[] flows, bool cancelPending)
        {
            if (cancelPending)
                foreach (MRKFlowHandle flow in flows) flow.RequestCancel();
            if (cancelPending) { Task cancellation = _worker.StopAsync(true); }
            await Task.WhenAll(flows.Select(x => (Task)x.Completion)).ConfigureAwait(false);
            await _worker.StopAsync(false).ConfigureAwait(false);
        }

        /// <summary>每个Flow固定本次Plan；流结束后才释放旧版本的运行引用。</summary>
        public MRKFlowHandle StartFromSlot(MRKExecutionPlanSlot slot,
            IDictionary<string, object> inputs = null,
            CancellationToken cancellation = default(CancellationToken))
        {
            if (slot == null) throw new ArgumentNullException("slot");
            MRKExecutionPlanLease lease = slot.Capture();
            try
            {
                MRKFlowHandle flow = StartFlow(lease.Plan, inputs, cancellation);
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
        { return StartFlowWithControl(plan, null, inputs, cancellation); }

        public MRKFlowHandle StartFlowWithControl(MRKExecutionPlan plan,
            MRKExecutionDebugController control, IDictionary<string, object> inputs = null,
            CancellationToken cancellation = default(CancellationToken))
        {
            if (plan == null) throw new ArgumentNullException("plan");
            MRKExecutionPortBinder.VerifyFlowInputs(plan, inputs);
            foreach (MRKExecutionStep candidate in plan.Steps)
                if (!string.IsNullOrEmpty(candidate.ExecutionGroup) &&
                    !_groups.Contains(candidate.ExecutionGroup))
                    throw new ArgumentException("流程引用了未配置执行组：" + candidate.ExecutionGroup,
                        "plan");
            lock (_lifecycleGate)
            {
                if (_stopping) throw new InvalidOperationException("运行引擎已经停止接收新流程");
                MRKExecutionContext context = new MRKExecutionContext(plan.Id, inputs);
                CancellationTokenSource linked =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                MRKFlowHandle handle = new MRKFlowHandle(context, linked);
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

        private async Task<MRKFlowResult> RunFlowAsync(MRKExecutionPlan plan, MRKFlowHandle flow, MRKExecutionDebugController control)
        {
            MRKExecutionContext context = flow.Context;
            Dictionary<string, Task<object>> tasks =
                new Dictionary<string, Task<object>>(StringComparer.Ordinal);
            Func<string, Task<object>> schedule = null;
            schedule = id =>
            {
                Task<object> existing;
                if (tasks.TryGetValue(id, out existing)) return existing;
                Task<object> task = RunStepAsync(plan.GetStep(id), context, flow.Token, schedule, control);
                tasks.Add(id, task);
                return task;
            };
            try
            {
                foreach (MRKExecutionStep step in plan.Steps)
                {
                    Task<object> scheduled = schedule(step.Id);
                }
                await Task.WhenAll(tasks.Values.ToArray()).ConfigureAwait(false);
                return new MRKFlowResult(MRKFlowState.Completed, context, null);
            }
            catch (Exception ex)
            {
                MRKFlowState state = ex is OperationCanceledException && flow.Token.IsCancellationRequested
                    ? MRKFlowState.Cancelled : MRKFlowState.Failed;
                return new MRKFlowResult(state, context, ex);
            }
            finally { flow.Close(); }
        }

        private async Task<object> RunStepAsync(MRKExecutionStep step, MRKExecutionContext context,
            CancellationToken cancellation, Func<string, Task<object>> schedule, MRKExecutionDebugController control)
        {
            context.SetState(step.Id, MRKStepState.WaitingDependencies);
            try
            {
                List<Task<object>> dependencies = new List<Task<object>>();
                foreach (string id in step.Dependencies) dependencies.Add(schedule(id));
                await Task.WhenAll(dependencies).ConfigureAwait(false);
                cancellation.ThrowIfCancellationRequested();
                if (control != null)
                {
                    Task checkpoint = control.WaitBeforeAsync(step.Id, cancellation);
                    if (!checkpoint.IsCompleted) context.SetState(step.Id, MRKStepState.Paused);
                    await checkpoint.ConfigureAwait(false);
                }
                // 在占用Worker槽位之前等资源，避免等锁的节点堵住真正持锁的执行任务。
                List<MRKResourceLease> leases = new List<MRKResourceLease>();
                MRKExecutionGroupLease groupLease = null;
                try
                {
                    if (!string.IsNullOrWhiteSpace(step.ExecutionGroup))
                    {
                        context.SetState(step.Id, MRKStepState.WaitingGroup);
                        groupLease = await _groups.AcquireAsync(step.ExecutionGroup, cancellation)
                            .ConfigureAwait(false);
                    }
                    if (step.RequiredResources.Count > 0)
                        context.SetState(step.Id, MRKStepState.WaitingResources);
                    foreach (string resourceId in step.RequiredResources)
                        leases.Add(await _resources.AcquireAsync(resourceId,
                            context.FlowId.ToString("N") + ":" + step.Id, cancellation)
                            .ConfigureAwait(false));

                    MRKExecutionContext nodeContext = context.WithBoundInputs(
                        MRKExecutionPortBinder.Bind(step, context));
                    FCRWorkHandle<object> work = _worker.Submit<object>(
                        ct => step.Policy == null ? step.ExecuteAsync(nodeContext, ct) :
                            step.Policy.ExecuteAsync(nodeContext, step.ExecuteAsync, ct),
                        step.SerialKey, false, cancellation);
                    context.SetState(step.Id, MRKStepState.Scheduled);
                    context.TrackWork(step.Id, work);
                    FCRWorkResult<object> result = await work.Completion.ConfigureAwait(false);
                    if (result.State == FCRWorkState.Cancelled)
                        throw new OperationCanceledException("Work cancelled", cancellation);
                    if (!result.Succeeded)
                        throw new InvalidOperationException("STEP_" + step.Id + "_" + result.State, result.Error);
                    MRKExecutionPortBinder.VerifyOutput(step, result.Value);
                    context.SetResult(step.Id, result.Value);
                    context.SetState(step.Id, MRKStepState.Completed);
                    return result.Value;
                }
                finally
                {
                    for (int i = leases.Count - 1; i >= 0; i--) leases[i].Dispose();
                    if (groupLease != null) groupLease.Dispose();
                }
            }
            catch (OperationCanceledException)
            {
                context.SetState(step.Id, MRKStepState.Cancelled);
                throw;
            }
            catch (Exception)
            {
                context.SetState(step.Id, MRKStepState.Failed);
                throw;
            }
        }
    }
}
