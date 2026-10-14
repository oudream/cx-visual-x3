using System;
using System.IO;
using System.Threading;
using CxVisual.Mechanism.Core;

namespace CxVisual.Tests.ArchitectureSmoke
{
    /// <summary>配置L1行为合同：先于实现写入；后续不允许用空壳让测试通过。</summary>
    internal static class ConfigL1Tests
    {
        public sealed class DetectionOptions
        {
            [CxConfigItem("阈值", Min = 0, Max = 4095, Category = "检测")]
            public int Threshold { get; set; } = 100;

            [CxConfigItem("检测名称", Required = true, Category = "检测")]
            public string Name { get; set; } = "demo";

            [CxConfigItem("启用")]
            public bool Enabled { get; set; } = true;
        }

        public sealed class NetworkOptions
        {
            [CxConfigItem("端口", Min = 1, Max = 65535)]
            public int Port { get; set; } = 5000;
            public string Host { get; set; } = "127.0.0.1";
        }

        internal static void Run()
        {
            string root = Path.Combine(Path.GetTempPath(), "cxvisual-config-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                DefaultAndReload(root);
                ReadIsolationAndEditValidation(root);
                RevisionConflictAndIndependentInstances(root);
                NotificationAndUnsubscribe(root);
                PersistedVsUnsaved(root);
                CrossFieldValidation(root);
                CorruptOrUnknownSchemaDoesNotOverwrite(root);
                SaveFailureDoesNotPublish(root);
                ExternalConcurrentWriterConflict(root);
                MissingRequiredFileFails(root);
                MetadataForEditor();
                Console.WriteLine("PASS ConfigL1: defaults, snapshots, edits, validation, persistence, notification, conflicts, failure/recovery, metadata");
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        private static void DefaultAndReload(string root)
        {
            string directory = Path.Combine(root, "default");
            CxConfigManager manager = new CxConfigManager(directory);
            manager.Register<DetectionOptions>("Vision.Detect");
            manager.Initialize();
            Check(File.Exists(Path.Combine(directory, "Vision.Detect.json")), "No generated default JSON");
            Check(manager.Get<DetectionOptions>("Vision.Detect").Threshold == 100, "Invalid default");
            manager.UpdateAndSave<DetectionOptions>("Vision.Detect", x => x.Threshold = 300);
            CxConfigManager reloaded = new CxConfigManager(directory);
            reloaded.Register<DetectionOptions>("Vision.Detect");
            reloaded.Initialize();
            Check(reloaded.Get<DetectionOptions>("Vision.Detect").Threshold == 300, "Persisted value missing");
            Check(reloaded.GetRevision("Vision.Detect") == 1, "Revision did not persist");
        }

        private static void ReadIsolationAndEditValidation(string root)
        {
            CxConfigManager m = Ready(root, "isolated");
            DetectionOptions copy = m.Get<DetectionOptions>("Vision.Detect");
            copy.Threshold = 4000;
            Check(m.Get<DetectionOptions>("Vision.Detect").Threshold == 100, "Get leaked mutable reference");
            Expect<ArgumentException>(() => m.Update<DetectionOptions>("Vision.Detect", x => x.Threshold = 9000), "Out of range accepted");
            Check(m.GetRevision("Vision.Detect") == 0, "Rejected change raised revision");
            Check(m.Get<DetectionOptions>("Vision.Detect").Threshold == 100, "Rejected edit changed active snapshot");
            m.Update<DetectionOptions>("Vision.Detect", x => x.Threshold = 200);
            Check(m.GetRevision("Vision.Detect") == 1 && m.IsDirty("Vision.Detect"), "Commit/dirty status failed");
        }

        private static void RevisionConflictAndIndependentInstances(string root)
        {
            string directory = Path.Combine(root, "instances");
            CxConfigManager m = new CxConfigManager(directory);
            m.Register<DetectionOptions>("Detector.A");
            m.Register<DetectionOptions>("Detector.B");
            m.Initialize();
            m.Update<DetectionOptions>("Detector.A", x => x.Threshold = 222, 0);
            Expect<InvalidOperationException>(() => m.Update<DetectionOptions>("Detector.A", x => x.Threshold = 5, 0), "Revision conflict accepted");
            Check(m.Get<DetectionOptions>("Detector.B").Threshold == 100, "Other instance affected");
            Check(m.GetRevision("Detector.A") == 1 && m.GetRevision("Detector.B") == 0, "Instance versions not isolated");
        }

        private static void NotificationAndUnsubscribe(string root)
        {
            CxConfigManager m = Ready(root, "notify");
            int seen = 0;
            int lastThreshold = 0;
            IDisposable subscription = m.Subscribe<DetectionOptions>("Vision.Detect", (value, change) =>
            {
                seen++;
                lastThreshold = value.Threshold;
                value.Threshold = 3333; // 订阅者不能修改已发布快照。
            });
            m.Update<DetectionOptions>("Vision.Detect", x => x.Threshold = 210);
            Check(seen == 1 && lastThreshold == 210, "Change notification missing");
            Check(m.Get<DetectionOptions>("Vision.Detect").Threshold == 210, "Subscriber mutated state");
            subscription.Dispose();
            m.Update<DetectionOptions>("Vision.Detect", x => x.Threshold = 220);
            Check(seen == 1, "Disposed observer still runs");
        }

        private static void PersistedVsUnsaved(string root)
        {
            string directory = Path.Combine(root, "dirty");
            CxConfigManager m = Ready(root, "dirty");
            m.Update<DetectionOptions>("Vision.Detect", x => x.Threshold = 777);
            CxConfigManager other = new CxConfigManager(directory);
            other.Register<DetectionOptions>("Vision.Detect");
            other.Initialize();
            Check(other.Get<DetectionOptions>("Vision.Detect").Threshold == 100, "Uncommitted data reached disk");
            m.Save("Vision.Detect");
            Check(!m.IsDirty("Vision.Detect"), "Save did not clear dirty");
            CxConfigManager after = new CxConfigManager(directory);
            after.Register<DetectionOptions>("Vision.Detect");
            after.Initialize();
            Check(after.Get<DetectionOptions>("Vision.Detect").Threshold == 777, "Explicit Save failed");
        }

        private static void CrossFieldValidation(string root)
        {
            CxConfigManager m = new CxConfigManager(Path.Combine(root, "cross"));
            m.Register<NetworkOptions>("Network", validator: x => x.Host == "127.0.0.1" && x.Port == 80
                ? "Port 80 on local host is prohibited by this test rule" : null);
            m.Initialize();
            Expect<ArgumentException>(() => m.Update<NetworkOptions>("Network", x => x.Port = 0), "Attribute validation not applied");
            Expect<ArgumentException>(() => m.Update<NetworkOptions>("Network", x => x.Port = 80), "Cross-field validator not called");
            Check(m.Get<NetworkOptions>("Network").Port == 5000, "Invalid edit leaked");
        }

        private static void CorruptOrUnknownSchemaDoesNotOverwrite(string root)
        {
            string directory = Path.Combine(root, "corrupt");
            CxConfigManager m = Ready(root, "corrupt");
            string file = Path.Combine(directory, "Vision.Detect.json");
            File.WriteAllText(file, "{invalid-json");
            CxConfigManager next = new CxConfigManager(directory);
            next.Register<DetectionOptions>("Vision.Detect");
            Expect<Exception>(() => next.Initialize(), "Malformed file silently reset");
            Check(File.ReadAllText(file) == "{invalid-json", "Malformed file was overwritten");
            File.WriteAllText(file, "{\"Id\":\"Vision.Detect\",\"SchemaVersion\":999,\"Revision\":0,\"Data\":{\"Threshold\":10,\"Name\":\"x\",\"Enabled\":true}}");
            CxConfigManager future = new CxConfigManager(directory);
            future.Register<DetectionOptions>("Vision.Detect");
            Expect<InvalidDataException>(() => future.Initialize(), "Future schema accepted");
            Check(File.ReadAllText(file).Contains("999"), "Unknown schema rewritten");
        }

        private static void SaveFailureDoesNotPublish(string root)
        {
            string directory = Path.Combine(root, "failure");
            CxConfigManager m = Ready(root, "failure");
            string file = Path.Combine(directory, "Vision.Detect.json");
            string before = File.ReadAllText(file);
            string temp = file + ".tmp";
            Directory.CreateDirectory(temp);
            int notified = 0;
            IDisposable observer = m.Subscribe<DetectionOptions>("Vision.Detect", (x, change) => notified++);
            try
            {
                Expect<Exception>(() => m.UpdateAndSave<DetectionOptions>("Vision.Detect", x => x.Threshold = 999), "Failed save reported success");
                Check(m.Get<DetectionOptions>("Vision.Detect").Threshold == 100, "Failed UpdateAndSave published in memory");
                Check(m.GetRevision("Vision.Detect") == 0, "Failed UpdateAndSave committed a revision");
                Check(File.ReadAllText(file) == before && notified == 0, "Failed save destroyed old version or notified");
            }
            finally { observer.Dispose(); Directory.Delete(temp); }
        }

        private static void ExternalConcurrentWriterConflict(string root)
        {
            string directory = Path.Combine(root, "concurrent");
            CxConfigManager a = Ready(root, "concurrent");
            CxConfigManager b = new CxConfigManager(directory);
            b.Register<DetectionOptions>("Vision.Detect");
            b.Initialize();
            a.UpdateAndSave<DetectionOptions>("Vision.Detect", x => x.Threshold = 777);
            b.Update<DetectionOptions>("Vision.Detect", x => x.Threshold = 222);
            Expect<InvalidOperationException>(() => b.Save("Vision.Detect"), "Stale writer overwrote newer saved revision");
            Check(File.ReadAllText(Path.Combine(directory, "Vision.Detect.json")).Contains("777"), "External write was clobbered");
        }

        private static void MissingRequiredFileFails(string root)
        {
            CxConfigManager m = new CxConfigManager(Path.Combine(root, "required"));
            m.Register<DetectionOptions>("Machine.Safety", requireExisting: true);
            Expect<FileNotFoundException>(() => m.Initialize(), "Critical configuration silently defaulted");
        }

        private static void MetadataForEditor()
        {
            CxConfigProperty[] properties = CxConfigDescription.Describe<DetectionOptions>();
            bool found = false;
            foreach (CxConfigProperty p in properties)
                if (p.Name == "Threshold")
                {
                    found = true;
                    Check(p.Title == "阈值" && p.Category == "检测" && p.Minimum == 0 && p.Maximum == 4095,
                        "Editor metadata incomplete");
                }
            Check(found, "Config editor did not discover property");
        }

        private static CxConfigManager Ready(string root, string folder)
        {
            CxConfigManager manager = new CxConfigManager(Path.Combine(root, folder));
            manager.Register<DetectionOptions>("Vision.Detect");
            manager.Initialize();
            return manager;
        }

        private static void Check(bool condition, string error)
        {
            if (!condition) throw new InvalidOperationException(error);
        }

        private static void Expect<T>(Action action, string error) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new InvalidOperationException(error);
        }
    }
}
