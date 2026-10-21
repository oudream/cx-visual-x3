using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VariableInterfaces;

namespace DeviceInterfaces
{
    // VariableTable VariableInfo IVariableInfo 的壳
    public class VariableMeasureCustom<TValue> : VariableMeasureBase
    {
        public Type ValueType { get; private set; }

        public VariableMeasureCustom(string deviceName, string variableName)
            : base(deviceName, variableName)
        {
            ValueType = typeof(TValue);
        }

        public TValue GetValue()
        {
            return VariableManager.GetVariableValue<TValue>(DeviceName, VariableName);
        }

        public (TValue, long) GetValue2()
        {
            return VariableManager.GetVariableValue2<TValue>(DeviceName, VariableName);
        }

        public (TValue, long, bool) GetValue3()
        {
            return VariableManager.GetVariableValue3<TValue>(DeviceName, VariableName);
        }

        public void SetValue(TValue value)
        {
            var table = VariableManager.FindVariableTable(DeviceName);
            if (table != null)
            {
                table.UpdateVariable(VariableName, value);
            }
        }

        public SendPointValueResult SendValue(DriverModuleBase driverModule, TValue value)
        {
            return driverModule.SendPointValue(DeviceName, VariableName, value);
        }

    }

}
