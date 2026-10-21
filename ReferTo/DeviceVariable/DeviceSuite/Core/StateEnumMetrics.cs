using CommonInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CxDeviceSuite.Core
{
    // 枚举必须要有 Start End

    // 枚举信息类
    public class StateEnumItem
    {
        public string Title { get; set; }
        public string Name { get; set; }
        public int Value { get; set; }
    }

    // 枚举机信息的收集状态
    public class StateEnumMetrics
    {
        public static (bool, string) CheckEnumType(Type enumType)
        {
            try
            {
                DoCheckEnumType(enumType);
                return (true, null);
            }
            catch (Exception e)
            {
                return (false, e.Message);
            }
        }

        private static void DoCheckEnumType(Type enumType)
        {
            if (!enumType.IsEnum)
            {
                throw new ArgumentException("参数必须是枚举类型");
            }
            var enumNames = Enum.GetNames(enumType);
            if (enumNames.Length < 2)
            {
                throw new ArgumentException("枚举至少需要两个值");
            }
            if (enumNames[0] != "Start" || enumNames[enumNames.Length - 1] != "End")
            {
                throw new ArgumentException("枚举必须以Start开始，以End结束");
            }
        }

        public Type EnumType { get; private set; }
        public List<StateEnumItem> Items { get; private set; }

        public StateEnumItem StartItem { get; private set; }
        public StateEnumItem EndItem { get; private set; }

        // 检测 enumType 是否以 Start 作为第一个枚举值，并以 End 作为最后一个枚举值
        public StateEnumMetrics(Type enumType)
        {
            DoCheckEnumType(enumType);
            EnumType = enumType;
            Items = new List<StateEnumItem>();
            var enumValues = Enum.GetValues(enumType);
            foreach (var value in enumValues)
            {
                var memInfo = enumType.GetMember(value.ToString());
                var attributes = memInfo[0].GetCustomAttributes(typeof(TitleAttribute), false);
                string title = attributes.Length > 0 ? ((TitleAttribute)attributes[0]).Title : "";

                Items.Add(new StateEnumItem
                {
                    Title = title,
                    Name = value.ToString(),
                    Value = (int)value
                });
            }
            StartItem = Items[0];
            EndItem = Items[Items.Count - 1];
        }

        // 根据名称获取枚举信息
        public StateEnumItem GetEnumInfoByName(string name)
        {
            return Items.Find(e => e.Name == name);
        }

        // 根据值获取枚举信息
        public StateEnumItem GetEnumInfoByValue(int value)
        {
            return Items.Find(e => e.Value == value);
        }

        public bool IsStart(int value)
        {
            return value == StartItem.Value;
        }

        public bool IsEnd(int value)
        {
            return value == EndItem.Value;
        }

    }


}
