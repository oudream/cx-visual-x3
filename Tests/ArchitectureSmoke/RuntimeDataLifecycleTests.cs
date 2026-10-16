using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Execution;
using CxVisual.Foundation.Data.Types;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Data.Execution;
using CxVisual.Mechanism.Data.RuntimeData;
using CxVisual.Mechanism.Runtime.Execution;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class RuntimeDataLifecycleTests
    {
        internal static void Run()
        {
            FoundationTypeSafetyContract();
            StackFrameCommitAndAbort();
            HeapStackMoveCopyConflictAndOwnedRejection();
            MultiOutputFanoutAndTypeIsolation();
            MissingOutputMustAbortEverything();
            CancelledStepMustNotPublish();
            RetryMustDiscardFailedAttemptData();
            InlineSubflowOutputIsMapped();
            MultiStepSubflowCommitBarrier();
            ThreeLevelSubflowCommit();
            ExplicitInputCannotInheritParent();
        }

        private static void Check(bool condition, string reason)
        {
            if (!condition) throw new InvalidOperationException("运行时数据M1/M2验收：" + reason);
        }

        private static T Wait<T>(Task<T> task)
        {
            Task winner = Task.WhenAny(task, Task.Delay(10000)).GetAwaiter().GetResult();
            if (!object.ReferenceEquals(winner, task))
                throw new TimeoutException("运行时数据集成测试超时");
            return task.GetAwaiter().GetResult();
        }

        private static void Wait(Task task)
        {
            Task winner = Task.WhenAny(task, Task.Delay(10000)).GetAwaiter().GetResult();
            if (!object.ReferenceEquals(winner, task))
                throw new TimeoutException("运行时数据集成测试超时");
            task.GetAwaiter().GetResult();
        }

        private static IDictionary<string, Type> Outputs(params KeyValuePair<string, Type>[] items)
        {
            Dictionary<string, Type> map = new Dictionary<string, Type>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, Type> item in items) map.Add(item.Key, item.Value);
            return map;
        }


        private static void HeapStackMoveCopyConflictAndOwnedRejection()
        {
            using (MDSDataStack stack = new MDSDataStack())
            {
                IFDEExecutionDataScope scope = stack.RootScope.CreateChild("transfers");
                using (MDSDataFrame frame = stack.OpenFrame("transfers", scope, null,
                    new Dictionary<string, Type>()))
                {
                    frame.Data.Set("Image", new byte[] {4,5,6});
                    frame.Data.CopyToHeap("Image", "Backup");
                    Check(frame.Data.Get<byte[]>("Image")[0] == 4 &&
                        stack.FlowHeap.Get<byte[]>("Backup")[0] == 4,
                        "Stack→Heap Copy错误");
                    frame.Data.MoveToHeap("Image", "SharedImage");
                    byte[] missing;
                    Check(!frame.Data.TryGet<byte[]>("Image", out missing) &&
                        stack.FlowHeap.Get<byte[]>("SharedImage")[0] == 4,
                        "Stack→Heap Move没有移除来源");
                    frame.Data.CopyFromHeap("Backup", "Working");
                    frame.Data.Get<byte[]>("Working")[0] = 88;
                    Check(stack.FlowHeap.Get<byte[]>("Backup")[0] == 4,
                        "Heap→Stack Copy泄漏引用");
                    bool collision = false;
                    try { frame.Data.MoveFromHeap("SharedImage", "Working"); }
                    catch (InvalidOperationException) { collision = true; }
                    Check(collision && stack.FlowHeap.Contains("SharedImage"),
                        "同名冲突Move错误地移走来源");
                    frame.Data.MoveFromHeap("SharedImage", "Working", replace: true);
                    Check(!stack.FlowHeap.Contains("SharedImage") &&
                        frame.Data.Get<byte[]>("Working")[0] == 4,
                        "显式Replace Move没有替换目标");
                    scope.PutOwned("SdkResource", new byte[] {1}, data => { });
                    bool denied = false;
                    try { frame.Data.MoveToHeap("SdkResource"); }
                    catch (InvalidOperationException) { denied = true; }
                    Check(denied && !stack.FlowHeap.Contains("SdkResource"),
                        "带资源释放回调的数据被直接移动");
                }
                Check(stack.ActiveFrames == 0, "调用帧结束后占用没有回到零");
            }
            MDSDataHeap heap = new MDSDataHeap();
            heap.Set("Existing", 1);
            heap.Replace("Existing", 2);
            Check(heap.Get<int>("Existing") == 2, "显式Replace失败");
            heap.Dispose();
            bool disposed = false;
            try { heap.Get<int>("Existing"); }
            catch (ObjectDisposedException) { disposed = true; }
            Check(disposed, "已关闭Heap仍可被读取");
        }

        private static void FoundationTypeSafetyContract()
        {
            FDTDataTypeDescriptor scalar = FDTDataTypeDescriptor.Describe(typeof(decimal));
            FDTDataTypeDescriptor binary = FDTDataTypeDescriptor.Describe(typeof(byte[]));
            FDTDataTypeDescriptor unknown = FDTDataTypeDescriptor.Describe(typeof(List<int>));
            Check(scalar.Kind == FDTDataKind.Scalar && scalar.IsSafelyCopyable,
                "Foundation数值类型描述不正确");
            Check(binary.Kind == FDTDataKind.Buffer && binary.IsSafelyCopyable,
                "Foundation二进制类型描述不正确");
            Check(!unknown.IsSafelyCopyable, "未知可变集合被宣称安全复制");
            byte[] original = new byte[] { 1, 2 };
            byte[] copy = (byte[])FDTDataValueSafety.Capture(original);
            copy[0] = 8;
            Check(original[0] == 1, "基础安全复制仍共享可变数组");
        }

        private static void StackFrameCommitAndAbort()
        {
            using (MDSDataStack stack = new MDSDataStack())
            {
                IFDEExecutionDataScope firstScope = stack.RootScope.CreateChild("first");
                using (MDSDataFrame frame = stack.OpenFrame("first", firstScope,
                    new Dictionary<string, object> {{"A", 4}},
                    Outputs(new KeyValuePair<string, Type>("Result", typeof(byte[])),
                        new KeyValuePair<string, Type>("Score", typeof(int)))))
                {
                    Check(frame.Data.Get<int>("A") == 4, "明确输入没有映射");
                    frame.Data.Set("Temp", 9);
                    Check(frame.Data.Get<int>("Temp") == 9, "Local不可读");
                    bool overwrite = false;
                    try { frame.Data.Set("A", 12); }
                    catch (InvalidOperationException) { overwrite = true; }
                    Check(overwrite, "Input可被覆盖");
                    byte[] original = new byte[] {1,2,3};
                    frame.Data.Return("Result", original);
                    original[0] = 99;
                    byte[] pending;
                    Check(!stack.TryReadOutput("first", "Result", out pending),
                        "PendingOutput提前发布");
                    bool incomplete = false;
                    try { frame.CommitOutputs(); }
                    catch (InvalidOperationException) { incomplete = true; }
                    Check(incomplete && !stack.TryReadOutput("first", "Result", out pending),
                        "缺少输出时错误地部分提交");
                    frame.Data.Return("Score", 3);
                    frame.CommitOutputs();
                }
                byte[] read;
                Check(stack.TryReadOutput("first", "Result", out read) && read[0] == 1,
                    "输出未以安全快照发布");
                read[0] = 55;
                byte[] readAgain;
                Check(stack.TryReadOutput("first", "Result", out readAgain) && readAgain[0] == 1,
                    "读取已提交输出泄漏可变数组");
                IFDEExecutionDataScope second = stack.RootScope.CreateChild("second");
                using (MDSDataFrame bad = stack.OpenFrame("second", second, null,
                    Outputs(new KeyValuePair<string, Type>("Failure", typeof(int)))))
                {
                    bad.Data.Return("Failure", 9);
                    bad.AbortOutputs();
                    bool rejected = false;
                    try { bad.CommitOutputs(); }
                    catch (InvalidOperationException) { rejected = true; }
                    Check(rejected, "已取消Frame仍可提交");
                }
                int forbidden;
                Check(!stack.TryReadOutput("second", "Failure", out forbidden),
                    "取消后的输出可见");
                IFDEExecutionDataScope live = stack.RootScope.CreateChild("active");
                MDSDataFrame active = stack.OpenFrame("active", live, null,
                    new Dictionary<string, Type>());
                bool refused = false;
                try { stack.Dispose(); }
                catch (InvalidOperationException) { refused = true; }
                Check(refused, "活跃Frame期间允许释放Stack");
                active.Dispose();
            }
        }

        private static void MultiOutputFanoutAndTypeIsolation()
        {
            int left = 0, right = 0;
            MRKExecutionPlan plan = new MRKExecutionPlan("fanout", new[]
            {
                new MRKExecutionStep("capture", new string[0], (ctx,ct) =>
                {
                    byte[] image = new byte[] {7, 8, 9};
                    ctx.Data.Return("Image", image);
                    ctx.Data.Return("Score", 42);
                    image[0] = 99;
                    return Task.FromResult((object)1);
                }, dataOutputs: Outputs(
                    new KeyValuePair<string, Type>("Image", typeof(byte[])),
                    new KeyValuePair<string, Type>("Score", typeof(int)))),
                new MRKExecutionStep("ai", new [] {"capture"}, (ctx,ct) =>
                {
                    byte[] img = ctx.Data.Get<byte[]>("InputImage");
                    Check(img[0] == 7, "AI未得到原始图像");
                    img[0] = 33;
                    Interlocked.Exchange(ref left, ctx.Data.Get<int>("Threshold"));
                    return Task.FromResult((object)2);
                }, inputs: new [] {
                    new MRKExecutionInput("InputImage", MRKInputSource.StepOutput,
                        "capture.Image", typeof(byte[])),
                    new MRKExecutionInput("Threshold", MRKInputSource.StepOutput,
                        "capture.Score", typeof(int))
                }, enableDataContext: true),
                new MRKExecutionStep("cv", new [] {"capture"}, (ctx,ct) =>
                {
                    byte[] img = ctx.Data.Get<byte[]>("InputImage");
                    Check(img[0] == 7, "CV输入被AI修改");
                    Interlocked.Exchange(ref right, ctx.Data.Get<int>("Threshold"));
                    return Task.FromResult((object)3);
                }, inputs: new [] {
                    new MRKExecutionInput("InputImage", MRKInputSource.StepOutput,
                        "capture.Image", typeof(byte[])),
                    new MRKExecutionInput("Threshold", MRKInputSource.StepOutput,
                        "capture.Score", typeof(int))
                }, enableDataContext: true)
            });
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(3, 5));
            try
            {
                MRKFlowResult result = Wait(engine.StartFlow(plan).Completion);
                Check(result.State == MRKFlowState.Completed && left == 42 && right == 42,
                    "并行只读扇出没有完成");
            }
            finally { Wait(engine.StopAsync()); }
        }

        private static void MissingOutputMustAbortEverything()
        {
            int downstream = 0;
            MRKExecutionPlan plan = new MRKExecutionPlan("failure", new[]
            {
                new MRKExecutionStep("fail", new string[0], (ctx,ct) =>
                {
                    ctx.Data.Return("One", 1);
                    return Task.FromResult((object)1);
                }, dataOutputs: Outputs(
                    new KeyValuePair<string, Type>("One", typeof(int)),
                    new KeyValuePair<string, Type>("Two", typeof(int)))),
                new MRKExecutionStep("after", new [] {"fail"}, (ctx,ct) =>
                {
                    Interlocked.Increment(ref downstream);
                    return Task.FromResult((object)1);
                }, inputs: new [] {
                    new MRKExecutionInput("One", MRKInputSource.StepOutput,
                        "fail.One", typeof(int))
                })
            });
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(2, 4));
            try
            {
                MRKFlowResult failed = Wait(engine.StartFlow(plan).Completion);
                Check(failed.State == MRKFlowState.Failed && downstream == 0,
                    "缺少输出却运行了下游节点");
            }
            finally { Wait(engine.StopAsync()); }
        }

        private static void CancelledStepMustNotPublish()
        {
            TaskCompletionSource<bool> entered = new TaskCompletionSource<bool>();
            TaskCompletionSource<bool> finish = new TaskCompletionSource<bool>();
            int downstream = 0;
            MRKExecutionPlan plan = new MRKExecutionPlan("cancel", new[]
            {
                new MRKExecutionStep("slow", new string[0], async (ctx,ct) =>
                {
                    ctx.Data.Return("Partial", 123);
                    entered.TrySetResult(true);
                    await finish.Task.ConfigureAwait(false);
                    return (object)1;
                }, dataOutputs: Outputs(
                    new KeyValuePair<string, Type>("Partial", typeof(int)))),
                new MRKExecutionStep("after", new [] {"slow"}, (ctx,ct) =>
                {
                    Interlocked.Increment(ref downstream);
                    return Task.FromResult((object)1);
                }, inputs: new [] {
                    new MRKExecutionInput("Partial", MRKInputSource.StepOutput,
                        "slow.Partial", typeof(int))
                })
            });
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(2, 4));
            try
            {
                MRKFlowHandle handle = engine.StartFlow(plan);
                Wait(entered.Task);
                Check(handle.RequestCancel(), "无法请求取消");
                finish.TrySetResult(true);
                MRKFlowResult result = Wait(handle.Completion);
                Check(result.State == MRKFlowState.Cancelled && downstream == 0,
                    "取消后仍发布了已暂存输出");
            }
            finally { finish.TrySetResult(true); Wait(engine.StopAsync()); }
        }

        private static void RetryMustDiscardFailedAttemptData()
        {
            int attempts = 0;
            MRKExecutionPlan plan = new MRKExecutionPlan("retry", new[]
            {
                new MRKExecutionStep("work", new string[0], (ctx,ct) =>
                {
                    int attempt = Interlocked.Increment(ref attempts);
                    ctx.Data.Set("Local", attempt);
                    ctx.Data.Return("Result", attempt);
                    if (attempt == 1) throw new InvalidOperationException("人工注入短暂故障");
                    return Task.FromResult((object)ctx.Data.Get<int>("Local"));
                }, policy: new MRKExecutionPolicy(maxAttempts: 2, idempotent: true),
                   output: new MRKExecutionOutput(typeof(int)),
                   dataOutputs: Outputs(
                       new KeyValuePair<string, Type>("Result", typeof(int)))),
                new MRKExecutionStep("read", new [] {"work"}, (ctx,ct) =>
                {
                    return Task.FromResult((object)ctx.Data.Get<int>("Mapped"));
                }, inputs: new [] {
                    new MRKExecutionInput("Mapped", MRKInputSource.StepOutput,
                        "work.Result", typeof(int))
                }, output: new MRKExecutionOutput(typeof(int)))
            });
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(2, 4));
            try
            {
                MRKFlowResult result = Wait(engine.StartFlow(plan).Completion);
                Check(result.State == MRKFlowState.Completed && attempts == 2 &&
                    (int)result.Results["read"] == 2,
                    "失败尝试的局部数据或输出污染了成功重试");
            }
            finally { Wait(engine.StopAsync()); }
        }

        private static void InlineSubflowOutputIsMapped()
        {
            MRKExecutionPlan child = new MRKExecutionPlan("child", new[]
            {
                new MRKExecutionStep("make", new string[0], (ctx,ct) =>
                {
                    ctx.Data.Return("Value", 86);
                    return Task.FromResult((object)1);
                }, dataOutputs: Outputs(
                    new KeyValuePair<string, Type>("Value", typeof(int))))
            });
            List<MRKExecutionStep> steps = new List<MRKExecutionStep>(
                MRKExecutionPlan.InlineSubflow("nested", child, null));
            steps.Add(new MRKExecutionStep("consume", new [] {"nested.make"}, (ctx,ct) =>
            {
                return Task.FromResult((object)(ctx.Data.Get<int>("Input") + 1));
            }, inputs: new [] {
                new MRKExecutionInput("Input", MRKInputSource.StepOutput,
                    "nested.make.Value", typeof(int))
            }, output: new MRKExecutionOutput(typeof(int)), enableDataContext: true));
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(2, 4));
            try
            {
                MRKFlowResult result = Wait(engine.StartFlow(new MRKExecutionPlan("parent", steps)).Completion);
                Check(result.State == MRKFlowState.Completed &&
                    (int)result.Results["consume"] == 87, "内联子流程输出映射失效");
            }
            finally { Wait(engine.StopAsync()); }
        }

        private static void MultiStepSubflowCommitBarrier()
        {
            MRKExecutionStep produce = new MRKExecutionStep("produce", new string[0],
                (ctx,ct) =>
                {
                    ctx.Data.Return("Area", 10);
                    return Task.FromResult((object)1);
                }, dataOutputs: Outputs(
                    new KeyValuePair<string, Type>("Area", typeof(int))));
            MRKExecutionStep fail = new MRKExecutionStep("fail", new[] {"produce"},
                (ctx,ct) => { throw new InvalidOperationException("子流程第二节点失败"); });
            MRKExecutionPlan child = new MRKExecutionPlan("sub", new[] {produce, fail},
                new Dictionary<string, string> {{"Area", "produce.Area"}});
            List<MRKExecutionStep> steps = new List<MRKExecutionStep>(
                MRKExecutionPlan.InlineSubflow("sub", child, null));
            int parentRead = 0;
            steps.Add(new MRKExecutionStep("parentRead", new[] {"sub.__commit"},
                (ctx,ct) =>
                {
                    Interlocked.Increment(ref parentRead);
                    return Task.FromResult((object)ctx.Data.Get<int>("Area"));
                }, inputs: new[] {
                    new MRKExecutionInput("Area", MRKInputSource.StepOutput,
                        "sub.__commit.Area", typeof(int))
                }));
            MRKExecutionPlan parent = new MRKExecutionPlan("parent", steps);
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(3, 5));
            try
            {
                MRKFlowResult result = Wait(engine.StartFlow(parent).Completion);
                Check(result.State == MRKFlowState.Failed && parentRead == 0,
                    "子流程失败却发布了部分已暂存的导出");
            }
            finally { Wait(engine.StopAsync()); }
            bool boundaryDenied = false;
            try
            {
                List<MRKExecutionStep> invalid = new List<MRKExecutionStep>(
                    MRKExecutionPlan.InlineSubflow("bad", child, null));
                invalid.Add(new MRKExecutionStep("escape", new[] {"bad.produce"},
                    (ctx,ct) => Task.FromResult((object)1),
                    inputs: new [] {
                        new MRKExecutionInput("Area", MRKInputSource.StepOutput,
                            "bad.produce.Area", typeof(int))
                    }));
                new MRKExecutionPlan("invalid", invalid);
            }
            catch (ArgumentException) { boundaryDenied = true; }
            Check(boundaryDenied, "外部绕过子流程屏障直接读取内部输出未被拒绝");
        }

        private static void ThreeLevelSubflowCommit()
        {
            MRKExecutionPlan leaf = new MRKExecutionPlan("leaf", new[] {
                new MRKExecutionStep("calculate", new string[0], (ctx,ct) =>
                {
                    ctx.Data.Return("Value", 71);
                    return Task.FromResult((object)1);
                }, dataOutputs: Outputs(
                    new KeyValuePair<string, Type>("Value", typeof(int))))
            }, new Dictionary<string, string> {{"Value", "calculate.Value"}});
            MRKExecutionPlan middle = new MRKExecutionPlan("middle",
                MRKExecutionPlan.InlineSubflow("leaf", leaf, null),
                new Dictionary<string, string> {{"Result", "leaf.__commit.Value"}});
            List<MRKExecutionStep> steps = new List<MRKExecutionStep>(
                MRKExecutionPlan.InlineSubflow("middle", middle, null));
            steps.Add(new MRKExecutionStep("final", new [] {"middle.__commit"},
                (ctx,ct) => Task.FromResult((object)(ctx.Data.Get<int>("Value") + 1)),
                inputs: new [] {
                    new MRKExecutionInput("Value", MRKInputSource.StepOutput,
                        "middle.__commit.Result", typeof(int))
                }, output: new MRKExecutionOutput(typeof(int))));
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(3, 5));
            try
            {
                MRKFlowResult result = Wait(engine.StartFlow(new MRKExecutionPlan("root", steps)).Completion);
                Check(result.State == MRKFlowState.Completed &&
                    (int)result.Results["final"] == 72,
                    "三层子流程输出没有逐层提交");
            }
            finally { Wait(engine.StopAsync()); }
        }

        private static void ExplicitInputCannotInheritParent()
        {
            MDEExecutionDataScope root = MDEExecutionDataScope.CreateRoot("original");
            root.Put("Secret", 123);
            MRKExecutionPlan plan = new MRKExecutionPlan("strict", new[]
            {
                new MRKExecutionStep("node", new string[0], (ctx,ct) =>
                {
                    int hidden;
                    Check(!ctx.Data.TryGet<int>("Secret", out hidden),
                        "父级Local未经映射暴露给新DataContext");
                    Check(ctx.DataScope.TryRead<int>("Secret", out hidden) && hidden == 123,
                        "旧DataScope兼容读取行为被破坏");
                    ctx.Data.Set("Local", 9);
                    return Task.FromResult((object)ctx.Data.Get<int>("Local"));
                }, output: new MRKExecutionOutput(typeof(int)), enableDataContext: true)
            });
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(1, 3));
            try
            {
                MRKFlowResult result = Wait(engine.StartFlowWithData(plan, root).Completion);
                Check(result.State == MRKFlowState.Completed && (int)result.Results["node"] == 9,
                    "新旧作用域兼容不正确");
            }
            finally { root.Dispose(); Wait(engine.StopAsync()); }
        }
    }
}
