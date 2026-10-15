using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace CxVisual.Foundation.Data.Configuration
{
    public static class FCMConfigSchemaBuilder
    {
        private static readonly ConcurrentDictionary<Type, FCMConfigSchema> Cache = new ConcurrentDictionary<Type, FCMConfigSchema>();
        public static FCMConfigSchema Describe<T>() { return Describe(typeof(T)); }
        public static FCMConfigSchema Describe(Type type)
        {
            if (type == null) throw new ArgumentNullException("type");
            return Cache.GetOrAdd(type, Create);
        }
        public static IList<FCMConfigMemberDescriptor> Members(Type type)
        {
            List<FCMConfigMemberDescriptor> items = new List<FCMConfigMemberDescriptor>();
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanRead || !property.CanWrite || property.GetIndexParameters().Length != 0) continue;
                FCMConfigItemAttribute item = property.GetCustomAttributes(typeof(FCMConfigItemAttribute), true)
                    .Cast<FCMConfigItemAttribute>().FirstOrDefault();
                items.Add(new FCMConfigMemberDescriptor
                {
                    Name = property.Name, MemberId = item != null && !string.IsNullOrWhiteSpace(item.MemberId) ? item.MemberId : property.Name,
                    Label = item == null ? property.Name : item.Label, MemberType = property.PropertyType, Property = property,
                    Order = item == null ? 0 : item.Order, AfterId = item == null ? null : item.AfterId,
                    BeforeId = item == null ? null : item.BeforeId,
                    AliasFrom = item == null ? null : item.AliasFrom, SinceSchemaVersion = item == null ? 1 : item.SinceSchemaVersion,
                    ApplyPolicy = item == null ? FCMConfigApplyPolicy.Immediate : item.ApplyPolicy,
                    Unit = item == null ? null : item.Unit
                });
            }
            return items.OrderBy(x => x.Order <= 0 ? int.MaxValue : x.Order).ThenBy(x => x.MemberId, StringComparer.Ordinal).ToArray();
        }
        private static FCMConfigSchema Create(Type type)
        {
            FCMConfigSectionAttribute section = type.GetCustomAttributes(typeof(FCMConfigSectionAttribute), false)
                .Cast<FCMConfigSectionAttribute>().FirstOrDefault();
            if (section == null || string.IsNullOrWhiteSpace(section.SectionId) || section.SchemaVersion < 1)
                throw new InvalidOperationException("Config model must have a valid [FCMConfigSection].");
            IList<FCMConfigMemberDescriptor> members = Members(type);
            ValidateMembers(members);
            return new FCMConfigSchema(type, section.SectionId, section.SchemaVersion, members);
        }
        private static void ValidateMembers(IList<FCMConfigMemberDescriptor> members)
        {
            if (members.Select(x => x.MemberId).Distinct(StringComparer.Ordinal).Count() != members.Count)
                throw new InvalidOperationException("Config has duplicate MemberId.");
            foreach (FCMConfigMemberDescriptor m in members)
            {
                if (!string.IsNullOrEmpty(m.AliasFrom) && members.Any(x => x.Name == m.AliasFrom && x.Name != m.Name))
                    throw new InvalidOperationException("AliasFrom collides with an existing property: " + m.AliasFrom);
                if (m.BeforeId == m.MemberId || m.AfterId == m.MemberId)
                    throw new InvalidOperationException("Self-referencing config insertion anchor: " + m.Name);
                if (!string.IsNullOrWhiteSpace(m.BeforeId) && !members.Any(x => x.MemberId == m.BeforeId))
                    throw new InvalidOperationException("Unknown BeforeId: " + m.BeforeId);
                if (!string.IsNullOrWhiteSpace(m.AfterId) && !members.Any(x => x.MemberId == m.AfterId))
                    throw new InvalidOperationException("Unknown AfterId: " + m.AfterId);
            }
            // The ordering graph must be acyclic.
            Dictionary<string, List<string>> next = members.ToDictionary(x => x.MemberId, x => new List<string>(), StringComparer.Ordinal);
            foreach (FCMConfigMemberDescriptor m in members)
            {
                if (!string.IsNullOrWhiteSpace(m.BeforeId)) next[m.MemberId].Add(m.BeforeId);
                if (!string.IsNullOrWhiteSpace(m.AfterId)) next[m.AfterId].Add(m.MemberId);
            }
            Dictionary<string, int> colors = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string key in next.Keys) Visit(key, next, colors);
        }
        private static void Visit(string key, Dictionary<string, List<string>> edges, Dictionary<string, int> colors)
        {
            int state;
            if (colors.TryGetValue(key, out state))
            { if (state == 1) throw new InvalidOperationException("Cyclic config insertion anchors: " + key); return; }
            colors[key] = 1;
            foreach (string target in edges[key]) Visit(target, edges, colors);
            colors[key] = 2;
        }
        public static bool IsScalar(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            return Attribute.IsDefined(type, typeof(FCMConfigConverterAttribute)) ||
                type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) ||
                type == typeof(Guid) || type == typeof(DateTime) || type == typeof(DateTimeOffset) ||
                type == typeof(TimeSpan) || type == typeof(Uri) || type == typeof(Version);
        }
    }

    public static class FCMConfigValidationEngine
    {
        public static FCMConfigValidationReport Validate<T>(T value, string scope)
        {
            FCMConfigSchema schema = FCMConfigSchemaBuilder.Describe<T>();
            FCMConfigValidationReport report = new FCMConfigValidationReport();
            Stack<object> stack = new Stack<object>();
            Walk(value, typeof(T), new FCMConfigValidationContext(schema.SectionId, scope, ""), report, stack, 0);
            return report;
        }
        public static async Task<FCMConfigValidationReport> ValidateAsync<T>(T value, string scope, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FCMConfigValidationReport report = Validate(value, scope);
            Type interfaceType = typeof(IFCMAsyncConfigValidator<>).MakeGenericType(typeof(T));
            foreach (FCMConfigValidatorAttribute attr in typeof(T).GetCustomAttributes(typeof(FCMConfigValidatorAttribute), false))
            {
                if (!interfaceType.IsAssignableFrom(attr.ValidatorType)) continue;
                cancellationToken.ThrowIfCancellationRequested();
                object validator = Activator.CreateInstance(attr.ValidatorType);
                MethodInfo method = interfaceType.GetMethod("ValidateAsync");
                Task<IEnumerable<FCMConfigIssue>> task = (Task<IEnumerable<FCMConfigIssue>>)method.Invoke(validator,
                    new object[] { value, new FCMConfigValidationContext(FCMConfigSchemaBuilder.Describe<T>().SectionId, scope, ""), cancellationToken });
                report.AddRange(await task.ConfigureAwait(false));
            }
            return report;
        }
        private static void Walk(object value, Type type, FCMConfigValidationContext context,
            FCMConfigValidationReport result, Stack<object> stack, int depth)
        {
            if (value == null) return;
            if (depth > 64) { result.Add(FCMConfigIssue.Error(context.Path, "DEPTH_LIMIT", "Config nesting exceeds limit.")); return; }
            Type actualType = Nullable.GetUnderlyingType(type) ?? type;
            if (FCMConfigSchemaBuilder.IsScalar(actualType)) return;
            if (stack.Any(x => Object.ReferenceEquals(x, value)))
            { result.Add(FCMConfigIssue.Error(context.Path, "OBJECT_CYCLE", "Cyclic object reference is not supported.")); return; }
            stack.Push(value);
            try
            {
                IDictionary dict = value as IDictionary;
                if (dict != null)
                {
                    foreach (DictionaryEntry entry in dict)
                    {
                        if (!(entry.Key is string)) { result.Add(FCMConfigIssue.Error(context.Path, "MAP_KEY_TYPE", "Only string dictionary keys are supported.")); continue; }
                        string key = Convert.ToString(entry.Key, CultureInfo.InvariantCulture);
                        Walk(entry.Value, entry.Value == null ? typeof(object) : entry.Value.GetType(),
                            context.At(context.Path + "[\"" + key.Replace("\"", "\\\"") + "\"]"), result, stack, depth + 1);
                    }
                    return;
                }
                IEnumerable sequence = value as IEnumerable;
                if (sequence != null && !(value is string))
                {
                    int i = 0;
                    foreach (object element in sequence)
                    {
                        Walk(element, element == null ? typeof(object) : element.GetType(), context.At(context.Path + "[" + i + "]"), result, stack, depth + 1);
                        i++;
                    }
                    return;
                }
                foreach (FCMConfigMemberDescriptor member in FCMConfigSchemaBuilder.Members(actualType))
                {
                    object v = member.Property.GetValue(value, null);
                    string path = string.IsNullOrEmpty(context.Path) ? member.Name : context.Path + "." + member.Name;
                    foreach (ValidationAttribute attribute in member.Property.GetCustomAttributes(typeof(ValidationAttribute), true))
                    {
                        try
                        {
                            RegularExpressionAttribute regex = attribute as RegularExpressionAttribute;
                            if (regex != null)
                            {
                                if (v != null && !Regex.IsMatch(Convert.ToString(v, CultureInfo.InvariantCulture),
                                    @"\A(?:" + regex.Pattern + @")\z", RegexOptions.None, TimeSpan.FromMilliseconds(250)))
                                    result.Add(FCMConfigIssue.Error(path, "REGEX", "Value does not match " + regex.Pattern));
                                continue;
                            }
                            ValidationResult validation = attribute.GetValidationResult(v,
                                new ValidationContext(value, null, null) { MemberName = member.Name });
                            if (validation != ValidationResult.Success)
                                result.Add(FCMConfigIssue.Error(path, attribute.GetType().Name, validation.ErrorMessage));
                        }
                        catch (Exception error) { result.Add(FCMConfigIssue.Error(path, "VALIDATION_EXCEPTION", error.Message)); }
                    }
                    foreach (FCMConfigCustomValidatorAttribute attr in member.Property.GetCustomAttributes(typeof(FCMConfigCustomValidatorAttribute), true))
                    {
                        try
                        {
                            MethodInfo method = attr.Owner.GetMethod(attr.MethodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                            if (method == null) throw new MissingMethodException(attr.Owner.FullName, attr.MethodName);
                            FCMConfigIssue issue = method.Invoke(null, new object[] { v, context.At(path) }) as FCMConfigIssue;
                            result.Add(issue);
                        }
                        catch (Exception ex) { result.Add(FCMConfigIssue.Error(path, "CUSTOM_VALIDATOR_EXCEPTION", ex.InnerException == null ? ex.Message : ex.InnerException.Message)); }
                    }
                    Walk(v, member.MemberType, context.At(path), result, stack, depth + 1);
                }
                foreach (FCMConfigValidatorAttribute attr in actualType.GetCustomAttributes(typeof(FCMConfigValidatorAttribute), false))
                {
                    try
                    {
                        Type interfaceType = typeof(IFCMConfigValidator<>).MakeGenericType(actualType);
                        if (!interfaceType.IsAssignableFrom(attr.ValidatorType))
                        {
                            Type asyncType = typeof(IFCMAsyncConfigValidator<>).MakeGenericType(actualType);
                            if (asyncType.IsAssignableFrom(attr.ValidatorType)) continue;
                            throw new InvalidOperationException("Validator does not implement " + interfaceType.Name);
                        }
                        object validator = Activator.CreateInstance(attr.ValidatorType);
                        MethodInfo method = interfaceType.GetMethod("Validate");
                        IEnumerable<FCMConfigIssue> issues = (IEnumerable<FCMConfigIssue>)method.Invoke(validator, new object[] { value, context });
                        result.AddRange(issues);
                    }
                    catch (Exception ex) { result.Add(FCMConfigIssue.Error(context.Path, "OBJECT_VALIDATOR_EXCEPTION", ex.InnerException == null ? ex.Message : ex.InnerException.Message)); }
                }
            }
            finally { stack.Pop(); }
        }
    }
}