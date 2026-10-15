using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using CxVisual.TestCenter.Core;
using CxVisual.TestCenter.Discovery;
using CxVisual.TestCenter.Execution;
using CxVisual.TestCenter.Fixtures;
using CxVisual.TestCenter.Reports;

namespace CxVisual.TestCenter.Cases
{
    // 测试框架本身的回归用例：没有Mock通过状态，每个测试都有明确的真实断言。
    public static class TCTFrameworkCases
    {
        [TCTTest("TCT.CORE.001", "Catalog重复ID拒绝", TCTTestAspect.Foundation, "TestFramework",
            CapabilityAreaId = "CA-TEST-DISCOVERY", CapabilityId = "TCT-CATALOG", ObjectId = "TCTTestCatalog")]
        public static void CatalogDuplicate(TCTTestContext context)
        {
            TCTTestCatalog catalog = new TCTTestCatalog();
            ITCTTestCase test = new TCTInlineCase("TCT.INNER.TEST", c => c.AssertEqual(1, 1, "inner"));
            catalog.Register(test);
            context.AssertThrows<InvalidOperationException>(() => catalog.Register(test), "Duplicate TestId");
            context.AssertEqual(1, catalog.Cases.Count, "原Catalog未变更");
        }

        [TCTTest("TCT.CORE.002", "断言失败结果真实分类", TCTTestAspect.Foundation, "TestFramework",
            CapabilityAreaId = "CA-TEST-ASSERT", CapabilityId = "TCT-STATUS", ObjectId = "TCTTestRunner")]
        public static void FailedClassification(TCTTestContext context)
        {
            ITCTTestCase test = new TCTInlineCase("TCT.INNER.FAIL", c => c.AssertEqual(1, 2, "ExpectedFailure"));
            TCTTestCaseResult result = TCTTestRunner.RunOneAsync(test, CancellationToken.None, context.Seed).GetAwaiter().GetResult();
            context.AssertEqual(TCTTestStatus.Failed, result.Status, "失败状态");
            context.AssertEqual(1, result.Assertions.Count, "断言记录");
            context.AssertTrue(result.Error.Contains("ExpectedFailure"), "异常保留名称", true, result.Error.Contains("ExpectedFailure"));
        }

        [TCTTest("TCT.CORE.003", "无断言不能通过", TCTTestAspect.Foundation, "TestFramework",
            CapabilityAreaId = "CA-TEST-ASSERT", CapabilityId = "TCT-INCONCLUSIVE", ObjectId = "TCTTestRunner")]
        public static void NoAssertionIsInconclusive(TCTTestContext context)
        {
            ITCTTestCase test = new TCTInlineCase("TCT.INNER.EMPTY", c => { });
            TCTTestCaseResult result = TCTTestRunner.RunOneAsync(test, CancellationToken.None, 0).GetAwaiter().GetResult();
            context.AssertEqual(TCTTestStatus.Inconclusive, result.Status, "无断言状态");
        }

        [TCTTest("TCT.CORE.004", "不支持功能不会伪装通过", TCTTestAspect.Foundation, "TestFramework",
            CapabilityAreaId = "CA-TEST-ASSERT", CapabilityId = "TCT-UNSUPPORTED", ObjectId = "TCTTestRunner")]
        public static void Unsupported(TCTTestContext context)
        {
            ITCTTestCase test = new TCTInlineCase("TCT.INNER.UNSUPPORTED",
                c => { throw new NotSupportedException("No production adapter"); });
            TCTTestCaseResult result = TCTTestRunner.RunOneAsync(test, CancellationToken.None, 0).GetAwaiter().GetResult();
            context.AssertEqual(TCTTestStatus.Unsupported, result.Status, "不支持状态");
        }

        [TCTTest("TCT.IO.001", "隔离Worker XML协议回环", TCTTestAspect.Foundation, "TestFramework",
            CapabilityAreaId = "CA-TEST-ISOLATION", CapabilityId = "TCT-WIRE", ObjectId = "TCTWireCodec")]
        public static void WireRoundtrip(TCTTestContext context)
        {
            ITCTTestCase test = new TCTInlineCase("TCT.INNER.WIRE", c =>
            {
                c.AssertEqual("Ω中文", "Ω中文", "unicode");
                c.Observe("Geometry", "actual", "测试数据Ω");
            });
            TCTTestCaseResult result = TCTTestRunner.RunOneAsync(test, CancellationToken.None, 13).GetAwaiter().GetResult();
            string file = Path.Combine(Path.GetTempPath(), "TCT-" + Guid.NewGuid().ToString("N") + ".xml");
            try
            {
                TCTWireCodec.Write(file, result);
                TCTTestCaseResult read = TCTWireCodec.Read(file, test.Descriptor);
                context.AssertEqual(result.Status, read.Status, "状态回环");
                context.AssertEqual(result.Assertions.Count, read.Assertions.Count, "断言数回环");
                context.AssertEqual(result.Observations[0].Details, read.Observations[0].Details, "观察数据回环");
            }
            finally { if (File.Exists(file)) File.Delete(file); }
        }

