using System;
using System.Collections.Generic;
using System.IO;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Runtime;
using CxVisual.Foundation.Data;
using CxVisual.Professional.Vision;
using CxVisual.Mechanism.Lifecycle;
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
                    // 单次离线RAW检测。使用真实像素计算；不是相机采集、AI推理或硬件ACK。
                    string directory = args[1];
                    string id = args[2];
                    int width = int.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture);
                    int height = int.Parse(args[5], System.Globalization.CultureInfo.InvariantCulture);
                    int bits = int.Parse(args[6], System.Globalization.CultureInfo.InvariantCulture);
                    if (bits != 8 && bits != 16)
                        throw new ArgumentException("RAW pixel bit depth must be 8 or 16");
                    MTCConfigManager config = new MTCConfigManager(directory);
                    config.Register<CxVisionInspectionOptions>(id, requireExisting: true,
                        validator: CxVisionInspectionOptions.Validate);
                    config.Initialize();
                    CxVisionInspectionRuntime runtime = new CxVisionInspectionRuntime(config, id);
                    CxVisionGrayFrame frame = CxVisionRawFrameReader.Read(
                        args[3], width, height, (CxVisionPixelDepth)bits);
                    using (CxVisionInspectionTask task = runtime.BeginInspection())
                    {
                        CxVisionInspectionResult result = task.Inspect(frame);
                        Console.WriteLine("Vision CPU-Threshold-4 ConfigId=" + id +
                            " Revision=" + result.ConfigRevision + " Defects=" + result.Regions.Count +
                            " ThresholdPixels=" + result.ThresholdPixelCount +
                            " Passed=" + result.Passed);
                    }
                    return 0;
                }
                // 版本化配置的独立Runtime进程入口（仅本机可信命令行，不提供网络写入接口）。
                if (verb == "config-status" && args.Length == 2)
                {
                    MVDConfigReleaseReceiver receiver = new MVDConfigReleaseReceiver(args[1]);
                    Console.WriteLine("ActiveReleaseId=" + (receiver.ActiveReleaseId ?? "(none)"));
                    return 0;
                }
                if (verb == "config-check" && args.Length == 4)
                {
                    MVDConfigReleaseReceiver receiver = new MVDConfigReleaseReceiver(args[1]);
                    string json = receiver.ReadActiveConfigJson(args[2]);
                    CxVisual.Foundation.Data.FCMConfigDocumentHeader hdr =
                        CxVisual.Foundation.Data.FCMConfigJsonFile.ReadHeader(json);
                    long expected = long.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture);
                    if (hdr.Revision != expected)
                        throw new InvalidDataException("Expected revision " + expected +
                            ", active revision is " + hdr.Revision);
                    Console.WriteLine("ActiveReleaseId=" + receiver.ActiveReleaseId +
                        " ConfigId=" + hdr.Id + " Revision=" + hdr.Revision);
                    return 0;
                }
                if (verb == "config-stage" && args.Length == 3)
                {
                    Console.WriteLine("Staged=" + new MVDConfigReleaseReceiver(args[1]).Stage(args[2]));
                    return 0;
                }
                if (verb == "config-activate" && args.Length == 3)
                {
                    new MVDConfigReleaseReceiver(args[1]).Activate(args[2]);
                    Console.WriteLine("Activated=" + args[2]);
                    return 0;
                }
                if (verb == "config-rollback" && args.Length == 2)
                {
                    MVDConfigReleaseReceiver receiver = new MVDConfigReleaseReceiver(args[1]);
                    receiver.Rollback();
                    Console.WriteLine("RolledBackTo=" + (receiver.ActiveReleaseId ?? "(none)"));
                    return 0;
                }
                if (verb == "load" && args.Length == 2)
                {
                    CxProjectDocument document = CxDocumentSerializer.Load(args[1]);
                    Console.WriteLine("Project=" + document.Project.Name + ", revision=" + document.Revision);
                    Show(CxSceneProjection.Project(document));
                    return 0;
                }
                Console.WriteLine("Usage: CxVisual.Runtime.Host.exe [demo | vision-demo | save <file.xml> | load <file.xml> | config-status <root> | config-check <root> <id> <revision> | config-stage <root> <package> | config-activate <root> <releaseId> | config-rollback <root> | vision-inspect <configDir> <configId> <rawFile> <width> <height> <8|16>]");
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
            string root = Path.Combine(Path.GetTempPath(), "cxvision-demo-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                FCMConfigKey key = new FCMConfigKey("Vision", "Demo");
                MTCConfigManager manager = MTCConfigManager.Create<CxVisionInspectionOptions>(
                    root, key, CxVisionInspectionOptions.Validate);
                manager.UpdateAndSave<CxVisionInspectionOptions>(key.Id, x =>
                {
                    x.PixelDepth = CxVisionPixelDepth.Bits8;
                    x.GrayThreshold = 200;
                    x.MinDefectArea = 2;
                    x.MaxDefectArea = 100;
                });
                CxVisionInspectionRuntime runtime = new CxVisionInspectionRuntime(manager, key.Id);
                CxVisionGrayFrame frame = new CxVisionGrayFrame(5, 3, CxVisionPixelDepth.Bits8,
                    new ushort[] { 210,210,0,0,0, 210,0,0,0,0, 0,0,0,0,250 });
                using (CxVisionInspectionTask current = runtime.BeginInspection())
                {
                    CxVisionInspectionResult result = current.Inspect(frame);
                    Console.WriteLine("VisionDemo Revision=" + result.ConfigRevision +
                        " Defects=" + result.Regions.Count + " ThresholdPixels=" + result.ThresholdPixelCount);
                    manager.UpdateAndSave<CxVisionInspectionOptions>(key.Id, x => x.GrayThreshold = 230);
                    if (!runtime.RequestApply(MRKConfigApplyPolicy.NextTask))
                        throw new InvalidOperationException("VisionDemo failed staging: " + runtime.LastError);
                    if (current.Inspect(frame).Regions.Count != 1)
                        throw new InvalidOperationException("VisionDemo changed an in-flight inspection");
                }
                using (CxVisionInspectionTask next = runtime.BeginInspection())
                {
                    CxVisionInspectionResult result = next.Inspect(frame);
                    Console.WriteLine("VisionDemo Revision=" + result.ConfigRevision +
                        " Defects=" + result.Regions.Count + " ThresholdPixels=" + result.ThresholdPixelCount);
                    if (result.ConfigRevision != 2 || result.Regions.Count != 0)
                        throw new InvalidOperationException("VisionDemo runtime did not activate the next version");
                }
                Console.WriteLine("VisionDemo PASS CPU threshold pipeline; no camera, AI inference or hardware ACK");
                return 0;
            }
            finally { try { Directory.Delete(root, true); } catch (IOException) { } }
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
