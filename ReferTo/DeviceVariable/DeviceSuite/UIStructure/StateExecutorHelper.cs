using CxDeviceSuite.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CxDeviceSuite.UIStructure
{
    public class StateExecutorHelper
    {
        public static List<StateExecutorDetail> ExportStateExecutorList(StateMachine stateMachine)
        {
            var result = new List<StateExecutorDetail>();
            for (int i = 0; i < stateMachine.ExecutorMetrics.GetExecutionCount(); i++)
            {
                var executor = stateMachine.ExecutorMetrics.GetExecutionItem(i);
                var executorDetail = new StateExecutorDetail();
                {
                    var metrics = stateMachine.EnumMetrics.GetEnumInfoByValue(executor.State);
                    executorDetail.StateName = metrics?.Name;
                    executorDetail.StateTitle = metrics?.Title;
                }
                executorDetail.State = executor.State;
                executorDetail.ShouldExecute = executor.ShouldExecute;
                executorDetail.RetryTimes = executor.RetryTimes;
                executorDetail.DelayBefore = executor.DelayBefore;
                executorDetail.DelayAfter = executor.DelayAfter;
                executorDetail.Timeout = executor.Timeout;
                result.Add(executorDetail);
            }

            return result;
        }
    }
}
