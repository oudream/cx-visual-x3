using CommonInterfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DeviceInterfaces
{
    /// <summary>
    /// 外部设备种类
    /// </summary>
    public enum EquipType
    {
        控制卡,
        PLC,
    }

    /// <summary>
    /// 板卡品牌
    /// </summary>
    public enum MotionBrand
    {
        LeadShine,
        Googol,
        ZMotion,
    }

    /// <summary>
    /// 控制器连接方式
    /// </summary>
    public enum ConnectMode
    {
        EtherCat,
        Com,
        PCI,
    }

    /// <summary>
    /// 轴类型
    /// </summary>
    public enum AxisType
    {
        Servo,
        Stepping,
    }
    /// <summary>
    /// 轴单位
    /// </summary>
    public enum AxisUnit
    {
        mm,
        radian,
        angle,
    }

    /// <summary>
    /// 雷赛支持的型号
    /// </summary>
    public enum Model_LeadShine
    {
        DMC1000B,
        DMC1000S,
        DMC1220,
        DMC3K,
        DMC5K,
    }

    /// <summary>
    /// 固高支持的型号
    /// </summary>
    public enum Model_Googol
    {
        GVN,
        GSNE,
        GSN,
        GEN,
        GTS,
        GHN,
        GTN,
        GTN_Scan,
        GT,
        GE_S,
        GE_P,
        GE_Laser,
    }

}
