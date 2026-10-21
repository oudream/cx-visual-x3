using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CommonInterfaces
{
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
    public class EnumTypeAttribute : Attribute
    {
        public Type Type { get; }

        public EnumTypeAttribute(Type type)
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


    [AttributeUsage(AttributeTargets.Field)]
    public class RemarkAttribute : Attribute
    {
        public string Remark { get; }

        public RemarkAttribute(string remark)
        {
            Remark = remark;
        }
    }



}
