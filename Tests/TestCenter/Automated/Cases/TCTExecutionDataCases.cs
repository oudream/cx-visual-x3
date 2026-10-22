using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Execution;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Data.Execution;
using CxVisual.Mechanism.Runtime.Execution;
using CxVisual.TestCenter.Core;
using CxVisual.TestCenter.Discovery;

namespace CxVisual.TestCenter.Cases
{
    // Source-backed execution scope/compiler cases, never a second TCT DataScope or Graph.
    public static class TCTExecutionDataCases
    {
        private sealed class TCTKnownResolver : IMRKCapabilityResolver
        {
            private readonly Func<MRKExecutionContext, CancellationToken, Task<object>> _handler;
            internal int ResolveCount;
            public TCTKnownResolver(Func<MRKExecutionContext, CancellationToken, Task<object>> handler)
            { _handler = handler; }
            public bool TryResolve(string capabilityId, out MRKResolvedCapability capability)
            {
                if (capabilityId == "TCT.Calculation")
                {
                    Interlocked.Increment(ref ResolveCount);
                    capability = new MRKResolvedCapability(_handler, new MRKExecutionOutput(typeof(int)));
                    return true;
                }
                capability = null;
                return false;
            }
        }

        [TCTTest("TCT.MDATA.004", "DataScope父级只读继承与分支隔离", TCTTestAspect.Mechanism,
            "Data / ExecutionScope", ObjectId = "MDEExecutionDataScope")]
        public static void ScopeParentChild(TCTTestContext context)
        {
            using (MDEExecutionDataScope root = MDEExecutionDataScope.CreateRoot("flow"))
            {
                root.Put("batch", "A-2026");
                using (IFDEExecutionDataScope left = root.CreateChild("vision"))
                using (IFDEExecutionDataScope right = root.CreateChild("ct"))
                {
                    string batch;
                    context.AssertTrue(left.TryRead("batch", out batch), "子流程可读父级数据", true, true);
                    context.AssertEqual("A-2026", batch, "父级批次值");
                    left.Put("result", 1024);
                    int result;
                    context.AssertTrue(!right.TryRead("result", out result), "分支结果不串用", false, false);
                    context.AssertTrue(!root.TryRead("result", out result), "父级不可读子局部结果", false, false);
                    context.AssertThrows<InvalidOperationException>(
                        () => left.Put("result", 2048), "局部数据不支持静默覆盖");
                    context.AssertEqual(1, left.SnapshotLocal().Count, "子作用域仅包含本地数据");
                    context.AssertEqual(1, root.SnapshotLocal().Count, "父级Snapshot没有子数据");
                    context.Observe("Runtime", "MDE真实数据作用域",
                        "flow/batch=A-2026; flow/vision/result=1024; sibling flow/ct cannot access result");
                }
            }
        }

        [TCTTest("TCT.MDATA.005", "DataScope占用期禁止销毁父级", TCTTestAspect.Mechanism,
            "Data / ExecutionScope", ObjectId = "MDEExecutionDataScope")]
        public static void ParentCannotDisposeEarly(TCTTestContext context)
        {
            MDEExecutionDataScope root = MDEExecutionDataScope.CreateRoot("flow");
            IFDEExecutionDataScope child = root.CreateChild("subflow");
            try
            {
                context.AssertThrows<InvalidOperationException>(() => root.Dispose(), "子级仍存活不能释放父级");
                context.AssertTrue(!root.IsClosed, "父级仍处可读状态", false, root.IsClosed);
                child.Dispose();
                root.Dispose();
                root.Dispose();
                context.AssertTrue(root.IsClosed, "子级退出后正常关闭", true, root.IsClosed);
                context.AssertThrows<ObjectDisposedException>(() => root.Put("after", 1), "关闭后禁止写入");
            }
            finally { child.Dispose(); root.Dispose(); }
        }

