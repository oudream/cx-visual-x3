using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Commands;
using CxVisual.Foundation.Data.Types;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Runtime.Execution;

namespace CxVisual.Mechanism.Runtime.Commands
{
    /// <summary>复用F19唯一队列与公平性，不另写Queue/ThreadPool调度。每Host一个共享Worker。</summary>
    public sealed class MRKCommandDispatcher
    {
        private readonly FCRWorker _worker;
        public MRKCommandDispatcher(int maxConcurrent = 4, int queueCapacity = 64)
        {
            if (maxConcurrent < 1 || queueCapacity < 1)
                throw new ArgumentOutOfRangeException("maxConcurrent/queueCapacity");
            _worker = new FCRWorker(new FCRWorkerOptions(maxConcurrent, queueCapacity));
        }
        internal FCRWorkHandle<bool> Enqueue(Func<CancellationToken, Task<bool>> job,
            string key, int priority, CancellationToken cancellation)
        {
            if (job == null) throw new ArgumentNullException("job");
            return _worker.Submit<bool>(job, key, false, cancellation, priority);
        }
        public FCRWorkerSnapshot Snapshot() { return _worker.GetSnapshot(); }
        public Task StopAsync(bool cancelPending = false)
        { return _worker.StopAsync(cancelPending); }
    }

    /// <summary>
    /// 唯一Handler执行管线：资源租约、拦截器、Schema、业务返回值与MRK操作账本。
    /// 无第二套调度器；由Center经Dispatcher按Direct/ThreadPool/Queued调度。
    /// </summary>
    public sealed class MRKCommandExecutor
    {
        private readonly MRKExternalOperationTracker _tracker;
        private readonly MRKResourceLeaseCoordinator _leases;
        private readonly FDTDataAdapterRegistry _adapters;
        private readonly Func<IMRKCommandInterceptor[]> _interceptors;
        internal MRKCommandExecutor(MRKExternalOperationTracker tracker,
            MRKResourceLeaseCoordinator leases, FDTDataAdapterRegistry adapters,
            Func<IMRKCommandInterceptor[]> interceptors)
        {
            _tracker = tracker ?? throw new ArgumentNullException("tracker");
            _leases = leases ?? throw new ArgumentNullException("leases");
            _adapters = adapters;
            _interceptors = interceptors ?? throw new ArgumentNullException("interceptors");
        }
        internal async Task ExecuteAsync(MRKCommandExecution operation, MRKCommandRegistry.Entry entry)
        {
            MRKCommandRequest request = operation.Request;
            MRKCommandContext context = new MRKCommandContext(request, operation.Descriptor, operation.Report,
                operation.WaitWhilePausedAsync, operation.ReportIntermediate);
            MRKCommandResponse response;
            MRKResourceLease lease = null;
            bool handlerCompleted = false;
            try
            {
                operation.Cancellation.Token.ThrowIfCancellationRequested();
                if (!string.IsNullOrWhiteSpace(operation.Descriptor.Policy.ResourceId))
                    lease = await _leases.AcquireAsync(operation.Descriptor.Policy.ResourceId,
                        operation.OperationId.ToString("N"), operation.Cancellation.Token).ConfigureAwait(false);
                operation.Running();
                IMRKCommandInterceptor[] interceptors = _interceptors();
                // 拦截器与Handler只得到隔离副本，不允许修改幂等指纹的唯一输入快照。
                foreach (IMRKCommandInterceptor interceptor in interceptors)
                    await interceptor.BeforeAsync(context,
                        FDCCommandValueSnapshot.Capture(operation.Inputs, _adapters),
                        operation.Cancellation.Token).ConfigureAwait(false);
                Task<MRKCommandHandlerResult> pending = entry.Handler.ExecuteAsync(context,
                    FDCCommandValueSnapshot.Capture(operation.Inputs, _adapters),
                    operation.Cancellation.Token);
                if (pending == null) throw new InvalidOperationException("Handler返回空Task");
                MRKCommandHandlerResult result = await pending.ConfigureAwait(false);
                if (result == null) throw new InvalidOperationException("Handler未返回业务结果");
                IReadOnlyDictionary<string, object> outputs =
                    operation.Descriptor.Definition.ValidateOutputs(result.Outputs);
                response = new MRKCommandResponse(request.RequestId, operation.OperationId,
                    MRKCommandStage.Accepted, MRKCommandState.Completed,
                    outputs, result.BusinessSuccess, result.BusinessCode,
                    durationMs: operation.DurationMs, adapters: _adapters);
                handlerCompleted = true;
                // 完成拦截器故障不可误称设备未动作：返回Unknown供上层独立核对。
                foreach (IMRKCommandInterceptor interceptor in interceptors)
                    await interceptor.AfterAsync(context, response).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (operation.Cancellation.IsCancellationRequested)
            {
                response = new MRKCommandResponse(request.RequestId, operation.OperationId,
                    MRKCommandStage.Accepted, MRKCommandState.Cancelled,
                    errorCode: "Cancelled", error: "Handler协作取消；设备状态须另外确认",
                    durationMs: operation.DurationMs);
            }
            catch (Exception ex)
            {
                response = new MRKCommandResponse(request.RequestId, operation.OperationId,
                    MRKCommandStage.Accepted,
                    handlerCompleted ? MRKCommandState.Unknown : MRKCommandState.Failed,
                    errorCode: handlerCompleted ? "PostExecutionHookFailure" :
                        (ex is UnauthorizedAccessException ? "Unauthorized" : "HandlerFailure"),
                    error: ex.Message, durationMs: operation.DurationMs);
            }
            finally
            {
                if (lease != null) lease.Dispose();
                entry.Exit();
            }
            operation.Finish(response);
            MRKExternalOperation tracked;
            if (_tracker.TryGet(operation.OperationId, out tracked))
            {
                if (response.State == MRKCommandState.Completed) tracked.Confirm();
                else if (response.State != MRKCommandState.Unknown) tracked.Fail(); // MRK.Failed为Provider结算，不等于现场安全停止。
            }
        }

    }
}
