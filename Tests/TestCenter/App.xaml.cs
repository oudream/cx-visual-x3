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
                TCTTestCatalog catalog = TCTRegistration.CreateCatalog();
                string command = e.Args[0].ToLowerInvariant();
                if (command == "--nunit-probe")
                {
                    if (e.Args.Length < 2 || e.Args.Length > 3 ||
                        (e.Args.Length == 3 && e.Args[2] != "--negative"))
                        throw new ArgumentException("--nunit-probe <result.xml> [--negative]");
                    exit = TCTNUnitEngineProbe.Execute(e.Args[1], e.Args.Length == 3);
                }
                else if (command == "--worker")
                {
                    if (e.Args.Length != 4 && e.Args.Length != 7) throw new ArgumentException("--worker <id> <result-file> <seed> [runId attemptId iteration]");
                    ITCTTestCase test = catalog.Cases.SingleOrDefault(x => x.Descriptor.Id == e.Args[1]);
                    if (test == null) throw new InvalidOperationException("Unknown worker test ID");
                    int seed = int.Parse(e.Args[3], CultureInfo.InvariantCulture);
                    TCTTestAttempt attempt = e.Args.Length == 7 ?
                        new TCTTestAttempt(e.Args[4], test.Descriptor.Id,
                            int.Parse(e.Args[6], CultureInfo.InvariantCulture), seed, e.Args[5]) :
                        new TCTTestAttempt("AdHoc", test.Descriptor.Id, 0, seed);
                    TCTTestArtifactStore store = e.Args.Length == 7 ?
                        new TCTTestArtifactStore(TCTTestArtifactStore.RunRoot(attempt.RunId)) : null;
                    TCTTestCaseResult result = await TCTTestRunner.RunOneAsync(test,
                        CancellationToken.None, seed, attempt, store);
                    TCTWireCodec.Write(e.Args[2], result);
                    exit = 0; // 结果状态在结构化文件内，不能用退出码代替断言状态。
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
