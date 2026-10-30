using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Commands;
using CxVisual.Mechanism.Runtime.Commands;
using CxVisual.Mechanism.Runtime.Execution;
using CxVisual.Professional.Automation.DeviceVariables;

namespace CxVisual.Professional.Automation.Plc
{
    /// <summary>
    /// Automation PLC专业动作贡献给现有MRK命令中心的注册适配。
    /// 不持有第二套命令队列、授权表或资源租约。
    /// 目前正式验证范围仅为Modbus UInt16寄存器写入及读回一致，不表示物理执行器已动作。
    /// </summary>
    public static class PADPlcMrkCommandRegistration
    {
        public const string WriteUInt16Code = "WriteUInt16";
        public const string WritePermission = "Automation.Plc.Write";

        public static string TargetForDevice(string deviceRef)
        {
            if (string.IsNullOrWhiteSpace(deviceRef))
                throw new ArgumentException("PLC设备引用不能为空", "deviceRef");
            return "Automation/PLC/" + deviceRef.Trim();
        }

        public static string ResourceForDevice(string deviceRef)
        {
            if (string.IsNullOrWhiteSpace(deviceRef))
                throw new ArgumentException("PLC设备引用不能为空", "deviceRef");
            return "Automation.PLC.Device/" + deviceRef.Trim();
        }

        /// <summary>
        /// 将一个设备的强类型写能力注册给Host提供的唯一MRKCommandCenter。
        /// Host必须提供可信的MRK授权委托，并复用同一MRKResourceLeaseCoordinator；
        /// 不能将Caller字符串本身当作可信身份。
        /// </summary>
        public static IDisposable RegisterUInt16Write(MRKCommandCenter center,
            PADDeviceVariableProvider provider, string deviceRef, long commandId,
            string approvedVersion = null, Func<bool> effectiveStillApproved = null,
            Func<string> currentSessionGeneration = null)
        {
            if (center == null) throw new ArgumentNullException("center");
            if (provider == null) throw new ArgumentNullException("provider");
            if (commandId <= 0) throw new ArgumentOutOfRangeException("commandId");

            bool pinned = !string.IsNullOrWhiteSpace(approvedVersion);
            if (!pinned && (effectiveStillApproved != null ||
                currentSessionGeneration != null))
                throw new ArgumentException("必须先提供被批准的运行配置版本");
            string target = TargetForDevice(deviceRef);
            List<FDCCommandParameterDefinition> arguments =
                new List<FDCCommandParameterDefinition> {
                    new FDCCommandParameterDefinition("StableId", typeof(string), true),
                    new FDCCommandParameterDefinition("Value", typeof(ushort), true)
                };
            if (pinned)
                arguments.Add(new FDCCommandParameterDefinition(
                    "EffectiveVersion", typeof(string), true));
            // Runtime SCADA session has a live F17 Generation that is only
            // known after Open; read it at invocation, never capture a
            // constructor-time GUID or accept a stale HMI context.
            if(currentSessionGeneration!=null)
                arguments.Add(new FDCCommandParameterDefinition(
                    "SessionGeneration",typeof(string),true));
            FDCCommandDefinition definition = new FDCCommandDefinition(
                commandId, WriteUInt16Code, "PLC UInt16寄存器受控写入与回读验证",
                arguments,
                new[] {
                    new FDCCommandParameterDefinition("RegisterReadbackMatched", typeof(bool), true)
                },
                permissionKey: WritePermission);
            MRKCommandPolicy policy = new MRKCommandPolicy(
                maxConcurrent: 1, resourceId: ResourceForDevice(deviceRef),
                dispatch: MRKCommandDispatch.ThreadPool,
                supportsCancellation: false, supportsPause: false,
                retryPolicy: new MRKExecutionPolicy(maxAttempts: 1, idempotent: false));
            MRKCommandDescriptor descriptor = new MRKCommandDescriptor(
                target, definition, MRKCommandKind.Device, policy,
                new[] { "Automation.Plc.WriteRegisterUInt16" });

            return center.Register(descriptor, new MRKDelegateCommandHandler(
                async (context, inputs, cancellation) =>
                {
                    if (pinned)
                    {
                        object requested;
                        if (!inputs.TryGetValue("EffectiveVersion", out requested) ||
                            !string.Equals(requested as string, approvedVersion,
                                StringComparison.Ordinal) ||
                            (effectiveStillApproved != null && !effectiveStillApproved()))
                            throw new InvalidOperationException(
                                "scada.command.effective_version_mismatch");
                    }
                    if(currentSessionGeneration!=null)
                    {
                        object expectedGeneration;
                        string actual=currentSessionGeneration();
                        if(!inputs.TryGetValue("SessionGeneration",out expectedGeneration)||
                            String.IsNullOrWhiteSpace(actual)||
                            !String.Equals(expectedGeneration as string,actual,
                                StringComparison.Ordinal))
                            throw new InvalidOperationException(
                                "scada.command.session_generation_mismatch");
                    }
                    string stableId = (string)inputs["StableId"];
                    ushort value = (ushort)inputs["Value"];
                    try
                    {
                        bool verified = await provider.WriteAsync(stableId, value,
                            context.Caller, cancellation).ConfigureAwait(false);
                        return new MRKCommandHandlerResult(
                            new Dictionary<string, object> {
                                { "RegisterReadbackMatched", verified }
                            },
                            businessSuccess: verified,
                            businessCode: verified ? "PLC_REGISTER_READBACK_CONFIRMED" :
                                "PLC_REGISTER_NOT_CONFIRMED");
                    }
                    catch (PADPlcOutcomeUnknownException)
                    {
                        // MRK Completed只说明Handler已返回。业务结果必须明确Unknown；
                        // 不把寄存器写回读结果冒充运动/气缸/X-Ray等动作PhysicalDone。
                        // 现场结果不明时不能由此Handler重试，必须另外重新观测。
                        return new MRKCommandHandlerResult(
                            new Dictionary<string, object> {
                                { "RegisterReadbackMatched", false }
                            },
                            businessSuccess: false,
                            businessCode: "PLC_REGISTER_OUTCOME_UNKNOWN");
                    }
                }));
        }
    }
}
