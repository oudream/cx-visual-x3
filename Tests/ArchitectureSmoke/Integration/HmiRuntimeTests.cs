using System;
using System.Collections.Generic;
using System.Linq;
using CxVisual.Mechanism.Core;
using System.Threading;
using CxVisual.Mechanism.Data.RuntimeData;
using CxVisual.Mechanism.Data.Variables;
using CxVisual.Professional.HMI;

namespace CxVisual.Tests.ArchitectureSmoke
{
    /// <summary>真正的MDS运行变量和HMI合同，不使用独立伪数据仓；G1非实机HIL。</summary>
    internal static class HmiRuntimeTests
    {
        private static void Check(bool ok, string message)
        {
            if (!ok) throw new InvalidOperationException("HMI-G1: " + message);
        }
        public static void Run()
        {
            MDSVariableTable table = new MDSVariableTable("plc/station1");
            table.Define(new MDSVariableDefinition("word100", typeof(ushort), "rpm", true));
            table.Define(new MDSVariableDefinition("input101", typeof(short), "℃", true));
            using (PHMHmiRuntimeSession view = new PHMHmiRuntimeSession(
                Guid.NewGuid(), 1, table, PHMHmiMode.DesignPreview, TimeSpan.FromMinutes(1)))
            {
                Check(!view.Read("word100").HasValue && view.Read("word100").Text == "—",
                    "未采样不得假装0值");
                table.Update<ushort>("word100", 91);
                table.Update<short>("input101", -12);
                Check(view.Read("word100").Text == "91" &&
                    view.Read("word100").Unit == "rpm" &&
                    view.Read("word100").IsUsable, "UInt16真实快照");
                Check((double)view.Resolve(MDSBindingSource.Variable, "word100", typeof(double))
                    == 91d, "UInt16变换为Visual数值");
                Check((double)view.Resolve(MDSBindingSource.Variable, "input101", typeof(double))
                    == -12d, "Int16负值正确");
                Check(view.Resolve(MDSBindingSource.Variable, "word100", typeof(bool)) == null,
                    "数值1不能隐式成为联锁布尔值");
                table.Resolve<ushort>("word100").Update(91,
                    new MDSVariableUpdateOptions("device", MDSVariableQuality.Bad));
                Check(view.Read("word100").Quality == MDSVariableQuality.Bad &&
                    view.Read("word100").Text == "91" && !view.Read("word100").IsUsable,
                    "Bad保持末值但不宣称有效");
                Check(view.Resolve(MDSBindingSource.Variable, "word100", typeof(double)) == null,
                    "Bad不可驱动正常动画");
                Check(view.RequestActionAsync(null, null, "Click", null, null,
                    CancellationToken.None).GetAwaiter().GetResult().State ==
                    PHMHmiOperationState.Rejected, "设计预览不得下发");
                bool rejected = false;
                try { view.Subscribe(x => { }, new[] { "not-a-point" }); }
                catch (KeyNotFoundException) { rejected = true; }
                Check(rejected, "未知点必须拒绝");
            }
            using (PHMHmiRuntimeSession online = new PHMHmiRuntimeSession(
                Guid.NewGuid(), 2, table, PHMHmiMode.AuthorizedOnline, TimeSpan.FromMinutes(1)))
            {
                Check(online.RequestActionAsync(null, null, "Click", null, null,
                    CancellationToken.None).GetAwaiter().GetResult().State ==
                    PHMHmiOperationState.Rejected, "无受信宿主必须拒绝");
            }
            using (PHMHmiRuntimeSession stale = new PHMHmiRuntimeSession(
                Guid.NewGuid(), 1, table, PHMHmiMode.Simulation, TimeSpan.Zero))
                Check(stale.Read("input101").Quality == MDSVariableQuality.Stale,
                    "采样超期必须Stale");
            // 真MDS异步LatestOnly通知：仅刷新绑定点，不制造额外Current。
            using (PHMHmiRuntimeSession watcher = new PHMHmiRuntimeSession(
                Guid.NewGuid(), 1, table, PHMHmiMode.DesignPreview, TimeSpan.FromMinutes(1)))
            using (ManualResetEventSlim notified = new ManualResetEventSlim(false))
            using (IDisposable subscription = watcher.Subscribe(names =>
            {
                if (names.Contains("input101")) notified.Set();
            }, new[] { "input101" }))
            {
                table.Update<short>("input101", -13);
                Check(notified.Wait(TimeSpan.FromSeconds(5)), "MDS订阅必须及时到达");
                Check(watcher.Read("input101").Text == "-13", "变更后应重新读取唯一MDS真值");
            }
            // 真MEM保存的ActionId经过MVI路由，只有注入的宿主处理器才可能接收；
            // 本测试的返回仅验证转交/Unknown UI合同，不伪称PLC动作成功。
            using (MEMEngineeringSession doc = MEMEngineering.Create("HMI Action"))
            {
                MEMPage page = doc.AddPage("Monitor");
                var button = doc.AddRectangle(page.RootContent, "Control", 10, 10, 90, 35);
                doc.SetVisualBehavior(button, new MRCVisualBehaviorDefinition(
                    eventActions: new[] { new MRCVisualEventAction("Click", "plc.write") }));
                using (PHMHmiRuntimeSession runtime = new PHMHmiRuntimeSession(
                    doc.Project.Id, doc.Revision, table,
                    PHMHmiMode.AuthorizedOnline, TimeSpan.FromMinutes(1)))
                {
                    int calls = 0;
                    var response = runtime.RequestActionAsync(doc, doc.GetAddress(button),
                        "Click", "91", (request, token) =>
                        {
                            calls++;
                            Check(request.ActionId == "plc.write" &&
                                request.EventValue == "91", "ActionId必须来自MEM保存定义");
                            return System.Threading.Tasks.Task.FromResult(
                                new PHMHmiOperationFeedback(PHMHmiOperationState.Unknown,
                                    "PLC_REGISTER_OUTCOME_UNKNOWN"));
                        }, CancellationToken.None).GetAwaiter().GetResult();
                    Check(calls == 1 && response.State == PHMHmiOperationState.Unknown,
                        "未知回读不得伪报Confirmed或自动重试");
                    doc.AddText(page.RootContent, "New", "change", 100, 100);
                    response = runtime.RequestActionAsync(doc, doc.GetAddress(button),
                        "Click", "92", (request, token) =>
                        {
                            calls++;
                            return System.Threading.Tasks.Task.FromResult(
                                new PHMHmiOperationFeedback(PHMHmiOperationState.Confirmed, ""));
                        }, CancellationToken.None).GetAwaiter().GetResult();
                    Check(response.State == PHMHmiOperationState.Rejected && calls == 1,
                        "旧工程Revision会话不能下发");
                }
                doc.Close(true);
            }
            Console.WriteLine("PASS HMI-G1 UInt16/Int16, Quality, Stale and safe mode gates");
        }
    }
}
