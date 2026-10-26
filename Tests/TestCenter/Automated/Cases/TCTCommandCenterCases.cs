using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Commands;
using CxVisual.Mechanism.Runtime.Commands;
using CxVisual.Mechanism.Data.Variables;
using CxVisual.TestCenter.Core;
using CxVisual.TestCenter.Discovery;

namespace CxVisual.TestCenter.Cases
{
    /// <summary>
    /// 正式TestCenter Runner/Run/Attempt案例。每个案例创建并释放独立的MRK Center；
    /// 只测真实FDC/MRK Owner，不以人工工作区日志假冒Passed。
    /// </summary>
    public static class TCTCommandCenterCases
    {
        private static FDCCommandDefinition Definition()
        {
            return new FDCCommandDefinition(970101, "Multiply", "TCT倍乘验证",
                new[] {
                    new FDCCommandParameterDefinition("Input", typeof(double), true,
                        minValue: -100.0, maxValue: 100.0),
                    new FDCCommandParameterDefinition("Factor", typeof(double), false, true, 2.0)
                },
                new[] { new FDCCommandParameterDefinition("Output", typeof(double), true) });
        }

        private static MRKDelegateCommandHandler Handler(Action onExecute = null)
        {
            return new MRKDelegateCommandHandler((ctx, inputs, token) =>
            {
                if (onExecute != null) onExecute();
                return Task.FromResult(new MRKCommandHandlerResult(
                    new Dictionary<string, object> {
                        { "Output", (double)inputs["Input"] * (double)inputs["Factor"] }
                    }));
            });
        }

        private static MRKCommandRequest Request(double input, Guid id = default(Guid))
        {
            return new MRKCommandRequest("TCT/Command", "Multiply",
                new Dictionary<string, object> { { "Input", input } },
                caller: "TCT.Command.Cases", operationId: id, expectedSchemaVersion: 1,
                commandId: 970101);
        }

        [TCTTest("TCT.CMD.001", "Schema默认值、范围拒绝和真实Handler执行",
            TCTTestAspect.Mechanism, "Command / Schema",
            ObjectId = "FDCCommandDefinition", Kind = TCTTestKind.Integration)]
        public static void SchemaAndInvalidInputs(TCTTestContext test)
        {
            int executed = 0;
            using (MRKCommandCenter center = new MRKCommandCenter())
            using (center.Register(new MRKCommandDescriptor("TCT/Command", Definition()),
                Handler(() => Interlocked.Increment(ref executed))))
            {
                MRKCommandResponse valid = center.InvokeAsync(Request(3))
                    .GetAwaiter().GetResult();
                test.AssertEqual(MRKCommandState.Completed, valid.State,
                    "使用真实MRK Handler完成");
                test.AssertEqual(6.0, (double)valid.Outputs["Output"], "默认Factor=2");
                MRKCommandResponse invalid = center.InvokeAsync(Request(300))
                    .GetAwaiter().GetResult();
                test.AssertEqual(MRKCommandStage.Rejected, invalid.Stage,
                    "超出输入范围在Handler之前被拒绝");
                test.AssertEqual("InvalidInput", invalid.ErrorCode, "严格Input校验");
                test.AssertEqual(1, executed, "无效输入不得产生执行副作用");
                MRKCommandResponse version = center.InvokeAsync(
                    new MRKCommandRequest("TCT/Command", "Multiply",
                        new Dictionary<string, object> { { "Input", 3.0 } },
                        expectedSchemaVersion: 999)).GetAwaiter().GetResult();
                test.AssertEqual("SchemaVersionMismatch", version.ErrorCode,
                    "错误Schema版本应明确拒绝");
                test.AssertEqual(1, executed, "Schema拒绝不能执行Handler");
                test.Observe("Command", "Schema验证事实",
                    "Completed Output=6; InvalidInput/SchemaVersionMismatch both rejected; executions=1");
            }
        }

