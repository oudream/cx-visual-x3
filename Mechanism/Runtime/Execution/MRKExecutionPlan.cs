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
            bool allowSkippedDependencies = false)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Step ID required", "id");
            if (executeAsync == null) throw new ArgumentNullException("executeAsync");
            Id = id;
            ExecuteAsync = executeAsync;
            SerialKey = serialKey;
            Policy = policy;
            CompensateAsync = compensateAsync;
            ExecutionGroup = executionGroup;
            Output = output;
            Condition = condition;
            AllowSkippedDependencies = allowSkippedDependencies;
            Inputs = new List<MRKExecutionInput>(inputs ?? new MRKExecutionInput[0]).AsReadOnly();
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
        public string ExecutionGroup { get; private set; }
        public IList<MRKExecutionInput> Inputs { get; private set; }
        public MRKExecutionOutput Output { get; private set; }
        /// <summary>编排节点是否运行；无副作用的Guard应在资源申请前判定。</summary>
        public Func<MRKExecutionContext, bool> Condition { get; private set; }
        public bool AllowSkippedDependencies { get; private set; }
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
        public MRKExecutionPlan(string id, IEnumerable<MRKExecutionStep> steps)
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
            string prefix, MRKExecutionPlan child, IEnumerable<string> entryDependencies)
        {
            if (string.IsNullOrWhiteSpace(prefix)) throw new ArgumentException("前缀不能为空", "prefix");
            if (child == null) throw new ArgumentNullException("child");
            string scope = prefix + ".";
            List<string> entry = new List<string>(entryDependencies ?? new string[0]);
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
                    bindings.Add(input.Source == MRKInputSource.StepResult ?
                        new MRKExecutionInput(input.Name, input.Source,
                            scope + input.SourceName, input.ValueType, input.Required) : input);
                list.Add(new MRKExecutionStep(scope + local.Id, depends,
                    (context, token) => local.ExecuteAsync(context.ForSubflow(scope), token),
                    local.SerialKey, local.Policy, local.RequiredResources, compensation,
                    local.ExecutionGroup, bindings, local.Output,
                    local.Condition == null ? (Func<MRKExecutionContext, bool>)null
                        : context => local.Condition(context.ForSubflow(scope)),
                    local.AllowSkippedDependencies));
            }
            return list.AsReadOnly();
        }

        public string Id { get; private set; }
        public IList<MRKExecutionStep> Steps { get { return _steps; } }
        public MRKExecutionStep GetStep(string id) { return _byId[id]; }
    }
}
