using CommonInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceInterfaces
{
    public static class MeasureStringConfig
    {
        [Name("Value"), ValueType(typeof(string)), Title("字符值")]
        public static string Value = ConstHelper.VariableNameValue;
    }
}
