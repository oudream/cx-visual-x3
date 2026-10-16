using System;
using System.Collections.Generic;
using System.IO;
using CxVisual.Mechanism.Core;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Mechanism.Data.Configuration;
using CxVisual.Professional.Vision;
using CxVisual.Mechanism.Visual;
using CxVisual.Foundation.Infrastructure;

namespace CxVisual.Runtime.Host
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                string verb = args.Length == 0 ? "demo" : args[0].ToLowerInvariant();
                if (verb == "demo") return Demo();
                if (verb == "runtime-run" && args.Length == 2)
                    return MRHUnifiedRuntimeHost.RunOne(args[1]);
                if (verb == "runtime-recover" && args.Length == 2)
                    return MRHUnifiedRuntimeHost.Recover(args[1]);
                if (verb == "runtime-serve" && args.Length == 2)
                    return MRHUnifiedRuntimeHost.Serve(args[1]);
                if (verb == "save" && args.Length == 2)
                {
                    CxProjectDocument document = CxDocumentFactory.CreateSample();
                    CxDocumentSerializer.Save(document, args[1]);
                    Console.WriteLine("Saved " + Path.GetFullPath(args[1]));
                    return 0;
                }
                if (verb == "vision-demo" && args.Length == 1) return VisionDemo();
                if (verb == "vision-inspect" && args.Length == 7)
                {
                    // Real pixel processing, with V1.4 configuration pinned for this inspection.
                    int width = int.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture);
                    int height = int.Parse(args[5], System.Globalization.CultureInfo.InvariantCulture);
                    int bits = int.Parse(args[6], System.Globalization.CultureInfo.InvariantCulture);
                    if (bits != 8 && bits != 16)
                        throw new ArgumentException("RAW bit depth must be 8 or 16");
                    MHSConfigService service = new MHSConfigService(args[1]);
                    MHSConfigSection<CxVisionInspectionOptions> config = service.ForMachine(args[2])
                        .Open<CxVisionInspectionOptions>();
                    CxVisionInspectionRuntime runtime = new CxVisionInspectionRuntime(config);
                    CxVisionGrayFrame frame = CxVisionRawFrameReader.Read(args[3], width, height,
                        (CxVisionPixelDepth)bits);
                    using (CxVisionInspectionTask task = runtime.BeginInspection())
                    {
                        CxVisionInspectionResult result = task.Inspect(frame);
                        Console.WriteLine("Vision CPU-Threshold-4 ConfigId=" + args[2] +
                            " Revision=" + result.ConfigRevision + " Defects=" + result.Regions.Count +
                            " ThresholdPixels=" + result.ThresholdPixelCount + " Passed=" + result.Passed);
                    }
                    return 0;
                }
                if (verb == "load" && args.Length == 2)
                {
                    CxProjectDocument document = CxDocumentSerializer.Load(args[1]);
                    Console.WriteLine("Project=" + document.Project.Name + ", revision=" + document.Revision);
                    Show(CxSceneProjection.Project(document));
                    return 0;
                }
                Console.WriteLine("Usage: CxVisual.Runtime.Host.exe [runtime-run <journal> | runtime-recover <journal> | runtime-serve <journal> | demo | vision-demo | save <file.xml> | load <file.xml> | vision-inspect <configRoot> <machineId> <rawFile> <width> <height> <8|16>]");
                return 2;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERROR: " + ex.Message);
                return 1;
            }
        }

        /// <summary>开发者无需机台/SDK的一键Vision闭环；使用真实像素算法及持久化参数。</summary>
        private static int VisionDemo()
        {
            string root = Path.Combine(Path.GetTempPath(), "cxvision-v14-" + Guid.NewGuid().ToString("N"));
            try
            {
                MHSConfigService service = new MHSConfigService(root);
                MHSConfigSection<CxVisionInspectionOptions> config =
                    service.ForMachine("VisionDemo").Open<CxVisionInspectionOptions>();
                using (MHSConfigEdit<CxVisionInspectionOptions> edit = config.BeginEdit("8位检测测试"))
                {
                    edit.WorkingCopy.PixelDepth = CxVisionPixelDepth.Bits8;
                    edit.WorkingCopy.GrayThreshold = 200;
                    edit.WorkingCopy.MinDefectArea = 2;
                    edit.WorkingCopy.MaxDefectArea = 100;
                    MHSConfigCommitResult commit = edit.CommitAsync(System.Threading.CancellationToken.None)
                        .GetAwaiter().GetResult();
                    if (!commit.Success) throw new InvalidOperationException("Vision config: " + commit.ErrorCode);
                }
                config.ApplyPendingAtTaskBoundary();
                CxVisionInspectionRuntime runtime = new CxVisionInspectionRuntime(config);
                CxVisionGrayFrame frame = new CxVisionGrayFrame(5, 3, CxVisionPixelDepth.Bits8,
                    new ushort[] { 210, 210, 0, 0, 0, 210, 0, 0, 0, 0, 0, 0, 0, 0, 250 });
                using (CxVisionInspectionTask current = runtime.BeginInspection())
                {
                    CxVisionInspectionResult result = current.Inspect(frame);
                    Console.WriteLine("VisionDemo Revision=" + result.ConfigRevision +
                        " Defects=" + result.Regions.Count + " ThresholdPixels=" + result.ThresholdPixelCount);
                    if (result.Regions.Count != 1) throw new InvalidOperationException("Expected one connected defect");
                    using (MHSConfigEdit<CxVisionInspectionOptions> edit = config.BeginEdit("下一任务阈值"))
                    {
                        edit.WorkingCopy.GrayThreshold = 230;
                        MHSConfigCommitResult commit = edit.CommitAsync(System.Threading.CancellationToken.None)
                            .GetAwaiter().GetResult();
                        if (!commit.Success) throw new InvalidOperationException("Vision commit: " + commit.ErrorCode);
                    }
                    if (!runtime.RequestApply(FCMConfigApplyPolicy.NextTask))
                        throw new InvalidOperationException("NextTask staging failed");
                    if (current.Inspect(frame).Regions.Count != 1)
                        throw new InvalidOperationException("In-flight detection changed parameters");
                }
                using (CxVisionInspectionTask next = runtime.BeginInspection())
                {
                    CxVisionInspectionResult result = next.Inspect(frame);
                    Console.WriteLine("VisionDemo Revision=" + result.ConfigRevision +
                        " Defects=" + result.Regions.Count + " ThresholdPixels=" + result.ThresholdPixelCount);
                    if (result.Regions.Count != 0) throw new InvalidOperationException("NextTask was not applied");
                }
                Console.WriteLine("VisionDemo PASS CPU threshold pipeline; no camera or hardware ACK");
                return 0;
            }
            finally { try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch (IOException) { } }
        }

        private static int Demo()
        {
            CxProjectDocument document = CxDocumentFactory.CreateSample();
            CxVisualEditor visual = new CxVisualEditor(new CxEngineeringSession(document));
            Console.WriteLine("CxVisual unified engineering demo — no external device or driver");
            using (FLDFileLogSink logger = new FLDFileLogSink(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "runtime-host.log")))
                logger.Write(new FLDLogRecord("RuntimeHost", "Demo", "Engineering demo started"));
            Show(visual.Scene);
            CxCommitResult added = visual.AddRectangle("ROI", 450, 65, 95, 95);
            if (!added.Success) throw new InvalidOperationException(added.ErrorCode);
            CxSceneItem item = visual.Scene[visual.Scene.Count - 1];
            CxCommitResult moved = visual.Move(item.Id, 470, 75);
            CxCommitResult undone = visual.Undo();
            CxCommitResult redone = visual.Redo();
            if (!moved.Success || !undone.Success || !redone.Success)
                throw new InvalidOperationException("Visual edit demo failed");
            Console.WriteLine("Add → Move → Undo → Redo succeeded. Revision=" + document.Revision);
            Show(visual.Scene);
            return 0;
        }
        private static void Show(IList<CxSceneItem> items)
        {
            foreach (CxSceneItem x in items)
                Console.WriteLine("  " + x.Name + " (" + x.Bounds.X + "," + x.Bounds.Y + ") size=" + x.Bounds.Width + "x" + x.Bounds.Height);
        }
    }
}