        [TCTTest("TCT.CMD.002", "真实批量Operation独立身份及结果关联",
            TCTTestAspect.Mechanism, "Command / Batch",
            ObjectId = "MRKCommandCenter", Kind = TCTTestKind.Integration)]
        public static void BatchAndCorrelation(TCTTestContext test)
        {
            int executed = 0;
            using (MRKCommandCenter center = new MRKCommandCenter())
            using (center.Register(new MRKCommandDescriptor("TCT/Command", Definition(),
                policy: new MRKCommandPolicy(12)), Handler(() => Interlocked.Increment(ref executed))))
            {
                MRKCommandRequest[] requests = Enumerable.Range(1, 8)
                    .Select(i => Request((double)i)).ToArray();
                IReadOnlyList<MRKCommandResponse> result = center.InvokeBatchAsync(requests)
                    .GetAwaiter().GetResult();
                test.AssertEqual(8, result.Count, "调用正式MRK批量API返回8个结果");
                test.AssertEqual(8, result.Select(r => r.OperationId).Distinct().Count(),
                    "每一批量任务均拥有独立OperationId");
                for (int i = 0; i < result.Count; ++i)
                {
                    test.AssertEqual(requests[i].OperationId, result[i].OperationId,
                        "OperationId关联保持顺序" + i);
                    test.AssertEqual(MRKCommandState.Completed, result[i].State,
                        "单个批量任务状态" + i);
                    test.AssertEqual(2.0 * (i + 1), (double)result[i].Outputs["Output"],
                        "输出跟随对应输入" + i);
                }
                test.AssertEqual(8, executed, "不重复执行业务Handler");
                test.Observe("Command", "Batch真实结果",
                    "8 independent operations completed, correct mapping, executed=" + executed);
            }
        }

        [TCTTest("TCT.CMD.003", "Operation幂等重放不重复执行与参数冲突",
            TCTTestAspect.Mechanism, "Command / Reliability",
            ObjectId = "MRKCommandExecution", Kind = TCTTestKind.Integration)]
        public static void ReplayAndConflict(TCTTestContext test)
        {
            Guid operationId = Guid.NewGuid();
            int executed = 0;
            using (MRKCommandCenter center = new MRKCommandCenter())
            using (center.Register(new MRKCommandDescriptor("TCT/Command", Definition()),
                Handler(() => Interlocked.Increment(ref executed))))
            {
                MRKCommandResponse first = center.InvokeAsync(Request(4, operationId))
                    .GetAwaiter().GetResult();
                MRKCommandResponse replay = center.InvokeAsync(Request(4, operationId))
                    .GetAwaiter().GetResult();
                MRKCommandResponse conflicting = center.InvokeAsync(Request(5, operationId))
                    .GetAwaiter().GetResult();
                test.AssertEqual(8.0, (double)first.Outputs["Output"], "原Operation业务值");
                test.AssertEqual(8.0, (double)replay.Outputs["Output"], "幂等返回原操作");
                test.AssertEqual(first.OperationId, replay.OperationId, "原Operation身份");
                test.AssertEqual("OperationConflict", conflicting.ErrorCode,
                    "同ID不同参数拒绝");
                test.AssertEqual(1, executed, "重放与冲突均不产生二次动作");
            }
        }

        [TCTTest("TCT.CMD.004", "设备命令没有真实授权器时拒绝执行",
            TCTTestAspect.Integration, "Command / Authorization",
            ObjectId = "MRKCommandPolicy", Kind = TCTTestKind.Integration)]
        public static void DeviceAuthorization(TCTTestContext test)
        {
            int executed = 0;
            using (MRKCommandCenter host = new MRKCommandCenter())
            using (host.Register(new MRKCommandDescriptor("TCT/Device",
                Definition(), kind: MRKCommandKind.Device),
                Handler(() => Interlocked.Increment(ref executed))))
            {
                MRKCommandResponse result = host.InvokeAsync(new MRKCommandRequest(
                    "TCT/Device", "Multiply",
                    new Dictionary<string, object> { { "Input", 2.0 } }))
                    .GetAwaiter().GetResult();
                test.AssertEqual(MRKCommandStage.Rejected, result.Stage,
                    "设备Provider没有授权器必须拒绝");
                test.AssertEqual("Unauthorized", result.ErrorCode,
                    "设备授权拒绝码");
                test.AssertEqual(0, executed, "任何未授权命令不得调用设备Handler");
                test.Observe("Safety", "设备调用拒绝", "No authorize delegate: refused before Handler");
            }
        }

