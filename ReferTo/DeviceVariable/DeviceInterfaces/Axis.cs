using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;


namespace DeviceInterfaces
{
    // 

    public class Axis : Device
    {
        public AxisConfig AxisConfig { get; }
        public Axis(AxisConfig axisConfig) : base(axisConfig)
        {
            AxisConfig = axisConfig;
        }

        public void ToContext(Dictionary<string, string> context)
        {

        }

        public void FromContext(Dictionary<string, string> context)
        {

        }

    }


    public abstract class AxisMethod: DeviceMethod
    {
        protected AxisConfig _AxisConfig;

        /// <summary>
        /// 速度是否已设置
        /// </summary>
        public bool ProfileSetted { get { return _profileSetted; } }
        protected bool _profileSetted;

        public AxisMethod(AxisConfig axisConfig)
        {
            _AxisConfig = axisConfig;
        }
       
    }
}
