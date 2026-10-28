using System;
using System.Collections.Generic;
using CxVisual.Mechanism.Data.RuntimeData;

namespace CxVisual.Mechanism.Runtime.Execution
{
    /// <summary>
    /// 仅作用于显式启用的静态DAG。计划声明的具名StepOutput消费端全部终态后，
    /// 才释放生产者已经关闭的MDS调用帧。数据仍归同一个DataStack所有，不保存副本。
    /// 动态Loop/Try/ControlRegion由MRKExecutionPlan拒绝此模式。
    /// </summary>
    internal sealed class MRKCommittedOutputReclaimer
    {
        private readonly object _gate = new object();
        private readonly MDSDataStack _stack;
        private readonly Dictionary<string, int> _remaining =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, string[]> _sourcesByConsumer =
            new Dictionary<string, string[]>(StringComparer.Ordinal);
        private readonly HashSet<string> _settled = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _exports = new HashSet<string>(StringComparer.Ordinal);

        internal MRKCommittedOutputReclaimer(MRKExecutionPlan plan, MDSDataStack stack)
        {
            if (plan == null) throw new ArgumentNullException("plan");
            _stack = stack ?? throw new ArgumentNullException("stack");
            if (!plan.ReclaimCommittedWhenConsumersSettle)
                throw new ArgumentException("计划未开启自动数据回收", "plan");
            foreach (MRKExecutionStep step in plan.Steps)
                _remaining.Add(step.Id, 0);
            foreach (string export in plan.DataExports.Values)
            {
                int dot = export.LastIndexOf('.');
                _exports.Add(export.Substring(0, dot));
            }
            foreach (MRKExecutionStep step in plan.Steps)
            {
                var sources = new HashSet<string>(StringComparer.Ordinal);
                foreach (MRKExecutionInput input in step.Inputs)
                {
                    if (input.Source != MRKInputSource.StepOutput) continue;
                    int dot = input.SourceName.LastIndexOf('.');
                    if (dot < 1) throw new InvalidOperationException(
                        "具名输出缺少生产者：" + input.SourceName);
                    string producer = input.SourceName.Substring(0, dot);
                    if (!_remaining.ContainsKey(producer))
                        throw new InvalidOperationException("未知具名输出生产者：" + producer);
                    sources.Add(producer);
                }
                string[] dependencies = new string[sources.Count];
                sources.CopyTo(dependencies);
                _sourcesByConsumer.Add(step.Id, dependencies);
                foreach (string producer in dependencies)
                    _remaining[producer] = checked(_remaining[producer] + 1);
            }
        }

        /// <summary>在MDSDataFrame.Dispose完成之后结算，包含Skipped/Failed/Cancelled。</summary>
        internal void StepSettled(string stepId)
        {
            lock (_gate)
            {
                if (!_settled.Add(stepId))
                    throw new InvalidOperationException("重复结算节点数据消费者：" + stepId);
                string[] dependencies = _sourcesByConsumer[stepId];
                foreach (string producer in dependencies)
                {
                    int left = --_remaining[producer];
                    if (left < 0) throw new InvalidOperationException("负数数据消费者：" + producer);
                    if (left == 0 && _settled.Contains(producer))
                        TryRelease(producer);
                }
                if (_remaining[stepId] == 0) TryRelease(stepId);
            }
        }

        private void TryRelease(string producer)
        {
            if (_exports.Contains(producer)) return; // FlowResult直到完整导出成功再收尾
            _stack.ReleaseCommittedOutputs(producer);
        }
    }
}