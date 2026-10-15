using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.TestCenter.Core;
using CxVisual.TestCenter.Diagnostics;
using CxVisual.TestCenter.Discovery;
using CxVisual.TestCenter.Execution;
using CxVisual.TestCenter.Fixtures;
using CxVisual.TestCenter.Models;
using CxVisual.TestCenter.Reports;

namespace CxVisual.TestCenter.Cases
{
    public static class TCTFrameworkExtendedCases
    {
        [TCTTest("TCT.RUN.001", "Run/Attempt身份与重复并行", TCTTestAspect.Foundation, "TestFramework",
            CapabilityAreaId = "Unmapped", CapabilityId = "Unmapped", ObjectId = "TCTTestAttempt")]
        public static void UniqueAttempts(TCTTestContext context)
        {
            TCTTestCatalog catalog = new TCTTestCatalog();
            catalog.Register(new TCTProbeCase("TCT.INNER.UNIQUE", false, false));
            TCTTestRunRequest request = new TCTTestRunRequest {
                Repeat = 3, MaxConcurrency = 3, Isolation = TCTTestIsolation.InProcess
            };
            TCTTestRunSummary run = new TCTTestExecutionCoordinator().RunAsync(
                catalog, request, CancellationToken.None, null).GetAwaiter().GetResult();
            context.AssertEqual(3, run.Total, "重复数量");
            context.AssertEqual(3, run.Passed, "全部真实通过");
            context.AssertEqual(request.RunId, run.RunId, "RunId传播");
            context.AssertEqual(3, run.Results.Select(x => x.AttemptId).Distinct().Count(), "AttemptId独立");
            context.AssertSequenceEqual(new[] { 0, 1, 2 },
                run.Results.Select(x => x.Attempt.Iteration), "Iteration稳定");
            context.AssertTrue(run.Results.All(x => x.RunId == request.RunId), "RunId一致", true, true);
        }

        [TCTTest("TCT.RUN.002", "FailFast保留未启动尝试证据", TCTTestAspect.Foundation, "TestFramework")]
        public static void FailFast(TCTTestContext context)
        {
            TCTTestCatalog catalog = new TCTTestCatalog();
            catalog.Register(new TCTProbeCase("TCT.INNER.FAILFAST", true, false));
            TCTTestRunRequest request = new TCTTestRunRequest {
                Repeat = 4, MaxConcurrency = 4, FailFast = true, Isolation = TCTTestIsolation.InProcess
            };
            TCTTestRunSummary run = new TCTTestExecutionCoordinator().RunAsync(
                catalog, request, CancellationToken.None, null).GetAwaiter().GetResult();
            context.AssertEqual(4, run.Total, "总选择4次");
            context.AssertEqual(1, run.Results.Count(x => x.Status == TCTTestStatus.Failed), "首次失败");
            context.AssertEqual(3, run.Skipped, "其余必须明示未启动");
            context.AssertTrue(!run.Success, "不能假绿", false, run.Success);
        }

        [TCTTest("TCT.RUN.003", "Lifecycle错误保证Teardown与诊断", TCTTestAspect.Foundation, "TestFramework")]
        public static void Lifecycle(TCTTestContext context)
        {
            TCTProbeCase test = new TCTProbeCase("TCT.INNER.TEARDOWN", false, true);
            TCTTestCaseResult result = TCTTestRunner.RunOneAsync(test, CancellationToken.None, 1)
                .GetAwaiter().GetResult();
            context.AssertTrue(test.Setup && test.Teardown, "Setup/Teardown均调用", true, test.Setup && test.Teardown);
            context.AssertEqual(TCTTestStatus.Error, result.Status, "清理失败必须Error");
            context.AssertTrue(result.Diagnostics.Any(x => x.Code == "TCT.TEARDOWN"),
                "诊断含Teardown", true, true);
        }

        [TCTTest("TCT.RUN.004", "Run状态机拒绝非法二次终结", TCTTestAspect.Foundation, "TestFramework")]
        public static void RunState(TCTTestContext context)
        {
            TCTTestRunSession session = new TCTTestRunSession(Guid.NewGuid().ToString("N"), TCTTestRunProfile.CI);
            context.AssertEqual(TCTTestRunState.Prepared, session.State, "初始");
            session.Start(); session.RequestCancel();
            context.AssertEqual(TCTTestRunState.CancellationRequested, session.State, "取消请求态");
            session.Finish(false);
            context.AssertEqual(TCTTestRunState.Completed, session.State, "已终结");
            context.AssertThrows<InvalidOperationException>(() => session.Finish(false), "禁止二次终结");
        }

