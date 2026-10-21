using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CxVisual.Platform.Channels
{
    public enum TcpClientModuleState
    {
        Stopping,
        Stopped,
        Started,    //启动但是还未连接
        Connected,  //已连接
    }
}
