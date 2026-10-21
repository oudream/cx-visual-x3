using CommonInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceInterfaces
{
    public static class OutputProperty
    {
        [Name("OutputStatus"), ValueType(typeof(bool)), Title("当前输出状态")]
        public static string OutputStatus = "OutputStatus";

    }

}