        [TCTTest("TCT.CMD.005", "远程未配置Transport不可伪装本地执行",
            TCTTestAspect.Integration, "Command / Remote",
            ObjectId = "MRKCommandPipeClient", Kind = TCTTestKind.Integration)]
        public static void MissingTransport(TCTTestContext test)
        {
            int executed = 0;
            using (MRKCommandCenter center = new MRKCommandCenter())
            using (center.Register(new MRKCommandDescriptor("TCT/Command", Definition()),
                Handler(() => Interlocked.Increment(ref executed))))
            {
                MRKCommandResponse reply = center.InvokeAsync(new MRKCommandRequest(
                    "TCT/Command", "Multiply",
                    new Dictionary<string, object> { { "Input", 3.0 } },
                    options: new MRKCommandOptions(remote: true)))
                    .GetAwaiter().GetResult();
                test.AssertEqual("RemoteUnsupported", reply.ErrorCode,
                    "未接入Transport不能自动降级为本地Handler");
                test.AssertEqual(MRKCommandStage.Rejected, reply.Stage, "明确远程拒绝");
                test.AssertEqual(0, executed, "不应默默执行本地Handler");
            }
        }

        [TCTTest("TCT.CMD.006", "真实Windows命名管道调用与原Operation查询",
            TCTTestAspect.Integration, "Command / IPC",
            ObjectId = "MRKCommandPipeServer", Kind = TCTTestKind.Integration)]
        public static void PipeCallAndQuery(TCTTestContext test)
        {
            string pipe = "CxVisual.TCT.Case." + Guid.NewGuid().ToString("N");
            int invoked = 0;
            using (MRKCommandCenter host = new MRKCommandCenter())
            using (host.Register(new MRKCommandDescriptor("TCT/Command", Definition()),
                Handler(() => Interlocked.Increment(ref invoked))))
            using (MRKCommandPipeServer server = new MRKCommandPipeServer(host, pipe,
                peer => !string.IsNullOrWhiteSpace(peer)))
            {
                server.Start();
                try
                {
                    MRKCommandPipeClient client = new MRKCommandPipeClient(".", pipe);
                    MRKCommandRequest request = new MRKCommandRequest("TCT/Command", "Multiply",
                        new Dictionary<string, object> { { "Input", 5.0 } },
                        operationId: Guid.NewGuid(),
                        options: new MRKCommandOptions(remote: true),
                        expectedSchemaVersion: 1, commandId: 970101);
                    MRKCommandResponse reply = client.InvokeAsync(request).GetAwaiter().GetResult();
                    MRKCommandResponse queried = client.QueryAsync(request.OperationId)
                        .GetAwaiter().GetResult();
                    test.AssertEqual(MRKCommandState.Completed, reply.State, "OS Pipe真实命令完成");
                    test.AssertEqual(10.0, (double)reply.Outputs["Output"], "传输输出值");
                    test.AssertEqual(request.OperationId, queried.OperationId, "Query同一Operation");
                    test.AssertEqual(1, invoked, "不会因为Query重新运行Handler");
                }
                finally { server.StopAsync().GetAwaiter().GetResult(); }
            }
        }

