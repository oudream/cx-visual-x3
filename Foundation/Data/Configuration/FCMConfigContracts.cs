using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Data.Configuration
{
    public enum FCMConfigApplyPolicy { Immediate, NextFrame, NextTask, Reconnect, Restart }
    public enum FCMConfigChangeStage { Committed, Effective }
    public enum FCMConfigIssueSeverity { Warning, Error }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class FCMConfigSectionAttribute : Attribute
    {
        public FCMConfigSectionAttribute(string sectionId) { SectionId = sectionId; SchemaVersion = 1; }
        public string SectionId { get; private set; }
        public int SchemaVersion { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    public sealed class FCMConfigItemAttribute : Attribute
    {
        public FCMConfigItemAttribute(string label) { Label = label; ApplyPolicy = FCMConfigApplyPolicy.Immediate; }
        public string Label { get; private set; }
        public string MemberId { get; set; }
        public int Order { get; set; }
        public string BeforeId { get; set; }
        public string AfterId { get; set; }
        public string AliasFrom { get; set; }
        public int SinceSchemaVersion { get; set; }
        public string Unit { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public FCMConfigApplyPolicy ApplyPolicy { get; set; }
    }

    /// <summary>声明当前属性依赖的同 Section 绝对配置路径（不是校验器执行代码）。</summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true, Inherited = true)]
    public sealed class FCMConfigDependsOnAttribute : Attribute
    {
        public FCMConfigDependsOnAttribute(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Dependency path required", "path");
            Path = path;
        }
        public string Path { get; private set; }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class FCMConfigValidatorAttribute : Attribute
    {
        public FCMConfigValidatorAttribute(Type validatorType) { ValidatorType = validatorType; }
        public Type ValidatorType { get; private set; }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public sealed class FCMConfigCustomValidatorAttribute : Attribute
    {
        public FCMConfigCustomValidatorAttribute(Type owner, string methodName)
        { Owner = owner; MethodName = methodName; }
        public Type Owner { get; private set; }
        public string MethodName { get; private set; }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    public sealed class FCMConfigConverterAttribute : Attribute
    {
        public FCMConfigConverterAttribute(Type converterType) { ConverterType = converterType; }
        public Type ConverterType { get; private set; }
    }
    public interface IFCMConfigValueConverter<T>
    {
        string Write(T value);
        T Read(string json);
    }

    public sealed class FCMConfigIssue
    {
        public FCMConfigIssue(string path, string code, string message, FCMConfigIssueSeverity severity)
        { Path = path; Code = code; Message = message; Severity = severity; }
        public string Path { get; private set; }
        public string Code { get; private set; }
        public string Message { get; private set; }
        public FCMConfigIssueSeverity Severity { get; private set; }
        public static FCMConfigIssue Error(string path, string code, string message)
        { return new FCMConfigIssue(path, code, message, FCMConfigIssueSeverity.Error); }
        public static FCMConfigIssue Warning(string path, string code, string message)
        { return new FCMConfigIssue(path, code, message, FCMConfigIssueSeverity.Warning); }
    }

    public sealed class FCMConfigValidationContext
    {
        public FCMConfigValidationContext(string sectionId, string scope, string path)
        { SectionId = sectionId; Scope = scope; Path = path; }
        public string SectionId { get; private set; }
        public string Scope { get; private set; }
        public string Path { get; private set; }
        public FCMConfigValidationContext At(string path)
        { return new FCMConfigValidationContext(SectionId, Scope, path); }
    }

    public sealed class FCMConfigValidationReport
    {
        private readonly List<FCMConfigIssue> _issues = new List<FCMConfigIssue>();
        public IList<FCMConfigIssue> Issues { get { return _issues.AsReadOnly(); } }
        public bool IsValid
        {
            get
            {
                foreach (FCMConfigIssue issue in _issues)
                    if (issue.Severity == FCMConfigIssueSeverity.Error) return false;
                return true;
            }
        }
        public void Add(FCMConfigIssue issue) { if (issue != null) _issues.Add(issue); }
        public void AddRange(IEnumerable<FCMConfigIssue> issues)
        { if (issues != null) foreach (FCMConfigIssue issue in issues) Add(issue); }
    }

    public interface IFCMConfigValidator<T>
    {
        IEnumerable<FCMConfigIssue> Validate(T value, FCMConfigValidationContext context);
    }
    public interface IFCMAsyncConfigValidator<T>
    {
        Task<IEnumerable<FCMConfigIssue>> ValidateAsync(T value, FCMConfigValidationContext context, CancellationToken cancellationToken);
    }

    public sealed class FCMConfigMemberDescriptor
    {
        public string Name { get; internal set; }
        public string MemberId { get; internal set; }
        public string Label { get; internal set; }
        public Type MemberType { get; internal set; }
        public int Order { get; internal set; }
        public string BeforeId { get; internal set; }
        public string AfterId { get; internal set; }
        public string AliasFrom { get; internal set; }
        public int SinceSchemaVersion { get; internal set; }
        public string Unit { get; internal set; }
        public FCMConfigApplyPolicy ApplyPolicy { get; internal set; }
        public System.Reflection.PropertyInfo Property { get; internal set; }
    }

    public sealed class FCMConfigSchema
    {
        internal FCMConfigSchema(Type type, string sectionId, int schemaVersion, IList<FCMConfigMemberDescriptor> members)
        { ModelType = type; SectionId = sectionId; SchemaVersion = schemaVersion; Members = new List<FCMConfigMemberDescriptor>(members).AsReadOnly(); }
        public Type ModelType { get; private set; }
        public string SectionId { get; private set; }
        public int SchemaVersion { get; private set; }
        public IList<FCMConfigMemberDescriptor> Members { get; private set; }
    }

    public sealed class FCMConfigValueInfo<T>
    {
        public bool HasDefaultValue { get; set; }
        public T DefaultValue { get; set; }
        public bool HasPreviousSaved { get; set; }
        public T PreviousSavedValue { get; set; }
        public bool HasPreviousEffective { get; set; }
        public T PreviousEffectiveValue { get; set; }
        public bool HasLatestSavedValue { get; set; }
        public bool LatestSavedIsExplicitNull { get; set; }
        public T LatestSavedValue { get; set; }
        public bool HasEffectiveValue { get; set; }
        public bool EffectiveIsExplicitNull { get; set; }
        public T EffectiveValue { get; set; }
        public bool PreviousSavedIsExplicitNull { get; set; }
        public bool PreviousEffectiveIsExplicitNull { get; set; }
        public string SavedVersion { get; set; }
        public string EffectiveVersion { get; set; }
        public string EffectiveSource { get; set; }
        public bool IsPending { get; set; }
    }

    public sealed class FCMConfigChange<T>
    {
        public FCMConfigChangeStage Stage { get; set; }
        public string Path { get; set; }
        public T OldValue { get; set; }
        public T NewValue { get; set; }
        public bool HasDefaultValue { get; set; }
        public T DefaultValue { get; set; }
        public string Version { get; set; }
        public string TransactionId { get; set; }
        /// <summary>同一个发布器实例内单调递增；跨进程稳定游标另由持久 Journal 提供。</summary>
        public long Sequence { get; set; }
        public DateTimeOffset TimestampUtc { get; set; }
        public IList<string> ChangedPaths { get; set; }
    }
}