using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Infrastructure.Concurrency;

namespace CxVisual.Mechanism.Runtime.Execution
{
    /// <summary>
    /// 软件开发者的代码优先编排：仅创建瞬时CompileRequest，不保存第二套工程树。
    /// 复杂节点可使用Add保留完整的编译契约。
    /// </summary>
    public sealed class MRKCodeFlowBuilder
    {
        private readonly string _id;
        private readonly List<MRKStepCompileRequest> _steps =
            new List<MRKStepCompileRequest>();
        internal MRKCodeFlowBuilder(string id) { _id = id; }
        public MRKCodeFlowBuilder Step(string id, string capabilityId,
            params string[] after)
        {
            return Add(new MRKStepCompileRequest(id, capabilityId, after));
        }
        public MRKCodeFlowBuilder Subflow(string id, string flowId,
            params string[] after)
        {
            return Add(new MRKStepCompileRequest(id, null, after, subflowId:flowId));
        }
        public MRKCodeFlowBuilder Add(MRKStepCompileRequest definition)
        {
            if (definition == null) throw new ArgumentNullException("definition");
            if (string.IsNullOrWhiteSpace(definition.Id))
                throw new ArgumentException("节点ID不能为空", "definition");
            if (_steps.Any(x => x.Id == definition.Id))
                throw new ArgumentException("同一流程中节点ID重复：" + definition.Id);
            _steps.Add(definition);
            return this;
        }
        internal MRKFlowCompileDefinition Freeze()
        { return new MRKFlowCompileDefinition(_id, _steps); }
    }

    /// <summary>编排检查失败时不创建Worker或部分启动Flow。</summary>
    public sealed class MRKCodeRuntimeBuildException : InvalidOperationException
    {
        internal MRKCodeRuntimeBuildException(string flowId,
            IList<MRKCompileDiagnostic> diagnostics)
            : base("运行流程编译失败：" + flowId + " / " +
                string.Join("; ", diagnostics.Select(x => x.Code + "@" + x.StepId +
                    ": " + x.Message)))
        {
            FlowId = flowId;
            Diagnostics = new ReadOnlyCollection<MRKCompileDiagnostic>(
                new List<MRKCompileDiagnostic>(diagnostics));
        }
        public string FlowId { get; private set; }
        public IList<MRKCompileDiagnostic> Diagnostics { get; private set; }
    }

    /// <summary>
    /// 开发/模拟场景的简洁入口，构建后能力与流程定义均固定。
    /// 生产必须经授权Publisher/PlanSlot/RuntimeAccessGateway；
    /// 该类不执行生产发布或设备安全联锁。
    /// </summary>
    public sealed class MRKCodeRuntimeBuilder
    {
        private readonly Dictionary<string, MRKResolvedCapability> _capabilities =
            new Dictionary<string, MRKResolvedCapability>(StringComparer.Ordinal);
        private readonly Dictionary<string, MRKFlowCompileDefinition> _flows =
            new Dictionary<string, MRKFlowCompileDefinition>(StringComparer.Ordinal);

        public MRKCodeRuntimeBuilder Capability(string id,
            Func<MRKExecutionContext, CancellationToken, Task<object>> action,
            Type outputType = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("能力ID不能为空", "id");
            if (action == null) throw new ArgumentNullException("action");
            if (_capabilities.ContainsKey(id))
                throw new ArgumentException("重复能力：" + id, "id");
            _capabilities.Add(id, new MRKResolvedCapability(action,
                outputType == null ? null : new MRKExecutionOutput(outputType)));
            return this;
        }

        public MRKCodeRuntimeBuilder Capability<TResult>(string id,
            Func<MRKExecutionContext, CancellationToken, Task<TResult>> action)
        {
            if (action == null) throw new ArgumentNullException("action");
            return Capability(id, async (context, token) =>
                (object)await action(context, token).ConfigureAwait(false),
                typeof(TResult));
        }

        public MRKCodeRuntimeBuilder Flow(string id,
            Action<MRKCodeFlowBuilder> configure)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("流程ID不能为空", "id");
            if (configure == null) throw new ArgumentNullException("configure");
            if (_flows.ContainsKey(id))
                throw new ArgumentException("重复流程：" + id, "id");
            MRKCodeFlowBuilder draft = new MRKCodeFlowBuilder(id);
            configure(draft);
            _flows.Add(id, draft.Freeze());
            return this;
        }

