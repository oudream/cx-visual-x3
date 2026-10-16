using System;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Execution;
using CxVisual.Mechanism.Data.Execution;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Runtime.Execution;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class ExecutionDataScopeTests
    {
        internal static void Run()
        {
            ParentChildIsolationAndSingleAssignment();
            RootCannotCloseBeforeDescendants();
            OwnedDataReleasedOnceInReverseOrder();
            FlowAndStepScopesAreConnectedAndClosed();
        }
        private static void Check(bool ok, string msg)
        { if (!ok) throw new InvalidOperationException("运行数据作用域：" + msg); }
        private static void ParentChildIsolationAndSingleAssignment()
        {
            using (MDEExecutionDataScope root = MDEExecutionDataScope.CreateRoot("run"))
            {
                root.Put("plan", 4);
                using (IFDEExecutionDataScope left = root.CreateChild("left"))
                using (IFDEExecutionDataScope right = root.CreateChild("right"))
                {
                    int plan;
                    Check(left.TryRead("plan", out plan) && plan == 4, "未继承父数据");
                    left.Put("private", 22);
                    int number;
                    Check(!right.TryRead("private", out number), "分支局部数据泄露");
                    bool refused = false;
                    try { left.Put("private", 33); }
                    catch (InvalidOperationException) { refused = true; }
                    Check(refused, "已公布数据可以被隐式覆盖");
                    Check(left.SnapshotLocal().Count == 1 && root.SnapshotLocal().Count == 1,
                        "快照没有区分作用域");
                }
            }
        }
        private static void RootCannotCloseBeforeDescendants()
        {
            MDEExecutionDataScope root = MDEExecutionDataScope.CreateRoot("root");
            IFDEExecutionDataScope child = root.CreateChild("child");
            bool refused = false;
            try { root.Dispose(); }
            catch (InvalidOperationException) { refused = true; }
            Check(refused && !root.IsClosed, "父级被过早关闭");
            child.Dispose(); root.Dispose(); root.Dispose();
            Check(root.IsClosed, "正常关闭失败");
        }
        private static void OwnedDataReleasedOnceInReverseOrder()
        {
            int count = 0;
            string order = "";
            MDEExecutionDataScope scope = MDEExecutionDataScope.CreateRoot("owned");
            scope.PutOwned("a", new byte[10], value =>
            { Interlocked.Increment(ref count); order += "a"; });
            scope.PutOwned("b", new byte[20], value =>
            { Interlocked.Increment(ref count); order += "b"; });
            scope.Dispose(); scope.Dispose();
            Check(count == 2 && order == "ba", "资源回收不是逆序且恰好一次");
            bool refused = false;
            try { scope.Put("late", 1); } catch (ObjectDisposedException) { refused = true; }
            Check(refused, "关闭后仍可发布数据");
        }        private static void FlowAndStepScopesAreConnectedAndClosed()
        {
            int released = 0;
            MDEExecutionDataScope root = MDEExecutionDataScope.CreateRoot("host");
            root.Put("BatchId", "B007");
            MRKExecutionPlan plan = new MRKExecutionPlan("scoped", new[]
            {
                new MRKExecutionStep("left", new string[0], (ctx,ct) =>
                {
                    string batch;
                    Check(ctx.DataScope != null &&
                        ctx.DataScope.TryRead("BatchId", out batch) && batch == "B007",
                        "Flow未继承统一数据作用域");
                    ctx.DataScope.PutOwned("image", new byte[32], value =>
                        Interlocked.Increment(ref released));
                    return Task.FromResult((object)1);
                }),
                new MRKExecutionStep("right", new string[0], (ctx,ct) =>
                {
                    byte[] image;
                    Check(!ctx.DataScope.TryRead("image", out image),
                        "并行节点泄漏另一个节点的局部数据");
                    return Task.FromResult((object)2);
                })
            });
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(2,4));
            MRKFlowResult result = Wait(engine.StartFlowWithData(plan, root).Completion);
            Check(result.State == MRKFlowState.Completed && released == 1,
                "Flow结束前资源未安全释放");
            root.Dispose();
            Check(root.IsClosed && released == 1, "关闭后的根作用域仍持有子资源");
            Wait(engine.StopAsync());
        }
        private static T Wait<T>(Task<T> task)
        {
            Task first = Task.WhenAny(task, Task.Delay(6000)).GetAwaiter().GetResult();
            if (!object.ReferenceEquals(first,task)) throw new TimeoutException("数据作用域运行测试超时");
            return task.GetAwaiter().GetResult();
        }
        private static void Wait(Task task)
        {
            Task first = Task.WhenAny(task, Task.Delay(6000)).GetAwaiter().GetResult();
            if (!object.ReferenceEquals(first,task)) throw new TimeoutException("数据作用域运行测试超时");
            task.GetAwaiter().GetResult();
        }

    }
}