        [TCTTest("TCT.IO.004", "Artifact SHA-256回环及篡改拒绝", TCTTestAspect.Foundation, "TestFramework")]
        public static void Artifacts(TCTTestContext context)
        {
            string root = Path.Combine(Path.GetTempPath(), "TCT-art-" + Guid.NewGuid().ToString("N"));
            try
            {
                TCTTestArtifactStore store = new TCTTestArtifactStore(root);
                byte[] data = Encoding.UTF8.GetBytes("actual geometry evidence / 真实结果");
                TCTArtifactRef artifact = store.Save(data, "text/plain", context.AttemptId);
                context.AssertSequenceEqual(data, store.Read(artifact), "原始字节Roundtrip");
                string artifactPath = Path.Combine(root, artifact.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                File.WriteAllText(artifactPath, "tampered");
                context.AssertThrows<InvalidDataException>(() => store.Read(artifact), "拒绝长度或Hash不一致");
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        [TCTTest("TCT.IO.005", "沙箱越界与自动清理", TCTTestAspect.Foundation, "TestFramework")]
        public static void Sandbox(TCTTestContext context)
        {
            string root;
            using (TCTTestSandbox scope = new TCTTestSandbox())
            {
                root = scope.Root;
                context.AssertThrows<UnauthorizedAccessException>(() => scope.PathFor("../escape.txt"), "越界");
                string path = scope.PathFor("data/snapshot.bin");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "test");
                context.AssertTrue(File.Exists(path), "数据存在", true, File.Exists(path));
            }
            context.AssertTrue(!Directory.Exists(root), "Dispose清理", false, Directory.Exists(root));
        }

        [TCTTest("TCT.IO.006", "Run历史报告可回读比较", TCTTestAspect.Foundation, "TestFramework")]
        public static void History(TCTTestContext context)
        {
            string root = Path.Combine(Path.GetTempPath(), "TCT-history-" + Guid.NewGuid().ToString("N"));
            try
            {
                TCTTestCatalog catalog = new TCTTestCatalog();
                catalog.Register(new TCTProbeCase("TCT.INNER.HISTORY", false, false));
                TCTTestRunSummary run = new TCTTestExecutionCoordinator().RunAsync(catalog,
                    new TCTTestRunRequest { Isolation = TCTTestIsolation.InProcess },
                    CancellationToken.None, null).GetAwaiter().GetResult();
                TCTTestResultRepository history = new TCTTestResultRepository(root);
                string path = history.Save(run);
                context.AssertTrue(File.Exists(Path.Combine(path, "summary.json")), "JSON归档", true, true);
                context.AssertTrue(File.Exists(Path.Combine(path, "report.junit.xml")), "JUnit归档", true, true);
                TCTStoredTestRun saved = history.Load(run.RunId);
                context.AssertEqual("Passed", saved.Statuses["TCT.INNER.HISTORY#0"], "结果恢复");
                TCTTestRunDiff diff = TCTTestResultRepository.Compare(saved, saved);
                context.AssertEqual(0, diff.Regressions.Count, "无差异");
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        [TCTTest("TCT.DIAG.001", "Observer事件保序与容量保护", TCTTestAspect.Foundation, "TestFramework")]
        public static void Observer(TCTTestContext context)
        {
            TCTTestObserverHub hub = new TCTTestObserverHub(2);
            TCTTestEvent a = hub.Publish("x", TCTTestEventKind.Started, "started");
            TCTTestEvent b = hub.Publish("x", TCTTestEventKind.Assertion, "pass");
            context.AssertEqual(1L, a.Sequence, "首序号");
            context.AssertEqual(2L, b.Sequence, "次序号");
            context.AssertThrows<InvalidOperationException>(() =>
                hub.Publish("x", TCTTestEventKind.Completed, "overflow"), "不得静默丢证据");
            hub.Seal();
            context.AssertThrows<InvalidOperationException>(() =>
                hub.Publish("x", TCTTestEventKind.Diagnostic, "sealed"), "封存后拒写");
        }

        [TCTTest("TCT.DIAG.002", "正式能力ID白名单阻止虚报覆盖", TCTTestAspect.Foundation, "TestFramework")]
        public static void Coverage(TCTTestContext context)
        {
            TCTTestCatalog catalog = new TCTTestCatalog();
            catalog.Register(new TCTProbeCase("TCT.INNER.COVERAGE", false, false));
            TCTTestRunSummary run = new TCTTestExecutionCoordinator().RunAsync(catalog,
                new TCTTestRunRequest { Isolation = TCTTestIsolation.InProcess },
                CancellationToken.None, null).GetAwaiter().GetResult();
            var coverage = TCTCapabilityCoverageService.Calculate(run, new string[0]);
            context.AssertEqual("Unmapped", coverage[0].CapabilityId, "不能猜正式能力ID");
            context.AssertEqual(0, coverage[0].Verified, "未映射不得标Verified");
            context.AssertEqual(1, coverage[0].Unmapped, "未映射数");
        }

        [TCTTest("TCT.DIAG.003", "结构和字节差异可定位", TCTTestAspect.Foundation, "TestFramework")]
        public static void Diff(TCTTestContext context)
        {
            TCTSnapshotDiff bytes = TCTSnapshotDiffService.CompareBytes(
                new byte[] { 1, 2, 3 }, new byte[] { 1, 9, 3, 4 });
            context.AssertEqual(2, bytes.Differences, "字节差异含额外长度");
            TCTSnapshotDiff lines = TCTSnapshotDiffService.CompareLines("a\nb", "a\nc");
            context.AssertEqual(1, lines.Differences, "文本差异");
            context.AssertTrue(lines.Explanation.Contains("line=2"), "首个差异行", true, true);
        }

        [TCTTest("TCT.DIAG.004", "相对数值误差和特殊值", TCTTestAspect.Foundation, "TestFramework")]
        public static void Numerics(TCTTestContext context)
        {
            context.AssertTrue(TCTNumericComparer.AreClose(1000, 1000.1, 0, 0.0002),
                "相对容差", true, true);
            context.AssertTrue(!TCTNumericComparer.AreClose(double.NaN, double.NaN, 0, 0),
                "NaN默认不相等", false, false);
            context.AssertTrue(!TCTNumericComparer.AreClose(double.PositiveInfinity, double.PositiveInfinity, 0, 0),
                "Infinity须显式策略", false, false);
            context.AssertThrows<ArgumentOutOfRangeException>(() =>
                TCTNumericComparer.AreClose(1, 1, -1, 0), "非法容差");
        }

        [TCTTest("TCT.RUN.005", "独占资源串行与释放", TCTTestAspect.Foundation, "TestFramework")]
        public static void ResourceGate(TCTTestContext context)
        {
            TCTResourceGate gate = new TCTResourceGate();
            IDisposable first = gate.AcquireAsync(new[] { "GPU0" }, CancellationToken.None)
                .GetAwaiter().GetResult();
            Task<IDisposable> pending = gate.AcquireAsync(new[] { "GPU0" }, CancellationToken.None);
            context.AssertTrue(!pending.IsCompleted, "第二个请求必须等待", false, pending.IsCompleted);
            first.Dispose();
            using (IDisposable second = pending.GetAwaiter().GetResult())
                context.AssertTrue(true, "释放后可获取", true, true);
        }

        private sealed class TCTProbeCase : ITCTTestCase, ITCTTestLifecycle
        {
            private readonly bool _fail;
            private readonly bool _failTeardown;
            public TCTProbeCase(string id, bool fail, bool failTeardown)
            {
                Descriptor = new TCTTestCaseDescriptor(id, id, TCTTestAspect.Foundation,
                    "TestFramework", "Unmapped", "Unmapped", "TCT");
                _fail = fail; _failTeardown = failTeardown;
            }
            public bool Setup { get; private set; }
            public bool Teardown { get; private set; }
            public TCTTestCaseDescriptor Descriptor { get; private set; }
            public Task SetupAsync(TCTTestContext context)
            { Setup = true; return Task.FromResult(0); }
            public void Execute(TCTTestContext context)
            { context.AssertEqual(1, _fail ? 2 : 1, "实际执行断言"); }
            public Task TeardownAsync(TCTTestContext context)
            {
                Teardown = true;
                if (_failTeardown) throw new IOException("Intentional teardown failure");
                return Task.FromResult(0);
            }
        }
    }
}
