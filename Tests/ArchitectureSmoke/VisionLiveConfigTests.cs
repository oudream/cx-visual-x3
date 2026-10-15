using System;
using System.IO;
using System.Diagnostics;
using System.Linq;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Runtime;
using CxVisual.Professional.Vision;

namespace CxVisual.Tests.ArchitectureSmoke
{
    /// <summary>真实CPU图像处理链的端到端TDD案例，不以模拟Applier替代像素计算。</summary>
    internal static class VisionLiveConfigTests
    {
        internal static void Run()
        {
            string root = Path.Combine(Path.GetTempPath(), "cxvision-live-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                PixelConnectivityAndTaskBoundary(root);
                UnsupportedAiAndFailedApplyPreserveRunningRevision(root);
                SixteenBitImageAndInvalidFrames(root);
                DiskReloadAndSeparateRuntimeProcess(root);
                Console.WriteLine("PASS Vision live config: real 4-neighbor threshold regions, stable in-flight task, NextTask, rejected AI, 8/16-bit, reload and separate Runtime.Host CLI");
            }
            finally { Directory.Delete(root, true); }
        }

        private static MTCConfigManager Setup(string directory, string id, CxVisionPixelDepth bitDepth, int threshold, double minArea)
        {
            MTCConfigManager manager = new MTCConfigManager(directory);
            manager.Register<CxVisionInspectionOptions>(id, validator: CxVisionInspectionOptions.Validate);
            manager.Initialize();
            manager.UpdateAndSave<CxVisionInspectionOptions>(id, x =>
            {
                x.PixelDepth = bitDepth;
                x.GrayThreshold = threshold;
                x.MinDefectArea = minArea;
                x.MaxDefectArea = 100;
                x.UseAi = false;
            });
            return manager;
        }

        private static CxVisionGrayFrame Frame8()
        {
            // 3连通亮点（210）和单个亮点（250）；
            // MinDefectArea=2应只报告3连通区域。
            return new CxVisionGrayFrame(5, 3, CxVisionPixelDepth.Bits8,
                new ushort[] { 210,210,0,0,0,
                               210,0,0,0,0,
                               0,0,0,0,250 });
        }

        private static void PixelConnectivityAndTaskBoundary(string root)
        {
            string id = "Vision.Line01";
            MTCConfigManager manager = Setup(Path.Combine(root, "production"), id,
                CxVisionPixelDepth.Bits8, 200, 2);
            CxVisionInspectionRuntime runtime = new CxVisionInspectionRuntime(manager, id);
            using (CxVisionInspectionTask current = runtime.BeginInspection())
            {
                CxVisionInspectionResult before = current.Inspect(Frame8());
                Check(before.ConfigRevision == 1 && before.ThresholdPixelCount == 4 &&
                    before.Regions.Count == 1 && before.Regions[0].PixelArea == 3 &&
                    before.Regions[0].MinX == 0 && before.Regions[0].MaxY == 1,
                    "Original threshold or 4-neighbor connected components wrong");
                Check(!before.Passed, "Detected defect was marked as passed");
                manager.UpdateAndSave<CxVisionInspectionOptions>(id, x => x.GrayThreshold = 230);
                Check(runtime.RequestApply(MRKConfigApplyPolicy.NextTask), "NextTask rejected");
                Check(runtime.ActiveRevision == 1 && runtime.PendingRevision == 2,
                    "Pending revision changed active job");
                CxVisionInspectionResult again = current.Inspect(Frame8());
                Check(again.ConfigRevision == 1 && again.ThresholdPixelCount == 4 &&
                    again.Regions.Count == 1, "In-flight inspection changed config");
                using (CxVisionInspectionTask parallel = runtime.BeginInspection())
                    Check(parallel.Inspect(Frame8()).ConfigRevision == 1,
                        "Concurrent task saw new config before all prior tasks ended");
            }
            using (CxVisionInspectionTask next = runtime.BeginInspection())
            {
                CxVisionInspectionResult result = next.Inspect(Frame8());
                Check(result.ConfigRevision == 2 && result.ThresholdPixelCount == 1 &&
                    result.Regions.Count == 0 && result.Passed,
                    "NextTask did not use newly verified running threshold");
            }
            Check(runtime.ApplyStatus == MRKConfigApplyStatus.VerifiedByProvider && runtime.ActiveRevision == 2,
                "CPU provider did not report verified activation");
        }

        private static void UnsupportedAiAndFailedApplyPreserveRunningRevision(string root)
        {
            string id = "Vision.AiGuard";
            MTCConfigManager m = Setup(Path.Combine(root, "ai-guard"), id,
                CxVisionPixelDepth.Bits8, 200, 2);
            CxVisionInspectionRuntime runtime = new CxVisionInspectionRuntime(m, id);
            m.UpdateAndSave<CxVisionInspectionOptions>(id, x =>
            {
                x.UseAi = true;
                x.AiModelName = "model-not-installed";
            });
            Check(!runtime.RequestApply(MRKConfigApplyPolicy.Immediate),
                "CPU-only provider silently accepted AI request");
            Check(runtime.ActiveRevision == 1 &&
                runtime.ApplyStatus == MRKConfigApplyStatus.Failed && !runtime.IsFaulted,
                "Failed AI application modified active or faulted healthy processing");
            Check(runtime.LastError != null && runtime.LastError.Contains("AI"),
                "Failure did not identify unsupported AI");
            using (CxVisionInspectionTask t = runtime.BeginInspection())
                Check(t.Inspect(Frame8()).Regions.Count == 1 && t.Inspect(Frame8()).ConfigRevision == 1,
                    "CPU processing did not continue with last healthy configuration");
            m.UpdateAndSave<CxVisionInspectionOptions>(id, x => x.UseAi = false);
            Check(runtime.RequestApply(MRKConfigApplyPolicy.Immediate) &&
                runtime.ActiveRevision == 3 && runtime.ApplyStatus == MRKConfigApplyStatus.VerifiedByProvider,
                "CPU provider could not recover after rejected AI config");
        }

