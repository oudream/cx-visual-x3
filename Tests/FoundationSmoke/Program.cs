using System;
using System.IO;
using CxVisual.Foundation.Core;
using CxVisual.Foundation.Geometry;
using CxVisual.Foundation.Infrastructure;

namespace CxVisual.Tests.FoundationSmoke
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                TestFoundationValues();
                TestLogDispatchAndCapacity();
                TestLogFailureIsolation();
                TestImportantFile();
                Console.WriteLine("PASS Foundation: identity, geometry, numeric, logging and important event tests");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FAIL Foundation: " + ex);
                return 1;
            }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void TestFoundationValues()
        {
            CxStableId id = CxStableId.NewId();
            Check(id.Value != Guid.Empty && id.Equals(new CxStableId(id.Value)),
                "Identity value semantics failed");
            CxRect2D rect = new CxRect2D(10, 20, 20, 30);
            Check(rect.Contains(new CxPoint2D(15, 25)), "Hit test miss");
            Check(!rect.Contains(new CxPoint2D(50, 25)), "Hit test false positive");
            Check(rect.Intersects(new CxRect2D(20, 25, 20, 20)), "Rectangle intersection failed");
            CxPoint2D point = new CxAffine2D(1, 0, 0, 1, 30, 40)
                .Transform(new CxPoint2D(2, 4));
            Check(point.X == 32 && point.Y == 44, "Transform failed");
            bool finiteRejected = false;
            try { CxNumericGuard.RequireFinite(double.NaN, "value"); }
            catch (ArgumentOutOfRangeException) { finiteRejected = true; }
            Check(finiteRejected, "NaN check was not enforced");
        }

        private static void TestLogDispatchAndCapacity()
        {
            CxMemoryLogSink memory = new CxMemoryLogSink(2);
            int callbacks = 0;
            using (CxLogService logging = new CxLogService(
                new ICxLogSink[] { memory }, CxLogLevel.Warning))
            {
                IDisposable subscription = null;
                subscription = logging.Subscribe(record =>
                {
                    ++callbacks;
                    subscription.Dispose(); // 订阅回调中的反向注销不允许死锁
                });

                logging.Publish(new CxLogRecord("Core", "BelowThreshold", "skip"));
                Check(memory.Count == 0 && callbacks == 0, "Minimum level filter failed");

                logging.Publish(new CxLogRecord(
                    "System", "Warn", "warning", CxLogLevel.Warning,
                    "System.Runtime", "trace-1", false, null, "worker-A"));
                logging.Publish(new CxLogRecord(
                    "System", "Error", "error", CxLogLevel.Error,
                    "System.Runtime", "trace-1", false, new InvalidOperationException("test")));
                logging.Publish(new CxLogRecord(
                    "System", "Fatal", "important event", CxLogLevel.Critical,
                    "System.Runtime", "trace-2", true, null));

                Check(callbacks == 1, "Subscription disposal or callback count failed");
                Check(memory.Count == 2 && memory.DroppedCount == 1, "Bounded buffer did not evict");
                Check(memory.Get(0).Level == CxLogLevel.Error, "Log order lost");
                CxLogRecord[] snapshot = memory.Snapshot();
                Check(snapshot.Length == 2 && snapshot[1].IsImportant, "Snapshot failed");
                Check(snapshot[0].ToStructuredString().Contains("trace-1"),
                    "Correlation context was lost");
            }
        }

        private static void TestLogFailureIsolation()
        {
            CxMemoryLogSink memory = new CxMemoryLogSink(2);
            using (CxLogService logging = new CxLogService(
                new ICxLogSink[] { new AlwaysFailSink(), memory }))
            {
                bool aggregated = false;
                try { logging.Publish(new CxLogRecord("Test", "Failure", "still delivered")); }
                catch (AggregateException ex) { aggregated = ex.InnerExceptions.Count == 1; }
                Check(aggregated, "Consumer failure was silently swallowed");
                Check(memory.Count == 1, "Other consumers skipped when one sink failed");
            }
        }

        private static void TestImportantFile()
        {
            string directory = Path.Combine(Path.GetTempPath(),
                "CxVisual-LogSmoke-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "important.log");
            try
            {
                using (CxImportantEventFileSink sink = new CxImportantEventFileSink(path))
                {
                    sink.Write(new CxLogRecord("Run", "Ordinary", "not written"));
                    sink.Write(new CxLogRecord(
                        "Run", "Fault", "line1\nline2", CxLogLevel.Critical,
                        "Run.Motion", "flow-9", true, null));
                }
                string[] lines = File.ReadAllLines(path);
                Check(lines.Length == 1, "Important-only file filter failed");
                Check(lines[0].Contains("Critical") && lines[0].Contains("flow-9"),
                    "Important record metadata missing");
                Check(!lines[0].Contains("line2" + Environment.NewLine),
                    "Log injection with newline was not escaped");
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private sealed class AlwaysFailSink : ICxLogSink
        {
            public void Write(CxLogRecord record)
            {
                throw new InvalidOperationException("Synthetic sink failure");
            }
        }
    }
}
