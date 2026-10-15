using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using CxVisual.TestCenter.Core;

namespace CxVisual.TestCenter.Models
{
    // Definition / Run / Attempt / Runtime IDs are separate, never substitutes.
    public enum TCTTestRunState { Prepared, Running, CancellationRequested, Completed, Faulted }
    public enum TCTTestRunProfile { Dev, Smoke, Regression, Stress, Benchmark, CI }
    public enum TCTTestDiagnosticSeverity { Info, Warning, Error, Critical }
    public enum TCTTestEventKind { Started, Assertion, Observation, Diagnostic, Completed }

    public sealed class TCTTestAttempt
    {
        public TCTTestAttempt(string runId, string testId, int iteration, int seed, string attemptId = null)
        {
            if (string.IsNullOrWhiteSpace(runId)) throw new ArgumentException("RunId required", "runId");
            if (string.IsNullOrWhiteSpace(testId)) throw new ArgumentException("TestId required", "testId");
            if (iteration < 0) throw new ArgumentOutOfRangeException("iteration");
            RunId = runId; TestId = testId; Iteration = iteration; Seed = seed;
            AttemptId = string.IsNullOrWhiteSpace(attemptId) ? Guid.NewGuid().ToString("N") : attemptId;
            CreatedUtc = DateTime.UtcNow;
        }
        public string RunId { get; private set; }
        public string AttemptId { get; private set; }
        public string TestId { get; private set; }
        public int Iteration { get; private set; }
        public int Seed { get; private set; }
        public DateTime CreatedUtc { get; private set; }
    }

    public sealed class TCTTestEvidence
    {
        public TCTTestEvidence()
        {
            CapturedUtc = DateTime.UtcNow;
            OS = Environment.OSVersion.ToString();
            Machine = Environment.MachineName;
            Framework = Environment.Version.ToString();
            ProcessBits = IntPtr.Size * 8;
            Repository = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY") ?? "Unknown";
            Commit = Environment.GetEnvironmentVariable("GITHUB_SHA") ?? "Unknown";
            Configuration = Environment.GetEnvironmentVariable("TCT_BUILD_CONFIGURATION") ?? "Unknown";
            AssemblyVersion = typeof(TCTTestEvidence).Assembly.GetName().Version.ToString();
        }
        public DateTime CapturedUtc { get; private set; }
        public string OS { get; private set; }
        public string Machine { get; private set; }
        public string Framework { get; private set; }
        public int ProcessBits { get; private set; }
        public string Repository { get; private set; }
        public string Commit { get; private set; }
        public string Configuration { get; private set; }
        public string AssemblyVersion { get; private set; }
        // Credentials, environment variable dumps and absolute user paths are deliberately excluded.
    }

    public sealed class TCTTestDiagnostic
    {
        public TCTTestDiagnostic(string code, TCTTestDiagnosticSeverity severity,
            string stage, string message, string attemptId)
        {
            Code = code ?? "TCT.UNKNOWN"; Severity = severity;
            Stage = stage ?? string.Empty; Message = message ?? string.Empty;
            AttemptId = attemptId ?? string.Empty; TimestampUtc = DateTime.UtcNow;
        }
        public string Code { get; private set; }
        public TCTTestDiagnosticSeverity Severity { get; private set; }
        public string Stage { get; private set; }
        public string Message { get; private set; }
        public string AttemptId { get; private set; }
        public DateTime TimestampUtc { get; private set; }
    }

