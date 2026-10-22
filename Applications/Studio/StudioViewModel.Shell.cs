namespace CxVisual.Studio
{
    public sealed partial class StudioViewModel
    {
        /// <summary>主页和顶部工程标题实时投影原MEM项目名称；不保存第二份工程名称。</summary>
        public string StudioProjectTitle
        {
            get{return _editor==null||_editor.Document.IsClosed?"尚未打开工程":
                (_editor.Document.Project.Name??"未命名工程");}
        }
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
