using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Commands;
using CxVisual.Mechanism.Data.Variables;
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
            Console.WriteLine("PASS BUS-CMD CommandCenter: registration/router/authorization/MDSSchema/mapping/batch/concurrency/timeout/snapshot");
        }
    }
}
