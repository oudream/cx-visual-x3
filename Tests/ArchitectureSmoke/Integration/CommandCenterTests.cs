using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Commands;
using CxVisual.Mechanism.Data.Variables;
using CxVisual.Mechanism.Data.RuntimeData;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Data.Execution;
using CxVisual.Foundation.Data.Commands;
using CxVisual.Mechanism.Runtime.Commands;
using CxVisual.Mechanism.Runtime.Execution;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class CommandCenterTests
    {
        private static void Check(bool ok, string message)
        { if (!ok) throw new InvalidOperationException("BUS-CMD: " + message); }
        private static readonly string Target = "/machine/1201/app/XRay/module/Algorithm/command";
        private static FDCCommandDefinition Definition(string code = "Calculate")
        {
            return new FDCCommandDefinition(5183, code, "计算",
                new[] {
                    new FDCCommandParameterDefinition("Input", typeof(double), true, minValue: -1000.0, maxValue: 1000.0),
                    new FDCCommandParameterDefinition("Factor", typeof(double), false, true, 2.0)
                },
                new[] { new FDCCommandParameterDefinition("Output", typeof(double), true) });
        }
        private static MRKDelegateCommandHandler Handler(Func<double, double> fn)
        {
            return new MRKDelegateCommandHandler((context, inputs, ct) =>
            {
                double answer = fn((double)inputs["Input"] * (double)inputs["Factor"]);
                context.ReportProgress(100, "完成");
                return Task.FromResult(new MRKCommandHandlerResult(
                    new Dictionary<string, object> { { "Output", answer } },
                    answer >= 0, answer >= 0 ? "OK" : "Negative"));
            });
        }
        private static MRKCommandRequest Request(double input, Guid operationId = default(Guid),
            MRKCommandOptions options = null, string caller = "test")
        {
            return new MRKCommandRequest(Target, "Calculate",
                new Dictionary<string, object> { { "Input", input } },
                caller, operationId, options: options);
        }
        private sealed class MdsBinding : IFDCCommandValueBinding
        {
            private readonly MDSVariableTable _variables;
            internal MdsBinding(MDSVariableTable variables) { _variables = variables; }
            public Task<object> ReadAsync(string name, CancellationToken cancellation)
            { cancellation.ThrowIfCancellationRequested(); return Task.FromResult((object)_variables.Get<double>(name)); }
            public Task WriteAsync(string name, object value, CancellationToken cancellation)
            { cancellation.ThrowIfCancellationRequested(); _variables.Update<double>(name, (double)value); return Task.CompletedTask; }
        }


        /// <summary>命令值Wire V1：跨进程白名单值的版本、长度、嵌套、数组与污染反证。</summary>
        private static void WireCodecRun()
        {
            Guid identity = Guid.NewGuid();
            Dictionary<string, object> tree = new Dictionary<string, object> {
                { "Guid", identity },
                { "Text", "检测完成" },
                { "Number", 2.5 },
                { "Integers", new int[] { 1, 3, 5 } },
                { "Bytes", new byte[] { 7, 9 } },
                { "Time", new DateTime(2026, 10, 25, 10, 0, 0, DateTimeKind.Utc) },
                { "Nested", new Dictionary<string, object> {
                    { "Result", true },
                    { "Scores", new List<object> { 3.0, 5.0, null } }
                } }
            };
            byte[] frame = FDCCommandValueCodec.Encode(tree);
            byte[] alternative = FDCCommandValueCodec.Encode(
                tree.OrderByDescending(x => x.Key, StringComparer.Ordinal)
                    .ToDictionary(x => x.Key, x => x.Value));
            Check(frame.SequenceEqual(alternative), "wire encoding must not depend on map insertion order");
            ((byte[])tree["Bytes"])[0] = 99;
            IReadOnlyDictionary<string, object> decoded = FDCCommandValueCodec.Decode(frame);
            Check((double)decoded["Number"] == 2.5 &&
                ((int[])decoded["Integers"])[2] == 5 &&
                ((byte[])decoded["Bytes"])[0] == 7 &&
                ((Guid)decoded["Guid"]) == identity &&
                ((DateTime)decoded["Time"]).Kind == DateTimeKind.Utc &&
                ((bool)((Dictionary<string, object>)decoded["Nested"])["Result"]) &&
                ((List<object>)((Dictionary<string, object>)decoded["Nested"])["Scores"]).Count == 3,
                "wire typed values/array/nested map roundtrip");

            bool truncated = false;
            try { FDCCommandValueCodec.Decode(frame.Take(frame.Length - 1).ToArray()); }
            catch (EndOfStreamException) { truncated = true; }
            catch (InvalidDataException) { truncated = true; }
            Check(truncated, "truncated wire frame must reject");
            byte[] badHeader = (byte[])frame.Clone();
            badHeader[0] ^= 0x40;
            bool invalidHeader = false;
            try { FDCCommandValueCodec.Decode(badHeader); }
            catch (InvalidDataException) { invalidHeader = true; }
            Check(invalidHeader, "wire version/header mismatch must reject");
            bool unknownTag = false;
            byte[] simple = FDCCommandValueCodec.Encode(new Dictionary<string, object> {
                { "x", 17 }
            });
            simple[14] = 255; // Header(5)+Count(4)+NameLen(4)+Name(1)后的Tag
            try { FDCCommandValueCodec.Decode(simple); }
            catch (InvalidDataException) { unknownTag = true; }
            Check(unknownTag, "unknown wire tag must fail closed");
            bool huge = false;
            try { FDCCommandValueCodec.Encode(new Dictionary<string, object> {
                { "payload", new byte[2 * 1024 * 1024 + 1] }
            }); }
            catch (ArgumentException) { huge = true; }
            Check(huge, "binary payload over configured limit must reject");
            bool unsupported = false;
            try { FDCCommandValueCodec.Encode(new Dictionary<string, object> {
                { "ArbitraryObject", new object() }
            }); }
            catch (NotSupportedException) { unsupported = true; }
            Check(unsupported, "arbitrary CLR DTO deserialization must fail closed");
        }

        private sealed class CommandExpressionResolver : IMDSExternalBindingResolver
        {
            internal int Calls;
            internal readonly MDSVariableTable Variables;
            internal CommandExpressionResolver(MDSVariableTable variables)
            { Variables = variables; }
            public object Resolve(MDSBindingSource source, string sourceName, Type targetType)
            {
                Interlocked.Increment(ref Calls);
                if (source == MDSBindingSource.Expression && sourceName == "gain*2")
                    return 2.0;
                if (source == MDSBindingSource.Variable && sourceName == "Factor")
                    return Variables.Get<double>("Factor");
                throw new KeyNotFoundException("来源未授权或未定义：" + sourceName);
            }
        }

        /// <summary>使用MDS已有绑定类型，实现常量、端口、变量、表达式来源与输出写回。</summary>
        private static void ConfiguredBindingRun()
        {
            MDSVariableTable table = new MDSVariableTable("command-config");
            table.Define(new MDSVariableDefinition("Factor", typeof(double)));
            table.Define(new MDSVariableDefinition("Answer", typeof(double)));
            table.Update("Factor", 3.0);
            int invoked = 0;
            using (MDEExecutionDataScope scope = MDEExecutionDataScope.CreateRoot("cmd-config"))
            {
                scope.Put("SourcePort", 4.0);
                MDSDataContext context = new MDSDataContext(scope);
                CommandExpressionResolver external = new CommandExpressionResolver(table);
                using (MRKCommandMdsBinding owner = new MRKCommandMdsBinding())
                using (MRKCommandCenter center = new MRKCommandCenter())
                using (center.Register(new MRKCommandDescriptor("configured", Definition()),
                    Handler(x => { Interlocked.Increment(ref invoked); return x; })))
                {
                    owner.BindVariable<double>("answer", table, "Answer");
                    MRKCommandInputSource[] sources = new[] {
                        new MRKCommandInputSource("Input",
                            new MDSDataBinding(MDSBindingSource.CurrentSlot, typeof(double),
                                sourceName: "SourcePort")),
                        new MRKCommandInputSource("Factor",
                            new MDSDataBinding(MDSBindingSource.Expression, typeof(double),
                                sourceName: "gain*2"))
                    };
                    MRKCommandBindingResult result = center.InvokeConfiguredResultAsync(
                        new MRKCommandRequest("configured", 5183, expectedSchemaVersion: 1),
                        context, sources, owner,
                        new[] { new MRKCommandParameterBinding("Output", "answer") },
                        external).GetAwaiter().GetResult();
                    Check(result.Execution.State == MRKCommandState.Completed &&
                        result.MappingSucceeded && table.Get<double>("Answer") == 8.0 &&
                        external.Calls == 1 && invoked == 1,
                        "MDS port plus expression input and real variable output");

                    MRKCommandInputSource[] constant = new[] {
                        new MRKCommandInputSource("Input",
                            new MDSDataBinding(MDSBindingSource.Constant, typeof(double),
                                constant: 5.0)),
                        new MRKCommandInputSource("Factor",
                            new MDSDataBinding(MDSBindingSource.Variable, typeof(double),
                                sourceName: "Factor"))
                    };
                    MRKCommandBindingResult variable = center.InvokeConfiguredResultAsync(
                        new MRKCommandRequest("configured", "Calculate"), context, constant,
                        external: external).GetAwaiter().GetResult();
                    Check(variable.Execution.State == MRKCommandState.Completed &&
                        (double)variable.Execution.Outputs["Output"] == 15.0 &&
                        !variable.MappingAttempted && invoked == 2 && external.Calls == 2,
                        "constant and actual MDS variable resolved by existing external owner");

                    MRKCommandBindingResult versionRejected = center.InvokeConfiguredResultAsync(
                        new MRKCommandRequest("configured", 5183, expectedSchemaVersion: 999),
                        context, sources, external: external).GetAwaiter().GetResult();
                    Check(versionRejected.Execution.Stage == MRKCommandStage.Rejected &&
                        versionRejected.Execution.ErrorCode == "SchemaVersionMismatch" &&
                        external.Calls == 2 && invoked == 2,
                        "outdated Schema must reject before evaluating expressions");

                    bool duplicate = false;
                    try
                    {
                        center.InvokeConfiguredResultAsync(
                            new MRKCommandRequest("configured", "Calculate",
                                new Dictionary<string, object> { { "Input", 12.0 } }),
                            context, sources, external: external).GetAwaiter().GetResult();
                    }
                    catch (ArgumentException) { duplicate = true; }
                    Check(duplicate && invoked == 2 && external.Calls == 2,
                        "duplicate static and dynamic inputs rejected before owner resolution");

                    bool noResolver = false;
                    try
                    {
                        center.InvokeConfiguredResultAsync(
                            new MRKCommandRequest("configured", "Calculate"),
                            context, sources).GetAwaiter().GetResult();
                    }
                    catch (InvalidOperationException) { noResolver = true; }
                    Check(noResolver && invoked == 2,
                        "expression without authorized MDS resolver must fail closed");

                    bool wrongPort = false;
                    try
                    {
                        center.InvokeConfiguredResultAsync(new MRKCommandRequest(
                            "configured", "Calculate"), context,
                            new[] { new MRKCommandInputSource("Unregistered",
                                new MDSDataBinding(MDSBindingSource.Expression,
                                    typeof(double), "gain*2")) }, external: external)
                            .GetAwaiter().GetResult();
                    }
                    catch (ArgumentException) { wrongPort = true; }
                    Check(wrongPort && external.Calls == 2 && invoked == 2,
                        "unknown port must reject before expression evaluation or side effects");
                }
            }
        }

        /// <summary>同步设备SDK运行于唯一F19专用线程，不声称异步续体具有线程亲和。</summary>
        private static void DedicatedCommandRun()
        {
            FCRDedicatedExecutor thread = new FCRDedicatedExecutor("BUS-CMD-SDK", 2);
            try
            {
                int count = 0;
                int actualThread = 0;
                using (MRKCommandCenter center = new MRKCommandCenter())
                using (center.Register(new MRKCommandDescriptor("sdk", Definition()),
                    new MRKDedicatedCommandHandler(thread, (ctx, inputs, token) =>
                    {
                        token.ThrowIfCancellationRequested();
                        Interlocked.Increment(ref count);
                        int identity = Thread.CurrentThread.ManagedThreadId;
                        if (actualThread == 0) actualThread = identity;
                        Check(identity == actualThread, "SDK thread affinity lost");
                        double n = (double)inputs["Input"];
                        if (n == -1.0) throw new InvalidOperationException("模拟SDK单次异常");
                        return new MRKCommandHandlerResult(new Dictionary<string, object> {
                            { "Output", n * (double)inputs["Factor"] } });
                    })))
                {
                    MRKCommandResponse first = center.InvokeAsync(new MRKCommandRequest(
                        "sdk", "Calculate",
                        new Dictionary<string, object> { { "Input", 2.0 } }))
                        .GetAwaiter().GetResult();
                    MRKCommandResponse failure = center.InvokeAsync(new MRKCommandRequest(
                        "sdk", "Calculate",
                        new Dictionary<string, object> { { "Input", -1.0 } }))
                        .GetAwaiter().GetResult();
                    MRKCommandResponse recovered = center.InvokeAsync(new MRKCommandRequest(
                        "sdk", "Calculate",
                        new Dictionary<string, object> { { "Input", 3.0 } }))
                        .GetAwaiter().GetResult();
                    Check(first.State == MRKCommandState.Completed &&
                        failure.State == MRKCommandState.Failed &&
                        recovered.State == MRKCommandState.Completed &&
                        actualThread == thread.ThreadId &&
                        thread.ThreadId != Thread.CurrentThread.ManagedThreadId &&
                        (double)recovered.Outputs["Output"] == 6.0 && count == 3,
                        "dedicated SDK thread must survive one Handler failure");
                }
            }
            finally { thread.StopAsync().GetAwaiter().GetResult(); }
            bool refused = false;
            try
            {
                thread.InvokeAsync(token => 1).GetAwaiter().GetResult();
            }
            catch (InvalidOperationException) { refused = true; }
            Check(refused, "stopped SDK executor must reject new calls");
        }

        /// <summary>复用MRK统一幂等重试Policy，仅Handler异常可重试，禁止设备副作用自动重发。</summary>
        private static void RetryRun()
        {
            int attempts = 0;
            MRKExecutionPolicy retry = new MRKExecutionPolicy(
                maxAttempts: 3, idempotent: true,
                retryCondition: ex => ex is InvalidOperationException);
            MRKCommandPolicy policy = new MRKCommandPolicy(retryPolicy: retry);
            using (MRKCommandCenter center = new MRKCommandCenter())
            using (center.Register(new MRKCommandDescriptor("retry", Definition(),
                policy: policy), new MRKDelegateCommandHandler((ctx, inputs, token) =>
                {
                    if (Interlocked.Increment(ref attempts) < 3)
                        throw new InvalidOperationException("模拟瞬时算法异常");
                    return Task.FromResult(new MRKCommandHandlerResult(
                        new Dictionary<string, object> { { "Output", 12.0 } }));
                })))
            {
                MRKCommandSubmission submission = center.Submit(new MRKCommandRequest(
                    "retry", "Calculate", new Dictionary<string, object> { { "Input", 1.0 } }));
                Check(submission.Operation.Completion.Wait(TimeSpan.FromSeconds(5)) &&
                    submission.Operation.Snapshot().State == MRKCommandState.Completed &&
                    submission.Operation.AttemptCount == 3 && attempts == 3,
                    "idempotent handler allowed bounded exception retry");
            }

            int timeoutCount = 0;
            using (MRKCommandCenter center = new MRKCommandCenter())
            using (center.Register(new MRKCommandDescriptor("no-timeout-retry", Definition(),
                policy: policy), new MRKDelegateCommandHandler((ctx, inputs, token) =>
                {
                    Interlocked.Increment(ref timeoutCount);
                    throw new TimeoutException("模拟未知设备应答");
                })))
            {
                MRKCommandSubmission submission = center.Submit(new MRKCommandRequest(
                    "no-timeout-retry", "Calculate",
                    new Dictionary<string, object> { { "Input", 1.0 } }));
                Check(submission.Operation.Completion.Wait(TimeSpan.FromSeconds(5)) &&
                    submission.Operation.Snapshot().State == MRKCommandState.Failed &&
                    timeoutCount == 1 && submission.Operation.AttemptCount == 1,
                    "timeout may be unknown side effect and must never automatically retry");
            }
            bool invalid = false;
            try { new MRKExecutionPolicy(maxAttempts: 2, idempotent: false); }
            catch (ArgumentException) { invalid = true; }
            Check(invalid, "non-idempotent retries rejected by MRK policy");

            int invoked = 0;
            using (MRKCommandCenter device = new MRKCommandCenter(authorize: ctx => true))
            using (device.Register(new MRKCommandDescriptor("axis", Definition(),
                MRKCommandKind.Device, policy), Handler(x =>
                {
                    Interlocked.Increment(ref invoked); return x;
                })))
            {
                MRKCommandResponse rejected = device.InvokeAsync(new MRKCommandRequest(
                    "axis", "Calculate", new Dictionary<string, object> { { "Input", 1.0 } }))
                    .GetAwaiter().GetResult();
                Check(rejected.Stage == MRKCommandStage.Rejected &&
                    rejected.ErrorCode == "DeviceAutoRetryForbidden" && invoked == 0,
                    "device side-effect automatic retry must fail closed");
            }
        }

        /// <summary>正式ID寻址与Code/ID双身份验证；ID调用仍共用注册与MDS绑定。</summary>
        private static void CommandIdRun()
        {
            int invoked = 0;
            using (MRKCommandCenter center = new MRKCommandCenter())
            using (center.Register(new MRKCommandDescriptor("identity", Definition()),
                Handler(x => { Interlocked.Increment(ref invoked); return x; })))
            {
                MRKCommandResponse byId = center.InvokeAsync(new MRKCommandRequest(
                    "identity", 5183, new Dictionary<string, object> { { "Input", 2.0 } },
                    expectedSchemaVersion: 1)).GetAwaiter().GetResult();
                Check(byId.State == MRKCommandState.Completed &&
                    (double)byId.Outputs["Output"] == 4.0 && invoked == 1,
                    "stable CommandId-only routing executes selected handler");
                MRKCommandResponse mismatch = center.InvokeAsync(new MRKCommandRequest(
                    "identity", "Calculate",
                    new Dictionary<string, object> { { "Input", 1.0 } },
                    commandId: 9999)).GetAwaiter().GetResult();
                Check(mismatch.ErrorCode == "CommandIdentityMismatch" && invoked == 1,
                    "Code/Id conflict must not call handler");
                MRKCommandResponse unknown = center.InvokeAsync(new MRKCommandRequest(
                    "identity", 9999)).GetAwaiter().GetResult();
                Check(unknown.ErrorCode == "HandlerNotFound" && invoked == 1,
                    "unknown CommandId must not resolve another command");
                FaultingCommandBinding binding = new FaultingCommandBinding();
                MRKCommandBindingResult mapped = center.InvokeBoundResultAsync(
                    new MRKCommandRequest("identity", 5183, expectedSchemaVersion: 1),
                    binding,
                    new[] { new MRKCommandParameterBinding("Input", "read") },
                    new[] { new MRKCommandParameterBinding("Output", "ok") })
                    .GetAwaiter().GetResult();
                Check(mapped.Execution.State == MRKCommandState.Completed &&
                    mapped.MappingSucceeded && binding.Writes.Count == 1 && invoked == 2,
                    "ID-only command must preserve MDS input/output binding");
            }
        }

        /// <summary>命令业务已完成而输出写回失败时不得丢失执行回执或隐藏局部写入。</summary>
        private static void OutputMappingRun()
        {
            FDCCommandDefinition definition = new FDCCommandDefinition(8808, "Produce", "生成",
                new FDCCommandParameterDefinition[0],
                new[] {
                    new FDCCommandParameterDefinition("First", typeof(double), true),
                    new FDCCommandParameterDefinition("Second", typeof(double), false)
                });
            MRKCommandParameterBinding[] bindings = new[] {
                new MRKCommandParameterBinding("First", "ok"),
                new MRKCommandParameterBinding("Second", "fail")
            };
            int invoked = 0;
            using (MRKCommandCenter center = new MRKCommandCenter())
            using (center.Register(new MRKCommandDescriptor("mapper", definition),
                new MRKDelegateCommandHandler((context, inputs, ct) =>
                {
                    Interlocked.Increment(ref invoked);
                    return Task.FromResult(new MRKCommandHandlerResult(
                        new Dictionary<string, object> { { "First", 10.0 }, { "Second", 20.0 } }));
                })))
            {
                FaultingCommandBinding owner = new FaultingCommandBinding();
                MRKCommandRequest request = new MRKCommandRequest("mapper", "Produce");
                MRKCommandBindingResult result = center.InvokeBoundResultAsync(request, owner,
                    null, bindings).GetAwaiter().GetResult();
                Check(result.Execution.State == MRKCommandState.Completed &&
                    result.MappingAttempted && !result.MappingSucceeded &&
                    result.MappingErrorCode == "OutputMappingFailed" &&
                    result.MappedAddresses.Count == 1 &&
                    result.MappedAddresses[0] == "ok" && owner.Writes.Count == 1 &&
                    center.Query(request.OperationId).State == MRKCommandState.Completed,
                    "partial output mapping must preserve successful command execution receipt");
                bool caught = false;
                try
                {
                    center.InvokeBoundAsync(new MRKCommandRequest("mapper", "Produce"),
                        new FaultingCommandBinding(), null, bindings).GetAwaiter().GetResult();
                }
                catch (MRKCommandOutputMappingException ex)
                {
                    caught = ex.Result.Execution.State == MRKCommandState.Completed &&
                        ex.Result.MappedAddresses.Count == 1;
                }
                Check(caught && invoked == 2, "legacy caller must receive structured failure");
            }
            using (MRKCommandCenter center = new MRKCommandCenter())
            using (center.Register(new MRKCommandDescriptor("missing", definition),
                new MRKDelegateCommandHandler((context, inputs, ct) =>
                    Task.FromResult(new MRKCommandHandlerResult(
                        new Dictionary<string, object> { { "First", 10.0 } })))))
            {
                FaultingCommandBinding owner = new FaultingCommandBinding();
                MRKCommandBindingResult result = center.InvokeBoundResultAsync(
                    new MRKCommandRequest("missing", "Produce"), owner,
                    null, bindings).GetAwaiter().GetResult();
                Check(result.Execution.State == MRKCommandState.Completed &&
                    result.MappingErrorCode == "OutputMissing" &&
                    !result.MappingSucceeded && owner.Writes.Count == 0,
                    "missing optional mapping port must reject before any output write");
            }
        }
        private sealed class FaultingCommandBinding : IFDCCommandValueBinding
        {
            internal readonly List<string> Writes = new List<string>();
            public Task<object> ReadAsync(string address, CancellationToken cancellation)
            { return Task.FromResult((object)1.0); }
            public Task WriteAsync(string address, object value, CancellationToken cancellation)
            {
                cancellation.ThrowIfCancellationRequested();
                if (address == "fail") throw new InvalidOperationException("模拟MDS写回故障");
                Writes.Add(address);
                return Task.CompletedTask;
            }
        }

        /// <summary>PauseRequested != Paused；中间值是快照且协作取消可打断暂停点。</summary>
        private static void LongTaskRun()
        {
            TaskCompletionSource<bool> entered = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> checkpoint = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            MRKCommandPolicy pausable = new MRKCommandPolicy(supportsPause: true);
            using (MRKCommandCenter center = new MRKCommandCenter())
            using (center.Register(new MRKCommandDescriptor("longjob", Definition(),
                policy: pausable), new MRKDelegateCommandHandler(async (ctx, inputs, token) =>
                {
                    entered.TrySetResult(true);
                    await checkpoint.Task.ConfigureAwait(false);
                    ctx.ReportProgress(25, "开始检查点");
                    ctx.ReportIntermediate(new Dictionary<string, object> {
                        { "Partial", new byte[] { 8, 9 } } });
                    await ctx.WaitWhilePausedAsync(token).ConfigureAwait(false);
                    ctx.ReportProgress(100, "完成");
                    return new MRKCommandHandlerResult(
                        new Dictionary<string, object> { { "Output", 42.0 } });
                })))
            {
                MRKCommandSubmission pending = center.Submit(new MRKCommandRequest(
                    "longjob", "Calculate",
                    new Dictionary<string, object> { { "Input", 1.0 } }));
                Check(pending.Operation != null && entered.Task.Wait(TimeSpan.FromSeconds(5)),
                    "long command entered handler");
                Check(center.Pause(pending.Operation.OperationId) &&
                    center.Query(pending.Operation.OperationId).State ==
                        MRKCommandState.PauseRequested, "pause request must not claim Paused");
                checkpoint.TrySetResult(true);
                Check(SpinWait.SpinUntil(() => center.Query(pending.Operation.OperationId).State ==
                    MRKCommandState.Paused, 5000), "handler must acknowledge checkpoint");
                Check(center.GetMetrics().Paused == 1 && pending.Operation.Progress == 25,
                    "paused and progress visible");
                byte[] partial = (byte[])pending.Operation.IntermediateOutputs["Partial"];
                partial[0] = 99;
                Check(((byte[])pending.Operation.IntermediateOutputs["Partial"])[0] == 8,
                    "intermediate output must be isolated");
                Check(center.Resume(pending.Operation.OperationId), "resume paused handler");
                Check(pending.Operation.Completion.Wait(TimeSpan.FromSeconds(5)) &&
                    pending.Operation.Snapshot().State == MRKCommandState.Completed &&
                    pending.Operation.Progress == 100, "resume completes original task");
                Check(!center.Pause(pending.Operation.OperationId) &&
                    !center.Resume(pending.Operation.OperationId),
                    "completed job must reject pause/resume");
            }

            TaskCompletionSource<bool> began = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> proceed = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using (MRKCommandCenter center = new MRKCommandCenter())
            using (center.Register(new MRKCommandDescriptor("cancel-pause", Definition(),
                policy: pausable), new MRKDelegateCommandHandler(async (ctx, inputs, token) =>
                {
                    began.TrySetResult(true);
                    await proceed.Task.ConfigureAwait(false);
                    await ctx.WaitWhilePausedAsync(token).ConfigureAwait(false);
                    return new MRKCommandHandlerResult(
                        new Dictionary<string, object> { { "Output", 1.0 } });
                })))
            {
                MRKCommandSubmission job = center.Submit(new MRKCommandRequest(
                    "cancel-pause", "Calculate",
                    new Dictionary<string, object> { { "Input", 1.0 } }));
                Check(began.Task.Wait(TimeSpan.FromSeconds(5)) &&
                    center.Pause(job.Operation.OperationId), "second job pause requested");
                proceed.TrySetResult(true);
                Check(SpinWait.SpinUntil(() => center.Query(job.Operation.OperationId).State ==
                    MRKCommandState.Paused, 5000), "second handler reached pause");
                Check(center.Cancel(job.Operation.OperationId), "cancel while paused requested");
                Check(job.Operation.Completion.Wait(TimeSpan.FromSeconds(5)) &&
                    job.Operation.Snapshot().State == MRKCommandState.Cancelled,
                    "cancellation must interrupt cooperative pause");
            }
            using (MRKCommandCenter center = new MRKCommandCenter())
            using (center.Register(new MRKCommandDescriptor("plain", Definition()), Handler(x => x)))
            {
                MRKCommandSubmission job = center.Submit(new MRKCommandRequest(
                    "plain", "Calculate", new Dictionary<string, object> { { "Input", 1.0 } }));
                Check(!center.Pause(job.Operation.OperationId),
                    "pause unsupported handler must fail explicitly");
            }
        }

        /// <summary>F19正式有界队列：优先级、同目标FIFO、未执行取消、溢出反证。</summary>
        private static void QueuedRun()
        {
            TaskCompletionSource<bool> started = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> release = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            List<string> order = new List<string>();
            int cancelledInvocations = 0;
            FDCCommandDefinition definition = Definition();
            MRKCommandPolicy queued = new MRKCommandPolicy(
                dispatch: MRKCommandDispatch.Queued);
            Func<string, int, MRKCommandRequest> makeRequest = (target, priority) =>
                new MRKCommandRequest(target, "Calculate",
                    new Dictionary<string, object> { { "Input", 1.0 } },
                    options: new MRKCommandOptions(priority: priority));
            using (MRKCommandCenter center = new MRKCommandCenter(
                queuedConcurrency: 1, queueCapacity: 3))
            using (center.Register(new MRKCommandDescriptor("first", definition,
                policy: queued), new MRKDelegateCommandHandler(async (ctx, input, ct) =>
                {
                    started.TrySetResult(true);
                    await release.Task.ConfigureAwait(false);
                    lock (order) order.Add("first");
                    return new MRKCommandHandlerResult(
                        new Dictionary<string, object> { { "Output", 1.0 } });
                })))
            using (center.Register(new MRKCommandDescriptor("low", definition,
                policy: queued), new MRKDelegateCommandHandler((ctx, input, ct) =>
                {
                    lock (order) order.Add("low");
                    return Task.FromResult(new MRKCommandHandlerResult(
                        new Dictionary<string, object> { { "Output", 2.0 } }));
                })))
            using (center.Register(new MRKCommandDescriptor("high", definition,
                policy: queued), new MRKDelegateCommandHandler((ctx, input, ct) =>
                {
                    lock (order) order.Add("high");
                    return Task.FromResult(new MRKCommandHandlerResult(
                        new Dictionary<string, object> { { "Output", 3.0 } }));
                })))
            using (center.Register(new MRKCommandDescriptor("cancelled", definition,
                policy: queued), new MRKDelegateCommandHandler((ctx, input, ct) =>
                {
                    Interlocked.Increment(ref cancelledInvocations);
                    return Task.FromResult(new MRKCommandHandlerResult(
                        new Dictionary<string, object> { { "Output", 4.0 } }));
                })))
            {
                MRKCommandSubmission first = center.Submit(makeRequest("first", 0));
                Check(first.Operation != null && started.Task.Wait(TimeSpan.FromSeconds(5)),
                    "first queue command starts");
                MRKCommandSubmission low = center.Submit(makeRequest("low", -10));
                MRKCommandSubmission high = center.Submit(makeRequest("high", 10));
                MRKCommandSubmission cancelled = center.Submit(makeRequest("cancelled", 0));
                Check(low.Operation != null && high.Operation != null &&
                    cancelled.Operation != null && center.GetMetrics().Queued == 3,
                    "queued state and bounded pending");
                MRKCommandSubmission full = center.Submit(makeRequest("overflow", 20));
                Check(full.Operation == null && full.Response.ErrorCode == "HandlerNotFound",
                    "unregistered target must reject before queue allocation");
                MRKCommandSubmission fullTarget = center.Submit(makeRequest("high", 20));
                Check(fullTarget.Operation == null && fullTarget.Response.ErrorCode == "QueueFull",
                    "queue at capacity must reject without ledger allocation");
                Check(center.Cancel(cancelled.Operation.OperationId), "queued cancellation requested");
                Check(cancelled.Operation.Completion.Wait(TimeSpan.FromSeconds(5)) &&
                    cancelled.Operation.Snapshot().State == MRKCommandState.Cancelled &&
                    cancelledInvocations == 0, "cancelled queued command must never invoke Handler");
                release.TrySetResult(true);
                Check(first.Operation.Completion.Wait(TimeSpan.FromSeconds(5)) &&
                    low.Operation.Completion.Wait(TimeSpan.FromSeconds(5)) &&
                    high.Operation.Completion.Wait(TimeSpan.FromSeconds(5)),
                    "queued work must drain");
                lock (order)
                    Check(order.Count == 3 && order[0] == "first" &&
                        order[1] == "high" && order[2] == "low",
                        "high priority must precede lower priority across distinct targets");
                Check(center.GetMetrics().Completed == 3 &&
                    center.GetMetrics().Cancelled == 1 &&
                    center.GetQueueSnapshot().Pending == 0,
                    "queue metrics and cleanup");
            }
            bool rejectInvalidPolicy = false;
            try { new MRKCommandPolicy(2, dispatch: MRKCommandDispatch.Queued); }
            catch (ArgumentException) { rejectInvalidPolicy = true; }
            Check(rejectInvalidPolicy, "same key queue must be serial");
        }

        // 进阶正反：注册热替换、过滤发现、操作身份、快照与执行隔离。
        private static void ExtendedRun()
        {

            // 稳定命令ID与已发布Schema版本都能用于目标发现；版本不符不得触发Handler。
            int versionInvoked = 0;
            using (MRKCommandCenter versioned = new MRKCommandCenter())
            using (versioned.Register(new MRKCommandDescriptor("versioned", Definition()),
                Handler(x => { Interlocked.Increment(ref versionInvoked); return x; })))
            {
                Check(versioned.Router.ResolveById("versioned", 5183) != null &&
                    versioned.Router.ResolveById("versioned", 9999) == null &&
                    versioned.Router.Discover(commandId: 5183, schemaVersion: 1).Count == 1 &&
                    versioned.Router.Discover(commandId: 5183, schemaVersion: 2).Count == 0,
                    "command ID and exact published schema discovery");
                MRKCommandRequest stale = new MRKCommandRequest("versioned", "Calculate",
                    new Dictionary<string, object> { { "Input", 3.0 } },
                    expectedSchemaVersion: 2);
                MRKCommandResponse rejected = versioned.InvokeAsync(stale).GetAwaiter().GetResult();
                Check(rejected.Stage == MRKCommandStage.Rejected &&
                    rejected.ErrorCode == "SchemaVersionMismatch" && versionInvoked == 0,
                    "stale schema must not perform side effects");
                MRKCommandRequest match = new MRKCommandRequest("versioned", "Calculate",
                    new Dictionary<string, object> { { "Input", 3.0 } },
                    expectedSchemaVersion: 1);
                MRKCommandResponse accepted = versioned.InvokeAsync(match).GetAwaiter().GetResult();
                Check(accepted.State == MRKCommandState.Completed && versionInvoked == 1,
                    "matching schema should execute once");
                bool duplicateId = false;
                try
                {
                    using (versioned.Register(new MRKCommandDescriptor("versioned",
                        Definition("AnotherCommand")), Handler(x => x))) { }
                }
                catch (InvalidOperationException) { duplicateId = true; }
                Check(duplicateId && versioned.Router.Discover(target: "versioned").Count == 1,
                    "same target cannot register same CommandId with different command codes");
            }
            FDCCommandDefinition broadcastDefinition = Definition();
            using (MRKCommandCenter center = new MRKCommandCenter())
            using (center.Register(new MRKCommandDescriptor("line1", broadcastDefinition),
                Handler(x => x)))
            using (center.Register(new MRKCommandDescriptor("line2", broadcastDefinition),
                Handler(x => x + 1)))
            {
                IReadOnlyList<MRKCommandResponse> results = center.InvokeBroadcastAsync(
                    "Calculate", new Dictionary<string, object> { { "Input", 1.0 } })
                    .GetAwaiter().GetResult();
                Check(results.Count == 2 &&
                    (double)results[0].Outputs["Output"] == 2.0 &&
                    (double)results[1].Outputs["Output"] == 3.0 &&
                    results[0].OperationId != results[1].OperationId,
                    "broadcast must independently dispatch and correlate responses");
                MRKCommandResponse sync = center.Invoke(new MRKCommandRequest(
                    "line1", "Calculate", new Dictionary<string, object> { { "Input", 4.0 } }));
                Check(sync.State == MRKCommandState.Completed &&
                    (double)sync.Outputs["Output"] == 8.0, "sync SDK call");
            }
            using (MRKCommandCenter devices = new MRKCommandCenter(
                authorize: ctx => true))
            using (devices.Register(new MRKCommandDescriptor("axis1", Definition("MoveAbs"),
                MRKCommandKind.Device), Handler(x => x)))
            {
                bool blocked = false;
                try { devices.InvokeBroadcastAsync("MoveAbs").GetAwaiter().GetResult(); }
                catch (UnauthorizedAccessException) { blocked = true; }
                Check(blocked, "device broadcast must fail closed even if center has authorize delegate");
                IReadOnlyList<MRKCommandResponse> allowed = devices.InvokeBroadcastAsync("MoveAbs",
                    new Dictionary<string, object> { { "Input", 1.0 } },
                    allowDeviceBroadcast: true).GetAwaiter().GetResult();
                Check(allowed.Count == 1 && allowed[0].State == MRKCommandState.Completed,
                    "explicit device broadcast still uses target-side authorization");
            }

            FDCCommandDefinition hierarchical = new FDCCommandDefinition(7003, "Nested", "复杂参数",
                new[] { new FDCCommandParameterDefinition("Spec",
                    typeof(Dictionary<string, object>), true,
                    children: new[] {
                        new FDCCommandParameterDefinition("Threshold", typeof(double), true,
                            minValue: 0.0, maxValue: 1.0),
                        new FDCCommandParameterDefinition("Label", typeof(string), true,
                            formatPattern: "^[A-Z]+$")
                    }) },
                new[] { new FDCCommandParameterDefinition("Result",
                    typeof(Dictionary<string, object>), true,
                    children: new[] {
                        new FDCCommandParameterDefinition("OK", typeof(bool), true)
                    }) });
            int handled = 0;
            using (MRKCommandCenter nested = new MRKCommandCenter())
            using (nested.Register(new MRKCommandDescriptor("nested", hierarchical),
                new MRKDelegateCommandHandler((context, inputs, ct) =>
                {
                    ++handled;
                    Dictionary<string, object> nestedInput = (Dictionary<string, object>)inputs["Spec"];
                    Check((double)nestedInput["Threshold"] == 0.5 &&
                        (string)nestedInput["Label"] == "VALID", "nested input values");
                    return Task.FromResult(new MRKCommandHandlerResult(
                        new Dictionary<string, object> {
                            { "Result", new Dictionary<string, object> { { "OK", true } } }
                        }));
                })))
            {
                Func<object, MRKCommandResponse> invoke = spec => nested.InvokeAsync(
                    new MRKCommandRequest("nested", "Nested",
                        new Dictionary<string, object> { { "Spec", spec } }))
                    .GetAwaiter().GetResult();
                MRKCommandResponse good = invoke(new Dictionary<string, object> {
                    { "Threshold", 0.5 }, { "Label", "VALID" } });
                Check(good.State == MRKCommandState.Completed && handled == 1,
                    "structured input and output success");
                MRKCommandResponse tooHigh = invoke(new Dictionary<string, object> {
                    { "Threshold", 1.5 }, { "Label", "VALID" } });
                Check(tooHigh.ErrorCode == "InvalidInput" && handled == 1,
                    "nested range fail closed");
                MRKCommandResponse badFormat = invoke(new Dictionary<string, object> {
                    { "Threshold", 0.5 }, { "Label", "bad" } });
                Check(badFormat.ErrorCode == "InvalidInput" && handled == 1,
                    "nested format fail closed");
                MRKCommandResponse missing = invoke(new Dictionary<string, object> {
                    { "Threshold", 0.5 } });
                Check(missing.ErrorCode == "InvalidInput" && handled == 1,
                    "nested required fail closed");
                MRKCommandResponse extra = invoke(new Dictionary<string, object> {
                    { "Threshold", 0.5 }, { "Label", "VALID" }, { "Unknown", 3 } });
                Check(extra.ErrorCode == "InvalidInput" && handled == 1,
                    "nested unknown member fail closed");
                ((Dictionary<string, object>)good.Outputs["Result"])["OK"] = false;
                Check((bool)((Dictionary<string, object>)nested.Query(good.OperationId)
                    .Outputs["Result"])["OK"], "nested response snapshot remains immutable externally");
            }

            // 真正的节点DataContext输入/输出使用正式MDS作用域，不另造Heap或Frame。
            using (MDEExecutionDataScope root = MDEExecutionDataScope.CreateRoot("cmd-node"))
            {
                root.Put("incoming", 6.0);
                MDSDataContext context = new MDSDataContext(root);
                using (MRKCommandMdsBinding addresses = new MRKCommandMdsBinding())
                using (MRKCommandCenter center = new MRKCommandCenter())
                using (center.Register(new MRKCommandDescriptor(Target, Definition()),
                    Handler(x => x)))
                {
                    addresses.BindNode<double>("node.input", context, "incoming");
                    addresses.BindNode<double>("node.output", context, "result", true);
                    MRKCommandResponse response = center.InvokeBoundAsync(
                        new MRKCommandRequest(Target, "Calculate"), addresses,
                        new[] { new MRKCommandParameterBinding("Input", "node.input") },
                        new[] { new MRKCommandParameterBinding("Output", "node.output") })
                        .GetAwaiter().GetResult();
                    Check(response.State == MRKCommandState.Completed &&
                        context.Get<double>("result") == 12.0,
                        "MDS node context input/output mapping");
                    bool writeDenied = false;
                    try { addresses.WriteAsync("node.input", 99.0, CancellationToken.None)
                        .GetAwaiter().GetResult(); }
                    catch (UnauthorizedAccessException) { writeDenied = true; }
                    Check(writeDenied && context.Get<double>("incoming") == 6.0,
                        "node input remains readonly through command binding");
                }
            }
            // 正式适配器复用MDS变量真源，而不是测试中自造存取委托。
            MDSVariableTable variableTable = new MDSVariableTable("command-binding");
            variableTable.Define(new MDSVariableDefinition("in", typeof(double)));
            variableTable.Define(new MDSVariableDefinition("out", typeof(double)));
            variableTable.Define(new MDSVariableDefinition("readonly", typeof(double), writable: false));
            variableTable.Update("in", 9.0);
            using (MRKCommandMdsBinding bindings = new MRKCommandMdsBinding())
            using (MRKCommandCenter center = new MRKCommandCenter())
            using (center.Register(new MRKCommandDescriptor(Target, Definition()),
                Handler(x => x)))
            {
                bindings.BindVariable<double>("input-address", variableTable, "in");
                bindings.BindVariable<double>("output-address", variableTable, "out");
                bindings.BindVariable<double>("readonly-address", variableTable, "readonly");
                MRKCommandResponse mapped = center.InvokeBoundAsync(
                    new MRKCommandRequest(Target, "Calculate"), bindings,
                    new[] { new MRKCommandParameterBinding("Input", "input-address") },
                    new[] { new MRKCommandParameterBinding("Output", "output-address") })
                    .GetAwaiter().GetResult();
                Check(mapped.State == MRKCommandState.Completed &&
                    variableTable.Get<double>("out") == 18.0, "real MDS adapter mapping");
                bool readOnly = false;
                try { bindings.WriteAsync("readonly-address", 1.0, CancellationToken.None)
                    .GetAwaiter().GetResult(); }
                catch (UnauthorizedAccessException) { readOnly = true; }
                Check(readOnly, "readonly MDS binding must fail closed");
                bool duplicate = false;
                try { bindings.BindVariable<double>("input-address", variableTable, "in"); }
                catch (ArgumentException) { duplicate = true; }
                Check(duplicate, "duplicate MDS alias must reject");
                bool mismatched = false;
                try { bindings.BindVariable<int>("wrong", variableTable, "in"); }
                catch (InvalidCastException) { mismatched = true; }
                Check(mismatched, "MDS binding type mismatch must reject");
                Check(bindings.Unbind("input-address"), "unbind address");
                bool missing = false;
                try { bindings.ReadAsync("input-address", CancellationToken.None)
                    .GetAwaiter().GetResult(); }
                catch (KeyNotFoundException) { missing = true; }
                Check(missing, "unbound address must reject");
            }
            using (MRKCommandCenter center = new MRKCommandCenter())
            {
                IDisposable old = center.Register(new MRKCommandDescriptor(Target, Definition()),
                    Handler(x => x));
                IDisposable updated = center.Replace(new MRKCommandDescriptor(Target, Definition()),
                    Handler(x => x + 10));
                old.Dispose();
                Check(center.Router.Discover(name: "计算", commandCode: "Calculate").Count == 1,
                    "discover by name and code");
                Check(center.Router.Discover(name: "wrong").Count == 0,
                    "nonexistent name must not match");
                MRKCommandResponse newer = center.InvokeAsync(Request(2)).GetAwaiter().GetResult();
                Check((double)newer.Outputs["Output"] == 14.0,
                    "replace must route to new generation after old token disposed");
                Guid operation = Guid.NewGuid();
                MRKCommandRequest a = Request(4, operation);
                MRKCommandResponse first = center.InvokeAsync(a).GetAwaiter().GetResult();
                MRKCommandRequest b = Request(4, operation);
                MRKCommandResponse replay = center.InvokeAsync(b).GetAwaiter().GetResult();
                Check(first.RequestId == a.RequestId && replay.RequestId == b.RequestId &&
                    replay.OperationId == first.OperationId, "deduplicated response must keep caller RequestId");
                MRKCommandMetrics metrics = center.GetMetrics();
                Check(metrics.Registered == 1 && metrics.Completed == 2 &&
                    metrics.TotalOperations == 2, "metrics must only account for unique operations");
                updated.Dispose();
                Check(center.Router.Discover().Count == 0, "replacement token must unregister generation");
            }
            FDCCommandDefinition bytes = new FDCCommandDefinition(6302, "ArrayCommand", "数组安全",
                new[] { new FDCCommandParameterDefinition("Data", typeof(byte[]), true) },
                new[] { new FDCCommandParameterDefinition("Result", typeof(byte[]), true) });
            int count = 0;
            using (MRKCommandCenter center = new MRKCommandCenter())
            using (center.Register(new MRKCommandDescriptor("array", bytes),
                new MRKDelegateCommandHandler((ctx, inputs, ct) =>
                {
                    byte[] source = (byte[])inputs["Data"];
                    Interlocked.Increment(ref count);
                    byte firstValue = source[0];
                    source[0] = 77;
                    return Task.FromResult(new MRKCommandHandlerResult(
                        new Dictionary<string, object> { { "Result", new byte[] { firstValue, 2 } } }));
                })))
            {
                Guid op = Guid.NewGuid();
                MRKCommandRequest firstRequest = new MRKCommandRequest("array", "ArrayCommand",
                    new Dictionary<string, object> { { "Data", new byte[] { 3, 4 } } },
                    operationId: op);
                MRKCommandResponse result = center.InvokeAsync(firstRequest).GetAwaiter().GetResult();
                Check(((byte[])result.Outputs["Result"])[0] == 3, "handler input isolation");
                ((byte[])result.Outputs["Result"])[0] = 99;
                Check(((byte[])result.Outputs["Result"])[0] == 3 &&
                    ((byte[])center.Query(op).Outputs["Result"])[0] == 3,
                    "response nested mutable output must be defensive copy");
                MRKCommandRequest same = new MRKCommandRequest("array", "ArrayCommand",
                    new Dictionary<string, object> { { "Data", new byte[] { 3, 4 } } },
                    operationId: op);
                MRKCommandResponse replay = center.InvokeAsync(same).GetAwaiter().GetResult();
                Check(count == 1 && replay.RequestId == same.RequestId &&
                    ((byte[])replay.Outputs["Result"])[0] == 3,
                    "handler mutation must not break operation deduplication");
                bool rejected = false;
                try
                {
                    center.InvokeBoundAsync(same, new MdsBinding(new MDSVariableTable("unused")),
                        null, new[] { new MRKCommandParameterBinding("Typo", "somewhere") })
                        .GetAwaiter().GetResult();
                }
                catch (ArgumentException) { rejected = true; }
                Check(rejected, "invalid output binding must fail before executing");
                Check(count == 1, "invalid binding must cause zero additional side effects");
            }
        }
        public static void Run()
        {
            int count = 0;
            using (MRKCommandCenter center = new MRKCommandCenter())
            using (IDisposable registration = center.Register(
                new MRKCommandDescriptor(Target, Definition(), MRKCommandKind.Module,
                    new MRKCommandPolicy(8), new[] { "Calculate" }),
                Handler(x => { Interlocked.Increment(ref count); return x; })))
            {
                Check(center.Router.Discover(Target, MRKCommandKind.Module, "Calculate").Count == 1,
                    "target/capability discovery");
                MRKCommandResponse first = center.InvokeAsync(Request(5.0)).GetAwaiter().GetResult();
                Check(first.Stage == MRKCommandStage.Accepted && first.State == MRKCommandState.Completed &&
                    (double)first.Outputs["Output"] == 10.0 && first.BusinessSuccess == true,
                    "parameter default / handler / response");
                Check(center.Query(first.OperationId).State == MRKCommandState.Completed,
                    "operation query");
                MRKCommandResponse invalid = center.InvokeAsync(Request(2000)).GetAwaiter().GetResult();
                Check(invalid.Stage == MRKCommandStage.Rejected && invalid.ErrorCode == "InvalidInput",
                    "reject range before handler");
                MRKCommandResponse unknown = center.InvokeAsync(
                    new MRKCommandRequest(Target, "Missing")).GetAwaiter().GetResult();
                Check(unknown.ErrorCode == "HandlerNotFound", "unknown handler");
                MRKCommandResponse remote = center.InvokeAsync(
                    Request(3, options: new MRKCommandOptions(remote: true))).GetAwaiter().GetResult();
                Check(remote.ErrorCode == "RemoteUnsupported", "remote must not fake local success");
                Guid same = Guid.NewGuid();
                MRKCommandResponse original = center.InvokeAsync(Request(7, same)).GetAwaiter().GetResult();
                MRKCommandResponse replay = center.InvokeAsync(Request(7, same)).GetAwaiter().GetResult();
                Check(replay.OperationId == original.OperationId && count == 2,
                    "idempotent local replay must not execute twice");
                MRKCommandResponse conflict = center.InvokeAsync(Request(8, same)).GetAwaiter().GetResult();
                Check(conflict.ErrorCode == "OperationConflict", "replay binding conflict");
                MRKCommandResponse callerConflict = center.InvokeAsync(
                    Request(7, same, caller: "untrusted")).GetAwaiter().GetResult();
                Check(callerConflict.ErrorCode == "OperationConflict", "caller mismatch");
                Check(center.Registry.SetEnabled(Target, "Calculate", false), "disable");
                Check(center.InvokeAsync(Request(1)).GetAwaiter().GetResult().ErrorCode == "HandlerNotFound",
                    "disabled command must not invoke");
                Check(center.Registry.SetEnabled(Target, "Calculate", true), "enable");
                MRKCommandResponse business = center.InvokeAsync(Request(-1)).GetAwaiter().GetResult();
                Check(business.State == MRKCommandState.Completed && business.BusinessSuccess == false,
                    "business negative distinct from execution failure");
                MDSVariableTable vars = new MDSVariableTable("command-test");
                vars.Define(new MDSVariableDefinition("source", typeof(double)));
                vars.Define(new MDSVariableDefinition("target", typeof(double)));
                vars.Update("source", 13.0);
                MRKCommandResponse mapped = center.InvokeBoundAsync(
                    new MRKCommandRequest(Target, "Calculate"),
                    new MdsBinding(vars),
                    new[] { new MRKCommandParameterBinding("Input", "source") },
                    new[] { new MRKCommandParameterBinding("Output", "target") })
                    .GetAwaiter().GetResult();
                Check(mapped.State == MRKCommandState.Completed && vars.Get<double>("target") == 26.0,
                    "real MDS variable read and output write");
                MRKCommandResponse[] batch = new List<MRKCommandResponse>(
                    center.InvokeBatchAsync(new[] { Request(1), Request(2) })
                    .GetAwaiter().GetResult()).ToArray();
                Check(batch.Length == 2 && batch[0].State == MRKCommandState.Completed &&
                    batch[1].State == MRKCommandState.Completed, "batch");
            }
            using (MRKCommandCenter denied = new MRKCommandCenter())
            using (denied.Register(new MRKCommandDescriptor("axis", Definition("MoveAbs"),
                MRKCommandKind.Device), Handler(x => x)))
            {
                Check(denied.InvokeAsync(new MRKCommandRequest("axis", "MoveAbs",
                    new Dictionary<string, object> { { "Input", 1.0 } }))
                    .GetAwaiter().GetResult().ErrorCode == "Unauthorized",
                    "device without authorizer fail closed");
            }
            using (MRKCommandCenter authorized = new MRKCommandCenter(
                authorize: ctx => ctx.Caller == "system"))
            using (authorized.Register(new MRKCommandDescriptor("axis", Definition("MoveAbs"),
                MRKCommandKind.Device), Handler(x => x)))
            {
                MRKCommandResponse result = authorized.InvokeAsync(new MRKCommandRequest("axis", "MoveAbs",
                    new Dictionary<string, object> { { "Input", 1.0 } }, "system"))
                    .GetAwaiter().GetResult();
                Check(result.State == MRKCommandState.Completed, "authorized device command");
            }
            using (MRKCommandCenter failed = new MRKCommandCenter())
            using (failed.Register(new MRKCommandDescriptor(Target, Definition()),
                new MRKDelegateCommandHandler((c, i, ct) =>
                { throw new InvalidOperationException("algorithm fault"); })))
            {
                Check(failed.InvokeAsync(Request(1)).GetAwaiter().GetResult().ErrorCode == "HandlerFailure",
                    "handler failure isolation");
            }
            using (MRKCommandCenter bounded = new MRKCommandCenter())
            {
                TaskCompletionSource<bool> gate = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                using (bounded.Register(new MRKCommandDescriptor(Target, Definition(),
                    policy: new MRKCommandPolicy(1)),
                    new MRKDelegateCommandHandler(async (c, i, ct) =>
                    {
                        await gate.Task.ConfigureAwait(false);
                        return new MRKCommandHandlerResult(
                            new Dictionary<string, object> { { "Output", 9.0 } });
                    })))
                {
                    MRKCommandSubmission pending = bounded.Submit(Request(3));
                    Check(pending.Response.Stage == MRKCommandStage.Accepted, "accepted pending");
                    Check(bounded.Submit(Request(4)).Response.ErrorCode == "Busy", "bounded admission");
                    gate.TrySetResult(true);
                    Check(pending.Operation.Completion.GetAwaiter().GetResult().State ==
                        MRKCommandState.Completed, "pending completion");
                }
            }
            using (MRKCommandCenter timed = new MRKCommandCenter())
            using (timed.Register(new MRKCommandDescriptor(Target, Definition(),
                policy: new MRKCommandPolicy(1, supportsCancellation: false)),
                new MRKDelegateCommandHandler(async (c, i, ct) =>
                {
                    await Task.Delay(100).ConfigureAwait(false); // 模拟不支持取消的设备Provider
                    return new MRKCommandHandlerResult(
                        new Dictionary<string, object> { { "Output", 3.0 } });
                })))
            {
                MRKCommandResponse timedOut = timed.InvokeAsync(Request(1,
                    options: new MRKCommandOptions(5))).GetAwaiter().GetResult();
                Check(timedOut.State == MRKCommandState.Unknown && timedOut.ErrorCode == "TimeoutUnknown",
                    "timeout is unknown not failure");
                for (int i = 0; i < 50 && timed.Query(timedOut.OperationId).State != MRKCommandState.Completed; ++i)
                    Thread.Sleep(10);
                Check(timed.Query(timedOut.OperationId).State == MRKCommandState.Completed,
                    "late completion must remain queryable");
            }
            // 调用时复制byte[]，禁止调用方改写正在执行的参数。
            FDCCommandDefinition bytes = new FDCCommandDefinition(6301, "Copy", "快照",
                new[] { new FDCCommandParameterDefinition("Data", typeof(byte[]), true) },
                new[] { new FDCCommandParameterDefinition("First", typeof(int), true) });
            using (MRKCommandCenter copied = new MRKCommandCenter())
            {
                TaskCompletionSource<bool> gate = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                using (copied.Register(new MRKCommandDescriptor("algorithm", bytes),
                    new MRKDelegateCommandHandler(async (c, i, ct) =>
                    {
                        await gate.Task.ConfigureAwait(false);
                        return new MRKCommandHandlerResult(new Dictionary<string, object> {
                            { "First", (int)((byte[])i["Data"])[0] } });
                    })))
                {
                    byte[] data = new byte[] { 7, 8 };
                    MRKCommandSubmission submission = copied.Submit(new MRKCommandRequest(
                        "algorithm", "Copy", new Dictionary<string, object> { { "Data", data } }));
                    data[0] = 99;
                    gate.TrySetResult(true);
                    Check((int)submission.Operation.Completion.GetAwaiter().GetResult().Outputs["First"] == 7,
                        "byte array snapshot safety");
                }
            }
            WireCodecRun();
            QueuedRun();
            ConfiguredBindingRun();
            DedicatedCommandRun();
            LongTaskRun();
            OutputMappingRun();
            CommandIdRun();
            RetryRun();
            ExtendedRun();
            Console.WriteLine("PASS BUS-CMD CommandCenter: registration/router/authorization/MDSSchema/mapping/batch/concurrency/timeout/snapshot/replacement/replay/copy");
        }
    }
}
