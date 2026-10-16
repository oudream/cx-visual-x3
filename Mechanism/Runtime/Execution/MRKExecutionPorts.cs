using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CxVisual.Mechanism.Runtime.Execution
{
    /// <summary>只描述执行计划内节点端口契约；不是M04的第二套全局Schema注册表。</summary>
    public enum MRKInputSource { FlowInput, StepResult, StepOutput }

    public sealed class MRKExecutionInput
    {
        public MRKExecutionInput(string name, MRKInputSource source, string sourceName,
            Type valueType, bool required = true)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("端口名为空", "name");
            if (string.IsNullOrWhiteSpace(sourceName)) throw new ArgumentException("来源名为空", "sourceName");
            if (valueType == null) throw new ArgumentNullException("valueType");
            if (valueType == typeof(void)) throw new ArgumentException("输入类型无效", "valueType");
            Name = name; Source = source; SourceName = sourceName;
            ValueType = valueType; Required = required;
        }
        public string Name { get; private set; }
        public MRKInputSource Source { get; private set; }
        public string SourceName { get; private set; }
        public Type ValueType { get; private set; }
        public bool Required { get; private set; }
    }

    public sealed class MRKExecutionOutput
    {
        public MRKExecutionOutput(Type valueType, bool allowNull = false)
        {
            if (valueType == null || valueType == typeof(void))
                throw new ArgumentException("输出类型无效", "valueType");
            ValueType = valueType; AllowNull = allowNull;
        }
        public Type ValueType { get; private set; }
        public bool AllowNull { get; private set; }
    }

    /// <summary>运行期类型安全错误包含节点和端口身份，便于调试界面准确定位。</summary>
    public sealed class MRKExecutionPortException : InvalidOperationException
    {
        public MRKExecutionPortException(string stepId, string portName, string reason)
            : base("节点 " + stepId + " 端口 " + portName + "：" + reason)
        { StepId = stepId; PortName = portName; }
        public string StepId { get; private set; }
        public string PortName { get; private set; }
    }

    internal static class MRKExecutionPortBinder
    {
        internal static void VerifyPlanStep(MRKExecutionStep step,
            IDictionary<string, MRKExecutionStep> catalog)
        {
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            foreach (MRKExecutionInput input in step.Inputs)
            {
                if (!names.Add(input.Name))
                    throw new ArgumentException("节点 " + step.Id + " 重复输入端口：" + input.Name);
                if (input.Source == MRKInputSource.FlowInput) continue;
                string producer = input.SourceName;
                string outputName = null;
                if (input.Source == MRKInputSource.StepOutput)
                {
                    int separator = producer.LastIndexOf('.');
                    if (separator < 1 || separator == producer.Length - 1)
                        throw new ArgumentException("具名数据输出来源必须为节点ID.输出名");
                    outputName = producer.Substring(separator + 1);
                    producer = producer.Substring(0, separator);
                }
                else if (input.Source != MRKInputSource.StepResult)
                    throw new ArgumentException("未知输入来源类型");
                if (!catalog.ContainsKey(producer))
                    throw new ArgumentException("输入端口依赖未知节点：" + producer);
                if (!step.Dependencies.Contains(producer))
                    throw new ArgumentException("输入端口来源必须声明为直接依赖：" + producer);
                if (outputName == null)
                {
                    MRKExecutionOutput result = catalog[producer].Output;
                    if (result != null && !input.ValueType.IsAssignableFrom(result.ValueType))
                        throw new ArgumentException("端口类型不匹配：" + step.Id + "." + input.Name);
                }
                else
                {
                    Type published;
                    if (!catalog[producer].DataOutputs.TryGetValue(outputName, out published))
                        throw new ArgumentException("数据输出未声明：" + producer + "." + outputName);
                    if (!input.ValueType.IsAssignableFrom(published))
                        throw new ArgumentException("具名数据输出类型不匹配：" + step.Id + "." + input.Name);
                }
            }
        }

        internal static void VerifyFlowInputs(MRKExecutionPlan plan,
            IDictionary<string, object> inputs)
        {
            foreach (MRKExecutionStep step in plan.Steps)
                foreach (MRKExecutionInput input in step.Inputs)
                {
                    if (input.Source != MRKInputSource.FlowInput) continue;
                    object value;
                    if (inputs == null || !inputs.TryGetValue(input.SourceName, out value))
                    {
                        if (input.Required)
                            throw new MRKExecutionPortException(step.Id, input.Name, "必要流程输入缺失");
                        continue;
                    }
                    VerifyValue(step.Id, input.Name, value, input.ValueType, input.Required);
                }
        }

        internal static IDictionary<string, object> Bind(MRKExecutionStep step,
            MRKExecutionContext context)
        {
            Dictionary<string, object> values = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (MRKExecutionInput input in step.Inputs)
            {
                object value;
                bool found;
                if (input.Source == MRKInputSource.FlowInput)
                    found = context.Inputs.TryGetValue(input.SourceName, out value);
                else if (input.Source == MRKInputSource.StepOutput)
                    found = context.TryGetCommittedData(input.SourceName, out value);
                else
                    found = context.TryGetAbsoluteResult(input.SourceName, out value);
                if (!found)
                {
                    if (input.Required)
                        throw new MRKExecutionPortException(step.Id, input.Name, "来源未就绪");
                    continue;
                }
                VerifyValue(step.Id, input.Name, value, input.ValueType, input.Required);
                // 对遗留StepResult/FlowInput的可写数组也做防御复制，避免扇出修改原值。
                values.Add(input.Name, value is byte[] ? ((byte[])value).Clone() : value);
            }
            return new ReadOnlyDictionary<string, object>(values);
        }

        internal static void VerifyOutput(MRKExecutionStep step, object value)
        {
            if (step.Output == null) return;
            VerifyValue(step.Id, "输出", value, step.Output.ValueType,
                !step.Output.AllowNull);
        }

        private static void VerifyValue(string step, string port,
            object value, Type expected, bool required)
        {
            if (value == null)
            {
                if (required) throw new MRKExecutionPortException(step, port, "不允许null");
                return;
            }
            if (!expected.IsInstanceOfType(value))
                throw new MRKExecutionPortException(step, port,
                    "期望 " + expected.FullName + "，实际 " + value.GetType().FullName);
        }
    }
}
