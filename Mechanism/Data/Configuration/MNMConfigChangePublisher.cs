using System;
using System.Collections.Generic;
using System.Linq;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Mechanism.Data.Configuration
{
    internal interface IMNMConfigListener<T>
    {
        void Notify(T before, T after, string version, string transactionId, FCMConfigChangeStage stage,
            IList<string> changedPaths, long sequence, DateTimeOffset timestampUtc);
    }
    /// <summary>
    /// 有序的本进程配置通知。发布期间的重入操作先入队、后分发，禁止嵌套回调改变顺序。
    /// 同步回调仍可能较慢；可靠跨进程重放和背压需要后续持久队列，不在此假称支持。
    /// </summary>
    public sealed class MNMConfigChangePublisher<T>
    {
        private sealed class Envelope
        {
            internal T Before, After;
            internal string Version, TransactionId;
            internal FCMConfigChangeStage Stage;
            internal IList<string> ChangedPaths;
            internal IMNMConfigListener<T>[] Listeners;
            internal long Sequence;
            internal DateTimeOffset TimestampUtc;
        }
        private readonly object _gate = new object();
        private readonly List<IMNMConfigListener<T>> _listeners = new List<IMNMConfigListener<T>>();
        private readonly Queue<Envelope> _pending = new Queue<Envelope>();
        private long _sequence;
        private bool _dispatching;
        public event Action<Exception> SubscriberError;
        public long LastSequence { get { lock (_gate) return _sequence; } }
        public IDisposable Subscribe<TValue>(string path, Func<T, TValue> accessor, T defaultModel,
            FCMConfigChangeStage stage, Action<FCMConfigChange<TValue>> callback)
        {
            if (callback == null) throw new ArgumentNullException("callback");
            IMNMConfigListener<T> listener = new Listener<TValue>(path, accessor, defaultModel, stage, callback);
            lock (_gate) _listeners.Add(listener);
            return new Subscription(this, listener);
        }
        internal void Publish(T previous, T next, int revision, string transactionId, FCMConfigChangeStage stage,
            IList<string> changedPaths)
        {
            bool drain = false;
            lock (_gate)
            {
                _pending.Enqueue(new Envelope
                {
                    Before = previous, After = next, Version = MHSConfigStorage.Name(revision),
                    TransactionId = transactionId, Stage = stage,
                    ChangedPaths = new List<string>(changedPaths).AsReadOnly(),
                    Listeners = _listeners.ToArray(),
                    Sequence = ++_sequence,
                    TimestampUtc = DateTimeOffset.UtcNow
                });
                if (!_dispatching) { _dispatching = true; drain = true; }
            }
            if (drain) Drain();
        }
        private void Drain()
        {
            while (true)
            {
                Envelope message;
                lock (_gate)
                {
                    if (_pending.Count == 0) { _dispatching = false; return; }
                    message = _pending.Dequeue();
                }
                foreach (IMNMConfigListener<T> listener in message.Listeners)
                {
                    try
                    {
                        listener.Notify(message.Before, message.After, message.Version,
                            message.TransactionId, message.Stage, message.ChangedPaths,
                            message.Sequence, message.TimestampUtc);
                    }
                    catch (Exception ex)
                    {
                        Action<Exception> handler = SubscriberError;
                        if (handler != null)
                        {
                            try { handler(ex); }
                            catch { /* Subscriber diagnostics are not allowed to destroy committed data. */ }
                        }
                    }
                }
            }
        }
        private sealed class Listener<TValue> : IMNMConfigListener<T>
        {
            private readonly string _path;
            private readonly Func<T, TValue> _get;
            private readonly TValue _default;
            private readonly FCMConfigChangeStage _stage;
            private readonly Action<FCMConfigChange<TValue>> _callback;
            internal Listener(string path, Func<T, TValue> get, T defaultModel, FCMConfigChangeStage stage,
                Action<FCMConfigChange<TValue>> callback)
            { _path = path; _get = get; _default = SafeGet(get, defaultModel); _stage = stage; _callback = callback; }
            private static TValue Copy(TValue v)
            { return FCMJsonValueCodec.Deserialize<TValue>(FCMJsonValueCodec.Serialize(v)); }
            private static TValue SafeGet(Func<T, TValue> fn, T model)
            {
                try { return fn(model); }
                catch (KeyNotFoundException) { return default(TValue); }
                catch (IndexOutOfRangeException) { return default(TValue); }
                catch (ArgumentOutOfRangeException) { return default(TValue); }
                catch (NullReferenceException) { return default(TValue); }
            }
            public void Notify(T oldValue, T newValue, string version, string transactionId,
                FCMConfigChangeStage stage, IList<string> changedPaths, long sequence, DateTimeOffset timestampUtc)
            {
                if (stage != _stage) return;
                TValue oldItem = SafeGet(_get, oldValue), newItem = SafeGet(_get, newValue);
                if (FCMJsonValueCodec.Serialize(oldItem) == FCMJsonValueCodec.Serialize(newItem)) return;
                _callback(new FCMConfigChange<TValue>
                {
                    Path = _path, Stage = stage,
                    OldValue = Copy(oldItem), NewValue = Copy(newItem), DefaultValue = Copy(_default),
                    Version = version, TransactionId = transactionId, Sequence = sequence,
                    TimestampUtc = timestampUtc,
                    ChangedPaths = new List<string>(changedPaths).AsReadOnly()
                });
            }
        }
        private sealed class Subscription : IDisposable
        {
            private MNMConfigChangePublisher<T> _owner;
            private IMNMConfigListener<T> _listener;
            public Subscription(MNMConfigChangePublisher<T> owner, IMNMConfigListener<T> listener)
            { _owner = owner; _listener = listener; }
            public void Dispose()
            {
                MNMConfigChangePublisher<T> owner = System.Threading.Interlocked.Exchange(ref _owner, null);
                if (owner != null) lock (owner._gate) owner._listeners.Remove(_listener);
            }
        }
    }
}
