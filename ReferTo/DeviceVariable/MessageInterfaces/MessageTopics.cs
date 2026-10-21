using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MessageInterfaces
{
    public static class MessageTopics
    {
        // 状态判定超时，协程发出，Error
        public const string StatusJudgementTimeout = "StatusJudgementTimeout";
        // 工序错误，协程发出，Error
        public const string ProcedureError = "ProcedureError";
        // 急停命令，用户发出，Command
        public const string EmergencyStopCommand = "EmergencyStopCommand";
        // 等首次变量，用户发出，Command
        public const string WaitVariableFirstValue = "WaitVariableFirstValue";
        // 复位工序，用户发出，Command
        public const string ResetProcedure = "ResetProcedure";
        // 从头开始，用户发出，Command
        public const string StartFromFirst = "StartFromFirst";
        // 继续开始，用户发出，Command
        public const string StartByContinue = "StartByContinue";
        // 协程完成，协程发出，Command
        public const string CoroutineComplete = "CoroutineComplete";
    }

}
