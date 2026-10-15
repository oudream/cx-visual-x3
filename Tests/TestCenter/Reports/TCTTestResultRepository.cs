using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using CxVisual.TestCenter.Core;
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
            TCTReportWriter.WriteJson(Path.Combine(directory, "summary.json"), run);
            TCTReportWriter.WriteJUnit(Path.Combine(directory, "report.junit.xml"), run);
            TCTReportWriter.WriteText(Path.Combine(directory, "report.txt"), run);
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
