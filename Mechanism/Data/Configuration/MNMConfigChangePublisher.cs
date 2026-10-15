using System;
using System.Collections.Generic;
using System.Linq;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Mechanism.Data.Configuration
{
    internal interface IMNMConfigListener<T>
    {
        void Notify(T before, T after, string version, string transactionId, FCMConfigChangeStage stage,
            IList<string> changedPaths);
    }
    public sealed class MNMConfigChangePublisher<T>
    {
        private readonly object _gate = new object();
        private readonly List<IMNMConfigListener<T>> _listeners = new List<IMNMConfigListener<T>>();
        public event Action<Exception> SubscriberError;

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
            IMNMConfigListener<T>[] copy;
            lock (_gate) copy = _listeners.ToArray();
            foreach (IMNMConfigListener<T> listener in copy)
            {
                try { listener.Notify(previous, next, MHSConfigStorage.Name(revision), transactionId, stage, changedPaths); }
                catch (Exception ex)
                {
                    Action<Exception> handler = SubscriberError;
                    if (handler != null) { try { handler(ex); } catch { /* diagnostic observers must not interrupt commits */ } }
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
            public Listener(string path, Func<T, TValue> get, T defaultModel, FCMConfigChangeStage stage, Action<FCMConfigChange<TValue>> callback)
            { _path = path; _get = get; _default = SafeGet(get, defaultModel); _stage = stage; _callback = callback; }
            private static TValue Copy(TValue v) { return FCMJsonValueCodec.Deserialize<TValue>(FCMJsonValueCodec.Serialize(v)); }
            private static TValue SafeGet(Func<T, TValue> fn, T model)
            {
                try { return fn(model); }
                catch (KeyNotFoundException) { return default(TValue); }
                catch (IndexOutOfRangeException) { return default(TValue); }
                catch (ArgumentOutOfRangeException) { return default(TValue); }
                catch (NullReferenceException) { return default(TValue); }
            }
            public void Notify(T oldValue, T newValue, string version, string transactionId,
                FCMConfigChangeStage stage, IList<string> changedPaths)
            {
                if (stage != _stage) return;
                TValue oldItem = SafeGet(_get, oldValue), newItem = SafeGet(_get, newValue);
                if (FCMJsonValueCodec.Serialize(oldItem) == FCMJsonValueCodec.Serialize(newItem)) return;
                _callback(new FCMConfigChange<TValue> { Path = _path, Stage = stage,
                    OldValue = Copy(oldItem), NewValue = Copy(newItem), DefaultValue = Copy(_default),
                    Version = version, TransactionId = transactionId,
                    ChangedPaths = new List<string>(changedPaths).AsReadOnly() });
            }
        }
        private sealed class Subscription : IDisposable
        {
            private MNMConfigChangePublisher<T> _owner;
            private IMNMConfigListener<T> _listener;
            public Subscription(MNMConfigChangePublisher<T> owner, IMNMConfigListener<T> listener) { _owner = owner; _listener = listener; }
            public void Dispose()
            {
                MNMConfigChangePublisher<T> owner = System.Threading.Interlocked.Exchange(ref _owner, null);
                if (owner != null) lock (owner._gate) owner._listeners.Remove(_listener);
            }
        }
    }
}