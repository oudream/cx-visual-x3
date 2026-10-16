using System;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Execution;
using CxVisual.Mechanism.Data.Execution;
using CxVisual.Mechanism.Data.RuntimeData;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Runtime.Execution;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class ExecutionDataScopeTests
    {
        internal static void Run()
        {
            HeapMoveCopyAndIsolation();
            DataContextStagesAndCommits();
            ParentChildIsolationAndSingleAssignment();
            RootCannotCloseBeforeDescendants();
            OwnedDataReleasedOnceInReverseOrder();
            FlowAndStepScopesAreConnectedAndClosed();
            CleanupFailureStillReleasesOtherOwnedObjects();
        }


        private static void DataContextStagesAndCommits()
        {
            using (MDEExecutionDataScope scope = MDEExecutionDataScope.CreateRoot("context"))
            {
                MDSDataContext context = new MDSDataContext(scope);
                context.Set("Threshold", 120);
                Check(context.Get<int>("Threshold") == 120, "DataContext读取失败");
                bool rejected = false;
                try { context.Set("Threshold", 121); }
                catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "局部重复写入未拒绝");
                byte[] original = new byte[] { 1, 2 };
                context.Return("Image", original);
                original[0] = 44;
                byte[] pending = (byte[])context.InspectPendingOutputs()["Image"];
                pending[1] = 55;
                byte[] committed = (byte[])context.CommitOutputs()["Image"];
                Check(committed[0] == 1 && committed[1] == 2, "Output暂存未防止别名修改");
                rejected = false;
                try { context.Return("Late", 1); }
                catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "已提交上下文仍能Return");
            }
            using (MDEExecutionDataScope scope = MDEExecutionDataScope.CreateRoot("abort"))
            {
                MDSDataContext context = new MDSDataContext(scope);
                context.Return("Partial", 42);
                context.AbortOutputs();
                bool rejected = false;
                try { context.CommitOutputs(); }
                catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "取消后仍然可以提交暂存输出");
            }
        }

        private static void HeapMoveCopyAndIsolation()
        {
            MDSDataHeap a = new MDSDataHeap();
            MDSDataHeap b = new MDSDataHeap();
            byte[] original = new byte[] { 1, 2, 3 };
            a.Set("Image", original);
            original[0] = 99;
            Check(a.Get<byte[]>("Image")[0] == 1, "Set暴露原数组写入别名");
            byte[] view = a.Get<byte[]>("Image");
            view[1] = 99;
            Check(a.Get<byte[]>("Image")[1] == 2, "Get暴露可写底层数据");
            a.CopyTo("Image", b, "Backup");
            byte[] backup = b.Get<byte[]>("Backup");
            backup[0] = 88;
            Check(a.Get<byte[]>("Image")[0] == 1, "Copy并非独立数据");
            b.Set("Busy", 123);
            bool conflict = false;
            try { a.MoveTo("Image", b, "Busy"); }
            catch (InvalidOperationException) { conflict = true; }
            Check(conflict && a.Count == 1, "Move冲突时源数据丢失");
            a.MoveTo("Image", b, "Moved");
            Check(a.Count == 0 && b.Get<byte[]>("Moved")[0] == 1, "Move交接失败");
            Check(b.Search("B").Count == 2, "搜索前缀不正确");
            bool rejected = false;
            try { b.Set("Unsafe", new System.Collections.Generic.List<int> { 1 }); }
            catch (NotSupportedException) { rejected = true; }
            Check(rejected, "未知可变对象被不安全共享");
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

        private static void CleanupFailureStillReleasesOtherOwnedObjects()
        {
            int released = 0;
            MDEExecutionDataScope root = MDEExecutionDataScope.CreateRoot("exceptions");
            IFDEExecutionDataScope child = root.CreateChild("fault");
            child.PutOwned("first", new byte[1], item => Interlocked.Increment(ref released));
            child.PutOwned("second", new byte[2], item =>
                { Interlocked.Increment(ref released); throw new InvalidOperationException("release fail"); });
            bool failed = false;
            try { child.Dispose(); }
            catch (AggregateException) { failed = true; }
            Check(failed && released == 2 && child.IsClosed,
                "cleanup exception must aggregate after all releases");
            root.Dispose();
            Check(root.IsClosed, "child cleanup error leaked parent scope reference");
        }

    }
}
