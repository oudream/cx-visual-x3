using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceInterfaces
{
    public abstract class IOModule : Device
    {
        public IOModule(string name, DeviceConfig config) : base(config) { }
    }
}
