using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CommonInterfaces
{
    public static class EnumHelper
    {
        // 获取枚举值的 Description
        public static string GetDescription(this Enum value)
        {
            FieldInfo field = value.GetType().GetField(value.ToString());
            DescriptionAttribute attribute = field?.GetCustomAttribute<DescriptionAttribute>();
            return attribute?.Description ?? value.ToString();
        }

        // 获取枚举类型本身的 Description
        public static string GetEnumDescription<T>() where T : Enum
        {
            Type type = typeof(T);
            DescriptionAttribute attribute = type.GetCustomAttribute<DescriptionAttribute>();
            return attribute?.Description ?? type.Name;
        }

    }
}
