using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Data.Configuration;
using CxVisual.Mechanism.Data.Execution;
using CxVisual.Mechanism.Runtime.Execution;
using CxVisual.Mechanism.Visual;
using CxVisual.TestCenter.Core;
using CxVisual.TestCenter.Discovery;
using CxVisual.TestCenter.Fixtures;

namespace CxVisual.TestCenter.Cases
{
    public static class TCTAdversarialCases
    {
        [TCTTest("TCT.MDATA.007", "并发编辑旧Revision必须拒绝后提交", TCTTestAspect.Mechanism,
            "Data / Configuration", ObjectId = "MHSConfigSection")]
        public static void StaleConfigEdit(TCTTestContext context)
        {
            using (TCTTestSandbox sandbox = new TCTTestSandbox())
            {
                MHSConfigService service = new MHSConfigService(sandbox.PathFor("config"));
                MHSConfigSection<TCTMechanismActualCases.TCTMachineConfiguration> section =
                    service.ForMachine("stale").Open<TCTMechanismActualCases.TCTMachineConfiguration>();
                using (MHSConfigEdit<TCTMechanismActualCases.TCTMachineConfiguration> first = section.BeginEdit("first"))
                using (MHSConfigEdit<TCTMechanismActualCases.TCTMachineConfiguration> stale = section.BeginEdit("stale"))
                {
                    first.WorkingCopy.ExposureMs = 110;
                    stale.WorkingCopy.ExposureMs = 120;
                    MHSConfigCommitResult committed = first.CommitAsync(CancellationToken.None)
                        .GetAwaiter().GetResult();
                    MHSConfigCommitResult rejected = stale.CommitAsync(CancellationToken.None)
                        .GetAwaiter().GetResult();
                    context.AssertTrue(committed.Success, "第一份有效修改提交成功", true, committed.Success);
                    context.AssertTrue(!rejected.Success, "旧Revision不能覆盖新配置", false, rejected.Success);
                    context.AssertEqual("CONFIG_REVISION_CONFLICT", rejected.ErrorCode, "冲突可诊断");
                    context.AssertEqual(110, section.GetLatestSaved().ExposureMs, "最终保存没有被旧编辑覆盖");
                }
            }
        }

        [TCTTest("TCT.MDATA.008", "离线配置包SHA256篡改被拒绝", TCTTestAspect.Mechanism,
            "Data / Configuration", ObjectId = "MHSConfigPackage")]
        public static void TamperedConfigPackage(TCTTestContext context)
        {
            using (TCTTestSandbox sandbox = new TCTTestSandbox())
            {
                MHSConfigSection<TCTMechanismActualCases.TCTMachineConfiguration> section =
                    new MHSConfigService(sandbox.PathFor("config")).ForMachine("source")
                        .Open<TCTMechanismActualCases.TCTMachineConfiguration>();
                MHSConfigPackage package = section.ExportPackage();
                context.AssertTrue(package.Verify(), "原始包完整性通过", true, package.Verify());
                package.Json += " ";
                context.AssertTrue(!package.Verify(), "修改载荷不能通过同一Hash", false, package.Verify());
                MHSConfigCommitResult rejected = section.ImportPackageAsync(package, CancellationToken.None)
                    .GetAwaiter().GetResult();
                context.AssertTrue(!rejected.Success, "篡改包不能导入", false, rejected.Success);
                context.AssertEqual("CONFIG_PACKAGE_HASH_MISMATCH", rejected.ErrorCode, "拒绝代码明确");
                context.AssertEqual(80, section.Get().ExposureMs, "有效配置保持不变");
            }
        }

