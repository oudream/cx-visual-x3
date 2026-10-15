using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.TestCenter.Core;

namespace CxVisual.TestCenter.Execution
{
    // 子进程隔离由同一个TestCenter.exe的--worker入口实现，不新增工程/DLL。
    public sealed class TCTTestHostController
    {
        public async Task<TCTTestCaseResult> RunIsolatedAsync(ITCTTestCase test,
            TimeSpan timeout, int seed, CancellationToken cancellationToken)
        {
            if (test == null) throw new ArgumentNullException("test");
            string directory = Path.Combine(Path.GetTempPath(), "CxVisual-TCT",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string resultFile = Path.Combine(directory, "result.xml");
            Stopwatch watch = Stopwatch.StartNew();
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                string executable = Process.GetCurrentProcess().MainModule.FileName;
                ProcessStartInfo start = new ProcessStartInfo(executable);
                start.Arguments = "--worker " + Quote(test.Descriptor.Id) + " " +
                    Quote(resultFile) + " " + seed.ToString(CultureInfo.InvariantCulture);
                start.UseShellExecute = false;
                start.CreateNoWindow = true;
                start.WindowStyle = ProcessWindowStyle.Hidden;
                start.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
                using (Process child = Process.Start(start))
                {
                    if (child == null) throw new InvalidOperationException("Cannot start TCT worker process");
                    Task exited = Task.Run(() => child.WaitForExit());
                    Task deadline = Task.Delay(timeout);
                    Task cancelled = Task.Delay(Timeout.Infinite, cancellationToken);
                    Task first = await Task.WhenAny(exited, deadline, cancelled).ConfigureAwait(false);
                    if (first != exited)
                    {
                        TryKill(child);
                        await exited.ConfigureAwait(false);
                        return Failure(test.Descriptor, first == cancelled ?
                            TCTTestStatus.Cancelled : TCTTestStatus.TimedOut, watch.Elapsed,
                            first == cancelled ? "Run cancelled; worker terminated" :
                                "Child process timeout (" + timeout + "); worker terminated");
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!File.Exists(resultFile))
                        return Failure(test.Descriptor, TCTTestStatus.Error, watch.Elapsed,
                            "Worker exited " + child.ExitCode + " without result file");
                    TCTTestCaseResult result = TCTWireCodec.Read(resultFile, test.Descriptor);
                    if (child.ExitCode != 0 && result.Status == TCTTestStatus.Passed)
                        return Failure(test.Descriptor, TCTTestStatus.Error, watch.Elapsed,
                            "Worker exited unexpectedly (" + child.ExitCode + ")");
                    return result;
                }
            }
            catch (OperationCanceledException)
            {
                return Failure(test.Descriptor, TCTTestStatus.Cancelled, watch.Elapsed, "Run cancelled");
            }
            catch (Exception ex)
            {
                return Failure(test.Descriptor, TCTTestStatus.Error, watch.Elapsed, ex.ToString());
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
        private static void TryKill(Process process)
        {
            try { if (!process.HasExited) process.Kill(); }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        }
        internal static TCTTestCaseResult Failure(TCTTestCaseDescriptor descriptor,
            TCTTestStatus status, TimeSpan elapsed, string error)
        {
            return TCTTestCaseResult.FromTransport(descriptor, status, elapsed, error,
                new List<TCTAssertionResult>(), new List<TCTTestObservation>());
        }
    }

    public sealed class TCTTestExecutionCoordinator
    {
        private readonly TCTTestHostController _host = new TCTTestHostController();

        // 限制并发Worker数，保留用例结果原顺序；观察回调可能并发发生，UI负责Dispatcher封送。
        public async Task<TCTTestRunSummary> RunAsync(TCTTestCatalog catalog, TCTTestRunRequest request,
            CancellationToken cancellationToken, Action<TCTTestCaseResult> onResult)
        {
            if (catalog == null) throw new ArgumentNullException("catalog");
            if (request == null) throw new ArgumentNullException("request");
            request.Validate();
            IList<ITCTTestCase> tests = catalog.Select(request);
            int count = checked(tests.Count * request.Repeat);
            if (count == 0) return new TCTTestRunSummary(new List<TCTTestCaseResult>());
            TCTTestCaseResult[] results = new TCTTestCaseResult[count];
            int next = -1;
            int workers = Math.Min(count, request.MaxConcurrency);
            List<Task> tasks = new List<Task>();
            for (int w = 0; w < workers; w++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        int index = Interlocked.Increment(ref next);
                        if (index >= count) break;
                        ITCTTestCase test = tests[index % tests.Count];
                        int seed = unchecked(request.Seed + index / tests.Count);
                        TCTTestCaseResult result;
                        if (request.Isolation == TCTTestIsolation.ChildProcess)
                            result = await _host.RunIsolatedAsync(test, request.Timeout, seed,
                                cancellationToken).ConfigureAwait(false);
                        else
                            // InProcess为可信测试的调试模式；无法强杀死锁线程。
                            result = await TCTTestRunner.RunOneAsync(test, cancellationToken, seed)
                                .ConfigureAwait(false);
                        results[index] = result;
                        if (onResult != null) onResult(result);
                    }
                }, CancellationToken.None));
            }
            await Task.WhenAll(tasks).ConfigureAwait(false);
            return new TCTTestRunSummary(results.Where(x => x != null).ToList());
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
        public static void Write(string file, TCTTestCaseResult result)
        {
            if (result == null) throw new ArgumentNullException("result");
            System.Xml.Linq.XElement root = new System.Xml.Linq.XElement("TestResult",
                new System.Xml.Linq.XAttribute("schema", "1"),
                new System.Xml.Linq.XAttribute("id", result.Descriptor.Id),
                new System.Xml.Linq.XAttribute("status", result.Status.ToString()),
                new System.Xml.Linq.XAttribute("ticks", result.Elapsed.Ticks),
                new System.Xml.Linq.XElement("Error", result.Error),
                new System.Xml.Linq.XElement("Assertions", result.Assertions.Select(a =>
                    new System.Xml.Linq.XElement("Assertion",
                        new System.Xml.Linq.XAttribute("name", a.Name),
                        new System.Xml.Linq.XAttribute("passed", a.Passed),
                        new System.Xml.Linq.XElement("Expected", a.Expected),
                        new System.Xml.Linq.XElement("Actual", a.Actual)))),
                new System.Xml.Linq.XElement("Observations", result.Observations.Select(o =>
                    new System.Xml.Linq.XElement("Observation",
                        new System.Xml.Linq.XAttribute("kind", o.Kind),
                        new System.Xml.Linq.XAttribute("title", o.Title),
                        new System.Xml.Linq.XAttribute("origin", o.Origin),
                        o.Details))));
            string temp = file + ".partial";
            new System.Xml.Linq.XDocument(root).Save(temp);
            if (File.Exists(file)) File.Delete(file);
            File.Move(temp, file);
        }
        public static TCTTestCaseResult Read(string file, TCTTestCaseDescriptor descriptor)
        {
            System.Xml.Linq.XElement root = System.Xml.Linq.XDocument.Load(file).Root;
            if (root == null || (string)root.Attribute("schema") != "1" ||
                (string)root.Attribute("id") != descriptor.Id)
                throw new InvalidDataException("Worker result schema/id mismatch");
            TCTTestStatus status;
            if (!Enum.TryParse((string)root.Attribute("status"), out status))
                throw new InvalidDataException("Worker result status invalid");
            long ticks = (long)root.Attribute("ticks");
            List<TCTAssertionResult> assertions = root.Element("Assertions").Elements("Assertion")
                .Select(x => new TCTAssertionResult((string)x.Attribute("name"),
                    (string)x.Element("Expected"), (string)x.Element("Actual"),
                    (bool)x.Attribute("passed"))).ToList();
            List<TCTTestObservation> observations = root.Element("Observations").Elements("Observation")
                .Select(x => new TCTTestObservation((string)x.Attribute("kind"),
                    (string)x.Attribute("title"), x.Value, (string)x.Attribute("origin"))).ToList();
            return TCTTestCaseResult.FromTransport(descriptor, status, new TimeSpan(ticks),
                (string)root.Element("Error"), assertions, observations);
        }
    }
}
