using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
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
                        if (window.DocumentHost.ActiveMode != "Graph")
                            throw new InvalidOperationException("M2 Graph shell mode did not activate");
                        var graphTab = (System.Windows.Controls.TabItem)
                            ((System.Windows.Controls.TabControl)window.DocumentHost.Content).SelectedItem;
                        var unsupported = graphTab.Content as System.Windows.Controls.TextBlock;
                        if (unsupported == null || !unsupported.Text.Contains("NoEvidence"))
                            throw new InvalidOperationException("M2 unsupported mode manufactured data");
                        window.DocumentHost.ShowMode("ObjectTree");
                        if (window.DocumentHost.ActiveMode != "ObjectTree")
                            throw new InvalidOperationException("M2 viewer context switching failed");
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
