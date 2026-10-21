using CxDeviceSuite.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CxDeviceSuite.UIStructure
{
    public class VariableJudgementDetail
    {
        [DisplayName("设备名")] // 设置列头为 "设备名"
        public string DeviceName { get; set; }
        
        [DisplayName("设备属性")] // 设置列头为 "设备属性"
        public string VariableName { get; set; }

        [Browsable(false)] // 不在 DataGridView 中显示
        public Type VariableType { get; set; }

        [DisplayName("变量类型名")] 
        public string VariableTypeName { get; set; }
        
        [DisplayName("变量判定名")] 
        public string VariableEvaluatorName { get; set; }

        // 使用‘|’分隔多个值
        [DisplayName("变量判定值")] 
        public string Values { get; set; }

        [DisplayName("逻辑关系")] 
        public LogicOperator Operator { get; set; }

    }
}