        [TCTTest("TCT.CMD.007", "MDS绑定结果与只读写回失败独立回执",
            TCTTestAspect.Integration, "Command / MDS",
            ObjectId = "MRKCommandMdsBinding", Kind = TCTTestKind.Integration)]
        public static void MdsWritebackReceipt(TCTTestContext test)
        {
            MDSVariableTable table = new MDSVariableTable("TCT.Command.Case." + Guid.NewGuid().ToString("N"));
            table.Define(new MDSVariableDefinition("Input", typeof(double)));
            table.Define(new MDSVariableDefinition("Output", typeof(double)));
            table.Define(new MDSVariableDefinition("ReadOnlyOutput", typeof(double), writable: false));
            table.Update("Input", 4.0);
            table.Update("Output", 0.0);
            int executed = 0;
            using (MRKCommandMdsBinding binding = new MRKCommandMdsBinding())
            using (MRKCommandCenter center = new MRKCommandCenter())
            using (center.Register(new MRKCommandDescriptor("TCT/Command", Definition()),
                Handler(() => Interlocked.Increment(ref executed))))
            {
                binding.BindVariable<double>("input", table, "Input");
                binding.BindVariable<double>("output", table, "Output");
                binding.BindVariable<double>("readonly", table, "ReadOnlyOutput");
                MRKCommandRequest first = new MRKCommandRequest("TCT/Command", "Multiply");
                MRKCommandBindingResult success = center.InvokeBoundResultAsync(first,
                    binding, new[] { new MRKCommandParameterBinding("Input", "input") },
                    new[] { new MRKCommandParameterBinding("Output", "output") })
                    .GetAwaiter().GetResult();
                test.AssertEqual(MRKCommandState.Completed, success.Execution.State,
                    "实际Handler完成");
                test.AssertTrue(success.MappingAttempted && success.MappingSucceeded,
                    "正式MDS输出写回成功", true, success.MappingSucceeded);
                test.AssertEqual(8.0, table.Get<double>("Output"), "MDS变量确实收到结果");

                MRKCommandRequest second = new MRKCommandRequest("TCT/Command", "Multiply");
                MRKCommandBindingResult refused = center.InvokeBoundResultAsync(second,
                    binding, new[] { new MRKCommandParameterBinding("Input", "input") },
                    new[] { new MRKCommandParameterBinding("Output", "readonly") })
                    .GetAwaiter().GetResult();
                test.AssertEqual(MRKCommandState.Completed, refused.Execution.State,
                    "写回失败不等于Handler失败");
                test.AssertTrue(refused.MappingAttempted && !refused.MappingSucceeded,
                    "拒绝只读MDS输出", true, refused.MappingSucceeded);
                test.AssertEqual("OutputMappingFailed", refused.MappingErrorCode,
                    "输出Owner拒绝必须独立报告");
                test.AssertEqual(2, executed, "两次明确调用、没有失败后的隐式重试");
            }
        }

        [TCTTest("TCT.CMD.008", "最大并发满载时拒绝新命令且不产生副作用",
            TCTTestAspect.Mechanism, "Command / Concurrency",
            ObjectId = "MRKCommandPolicy", Kind = TCTTestKind.Integration)]
        public static void ConcurrentBudget(TCTTestContext test)
        {
            int calls = 0;
            TaskCompletionSource<bool> entered = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> release = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using (MRKCommandCenter center = new MRKCommandCenter())
            using (center.Register(new MRKCommandDescriptor("TCT/Command", Definition(),
                policy: new MRKCommandPolicy(1)),
                new MRKDelegateCommandHandler(async (ctx, args, token) =>
                {
                    Interlocked.Increment(ref calls);
                    entered.TrySetResult(true);
                    await release.Task.ConfigureAwait(false);
                    return new MRKCommandHandlerResult(new Dictionary<string, object> {
                        { "Output", (double)args["Input"] * 2.0 }
                    });
                })))
            {
                Task<MRKCommandResponse> running = center.InvokeAsync(Request(3));
                try
                {
                    test.AssertTrue(entered.Task.Wait(TimeSpan.FromSeconds(3)),
                        "第一个Handler已占用并发槽", true, entered.Task.IsCompleted);
                    MRKCommandResponse refused = center.InvokeAsync(Request(4))
                        .GetAwaiter().GetResult();
                    test.AssertEqual(MRKCommandStage.Rejected, refused.Stage, "并发容量拒绝");
                    test.AssertEqual("Busy", refused.ErrorCode, "有限槽位拒绝码");
                    test.AssertEqual(1, calls, "被拒绝的第二次调用不能执行");
                }
                finally { release.TrySetResult(true); }
                MRKCommandResponse settled = running.GetAwaiter().GetResult();
                test.AssertEqual(MRKCommandState.Completed, settled.State,
                    "释放阻塞后原Operation正常结束");
            }
        }

