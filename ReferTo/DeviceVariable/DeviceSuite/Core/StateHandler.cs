using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CxDeviceSuite.Core
{
    // 处理结果信息，写到状态机日志中
    public enum StateHandleResult
    {
        Success, // 处理成功，继续执行下一个状态
        Warning, // 处理成功，但产生警告，继续执行下一个状态
        Retry,   // 重试当前状态
        Failure, // 处理失败，停止当前工序
        Fatal,   // 致命错误，急停全部工序
    }

    public interface IStateHandler
    {
        StateHandleResult HandleState(int state);
    }

}
