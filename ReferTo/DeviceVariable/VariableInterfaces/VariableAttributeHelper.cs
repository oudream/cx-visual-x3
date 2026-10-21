using CommonInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace VariableInterfaces
{

    public static class VariableAttributeHelper
    {
        public static IEnumerable<FieldInfo> GetAttributeInfo<T>()
        {
            return typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static)
                            .Where(field => field.IsLiteral && !field.IsInitOnly);
        }

        public static void DisplayVariableInfo<T>()
        {
            foreach (var attr in GetAttributeInfo<T>())
            {
                var nameAttr = attr.GetCustomAttribute<NameAttribute>();
                var valueTypeAttr = attr.GetCustomAttribute<ValueTypeAttribute>();
                var maxLengthAttr = attr.GetCustomAttribute<MaxLengthAttribute>();
                var titleAttr = attr.GetCustomAttribute<TitleAttribute>();

                Console.WriteLine($"Variable: {attr.Name}, Name: {nameAttr?.Name}, Type: {valueTypeAttr?.Type}, MaxLength: {maxLengthAttr?.MaxLength}, Title: {titleAttr?.Title}");
            }
        }

        public static IEnumerable<DeviceVariableDescription> CreateVariableDescriptions(Type propertyType)
        {
            var fields = propertyType.GetFields(BindingFlags.Public | BindingFlags.Static);

            foreach (var field in fields)
            {
                var nameAttr = field.GetCustomAttribute<NameAttribute>();
                var valueTypeAttr = field.GetCustomAttribute<ValueTypeAttribute>();
                var rangeAttr = field.GetCustomAttribute<RangeAttribute>();
                var titleAttr = field.GetCustomAttribute<TitleAttribute>();
                var enumTypeNameAttr = field.GetCustomAttribute<EnumTypeAttribute>();

                yield return new DeviceVariableDescription(
                    name: field.Name,
                    type: valueTypeAttr?.Type ?? typeof(object),
                    minValue: rangeAttr?.MinValue ?? 0, // double.MinValue,
                    maxValue: rangeAttr?.MaxValue ?? 0, // double.MaxValue,
                    title: titleAttr?.Title ?? field.Name,
                    enumType: enumTypeNameAttr?.Type ?? null
                );
            }
        }
    }
}
