using System;
using System.Collections.Generic;
using System.Linq;
using CxVisual.Foundation.Core;
namespace CxVisual.Mechanism.Core
{
    /// <summary>一次注册的可撤销令牌；旧令牌不允许误删除新的代次。</summary>
    public sealed class MRIEndpointRegistration : IDisposable
    {
        private readonly MRIEndpointRegistry _registry;
        private readonly Guid _token;
        private bool _disposed;
        internal MRIEndpointRegistration(MRIEndpointRegistry registry,Guid token)
        {_registry=registry;_token=token;}
        public void Dispose()
        {
            if(_disposed)return;
            _registry.Unregister(_token);
            _disposed=true;
        }
    }

    /// <summary>唯一Host/Run端点双索引；不存Message、Variable、Command或Heap真值。</summary>
    public sealed class MRIEndpointRegistry : IDisposable
    {
        private sealed class Entry
        {
            internal Guid Token;
            internal FOIInteractionEndpointAddress Address;
        }
        private readonly object _gate=new object();
        private readonly Dictionary<string,Entry> _paths=new Dictionary<string,Entry>(StringComparer.Ordinal);
        private readonly Dictionary<long,Entry> _ids=new Dictionary<long,Entry>();
        private readonly Guid _runId;
        private bool _disposed;
        public MRIEndpointRegistry(Guid runId)
        {
            if(runId==Guid.Empty)throw new ArgumentException("无效RunId","runId");
            _runId=runId;
        }
        public Guid RunId{get{return _runId;}}
        private void DemandOpen(){if(_disposed)throw new ObjectDisposedException("MRIEndpointRegistry");}
        public MRIEndpointRegistration Register(FOIInteractionEndpointAddress address)
        {
            if(address==null)throw new ArgumentNullException("address");
            lock(_gate)
            {
                DemandOpen();
                if(address.RunId!=_runId)throw new InvalidOperationException("跨Run注册被拒绝");
                if(_paths.ContainsKey(address.Path)||_ids.ContainsKey(address.NumericId))
                    throw new InvalidOperationException("重复端点路径或数字ID");
                Entry entry=new Entry{Token=Guid.NewGuid(),Address=address};
                _paths.Add(address.Path,entry);_ids.Add(address.NumericId,entry);
                return new MRIEndpointRegistration(this,entry.Token);
            }
        }
        public FOIInteractionEndpointAddress Resolve(string path)
        {
            lock(_gate)
            {
                DemandOpen(); Entry e;
                return path!=null&&_paths.TryGetValue(path,out e)?e.Address:null;
            }
        }
        public FOIInteractionEndpointAddress Resolve(long numericId)
        {
            lock(_gate)
            {
                DemandOpen(); Entry e;
                return _ids.TryGetValue(numericId,out e)?e.Address:null;
            }
        }
        public FOIInteractionEndpointAddress[] Snapshot()
        {
            lock(_gate){DemandOpen();return _ids.Values.Select(e=>e.Address).OrderBy(a=>a.Path,StringComparer.Ordinal).ToArray();}
        }
        internal bool Unregister(Guid token)
        {
            lock(_gate)
            {
                if(_disposed)return false;
                Entry e=_ids.Values.FirstOrDefault(v=>v.Token==token);
                if(e==null)return false;
                _ids.Remove(e.Address.NumericId);_paths.Remove(e.Address.Path);return true;
            }
        }
        public void Dispose(){lock(_gate){if(_disposed)return;_disposed=true;_paths.Clear();_ids.Clear();}}
    }
}