        [TCTTest("TCT.MDATA.006", "DataScope资源逆序且恰好释放一次", TCTTestAspect.Mechanism,
            "Data / OwnedResource", ObjectId = "MDEExecutionDataScope")]
        public static void OwnedResourcesRelease(TCTTestContext context)
        {
            string order = "";
            MDEExecutionDataScope root = MDEExecutionDataScope.CreateRoot("owned");
            root.PutOwned("first", new byte[4], x => order += "A");
            root.PutOwned("second", new byte[16], x => order += "B");
            root.Dispose();
            root.Dispose();
            context.AssertEqual("BA", order, "资源逆序仅释放一次");
            context.AssertTrue(root.IsClosed, "释放后关闭", true, root.IsClosed);
        }

        [TCTTest("TCT.MRK.003", "MRK编译真实能力后执行同一Delegate", TCTTestAspect.Mechanism,
            "Runtime / Compile", ObjectId = "MRKExecutionPlanCompiler")]
        public static void CompilerExecutesKnownCapability(TCTTestContext context)
        {
            int executed = 0;
            TCTKnownResolver resolver = new TCTKnownResolver((runtime, token) =>
            {
                Interlocked.Increment(ref executed);
                return Task.FromResult((object)42);
            });
            MRKCompileResult compiled = new MRKExecutionPlanCompiler().Compile("TCT-Compiled", new[] {
                new MRKStepCompileRequest("b", "TCT.Calculation", new[] { "a" },
                    inputs: new [] { new MRKExecutionInput("origin", MRKInputSource.StepResult, "a", typeof(int)) }),
                new MRKStepCompileRequest("a", "TCT.Calculation")
            }, resolver);
            context.AssertTrue(compiled.Succeeded, "真实计划编译通过", true, compiled.Succeeded);
            context.AssertEqual(2, resolver.ResolveCount, "能力解析两次");
            context.AssertEqual("a", compiled.Plan.Steps[0].Id, "排序固定不受定义顺序影响");

            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(2, 8));
            try
            {
                MRKFlowResult actual = engine.StartFlow(compiled.Plan).Completion.GetAwaiter().GetResult();
                context.AssertEqual(MRKFlowState.Completed, actual.State, "编译计划真实执行");
                context.AssertEqual(42, (int)actual.Results["b"], "运行使用解析到的Delegate");
                context.AssertEqual(2, executed, "没有复制模拟执行");
                context.Observe("Runtime", "能力编译→计划→执行",
                    "Plan=" + compiled.Plan.Id + "; FlowId=" + actual.FlowId +
                    "; Resolved=2; Invoked=" + executed + "; Result.b=42");
            }
            finally { engine.StopAsync(true).GetAwaiter().GetResult(); }
        }

        [TCTTest("TCT.MRK.004", "MRK编译聚合错误且拒绝无效图执行", TCTTestAspect.Mechanism,
            "Runtime / Compile", ObjectId = "MRKExecutionPlanCompiler")]
        public static void CompilerRejectsInvalidGraph(TCTTestContext context)
        {
            int executed = 0;
            TCTKnownResolver resolver = new TCTKnownResolver((runtime, token) =>
            {
                Interlocked.Increment(ref executed); return Task.FromResult((object)1);
            });
            MRKCompileResult invalid = new MRKExecutionPlanCompiler().Compile("Invalid", new[] {
                new MRKStepCompileRequest("same", "missing", new [] { "ghost" }),
                new MRKStepCompileRequest("same", "TCT.Calculation"),
                new MRKStepCompileRequest("third", "missing-again")
            }, resolver);
            context.AssertTrue(!invalid.Succeeded && invalid.Plan == null,
                "编译失败不返回可执行计划", false, invalid.Succeeded);
            context.AssertTrue(invalid.Diagnostics.Any(x => x.Code == "DUPLICATE_STEP"),
                "重复节点诊断", true, true);
            context.AssertTrue(invalid.Diagnostics.Any(x => x.Code == "UNKNOWN_DEPENDENCY"),
                "缺失依赖诊断", true, true);
            context.AssertTrue(invalid.Diagnostics.Count(x => x.Code == "UNKNOWN_CAPABILITY") >= 2,
                "未知能力诊断", true, true);
            context.AssertEqual(0, executed, "无效图绝不能调用真实能力");
            context.Observe("Graph", "MRKCompileResult真实编译诊断",
                string.Join(" | ", invalid.Diagnostics.Select(x => x.StepId + ":" + x.Code)));
        }
    }
}