using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CxVisual.TestCenter.Core;
using CxVisual.TestCenter.Discovery;
using CxVisual.TestCenter.Fixtures;
using CxVisual.TestCenter.Models;
using CxVisual.TestCenter.Reports;

namespace CxVisual.TestCenter.Cases
{
    public static class TCTArchiveComparisonCases
    {
        private static TCTTestCaseResult Capture(string runId, string value)
        {
            const string id = "TCT.LOCAL.ARCHIVE.DIFF";
            var attempt = new TCTTestAttempt(runId, id, 0, 21);
            var store = new TCTTestArtifactStore(TCTTestArtifactStore.RunRoot(runId));
            var ctx = new TCTTestContext(default(System.Threading.CancellationToken),
                21, attempt, store);
            ctx.AssertEqual("FGERect2D", "FGERect2D", "真实Owner标识");
            ctx.ObserveStructured("Geometry", "矩形实际边界", "value=" + value,
                "FGERect2D", "Foundation.Geometry/FGERect2D", null,
                Encoding.UTF8.GetBytes(value), "text/plain");
            var descriptor = new TCTTestCaseDescriptor(id, "几何Run对比",
                TCTTestAspect.Foundation, "Geometry", "Unmapped", "Unmapped", "FGERect2D");
            return TCTTestCaseResult.FromTransport(descriptor, TCTTestStatus.Passed,
                TimeSpan.Zero, "", ctx.Assertions, ctx.Observations, attempt, ctx.Artifacts);
        }

        [TCTTest("TCT.M2.HISTORY.DIFF.001",
            "归档双Run的真实Attempt及结构化Observation差异",
            TCTTestAspect.Foundation, "TestFramework", ObjectId = "TCTTestResultRepository")]
        public static void TwoVerifiedRunsAreCompared(TCTTestContext test)
        {
            string a = Guid.NewGuid().ToString("N"), b = Guid.NewGuid().ToString("N");
            string liveA = TCTTestArtifactStore.RunRoot(a);
            string liveB = TCTTestArtifactStore.RunRoot(b);
            using (var sandbox = new TCTTestSandbox())
            {
                try
                {
                    TCTTestCaseResult left = Capture(a, "rect=10,20,100,80");
                    TCTTestCaseResult right = Capture(b, "rect=10,20,101,80");
                    var history = new TCTTestResultRepository(sandbox.Root);
                    string leftPath = history.Save(new TCTTestRunSummary(
                        new List<TCTTestCaseResult> { left }, a));
                    history.Save(new TCTTestRunSummary(
                        new List<TCTTestCaseResult> { right }, b));
                    Directory.Delete(liveA, true);
                    Directory.Delete(liveB, true);

                    TCTArchivedRunComparison result = history.CompareArchived(a, b);
                    test.AssertEqual(1, result.Rows.Count, "双Run同TestId/Iteration正确配对");
                    test.AssertEqual(1, result.ChangedCount, "真实Payload变化不会被忽略");
                    test.AssertEqual(1, result.Rows[0].EvidenceChanges, "Observation内容差异");
                    test.AssertEqual("Changed", result.Rows[0].Difference, "GUI比较结果真实状态");
                    test.AssertEqual(a, result.Before.RunId, "Before来源RunId");
                    test.AssertEqual(b, result.After.RunId, "After来源RunId");
                    test.AssertThrows<ArgumentException>(() => history.CompareArchived(a, a),
                        "不能拿同一个Run冒充两次回归");

                    // Corrupt archive must fail closed, not become an empty/no-diff result.
                    string path = Path.Combine(leftPath, left.Observations[0].Evidence.RelativePath
                        .Replace('/', Path.DirectorySeparatorChar));
                    byte[] bytes = File.ReadAllBytes(path);
                    bytes[bytes.Length - 1] ^= 1;
                    File.WriteAllBytes(path, bytes);
                    test.AssertThrows<InvalidDataException>(() => history.CompareArchived(a, b),
                        "历史证据篡改必须拒绝比较");
                }
                finally
                {
                    if (Directory.Exists(liveA)) Directory.Delete(liveA, true);
                    if (Directory.Exists(liveB)) Directory.Delete(liveB, true);
                }
            }
        }
    }
}
