using System;
using System.IO;
using CxVisual.Foundation.Data;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Lifecycle;
using CxVisual.Mechanism.Runtime;

namespace CxVisual.Tests.ArchitectureSmoke
{
    /// <summary>配置实现只在 Foundation.Data / Mechanism.Data 编译；旧命名空间保持源码兼容。</summary>
    internal static class ConfigConsolidationTests
    {
        private sealed class Sample
        {
            [MPMConfigItem("曝光时间", Min = 1, Max = 2000)]
            public int ExposureMs { get; set; } = 80;
            public string Device { get; set; } = "D1";
        }

        internal static void Run()
        {
            Check(typeof(FCMConfigJsonFile).Assembly.GetName().Name == "CxVisual.Foundation.Data", "FCM owner");
            Check(typeof(FCMConfigKey).Assembly.GetName().Name == "CxVisual.Foundation.Data", "FCM key owner");
            Check(typeof(MPMConfigDescription).Assembly.GetName().Name == "CxVisual.Mechanism.Data", "MPM owner");
            Check(typeof(MTCConfigManager).Assembly.GetName().Name == "CxVisual.Mechanism.Data", "MTC owner");
            Check(typeof(MPMConfigLayeredProfile<Sample>).Assembly.GetName().Name == "CxVisual.Mechanism.Data", "Layer owner");
            Check(typeof(MVDConfigReleaseBuilder).Assembly.GetName().Name == "CxVisual.Mechanism.Data", "Release owner");
            Check(typeof(MRKConfigRuntimeSession<Sample>).Assembly.GetName().Name == "CxVisual.Mechanism.Data", "Runtime owner");
            string root = Path.Combine(Path.GetTempPath(), "CxConfigConsolidation-" + Guid.NewGuid().ToString("N"));
            try
            {
                MTCConfigManager manager = new MTCConfigManager(root);
                manager.Register<Sample>("Machine.Detector");
                manager.Initialize();
                int notifications = 0;
                using (IDisposable subscription = manager.Subscribe<Sample>("Machine.Detector",
                    (value, change) => { if (change.Saved && value.ExposureMs == 120) notifications++; }))
                {
                    manager.UpdateAndSave<Sample>("Machine.Detector", x => x.ExposureMs = 120);
                    Check(manager.Get<Sample>("Machine.Detector").ExposureMs == 120, "Save/reload");
                }
                Check(notifications == 1, "Saved subscription");
                MTCConfigManager reopened = new MTCConfigManager(root);
                reopened.Register<Sample>("Machine.Detector");
                reopened.Initialize();
                Check(reopened.Get<Sample>("Machine.Detector").ExposureMs == 120, "Disk reload");
                Console.WriteLine("PASS config consolidated ownership and legacy behavior");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void Check(bool ok, string why)
        {
            if (!ok) throw new InvalidOperationException("CONFIG_CONSOLIDATION: " + why);
        }
    }
}
