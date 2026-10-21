using CommonInterfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceInterfaces
{
    public class CylinderSingleBit : Device
    {
        public CylinderSingleBit(DeviceConfig config) : base(config)
        {
        }
    }

    public class CylinderDoubleBit : Device
    {
        public CylinderDoubleBit(DeviceConfig config) : base(config)
        {
        }
    }

    /// <summary>
    /// 气缸状态，激活，未激活
    /// </summary>
    public enum CylinderStatus { Activated, Deactivated }
   
}
