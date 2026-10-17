using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.TestCenter.Core;
using CxVisual.TestCenter.Models;
using CxVisual.TestCenter.Fixtures;

namespace CxVisual.TestCenter.Execution
{
    // 子进程隔离由同一个TestCenter.exe的--worker入口实现，不新增工程/DLL。
    public sealed class TCTTestHostController
    {
        public async Task<TCTTestCaseResult> RunIsolatedAsync(ITCTTestCase test,
            TimeSpan timeout, int seed, CancellationToken cancellationToken, TCTTestAttempt attempt = null,
            Action<TCTTestEvent> onEvent = null)
        {
            if (test == null) throw new ArgumentNullException("test");
            string directory = Path.Combine(Path.GetTempPath(), "CxVisual-TCT",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string resultFile = Path.Combine(directory, "result.xml");
            string eventFile = Path.Combine(directory, "events.log");
            TCTTestEventJournal journal = new TCTTestEventJournal(eventFile);
            int eventsRead = 0;
            Stopwatch watch = Stopwatch.StartNew();
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                string executable = Process.GetCurrentProcess().MainModule.FileName;
                ProcessStartInfo start = new ProcessStartInfo(executable);
                start.Arguments = "--worker " + Quote(test.Descriptor.Id) + " " +
                    Quote(resultFile) + " " + seed.ToString(CultureInfo.InvariantCulture) +
                    (attempt == null ? "" : " " + Quote(attempt.RunId) + " " + Quote(attempt.AttemptId) +
                    " " + attempt.Iteration.ToString(CultureInfo.InvariantCulture) + " " + Quote(eventFile) +
                    (test is ITCTTestDefinitionFingerprint ?
                        " " + Quote(((ITCTTestDefinitionFingerprint)test).DefinitionFingerprint) : ""));
                start.UseShellExecute = false;
                start.CreateNoWindow = true;
                start.WindowStyle = ProcessWindowStyle.Hidden;
                start.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
                using (Process child = Process.Start(start))
                {
                    if (child == null) throw new InvalidOperationException("Cannot start TCT worker process");
                    Task exited = Task.Run(() => child.WaitForExit());
                    Task eventReader = Task.Run(async () =>
                    {
                        while (!exited.IsCompleted)
                        {
                            if (onEvent != null)
                                foreach (TCTTestEvent ev in journal.ReadNew(
                                    attempt == null ? "AdHoc" : attempt.AttemptId, ref eventsRead))
                                    onEvent(ev);
                            await Task.Delay(100).ConfigureAwait(false);
                        }
                        if (onEvent != null)
                            foreach (TCTTestEvent ev in journal.ReadNew(
                                attempt == null ? "AdHoc" : attempt.AttemptId, ref eventsRead))
                                onEvent(ev);
                    });
                    using (CancellationTokenSource waits = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                    {
                        Task deadline = Task.Delay(timeout, waits.Token);
                        Task cancelled = Task.Delay(Timeout.Infinite, waits.Token);
                        Task first = await Task.WhenAny(exited, deadline, cancelled).ConfigureAwait(false);
                        if (first != exited)
                        {
                            bool killed = TryKill(child);
                            Task grace = Task.Delay(TimeSpan.FromSeconds(5));
                            Task stopped = await Task.WhenAny(exited, grace).ConfigureAwait(false);
                            if (stopped != exited || !child.HasExited)
                                return Failure(test.Descriptor, TCTTestStatus.Error, watch.Elapsed,
                                    "Worker did not exit after attempted termination; check orphan process", attempt);
                            return Failure(test.Descriptor, first == cancelled ?
                                TCTTestStatus.Cancelled : TCTTestStatus.TimedOut, watch.Elapsed,
                                first == cancelled ? "Run cancelled; worker exited" :
                                    "Worker exceeded deadline and exited after terminate", attempt);
                        }
                        waits.Cancel();
                    }
                    await eventReader.ConfigureAwait(false);
                    if (!File.Exists(resultFile))
                        return Failure(test.Descriptor, TCTTestStatus.Error, watch.Elapsed,
                            "Worker exited " + child.ExitCode + " without result file", attempt);
                    TCTTestCaseResult result = TCTWireCodec.Read(resultFile, test.Descriptor, attempt);
                    if (File.Exists(eventFile) && attempt != null)
                    {
                        TCTTestArtifactStore store = new TCTTestArtifactStore(
                            TCTTestArtifactStore.RunRoot(attempt.RunId));
                        TCTArtifactRef eventArtifact = store.Save(File.ReadAllBytes(eventFile),
                            "application/vnd.cxvisual.test-events", attempt.AttemptId);
                        List<TCTArtifactRef> all = result.Artifacts.Concat(new[] { eventArtifact }).ToList();
                        result = TCTTestCaseResult.FromTransport(result.Descriptor, result.Status,
                            result.Elapsed, result.Error, result.Assertions, result.Observations,
                            attempt, all, result.Diagnostics);
                    }
                    if (child.ExitCode != 0 && result.Status == TCTTestStatus.Passed)
                        return Failure(test.Descriptor, TCTTestStatus.Error, watch.Elapsed,
                            "Worker exited unexpectedly (" + child.ExitCode + ")", attempt);
                    return result;
                }
            }
            catch (OperationCanceledException)
            {
                return Failure(test.Descriptor, TCTTestStatus.Cancelled, watch.Elapsed, "Run cancelled", attempt);
            }
            catch (Exception ex)
            {
                return Failure(test.Descriptor, TCTTestStatus.Error, watch.Elapsed, ex.ToString(), attempt);
            }
            finally
            {
                watch.Stop();
                // 删除文件前要先确认已退出；如果外部进程拒绝终止，尽力清理临时目录。
                try { Directory.Delete(directory, true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private static string Quote(string value) { return "\"" + value.Replace("\"", "\\\"") + "\""; }
        private static bool TryKill(Process process)
        {
            try { if (!process.HasExited) process.Kill(); return true; }
            catch (InvalidOperationException) { return process.HasExited; }
            catch (System.ComponentModel.Win32Exception) { return false; }
        }
        internal static TCTTestCaseResult Failure(TCTTestCaseDescriptor descriptor,
            TCTTestStatus status, TimeSpan elapsed, string error, TCTTestAttempt attempt = null)
        {
            return TCTTestCaseResult.FromTransport(descriptor, status, elapsed, error,
                new List<TCTAssertionResult>(), new List<TCTTestObservation>(), attempt);
        }
    }

    public sealed class TCTTestExecutionCoordinator
    {
        private readonly TCTTestHostController _host = new TCTTestHostController();
        private readonly TCTResourceGate _resources = new TCTResourceGate();
        public async Task<TCTTestRunSummary> RunAsync(TCTTestCatalog catalog, TCTTestRunRequest request,
            CancellationToken cancellationToken, Action<TCTTestCaseResult> onResult,
            Action<TCTTestEvent> onEvent = null)
        {
            if (catalog == null) throw new ArgumentNullException("catalog");
            if (request == null) throw new ArgumentNullException("request");
            request.Validate();
            IList<ITCTTestCase> tests = catalog.Select(request);
            int count = checked(tests.Count * request.Repeat);
            TCTTestRunSession session = new TCTTestRunSession(request.RunId, request.Profile);
            session.Start();
            if (count == 0)
            {
                session.Finish(false);
                return new TCTTestRunSummary(new List<TCTTestCaseResult>(), session.RunId, session.Evidence);
            }

            TCTTestCaseResult[] results = new TCTTestCaseResult[count];
            TCTTestArtifactStore artifactStore = new TCTTestArtifactStore(
                TCTTestArtifactStore.RunRoot(request.RunId));
            int next = -1;
            int stop = 0;
            int workers = request.FailFast ? 1 : Math.Min(count, request.MaxConcurrency);
            List<Task> tasks = new List<Task>();
            try
            {
                for (int w = 0; w < workers; w++)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        while (!cancellationToken.IsCancellationRequested && Volatile.Read(ref stop) == 0)
                        {
                            int index = Interlocked.Increment(ref next);
                            if (index >= count) break;
                            ITCTTestCase test = tests[index % tests.Count];
                            int iteration = index / tests.Count;
                            int seed = unchecked(request.Seed + iteration);
                            TCTTestAttempt attempt = new TCTTestAttempt(request.RunId, test.Descriptor.Id,
                                iteration, seed);
                            TCTTestCaseResult result;
                            try
                            {
                                using (IDisposable lease = await _resources.AcquireAsync(
                                    test.Descriptor.ExclusiveResourceKeys, cancellationToken).ConfigureAwait(false))
                                {
                                    if (request.Isolation == TCTTestIsolation.ChildProcess)
                                        result = await _host.RunIsolatedAsync(test, request.Timeout, seed,
                                            cancellationToken, attempt, onEvent).ConfigureAwait(false);
                                    else
                                        // InProcess is for trusted debugging; cannot terminate a hung thread.
                                        result = await TCTTestRunner.RunOneAsync(test, cancellationToken,
                                            seed, attempt, artifactStore, onEvent).ConfigureAwait(false);
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                result = TCTTestHostController.Failure(test.Descriptor,
                                    TCTTestStatus.Cancelled, TimeSpan.Zero, "Cancelled while waiting for exclusive resource", attempt);
                            }
                            results[index] = result;
                            if (onResult != null) onResult(result);
                            if (request.FailFast && result.Status != TCTTestStatus.Passed)
                                Interlocked.Exchange(ref stop, 1);
                        }
                    }, CancellationToken.None));
                }
                await Task.WhenAll(tasks).ConfigureAwait(false);
                if (cancellationToken.IsCancellationRequested) session.RequestCancel();
                // Selection identity and unstarted attempts remain visible; no silent loss on fail-fast/cancel.
                for (int index = 0; index < count; index++)
                {
                    if (results[index] != null) continue;
                    ITCTTestCase test = tests[index % tests.Count];
                    TCTTestAttempt attempt = new TCTTestAttempt(request.RunId, test.Descriptor.Id,
                        index / tests.Count, unchecked(request.Seed + index / tests.Count));
                    string reason = cancellationToken.IsCancellationRequested ?
                        "Not started due to Run cancellation" : "Not started because FailFast stopped scheduling";
                    results[index] = TCTTestHostController.Failure(test.Descriptor,
                        TCTTestStatus.Skipped, TimeSpan.Zero, reason, attempt);
                }
                session.Finish(false);
                return new TCTTestRunSummary(results, session.RunId, session.Evidence);
            }
            catch
            {
                session.Finish(true);
                throw;
            }
        }
    }

    public sealed class TCTTestResultCollector : ITCTTestResultSink
    {
        private readonly object _gate = new object();
        private readonly List<TCTTestCaseResult> _results = new List<TCTTestCaseResult>();
        public void OnResult(TCTTestCaseResult result)
        {
            if (result == null) throw new ArgumentNullException("result");
            lock (_gate) _results.Add(result);
        }
        public TCTTestRunSummary Snapshot()
        {
            lock (_gate) return new TCTTestRunSummary(new List<TCTTestCaseResult>(_results));
        }
        public void Clear() { lock (_gate) _results.Clear(); }
    }

    // 封装父进程运行结果与Worker结果之间的最小XML协议。
    public static class TCTWireCodec
    {
        private static System.Xml.Linq.XElement WriteObservation(TCTTestObservation o)
        {
            System.Xml.Linq.XElement x = new System.Xml.Linq.XElement("Observation",
                new System.Xml.Linq.XAttribute("kind", o.Kind),
                new System.Xml.Linq.XAttribute("title", o.Title),
                new System.Xml.Linq.XAttribute("origin", o.Origin), o.Details);
            if (o.Envelope == null) return x;
            TCTObservationEnvelope e = o.Envelope;
            x.Add(new System.Xml.Linq.XAttribute("schema", e.SchemaVersion),
                new System.Xml.Linq.XAttribute("runId", e.RunId),
                new System.Xml.Linq.XAttribute("attemptId", e.AttemptId),
                new System.Xml.Linq.XAttribute("utcTicks", e.TimestampUtc.Ticks),
                new System.Xml.Linq.XAttribute("sourceObjectId", e.SourceObjectId),
                new System.Xml.Linq.XAttribute("definitionAddress", e.DefinitionAddress),
                new System.Xml.Linq.XAttribute("runtimeInstanceId", e.RuntimeInstanceId),
                new System.Xml.Linq.XAttribute("evidencePath", o.Evidence.RelativePath),
                new System.Xml.Linq.XAttribute("evidenceHash", o.Evidence.Sha256),
                new System.Xml.Linq.XAttribute("evidenceBytes", o.Evidence.ByteSize));
            if (e.PayloadRef != null)
                x.Add(new System.Xml.Linq.XAttribute("payloadPath", e.PayloadRef.RelativePath),
                    new System.Xml.Linq.XAttribute("payloadMime", e.PayloadRef.Mime),
                    new System.Xml.Linq.XAttribute("payloadHash", e.PayloadRef.Sha256),
                    new System.Xml.Linq.XAttribute("payloadBytes", e.PayloadRef.ByteSize));
            return x;
        }
        private static TCTTestObservation ReadObservation(System.Xml.Linq.XElement x,
            TCTTestAttempt attempt)
        {
            string kind = (string)x.Attribute("kind"), title = (string)x.Attribute("title");
            string origin = (string)x.Attribute("origin") ?? "Actual";
            if (x.Attribute("schema") == null)
                return new TCTTestObservation(kind, title, x.Value, origin);
            if ((int)x.Attribute("schema") != TCTObservationEnvelope.CurrentSchema ||
                (string)x.Attribute("runId") != attempt.RunId ||
                (string)x.Attribute("attemptId") != attempt.AttemptId)
                throw new InvalidDataException("Observation schema/Attempt mismatch");
            TCTArtifactRef payload = x.Attribute("payloadPath") == null ? null :
                new TCTArtifactRef((string)x.Attribute("payloadPath"),
                    (string)x.Attribute("payloadMime"), (string)x.Attribute("payloadHash"),
                    (long)x.Attribute("payloadBytes"), attempt.AttemptId);
            TCTArtifactRef evidence = new TCTArtifactRef((string)x.Attribute("evidencePath"),
                TCTObservationCodec.Mime, (string)x.Attribute("evidenceHash"),
                (long)x.Attribute("evidenceBytes"), attempt.AttemptId);
            TCTObservationEnvelope envelope = new TCTObservationEnvelope(
                (int)x.Attribute("schema"), attempt.RunId, attempt.AttemptId,
                kind, title, origin, x.Value, (string)x.Attribute("sourceObjectId"),
                (string)x.Attribute("definitionAddress"), (string)x.Attribute("runtimeInstanceId"),
                payload, new DateTime((long)x.Attribute("utcTicks"), DateTimeKind.Utc));
            return new TCTTestObservation(kind, title, x.Value, origin, envelope, evidence);
        }

        public static void Write(string file, TCTTestCaseResult result)
        {
            if (result == null) throw new ArgumentNullException("result");
            System.Xml.Linq.XElement root = new System.Xml.Linq.XElement("TestResult",
                new System.Xml.Linq.XAttribute("schema", "1"),
                new System.Xml.Linq.XAttribute("id", result.Descriptor.Id),
                new System.Xml.Linq.XAttribute("status", result.Status.ToString()),
                new System.Xml.Linq.XAttribute("ticks", result.Elapsed.Ticks),
                new System.Xml.Linq.XAttribute("runId", result.RunId),
                new System.Xml.Linq.XAttribute("attemptId", result.AttemptId),
                new System.Xml.Linq.XAttribute("iteration", result.Attempt == null ? 0 : result.Attempt.Iteration),
                new System.Xml.Linq.XAttribute("seed", result.Attempt == null ? 0 : result.Attempt.Seed),
                new System.Xml.Linq.XElement("Error", result.Error),
                new System.Xml.Linq.XElement("Artifacts", result.Artifacts.Select(a =>
                    new System.Xml.Linq.XElement("Artifact",
                        new System.Xml.Linq.XAttribute("path", a.RelativePath),
                        new System.Xml.Linq.XAttribute("mime", a.Mime),
                        new System.Xml.Linq.XAttribute("hash", a.Sha256),
                        new System.Xml.Linq.XAttribute("bytes", a.ByteSize),
                        new System.Xml.Linq.XAttribute("attemptId", a.AttemptId)))),
                new System.Xml.Linq.XElement("Diagnostics", result.Diagnostics.Select(d =>
                    new System.Xml.Linq.XElement("Diagnostic",
                        new System.Xml.Linq.XAttribute("code", d.Code),
                        new System.Xml.Linq.XAttribute("severity", d.Severity),
                        new System.Xml.Linq.XAttribute("stage", d.Stage),
                        d.Message))),
                new System.Xml.Linq.XElement("Assertions", result.Assertions.Select(a =>
                    new System.Xml.Linq.XElement("Assertion",
                        new System.Xml.Linq.XAttribute("name", a.Name),
                        new System.Xml.Linq.XAttribute("passed", a.Passed),
                        new System.Xml.Linq.XElement("Expected", a.Expected),
                        new System.Xml.Linq.XElement("Actual", a.Actual)))),
                new System.Xml.Linq.XElement("Observations",
                    result.Observations.Select(WriteObservation)));
            string temp = file + ".partial";
            new System.Xml.Linq.XDocument(root).Save(temp);
            if (File.Exists(file)) File.Delete(file);
            File.Move(temp, file);
        }
        public static TCTTestCaseResult Read(string file, TCTTestCaseDescriptor descriptor, TCTTestAttempt expectedAttempt = null)
        {
            System.Xml.Linq.XElement root = System.Xml.Linq.XDocument.Load(file).Root;
            if (root == null || (string)root.Attribute("schema") != "1" ||
                (string)root.Attribute("id") != descriptor.Id)
                throw new InvalidDataException("Worker result schema/id mismatch");
            if (expectedAttempt != null &&
                ((string)root.Attribute("runId") != expectedAttempt.RunId ||
                 (string)root.Attribute("attemptId") != expectedAttempt.AttemptId ||
                 (int)root.Attribute("iteration") != expectedAttempt.Iteration ||
                 (int)root.Attribute("seed") != expectedAttempt.Seed))
                throw new InvalidDataException("Worker result Run/Attempt identity mismatch");
            TCTTestAttempt attempt = expectedAttempt ??
                new TCTTestAttempt((string)root.Attribute("runId"), descriptor.Id,
                    (int)root.Attribute("iteration"), (int)root.Attribute("seed"),
                    (string)root.Attribute("attemptId"));
            TCTTestStatus status;
            if (!Enum.TryParse((string)root.Attribute("status"), out status))
                throw new InvalidDataException("Worker result status invalid");
            long ticks = (long)root.Attribute("ticks");
            List<TCTAssertionResult> assertions = root.Element("Assertions").Elements("Assertion")
                .Select(x => new TCTAssertionResult((string)x.Attribute("name"),
                    (string)x.Element("Expected"), (string)x.Element("Actual"),
                    (bool)x.Attribute("passed"))).ToList();
            List<TCTTestObservation> observations = root.Element("Observations").Elements("Observation")
                .Select(x => ReadObservation(x, attempt)).ToList();
            List<TCTArtifactRef> artifacts = root.Element("Artifacts") == null ?
                new List<TCTArtifactRef>() : root.Element("Artifacts").Elements("Artifact")
                .Select(a => new TCTArtifactRef((string)a.Attribute("path"), (string)a.Attribute("mime"),
                    (string)a.Attribute("hash"), (long)a.Attribute("bytes"),
                    (string)a.Attribute("attemptId"))).ToList();
            if (artifacts.Any(a => a.AttemptId != attempt.AttemptId))
                throw new InvalidDataException("Artifact attempt identity mismatch");
            List<TCTTestDiagnostic> diagnostics = root.Element("Diagnostics") == null ?
                new List<TCTTestDiagnostic>() : root.Element("Diagnostics").Elements("Diagnostic")
                .Select(d => new TCTTestDiagnostic((string)d.Attribute("code"),
                    (TCTTestDiagnosticSeverity)Enum.Parse(typeof(TCTTestDiagnosticSeverity),
                    (string)d.Attribute("severity")), (string)d.Attribute("stage"),
                    d.Value, attempt.AttemptId)).ToList();
            return TCTTestCaseResult.FromTransport(descriptor, status, new TimeSpan(ticks),
                (string)root.Element("Error"), assertions, observations, attempt, artifacts, diagnostics);
        }
    }
}
