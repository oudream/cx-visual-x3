using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Mechanism.Data.Configuration;

namespace CxVisual.Tests.ArchitectureSmoke
{
    /// <summary>
    /// Measured D-gate tests, in the existing .NET Framework 4.8 test assemblies.
    /// These are observations, not approved production SLA thresholds.
    /// </summary>
    internal static class ConfigPerformanceTests
    {
        private static volatile long _checksum;
        private static void Require(bool ok, string message)
        { if (!ok) throw new InvalidOperationException("CONFIG_D_FAILURE: " + message); }

        private static void Measure(string operation, int count, Action action)
        {
            action(); // warm up JIT and the access path
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            long beforeBytes = GC.GetTotalMemory(false);
            int[] beforeGc = new[] { GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2) };
            long[] ticks = new long[count];
            Stopwatch wall = Stopwatch.StartNew();
            for (int i = 0; i < count; ++i)
            {
                long start = Stopwatch.GetTimestamp();
                action();
                ticks[i] = Stopwatch.GetTimestamp() - start;
            }
            wall.Stop();
            Array.Sort(ticks);
            double micros = 1000000.0 / Stopwatch.Frequency;
            long afterBytes = GC.GetTotalMemory(false);
            Console.WriteLine("CONFIG_D_METRIC operation=" + operation +
                " samples=" + count +
                " elapsedMs=" + wall.ElapsedMilliseconds +
                " p50Us=" + (ticks[(int)((count - 1) * 0.50)] * micros).ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                " p95Us=" + (ticks[(int)((count - 1) * 0.95)] * micros).ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                " p99Us=" + (ticks[(int)((count - 1) * 0.99)] * micros).ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                " memDeltaBytes=" + (afterBytes - beforeBytes) +
                " gc0=" + (GC.CollectionCount(0) - beforeGc[0]) +
                " gc1=" + (GC.CollectionCount(1) - beforeGc[1]) +
                " gc2=" + (GC.CollectionCount(2) - beforeGc[2]));
        }

        private static void ReaderConcurrency(MHSConfigSection<V13ConfigSample> config, int threads)
        {
            long invalid = 0;
            using (MHSConfigSnapshot<V13ConfigSample> task = config.CaptureForTask("D-fixed-" + threads))
            {
                int stable = task.Value(x => x.ExposureMs);
                Task[] readers = Enumerable.Range(0, threads).Select(index => Task.Run(() =>
                {
                    Func<int> current = config.PrepareValue(x => x.ExposureMs);
                    for (int n = 0; n < 1000; ++n)
                    {
                        int live = current();
                        if ((live != 80 && live != 100 && live != 120 && live != 140) ||
                            task.Value(x => x.ExposureMs) != stable)
                            Interlocked.Increment(ref invalid);
                    }
                })).ToArray();
                int changed = threads == 1 ? 100 : threads == 4 ? 120 : 140;
                Task writer = Task.Run(() =>
                {
                    MHSConfigCommitResult saved = config.UpdateAsync(x => x.ExposureMs = changed,
                        "D-reader-writer-" + threads, CancellationToken.None).GetAwaiter().GetResult();
                    Require(saved.Success, "concurrent writer commits");
                    Require(config.ApplyPendingAtTaskBoundary(), "concurrent writer applies");
                });
                Task.WaitAll(readers.Concat(new[] { writer }).ToArray());
                Require(invalid == 0 && config.Value(x => x.ExposureMs) == changed,
                    "concurrent readers saw only complete effective revisions");
            }
            Console.WriteLine("CONFIG_D_CHECK concurrentReaders=" + threads + " status=PASS");
        }

