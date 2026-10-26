using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Commands;
using CxVisual.Mechanism.Runtime.Commands;
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
    }
}
