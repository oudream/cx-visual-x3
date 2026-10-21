using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Mechanism.Runtime.Execution
{
    /// <summary>
    /// Step按节点持有；WholeFlow按完整Flow持有；Stage按同一Flow内所有同组
    /// 静态DAG节点的阶段生命周期持有，不在节点交界释放，也不锁住后续无关Stage。
    /// </summary>
    public enum MRKExecutionGroupScope { Step, WholeFlow, Stage }

    /// <summary>组配额与等待上限是独立于线程池和串行Key的运行约束。</summary>
    public sealed class MRKExecutionGroupDefinition
    {
        public MRKExecutionGroupDefinition(string id, int maxConcurrency, int maxWaiting,
            MRKExecutionGroupScope scope = MRKExecutionGroupScope.Step)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("执行组ID为空", "id");
            if (maxConcurrency < 1) throw new ArgumentOutOfRangeException("maxConcurrency");
            if (maxWaiting < 0) throw new ArgumentOutOfRangeException("maxWaiting");
            if (!Enum.IsDefined(typeof(MRKExecutionGroupScope), scope))
                throw new ArgumentOutOfRangeException("scope");
            Scope = scope;
            Id = id;
            MaxConcurrency = maxConcurrency;
            MaxWaiting = maxWaiting;
        }
        public string Id { get; private set; }
        public int MaxConcurrency { get; private set; }
        public int MaxWaiting { get; private set; }
        public MRKExecutionGroupScope Scope { get; private set; }
    }

    public sealed class MRKExecutionGroupSnapshot
    {
        internal MRKExecutionGroupSnapshot(string id, int active, int waiting, long rejected)
        { Id = id; Active = active; Waiting = waiting; Rejected = rejected; }
        public string Id { get; private set; }
        public int Active { get; private set; }
        public int Waiting { get; private set; }
        public long Rejected { get; private set; }
    }

    public sealed class MRKExecutionGroupLease : IDisposable
    {
        private Action _release;
        internal MRKExecutionGroupLease(Action release) { _release = release; }
        public void Dispose()
        {
            Action release = Interlocked.Exchange(ref _release, null);
            if (release != null) release();
        }
    }

    public sealed class MRKExecutionGroupRejectedException : InvalidOperationException
    {
        public MRKExecutionGroupRejectedException(string id)
            : base("执行组等待容量已满：" + id) { GroupId = id; }
        public string GroupId { get; private set; }
    }

    /// <summary>
    /// 进程内组配额。节点必须先拿组席位、再依次取得资源、最后才提交统一F19 Worker。
    /// 因此组/资源遵循同一加锁次序；等待不会占工作线程。
    /// </summary>
    public sealed class MRKExecutionGroups
    {
        private sealed class Group
        {
            internal readonly MRKExecutionGroupDefinition Definition;
            internal readonly SemaphoreSlim Permit;
            internal readonly object Gate = new object();
            internal int Active, Waiting;
            internal long Rejected;
            internal Group(MRKExecutionGroupDefinition definition)
            {
                Definition = definition;
                Permit = new SemaphoreSlim(definition.MaxConcurrency, definition.MaxConcurrency);
            }
        }
        private readonly Dictionary<string, Group> _groups =
            new Dictionary<string, Group>(StringComparer.Ordinal);

        public MRKExecutionGroups(IEnumerable<MRKExecutionGroupDefinition> definitions)
        {
            if (definitions == null) return;
            foreach (MRKExecutionGroupDefinition definition in definitions)
            {
                if (definition == null) throw new ArgumentException("执行组不可为null", "definitions");
                if (_groups.ContainsKey(definition.Id))
                    throw new ArgumentException("执行组ID重复：" + definition.Id, "definitions");
                _groups.Add(definition.Id, new Group(definition));
            }
        }

        public bool Contains(string id) { return _groups.ContainsKey(id); }
        public MRKExecutionGroupScope GetScope(string id)
        {
            Group group;
            if (!_groups.TryGetValue(id, out group))
                throw new KeyNotFoundException("未知执行组：" + id);
            return group.Definition.Scope;
        }

        public async Task<MRKExecutionGroupLease> AcquireAsync(string id,
            CancellationToken cancellation = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("执行组ID不能为空", "id");
            Group group;
            if (!_groups.TryGetValue(id, out group))
                throw new KeyNotFoundException("未知执行组：" + id);
            cancellation.ThrowIfCancellationRequested();

            bool acquiredImmediately = false;
            lock (group.Gate)
            {
                if (group.Permit.Wait(0))
                {
                    group.Active++;
                    acquiredImmediately = true;
                }
                else
                {
                    if (group.Waiting >= group.Definition.MaxWaiting)
                    {
                        group.Rejected++;
                        throw new MRKExecutionGroupRejectedException(id);
                    }
                    group.Waiting++;
                }
            }
            if (!acquiredImmediately)
            {
                try
                {
                    await group.Permit.WaitAsync(cancellation).ConfigureAwait(false);
                    lock (group.Gate)
                    { group.Waiting--; group.Active++; }
                }
                catch
                {
                    lock (group.Gate) group.Waiting--;
                    throw;
                }
            }
            // Avoid triggering user callbacks under lock. Permit will be released exactly once.
            return new MRKExecutionGroupLease(() =>
            {
                lock (group.Gate)
                {
                    group.Active--;
                    group.Permit.Release();
                }
            });
        }

        public IList<MRKExecutionGroupSnapshot> Snapshot()
        {
            List<MRKExecutionGroupSnapshot> results = new List<MRKExecutionGroupSnapshot>();
            foreach (KeyValuePair<string, Group> entry in _groups)
                lock (entry.Value.Gate)
                    results.Add(new MRKExecutionGroupSnapshot(entry.Key,
                        entry.Value.Active, entry.Value.Waiting, entry.Value.Rejected));
            return new ReadOnlyCollection<MRKExecutionGroupSnapshot>(results);
        }
    }
}
