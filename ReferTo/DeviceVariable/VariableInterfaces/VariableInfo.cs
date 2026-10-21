using CommonInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VariableInterfaces
{
    public interface IVariableInfo
    {
        bool HasInitialValue { get; }
        long LastUpdated { get; }
    }

    public class VariableInfo<TValue> : IVariableInfo
    {
        // 变量值，最好是值类型
        public TValue Value { get; set; }
        // 变量更新时间
        public long LastUpdated { get; set; }
        // 变量是否需要初始值
        public bool HasInitialValue { get; set; }
        // 继承
        bool IVariableInfo.HasInitialValue => this.HasInitialValue;
        long IVariableInfo.LastUpdated => this.LastUpdated;
        //
        public VariableInfo(TValue value)
        {
            Value = value;
            HasInitialValue = true;
        }
        public VariableInfo(TValue value, long lastUpdated) : this(value)
        {
            LastUpdated = lastUpdated;
        }
        public VariableInfo(TValue value, bool hasInitialValue) : this(value)
        {
            HasInitialValue = hasInitialValue;
        }
        public VariableInfo(TValue value, long lastUpdated, bool hasInitialValue) : this(value)
        {
            LastUpdated = lastUpdated;
            HasInitialValue = hasInitialValue;
        }
    }
    
}
