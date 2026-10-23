using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Mechanism.Connectivity;
using CxVisual.Mechanism.Data.Variables;
using CxVisual.Mechanism.Data.RuntimeData;

namespace CxVisual.Mechanism.Data
{
    // MDS Owner 的正式数据入口，不创建第二个 Heap、Stack 或 VariableManager。
    // Message/Command 的真实 Owner 尚未装配时显式拒绝，而不是默认成功。
    public sealed class MDSProtocolDataTargetAccess : IMPADataTargetAccess
    {
        private readonly MDSVariableRegistry _variables;
        private readonly MDSDataHeap _heap;
        private readonly IMDSDataContext _frame;
        private readonly Func<string, object, CancellationToken, Task> _publish;
        private readonly Func<string, object, CancellationToken, Task> _invoke;
        private readonly Func<string, object, bool> _authorizeCommand;

        public MDSProtocolDataTargetAccess(MDSVariableRegistry variables = null,
            MDSDataHeap heap = null, IMDSDataContext frame = null,
            Func<string, object, CancellationToken, Task> publish = null,
            Func<string, object, CancellationToken, Task> invoke = null,
            Func<string, object, bool> authorizeCommand = null)
        {
            _variables = variables; _heap = heap; _frame = frame;
            _publish = publish; _invoke = invoke; _authorizeCommand = authorizeCommand;
        }
        public Task<object> ReadAsync(MPADataTargetKind kind, string targetRef, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(targetRef)) throw new ArgumentException("目标引用为空");
            switch (kind)
            {
                case MPADataTargetKind.Variable:
                    MDSVariableTable table = FindVariable(targetRef, out string name);
                    MDSVariableDefinition definition = table.Definitions().SingleOrDefault(x => x.Name == name);
                    if (definition == null) throw new InvalidOperationException("MDS Variable 未定义：" + name);
                    return Task.FromResult(GetVariable(table, name, definition.ValueType));
                case MPADataTargetKind.DataHeap:
                    if (_heap == null) throw new NotSupportedException("当前没有 MDS Heap Owner");
                    return Task.FromResult(InvokeGeneric(_heap, "Get", _heap.Inspect(targetRef).DataType, targetRef));
                case MPADataTargetKind.DataStack:
                    if (_frame == null) throw new NotSupportedException("当前没有有效 MRK 节点数据帧");
                    return Task.FromResult(InvokeGeneric(_frame, "Get", _frame.Inspect(targetRef).DataType, targetRef));
                default: throw new NotSupportedException("当前 Owner 不支持该读取类别：" + kind);
            }
        }
        public Task WriteAsync(MPADataTargetKind kind, string targetRef, object value, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(targetRef)) throw new ArgumentException("目标引用为空");
            if (value == null) throw new ArgumentException("必须使用具体类型的数据值");
            switch (kind)
            {
                case MPADataTargetKind.Variable:
                    MDSVariableTable table = FindVariable(targetRef, out string name);
                    MDSVariableDefinition definition = table.Definitions().SingleOrDefault(x => x.Name == name);
                    if (definition == null || value.GetType() != definition.ValueType)
                        throw new InvalidOperationException("MDS Variable 未定义或数据类型不匹配");
                    InvokeGeneric(table, "Update", definition.ValueType, name, value);
                    break;
                case MPADataTargetKind.DataHeap:
                    if (_heap == null) throw new NotSupportedException("当前没有 MDS Heap Owner");
                    InvokeGeneric(_heap, _heap.Contains(targetRef) ? "Replace" : "Set",
                        value.GetType(), targetRef, value);
                    break;
                case MPADataTargetKind.DataStack:
                    if (_frame == null) throw new NotSupportedException("当前没有有效 MRK 节点数据帧");
                    InvokeGeneric(_frame, "Set", value.GetType(), targetRef, value);
                    break;
                default: throw new NotSupportedException("非数据值目标不支持 WriteValue：" + kind);
            }
            return Task.CompletedTask;
        }
        public Task PublishAsync(string targetRef, object value, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (_publish == null) throw new NotSupportedException("Message Owner 尚未接入");
            return _publish(targetRef, value, ct);
        }
        public Task InvokeAsync(string targetRef, object value, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (_invoke == null || _authorizeCommand == null)
                throw new NotSupportedException("命令执行要求真实 Owner 与显式授权回调");
            if (!_authorizeCommand(targetRef, value))
                throw new UnauthorizedAccessException("当前执行上下文未获命令授权：" + targetRef);
            return _invoke(targetRef, value, ct);
        }
        private MDSVariableTable FindVariable(string targetRef, out string name)
        {
            if (_variables == null) throw new NotSupportedException("尚未接入 MDS Variable Registry");
            int split = targetRef.IndexOf('/');
            if (split <= 0 || split == targetRef.Length - 1)
                throw new ArgumentException("变量引用必须是 scope/name");
            name = targetRef.Substring(split + 1);
            MDSVariableTable table = _variables.Find(targetRef.Substring(0, split));
            if (table == null) throw new InvalidOperationException("MDS 变量作用域不存在");
            return table;
        }
        private static object GetVariable(MDSVariableTable table, string name, Type type)
        { return InvokeGeneric(table, "Get", type, name); }
        private static object InvokeGeneric(object owner, string name, Type type, params object[] args)
        {
            MethodInfo method = owner.GetType().GetMethods().FirstOrDefault(x =>
                x.Name == name && x.IsGenericMethodDefinition &&
                x.GetGenericArguments().Length == 1 &&
                x.GetParameters().Length == args.Length &&
                x.GetParameters()[0].ParameterType == typeof(string));
            if (method == null) throw new MissingMethodException(owner.GetType().Name, name);
            try { return method.MakeGenericMethod(type).Invoke(owner, args); }
            catch (TargetInvocationException error) when (error.InnerException != null)
            { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
        }
    }
}
