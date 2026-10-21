using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;

// 控制平台，有设备、设备属性表（为变量表）、状态机、状态判定器。状态判定器由一个状态对多个变量判定器组成
// 常规单个变量判定使用标准内置的，但如果复杂的就需要创建自定义状态判定器。
// 自定义状态判定器有可配置部分（关联哪些变量、名称），只是逻辑部分需要开放式代码
namespace CxDeviceSuite.Core
{
    // 状态机
    // 状态机的核心组件，包括枚举信息、状态判定器、状态执行器
    public class StateMachine
    {
        // 枚举类型
        private Type enumType;
        // 状态处理执行接口
        private IStateHandler stateHandler;
        // 状态枚举信息度量器：状态枚举信息的收集
        public StateEnumMetrics EnumMetrics { get; private set; }
        // 状态判定器映射表 < 状态 ：状态判定器 >，前置判定作用，有等待变量
        public Dictionary<int, IStateEvaluator> PreEvaluators { get; private set; }
        // 状态转换器映射表 < 状态 ：状态转换器 ，后置转换作用，操作后马上判断并转换到下一个状态
        public Dictionary<int, StateTransitioner> Transitioners { get; private set; }
        // 状态执行度量器：状态执行方面的信息收集、测量
        public StateExecutorMetrics ExecutorMetrics { get; private set; }
        // 当前执行项
        private StateExecution currentExecutorItem;

        public StateMachine(Type enumType, IStateHandler stateHandler)
        {
            if (!enumType.IsEnum)
            {
                throw new ArgumentException("参数必须是枚举类型", nameof(enumType));
            }
            var enumMetrics = new StateEnumMetrics(enumType);
            if (enumMetrics.Items.Count <= 0)
            {
                throw new ArgumentException("枚举数量不合法，需要大于0", nameof(enumType));
            }

            this.enumType = enumType;
            this.stateHandler = stateHandler;
            this.EnumMetrics = enumMetrics;
            PreEvaluators = new Dictionary<int, IStateEvaluator>();
            ExecutorMetrics = StateExecutorMetrics.CreateStateExecutionMetrics(this.EnumMetrics.Items);
            Transitioners = StateExecutorMetrics.CreateStateTransitioners(enumMetrics, ExecutorMetrics);
            SetCurrentExecutorItemToFirst();
        }

        public void AddPreEvaluator(int stateValue, IStateEvaluator evaluator)
        {
            PreEvaluators.Add(stateValue, evaluator);
        }

        public StateTransitioner GetCurrentStateTransitioner()
        {
            if (currentExecutorItem != null && Transitioners.TryGetValue(currentExecutorItem.State, out var transitioner))
            {
                return transitioner;
            }
            return null;
        }

        public IStateEvaluator GetCurrentEvaluator()
        {
            if (currentExecutorItem != null)
            {
                if (PreEvaluators.TryGetValue(currentExecutorItem.State, out var evaluator))
                {
                    return evaluator;
                }
            }
            return null;
        }

        public StateEnumItem GetCurrentStateInfo()
        {
            if (currentExecutorItem != null)
            {
                return EnumMetrics.GetEnumInfoByValue(currentExecutorItem.State);
            }
            return null;
        }

        // 执行当前状态的处理函数
        public StateHandleResult ExecuteCurrent()
        {
            if (currentExecutorItem == null)
            {
                throw new Exception("当前Executor为空");
            }

            currentExecutorItem.RecordExecution();
            var result = stateHandler.HandleState(currentExecutorItem.State);
            if (result == StateHandleResult.Retry)
            {
                var retryTimes = 1;
                while (retryTimes < currentExecutorItem.RetryTimes && result == StateHandleResult.Retry)
                {
                    result = stateHandler.HandleState(currentExecutorItem.State);
                    retryTimes++;
                }
            }

            return result; 
        }

        internal StateExecution GetCurrentExecutorItem()
        {
            return currentExecutorItem;
        }

        internal void SetCurrentExecutorItem(StateExecution nextExecutor)
        {
            currentExecutorItem = nextExecutor;
        }
        
        internal void SetCurrentExecutorItemToFirst()
        {
            if (ExecutorMetrics.GetExecutionCount() > 0)
            {
                currentExecutorItem = ExecutorMetrics.GetExecutionItem(0);
            }
        }

        internal bool IsEnd(StateExecution stateExecutionItem)
        {
            return EnumMetrics.IsEnd(stateExecutionItem.State);
        }

        internal bool IsEndByCurrentExecutor()
        {
            if (currentExecutorItem == null)
            {
                return true;
            }
            return EnumMetrics.IsEnd(currentExecutorItem.State);
        }

        // 获取转换器数量
        internal int GetTransitionersCount()
        {
            return Transitioners.Count;
        }

        // 获取转换器
        internal StateTransitioner GetTransitioner(int index)
        {
            return Transitioners[index];
        }
    }

}
