using System;
using System.IO;
using System.Text;
using CxVisual.TestCenter.Fixtures;

namespace CxVisual.TestCenter.Fixtures
{
    public sealed class TCTGoldenComparison
    {
        public TCTGoldenComparison(bool found, string expectedSha, string actualSha,
            TCTSnapshotDiff diff)
        {
            BaselineFound = found;
            ExpectedSha256 = expectedSha ?? string.Empty;
            ActualSha256 = actualSha ?? string.Empty;
            Diff = diff ?? throw new ArgumentNullException("diff");
        }
        public bool BaselineFound { get; private set; }
        public bool Matches { get { return BaselineFound && Diff.Equal; } }
        public string ExpectedSha256 { get; private set; }
        public string ActualSha256 { get; private set; }
        public TCTSnapshotDiff Diff { get; private set; }
    }

    public sealed class TCTGoldenApprovalReceipt
    {
        public TCTGoldenApprovalReceipt(string relativePath, string actor, string oldHash,
            string newHash, DateTime timeUtc)
        {
            RelativePath = relativePath; Actor = actor; OldSha256 = oldHash;
            NewSha256 = newHash; ApprovedUtc = timeUtc;
        }
        public string RelativePath { get; private set; }
        public string Actor { get; private set; }
        public string OldSha256 { get; private set; }
        public string NewSha256 { get; private set; }
        public DateTime ApprovedUtc { get; private set; }
    }

    // Human-approved baseline workflow. It cannot be invoked by test execution implicitly.
    public sealed class TCTGoldenService
    {
        public const string NoExistingBaseline = "MISSING";
        private readonly TCTTestFixtureService _fixture;
        public TCTGoldenService(TCTTestFixtureService fixture)
        {
            _fixture = fixture ?? throw new ArgumentNullException("fixture");
        }
        public TCTGoldenComparison Compare(string path, byte[] actual)
        {
            if (actual == null) throw new ArgumentNullException("actual");
            string target = _fixture.ResolvePath(path);
            string actualHash = TCTBaselineService.Sha256(actual);
            if (!File.Exists(target))
                return new TCTGoldenComparison(false, NoExistingBaseline, actualHash,
                    new TCTSnapshotDiff(false, actual.Length, "Missing baseline; no automatic approval"));
            byte[] expected = _fixture.ReadBytes(path);
            return new TCTGoldenComparison(true, TCTBaselineService.Sha256(expected), actualHash,
                TCTSnapshotDiffService.CompareBytes(expected, actual));
        }
        // Append-only local audit log. This records an explicit operator confirmation;
        // it is not a substitute for authenticated approval or an immutable audit server.
        private void AppendAudit(Guid receiptId, string phase, DateTime timeUtc, string relativePath,
            string actor, string oldHash, string newHash)
        {
            string file = _fixture.ResolvePath(".tct-golden-approvals.log");
            string line = receiptId.ToString("N") + "|" + phase + "|" +
                timeUtc.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" +
                Convert.ToBase64String(Encoding.UTF8.GetBytes(relativePath)) + "|" +
                Convert.ToBase64String(Encoding.UTF8.GetBytes(actor)) + "|" + oldHash + "|" +
                newHash + Environment.NewLine;
            byte[] data = Encoding.UTF8.GetBytes(line);
            using (FileStream stream = new FileStream(file, FileMode.Append, FileAccess.Write,
                FileShare.Read, 4096, FileOptions.WriteThrough))
            {
                stream.Write(data, 0, data.Length);
                stream.Flush(true);
            }
        }

        public System.Collections.Generic.IList<TCTGoldenApprovalReceipt> ReadApprovedHistory()
        {
            string file = _fixture.ResolvePath(".tct-golden-approvals.log");
            var approved = new System.Collections.Generic.List<TCTGoldenApprovalReceipt>();
            if (!File.Exists(file)) return approved;
            var prepared = new System.Collections.Generic.Dictionary<string, string>();
            foreach (string line in File.ReadAllLines(file, Encoding.UTF8))
            {
                string[] fields = line.Split('|');
                if (fields.Length != 7) throw new InvalidDataException("Invalid Golden audit record");
                Guid id;
                long ticks;
                if (!Guid.TryParseExact(fields[0], "N", out id) ||
                    !long.TryParse(fields[2], out ticks))
                    throw new InvalidDataException("Invalid Golden audit identity/time");
                if (fields[1] == "PREPARED")
                {
                    prepared[fields[0]] = line;
                }
                else if (fields[1] == "COMMITTED")
                {
                    string before;
                    if (!prepared.TryGetValue(fields[0], out before))
                        throw new InvalidDataException("Golden commit has no prepared record");
                    string[] old = before.Split('|');
                    if (old[3] != fields[3] || old[4] != fields[4] ||
                        old[5] != fields[5] || old[6] != fields[6])
                        throw new InvalidDataException("Golden approval audit mismatch");
                    approved.Add(new TCTGoldenApprovalReceipt(
                        Encoding.UTF8.GetString(Convert.FromBase64String(fields[3])),
                        Encoding.UTF8.GetString(Convert.FromBase64String(fields[4])),
                        fields[5], fields[6], new DateTime(ticks, DateTimeKind.Utc)));
                    prepared.Remove(fields[0]);
                }
                else throw new InvalidDataException("Unknown Golden audit phase");
            }
            // Orphan PREPARED means interrupted approval; it must not be reported as committed.
            return approved;
        }

        public TCTGoldenApprovalReceipt Approve(string relativePath, byte[] bytes, string actor,
            string approval, string expectedPreviousSha256)
        {
            if (bytes == null) throw new ArgumentNullException("bytes");
            if (string.IsNullOrWhiteSpace(actor) || actor.Length > 128)
                throw new ArgumentException("Human approver identity required", "actor");
            if (!string.Equals(approval, "APPROVE_GOLDEN_EXPLICITLY", StringComparison.Ordinal))
                throw new UnauthorizedAccessException("Explicit Golden approval required");
            if (string.IsNullOrWhiteSpace(expectedPreviousSha256))
                throw new ArgumentException("Known previous baseline hash required", "expectedPreviousSha256");
            string destination = _fixture.ResolvePath(relativePath);
            string targetDir = Path.GetDirectoryName(destination);
            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
            // Exclusive per-Golden lock: stale expected SHA cannot win a concurrent approval.
            using (FileStream guard = new FileStream(destination + ".tct.lock",
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                string current = File.Exists(destination) ?
                    TCTBaselineService.Sha256(_fixture.ReadBytes(relativePath)) : NoExistingBaseline;
                if (!string.Equals(current, expectedPreviousSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Golden version changed; refresh and approve again");
                string newHash = TCTBaselineService.Sha256(bytes);
                Guid receiptId = Guid.NewGuid();
                DateTime timeUtc = DateTime.UtcNow;
                string temp = destination + "." + Guid.NewGuid().ToString("N") + ".partial";
                try
                {
                    File.WriteAllBytes(temp, bytes);
                    AppendAudit(receiptId, "PREPARED", timeUtc, relativePath, actor, current, newHash);
                    if (File.Exists(destination)) File.Replace(temp, destination, null);
                    else File.Move(temp, destination);
                    AppendAudit(receiptId, "COMMITTED", timeUtc, relativePath, actor, current, newHash);
                    return new TCTGoldenApprovalReceipt(relativePath, actor, current, newHash, timeUtc);
                }
                finally { if (File.Exists(temp)) File.Delete(temp); }
            }
        }
    }
}
