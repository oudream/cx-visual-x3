using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using CxVisual.Foundation.Data.Types;
namespace CxVisual.Foundation.Data.Commands
{
    /// <summary>命令基础输入/输出参数契约，复用统一Foundation数据类型。</summary>
    public sealed class FDCCommandParameterDefinition
    {
        public FDCCommandParameterDefinition(string code, Type dataType, bool required,
            bool hasDefaultValue = false, object defaultValue = null,
            string unit = null, string description = null,
            object minValue = null, object maxValue = null,
            bool? allowNull = null, bool readOnly = false, string formatPattern = null,
            IEnumerable<FDCCommandParameterDefinition> children = null)
        {
            CheckCode(code);
            if (dataType == null || dataType == typeof(void) || dataType.IsPointer ||
                dataType.ContainsGenericParameters) throw new ArgumentException("无效参数类型", "dataType");
            if (required && hasDefaultValue) throw new ArgumentException("必填参数不可指定默认值");
            if (unit != null && unit.Length > 32) throw new ArgumentException("单位过长", "unit");
            Code = code; DataType = dataType; TypeDescriptor = FDTDataTypeDescriptor.Describe(dataType); Required = required; HasDefaultValue = hasDefaultValue;
            Unit = unit; Description = description ?? string.Empty;
            MinValue = minValue; MaxValue = maxValue;
            AllowNull = allowNull ?? (!required && TypeDescriptor.IsNullable);
            ReadOnly = readOnly;
            FormatPattern = formatPattern;
            if (AllowNull && !TypeDescriptor.IsNullable)
                throw new ArgumentException("非空值类型不能设置可空", "allowNull");
            if (formatPattern != null)
            {
                if (dataType != typeof(string)) throw new ArgumentException("仅字符串允许格式模式");
                if (formatPattern.Length > 256) throw new ArgumentException("格式模式过长");
                new Regex(formatPattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
            }
            FDCCommandParameterDefinition[] nested =
                (children ?? Enumerable.Empty<FDCCommandParameterDefinition>()).ToArray();
            if (nested.Any(c => c == null) ||
                nested.GroupBy(c => c.Code, StringComparer.Ordinal).Any(g => g.Count() > 1))
                throw new ArgumentException("结构化参数包含空成员或重复成员名称");
            if (nested.Length > 0 &&
                dataType != typeof(Dictionary<string, object>) &&
                dataType != typeof(IDictionary<string, object>) &&
                dataType != typeof(IReadOnlyDictionary<string, object>))
                throw new ArgumentException("嵌套子参数仅适用于命名对象Map");
            Children = Array.AsReadOnly(nested);
            if (minValue != null) ValidateType(minValue);
            if (maxValue != null) ValidateType(maxValue);
            if (minValue != null && maxValue != null && Compare(minValue, maxValue) > 0)
                throw new ArgumentException("最小值不得大于最大值");
            if (hasDefaultValue)
            {
                Type t = defaultValue == null ? null : defaultValue.GetType();
                if (t != null && !(t.IsPrimitive || t.IsEnum || defaultValue is decimal ||
                    defaultValue is string || defaultValue is DateTime || defaultValue is Guid))
                    throw new ArgumentException("可变引用类型的默认值应交由FCM值适配器生成");
                ValidateValue(defaultValue);
            }
            DefaultValue = defaultValue;
        }
        public string Code { get; private set; }
        public Type DataType { get; private set; }
        public FDTDataTypeDescriptor TypeDescriptor { get; private set; }
        public bool Required { get; private set; }
        public bool HasDefaultValue { get; private set; }
        public object DefaultValue { get; private set; }
        public string Unit { get; private set; }
        public string Description { get; private set; }
        public object MinValue { get; private set; }
        public object MaxValue { get; private set; }
        public bool AllowNull { get; private set; }
        /// <summary>Studio编辑权限元数据，生产阶段还须由接收Host授权。</summary>
        public bool ReadOnly { get; private set; }
        public string FormatPattern { get; private set; }
        public IReadOnlyList<FDCCommandParameterDefinition> Children { get; private set; }
        internal static void CheckCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code) || code.Length > 64 || code == "." || code == "..")
                throw new ArgumentException("命令编码无效", "code");
            foreach (char c in code)
                if (!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
                    (c >= '0' && c <= '9') || c == '_' || c == '-' || c == '.'))
                    throw new ArgumentException("命令编码存在非法字符", "code");
        }
        private void ValidateType(object value)
        {
            if (value == null)
            {
                if (!AllowNull)
                    throw new ArgumentException("参数不能为空: " + Code);
                return;
            }
            Type type = Nullable.GetUnderlyingType(DataType) ?? DataType;
            if (!type.IsInstanceOfType(value)) throw new ArgumentException("参数类型不匹配: " + Code);
            if ((value is double && (double.IsNaN((double)value) || double.IsInfinity((double)value))) ||
                (value is float && (float.IsNaN((float)value) || float.IsInfinity((float)value))))
                throw new ArgumentException("浮点参数不可为NaN或无穷大: " + Code);
        }
        private static int Compare(object a, object b)
        {
            IComparable c = a as IComparable;
            if (c == null) throw new ArgumentException("有界参数类型必须可比较");
            return c.CompareTo(b);
        }
        public void ValidateValue(object value)
        {
            ValidateType(value);
            if (value == null) return;
            if (FormatPattern != null)
            {
                bool matches;
                try
                {
                    matches = Regex.IsMatch((string)value, FormatPattern,
                        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
                }
                catch (RegexMatchTimeoutException)
                {
                    // 用户可控输入不允许把正则超时异常冲出命令受理边界。
                    throw new ArgumentException("参数格式校验超时: " + Code);
                }
                if (!matches) throw new ArgumentException("参数格式不符合要求: " + Code);
            }
            if (Children.Count > 0)
            {
                IEnumerable<KeyValuePair<string, object>> pairs =
                    value as IDictionary<string, object>;
                if (pairs == null)
                    pairs = value as IReadOnlyDictionary<string, object>;
                if (pairs == null) throw new ArgumentException("结构参数必须是命名对象: " + Code);
                Dictionary<string, object> nestedValues = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, object> item in pairs)
                {
                    if (string.IsNullOrWhiteSpace(item.Key) || !Children.Any(c => c.Code == item.Key))
                        throw new ArgumentException("未知结构成员: " + Code + "." + item.Key);
                    nestedValues.Add(item.Key, item.Value);
                }
                foreach (FDCCommandParameterDefinition child in Children)
                {
                    object childValue;
                    if (nestedValues.TryGetValue(child.Code, out childValue))
                        child.ValidateValue(childValue);
                    else if (child.Required)
                        throw new ArgumentException("缺少必填结构成员: " + Code + "." + child.Code);
                }
            }
            if (MinValue != null && Compare(value, MinValue) < 0)
                throw new ArgumentOutOfRangeException(Code, "参数小于下限");
            if (MaxValue != null && Compare(value, MaxValue) > 0)
                throw new ArgumentOutOfRangeException(Code, "参数大于上限");
        }
    }
    /// <summary>命令静态定义，不保存执行者、RequestId或MRK Operation状态。</summary>
    public sealed class FDCCommandDefinition
    {
        private readonly ReadOnlyCollection<FDCCommandParameterDefinition> _inputs;
        private readonly ReadOnlyCollection<FDCCommandParameterDefinition> _outputs;
        public FDCCommandDefinition(long id, string code, string name,
            IEnumerable<FDCCommandParameterDefinition> inputs,
            IEnumerable<FDCCommandParameterDefinition> outputs, int schemaVersion = 1,
            string description = null, string permissionKey = null, int timeoutMilliseconds = 0)
        {
            if (id <= 0) throw new ArgumentOutOfRangeException("id");
            FDCCommandParameterDefinition.CheckCode(code);
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("显示名无效", "name");
            if (schemaVersion < 1) throw new ArgumentOutOfRangeException("schemaVersion");
            if (timeoutMilliseconds < 0) throw new ArgumentOutOfRangeException("timeoutMilliseconds");
            FDCCommandParameterDefinition[] ins = (inputs ?? Enumerable.Empty<FDCCommandParameterDefinition>()).ToArray();
            FDCCommandParameterDefinition[] outs = (outputs ?? Enumerable.Empty<FDCCommandParameterDefinition>()).ToArray();
            if (ins.Concat(outs).Any(x => x == null) ||
                ins.GroupBy(x => x.Code, StringComparer.Ordinal).Any(g => g.Count() > 1) ||
                outs.GroupBy(x => x.Code, StringComparer.Ordinal).Any(g => g.Count() > 1))
                throw new ArgumentException("存在空参数或重复参数编码");
            CommandId = id; CommandCode = code; Name = name; SchemaVersion = schemaVersion;
            Description = description ?? string.Empty; PermissionKey = permissionKey ?? string.Empty;
            TimeoutMilliseconds = timeoutMilliseconds;
            _inputs = Array.AsReadOnly(ins); _outputs = Array.AsReadOnly(outs);
        }
        public long CommandId { get; private set; }
        public string CommandCode { get; private set; }
        public string Name { get; private set; }
        public int SchemaVersion { get; private set; }
        public string Description { get; private set; }
        public string PermissionKey { get; private set; }
        /// <summary>0意味着继承Host默认超时，不代表无限等待。</summary>
        public int TimeoutMilliseconds { get; private set; }
        public IReadOnlyList<FDCCommandParameterDefinition> Inputs { get { return _inputs; } }
        public IReadOnlyList<FDCCommandParameterDefinition> Outputs { get { return _outputs; } }
        public IReadOnlyDictionary<string, object> BindInputs(IReadOnlyDictionary<string, object> values)
        { return Bind(_inputs, values, true); }
        public IReadOnlyDictionary<string, object> ValidateOutputs(IReadOnlyDictionary<string, object> values)
        { return Bind(_outputs, values, false); }
        private static IReadOnlyDictionary<string, object> Bind(
            IEnumerable<FDCCommandParameterDefinition> definitions,
            IReadOnlyDictionary<string, object> values, bool defaults)
        {
            FDCCommandParameterDefinition[] ds = definitions.ToArray();
            IReadOnlyDictionary<string, object> provided = values ??
                new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());
            Dictionary<string, object> result = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> pair in provided)
                if (!ds.Any(d => string.Equals(d.Code, pair.Key, StringComparison.Ordinal)))
                    throw new ArgumentException("未知参数: " + pair.Key);
            foreach (FDCCommandParameterDefinition d in ds)
            {
                object value;
                if (provided.TryGetValue(d.Code, out value))
                { d.ValidateValue(value); result.Add(d.Code, value); }
                else if (defaults && d.HasDefaultValue) result.Add(d.Code, d.DefaultValue);
                else if (d.Required) throw new ArgumentException("缺少必填参数: " + d.Code);
            }
            return new ReadOnlyDictionary<string, object>(result);
        }
    }
}
