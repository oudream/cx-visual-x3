using CxDeviceSuite.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CxDeviceSuite.UIStructure
{
    // 状态机的转换集：{状态1: 转换器1，状态2: 转换器2，...}
    // 转换器的状态判定集：{状态1: 判定器1，状态2: 判定器2，...}
    public static class StateTransitionerHelper
    {
        // 导出转换器详情列表的方法
        // StateTransitionerDetail
        // StateEvaluatorDetail
        // VariableJudgementDetail
        public static List<StateTransitionerDetail> ExportTransitionStateVariableJudgementList(StateMachine stateMachine)
        {
            var result = new List<StateTransitionerDetail>();
            var metrics = stateMachine.EnumMetrics;
            foreach (var transitionerEnumMetric in metrics.Items)
            {
                if (stateMachine.Transitioners.TryGetValue(transitionerEnumMetric.Value, out StateTransitioner transitioner))
                {
                    StateTransitionerDetail stateTransitionerDetail = new StateTransitionerDetail();
                    stateTransitionerDetail.State = transitionerEnumMetric.Value;
                    stateTransitionerDetail.StateName = transitionerEnumMetric.Name;
                    stateTransitionerDetail.StateTitle = transitionerEnumMetric.Title;
                    stateTransitionerDetail.Timeout = transitioner.TimeOut;
                    for (int i = 0; i < transitioner.GetTransitionItemsCount(); i++)
                    {
                        var stateTransitionItem = transitioner.GetTransitionItem(i);
                        var stateEvaluatorEnumMetric = metrics.GetEnumInfoByValue(stateTransitionItem.StateValue);
                        if (stateEvaluatorEnumMetric != null)
                        {
                            var evaluator = stateTransitionItem.Evaluator;
                            if (evaluator is StateEvaluator stateEvaluator)
                            {
                                StateEvaluatorDetail stateEvalluatorDetail = new StateEvaluatorDetail();
                                stateEvalluatorDetail.State = stateEvaluatorEnumMetric.Value;
                                stateEvalluatorDetail.StateName = stateEvaluatorEnumMetric.Name;
                                stateEvalluatorDetail.StateTitle = stateEvaluatorEnumMetric.Title;
                                for (int j = 0; j < stateEvaluator.GetJudgementCount(); j++)
                                {
                                    var judgement = stateEvaluator.GetJudgement(j);
                                    var judgementDetail = StateEvaluatorHelper.GetDetailForJudgement(judgement);
                                    // VariableJudgementDetail
                                    stateEvalluatorDetail.VariableJudgements.Add(judgementDetail);
                                }
                                // StateEvaluatorDetail
                                stateTransitionerDetail.StateEvaluatorDetails.Add(stateEvalluatorDetail);
                            }
                            else if (evaluator is StateEvaluatorTrue stateEvaluatorTrue)
                            {
                                StateEvaluatorDetail stateEvalluatorDetail = new StateEvaluatorDetail();
                                stateEvalluatorDetail.State = stateEvaluatorEnumMetric.Value;
                                stateEvalluatorDetail.StateName = stateEvaluatorEnumMetric.Name;
                                stateEvalluatorDetail.StateTitle = stateEvaluatorEnumMetric.Title;
                                stateEvalluatorDetail.Remark = "True判定";
                                stateTransitionerDetail.StateEvaluatorDetails.Add(stateEvalluatorDetail);
                            }
                            else if (evaluator is StateEvaluatorCustom stateEvaluatorCustom)
                            {
                                StateEvaluatorDetail stateEvalluatorDetail = new StateEvaluatorDetail();
                                stateEvalluatorDetail.State = stateEvaluatorEnumMetric.Value;
                                stateEvalluatorDetail.StateName = stateEvaluatorEnumMetric.Name;
                                stateEvalluatorDetail.StateTitle = stateEvaluatorEnumMetric.Title;
                                stateEvalluatorDetail.Remark = "脚本判定";
                                stateTransitionerDetail.StateEvaluatorDetails.Add(stateEvalluatorDetail);
                            }
                        }
                    }
                    // StateTransitionerDetail
                    result.Add(stateTransitionerDetail);
                }
            }
            
            return result;
        }
    }

}
