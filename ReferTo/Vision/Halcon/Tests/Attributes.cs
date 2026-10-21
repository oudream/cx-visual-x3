using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HalconDriverModule.Tests
{
    // 在“元数据方法”上描述过程名、输入、输出、默认断言

    public enum HalconInvokeKind
    {
        HDev,
        CSharp,
        Auto
    }

    public enum HalconValueKind
    {
        Image, Region, Xld, Dict,
        TupleInt, TupleDouble, TupleString,
        Any
    }

    public enum HalconValueSource
    {
        CaseInput,       // 从case.inputs读取
        SharedContext,   // 从Runner共享上下文读取（例如 ConfigDict）
        Constant         // 固定值
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class HalconOpAttribute : Attribute
    {
        public string Procedure { get; }
        public HalconInvokeKind Invoke { get; set; } = HalconInvokeKind.Auto;
        public int TimeoutMs { get; set; } = 500;
        public string[] Tags { get; set; } = Array.Empty<string>();
        public HalconOpAttribute(string procedure) => Procedure = procedure;
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class HalconInAttribute : Attribute
    {
        public string Key { get; }
        // C# 方法参数名映射（不填则默认用 Key 匹配参数名）
        public string Param { get; set; }
        public HalconValueKind Kind { get; set; } = HalconValueKind.Any;
        public bool Required { get; set; } = true;
        public HalconValueSource Source { get; set; } = HalconValueSource.CaseInput;

        // Source=SharedContext 时使用
        public string ContextKey { get; set; }

        // Source=Constant 时使用
        public object Constant { get; set; }

        public HalconInAttribute(string key) => Key = key;
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class HalconOutAttribute : Attribute
    {
        public string Key { get; }
        // C# out/ref 参数名映射（不填则默认用 Key 匹配参数名）
        public string Param { get; set; }
        // 当 C# 方法没有显式 out 参数时，可从静态属性/方法导出
        public string ExportFromStaticProperty { get; set; }
        public string ExportFromStaticMethod { get; set; }
        public HalconValueKind Kind { get; set; } = HalconValueKind.Any;
        public bool Required { get; set; } = true;

        // 用哨兵值代替 nullable
        public double RangeMin { get; set; } = double.NaN;
        public double RangeMax { get; set; } = double.NaN;
        public int Length { get; set; } = -1;

        // string[] 可以
        public string[] OneOf { get; set; } = null;

        public HalconOutAttribute(string key) => Key = key;
    }
}
