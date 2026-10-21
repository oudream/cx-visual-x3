using CxDeviceSuite.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CxDeviceSuite.UIStructure
{
    public class StateEvaluatorDetail
    {
        public StateEvaluatorDetail() 
        {
            VariableJudgements = new List<VariableJudgementDetail>();
        }

        [Browsable(false)] // 不在 DataGridView 中显示
        public int State { get; set; }

        [DisplayName("状态名")] // 设置列头为 "State Name"
        public string StateName { get; set; }

        [DisplayName("状态中文名")] // 设置列头为 "State Title"
        public string StateTitle { get; set; }
    
        [Browsable(false)] // 此属性为复杂类型，通常不直接显示在 DataGridView 中
        public List<VariableJudgementDetail> VariableJudgements { get; set;}

        [DisplayName("备注")] // 设置列头为 "Remark"
        public string Remark { get; set; }

    }
}
