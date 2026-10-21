using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceInterfaces
{
    public class Analog : Device
    {
        public Analog(string name, AnalogConfig config) : base(config) { }
    }

    public static class AnalogProperty
    {

    }

}
