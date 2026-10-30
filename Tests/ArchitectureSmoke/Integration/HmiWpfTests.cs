using System;
using System.Linq;
using System.Threading;
using System.Windows;
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
