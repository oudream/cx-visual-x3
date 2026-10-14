using System;
using System.IO;
using System.Linq;
using System.Threading;
using CxVisual.Foundation.Infrastructure;

namespace CxVisual.Tests.FoundationSmoke
{
    internal static class LoggingAndMonitoringTests
    {
        public static void Run()
        {
            string root = Path.Combine(Path.GetTempPath(), "CxVisual-F16F19-"
                + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                TestLogging(root);
                TestMonitoring();
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void Assert(bool condition, string reason)
        {
            if (!condition) throw new InvalidOperationException(reason);
        }

        private static void TestLogging(string root)
        {
            int published = 0;
            using (CxLogHub hub = new CxLogHub(root, 256, 5))
            {
                Action<CxLogRecord> listener = record => Interlocked.Increment(ref published);
                hub.RecordReceived += listener;
                Assert(hub.Log(CxLogLevel.Information, "F16", "ordinary"),
                    "Ordinary log enqueue failed");
                CxDedicatedLogger channel = hub.ForDedicated("M13", "PLC01");
                Assert(channel.Warning("tcp timeout"), "Dedicated enqueue failed");
                Assert(hub.Log(CxLogLevel.Critical, "F16", "alarm",
                    "PLC01", "SafetyStop", true, "run-001"),
                    "Important enqueue failed");
                hub.Flush();
                Assert(published == 3, "Publish count mismatch");
                Assert(hub.Snapshot().Length == 3, "Snapshot count mismatch");
                Assert(hub.DroppedCount == 0, "Unexpected dropped records");
                hub.RecordReceived -= listener;
                Assert(!hub.Snapshot().Any(x => x.ModuleId == "Unknown"),
                    "Unexpected category");
            }

            string common = Path.Combine(root, "Common");
            string dedicated = Path.Combine(root, "Dedicated");
            string important = Path.Combine(root, "Important");
            Assert(Directory.GetFiles(common, "*.log").Length > 0, "Common log missing");
            Assert(Directory.GetFiles(dedicated, "PLC01-*.log").Length > 0,
                "Dedicated PLC01 file missing");
            string[] importantFiles = Directory.GetFiles(important, "*.log");
            Assert(importantFiles.Length > 0, "Important file missing");
            string importantText = File.ReadAllText(importantFiles[0]);
            Assert(importantText.Contains("SafetyStop"), "Important correlation missing");
            Assert(!importantText.Contains("tcp timeout"), "Normal record leaked to important file");

            bool traversalRejected = false;
            try { CxRollingFileLogSink.ValidateName("../outside"); }
            catch (ArgumentException) { traversalRejected = true; }
            Assert(traversalRejected, "Path traversal was not rejected");

            // 小文件轮转、跨进程再次开启追加、历史保留：
            string rotation = Path.Combine(root, "Rotation");
            string otherName = Path.Combine(rotation, "other-20200101-000.log");
            Directory.CreateDirectory(rotation);
            File.WriteAllText(otherName, "other log must survive");
            File.WriteAllText(Path.Combine(rotation, "test-20200101-000.log"), "old test only");
            using (CxRollingFileLogSink sink = new CxRollingFileLogSink(rotation, "test", 4096, 7))
            {
                for (int i = 0; i < 100; ++i)
                    sink.Write(new CxLogRecord("F16", "rotate",
                        new string('A', 150) + i.ToString()));
            }
            int firstFileCount = Directory.GetFiles(rotation, "test-*.log").Length;
            Assert(firstFileCount >= 2, "File size rotation missing");
            Assert(File.Exists(otherName), "Retention deleted unrelated logs");
            Assert(!File.Exists(Path.Combine(rotation, "test-20200101-000.log")),
                "Retention failed for expired own file");
            long oldBytes = Directory.GetFiles(rotation, "test-*.log").Sum(p => new FileInfo(p).Length);
            using (CxRollingFileLogSink sink = new CxRollingFileLogSink(rotation, "test", 4096, 7))
                sink.Write(new CxLogRecord("F16", "restart", "append, never truncate"));
            long newBytes = Directory.GetFiles(rotation, "test-*.log").Sum(p => new FileInfo(p).Length);
            Assert(newBytes > oldBytes, "Restart should append rather than truncate");
        }

        private static void TestMonitoring()
        {
            using (CxSystemMonitor monitor = new CxSystemMonitor(250, "unlikely-process"))
            {
                CxSystemSnapshot first = monitor.SampleNow();
                Assert(first.LogicalProcessors > 0, "CPU count missing");
                Assert(first.Processes != null, "Process collection missing");
                Thread.Sleep(300);
                CxSystemSnapshot second = monitor.SampleNow();
                Assert(second.TimeUtc >= first.TimeUtc, "Sample time reversed");
                int received = 0;
                Action<CxSystemSnapshot> callback = value => Interlocked.Increment(ref received);
                monitor.Sampled += callback;
                monitor.Start();
                Thread.Sleep(800);
                monitor.Stop();
                monitor.Sampled -= callback;
                Assert(received > 0, "Periodic monitoring did not produce any sample");
            }
        }
    }
}
