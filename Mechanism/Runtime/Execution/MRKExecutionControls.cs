using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Mechanism.Runtime.Execution
{
    /// <summary>运行诊断仅存执行状态和时间；不存图像、凭据或任意专业负载。</summary>
    public sealed class MRKExecutionTransition
    {
        internal MRKExecutionTransition(string stepId, MRKStepState state, long sequence)
        { StepId = stepId; State = state; Sequence = sequence; TimestampUtc = DateTime.UtcNow; }
        public string StepId { get; private set; }
        public MRKStepState State { get; private set; }
        public long Sequence { get; private set; }
        public DateTime TimestampUtc { get; private set; }
    }

    /// <summary>
    /// 按序号增量读取，若UI断线过久产生数据缺口，必须主动重新获取Flow状态快照。
    /// </summary>
    public sealed class MRKTraceBatch
    {
        internal MRKTraceBatch(IList<MRKExecutionTransition> changes, long cursor, long lost)
        { Changes = changes; NextCursor = cursor; LostCount = lost; }
        public IList<MRKExecutionTransition> Changes { get; private set; }
        public long NextCursor { get; private set; }
        public long LostCount { get; private set; }
        public bool HasGap { get { return LostCount > 0; } }
    }

    /// <summary>有界执行轨迹，防止长期运行的流程无限占用监控内存。</summary>
    public sealed class MRKExecutionTrace
    {
        private readonly object _gate = new object();
        private readonly Queue<MRKExecutionTransition> _items =
            new Queue<MRKExecutionTransition>();
        private readonly int _capacity;
        private long _sequence, _dropped;
        public MRKExecutionTrace(int capacity = 2048)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException("capacity");
            _capacity = capacity;
        }
        public long Dropped { get { lock (_gate) return _dropped; } }
        internal void Record(string stepId, MRKStepState state)
        {
            lock (_gate)
            {
                if (_items.Count == _capacity) { _items.Dequeue(); _dropped++; }
                _items.Enqueue(new MRKExecutionTransition(stepId, state, ++_sequence));
            }
        }
        public IList<MRKExecutionTransition> Snapshot()
        { lock (_gate) return new ReadOnlyCollection<MRKExecutionTransition>(
            new List<MRKExecutionTransition>(_items)); }
        public MRKTraceBatch ReadSince(long lastSequence)
        {
            if (lastSequence < 0) throw new ArgumentOutOfRangeException("lastSequence");
            lock (_gate)
            {
                long first = _items.Count == 0 ? _sequence + 1 : _items.Peek().Sequence;
                long lost = lastSequence < first - 1 ? first - 1 - lastSequence : 0;
                List<MRKExecutionTransition> changes = new List<MRKExecutionTransition>();
                foreach (MRKExecutionTransition item in _items)
                    if (item.Sequence > lastSequence) changes.Add(item);
                return new MRKTraceBatch(
                    new ReadOnlyCollection<MRKExecutionTransition>(changes), _sequence, lost);
            }
        }
    }

    /// <summary>
    /// 可选调试控制器。只在工作提交给Worker前暂停，避免等待断点持有执行线程/设备租约。
    /// StepOnce只释放一个等待节点；正式设备控制不应通过此API绕开授权。
    /// </summary>
    public sealed class MRKExecutionDebugController
    {
        private readonly object _gate = new object();
        private readonly HashSet<string> _breakpoints = new HashSet<string>(StringComparer.Ordinal);
        private readonly LinkedList<TaskCompletionSource<bool>> _waiting =
            new LinkedList<TaskCompletionSource<bool>>();
        private bool _paused;
        public bool IsPaused { get { lock (_gate) return _paused; } }
        public int WaitingCount
        { get { lock (_gate) return _waiting.Count; } }

        public void AddBreakpoint(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("断点ID不能为空", "id");
            lock (_gate) _breakpoints.Add(id);
        }
        public void RemoveBreakpoint(string id) { lock (_gate) _breakpoints.Remove(id); }
        public void Pause() { lock (_gate) _paused = true; }

        public void Resume()
        {
            lock (_gate)
            {
                _paused = false;
                while (_waiting.Count > 0)
                {
                    TaskCompletionSource<bool> next = _waiting.First.Value;
                    _waiting.RemoveFirst();
                    next.TrySetResult(true);
                }
            }
        }
        public bool StepOnce()
        {
            lock (_gate)
            {
                if (!_paused) return false;
                while (_waiting.Count > 0)
                {
                    TaskCompletionSource<bool> candidate = _waiting.First.Value;
                    _waiting.RemoveFirst();
                    if (candidate.TrySetResult(true)) return true;
                }
                return false;
            }
        }
        // 兼容旧Step断点，同时允许按loop.iN.step的真实Invocation路径精准暂停。
        internal Task WaitBeforeAsync(string stepId, CancellationToken cancellation)
        { return WaitBeforeAsync(stepId,stepId,cancellation); }

        internal Task WaitBeforeAsync(string stepId,string qualifiedStepId,
            CancellationToken cancellation)
        {
            if(string.IsNullOrWhiteSpace(stepId)||string.IsNullOrWhiteSpace(qualifiedStepId))
                throw new ArgumentException("debug.breakpoint_step_required");
            cancellation.ThrowIfCancellationRequested();
            TaskCompletionSource<bool> pending;
            LinkedListNode<TaskCompletionSource<bool>> node;
            lock (_gate)
            {
                if (_breakpoints.Contains(stepId)||_breakpoints.Contains(qualifiedStepId))
                    _paused = true;
                if (!_paused) return Task.FromResult(true);
                pending = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                node = _waiting.AddLast(pending);
            }
            return AwaitAsync(pending, node, cancellation);
        }
        private async Task AwaitAsync(TaskCompletionSource<bool> pending,
            LinkedListNode<TaskCompletionSource<bool>> node,
            CancellationToken cancellation)
        {
            try
            {
                using (CancellationTokenRegistration registration =
                    cancellation.Register(() => pending.TrySetCanceled()))
                {
                    await pending.Task.ConfigureAwait(false);
                    cancellation.ThrowIfCancellationRequested();
                }
            }
            finally
            {
                // 取消的断点等待者立即摘除，长运行调试不积累失效项。
                lock (_gate) if (node.List != null) _waiting.Remove(node);
            }
        }
    }

    /// <summary>条件与有限循环是组合执行操作，不新增第二套流程引擎。</summary>
    public static class MRKExecutionOperations
    {
        public static Func<MRKExecutionContext, CancellationToken, Task<object>> Condition(
            Func<MRKExecutionContext, bool> predicate,
            Func<MRKExecutionContext, CancellationToken, Task<object>> whenTrue,
            Func<MRKExecutionContext, CancellationToken, Task<object>> whenFalse = null)
        {
            if (predicate == null) throw new ArgumentNullException("predicate");
            if (whenTrue == null) throw new ArgumentNullException("whenTrue");
            return async (context, token) =>
            {
                token.ThrowIfCancellationRequested();
                Func<MRKExecutionContext, CancellationToken, Task<object>> selected =
                    predicate(context) ? whenTrue : whenFalse;
                if (selected == null) return null;
                Task<object> task = selected(context, token);
                if (task == null) throw new InvalidOperationException("条件分支返回空任务");
                return await task.ConfigureAwait(false);
            };
        }
        /// <summary>有界循环；每次异步迭代顺序执行且受同一取消令牌控制。</summary>
        public static Func<MRKExecutionContext, CancellationToken, Task<object>> Loop(
            int maxIterations,
            Func<MRKExecutionContext, int, bool> shouldContinue,
            Func<MRKExecutionContext, int, CancellationToken, Task<object>> body)
        {
            if (maxIterations < 1) throw new ArgumentOutOfRangeException("maxIterations");
            if (shouldContinue == null) throw new ArgumentNullException("shouldContinue");
            if (body == null) throw new ArgumentNullException("body");
            return async (context, token) =>
            {
                object last = null;
                for (int index = 0; index < maxIterations; index++)
                {
                    token.ThrowIfCancellationRequested();
                    if (!shouldContinue(context, index)) return last;
                    Task<object> work = body(context, index, token);
                    if (work == null) throw new InvalidOperationException("循环体返回空任务");
                    last = await work.ConfigureAwait(false);
                }
                // 达到上限即按上限完成；不支持无限循环。
                return last;
            };
        }
    }
}
