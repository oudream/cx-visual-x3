using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VariableInterfaces;

namespace CxDeviceSuite.Core
{
    public interface IProcedureEvaluator
    {
        // 获取这个状态评估器所需要的所有变量的列表
        IEnumerable<(string deviceName, string variableName)> RequiredVariables();
        // 判断变量是否是这个状态评估器需要的
        bool IsVariableRelevant(string deviceName, string variableName);
        // 执行状态评估逻辑
        bool Evaluate();
    }

    // 状态判定器
    public class ProcedureEvaluator : IProcedureEvaluator
    {
        private List<VariableJudgementBase> judgements = new List<VariableJudgementBase>();

        public IEnumerable<(string deviceName, string variableName)> RequiredVariables()
        {
            // 返回所有判定所涉及的变量
            return judgements.Select(j => (j.DeviceName, j.VariableName));
        }

        // 当有变量变化（DeviceName、VariableName）推送时，先判断（DeviceName、VariableName）是否在judgements内，
        public bool IsVariableRelevant(string deviceName, string variableName)
        {
            // 检查变量是否在判定列表中
            return judgements.Any(j => j.DeviceName == deviceName && j.VariableName == variableName);
        }

        // 如果在，取值，进行判断，通过就调用 State 对应方法
        public bool Evaluate()
        {
            bool result = false;
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

                //
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
            return result;
        }

        public void AddJudgement(VariableJudgementBase judgement)
        {
            judgements.Add(judgement);
        }

    }
}
