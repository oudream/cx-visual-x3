using CommonInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VariableInterfaces;

namespace DeviceInterfaces
{
    // VariableTable VariableInfo IVariableInfo 的壳
    public class VariableMeasure<TValue> : VariableMeasureCustom<TValue>
    {
        public VariableMeasure(string deviceName) : base(deviceName, ConstHelper.VariableNameValue)
        {
            TValue value;
            if (!VariableManager.TryGetVariableValue<TValue>(deviceName, VariableName, out value))
            {
                throw new InvalidOperationException($"设备[{deviceName}]的变量[{VariableName}]的类型[{ValueType}]不正确或找不到.");
            }
        }

    }

}
