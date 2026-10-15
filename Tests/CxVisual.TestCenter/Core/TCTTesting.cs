using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.TestCenter.Core
{
    // 测试专用类型统一 TCT 前缀；正式产品不依赖本程序集。
    public enum TCTTestStatus { NotRun, Passed, Failed, Cancelled }
    public enum TCTTestAspect { Foundation, Mechanism }

    public interface ITCTTestCase
    {
        TCTTestCaseDescriptor Descriptor { get; }
        void Execute(TCTTestContext context);
    }

    public sealed class TCTTestCaseDescriptor
    {
        public TCTTestCaseDescriptor(string id, string name, TCTTestAspect aspect, string group,
            string capabilityAreaId, string capabilityId, string objectId)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("TestId is required", "id");
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Test name is required", "name");
            Id = id; Name = name; Aspect = aspect; Group = group ?? string.Empty;
            CapabilityAreaId = capabilityAreaId ?? string.Empty;
            CapabilityId = capabilityId ?? string.Empty;
            ObjectId = objectId ?? string.Empty;
        }
        public string Id { get; private set; }
        public string Name { get; private set; }
        public TCTTestAspect Aspect { get; private set; }
        public string Group { get; private set; }
        public string CapabilityAreaId { get; private set; }
        public string CapabilityId { get; private set; }
        public string ObjectId { get; private set; }
    }

    public sealed class TCTAssertionResult
    {
        public TCTAssertionResult(string name, string expected, string actual, bool passed)
        { Name = name; Expected = expected; Actual = actual; Passed = passed; }
        public string Name { get; private set; }
        public string Expected { get; private set; }
        public string Actual { get; private set; }
        public bool Passed { get; private set; }
    }

    public sealed class TCTTestObservation
    {
        public TCTTestObservation(string kind, string title, string details)
        { Kind = kind; Title = title; Details = details; }
        public string Kind { get; private set; }
        public string Title { get; private set; }
        public string Details { get; private set; }
    }

    public sealed class TCTTestContext
    {
        private readonly List<TCTAssertionResult> _assertions = new List<TCTAssertionResult>();
        private readonly List<TCTTestObservation> _observations = new List<TCTTestObservation>();
        internal TCTTestContext(CancellationToken cancellationToken) { CancellationToken = cancellationToken; }
        public CancellationToken CancellationToken { get; private set; }
        public IList<TCTAssertionResult> Assertions { get { return _assertions.AsReadOnly(); } }
        public IList<TCTTestObservation> Observations { get { return _observations.AsReadOnly(); } }
        public void ThrowIfCancelled() { CancellationToken.ThrowIfCancellationRequested(); }
        public void AssertTrue(bool condition, string name, object expected, object actual)
        {
            ThrowIfCancelled();
            string e = Convert.ToString(expected, System.Globalization.CultureInfo.InvariantCulture);
            string a = Convert.ToString(actual, System.Globalization.CultureInfo.InvariantCulture);
            _assertions.Add(new TCTAssertionResult(name, e, a, condition));
            if (!condition) throw new InvalidOperationException("Assertion failed: " + name + "; expected=" + e + "; actual=" + a);
        }
        public void Observe(string kind, string title, string details)
        {
            ThrowIfCancelled();
            _observations.Add(new TCTTestObservation(kind, title, details));
        }
    }

    public sealed class TCTTestCaseResult
    {
        internal TCTTestCaseResult(TCTTestCaseDescriptor descriptor, TCTTestStatus status, TimeSpan elapsed,
            string error, IList<TCTAssertionResult> assertions, IList<TCTTestObservation> observations)
        {
            Descriptor = descriptor; Status = status; Elapsed = elapsed; Error = error ?? string.Empty;
            Assertions = new List<TCTAssertionResult>(assertions).AsReadOnly();
            Observations = new List<TCTTestObservation>(observations).AsReadOnly();
        }
        public TCTTestCaseDescriptor Descriptor { get; private set; }
        public TCTTestStatus Status { get; private set; }
        public TimeSpan Elapsed { get; private set; }
        public string Error { get; private set; }
        public IList<TCTAssertionResult> Assertions { get; private set; }
        public IList<TCTTestObservation> Observations { get; private set; }
    }

    public sealed class TCTTestRunSummary
    {
        internal TCTTestRunSummary(IList<TCTTestCaseResult> results)
        {
            Results = new List<TCTTestCaseResult>(results).AsReadOnly();
        }
        public IList<TCTTestCaseResult> Results { get; private set; }
        public int Passed { get { return Results.Count(r => r.Status == TCTTestStatus.Passed); } }
        public int Failed { get { return Results.Count(r => r.Status == TCTTestStatus.Failed); } }
        public int Cancelled { get { return Results.Count(r => r.Status == TCTTestStatus.Cancelled); } }
        public int Total { get { return Results.Count; } }
    }

    public sealed class TCTTestCatalog
    {
        private readonly List<ITCTTestCase> _cases = new List<ITCTTestCase>();
        public IList<ITCTTestCase> Cases { get { return _cases.AsReadOnly(); } }
        public void Register(ITCTTestCase test)
        {
            if (test == null || test.Descriptor == null) throw new ArgumentNullException("test");
            if (_cases.Any(c => c.Descriptor.Id == test.Descriptor.Id))
                throw new InvalidOperationException("Duplicate TestId: " + test.Descriptor.Id);
            _cases.Add(test);
        }
    }

    public sealed class TCTTestRunner
    {
        public async Task<TCTTestRunSummary> RunAsync(IEnumerable<ITCTTestCase> cases,
            CancellationToken cancellationToken, Action<TCTTestCaseResult> onCompleted)
        {
            if (cases == null) throw new ArgumentNullException("cases");
            List<TCTTestCaseResult> results = new List<TCTTestCaseResult>();
            foreach (ITCTTestCase test in cases)
            {
                if (cancellationToken.IsCancellationRequested) break;
                TCTTestCaseResult result = await Task.Run(() => RunOne(test, cancellationToken));
                results.Add(result);
                if (onCompleted != null) onCompleted(result);
            }
            return new TCTTestRunSummary(results);
        }

        private static TCTTestCaseResult RunOne(ITCTTestCase test, CancellationToken cancellationToken)
        {
            if (test == null) throw new ArgumentException("Test is null", "test");
            TCTTestContext context = new TCTTestContext(cancellationToken);
            Stopwatch watch = Stopwatch.StartNew();
            TCTTestStatus status = TCTTestStatus.Passed;
            string error = string.Empty;
            try { context.ThrowIfCancelled(); test.Execute(context); context.ThrowIfCancelled(); }
            catch (OperationCanceledException) { status = TCTTestStatus.Cancelled; error = "Cancelled"; }
            catch (Exception ex) { status = TCTTestStatus.Failed; error = ex.ToString(); }
            finally { watch.Stop(); }
            return new TCTTestCaseResult(test.Descriptor, status, watch.Elapsed, error,
                context.Assertions, context.Observations);
        }
    }

    public static class TCTReportWriter
    {
        public static void WriteText(string path, TCTTestRunSummary summary)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Report path required", "path");
            if (summary == null) throw new ArgumentNullException("summary");
            string full = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(full);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            using (StreamWriter writer = new StreamWriter(full, false, new UTF8Encoding(false)))
            {
                writer.WriteLine("CxVisual.TestCenter / " + DateTime.UtcNow.ToString("o"));
                writer.WriteLine("Total=" + summary.Total + " Passed=" + summary.Passed +
                    " Failed=" + summary.Failed + " Cancelled=" + summary.Cancelled);
                foreach (TCTTestCaseResult result in summary.Results)
                {
                    writer.WriteLine(result.Status + " | " + result.Descriptor.Id + " | " +
                        result.Descriptor.Name + " | " + result.Elapsed.TotalMilliseconds.ToString("F3") + "ms");
                    foreach (TCTAssertionResult assertion in result.Assertions)
                        writer.WriteLine("  " + (assertion.Passed ? "PASS" : "FAIL") + " " +
                            assertion.Name + " expected=" + assertion.Expected + " actual=" + assertion.Actual);
                    if (result.Error.Length > 0) writer.WriteLine("  ERROR " + result.Error);
                }
            }
        }
    }
}
