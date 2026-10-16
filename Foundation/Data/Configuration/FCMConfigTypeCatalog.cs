using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace CxVisual.Foundation.Data.Configuration
{
    public enum FCMConfigTypeKind
    {
        Boolean, Integer, Floating, Decimal, String, Enum, Time, Identifier, Resource,
        Nullable, Array, List, Set, Map, Object, Converter, Unsupported
    }
    public sealed class FCMConfigTypeIssue
    {
        internal FCMConfigTypeIssue(string path, string code, string message)
        { Path = path; Code = code; Message = message; }
        public string Path { get; private set; }
        public string Code { get; private set; }
        public string Message { get; private set; }
    }
    /// <summary>类型能力描述是只读静态契约，不检查实例中的具体数值。</summary>
    public sealed class FCMConfigTypeContract
    {
        internal FCMConfigTypeContract(Type type, FCMConfigTypeKind kind,
            IList<FCMConfigTypeIssue> issues)
        { DataType = type; Kind = kind; Issues = new List<FCMConfigTypeIssue>(issues).AsReadOnly(); }
        public Type DataType { get; private set; }
        public FCMConfigTypeKind Kind { get; private set; }
        public IList<FCMConfigTypeIssue> Issues { get; private set; }
        public bool CanRoundtrip { get { return Issues.Count == 0; } }
    }

    /// <summary>
    /// 与 FCMJsonValueCodec 所支持的 CLR 类型保持一致；
    /// 显式拒绝任意多态、非字符串字典 Key、无参构造缺失以及循环的静态类型树。
    /// </summary>
    public static class FCMConfigTypeCatalog
    {
        public static FCMConfigTypeContract Describe<T>() { return Describe(typeof(T)); }
        public static FCMConfigTypeContract Describe(Type type)
        {
            if (type == null) throw new ArgumentNullException("type");
            List<FCMConfigTypeIssue> errors = new List<FCMConfigTypeIssue>();
            FCMConfigTypeKind kind = Inspect(type, "$", new HashSet<Type>(), errors, 0);
            return new FCMConfigTypeContract(type, kind, errors);
        }
        private static FCMConfigTypeKind Inspect(Type type, string path,
            HashSet<Type> stack, IList<FCMConfigTypeIssue> issues, int depth)
        {
            if (depth > 64) { Issue(issues, path, "CONFIG_TYPE_DEPTH", "Type nesting exceeds 64"); return FCMConfigTypeKind.Unsupported; }
            Type nullable = Nullable.GetUnderlyingType(type);
            if (nullable != null)
            {
                Inspect(nullable, path + "?", stack, issues, depth + 1);
                return FCMConfigTypeKind.Nullable;
            }
            if (type == typeof(string) || type == typeof(char)) return FCMConfigTypeKind.String;
            if (type == typeof(bool)) return FCMConfigTypeKind.Boolean;
            if (type.IsEnum) return FCMConfigTypeKind.Enum;
            if (type == typeof(float) || type == typeof(double)) return FCMConfigTypeKind.Floating;
            if (type == typeof(decimal)) return FCMConfigTypeKind.Decimal;
            if (type == typeof(Guid)) return FCMConfigTypeKind.Identifier;
            if (type == typeof(DateTime) || type == typeof(DateTimeOffset) ||
                type == typeof(TimeSpan)) return FCMConfigTypeKind.Time;
            if (type == typeof(Uri) || type == typeof(Version)) return FCMConfigTypeKind.Resource;
            if (type.IsPrimitive) return FCMConfigTypeKind.Integer;
            FCMConfigConverterAttribute codec = (FCMConfigConverterAttribute)Attribute.GetCustomAttribute(
                type, typeof(FCMConfigConverterAttribute));
            if (codec != null)
            {
                Type contract = typeof(IFCMConfigValueConverter<>).MakeGenericType(type);
                if (codec.ConverterType == null || !contract.IsAssignableFrom(codec.ConverterType))
                    Issue(issues, path, "CONFIG_CONVERTER_CONTRACT", "Registered converter has wrong interface");
                return FCMConfigTypeKind.Converter;
            }
            if (type.IsArray)
            {
                if (type.GetArrayRank() != 1)
                {
                    Issue(issues, path, "CONFIG_ARRAY_RANK", "Only one-dimensional arrays supported");
                    return FCMConfigTypeKind.Unsupported;
                }
                Inspect(type.GetElementType(), path + "[]", stack, issues, depth + 1);
                return FCMConfigTypeKind.Array;
            }
            if (type.IsGenericType)
            {
                Type generic = type.GetGenericTypeDefinition();
                Type[] arguments = type.GetGenericArguments();
                if (generic == typeof(List<>) || generic == typeof(HashSet<>))
                {
                    Inspect(arguments[0], path + "[]", stack, issues, depth + 1);
                    return generic == typeof(List<>) ? FCMConfigTypeKind.List : FCMConfigTypeKind.Set;
                }
                if (generic == typeof(Dictionary<,>))
                {
                    if (arguments[0] != typeof(string))
                        Issue(issues, path, "CONFIG_MAP_KEY", "Dictionary requires string keys");
                    Inspect(arguments[1], path + "{}", stack, issues, depth + 1);
                    return FCMConfigTypeKind.Map;
                }
            }
            if (type == typeof(object) || type.IsInterface || type.IsAbstract || type.IsPointer)
            {
                Issue(issues, path, "CONFIG_POLYMORPHIC_TYPE", "Use a concrete model or explicit Converter");
                return FCMConfigTypeKind.Unsupported;
            }
            if (!type.IsValueType && type.GetConstructor(Type.EmptyTypes) == null)
            {
                Issue(issues, path, "CONFIG_NO_DEFAULT_CONSTRUCTOR", "Model needs public parameterless constructor");
                return FCMConfigTypeKind.Unsupported;
            }
            if (!stack.Add(type))
            {
                Issue(issues, path, "CONFIG_TYPE_CYCLE", "Recursive model requires explicit Converter");
                return FCMConfigTypeKind.Unsupported;
            }
            try
            {
                foreach (FCMConfigMemberDescriptor member in FCMConfigSchemaBuilder.Members(type))
                    Inspect(member.MemberType, path + "." + member.Name, stack, issues, depth + 1);
            }
            finally { stack.Remove(type); }
            return FCMConfigTypeKind.Object;
        }
        private static void Issue(IList<FCMConfigTypeIssue> list, string path, string code, string reason)
        { list.Add(new FCMConfigTypeIssue(path, code, reason)); }
    }
}
