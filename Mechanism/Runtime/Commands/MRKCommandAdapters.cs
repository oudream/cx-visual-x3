using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Data.RuntimeData;

namespace CxVisual.Mechanism.Runtime.Commands
{
    /// <summary>
    /// 命令输入端口引用既有 MDSDataBinding；来源、类型、常量、表达式标识和作用域语义
    /// 均由 MDS 定义，命令中心不复制 Schema、点表或表达式求值引擎。
    /// </summary>
    public sealed class MRKCommandInputSource
    {
        public MRKCommandInputSource(string parameter, MDSDataBinding binding)
        {
            if (string.IsNullOrWhiteSpace(parameter))
                throw new ArgumentException("命令入参名称不能为空", "parameter");
            Parameter = parameter;
            Binding = binding ?? throw new ArgumentNullException("binding");
        }

        public string Parameter { get; private set; }
        public MDSDataBinding Binding { get; private set; }
    }

    /// <summary>
    /// 同步线程亲和SDK的Handler适配器。真实SDK操作只在F19专用线程执行；
    /// 不声称async/await续体具有线程亲和性；Executor由外部Host管理其StopAsync生命周期。
    /// </summary>
    public sealed class MRKDedicatedCommandHandler : IMRKCommandHandler
    {
        private readonly FCRDedicatedExecutor _executor;
        private readonly Func<MRKCommandContext, IReadOnlyDictionary<string, object>,
            CancellationToken, MRKCommandHandlerResult> _execute;

        public MRKDedicatedCommandHandler(FCRDedicatedExecutor executor,
            Func<MRKCommandContext, IReadOnlyDictionary<string, object>,
                CancellationToken, MRKCommandHandlerResult> execute)
        {
            _executor = executor ?? throw new ArgumentNullException("executor");
            _execute = execute ?? throw new ArgumentNullException("execute");
        }

        public Task<MRKCommandHandlerResult> ExecuteAsync(MRKCommandContext context,
            IReadOnlyDictionary<string, object> inputs, CancellationToken cancellation)
        {
            if (context == null) throw new ArgumentNullException("context");
            if (inputs == null) throw new ArgumentNullException("inputs");
            cancellation.ThrowIfCancellationRequested();
            return _executor.InvokeAsync(token => _execute(context, inputs, token), cancellation);
        }
    }
}
