using CommonInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceInterfaces
{
    public static class MeasureLongConfig
    {
        [Name("Value"), ValueType(typeof(long)), Title("长整型值")]
        public static string Value = ConstHelper.VariableNameValue;
    }
}