        [TCTTest("TCT.IO.002", "Fixture路径越界拒绝", TCTTestAspect.Foundation, "TestFramework",
            CapabilityAreaId = "CA-TEST-FIXTURE", CapabilityId = "TCT-FIXTURE-PATH", ObjectId = "TCTTestFixtureService")]
        public static void PathGuard(TCTTestContext context)
        {
            TCTTestFixtureService fixture = new TCTTestFixtureService(Path.Combine(Path.GetTempPath(), "TCT-fixture-root"));
            context.AssertThrows<UnauthorizedAccessException>(() => fixture.ResolvePath("..\\outside.data"), "禁止父目录越界");
            context.AssertTrue(fixture.ResolvePath("sub\\valid.data").Contains("sub"), "允许子目录", true, "sub");
        }

        [TCTTest("TCT.IO.003", "Golden必须明确审批", TCTTestAspect.Foundation, "TestFramework",
            CapabilityAreaId = "CA-TEST-FIXTURE", CapabilityId = "TCT-BASELINE", ObjectId = "TCTBaselineService")]
        public static void BaselineApproval(TCTTestContext context)
        {
            string root = Path.Combine(Path.GetTempPath(), "TCT-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                TCTBaselineService baseline = new TCTBaselineService(new TCTTestFixtureService(root));
                byte[] bytes = Encoding.UTF8.GetBytes("Golden");
                context.AssertThrows<InvalidOperationException>(() =>
                    baseline.Approve("example.data", bytes, "no"), "拒绝隐式审批");
                context.AssertTrue(!File.Exists(Path.Combine(root, "example.data")), "拒绝写入", false, File.Exists(Path.Combine(root, "example.data")));
                baseline.Approve("example.data", bytes, "APPROVE_BASELINE");
                context.AssertTrue(baseline.Matches("example.data", bytes), "显式审批后可比较", true, true);
                context.AssertTrue(!baseline.Matches("example.data", Encoding.UTF8.GetBytes("Changed")), "变更被检测", false, baseline.Matches("example.data", Encoding.UTF8.GetBytes("Changed")));
            }
            finally { Directory.Delete(root, true); }
        }

        [TCTTest("TCT.REPORT.001", "JUnit与JSON报告可解析", TCTTestAspect.Foundation, "TestFramework",
            CapabilityAreaId = "CA-TEST-REPORT", CapabilityId = "TCT-REPORT", ObjectId = "TCTReportWriter")]
        public static void ReportParse(TCTTestContext context)
        {
            ITCTTestCase test = new TCTInlineCase("TCT.INNER.REPORT", c => c.AssertEqual(3, 3, "report"));
            TCTTestCaseResult result = TCTTestRunner.RunOneAsync(test, CancellationToken.None, 0).GetAwaiter().GetResult();
            TCTTestRunSummary summary = new TCTTestRunSummary(new List<TCTTestCaseResult> { result });
            string root = Path.Combine(Path.GetTempPath(), "TCT-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string junit = Path.Combine(root, "junit.xml"), json = Path.Combine(root, "result.json");
                TCTReportWriter.WriteJUnit(junit, summary);
                TCTReportWriter.WriteJson(json, summary);
                context.AssertEqual("1", (string)System.Xml.Linq.XDocument.Load(junit).Root.Attribute("tests"), "JUnit测试数");
                object parsed = new System.Web.Script.Serialization.JavaScriptSerializer().DeserializeObject(File.ReadAllText(json));
                context.AssertTrue(parsed != null, "JSON可解析", true, parsed != null);
            }
            finally { Directory.Delete(root, true); }
        }

        [TCTTest("TCT.CORE.005", "筛选与重复执行请求校验", TCTTestAspect.Foundation, "TestFramework",
            CapabilityAreaId = "CA-TEST-DISCOVERY", CapabilityId = "TCT-FILTER", ObjectId = "TCTTestRunRequest")]
        public static void CatalogFilter(TCTTestContext context)
        {
            TCTTestCatalog catalog = new TCTTestCatalog();
            catalog.Register(new TCTInlineCase("A.CASE", c => c.AssertEqual(1, 1, "a")));
            catalog.Register(new TCTInlineCase("B.CASE", c => c.AssertEqual(2, 2, "b")));
            TCTTestRunRequest request = new TCTTestRunRequest { Filter = "A." };
            context.AssertEqual(1, catalog.Select(request).Count, "ID过滤");
            request.Repeat = 0;
            context.AssertThrows<ArgumentOutOfRangeException>(() => request.Validate(), "禁止重复0次");
        }

        private sealed class TCTInlineCase : ITCTTestCase
        {
            private readonly Action<TCTTestContext> _action;
            public TCTInlineCase(string id, Action<TCTTestContext> action)
            {
                Descriptor = new TCTTestCaseDescriptor(id, id, TCTTestAspect.Foundation, "TestFramework",
                    "TCT", "TCT", "TCT");
                _action = action;
            }
            public TCTTestCaseDescriptor Descriptor { get; private set; }
            public void Execute(TCTTestContext context) { _action(context); }
        }
    }

    public static class TCTRegistration
    {
        public static TCTTestCatalog CreateCatalog()
        {
            TCTTestCatalog catalog = new TCTTestCatalog();
            TCTRealCases.Register(catalog);
            TCTAttributeFrameworkAdapter attributes =
                new TCTAttributeFrameworkAdapter(typeof(TCTFrameworkCases).Assembly);
            foreach (ITCTTestCase test in attributes.Discover()) catalog.Register(test);
            return catalog;
        }
    }
}
