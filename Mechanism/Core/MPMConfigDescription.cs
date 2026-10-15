using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace CxVisual.Mechanism.Core
{
    /// <summary>M04：普通强类型C#配置属性的可选展示与校验描述；不绑定WPF。</summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class MPMConfigItemAttribute : Attribute
    {
        public MPMConfigItemAttribute(string title) { Title = title; }
        public string Title { get; private set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string Unit { get; set; }
        public bool Required { get; set; }
        public double Min { get; set; } = double.NegativeInfinity;
        public double Max { get; set; } = double.PositiveInfinity;
    }

    /// <summary>配置属性元数据。PropertyInfo供公共编辑器/校验器使用，非持久化DTO。</summary>
    public sealed class MPMConfigProperty
    {
        public string Name { get; internal set; }
        public string Title { get; internal set; }
        public string Category { get; internal set; }
        public string Description { get; internal set; }
        public string Unit { get; internal set; }
        public double Minimum { get; internal set; }
        public double Maximum { get; internal set; }
        public bool Required { get; internal set; }
        public Type PropertyType { get; internal set; }
        public PropertyInfo Property { get; internal set; }
    }

    public static class MPMConfigDescription
    {
        public static MPMConfigProperty[] Describe<T>() { return Describe(typeof(T)); }
        public static MPMConfigProperty[] Describe(Type type)
        {
            if (type == null) throw new ArgumentNullException("type");
            List<MPMConfigProperty> values = new List<MPMConfigProperty>();
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanRead || !property.CanWrite ||
                    property.GetIndexParameters().Length != 0 ||
                    property.GetGetMethod() == null || property.GetSetMethod() == null)
                    continue;
                BrowsableAttribute browsable = property.GetCustomAttribute<BrowsableAttribute>();
                if (browsable != null && !browsable.Browsable) continue;
                MPMConfigItemAttribute a = property.GetCustomAttribute<MPMConfigItemAttribute>();
                values.Add(new MPMConfigProperty
                {
                    Name = property.Name,
                    Title = a != null && !string.IsNullOrEmpty(a.Title) ? a.Title : property.Name,
                    Category = a != null && !string.IsNullOrEmpty(a.Category) ? a.Category : "通用",
                    Description = a == null ? string.Empty : a.Description ?? string.Empty,
                    Unit = a == null ? string.Empty : a.Unit ?? string.Empty,
                    Minimum = a == null ? double.NegativeInfinity : a.Min,
                    Maximum = a == null ? double.PositiveInfinity : a.Max,
                    Required = a != null && a.Required,
                    PropertyType = property.PropertyType,
                    Property = property
                });
            }
            return values.ToArray();
        }

        /// <summary>返回全部属性错误；专业可通过Register提供跨字段Validator。</summary>
        public static string Validate<T>(T value)
        {
            if (value == null) return "Configuration is null";
            foreach (MPMConfigProperty property in Describe(typeof(T)))
            {
                object v = property.Property.GetValue(value, null);
                if (property.Required && (v == null || (v is string && string.IsNullOrWhiteSpace((string)v))))
                    return property.Title + " is required";
                if (v == null) continue;
                Type numericType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                if (!IsNumber(numericType)) continue;
                double number = Convert.ToDouble(v, CultureInfo.InvariantCulture);
                if (double.IsNaN(number) || double.IsInfinity(number) ||
                    number < property.Minimum || number > property.Maximum)
                    return property.Title + " must be between " + property.Minimum + " and " + property.Maximum;
            }
            return null;
        }

        private static bool IsNumber(Type type)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte: case TypeCode.SByte:
                case TypeCode.Int16: case TypeCode.Int32: case TypeCode.Int64:
                case TypeCode.UInt16: case TypeCode.UInt32: case TypeCode.UInt64:
                case TypeCode.Single: case TypeCode.Double: case TypeCode.Decimal:
                    return true;
                default: return false;
            }
        }
    }
}
