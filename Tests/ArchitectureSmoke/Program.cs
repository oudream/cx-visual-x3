using System;
using System.Collections.Generic;
using System.Reflection;
using System.Diagnostics;
using System.IO;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Visual;
using CxVisual.Tests.FoundationSmoke;
using CxVisual.Tests.ConfigFoundationSmoke;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                if (args != null && args.Length == 2 && args[0] == "--channel-shm-child")
                    return ModuleChannelTransportSmokeTests.RunSharedMemoryChild(args[1]);
                if (args != null && args.Length == 2 && args[0] == "--channel-shm-crash-child")
                    return FTRSharedMemoryCrashTests.RunCrashChild(args[1]);
                if (args != null && args.Length == 3 && args[0] == "--channel-pipe-child")
                    return FTRNamedPipeBulkTests.RunChild(args[1], args[2]);
                if (args != null && args.Length == 2 && args[0] == "--command-pipe-child")
                    return CommandCenterTests.RunPipeChild(args[1]);
                if (Array.IndexOf(args, "--concurrency") >= 0 ||
                    Array.IndexOf(args, "--foundation") >= 0)
                    return FoundationSmokeRunner.Run(args);
                if (Array.IndexOf(args, "--config-foundation") >= 0 ||
                    Array.IndexOf(args, "--config-performance-smoke") >= 0 ||
                    Array.IndexOf(args, "--config-performance-full") >= 0)
                    return ConfigFoundationSmokeRunner.Run(args);
                if (Array.IndexOf(args, "--interaction-stage1") >= 0) { InteractionStageOneTests.Run(); return 0; }
                if (Array.IndexOf(args, "--command-center") >= 0) { CommandCenterTests.Run(); return 0; }
                if (Array.IndexOf(args, "--numerics-benchmark") >= 0) { FNUNumericsBenchmarks.Run(); return 0; }
                if (Array.IndexOf(args, "--numerics") >= 0) { FNUNumericsTests.Run(); return 0; }
                if (Array.IndexOf(args, "--volume") >= 0) { FVOVolumeTests.Run(); MVIVolumeViewTests.Run(); CT3DRenderingTests.Run(); CT3DVolumeLoadTests.Run(); CT3DVoxelMeshTests.Run(); CT3DAnalysisPersistenceTests.Run(); MPWVolumeSourceTests.Run(); return 0; }
                if (Array.IndexOf(args, "--imaging") >= 0) { FIMImagingTests.Run(); MVIImageSessionTests.Run(); MVIImageViewportTests.Run(); MVIImageOverlayTests.Run(); MVIImageDisplayTests.Run(); MVIImageTransformTests.Run(); MVIImageComparisonTests.Run(); RuntimeVisionImageInteropTests.Run(); RuntimeVisionResultTests.Run(); RuntimeVisionArtifactsTests.Run(); return 0; }
                if (Array.IndexOf(args, "--decision") >= 0) { MDCDecisionTests.Run(); return 0; }
                if (Array.IndexOf(args, "--vision-results") >= 0) { RuntimeVisionResultTests.Run(); RuntimeVisionArtifactsTests.Run(); return 0; }
                // Focused B1-B4 vertical gate: real engineering + MRK/MDS Vision.
                // It supplements rather than replaces the complete ArchitectureSmoke suite.
                if (Array.IndexOf(args, "--orchestration-vertical") >= 0) {
                    OrganizationLogicIntegrationTests.Run();
                    RuntimeVisionDataFlowTests.Run();
                    return 0;
                }
                // B4/B6 focused industrial gate: actual MRK cancellation, timeout,
                // breakpoints, resource leases, journal recovery and real MDS Vision.
                // These are the authoritative existing suites, not mock assertions.
                if (Array.IndexOf(args, "--orchestration-industrial") >= 0) {
                    ExecutionControlTests.Run();
                    ExecutionGroupTests.Run();
                    ExecutionPolicyTests.Run();
                    ExecutionJournalTests.Run();
                    ExecutionJournalTests.RunHostProcessFailClosed();
                    RuntimeVisionDataFlowTests.Run();
                    return 0;
                }
                if (Array.IndexOf(args, "--message-center") >= 0) { MessageCenterTests.Run(); return 0; }
                if (Array.IndexOf(args, "--channel-uml-core") >= 0) { ModuleChannelTransportSmokeTests.RunUmlCore(); FTRWebSocketAtomicTests.Run(); FTRMultiClientBudgetTests.Run(); FTRSharedMemoryCrashTests.Run(); return 0; }
                if (Array.IndexOf(args, "--channel-transport") >= 0) { ModuleChannelTransportSmokeTests.Run(); FTRWebSocketAtomicTests.Run(); FTRMultiClientBudgetTests.Run(); FTRSharedMemoryCrashTests.Run(); return 0; }
                if (Array.IndexOf(args, "--channel-bulk") >= 0) { FTRTcpBulkTests.Run(); return 0; }
                if (Array.IndexOf(args, "--channel-bulk-long") >= 0) { FTRTcpBulkTests.RunVolumeScale(); FTRNamedPipeBulkTests.RunVolumeScale(); return 0; }
                if (Array.IndexOf(args, "--runtime-data-soak") >= 0)
                {
                    int index = Array.IndexOf(args, "--runtime-data-soak");
                    int iterations = index + 1 < args.Length
                        ? int.Parse(args[index + 1], System.Globalization.CultureInfo.InvariantCulture)
                        : 20000;
                    RuntimeDataLifecycleTests.RunSoak(iterations);
                    return 0;
                }
                if (Array.IndexOf(args, "--runtime-data-lifecycle") >= 0)
                {
                    RuntimeDataLifecycleTests.Run();
                    RuntimeDataStorageTests.Run();
                    RuntimeDataHardeningTests.Run();
                    RuntimeDataCompilerUnificationTests.Run();
                    Console.WriteLine("PASS MDS RuntimeData lifecycle, storage, hardening and compiler");
                    return 0;
                }
                if (Array.IndexOf(args, "--variables") >= 0) { ModuleFoundationSmokeTests.RunVariableSuite(); return 0; }
                if (Array.IndexOf(args, "--variable-benchmark") >= 0) { VariableThroughputBenchmarks.Run(); return 0; }
                if (Array.IndexOf(args, "--organization-wpf") >= 0) return OrganizationWpfTests.Run();
                if (Array.IndexOf(args, "--organization") >= 0)
                {
                    VisualAuthoringStage1Tests.Run(); VisualRuntimeProjectionTests.Run(); VisualShapeEditingTests.Run(); OrganizationH01Tests.Run(); OrganizationAdversarialTests.Run(); OrganizationH02Tests.Run(); OrganizationH03VisualTests.Run(); OrganizationH03ContractTests.Run(); OrganizationH04Tests.Run(); OrganizationH05Tests.Run(); OrganizationH06Tests.Run(); OrganizationLogicIntegrationTests.Run(); return 0;
                }
                if (args != null && args.Length > 0 && args[0] == "--config-wpf")
                {
                    return ConfigWpfTests.Run();
                }
                if (Array.IndexOf(args, "--organization-benchmark") >= 0 || Array.IndexOf(args, "--organization-stress") >= 0)
                {
                    OrganizationH01Tests.Run();
                    OrganizationPerformanceTests.Run(Array.IndexOf(args, "--organization-stress") >= 0);
                    return 0;
                }
                VisualAuthoringStage1Tests.Run();
                VisualRuntimeProjectionTests.Run();
                VisualShapeEditingTests.Run();
                OrganizationH01Tests.Run();
                OrganizationAdversarialTests.Run();
                OrganizationH02Tests.Run();
                OrganizationH03VisualTests.Run();
                OrganizationH03ContractTests.Run();
                OrganizationH04Tests.Run();
                OrganizationH05Tests.Run();
                OrganizationH06Tests.Run();
                OrganizationLogicIntegrationTests.Run();
                FNUNumericsTests.Run();
                UiIndependentAssemblies();
                ExecutionFlowTests.Run();
                ExecutionCompilerTests.Run();
                MDCDecisionTests.Run();
                RuntimeDeveloperExperienceTests.Run();
                RuntimeDataCompilerUnificationTests.Run();
                ExecutionPlacementTests.Run();
                ExecutionDataScopeTests.Run();
                RuntimeDataLifecycleTests.Run();
                RuntimeDataStorageTests.Run();
                RuntimeVisionDataFlowTests.Run();
                RuntimeVisionResultTests.Run();
                RuntimeVisionArtifactsTests.Run();
                RuntimeVisionImageInteropTests.Run();
                ModuleArchitectureReviewTests.Run();
                InteractionStageOneTests.Run();
                CommandCenterTests.Run();
                MessageCenterTests.Run();
                FVOVolumeTests.Run();
                MVIVolumeViewTests.Run();
                CT3DRenderingTests.Run();
                CT3DVolumeLoadTests.Run(); CT3DVoxelMeshTests.Run(); CT3DAnalysisPersistenceTests.Run();
                MPWVolumeSourceTests.Run();
                FIMImagingTests.Run();
                MVIImageSessionTests.Run();
                MVIImageViewportTests.Run();
                MVIImageOverlayTests.Run();
                MVIImageDisplayTests.Run();
                MVIImageTransformTests.Run();
                MVIImageComparisonTests.Run();
                RuntimeDataHardeningTests.Run();
                ExecutionJournalTests.Run();
                ExecutionControlTests.Run();
                ExecutionPolicyTests.Run();
                ExecutionTriggerTests.Run();
                ExecutionCompensationTests.Run();
                ExecutionGroupTests.Run();
                ExecutionPortTests.Run();
                ExecutionIdentityTests.Run();
                RuntimeSessionTests.Run();
                RuntimeCoordinationTests.Run();
                ConfigurationIntegrationTests.RunAll();
                ConfigV13ExtensionTests.Run();
                ConfigArchitectureCompletionTests.Run();
                ConfigDeveloperExperienceTests.Run();
                HierarchyTransactionPersistence();
                UndoRedo();
                RejectedCommand();
                // Foundation原为独立EXE：默认回归仍以子进程隔离静态日志、线程及句柄。
                if (RunIsolatedFoundationSmoke() != 0) return 1;
                Console.WriteLine("PASS architecture tests (UI-free visual editing and contract checks)");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FAIL architecture test: " + ex);
                return 1;
            }
        }
        private static int RunIsolatedFoundationSmoke()
        {
            string executable = Process.GetCurrentProcess().MainModule.FileName;
            ProcessStartInfo start = new ProcessStartInfo(executable, "--foundation")
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            using (Process child = Process.Start(start))
            {
                if (child == null) throw new InvalidOperationException("Foundation child process failed to start");
                var standardOutput = child.StandardOutput.ReadToEndAsync();
                var standardError = child.StandardError.ReadToEndAsync();
                if (!child.WaitForExit(180000))
                {
                    try { child.Kill(); } catch (InvalidOperationException) { }
                    throw new TimeoutException("Foundation smoke subprocess did not exit");
                }
                Console.Write(standardOutput.GetAwaiter().GetResult());
                Console.Error.Write(standardError.GetAwaiter().GetResult());
                if (child.ExitCode != 0)
                    throw new InvalidOperationException("Foundation smoke subprocess failed: " + child.ExitCode);
                return child.ExitCode;
            }
        }

        private static void Check(bool ok, string message)
        { if (!ok) throw new InvalidOperationException(message); }
        private static void UiIndependentAssemblies()
        {
            Assembly[] assemblies = { typeof(MEMEngineeringSession).Assembly, typeof(MVIVisualEditor).Assembly, typeof(CxVisual.Foundation.Imaging.FIMImageBuffer).Assembly };
            foreach (Assembly assembly in assemblies)
                foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
                {
                    Check(reference.Name != "PresentationFramework" && reference.Name != "PresentationCore"
                        && reference.Name != "System.Windows.Forms" && reference.Name != "WindowsBase",
                        "WPF leaked into core: " + assembly.GetName().Name + "/" + reference.Name);
                }
        }
        private static MEMEngineeringSession CreateSample()
        {
            var session = MEMEngineering.Create("Architecture Sample");
            var page = session.AddPage("Main Page");
            var group = session.AddGroup(page.RootContent, "Drawing Group");
            session.AddRectangle(group, "Detector", 50, 65, 170, 100);
            session.AddRectangle(group, "Source", 300, 180, 125, 75);
            session.ClearHistory();
            return session;
        }
        private static MRCGroup FirstGroup(MEMEngineeringSession session)
        {
            foreach (var child in session.Project.Pages[0].RootContent.VisualItems)
                if (child is MRCGroup) return (MRCGroup)child;
            throw new InvalidOperationException("Missing visual group");
        }
        private static void HierarchyTransactionPersistence()
        {
            var document = CreateSample();
            Check(document.Project.Kind == MEMNodeKind.Project, "Invalid root");
            MEMNode page = document.Project.Children[0];
            Check(page.Kind == MEMNodeKind.Page && page.Children[0].Kind == MEMNodeKind.Content,
                "Incorrect engineering tree");
            MRCGroup group = FirstGroup(document);
            int before = group.Children.Count, revision = document.Revision;
            var temporary = new MRCRectangle(Guid.NewGuid(), "Transactional", 5, 6, 20, 20);
            var unknown = new MRCRectangle(Guid.NewGuid(), "Unknown", 1, 2, 20, 20);
            var rejected = document.Edit("Reject invalid batch", e =>
            {
                e.Add(group, temporary);
                e.Move(unknown, 1, 2);
                return true;
            });
            Check(!rejected.Success && document.Revision == revision && group.Children.Count == before
                && temporary.Parent == null, "Atomic rollback failed");
            var conflict = document.Edit("Reject stale batch", revision + 5, e =>
            {
                e.Add(group, temporary);
                return true;
            });
            Check(!conflict.Success && conflict.ErrorCode == "org.version.conflict", "Optimistic lock missing");
            string path = Path.Combine(Path.GetTempPath(), "cxvisual-" + Guid.NewGuid().ToString("N") + ".cxproj");
            try
            {
                document.SaveAs(path);
                var loaded = MEMEngineering.Open(path);
                Check(loaded.Project.Id == document.Project.Id && loaded.Revision == revision, "Project roundtrip failed");
                Check(FirstGroup(loaded).Children.Count == before, "Project children missing");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }
        private static void UndoRedo()
        {
            var editor = new MVIVisualEditor(CreateSample());
            int count = editor.Scene.Count;
            Check(editor.AddRectangle("New", 10, 20, 30, 40).Success, "Add failed");
            Check(editor.Scene.Count == count + 1 && editor.CanUndo, "Add not recorded");
            Guid id = editor.Scene[editor.Scene.Count - 1].Id;
            Check(editor.Move(id, 90, 100).Success, "Move failed");
            Check(editor.Scene[editor.Scene.Count - 1].Bounds.X == 90, "Projection not updated");
            Check(editor.Undo().Success, "Undo move failed");
            Check(editor.Scene[editor.Scene.Count - 1].Bounds.X == 10, "Undo movement incorrect");
            Check(editor.Undo().Success, "Undo add failed");
            Check(editor.Scene.Count == count, "Undo add missing");
            Check(editor.Redo().Success && editor.Redo().Success, "Redo failed");
            Check(editor.Scene.Count == count + 1, "Redo count incorrect");
            Check(editor.Scene[editor.Scene.Count - 1].Bounds.X == 90, "Redo coordinates incorrect");
            Check(editor.Undo().Success, "Undo before branch failed");
            Check(editor.Move(id, 27, 33).Success, "Branch move failed");
            Check(!editor.CanRedo, "Redo branch should be cleared");
        }
        private static void RejectedCommand()
        {
            var editor = new MVIVisualEditor(CreateSample());
            int revision = editor.Document.Revision;
            var result = editor.Move(Guid.NewGuid(), 20, 30);
            Check(!result.Success, "Unknown element moved");
            Check(editor.Document.Revision == revision, "Rejected command changed revision");
            Check(!editor.CanUndo, "Rejected command entered undo history");
        }
    }
}
