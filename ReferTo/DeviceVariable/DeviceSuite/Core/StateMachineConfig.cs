using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;

namespace CxDeviceSuite.Core
{
    public class StateMachineConfig
    {
        public List<MachineConfig> Machines { get; set; }
    }

    public class MachineConfig
    {
        public string EnumType { get; set; }
        public List<StateConfig> States { get; set; }
    }

    public class StateConfig
    {
        public string State { get; set; }
        public List<VariableConfig> Variables { get; set; }
    }

    public class VariableConfig
    {
        public string Name { get; set; }
        // 其他变量判定器相关配置
        // ...
    }
}
