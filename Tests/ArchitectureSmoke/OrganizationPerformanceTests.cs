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
            Console.WriteLine("nodes,create_ms,live_bytes_with_history,live_bytes_after_clear_history,query100k_ms,move1k_ms,save_ms,load_ms,file_bytes,gc0,gc1,gc2");
            foreach (int count in stress ? new[] { 1000, 100000, 1000000 } : new[] { 1000, 100000 }) Measure(count);
        }
        private static void Measure(int count)
        {
            string path = Path.Combine(Path.GetTempPath(), "cx-h01-perf-" + Guid.NewGuid().ToString("N") + ".cxproj");
            long startMemory = GC.GetTotalMemory(true); int g0=GC.CollectionCount(0),g1=GC.CollectionCount(1),g2=GC.CollectionCount(2);
            var timer = Stopwatch.StartNew(); var session = MEMEngineering.Create("规模验证"); var page = session.AddPage("P"); MRCElement last = null;
            var result = session.Edit("批量生成", e => { for (int i = 0; i < count; i++) last=e.AddRectangle(page.RootContent, "R", i, 0, 1, 1); });
            if (!result.Success) throw new InvalidOperationException(result.Message);
            long create=timer.ElapsedMilliseconds, withHistory=GC.GetTotalMemory(true)-startMemory;
            session.ClearHistory(); long retained=GC.GetTotalMemory(true)-startMemory;
            timer.Restart();
            for(int i=0;i<100000;i++) if(!ReferenceEquals(session.Find(last.Id),last)) throw new InvalidOperationException("Indexed identity lost");
            long query=timer.ElapsedMilliseconds; timer.Restart();
            for(int i=0;i<1000;i++) session.Move(last,i,1);
            long move=timer.ElapsedMilliseconds; session.ClearHistory(); timer.Restart(); session.SaveAs(path);
            long save=timer.ElapsedMilliseconds; timer.Restart(); var loaded=MEMEngineering.Open(path); long load=timer.ElapsedMilliseconds;
            if(loaded.Count!=count+3 || ((MRCElement)loaded.Find(last.Id)).X!=999 || !loaded.Validate().IsValid) throw new InvalidOperationException("Scale roundtrip failed");
            long bytes=new FileInfo(path).Length; loaded.Close(); session.Close(); File.Delete(path);
            Console.WriteLine(string.Join(",",new long[]{count,create,withHistory,retained,query,move,save,load,bytes,GC.CollectionCount(0)-g0,GC.CollectionCount(1)-g1,GC.CollectionCount(2)-g2}));
            GC.KeepAlive(session); GC.KeepAlive(loaded);
        }
    }
}
