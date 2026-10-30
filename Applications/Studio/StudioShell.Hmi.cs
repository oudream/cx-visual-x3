using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AvalonDock.Layout;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Data.Variables;
using CxVisual.Mechanism.Presentation.Wpf;
using CxVisual.Professional.HMI;

namespace CxVisual.Studio
{
    /// <summary>HMI Viewer是已有Studio中央文档，MEM/MDS/MPW分别保有工程、数据和绘制Owner。</summary>
    public partial class MainWindow
    {
        private const string HmiViewerId = "hmi.scada.live";

        private void RegisterStudioHmiWorkbench()
        {
            RegisterStudioResourceViewer(HmiViewerId, "HMI实时运行观察",
                CreateStudioHmiRuntimeView, false, "core.page");
        }

        /// <summary>在现有Page下打开真正的MVI/SceneView运行投影；未连接时明确提示。</summary>
        public string OpenStudioHmiPage(Guid pageId)
        {
            return OpenStudioResourceEditor(pageId, HmiViewerId);
        }

        private FrameworkElement CreateStudioHmiRuntimeView(
            StudioResourceDocumentIdentity identity)
        {
            MEMEngineeringSession engineering = _viewModel.ProjectSession;
            if (engineering.Project.Id != identity.ProjectId ||
                !(engineering.Find(identity.ResourceId) is MEMPage))
                throw new InvalidOperationException("HMI页面工程身份已经失效");
            MDSVariableTable table = _studioVariableScope;
            if (table == null)
                return new TextBlock {
                    Text = "HMI运行数据未连接。请先附加受信Host的现有MDS Scope；" +
                        "Studio不会生成演示数据或自动连接PLC。",
                    TextWrapping = TextWrapping.Wrap, Margin = new Thickness(18)
                };
            // 尚无A提供的跨进程认证命令服务：只读，不提供在线控制回调。
            PHMHmiRuntimeSession runtime = new PHMHmiRuntimeSession(
                identity.ProjectId, engineering.Revision, table,
                PHMHmiMode.DesignPreview, TimeSpan.FromSeconds(5));
            try
            {
                return new MPWLiveSceneControl(
                    engineering, identity.ResourceId, runtime,
                    () => runtime.Points().Select(point =>
                    {
                        PHMHmiPointObservation value = runtime.Read(point.Name);
                        return new MPWLivePoint {
                            Name = value.PointId,
                            Value = value.Text + (value.Unit.Length > 0 ? " " + value.Unit : ""),
                            Unit = value.Unit, Quality = value.Quality.ToString(),
                            Source = value.Source,
                            Updated = value.RefreshUtc == default(DateTime) ? "—" :
                                value.RefreshUtc.ToString("O", CultureInfo.InvariantCulture)
                        };
                    }).ToArray(),
                    refresh => runtime.Subscribe(changes => refresh(),
                        runtime.Points().Select(p => p.Name)),
                    runtime);
            }
            catch { runtime.Dispose(); throw; }
        }

        /// <summary>切换MDS Scope/Host代次后关闭旧View，避免旧数据污染新设备。</summary>
        private void CloseStudioHmiRuntimeDocuments()
        {
            string[] keys = _studioViewerDocuments.Where(x =>
                x.Value.Identity.EditorId == HmiViewerId)
                .Select(x => x.Key).ToArray();
            foreach (string key in keys)
            {
                LayoutDocument document = FindStudioContent(key) as LayoutDocument;
                if (document != null) { document.Content = null; document.Close(); }
                ReleaseStudioViewerDocument(key);
            }
        }
    }
}
