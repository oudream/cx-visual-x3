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
            string serialKey = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Step ID required", "id");
            if (executeAsync == null) throw new ArgumentNullException("executeAsync");
            Id = id;
            ExecuteAsync = executeAsync;
            SerialKey = serialKey;
            List<string> list = new List<string>(dependencies ?? new string[0]);
            _dependencies = list.AsReadOnly();
        }
        public string Id { get; private set; }
        public string SerialKey { get; private set; }
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
        public string Id { get; private set; }
        public IList<MRKExecutionStep> Steps { get { return _steps; } }
        public MRKExecutionStep GetStep(string id) { return _byId[id]; }
    }
}
