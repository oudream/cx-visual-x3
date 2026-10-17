using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.TestCenter.Models;
using CxVisual.TestCenter.Fixtures;
using CxVisual.TestCenter.Execution;

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
    // Allows a supported external test framework to deliver its native verdict unchanged.
    // Ordinary TCT test cases still use the existing Runner and Context assertions.
    public interface ITCTExternalResultTestCase : ITCTTestCase
    {
        TCTTestCaseResult ExecuteExternal(TCTTestAttempt attempt, CancellationToken cancellation,
            TCTTestArtifactStore artifacts);
    }

    public interface ITCTTestResultSink
    {
        void OnResult(TCTTestCaseResult result);
    }

    public sealed class TCTTestCaseDescriptor
    {
        public TCTTestCaseDescriptor(string id, string name, TCTTestAspect aspect, string group,
            string capabilityAreaId, string capabilityId, string objectId,
            TCTTestKind kind = TCTTestKind.Unit, IEnumerable<string> exclusiveResources = null,
            IEnumerable<string> tags = null)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("TestId and Name are required");
            Id = id; Name = name; Aspect = aspect;
            Group = group ?? string.Empty;
            CapabilityAreaId = capabilityAreaId ?? "Unmapped";
            CapabilityId = capabilityId ?? "Unmapped";
            ObjectId = objectId ?? "Unmapped"; Kind = kind;
            List<string> keys = new List<string> { "Test:" + id };
            if (exclusiveResources != null)
                foreach (string key in exclusiveResources)
                {
                    if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Blank resource key");
                    if (!keys.Contains(key)) keys.Add(key);
                }
            ExclusiveResourceKeys = new ReadOnlyCollection<string>(keys);
            Tags = new ReadOnlyCollection<string>(tags == null ? new List<string>() :
                tags.Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .Distinct(StringComparer.Ordinal).ToList());
        }
        public string Id { get; private set; }
        public string Name { get; private set; }
        public TCTTestAspect Aspect { get; private set; }
        public string Group { get; private set; }
        public string CapabilityAreaId { get; private set; }
        public string CapabilityId { get; private set; }
        public string ObjectId { get; private set; }
        public TCTTestKind Kind { get; private set; }
        public IList<string> Tags { get; private set; }
        public IList<string> ExclusiveResourceKeys { get; private set; }
    }

    public sealed class TCTTestRunRequest
    {
        public TCTTestRunRequest()
        {
            Repeat = 1; MaxConcurrency = 1; Timeout = TimeSpan.FromSeconds(30);
            RunId = Guid.NewGuid().ToString("N"); Profile = TCTTestRunProfile.Smoke;
            Isolation = TCTTestIsolation.ChildProcess;
            Seed = 20261015;
            SelectedIds = new List<string>();
        }
        public IList<string> SelectedIds { get; private set; }
        public string RunId { get; private set; }
        public TCTTestRunProfile Profile { get; set; }
        public bool FailFast { get; set; }
        public int Repeat { get; set; }
        public int MaxConcurrency { get; set; }
        public TimeSpan Timeout { get; set; }
        public int Seed { get; set; }
        public TCTTestIsolation Isolation { get; set; }
        public string Filter { get; set; }
        public void Validate()
        {
            if (Repeat < 1 || Repeat > 100000) throw new ArgumentOutOfRangeException("Repeat");
            if (string.IsNullOrWhiteSpace(RunId)) throw new ArgumentException("RunId required");
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
        public TCTTestObservation(string kind, string title, string details, string origin = "Actual",
            TCTObservationEnvelope envelope = null, TCTArtifactRef evidence = null)
        {
            Kind = kind ?? "Generic"; Title = title ?? string.Empty;
            Details = details ?? string.Empty; Origin = origin ?? "Actual";
            Envelope = envelope; Evidence = evidence;
            if (Envelope != null && (Envelope.Kind != Kind || Envelope.Title != Title ||
                Envelope.Origin != Origin || Envelope.Details != Details ||
                Evidence == null || Evidence.Mime != TCTObservationCodec.Mime ||
                Evidence.AttemptId != Envelope.AttemptId))
                throw new InvalidDataException("Structured observation does not match envelope/evidence");
        }
        public TCTObservationEnvelope Envelope { get; private set; }
        public TCTArtifactRef Evidence { get; private set; }
        public string Kind { get; private set; }
        public string Title { get; private set; }
        public string Details { get; private set; }
        public string Origin { get; private set; }
    }

    public sealed class TCTTestContext
    {
        private readonly List<TCTAssertionResult> _assertions = new List<TCTAssertionResult>();
        private readonly List<TCTTestObservation> _observations = new List<TCTTestObservation>();
        private readonly List<TCTArtifactRef> _artifacts = new List<TCTArtifactRef>();
        private readonly List<TCTTestDiagnostic> _diagnostics = new List<TCTTestDiagnostic>();
        private readonly TCTTestArtifactStore _artifactStore;
        private readonly Action<TCTTestEvent> _onEvent;
        private long _eventSequence;
        internal TCTTestContext(CancellationToken token, int seed = 0,
            TCTTestAttempt attempt = null, TCTTestArtifactStore artifactStore = null,
            Action<TCTTestEvent> onEvent = null)
        {
            CancellationToken = token; Seed = seed; Attempt = attempt; _artifactStore = artifactStore;
            _onEvent = onEvent;
        }
        internal void Publish(TCTTestEventKind kind, string message)
        {
            if (_onEvent == null) return;
            if (_eventSequence >= 10000)
                throw new InvalidOperationException("Event capacity exhausted; cannot silently discard evidence");
            _onEvent(new TCTTestEvent(++_eventSequence, AttemptId, kind, message));
        }
        public TCTTestAttempt Attempt { get; private set; }
        public string RunId { get { return Attempt == null ? "AdHoc" : Attempt.RunId; } }
        public string AttemptId { get { return Attempt == null ? "AdHoc" : Attempt.AttemptId; } }
        public CancellationToken CancellationToken { get; private set; }
        public int Seed { get; private set; }
        public IList<TCTAssertionResult> Assertions { get { return _assertions.AsReadOnly(); } }
        public IList<TCTTestObservation> Observations { get { return _observations.AsReadOnly(); } }
        public IList<TCTArtifactRef> Artifacts { get { return _artifacts.AsReadOnly(); } }
        public IList<TCTTestDiagnostic> Diagnostics { get { return _diagnostics.AsReadOnly(); } }
        public TCTArtifactRef SaveArtifact(byte[] bytes, string mime)
        {
            ThrowIfCancelled();
            if (_artifactStore == null) throw new NotSupportedException("No artifact store configured for attempt");
            TCTArtifactRef artifact = _artifactStore.Save(bytes, mime, AttemptId);
            _artifacts.Add(artifact);
            return artifact;
        }
        public void Diagnostic(string code, TCTTestDiagnosticSeverity severity, string stage, string message)
        {
            _diagnostics.Add(new TCTTestDiagnostic(code, severity, stage, message, AttemptId));
            Publish(TCTTestEventKind.Diagnostic, code + ": " + message);
        }
        public void ThrowIfCancelled() { CancellationToken.ThrowIfCancellationRequested(); }
        public void AssertTrue(bool condition, string name, object expected, object actual)
        {
            ThrowIfCancelled();
            string e = TCTFormat.Value(expected), a = TCTFormat.Value(actual);
            _assertions.Add(new TCTAssertionResult(name, e, a, condition));
            Publish(TCTTestEventKind.Assertion, name + " = " + (condition ? "Passed" : "Failed") + "; expected=" + e + "; actual=" + a);
            if (!condition) throw new TCTAssertionException(name, e, a);
        }
        public void AssertEqual<T>(T expected, T actual, string name)
        {
            AssertTrue(EqualityComparer<T>.Default.Equals(expected, actual), name, expected, actual);
        }
        public void AssertNotEqual<T>(T unwanted, T actual, string name)
        {
            AssertTrue(!EqualityComparer<T>.Default.Equals(unwanted, actual), name, "not "+TCTFormat.Value(unwanted), actual);
        }
        public void AssertSame(object expected, object actual, string name)
        {
            AssertTrue(object.ReferenceEquals(expected, actual), name, "same reference", "same="+object.ReferenceEquals(expected, actual));
        }
        public void AssertBetween(double minimum, double maximum, double actual, string name)
        {
            if (minimum > maximum || double.IsNaN(minimum) || double.IsNaN(maximum))
                throw new ArgumentOutOfRangeException("minimum");
            AssertTrue(!double.IsNaN(actual) && actual >= minimum && actual <= maximum,
                name, "["+TCTFormat.Value(minimum)+","+TCTFormat.Value(maximum)+"]", actual);
        }
        public void AssertSequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string name)
        {
            if (expected == null || actual == null) throw new ArgumentNullException("sequence");
            T[] a = expected.ToArray(), b = actual.ToArray();
            bool equal = a.SequenceEqual(b);
            AssertTrue(equal, name, string.Join(",", a.Select(x => TCTFormat.Value(x))),
                string.Join(",", b.Select(x => TCTFormat.Value(x))));
        }
        public void AssertNear(double expected, double actual, double absoluteTolerance, string name)
        {
            if (double.IsNaN(absoluteTolerance) || absoluteTolerance < 0) throw new ArgumentOutOfRangeException("absoluteTolerance");
            bool equal = TCTNumericComparer.AreClose(expected, actual, absoluteTolerance, 0);
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
        // Opt-in for real Owner observations. Strings alone never claim structured provenance.
        public TCTObservationEnvelope ObserveStructured(string kind, string title, string details,
            string sourceObjectId, string definitionAddress = null, string runtimeInstanceId = null,
            byte[] payload = null, string payloadMime = "application/octet-stream",
            string origin = "Actual")
        {
            ThrowIfCancelled();
            if (_artifactStore == null || Attempt == null)
                throw new NotSupportedException("Structured observations require a persisted Attempt");
            TCTArtifactRef payloadRef = payload == null ? null :
                _artifactStore.Save(payload, payloadMime, Attempt.AttemptId);
            if (payloadRef != null) _artifacts.Add(payloadRef);
            TCTObservationEnvelope envelope = new TCTObservationEnvelope(
                TCTObservationEnvelope.CurrentSchema, RunId, AttemptId,
                kind, title, origin, details, sourceObjectId, definitionAddress,
                runtimeInstanceId, payloadRef);
            TCTArtifactRef evidence = _artifactStore.Save(
                TCTObservationCodec.Serialize(envelope), TCTObservationCodec.Mime, AttemptId);
            _artifacts.Add(evidence);
            _observations.Add(new TCTTestObservation(kind, title, details, origin, envelope, evidence));
            Publish(TCTTestEventKind.Observation, "Schema=" + envelope.SchemaVersion +
                "; Kind=" + kind + "; SourceObjectId=" + envelope.SourceObjectId +
                "; Evidence=" + evidence.Sha256);
            return envelope;
        }

        public void Observe(string kind, string title, string details, string origin = "Actual")
        {
            ThrowIfCancelled();
            _observations.Add(new TCTTestObservation(kind, title, details, origin));
            Publish(TCTTestEventKind.Observation, kind + ": " + title + " | " + details);
        }
    }

    public sealed class TCTAssertionException : Exception
    {
        public TCTAssertionException(string name, string expected, string actual)
            : base("Assertion failed: " + name + "; expected=" + expected + "; actual=" + actual) { }
    }

    public static class TCTNumericComparer
    {
        public static bool AreClose(double expected, double actual, double absoluteTolerance, double relativeTolerance)
        {
            if (absoluteTolerance < 0 || double.IsNaN(absoluteTolerance) ||
                relativeTolerance < 0 || double.IsNaN(relativeTolerance))
                throw new ArgumentOutOfRangeException("tolerance");
            if (double.IsNaN(expected) || double.IsNaN(actual)) return false;
            if (double.IsInfinity(expected) || double.IsInfinity(actual)) return false;
            double difference = Math.Abs(expected - actual);
            return difference <= absoluteTolerance ||
                difference <= relativeTolerance * Math.Max(Math.Abs(expected), Math.Abs(actual));
        }
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
            IList<TCTTestObservation> observations, TCTTestAttempt attempt = null,
            IList<TCTArtifactRef> artifacts = null, IList<TCTTestDiagnostic> diagnostics = null)
        {
            Descriptor = descriptor; Status = status; Elapsed = elapsed; Error = error ?? string.Empty;
            Attempt = attempt;
            Artifacts = new ReadOnlyCollection<TCTArtifactRef>(new List<TCTArtifactRef>(artifacts ??
                new List<TCTArtifactRef>()));
            Diagnostics = new ReadOnlyCollection<TCTTestDiagnostic>(new List<TCTTestDiagnostic>(diagnostics ??
                new List<TCTTestDiagnostic>()));
            Assertions = new ReadOnlyCollection<TCTAssertionResult>(new List<TCTAssertionResult>(assertions));
            Observations = new ReadOnlyCollection<TCTTestObservation>(new List<TCTTestObservation>(observations));
        }
        public TCTTestCaseDescriptor Descriptor { get; private set; }
        public TCTTestAttempt Attempt { get; private set; }
        public string RunId { get { return Attempt == null ? "AdHoc" : Attempt.RunId; } }
        public string AttemptId { get { return Attempt == null ? "AdHoc" : Attempt.AttemptId; } }
        public TCTTestStatus Status { get; private set; }
        public TimeSpan Elapsed { get; private set; }
        public string Error { get; private set; }
        public TCTTestSourceLocation SourceLocation
        {
            get { return TCTTestSourceLocator.TryParse(Error); }
        }
        public IList<TCTAssertionResult> Assertions { get; private set; }
        public IList<TCTTestObservation> Observations { get; private set; }
        public IList<TCTArtifactRef> Artifacts { get; private set; }
        public IList<TCTTestDiagnostic> Diagnostics { get; private set; }
        internal static TCTTestCaseResult FromTransport(TCTTestCaseDescriptor descriptor, TCTTestStatus status,
            TimeSpan elapsed, string error, IList<TCTAssertionResult> assertions,
            IList<TCTTestObservation> observations, TCTTestAttempt attempt = null,
            IList<TCTArtifactRef> artifacts = null, IList<TCTTestDiagnostic> diagnostics = null)
        {
            return new TCTTestCaseResult(descriptor, status, elapsed, error, assertions,
                observations, attempt, artifacts, diagnostics);
        }
    }

    public sealed class TCTTestRunSummary
    {
        internal TCTTestRunSummary(IList<TCTTestCaseResult> results, string runId = null, TCTTestEvidence evidence = null)
        {
            Results = new ReadOnlyCollection<TCTTestCaseResult>(new List<TCTTestCaseResult>(results));
            RunId = runId ?? (results.Count > 0 ? results[0].RunId : "Unknown");
            Evidence = evidence ?? new TCTTestEvidence();
        }
        public string RunId { get; private set; }
        public TCTTestEvidence Evidence { get; private set; }
        public IList<TCTTestCaseResult> Results { get; private set; }
        public int Total { get { return Results.Count; } }
        public int Passed { get { return Results.Count(r => r.Status == TCTTestStatus.Passed); } }
        public int Failed { get { return Results.Count(r => r.Status == TCTTestStatus.Failed || r.Status == TCTTestStatus.Error || r.Status == TCTTestStatus.TimedOut); } }
        public int Cancelled { get { return Results.Count(r => r.Status == TCTTestStatus.Cancelled); } }
        public int TimedOut { get { return Results.Count(r => r.Status == TCTTestStatus.TimedOut); } }
        public int Errors { get { return Results.Count(r => r.Status == TCTTestStatus.Error); } }
        public int Skipped { get { return Results.Count(r => r.Status == TCTTestStatus.Skipped); } }
        public int Inconclusive { get { return Results.Count(r => r.Status == TCTTestStatus.Inconclusive); } }
        public int Unsupported { get { return Results.Count(r => r.Status == TCTTestStatus.Unsupported || r.Status == TCTTestStatus.Inconclusive); } }
        public bool Success { get { return Total > 0 && Passed == Total; } }
    }

    public static class TCTTestProfilePolicy
    {
        public static bool Allows(TCTTestRunProfile profile, TCTTestKind kind)
        {
            if (profile == TCTTestRunProfile.Dev) return true;
            if (profile == TCTTestRunProfile.Benchmark) return kind == TCTTestKind.Benchmark;
            if (profile == TCTTestRunProfile.Stress) return kind == TCTTestKind.Stress;
            if (profile == TCTTestRunProfile.Smoke) return kind == TCTTestKind.Unit;
            // Regression & CI omit expensive Stress/Benchmark until explicitly requested.
            return kind == TCTTestKind.Unit || kind == TCTTestKind.Integration ||
                kind == TCTTestKind.Regression;
        }
    }

    public sealed class TCTTestCatalog
    {
        private readonly List<ITCTTestCase> _cases = new List<ITCTTestCase>();
        public IList<ITCTTestCase> Cases { get { return _cases.AsReadOnly(); } }
        public void RegisterAsync(ITCTAsyncTestCase test)
        {
            Register(new TCTAsyncTestCaseAdapter(test));
        }
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
                (request.SelectedIds.Count > 0 || TCTTestProfilePolicy.Allows(request.Profile, c.Descriptor.Kind)) &&
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
        public static Task<TCTTestCaseResult> RunOneAsync(ITCTTestCase test, CancellationToken token, int seed,
            TCTTestAttempt attempt = null, TCTTestArtifactStore store = null,
            Action<TCTTestEvent> onEvent = null)
        {
            if (test == null) throw new ArgumentNullException("test");
            ITCTExternalResultTestCase external = test as ITCTExternalResultTestCase;
            if (external != null)
            {
                return Task.Run(() =>
                {
                    Stopwatch watch = Stopwatch.StartNew();
                    try
                    {
                        if (onEvent != null)
                            onEvent(new TCTTestEvent(1, attempt == null ? "AdHoc" : attempt.AttemptId,
                                TCTTestEventKind.Started, test.Descriptor.Id));
                        token.ThrowIfCancellationRequested();
                        TCTTestCaseResult outcome = external.ExecuteExternal(attempt, token, store);
                        if (outcome == null || outcome.Descriptor.Id != test.Descriptor.Id ||
                            (attempt != null && outcome.AttemptId != attempt.AttemptId))
                            throw new InvalidOperationException("External framework returned an invalid attempt");
                        if (onEvent != null)
                            onEvent(new TCTTestEvent(2, attempt == null ? "AdHoc" : attempt.AttemptId,
                                TCTTestEventKind.Completed, outcome.Status.ToString()));
                        return outcome;
                    }
                    catch (OperationCanceledException)
                    {
                        return TCTTestCaseResult.FromTransport(test.Descriptor,
                            TCTTestStatus.Cancelled, watch.Elapsed, "External test cancelled",
                            new List<TCTAssertionResult>(), new List<TCTTestObservation>(), attempt);
                    }
                    catch (Exception ex)
                    {
                        return TCTTestCaseResult.FromTransport(test.Descriptor,
                            TCTTestStatus.Error, watch.Elapsed, ex.ToString(),
                            new List<TCTAssertionResult>(), new List<TCTTestObservation>(), attempt);
                    }
                    finally { watch.Stop(); }
                }, CancellationToken.None);
            }
            return Task.Run(() =>
            {
                TCTTestContext context = new TCTTestContext(token, seed, attempt, store, onEvent);
                context.Publish(TCTTestEventKind.Started, test.Descriptor.Id);
                Stopwatch watch = Stopwatch.StartNew();
                TCTTestStatus status = TCTTestStatus.Passed;
                string error = string.Empty;
                try
                {
                    context.ThrowIfCancelled(); TCTTestLifecycleExecutor.Execute(test, context); context.ThrowIfCancelled();
                    if (context.Assertions.Count == 0) status = TCTTestStatus.Inconclusive;
                }
                catch (OperationCanceledException) { status = TCTTestStatus.Cancelled; error = "Cancelled";
                    context.Diagnostic("TCT.CANCELLED", TCTTestDiagnosticSeverity.Warning, "Execute", error); }
                catch (TCTAssertionException ex) { status = TCTTestStatus.Failed; error = ex.ToString();
                    context.Diagnostic("TCT.ASSERT", TCTTestDiagnosticSeverity.Error, "Assert", ex.Message); }
                catch (NotSupportedException ex) { status = TCTTestStatus.Unsupported; error = ex.ToString();
                    context.Diagnostic("TCT.UNSUPPORTED", TCTTestDiagnosticSeverity.Warning, "Execute", ex.Message); }
                catch (Exception ex) { status = TCTTestStatus.Error; error = ex.ToString();
                    context.Diagnostic("TCT.EXCEPTION", TCTTestDiagnosticSeverity.Error, "Execute", ex.ToString()); }
                finally { watch.Stop(); }
                context.Publish(TCTTestEventKind.Completed, status.ToString());
                return new TCTTestCaseResult(test.Descriptor, status, watch.Elapsed,
                    error, context.Assertions, context.Observations, attempt, context.Artifacts, context.Diagnostics);
            }, CancellationToken.None);
        }
    }
}
