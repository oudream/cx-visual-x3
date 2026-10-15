using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.TestCenter.Core
{
    // 测试框架专有契约：生产代码不依赖TCT命名空间。
    public enum TCTTestAspect { Foundation, Mechanism, Integration }
    public enum TCTTestStatus { NotRun, Passed, Failed, Cancelled, Skipped, Unsupported, TimedOut, Error, Inconclusive }
    public enum TCTTestIsolation { InProcess, ChildProcess }
    public enum TCTTestKind { Unit, Integration, Regression, Stress, Benchmark }

    public interface ITCTTestCase
    {
        TCTTestCaseDescriptor Descriptor { get; }
        void Execute(TCTTestContext context);
    }
    public interface ITCTAsyncTestCase
    {
        TCTTestCaseDescriptor Descriptor { get; }
        Task ExecuteAsync(TCTTestContext context);
    }
    public interface ITCTTestDiscoverer
    {
        string Id { get; }
        IEnumerable<ITCTTestCase> Discover();
    }
    public interface ITCTTestObserver
    {
        void Start(TCTTestContext context);
        void Stop();
    }
    public interface ITCTTestFrameworkAdapter
    {
        string Id { get; }
        IEnumerable<ITCTTestCase> Discover();
    }
    public interface ITCTTestResultSink
    {
        void OnResult(TCTTestCaseResult result);
    }

    public sealed class TCTTestCaseDescriptor
    {
        public TCTTestCaseDescriptor(string id, string name, TCTTestAspect aspect, string group,
            string capabilityAreaId, string capabilityId, string objectId)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("TestId and Name are required");
            Id = id; Name = name; Aspect = aspect;
            Group = group ?? string.Empty;
            CapabilityAreaId = capabilityAreaId ?? "Unmapped";
            CapabilityId = capabilityId ?? "Unmapped";
            ObjectId = objectId ?? "Unmapped";
        }
        public string Id { get; private set; }
        public string Name { get; private set; }
        public TCTTestAspect Aspect { get; private set; }
        public string Group { get; private set; }
        public string CapabilityAreaId { get; private set; }
        public string CapabilityId { get; private set; }
        public string ObjectId { get; private set; }
    }

    public sealed class TCTTestRunRequest
    {
        public TCTTestRunRequest()
        {
            Repeat = 1; MaxConcurrency = 1; Timeout = TimeSpan.FromSeconds(30);
            Isolation = TCTTestIsolation.ChildProcess;
            Seed = 20261015;
            SelectedIds = new List<string>();
        }
        public IList<string> SelectedIds { get; private set; }
        public int Repeat { get; set; }
        public int MaxConcurrency { get; set; }
        public TimeSpan Timeout { get; set; }
        public int Seed { get; set; }
        public TCTTestIsolation Isolation { get; set; }
        public string Filter { get; set; }
        public void Validate()
        {
            if (Repeat < 1 || Repeat > 100000) throw new ArgumentOutOfRangeException("Repeat");
            if (MaxConcurrency < 1 || MaxConcurrency > 64) throw new ArgumentOutOfRangeException("MaxConcurrency");
            if (Timeout < TimeSpan.FromMilliseconds(100) || Timeout > TimeSpan.FromHours(1))
                throw new ArgumentOutOfRangeException("Timeout");
        }
    }

    public sealed class TCTAssertionResult
    {
        public TCTAssertionResult(string name, string expected, string actual, bool passed)
        {
            Name = name ?? string.Empty; Expected = expected ?? string.Empty;
            Actual = actual ?? string.Empty; Passed = passed;
        }
        public string Name { get; private set; }
        public string Expected { get; private set; }
        public string Actual { get; private set; }
        public bool Passed { get; private set; }
    }

    public sealed class TCTTestObservation
    {
        public TCTTestObservation(string kind, string title, string details, string origin = "Actual")
        {
            Kind = kind ?? "Generic"; Title = title ?? string.Empty;
            Details = details ?? string.Empty; Origin = origin ?? "Actual";
        }
        public string Kind { get; private set; }
        public string Title { get; private set; }
        public string Details { get; private set; }
        public string Origin { get; private set; }
    }

    public sealed class TCTTestContext
    {
        private readonly List<TCTAssertionResult> _assertions = new List<TCTAssertionResult>();
        private readonly List<TCTTestObservation> _observations = new List<TCTTestObservation>();
        internal TCTTestContext(CancellationToken token, int seed = 0)
        {
            CancellationToken = token; Seed = seed;
        }
        public CancellationToken CancellationToken { get; private set; }
        public int Seed { get; private set; }
        public IList<TCTAssertionResult> Assertions { get { return _assertions.AsReadOnly(); } }
        public IList<TCTTestObservation> Observations { get { return _observations.AsReadOnly(); } }
        public void ThrowIfCancelled() { CancellationToken.ThrowIfCancellationRequested(); }
        public void AssertTrue(bool condition, string name, object expected, object actual)
        {
            ThrowIfCancelled();
            string e = TCTFormat.Value(expected), a = TCTFormat.Value(actual);
            _assertions.Add(new TCTAssertionResult(name, e, a, condition));
            if (!condition) throw new TCTAssertionException(name, e, a);
        }
        public void AssertEqual<T>(T expected, T actual, string name)
        {
            AssertTrue(EqualityComparer<T>.Default.Equals(expected, actual), name, expected, actual);
        }
        public void AssertNear(double expected, double actual, double absoluteTolerance, string name)
        {
            if (double.IsNaN(absoluteTolerance) || absoluteTolerance < 0) throw new ArgumentOutOfRangeException("absoluteTolerance");
            bool equal = !double.IsNaN(actual) && Math.Abs(expected - actual) <= absoluteTolerance;
            AssertTrue(equal, name + " (tolerance=" + TCTFormat.Value(absoluteTolerance) + ")", expected, actual);
        }
        public void AssertThrows<T>(Action action, string name) where T : Exception
        {
            if (action == null) throw new ArgumentNullException("action");
            try { action(); }
            catch (T) { AssertTrue(true, name, typeof(T).Name, typeof(T).Name); return; }
            catch (Exception ex) { AssertTrue(false, name, typeof(T).Name, ex.GetType().Name); return; }
            AssertTrue(false, name, typeof(T).Name, "No exception");
        }
        public void Observe(string kind, string title, string details, string origin = "Actual")
        {
            ThrowIfCancelled();
            _observations.Add(new TCTTestObservation(kind, title, details, origin));
        }
    }

    public sealed class TCTAssertionException : Exception
    {
        public TCTAssertionException(string name, string expected, string actual)
            : base("Assertion failed: " + name + "; expected=" + expected + "; actual=" + actual) { }
    }

    public static class TCTFormat
    {
        public static string Value(object value)
        {
            return value == null ? "<null>" : Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }

    public sealed class TCTTestCaseResult
    {
        internal TCTTestCaseResult(TCTTestCaseDescriptor descriptor, TCTTestStatus status,
            TimeSpan elapsed, string error, IList<TCTAssertionResult> assertions,
            IList<TCTTestObservation> observations)
        {
            Descriptor = descriptor; Status = status; Elapsed = elapsed; Error = error ?? string.Empty;
            Assertions = new ReadOnlyCollection<TCTAssertionResult>(new List<TCTAssertionResult>(assertions));
            Observations = new ReadOnlyCollection<TCTTestObservation>(new List<TCTTestObservation>(observations));
        }
        public TCTTestCaseDescriptor Descriptor { get; private set; }
        public TCTTestStatus Status { get; private set; }
        public TimeSpan Elapsed { get; private set; }
        public string Error { get; private set; }
        public IList<TCTAssertionResult> Assertions { get; private set; }
        public IList<TCTTestObservation> Observations { get; private set; }
        internal static TCTTestCaseResult FromTransport(TCTTestCaseDescriptor descriptor, TCTTestStatus status,
            TimeSpan elapsed, string error, IList<TCTAssertionResult> assertions,
            IList<TCTTestObservation> observations)
        { return new TCTTestCaseResult(descriptor, status, elapsed, error, assertions, observations); }
    }

    public sealed class TCTTestRunSummary
    {
        internal TCTTestRunSummary(IList<TCTTestCaseResult> results)
        { Results = new ReadOnlyCollection<TCTTestCaseResult>(new List<TCTTestCaseResult>(results)); }
        public IList<TCTTestCaseResult> Results { get; private set; }
        public int Total { get { return Results.Count; } }
        public int Passed { get { return Results.Count(r => r.Status == TCTTestStatus.Passed); } }
        public int Failed { get { return Results.Count(r => r.Status == TCTTestStatus.Failed || r.Status == TCTTestStatus.Error || r.Status == TCTTestStatus.TimedOut); } }
        public int Cancelled { get { return Results.Count(r => r.Status == TCTTestStatus.Cancelled); } }
        public int Unsupported { get { return Results.Count(r => r.Status == TCTTestStatus.Unsupported || r.Status == TCTTestStatus.Inconclusive); } }
        public bool Success { get { return Total > 0 && Passed == Total; } }
    }

    public sealed class TCTTestCatalog
    {
        private readonly List<ITCTTestCase> _cases = new List<ITCTTestCase>();
        public IList<ITCTTestCase> Cases { get { return _cases.AsReadOnly(); } }
        public void Register(ITCTTestCase test)
        {
            if (test == null || test.Descriptor == null) throw new ArgumentNullException("test");
            if (_cases.Any(c => string.Equals(c.Descriptor.Id, test.Descriptor.Id, StringComparison.Ordinal)))
                throw new InvalidOperationException("Duplicate TestId: " + test.Descriptor.Id);
            _cases.Add(test);
        }
        public IList<ITCTTestCase> Select(TCTTestRunRequest request)
        {
            if (request == null) throw new ArgumentNullException("request");
            request.Validate();
            return _cases.Where(c =>
                (request.SelectedIds.Count == 0 || request.SelectedIds.Contains(c.Descriptor.Id)) &&
                (string.IsNullOrEmpty(request.Filter) || c.Descriptor.Id.IndexOf(request.Filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 c.Descriptor.Name.IndexOf(request.Filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 c.Descriptor.Group.IndexOf(request.Filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 c.Descriptor.CapabilityAreaId.IndexOf(request.Filter, StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList().AsReadOnly();
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
                TCTTestCaseResult result = await RunOneAsync(test, cancellationToken, 0).ConfigureAwait(false);
                results.Add(result);
                if (onCompleted != null) onCompleted(result);
            }
            return new TCTTestRunSummary(results);
        }
        public static Task<TCTTestCaseResult> RunOneAsync(ITCTTestCase test, CancellationToken token, int seed)
        {
            if (test == null) throw new ArgumentNullException("test");
            return Task.Run(() =>
            {
                TCTTestContext context = new TCTTestContext(token, seed);
                Stopwatch watch = Stopwatch.StartNew();
                TCTTestStatus status = TCTTestStatus.Passed;
                string error = string.Empty;
                try
                {
                    context.ThrowIfCancelled(); test.Execute(context); context.ThrowIfCancelled();
                    if (context.Assertions.Count == 0) status = TCTTestStatus.Inconclusive;
                }
                catch (OperationCanceledException) { status = TCTTestStatus.Cancelled; error = "Cancelled"; }
                catch (TCTAssertionException ex) { status = TCTTestStatus.Failed; error = ex.ToString(); }
                catch (NotSupportedException ex) { status = TCTTestStatus.Unsupported; error = ex.ToString(); }
                catch (Exception ex) { status = TCTTestStatus.Error; error = ex.ToString(); }
                finally { watch.Stop(); }
                return new TCTTestCaseResult(test.Descriptor, status, watch.Elapsed,
                    error, context.Assertions, context.Observations);
            }, CancellationToken.None);
        }
    }
}