        public static void Run(bool full)
        {
            string root = Path.Combine(Path.GetTempPath(), "CxConfig-D-" + Guid.NewGuid().ToString("N"));
            Console.WriteLine("CONFIG_D_ENV dateUtc=" + DateTime.UtcNow.ToString("o") +
                " os=" + Environment.OSVersion.VersionString.Replace(' ', '_') +
                " clr=" + Environment.Version + " x64=" + (IntPtr.Size == 8) +
                " cpuCount=" + Environment.ProcessorCount + " full=" + full);
            try
            {
                var service = new MHSConfigService(Path.Combine(root, "data"));
                var config = service.ForMachine("D-Readers").Open<V13ConfigSample>();
                Func<int> live = config.PrepareValue(x => x.ExposureMs);
                using (MHSConfigSnapshot<V13ConfigSample> snapshot = config.CaptureForTask("D-benchmark"))
                {
                    Func<int> pinned = snapshot.PrepareValue(x => x.ExposureMs);
                    Measure("prepared-effective", full ? 100000 : 20000, () => { _checksum = live(); });
                    Measure("direct-value", full ? 100000 : 20000,
                        () => { _checksum = config.Value(x => x.ExposureMs); });
                    Measure("pinned-value", full ? 100000 : 20000,
                        () => { _checksum = pinned(); });
                    Measure("complex-detached", full ? 2000 : 400,
                        () => { _checksum = config.Get().Detectors.Count; });
                }
                ReaderConcurrency(config, 1);
                ReaderConcurrency(config, 4);
                ReaderConcurrency(config, 16);

                int subs = full ? 1000 : 100;
                var subscriptions = new List<IDisposable>();
                int hits = 0;
                try
                {
                    for (int i = 0; i < subs; ++i)
                        subscriptions.Add(config.Watch(x => x.ExposureMs, change =>
                            Interlocked.Increment(ref hits)));
                    Require(config.UpdateAsync(x => x.ExposureMs = 160, "D-subscribers",
                        CancellationToken.None).GetAwaiter().GetResult().Success,
                        "subscriber update saved");
                    Require(hits == 0, "subscribers not fired before effective apply");
                    Require(config.ApplyPendingAtTaskBoundary() && hits == subs,
                        "each subscriber received exactly one effective notification");
                }
                finally { foreach (var watch in subscriptions) watch.Dispose(); }
                Console.WriteLine("CONFIG_D_CHECK subscribers=" + subs +
                    " notifications=" + hits + " status=PASS");

                int entries = full ? 10000 : 1000;
                var large = service.ForMachine("D-Collections").Open<ConfigDynamicCollections>();
                var values = new Dictionary<string, ConfigDynamicNode>(entries);
                for (int i = 0; i < entries; ++i)
                    values.Add("detector-" + i.ToString("D5"), new ConfigDynamicNode { Gain = (i % 10) + 1 });
                Stopwatch collectionWatch = Stopwatch.StartNew();
                Require(large.UpdateAsync(x => x.Detectors = values, "D-large-collection",
                    CancellationToken.None).GetAwaiter().GetResult().Success, "large collection commits");
                Require(large.ApplyPendingAtTaskBoundary(), "large collection applied");
                using (var editor = new MHSConfigEditorModel<ConfigDynamicCollections>(large, "D-dynamic-view"))
                {
                    int nodes = editor.DescribeCollectionItems().Count(x => x.CollectionPath == "Detectors");
                    Require(nodes == entries, "dynamic editor describes all collection entries");
                }
                var reopened = new MHSConfigService(Path.Combine(root, "data"))
                    .ForMachine("D-Collections").Open<ConfigDynamicCollections>();
                Require(reopened.Value(x => x.Detectors).Count == entries,
                    "large dictionary survives restart and retained count");
                collectionWatch.Stop();
                Console.WriteLine("CONFIG_D_CHECK dictionaryEntries=" + entries +
                    " roundtripMs=" + collectionWatch.ElapsedMilliseconds + " status=PASS");

                int revisions = full ? 1000 : 100;
                var journal = service.ForMachine("D-History").Open<V13ConfigSample>();
                Stopwatch auditWall = Stopwatch.StartNew();
                for (int i = 1; i <= revisions; ++i)
                {
                    int value = i + 100;
                    Require(journal.UpdateAsync(x => x.ExposureMs = value,
                        "D-journal", CancellationToken.None).GetAwaiter().GetResult().Success,
                        "append durable revision " + i);
                }
                var committed = journal.ReadCommittedAfter(null, revisions + 10);
                Require(committed.Count == revisions, "journal cursor counts all durable commits");
                var afterRestart = new MHSConfigService(Path.Combine(root, "data"))
                    .ForMachine("D-History").Open<V13ConfigSample>();
                Require(afterRestart.ReadCommittedAfter(null, revisions + 10).Count == revisions &&
                    afterRestart.GetLatestSaved().ExposureMs == revisions + 100,
                    "journal replay and Saved survive restart");
                auditWall.Stop();
                long diskBytes = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                    .Sum(name => new FileInfo(name).Length);
                Console.WriteLine("CONFIG_D_CHECK durableRevisions=" + revisions +
                    " journalMs=" + auditWall.ElapsedMilliseconds +
                    " diskBytes=" + diskBytes + " status=PASS");
                Console.WriteLine("PASS CONFIG_D_MEASURED (not a real-hardware or long-term SLA certification)");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }
}
