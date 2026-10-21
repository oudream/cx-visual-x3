using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Core.Tokens;

namespace DeviceInterfaces
{
    public class VariableMeasureBase
    {
        public string DeviceName { get; private set; }

        public string VariableName { get; private set; }

        public VariableMeasureBase(string deviceName, string variableName)
        {
            DeviceName = deviceName;
            VariableName = variableName;
        }

    }
}
