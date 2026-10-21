using CommonInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceInterfaces
{
    public static class MeasureIntegerConfig
    {
        [Name("Value"), ValueType(typeof(int)), Title("整型值")]
        public static string Value = ConstHelper.VariableNameValue;
    }
}