        [TCTTest("TCT.CMD.009", "协作式暂停恢复取消不等于硬件安全停机",
            TCTTestAspect.Mechanism, "Command / Lifecycle",
            ObjectId = "MRKCommandExecution", Kind = TCTTestKind.Integration)]
        public static void CooperativeLifecycle(TCTTestContext test)
        {
            TaskCompletionSource<bool> entered = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using (MRKCommandCenter center = new MRKCommandCenter())
            using (center.Register(new MRKCommandDescriptor("TCT/Command", Definition(),
                policy: new MRKCommandPolicy(1, supportsCancellation: true, supportsPause: true)),
                new MRKDelegateCommandHandler(async (ctx, args, cancellation) =>
                {
                    entered.TrySetResult(true);
                    while (true)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        await ctx.WaitWhilePausedAsync(cancellation).ConfigureAwait(false);
                        await Task.Delay(15, cancellation).ConfigureAwait(false);
                    }
                })))
            {
                MRKCommandRequest req = Request(4);
                Task<MRKCommandResponse> pending = center.InvokeAsync(req);
                try
                {
                    test.AssertTrue(entered.Task.Wait(TimeSpan.FromSeconds(3)),
                        "真实长任务Handler开始", true, entered.Task.IsCompleted);
                    test.AssertTrue(center.Pause(req.OperationId), "协作暂停请求被接受",
                        true, true);
                    MRKCommandState state = MRKCommandState.Running;
                    for (int i = 0; i < 80; ++i)
                    {
                        state = center.Query(req.OperationId).State;
                        if (state == MRKCommandState.Paused) break;
                        Thread.Sleep(15);
                    }
                    test.AssertEqual(MRKCommandState.Paused, state,
                        "只有到达Handler检查点才能确认Paused");
                    test.AssertTrue(center.Resume(req.OperationId), "恢复协作控制", true, true);
                    test.AssertTrue(center.Cancel(req.OperationId), "发送协作取消", true, true);
                    MRKCommandResponse outcome = pending.GetAwaiter().GetResult();
                    test.AssertEqual(MRKCommandState.Cancelled, outcome.State,
                        "原Operation协作结算取消；不证明PLC停机");
                }
                finally { center.Cancel(req.OperationId); }
            }
        }

        [TCTTest("TCT.CMD.010", "输出Schema违约由MRK隔离而非虚报成功",
            TCTTestAspect.Mechanism, "Command / Output",
            ObjectId = "FDCCommandDefinition", Kind = TCTTestKind.Integration)]
        public static void InvalidOutputSchema(TCTTestContext test)
        {
            using (MRKCommandCenter center = new MRKCommandCenter())
            using (center.Register(new MRKCommandDescriptor("TCT/Command", Definition()),
                new MRKDelegateCommandHandler((ctx, args, token) =>
                    Task.FromResult(new MRKCommandHandlerResult(
                        new Dictionary<string, object> { { "Output", "bad-double" } })))))
            {
                MRKCommandResponse reply = center.InvokeAsync(Request(3))
                    .GetAwaiter().GetResult();
                test.AssertEqual(MRKCommandState.Failed, reply.State,
                    "Schema类型违约绝不回报Completed");
                test.AssertEqual(0, reply.Outputs.Count, "错误输出不能泄露为有效Output");
                test.AssertTrue(!string.IsNullOrWhiteSpace(reply.ErrorCode),
                    "保留失败码供专业诊断", true, reply.ErrorCode);
            }
        }
    }
}
