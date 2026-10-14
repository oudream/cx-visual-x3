using System;
using System.IO;
using System.Diagnostics;
using System.Linq;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Runtime;
using CxVisual.Mechanism.Lifecycle;
using CxVisual.Professional.Vision;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class ConfigL2L4Tests
    {
        private struct ParamPair
        {
            public int A;
            public int B;
        }

        internal static void Run()
        {
            string root = Path.Combine(Path.GetTempPath(), "cxvisual-l2l4-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                LayerInheritanceAndSourceTrace(root);
                RestoreBackupAndMigration(root);
                TaskBoundariesAndSafeApplication(root);
                AtomicRuntimeValuesAreConsistent();
                ReleaseDistributionAndRollback(root);
                Console.WriteLine("PASS Config L2-L4: layers/templates/recovery/migration, task snapshot, atomic slot, verified package and multi-target rollback");
            }
            finally { Directory.Delete(root, true); }
        }

        private static CxConfigManager Setup(string root)
        {
            CxConfigManager manager = new CxConfigManager(root);
            manager.Register<CxVisionInspectionOptions>("Vision.Live", validator: CxVisionInspectionOptions.Validate);
            manager.Initialize();
            return manager;
        }

        private static void LayerInheritanceAndSourceTrace(string root)
        {
            string folder = Path.Combine(root, "layers");
            CxConfigManager m = new CxConfigManager(folder);
            CxConfigLayeredProfile<CxVisionInspectionOptions> profile =
                CxConfigLayeredProfile<CxVisionInspectionOptions>.Register(
                    m, "Vision.Default", CxVisionInspectionOptions.Validate,
                    new CxConfigLayerSpec("Vision.Template", 10),
                    new CxConfigLayerSpec("Vision.Machine", 20),
                    new CxConfigLayerSpec("Vision.Recipe", 30));
            m.Initialize();
            profile.UpdateLayer("Vision.Template", x => x.GrayThreshold = 1500);
            profile.SetTemplate("Vision.Machine", "Vision.Template");
            profile.UpdateLayer("Vision.Machine", x => x.GrayThreshold = 1700);
            profile.UpdateLayer("Vision.Recipe", x => x.MinDefectArea = 60);
            CxResolvedConfig<CxVisionInspectionOptions> cfg = profile.Resolve();
            Check(cfg.Value.GrayThreshold == 1700 && cfg.Value.MinDefectArea == 60, "Layers did not apply");
            Check(cfg.Origins["GrayThreshold"] == "Vision.Machine" &&
                  cfg.Origins["MinDefectArea"] == "Vision.Recipe", "Origin trail missing");
            Expect<InvalidOperationException>(() => profile.SetTemplate("Vision.Template", "Vision.Machine"),
                "Template cycle accepted");
            profile.ClearProperty("Vision.Machine", "GrayThreshold");
            Check(profile.Resolve().Value.GrayThreshold == 1500, "Layer clear failed to fall back to template");
            CxConfigManager reloaded = new CxConfigManager(folder);
            CxConfigLayeredProfile<CxVisionInspectionOptions> reopened =
                CxConfigLayeredProfile<CxVisionInspectionOptions>.Register(
                    reloaded, "Vision.Default", CxVisionInspectionOptions.Validate,
                    new CxConfigLayerSpec("Vision.Template", 10),
                    new CxConfigLayerSpec("Vision.Machine", 20),
                    new CxConfigLayerSpec("Vision.Recipe", 30));
            reloaded.Initialize();
            Check(reopened.Resolve().Value.MinDefectArea == 60 &&
                  reopened.Resolve().Value.GrayThreshold == 1500, "Layer/template reload failed");
            Expect<ArgumentException>(() => reopened.UpdateLayer("Vision.Recipe",
                x => { x.PixelDepth = CxVisionPixelDepth.Bits8; x.GrayThreshold = 300; }),
                "Effective schema/cross-field validation skipped");
        }

        private static void RestoreBackupAndMigration(string root)
        {
            string folder = Path.Combine(root, "restore");
            CxConfigManager m = Setup(folder);
            m.UpdateAndSave<CxVisionInspectionOptions>("Vision.Live", x => x.GrayThreshold = 1000);
            m.UpdateAndSave<CxVisionInspectionOptions>("Vision.Live", x => x.GrayThreshold = 2000);
            m.RestoreBackup<CxVisionInspectionOptions>("Vision.Live");
            Check(m.Get<CxVisionInspectionOptions>("Vision.Live").GrayThreshold == 1000 &&
                m.GetRevision("Vision.Live") == 3, "Restore must commit a new monotonic revision");
            CxConfigManager reloaded = Setup(folder);
            Check(reloaded.Get<CxVisionInspectionOptions>("Vision.Live").GrayThreshold == 1000,
                "Recovered version not persisted");

            string migrateDir = Path.Combine(root, "migration");
            CxConfigManager old = new CxConfigManager(migrateDir);
            old.Register<CxVisionInspectionOptions>("Vision.Legacy", schemaVersion: 1);
            old.Initialize();
            CxConfigManager upgraded = new CxConfigManager(migrateDir);
            upgraded.Register<CxVisionInspectionOptions>("Vision.Legacy", schemaVersion: 2,
                migrate: (schema, raw) =>
                {
                    Check(schema == 1 && raw.Contains("Vision.Legacy"), "Migration source wrong");
                    return new CxVisionInspectionOptions { GrayThreshold = 1200 };
                });
            upgraded.Initialize();
            Check(upgraded.Get<CxVisionInspectionOptions>("Vision.Legacy").GrayThreshold == 1200 &&
                upgraded.GetRevision("Vision.Legacy") == 1, "Explicit schema migration failed");
            Check(File.ReadAllText(Path.Combine(migrateDir, "Vision.Legacy.json.bak")).Contains("SchemaVersion\":1"),
                "Old schema must be retained in backup");
        }

        private static void TaskBoundariesAndSafeApplication(string root)
        {
            CxConfigManager m = Setup(Path.Combine(root, "runtime"));
            CxConfigRuntimeSession<CxVisionInspectionOptions> run =
                new CxConfigRuntimeSession<CxVisionInspectionOptions>(m, "Vision.Live",
                    cfg => cfg.MinDefectArea > 300 ? "Equipment busy" : null);
            CxConfigTaskSnapshot<CxVisionInspectionOptions> first = run.BeginTask();
            Check(first.Value.GrayThreshold == 1800 && first.Revision == 0, "Task did not freeze initial version");
            m.UpdateAndSave<CxVisionInspectionOptions>("Vision.Live", x => x.GrayThreshold = 2200);
            Check(run.RequestApply(CxConfigApplyPolicy.NextTask), "Valid config rejected");
            Check(run.ActiveRevision == 0, "Pending config applied mid-task");
            CxConfigTaskSnapshot<CxVisionInspectionOptions> second = run.BeginTask();
            Check(second.Revision == 0 && first.Value.GrayThreshold == 1800, "Active task changed unexpectedly");
            second.Dispose();
            first.Dispose();
            using (CxConfigTaskSnapshot<CxVisionInspectionOptions> third = run.BeginTask())
                Check(third.Revision == 1 && third.Value.GrayThreshold == 2200,
                    "NextTask boundary did not apply prepared config");
            m.UpdateAndSave<CxVisionInspectionOptions>("Vision.Live", x => x.MinDefectArea = 500);
            Check(!run.RequestApply(CxConfigApplyPolicy.Immediate) && run.ActiveRevision == 1,
                "Failed professional apply should retain active revision");
            m.UpdateAndSave<CxVisionInspectionOptions>("Vision.Live", x => x.MinDefectArea = 20);
            Check(run.RequestApply(CxConfigApplyPolicy.RestartSession), "RestartSession request failed");
            Check(run.ActiveRevision == 1, "RestartSession policy applied too soon");
            Check(run.OnSessionRestarted() && run.ActiveRevision == m.GetRevision("Vision.Live"),
                "RestartSession apply failed");
        }

        private static void AtomicRuntimeValuesAreConsistent()
        {
            CxConfigAtomicSlot<ParamPair> p = new CxConfigAtomicSlot<ParamPair>(new ParamPair { A = 1, B = 2 }, 0);
            for (int i = 1; i < 10000; ++i)
            {
                p.Publish(new ParamPair { A = i, B = i * 2 }, i);
                CxConfigAtomicSnapshot<ParamPair> snapshot = p.Read();
                Check(snapshot.Revision == snapshot.Value.A && snapshot.Value.B == snapshot.Value.A * 2,
                    "Atomic multi-field snapshot torn");
            }
        }

        private static void ReleaseDistributionAndRollback(string root)
        {
            string confRoot = Path.Combine(root, "release-config");
            CxConfigManager m = Setup(confRoot);
            CxConfigReleasePackage first = CxConfigReleaseBuilder.Build(m, new[] { "Vision.Live" },
                Path.Combine(root, "packages"), "release-001");
            CxConfigReleaseReceiver a = new CxConfigReleaseReceiver(Path.Combine(root, "runtimeA"));
            CxConfigReleaseReceiver b = new CxConfigReleaseReceiver(Path.Combine(root, "runtimeB"));
            CxConfigPublishReport r1 = CxConfigPublisher.Publish(first.Path, a, b);
            Check(r1.Success && a.ActiveReleaseId == "release-001" && b.ActiveReleaseId == "release-001",
                "Release 001 was not staged and activated on two targets");
            Check(RuntimeCli("config-status", Path.Combine(root, "runtimeA")).Contains("release-001"),
                "Runtime.Host separate process did not discover active release");
            Check(RuntimeCli("config-check", Path.Combine(root, "runtimeA"), "Vision.Live", "0")
                .Contains("Revision=0"), "Runtime.Host separate process failed to read revision");

            m.UpdateAndSave<CxVisionInspectionOptions>("Vision.Live", x => x.GrayThreshold = 2200);
            CxConfigReleasePackage second = CxConfigReleaseBuilder.Build(m, new[] { "Vision.Live" },
                Path.Combine(root, "packages"), "release-002");
            CxConfigPublishReport r2 = CxConfigPublisher.Publish(second.Path, a, b);
            Check(r2.Success && a.ActiveReleaseId == "release-002", "Release 002 activation failed");
            Check(a.ReadActiveConfigJson("Vision.Live").Contains("2200"),
                "Activated Runtime package does not contain new configuration");
            a.Rollback();
            Check(a.ActiveReleaseId == "release-001" &&
                a.ReadActiveConfigJson("Vision.Live").Contains("1800"), "Rollback to previous package failed");

            string tamperFile = Path.Combine(second.Path, "payload", "Vision.Live.json");
            File.AppendAllText(tamperFile, "altered");
            Expect<InvalidDataException>(() => b.Stage(second.Path), "Tampered release accepted");
            Check(b.ActiveReleaseId == "release-002", "Invalid stage damaged active release");

            CxConfigReleaseReceiver bad = new CxConfigReleaseReceiver(Path.Combine(root, "runtimeC"));
            CxConfigPublishReport fail = CxConfigPublisher.Publish(second.Path, a, bad);
            Check(!fail.Success && a.ActiveReleaseId == "release-001", "Failed publish changed previous active version");
        }


        private static string RuntimeCli(params string[] arguments)
        {
            string exe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CxVisual.Runtime.Host.exe");
            if (!File.Exists(exe)) throw new FileNotFoundException("Runtime.Host is required for L4 process-level test", exe);
            ProcessStartInfo start = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = string.Join(" ", arguments.Select(x => "\"" + x.Replace("\"", "") + "\"")),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (Process process = Process.Start(start))
            {
                if (!process.WaitForExit(10000))
                {
                    process.Kill();
                    throw new TimeoutException("Runtime.Host CLI did not exit");
                }
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                if (process.ExitCode != 0)
                    throw new InvalidOperationException("Runtime.Host CLI failed: " + error + " " + output);
                return output;
            }
        }

        private static void Expect<T>(Action action, string why) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new InvalidOperationException(why);
        }
        private static void Check(bool ok, string why)
        {
            if (!ok) throw new InvalidOperationException(why);
        }
    }
}
