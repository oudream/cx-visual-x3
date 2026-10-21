using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Mechanism.Runtime.Execution
{
    /// <summary>专业能力的输入端口声明，不包含来源绑定；实际连接由MRKExecutionInput负责。</summary>
    public sealed class MRKCapabilityInput
    {
        public MRKCapabilityInput(string name, Type valueType, bool required = true)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("输入端口名不能为空", "name");
            if (valueType == null || valueType == typeof(void))
                throw new ArgumentException("输入端口类型无效", "valueType");
            Name = name; ValueType = valueType; Required = required;
        }
        public string Name { get; private set; }
        public Type ValueType { get; private set; }
        public bool Required { get; private set; }
    }

    /// <summary>能力契约与实际委托的已解析适配，不是第二套专业实现或全局Schema。</summary>
    public sealed class MRKResolvedCapability
    {
        public MRKResolvedCapability(Func<MRKExecutionContext, CancellationToken, Task<object>> executeAsync,
            MRKExecutionOutput output = null,
            Func<MRKExecutionContext, CancellationToken, Task> compensateAsync = null,
            Func<MRKExecutionContext, CancellationToken, object> synchronousSdk = null,
            IDictionary<string, Type> dataOutputs = null,
            IEnumerable<MRKCapabilityInput> inputs = null)
        {
            if (executeAsync == null && synchronousSdk == null)
                throw new ArgumentException("能力必须提供异步或同步SDK实现");
            ExecuteAsync = executeAsync ?? ((context, token) =>
                Task.FromResult(synchronousSdk(context, token)));
            SynchronousSdk = synchronousSdk;
            Output = output;
            Dictionary<string, Type> declared = new Dictionary<string, Type>(StringComparer.Ordinal);
            if (dataOutputs != null)
                foreach (KeyValuePair<string, Type> entry in dataOutputs)
                {
                    if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null ||
                        entry.Value == typeof(void))
                        throw new ArgumentException("专业具名数据输出契约无效", "dataOutputs");
                    declared.Add(entry.Key, entry.Value);
                }
            DataOutputs = new ReadOnlyDictionary<string, Type>(declared);
            HasInputContract = inputs != null;
            List<MRKCapabilityInput> defined = new List<MRKCapabilityInput>();
            HashSet<string> inputNames = new HashSet<string>(StringComparer.Ordinal);
            if (inputs != null)
                foreach (MRKCapabilityInput input in inputs)
                {
                    if (input == null || !inputNames.Add(input.Name))
                        throw new ArgumentException("能力输入端口为空或重复", "inputs");
                    defined.Add(input);
                }
            Inputs = new ReadOnlyCollection<MRKCapabilityInput>(defined);
            CompensateAsync = compensateAsync;
        }
        public Func<MRKExecutionContext, CancellationToken, Task<object>> ExecuteAsync { get; private set; }
        public MRKExecutionOutput Output { get; private set; }
        /// <summary>能力的具名Data.Return契约，与直接构造MRKExecutionStep完全相同。</summary>
        public IDictionary<string, Type> DataOutputs { get; private set; }
        public IList<MRKCapabilityInput> Inputs { get; private set; }
        public bool HasInputContract { get; private set; }
        /// <summary>只验证专业能力的端口声明；绑定来源与依赖由原MRK端口校验。</summary>
        internal void ValidateInputs(IEnumerable<MRKExecutionInput> bindings)
        {
            if (!HasInputContract) return; // 遗留能力继续采用原编译调用契约
            Dictionary<string, MRKCapabilityInput> expected =
                Inputs.ToDictionary(x => x.Name, x => x, StringComparer.Ordinal);
            HashSet<string> actual = new HashSet<string>(StringComparer.Ordinal);
            foreach (MRKExecutionInput binding in bindings)
            {
                MRKCapabilityInput declared;
                if (binding == null || !actual.Add(binding.Name) ||
                    !expected.TryGetValue(binding.Name, out declared))
                    throw new ArgumentException("能力输入端口不存在或重复：" +
                        (binding == null ? "<null>" : binding.Name));
                if (!declared.ValueType.IsAssignableFrom(binding.ValueType))
                    throw new ArgumentException("能力输入端口类型不匹配：" + binding.Name);
                if (declared.Required && !binding.Required)
                    throw new ArgumentException("必需输入不能以可选绑定绕过：" + binding.Name);
            }
            foreach (MRKCapabilityInput port in Inputs)
                if (port.Required && !actual.Contains(port.Name))
                    throw new ArgumentException("能力必需输入未绑定：" + port.Name);
        }
        public Func<MRKExecutionContext, CancellationToken, object> SynchronousSdk { get; private set; }
        public Func<MRKExecutionContext, CancellationToken, Task> CompensateAsync { get; private set; }
    }

    /// <summary>由M18～M25的编排定义适配生成；真实能力实现经此解析，避免复制算法。</summary>
    public interface IMRKCapabilityResolver
    {
        bool TryResolve(string capabilityId, out MRKResolvedCapability capability);
    }

    /// <summary>
    /// 可选版本自证：专业Owner报告自己真正提供的能力版本。
    /// 发布时由MOC与Host声明比对；未实现时仍沿用原Host声明兼容契约。
    /// </summary>
    public interface IMRKVersionedCapabilityResolver : IMRKCapabilityResolver
    {
        bool TryGetCapabilityVersion(string capabilityId,out string version);
    }

    /// <summary>一次编译使用的瞬时节点描述，不负责工程保存或CxNode所有权。</summary>
    public sealed class MRKStepCompileRequest
    {
        public MRKStepCompileRequest(string id, string capabilityId, IEnumerable<string> dependencies = null,
            IEnumerable<MRKExecutionInput> inputs = null, string serialKey = null,
            string executionGroup = null, MRKExecutionPolicy policy = null,
            IEnumerable<string> requiredResources = null,
            Func<MRKExecutionContext, bool> condition = null,
            bool allowSkippedDependencies = false, string dedicatedExecutorId = null,
            int priority = 0, long estimatedPendingBytes = 0, string subflowId = null,
            MRKLoopRegion loopRegion = null)
        {
            Id = id; CapabilityId = capabilityId; SubflowId = subflowId;
            LoopRegion = loopRegion; SerialKey = serialKey;
            ExecutionGroup = executionGroup; Policy = policy; Condition = condition;
            AllowSkippedDependencies = allowSkippedDependencies;
            DedicatedExecutorId = dedicatedExecutorId;
            if (estimatedPendingBytes < 0)
                throw new ArgumentOutOfRangeException("estimatedPendingBytes");
            Priority = priority; EstimatedPendingBytes = estimatedPendingBytes;
            Dependencies = new ReadOnlyCollection<string>(new List<string>(dependencies ?? new string[0]));
            Inputs = new ReadOnlyCollection<MRKExecutionInput>(
                new List<MRKExecutionInput>(inputs ?? new MRKExecutionInput[0]));
            RequiredResources = new ReadOnlyCollection<string>(
                new List<string>(requiredResources ?? new string[0]));
        }
        public string Id { get; private set; }
        public string CapabilityId { get; private set; }
        public string SubflowId { get; private set; }
        public MRKLoopRegion LoopRegion {get;private set;}
        public IList<string> Dependencies { get; private set; }
        public IList<MRKExecutionInput> Inputs { get; private set; }
        public string SerialKey { get; private set; }
        public string ExecutionGroup { get; private set; }
        public MRKExecutionPolicy Policy { get; private set; }
        public IList<string> RequiredResources { get; private set; }
        public Func<MRKExecutionContext, bool> Condition { get; private set; }
        public bool AllowSkippedDependencies { get; private set; }
        public string DedicatedExecutorId { get; private set; }
        public int Priority { get; private set; }
        public long EstimatedPendingBytes { get; private set; }
    }

    public sealed class MRKCompileDiagnostic
    {
        internal MRKCompileDiagnostic(string stepId, string code, string message)
        { StepId = stepId; Code = code; Message = message; }
        public string StepId { get; private set; }
        public string Code { get; private set; }
        public string Message { get; private set; }
    }

    public sealed class MRKCompileResult
    {
        internal MRKCompileResult(MRKExecutionPlan plan, IList<MRKCompileDiagnostic> diagnostics)
        {
            Plan = plan;
            Diagnostics = new ReadOnlyCollection<MRKCompileDiagnostic>(
                new List<MRKCompileDiagnostic>(diagnostics));
        }
        public MRKExecutionPlan Plan { get; private set; }
        public IList<MRKCompileDiagnostic> Diagnostics { get; private set; }
        public bool Succeeded { get { return Plan != null && Diagnostics.Count == 0; } }
    }

    /// <summary>
    /// 从外部编排节点声明编译成统一Runtime计划，聚合静态错误并确定性排序。
    /// 不创建新调度器，也不持久保存另一棵工程树。
    /// </summary>
    public sealed class MRKExecutionPlanCompiler
    {
        public MRKCompileResult Compile(string planId, IEnumerable<MRKStepCompileRequest> source,
            IMRKCapabilityResolver resolver,
            IDictionary<string, string> dataExports = null,
            IEnumerable<MRKControlRegion> regions = null)
        {
            if (resolver == null) throw new ArgumentNullException("resolver");
            if (string.IsNullOrWhiteSpace(planId)) throw new ArgumentException("Plan ID不能为空", "planId");
            if (source == null) throw new ArgumentNullException("source");
            List<MRKCompileDiagnostic> errors = new List<MRKCompileDiagnostic>();
            List<MRKStepCompileRequest> definitions = source.ToList();
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (MRKStepCompileRequest item in definitions)
            {
                if (item == null) { errors.Add(new MRKCompileDiagnostic("", "NULL_STEP", "节点不能为空")); continue; }
                if (string.IsNullOrWhiteSpace(item.Id))
                    errors.Add(new MRKCompileDiagnostic("", "EMPTY_ID", "节点ID不能为空"));
                else if (!ids.Add(item.Id))
                    errors.Add(new MRKCompileDiagnostic(item.Id, "DUPLICATE_STEP", "节点ID重复"));
            }
            List<MRKExecutionStep> steps = new List<MRKExecutionStep>();
            foreach (MRKStepCompileRequest item in definitions.Where(x => x != null))
            {
                if (string.IsNullOrWhiteSpace(item.Id)) continue;
                if (!string.IsNullOrWhiteSpace(item.SubflowId))
                {
                    errors.Add(new MRKCompileDiagnostic(item.Id, "SUBFLOW_NEEDS_ORCHESTRATION",
                        "子流程定义需要通过统一编排编译入口处理"));
                    continue;
                }
                if (string.IsNullOrWhiteSpace(item.CapabilityId))
                {
                    errors.Add(new MRKCompileDiagnostic(item.Id, "EMPTY_CAPABILITY", "能力标识不能为空"));
                    continue;
                }
                foreach (string dependency in item.Dependencies)
                    if (string.IsNullOrEmpty(dependency) || !ids.Contains(dependency))
                        errors.Add(new MRKCompileDiagnostic(item.Id, "UNKNOWN_DEPENDENCY",
                            "缺失依赖：" + dependency));
                MRKResolvedCapability capability;
                if (!resolver.TryResolve(item.CapabilityId, out capability) || capability == null)
                {
                    errors.Add(new MRKCompileDiagnostic(item.Id, "UNKNOWN_CAPABILITY",
                        "无法解析能力：" + item.CapabilityId));
                    continue;
                }
                try
                {
                    capability.ValidateInputs(item.Inputs);
                    steps.Add(new MRKExecutionStep(item.Id, item.Dependencies,
                        capability.ExecuteAsync, item.SerialKey, item.Policy,
                        item.RequiredResources, capability.CompensateAsync,
                        item.ExecutionGroup, item.Inputs, capability.Output,
                        item.Condition, item.AllowSkippedDependencies, item.DedicatedExecutorId,
                        capability.SynchronousSdk,
                        item.Priority, item.EstimatedPendingBytes,
                        capability.DataOutputs, loopRegion:item.LoopRegion));
                }
                catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
                {
                    errors.Add(new MRKCompileDiagnostic(item.Id, "INVALID_STEP", ex.Message));
                }
            }
            if (errors.Count > 0) return new MRKCompileResult(null, errors);
            try
            {
                // 稳定排序，输入枚举顺序不能改变Plan中可观察的节点排列。
                MRKExecutionPlan compiled = new MRKExecutionPlan(planId,
                    steps.OrderBy(x => x.Id, StringComparer.Ordinal), dataExports, regions);
                return new MRKCompileResult(compiled, errors);
            }
            catch (ArgumentException ex)
            {
                errors.Add(new MRKCompileDiagnostic("", "INVALID_GRAPH", ex.Message));
                return new MRKCompileResult(null, errors);
            }
        }
    }
}
