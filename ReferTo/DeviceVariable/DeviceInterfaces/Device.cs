using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VariableInterfaces;

namespace DeviceInterfaces
{
    public class Device
    {
        private VariableTable variableTable;

        [Browsable(false)] // DataGridView 不显示
        public virtual DeviceConfig Config { get; protected set; }

        [DisplayName("设备名")] // DataGridView 显示名
        public string Name { get; private set; }

        [DisplayName("设备类型名")] // DataGridView 显示名
        public string DeviceTypeName { get; private set; }

        public Device(DeviceConfig config)
        {
            Name = config.Name;
            DeviceTypeName = config.DeviceTypeName;
            Config = config;
        }

        public void AssignVariableTable(VariableTable table)
        {
            variableTable = table;
        }

        public VariableTable GetVariableTable()
        {
            return variableTable;
        }

        public void SetAttribute<TValue>(string attributeName, TValue value)
        {
            variableTable.UpdateVariable(attributeName, value);
        }

        public T GetAttribute<T>(string attributeName)
        {
            return variableTable.GetVariable<T>(attributeName);
        }

        // 取变量值、最后更新时间
        public (T, long) GetAttribute2<T>(string attributeName)
        {
            return variableTable.GetVariable2<T>(attributeName);
        }

        // 取变量值、最后更新时间、是否要初始值
        public (T, long, bool) GetAttribute3<T>(string attributeName)
        {
            return variableTable.GetVariable3<T>(attributeName);
        }

    }

    public class DeviceConfig
    {
        [Category("基础配置"), Description("设备名称")]
        public string Name { get; set; }


        [Category("基础配置"), Description("设备类型名")]
        public string DeviceTypeName { get; set; }

    }

}