        private static void SixteenBitImageAndInvalidFrames(string root)
        {
            string id = "Vision.Detector16";
            MTCConfigManager m = Setup(Path.Combine(root, "depth16"), id,
                CxVisionPixelDepth.Bits16, 3000, 2);
            CxVisionInspectionRuntime runtime = new CxVisionInspectionRuntime(m, id);
            using (CxVisionInspectionTask t = runtime.BeginInspection())
            {
                CxVisionGrayFrame frame = new CxVisionGrayFrame(3, 2, CxVisionPixelDepth.Bits16,
                    new ushort[] { 3500,4000,0, 0,0,60000 });
                CxVisionInspectionResult result = t.Inspect(frame);
                Check(result.ConfigRevision == 1 && result.ThresholdPixelCount == 3 &&
                    result.Regions.Count == 1 && result.Regions[0].PixelArea == 2,
                    "16bit threshold, area filter or width stride wrong");
                Expect<ArgumentException>(() => t.Inspect(Frame8()),
                    "Runtime allowed a frame whose bit depth differs from effective config");
            }
            Expect<ArgumentException>(() => new CxVisionGrayFrame(2, 1, CxVisionPixelDepth.Bits8,
                new ushort[] { 1, 999 }), "8bit pixel larger than 255 accepted");
            Expect<ArgumentException>(() => new CxVisionGrayFrame(2, 2, CxVisionPixelDepth.Bits16,
                new ushort[] { 1, 2, 3 }), "Invalid image stride/size accepted");
            Expect<ArgumentOutOfRangeException>(() => new CxVisionGrayFrame(50000, 50000,
                CxVisionPixelDepth.Bits16, new ushort[1]), "Overlarge image accepted");
        }

        private static void DiskReloadAndSeparateRuntimeProcess(string root)
        {
            string directory = Path.Combine(root, "external");
            string id = "Vision.External";
            MTCConfigManager first = Setup(directory, id, CxVisionPixelDepth.Bits8, 200, 2);
            MTCConfigManager loaded = new MTCConfigManager(directory);
            loaded.Register<CxVisionInspectionOptions>(id, requireExisting: true,
                validator: CxVisionInspectionOptions.Validate);
            loaded.Initialize();
            using (CxVisionInspectionTask task = new CxVisionInspectionRuntime(loaded, id).BeginInspection())
                Check(task.Inspect(Frame8()).ConfigRevision == 1, "Disk reload changed config revision");

            string raw = Path.Combine(root, "frame.raw");
            File.WriteAllBytes(raw, new byte[] { 210,210,0,0,0,210,0,0,0,0,0,0,0,0,250 });
            string output = RuntimeCli("vision-inspect", directory, id, raw, "5", "3", "8");
            Check(output.Contains("Revision=1") && output.Contains("Defects=1") &&
                output.Contains("ThresholdPixels=4"), "Separate Runtime.Host process did not run actual pixels");
            first.UpdateAndSave<CxVisionInspectionOptions>(id, x => x.GrayThreshold = 230);
            output = RuntimeCli("vision-inspect", directory, id, raw, "5", "3", "8");
            Check(output.Contains("Revision=2") && output.Contains("Defects=0") &&
                output.Contains("ThresholdPixels=1"), "Separate Runtime.Host process did not load updated parameters");
            Expect<InvalidOperationException>(() => RuntimeCli("vision-inspect", directory, id,
                Path.Combine(root, "missing.raw"), "5", "3", "8"),
                "Missing raw file was silently processed");
        }

        private static string RuntimeCli(params string[] argv)
        {
            string exe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CxVisual.Runtime.Host.exe");
            if (!File.Exists(exe)) throw new FileNotFoundException("Runtime.Host is required", exe);
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = string.Join(" ", argv.Select(x => "\"" + x.Replace("\"", "") + "\"")),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (Process p = Process.Start(psi))
            {
                if (!p.WaitForExit(10000)) { p.Kill(); throw new TimeoutException("Vision CLI deadlocked"); }
                string stdout = p.StandardOutput.ReadToEnd();
                string stderr = p.StandardError.ReadToEnd();
                if (p.ExitCode != 0)
                    throw new InvalidOperationException("Vision CLI failed: " + stdout + " " + stderr);
                return stdout;
            }
        }

        private static void Expect<T>(Action action, string msg) where T : Exception
        {
            try { action(); } catch (T) { return; }
            throw new InvalidOperationException(msg);
        }
        private static void Check(bool ok, string msg)
        { if (!ok) throw new InvalidOperationException(msg); }
    }
}
