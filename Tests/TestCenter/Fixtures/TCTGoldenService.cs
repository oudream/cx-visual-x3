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
            string current = File.Exists(destination) ?
                TCTBaselineService.Sha256(_fixture.ReadBytes(relativePath)) : NoExistingBaseline;
            if (!string.Equals(current, expectedPreviousSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Golden version changed; refresh and approve again");
            string targetDir = Path.GetDirectoryName(destination);
            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
            // Avoid silent overwrite and leave old baseline intact when the atomic swap fails.
            string temp = destination + "." + Guid.NewGuid().ToString("N") + ".partial";
            try
            {
                File.WriteAllBytes(temp, bytes);
                // Re-check version immediately before atomic replace; no concurrent collaborators
                // should approve the same Golden without their own expected version.
                string again = File.Exists(destination) ?
                    TCTBaselineService.Sha256(_fixture.ReadBytes(relativePath)) : NoExistingBaseline;
                if (!string.Equals(current, again, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Golden changed during approval");
                if (File.Exists(destination)) File.Replace(temp, destination, null);
                else File.Move(temp, destination);
                return new TCTGoldenApprovalReceipt(relativePath, actor, current,
                    TCTBaselineService.Sha256(bytes), DateTime.UtcNow);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
