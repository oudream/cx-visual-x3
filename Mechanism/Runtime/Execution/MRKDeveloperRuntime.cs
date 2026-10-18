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
        private readonly Dictionary<string, string> _exports =
            new Dictionary<string, string>(StringComparer.Ordinal);
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
        /// <summary>节点具名数据端口的轻便声明，支持StepOutput与FlowInput绑定。</summary>
        public MRKCodeFlowBuilder StepData(string id, string capabilityId,
            IEnumerable<MRKExecutionInput> inputs, params string[] after)
        { return Add(new MRKStepCompileRequest(id, capabilityId, after, inputs)); }

        /// <summary>子流程调用Input通过入口Gate映射，不能隐式继承父级Local。</summary>
        public MRKCodeFlowBuilder Subflow(string id, string flowId,
            IEnumerable<MRKExecutionInput> inputs, params string[] after)
        { return Add(new MRKStepCompileRequest(id, null, after, inputs, subflowId:flowId)); }

        /// <summary>最终Flow公开的具名数据结果；来源必须是节点Id.输出名。</summary>
        public MRKCodeFlowBuilder Export(string name, string stepOutput)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(stepOutput))
                throw new ArgumentException("流程导出名称和来源必须非空");
            _exports.Add(name, stepOutput);
            return this;
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
        { return new MRKFlowCompileDefinition(_id, _steps, _exports); }
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

        /// <summary>业务能力直接声明Data.Return的具名输出；不需要另外构造执行器。</summary>
        public MRKCodeRuntimeBuilder DataCapability(string id,
            Func<MRKExecutionContext, CancellationToken, Task<object>> action,
            IDictionary<string, Type> dataOutputs,
            IEnumerable<MRKCapabilityInput> inputs = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("能力ID不能为空", "id");
            if (action == null) throw new ArgumentNullException("action");
            if (dataOutputs == null || dataOutputs.Count == 0)
                throw new ArgumentException("至少声明一个具名输出", "dataOutputs");
            if (_capabilities.ContainsKey(id))
                throw new ArgumentException("重复能力：" + id, "id");
            _capabilities.Add(id, new MRKResolvedCapability(action, dataOutputs: dataOutputs));
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

        /// <summary>
        /// 单请求业务能力：开发者只需接收业务请求和取消Token，
        /// 底层自动使用Flow输入request绑定，不需要手工访问Context字典。
        /// </summary>
        public MRKCodeRuntimeBuilder CapabilityForRequest<TRequest, TResult>(string id,
            Func<TRequest, CancellationToken, Task<TResult>> action)
        {
            if (action == null) throw new ArgumentNullException("action");
            return Capability<TResult>(id, (context, token) =>
            {
                object input;
                if (!context.Inputs.TryGetValue("request", out input) ||
                    !(input is TRequest))
                    throw new ArgumentException("流程缺少类型匹配的request输入：" +
                        typeof(TRequest).FullName);
                return action((TRequest)input, token);
            });
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

        /// <summary>冻结一次定义，供开发执行和正式授权发布共用。</summary>
        public MRKCodeRuntimeBlueprint Freeze()
        {
            return new MRKCodeRuntimeBlueprint(_flows, _capabilities);
        }

        public MRKDevelopmentRuntime Build(FCRWorkerOptions options = null)
        {
            return Freeze().BuildForDevelopment(options);
        }
    }

    /// <summary>
    /// 已验证的不可变代码编排快照；同时实现原Catalog与CapabilityResolver契约。
    /// Dev使用同一份已编译定义；生产仍必须经Publisher授权和安全点。
    /// </summary>
    public sealed class MRKCodeRuntimeBlueprint :
        IMRKFlowCompileCatalog, IMRKCapabilityResolver
    {
        private readonly IDictionary<string, MRKFlowCompileDefinition> _definitions;
        private readonly IDictionary<string, MRKResolvedCapability> _capabilities;
        private readonly IDictionary<string, MRKExecutionPlan> _plans;

        internal MRKCodeRuntimeBlueprint(
            IDictionary<string, MRKFlowCompileDefinition> definitions,
            IDictionary<string, MRKResolvedCapability> capabilities)
        {
            if (definitions.Count == 0)
                throw new InvalidOperationException("至少注册一个流程");
            _definitions = new ReadOnlyDictionary<string, MRKFlowCompileDefinition>(
                new Dictionary<string, MRKFlowCompileDefinition>(definitions,
                    StringComparer.Ordinal));
            _capabilities = new ReadOnlyDictionary<string, MRKResolvedCapability>(
                new Dictionary<string, MRKResolvedCapability>(capabilities,
                    StringComparer.Ordinal));
            MRKExecutionOrchestrationCompiler compiler =
                new MRKExecutionOrchestrationCompiler();
            Dictionary<string, MRKExecutionPlan> plans =
                new Dictionary<string, MRKExecutionPlan>(StringComparer.Ordinal);
            // 验证全部流程，未验证定义永远不能发布或创建Worker。
            foreach (string id in _definitions.Keys.OrderBy(x => x, StringComparer.Ordinal))
            {
                MRKCompileResult result = compiler.Compile(id, this, this);
                if (!result.Succeeded)
                    throw new MRKCodeRuntimeBuildException(id, result.Diagnostics);
                plans.Add(id, result.Plan);
            }
            _plans = new ReadOnlyDictionary<string, MRKExecutionPlan>(plans);
        }

        public IList<string> FlowIds
        { get { return new ReadOnlyCollection<string>(_plans.Keys.OrderBy(x => x,
            StringComparer.Ordinal).ToList()); } }

        public bool TryGet(string flowId, out MRKFlowCompileDefinition definition)
        { return _definitions.TryGetValue(flowId, out definition); }

        public bool TryResolve(string capabilityId, out MRKResolvedCapability capability)
        { return _capabilities.TryGetValue(capabilityId, out capability); }

        public MRKDevelopmentRuntime BuildForDevelopment(FCRWorkerOptions options = null)
        {
            return new MRKDevelopmentRuntime(new MRKExecutionEngine(
                options ?? new FCRWorkerOptions(2, 64)), _plans);
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

        /// <summary>自然的业务请求入口：RunAsync("CT.Scan", request)。</summary>
        public Task<MRKFlowResult> RunAsync(string flowId, object request,
            CancellationToken cancellation = default(CancellationToken))
        {
            return RunRequestAsync(flowId, request, cancellation);
        }

        /// <summary>约定将单个请求对象作为名为request的Flow输入。</summary>
        public Task<MRKFlowResult> RunRequestAsync<TRequest>(string flowId,
            TRequest request, CancellationToken cancellation = default(CancellationToken))
        {
            return RunAsync(flowId,
                (IDictionary<string, object>)new Dictionary<string, object>(StringComparer.Ordinal)
                { { "request", request } }, cancellation);
        }

        public FCRWorkerSnapshot Snapshot() { return _engine.GetSnapshot(); }
        public Task StopAsync(bool cancelPending = false)
        { return _engine.StopAsync(cancelPending); }
    }
}
