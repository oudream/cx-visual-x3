using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceInterfaces.UIStructure
{
    public class DeviceVariableDetail
    {
        // 设备名
        public string DeviceName;
        // 变量名
        public string VariableName;
        // 变量标题
        public string VariableTitle;
        // 变量值
        public string Value;
        // 变量值类型名
        public string ValueTypeName;
        // 变量值枚举类型名
        public string ValueEnumTypeName;
        // 变量更新时间
        public long LastUpdated;
        public string LastUpdatedString;
        // 变量是否需要初始值
        public bool HasInitialValue;
    }
}
