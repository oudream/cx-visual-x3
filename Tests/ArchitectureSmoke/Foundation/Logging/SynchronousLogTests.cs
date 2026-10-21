using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Infrastructure.Logging;
using CxVisual.Foundation.Infrastructure.Logging.Dedicated;

namespace CxVisual.Tests.FoundationSmoke
{
    /// <summary>F16同步专有文件日志；不依赖异步FLDLogService。</summary>
    internal static class SynchronousLogTests
    {
        public static void Run(string root)
        {
            Directory.CreateDirectory(root);
            TestSynchronousEntry(Path.Combine(root, "Sync"));
            TestMidnightSwitch(Path.Combine(root, "DaySwitch"));
            TestConcurrentWriting(Path.Combine(root, "Concurrent"));
            TestIOException(Path.Combine(root, "IOError"));
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("SynchronousLogTests: " + message);
        }

        private static void TestSynchronousEntry(string root)
        {
            Directory.CreateDirectory(root);
            string expired = Path.Combine(root, "Mes-20200101.log");
            string unrelated = Path.Combine(root, "MesExtended-20200101.log");
            File.WriteAllText(expired, "purge");
            File.WriteAllText(unrelated, "preserve");
            int pushed = 0;
            FLDLogEventHandler observer = delegate(FLDLogRecord record)
            {
                Interlocked.Increment(ref pushed);
            };
            FLDLogHelper.LogEvent += observer;
            try
            {
                FLDEventLogHelper.Init(root, retentionDays: 7);
                Check(FLDEventLogHelper.IsInitialized, "not initialized");
                Check(FLDEventLogHelper.Information(FLDLOG_TYPE.Mes, "Upload", "MES", "成功"),
                    "MES write failed");
                string today = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                string mesFile = Path.Combine(root, "Mes-" + today + ".log");
                Check(File.Exists(mesFile) && File.ReadAllText(mesFile).Contains("成功"),
                    "MES not synchronously readable after return");
                Check(!File.Exists(expired) && File.Exists(unrelated),
                    "Retention deleted unrelated files or failed to delete own old file");

                FLDEventLogHelper.SetLevel(FLDLOG_TYPE.Panda, FLDLogLevel.Error);
                Check(!FLDEventLogHelper.Information(FLDLOG_TYPE.Panda, "Skipped"),
                    "Per-category threshold did not filter");
                Check(FLDEventLogHelper.Error(FLDLOG_TYPE.Panda, "Request", "Panda", "接口失败"),
                    "Panda error write failed");
                string panda = Path.Combine(root, "Panda-" + today + ".log");
                Check(File.ReadAllText(panda).Contains("接口失败")
                    && !File.ReadAllText(panda).Contains("Skipped"),
                    "Panda category log invalid");

                Check(FLDEventLogHelper.Write("CT3DConfig", new FLDImportantEventLogEntry
                {
                    Level = FLDLogLevel.Warning,
                    EventType = "ConfigChanged",
                    Source = "CT3D",
                    User = "engineer",
                    Description = "参数变更",
                    ExtraData = new Dictionary<string, string> { { "Old", "1" }, { "New", "2" } }
                }, flushToDisk: true), "Custom sync file write failed");
                string configFile = Path.Combine(root, "CT3DConfig-" + today + ".log");
                Check(File.ReadAllText(configFile).Contains("engineer")
                    && File.ReadAllText(configFile).Contains("Old=1"), "Custom fields missing");
                Check(pushed == 0, "Sync log must not push realtime events");
                bool rejected = false;
                try { FLDEventLogHelper.Information("../escape", "Invalid"); }
                catch (ArgumentException) { rejected = true; }
                Check(rejected, "Path traversal not rejected");
            }
            finally
            {
                FLDLogHelper.LogEvent -= observer;
                FLDEventLogHelper.Release();
            }
            Check(!FLDEventLogHelper.IsInitialized, "Release did not reset state");
            FLDEventLogHelper.Init(root);
            try
            {
                FLDEventLogHelper.Information(FLDLOG_TYPE.Mes, "Resume", "MES", "继续追加");
                string file = Path.Combine(root, "Mes-" +
                    DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".log");
                string text = File.ReadAllText(file);
                Check(text.Contains("成功") && text.Contains("继续追加"),
                    "Reinitialization truncated existing log");
            }
            finally { FLDEventLogHelper.Release(); }
        }

        private static void TestMidnightSwitch(string root)
        {
            DateTime fake = new DateTime(2026, 10, 14, 23, 59, 59);
            using (FLDImportantEventLog writer = new FLDImportantEventLog(root,
                "Panda", retentionDays: 100, clock: () => fake))
            {
                writer.Write(new FLDImportantEventLogEntry { EventType = "BeforeMidnight" });
                string oldFile = writer.CurrentFilePath;
                fake = fake.AddSeconds(3);
                writer.Write(new FLDImportantEventLogEntry { EventType = "AfterMidnight" });
                string next = writer.CurrentFilePath;
                Check(oldFile != next && File.Exists(oldFile) && File.Exists(next),
                    "Day switch failed");
                Check(File.ReadAllText(oldFile).Contains("BeforeMidnight") &&
                    File.ReadAllText(next).Contains("AfterMidnight"), "Cross-day data missing");
                using (FileStream exclusive = new FileStream(oldFile, FileMode.Open,
                    FileAccess.ReadWrite, FileShare.None))
                    Check(exclusive.Length > 0, "Old writer still holds open file");
            }
        }

        private static void TestConcurrentWriting(string root)
        {
            FLDEventLogHelper.Init(root);
            try
            {
                Task[] tasks = new Task[4];
                for (int worker = 0; worker < 4; ++worker)
                {
                    int id = worker;
                    tasks[worker] = Task.Run(() =>
                    {
                        for (int i = 0; i < 50; ++i)
                            Check(FLDEventLogHelper.Warning(FLDLOG_TYPE.PLC,
                                "Concurrent", "Thread" + id, "message" + i),
                                "Concurrent write failed");
                    });
                }
                Task.WaitAll(tasks);
                string file = Path.Combine(root, "PLC-" +
                    DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".log");
                Check(File.ReadAllLines(file).Length == 200, "Concurrent lines lost");
            }
            finally { FLDEventLogHelper.Release(); }
        }

        private static void TestIOException(string root)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(root));
            File.WriteAllText(root, "not a directory");
            FLDEventLogHelper.Init(root);
            try
            {
                bool failed = false;
                try { FLDEventLogHelper.Error(FLDLOG_TYPE.Mes, "FailedWrite"); }
                catch (IOException) { failed = true; }
                catch (UnauthorizedAccessException) { failed = true; }
                Check(failed, "IO errors should propagate to the caller");
            }
            finally { FLDEventLogHelper.Release(); }
        }
    }
}
