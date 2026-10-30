using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Mechanism.Core.Modules;

namespace CxVisual.Mechanism.Lifecycle.Modules
{
    public enum MLCServiceModuleState { Stopped, Starting, Running, Stopping, Faulted }

    /// <summary>
    /// Immutable, in-process, owner-derived observation. Lifecycle Running is
    /// NOT the same as verified service/dependency readiness. Null means
    /// no provider has supplied actual readiness evidence.
    /// </summary>
    public sealed class MLCServiceModuleObservation
    {
        internal MLCServiceModuleObservation(string moduleId, string name, int version,
            MLCServiceModuleState state, bool? verifiedReady, DateTime observedAtUtc)
        {
            ModuleId = moduleId;
            Name = name;
            Version = version;
            State = state;
            VerifiedReady = verifiedReady;
            ObservedAtUtc = observedAtUtc;
        }
        public string ModuleId { get; private set; }
        public string Name { get; private set; }
        public int Version { get; private set; }
        public MLCServiceModuleState State { get; private set; }
        public bool LifecycleRunning { get { return State == MLCServiceModuleState.Running; } }
        public bool? VerifiedReady { get; private set; }
        public DateTime ObservedAtUtc { get; private set; }
    }

    // 复用已有 Lifecycle 所属项目；Host/工作队列/MRK 的唯一 Owner 不变。
    // OnStartAsync/OnStopAsync 必须由真实专业服务完成，没有成功的空壳默认实现。
    public abstract class MLCServiceModuleBase : IMLCServiceModule
    {
        private readonly SemaphoreSlim _operationGate = new SemaphoreSlim(1, 1);
        private readonly object _stateGate = new object();
        private MLCServiceModuleState _state = MLCServiceModuleState.Stopped;

        protected MLCServiceModuleBase(MFMModuleDescriptor descriptor, MLCServiceModuleConfiguration options = null)
        {
            if (descriptor == null) throw new ArgumentNullException("descriptor");
            if (descriptor.Kind != MFMModuleKind.Service) throw new ArgumentException("不是 Service 模块");
            Descriptor = descriptor; Options = options ?? new MLCServiceModuleConfiguration();
            Options.Validate();
        }
        public MFMModuleDescriptor Descriptor { get; private set; }
        public MLCServiceModuleConfiguration Options { get; private set; }
        public MLCServiceModuleState State { get { lock (_stateGate) return _state; } }
        public bool IsReady { get { return State == MLCServiceModuleState.Running; } }
        private void SetState(MLCServiceModuleState state) { lock (_stateGate) _state = state; }

        /// <summary>
        /// Read-only state of THIS running module instance, not a Host registry,
        /// OS process identity or a remote Supervision service. Providers may
        /// supply actual dependency/handler evidence via TryObserveReadiness.
        /// A successful OnStart alone MUST NOT become VerifiedReady=true.
        /// </summary>
        public MLCServiceModuleObservation Observe()
        {
            MLCServiceModuleState lifecycle = State;
            bool? verified = lifecycle == MLCServiceModuleState.Running
                ? (bool?)null : false;
            if (lifecycle == MLCServiceModuleState.Running)
            {
                bool ready;
                try
                {
                    if (TryObserveReadiness(out ready)) verified = ready;
                }
                catch (Exception)
                {
                    // Diagnostic failures must never mutate the Service state
                    // or fabricate healthy readiness. Unknown remains null.
                }
            }
            // A transition may happen while a provider checks its real
            // dependencies. Read again without holding _stateGate across a
            // professional callback (which could invert lock ordering).
            MLCServiceModuleState finalState = State;
            if (finalState != lifecycle)
                verified = finalState == MLCServiceModuleState.Running ? (bool?)null : false;
            return new MLCServiceModuleObservation(Descriptor.Id, Descriptor.Name,
                Descriptor.Version, finalState, verified, DateTime.UtcNow);
        }

        /// <summary>
        /// Return false when actual readiness evidence is unavailable.
        /// Do not infer readiness from Running, PID, listening port, etc.
        /// This hook must be side-effect free, fast and safe under observation.
        /// </summary>
        protected virtual bool TryObserveReadiness(out bool ready)
        {
            ready = false;
            return false;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (IsReady) return;
                if (State != MLCServiceModuleState.Stopped)
                    throw new InvalidOperationException("服务尚未停止或需要故障恢复");
                Options.Validate(); SetState(MLCServiceModuleState.Starting);
                try
                {
                    await OnStartAsync(cancellationToken).ConfigureAwait(false);
                    SetState(MLCServiceModuleState.Running);
                }
                catch { SetState(MLCServiceModuleState.Faulted); throw; }
            }
            finally { _operationGate.Release(); }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (State == MLCServiceModuleState.Stopped) return;
                if (State != MLCServiceModuleState.Running && State != MLCServiceModuleState.Faulted)
                    throw new InvalidOperationException("服务无法从当前状态停止");
                SetState(MLCServiceModuleState.Stopping);
                try
                {
                    await OnStopAsync(cancellationToken).ConfigureAwait(false);
                    SetState(MLCServiceModuleState.Stopped);
                }
                catch { SetState(MLCServiceModuleState.Faulted); throw; }
            }
            finally { _operationGate.Release(); }
        }
        protected abstract Task OnStartAsync(CancellationToken cancellationToken);
        protected abstract Task OnStopAsync(CancellationToken cancellationToken);
    }
}
