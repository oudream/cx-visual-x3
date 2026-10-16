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
        }
    }
}
