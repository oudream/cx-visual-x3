using CommonInterfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceInterfaces
{
    public class VectorConfig : DeviceConfig
    {
        [Category("配置")]
        [Description("坐标系")]
        public string CoordName { get; set; }

        [Category("配置")]
        [Description("点位坐标列表")]
        public List<double> PositionList { get; set; }

        public VectorConfig(string coordName)
        {
            CoordName = coordName;
            PositionList = new List<double>();
        }

        public VectorConfig()
        {
            CoordName = "";
            PositionList = new List<double>();
        }

    }

    public static class VectorProperty
    {
        [Name("CoordinatePosition"), ValueType(typeof(double)), Range(0, 100), Title("点位在空间中的确切坐标")]
        public static string CoordinatePosition = "CoordinatePosition";

        [Name("ReachStatus"), ValueType(typeof(PointStatus)), Title("是否已经到达或离开特定点位")]
        public static string ReachStatus = "ReachStatus";

        [Name("MotionTrajectory"), ValueType(typeof(string)), Title("点位移动的轨迹或路径")]
        public static string MotionTrajectory = "MotionTrajectory";

        [Name("MotionTrajectory"), ValueType(typeof(double)), Title("动态点位坐标1")]
        public static string DynPosition1 = "DynPosition1";

        [Name("MotionTrajectory"), ValueType(typeof(double)), Title("动态点位坐标2")]
        public static string DynPosition2 = "DynPosition2";

        [Name("MotionTrajectory"), ValueType(typeof(double)), Title("动态点位坐标3")]
        public static string DynPosition3 = "DynPosition3";

        [Name("MotionTrajectory"), ValueType(typeof(double)), Title("动态点位坐标4")]
        public static string DynPosition4 = "DynPosition4";

        [Name("MotionTrajectory"), ValueType(typeof(double)), Title("动态点位坐标5")]
        public static string DynPosition5 = "DynPosition5";

        [Name("MotionTrajectory"), ValueType(typeof(double)), Title("动态点位坐标6")]
        public static string DynPosition6 = "DynPosition6";

    }

}
