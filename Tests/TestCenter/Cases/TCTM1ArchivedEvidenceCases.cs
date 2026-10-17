using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CxVisual.Foundation.Geometry;
using CxVisual.TestCenter.Core;
using CxVisual.TestCenter.Discovery;
using CxVisual.TestCenter.Fixtures;
using CxVisual.TestCenter.Models;
using CxVisual.TestCenter.Reports;

namespace CxVisual.TestCenter.Cases
{
    public static class TCTM1ArchivedEvidenceCases
    {
        private static TCTTestCaseResult Capture(string runId, bool actual)
        {
            const string id = "TCT.LOCAL.ArchivedGeometry";
            TCTTestAttempt attempt = new TCTTestAttempt(runId, id, 0, 12);
            TCTTestArtifactStore store = new TCTTestArtifactStore(TCTTestArtifactStore.RunRoot(runId));
            var test = new TCTTestContext(default(System.Threading.CancellationToken), 12, attempt, store);
            test.AssertTrue(true, "Actual owner returned boolean", true, true);
            test.ObserveStructured("Geometry", "Contains", "inside=" + actual,
                "CxRect2D", "Geometry/CxRect2D.Contains", null,
                Encoding.UTF8.GetBytes(actual ? "inside" : "outside"), "text/plain");
            var descriptor = new TCTTestCaseDescriptor(id, "Actual geometry snapshot",
                TCTTestAspect.Foundation, "Geometry", "Unmapped", "Unmapped", "CxRect2D");
            return TCTTestCaseResult.FromTransport(descriptor, TCTTestStatus.Passed,
                TimeSpan.Zero, "", test.Assertions, test.Observations, attempt, test.Artifacts);
        }

        [TCTTest("TCT.M1.HISTORY.001", "完整Attempt及Owner证据归档后仍可回放与比较",
            TCTTestAspect.Foundation, "TestFramework", ObjectId = "TCTTestResultRepository")]
        public static void FullEvidenceSurvivesOriginalCleanup(TCTTestContext test)
        {
            var geometry = new CxRect2D(0, 0, 100, 80);
            bool inside = geometry.Contains(new CxPoint2D(25, 30));
            bool outside = geometry.Contains(new CxPoint2D(125, 30));
            test.AssertTrue(inside && !outside, "生产Owner给出两组真实输出", true, true);
            string leftId = Guid.NewGuid().ToString("N"), rightId = Guid.NewGuid().ToString("N");
            string liveLeft = TCTTestArtifactStore.RunRoot(leftId);
            string liveRight = TCTTestArtifactStore.RunRoot(rightId);
            using (TCTTestSandbox sandbox = new TCTTestSandbox())
            {
                try
                {
                    var repository = new TCTTestResultRepository(sandbox.Root);
                    string beforePath = repository.Save(new TCTTestRunSummary(
                        new List<TCTTestCaseResult> { Capture(leftId, inside) }, leftId));
                    string afterPath = repository.Save(new TCTTestRunSummary(
                        new List<TCTTestCaseResult> { Capture(rightId, outside) }, rightId));
                    test.AssertTrue(File.Exists(Path.Combine(beforePath, "attempts.manifest.json")),
                        "完整归档Manifest存在", true, true);
                    Directory.Delete(liveLeft, true);
                    Directory.Delete(liveRight, true);
                    TCTArchivedTestRun before = repository.LoadDetailed(leftId);
                    TCTArchivedTestRun after = repository.LoadDetailed(rightId);
                    test.AssertEqual(1, before.Attempts.Count, "归档独立Attempt数量");
                    test.AssertEqual(1, TCTObservationReplay.Read(before.Attempts[0],
                        new TCTTestArtifactStore(beforePath)).Count,
                        "原工作区清除后仍能回放真实Owner观察");
                    TCTObservationComparison diff = TCTObservationDiffService.Compare(
                        before.Attempts[0], after.Attempts[0],
                        new TCTTestArtifactStore(beforePath),
                        new TCTTestArtifactStore(afterPath));
                    test.AssertEqual(1, diff.Differences.Count, "跨历史Run真实结果变化");
                    test.AssertEqual(TCTObservationChangeKind.Changed, diff.Differences[0].Kind,
                        "差异由生产数值引起而非RunId或时间");

                    TCTArtifactRef evidence = before.Attempts[0].Observations[0].Evidence;
                    string tampered = Path.Combine(beforePath,
                        evidence.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                    byte[] bad = File.ReadAllBytes(tampered);
                    bad[bad.Length - 1] ^= 1;
                    File.WriteAllBytes(tampered, bad);
                    test.AssertThrows<InvalidDataException>(() => repository.LoadDetailed(leftId),
                        "归档后若内容被篡改不可伪装可回放");
                }
                finally
                {
                    if (Directory.Exists(liveLeft)) Directory.Delete(liveLeft, true);
                    if (Directory.Exists(liveRight)) Directory.Delete(liveRight, true);
                }
            }
        }
    }
}
