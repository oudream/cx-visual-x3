using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VariableInterfaces
{
    // Device Variable 就是 Device Dynamic Property
    // 用于存储设备类的某个动态属性描述
    // 设备类的某个动态属性描述，全局归档到 DeviceVariableAssociation -> DeviceVariableDescription
    public class DeviceVariableDescription
    {
        public string Name { get; }
        public Type Type { get; }
        public string TypeName => Type.Name;
        public string Title { get; }
        public Type EnumType{ get; }
        public string EnumTypeName => EnumType?.Name;
        public double MinValue { get; }
        public double MaxValue { get; }

        public DeviceVariableDescription(string name, Type type, double minValue, double maxValue, string title, Type enumType)
        {
            Name = name;
            Type = type;
            EnumType = enumType;
            Title = title;
            MinValue = minValue;
            MaxValue = maxValue;
        }

        public bool IsEnum
        {
            get
            {
                return EnumType != null;
            }
        }

        public string GetEnumName(int value)
        {
            if (EnumType == null)
            {
                return null;
            }
            return Enum.GetName(EnumType, value);
        }

    }

}
