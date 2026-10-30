using System;
using System.Collections.Generic;
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
            Console.WriteLine("PASS HMI-G1 UInt16/Int16, Quality, Stale and safe mode gates");
        }
    }
}
