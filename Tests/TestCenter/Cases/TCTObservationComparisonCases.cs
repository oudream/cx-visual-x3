using System;
using System.IO;
using System.Linq;
using System.Text;
using CxVisual.Foundation.Geometry;
using CxVisual.TestCenter.Core;
using CxVisual.TestCenter.Discovery;
using CxVisual.TestCenter.Fixtures;
using CxVisual.TestCenter.Models;

namespace CxVisual.TestCenter.Cases
{
    public static class TCTObservationComparisonCases
    {
        private static TCTTestCaseResult Capture(TCTTestSandbox sandbox, string id,
            bool actual, string sourceObjectId, out TCTTestArtifactStore store)
        {
            TCTTestAttempt attempt = new TCTTestAttempt(Guid.NewGuid().ToString("N"), id, 0, 13);
            store = new TCTTestArtifactStore(sandbox.PathFor("run-" + attempt.RunId));
            var test = new TCTTestContext(default(System.Threading.CancellationToken),
                13, attempt, store);
            test.AssertTrue(true, "ReferenceOwner invoked", true, true);
            test.ObserveStructured("Geometry", "Contains", "inside=" + actual,
                sourceObjectId, "Geometry/CxRect2D.Contains", null,
                Encoding.UTF8.GetBytes(actual ? "inside" : "outside"), "text/plain");
            return TCTTestCaseResult.FromTransport(
                new TCTTestCaseDescriptor(id, "Geometry run diff", TCTTestAspect.Foundation,
                    "Geometry", "Unmapped", "Unmapped", "CxRect2D"),
                TCTTestStatus.Passed, TimeSpan.Zero, "", test.Assertions, test.Observations,
                attempt, test.Artifacts);
        }

        [TCTTest("TCT.M1.DIFF.001", "不同Run的真实几何观察差异可复现",
            TCTTestAspect.Foundation, "Geometry / Observations", ObjectId = "CxRect2D")]
        public static void RealGeometryDiff(TCTTestContext test)
        {
            CxRect2D rect = new CxRect2D(0, 0, 100, 80);
            bool inside = rect.Contains(new CxPoint2D(25, 30));
            bool outside = rect.Contains(new CxPoint2D(120, 30));
            test.AssertTrue(inside && !outside, "生产几何两输入返回不同真实结果", true, true);
            using (var sandbox = new TCTTestSandbox())
            {
                TCTTestArtifactStore source, other, same;
                const string id = "TCT.LOCAL.GeometryRun";
                TCTTestCaseResult left = Capture(sandbox, id, inside, "CxRect2D", out source);
                TCTTestCaseResult right = Capture(sandbox, id, outside, "CxRect2D", out other);
                TCTTestCaseResult sameCase = Capture(sandbox, id, inside, "CxRect2D", out same);
                TCTObservationComparison diff = TCTObservationDiffService.Compare(left, right, source, other);
                test.AssertTrue(!diff.Equal, "不同实际几何结果不能误判相同", false, diff.Equal);
                test.AssertEqual(1, diff.Differences.Count, "一项逻辑差异");
                test.AssertEqual(TCTObservationChangeKind.Changed,
                    diff.Differences[0].Kind, "实际Snapshot变化");
                test.AssertTrue(TCTObservationDiffService.Compare(left, sameCase, source, same).Equal,
                    "跨Run但内容相同不因时间/Attempt变化产生差异", true, true);
                TCTTestArtifactStore wrong;
                TCTTestCaseResult different = Capture(sandbox, "TCT.LOCAL.Other", inside,
                    "CxRect2D", out wrong);
                test.AssertThrows<InvalidDataException>(() =>
                    TCTObservationDiffService.Compare(left, different, source, wrong),
                    "不同TestId绝不跨用例比较");
            }
        }
    }
}
