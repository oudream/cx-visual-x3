using CommonInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceInterfaces
{

    public static class CylinderSingleBitProperty
    {
        [Name("CylStatus"), ValueType(typeof(CylinderStatus)), Title("指示气缸是否被激活或处于特定位置")]
        public static string CylStatus = "CylStatus";

        [Name("CylInputStatus"), ValueType(typeof(int)), EnumType(typeof(BitStatus)), Title("气缸输入状态")]
        public static string CylInputStatus = "CylInputStatus";

        [Name("CylOutputStatus"), ValueType(typeof(int)), EnumType(typeof(BitStatus)), Title("气缸输出状态")]
        public static string CylOutputStatus = "CylOutputStatus";

    }

    public static class CylinderDoubleBitProperty
    {
        [Name("CylStatus2"), ValueType(typeof(CylinderStatus)), Title("指示气缸是否被激活或处于特定位置")]
        public static string CylStatus2 = "CylStatus2";

        [Name("CylInputStatus1"), ValueType(typeof(int)), EnumType(typeof(BitStatus)), Title("气缸输入1状态")]
        public static string CylInputStatus1 = "CylInputStatus1";

        [Name("CylInputStatus2"), ValueType(typeof(int)), EnumType(typeof(BitStatus)), Title("气缸输入2状态")]
        public static string CylInputStatus2 = "CylInputStatus2";

        [Name("CylOutputStatus1"), ValueType(typeof(int)), EnumType(typeof(BitStatus)), Title("气缸输出1状态")]
        public static string CylOutputStatus1 = "CylOutputStatus1";

        [Name("CylOutputStatus2"), ValueType(typeof(int)), EnumType(typeof(BitStatus)), Title("气缸输出1状态")]
        public static string CylOutputStatus2 = "CylOutputStatus2";
    }

}
