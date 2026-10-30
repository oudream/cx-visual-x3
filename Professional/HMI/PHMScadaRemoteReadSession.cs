using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using CxVisual.Mechanism.Data.RuntimeData;
using CxVisual.Mechanism.Data.Variables;

namespace CxVisual.Professional.HMI
{
    /// <summary>
    /// HMI只读远程运行观察会话。一次最新Snapshot是由A Host提供的只读帧，
    /// 并非本地MDS Current或可写Cache；失联清除帧，绝不继续显示Good。
    /// </summary>
    public sealed class PHMScadaRemoteReadSession : IMDSExternalBindingResolver, IDisposable
    {
        private sealed class Subscription : IDisposable
        {
            private PHMScadaRemoteReadSession _owner;
            private Action _callback;
            internal Subscription(PHMScadaRemoteReadSession owner,Action callback)
            { _owner=owner;_callback=callback; }
            public void Dispose()
            {
                PHMScadaRemoteReadSession owner=Interlocked.Exchange(ref _owner,null);
                Action callback=Interlocked.Exchange(ref _callback,null);
                if(owner!=null && callback!=null)owner.Unsubscribe(callback);
            }
        }
        private readonly object _gate=new object();
        private readonly PHMScadaReadOnlyPipeClient _client;
        private readonly PHMScadaDeviceIdentity _identity;
        private readonly TimeSpan _maxAge;
        private Timer _timer;
        private PHMScadaDeviceSnapshot _latest;
        private string _error="等待Runtime.Host首次采样";
        private string _lastObserverError;
        private Action _updated;
        private bool _disposed;
        private int _inFlight;

        public PHMScadaRemoteReadSession(PHMScadaReadOnlyPipeClient client,
            PHMScadaDeviceIdentity identity,TimeSpan maximumAge)
        {
            if(client==null)throw new ArgumentNullException("client");
            if(identity==null||!identity.Running)
                throw new InvalidOperationException("hmi.pipe.device_not_running");
            if(maximumAge<TimeSpan.Zero)throw new ArgumentOutOfRangeException("maximumAge");
            _client=client;_identity=identity;_maxAge=maximumAge;
        }
        public PHMScadaDeviceIdentity Identity{get{return _identity;}}
        public string LastObserverError
        {get{lock(_gate)return _lastObserverError;}}
        public void Start(int pollMilliseconds=1000)
        {
            if(pollMilliseconds<250||pollMilliseconds>60000)
                throw new ArgumentOutOfRangeException("pollMilliseconds");
            lock(_gate)
            {
                if(_disposed)throw new ObjectDisposedException("PHMScadaRemoteReadSession");
                if(_timer!=null)throw new InvalidOperationException("hmi.pipe.already_started");
                _timer=new Timer(Poll,null,0,pollMilliseconds);
            }
        }
        private void Poll(object state)
        {
            if(Interlocked.CompareExchange(ref _inFlight,1,0)!=0)return;
            PHMScadaDeviceSnapshot next=null;
            string fault=null;
            try{next=_client.Snapshot(_identity);}
            catch(Exception ex)
            {
                // 已过授权Generation/版本或网络失败都必须清除上次Good帧。
                fault=ex is PHMScadaPipeRejectedException
                    ?((PHMScadaPipeRejectedException)ex).ErrorCode:
                    ex.GetType().Name+": "+ex.Message;
            }
            try
            {
                lock(_gate)
                {
                    if(_disposed)return;
                    _latest=next;
                    _error=fault;
                    // 与Dispose/Unsubscribe共锁，关闭返回后不会再访问释放的WPF控件。
                    // 个别观察者错误不会杀死线程池或阻断其它订阅者。
                    Action listeners=_updated;
                    if(listeners!=null)
                        foreach(Delegate listener in listeners.GetInvocationList())
                            try{((Action)listener)();}
                            catch(Exception ex){_lastObserverError=ex.ToString();}
                }
            }
            finally{Interlocked.Exchange(ref _inFlight,0);}
        }
        public IDisposable Subscribe(Action callback)
        {
            if(callback==null)throw new ArgumentNullException("callback");
            lock(_gate)
            {
                if(_disposed)throw new ObjectDisposedException("PHMScadaRemoteReadSession");
                _updated+=callback;
                return new Subscription(this,callback);
            }
        }
        private void Unsubscribe(Action callback)
        { lock(_gate)_updated-=callback; }
        public IReadOnlyList<PHMScadaPointObservation> Points()
        {
            lock(_gate)
            {
                if(_disposed)throw new ObjectDisposedException("PHMScadaRemoteReadSession");
                if(_latest==null)
                    throw new InvalidOperationException("hmi.pipe.unavailable:"+
                        (_error??"unknown"));
                return _latest.Points;
            }
        }
        public object Resolve(MDSBindingSource source,string key,Type target)
        {
            if(source!=MDSBindingSource.Variable||String.IsNullOrWhiteSpace(key))return null;
            if(target==null)throw new ArgumentNullException("target");
            PHMScadaPointObservation sample;
            lock(_gate)
            {
                if(_disposed)throw new ObjectDisposedException("PHMScadaRemoteReadSession");
                if(_latest==null)return null;
                sample=_latest.Points.FirstOrDefault(x=>
                    x.StablePointId==key||x.VariableId==key);
            }
            if(sample==null||!sample.HasValue||
                sample.Quality!=MDSVariableQuality.Good||
                _maxAge==TimeSpan.Zero||
                sample.RefreshUtc==default(DateTime)||
                DateTime.UtcNow-sample.RefreshUtc>_maxAge)
                return null;
            object value=sample.Value;
            if(target.IsInstanceOfType(value))return value;
            if(target==typeof(string))
                return Convert.ToString(value,CultureInfo.InvariantCulture);
            if(target==typeof(double)&&value is IConvertible)
            {
                TypeCode code=Type.GetTypeCode(value.GetType());
                if(code!=TypeCode.Boolean&&code!=TypeCode.String&&
                    code!=TypeCode.DateTime&&code!=TypeCode.Object)
                {
                    double n=Convert.ToDouble(value,CultureInfo.InvariantCulture);
                    if(!Double.IsNaN(n)&&!Double.IsInfinity(n))return n;
                }
            }
            return null;
        }
        public void Dispose()
        {
            Timer old;
            lock(_gate)
            {
                if(_disposed)return;
                _disposed=true;old=_timer;_timer=null;_latest=null;_updated=null;
            }
            if(old!=null)old.Dispose();
        }
    }
}
