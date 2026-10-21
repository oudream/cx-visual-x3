using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CxDeviceSuite.UIStructure
{
    public class StateExecutorDetail
    {
        [Browsable(false)] // 不在 DataGridView 中显示
        public int State { get; set; }

        [DisplayName("状态名")] // 设置列头为 "State Name"
        public string StateName { get; set; }

        [DisplayName("状态中文名")] // 设置列头为 "State Title"
        public string StateTitle { get; set; }

        [DisplayName("是否启用")]
        public bool ShouldExecute { get; set; }
        
        [DisplayName("重试次数")]
        public int RetryTimes { get; set; }
        
        [DisplayName("前延时")]
        public int DelayBefore { get; set; }
        
        [DisplayName("后延时")]
        public int DelayAfter { get; set; }
        
        [DisplayName("运行超时")]
        public int Timeout { get; set; }
    }

}
