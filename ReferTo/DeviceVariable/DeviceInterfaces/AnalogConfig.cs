using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceInterfaces
{
    public class AnalogConfig : DeviceConfig
    {
        [Category("关联控制器"), Description("关联控制器")]
        public string ControllerName { get; set; }

        public AnalogConfig()
        {
        }
    }

}
