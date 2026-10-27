using System;
using System.Collections.Generic;
using System.Linq;

namespace CxVisual.Mechanism.Data.Variables
{
    /// <summary>
    /// 静态访问规则，必须在定义阶段确定；无策略时保留历史Writable及显式Provider绑定语义。
    /// 这里只判定变量运行值写入，不替代MRK命令安全审核或设备级权限。
    /// </summary>
    public sealed class MDSVariableAccessPolicy
    {
        private readonly HashSet<string> _principals;
        private readonly HashSet<string> _providers;

        public MDSVariableAccessPolicy(IEnumerable<string> allowedPrincipals = null,
            IEnumerable<string> allowedProviders = null, bool allowProviderRefresh = true)
        {
            _principals = Copy(allowedPrincipals);
            _providers = Copy(allowedProviders);
            AllowProviderRefresh = allowProviderRefresh;
        }
        private static HashSet<string> Copy(IEnumerable<string> names)
        {
            if (names == null) return null; // null=不附加额外限制；空集合=全部拒绝
            string[] values = names.ToArray();
            if (values.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("访问策略身份不得为空");
            return new HashSet<string>(values.Select(x => x.Trim()), StringComparer.Ordinal);
        }
        public bool AllowProviderRefresh { get; private set; }
        public string[] AllowedPrincipals
        { get { return _principals == null ? null : _principals.OrderBy(x => x, StringComparer.Ordinal).ToArray(); } }
        public string[] AllowedProviders
        { get { return _providers == null ? null : _providers.OrderBy(x => x, StringComparer.Ordinal).ToArray(); } }
        internal void Demand(MDSVariableDefinition definition, string principal, string source, bool fromProvider)
        {
            if (fromProvider)
            {
                if (!AllowProviderRefresh || (_providers != null && !_providers.Contains(source ?? "")))
                    throw new UnauthorizedAccessException("Provider无权刷新变量: " + definition.Name);
            }
            else
            {
                if (!definition.Writable) throw new InvalidOperationException("变量为只读: " + definition.Name);
                if (_principals != null && !_principals.Contains(principal ?? ""))
                    throw new UnauthorizedAccessException("调用者无权写入变量: " + definition.Name);
            }
        }
        public bool EquivalentTo(MDSVariableAccessPolicy other)
        {
            if (other == null || AllowProviderRefresh != other.AllowProviderRefresh) return false;
            return (_principals == null ? other._principals == null :
                other._principals != null && _principals.SetEquals(other._principals)) &&
                (_providers == null ? other._providers == null :
                other._providers != null && _providers.SetEquals(other._providers));
        }
    }
}
