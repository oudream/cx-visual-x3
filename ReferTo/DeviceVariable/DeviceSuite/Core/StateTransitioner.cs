using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CxDeviceSuite.Core
{
    // 状态转换器
    // 由多个状态条件仪（由状态值、判定器StateEvaluator）组成，转换执行时，看哪个状态条件仪通过，就返回哪个状态
    public class StateTransitioner
    {
        public static StateTransitioner CreateStateConditionTrue(int state)
        {
            var obj = new StateTransitioner(0);
            obj.transitionItems.Add(new StateTransitionItem(state, new StateEvaluatorTrue()));
            return obj;
        }

        private List<StateTransitionItem> transitionItems = new List<StateTransitionItem>();

        public int TimeOut { get; private set; }

        StateTransitioner(int timeout)
        {
            TimeOut = timeout;
        }

        public void AddCondition(StateTransitionItem transitionItem)
        {
            if (transitionItem == null)
                throw new ArgumentNullException(nameof(transitionItem));
            transitionItems.Add(transitionItem);
        }

        public long StartTransTime { get; private set; }

        public void StartTrans(long nowMS)
        {
            StartTransTime = nowMS;
        }

        public int TransTo()
        {
            foreach (var transitionItem in transitionItems)
            {
                if (transitionItem.Evaluator.Evaluate())
                {
                    return transitionItem.StateValue;
                }
            }
            return -1; // 如果没有条件满足，返回 -1
        }

        // 获取转换项数量
        public int GetTransitionItemsCount()
        {
            return transitionItems.Count;
        }

        // 获取转换项
        public StateTransitionItem GetTransitionItem(int index)
        {
            if (index >= 0 && index < transitionItems.Count)
            {
                return transitionItems[index];
            }
            return null;
        }

    }


    public class StateTransitionItem
    {
        public int StateValue { get; private set; }
        public IStateEvaluator Evaluator { get; private set; }

        public StateTransitionItem(int stateValue, IStateEvaluator evaluator)
        {
            StateValue = stateValue;
            Evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        }
    }
}
