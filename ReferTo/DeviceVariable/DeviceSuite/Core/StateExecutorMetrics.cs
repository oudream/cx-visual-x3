using CommonInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CxDeviceSuite.Core
{
    // 这个类的主要职责是收集和管理与状态执行相关的度量和统计信息。这个名称清晰地表达了类的核心功能和目的，即跟踪和记录状态机中各个状态的执行表现和相关数据
    public class StateExecutorMetrics
    {
        public static StateExecutorMetrics CreateStateExecutionMetrics(IEnumerable<StateEnumItem> enumItems)
        {
            var obj = new StateExecutorMetrics();

            foreach (var item in enumItems)
            {
                // 创建 StateExecutionConfig 为每个状态
                // 注意：这里的参数是示例，应根据实际需求进行调整
                var config = new StateExecutionConfig(item.Value, true, 1, 1, 0, 0);

                // 添加到 StateExecutionMetrics
                obj.executions.Add(new StateExecution(config));
            }

            return obj;
        }

        public static Dictionary<int, StateTransitioner> CreateStateTransitioners(StateEnumMetrics enumMetrics, StateExecutorMetrics executor)
        {
            var objs = new Dictionary<int, StateTransitioner>();

            for (int i = 0; i < executor.executions.Count; i++)
            {
                var item = executor.executions[i];
                int nextState = (i < executor.executions.Count - 1) ? executor.executions[i + 1].State : enumMetrics.EndItem.Value;
                objs.Add(item.State, StateTransitioner.CreateStateConditionTrue(nextState));
            }

            return objs;
        }

        private List<StateExecution> executions;

        StateExecutorMetrics()
        {
            executions = new List<StateExecution>();
        }

        // 获取执行项数量
        public int GetExecutionCount()
        {
            return executions.Count;
        }

        // 获取执行项
        public StateExecution GetExecutionItem(int index)
        {
            if (index >= 0 && index < executions.Count)
            {
                return executions[index];
            }
            return null;
        }

        public int FindStateExecutionIndex(StateExecution executionItem)
        {
            var state = executionItem.State;
            return executions.FindIndex(item => item.State == state);
        }

        public int FindStateExecutionIndex(int state)
        {
            return executions.FindIndex(item => item.State == state);
        }

        public StateExecution FindStateExecutionItem(int stateValue)
        {
            return executions.FirstOrDefault(item => item.State == stateValue);
        }

        public bool AddStateExecutionItem(StateExecution item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            if (!executions.Any(i => i.State == item.State))
            {
                executions.Add(item);
                return true;
            }
            return false;
        }

        public bool RemoveStateExecutionItem(int stateValue)
        {
            var itemToRemove = executions.FirstOrDefault(item => item.State == stateValue);
            if (itemToRemove != null)
            {
                return executions.Remove(itemToRemove);
            }
            return false;
        }

        public bool UpdateStateExecutionItem(StateExecution updatedItem)
        {
            if (updatedItem == null)
                throw new ArgumentNullException(nameof(updatedItem));

            // 查找要更新的项的索引
            var index = executions.FindIndex(item => item.State == updatedItem.State);
            if (index != -1)
            {
                // 直接在列表中替换对象
                executions[index] = updatedItem;
            }
            return index != -1;
        }

    }


    public class StateExecution
    {
        // 以下是配置项
        public int State { get; private set; }
        public bool ShouldExecute { get; private set; }
        public int RetryTimes { get; private set; }
        public int DelayBefore { get; private set; }
        public int DelayAfter { get; private set; }
        public int Timeout { get; private set; }
        // 以下是状态执行的统计数据
        public long PrepareTime { get; private set; }
        public long ExecutionTime { get; private set; }

        public StateExecution(StateExecutionConfig config)
        {
            State = config.State;
            ShouldExecute = config.ShouldExecute;
            RetryTimes = config.RetryTimes;
            DelayBefore = config.DelayBefore;
            DelayAfter = config.DelayAfter;
            Timeout = config.Timeout;
        }

        public void RecordExecutionPrepare()
        {
            PrepareTime = TimeHelper.GetNow();
        }

        public void RecordExecution()
        {
            ExecutionTime = TimeHelper.GetNow();
        }

    }


    public class StateExecutionConfig
    {
        public int State { get; set; }
        public bool ShouldExecute { get; set; }
        public int RetryTimes { get; set; }
        public int DelayBefore { get; set; }
        public int DelayAfter { get; set; }
        public int Timeout { get; set; }

        public StateExecutionConfig(int state, bool shouldExecute, int retryTimes, int pauseBefore, int pauseAfter, int timeout)
        {
            State = state;
            ShouldExecute = shouldExecute;
            RetryTimes = retryTimes;
            DelayBefore = pauseBefore;
            DelayAfter = pauseAfter;
            Timeout = timeout;
        }
    }

   
}
