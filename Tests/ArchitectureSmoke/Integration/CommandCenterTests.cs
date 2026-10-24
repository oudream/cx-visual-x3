using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Commands;
using CxVisual.Mechanism.Data.Variables;
using CxVisual.Mechanism.Data.RuntimeData;
using CxVisual.Mechanism.Logic.Commands;
using CxVisual.Mechanism.Runtime.Commands;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class CommandCenterTests
    {
        private static void Check(bool ok, string message)
        { if (!ok) throw new InvalidOperationException("BUS-CMD: " + message); }
        private static readonly string Target = "/machine/1201/app/XRay/module/Algorithm/command";
        private static MCECommandDefinition Definition(string code = "Calculate")
        {
            return new MCECommandDefinition(5183, code, "计算",
                new[] {
                    new MCECommandParameterDefinition("Input", typeof(double), true, minValue: -1000.0, maxValue: 1000.0),
                    new MCECommandParameterDefinition("Factor", typeof(double), false, true, 2.0)
                },
                new[] { new MCECommandParameterDefinition("Output", typeof(double), true) });
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

        // 进阶正反：注册热替换、过滤发现、操作身份、快照与执行隔离。
        private static void ExtendedRun()
        {

            MCECommandDefinition hierarchical = new MCECommandDefinition(7003, "Nested", "复杂参数",
                new[] { new MCECommandParameterDefinition("Spec",
                    typeof(Dictionary<string, object>), true,
                    children: new[] {
                        new MCECommandParameterDefinition("Threshold", typeof(double), true,
                            minValue: 0.0, maxValue: 1.0),
                        new MCECommandParameterDefinition("Label", typeof(string), true,
                            formatPattern: "^[A-Z]+$")
                    }) },
                new[] { new MCECommandParameterDefinition("Result",
                    typeof(Dictionary<string, object>), true,
                    children: new[] {
                        new MCECommandParameterDefinition("OK", typeof(bool), true)
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
            // 正式适配器复用MDS变量真源，而不是测试中自造存取委托。
            MDSVariableTable variableTable = new MDSVariableTable("command-binding");
            variableTable.Define(new MDSVariableDefinition("in", typeof(double)));
            variableTable.Define(new MDSVariableDefinition("out", typeof(double)));
            variableTable.Define(new MDSVariableDefinition("readonly", typeof(double), writable: false));
            variableTable.Update("in", 9.0);
            using (MDSCommandValueBinding bindings = new MDSCommandValueBinding())
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
            MCECommandDefinition bytes = new MCECommandDefinition(6302, "ArrayCommand", "数组安全",
                new[] { new MCECommandParameterDefinition("Data", typeof(byte[]), true) },
                new[] { new MCECommandParameterDefinition("Result", typeof(byte[]), true) });
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
            MCECommandDefinition bytes = new MCECommandDefinition(6301, "Copy", "快照",
                new[] { new MCECommandParameterDefinition("Data", typeof(byte[]), true) },
                new[] { new MCECommandParameterDefinition("First", typeof(int), true) });
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
            ExtendedRun();
            Console.WriteLine("PASS BUS-CMD CommandCenter: registration/router/authorization/MDSSchema/mapping/batch/concurrency/timeout/snapshot/replacement/replay/copy");
        }
    }
}
