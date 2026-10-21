using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using VariableInterfaces;

namespace CxDeviceSuite.Core
{
    // 状态判定器接口
    public abstract class IStateEvaluator
    {
        // 获取这个状态评估器所需要的所有变量的列表
        public abstract List<(string deviceName, string variableName)> RequiredVariables();
        // 执行状态评估逻辑
        public abstract bool Evaluate();
        // 判定超时
        // -1 表示无限等待，即不用判断超时
        // 0 表示不等待，即不用判断超时
        // > 0 表示等待多少毫秒
        public abstract int GetTimeOutMS();

        public long StartEvaluateTime { get; private set; }

        public void StartEvaluate(long nowMS) 
        {
            StartEvaluateTime = nowMS;
        }

    }

    // 状态判定器
    public class StateEvaluator : IStateEvaluator
    {
        private List<VariableJudgementBase> judgements = new List<VariableJudgementBase>();
        private List<(string deviceName, string variableName)> requiredVariables = new List<(string deviceName, string variableName)>();

        public int TimeOutMS { get; private set; }
       
        // 返回所有判定所涉及的变量
        public override List<(string deviceName, string variableName)> RequiredVariables()
        {
            return requiredVariables;
        }

        private void UpdateRequiredVariables()
        {
            // 返回所有判定所涉及的变量
            requiredVariables = judgements.Select(j => (j.DeviceName, j.VariableName)).ToList();
        }

        // 如果在，取值，进行判断，通过就调用 State 对应方法
        public override bool Evaluate()
        {
            bool result = false;
            bool first = true;
            foreach (var judgement in judgements)
            {
                // 检查是否是变化的变量
                Type variableType = judgement.VariableType;
                string deviceName = judgement.DeviceName;
                string variableName = judgement.VariableName;
                var table = VariableManager.FindVariableTable(judgement.DeviceName);
                if (table == null)
                {
                    // 如果找不到变量表，直接返回
                    // todo: throw exception
                    return false;
                }
                bool judgementResult = false;
                // 根据变量类型执行相应的逻辑
                if (variableType == typeof(int))
                {
                    var value = table.GetVariable<int>(variableName);
                    var evaluator = judgement.VariableEvaluator as IVariableEvaluator<int>;
                    if (evaluator != null)
                    {
                        judgementResult = evaluator.Evaluate(value);
                    }
                }
                else if (variableType == typeof(double))
                {
                    var value = table.GetVariable<double>(variableName);
                    var evaluator = judgement.VariableEvaluator as IVariableEvaluator<double>;
                    if (evaluator != null)
                    {
                        judgementResult = evaluator.Evaluate(value);
                    }
                }
                else if (variableType == typeof(bool))
                {
                    var value = table.GetVariable<bool>(variableName);
                    var evaluator = judgement.VariableEvaluator as IVariableEvaluator<bool>;
                    if (evaluator != null)
                    {
                        judgementResult = evaluator.Evaluate(value);
                    }
                }
                else if (variableType == typeof(long))
                {
                    var value = table.GetVariable<long>(variableName);
                    var evaluator = judgement.VariableEvaluator as IVariableEvaluator<long>;
                    if (evaluator != null)
                    {
                        judgementResult = evaluator.Evaluate(value);
                    }
                }
                else if (variableType == typeof(string))
                {
                    var value = table.GetVariable<string>(variableName);
                    var evaluator = judgement.VariableEvaluator as IVariableEvaluator<string>;
                    if (evaluator != null)
                    {
                        judgementResult = evaluator.Evaluate(value);
                    }
                }

                //第一个判断器判断时，结果为judgementResult
                if (first)
                {
                    result = judgementResult;
                    first = false;
                }
                else
                {
                    switch (judgement.Operator)
                    {
                        case LogicOperator.And:
                            result &= judgementResult;
                            if (!judgementResult) return false; // 对于 AND，如果任何一个判定器为假，立即返回假
                            break;
                        case LogicOperator.Or:
                            result |= judgementResult;
                            if (judgementResult) return true; // 对于 OR，如果任何一个判定器为真，立即返回真
                            break;
                    }
                }
            }
            return result;
        }

        public void AddJudgement(VariableJudgementBase judgement)
        {
            judgements.Add(judgement);
            UpdateRequiredVariables();
        }

        // 返回判定仪数量
        public int GetJudgementCount()
        {
            return judgements.Count;
        }

        // 返回判定仪
        public VariableJudgementBase GetJudgement(int index)
        {
            if (index >= 0 && index < judgements.Count)
            {
                return judgements[index];
            }
            return null;
        }

        // -1 表示无限等待
        // 0 表示不等待
        // > 0 表示等待多少毫秒
        public void SetTimeOutMS(int timeOut)
        {
            TimeOutMS = timeOut;
        }

        private int timeOutMS = -1;

        public override int GetTimeOutMS()
        {
            return timeOutMS;
        }
    }

    public enum LogicOperator
    {
        And,
        Or
    }

    // 状态与设备的变量、变量判定仪关联
    public abstract class VariableJudgementBase
    {
        public string DeviceName { get; private set; }
        public string VariableName { get; private set; }
        public Type VariableType { get; protected set; }
        public string VariableEvaluatorName { get; private set; }
        public LogicOperator Operator { get; }
        public Object VariableEvaluator { get; protected set; }

        public VariableJudgementBase(string deviceName, string variableName, string variableEvaluatorName, LogicOperator logicOperator= LogicOperator.And)
        {
            DeviceName = deviceName;
            VariableName = variableName;
            VariableEvaluatorName = variableEvaluatorName;
            Operator = logicOperator;
        }
    }

    public class VariableJudgementComparable1<TValue> : VariableJudgementBase where TValue : IComparable
    {
        public VariableJudgementComparable1(string deviceName, string variableName, TValue value, string variableEvaluatorName, LogicOperator logicOperator = LogicOperator.And) 
            : base(deviceName, variableName, variableEvaluatorName, logicOperator)
        {
            Value = value;
            VariableEvaluator = VariableEvaluatorManager.CreateEvaluatorCompare1<TValue>(variableEvaluatorName, value);
            VariableType = typeof(TValue);
        }

        public TValue Value { get; private set; }

    }


    public class VariableJudgementComparable2<TValue> : VariableJudgementBase where TValue : IComparable
    {
        public VariableJudgementComparable2(string deviceName, string variableName, TValue value1, TValue value2, string variableEvaluatorName, LogicOperator logicOperator = LogicOperator.And)
            : base(deviceName, variableName, variableEvaluatorName, logicOperator)
        {
            Value1 = value1;
            Value2 = value2;
            VariableEvaluator = VariableEvaluatorManager.CreateEvaluatorCompare2<TValue>(variableEvaluatorName, value1, value2);
            VariableType = typeof(TValue);
        }

        public TValue Value1 { get; private set; }
        public TValue Value2 { get; private set; }

    }


    public class VariableJudgementString1 : VariableJudgementBase
    {
        public VariableJudgementString1(string deviceName, string variableName, string value, string variableEvaluatorName, LogicOperator logicOperator = LogicOperator.And)
            : base(deviceName, variableName, variableEvaluatorName, logicOperator)
        {
            Value = value;
            VariableEvaluator = VariableEvaluatorManager.CreateEvaluatorString1(variableEvaluatorName, value);
        }

        public string Value { get; private set; }

    }


}