    public sealed class TCTArtifactRef
    {
        public TCTArtifactRef(string relativePath, string mime, string sha256, long bytes, string attemptId)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) ||
                relativePath.Contains("..") || relativePath.Contains(":"))
                throw new ArgumentException("Unsafe artifact relative path", "relativePath");
            if (bytes < 0) throw new ArgumentOutOfRangeException("bytes");
            RelativePath = relativePath; Mime = mime ?? "application/octet-stream";
            Sha256 = sha256 ?? string.Empty; ByteSize = bytes; AttemptId = attemptId ?? "";
        }
        public string RelativePath { get; private set; }
        public string Mime { get; private set; }
        public string Sha256 { get; private set; }
        public long ByteSize { get; private set; }
        public string AttemptId { get; private set; }
    }

    public sealed class TCTTestEvent
    {
        public TCTTestEvent(long sequence, string attemptId, TCTTestEventKind kind, string message)
        {
            if (sequence <= 0) throw new ArgumentOutOfRangeException("sequence");
            Sequence = sequence; AttemptId = attemptId ?? string.Empty;
            Kind = kind; Message = message ?? string.Empty; TimestampUtc = DateTime.UtcNow;
        }
        public long Sequence { get; private set; }
        public string AttemptId { get; private set; }
        public TCTTestEventKind Kind { get; private set; }
        public string Message { get; private set; }
        public DateTime TimestampUtc { get; private set; }
    }

    public sealed class TCTTestRunSession
    {
        private readonly object _gate = new object();
        private TCTTestRunState _state = TCTTestRunState.Prepared;
        private DateTime? _finishedUtc;
        public TCTTestRunSession(string runId, TCTTestRunProfile profile)
        {
            if (string.IsNullOrWhiteSpace(runId)) throw new ArgumentException("RunId required");
            RunId = runId; Profile = profile; Evidence = new TCTTestEvidence();
            StartedUtc = DateTime.UtcNow;
        }
        public string RunId { get; private set; }
        public TCTTestRunProfile Profile { get; private set; }
        public TCTTestEvidence Evidence { get; private set; }
        public DateTime StartedUtc { get; private set; }
        public TCTTestRunState State { get { lock (_gate) return _state; } }
        public DateTime? FinishedUtc { get { lock (_gate) return _finishedUtc; } }
        public void Start()
        {
            lock (_gate)
            {
                if (_state != TCTTestRunState.Prepared) throw new InvalidOperationException("Run already started");
                _state = TCTTestRunState.Running;
            }
        }
        public void RequestCancel()
        {
            lock (_gate)
                if (_state == TCTTestRunState.Running) _state = TCTTestRunState.CancellationRequested;
        }
        public void Finish(bool fatal)
        {
            lock (_gate)
            {
                if (_state != TCTTestRunState.Running && _state != TCTTestRunState.CancellationRequested)
                    throw new InvalidOperationException("Run cannot be finalized twice");
                _state = fatal ? TCTTestRunState.Faulted : TCTTestRunState.Completed;
                _finishedUtc = DateTime.UtcNow;
            }
        }
    }

    public sealed class TCTCapabilityCoverageEntry
    {
        public TCTCapabilityCoverageEntry(string capabilityId, int verified, int failed, int unmapped)
        {
            CapabilityId = capabilityId; Verified = verified; Failed = failed; Unmapped = unmapped;
        }
        public string CapabilityId { get; private set; }
        public int Verified { get; private set; }
        public int Failed { get; private set; }
        public int Unmapped { get; private set; }
    }
    // Does not guess official capability IDs; accepts only externally verified IDs.
    public static class TCTCapabilityCoverageService
    {
        public static IList<TCTCapabilityCoverageEntry> Calculate(TCTTestRunSummary summary,
            IEnumerable<string> officialCapabilityIds)
        {
            if (summary == null || officialCapabilityIds == null) throw new ArgumentNullException("summary");
            HashSet<string> official = new HashSet<string>(officialCapabilityIds, StringComparer.Ordinal);
            return summary.Results.GroupBy(result =>
                    official.Contains(result.Descriptor.CapabilityId) ? result.Descriptor.CapabilityId : "Unmapped")
                .Select(group => new TCTCapabilityCoverageEntry(group.Key,
                    group.Count(r => r.Status == TCTTestStatus.Passed && r.Assertions.Count > 0 && group.Key != "Unmapped"),
                    group.Count(r => r.Status == TCTTestStatus.Failed || r.Status == TCTTestStatus.Error ||
                        r.Status == TCTTestStatus.TimedOut),
                    group.Count(r => group.Key == "Unmapped")))
                .OrderBy(x => x.CapabilityId, StringComparer.Ordinal).ToList().AsReadOnly();
        }
    }
}
