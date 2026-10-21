using CommonInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceInterfaces
{
    public static class BitStatusProperty
    {
        [Name("InputStatus"), ValueType(typeof(int)), EnumType(typeof(BitStatus)), Title("当前输入状态")]
        public static string InputStatus = "InputStatus";

    }

}
