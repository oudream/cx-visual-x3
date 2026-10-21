using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VariableInterfaces
{
    public static class VariableEvaluatorManager
    {
        public const string EQ = "EQ";
        public const string GTE = "GTE";
        public const string LTE = "LTE";
        public const string ORE = "ORE";
        public const string CRE = "CRE";
        public const string LORC = "LORC";
        public const string ROLS = "ROLS";
        public const string CONTAINS = "CONTAINS";
        public const string STARTSWITH = "STARTSWITH";
        public const string ENDSWITH = "ENDSWITH";
        public const string EQUALS = "EQUALS";

        private static readonly Dictionary<string, Type> evaluatorMap = new Dictionary<string, Type>();

        static VariableEvaluatorManager()
        {
            // 注册所有已知的评估器
            RegisterEvaluator(EQ, typeof(EqualEvaluator<>));
            RegisterEvaluator(GTE, typeof(GreaterThanEvaluator<>));
            RegisterEvaluator(LTE, typeof(LessThanEvaluator<>));
            RegisterEvaluator(ORE, typeof(OpenRangeEvaluator<>));
            RegisterEvaluator(CRE, typeof(ClosedRangeEvaluator<>));
            RegisterEvaluator(LORC, typeof(LeftOpenRightClosedEvaluator<>));
            RegisterEvaluator(ROLS, typeof(RightOpenLeftClosedEvaluator<>));
            // 非泛型评估器
            RegisterEvaluator(CONTAINS, typeof(ContainsEvaluator));
            RegisterEvaluator(STARTSWITH, typeof(StartsWithEvaluator));
            RegisterEvaluator(ENDSWITH, typeof(EndsWithEvaluator));
            RegisterEvaluator(EQUALS, typeof(EqualsEvaluator));
        }

        private static void RegisterEvaluator(string name, Type evaluatorType)
        {
            evaluatorMap[name] = evaluatorType;
        }

        public static IVariableEvaluator<TValue> CreateEvaluatorCompare1<TValue>(string evaluatorName, TValue value) where TValue : IComparable
        {
            switch (evaluatorName)
            {
                case EQ: return new EqualEvaluator<TValue>(value);
                case GTE: return new GreaterThanEvaluator<TValue>(value);
                case LTE: return new LessThanEvaluator<TValue>(value);
                default: return null;
            }
        }

        public static IVariableEvaluator<TValue> CreateEvaluatorCompare2<TValue>(string evaluatorName, TValue value1, TValue value2) where TValue : IComparable
        {
            switch (evaluatorName)
            {
                case ORE: return new OpenRangeEvaluator<TValue>(value1, value2);
                case CRE: return new ClosedRangeEvaluator<TValue>(value1, value2);
                case LORC: return new LeftOpenRightClosedEvaluator<TValue>(value1, value2);
                case ROLS: return new RightOpenLeftClosedEvaluator<TValue>(value1, value2);
                default: return null;
            }
        }

        public static IVariableEvaluator<string> CreateEvaluatorString1(string evaluatorName, string value) 
        {
            switch (evaluatorName)
            {
                case CONTAINS: return new ContainsEvaluator(value, false);
                case STARTSWITH: return new StartsWithEvaluator(value, false);
                case ENDSWITH: return new EndsWithEvaluator(value, false);
                case EQUALS: return new EqualsEvaluator(value, false);
                default: return null;
            }
        }
    }


    public interface IVariableEvaluator<T>
    {
        bool Evaluate(T value);
    }


    // 判定指定的数值是否等于预设值
    public class EqualEvaluator<T> : IVariableEvaluator<T> where T : IComparable
    {
        private T expectedValue;

        public EqualEvaluator(T expectedValue)
        {
            this.expectedValue = expectedValue;
        }

        public bool Evaluate(T value)
        {
            return value.CompareTo(expectedValue) == 0;
        }
    }

    // 判定指定的数值是否大于预设值
    public class GreaterThanEvaluator<T> : IVariableEvaluator<T> where T : IComparable
    {
        private T threshold;

        public GreaterThanEvaluator(T threshold)
        {
            this.threshold = threshold;
        }

        public bool Evaluate(T value)
        {
            return value.CompareTo(threshold) > 0;
        }
    }

    // 判定指定的数值是否小于预设值
    public class LessThanEvaluator<T> : IVariableEvaluator<T> where T : IComparable
    {
        private T threshold;

        public LessThanEvaluator(T threshold)
        {
            this.threshold = threshold;
        }

        public bool Evaluate(T value)
        {
            return value.CompareTo(threshold) < 0;
        }
    }

    // 判定指定的数值是否在开区间 (lowerBound, upperBound) 内
    public class OpenRangeEvaluator<T> : IVariableEvaluator<T> where T : IComparable
    {
        private T lowerBound;
        private T upperBound;

        public OpenRangeEvaluator(T lowerBound, T upperBound)
        {
            this.lowerBound = lowerBound;
            this.upperBound = upperBound;
        }

        public bool Evaluate(T value)
        {
            return value.CompareTo(lowerBound) > 0 && value.CompareTo(upperBound) < 0;
        }
    }

    // 判定指定的数值是否在闭区间 [lowerBound, upperBound] 内
    public class ClosedRangeEvaluator<T> : IVariableEvaluator<T> where T : IComparable
    {
        private T lowerBound;
        private T upperBound;

        public ClosedRangeEvaluator(T lowerBound, T upperBound)
        {
            this.lowerBound = lowerBound;
            this.upperBound = upperBound;
        }

        public bool Evaluate(T value)
        {
            return value.CompareTo(lowerBound) >= 0 && value.CompareTo(upperBound) <= 0;
        }
    }

    // 判定指定的数值是否在半开闭区间 (lowerBound, upperBound] 内
    public class LeftOpenRightClosedEvaluator<T> : IVariableEvaluator<T> where T : IComparable
    {
        private T lowerBound;
        private T upperBound;

        public LeftOpenRightClosedEvaluator(T lowerBound, T upperBound)
        {
            this.lowerBound = lowerBound;
            this.upperBound = upperBound;
        }

        public bool Evaluate(T value)
        {
            return value.CompareTo(lowerBound) > 0 && value.CompareTo(upperBound) <= 0;
        }
    }

    // 判定指定的数值是否在半闭开区间 [lowerBound, upperBound) 内
    public class RightOpenLeftClosedEvaluator<T> : IVariableEvaluator<T> where T : IComparable
    {
        private T lowerBound;
        private T upperBound;

        public RightOpenLeftClosedEvaluator(T lowerBound, T upperBound)
        {
            this.lowerBound = lowerBound;
            this.upperBound = upperBound;
        }

        public bool Evaluate(T value)
        {
            return value.CompareTo(lowerBound) >= 0 && value.CompareTo(upperBound) < 0;
        }
    }


    // 判定指定的字符串是否包含预设子串（可选择是否忽略大小写）
    public class ContainsEvaluator : IVariableEvaluator<string>
    {
        private string substring;
        private StringComparison comparisonType;

        public ContainsEvaluator(string substring, bool ignoreCase)
        {
            this.substring = substring;
            this.comparisonType = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        }

        public bool Evaluate(string value)
        {
            return value.IndexOf(substring, comparisonType) >= 0;
        }
    }

    // 判定指定的字符串是否以预设子串开头（可选择是否忽略大小写）
    public class StartsWithEvaluator : IVariableEvaluator<string>
    {
        private string substring;
        private StringComparison comparisonType;

        public StartsWithEvaluator(string substring, bool ignoreCase)
        {
            this.substring = substring;
            this.comparisonType = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        }

        public bool Evaluate(string value)
        {
            return value.StartsWith(substring, comparisonType);
        }
    }

    // 判定指定的字符串是否以预设子串结尾（可选择是否忽略大小写）
    public class EndsWithEvaluator : IVariableEvaluator<string>
    {
        private string substring;
        private StringComparison comparisonType;

        public EndsWithEvaluator(string substring, bool ignoreCase)
        {
            this.substring = substring;
            this.comparisonType = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        }

        public bool Evaluate(string value)
        {
            return value.EndsWith(substring, comparisonType);
        }
    }

    // 判定指定的字符串是否与预设字符串全等（可选择是否忽略大小写）
    public class EqualsEvaluator : IVariableEvaluator<string>
    {
        private string comparisonString;
        private StringComparison comparisonType;

        public EqualsEvaluator(string comparisonString, bool ignoreCase)
        {
            this.comparisonString = comparisonString;
            this.comparisonType = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        }

        public bool Evaluate(string value)
        {
            return value.Equals(comparisonString, comparisonType);
        }
    }

}
