using CommonInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceInterfaces
{
    public static class MeasureDoubleConfig
    {
        [Name("Value"), ValueType(typeof(double)), Title("浮点值")]
        public static string Value = ConstHelper.VariableNameValue;
    }
}
