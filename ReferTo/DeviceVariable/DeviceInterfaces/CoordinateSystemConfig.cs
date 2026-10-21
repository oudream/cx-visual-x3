using CommonInterfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceInterfaces
{
    public class CoordinateSystemConfig : DeviceConfig
    {
        [Category("配置")]
        [Description("坐标系的轴列表")]
        public List<AxisConfig> AxisesList { get; set; }

    }

    public static class CoordinateSystemProperty
    {
        [Name("Status"), ValueType(typeof(bool)), Title("指示坐标系中所有轴是否正在运行")]
        public static string Status = "Status";

    }

}
