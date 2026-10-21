using CommonInterfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceInterfaces
{
    public class AxisConfig: DeviceConfig
    {
        /// <summary>
        /// 轴速度参数
        /// </summary>
        [Category("配置"), Description("轴的速度与加速度设置")]
        [TypeConverter(typeof(ExpandableObjectConverter))] // 允许在 PropertyGrid 中展开
        public Profile AxProfile { get; set; }

        [Category("关联控制器"), Description("关联控制器")]
        public string ControllerName { get; set; }

        /// <summary>
        /// 轴类型
        /// </summary>
        [Category("配置"), Description("轴的类型，伺服或步进")]
        public AxisType AxisType { get; set; }
        /// <summary>
        /// 轴号
        /// </summary>
        [Category("配置"), Description("轴在板卡的轴号")]
        public int AxisNo { get; set; }

        /// <summary>
        /// 单位
        /// </summary>
        [Category("配置"), Description("轴的单位的类型")]
        public AxisUnit Unit { get; set; }
        /// <summary>
        /// 脉冲当量，单位：pulse/unit
        /// </summary>
        [Category("配置"), Description("脉冲当量，单位：pulse/unit")]
        public double Scale { get; set; }

        /// <summary>
        /// 硬件正限位开关对应的输入点编号
        /// </summary>
        [Category("配置"), Description("硬件正限位开关对应的输入点编号")]
        public int FwdIn { get; set; }
        /// <summary>
        /// 硬件负限位开关对应的输入点编号
        /// </summary>
        [Category("配置"), Description("硬件负限位开关对应的输入点编号")]
        public int RevIn { get; set; }
        /// <summary>
        /// 回零原点信号对应的输入点编号
        /// </summary>
        [Category("配置"), Description("回零原点信号对应的输入点编号")]
        public int DatumIn { get; set; }
        /// <summary>
        /// 轴报警信号对应的输入点编号
        /// </summary>
        [Category("配置"), Description("轴报警信号对应的输入点编号")]
        public int AlmIn { get; set; }


        public AxisConfig()
        {
            AxProfile = new Profile().GetDefaultProfile();
        }
    }

    public static class AxisProperty
    {
        [Name("CurrentPosition"), ValueType(typeof(double)), Title("当前脉冲位置")]
        public static string CurrentPosition = "CurrentPosition";

        [Name("CurrentEncoder"), ValueType(typeof(double)), Title("当前编码器位置")]
        public static string CurrentEncoder = "CurrentEncoder";

        [Name("HomeSensorStatus"), ValueType(typeof(bool)), Title("原点感应器状态")]
        public static string HomeSensorStatus = "HomeSensorStatus";

        [Name("PosLimitSensor"), ValueType(typeof(bool)), Title("正限位感应器状态")]
        public static string PosLimitSensor = "PosLimitSensor";

        [Name("NegLimitSensor"), ValueType(typeof(bool)), Title("负限位感应器状态")]
        public static string NegLimitSensor = "NegLimitSensor";

        [Name("ResetDone"), ValueType(typeof(bool)), Title("复位是否完成")]
        public static string ResetDone = "ResetDone";

        [Name("AxisError"), ValueType(typeof(bool)), Title("驱动器是否报警")]
        public static string AxisError = "AxisError";
    }
}
