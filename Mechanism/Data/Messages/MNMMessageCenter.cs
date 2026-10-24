using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Mechanism.Data.Messages
{
    /// <summary>订阅注册令牌：释放后不再进入新消息的订阅快照。</summary>
    public sealed class MNMMessageSubscription : IDisposable
    {
        private readonly MNMMessageCenter _owner;
        internal readonly Guid Id;
        internal readonly string SubscriberId;
        internal readonly string SharedGroup;
        internal readonly MNMTopicFilter TopicFilter;
        internal readonly Type PayloadType;
        internal readonly Func<MNMMessage, Task> Handler;
        internal readonly MNMSubscriptionOptions Options;
        private int _disposed;
        internal MNMMessageSubscription(MNMMessageCenter owner, string pattern, string shared,
            string subscriberId, Type payloadType, Func<MNMMessage,Task> handler,
            MNMSubscriptionOptions options)
        {
            _owner = owner;
            Id = Guid.NewGuid();
            TopicFilter = new MNMTopicFilter(pattern);
            SharedGroup = shared;
            SubscriberId = subscriberId;
            PayloadType = payloadType;
            Handler = handler;
            Options = options;
        }
        public string Filter { get { return TopicFilter.Filter; } }
        public bool IsActive { get { return Interlocked.CompareExchange(ref _disposed,0,0)==0; } }
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed,1)==0)
                _owner.Unsubscribe(Id);
        }
        internal void Deactivate() { Interlocked.Exchange(ref _disposed, 1); }
    }

    internal sealed class MNMDeliveryWork
    {
        internal MNMMessage Message;
        internal MNMMessageSubscription[] Subscriptions;
        internal MNMPublishReceipt Receipt;
        internal bool Retain;
        internal TaskCompletionSource<MNMPublishReceipt> Completed =
            new TaskCompletionSource<MNMPublishReceipt>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// 进程内Message Center（M36）。独立于M09高速变量、MDS Heap、M12命令和MRK执行。
    /// 一个有界FIFO工作队列；默认Queued非阻塞Publish，PublishAsync等待消费结果。
    /// 不宣称断电持久QoS1、跨进程交付或 exactly-once。
    /// </summary>
    public sealed class MNMMessageCenter : IMNMMessagePublisher, IMNMMessageSubscriber, IDisposable
    {
        private readonly object _gate = new object();
        private readonly MNMMessageOptions _options;
        private readonly List<MNMMessageSubscription> _subs = new List<MNMMessageSubscription>();
        private readonly Dictionary<string, MNMMessageTypeDefinition> _types =
            new Dictionary<string, MNMMessageTypeDefinition>(StringComparer.Ordinal);
        private readonly MNMMessageRouter _router = new MNMMessageRouter();
        private readonly MNMMessageRetryPolicy _retry;
        private readonly MNMMessageThreadPoolScheduler _parallel;
        private readonly Queue<MNMDeliveryWork> _queue = new Queue<MNMDeliveryWork>();
        private readonly Dictionary<string, MNMMessage> _retained =
            new Dictionary<string, MNMMessage>(StringComparer.Ordinal);
        private readonly Queue<string> _retainedOrder = new Queue<string>();
        private readonly Queue<MNMDeadLetter> _deadLetters = new Queue<MNMDeadLetter>();
        private readonly SemaphoreSlim _signal = new SemaphoreSlim(0);
        private TaskCompletionSource<bool> _resume;
        private readonly Task _worker;
        private MNMMessageState _state = MNMMessageState.Running;
        private long _published, _accepted, _delivered, _failed, _retried, _rejected, _dropped;
        // 最近1024次交付耗时，非持久化性能SLA；容量固定，避免热路径无限分配。
        private readonly double[] _deliveryDurations = new double[1024];
        private int _deliveryCount;
        private int _deliveryWrite;
        private readonly AsyncLocal<int> _dispatchDepth = new AsyncLocal<int>();

        public MNMMessageCenter(MNMMessageOptions options = null)
        {
            _options = CaptureOptions(options);
            _options.Validate();
            _retry = new MNMMessageRetryPolicy(_options.MaxAttempts,_options.RetryDelayMilliseconds);
            _parallel = new MNMMessageThreadPoolScheduler(_options.MaxParallelDispatches);
            _resume = NewCompleted();
            _worker = Task.Run((Func<Task>)RunWorkerAsync);
        }

        private static MNMMessageOptions CaptureOptions(MNMMessageOptions supplied)
        {
            MNMMessageOptions src = supplied ?? new MNMMessageOptions();
            return new MNMMessageOptions {
                QueueCapacity=src.QueueCapacity, MaxParallelDispatches=src.MaxParallelDispatches,
                RetainedCapacity=src.RetainedCapacity, DeadLetterCapacity=src.DeadLetterCapacity,
                MaxAttempts=src.MaxAttempts, RetryDelayMilliseconds=src.RetryDelayMilliseconds,
                OverflowPolicy=src.OverflowPolicy, AuthorizePublish=src.AuthorizePublish,
                AuthorizeSubscribe=src.AuthorizeSubscribe
            };
        }

        private static MNMSubscriptionOptions CaptureSubscription(MNMSubscriptionOptions supplied)
        {
            MNMSubscriptionOptions src = supplied ?? new MNMSubscriptionOptions();
            return new MNMSubscriptionOptions {
                SubscriberId=src.SubscriberId, ReplayRetained=src.ReplayRetained,
                SourceId=src.SourceId,SourceFilter=src.SourceFilter,TypeId=src.TypeId,
                NoLocal=src.NoLocal,Predicate=src.Predicate,
                SynchronizationContext=src.SynchronizationContext
            };
        }

        private static MNMPublishOptions CapturePublish(MNMPublishOptions supplied)
        {
            MNMPublishOptions src = supplied ?? new MNMPublishOptions();
            return new MNMPublishOptions {
                SourceId=src.SourceId,TargetId=src.TargetId,TypeId=src.TypeId,
                DispatchMode=src.DispatchMode,QoS=src.QoS,Retain=src.Retain,
                RequireSubscriber=src.RequireSubscriber,TimeToLive=src.TimeToLive,
                CorrelationId=src.CorrelationId,ReplyTo=src.ReplyTo,Headers=src.Headers
            };
        }

        private static TaskCompletionSource<bool> NewCompleted()
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            tcs.TrySetResult(true);
            return tcs;
        }
        private void DemandOpen()
        {
            if (_state == MNMMessageState.Stopped)
                throw new ObjectDisposedException("MNMMessageCenter");
        }
        public MNMMessageState State { get { lock(_gate) return _state; } }

        public void RegisterType(MNMMessageTypeDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException("definition");
            lock (_gate)
            {
                DemandOpen();
                if (_types.ContainsKey(definition.TypeId))
                    throw new InvalidOperationException("重复消息类型定义：" + definition.TypeId);
                _types.Add(definition.TypeId, definition);
            }
        }
        public MNMMessageTypeDefinition FindType(string typeId)
        {
            lock(_gate)
            {
                DemandOpen();
                MNMMessageTypeDefinition result;
                return typeId != null && _types.TryGetValue(typeId,out result) ? result : null;
            }
        }

        private static void ParseShared(string raw, MNMSubscriptionOptions options,
            out string filter, out string shared)
        {
            filter = raw;
            shared = null;
            if (raw != null && raw.StartsWith("$share/", StringComparison.Ordinal))
            {
                int pos = raw.IndexOf('/', 7);
                if (pos <= 7) throw new ArgumentException("共享订阅需有Group和Filter", "raw");
                shared = raw.Substring(7,pos-7);
                if (!MNMTopicFilter.IsCode(shared)) throw new ArgumentException("共享组名称无效", "raw");
                filter = raw.Substring(pos + 1);
            }
        }

        public IDisposable Subscribe<T>(string topicFilter, Action<MNMMessageContext<T>> handler,
            MNMSubscriptionOptions options = null)
        {
            if (handler == null) throw new ArgumentNullException("handler");
            return SubscribeAsync<T>(topicFilter, context => { handler(context); return Task.CompletedTask; },options);
        }

        public IDisposable SubscribeAsync<T>(string topicFilter, Func<MNMMessageContext<T>,Task> handler,
            MNMSubscriptionOptions options = null)
        {
            if (handler == null) throw new ArgumentNullException("handler");
            MNMSubscriptionOptions opt = CaptureSubscription(options);
            string shared, filter;
            ParseShared(topicFilter,opt,out filter,out shared);
            MNMTopicFilter parsed = new MNMTopicFilter(filter);
            string subscriberId = string.IsNullOrWhiteSpace(opt.SubscriberId) ?
                Guid.NewGuid().ToString("N") : opt.SubscriberId;
            if (_options.AuthorizeSubscribe != null &&
                !_options.AuthorizeSubscribe(subscriberId, filter))
                throw new UnauthorizedAccessException("当前Subscriber未被授权订阅：" + filter);

            Func<MNMMessage,Task> callback = async message =>
            {
                MNMMessageContext<T> ctx = new MNMMessageContext<T>(this,message);
                if (opt.SynchronizationContext == null ||
                    object.ReferenceEquals(SynchronizationContext.Current,opt.SynchronizationContext))
                {
                    await handler(ctx).ConfigureAwait(false);
                    return;
                }
                TaskCompletionSource<bool> done = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                opt.SynchronizationContext.Post(async unused =>
                {
                    try { await handler(ctx).ConfigureAwait(false); done.TrySetResult(true); }
                    catch(Exception error) { done.TrySetException(error); }
                }, null);
                await done.Task.ConfigureAwait(false);
            };

            MNMMessageSubscription sub = new MNMMessageSubscription(
                this,filter,shared,subscriberId,typeof(T),callback,opt);
            MNMMessage[] replay;
            lock (_gate)
            {
                DemandOpen();
                // 同一个客户端对同一个Filter的重复订阅是替换；不同客户端保持独立。
                MNMMessageSubscription[] old = _subs.Where(s =>
                    s.SubscriberId == subscriberId && s.Filter == filter &&
                    s.SharedGroup == shared).ToArray();
                foreach (MNMMessageSubscription item in old)
                { item.Deactivate(); _subs.Remove(item); }
                _subs.Add(sub);
                replay = opt.ReplayRetained && shared == null ?
                    _retained.Values.Where(m => !IsExpired(m) &&
                    parsed.Matches(m.Header.Topic) && typeof(T).IsAssignableFrom(m.Payload.PayloadType)).
                    ToArray() : new MNMMessage[0];
            }
            foreach (MNMMessage message in replay)
            {
                // 重放作为独立的受控队列工作；订阅失败不撤销订阅本身。
                MNMDeliveryWork work = NewWork(message,new [] { sub });
                Enqueue(work);
            }
            return sub;
        }

        internal void Unsubscribe(Guid token)
        {
            lock(_gate)
            {
                MNMMessageSubscription existing = _subs.FirstOrDefault(s => s.Id == token);
                if (existing != null) { existing.Deactivate(); _subs.Remove(existing); }
            }
        }

        public MNMPublishReceipt Publish<T>(string topic, T payload, MNMPublishOptions options = null)
        {
            MNMPublishOptions opt = CapturePublish(options);
            MNMDeliveryWork work = Prepare(topic,payload,opt);
            if (work == null) throw new InvalidOperationException("构造消息工作项失败");
            if (work.Receipt.Stage == MNMPublishStage.Rejected) return work.Receipt;
            if (opt.DispatchMode == MNMDispatchMode.Inline)
            {
                if (SynchronizationContext.Current != null)
                    throw new InvalidOperationException("在UI/自定义同步上下文不能同步Inline等待回调；请使用PublishAsync或Queued");
                lock(_gate) { _accepted++; if (work.Retain) Retain(work.Message); }
                return DispatchWithDepthAsync(work).GetAwaiter().GetResult();
            }
            if (opt.DispatchMode == MNMDispatchMode.ThreadPool)
            {
                DispatchParallel(work);
                return work.Receipt;
            }
            Enqueue(work);
            return work.Receipt;
        }

        public async Task<MNMPublishReceipt> PublishAsync<T>(string topic, T payload,
            MNMPublishOptions options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            MNMPublishOptions opt = CapturePublish(options);
            MNMDeliveryWork work = Prepare(topic,payload,opt);
            if (work.Receipt.Stage == MNMPublishStage.Rejected) return work.Receipt;
            if (opt.DispatchMode == MNMDispatchMode.Inline || _dispatchDepth.Value > 0)
            {
                // 消息Handler中await嵌套PublishAsync时，不能排队等待唯一Worker自己。
                lock(_gate) { _accepted++; if (work.Retain) Retain(work.Message); }
                return await DispatchWithDepthAsync(work).ConfigureAwait(false);
            }
            if (opt.DispatchMode == MNMDispatchMode.ThreadPool)
                DispatchParallel(work);
            else
                Enqueue(work);
            using (cancellationToken.Register(() => work.Completed.TrySetCanceled()))
                return await work.Completed.Task.ConfigureAwait(false);
        }

        public async Task<MNMPublishReceipt[]> PublishBatchAsync<T>(
            IEnumerable<KeyValuePair<string,T>> messages, MNMPublishOptions options = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (messages == null) throw new ArgumentNullException("messages");
            List<MNMPublishReceipt> result = new List<MNMPublishReceipt>();
            foreach (KeyValuePair<string,T> item in messages)
                result.Add(await PublishAsync(item.Key,item.Value,options,cancellationToken).ConfigureAwait(false));
            return result.ToArray();
        }

        private MNMDeliveryWork Prepare<T>(string topic,T payload,MNMPublishOptions options)
        {
            MNMTopicFilter.ValidateTopic(topic);
            if (!Enum.IsDefined(typeof(MNMDispatchMode), options.DispatchMode) ||
                !Enum.IsDefined(typeof(MNMMessageQoS), options.QoS))
                throw new ArgumentException("无效消息分发或QoS选项");
            if (options.TimeToLive.HasValue && options.TimeToLive.Value <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException("TimeToLive");
            if (!string.IsNullOrEmpty(options.ReplyTo))
                MNMTopicFilter.ValidateTopic(options.ReplyTo);
            if (_options.AuthorizePublish != null &&
                !_options.AuthorizePublish(options.SourceId,topic))
                throw new UnauthorizedAccessException("发布者未经授权：" + topic);
            MNMMessageTypeDefinition definition = null;
            lock(_gate)
            {
                DemandOpen();
                if (!string.IsNullOrWhiteSpace(options.TypeId) &&
                    !_types.TryGetValue(options.TypeId,out definition))
                    throw new InvalidOperationException("未注册强类型消息：" + options.TypeId);
            }
            MNMMessage message = new MNMMessage(topic,payload,options,definition);
            MNMMessageSubscription[] receivers;
            lock(_gate)
            {
                DemandOpen();
                _published++;
                receivers = _router.Resolve(_subs,message);
            }
            MNMDeliveryWork work = NewWork(message,receivers);
            if (options.RequireSubscriber && receivers.Length == 0)
            {
                work.Receipt.Stage = MNMPublishStage.Rejected;
                work.Receipt.Error = "没有匹配且受权的订阅者";
                lock(_gate) _rejected++;
                work.Completed.TrySetResult(work.Receipt);
            }
            else if ((options.DispatchMode == MNMDispatchMode.Inline ||
                options.DispatchMode == MNMDispatchMode.ThreadPool) && State == MNMMessageState.Paused)
            {
                work.Receipt.Stage = MNMPublishStage.Rejected;
                work.Receipt.Error = "已暂停消息分发，Inline被拒绝";
                lock(_gate) _rejected++;
                work.Completed.TrySetResult(work.Receipt);
            }
            else
            {
                work.Retain = options.Retain;
            }
            return work;
        }

        private static bool IsExpired(MNMMessage m)
        { return m.Header.ExpiresUtc.HasValue && m.Header.ExpiresUtc.Value <= DateTime.UtcNow; }

        private static MNMDeliveryWork NewWork(MNMMessage message,MNMMessageSubscription[] subs)
        {
            return new MNMDeliveryWork
            {
                Message = message, Subscriptions = subs,
                Receipt = new MNMPublishReceipt
                {
                    MessageId = message.Header.MessageId,
                    Stage = MNMPublishStage.Accepted,
                    MatchedCount = subs.Length
                }
            };
        }

        private void Retain(MNMMessage message)
        {
            if (_options.RetainedCapacity == 0) return;
            if (!_retained.ContainsKey(message.Header.Topic))
                _retainedOrder.Enqueue(message.Header.Topic);
            _retained[message.Header.Topic]=message;
            while (_retained.Count > _options.RetainedCapacity)
            {
                if (_retainedOrder.Count == 0) break;
                _retained.Remove(_retainedOrder.Dequeue());
            }
        }

        private void DispatchParallel(MNMDeliveryWork work)
        {
            lock (_gate)
            {
                if (_state == MNMMessageState.Stopped)
                { Reject(work,"消息中心已停止"); return; }
                if (_state == MNMMessageState.Paused)
                { Reject(work,"暂停时不能提交ThreadPool分发"); return; }
                if (!_parallel.TrySchedule(async () =>
                {
                    try { await DispatchWithDepthAsync(work).ConfigureAwait(false); }
                    catch (Exception error)
                    {
                        work.Receipt.Stage = MNMPublishStage.Failed;
                        work.Receipt.Error = error.GetType().Name + ": " + error.Message;
                        work.Receipt.FailedCount = work.Subscriptions.Length;
                        lock (_gate) _failed += work.Subscriptions.Length;
                        work.Completed.TrySetResult(work.Receipt);
                    }
                }))
                { Reject(work,"ThreadPool并发额度已满"); return; }
                ++_accepted;
                if (work.Retain) Retain(work.Message);
            }
        }

        private void Enqueue(MNMDeliveryWork work)
        {
            lock(_gate)
            {
                if (_state == MNMMessageState.Stopped)
                {
                    Reject(work,"消息中心已经停止");
                    return;
                }
                if (_queue.Count >= _options.QueueCapacity)
                {
                    if (_options.OverflowPolicy == MNMOverflowPolicy.Reject)
                    {
                        Reject(work,"消息队列已满");
                        return;
                    }
                    if (_queue.Peek().Message.Header.QoS == MNMMessageQoS.AtLeastOnceInProcess)
                    {
                        Reject(work,"QoS1排队消息不能按DropOldest丢弃");
                        return;
                    }
                    MNMDeliveryWork dropped = _queue.Dequeue();
                    _dropped++;
                    Reject(dropped,"队列溢出，旧消息被丢弃");
                }
                _queue.Enqueue(work);
                if (work.Retain) Retain(work.Message);
                _accepted++;
                _signal.Release();
            }
        }

        private void Reject(MNMDeliveryWork work,string reason)
        {
            work.Receipt.Stage = MNMPublishStage.Rejected;
            work.Receipt.Error = reason;
            _rejected++;
            work.Completed.TrySetResult(work.Receipt);
        }

        private async Task RunWorkerAsync()
        {
            while (true)
            {
                await _signal.WaitAsync().ConfigureAwait(false);
                Task resume;
                lock(_gate) resume = _resume.Task;
                await resume.ConfigureAwait(false);
                MNMDeliveryWork work;
                lock(_gate)
                {
                    if (_queue.Count == 0)
                    {
                        if (_state == MNMMessageState.Stopped) return;
                        continue;
                    }
                    work = _queue.Dequeue();
                }
                await DispatchWithDepthAsync(work).ConfigureAwait(false);
            }
        }

        private async Task<MNMPublishReceipt> DispatchWithDepthAsync(MNMDeliveryWork work)
        {
            int previous = _dispatchDepth.Value;
            if (previous >= 16) throw new InvalidOperationException("嵌套消息深度超过16，拒绝无限递归");
            _dispatchDepth.Value = previous + 1;
            try { return await DeliverAsync(work).ConfigureAwait(false); }
            finally { _dispatchDepth.Value = previous; }
        }

        private async Task<MNMPublishReceipt> DeliverAsync(MNMDeliveryWork work)
        {
            if (IsExpired(work.Message))
            {
                work.Receipt.Stage = MNMPublishStage.Failed;
                work.Receipt.FailedCount = work.Subscriptions.Length;
                work.Receipt.Error = "消息在消费前过期";
                lock(_gate) _failed += work.Subscriptions.Length;
                RecordDeliveryLatency(work.Message);
                work.Completed.TrySetResult(work.Receipt);
                return work.Receipt;
            }
            foreach (MNMMessageSubscription s in work.Subscriptions)
            {
                if (!s.IsActive) continue;
                if (s.Options.Predicate != null)
                {
                    bool passes;
                    try { passes = s.Options.Predicate(work.Message); }
                    catch (Exception error)
                    {
                        RecordFailure(work,s,error);
                        continue;
                    }
                    if (!passes) continue;
                }
                Exception last = null;
                int attempts = _retry.Attempts(work.Message.Header.QoS);
                for (int i = 0; i < attempts; ++i)
                {
                    if (!s.IsActive || IsExpired(work.Message)) break;
                    try
                    {
                        await s.Handler(work.Message).ConfigureAwait(false);
                        last = null;
                        ++work.Receipt.DeliveredCount;
                        lock(_gate) ++_delivered;
                        break;
                    }
                    catch(Exception error)
                    {
                        last = error;
                        if (i < attempts-1)
                        {
                            lock(_gate) ++_retried;
                            await _retry.BackoffAsync().ConfigureAwait(false);
                        }
                    }
                }
                if (last != null) RecordFailure(work,s,last);
            }
            if (work.Receipt.FailedCount > 0)
            { work.Receipt.Stage = MNMPublishStage.Failed; work.Receipt.Error = "部分消息回调失败，已记录死信"; }
            else if (work.Receipt.DeliveredCount > 0)
                work.Receipt.Stage = MNMPublishStage.Delivered;
            // 零接收方、被过滤或订阅已注销时仅是Accepted，不伪造Delivered。
            RecordDeliveryLatency(work.Message);
            work.Completed.TrySetResult(work.Receipt);
            return work.Receipt;
        }

        private void RecordDeliveryLatency(MNMMessage message)
        {
            double ms=(DateTime.UtcNow-message.Header.CreatedUtc).TotalMilliseconds;
            lock(_gate)
            {
                _deliveryDurations[_deliveryWrite]=Math.Max(0,ms);
                _deliveryWrite=(_deliveryWrite+1)%_deliveryDurations.Length;
                if (_deliveryCount<_deliveryDurations.Length) ++_deliveryCount;
            }
        }

        private void RecordFailure(MNMDeliveryWork work,MNMMessageSubscription s,Exception error)
        {
            ++work.Receipt.FailedCount;
            lock(_gate)
            {
                ++_failed;
                if (_options.DeadLetterCapacity <= 0) return;
                while (_deadLetters.Count >= _options.DeadLetterCapacity)
                    _deadLetters.Dequeue();
                _deadLetters.Enqueue(new MNMDeadLetter
                {
                    MessageId=work.Message.Header.MessageId,
                    Topic=work.Message.Header.Topic,
                    SubscriberId=s.SubscriberId,
                    Error=error.GetType().Name + ": " + error.Message,
                    OccurredUtc=DateTime.UtcNow
                });
            }
        }

        public MNMDeadLetter[] DeadLetters()
        { lock(_gate) return _deadLetters.ToArray(); }

        public MNMMessageMetrics Metrics()
        {
            lock(_gate)
            {
                double[] sample = new double[_deliveryCount];
                Array.Copy(_deliveryDurations,sample,_deliveryCount);
                Array.Sort(sample);
                double sum=0; foreach(double x in sample) sum+=x;
                return new MNMMessageMetrics
                {
                    LatencySamples=sample.Length,
                    MeanDeliveryMilliseconds=sample.Length==0?0:sum/sample.Length,
                    P95DeliveryMilliseconds=sample.Length==0?0:sample[(int)Math.Ceiling(sample.Length*0.95)-1],
                    P99DeliveryMilliseconds=sample.Length==0?0:sample[(int)Math.Ceiling(sample.Length*0.99)-1],
                    Published=_published,Accepted=_accepted,Delivered=_delivered,Failed=_failed,
                    Retried=_retried,Rejected=_rejected,Dropped=_dropped,
                    QueueDepth=_queue.Count,ParallelInFlight=_parallel.InFlight,
                    SubscriptionCount=_subs.Count,
                    RetainedCount=_retained.Count,DeadLetterCount=_deadLetters.Count
                };
            }
        }

        public void Pause()
        {
            lock(_gate)
            {
                DemandOpen();
                if (_state == MNMMessageState.Paused) return;
                _state=MNMMessageState.Paused;
                _resume=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
        public void Resume()
        {
            lock(_gate)
            {
                DemandOpen();
                _state=MNMMessageState.Running;
                _resume.TrySetResult(true);
            }
        }

        public async Task StopAsync(bool drain = true)
        {
            lock(_gate)
            {
                if (_state != MNMMessageState.Stopped)
                {
                    _state=MNMMessageState.Stopped;
                    _resume.TrySetResult(true);
                    if (!drain)
                    {
                        while(_queue.Count > 0) Reject(_queue.Dequeue(),"消息中心停止，队列消息取消");
                    }
                    if (!drain)
                    {
                        foreach(MNMMessageSubscription s in _subs) s.Deactivate();
                        _subs.Clear();
                    }
                    _signal.Release();
                }
            }
            await _worker.ConfigureAwait(false);
            await _parallel.DrainAsync().ConfigureAwait(false);
            lock(_gate)
            {
                foreach(MNMMessageSubscription s in _subs) s.Deactivate();
                _subs.Clear();
                _retained.Clear();
                _deadLetters.Clear();
            }
        }

        public void Dispose()
        {
            // 不能从正在执行的回调线程同步等待Worker，否则会发生死锁。
            lock(_gate)
            {
                if (_state == MNMMessageState.Stopped) return;
                _state=MNMMessageState.Stopped;
                _resume.TrySetResult(true);
                while(_queue.Count > 0) Reject(_queue.Dequeue(),"Dispose取消队列中的消息");
                foreach(MNMMessageSubscription s in _subs) s.Deactivate();
                _subs.Clear();
                _retained.Clear();
                _deadLetters.Clear();
                _signal.Release();
            }
        }

        public async Task<MNMMessageContext<TResponse>> RequestAsync<TRequest,TResponse>(
            string requestTopic,TRequest payload,string responseTopic,TimeSpan timeout,
            CancellationToken cancellationToken=default(CancellationToken))
        {
            if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException("timeout");
            MNMTopicFilter.ValidateTopic(responseTopic);
            Guid correlation = Guid.NewGuid();
            TaskCompletionSource<MNMMessageContext<TResponse>> pending =
                new TaskCompletionSource<MNMMessageContext<TResponse>>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            using (Subscribe<TResponse>(responseTopic,ctx =>
            {
                if (ctx.Header.CorrelationId == correlation) pending.TrySetResult(ctx);
            },new MNMSubscriptionOptions { ReplayRetained=false }))
            {
                // 超时从发送开始计时，覆盖发布处理及回复等待，取消只是停止观察。
                Task timeoutTask = Task.Delay(timeout,cancellationToken);
                Task<MNMPublishReceipt> sending = PublishAsync(requestTopic,payload,
                    new MNMPublishOptions { CorrelationId=correlation,ReplyTo=responseTopic,
                        QoS=MNMMessageQoS.AtLeastOnceInProcess,RequireSubscriber=true },
                    cancellationToken);
                Task first = await Task.WhenAny(sending,timeoutTask).ConfigureAwait(false);
                if (first != sending)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new TimeoutException("消息请求发送等待超时；副作用状态未知");
                }
                MNMPublishReceipt sent = await sending.ConfigureAwait(false);
                if (sent.Stage == MNMPublishStage.Rejected || sent.Stage == MNMPublishStage.Failed)
                    throw new InvalidOperationException("请求发送失败：" + sent.Error);
                Task done = await Task.WhenAny(pending.Task,timeoutTask).ConfigureAwait(false);
                if (done == pending.Task) return await pending.Task.ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException("等待消息请求响应超时，不能推断业务动作未发生");
            }
        }
    }
}
