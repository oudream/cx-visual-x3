using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Infrastructure.Concurrency;

namespace CxVisual.Mechanism.Runtime.Execution
{
    /// <summary>
    /// 统一事件/定时触发入口：手工事件与周期触发复用相同的执行引擎。
    /// 用MaxInFlight控制在途流程数，超额明确拒绝，不隐式缓存或丢弃业务数据。
    /// </summary>
    public sealed class MRKExecutionTrigger
    {
        private readonly object _gate = new object();
        private readonly MRKExecutionEngine _engine;
        private readonly MRKExecutionPlan _plan;
        private readonly int _maxInFlight;
        private readonly Dictionary<Guid, MRKFlowHandle> _active =
            new Dictionary<Guid, MRKFlowHandle>();
        private long _rejected;
        private bool _stopping;
        private Task _stopTask;
        private FCRPeriodicJob _periodic;

        public MRKExecutionTrigger(MRKExecutionEngine engine, MRKExecutionPlan plan,
            int maxInFlight = 1)
        {
            if (engine == null) throw new ArgumentNullException("engine");
            if (plan == null) throw new ArgumentNullException("plan");
            if (maxInFlight < 1) throw new ArgumentOutOfRangeException("maxInFlight");
            _engine = engine;
            _plan = plan;
            _maxInFlight = maxInFlight;
        }
        public int InFlight { get { lock (_gate) return _active.Count; } }
        public long Rejected { get { lock (_gate) return _rejected; } }

        public MRKFlowHandle TryTrigger(IDictionary<string, object> inputs = null,
            CancellationToken cancellation = default(CancellationToken))
        {
            lock (_gate)
            {
                if (_stopping || _active.Count >= _maxInFlight)
                { _rejected++; return null; }
                cancellation.ThrowIfCancellationRequested();
                MRKFlowHandle flow = _engine.StartFlow(_plan, inputs, cancellation);
                _active.Add(flow.FlowId, flow);
                Task cleanup = flow.Completion.ContinueWith(completed =>
                {
                    lock (_gate) _active.Remove(flow.FlowId);
                }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
                return flow;
            }
        }

        /// <summary>开启周期触发，重入策略是超额拒绝；重入期间绝不创建额外Worker。</summary>
        public void StartPeriodic(TimeSpan interval, bool runImmediately = true)
        {
            lock (_gate)
            {
                if (_stopping) throw new InvalidOperationException("触发器已经停止");
                if (_periodic != null) throw new InvalidOperationException("周期触发已经启动");
                _periodic = new FCRPeriodicJob(interval, token =>
                {
                    TryTrigger(cancellation: token);
                    return Task.FromResult(true);
                }, runImmediately);
            }
        }

        /// <summary>停止新的触发；可请求取消在途流程，但不会强杀未协作退出的工作。</summary>
        public Task StopAsync(bool requestCancelActive = false)
        {
            MRKFlowHandle[] flows;
            lock (_gate)
            {
                _stopping = true;
                flows = new List<MRKFlowHandle>(_active.Values).ToArray();
                if (_stopTask == null) _stopTask = DrainAsync(flows);
            }
            if (requestCancelActive)
                foreach (MRKFlowHandle flow in flows) flow.RequestCancel();
            return _stopTask;
        }
        private async Task DrainAsync(MRKFlowHandle[] flows)
        {
            FCRPeriodicJob timer;
            lock (_gate) timer = _periodic;
            if (timer != null) await timer.StopAsync().ConfigureAwait(false);
            List<Task> tasks = new List<Task>();
            foreach (MRKFlowHandle flow in flows) tasks.Add(flow.Completion);
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
    }
}
