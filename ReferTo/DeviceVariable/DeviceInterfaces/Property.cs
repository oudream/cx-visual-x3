using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DeviceInterfaces
{
    /// <summary>
    /// 属性描述类
    /// </summary>
    public class PropertyDescription
    {
        public string Name { get; }
        public Type Type { get; }
        public double MinValue { get; }
        public double MaxValue { get; }
        public string Title { get; }

        public PropertyDescription(string name, Type type, double minValue, double maxValue, string title)
        {
            Name = name;
            Type = type;
            MinValue = minValue;
            MaxValue = maxValue;
            Title = title;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class NameAttribute : Attribute
    {
        public string Name { get; }

        public NameAttribute(string name)
        {
            Name = name;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class ValueTypeAttribute : Attribute
    {
        public Type Type { get; }

        public ValueTypeAttribute(Type type)
        {
            Type = type;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class MaxLengthAttribute : Attribute
    {
        public int MaxLength { get; }

        public MaxLengthAttribute(int maxLength)
        {
            MaxLength = maxLength;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class RangeAttribute : Attribute
    {
        public double MinValue { get; }
        public double MaxValue { get; }

        public RangeAttribute(double minValue, double maxValue)
        {
            MinValue = minValue;
            MaxValue = maxValue;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class TitleAttribute : Attribute
    {
        public string Title { get; }

        public TitleAttribute(string title)
        {
            Title = title;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class EditableAttribute : Attribute
    {
        public bool IsEditable { get; set; }

        public EditableAttribute(bool isEditable)
        {
            IsEditable = isEditable;
        }
    }


    public static class DevicePropertyHelper
    {
        public static IEnumerable<FieldInfo> GetPropertyInfo<T>()
        {
            return typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static)
                            .Where(field => field.IsLiteral && !field.IsInitOnly);
        }

        public static void DisplayPropertyInfo<T>()
        {
            foreach (var property in GetPropertyInfo<T>())
            {
                var nameAttr = property.GetCustomAttribute<NameAttribute>();
                var valueTypeAttr = property.GetCustomAttribute<ValueTypeAttribute>();
                var maxLengthAttr = property.GetCustomAttribute<MaxLengthAttribute>();
                var titleAttr = property.GetCustomAttribute<TitleAttribute>();

                Console.WriteLine($"Property: {property.Name}, Name: {nameAttr?.Name}, Type: {valueTypeAttr?.Type}, MaxLength: {maxLengthAttr?.MaxLength}, Title: {titleAttr?.Title}");
            }
        }

        public static IEnumerable<PropertyDescription> CreatePropertyDescriptions(Type propertyType)
        {
            var properties = propertyType.GetProperties(BindingFlags.Public);

            foreach (var property in properties)
            {
                var nameAttr = property.GetCustomAttribute<NameAttribute>();
                var valueTypeAttr = property.GetCustomAttribute<ValueTypeAttribute>();
                var rangeAttr = property.GetCustomAttribute<RangeAttribute>();
                var titleAttr = property.GetCustomAttribute<TitleAttribute>();
                if (nameAttr != null)
                {
                    yield return new PropertyDescription(
                    name: nameAttr.Name,
                    type: valueTypeAttr?.Type ?? typeof(object),
                    minValue: rangeAttr?.MinValue ?? double.MinValue,
                    maxValue: rangeAttr?.MaxValue ?? double.MaxValue,
                    title: titleAttr?.Title ?? nameAttr.Name
                    );
                }

            }
        }
    }


}
