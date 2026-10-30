using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Data.Variables;
using CxVisual.Mechanism.Presentation.Wpf;
using CxVisual.Professional.HMI;

namespace CxVisual.Tests.ArchitectureSmoke
{
    /// <summary>真实WPF STA窗口+MEM Page+MDS+HMI+MVI/SceneView，仍属G1软件门禁。</summary>
    internal static class HmiWpfTests
    {
        private static T FindVisual<T>(DependencyObject root) where T : DependencyObject
        {
            if (root == null) return null;
            T match = root as T;
            if (match != null) return match;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                T found = FindVisual<T>(VisualTreeHelper.GetChild(root, i));
                if (found != null) return found;
            }
            return null;
        }

        public static int Run()
        {
            Exception failure = null;
            Thread worker = new Thread(() =>
            {
                try
                {
                    using (MEMEngineeringSession doc = MEMEngineering.Create("HMI STA"))
                    {
                        MEMPage page = doc.AddPage("PLC画面");
                        var label = doc.AddText(page.RootContent, "RPM", "待采集", 20, 20);
                        doc.SetBinding(label, "text", MPMBinding.Variable("word100"));
                        int revision = doc.Revision;
                        MDSVariableTable table = new MDSVariableTable("plc");
                        table.Define(new MDSVariableDefinition("word100", typeof(ushort), "rpm", true));
                        table.Update<ushort>("word100", 73);
                        using (PHMHmiRuntimeSession session = new PHMHmiRuntimeSession(
                            doc.Project.Id, revision, table, PHMHmiMode.DesignPreview,
                            TimeSpan.FromMinutes(1)))
                        using (MPWLiveSceneControl view = new MPWLiveSceneControl(
                            doc, page.Id, session,
                            () => session.Points().Select(p =>
                            {
                                PHMHmiPointObservation v = session.Read(p.Name);
                                return new MPWLivePoint {
                                    Name = v.PointId, Value = v.Text, Unit = v.Unit,
                                    Quality = v.Quality.ToString(), Source = v.Source
                                };
                            }).ToArray(),
                            notify => session.Subscribe(x => notify(), new[] { "word100" }),
                            null))
                        {
                            Window window = new Window {
                                Content = view, Width = 700, Height = 400,
                                Left = -10000, Top = -10000, ShowActivated = false,
                                ShowInTaskbar = false
                            };
                            try
                            {
                                window.Show();
                                view.Measure(new Size(680, 370));
                                view.Arrange(new Rect(0, 0, 680, 370));
                                view.UpdateLayout();
                                Dispatcher.CurrentDispatcher.Invoke(new Action(() => { }),
                                    DispatcherPriority.Render);
                                if (!view.IsVisible)
                                    throw new InvalidOperationException("HMI WPF真实窗口未呈现");
                                table.Update<ushort>("word100", 91);
                                view.Refresh();
                                var frame = CxVisual.Mechanism.Visual.MVISceneProjection.ProjectResource(
                                    doc, page.Id, session);
                                if (frame.Count != 1 || frame[0].Text != "91")
                                    throw new InvalidOperationException(
                                        "MDS动态Text没有通过原MVI投影更新真实WPF页面");
                                var design = CxVisual.Mechanism.Visual.MVISceneProjection.ProjectResource(
                                    doc, page.Id);
                                if (design.Count != 1 || design[0].Text != "待采集")
                                    throw new InvalidOperationException(
                                        "运行值污染了MEM静态Text声明");
                                if (doc.Revision != revision)
                                    throw new InvalidOperationException("数据变化污染了MEM Revision");
                                if ((double)session.Resolve(
                                    CxVisual.Mechanism.Data.RuntimeData.MDSBindingSource.Variable,
                                    "word100", typeof(double)) != 91d)
                                    throw new InvalidOperationException("HMI的WPF运行绑定没有真实更新");
                            }
                            finally { window.Close(); }
                        }
                        // 真WPF Button.Click必须默认禁控；只有显式受信宿主委托
                        // 才允许沿MEM已保存ActionId走PHM运行会话，测试回执不代表PLC。
                        Guid actionButton = PHMHmiScreenAuthoring.AddButton(
                            doc, page.Id, "控制验证", 30, 90);
                        PHMHmiScreenAuthoring.AssignSavedAction(doc, actionButton, "plc.write");
                        int actionRevision = doc.Revision;
                        using (PHMHmiRuntimeSession online = new PHMHmiRuntimeSession(
                            doc.Project.Id, actionRevision, table,
                            PHMHmiMode.AuthorizedOnline, TimeSpan.FromMinutes(1)))
                        {
                            Func<System.Collections.Generic.IReadOnlyList<MPWLivePoint>> points =
                                () => new MPWLivePoint[0];
                            using (MPWLiveSceneControl preview = new MPWLiveSceneControl(
                                doc, page.Id, online, points, null, null))
                            {
                                Window previewWindow = new Window {
                                    Content = preview, Width = 700, Height = 400,
                                    Left = -10000, Top = -10000, ShowInTaskbar = false
                                };
                                try
                                {
                                    previewWindow.Show();
                                    previewWindow.UpdateLayout();
                                    Button previewButton = FindVisual<Button>(preview);
                                    if (previewButton == null || preview.CanRequestAction ||
                                        preview.LastActionFeedback != null)
                                        throw new InvalidOperationException(
                                            "HMI预览应绘制真实按钮但绝不能开放操作入口");
                                    previewButton.RaiseEvent(new RoutedEventArgs(
                                        ButtonBase.ClickEvent));
                                    if (preview.LastActionFeedback != null)
                                        throw new InvalidOperationException(
                                            "HMI无受信宿主仍然发送了操作事件");
                                }
                                finally { previewWindow.Close(); }
                            }
                            int hostCalls = 0;
                            using (MPWLiveSceneControl enabled = new MPWLiveSceneControl(
                                doc, page.Id, online, points, null, null,
                                async (widget, ct) =>
                                {
                                    PHMHmiOperationFeedback feedback =
                                        await online.RequestActionAsync(
                                            doc, widget.Address, widget.EventId, widget.Value,
                                            (invocation, token) =>
                                            {
                                                hostCalls++;
                                                if (invocation.ActionId != "plc.write")
                                                    throw new InvalidOperationException(
                                                        "HMI操作没有消费已保存ActionId");
                                                return System.Threading.Tasks.Task.FromResult(
                                                    new PHMHmiOperationFeedback(
                                                        PHMHmiOperationState.Unknown,
                                                        "PLC_REGISTER_OUTCOME_UNKNOWN"));
                                            }, ct);
                                    return new MPWLiveActionFeedback(
                                        feedback.State == PHMHmiOperationState.Unknown ?
                                            MPWLiveActionState.Unknown :
                                            MPWLiveActionState.Rejected, feedback.Reason);
                                }))
                            {
                                Window onlineWindow = new Window {
                                    Content = enabled, Width = 700, Height = 400,
                                    Left = -10000, Top = -10000, ShowInTaskbar = false
                                };
                                try
                                {
                                    onlineWindow.Show();
                                    onlineWindow.UpdateLayout();
                                    Button liveButton = FindVisual<Button>(enabled);
                                    if (liveButton == null || !enabled.CanRequestAction)
                                        throw new InvalidOperationException(
                                            "受信Host未开放真实WPF Widget点击");
                                    liveButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                                    Dispatcher.CurrentDispatcher.Invoke(new Action(() => {}),
                                        DispatcherPriority.Background);
                                    if (hostCalls != 1 || enabled.LastActionFeedback == null ||
                                        enabled.LastActionFeedback.State != MPWLiveActionState.Unknown)
                                        throw new InvalidOperationException(
                                            "HMI Click->MEM Action->受信适配->Unknown显示未闭环");
                                    // Unknown不得让原会话的同一按钮重复写入；
                                    // 即使程序直接抬升WPF Click事件，也必须被Owner拒绝。
                                    liveButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                                    if (enabled.CanRequestAction || hostCalls != 1)
                                        throw new InvalidOperationException(
                                            "HMI Unknown后旧会话重新发出非幂等操作");
                                    if (doc.Revision != actionRevision)
                                        throw new InvalidOperationException(
                                            "HMI现场操作污染MEM工程Revision");
                                }
                                finally { onlineWindow.Close(); }
                            }
                        }
                        doc.Close(true);
                    }
                }
                catch (Exception error) { failure = error; }
            });
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
            if (!worker.Join(TimeSpan.FromSeconds(60)))
                throw new TimeoutException("HMI真实WPF STA回归超时");
            if (failure != null) throw new InvalidOperationException(
                "HMI WPF G1失败", failure);
            Console.WriteLine("PASS HMI real WPF STA / MEM Page / MDS runtime update");
            return 0;
        }
    }
}
