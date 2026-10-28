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
            InactiveScopeMustNotReleaseDataStack();
            PendingFrameWithChildMustRemainTracked();
            SubflowExportsCannotBypassCommitBarrier();
            UndeclaredSubflowDataStaysPrivate();
            HeapNullTypeCannotBeChanged();
            StrictLocalNullAndLegacyMoveOwnership();
            FlowOutputsAndInputsHaveNoMutableAlias();
            ReadOnlyBufferCanShareWithoutCopying();
            ParallelHeapMovesDoNotDeadlockOrLoseData();
            StackFrameCommitAndAbort();
            HeapStackMoveCopyConflictAndOwnedRejection();
            MultiOutputFanoutAndTypeIsolation();
            ConcurrentFlowsNeverShareDataStack();
            MissingOutputMustAbortEverything();
            CancelledStepMustNotPublish();
            RetryMustDiscardFailedAttemptData();
            InlineSubflowOutputIsMapped();
            MultiStepSubflowCommitBarrier();
            FlowResultExposesOnlyCommittedExports();
            ThreeLevelSubflowCommit();
            ExplicitInputCannotInheritParent();
            RuntimeDataLifecyclePolicyTests.Run();
            CommittedFrameReleaseAccountsBudget();
            StaticDagReclaimsAfterAllConsumers();
            DynamicRegionsMustRejectPrematureReclamation();
            SessionResultsHaveBoundedRetentionAndExplicitHandoff();
        }

        private static void CommittedFrameReleaseAccountsBudget()
        {
            var budget = new MDSDataBudget(100);
            using (var stack = new MDSDataStack(null, null, null, budget))
            {
                using (var frame = stack.OpenFrame("step.1",
                    stack.RootScope.CreateChild("step.1"),
                    new Dictionary<string, object>(),
                    new Dictionary<string, Type> { { "Image", typeof(byte[]) } }))
                {
                    frame.Data.Return("Image", new byte[64]);
                    frame.CommitOutputs();
                    bool busy = false;
                    try { stack.ReleaseCommittedOutputs("step.1"); }
                    catch (MDSDataException) { busy = true; }
                    Check(busy && stack.CommittedFrameCount == 1,
                        "未关闭的Frame不能提前释放已提交输出");
                }
                Check(budget.RetainedBytes == 64 && stack.CommittedFrameCount == 1,
                    "Frame关闭后保留已提交输出账目");
                Check(stack.ReleaseCommittedOutputs("step.1") &&
                    stack.CommittedFrameCount == 0 && budget.RetainedBytes == 0,
                    "完成消费者后的逐帧释放必须返还预算");
                Check(!stack.ReleaseCommittedOutputs("step.1"),
                    "重复释放不应造成预算重复扣账");
                byte[] forgotten;
                Check(!stack.TryReadOutput("step.1", "Image", out forgotten),
                    "释放后应拒绝读取旧输出");
            }
        }

        private static void StaticDagReclaimsAfterAllConsumers()
        {
            MRKExecutionStep produce = new MRKExecutionStep("image", new string[0],
                (ctx, ct) =>
                {
                    ctx.Data.Return("Image", new byte[70]);
                    return Task.FromResult((object)true);
                }, dataOutputs: new Dictionary<string, Type> { { "Image", typeof(byte[]) } });
            Func<string, MRKExecutionStep> consumer = id => new MRKExecutionStep(
                id, new[] { "image" }, async (ctx, ct) =>
                {
                    byte[] image = ctx.Data.Get<byte[]>("Input");
                    await Task.Delay(30, ct).ConfigureAwait(false);
                    Check(image.Length == 70, "并行消费时输入仍然存活");
                    ctx.Data.Return("Vote", 1);
                    return true;
                },
                inputs: new[] { new MRKExecutionInput("Input",
                    MRKInputSource.StepOutput, "image.Image", typeof(byte[])) },
                dataOutputs: new Dictionary<string, Type> { { "Vote", typeof(int) } });
            MRKExecutionStep combine = new MRKExecutionStep("combine",
                new[] { "a", "b" }, (ctx, ct) =>
                {
                    Check(ctx.Data.Get<int>("Left") + ctx.Data.Get<int>("Right") == 2,
                        "双分支结果要正确归并");
                    ctx.Data.Return("Final", new byte[70]);
                    return Task.FromResult((object)true);
                },
                inputs: new[] {
                    new MRKExecutionInput("Left", MRKInputSource.StepOutput, "a.Vote", typeof(int)),
                    new MRKExecutionInput("Right", MRKInputSource.StepOutput, "b.Vote", typeof(int)) },
                dataOutputs: new Dictionary<string, Type> { { "Final", typeof(byte[]) } });
            var steps = new[] { produce, consumer("a"), consumer("b"), combine };
            var exports = new Dictionary<string, string> { { "Result", "combine.Final" } };
            var optimized = new MRKExecutionPlan("bounded-static-fanout", steps, exports,
                reclaimCommittedWhenConsumersSettle: true);
            var legacy = new MRKExecutionPlan("conservative-static-fanout", steps, exports);
            var engine = new MRKExecutionEngine(new FCRWorkerOptions(4, 16),
                dataPayloadBudgetBytes: 150);
            try
            {
                MRKFlowResult ok = Wait(engine.StartFlow(optimized).Completion);
                Check(ok.State == MRKFlowState.Completed &&
                    ok.GetCommittedOutput<byte[]>("Result").Length == 70,
                    "显式opt-in后可在150字节预算内完成双消费者与结果交接；State="
                    + ok.State + " Error=" + (ok.Error == null ? "<null>" : ok.Error.ToString()));
                MRKFlowResult old = Wait(engine.StartFlow(legacy).Completion);
                Check(old.State == MRKFlowState.Failed &&
                    old.Error != null,
                    "默认仍保守保留整Flow，低预算应明确失败，不能暗中更改旧行为");
            }
            finally { Wait(engine.StopAsync()); }
        }

        private static void DynamicRegionsMustRejectPrematureReclamation()
        {
            var body = new MRKExecutionPlan("dynamic-body", new[] {
                new MRKExecutionStep("write", new string[0], (ctx, ct) =>
                {
                    ctx.Data.Return("Value", 1);
                    return Task.FromResult((object)true);
                }, dataOutputs: new Dictionary<string, Type> { { "Value", typeof(int) } })
            });
            var looping = new MRKExecutionStep("loop", new string[0],
                (ctx, ct) => Task.FromResult((object)true),
                loopRegion: new MRKLoopRegion(body, 2, (ctx, i) => i < 2));
            bool rejected = false;
            try
            {
                new MRKExecutionPlan("must-not-reclaim-loop",
                    new[] { looping },
                    reclaimCommittedWhenConsumersSettle: true);
            }
            catch (ArgumentException) { rejected = true; }
            Check(rejected,
                "动态Loop的CollectLoopOutputs仍可能引用历史Invocation，必须拒绝提前自动回收");
        }

        private static void SessionResultsHaveBoundedRetentionAndExplicitHandoff()
        {
            var plan = new MRKExecutionPlan("session.retention", new[] {
                new MRKExecutionStep("emit", new string[0], (ctx, ct) =>
                {
                    ctx.Data.Return("Image", new byte[48]);
                    return Task.FromResult((object)true);
                }, dataOutputs: new Dictionary<string, Type> { { "Image", typeof(byte[]) } })
            }, new Dictionary<string, string> { { "Image", "emit.Image" } });
            var engine = new MRKExecutionEngine(new FCRWorkerOptions(2, 12));
            var options = new MRKSessionResultRetentionOptions(1, 64, TimeSpan.FromSeconds(3));
            var session = new MRKRuntimeSession("session.with.results", engine,
                new MRKExecutionPlanSlot(plan, 1), maxInFlight: 2,
                resultRetention: options);
            session.Start();
            try
            {
                MRKFlowHandle first = session.TryStartFlow();
                MRKFlowResult a = Wait(first.Completion);
                Check(a.State == MRKFlowState.Completed &&
                    SpinWait.SpinUntil(() => session.Snapshot().RetainedResults == 1, 3000),
                    "已成功提交的FlowResult应在Session里受控保留");
                Check(session.Snapshot().RetainedPayloadBytes == 48,
                    "Session保留账目按真实逻辑载荷记录");
                MRKFlowResult second = Wait(session.TryStartFlow().Completion);
                Check(second.State == MRKFlowState.Completed &&
                    SpinWait.SpinUntil(() => session.Snapshot().RetentionRejected == 1, 3000),
                    "Session上限不能悄悄扩大，应记录拒绝保留次数");
                MRKFlowResult handed;
                Check(session.TryTakeCompletedResult(first.FlowId, out handed) &&
                    object.ReferenceEquals(a, handed) &&
                    session.Snapshot().RetainedPayloadBytes == 0,
                    "领取将Session唯一保留引用转交调用方，不再占Session缓存配额");
                byte[] copy = handed.GetCommittedOutput<byte[]>("Image");
                copy[0] = 9;
                Check(handed.GetCommittedOutput<byte[]>("Image")[0] == 0,
                    "领取结果不能暴露原MDS已提交可写数据");
                Check(!session.TryTakeCompletedResult(first.FlowId, out handed) &&
                    !session.ReleaseRetainedResult(first.FlowId),
                    "重复领取和重复释放不能造成双重所有权");
                MRKFlowHandle next = session.TryStartFlow();
                Wait(next.Completion);
                Check(SpinWait.SpinUntil(() => session.Snapshot().RetainedResults == 1, 3000),
                    "取走结果后新的FlowResult可以重新占用预算");
                Wait(session.StopAsync());
                Check(session.Snapshot().RetainedResults == 0 &&
                    session.Snapshot().RetainedPayloadBytes == 0 &&
                    session.Snapshot().State == MRKRuntimeSessionState.Stopped,
                    "Session停止会清理仍由会话保留的快照");
                Check(a.GetCommittedOutput<byte[]>("Image").Length == 48,
                    "原Completion的独立快照与Session停止无关");
            }
            finally
            {
                Wait(session.StopAsync(true));
                Wait(engine.StopAsync(true));
            }
            var ttlEngine = new MRKExecutionEngine(new FCRWorkerOptions(1, 5));
            var ttl = new MRKRuntimeSession("short.retention", ttlEngine,
                new MRKExecutionPlanSlot(plan, 2), resultRetention:
                new MRKSessionResultRetentionOptions(2, 128, TimeSpan.FromSeconds(1)));
            ttl.Start();
            try
            {
                MRKFlowHandle shortFlow = ttl.TryStartFlow();
                Check(Wait(shortFlow.Completion).State == MRKFlowState.Completed &&
                    SpinWait.SpinUntil(() => ttl.Snapshot().RetainedResults == 1, 3000),
                    "TTL测试必须先完成真实Session保留");
                Thread.Sleep(1200);
                Check(ttl.SweepExpiredResults() == 1 &&
                    ttl.Snapshot().RetainedResults == 0,
                    "结果TTL到期应自动清除会话保留引用与计费");
                MRKFlowResult oldResult;
                Check(!ttl.TryTakeCompletedResult(shortFlow.FlowId, out oldResult),
                    "过期会话结果不能再次领取");
            }
            finally
            {
                Wait(ttl.StopAsync(true));
                Wait(ttlEngine.StopAsync(true));
            }
            bool invalid = false;
            try { new MRKSessionResultRetentionOptions(0, 1, TimeSpan.FromSeconds(1)); }
            catch (ArgumentOutOfRangeException) { invalid = true; }
            Check(invalid, "无界或非法结果保留策略应拒绝");
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



        private static void InactiveScopeMustNotReleaseDataStack()
        {
            MDSDataStack stack = new MDSDataStack();
            stack.FlowHeap.Set("Keep", 99);
            IFDEExecutionDataScope legacyChild = stack.RootScope.CreateChild("legacy");
            bool rejected = false;
            try { stack.Dispose(); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected && stack.FlowHeap.Get<int>("Keep") == 99,
                "旧式Scope子节点未结束却已清空FlowHeap");
            legacyChild.Dispose();
            stack.Dispose();
        }

        private static void PendingFrameWithChildMustRemainTracked()
        {
            using (MDSDataStack stack = new MDSDataStack())
            {
                IFDEExecutionDataScope scope = stack.RootScope.CreateChild("parent");
                MDSDataFrame frame = stack.OpenFrame("parent", scope,
                    new Dictionary<string, object>(),
                    new Dictionary<string, Type>());
                IFDEExecutionDataScope child = scope.CreateChild("live");
                bool rejected = false;
                try { frame.Dispose(); }
                catch (InvalidOperationException) { rejected = true; }
                Check(rejected && stack.ActiveFrames == 1,
                    "帧关闭被拒绝后仍提前解除ActiveFrame记录");
                bool blocked = false;
                try { stack.Dispose(); }
                catch (InvalidOperationException) { blocked = true; }
                Check(blocked, "DataStack允许删除仍活动的Frame");
                child.Dispose();
                frame.Dispose();
                Check(stack.ActiveFrames == 0, "子Frame真实完成后未释放占用");
            }
        }

        private static void UndeclaredSubflowDataStaysPrivate()
        {
            MRKExecutionPlan privateChild = new MRKExecutionPlan("private", new[]
            {
                new MRKExecutionStep("source", new string[0], (ctx, ct) =>
                {
                    ctx.Data.Return("Secret", 7);
                    return Task.FromResult((object)1);
                }, dataOutputs: Outputs(
                    new KeyValuePair<string, Type>("Secret", typeof(int))))
            });
            var steps = new List<MRKExecutionStep>(
                MRKExecutionPlan.InlineSubflow("private", privateChild, null));
            steps.Add(new MRKExecutionStep("unauthorized", new[] {"private.source"},
                (ctx, ct) => Task.FromResult((object)1),
                inputs: new [] {new MRKExecutionInput("Leaked", MRKInputSource.StepOutput,
                    "private.source.Secret", typeof(int))}));
            bool denied = false;
            try { new MRKExecutionPlan("root", steps); }
            catch (ArgumentException) { denied = true; }
            Check(denied, "没有声明DataExports的子流程泄漏了内部具名MDS数据");
        }

        private static void SubflowExportsCannotBypassCommitBarrier()
        {
            MRKExecutionPlan child = new MRKExecutionPlan("child", new[]
            {
                new MRKExecutionStep("produce", new string[0], (ctx, ct) =>
                {
                    ctx.Data.Return("Value", 17);
                    return Task.FromResult((object)1);
                }, dataOutputs: Outputs(
                    new KeyValuePair<string, Type>("Value", typeof(int))))
            }, new Dictionary<string, string> {{"Value", "produce.Value"}});
            IList<MRKExecutionStep> flattened =
                MRKExecutionPlan.InlineSubflow("nested", child, null);
            bool rejected = false;
            try
            {
                new MRKExecutionPlan("invalid-export", flattened,
                    new Dictionary<string, string>{{"Leaked", "nested.produce.Value"}});
            }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, "父流程可绕过子流程屏障直接声明导出内部结果");
            MRKExecutionPlan valid = new MRKExecutionPlan("valid-export", flattened,
                new Dictionary<string, string>{{"Value", "nested.__commit.Value"}});
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(2, 4));
            try
            {
                MRKFlowResult result = Wait(engine.StartFlow(valid).Completion);
                Check(result.State == MRKFlowState.Completed &&
                    result.GetCommittedOutput<int>("Value") == 17,
                    "经过屏障的合法导出不能运行");
            }
            finally { Wait(engine.StopAsync()); }
        }

        private static void HeapNullTypeCannotBeChanged()
        {
            MDSDataHeap heap = new MDSDataHeap();
            heap.Set<byte[]>("NullBuffer", null);
            bool rejected = false;
            try { heap.Get<string>("NullBuffer"); }
            catch (InvalidCastException) { rejected = true; }
            Check(rejected && heap.Get<byte[]>("NullBuffer") == null,
                "Null值读取绕过了存储的静态类型契约");
            heap.Dispose();
            Check(!FDTDataValueSafety.IsSimpleImmutable(typeof(IntPtr)) &&
                !FDTDataValueSafety.IsSimpleImmutable(typeof(UIntPtr)),
                "非托管指针被当作安全可共享值");
        }

        private static void FlowOutputsAndInputsHaveNoMutableAlias()
        {
            byte[] caller = new byte[] {4, 8};
            TaskCompletionSource<bool> gate = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> entered = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            MRKExecutionPlan plan = new MRKExecutionPlan("input-output-alias", new[]
            {
                new MRKExecutionStep("gate", new string[0], async (ctx, ct) =>
                {
                    entered.TrySetResult(true);
                    await gate.Task.ConfigureAwait(false);
                    return (object)true;
                }),
                new MRKExecutionStep("consume", new [] {"gate"}, (ctx, ct) =>
                {
                    byte[] value = ctx.Data.Get<byte[]>("Image");
                    Check(value[0] == 4, "StartFlow的外部数组修改污染了正在运行的Flow");
                    ctx.Data.Return("Output", value);
                    return Task.FromResult((object)1);
                }, inputs: new [] {
                    new MRKExecutionInput("Image", MRKInputSource.FlowInput,
                        "InputImage", typeof(byte[]))
                }, dataOutputs: Outputs(
                    new KeyValuePair<string, Type>("Output", typeof(byte[]))))
            }, new Dictionary<string, string> {{"Image", "consume.Output"}});
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(2, 4));
            try
            {
                MRKFlowHandle handle = engine.StartFlow(plan,
                    new Dictionary<string, object>{{"InputImage", caller}});
                Wait(entered.Task);
                caller[0] = 99;
                gate.TrySetResult(true);
                MRKFlowResult result = Wait(handle.Completion);
                Check(result.State == MRKFlowState.Completed, "Flow输入快照保护未完成执行");
                byte[] exposed = (byte[])result.CommittedOutputs["Image"];
                exposed[0] = 88;
                Check(result.GetCommittedOutput<byte[]>("Image")[0] == 4,
                    "CommittedOutputs的ReadOnlyDictionary仍泄露可写内容");
            }
            finally { gate.TrySetResult(true); Wait(engine.StopAsync()); }
        }


        private static void ParallelHeapMovesDoNotDeadlockOrLoseData()
        {
            MDSDataHeap a = new MDSDataHeap();
            MDSDataHeap b = new MDSDataHeap();
            const int count = 150;
            for (int i = 0; i < count; i++)
            {
                a.Set("A." + i, i);
                b.Set("B." + i, i);
            }
            Task ab = Task.Run(() =>
            {
                for (int i = 0; i < count; i++)
                    a.MoveTo("A." + i, b, "MovedA." + i);
            });
            Task ba = Task.Run(() =>
            {
                for (int i = 0; i < count; i++)
                    b.MoveTo("B." + i, a, "MovedB." + i);
            });
            Wait(Task.WhenAll(ab, ba));
            Check(a.Count == count && b.Count == count,
                "逆方向并行Move造成数据丢失或重复");
            for (int i = 0; i < count; i++)
            {
                Check(a.Get<int>("MovedB." + i) == i &&
                    b.Get<int>("MovedA." + i) == i,
                    "逆方向并行Move发生来源或目标数据串值");
            }
            a.Dispose(); b.Dispose();
        }

        private static void ReadOnlyBufferCanShareWithoutCopying()
        {
            byte[] source = new byte[] {11, 22, 33};
            FDTReadOnlyBuffer immutable = new FDTReadOnlyBuffer(source);
            source[0] = 99;
            byte[] independent = immutable.ToArray();
            independent[1] = 66;
            Check(immutable[0] == 11 && immutable[1] == 22 &&
                  FDTDataTypeDescriptor.Describe(typeof(FDTReadOnlyBuffer)).IsSafelyCopyable,
                "只读Buffer未隔离写入或类型能力错误");

            object a = null, b = null;
            MRKExecutionPlan plan = new MRKExecutionPlan("readonly", new[]
            {
                new MRKExecutionStep("producer", new string[0], (ctx, ct) =>
                {
                    ctx.Data.Return("Image", immutable);
                    return Task.FromResult((object)1);
                }, dataOutputs: Outputs(
                    new KeyValuePair<string, Type>("Image", typeof(FDTReadOnlyBuffer)))),
                new MRKExecutionStep("a", new[] {"producer"}, (ctx, ct) =>
                {
                    a = ctx.Data.Get<FDTReadOnlyBuffer>("Input");
                    return Task.FromResult((object)1);
                }, inputs: new[] {new MRKExecutionInput("Input", MRKInputSource.StepOutput,
                    "producer.Image", typeof(FDTReadOnlyBuffer))}),
                new MRKExecutionStep("b", new[] {"producer"}, (ctx, ct) =>
                {
                    b = ctx.Data.Get<FDTReadOnlyBuffer>("Input");
                    return Task.FromResult((object)1);
                }, inputs: new[] {new MRKExecutionInput("Input", MRKInputSource.StepOutput,
                    "producer.Image", typeof(FDTReadOnlyBuffer))})
            });
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(3, 5));
            try
            {
                MRKFlowResult result = Wait(engine.StartFlow(plan).Completion);
                Check(result.State == MRKFlowState.Completed &&
                      object.ReferenceEquals(a, b) && object.ReferenceEquals(a, immutable),
                    "已安全冻结的Buffer不应对每个消费者复制主体");
            }
            finally { Wait(engine.StopAsync()); }
        }


        private static void StrictLocalNullAndLegacyMoveOwnership()
        {
            using (MDSDataStack stack = new MDSDataStack())
            {
                IFDEExecutionDataScope scope = stack.RootScope.CreateChild("null-type");
                using (MDSDataFrame frame = stack.OpenFrame("null-type", scope, null,
                    new Dictionary<string, Type>()))
                {
                    frame.Data.Set<byte[]>("OptionalBuffer", null);
                    bool rejected = false;
                    try { frame.Data.Get<string>("OptionalBuffer"); }
                    catch (InvalidCastException) { rejected = true; }
                    Check(rejected && frame.Data.Get<byte[]>("OptionalBuffer") == null,
                        "Scope中typed null被转换成不兼容类型");
                    frame.Data.MoveToHeap("OptionalBuffer", "OnHeap");
                    bool rejectHeap = false;
                    try { stack.FlowHeap.Get<string>("OnHeap"); }
                    catch (InvalidCastException) { rejectHeap = true; }
                    Check(rejectHeap && stack.FlowHeap.Get<byte[]>("OnHeap") == null,
                        "堆栈Move丢失空值声明类型");
                }
            }
            using (MDSDataStack stack = new MDSDataStack())
            {
                IFDEExecutionDataScope scope = stack.RootScope.CreateChild("legacy");
                byte[] external = new byte[] {4,5,6};
                scope.Put("Legacy", external);
                using (MDSDataFrame frame = stack.OpenFrame("legacy", scope, null,
                    new Dictionary<string, Type>()))
                {
                    frame.Data.MoveToHeap("Legacy", "Imported");
                    external[0] = 88;
                    Check(stack.FlowHeap.Get<byte[]>("Imported")[0] == 4,
                        "旧Scope裸数组移动未建立安全所有权");
                }
            }
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
                    catch (MDSDataException ex)
                    { incomplete = ex.Code == MDSDataErrorCode.OutputMissing; }
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

        private static void ConcurrentFlowsNeverShareDataStack()
        {
            MRKExecutionPlan plan = new MRKExecutionPlan("parallel-runs", new[]
            {
                new MRKExecutionStep("step", new string[0], (ctx,ct) =>
                {
                    int number = ctx.Data.Get<int>("Input");
                    ctx.Data.Set("Local", number);
                    ctx.Data.Return("Output", ctx.Data.Get<int>("Local"));
                    return Task.FromResult((object)number);
                }, inputs: new [] {
                    new MRKExecutionInput("Input", MRKInputSource.FlowInput,
                        "External", typeof(int))
                }, output: new MRKExecutionOutput(typeof(int)),
                dataOutputs: Outputs(new KeyValuePair<string, Type>("Output", typeof(int))))
            }, new Dictionary<string, string> {{"Answer", "step.Output"}});
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(3, 5));
            try
            {
                MRKFlowHandle first = engine.StartFlow(plan,
                    new Dictionary<string, object> {{"External", 101}});
                MRKFlowHandle second = engine.StartFlow(plan,
                    new Dictionary<string, object> {{"External", 202}});
                MRKFlowResult a = Wait(first.Completion);
                MRKFlowResult b = Wait(second.Completion);
                Check(a.FlowId != b.FlowId && a.State == MRKFlowState.Completed &&
                    b.State == MRKFlowState.Completed &&
                    a.GetCommittedOutput<int>("Answer") == 101 &&
                    b.GetCommittedOutput<int>("Answer") == 202,
                    "并发运行实例之间发生数据串值");
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
            }, new Dictionary<string, string> {{"Value", "make.Value"}});
            List<MRKExecutionStep> steps = new List<MRKExecutionStep>(
                MRKExecutionPlan.InlineSubflow("nested", child, null));
            steps.Add(new MRKExecutionStep("consume", new [] {"nested.__commit"}, (ctx,ct) =>
            {
                return Task.FromResult((object)(ctx.Data.Get<int>("Input") + 1));
            }, inputs: new [] {
                new MRKExecutionInput("Input", MRKInputSource.StepOutput,
                    "nested.__commit.Value", typeof(int))
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

        private static void FlowResultExposesOnlyCommittedExports()
        {
            MRKExecutionPlan plan = new MRKExecutionPlan("export-success", new[]
            {
                new MRKExecutionStep("capture", new string[0], (ctx,ct) =>
                {
                    ctx.Data.Return("Image", new byte[] {3,4,5});
                    ctx.Data.Return("Area", 12);
                    return Task.FromResult((object)1);
                }, dataOutputs: Outputs(
                    new KeyValuePair<string, Type>("Image", typeof(byte[])),
                    new KeyValuePair<string, Type>("Area", typeof(int))))
            }, new Dictionary<string, string> {
                {"Image", "capture.Image"}, {"Area", "capture.Area"}
            });
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(1, 3));
            try
            {
                MRKFlowResult ok = Wait(engine.StartFlow(plan).Completion);
                Check(ok.State == MRKFlowState.Completed &&
                    ok.GetCommittedOutput<int>("Area") == 12 &&
                    ok.GetCommittedOutput<byte[]>("Image")[0] == 3,
                    "Flow成功后无法通过已提交导出取值");
                byte[] first = ok.GetCommittedOutput<byte[]>("Image");
                first[0] = 99;
                Check(ok.GetCommittedOutput<byte[]>("Image")[0] == 3,
                    "FlowResult导出暴露可写内部数组");
            }
            finally { Wait(engine.StopAsync()); }
            MRKExecutionPlan fail = new MRKExecutionPlan("export-fail", new[]
            {
                new MRKExecutionStep("capture", new string[0], (ctx,ct) =>
                {
                    ctx.Data.Return("Image", new byte[] {1});
                    throw new InvalidOperationException("未完成的任务");
                }, dataOutputs: Outputs(
                    new KeyValuePair<string, Type>("Image", typeof(byte[]))))
            }, new Dictionary<string, string> {{"Image", "capture.Image"}});
            MRKExecutionEngine bad = new MRKExecutionEngine(new FCRWorkerOptions(1, 3));
            try
            {
                MRKFlowResult result = Wait(bad.StartFlow(fail).Completion);
                Check(result.State == MRKFlowState.Failed &&
                    result.CommittedOutputs.Count == 0,
                    "失败Flow却暴露了完整导出");
                bool rejected = false;
                try { result.GetCommittedOutput<byte[]>("Image"); }
                catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "失败Flow仍可读取未提交导出");
            }
            finally { Wait(bad.StopAsync()); }
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
                    Check(ctx.DataScope == null,
                        "托管DataContext节点仍然暴露旧父级可写作用域");
                    ctx.Data.Set("Local", 9);
                    return Task.FromResult((object)ctx.Data.Get<int>("Local"));
                }, output: new MRKExecutionOutput(typeof(int)), enableDataContext: true),
                new MRKExecutionStep("legacy", new string[0], (ctx,ct) =>
                {
                    int inherited = 0;
                    Check(ctx.DataScope != null &&
                        ctx.DataScope.TryRead<int>("Secret", out inherited) && inherited == 123,
                        "未迁移的旧DataScope兼容行为被破坏");
                    return Task.FromResult((object)inherited);
                }, output: new MRKExecutionOutput(typeof(int)))
            });
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(1, 3));
            try
            {
                MRKFlowResult result = Wait(engine.StartFlowWithData(plan, root).Completion);
                Check(result.State == MRKFlowState.Completed &&
                    (int)result.Results["node"] == 9 && (int)result.Results["legacy"] == 123,
                    "新旧作用域兼容不正确");
            }
            finally { root.Dispose(); Wait(engine.StopAsync()); }
        }
    }
}
