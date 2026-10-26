using System;

namespace CxVisual.Foundation.Data.Variables
{
    /// <summary>跨模块引用变量的不可变键；不是工程定义或运行状态仓库。</summary>
    public struct FDVVariableKey : IEquatable<FDVVariableKey>
    {
        public FDVVariableKey(string scopeId, string variableId)
        {
            if (string.IsNullOrWhiteSpace(scopeId) || string.IsNullOrWhiteSpace(variableId))
                throw new ArgumentException("变量Scope和VariableId不能为空");
            ScopeId = scopeId.Trim(); VariableId = variableId.Trim();
        }
        public string ScopeId { get; private set; }
        public string VariableId { get; private set; }
        public bool Equals(FDVVariableKey other)
        {
            return string.Equals(ScopeId, other.ScopeId, StringComparison.Ordinal) &&
                   string.Equals(VariableId, other.VariableId, StringComparison.Ordinal);
        }
        public override bool Equals(object obj) { return obj is FDVVariableKey && Equals((FDVVariableKey)obj); }
        public override int GetHashCode()
        {
            unchecked { return ((ScopeId == null ? 0 : StringComparer.Ordinal.GetHashCode(ScopeId)) * 397) ^
                (VariableId == null ? 0 : StringComparer.Ordinal.GetHashCode(VariableId)); }
        }
        public override string ToString() { return ScopeId + "/" + VariableId; }
        public static bool operator ==(FDVVariableKey a, FDVVariableKey b) { return a.Equals(b); }
        public static bool operator !=(FDVVariableKey a, FDVVariableKey b) { return !a.Equals(b); }
    }
}
