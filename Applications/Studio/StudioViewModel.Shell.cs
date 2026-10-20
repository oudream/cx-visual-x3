namespace CxVisual.Studio
{
    public sealed partial class StudioViewModel
    {
        /// <summary>为图形结构工具窗暴露当前工程真实定义对象；不是第二个模型。</summary>
        public CxVisual.Mechanism.Core.MEMNode CurrentStudioDefinition
        {
            get
            {
                if (_editor == null || !_editor.CurrentResourceId.HasValue) return null;
                return ProjectSession.Find(_editor.CurrentResourceId.Value);
            }
        }
        /// <summary>Shell仅报告布局告警，不修改MEM对象或业务Revision。</summary>
        public void ReportStudioShellWarning(string message)
        {
            Status = message;
        }
    }
}
