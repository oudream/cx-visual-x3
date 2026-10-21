using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using CxVisual.Mechanism.Core;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class OrganizationPerformanceTests
    {
        public static void Run(bool stress)
        {
            Console.WriteLine("elements,create_ms,live_bytes_with_history,live_bytes_after_clear_history,mixed_query100k_us,leaf_move1k_us,group_move1k_us,save_ms,load_ms,file_bytes,gc0,gc1,gc2");
            foreach (int count in stress ? new[] { 1000, 100000, 1000000 } : new[] { 1000, 100000 }) Measure(count);
        }
        private static void Measure(int count)
        {
            string path = Path.Combine(Path.GetTempPath(), "cx-h01-perf-" + Guid.NewGuid().ToString("N") + ".cxproj");
            long startMemory = GC.GetTotalMemory(true); int g0=GC.CollectionCount(0),g1=GC.CollectionCount(1),g2=GC.CollectionCount(2);
            var timer = Stopwatch.StartNew(); var session = MEMEngineering.Create("规模验证"); var page = session.AddPage("P"); var group = session.AddGroup(page.RootContent,"G"); MRCElement last = null;
            var result = session.Edit("批量生成", e => { for (int i = 0; i < count; i++) last=e.AddRectangle(group, "R", i, 0, 1, 1); });
            if (!result.Success) throw new InvalidOperationException(result.Message);
            long create=timer.ElapsedMilliseconds, withHistory=GC.GetTotalMemory(true)-startMemory;
            session.ClearHistory(); long retained=GC.GetTotalMemory(true)-startMemory;
            timer.Restart();
            for(int i=0;i<100000;i++) { var expected=group.Children[(int)(i*7919L%count)]; if(!ReferenceEquals(session.Find(expected.Id),expected)) throw new InvalidOperationException("Indexed identity lost"); }
            long query=timer.ElapsedTicks*1000000/Stopwatch.Frequency; timer.Restart();
            for(int i=0;i<1000;i++) session.Move(last,i,1);
            long move=timer.ElapsedTicks*1000000/Stopwatch.Frequency; timer.Restart();
            for(int i=0;i<1000;i++) session.Move(group,i,2);
            long groupMove=timer.ElapsedTicks*1000000/Stopwatch.Frequency;
            if(last.X!=999 || last.Y!=1) throw new InvalidOperationException("Group movement rewrote a descendant");
            session.ClearHistory(); timer.Restart(); session.SaveAs(path);
            long save=timer.ElapsedMilliseconds; timer.Restart(); var loaded=MEMEngineering.Open(path); long load=timer.ElapsedMilliseconds;
            if(loaded.Count!=count+4 || ((MRCElement)loaded.Find(last.Id)).X!=999 || ((MRCGroup)loaded.Find(group.Id)).X!=999 || !loaded.Validate().IsValid) throw new InvalidOperationException("Scale roundtrip failed");
            long bytes=new FileInfo(path).Length; loaded.Close(); session.Close(); File.Delete(path);
            Console.WriteLine(string.Join(",",new long[]{count,create,withHistory,retained,query,move,groupMove,save,load,bytes,GC.CollectionCount(0)-g0,GC.CollectionCount(1)-g1,GC.CollectionCount(2)-g2}));
            GC.KeepAlive(session); GC.KeepAlive(loaded);
        }
    }
}
