namespace CxVisual.Studio
{
    public sealed partial class StudioViewModel
    {
        /// <summary>Shell仅报告布局告警，不修改MEM对象或业务Revision。</summary>
        public void ReportStudioShellWarning(string message)
        {
            Status = message;
        }
    }
}
