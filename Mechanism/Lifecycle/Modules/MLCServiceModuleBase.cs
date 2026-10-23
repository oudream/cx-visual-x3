using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Mechanism.Core.Modules;

namespace CxVisual.Mechanism.Lifecycle.Modules
{
    public enum MLCServiceModuleState { Stopped, Starting, Running, Stopping, Faulted }

    public sealed class MLCServiceModuleOptions
    {
        public TimeSpan StartupTimeout { get; set; } = TimeSpan.FromSeconds(30);
        public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(30);
        public int MaxConcurrentRequests { get; set; } = 1;
        public string[] DependencyRefs { get; set; } = new string[0];
        public void Validate()
        {
            if (StartupTimeout <= TimeSpan.Zero || ShutdownTimeout <= TimeSpan.Zero ||
                MaxConcurrentRequests < 1 || DependencyRefs == null ||
                DependencyRefs.Any(string.IsNullOrWhiteSpace) ||
                DependencyRefs.Distinct(StringComparer.Ordinal).Count() != DependencyRefs.Length)
                throw new ArgumentException("服务依赖/运行配置无效");
        }
    }

    // 复用已有 Lifecycle 所属项目；Host/工作队列/MRK 的唯一 Owner 不变。
    // OnStartAsync/OnStopAsync 必须由真实专业服务完成，没有成功的空壳默认实现。
    public abstract class MLCServiceModuleBase : IMLCServiceModule
    {
        private readonly SemaphoreSlim _operationGate = new SemaphoreSlim(1, 1);
        private readonly object _stateGate = new object();
        private MLCServiceModuleState _state = MLCServiceModuleState.Stopped;

        protected MLCServiceModuleBase(MFMModuleDescriptor descriptor, MLCServiceModuleOptions options = null)
        {
            if (descriptor == null) throw new ArgumentNullException("descriptor");
            if (descriptor.Kind != MFMModuleKind.Service) throw new ArgumentException("不是 Service 模块");
            Descriptor = descriptor; Options = options ?? new MLCServiceModuleOptions();
            Options.Validate();
        }
        public MFMModuleDescriptor Descriptor { get; private set; }
        public MLCServiceModuleOptions Options { get; private set; }
        public MLCServiceModuleState State { get { lock (_stateGate) return _state; } }
        public bool IsReady { get { return State == MLCServiceModuleState.Running; } }
        private void SetState(MLCServiceModuleState state) { lock (_stateGate) _state = state; }

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
