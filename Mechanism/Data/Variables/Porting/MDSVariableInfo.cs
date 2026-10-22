// PORTING REVIEW: ReferTo C# source copied and type-renamed; NOT COMPILED.
// Source: ReferTo/DeviceVariable/VariableInterfaces/VariableInfo.cs; remove old static/global deps before direct integration. No Adapter.
using CommonInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Mechanism.Data.Variables.Porting
{
    public interface IMDSVariableInfo
    {
        bool HasInitialValue { get; }
        long LastUpdated { get; }
    }

    public class MDSVariableInfo<TValue> : IMDSVariableInfo
    {
        // 变量值，最好是值类型
        public TValue Value { get; set; }
        // 变量更新时间
        public long LastUpdated { get; set; }
        // 变量是否需要初始值
        public bool HasInitialValue { get; set; }
        // 继承
        bool IMDSVariableInfo.HasInitialValue => this.HasInitialValue;
        long IMDSVariableInfo.LastUpdated => this.LastUpdated;
        //
        public MDSVariableInfo(TValue value)
        {
            Value = value;
            HasInitialValue = true;
        }
        public MDSVariableInfo(TValue value, long lastUpdated) : this(value)
        {
            LastUpdated = lastUpdated;
        }
        public MDSVariableInfo(TValue value, bool hasInitialValue) : this(value)
        {
            HasInitialValue = hasInitialValue;
        }
        public MDSVariableInfo(TValue value, long lastUpdated, bool hasInitialValue) : this(value)
        {
            LastUpdated = lastUpdated;
            HasInitialValue = hasInitialValue;
        }
    }
    
}
