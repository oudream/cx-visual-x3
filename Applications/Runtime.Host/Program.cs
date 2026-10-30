using System;
using System.Collections.Generic;
using System.IO;
using CxVisual.Mechanism.Core;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Mechanism.Data.Configuration;
using CxVisual.Professional.Vision;
using CxVisual.Mechanism.Visual;
using CxVisual.Foundation.Infrastructure.Logging;
using CxVisual.Foundation.Infrastructure.Logging.Sinks;

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
                if (verb == "scada-g1-smoke" && args.Length == 1)
                    return MRHScadaSmoke.Run();
                if (verb == "scada-group-serve" && args.Length == 3)
                    return MRHScadaRuntimeGroup.Serve(args[1],args[2]);
                if (verb == "scada-group-serve" && args.Length == 5 && args[3] == "--read-pipe")
                    return MRHScadaRuntimeGroup.Serve(args[1],args[2],args[4]);
                if (verb == "scada-group-serve" && args.Length == 4 && args[3] == "--recover")
                    return MRHScadaGroupRecoverySupervisor.Serve(args[1],args[2],null);
                if (verb == "scada-group-serve" && args.Length == 6 &&
                    args[3] == "--recover" && args[4] == "--read-pipe")
                    return MRHScadaGroupRecoverySupervisor.Serve(args[1],args[2],args[5]);
                if (verb == "scada-group-serve" && args.Length == 4 &&
                    args[3] == "--recover-device")
                    return MRHScadaIsolatedRecoverySupervisor.Serve(args[1],args[2],null);
                if (verb == "scada-group-serve" && args.Length == 6 &&
                    args[3] == "--recover-device" && args[4] == "--read-pipe")
                    return MRHScadaIsolatedRecoverySupervisor.Serve(args[1],args[2],args[5]);
                if (verb == "scada-serve" && args.Length >= 7 && args.Length <= 10)
                {
                    bool contiguous=false,serviceMode=false,recover=false;
                    for(int i=7;i<args.Length;i++)
                    {
                        if(args[i]=="--contiguous"&&!contiguous)contiguous=true;
                        else if(args[i]=="--service"&&!serviceMode)serviceMode=true;
                        else if(args[i]=="--recover"&&!recover)recover=true;
                        else throw new ArgumentException("未知或重复的SCADA运行选项: "+args[i]);
                    }
                    if(recover&&!serviceMode)
                        throw new ArgumentException("--recover必须搭配--service");
                    return MRHScadaRuntime.Serve(args[1],args[2],args[3],
                        args[4],args[5],args[6],contiguous,serviceMode,recover);
                }
                if (verb == "release-trusted-smoke" && args.Length == 2)
                    return MRHTrustedReleaseSmoke.Run(args[1]);
                if (verb == "release-ledger-smoke" && args.Length == 2)
                    return ReleaseLedgerSmoke(args[1],"commission");
                if (verb == "release-ledger-resume" && args.Length == 2)
                    return ReleaseLedgerSmoke(args[1],"resume");
                if (verb == "release-ledger-corrupt-check" && args.Length == 2)
                    return ReleaseLedgerSmoke(args[1],"corrupt");
                if (verb == "save" && args.Length == 2)
                {
                    MEMEngineeringSession document = CreateSample();
                    document.SaveAs(args[1]);
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
                    MEMEngineeringSession document = MEMEngineering.Open(args[1]);
                    Console.WriteLine("Project=" + document.Project.Name + ", revision=" + document.Revision);
                    Show(MVISceneProjection.Project(document));
                    return 0;
                }
                Console.WriteLine("Usage: CxVisual.Runtime.Host.exe [runtime-run <journal> | runtime-recover <journal> | runtime-serve <journal> | scada-group-serve <project.cxproj> <mhsRoot> [--recover|--recover-device] [--read-pipe <pipeName>] | scada-serve <project.cxproj> <mhsRoot> <MEMDeviceGuid> <connectorModuleId> <scopeId> <intervalMs> [--contiguous] [--service] [--recover] | demo | vision-demo | save <file.cxproj> | load <file.cxproj> | vision-inspect <configRoot> <machineId> <rawFile> <width> <height> <8|16> | release-trusted-smoke <directory> | release-ledger-smoke <path> | release-ledger-resume <path> | release-ledger-corrupt-check <path>]");
                return 2;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERROR: " + ex.Message);
                return 1;
            }
        }


        /// <summary>Local CI-only Host ledger test. No production key or
        /// authorization credential is used or created by this command.</summary>
        private static int ReleaseLedgerSmoke(string path,string phase)
        {
            const string scope="ci-station-42";
            Func<string,bool> authorize=principal=>principal=="ci-release-officer";
            if(phase=="commission")
                MRHFileTrustedReleaseAdmission.CommissionNew(path,scope,1);
            if(phase=="corrupt")
            {
                bool denied=false;
                try {new MRHFileTrustedReleaseAdmission(path,scope,authorize);}
                catch(InvalidDataException) {denied=true;}
                if(!denied)throw new InvalidOperationException(
                    "Corrupted trusted release journal was accepted");
                Console.WriteLine("PASS release ledger rejects torn record");
                return 0;
            }
            var ledger=new MRHFileTrustedReleaseAdmission(path,scope,authorize);
            byte[] digest=new byte[32];
            digest[0]=42;
            if(ledger.TryAuthorizeAndReserve("observer",scope,2,digest) ||
                ledger.TryAuthorizeAndReserve("ci-release-officer","other",2,digest))
                throw new InvalidOperationException("Unauthorized release was reserved");
            if(phase=="commission")
            {
                if(!ledger.TryAuthorizeAndReserve("ci-release-officer",scope,2,digest)||
                    ledger.TryAuthorizeAndReserve("ci-release-officer",scope,2,digest))
                    throw new InvalidOperationException("First release watermark invalid");
            }
            else if(phase=="resume")
            {
                if(ledger.TryAuthorizeAndReserve("ci-release-officer",scope,2,digest)||
                    !ledger.TryAuthorizeAndReserve("ci-release-officer",scope,3,digest)||
                    ledger.TryAuthorizeAndReserve("ci-release-officer",scope,3,digest))
                    throw new InvalidOperationException("Restarted Host did not preserve watermark");
            }
            else throw new ArgumentException("Unknown ledger smoke phase");
            Console.WriteLine("PASS Host release ledger " + phase);
            return 0;
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

        private static MEMEngineeringSession CreateSample()
        {
            var session = MEMEngineering.Create("Demo Project");
            var page = session.AddPage("Main Page");
            var group = session.AddGroup(page.RootContent, "Drawing Group");
            session.AddRectangle(group, "Detector", 50, 65, 170, 100);
            session.AddRectangle(group, "Source", 300, 180, 125, 75);
            session.ClearHistory();
            return session;
        }
        private static int Demo()
        {
            MEMEngineeringSession document = CreateSample();
            MVIVisualEditor visual = new MVIVisualEditor(document);
            Console.WriteLine("CxVisual unified engineering demo — no external device or driver");
            using (FLDFileLogSink logger = new FLDFileLogSink(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "runtime-host.log")))
                logger.Write(new FLDLogRecord("RuntimeHost", "Demo", "Engineering demo started"));
            Show(visual.Scene);
            MTCEditResult<bool> added = visual.AddRectangle("ROI", 450, 65, 95, 95);
            if (!added.Success) throw new InvalidOperationException(added.ErrorCode);
            MVISceneItem item = visual.Scene[visual.Scene.Count - 1];
            MTCEditResult<bool> moved = visual.Move(item.Id, 470, 75);
            MTCEditResult<bool> undone = visual.Undo();
            MTCEditResult<bool> redone = visual.Redo();
            if (!moved.Success || !undone.Success || !redone.Success)
                throw new InvalidOperationException("Visual edit demo failed");
            Console.WriteLine("Add → Move → Undo → Redo succeeded. Revision=" + document.Revision);
            Show(visual.Scene);
            return 0;
        }
        private static void Show(IList<MVISceneItem> items)
        {
            foreach (MVISceneItem x in items)
                Console.WriteLine("  " + x.Name + " (" + x.Bounds.X + "," + x.Bounds.Y + ") size=" + x.Bounds.Width + "x" + x.Bounds.Height);
        }
    }
}
