using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using CxVisual.TestCenter.Core;
using CxVisual.TestCenter.Execution;
using CxVisual.TestCenter.Fixtures;
using CxVisual.TestCenter.Models;
using CxVisual.TestCenter.Reports;

namespace CxVisual.TestCenter.Reports
{
    public sealed class TCTStoredTestRun
    {
        public TCTStoredTestRun(string runId, string commit, string os,
            IDictionary<string, string> statuses)
        {
            RunId = runId; Commit = commit; OS = os;
            Statuses = new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(statuses, StringComparer.Ordinal));
        }
        public string RunId { get; private set; }
        public string Commit { get; private set; }
        public string OS { get; private set; }
        public IDictionary<string, string> Statuses { get; private set; }
    }

    public sealed class TCTTestRunDiff
    {
        public TCTTestRunDiff(bool comparable, IList<string> regressions, IList<string> recoveries,
            IList<string> missing)
        {
            Comparable = comparable;
            Regressions = new ReadOnlyCollection<string>(new List<string>(regressions));
            Recoveries = new ReadOnlyCollection<string>(new List<string>(recoveries));
            Missing = new ReadOnlyCollection<string>(new List<string>(missing));
        }
        public bool Comparable { get; private set; }
        public IList<string> Regressions { get; private set; }
        public IList<string> Recoveries { get; private set; }
        public IList<string> Missing { get; private set; }
    }

    public sealed class TCTArchivedTestRun
    {
        public TCTArchivedTestRun(string runId, string commit, string os,
            IList<TCTTestCaseResult> attempts)
        {
            RunId = runId; Commit = commit; OS = os;
            Attempts = new ReadOnlyCollection<TCTTestCaseResult>(
                new List<TCTTestCaseResult>(attempts));
        }
        public string RunId { get; private set; }
        public string Commit { get; private set; }
        public string OS { get; private set; }
        public IList<TCTTestCaseResult> Attempts { get; private set; }
    }

    // History is opt-in; never deletes user files or edits a golden baseline.
    public sealed class TCTTestResultRepository
    {
        private readonly string _root;
        public TCTTestResultRepository(string root)
        {
            _root = Path.GetFullPath(root ?? throw new ArgumentNullException("root"));
        }
        public string Save(TCTTestRunSummary run)
        {
            if (run == null) throw new ArgumentNullException("run");
            Guid id;
            if (!Guid.TryParseExact(run.RunId, "N", out id))
                throw new InvalidDataException("History requires canonical RunId");
            string directory = Path.Combine(_root, "Run_" + run.RunId);
            Directory.CreateDirectory(directory);
            if (File.Exists(Path.Combine(directory, "attempts.manifest.json")))
                throw new InvalidOperationException("History Run already committed; never overwrite");
            TCTTestArtifactStore archivedStore = new TCTTestArtifactStore(directory);
            TCTTestArtifactStore liveStore = new TCTTestArtifactStore(
                TCTTestArtifactStore.RunRoot(run.RunId));
            string attemptsPath = Path.Combine(directory, "attempts");
            Directory.CreateDirectory(attemptsPath);
            var manifest = new List<Dictionary<string, object>>();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (TCTTestCaseResult result in run.Results)
            {
                Guid parsed;
                if (result.Attempt == null || result.RunId != run.RunId ||
                    !Guid.TryParseExact(result.AttemptId, "N", out parsed) ||
                    !visited.Add(result.AttemptId))
                    throw new InvalidDataException("History attempt identity is missing, foreign or duplicated");
                foreach (TCTArtifactRef artifact in result.Artifacts)
                {
                    if (artifact.AttemptId != result.AttemptId)
                        throw new InvalidDataException("Cross-Attempt artifact cannot be archived");
                    byte[] original = liveStore.Read(artifact);
                    TCTArtifactRef archived = archivedStore.Save(original, artifact.Mime, artifact.AttemptId);
                    if (archived.RelativePath != artifact.RelativePath ||
                        archived.Sha256 != artifact.Sha256 || archived.ByteSize != artifact.ByteSize)
                        throw new InvalidDataException("Evidence changed while archiving");
                }
                string attemptFile = Path.Combine(attemptsPath, result.AttemptId + ".xml");
                TCTWireCodec.Write(attemptFile, result);
                string hash = TCTBaselineService.Sha256(File.ReadAllBytes(attemptFile));
                manifest.Add(new Dictionary<string, object> {
                    { "attemptId", result.AttemptId },
                    { "id", result.Descriptor.Id },
                    { "name", result.Descriptor.Name },
                    { "aspect", result.Descriptor.Aspect.ToString() },
                    { "group", result.Descriptor.Group },
                    { "areaId", result.Descriptor.CapabilityAreaId },
                    { "capabilityId", result.Descriptor.CapabilityId },
                    { "objectId", result.Descriptor.ObjectId },
                    { "kind", result.Descriptor.Kind.ToString() },
                    { "status", result.Status.ToString() },
                    { "sha256", hash }
                });
            }
            TCTReportWriter.WriteJson(Path.Combine(directory, "summary.json"), run);
            TCTReportWriter.WriteJUnit(Path.Combine(directory, "report.junit.xml"), run);
            TCTReportWriter.WriteText(Path.Combine(directory, "report.txt"), run);
            JavaScriptSerializer serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            string manifestFile = Path.Combine(directory, "attempts.manifest.json");
            string temporary = manifestFile + ".partial";
            File.WriteAllText(temporary, serializer.Serialize(new { schema = 1,
                runId = run.RunId, attempts = manifest }));
            File.Move(temporary, manifestFile);
            return directory;
        }
        public TCTStoredTestRun Load(string runId)
        {
            Guid id;
            if (!Guid.TryParseExact(runId, "N", out id))
                throw new ArgumentException("RunId must be canonical GUID", "runId");
            string path = Path.Combine(_root, "Run_" + runId, "summary.json");
            JavaScriptSerializer serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            Dictionary<string, object> doc = serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
            if (Convert.ToInt32(doc["schemaVersion"]) < 2)
                throw new InvalidDataException("History format version not supported");
            if ((string)doc["runId"] != runId) throw new InvalidDataException("History RunId mismatch");
            Dictionary<string, object> environment = (Dictionary<string, object>)doc["environment"];
            System.Collections.IEnumerable cases = (System.Collections.IEnumerable)doc["cases"];
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (object entry in cases)
            {
                Dictionary<string, object> item = (Dictionary<string, object>)entry;
                // A repeated test can have more than one Attempt; concatenate identity to retain each.
                string key = (string)item["id"] + "#" + Convert.ToString(item["iteration"]);
                if (values.ContainsKey(key)) throw new InvalidDataException("Duplicate TestId/Iteration in history");
                values.Add(key, (string)item["status"]);
            }
            return new TCTStoredTestRun(runId, (string)environment["Commit"], (string)environment["OS"], values);
        }
        public TCTArchivedTestRun LoadDetailed(string runId)
        {
            Guid parsed;
            if (!Guid.TryParseExact(runId, "N", out parsed))
                throw new ArgumentException("Canonical RunId expected", "runId");
            TCTStoredTestRun header = Load(runId);
            string directory = Path.Combine(_root, "Run_" + runId);
            string manifestFile = Path.Combine(directory, "attempts.manifest.json");
            JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var document = json.Deserialize<Dictionary<string, object>>(File.ReadAllText(manifestFile));
            if (Convert.ToInt32(document["schema"]) != 1 || (string)document["runId"] != runId)
                throw new InvalidDataException("Archived result manifest schema/RunId invalid");
            var entries = (System.Collections.IEnumerable)document["attempts"];
            var attempts = new List<TCTTestCaseResult>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            TCTTestArtifactStore store = new TCTTestArtifactStore(directory);
            foreach (object entry in entries)
            {
                var item = (Dictionary<string, object>)entry;
                string id = (string)item["id"], attemptId = (string)item["attemptId"];
                Guid attemptGuid;
                if (!Guid.TryParseExact(attemptId, "N", out attemptGuid) ||
                    !seen.Add(attemptId)) throw new InvalidDataException("Duplicate/unsafe archive AttemptId");
                string file = Path.Combine(directory, "attempts", attemptId + ".xml");
                if (!File.Exists(file) ||
                    !string.Equals(TCTBaselineService.Sha256(File.ReadAllBytes(file)),
                        (string)item["sha256"], StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Archived Attempt result missing or tampered");
                TCTTestAspect aspect;
                TCTTestKind kind;
                if (!Enum.TryParse((string)item["aspect"], out aspect) ||
                    !Enum.IsDefined(typeof(TCTTestAspect), aspect) ||
                    !Enum.TryParse((string)item["kind"], out kind) ||
                    !Enum.IsDefined(typeof(TCTTestKind), kind))
                    throw new InvalidDataException("Invalid archived descriptor enumeration");
                var descriptor = new TCTTestCaseDescriptor(id, (string)item["name"],
                    aspect, (string)item["group"], (string)item["areaId"],
                    (string)item["capabilityId"], (string)item["objectId"], kind);
                TCTTestCaseResult attempt = TCTWireCodec.Read(file, descriptor);
                if (attempt.RunId != runId || attempt.AttemptId != attemptId ||
                    attempt.Status.ToString() != (string)item["status"])
                    throw new InvalidDataException("Archived Attempt identity/status mismatch");
                foreach (TCTArtifactRef artifact in attempt.Artifacts)
                    store.Read(artifact); // Missing, wrong size, wrong SHA256 must reject archive.
                TCTObservationReplay.Read(attempt, store); // Include schema and logical Owner verification.
                attempts.Add(attempt);
            }
            // summary.json is not an independent authority: compare it with every SHA256
            // verified Attempt result. A forged Passed summary cannot hide a failed Attempt.
            if (header.Statuses.Count != attempts.Count)
                throw new InvalidDataException("Archived summary/Attempt count mismatch");
            foreach (TCTTestCaseResult attempt in attempts)
            {
                string key = attempt.Descriptor.Id + "#" +
                    attempt.Attempt.Iteration.ToString(System.Globalization.CultureInfo.InvariantCulture);
                string expected;
                if (!header.Statuses.TryGetValue(key, out expected) ||
                    !string.Equals(expected, attempt.Status.ToString(), StringComparison.Ordinal))
                    throw new InvalidDataException("Archived summary contradicts verified Attempt " + key);
            }
            return new TCTArchivedTestRun(runId, header.Commit, header.OS, attempts);
        }

        public static TCTTestRunDiff Compare(TCTStoredTestRun before, TCTStoredTestRun after)
        {
            if (before == null || after == null) throw new ArgumentNullException("run");
            bool comparable = before.OS == after.OS && before.Commit == after.Commit &&
                before.Commit != "Unknown";
            List<string> regressions = new List<string>(), recoveries = new List<string>(),
                missing = new List<string>();
            foreach (KeyValuePair<string, string> entry in before.Statuses)
            {
                string current;
                if (!after.Statuses.TryGetValue(entry.Key, out current))
                { missing.Add(entry.Key); continue; }
                if (entry.Value == "Passed" && current != "Passed") regressions.Add(entry.Key);
                else if (entry.Value != "Passed" && current == "Passed") recoveries.Add(entry.Key);
            }
            return new TCTTestRunDiff(comparable, regressions, recoveries, missing);
        }
    }
}