        public MRKDevelopmentRuntime Build(FCRWorkerOptions options = null)
        {
            if (_flows.Count == 0)
                throw new InvalidOperationException("至少注册一个流程");
            // 冻结能力解析与流程引用：构建后不受Builder再次注册影响。
            Dictionary<string, MRKResolvedCapability> capabilities =
                new Dictionary<string, MRKResolvedCapability>(_capabilities,
                    StringComparer.Ordinal);
            Dictionary<string, MRKFlowCompileDefinition> definitions =
                new Dictionary<string, MRKFlowCompileDefinition>(_flows,
                    StringComparer.Ordinal);
            IMRKCapabilityResolver resolver = new CodeResolver(capabilities);
            IMRKFlowCompileCatalog catalog = new CodeCatalog(definitions);
            MRKExecutionOrchestrationCompiler compiler =
                new MRKExecutionOrchestrationCompiler();
            Dictionary<string, MRKExecutionPlan> plans =
                new Dictionary<string, MRKExecutionPlan>(StringComparer.Ordinal);
            // 在创建Worker之前验证所有流程，即使它们尚未被调用。
            foreach (string id in definitions.Keys.OrderBy(x => x, StringComparer.Ordinal))
            {
                MRKCompileResult result = compiler.Compile(id, catalog, resolver);
                if (!result.Succeeded)
                    throw new MRKCodeRuntimeBuildException(id, result.Diagnostics);
                plans.Add(id, result.Plan);
            }
            return new MRKDevelopmentRuntime(new MRKExecutionEngine(
                options ?? new FCRWorkerOptions(2, 64)), plans);
        }

        private sealed class CodeResolver : IMRKCapabilityResolver
        {
            private readonly IDictionary<string, MRKResolvedCapability> _values;
            internal CodeResolver(IDictionary<string, MRKResolvedCapability> values)
            { _values = values; }
            public bool TryResolve(string id, out MRKResolvedCapability value)
            { return _values.TryGetValue(id, out value); }
        }
        private sealed class CodeCatalog : IMRKFlowCompileCatalog
        {
            private readonly IDictionary<string, MRKFlowCompileDefinition> _values;
            internal CodeCatalog(IDictionary<string, MRKFlowCompileDefinition> values)
            { _values = values; }
            public bool TryGet(string id, out MRKFlowCompileDefinition value)
            { return _values.TryGetValue(id, out value); }
        }
    }

    /// <summary>
    /// 多流程共享唯一ExecutionEngine/F19 Worker的进程内开发运行门面。
    /// RunAsync返回明确的FlowResult：Failure/Cancelled不是隐式成功。
    /// 使用完毕须await StopAsync；非协作任务停止时仍需等待真实结算。
    /// </summary>
    public sealed class MRKDevelopmentRuntime
    {
        private readonly MRKExecutionEngine _engine;
        private readonly IDictionary<string, MRKExecutionPlan> _plans;
        internal MRKDevelopmentRuntime(MRKExecutionEngine engine,
            IDictionary<string, MRKExecutionPlan> plans)
        {
            _engine = engine;
            _plans = new ReadOnlyDictionary<string, MRKExecutionPlan>(plans);
        }
        public IList<string> FlowIds
        { get { return new ReadOnlyCollection<string>(_plans.Keys.OrderBy(x => x,
            StringComparer.Ordinal).ToList()); } }

        public MRKFlowHandle StartFlow(string flowId,
            IDictionary<string, object> inputs = null,
            CancellationToken cancellation = default(CancellationToken))
        {
            MRKExecutionPlan plan;
            if (string.IsNullOrWhiteSpace(flowId) ||
                !_plans.TryGetValue(flowId, out plan))
                throw new ArgumentException("未注册运行流程：" + flowId, "flowId");
            return _engine.StartFlow(plan, inputs, cancellation);
        }

        public Task<MRKFlowResult> RunAsync(string flowId,
            IDictionary<string, object> inputs = null,
            CancellationToken cancellation = default(CancellationToken))
        {
            return StartFlow(flowId, inputs, cancellation).Completion;
        }

        /// <summary>约定将单个请求对象作为名为request的Flow输入。</summary>
        public Task<MRKFlowResult> RunRequestAsync<TRequest>(string flowId,
            TRequest request, CancellationToken cancellation = default(CancellationToken))
        {
            return RunAsync(flowId,
                new Dictionary<string, object>(StringComparer.Ordinal)
                { { "request", request } }, cancellation);
        }

        public FCRWorkerSnapshot Snapshot() { return _engine.GetSnapshot(); }
        public Task StopAsync(bool cancelPending = false)
        { return _engine.StopAsync(cancelPending); }
    }
}
