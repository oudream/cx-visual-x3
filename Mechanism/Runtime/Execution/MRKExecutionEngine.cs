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
    public enum MRKStepState { WaitingDependencies, Scheduled, Completed, Failed, Cancelled }

    /// <summary>同一次流程的局部快照；不对外暴露可写的全局数据字典。</summary>
    public sealed class MRKExecutionContext
    {
        private readonly ConcurrentDictionary<string, object> _results =
            new ConcurrentDictionary<string, object>(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, MRKStepState> _states =
            new ConcurrentDictionary<string, MRKStepState>(StringComparer.Ordinal);
        private readonly ReadOnlyDictionary<string, object> _inputs;
        internal MRKExecutionContext(string planId, IDictionary<string, object> inputs)
        {
            FlowId = Guid.NewGuid();
            PlanId = planId;
            _inputs = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>(
                inputs ?? new Dictionary<string, object>(), StringComparer.Ordinal));
        }
        public Guid FlowId { get; private set; }
        public string PlanId { get; private set; }
        public IDictionary<string, object> Inputs { get { return _inputs; } }
        public bool TryGetResult(string stepId, out object result)
        { return _results.TryGetValue(stepId, out result); }
        internal void SetResult(string stepId, object result)
        { _results[stepId] = result ?? NullResult.Instance; }
        internal void SetState(string stepId, MRKStepState state) { _states[stepId] = state; }
        public IDictionary<string, object> ResultsSnapshot()
        {
            Dictionary<string, object> snapshot = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> item in _results)
                snapshot.Add(item.Key, item.Value is NullResult ? null : item.Value);
            return new ReadOnlyDictionary<string, object>(snapshot);
        }
        public IDictionary<string, MRKStepState> StatesSnapshot()
        { return new ReadOnlyDictionary<string, MRKStepState>(
            new Dictionary<string, MRKStepState>(_states, StringComparer.Ordinal)); }
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
        internal void Close() { _cancellation.Dispose(); }
    }

    public interface IMRKExecutionEngine
    {
        FCRWorkHandle<T> Submit<T>(Func<CancellationToken, Task<T>> action,
            string serialKey = null, bool droppable = false,
            CancellationToken cancellation = default(CancellationToken));
        MRKFlowHandle StartFlow(MRKExecutionPlan plan, IDictionary<string, object> inputs = null,
            CancellationToken cancellation = default(CancellationToken));
        FCRWorkerSnapshot GetSnapshot();
        Task StopAsync(bool cancelPending = false);
    }

    /// <summary>
    /// M30轻量执行闭环：共享F19 Worker，不依赖WPF、不重新持久化Graph、不执行专业设备安全动作。
    /// 当前计划支持DAG顺序/并行/全分支汇合；子流程、调试单步、资源租约为后续专门实现。
    /// </summary>
    public sealed class MRKExecutionEngine : IMRKExecutionEngine
    {
        private readonly FCRWorker _worker;
        public MRKExecutionEngine(FCRWorkerOptions options)
        { _worker = new FCRWorker(options); }
        public FCRWorkHandle<T> Submit<T>(Func<CancellationToken, Task<T>> action,
            string serialKey = null, bool droppable = false,
            CancellationToken cancellation = default(CancellationToken))
        { return _worker.Submit(action, serialKey, droppable, cancellation); }
        public FCRWorkerSnapshot GetSnapshot() { return _worker.GetSnapshot(); }
        public Task StopAsync(bool cancelPending = false) { return _worker.StopAsync(cancelPending); }

        public MRKFlowHandle StartFlow(MRKExecutionPlan plan, IDictionary<string, object> inputs = null,
            CancellationToken cancellation = default(CancellationToken))
        {
            if (plan == null) throw new ArgumentNullException("plan");
            MRKExecutionContext context = new MRKExecutionContext(plan.Id, inputs);
            CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            MRKFlowHandle handle = new MRKFlowHandle(context, linked);
            handle.SetCompletion(RunFlowAsync(plan, handle));
            return handle;
        }

        private async Task<MRKFlowResult> RunFlowAsync(MRKExecutionPlan plan, MRKFlowHandle flow)
        {
            MRKExecutionContext context = flow.Context;
            Dictionary<string, Task<object>> tasks =
                new Dictionary<string, Task<object>>(StringComparer.Ordinal);
            Func<string, Task<object>> schedule = null;
            schedule = id =>
            {
                Task<object> existing;
                if (tasks.TryGetValue(id, out existing)) return existing;
                Task<object> task = RunStepAsync(plan.GetStep(id), context, flow.Token, schedule);
                tasks.Add(id, task);
                return task;
            };
            try
            {
                foreach (MRKExecutionStep step in plan.Steps) schedule(step.Id);
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
            CancellationToken cancellation, Func<string, Task<object>> schedule)
        {
            context.SetState(step.Id, MRKStepState.WaitingDependencies);
            try
            {
                List<Task<object>> dependencies = new List<Task<object>>();
                foreach (string id in step.Dependencies) dependencies.Add(schedule(id));
                await Task.WhenAll(dependencies).ConfigureAwait(false);
                cancellation.ThrowIfCancellationRequested();
                FCRWorkHandle<object> work = _worker.Submit<object>(
                    ct => step.ExecuteAsync(context, ct), step.SerialKey, false, cancellation);
                context.SetState(step.Id, MRKStepState.Scheduled);
                FCRWorkResult<object> result = await work.Completion.ConfigureAwait(false);
                if (result.State == FCRWorkState.Cancelled)
                    throw new OperationCanceledException("Work cancelled", cancellation);
                if (!result.Succeeded)
                    throw new InvalidOperationException("STEP_" + step.Id + "_" + result.State, result.Error);
                context.SetResult(step.Id, result.Value);
                context.SetState(step.Id, MRKStepState.Completed);
                return result.Value;
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