        [TCTTest("TCT.MDATA.009", "Owned数据释放异常仍继续释放其他资源", TCTTestAspect.Mechanism,
            "Data / OwnedResource", ObjectId = "MDEExecutionDataScope")]
        public static void CleanupFailureIsAggregated(TCTTestContext context)
        {
            int released = 0;
            string order = "";
            MDEExecutionDataScope root = MDEExecutionDataScope.CreateRoot("cleanup-errors");
            root.PutOwned("first", 1, x => { released++; order += "A"; });
            root.PutOwned("second", 2, x =>
            {
                released++; order += "B"; throw new IOException("expected-test-disposal-error");
            });
            context.AssertThrows<AggregateException>(() => root.Dispose(), "释放异常汇总");
            context.AssertEqual("BA", order, "即使先释放异常仍继续逆序清理");
            context.AssertEqual(2, released, "每个回收动作恰好一次");
            context.AssertTrue(root.IsClosed, "有回收异常也不能保留可变开放状态", true, root.IsClosed);
            root.Dispose();
            context.AssertEqual(2, released, "重复Dispose不再次释放");
        }

        [TCTTest("TCT.MVIS.003", "Undo后新事务须截断旧Redo分支", TCTTestAspect.Mechanism,
            "Visual / Editing", ObjectId = "CxVisualEditor")]
        public static void RedoBranchInvalidation(TCTTestContext context)
        {
            MEMEngineeringSession document = TCTRealCases.CreateOrganizationSample();
            CxVisualEditor editor = new CxVisualEditor(document);
            int initialRevision = document.Revision;
            context.AssertTrue(editor.AddRectangle("A", 10, 10, 20, 20).Success, "新增A", true, true);
            context.AssertTrue(editor.AddRectangle("B", 50, 10, 20, 20).Success, "新增B", true, true);
            context.AssertTrue(editor.Undo().Success, "撤销B", true, true);
            context.AssertTrue(editor.CanRedo, "撤销后原分支可Redo", true, editor.CanRedo);
            context.AssertTrue(editor.AddRectangle("C", 50, 20, 20, 20).Success, "新分支C", true, true);
            context.AssertTrue(!editor.CanRedo, "新分支覆盖后不能Redo已废弃B", false, editor.CanRedo);
            context.AssertTrue(!editor.Scene.Any(x => x.Name == "B"), "已废弃分支不应出现", false, false);
            context.AssertTrue(editor.Scene.Any(x => x.Name == "C"), "新分支应可见", true, true);
            context.AssertEqual(initialRevision + 4, document.Revision, "事务Revision顺序");
            context.Observe("ObjectTree", "真实Undo编辑分支", "Add A → Add B → Undo B → Add C; Redo=false");
        }

        [TCTTest("TCT.MRK.005", "循环依赖在编译器拒绝且执行委托未被调用", TCTTestAspect.Mechanism,
            "Runtime / Compile", ObjectId = "MRKExecutionPlanCompiler")]
        public static void CompilerRejectsCycle(TCTTestContext context)
        {
            int invoked = 0;
            TCTCycleResolver resolver = new TCTCycleResolver(() => Interlocked.Increment(ref invoked));
            MRKCompileResult result = new MRKExecutionPlanCompiler().Compile("cyclic", new[]
            {
                new MRKStepCompileRequest("a", "TCT.SafeCalc", new[] { "b" }),
                new MRKStepCompileRequest("b", "TCT.SafeCalc", new[] { "a" })
            }, resolver);
            context.AssertTrue(!result.Succeeded && result.Plan == null, "循环图不产生Plan", false, result.Succeeded);
            context.AssertTrue(result.Diagnostics.Any(x => x.Code == "INVALID_GRAPH"),
                "编译错误能指示InvalidGraph", true, true);
            context.AssertEqual(0, invoked, "编译不能调用专业执行委托");
            context.Observe("Graph", "真实MRK循环检测", string.Join(" | ",
                result.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        }

        private sealed class TCTCycleResolver : IMRKCapabilityResolver
        {
            private readonly Action _onRun;
            public TCTCycleResolver(Action onRun) { _onRun = onRun; }
            public bool TryResolve(string id, out MRKResolvedCapability capability)
            {
                if (id != "TCT.SafeCalc") { capability = null; return false; }
                capability = new MRKResolvedCapability((ctx, cancellation) =>
                {
                    _onRun();
                    return Task.FromResult((object)1);
                });
                return true;
            }
        }
    }
}
