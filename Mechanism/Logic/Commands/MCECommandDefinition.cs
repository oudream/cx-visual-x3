using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CxVisual.Foundation.Data.Types;
namespace CxVisual.Mechanism.Logic.Commands
{
    /// <summary>IoT风格的输入/输出参数契约：类型、必填、默认、单位与范围。</summary>
    public sealed class MCECommandParameterDefinition
    {
        public MCECommandParameterDefinition(string code, Type dataType, bool required,
            bool hasDefaultValue = false, object defaultValue = null,
            string unit = null, string description = null,
            object minValue = null, object maxValue = null)
        {
            CheckCode(code);
            if (dataType == null || dataType == typeof(void) || dataType.IsPointer ||
                dataType.ContainsGenericParameters) throw new ArgumentException("无效参数类型", "dataType");
            if (required && hasDefaultValue) throw new ArgumentException("必填参数不可指定默认值");
            if (unit != null && unit.Length > 32) throw new ArgumentException("单位过长", "unit");
            Code = code; DataType = dataType; TypeDescriptor = FDTDataTypeDescriptor.Describe(dataType); Required = required; HasDefaultValue = hasDefaultValue;
            Unit = unit; Description = description ?? string.Empty;
            MinValue = minValue; MaxValue = maxValue;
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
                if (Required || (DataType.IsValueType && Nullable.GetUnderlyingType(DataType) == null))
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
            if (MinValue != null && Compare(value, MinValue) < 0)
                throw new ArgumentOutOfRangeException(Code, "参数小于下限");
            if (MaxValue != null && Compare(value, MaxValue) > 0)
                throw new ArgumentOutOfRangeException(Code, "参数大于上限");
        }
    }
    /// <summary>命令静态定义，不保存执行者、RequestId或MRK Operation状态。</summary>
    public sealed class MCECommandDefinition
    {
        private readonly ReadOnlyCollection<MCECommandParameterDefinition> _inputs;
        private readonly ReadOnlyCollection<MCECommandParameterDefinition> _outputs;
        public MCECommandDefinition(long id, string code, string name,
            IEnumerable<MCECommandParameterDefinition> inputs,
            IEnumerable<MCECommandParameterDefinition> outputs, int schemaVersion = 1,
            string description = null, string permissionKey = null, int timeoutMilliseconds = 0)
        {
            if (id <= 0) throw new ArgumentOutOfRangeException("id");
            MCECommandParameterDefinition.CheckCode(code);
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("显示名无效", "name");
            if (schemaVersion < 1) throw new ArgumentOutOfRangeException("schemaVersion");
            if (timeoutMilliseconds < 0) throw new ArgumentOutOfRangeException("timeoutMilliseconds");
            MCECommandParameterDefinition[] ins = (inputs ?? Enumerable.Empty<MCECommandParameterDefinition>()).ToArray();
            MCECommandParameterDefinition[] outs = (outputs ?? Enumerable.Empty<MCECommandParameterDefinition>()).ToArray();
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
        public IReadOnlyList<MCECommandParameterDefinition> Inputs { get { return _inputs; } }
        public IReadOnlyList<MCECommandParameterDefinition> Outputs { get { return _outputs; } }
        public IReadOnlyDictionary<string, object> BindInputs(IReadOnlyDictionary<string, object> values)
        { return Bind(_inputs, values, true); }
        public IReadOnlyDictionary<string, object> ValidateOutputs(IReadOnlyDictionary<string, object> values)
        { return Bind(_outputs, values, false); }
        private static IReadOnlyDictionary<string, object> Bind(
            IEnumerable<MCECommandParameterDefinition> definitions,
            IReadOnlyDictionary<string, object> values, bool defaults)
        {
            MCECommandParameterDefinition[] ds = definitions.ToArray();
            IReadOnlyDictionary<string, object> provided = values ??
                new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());
            Dictionary<string, object> result = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> pair in provided)
                if (!ds.Any(d => string.Equals(d.Code, pair.Key, StringComparison.Ordinal)))
                    throw new ArgumentException("未知参数: " + pair.Key);
            foreach (MCECommandParameterDefinition d in ds)
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
