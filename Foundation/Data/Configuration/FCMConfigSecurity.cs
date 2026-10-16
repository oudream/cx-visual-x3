using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace CxVisual.Foundation.Data.Configuration
{
    public enum FCMConfigAccessAction
    {
        Read, ReadSensitive, Edit, Commit, Apply, Restore, Subscribe, Export, Import, Audit
    }

    /// <summary>操作者主体身份只携带角色声明；身份验证由宿主负责。</summary>
    public sealed class FCMConfigPrincipal
    {
        public FCMConfigPrincipal(string principalId, IEnumerable<string> roles)
        {
            if (string.IsNullOrWhiteSpace(principalId)) throw new ArgumentException("Principal identity required");
            if (roles == null) throw new ArgumentNullException("roles");
            PrincipalId = principalId;
            string[] array = roles.ToArray();
            if (array.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Role name is empty");
            Roles = Array.AsReadOnly(array.Distinct(StringComparer.Ordinal).ToArray());
        }
        public string PrincipalId { get; private set; }
        public IList<string> Roles { get; private set; }
    }

    /// <summary>权限判定包括配置实例、动作和字段路径，不在低层接入具体 IAM SDK。</summary>
    public sealed class FCMConfigAccessRequest
    {
        public FCMConfigAccessRequest(FCMConfigIdentity identity, FCMConfigAccessAction action, string fieldPath)
        {
            Identity = identity ?? throw new ArgumentNullException("identity");
            Action = action; FieldPath = fieldPath;
        }
        public FCMConfigIdentity Identity { get; private set; }
        public FCMConfigAccessAction Action { get; private set; }
        public string FieldPath { get; private set; }
    }

    public sealed class FCMConfigAccessDecision
    {
        private FCMConfigAccessDecision(bool granted, string reason)
        { Granted = granted; Reason = reason; }
        public bool Granted { get; private set; }
        public string Reason { get; private set; }
        public static FCMConfigAccessDecision Permit()
        { return new FCMConfigAccessDecision(true, null); }
        public static FCMConfigAccessDecision Deny(string reason)
        { return new FCMConfigAccessDecision(false, reason ?? "CONFIG_ACCESS_DENIED"); }
    }

    public interface IFCMConfigAccessPolicy
    {
        FCMConfigAccessDecision Evaluate(FCMConfigPrincipal principal, FCMConfigAccessRequest request);
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    public sealed class FCMConfigSensitiveAttribute : Attribute { }

    /// <summary>默认拒绝的角色策略，授权必须明确匹配角色、动作、Section 和 Scope。</summary>
    public sealed class FCMConfigRolePolicy : IFCMConfigAccessPolicy
    {
        private sealed class Rule
        {
            public string Role, Section, ScopeKind, ScopeId, Path;
            public FCMConfigAccessAction Action;
        }
        private readonly object _gate = new object();
        private readonly List<Rule> _grants = new List<Rule>();
        public void Grant(string role, FCMConfigAccessAction action,
            string sectionId, string scopeKind, string scopeId, string fieldPath = "*")
        {
            if (string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(sectionId) ||
                string.IsNullOrWhiteSpace(scopeKind) || string.IsNullOrWhiteSpace(scopeId) ||
                string.IsNullOrWhiteSpace(fieldPath))
                throw new ArgumentException("Role rule fields cannot be blank");
            lock (_gate)
                _grants.Add(new Rule { Role = role, Action = action, Section = sectionId,
                    ScopeKind = scopeKind, ScopeId = scopeId, Path = fieldPath });
        }
        public void Revoke(string role, FCMConfigAccessAction action,
            string sectionId, string scopeKind, string scopeId)
        {
            lock (_gate) _grants.RemoveAll(r => r.Role == role && r.Action == action &&
                r.Section == sectionId && r.ScopeKind == scopeKind && r.ScopeId == scopeId);
        }
        public FCMConfigAccessDecision Evaluate(FCMConfigPrincipal principal, FCMConfigAccessRequest request)
        {
            if (principal == null || request == null) return FCMConfigAccessDecision.Deny("No principal or request");
            lock (_gate)
            {
                foreach (Rule rule in _grants)
                {
                    if (!principal.Roles.Contains(rule.Role) || rule.Action != request.Action ||
                        !Matches(rule.Section, request.Identity.SectionId) ||
                        !Matches(rule.ScopeKind, request.Identity.ScopeKind) ||
                        !Matches(rule.ScopeId, request.Identity.ScopeId)) continue;
                    if (rule.Path == "*" || rule.Path == request.FieldPath ||
                        (request.FieldPath != null && request.FieldPath.StartsWith(rule.Path + ".",
                            StringComparison.Ordinal)))
                        return FCMConfigAccessDecision.Permit();
                }
            }
            return FCMConfigAccessDecision.Deny("CONFIG_ACCESS_DENIED");
        }
        private static bool Matches(string rule, string actual)
        { return rule == "*" || string.Equals(rule, actual, StringComparison.Ordinal); }
    }

    /// <summary>安全属性树判断。完整模型读取包含任何敏感字段时必须拥有 ReadSensitive。</summary>
    public static class FCMConfigSensitivity
    {
        public static bool ContainsSensitive(Type modelType)
        { return Walk(modelType, new HashSet<Type>()); }
        public static bool IsSensitivePath(Type modelType, string path)
        {
            if (path == null) return ContainsSensitive(modelType);
            Type current = modelType;
            string[] segments = path.Split('.');
            foreach (string raw in segments)
            {
                string member = raw.Split('[')[0];
                System.Reflection.PropertyInfo prop = current.GetProperty(member);
                if (prop == null) return true; // Unknown path: fail closed.
                if (prop.IsDefined(typeof(FCMConfigSensitiveAttribute), true)) return true;
                current = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            }
            return ContainsSensitive(current);
        }
        private static bool Walk(Type type, HashSet<Type> seen)
        {
            if (type == null || FCMConfigSchemaBuilder.IsScalar(type) ||
                !seen.Add(type)) return false;
            try
            {
                foreach (System.Reflection.PropertyInfo p in type.GetProperties(
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                {
                    if (!p.CanRead) continue;
                    if (p.IsDefined(typeof(FCMConfigSensitiveAttribute), true) ||
                        Walk(p.PropertyType, seen)) return true;
                }
                return false;
            }
            finally { seen.Remove(type); }
        }
    }
}
