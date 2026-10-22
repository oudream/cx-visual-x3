using System;
namespace CxVisual.Mechanism.Data.Variables
{
    // 来自 ReferTo VariableInfo；与单个 Flow 的 MDS Heap/Stack 区分作用域。
    public sealed class MDSVariableSample<T>
    {
        public MDSVariableSample(T value, DateTime updatedUtc, bool hasInitialValue)
        { Value=value; UpdatedUtc=updatedUtc; HasInitialValue=hasInitialValue; }
        public T Value { get; private set; }
        public DateTime UpdatedUtc { get; private set; }
        public bool HasInitialValue { get; private set; }
    }
    // 目标 VariableTable 直接实现；不保留旧静态 VariableManager 兼容外壳。
    public interface IMDSVariableTable
    {
        string ScopeId { get; }
        T Get<T>(string name);
        bool TryGet<T>(string name, out T value);
        void Update<T>(string name, T value);
        event Action<string> VariableChanged;
    }
}
