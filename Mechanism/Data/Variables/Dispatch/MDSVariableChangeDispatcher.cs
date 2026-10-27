using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace CxVisual.Mechanism.Data.Variables
{
    [Flags]
    public enum MDSVariableChangeKind { None = 0, Value = 1, Refresh = 2, Quality = 4, Source = 8 }

    /// <summary>提交时封存的值与版本，消费者不能改写源变量或已发布对象。</summary>
    public sealed class MDSVariableChange
    {
        private readonly object _value;
        private readonly object _previous;
        public MDSVariableChange(string id, string name, object value, object previous,
            bool hasPrevious, MDSVariableQuality quality, string source,
            DateTime utc, long revision, MDSVariableChangeKind kind)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("VariableId不能为空", "id");
            VariableId = id; Name = name;
            _value = MDSVariableValueRules.Copy(value);
            _previous = MDSVariableValueRules.Copy(previous);
            HasPreviousValue = hasPrevious; Quality = quality; Source = source;
            UpdatedUtc = utc; Revision = revision; Kind = kind;
        }
        public string VariableId { get; private set; }
        public string Name { get; private set; }
        public object CurrentValue { get { return MDSVariableValueRules.Copy(_value); } }
        public object PreviousValue { get { return MDSVariableValueRules.Copy(_previous); } }
        public bool HasPreviousValue { get; private set; }
        public MDSVariableQuality Quality { get; private set; }
        public string Source { get; private set; }
        public DateTime UpdatedUtc { get; private set; }
        public long Revision { get; private set; }
        public MDSVariableChangeKind Kind { get; private set; }
    }

    public sealed class MDSVariableDispatchMetrics
    {
        public long Received { get; internal set; }
        public long Delivered { get; internal set; }
        public long Coalesced { get; internal set; }
        public long Overflow { get; internal set; }
        public long SubscriberFailures { get; internal set; }
    }

    /// <summary>变化订阅选择器；默认行为与既有Subscribe一致，保留全部变化类别。</summary>
    public sealed class MDSVariableSubscriptionOptions
    {
        internal readonly HashSet<string> Ids;
        public MDSVariableSubscriptionOptions(IEnumerable<string> variableIds = null,
            MDSVariableChangeKind kinds = MDSVariableChangeKind.Value | MDSVariableChangeKind.Refresh |
                MDSVariableChangeKind.Quality | MDSVariableChangeKind.Source)
        {
            if (kinds == MDSVariableChangeKind.None ||
                (kinds & ~(MDSVariableChangeKind.Value | MDSVariableChangeKind.Refresh |
                    MDSVariableChangeKind.Quality | MDSVariableChangeKind.Source)) != 0)
                throw new ArgumentOutOfRangeException("kinds");
            Ids = variableIds == null ? null : new HashSet<string>(variableIds, StringComparer.Ordinal);
            Kinds = kinds;
        }
        public MDSVariableChangeKind Kinds { get; private set; }
    }

    public sealed class MDSVariableSubscription : IDisposable
    {
        private readonly MDSVariableChangeDispatcher _owner;
        internal bool Active;
        internal readonly Action<IReadOnlyList<MDSVariableChange>> Callback;
        internal readonly HashSet<string> Ids;
        internal readonly MDSVariableChangeKind Kinds;
        internal MDSVariableSubscription(MDSVariableChangeDispatcher owner,
            Action<IReadOnlyList<MDSVariableChange>> callback, MDSVariableSubscriptionOptions options)
        {
            _owner = owner; Callback = callback; Active = true;
            Ids = options.Ids; Kinds = options.Kinds;
        }
        public void Dispose() { _owner.Unsubscribe(this); }
    }

    /// <summary>基于x2事件触发/LatestOnly机制；有界合并是状态观察，不承诺逐笔必达。</summary>
    public sealed class MDSVariableChangeDispatcher : IDisposable
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, MDSVariableChange> _latest =
            new Dictionary<string, MDSVariableChange>(StringComparer.Ordinal);
        private readonly Queue<string> _order = new Queue<string>();
        private readonly List<MDSVariableSubscription> _subscribers = new List<MDSVariableSubscription>();
        private readonly AutoResetEvent _wake = new AutoResetEvent(false);
        private readonly Thread _thread;
        private readonly int _capacity;
        private readonly MDSVariableTable _sourceTable;
        private bool _active = true, _dispatching;
        private long _received, _delivered, _coalesced, _overflow, _failures;
        private Exception _lastError;

        public MDSVariableChangeDispatcher(int capacity = 4096)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException("capacity");
            _capacity = capacity;
            _thread = new Thread(Loop) { IsBackground = true, Name = "CxVisual.Variable.LatestOnly" };
            _thread.Start();
        }
        // 从Table创建的Dispatcher在Dispose时自动解除订阅。
        public MDSVariableChangeDispatcher(MDSVariableTable table, int capacity = 4096)
            : this(capacity)
        {
            if (table == null) throw new ArgumentNullException("table");
            _sourceTable = table;
            _sourceTable.ChangeCaptured += Enqueue;
        }
        public MDSVariableSubscription Subscribe(Action<IReadOnlyList<MDSVariableChange>> callback, IEnumerable<string> ids = null)
        {
            return SubscribeWithOptions(callback, new MDSVariableSubscriptionOptions(ids));
        }
        public MDSVariableSubscription SubscribeWithOptions(
            Action<IReadOnlyList<MDSVariableChange>> callback, MDSVariableSubscriptionOptions options)
        {
            if (callback == null) throw new ArgumentNullException("callback");
            if (options == null) throw new ArgumentNullException("options");
            lock (_gate)
            {
                if (!_active) throw new ObjectDisposedException("MDSVariableChangeDispatcher");
                MDSVariableSubscription sub = new MDSVariableSubscription(this, callback, options);
                _subscribers.Add(sub); return sub;
            }
        }
        internal void Unsubscribe(MDSVariableSubscription sub)
        {
            lock (_gate) { sub.Active = false; _subscribers.Remove(sub); }
        }
        public void Enqueue(MDSVariableChange change)
        {
            if (change == null) throw new ArgumentNullException("change");
            lock (_gate)
            {
                if (!_active) return;
                _received++;
                MDSVariableChange prior;
                if (_latest.TryGetValue(change.VariableId, out prior))
                {
                    if (prior.Revision > change.Revision) return;
                    _latest[change.VariableId] = change; _coalesced++;
                }
                else
                {
                    if (_latest.Count >= _capacity)
                    {
                        string oldest = _order.Dequeue();
                        _latest.Remove(oldest);
                        _overflow++;
                    }
                    _latest.Add(change.VariableId, change);
                    _order.Enqueue(change.VariableId);
                }
                _wake.Set();
            }
        }
        private void Loop()
        {
            while (true)
            {
                _wake.WaitOne();
                while (true)
                {
                    MDSVariableChange[] batch;
                    MDSVariableSubscription[] callbacks;
                    lock (_gate)
                    {
                        if (_latest.Count == 0)
                        {
                            _dispatching = false; Monitor.PulseAll(_gate);
                            if (!_active) { _wake.Dispose(); return; }
                            break;
                        }
                        batch = _order.Select(id => _latest[id]).ToArray();
                        _latest.Clear(); _order.Clear();
                        callbacks = _subscribers.Where(x => x.Active).ToArray();
                        _dispatching = true;
                    }
                    foreach (MDSVariableSubscription subscription in callbacks)
                    {
                        if (!subscription.Active) continue;
                        MDSVariableChange[] selected = batch.Where(x =>
                            (subscription.Ids == null || subscription.Ids.Contains(x.VariableId)) &&
                            (x.Kind & subscription.Kinds) != MDSVariableChangeKind.None).ToArray();
                        if (selected.Length == 0) continue;
                        try { subscription.Callback(Array.AsReadOnly(selected)); }
                        catch (Exception e) { _lastError = e; Interlocked.Increment(ref _failures); }
                    }
                    Interlocked.Add(ref _delivered, batch.Length);
                }
            }
        }
        public bool Drain(TimeSpan timeout)
        {
            if (Thread.CurrentThread == _thread) throw new InvalidOperationException("派发线程不能等待自己");
            DateTime until = DateTime.UtcNow.Add(timeout);
            lock (_gate)
            {
                while (_latest.Count > 0 || _dispatching)
                {
                    TimeSpan remaining = until - DateTime.UtcNow;
                    if (remaining <= TimeSpan.Zero || !Monitor.Wait(_gate, remaining)) return false;
                }
                return true;
            }
        }
        public MDSVariableDispatchMetrics Metrics()
        {
            return new MDSVariableDispatchMetrics {
                Received = Interlocked.Read(ref _received), Delivered = Interlocked.Read(ref _delivered),
                Coalesced = Interlocked.Read(ref _coalesced), Overflow = Interlocked.Read(ref _overflow),
                SubscriberFailures = Interlocked.Read(ref _failures)
            };
        }
        public Exception LastSubscriberError { get { return _lastError; } }
        public void Dispose()
        {
            bool stop = false;
            lock (_gate)
            {
                if (_active) { _active = false; stop = true; _wake.Set(); }
            }
            // 不能持有派发锁反向请求Table锁，以免与变量提交线程形成锁循环。
            if (stop && _sourceTable != null) _sourceTable.ChangeCaptured -= Enqueue;
            if (Thread.CurrentThread != _thread) _thread.Join();
            lock (_gate)
            {
                foreach (MDSVariableSubscription s in _subscribers) s.Active = false;
                _subscribers.Clear();
            }
        }
    }
}
