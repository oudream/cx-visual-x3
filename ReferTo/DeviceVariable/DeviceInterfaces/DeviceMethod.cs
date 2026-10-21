using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceInterfaces
{
    public abstract class DeviceMethod
    {
        public bool DryRun { get { return _dryRun; } }
        protected bool _dryRun;
        public void SetDryRun(bool value)
        {
            _dryRun = value;
        }
    }
}
