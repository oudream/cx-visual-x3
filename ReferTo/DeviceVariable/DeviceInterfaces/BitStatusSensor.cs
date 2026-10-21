using CommonInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceInterfaces
{
    public class BitStatusSensor : Device
    {
        public BitStatusSensor(DeviceConfig config) : base(config)
        {

        }
    }

    /// <summary>
    /// Bit状态，On或Off
    /// </summary>
    public enum BitStatus { Off, On, None }

}
