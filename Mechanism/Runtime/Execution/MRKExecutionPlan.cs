using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Mechanism.Runtime.Execution
{
    /// <summary>
    /// 已解析的运行节点，不是工程持久化Graph或新的CxNode。
    /// 具体专业行为由Provider传入，不在Runtime复写设备/算法逻辑。
    /// </summary>
    public sealed class MRKExecutionStep
    {
        private readonly ReadOnlyCollection<string> _dependencies;
        public MRKExecutionStep(string id, IEnumerable<string> dependencies,
            Func<MRKExecutionContext, CancellationToken, Task<object>> executeAsync,
            string serialKey = null, MRKExecutionPolicy policy = null,
            IEnumerable<string> requiredResources = null,
            Func<MRKExecutionContext, CancellationToken, Task> compensateAsync = null,
            string executionGroup = null,
            IEnumerable<MRKExecutionInput> inputs = null, MRKExecutionOutput output = null,
            Func<MRKExecutionContext, bool> condition = null,
            bool allowSkippedDependencies = false,
            string dedicatedExecutorId = null,
            Func<MRKExecutionContext, CancellationToken, object> synchronousSdk = null,
            int priority = 0, long estimatedPendingBytes = 0,
            IDictionary<string, Type> dataOutputs = null, bool enableDataContext = false,
            string subflowAccessGroup = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Step ID required", "id");
            if (executeAsync == null) throw new ArgumentNullException("executeAsync");
            if (estimatedPendingBytes < 0)
                throw new ArgumentOutOfRangeException("estimatedPendingBytes");
            Id = id; Priority = priority; EstimatedPendingBytes = estimatedPendingBytes;
            ExecuteAsync = executeAsync;
            SerialKey = serialKey;
            Policy = policy;
            CompensateAsync = compensateAsync;
            ExecutionGroup = executionGroup;
            Output = output;
            Condition = condition;
            AllowSkippedDependencies = allowSkippedDependencies;
            if (string.IsNullOrWhiteSpace(dedicatedExecutorId) != (synchronousSdk == null))
                throw new ArgumentException("专用线程执行必须同时声明Executor ID及同步SDK回调");
            DedicatedExecutorId = dedicatedExecutorId;
            SynchronousSdk = synchronousSdk;
            Inputs = new List<MRKExecutionInput>(inputs ?? new MRKExecutionInput[0]).AsReadOnly();
            Dictionary<string, Type> declaredData = new Dictionary<string, Type>(StringComparer.Ordinal);
            if (dataOutputs != null)
                foreach (KeyValuePair<string, Type> entry in dataOutputs)
                {
                    if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null ||
                        entry.Value == typeof(void))
                        throw new ArgumentException("具名输出契约无效", "dataOutputs");
                    declaredData.Add(entry.Key, entry.Value);
                }
            DataOutputs = new ReadOnlyDictionary<string, Type>(declaredData);
            EnableDataContext = enableDataContext || declaredData.Count != 0;
            SubflowAccessGroup = subflowAccessGroup;
            List<string> resources = new List<string>();
            if (requiredResources != null)
                foreach (string resource in requiredResources)
                {
                    if (string.IsNullOrWhiteSpace(resource))
                        throw new ArgumentException("资源ID不能为空", "requiredResources");
                    if (!resources.Contains(resource)) resources.Add(resource);
                }
            resources.Sort(StringComparer.Ordinal);
            RequiredResources = resources.AsReadOnly();
            List<string> list = new List<string>(dependencies ?? new string[0]);
            _dependencies = list.AsReadOnly();
        }
        public string Id { get; private set; }
        public string SerialKey { get; private set; }
        public int Priority { get; private set; }
        public long EstimatedPendingBytes { get; private set; }
        public string ExecutionGroup { get; private set; }
        public IList<MRKExecutionInput> Inputs { get; private set; }
        public MRKExecutionOutput Output { get; private set; }
        /// <summary>同一MRK节点的具名数据输出契约；Data项目负责实际数据所有权。</summary>
        public IDictionary<string, Type> DataOutputs { get; private set; }
        public bool EnableDataContext { get; private set; }
        /// <summary>子流程内部访问边界；由InlineSubflow产生，不创建第二棵工程树。</summary>
        public string SubflowAccessGroup { get; private set; }
        /// <summary>编排节点是否运行；无副作用的Guard应在资源申请前判定。</summary>
        public Func<MRKExecutionContext, bool> Condition { get; private set; }
        public bool AllowSkippedDependencies { get; private set; }
        public string DedicatedExecutorId { get; private set; }
        /// <summary>必须同步执行且不可在内部阻塞等待专用线程自身队列。</summary>
        public Func<MRKExecutionContext, CancellationToken, object> SynchronousSdk { get; private set; }
        public MRKExecutionPolicy Policy { get; private set; }
        /// <summary>显式声明的可安全调用补偿动作；绝不会在失败后默认执行。</summary>
        public Func<MRKExecutionContext, CancellationToken, Task> CompensateAsync { get; private set; }
        public IList<string> RequiredResources { get; private set; }
        public IList<string> Dependencies { get { return _dependencies; } }
        public Func<MRKExecutionContext, CancellationToken, Task<object>> ExecuteAsync { get; private set; }
    }

    /// <summary>每次编译的不可变DAG计划；由编排定义转换而来，不能代替编排的权威工程树。</summary>
    public sealed class MRKExecutionPlan
    {
        private readonly Dictionary<string, MRKExecutionStep> _byId;
        private readonly ReadOnlyCollection<MRKExecutionStep> _steps;
        public IDictionary<string, string> DataExports { get; private set; }
        public IDictionary<string, Type> DataExportTypes { get; private set; }
        public MRKExecutionPlan(string id, IEnumerable<MRKExecutionStep> steps,
            IDictionary<string, string> dataExports = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Plan ID required", "id");
            if (steps == null) throw new ArgumentNullException("steps");
            Id = id;
            _byId = new Dictionary<string, MRKExecutionStep>(StringComparer.Ordinal);
            List<MRKExecutionStep> list = new List<MRKExecutionStep>();
            foreach (MRKExecutionStep step in steps)
            {
                if (step == null) throw new ArgumentException("Null step", "steps");
                if (_byId.ContainsKey(step.Id))
                    throw new ArgumentException("Duplicate step: " + step.Id, "steps");
                _byId.Add(step.Id, step);
                list.Add(step);
            }
            foreach (MRKExecutionStep step in list)
                foreach (string dependency in step.Dependencies)
                    if (dependency == null || !_byId.ContainsKey(dependency))
                        throw new ArgumentException("Unknown dependency on " + step.Id, "steps");
            foreach (MRKExecutionStep step in list)
                MRKExecutionPortBinder.VerifyPlanStep(step, _byId);
            Dictionary<string, int> marks = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (MRKExecutionStep step in list) Visit(step.Id, marks);
            Dictionary<string, string> exports = new Dictionary<string, string>(StringComparer.Ordinal);
            Dictionary<string, Type> exportTypes = new Dictionary<string, Type>(StringComparer.Ordinal);
            if (dataExports != null)
                foreach (KeyValuePair<string, string> item in dataExports)
                {
                    if (string.IsNullOrWhiteSpace(item.Key) ||
                        string.IsNullOrWhiteSpace(item.Value))
                        throw new ArgumentException("子流程导出映射不能为空", "dataExports");
                    int lastDot = item.Value.LastIndexOf('.');
                    if (lastDot < 1 || lastDot == item.Value.Length - 1)
                        throw new ArgumentException("子流程导出来源须为节点ID.输出名");
                    string producer = item.Value.Substring(0, lastDot);
                    string slot = item.Value.Substring(lastDot + 1);
                    MRKExecutionStep source;
                    Type valueType;
                    if (!_byId.TryGetValue(producer, out source) ||
                        !source.DataOutputs.TryGetValue(slot, out valueType))
                        throw new ArgumentException("子流程导出未声明的具名输出：" + item.Value);
                    // 如果来源属于内联子流程内部，必须通过其 __commit 节点。
                    // 否则顶层Flow.DataExports就能绕过已声明的子流程原子屏障。
                    if (source.SubflowAccessGroup != null)
                        throw new ArgumentException("流程导出不能绕过子流程提交屏障：" + item.Value);
                    exports.Add(item.Key, item.Value);
                    exportTypes.Add(item.Key, valueType);
                }
            DataExports = new ReadOnlyDictionary<string, string>(exports);
            DataExportTypes = new ReadOnlyDictionary<string, Type>(exportTypes);
            _steps = list.AsReadOnly();
        }
        private void Visit(string id, Dictionary<string, int> marks)
        {
            int state;
            if (marks.TryGetValue(id, out state))
            {
                if (state == 1) throw new ArgumentException("Cycle in execution plan: " + id);
                return;
            }
            marks[id] = 1;
            foreach (string dep in _byId[id].Dependencies) Visit(dep, marks);
            marks[id] = 2;
        }
        /// <summary>子流程在编译期展开：复用现有Worker，不把每次子流程变成独立的线程池。</summary>
        public static IList<MRKExecutionStep> InlineSubflow(
            string prefix, MRKExecutionPlan child, IEnumerable<string> entryDependencies,
            IEnumerable<MRKExecutionInput> mappedInputs = null)
        {
            if (string.IsNullOrWhiteSpace(prefix)) throw new ArgumentException("前缀不能为空", "prefix");
            if (child == null) throw new ArgumentNullException("child");
            string scope = prefix + ".";
            List<string> entry = new List<string>(entryDependencies ?? new string[0]);
            Dictionary<string, MRKExecutionInput> callerInputs = null;
            if (mappedInputs != null)
            {
                callerInputs = new Dictionary<string, MRKExecutionInput>(StringComparer.Ordinal);
                foreach (MRKExecutionInput input in mappedInputs)
                    callerInputs.Add(input.Name, input);
            }
            List<MRKExecutionStep> list = new List<MRKExecutionStep>();
            foreach (MRKExecutionStep current in child.Steps)
            {
                List<string> depends = new List<string>();
                foreach (string id in current.Dependencies) depends.Add(scope + id);
                if (current.Dependencies.Count == 0) depends.AddRange(entry);
                MRKExecutionStep local = current;
                Func<MRKExecutionContext, CancellationToken, Task> compensation = null;
                if (local.CompensateAsync != null)
                    compensation = (context, token) =>
                        local.CompensateAsync(context.ForSubflow(scope), token);
                List<MRKExecutionInput> bindings = new List<MRKExecutionInput>();
                foreach (MRKExecutionInput input in local.Inputs)
                {
                    if (input.Source == MRKInputSource.FlowInput && callerInputs != null)
                    {
                        MRKExecutionInput caller;
                        if (!callerInputs.TryGetValue(input.SourceName, out caller))
                            throw new ArgumentException("子流程未映射必需输入：" +
                                child.Id + "." + input.SourceName);
                        if (!input.ValueType.IsAssignableFrom(caller.ValueType))
                            throw new ArgumentException("子流程入口类型不匹配：" +
                                child.Id + "." + input.SourceName);
                        bindings.Add(new MRKExecutionInput(input.Name,
                            MRKInputSource.StepOutput, prefix + "$entry." +
                            caller.Name, input.ValueType, input.Required));
                        if (!depends.Contains(prefix + "$entry")) depends.Add(prefix + "$entry");
                    }
                    else
                        bindings.Add(input.Source == MRKInputSource.StepResult ||
                            input.Source == MRKInputSource.StepOutput ?
                            new MRKExecutionInput(input.Name, input.Source,
                                scope + input.SourceName, input.ValueType,
                                input.Required) : input);
                }
                // 新MDS具名Output即使尚未声明DataExports，也属于子流程内部数据。
                // 纯旧式MRK StepResult保持已有兼容入口。
                // 已展开的内层入口Gate属于其被调用子流程；不能把它视为外层
                // 普通输出，否则第二层/第三层输入会被端口权限校验错误拒绝。
                string accessGroup = local.SubflowAccessGroup == null
                    ? (local.Id.EndsWith("$entry", StringComparison.Ordinal)
                        ? scope + local.Id.Substring(0, local.Id.Length - "$entry".Length)
                        : (child.DataExports.Count == 0 && local.DataOutputs.Count == 0
                            ? null : prefix))
                    : scope + local.SubflowAccessGroup;
                list.Add(new MRKExecutionStep(scope + local.Id, depends,
                    (context, token) => local.ExecuteAsync(context.ForSubflow(scope), token),
                    local.SerialKey, local.Policy, local.RequiredResources, compensation,
                    local.ExecutionGroup, bindings, local.Output,
                    local.Condition == null ? (Func<MRKExecutionContext, bool>)null
                        : context => local.Condition(context.ForSubflow(scope)),
                    local.AllowSkippedDependencies, local.DedicatedExecutorId,
                    local.SynchronousSdk == null ?
                        (Func<MRKExecutionContext, CancellationToken, object>)null :
                        (context, token) => local.SynchronousSdk(context.ForSubflow(scope), token),
                    local.Priority, local.EstimatedPendingBytes, local.DataOutputs,
                    local.EnableDataContext, accessGroup));
            }
            if (child.DataExports.Count != 0)
            {
                if (child._byId.ContainsKey("__commit"))
                    throw new ArgumentException("子流程导出使用保留节点名__commit");
                List<string> complete = new List<string>();
                foreach (MRKExecutionStep step in child.Steps)
                    complete.Add(scope + step.Id);
                List<MRKExecutionInput> exportInputs = new List<MRKExecutionInput>();
                foreach (KeyValuePair<string, string> item in child.DataExports)
                    exportInputs.Add(new MRKExecutionInput(item.Key, MRKInputSource.StepOutput,
                        scope + item.Value, child.DataExportTypes[item.Key]));
                list.Add(new MRKExecutionStep(scope + "__commit", complete,
                    (context, token) =>
                    {
                        token.ThrowIfCancellationRequested();
                        foreach (KeyValuePair<string, object> item in context.BoundInputs)
                            context.Data.Return(item.Key, item.Value);
                        return Task.FromResult((object)true);
                    }, inputs: exportInputs, dataOutputs: child.DataExportTypes,
                    enableDataContext: true));
            }
            return list.AsReadOnly();
        }

        public string Id { get; private set; }
        public IList<MRKExecutionStep> Steps { get { return _steps; } }
        public MRKExecutionStep GetStep(string id) { return _byId[id]; }
    }
}
