using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace CxVisual.Foundation.Data.Configuration
{
    /// <summary>对工具层开放的只读配置属性元数据；包含约束、默认值、类型与显示顺序。</summary>
    public sealed class FCMConfigFieldInfo
    {
        internal FCMConfigFieldInfo(string path, string memberId, string title, Type type, int depth, int order,
            string unit, string category, string description, bool required, string range, string regex,
            string defaultJson, FCMConfigApplyPolicy applyPolicy)
        {
            Path = path; MemberId = memberId; Title = title; DataType = type; Depth = depth;
            Order = order; Unit = unit; Category = category; Description = description;
            Required = required; Range = range; Regex = regex; DefaultJson = defaultJson; ApplyPolicy = applyPolicy;
        }
        public string Path { get; private set; }
        public string MemberId { get; private set; }
        public string Title { get; private set; }
        public Type DataType { get; private set; }
        public int Depth { get; private set; }
        public int Order { get; private set; }
        public string Unit { get; private set; }
        public string Category { get; private set; }
        public string Description { get; private set; }
        public bool Required { get; private set; }
        public string Range { get; private set; }
        public string Regex { get; private set; }
        /// <summary>无默认值时为 null；默认值为 JSON null 时为字符串 "null"。</summary>
        public string DefaultJson { get; private set; }
        public FCMConfigApplyPolicy ApplyPolicy { get; private set; }
    }

    /// <summary>
    /// 配置定义的统一可读目录：自动表单、搜索、API 文档都复用 Schema 元数据。
    /// 不创建配置副本，不管理运行值和文件。
    /// </summary>
    public static class FCMConfigCatalog
    {
        public static IList<FCMConfigFieldInfo> Describe<T>() where T : new()
        { return Describe(typeof(T)); }
        public static IList<FCMConfigFieldInfo> Describe(Type modelType)
        {
            if (modelType == null) throw new ArgumentNullException("modelType");
            FCMConfigSchemaBuilder.Describe(modelType);
            object defaults = Activator.CreateInstance(modelType);
            List<FCMConfigFieldInfo> fields = new List<FCMConfigFieldInfo>();
            Visit(modelType, defaults, "", "", "", 0, fields, new HashSet<Type>());
            return fields.AsReadOnly();
        }
        /// <summary>从属性声明建立可校验的依赖图，供增量校验与编辑影响分析共用。</summary>
        public static FCMConfigDependencyGraph BuildDependencyGraph<T>() where T : new()
        { return BuildDependencyGraph(typeof(T)); }
        public static FCMConfigDependencyGraph BuildDependencyGraph(Type modelType)
        {
            IList<FCMConfigFieldInfo> fields = Describe(modelType);
            HashSet<string> known = new HashSet<string>(fields.Select(x => x.Path), StringComparer.Ordinal);
            FCMConfigDependencyGraph graph = new FCMConfigDependencyGraph();
            foreach (FCMConfigFieldInfo field in fields)
            {
                Type owner = modelType;
                System.Reflection.PropertyInfo property = null;
                foreach (string member in field.Path.Split('.'))
                {
                    property = owner.GetProperty(member, System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.Public);
                    if (property == null) throw new InvalidOperationException("CONFIG_DEPENDENCY_UNKNOWN_MEMBER: " + field.Path);
                    owner = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                }
                foreach (FCMConfigDependsOnAttribute attribute in property.GetCustomAttributes(
                    typeof(FCMConfigDependsOnAttribute), true).Cast<FCMConfigDependsOnAttribute>())
                {
                    if (!known.Contains(attribute.Path))
                        throw new InvalidOperationException("CONFIG_DEPENDENCY_UNKNOWN_TARGET: " + attribute.Path);
                    graph.Register(field.Path, attribute.Path);
                }
            }
            return graph;
        }

        public static IList<FCMConfigFieldInfo> Search<T>(string keyword) where T : new()
        {
            IList<FCMConfigFieldInfo> all = Describe<T>();
            if (string.IsNullOrWhiteSpace(keyword)) return all;
            return all.Where(x => Contains(x.Path, keyword) || Contains(x.MemberId, keyword) ||
                Contains(x.Title, keyword) || Contains(x.Category, keyword) ||
                Contains(x.Description, keyword) || Contains(x.Unit, keyword))
                .ToList().AsReadOnly();
        }
        public static string ExportMarkdown<T>() where T : new()
        {
            FCMConfigSchema schema = FCMConfigSchemaBuilder.Describe<T>();
            StringBuilder text = new StringBuilder();
            text.AppendLine("# " + schema.SectionId);
            text.AppendLine();
            text.AppendLine("SchemaVersion: " + schema.SchemaVersion);
            text.AppendLine();
            text.AppendLine("| 配置路径 | 数据类型 | 名称 | 类别 | 单位 | 默认值 | 规则 | 生效方式 |");
            text.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- |");
            foreach (FCMConfigFieldInfo field in Describe<T>())
            {
                string rule = field.Required ? "Required" : "";
                if (!string.IsNullOrEmpty(field.Range)) rule += " Range:" + field.Range;
                if (!string.IsNullOrEmpty(field.Regex)) rule += " Regex:" + field.Regex;
                text.AppendLine("| " + Safe(field.Path) + " | " + Safe(field.DataType.Name) + " | " +
                    Safe(field.Title) + " | " + Safe(field.Category) + " | " + Safe(field.Unit) + " | " +
                    Safe(field.DefaultJson) + " | " + Safe(rule.Trim()) + " | " + field.ApplyPolicy + " |");
            }
            return text.ToString();
        }
        private static bool Contains(string source, string query)
        { return source != null && source.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0; }
        private static string Safe(string value)
        { return (value ?? "").Replace("|", "\\|").Replace("\r", " ").Replace("\n", " "); }
        private static void Visit(Type type, object sample, string path, string memberId, string category,
            int depth, IList<FCMConfigFieldInfo> result, HashSet<Type> recursion)
        {
            if (depth > 16) throw new InvalidOperationException("Configuration catalog depth exceeds 16");
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (FCMConfigSchemaBuilder.IsScalar(type)) return;
            if (type.IsArray || typeof(IEnumerable).IsAssignableFrom(type))
                return; // Dynamic dictionary/list entries are described at their owning property.
            if (!recursion.Add(type)) return; // Model cycles are rejected separately by serialization and validation.
            try
            {
                foreach (FCMConfigMemberDescriptor member in FCMConfigSchemaBuilder.Members(type))
                {
                    string itemPath = path.Length == 0 ? member.Name : path + "." + member.Name;
                    string itemId = memberId.Length == 0 ? member.MemberId : memberId + "." + member.MemberId;
                    PropertyInfo property = member.Property;
                    RequiredAttribute required = property.GetCustomAttributes(typeof(RequiredAttribute), true)
                        .Cast<RequiredAttribute>().FirstOrDefault();
                    RangeAttribute range = property.GetCustomAttributes(typeof(RangeAttribute), true)
                        .Cast<RangeAttribute>().FirstOrDefault();
                    RegularExpressionAttribute regex = property.GetCustomAttributes(typeof(RegularExpressionAttribute), true)
                        .Cast<RegularExpressionAttribute>().FirstOrDefault();
                    FCMConfigItemAttribute metadata = property.GetCustomAttributes(typeof(FCMConfigItemAttribute), true)
                        .Cast<FCMConfigItemAttribute>().FirstOrDefault();
                    object value = sample == null ? null : property.GetValue(sample, null);
                    string defaultJson = value == null ? (sample == null ? null : "null") : FCMJsonValueCodec.Serialize(value);
                    string itemCategory = metadata == null || string.IsNullOrWhiteSpace(metadata.Category)
                        ? category : metadata.Category;
                    result.Add(new FCMConfigFieldInfo(itemPath, itemId, member.Label, member.MemberType, depth,
                        member.Order, member.Unit, itemCategory, metadata == null ? null : metadata.Description,
                        required != null, range == null ? null : range.Minimum + " .. " + range.Maximum,
                        regex == null ? null : regex.Pattern, defaultJson, member.ApplyPolicy));
                    Type nested = Nullable.GetUnderlyingType(member.MemberType) ?? member.MemberType;
                    if (!FCMConfigSchemaBuilder.IsScalar(nested))
                        Visit(nested, value, itemPath, itemId, itemCategory, depth + 1, result, recursion);
                }
            }
            finally { recursion.Remove(type); }
        }
    }
}
