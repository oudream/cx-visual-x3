using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CxVisual.Mechanism.Data.Variables;

namespace CxVisual.Tests.ArchitectureSmoke
{
    // 只发布真实测量结果，不把任意固定机型的吞吐当成整个产品的SLA。
    public static class VariableThroughputBenchmarks
    {
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("VARBENCH: " + message); }
        public static void Run()
        {
            const int count = 250000;
            MDSVariableTable table = new MDSVariableTable("Benchmark");
            for (int i = 0; i < 4; ++i)
                table.Define(new MDSVariableDefinition("Counter" + i, typeof(int)));
            MDSVariable<int>[] values = Enumerable.Range(0, 4)
                .Select(i => table.Resolve<int>("Counter" + i)).ToArray();
            for (int i = 0; i < 10000; ++i) values[0].Update(i);
            long previous = values[0].Snapshot().Revision;
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            int gc0 = GC.CollectionCount(0);
            Stopwatch clock = Stopwatch.StartNew();
            for (int i = 0; i < count * 4; ++i) values[0].Update(i);
            clock.Stop();
            Check(values[0].Snapshot().Revision == previous + count * 4, "单线程提交次数不正确");
            Console.WriteLine("VARBENCH single=1000000 throughput/s=" +
                (1000000.0 / Math.Max(clock.Elapsed.TotalSeconds, 0.000001)).ToString("F0") +
                " elapsedMs=" + clock.Elapsed.TotalMilliseconds.ToString("F1") +
                " Gen0=" + (GC.CollectionCount(0) - gc0) + " CPU=" + Environment.ProcessorCount);

            long[] revisions = values.Select(x => x.Snapshot().Revision).ToArray();
            clock.Restart();
            Parallel.For(0, 4, worker => {
                MDSVariable<int> variable = values[worker];
                for (int j = 0; j < count; ++j) variable.Update(j);
            });
            clock.Stop();
            for (int i = 0; i < 4; ++i)
                Check(values[i].Snapshot().Revision == revisions[i] + count, "并行计数不一致" + i);
            Console.WriteLine("VARBENCH parallel=4x250000 throughput/s=" +
                (1000000.0 / Math.Max(clock.Elapsed.TotalSeconds, 0.000001)).ToString("F0") +
                " elapsedMs=" + clock.Elapsed.TotalMilliseconds.ToString("F1"));

            using (MDSVariableChangeDispatcher dispatcher = table.CreateDispatcher(256))
            using (dispatcher.Subscribe(batch => { }))
            {
                previous = values[0].Snapshot().Revision;
                clock.Restart();
                for (int i = 0; i < 100000; ++i) values[0].Update(i);
                Check(dispatcher.Drain(TimeSpan.FromSeconds(15)), "异步通知未排空");
                clock.Stop();
                Check(values[0].Snapshot().Revision == previous + 100000, "派发影响权威更新");
                MDSVariableDispatchMetrics metrics = dispatcher.Metrics();
                Console.WriteLine("VARBENCH observed=100000 elapsedMs=" +
                    clock.Elapsed.TotalMilliseconds.ToString("F1") +
                    " coalesced=" + metrics.Coalesced + " overflow=" + metrics.Overflow +
                    " errors=" + metrics.SubscriberFailures);
            }
            Console.WriteLine("VARBENCH PASS (throughput measured, not production SLA)");
        }
    }
}
