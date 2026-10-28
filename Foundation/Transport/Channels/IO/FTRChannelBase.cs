using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Transport
{
public abstract class FTRChannelBase : IFTRChannelTransport
    {
        private readonly object _gate = new object();
        // 通道只保存有限的诊断证据，不占用生产接收队列或协议缓存。
        // 大报文仍完整进入收发及上层事件，超过单条证据上限时仅省略诊断副本。
        private const int MaxEvidenceFrames = 10000;
        private const int MaxEvidencePayloadBytes = 256 * 1024;
        private const int MaxEvidenceBytes = 4 * 1024 * 1024;
        private readonly Queue<FTRChannelFrame> _frames = new Queue<FTRChannelFrame>();
        private int _retainedEvidenceBytes;
        private long _sequence;
        private Guid _sessionId;
        private FTRChannelState _state = FTRChannelState.Closed;
        private readonly FTRChannelDiagnostics _diagnostics=new FTRChannelDiagnostics();
        private FTRReceivePipeline _receivePipeline;
        private FTRBufferedByteBudget _inFlightBudget;
        internal FTRBufferedByteBudget InFlightBudget
        {
            get {return _inFlightBudget;}
        }
        protected void ConfigureInFlightBudget(int limit)
        {
            _inFlightBudget=new FTRBufferedByteBudget(limit);
        }
        private bool _capturePayloadEvidence=true;
        private readonly object _disposeGate=new object();
        private bool _disposed;
        private int _activeSends,_asyncSends,_sendCapacity;
        private long _pendingSendBytes,_sendByteBudget,_rejectedSends;
        private bool _boundedSend;
        /// <summary>
        /// Configure the existing bounded async send admission counters from the
        /// authoritative Channel FCM configuration; no second queue is introduced.
        /// Called before the physical channel starts accepting sends.
        /// </summary>
        protected void ConfigureSendAdmission(FTRChannelConfiguration configuration)
        {
            if(configuration==null)throw new ArgumentNullException("configuration");
            if(configuration.SendQueueCapacity<1||configuration.SendByteBudget<1)
                throw new ArgumentException("通道发送预算无效","configuration");
            lock(_gate)
            {
                if(_activeSends!=0||_asyncSends!=0)
                    throw new InvalidOperationException("发送仍在运行，无法重设通道预算");
                _sendCapacity=configuration.SendQueueCapacity;
                _sendByteBudget=configuration.SendByteBudget;
                _boundedSend=true;
                _pendingSendBytes=0;
            }
        }
        protected FTRChannelBase(string id, FTRChannelTopology topology)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("通道 Id 不可为空");
            Id = id.Trim(); Topology = topology; _sessionId = Guid.NewGuid();
        }
        public string Id { get; private set; }
        public Guid SessionId { get {lock(_gate)return _sessionId;} }
        public FTRChannelTopology Topology { get; private set; }
        public FTRChannelState State { get { lock (_gate) return _state; } }
        public event Action<FTRChannelReceiveContext> Received;
        public event Action<FTRChannelFrame> FrameRecorded;
        public event Action<FTRChannelState> StateChanged;
        /// <summary>低复制接收接口；回调结束自动释放借用，后级排队必须Retain并Dispose。</summary>
        public event Action<FTRReceiveBuffer> BufferReceived;
        public FTRChannelDiagnosticsSnapshot Diagnostics
        {
            get {var snapshot=_diagnostics.Snapshot();
                FTRReceivePipeline pipeline=_receivePipeline;
                snapshot.ReceiveQueue=pipeline==null?null:pipeline.Snapshot();
                lock(_gate){snapshot.PendingSends=_activeSends;snapshot.PendingSendSnapshots=_asyncSends;
                    snapshot.PendingSendBytes=_pendingSendBytes;snapshot.RejectedSends=_rejectedSends;}
                FTRBufferedByteBudget budget=_inFlightBudget;
                if(budget!=null)
                {
                    long used,peak,rejected,limit;
                    budget.Snapshot(out used,out peak,out rejected,out limit);
                    snapshot.BufferedPayloadBytes=used;
                    snapshot.PeakBufferedPayloadBytes=peak;
                    snapshot.RejectedPayloadBuffers=rejected;
                    snapshot.PayloadBufferByteBudget=limit;
                }
                return snapshot;}
        }
        /// <summary>统一诊断视图；仅采样已有计数器与存活Peer，不增加接收者或定时线程。</summary>
        public FTRChannelHealthSnapshot Health
        {
            get
            {
                FTRChannelState state;Guid session;
                lock(_gate){state=_state;session=_sessionId;}
                var multi=this as IFTRSubChannelAware;
                // 离线目录允许注册虚拟Peer，但不是已经建立的物理连接。
                int peers=state!=FTRChannelState.Open ? 0 :
                    (multi==null ? 1 : multi.SnapshotSubChannels().Length);
                return new FTRChannelHealthSnapshot(Id,session,state,peers,Diagnostics);
            }
        }
        private void NotifyState()
        {
            var handlers=StateChanged;if(handlers==null)return;
            FTRChannelState state=State;
            foreach(Action<FTRChannelState> listener in handlers.GetInvocationList())
                try{listener(state);}catch(Exception error){_diagnostics.RecordError(error,"Observer",FTRFaultScope.Operation);}
        }
        public void Open() { OpenAsync(CancellationToken.None).GetAwaiter().GetResult(); }
        public void Close() { CloseAsync(CancellationToken.None).GetAwaiter().GetResult(); }
        public async Task OpenAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            lock (_gate)
            {
                if(_disposed)throw new ObjectDisposedException("FTRChannelBase");
                if (_state != FTRChannelState.Closed)
                    throw new InvalidOperationException("通道只能从 Closed 打开");
                // 先改变状态，再启动异步接收，避免高速回环包在 Closed 状态被丢弃。
                _sessionId = Guid.NewGuid(); // 新Open不得沿用旧物理连接的SessionId
                _state = FTRChannelState.Opening;
            }
            NotifyState();
            try
            {
                await OnOpenAsync(ct).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();
                lock (_gate)
                {
                    if (_state != FTRChannelState.Opening)
                        throw new IOException("通道打开期间收发任务已发生故障");
                    _state = FTRChannelState.Open;
                }
                NotifyState();
            }
            catch(Exception error)
            {
                _diagnostics.RecordError(error,"Open",FTRFaultScope.Channel);
                // 打开部分成功时也必须释放监听 Socket、串口与后台任务。
                lock (_gate) _state = FTRChannelState.Faulted;
                NotifyState();
                try { await OnCloseAsync(CancellationToken.None).ConfigureAwait(false); }
                catch { /* 保留最初的 Open 异常，不覆盖诊断根因 */ }
                throw;
            }
        }
        public async Task CloseAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if(IsAdditionalTransportThread || (_receivePipeline!=null&&(_receivePipeline.IsDeliveryThread||_receivePipeline.IsReceiveThread)))
                throw new InvalidOperationException("传输线程或接收回调内不能等待自身Close；请在回调返回后由Host关闭");
            lock (_gate)
            {
                if (_state == FTRChannelState.Closed) return;
                if (_state == FTRChannelState.Opening || _state == FTRChannelState.Closing)
                    throw new InvalidOperationException("通道正在生命周期转换中");
                // 允许 Faulted 通道清理与恢复，而非卡死在永久 Faulted。
                _state = FTRChannelState.Closing;
            }
            NotifyState();
            try
            {
                await OnCloseAsync(ct).ConfigureAwait(false);
                lock (_gate) _state = FTRChannelState.Closed;
                NotifyState();
            }
            catch(Exception error)
            {
                _diagnostics.RecordError(error,"Close",FTRFaultScope.Channel);
                lock (_gate) _state = FTRChannelState.Faulted;
                NotifyState();
                throw;
            }
        }
        public void Send(byte[] bytes, string correlationId = null, int segmentBytes = 0)
        {
            if (segmentBytes < 0) throw new ArgumentOutOfRangeException("segmentBytes");
            SendCore(bytes,0,bytes==null?0:bytes.Length,new FTRSubChannelAddress(Id),
                CancellationToken.None,correlationId,segmentBytes);
        }
        /// <summary>同步借用发送切片；返回前调用方不得修改源缓冲，TCP不复制整块数据。</summary>
        public void Send(byte[] bytes,int offset,int count,FTRSubChannelAddress address,CancellationToken ct)
        {SendCore(bytes,offset,count,address,ct,null,0);}
        private void SendCore(byte[] bytes,int offset,int count,FTRSubChannelAddress address,
            CancellationToken ct,string correlation,int segmentBytes)
        {
            if(bytes==null||offset<0||count<0||(count==0&&!SupportsEmptySend)||offset>bytes.Length-count)
                throw new ArgumentException("发送切片无效");
            if(address==null||address.ChannelId!=Id)throw new ArgumentException("通道身份不匹配");
            ct.ThrowIfCancellationRequested();
            EnterSend(count,false);
            try
            {
                try{OnSendSynchronous(bytes,offset,count,address,ct,segmentBytes);}
                finally{ExitSend(count,false);}
                _diagnostics.RecordSend(count);Record(FTRFrameDirection.Tx,bytes,correlation,null,offset,count,address);
            }
            catch(Exception error)
            {_diagnostics.RecordError(error,"Send",FTRFaultScope.Operation);Record(FTRFrameDirection.Error,bytes,correlation,error.Message,offset,count,address);throw;}
        }
        protected virtual void OnSendSynchronous(byte[] bytes,int offset,int count,
            FTRSubChannelAddress address,CancellationToken ct,int segmentBytes)
        {
            int size=segmentBytes==0?count:segmentBytes;
            for(int done=0;done<count;)
            {
                int n=Math.Min(size,count-done);byte[] slice;
                if(offset==0&&n==bytes.Length)slice=bytes;
                else {slice=new byte[n];Buffer.BlockCopy(bytes,offset+done,slice,0,n);}
                OnSendAsync(slice,address,ct).GetAwaiter().GetResult();done+=n;
            }
        }
        protected virtual bool SupportsEmptySend { get { return false; } }
        public Task SendAsync(byte[] bytes,FTRSubChannelAddress address,CancellationToken ct)
        {return SendAsync(bytes,0,bytes==null?0:bytes.Length,address,ct);}

        /// <summary>异步切片发送只复制一次；接纳预算覆盖克隆后整个待执行期。</summary>
        public async Task SendAsync(byte[] bytes,int offset,int count,
            FTRSubChannelAddress address,CancellationToken ct)
        {
            if(bytes==null||offset<0||count<0||offset>bytes.Length-count||
                (count==0&&!SupportsEmptySend))throw new ArgumentException("发送切片无效");
            if(address==null||address.ChannelId!=Id)
                throw new ArgumentException("通道身份不匹配");
            ct.ThrowIfCancellationRequested();
            try{EnterSend(count,true);}
            catch(FTRBudgetExceededException error)
            {
                _diagnostics.RecordError(error,"SendAdmission",FTRFaultScope.Operation);
                throw;
            }
            byte[] owned=null;
            try
            {
                try
                {
                    owned=new byte[count];
                    Buffer.BlockCopy(bytes,offset,owned,0,count);
                    await OnSendAsync(owned,address,ct).ConfigureAwait(false);
                }
                finally{ExitSend(count,true);}
                _diagnostics.RecordSend(count);
                Record(FTRFrameDirection.Tx,owned,null,null,0,-1,address);
            }
            catch(Exception error)
            {
                _diagnostics.RecordError(error,"Send",FTRFaultScope.Operation);
                Record(FTRFrameDirection.Error,owned,null,error.Message,0,-1,address);
                throw;
            }
        }
        private void EnterSend(int bytes,bool snapshot)
        {
            lock(_gate)
            {
                if(_state!=FTRChannelState.Open)throw new InvalidOperationException("通道尚未打开");
                if(_boundedSend&&snapshot&&(_asyncSends>=_sendCapacity||bytes>_sendByteBudget-_pendingSendBytes))
                {_rejectedSends++;throw new FTRBudgetExceededException("异步发送预算已满，尚未接纳或发送；请分块或稍后提交");}
                _activeSends++;if(snapshot){_asyncSends++;_pendingSendBytes+=bytes;}
            }
        }
        private void ExitSend(int bytes,bool snapshot)
        {lock(_gate){_activeSends--;if(snapshot){_asyncSends--;_pendingSendBytes-=bytes;}Monitor.PulseAll(_gate);}}
        protected void WaitForPendingSends(int timeoutMilliseconds,CancellationToken cancellation)
        {
            Stopwatch timer=Stopwatch.StartNew();
            lock(_gate)while(_activeSends>0)
            {
                cancellation.ThrowIfCancellationRequested();
                long remaining=timeoutMilliseconds-timer.ElapsedMilliseconds;
                if(remaining<=0)throw new TimeoutException("TCP sends have not settled");
                Monitor.Wait(_gate,(int)Math.Min(20,remaining));
            }
        }
        public IList<FTRChannelFrame> ReadFrames()
        {
            lock (_gate) return _frames.ToArray();
        }
        public void Dispose()
        {
            lock(_disposeGate)
            {
                lock(_gate)if(_disposed)return;
                Close();
                lock(_gate)
                {
                    if(_state!=FTRChannelState.Closed)
                        throw new InvalidOperationException("Concurrent Open while disposing");
                    _disposed=true;
                }
            }
        }
        protected void ReceiveBytes(FTRSubChannelAddress address,byte[] bytes,string correlationId=null)
        {ReceiveBytes(address,bytes,bytes==null?0:bytes.Length,correlationId);}
        /// <summary>消息通道借用原始聚合缓冲切片，Received仍产生独立、受额度约束的数据快照。</summary>
        protected void ReceiveBytes(FTRSubChannelAddress address,byte[] bytes,int count,string correlationId=null)
        {
            FTRChannelState current=State;
            if(current!=FTRChannelState.Open && current!=FTRChannelState.Opening)
                throw new IOException("接收通道未打开");
            if(address==null||address.ChannelId!=Id)
                throw new ArgumentException("接收地址不属于本通道");
            if(bytes==null||count<0||count>bytes.Length)
                throw new ArgumentOutOfRangeException("count");
            FTRBufferedByteBudget budget=_inFlightBudget;
            using(var lease=budget==null?null:budget.Lease())
            {
                if(lease!=null)lease.Reserve(count);
                var context=new FTRChannelReceiveContext(address,bytes,count,correlationId){SessionId=this.SessionId};
                _diagnostics.RecordReceive(count);
                Record(FTRFrameDirection.Rx,bytes,correlationId,null,0,count,address);
                // 保持与池化接收路径相同的Observer隔离语义：一个插件/订阅者报错
                // 不能中断其他订阅者或导致HTTP/WS单Peer被误判为物理故障。
                Action<FTRChannelReceiveContext> handler=Received;
                if(handler!=null)
                    foreach(Action<FTRChannelReceiveContext> observer in handler.GetInvocationList())
                        try{observer(context);}
                        catch(Exception error){RecordObserverError(error);}
            }
        }
        protected void StartBufferedReceive(FTRChannelConfiguration configuration)
        {
            ConfigureSendAdmission(configuration);
            _capturePayloadEvidence=configuration.CapturePayloadEvidence;
            _receivePipeline=new FTRReceivePipeline(Id,configuration,DeliverBuffer,RecordReceiveError);
        }
        protected FTRReceiveBuffer RentReceiveBuffer(CancellationToken cancellation)
        {return _receivePipeline.Rent(cancellation);}
        protected void EnterReceiveThread() { _receivePipeline.EnterReceiveThread(); }
        protected virtual bool IsAdditionalTransportThread { get { return false; } }
        protected void PublishReceiveBuffer(FTRSubChannelAddress address,FTRReceiveBuffer buffer,int count,string remoteEndpoint=null)
        {_receivePipeline.Publish(address,buffer,count,remoteEndpoint);}
        protected void AfterReceived(Action action,CancellationToken cancellation){_receivePipeline.AfterReceived(action,cancellation);}
        protected void DrainBufferedReceive(int timeoutMilliseconds,CancellationToken cancellation)
        {if(_receivePipeline!=null)_receivePipeline.CompleteAndDrain(timeoutMilliseconds,cancellation);}
        protected void RecordReceiveError(Exception error)
        {RecordReceiveError(error,FTRFaultScope.Channel);}
        protected void RecordReceiveError(Exception error,FTRFaultScope scope)
        {RecordReceiveError(error,scope,null);}
        protected void RecordReceiveError(Exception error,FTRFaultScope scope,FTRSubChannelAddress address)
        {
            _diagnostics.RecordError(error,"Receive",scope,address);
            Record(FTRFrameDirection.Error,new byte[0],null,error.Message,0,-1,address);
        }
        protected void RecordObserverError(Exception error)
        {_diagnostics.RecordError(error,"Observer",FTRFaultScope.Operation);Record(FTRFrameDirection.Error,new byte[0],null,error.Message);}
        private void DeliverBuffer(FTRReceiveBuffer buffer)
        {
            // 关闭先停止OS读取，然后排空已接纳数据；Closing不能丢掉已排入的可靠字节。
            FTRChannelState state=State;
            if(state==FTRChannelState.Closed)throw new IOException("Buffered receive after close");
            if(buffer.Address.ChannelId!=Id)throw new ArgumentException("接收地址不属于本通道");
            var peers=this as IFTRMultiPeerChannel;
            if(peers!=null&&buffer.Address.HasSubChannel&&peers.FindSubChannel(buffer.Address)==null)
            {RecordReceiveError(new IOException("已显式移除的SubChannel接收块被拒绝"));return;}
            _diagnostics.RecordReceive(buffer.Count);
            Record(FTRFrameDirection.Rx,buffer.WritableBuffer,null,null,0,buffer.Count,buffer.Address);
            var borrowed=BufferReceived;
            if(borrowed!=null)foreach(Action<FTRReceiveBuffer> listener in borrowed.GetInvocationList())
                using(var lease=buffer.Retain())
                    try{listener(lease);}catch(Exception error){RecordObserverError(error);}
            var legacy=Received;
            if(legacy!=null)
            {
                var context=new FTRChannelReceiveContext(buffer.Address,buffer.WritableBuffer,buffer.Count,null,buffer.RemoteEndpoint){SessionId=this.SessionId};
                foreach(Action<FTRChannelReceiveContext> listener in legacy.GetInvocationList())
                    try{listener(context);}catch(Exception error){RecordObserverError(error);}
            }
        }
        protected void Fault(Exception error)
        {
            if (error == null) throw new ArgumentNullException("error");
            lock (_gate) _state = FTRChannelState.Faulted;
            _diagnostics.RecordError(error,"Transport",FTRFaultScope.Channel);
            NotifyState();
            Record(FTRFrameDirection.Error, new byte[0], null, error.Message);
        }
        private void Record(FTRFrameDirection direction, byte[] bytes, string correlation, string error,
            int offset=0,int count=-1,FTRSubChannelAddress address=null)
        {
            byte[] evidence = bytes ?? new byte[0];
            if(count<0)count=evidence.Length;
            if (!_capturePayloadEvidence||count > MaxEvidencePayloadBytes)
            {
                // 只限制留存证据，不改变真实网络/串口/虚拟通道的字节数据。
                string omission = "诊断仅保留元数据；原始字节数=" + count;
                error = string.IsNullOrEmpty(error) ? omission : error + "；" + omission;
                evidence = new byte[0];
            }
            else if(offset!=0||count!=evidence.Length)
            {byte[] slice=new byte[count];Buffer.BlockCopy(evidence,offset,slice,0,count);evidence=slice;}
            FTRChannelFrame entry;
            lock (_gate)
            {
                entry = new FTRChannelFrame(++_sequence, SessionId, Id, direction, evidence,
                    correlation, error, null,address);
                while (_frames.Count > 0 &&
                       (_frames.Count >= MaxEvidenceFrames ||
                        _retainedEvidenceBytes + entry.StoredByteCount > MaxEvidenceBytes))
                {
                    FTRChannelFrame removed = _frames.Dequeue();
                    _retainedEvidenceBytes -= removed.StoredByteCount;
                }
                _frames.Enqueue(entry);
                _retainedEvidenceBytes += entry.StoredByteCount;
            }
            Action<FTRChannelFrame> handler = FrameRecorded;
            if (handler != null)foreach(Action<FTRChannelFrame> listener in handler.GetInvocationList())
                try{listener(entry);}catch(Exception observerError){_diagnostics.RecordError(observerError,"Observer",FTRFaultScope.Operation);}
        }
        protected abstract Task OnOpenAsync(CancellationToken ct);
        protected abstract Task OnCloseAsync(CancellationToken ct);
        protected abstract Task OnSendAsync(byte[] bytes, FTRSubChannelAddress destination, CancellationToken ct);
    }
}
