using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using CxVisual.Foundation.Data.Messages;

namespace CxVisual.Mechanism.Data.Messages
{
    /// <summary>
    /// Strategy：QoS0至多尝试一次，QoS1仅当前进程内有限次数重试。
    /// 不模拟Broker ACK、持久化、ExactlyOnce或设备操作幂等。
    /// </summary>
    internal sealed class MNMMessageRetryPolicy
    {
        private readonly int _attempts;
        private readonly int _delayMs;
        internal MNMMessageRetryPolicy(int attempts, int delayMs)
        {
            if (attempts < 1 || delayMs < 0) throw new ArgumentOutOfRangeException("attempts");
            _attempts = attempts; _delayMs = delayMs;
        }
        internal int Attempts(MNMMessageQoS qos)
        { return qos == MNMMessageQoS.AtLeastOnceInProcess ? _attempts : 1; }
        internal Task BackoffAsync()
        { return _delayMs > 0 ? Task.Delay(_delayMs) : Task.CompletedTask; }
    }

    /// <summary>
    /// 有界ThreadPool dispatcher：保留默认单队列顺序模式；该模式明确不保证同Topic顺序。
    /// 在投递开始之前要求容量配额，绝不创建无界Task。
    /// </summary>
    internal sealed class MNMMessageThreadPoolScheduler
    {
        private readonly object _gate = new object();
        private readonly int _capacity;
        private readonly HashSet<Task> _running = new HashSet<Task>();

        internal MNMMessageThreadPoolScheduler(int capacity)
        { if (capacity < 1) throw new ArgumentOutOfRangeException("capacity"); _capacity = capacity; }

        internal bool TrySchedule(Func<Task> callback)
        {
            if (callback == null) throw new ArgumentNullException("callback");
            lock (_gate)
            {
                if (_running.Count >= _capacity) return false;
                TaskCompletionSource<bool> completed =
                    new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                Task tracking = completed.Task;
                _running.Add(tracking);
                Task.Run(async () =>
                {
                    try { await callback().ConfigureAwait(false); }
                    finally
                    {
                        // callbacks由MessageCenter封装异常，Tracker只负责令牌和生命周期。
                        completed.TrySetResult(true);
                        lock (_gate) _running.Remove(tracking);
                    }
                });
                return true;
            }
        }

        internal int InFlight { get { lock (_gate) return _running.Count; } }

        internal async Task DrainAsync()
        {
            while (true)
            {
                Task[] pending;
                lock (_gate)
                {
                    if (_running.Count == 0) return;
                    pending = _running.ToArray();
                }
                await Task.WhenAll(pending).ConfigureAwait(false);
            }
        }
    }
}
