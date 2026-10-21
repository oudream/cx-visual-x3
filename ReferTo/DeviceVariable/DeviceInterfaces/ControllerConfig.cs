using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceInterfaces
{
    public static class ControllerProperty
    {

    }

    public class ControllerConfig : DeviceConfig
    {
        [Category("控制器配置"), Description("控制器类型")]
        public EquipType EquipType { get; set; }
        [Category("控制器配置"), Description("控制器品牌")]
        public MotionBrand MotionBrand { get; set; }
        [Category("控制器配置"), Description("控制器ID")]
        public int CardID { get; set; }

        [Category("控制器配置"), Description("是否加载参数文件")]
        public bool LoadParamFlag { get; set; }
        [Category("控制器配置"), Description("参数文件路径与名称")]
        public string ParamFilePath { get; set; }
        [Category("控制器配置"), Description("IP地址")]
        public string IP { get; set; }
        [Category("控制器配置"), Description("串口编号")]
        public int Com { get; set; }
        [Category("控制器配置"), Description("控制器连接方式")]
        public ConnectMode ConnectMode { get; set; }

    }

}
