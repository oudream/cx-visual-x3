using CxVisual.Platform.VariableObjects;
using S7.Net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CxVisual.Platform.Protocols.S7
{
    public class S7DriverModuleConfig : DriverModuleConfig
    {

        [Category("PLC"), DisplayName("IP地址")]
        public string PlcIp { get; set; }

        [Category("PLC"), DisplayName("CPU类型")]
        public CpuType CpuType { get; set; }

        [Category("PLC"), DisplayName("读取间隔")]
        public int ReadInterval { get; set; }

        [Category("PLC"), DisplayName("是否启动日志")]
        public bool LogEnabled { get; set; } = false;

        public S7DriverModuleConfig(long id, string name) : base(id, name)
        {
        }

        public S7DriverModuleConfig() : base(0, "")
        {

        }

    }

}
