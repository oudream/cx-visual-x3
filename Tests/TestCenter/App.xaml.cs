using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using AvalonDock.Layout;
using CxVisual.TestCenter.Cases;
using CxVisual.TestCenter.Core;
using CxVisual.TestCenter.Execution;
using CxVisual.TestCenter.Reports;
using CxVisual.TestCenter.Models;
using CxVisual.TestCenter.Fixtures;
using CxVisual.TestCenter.NUnitIntegration;

namespace CxVisual.TestCenter.UI
{
    public partial class TCTApp : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            if (e.Args.Length == 1 && e.Args[0] == "--variable-workspace-smoke")
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                try
                {
                    TCTManualVariableFrame.VerifyWorkspace();
                    Console.WriteLine("PASS MDS Variable WPF Workspace");
                    Shutdown(0);
                }
                catch (Exception error)
                {
                    System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                        "CxVisual-Variable-Workspace-Error.txt"), error.ToString());
                    Shutdown(3);
                }
                return;
            }
            if (e.Args.Length == 1 && e.Args[0] == "--data-lifecycle-ui-smoke")
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                try
                {
                    TCTManualDataLifecycleFrame.VerifyWorkspace();
                    Console.WriteLine("PASS MDS five-dimensional lifecycle WPF operations");
                    Shutdown(0);
                }
                catch (Exception error)
                {
                    File.WriteAllText(Path.Combine(Path.GetTempPath(),
                        "CxVisual-Data-Lifecycle-WPF-Error.txt"), error.ToString());
                    Shutdown(3);
                }
                return;
            }
            if (e.Args.Length == 1 && e.Args[0] == "--volume-gpu-smoke")
            {
                ShutdownMode=ShutdownMode.OnExplicitShutdown;
                try
                {
                    TCTVolumeGpuHardwareSmoke.Run();
                    Shutdown(0);
                }
                catch(Exception error)
                {
                    File.WriteAllText(Path.Combine(Path.GetTempPath(),
                        "CxVisual-GPU-Hardware-Error.txt"),error.ToString());
                    Shutdown(3);
                }
                return;
            }
            if (e.Args.Length == 1 && e.Args[0] == "--volume-viewer-smoke")
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                try
                {
                    TCTVolumeViewerSmoke.Run();
                    Shutdown(0);
                }
                catch (Exception error)
                {
                    File.WriteAllText(Path.Combine(Path.GetTempPath(),
                        "CxVisual-Volume-Viewer-Error.txt"), error.ToString());
                    Shutdown(2);
                }
                return;
            }
            // 专项真实WPF图元测试入口：公共按钮→MVI→MEM→Undo，失败退出码非零。
            if (e.Args.Length == 1 && e.Args[0] == "--graphics-viewer-smoke")
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                try { TCTGraphicsFrameSmoke.Run(); Shutdown(0); }
                catch (Exception error)
                {
                    File.WriteAllText(Path.Combine(Path.GetTempPath(),
                        "CxVisual-Graphics-Viewer-Error.txt"),error.ToString());
                    Shutdown(2);
                }
                return;
            }
            if (e.Args.Length == 1 && e.Args[0] == "--imaging-viewer-smoke")
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                try
                {
                    TCTImageViewerSmoke.Run();
                    Shutdown(0);
                }
                catch (Exception error)
                {
                    File.WriteAllText(Path.Combine(Path.GetTempPath(),
                        "CxVisual-Imaging-Viewer-Error.txt"), error.ToString());
                    Shutdown(2);
                }
                return;
            }
            if (e.Args.Length == 0)
            {
                TCTMainWindow window = new TCTMainWindow();
                MainWindow = window;
                window.Show();
                return;
            }
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            int exit = 2;
            try
            {
                string command = e.Args[0].ToLowerInvariant();
                // The default run discovers both frameworks. A native Worker must not load
                // NUnit Engine for all ordinary TCT cases; only NUnit attempts need it.
                bool requiresNUnit = command == "--run" || command == "--list" ||
                    (command == "--worker" && e.Args.Length > 1 &&
                     e.Args[1].StartsWith("NUNIT.", StringComparison.Ordinal));
                TCTTestCatalog catalog = TCTRegistration.CreateCatalog(requiresNUnit);
                if (command == "--list")
                {
                    if (e.Args.Length != 3 || e.Args[1] != "--report")
                        throw new ArgumentException("--list --report <catalog.json>");
                    var cases = catalog.Cases.OrderBy(x => x.Descriptor.Id, StringComparer.Ordinal)
                        .Select(test =>
                        {
                            ITCTParameterizedCaseIdentity row = test as ITCTParameterizedCaseIdentity;
                            ITCTTestDefinitionFingerprint versioned = test as ITCTTestDefinitionFingerprint;
                            return new {
                                id = test.Descriptor.Id,
                                definitionId = row == null ? test.Descriptor.Id : row.DefinitionId,
                                caseId = row == null ? test.Descriptor.Id : row.CaseId,
                                rowKey = row == null ? "default" : row.RowKey,
                                fingerprint = versioned == null ? "" : versioned.DefinitionFingerprint,
                                aspect = test.Descriptor.Aspect.ToString(),
                                group = test.Descriptor.Group,
                                capabilityId = test.Descriptor.CapabilityId
                            };
                        }).ToArray();
                    string output = Path.GetFullPath(e.Args[2]);
                    Directory.CreateDirectory(Path.GetDirectoryName(output));
                    var serializer = new System.Web.Script.Serialization.JavaScriptSerializer {
                        MaxJsonLength = int.MaxValue };
                    string temporary = output + ".partial";
                    File.WriteAllText(temporary, serializer.Serialize(new {
                        schema = 1, count = cases.Length, cases = cases }), System.Text.Encoding.UTF8);
                    if (File.Exists(output)) File.Delete(output);
                    File.Move(temporary, output);
                    exit = cases.Length == 0 ? 1 : 0;
                }
                else if (command == "--nunit-probe")
                {
                    if (e.Args.Length < 2 || e.Args.Length > 3 ||
                        (e.Args.Length == 3 && e.Args[2] != "--negative"))
                        throw new ArgumentException("--nunit-probe <result.xml> [--negative]");
                    string probeReport = e.Args[1];
                    bool negativeControl = e.Args.Length == 3;
                    // NUnit async tests must not execute synchronously on the WPF STA dispatcher.
                    exit = await System.Threading.Tasks.Task.Run(() =>
                        TCTNUnitEngineProbe.Execute(probeReport, negativeControl));
                }
                else if (command == "--worker")
                {
                    if (e.Args.Length != 4 && e.Args.Length != 7 &&
                        e.Args.Length != 8 && e.Args.Length != 9) throw new ArgumentException("--worker <id> <result-file> <seed> [runId attemptId iteration]");
                    ITCTTestCase test = catalog.Cases.SingleOrDefault(x => x.Descriptor.Id == e.Args[1]);
                    if (test == null) throw new InvalidOperationException("Unknown worker test ID");
                    if (test is ITCTTestDefinitionFingerprint && e.Args.Length != 9)
                        throw new InvalidDataException("NUnit worker requires parent definition fingerprint");
                    if (e.Args.Length == 9)
                    {
                        ITCTTestDefinitionFingerprint fingerprinted =
                            test as ITCTTestDefinitionFingerprint;
                        if (fingerprinted == null ||
                            !string.Equals(fingerprinted.DefinitionFingerprint, e.Args[8],
                                StringComparison.Ordinal))
                            throw new InvalidDataException("NUnit Worker/Parent definition fingerprint mismatch");
                    }
                    int seed = int.Parse(e.Args[3], CultureInfo.InvariantCulture);
                    TCTTestAttempt attempt = e.Args.Length >= 7 ?
                        new TCTTestAttempt(e.Args[4], test.Descriptor.Id,
                            int.Parse(e.Args[6], CultureInfo.InvariantCulture), seed, e.Args[5]) :
                        new TCTTestAttempt("AdHoc", test.Descriptor.Id, 0, seed);
                    TCTTestArtifactStore store = e.Args.Length >= 7 ?
                        new TCTTestArtifactStore(TCTTestArtifactStore.RunRoot(attempt.RunId)) : null;
                    TCTTestEventJournal events = e.Args.Length >= 8 ? new TCTTestEventJournal(e.Args[7]) : null;
                    TCTTestCaseResult result = await TCTTestRunner.RunOneAsync(test,
                        CancellationToken.None, seed, attempt, store, events == null ? null :
                            new Action<TCTTestEvent>(events.Write));
                    TCTWireCodec.Write(e.Args[2], result);
                    exit = 0; // 结果状态在结构化文件内，不能用退出码代替断言状态。
                }
                else if (command == "--connector-ui-automation")
                {
                    if (e.Args.Length != 1)
                        throw new ArgumentException("--connector-ui-automation takes no arguments");
                    TCTMainWindow window = new TCTMainWindow();
                    MainWindow = window;
                    try
                    {
                        await TCTWpfAutomationHarness.RunConnectorAsync(window);
                        exit = 0;
                    }
                    finally { window.Close(); }
                }
                else if (command == "--command-ui-automation")
                {
                    if (e.Args.Length != 1)
                        throw new ArgumentException("--command-ui-automation takes no arguments");
                    TCTMainWindow window = new TCTMainWindow();
                    MainWindow = window;
                    try
                    {
                        await TCTWpfAutomationHarness.RunCommandAsync(window);
                        exit = 0;
                    }
                    finally { window.Close(); }
                }
                else if (command == "--command-ui-smoke")
                {
                    if (e.Args.Length != 1)
                        throw new ArgumentException("--command-ui-smoke takes no arguments");
                    TCTMainWindow window = new TCTMainWindow();
                    MainWindow = window;
                    try
                    {
                        TCTWorkspaceDescriptor created = window.CreateWorkspaceInstance("manual.command");
                        if (created == null || !window.DocumentHost.HasWorkspace(created.WorkspaceId))
                            throw new InvalidOperationException("命令工作区未按正式树注册并打开");
                        System.Windows.Controls.TabControl tabs =
                            window.DocumentHost.Content as System.Windows.Controls.TabControl;
                        TCTManualCommandFrame frame =
                            (tabs.SelectedItem as System.Windows.Controls.TabItem)?.Content
                                as TCTManualCommandFrame;
                        if (frame == null || !frame.Commands.Any(x => x.Id == "command.invoke"))
                            throw new InvalidOperationException("命令工作区缺失真实操作入口");
                        await TCTManualCommandFrame.VerifyLocalAsync();
                        Console.WriteLine("PASS TCT.MANUAL.COMMAND Registry/Workspace/FDC/MRK/MDS/WPF");
                        exit = 0;
                    }
                    finally { window.Close(); }
                }
                else if (command == "--message-ui-automation")
                {
                    if (e.Args.Length != 1)
                        throw new ArgumentException("--message-ui-automation takes no arguments");
                    TCTMainWindow window = new TCTMainWindow();
                    MainWindow = window;
                    try
                    {
                        await TCTWpfAutomationHarness.RunMessageAsync(window);
                        exit = 0;
                    }
                    finally { window.Close(); }
                }
                else if (command == "--ui-automation")
                {
                    if (e.Args.Length != 1)
                        throw new ArgumentException("--ui-automation takes no arguments");
                    TCTMainWindow window = new TCTMainWindow();
                    MainWindow = window;
                    try
                    {
                        await TCTWpfAutomationHarness.RunAsync(window);
                        exit = 0;
                    }
                    finally { window.Close(); }
                }
                else if (command == "--ui-smoke")
                {
                    if (e.Args.Length != 1) throw new ArgumentException("--ui-smoke takes no arguments");
                    // Real WPF elements, real registered test, and real TCT observations;
                    // not a mock screenshot or substitute for user interaction testing.
                    TCTMainWindow window = new TCTMainWindow();
                    try
                    {
                        ITCTTestCase test = catalog.Cases.Single(x => x.Descriptor.Id == "TCT.CORE.001");
                        TCTTestAttempt attempt = new TCTTestAttempt(Guid.NewGuid().ToString("N"),
                            test.Descriptor.Id, 0, 20261016);
                        System.Collections.Generic.List<TCTTestEvent> events =
                            new System.Collections.Generic.List<TCTTestEvent>();
                        TCTTestCaseResult actual = await TCTTestRunner.RunOneAsync(test,
                            CancellationToken.None, 20261016, attempt, null, events.Add);
                        if (actual.Status != TCTTestStatus.Passed || events.Count < 3)
                            throw new InvalidOperationException("WPF smoke actual test/stream is not successful");
                        window.Results.Add(actual);
                        window.Inspector.ShowResult(actual);
                        window.DocumentHost.ShowResult(actual);
                        foreach (TCTTestEvent evt in events) window.Timeline.AddEvent(evt);
                        if (window.Timeline.EventCount != events.Count)
                            throw new InvalidOperationException("WPF Timeline did not bind real event count");
                        TCTTestAttempt second = new TCTTestAttempt(Guid.NewGuid().ToString("N"),
                            test.Descriptor.Id, 1, 20261016);
                        System.Collections.Generic.List<TCTTestEvent> secondEvents =
                            new System.Collections.Generic.List<TCTTestEvent>();
                        TCTTestCaseResult secondResult = await TCTTestRunner.RunOneAsync(test,
                            CancellationToken.None, 20261016, second, null, secondEvents.Add);
                        if (secondResult.Status != TCTTestStatus.Passed || secondEvents.Count == 0)
                            throw new InvalidOperationException("Second real Attempt failed");
                        foreach (TCTTestEvent evt in secondEvents) window.Timeline.AddEvent(evt);
                        window.Timeline.ShowOnlyAttempt(attempt.AttemptId);
                        if (window.Timeline.VisibleCount != events.Count ||
                            window.Timeline.EventCount != events.Count + secondEvents.Count)
                            throw new InvalidOperationException("Timeline selection did not isolate Attempt");
                        window.Timeline.ShowAll();
                        if (window.Timeline.VisibleCount != events.Count + secondEvents.Count)
                            throw new InvalidOperationException("Timeline All events view did not restore");
                        // M2 selection regression: selecting a result must actually scope the visible
                        // Timeline events, rather than only changing the focus label.
                        window.Timeline.FocusAttempt(attempt.AttemptId);
                        if (window.Timeline.FocusedAttemptId != attempt.AttemptId ||
                            window.Timeline.VisibleCount != events.Count)
                            throw new InvalidOperationException("M2 selected Attempt did not filter Timeline");
                        window.Timeline.FocusAttempt(null);
                        if (window.Timeline.VisibleCount != events.Count + secondEvents.Count)
                            throw new InvalidOperationException("M2 clearing result focus did not restore Timeline");

                        // M2 first interactive chain: actual Foundation Geometry Owner -> structured
                        // Observation/Artifact -> Viewer button click -> Inspector identity.
                        ITCTTestCase geometry = catalog.Cases.Single(x => x.Descriptor.Id == "TCT.M1.OBS.001");
                        TCTTestAttempt geoAttempt = new TCTTestAttempt(Guid.NewGuid().ToString("N"),
                            geometry.Descriptor.Id, 0, 20261017);
                        TCTTestArtifactStore evidenceStore = new TCTTestArtifactStore(
                            TCTTestArtifactStore.RunRoot(geoAttempt.RunId));
                        TCTTestCaseResult geometryResult = await TCTTestRunner.RunOneAsync(geometry,
                            CancellationToken.None, 20261017, geoAttempt, evidenceStore);
                        if (geometryResult.Status != TCTTestStatus.Passed ||
                            geometryResult.Observations.Count != 1 ||
                            geometryResult.Observations[0].Envelope == null)
                            throw new InvalidOperationException("M2 Geometry Owner observation is not real or structured");
                        window.Inspector.ShowResult(geometryResult);
                        window.DocumentHost.ShowResult(geometryResult);
                        if (window.DocumentHost.ViewerCount != 1)
                            throw new InvalidOperationException("M2 Geometry viewer not bound to Owner evidence");
                        var geoTabs = (System.Windows.Controls.TabControl)window.DocumentHost.Content;
                        var geoTab = (System.Windows.Controls.TabItem)geoTabs.Items[1];
                        var geoView = (TCTGeometryTestView)geoTab.Content;
                        var geoScroll = (System.Windows.Controls.ScrollViewer)geoView.Content;
                        var geoPanel = (System.Windows.Controls.StackPanel)geoScroll.Content;
                        var geoButton = geoPanel.Children.OfType<System.Windows.Controls.Button>().FirstOrDefault();
                        if (geoButton == null)
                            throw new InvalidOperationException("M2 observation selection command missing");
                        geoButton.RaiseEvent(new RoutedEventArgs(
                            System.Windows.Controls.Primitives.ButtonBase.ClickEvent, geoButton));
                        if (window.Inspector.SelectedObservationTitle != geometryResult.Observations[0].Title)
                            throw new InvalidOperationException("M2 Geometry Viewer->Inspector selection broken");

                        // M2 Ownership case MUST read real MEMEngineeringSession / MEMProjectSnapshot evidence.
                        ITCTTestCase owner = catalog.Cases.Single(x => x.Descriptor.Id == "M-OBJ-001");
                        TCTTestAttempt ownerAttempt = new TCTTestAttempt(Guid.NewGuid().ToString("N"),
                            owner.Descriptor.Id, 0, 20261017);
                        TCTTestArtifactStore ownerStore = new TCTTestArtifactStore(
                            TCTTestArtifactStore.RunRoot(ownerAttempt.RunId));
                        TCTTestCaseResult ownerResult = await TCTTestRunner.RunOneAsync(owner,
                            CancellationToken.None, 20261017, ownerAttempt, ownerStore);
                        if (ownerResult.Status != TCTTestStatus.Passed ||
                            ownerResult.Observations.All(x => x.Kind != "ObjectTree" ||
                                x.Envelope == null || x.Envelope.PayloadRef == null))
                            throw new InvalidOperationException("M2 MEM Ownership failed to produce genuine snapshot evidence");
                        if (TCTObservationReplay.Read(ownerResult, ownerStore).Count != 1)
                            throw new InvalidOperationException("M2 Owner snapshot did not roundtrip evidence");
                        window.Inspector.ShowResult(ownerResult);
                        window.DocumentHost.ShowResult(ownerResult);
                        if (window.DocumentHost.ViewerCount != 1)
                            throw new InvalidOperationException("M2 ObjectTree viewer not bound to real Owner observation");
                        var ownerTabs = (System.Windows.Controls.TabControl)window.DocumentHost.Content;
                        var ownerView = (TCTObjectTestView)((System.Windows.Controls.TabItem)ownerTabs.Items[1]).Content;
                        var ownerPanel = (System.Windows.Controls.StackPanel)
                            ((System.Windows.Controls.ScrollViewer)ownerView.Content).Content;
                        if (ownerView.EvidenceProblem != null || ownerView.VerifiedNodeCount < 7 ||
                            ownerView.VerifiedReferenceCount != 1 || ownerView.OwnershipTree == null)
                            throw new InvalidOperationException("M2 MEM snapshot ownership/reference not rendered from verified artifact");
                        var ownerButton = ownerPanel.Children.OfType<System.Windows.Controls.Button>().FirstOrDefault();
                        if (ownerButton == null)
                            throw new InvalidOperationException("M2 ObjectTree observation command missing");
                        ownerButton.RaiseEvent(new RoutedEventArgs(
                            System.Windows.Controls.Primitives.ButtonBase.ClickEvent, ownerButton));
                        if (window.Inspector.SelectedObservationTitle != "Ownership")
                            throw new InvalidOperationException("M2 Object Viewer->Inspector selection broken");
                        var rootItem = (System.Windows.Controls.TreeViewItem)ownerView.OwnershipTree.Items[0];
                        var selection = new RoutedPropertyChangedEventArgs<object>(null, rootItem);
                        selection.RoutedEvent = System.Windows.Controls.TreeView.SelectedItemChangedEvent;
                        ownerView.OwnershipTree.RaiseEvent(selection);
                        if (window.Inspector.SelectedObjectId == null ||
                            !window.Inspector.SelectedObjectId.Equals(
                                ownerResult.Observations[0].Envelope.SourceObjectId,
                                StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("M2 MEM object selection did not reach Inspector");
                        // The real GUI must also consume a separate Worker process result, not
                        // only locally-created observations. Parent and Worker use the same RunRoot.
                        TCTTestAttempt isolated = new TCTTestAttempt(Guid.NewGuid().ToString("N"),
                            owner.Descriptor.Id, 0, 20261018);
                        TCTTestCaseResult workerOwnership =
                            await new TCTTestHostController().RunIsolatedAsync(owner,
                                TimeSpan.FromSeconds(20), 20261018, CancellationToken.None, isolated);
                        if (workerOwnership.Status != TCTTestStatus.Passed ||
                            workerOwnership.Observations.Count != 1 ||
                            TCTObservationReplay.Read(workerOwnership).Count != 1)
                            throw new InvalidOperationException("M2 cross-process MEM snapshot evidence missing");
                        window.Inspector.ShowResult(workerOwnership);
                        window.DocumentHost.ShowResult(workerOwnership);
                        var workerTabs = (System.Windows.Controls.TabControl)window.DocumentHost.Content;
                        var workerViewer = (TCTObjectTestView)
                            ((System.Windows.Controls.TabItem)workerTabs.Items[1]).Content;
                        if (workerViewer.VerifiedNodeCount < 7 ||
                            workerViewer.VerifiedReferenceCount != 1 ||
                            workerViewer.EvidenceProblem != null)
                            throw new InvalidOperationException("M2 WPF viewer failed to render cross-worker MEM evidence");
                        // A six-mode shell must never manufacture a Graph when the selected
                        // Owner has supplied only ObjectTree evidence.
                        window.DocumentHost.ShowMode("Graph");
                        if (window.DocumentHost.ActiveMode != "Graph" ||
                            window.DocumentHost.ViewerCount != 1)
                            throw new InvalidOperationException("M2 NoEvidence mode must not count as a real viewer");
                        var graphTab = (System.Windows.Controls.TabItem)
                            ((System.Windows.Controls.TabControl)window.DocumentHost.Content).SelectedItem;
                        var unsupported = graphTab.Content as System.Windows.Controls.TextBlock;
                        if (unsupported == null || !unsupported.Text.Contains("NoEvidence"))
                            throw new InvalidOperationException("M2 unsupported mode manufactured data");
                        window.DocumentHost.ShowMode("ObjectTree");
                        if (window.DocumentHost.ActiveMode != "ObjectTree")
                            throw new InvalidOperationException("M2 viewer context switching failed");

                        // Before/After in one real MEM Attempt: the UI must derive
                        // changes from verified snapshots, not textual assert labels.
                        ITCTTestCase revisionCase = catalog.Cases.Single(x =>
                            x.Descriptor.Id == "M-OBJ-006");
                        TCTTestAttempt revisionAttempt = new TCTTestAttempt(Guid.NewGuid().ToString("N"),
                            revisionCase.Descriptor.Id, 0, 20261018);
                        TCTTestArtifactStore revisionStore = new TCTTestArtifactStore(
                            TCTTestArtifactStore.RunRoot(revisionAttempt.RunId));
                        TCTTestCaseResult revisionResult = await TCTTestRunner.RunOneAsync(revisionCase,
                            CancellationToken.None, 20261018, revisionAttempt, revisionStore);
                        if (revisionResult.Status != TCTTestStatus.Passed ||
                            revisionResult.Observations.Count != 2 ||
                            TCTObservationReplay.Read(revisionResult, revisionStore).Count != 2)
                            throw new InvalidOperationException("M2 MEM revision comparison evidence invalid");
                        window.DocumentHost.ShowResult(revisionResult);
                        var revisionTabs = (System.Windows.Controls.TabControl)window.DocumentHost.Content;
                        var revisionViewer = (TCTObjectTestView)
                            ((System.Windows.Controls.TabItem)revisionTabs.Items[1]).Content;
                        if (revisionViewer.VerifiedDiffCount != 1 || revisionViewer.EvidenceProblem != null)
                            throw new InvalidOperationException("M2 WPF failed to display actual MEM Revision diff");

                        // Stage 1: real FGERect2D -> versioned GeometryEvidence -> WPF canvas.
                        ITCTTestCase shapeCase = catalog.Cases.Single(x =>
                            x.Descriptor.Id == "TCT.M2.GEOMETRY.001");
                        TCTTestAttempt shapeAttempt = new TCTTestAttempt(Guid.NewGuid().ToString("N"),
                            shapeCase.Descriptor.Id, 0, 20261021);
                        TCTTestArtifactStore shapeStore = new TCTTestArtifactStore(
                            TCTTestArtifactStore.RunRoot(shapeAttempt.RunId));
                        TCTTestCaseResult shapeResult = await TCTTestRunner.RunOneAsync(shapeCase,
                            CancellationToken.None, 20261021, shapeAttempt, shapeStore);
                        if (shapeResult.Status != TCTTestStatus.Passed ||
                            TCTObservationReplay.Read(shapeResult, shapeStore).Count != 1)
                            throw new InvalidOperationException("Real geometry owner evidence missing");
                        window.DocumentHost.ShowResult(shapeResult);
                        var shapeTabs = (System.Windows.Controls.TabControl)window.DocumentHost.Content;
                        var shapeView = (TCTGeometryTestView)
                            ((System.Windows.Controls.TabItem)shapeTabs.Items[1]).Content;
                        if (shapeView.VerifiedFigureCount != 4 ||
                            shapeView.EvidenceCanvas == null ||
                            shapeView.EvidenceProblem != null)
                            throw new InvalidOperationException("Geometry expected/actual/diff evidence canvas failed");
                        var buttons = (System.Windows.Controls.StackPanel)shapeView.EvidenceCanvas.Content;
                        var toolbar = (System.Windows.Controls.StackPanel)buttons.Children[0];
                        var zoomIn = (System.Windows.Controls.Button)toolbar.Children[0];
                        zoomIn.RaiseEvent(new RoutedEventArgs(
                            System.Windows.Controls.Primitives.ButtonBase.ClickEvent, zoomIn));
                        if (shapeView.EvidenceCanvas.Zoom <= 1)
                            throw new InvalidOperationException("Geometry zoom UI click failed");
                        shapeView.EvidenceCanvas.SetLayerVisible("Actual", false);
                        if (shapeView.EvidenceCanvas.VisibleFigureCount != 3 ||
                            shapeView.EvidenceCanvas.RenderedFigureCount != 4)
                            throw new InvalidOperationException("Expected/Actual/Diff layer filter incorrect");
                        shapeView.EvidenceCanvas.SetLayerVisible("Actual", true);
                        if (shapeView.EvidenceCanvas.VisibleFigureCount != 4)
                            throw new InvalidOperationException("Geometry layer restore failed");


                        // Stage 1: history browser must verify full Attempt/Artifact after live
                        // Worker storage has been removed, with no fabricated outcomes.
                        using (TCTTestSandbox archiveSandbox = new TCTTestSandbox())
                        {
                            var history = new TCTTestHistoryControl(
                                System.IO.Path.Combine(archiveSandbox.Root, "History"));
                            history.SetCurrent(new TCTTestRunSummary(
                                new System.Collections.Generic.List<TCTTestCaseResult> { shapeResult },
                                shapeAttempt.RunId));
                            history.SaveCurrent();
                            if (history.HistoryCount != 1)
                                throw new InvalidOperationException("History UI did not catalogue the real Run");
                            string live = TCTTestArtifactStore.RunRoot(shapeAttempt.RunId);
                            if (Directory.Exists(live)) Directory.Delete(live, true);
                            TCTArchivedTestRun reloaded = null;
                            string location = null;
                            history.HistoryOpened += (run, directory) =>
                            { reloaded = run; location = directory; };
                            history.LoadVerified(shapeAttempt.RunId);
                            if (reloaded == null || reloaded.Attempts.Count != 1 ||
                                reloaded.Attempts[0].AttemptId != shapeAttempt.AttemptId)
                                throw new InvalidOperationException("History GUI lost real Attempt identity");
                            window.DocumentHost.ShowResult(reloaded.Attempts[0], location);
                            var archivedTabs = (System.Windows.Controls.TabControl)window.DocumentHost.Content;
                            var archivedGeometry = (TCTGeometryTestView)
                                ((System.Windows.Controls.TabItem)archivedTabs.Items[1]).Content;
                            if (archivedGeometry.VerifiedFigureCount != 4 ||
                                archivedGeometry.EvidenceProblem != null)
                                throw new InvalidOperationException("History geometry viewer lost verified Artifact");

                            // History compare is a real two-Run owner evidence operation, not a
                            // pair of verdict strings or mocked screen content.
                            TCTTestAttempt nextAttempt = new TCTTestAttempt(
                                Guid.NewGuid().ToString("N"), shapeCase.Descriptor.Id, 0, 20261021);
                            TCTTestArtifactStore nextStore = new TCTTestArtifactStore(
                                TCTTestArtifactStore.RunRoot(nextAttempt.RunId));
                            TCTTestCaseResult nextResult = await TCTTestRunner.RunOneAsync(shapeCase,
                                CancellationToken.None, 20261021, nextAttempt, nextStore);
                            if (nextResult.Status != TCTTestStatus.Passed)
                                throw new InvalidOperationException("Second real geometry run failed");
                            history.SetCurrent(new TCTTestRunSummary(
                                new System.Collections.Generic.List<TCTTestCaseResult> { nextResult },
                                nextAttempt.RunId));
                            history.SaveCurrent();
                            string nextLive = TCTTestArtifactStore.RunRoot(nextAttempt.RunId);
                            if (Directory.Exists(nextLive)) Directory.Delete(nextLive, true);
                            TCTArchivedRunComparison twoRuns =
                                history.CompareArchives(shapeAttempt.RunId, nextAttempt.RunId);
                            var diffView = new TCTTestDiffControl();
                            diffView.ShowComparison(twoRuns,
                                Path.Combine(archiveSandbox.Root, "History"));
                            if (twoRuns.Rows.Count != 1 || twoRuns.ChangedCount != 0 ||
                                diffView.CurrentComparison != twoRuns ||
                                diffView.SelectedEvidenceChanges != 0)
                                throw new InvalidOperationException("WPF historical owner diff is inaccurate");
                        }
                        // Stage 1 integrated visual/object sample: the real MVISceneProjection
                        // expands a shared Symbol into two separately addressed instances.
                        ITCTTestCase symbolCase = catalog.Cases.Single(x =>
                            x.Descriptor.Id == "M-OBJ-007");
                        TCTTestAttempt symbolAttempt = new TCTTestAttempt(Guid.NewGuid().ToString("N"),
                            symbolCase.Descriptor.Id, 0, 20261021);
                        TCTTestArtifactStore symbolStore = new TCTTestArtifactStore(
                            TCTTestArtifactStore.RunRoot(symbolAttempt.RunId));
                        TCTTestCaseResult symbolResult = await TCTTestRunner.RunOneAsync(symbolCase,
                            CancellationToken.None, 20261021, symbolAttempt, symbolStore);
                        if (symbolResult.Status != TCTTestStatus.Passed ||
                            TCTObservationReplay.Read(symbolResult, symbolStore).Count != 2)
                            throw new InvalidOperationException("Real MEM/Symbol/Visual evidence missing");
                        window.DocumentHost.ShowResult(symbolResult);
                        if (window.DocumentHost.ViewerCount != 2)
                            throw new InvalidOperationException("Symbol case must have Geometry and Object evidence");
                        var symbolTabs = (System.Windows.Controls.TabControl)window.DocumentHost.Content;
                        var symbolGeometry = symbolTabs.Items.OfType<System.Windows.Controls.TabItem>()
                            .Select(tab => tab.Content).OfType<TCTGeometryTestView>().Single();
                        var symbolObject = symbolTabs.Items.OfType<System.Windows.Controls.TabItem>()
                            .Select(tab => tab.Content).OfType<TCTObjectTestView>().Single();
                        if (symbolGeometry.VerifiedFigureCount != 4 ||
                            symbolObject.VerifiedReferenceCount != 2 ||
                            symbolGeometry.EvidenceProblem != null || symbolObject.EvidenceProblem != null)
                            throw new InvalidOperationException("Real Symbol instance projection not in both viewers");
                        if (symbolObject.RelationDiagram == null ||
                            symbolObject.RelationDiagram.VerifiedReferenceLines != 2 ||
                            symbolObject.RelationDiagram.RenderedNodeCount < 7)
                            throw new InvalidOperationException("MEM relation diagram lost verified ownership/reference");
                        var diagramNode = symbolObject.OwnershipTree.Items.OfType<System.Windows.Controls.TreeViewItem>().First();
                        var diagramId = Guid.Parse((string)((System.Xml.Linq.XElement)diagramNode.Tag).Attribute("id"));
                        symbolObject.RelationDiagram.SelectNode(diagramId);
                        if (window.Inspector.SelectedObjectId != diagramId.ToString("D"))
                            throw new InvalidOperationException("Diagram selection did not reach Inspector");
                        if (symbolObject.RelationDiagram.VerifiedReferenceIds.Count != 2)
                            throw new InvalidOperationException("MEM relation diagram lost reference identity");
                        Guid chosenReference = symbolObject.RelationDiagram.VerifiedReferenceIds[0];
                        symbolObject.RelationDiagram.SelectReference(chosenReference);
                        if (window.Inspector.SelectedObjectId != chosenReference.ToString("D") ||
                            !window.Inspector.SelectedObjectDetails.Contains("DefinitionTargetId"))
                            throw new InvalidOperationException("Reference edge did not locate its verified target");
                        // Two references of the same Symbol definition can overlap geometrically.
                        // The figure list must retain and select their independent InstancePath.
                        var symbolProof = symbolGeometry.EvidenceCanvas;
                        var duplicateOwner = symbolProof.VerifiedFigures.Select((figure, index) =>
                            new { Figure = figure, Index = index })
                            .Where(x => !string.IsNullOrEmpty(x.Figure.DefinitionAddress))
                            .GroupBy(x => x.Figure.SourceObjectId)
                            .FirstOrDefault(group => group.Select(x => x.Figure.DefinitionAddress)
                                .Distinct(StringComparer.Ordinal).Count() == 2);
                        if (duplicateOwner == null)
                            throw new InvalidOperationException("Symbol instance identities missing from Geometry");
                        int[] figureIndices = duplicateOwner.Select(x => x.Index).ToArray();
                        symbolProof.SelectFigure(figureIndices[0]);
                        string firstAddress = window.Inspector.SelectedObjectDetails;
                        symbolProof.SelectFigure(figureIndices[1]);
                        string secondAddress = window.Inspector.SelectedObjectDetails;
                        if (string.IsNullOrWhiteSpace(firstAddress) ||
                            string.IsNullOrWhiteSpace(secondAddress) ||
                            string.Equals(firstAddress, secondAddress, StringComparison.Ordinal))
                            throw new InvalidOperationException("Overlapping Symbol references cannot be distinguished");

                        // Suite/Plan is only CaseId selection; Runner and result remain unified.
                        var planUi = new TCTTestPlanControl();
                        planUi.Bind(catalog, () => new[] {
                            shapeCase.Descriptor.Id, owner.Descriptor.Id });
                        planUi.CaptureFromExplorer();
                        if (planUi.CaseCount != 2)
                            throw new InvalidOperationException("M2 plan selection did not preserve CaseIds");
                        using (TCTTestSandbox planSandbox = new TCTTestSandbox())
                        {
                            string planFile = Path.Combine(planSandbox.Root, "case.tctplan");
                            planUi.SaveTo(planFile);
                            planUi.Bind(catalog, () => new[] { shapeCase.Descriptor.Id });
                            planUi.LoadFrom(planFile);
                            if (planUi.CaseCount != 2 ||
                                !planUi.CaseIds.Contains(owner.Descriptor.Id))
                                throw new InvalidOperationException("Saved Plan failed roundtrip");
                            string[] requested = null;
                            planUi.RunRequested += ids => requested = ids;
                            planUi.Run();
                            if (requested == null || requested.Length != 2)
                                throw new InvalidOperationException("Plan execution event missing real CaseIds");
                            string xml = File.ReadAllText(planFile);
                            File.WriteAllText(planFile, xml.Replace("schema=\"1\"", "schema=\"88\""));
                            bool blocked = false;
                            try { planUi.LoadFrom(planFile); }
                            catch (InvalidDataException) { blocked = true; }
                            if (!blocked || planUi.CaseCount != 2)
                                throw new InvalidOperationException("Invalid Plan damaged current selection");
                        }
                        if (window.TestDock.Layout.Descendents()
                                .OfType<AvalonDock.Layout.LayoutContent>().Count() < 7)
                            throw new InvalidOperationException("Expected seven real docked TestCenter panes");
                        window.MenuThemeDark.RaiseEvent(new RoutedEventArgs(
                            System.Windows.Controls.MenuItem.ClickEvent, window.MenuThemeDark));
                        if (!(window.TestDock.Theme is AvalonDock.Themes.Vs2013DarkTheme))
                            throw new InvalidOperationException("TestCenter VS dark theme failed");
                        window.MenuRestoreLayout.RaiseEvent(new RoutedEventArgs(
                            System.Windows.Controls.MenuItem.ClickEvent, window.MenuRestoreLayout));
                        if (!(window.TestDock.Theme is AvalonDock.Themes.Vs2013LightTheme))
                            throw new InvalidOperationException("TestCenter default layout/theme restore failed");
                        exit = 0;
                    }
                    finally { window.Close(); }
                }
                else if (command == "--replay-history")
                {
                    if (e.Args.Length != 4)
                        throw new ArgumentException("--replay-history <history-dir> <runId> <report.json>");
                    TCTArchivedTestRun archived = new TCTTestResultRepository(e.Args[1])
                        .LoadDetailed(e.Args[2]);
                    var observations = archived.Attempts.SelectMany(result =>
                        result.Observations.Where(o => o.Envelope != null).Select(o => new {
                            testId = result.Descriptor.Id,
                            attemptId = result.AttemptId,
                            kind = o.Kind, title = o.Title,
                            sourceObjectId = o.Envelope.SourceObjectId,
                            definitionAddress = o.Envelope.DefinitionAddress,
                            runtimeInstanceId = o.Envelope.RuntimeInstanceId,
                            evidenceSha256 = o.Evidence.Sha256,
                            payloadSha256 = o.Envelope.PayloadRef == null ?
                                "" : o.Envelope.PayloadRef.Sha256
                        })).ToArray();
                    string output = Path.GetFullPath(e.Args[3]);
                    Directory.CreateDirectory(Path.GetDirectoryName(output));
                    var serializer = new System.Web.Script.Serialization.JavaScriptSerializer {
                        MaxJsonLength = int.MaxValue };
                    string temporary = output + ".partial";
                    File.WriteAllText(temporary, serializer.Serialize(new {
                        schema = 1, runId = archived.RunId, commit = archived.Commit,
                        count = archived.Attempts.Count,
                        originalPassed = archived.Attempts.Count > 0 &&
                            archived.Attempts.All(x => x.Status == TCTTestStatus.Passed),
                        statuses = archived.Attempts.Select(x => new {
                            testId = x.Descriptor.Id, attemptId = x.AttemptId,
                            status = x.Status.ToString()
                        }).ToArray(),
                        observations = observations
                    }), System.Text.Encoding.UTF8);
                    if (File.Exists(output)) File.Delete(output);
                    File.Move(temporary, output);
                    exit = archived.Attempts.Count > 0 ? 0 : 1;
                }
                else if (command == "--run")
                {
                    TCTTestRunRequest request = new TCTTestRunRequest();
                    string report = Path.Combine(Environment.CurrentDirectory, "TestCenterResults.xml");
                    string format = "junit";
                    string history = null;
                    for (int i = 1; i < e.Args.Length; i++)
                    {
                        string arg = e.Args[i].ToLowerInvariant();
                        if (arg == "--report" && i + 1 < e.Args.Length) report = e.Args[++i];
                        else if (arg == "--history-dir" && i + 1 < e.Args.Length) history = e.Args[++i];
                        else if (arg == "--format" && i + 1 < e.Args.Length) format = e.Args[++i].ToLowerInvariant();
                        else if (arg == "--filter" && i + 1 < e.Args.Length) request.Filter = e.Args[++i];
                        else if (arg == "--id" && i + 1 < e.Args.Length) request.SelectedIds.Add(e.Args[++i]);
                        else if (arg == "--repeat" && i + 1 < e.Args.Length)
                            request.Repeat = int.Parse(e.Args[++i], CultureInfo.InvariantCulture);
                        else if (arg == "--timeout-ms" && i + 1 < e.Args.Length)
                            request.Timeout = TimeSpan.FromMilliseconds(int.Parse(e.Args[++i], CultureInfo.InvariantCulture));
                        else if (arg == "--parallel" && i + 1 < e.Args.Length)
                            request.MaxConcurrency = int.Parse(e.Args[++i], CultureInfo.InvariantCulture);
                        else if (arg == "--seed" && i + 1 < e.Args.Length)
                            request.Seed = int.Parse(e.Args[++i], CultureInfo.InvariantCulture);
                        else if (arg == "--profile" && i + 1 < e.Args.Length)
                        {
                            TCTTestRunProfile parsedProfile;
                            if (!Enum.TryParse(e.Args[++i], true, out parsedProfile) ||
                                !Enum.IsDefined(typeof(TCTTestRunProfile), parsedProfile))
                                throw new ArgumentException("Unknown test profile");
                            request.Profile = parsedProfile;
                        }
                        else if (arg == "--fail-fast") request.FailFast = true;
                        else if (arg == "--inproc") request.Isolation = TCTTestIsolation.InProcess;
                        else if (arg == "--isolate") request.Isolation = TCTTestIsolation.ChildProcess;
                        else throw new ArgumentException("Unknown/incomplete argument: " + e.Args[i]);
                    }
                    TCTTestRunSummary summary = await new TCTTestExecutionCoordinator().RunAsync(
                        catalog, request, CancellationToken.None, null);
                    if (format == "json") TCTReportWriter.WriteJson(report, summary);
                    else if (format == "junit") TCTReportWriter.WriteJUnit(report, summary);
                    else if (format == "text") TCTReportWriter.WriteText(report, summary);
                    else throw new ArgumentException("Unknown format: " + format);
                    if (history != null) new TCTTestResultRepository(history).Save(summary);
                    exit = summary.Success ? 0 : 1;
                }
                else throw new ArgumentException("Unsupported command: " + e.Args[0]);
            }
            catch (Exception ex)
            {
                try
                {
                    File.WriteAllText(Path.Combine(Path.GetTempPath(), "CxVisual-TestCenter-HostError.txt"),
                        DateTime.UtcNow.ToString("o") + Environment.NewLine + ex);
                }
                catch (IOException) { }
                exit = 2;
            }
            Shutdown(exit);
        }
    }
}
