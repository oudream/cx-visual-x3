using System;
using System.IO;
using System.Linq;
using System.Text;
using CxVisual.TestCenter.Core;
using CxVisual.TestCenter.Discovery;
using CxVisual.TestCenter.Execution;
using CxVisual.TestCenter.Fixtures;
using CxVisual.TestCenter.Models;

namespace CxVisual.TestCenter.Cases
{
    // Adversarial framework tests, compiled automatically via Cases wildcard.
    public static class TCTDeveloperExperienceCases
    {
        [TCTTest("TCT.DEV.001", "Golden必须明确审批与版本校验",
            TCTTestAspect.Foundation, "TestFramework")]
        public static void GoldenApproval(TCTTestContext test)
        {
            using (TCTTestSandbox sandbox = new TCTTestSandbox())
            {
                TCTGoldenService golden = new TCTGoldenService(
                    new TCTTestFixtureService(sandbox.Root));
                byte[] sample = Encoding.UTF8.GetBytes("v1\n");
                TCTGoldenComparison missing = golden.Compare("golden/sample.bin", sample);
                test.AssertTrue(!missing.BaselineFound, "初始基准缺失明确显示", false, missing.BaselineFound);
                test.AssertThrows<UnauthorizedAccessException>(() =>
                    golden.Approve("golden/sample.bin", sample, "reviewer", "none",
                        TCTGoldenService.NoExistingBaseline), "未授权禁止Approve");
                test.AssertThrows<InvalidOperationException>(() =>
                    golden.Approve("golden/sample.bin", sample, "reviewer",
                        "APPROVE_GOLDEN_EXPLICITLY", "WRONG"), "禁止未经版本校验的审批");
                TCTGoldenApprovalReceipt receipt = golden.Approve("golden/sample.bin",
                    sample, "reviewer", "APPROVE_GOLDEN_EXPLICITLY",
                    TCTGoldenService.NoExistingBaseline);
                test.AssertEqual("reviewer", receipt.Actor, "审批人");
                test.AssertTrue(golden.Compare("golden/sample.bin", sample).Matches,
                    "保存后的真实Golden比较", true, true);
                TCTGoldenComparison changed = golden.Compare("golden/sample.bin",
                    Encoding.UTF8.GetBytes("v2\n"));
                test.AssertTrue(!changed.Matches, "不同结果不得变成Passed", false, changed.Matches);
                test.AssertTrue(changed.Diff.Differences > 0, "可定位差异数量", true, changed.Diff.Differences);
                test.AssertThrows<InvalidOperationException>(() =>
                    golden.Approve("golden/sample.bin", Encoding.UTF8.GetBytes("v2\n"),
                        "reviewer", "APPROVE_GOLDEN_EXPLICITLY",
                        TCTGoldenService.NoExistingBaseline), "旧版本审批不能覆盖新版本");
                test.Observe("ObjectTree", "Golden审批真实证据",
                    "Actor=" + receipt.Actor + " SHA256=" + receipt.NewSha256);
            }
        }

        [TCTTest("TCT.DEV.002", "真实事件流记录、回放与截断边界",
            TCTTestAspect.Foundation, "TestFramework")]
        public static void EventJournal(TCTTestContext test)
        {
            using (TCTTestSandbox sandbox = new TCTTestSandbox())
            {
                string file = sandbox.PathFor("events.log");
                TCTTestEventJournal journal = new TCTTestEventJournal(file);
                DateTime stamp = new DateTime(638964000000000000L, DateTimeKind.Utc);
                journal.Write(new TCTTestEvent(1, "attempt", TCTTestEventKind.Started,
                    "Run started", stamp));
                journal.Write(new TCTTestEvent(2, "attempt", TCTTestEventKind.Observation,
                    "真实运行状态包含\\n换行和|字段", stamp));
                int count = 0;
                var events = journal.ReadNew("attempt", ref count);
                test.AssertEqual(2, events.Count, "有序事件数量");
                test.AssertEqual(2, count, "Reader cursor");
                test.AssertEqual("真实运行状态包含\\n换行和|字段", events[1].Message, "复杂字符保持");
                test.AssertEqual(stamp, events[1].TimestampUtc, "UTC时间戳保持");
                File.AppendAllText(file, "3|1|");
                test.AssertEqual(0, journal.ReadNew("attempt", ref count).Count,
                    "半写尾部不应伪造事件");
            }
        }

        [TCTTest("TCT.DEV.003", "源码定位只接受真实PDB堆栈",
            TCTTestAspect.Foundation, "TestFramework")]
        public static void SourceLocation(TCTTestContext test)
        {
            string source = Path.Combine(Path.GetTempPath(), "CxVisual", "Cases", "Sample.cs");
            string stack = "at Sample.Calculate() in " + source + ":line 42";
            TCTTestSourceLocation result = TCTTestSourceLocator.TryParse(stack);
            test.AssertTrue(result != null, "PDB样式行有可读位置", true, result != null);
            test.AssertEqual(source, result.File, "绝不更改可核查路径");
            test.AssertEqual(42, result.Line, "原始行号");
            test.AssertTrue(TCTTestSourceLocator.TryParse("at Sample.Calculate()") == null,
                "缺PDB不猜测位置", true, true);
        }
    }
}
