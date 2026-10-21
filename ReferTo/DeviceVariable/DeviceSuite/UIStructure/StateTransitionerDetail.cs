using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CxDeviceSuite.UIStructure
{
    // 状态转换器详情 StateTransitioner
    // StateTransitionItem.Evaluator 对应 StateVariableJudgementDetail
    public class StateTransitionerDetail
    {
        public StateTransitionerDetail()
        {
            StateEvaluatorDetails = new List<StateEvaluatorDetail>();
        }

        [Browsable(false)] // 不在 DataGridView 中显示
        public int State { get; set; }

        [DisplayName("状态名")] // 设置列头为 "State Name"
        public string StateName { get; set; }

        [DisplayName("状态中文名")] // 设置列头为 "State Title"
        public string StateTitle { get; set; }

        [DisplayName("超时")] 
        public int Timeout { get; set; }

        [Browsable(false)] // 不在 DataGridView 中显示
        public List<StateEvaluatorDetail> StateEvaluatorDetails { get; set; }
    }
}
