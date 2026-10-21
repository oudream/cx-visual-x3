using CxDeviceSuite.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CxDeviceSuite.UIStructure
{
    public static class StateEvaluatorHelper
    {
        public static List<StateEvaluatorDetail> ExportPreStateVariableJudgementList(StateMachine stateMachine)
        {
            var result = new List<StateEvaluatorDetail>();
            foreach (var metric in stateMachine.EnumMetrics.Items)
            {
                if (stateMachine.PreEvaluators.TryGetValue(metric.Value, out IStateEvaluator evaluator))
                {
                    if (evaluator is StateEvaluator stateEvaluator)
                    {
                        StateEvaluatorDetail stateJudgementDetail = new StateEvaluatorDetail();
                        stateJudgementDetail.State = metric.Value;
                        stateJudgementDetail.StateName = metric.Name;
                        stateJudgementDetail.StateTitle = metric.Title;
                        for (int i = 0; i < stateEvaluator.GetJudgementCount(); i++)
                        {
                            var judgement = stateEvaluator.GetJudgement(i);
                            var judgementDetail = GetDetailForJudgement(judgement);
                            stateJudgementDetail.VariableJudgements.Add(judgementDetail);
                        }
                        result.Add(stateJudgementDetail);
                    }
                }
            }
            return result;
        }

      


        // 此方法适用于处理不同类型的判断并填充详细信息
        public static VariableJudgementDetail GetDetailForJudgement(VariableJudgementBase judgement)
        {
            var detail = new VariableJudgementDetail
            {
                // 假设我们可以从judgement或相关的上下文中获取以下信息
                DeviceName = judgement.DeviceName, 
                VariableName = judgement.VariableName, 
                Operator = judgement.Operator, // 示例操作符，实际值应从judgement获取
                VariableType = judgement.VariableType,
                VariableEvaluatorName = judgement.VariableEvaluatorName,
            };

            // 根据VariableType来决定如何展示Values
            if (judgement.VariableType == typeof(int))
            {
                detail.VariableTypeName = "int";
                detail.Values = ProcessComparableJudgement<int>(judgement);
            }
            else if (judgement.VariableType == typeof(double))
            {
                detail.VariableTypeName = "double";
                detail.Values = ProcessComparableJudgement<double>(judgement);
            }
            else if (judgement.VariableType == typeof(bool))
            {
                detail.VariableTypeName = "bool";
                detail.Values = ProcessComparableJudgement<bool>(judgement);
            }
            else if (judgement.VariableType == typeof(long))
            {
                detail.VariableTypeName = "long";
                detail.Values = ProcessComparableJudgement<long>(judgement);
            }
            else if (judgement.VariableType == typeof(string))
            {
                detail.VariableTypeName = "string";
                detail.Values = ProcessComparableJudgement<string>(judgement);
            }
            // 可以添加更多类型的处理逻辑

            return detail;
        }

        // 泛型方法用于处理VariableJudgementComparable1<T>和VariableJudgementComparable2<T>
        private static string ProcessComparableJudgement<T>(VariableJudgementBase judgement) where T : IComparable
        {
            // 这里需要根据judgement的实际类型来处理不同的逻辑
            if (judgement is VariableJudgementComparable1<T> comparable1)
            {
                // 示例：返回Comparable1的值的字符串表示
                return $"{comparable1.Value}";
            }
            else if (judgement is VariableJudgementComparable2<T> comparable2)
            {
                // 示例：返回Comparable2的值的字符串表示
                return $"{comparable2.Value1}|{comparable2.Value2}";
            }
            else if (judgement is VariableJudgementString1 string1)
            {
                // 示例：返回Comparable2的值的字符串表示
                return string1.Value;
            }
            else
            {
                return "null";
            }
        }

    }
}
