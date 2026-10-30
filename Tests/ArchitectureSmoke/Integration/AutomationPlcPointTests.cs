using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Foundation.Transport;
using CxVisual.Mechanism.Connectivity;
using CxVisual.Mechanism.Core.Modules;
using CxVisual.Mechanism.Data;
using CxVisual.Mechanism.Data.Variables;
using CxVisual.Mechanism.Runtime.Commands;
using CxVisual.Mechanism.Runtime.Execution;
using CxVisual.Professional.Automation.DeviceVariables;
using CxVisual.Professional.Automation.Plc;

namespace CxVisual.Tests.ArchitectureSmoke
{
    /// <summary>真实Modbus MBAP/FC03/FC06通过本机TCP回环、F17 TCP、MPA、MCN、MDS与Automation授权路径，
    /// 不使用伪造的Connector、SDK或“假通过”。G1；G2/G3需现场实机。</summary>
    internal static class AutomationPlcPointTests
    {
        private static void Check(bool ok, string message)
        {
            if (!ok) throw new InvalidOperationException("AUT-PLC-G1: " + message);
        }
        private static void Reject<TException>(Action action) where TException : Exception
        {
            try { action(); }
            catch (TException) { return; }
            throw new InvalidOperationException("AUT-PLC-G1: expected " + typeof(TException).Name);
        }

        // 真正的Decision/MRK执行引擎运行PLC Capability；设备命令仍由
        // 唯一MRKCommandCenter拥有，结果不能把寄存器回读等同物理动作完成。
        private static MRKFlowResult ExecutePinnedCapability(MRKCommandCenter commands,
            string commandEffectiveVersion)
        {
            PADPlcMrkWriteCapabilityProvider provider=
                new PADPlcMrkWriteCapabilityProvider(commands,
                    "plc-versioned","operator","plc-capability-contract-v1",
                    commandEffectiveVersion:commandEffectiveVersion);
            MRKResolvedCapability binding;
            Check(provider.TryResolve(
                PADPlcMrkWriteCapabilityProvider.CapabilityId,out binding),
                "受控PLC Capability无法从专业Owner解析");
            MRKExecutionPlan plan=new MRKExecutionPlan("scada-pinned-plc-command",
                new[]{new MRKExecutionStep("pinned-plc",new string[0],
                    binding.ExecuteAsync,
                    policy:new MRKExecutionPolicy(maxAttempts:1,idempotent:false),
                    inputs:new[]{
                        new MRKExecutionInput("StableId",MRKInputSource.FlowInput,
                            "StableId",typeof(string)),
                        new MRKExecutionInput("Value",MRKInputSource.FlowInput,
                            "Value",typeof(ushort))
                    },output:binding.Output,dataOutputs:binding.DataOutputs)});
            MRKExecutionEngine engine=new MRKExecutionEngine(new FCRWorkerOptions(1,8));
            try
            {
                MRKFlowHandle flow=engine.StartFlow(plan,new Dictionary<string,object>{
                    {"StableId","word"},{"Value",(ushort)91}
                });
                Check(flow.Completion.Wait(TimeSpan.FromSeconds(12)),
                    "正式版本锁定PLC Capability未在有界时间结算");
                return flow.Completion.GetAwaiter().GetResult();
            }
            finally{engine.StopAsync(true).GetAwaiter().GetResult();}
        }

        public static void Run()
        {
            // 使用真实OS TCP Socket，而非绕过MCN Connector的Modbus TCP Channel类型约束。
            // 模拟器只提供现场寄存器报文行为，不实现/替换正式F17、MCN或MPA。
            using (TcpPlcLoopback plc = new TcpPlcLoopback())
            {
            MPAProtocolConfiguration protocolConfig = new MPAProtocolConfiguration("aut-modbus", "modbus.tcp");
            protocolConfig.TimeoutMs = 500;
            protocolConfig.Points.Add(new MPAPointDefinition("word100", "holding:100", typeof(ushort))
            {
                StationAddress = "1", CanRead = true, CanWrite = true
            });
            protocolConfig.Mappings.Add(new MPADataMapping {
                PointRef = "word100", TargetKind = MPADataTargetKind.Variable,
                TargetRef = "plc/Station.Word", Direction = MPADataDirection.Inbound,
                Operation = MPADataOperation.WriteValue, Required = true
            });
            protocolConfig.Mappings.Add(new MPADataMapping {
                PointRef = "word100", TargetKind = MPADataTargetKind.Variable,
                TargetRef = "plc/Station.Word", Direction = MPADataDirection.Outbound,
                Operation = MPADataOperation.ReadValue, Required = true
            });
            protocolConfig.Points.Add(new MPAPointDefinition("input101", "holding:101", typeof(ushort))
            {
                StationAddress = "1", CanRead = true, CanWrite = false
            });
            protocolConfig.Mappings.Add(new MPADataMapping {
                PointRef = "input101", TargetKind = MPADataTargetKind.Variable,
                TargetRef = "plc/Station.Input", Direction = MPADataDirection.Inbound,
                Operation = MPADataOperation.WriteValue, Required = true
            });
            MCNConnectorConfiguration connectorConfig = new MCNConnectorConfiguration {
                DisplayName = "AUT-PLC Connector",
                Channel = new FTRTcpClientChannelConfiguration {
                    Id = "aut-plc-channel", Kind = FTRChannelKind.TcpClient,
                    Host = "127.0.0.1", Port = plc.Port, ConnectTimeoutMs = 2000
                },
                Protocol = protocolConfig
            };
            Reject<ArgumentException>(() => new MCNConnectorConfiguration {
                DisplayName = "禁用虚拟Modbus TCP Connector",
                Channel = new FTRVirtualChannelConfiguration { Id = "invalid-virtual", Kind = FTRChannelKind.Virtual },
                Protocol = protocolConfig
            }.Validate());
            MCNConnectorModule connector = new MCNConnectorModule(
                connectorConfig.Describe("aut-plc-connector", MFMModuleKind.Connector),
                connectorConfig, new FTRTcpClientChannel(connectorConfig.Channel), new MPAModbusTcpProtocol());

            MDSVariableRegistry registry = new MDSVariableRegistry();
            MDSVariableTable table = new MDSVariableTable("plc");
            PADDeviceVariableCatalog catalog = new PADDeviceVariableCatalog("aut-plc");
            catalog.Register(new PADDeviceVariableDefinition("word", "Station", "Word",
                typeof(ushort), null, true));
            catalog.Register(new PADDeviceVariableDefinition("input", "Station", "Input",
                typeof(ushort), null, false));
            catalog.InstallDefinitions(table);
            registry.Register(table);
            PADPlcPointBridge bridge = new PADPlcPointBridge(
                connector, catalog, new FTRSubChannelAddress("aut-plc-channel"), "plc",
                new [] { new PADPlcPointBinding("word", "word100"),
                    new PADPlcPointBinding("input", "input101") });
            Reject<ArgumentException>(() => new PADPlcPointBridge(connector, catalog,
                new FTRSubChannelAddress("aut-plc-channel"), "plc",
                new [] { new PADPlcPointBinding("word", "missing-point") }));
            Reject<ArgumentException>(() => new PADPlcPointBridge(connector, catalog,
                new FTRSubChannelAddress("aut-plc-channel"), "wrong-scope",
                new [] { new PADPlcPointBinding("word", "word100") }));
            // 不能将一个PLC测点额外映射到未授权MDS目标或命令中心；
            // 这类隐藏副作用可能使普通读点引起跨站写操作。
            MPADataMapping illicitVariable = new MPADataMapping {
                PointRef = "word100", TargetKind = MPADataTargetKind.Variable,
                TargetRef = "plc/Unexpected", Direction = MPADataDirection.Inbound,
                Operation = MPADataOperation.WriteValue, Required = true
            };
            protocolConfig.Mappings.Add(illicitVariable);
            Reject<ArgumentException>(() => new PADPlcPointBridge(connector, catalog,
                new FTRSubChannelAddress("aut-plc-channel"), "plc",
                new [] { new PADPlcPointBinding("word", "word100") }));
            protocolConfig.Mappings.Remove(illicitVariable);
            MPADataMapping illicitCommand = new MPADataMapping {
                PointRef = "word100", TargetKind = MPADataTargetKind.Command,
                TargetRef = "unsafe-command", Direction = MPADataDirection.Inbound,
                Operation = MPADataOperation.InvokeCommand, Required = true
            };
            protocolConfig.Mappings.Add(illicitCommand);
            Reject<ArgumentException>(() => new PADPlcPointBridge(connector, catalog,
                new FTRSubChannelAddress("aut-plc-channel"), "plc",
                new [] { new PADPlcPointBinding("word", "word100") }));
            protocolConfig.Mappings.Remove(illicitCommand);
            PADDeviceVariableProvider provider = bridge.CreateWriteProvider(
                (principal, variable) => principal == "operator" && variable.StableId == "word");
            using (MDSVariableProviderHost host = new MDSVariableProviderHost(registry))
            {
                host.Register(provider);
                catalog.Bind(host, "plc");
                connector.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
                host.Start();
                try
                {
                    object first = bridge.ReadAndMapAsync("word", CancellationToken.None)
                        .GetAwaiter().GetResult();
                    Check(first is ushort && (ushort)first == 42 &&
                        table.Get<ushort>("Station.Word") == 42 &&
                        plc.Requests == 1 && plc.Writes == 0,
                        "F17/MPA/MCN真实03帧读取→MDS唯一变量");

                    // 只读PLC输入DI不能通过普通MDS.Update写入，必须由设备Provider采样。
                    object input = bridge.ReadAndMapAsync("input", CancellationToken.None)
                        .GetAwaiter().GetResult();
                    Check(input is ushort && (ushort)input == 99 &&
                        table.Get<ushort>("Station.Input") == 99 &&
                        table.Inspect("Station.Input").Quality == MDSVariableQuality.Good,
                        "只读PLC输入应通过MDS ProviderHost接纳真实规约采样");
                    Reject<InvalidOperationException>(() => provider.WriteAsync("input", (ushort)12,
                        "operator", CancellationToken.None).GetAwaiter().GetResult());

                    // 运行态点表发生跨目标或地址漂移时，必须在发送报文前拒绝。
                    protocolConfig.Mappings.Add(illicitCommand);
                    Reject<InvalidOperationException>(() => bridge.ReadAndMapAsync("word",
                        CancellationToken.None).GetAwaiter().GetResult());
                    protocolConfig.Mappings.Remove(illicitCommand);
                    MPAPointDefinition livePoint = protocolConfig.Points[0];
                    string originalAddress = livePoint.Address;
                    livePoint.Address = "holding:101";
                    Reject<InvalidOperationException>(() => bridge.ReadAndMapAsync("word",
                        CancellationToken.None).GetAwaiter().GetResult());
                    livePoint.Address = originalAddress;
                    Check(plc.Requests == 2 && plc.Writes == 0 &&
                        table.Get<ushort>("Station.Word") == 42,
                        "运行态点表漂移应先拒绝，不能触发未授权硬件报文");

                    Reject<UnauthorizedAccessException>(() => provider.WriteAsync("word", (ushort)60,
                        "guest", CancellationToken.None).GetAwaiter().GetResult());
                    Reject<InvalidCastException>(() => provider.WriteAsync("word", 60,
                        "operator", CancellationToken.None).GetAwaiter().GetResult());
                    Check(plc.Writes == 0 && table.Get<ushort>("Station.Word") == 42,
                        "拒绝未授权/错误类型写入，禁止污染MDS");

                    Check(provider.WriteAsync("word", (ushort)55, "operator",
                        CancellationToken.None).GetAwaiter().GetResult() &&
                        plc.Writes == 1 && plc.ActualRegister == 55 &&
                        table.Get<ushort>("Station.Word") == 55,
                        "授权06写命令+协议回显+真实03设备回读一致才报告成功");
                    plc.IgnoreWrite = true;
                    Reject<PADPlcOutcomeUnknownException>(() => provider.WriteAsync("word", (ushort)77,
                        "operator", CancellationToken.None).GetAwaiter().GetResult());
                    Check(plc.ActualRegister == 55 && table.Get<ushort>("Station.Word") == 55 &&
                        plc.Writes == 2, "ACK但硬件寄存器未变必须返回Unknown，不自动重发");

                    // 无新采样时仅改变质量：保留上一条真实寄存器值和采样时刻。
                    IMDSVariableSnapshot beforeTimeout = table.Inspect("Station.Word");
                    plc.IgnoreWrite = false; plc.IgnoreRead = true;
                    Reject<PADPlcOutcomeUnknownException>(() => provider.WriteAsync("word", (ushort)88,
                        "operator", CancellationToken.None).GetAwaiter().GetResult());
                    Check(plc.Writes == 3 && plc.ActualRegister == 88 &&
                        table.Get<ushort>("Station.Word") == 55,
                        "写ACK后读回超时属于Unknown，不能伪造最新MDS成功样本");
                    IMDSVariableSnapshot afterTimeout = table.Inspect("Station.Word");
                    Check(afterTimeout.Quality == MDSVariableQuality.Stale &&
                        afterTimeout.HasValue &&
                        object.Equals(afterTimeout.CurrentObject, (ushort)55) &&
                        afterTimeout.RefreshUtc == beforeTimeout.RefreshUtc &&
                        afterTimeout.Revision > beforeTimeout.Revision,
                        "PLC回读超时应标记MDS Stale，不能刷新真实采样时间");
                    plc.IgnoreRead = false;
                    object recovered = bridge.ReadAndMapAsync("word", CancellationToken.None)
                        .GetAwaiter().GetResult();
                    Check((ushort)recovered == 88 && table.Get<ushort>("Station.Word") == 88 &&
                        table.Inspect("Station.Word").Quality == MDSVariableQuality.Good,
                        "现场重新采样后数据值与MDS Quality必须一起恢复Good");
                    IMDSVariableSnapshot validSample = table.Inspect("Station.Word");
                    Reject<ArgumentOutOfRangeException>(() =>
                        provider.EmitQuality("word", MDSVariableQuality.Good));
                    Check(provider.EmitQuality("word", MDSVariableQuality.Bad),
                        "质量报告需要活动的PAD Provider绑定");
                    IMDSVariableSnapshot badSample = table.Inspect("Station.Word");
                    Check(badSample.Quality == MDSVariableQuality.Bad &&
                        badSample.RefreshUtc == validSample.RefreshUtc &&
                        badSample.ChangeUtc == validSample.ChangeUtc &&
                        badSample.Revision > validSample.Revision &&
                        object.Equals(badSample.CurrentObject, validSample.CurrentObject),
                        "质量-only变更不能伪造物理值和采样时间");
                    Check((ushort)bridge.ReadAndMapAsync("word", CancellationToken.None)
                        .GetAwaiter().GetResult() == 88 &&
                        table.Inspect("Station.Word").Quality == MDSVariableQuality.Good,
                        "真实回读须恢复Good而不能仅凭质量事件恢复Good");
                    // 真实03帧已经送出，但在回包到达前配置被篡改：
                    // 暂存数据不得污染唯一MDS仓，也不得伪报一个有效采样。
                    plc.PauseNextRead();
                    IMDSVariableSnapshot beforeDrift = table.Inspect("Station.Word");
                    Task<object> inFlight = bridge.ReadAndMapAsync("word", CancellationToken.None);
                    Check(plc.WaitUntilReadPaused(), "PLC模拟器未进入待读回状态");
                    livePoint.Address = "holding:101";
                    try
                    {
                        plc.ReleasePausedRead();
                        Reject<InvalidOperationException>(() => inFlight.GetAwaiter().GetResult());
                    }
                    finally
                    {
                        livePoint.Address = originalAddress;
                        plc.ReleasePausedRead();
                    }
                    IMDSVariableSnapshot afterDrift = table.Inspect("Station.Word");
                    Check(afterDrift.Quality == MDSVariableQuality.Stale &&
                        afterDrift.Revision > beforeDrift.Revision &&
                        afterDrift.RefreshUtc == beforeDrift.RefreshUtc &&
                        afterDrift.ChangeUtc == beforeDrift.ChangeUtc &&
                        object.Equals(afterDrift.CurrentObject,beforeDrift.CurrentObject) &&
                        table.Get<ushort>("Station.Word") == 88,
                        "读回中点表漂移必须仅标记Stale，不能写入旧回包或伪刷新采样时间");
                    Check((ushort)bridge.ReadAndMapAsync("word", CancellationToken.None)
                        .GetAwaiter().GetResult() == 88,
                        "点表恢复后必须重新采样，不可复用已拒绝的回包");

                    // 对接正式MRK，而不是在Automation新建命令中心/线程/资源表。
                    // 同一个PLC设备的命令使用Host唯一ResourceLease并且没有自动重试。
                    string plcTarget = PADPlcMrkCommandRegistration.TargetForDevice("station01");
                    string plcResource = PADPlcMrkCommandRegistration.ResourceForDevice("station01");
                    MRKResourceLeaseCoordinator sharedLeases = new MRKResourceLeaseCoordinator();
                    int writesBeforeMrk = plc.Writes;
                    using (MRKCommandCenter insecure = new MRKCommandCenter(leases: sharedLeases))
                    using (PADPlcMrkCommandRegistration.RegisterUInt16Write(
                        insecure, provider, "station01", 970601))
                    {
                        MRKCommandResponse rejected = insecure.InvokeAsync(
                            new MRKCommandRequest(plcTarget,
                                PADPlcMrkCommandRegistration.WriteUInt16Code,
                                new Dictionary<string, object> {
                                    { "StableId", "word" }, { "Value", (ushort)91 }
                                }, caller: "operator")).GetAwaiter().GetResult();
                        Check(rejected.Stage == MRKCommandStage.Rejected &&
                            rejected.ErrorCode == "Unauthorized" && plc.Writes == writesBeforeMrk,
                            "MRK设备命令无Host授权器必须拒绝、不能调用PLC写");
                    }
                    using (MRKCommandCenter commandHost = new MRKCommandCenter(
                        leases: sharedLeases,
                        authorize: ctx => ctx.Caller == "operator" &&
                            ctx.Descriptor.Definition.PermissionKey ==
                                PADPlcMrkCommandRegistration.WritePermission))
                    using (PADPlcMrkCommandRegistration.RegisterUInt16Write(
                        commandHost, provider, "station01", 970601))
                    {
                        MRKCommandResponse rejected = commandHost.InvokeAsync(
                            new MRKCommandRequest(plcTarget,
                                PADPlcMrkCommandRegistration.WriteUInt16Code,
                                new Dictionary<string, object> {
                                    { "StableId", "word" }, { "Value", (ushort)91 }
                                }, caller: "guest")).GetAwaiter().GetResult();
                        Check(rejected.Stage == MRKCommandStage.Rejected &&
                            rejected.ErrorCode == "Unauthorized" && plc.Writes == writesBeforeMrk,
                            "MRK不可信身份在实际写设备以前被拒绝");

                        Guid operationId = Guid.NewGuid();
                        MRKCommandRequest authorized = new MRKCommandRequest(
                            plcTarget, PADPlcMrkCommandRegistration.WriteUInt16Code,
                            new Dictionary<string, object> {
                                { "StableId", "word" }, { "Value", (ushort)91 }
                            }, caller: "operator", operationId: operationId,
                            expectedSchemaVersion: 1);
                        MRKResourceLease externallyHeld = sharedLeases.TryAcquire(plcResource, "line-maintenance");
                        Check(externallyHeld != null, "共享MRK资源租约须可获取");
                        Task<MRKCommandResponse> pending = commandHost.InvokeAsync(authorized);
                        try
                        {
                            Check(plc.Writes == writesBeforeMrk,
                                "同资源租约占用时设备写入不得提前发生");
                        }
                        finally { externallyHeld.Dispose(); }
                        MRKCommandResponse accepted = pending.GetAwaiter().GetResult();
                        Check(accepted.Stage == MRKCommandStage.Accepted &&
                            accepted.State == MRKCommandState.Completed &&
                            accepted.BusinessSuccess == true &&
                            accepted.BusinessCode == "PLC_REGISTER_READBACK_CONFIRMED" &&
                            (bool)accepted.Outputs["RegisterReadbackMatched"] &&
                            plc.ActualRegister == 91 && plc.Writes == writesBeforeMrk + 1,
                            "MRK强类型Schema/双重授权/设备资源租约/FC06+FC03回读链应成功");

                        MRKCommandResponse dedup = commandHost.InvokeAsync(
                            new MRKCommandRequest(plcTarget,
                                PADPlcMrkCommandRegistration.WriteUInt16Code,
                                new Dictionary<string, object> {
                                    { "StableId", "word" }, { "Value", (ushort)91 }
                                }, caller: "operator", operationId: operationId,
                                expectedSchemaVersion: 1)).GetAwaiter().GetResult();
                        Check(dedup.OperationId == operationId &&
                            dedup.State == MRKCommandState.Completed &&
                            plc.Writes == writesBeforeMrk + 1,
                            "MRK同OperationId必须复用已结算响应，不允许重复物理写请求");

                        plc.IgnoreWrite = true;
                        MRKCommandResponse unknown = commandHost.InvokeAsync(
                            new MRKCommandRequest(plcTarget,
                                PADPlcMrkCommandRegistration.WriteUInt16Code,
                                new Dictionary<string, object> {
                                    { "StableId", "word" }, { "Value", (ushort)94 }
                                }, caller: "operator")).GetAwaiter().GetResult();
                        Check(unknown.Stage == MRKCommandStage.Accepted &&
                            unknown.State == MRKCommandState.Completed &&
                            unknown.BusinessSuccess == false &&
                            unknown.BusinessCode == "PLC_REGISTER_OUTCOME_UNKNOWN" &&
                            !(bool)unknown.Outputs["RegisterReadbackMatched"] &&
                            plc.Writes == writesBeforeMrk + 2 && plc.ActualRegister == 91,
                            "MRK Handler可以结算，但未知现场结果不得伪报寄存器确认或自动重试");
                        plc.IgnoreWrite = false;
                        // One MEM/Decision/MRK Plan actually chains real Vision CPU
                        // and this authorized PLC command center over OS TCP.
                        AutomationPlcPlanIntegrationTests.Run(commandHost,
                            sharedLeases,bridge,table,()=>plc.Writes,
                            ()=>plc.ActualRegister,enabled=>plc.IgnoreWrite=enabled);
                    }

                    // SCADA/HMI旧配置版本的写请求须在FC06发送前FailClosed。
                    bool latestApproved=true;
                    int beforePinned=plc.Writes;
                    using (MRKCommandCenter versionedCenter=new MRKCommandCenter(
                        authorize:ctx=>ctx.Caller=="operator"))
                    using (PADPlcMrkCommandRegistration.RegisterUInt16Write(
                        versionedCenter,provider,"plc-versioned",970602,
                        "approved-effective-v1",()=>latestApproved))
                    {
                        string target=PADPlcMrkCommandRegistration.TargetForDevice("plc-versioned");
                        MRKCommandResponse stale=versionedCenter.InvokeAsync(
                            new MRKCommandRequest(target,
                                PADPlcMrkCommandRegistration.WriteUInt16Code,
                                new Dictionary<string,object>{
                                    {"StableId","word"},{"Value",(ushort)91},
                                    {"EffectiveVersion","old-effective-v0"}
                                },caller:"operator")).GetAwaiter().GetResult();
                        Check(stale.State!=MRKCommandState.Completed ||
                            stale.BusinessSuccess!=true,
                            "旧Effective版本不能被登记成设备成功");
                        Check(plc.Writes==beforePinned,
                            "旧HMI设备版本不得发送任何FC06");
                        latestApproved=false;
                        MRKCommandResponse drift=versionedCenter.InvokeAsync(
                            new MRKCommandRequest(target,
                                PADPlcMrkCommandRegistration.WriteUInt16Code,
                                new Dictionary<string,object>{
                                    {"StableId","word"},{"Value",(ushort)91},
                                    {"EffectiveVersion","approved-effective-v1"}
                                },caller:"operator")).GetAwaiter().GetResult();
                        Check((drift.State!=MRKCommandState.Completed ||
                            drift.BusinessSuccess!=true) && plc.Writes==beforePinned,
                            "配置已经变化，即使客户端提交旧审批版本也须拒绝发FC06");
                        latestApproved=true;
                        MRKCommandResponse current=versionedCenter.InvokeAsync(
                            new MRKCommandRequest(target,
                                PADPlcMrkCommandRegistration.WriteUInt16Code,
                                new Dictionary<string,object>{
                                    {"StableId","word"},{"Value",(ushort)91},
                                    {"EffectiveVersion","approved-effective-v1"}
                                },caller:"operator")).GetAwaiter().GetResult();
                        Check(current.BusinessSuccess==true &&
                            current.BusinessCode=="PLC_REGISTER_READBACK_CONFIRMED" &&
                            plc.Writes==beforePinned+1,
                            "当前Effective版本且有可信MRK/PAD授权才执行FC06+FC03");

                        // 真实MOC/Decision能力执行也必须携带命令层MHS Effective。
                        // 此前适配器只传StableId/Value，无法调成功该正式必填Schema。
                        int beforeCapability=plc.Writes;
                        MRKFlowResult pinnedGood=ExecutePinnedCapability(
                            versionedCenter,"approved-effective-v1");
                        Check(pinnedGood.State==MRKFlowState.Completed &&
                            pinnedGood.Results.ContainsKey("pinned-plc") &&
                            object.Equals(pinnedGood.Results["pinned-plc"],true) &&
                            plc.Writes==beforeCapability+1 &&
                            plc.ActualRegister==91,
                            "Decision→MRK版本锁定真实FC06+FC03不完整");

                        int beforeBadVersion=plc.Writes;
                        MRKFlowResult pinnedBad=ExecutePinnedCapability(
                            versionedCenter,"old-effective-v0");
                        Check(pinnedBad.State==MRKFlowState.Failed &&
                            !pinnedBad.Results.ContainsKey("pinned-plc") &&
                            plc.Writes==beforeBadVersion,
                            "错误命令Effective版本不得执行FC06或输出成功");

                        MRKFlowResult missingPin=ExecutePinnedCapability(
                            versionedCenter,null);
                        Check(missingPin.State==MRKFlowState.Failed &&
                            !missingPin.Results.ContainsKey("pinned-plc") &&
                            plc.Writes==beforeBadVersion,
                            "正式MRK必填Effective不可被老未锁定Capability绕过");

                        latestApproved=false;
                        try
                        {
                            MRKFlowResult pinnedDrift=ExecutePinnedCapability(
                                versionedCenter,"approved-effective-v1");
                            Check(pinnedDrift.State==MRKFlowState.Failed &&
                                !pinnedDrift.Results.ContainsKey("pinned-plc") &&
                                plc.Writes==beforeBadVersion,
                                "工程配置漂移后即使提交旧批准版本也不能经Flow发FC06");
                        }
                        finally{latestApproved=true;}
                    }

                    // A4受信会话版本栅栏：即使Connector Effective不变，
                    // F17 Open换代也必须拒绝旧HMI Command Generation。
                    string activeGeneration=Guid.NewGuid().ToString("N");
                    int generationWrites=plc.Writes;
                    using(MRKCommandCenter generationCenter=new MRKCommandCenter(
                        authorize:ctx=>ctx.Caller=="operator"))
                    using(PADPlcMrkCommandRegistration.RegisterUInt16Write(
                        generationCenter,provider,"plc-generation",970603,
                        "approved-effective-v1",()=>true,()=>activeGeneration))
                    {
                        string target=PADPlcMrkCommandRegistration.TargetForDevice(
                            "plc-generation");
                        Func<string,MRKCommandResponse> send=expectedGeneration=>
                            generationCenter.InvokeAsync(new MRKCommandRequest(target,
                                PADPlcMrkCommandRegistration.WriteUInt16Code,
                                new Dictionary<string,object>{
                                    {"StableId","word"},{"Value",(ushort)91},
                                    {"EffectiveVersion","approved-effective-v1"},
                                    {"SessionGeneration",expectedGeneration}
                                },caller:"operator",expectedSchemaVersion:1))
                                .GetAwaiter().GetResult();
                        MRKCommandResponse staleGeneration=send(Guid.NewGuid().ToString("N"));
                        Check(staleGeneration.BusinessSuccess!=true &&
                            plc.Writes==generationWrites,
                            "同一Effective的旧会话Generation必须在真实FC06前拒绝");
                        MRKCommandResponse correctGeneration=send(activeGeneration);
                        Check(correctGeneration.BusinessSuccess==true &&
                            correctGeneration.BusinessCode=="PLC_REGISTER_READBACK_CONFIRMED" &&
                            plc.Writes==generationWrites+1,
                            "MRK双版本匹配应有且仅有一笔FC06+FC03回读");
                        string previousGeneration=activeGeneration;
                        activeGeneration=Guid.NewGuid().ToString("N");
                        MRKCommandResponse afterReconnection=send(previousGeneration);
                        Check(afterReconnection.BusinessSuccess!=true &&
                            plc.Writes==generationWrites+1,
                            "旧Generation不得在新的F17连接代次执行写入");
                    }

                    // 专业多点扫描不是Modbus单报文合并批读：真实TCP逐点FC03，
                    // 一点失败不能伪装全成功，也不能覆盖上一笔MDS有效值。
                    PADPlcScanResult whole = bridge.ScanAsync(
                        new[] { "input", "word" }, CancellationToken.None).GetAwaiter().GetResult();
                    Check(whole.AllSucceeded && whole.Points.Count == 2 &&
                        whole.SucceededCount == 2 && whole.FailedCount == 0 &&
                        whole.Points[0].StableId == "input" &&
                        (ushort)whole.Points[0].Value == 99 &&
                        whole.Points[1].StableId == "word" &&
                        (ushort)whole.Points[1].Value == 91 &&
                        table.Inspect("Station.Input").Quality == MDSVariableQuality.Good,
                        "两个PLC寄存器须按配置顺序实际读取并写回唯一MDS仓");

                    int requestsBeforePreflight = plc.Requests;
                    Reject<ArgumentException>(() => bridge.ScanAsync(
                        new[] { "input", "input" }, CancellationToken.None).GetAwaiter().GetResult());
                    Reject<ArgumentOutOfRangeException>(() => bridge.ScanAsync(
                        new string[0], CancellationToken.None).GetAwaiter().GetResult());
                    Reject<KeyNotFoundException>(() => bridge.ScanAsync(
                        new[] { "input", "unknown-point" }, CancellationToken.None).GetAwaiter().GetResult());
                    using (CancellationTokenSource cancelled = new CancellationTokenSource())
                    {
                        cancelled.Cancel();
                        Reject<OperationCanceledException>(() => bridge.ScanAsync(
                            new[] { "input", "word" }, cancelled.Token).GetAwaiter().GetResult());
                    }
                    Check(plc.Requests == requestsBeforePreflight,
                        "非法、重复或取消的点集合必须在网络IO以前拒绝");

                    IMDSVariableSnapshot inputBeforeDrop = table.Inspect("Station.Input");
                    plc.DropNextInputRead();
                    PADPlcScanResult partial = bridge.ScanAsync(
                        new[] { "input", "word" }, CancellationToken.None).GetAwaiter().GetResult();
                    IMDSVariableSnapshot inputAfterDrop = table.Inspect("Station.Input");
                    Check(!partial.AllSucceeded && partial.SucceededCount == 1 &&
                        partial.FailedCount == 1 && partial.Points.Count == 2 &&
                        partial.Points[0].StableId == "input" &&
                        !partial.Points[0].Succeeded && partial.Points[0].FailureCode == "Timeout" &&
                        partial.Points[0].Value == null &&
                        partial.Points[1].Succeeded && partial.Points[1].FailureCode == "" &&
                        (ushort)partial.Points[1].Value == 91 &&
                        inputAfterDrop.Quality == MDSVariableQuality.Stale &&
                        inputAfterDrop.RefreshUtc == inputBeforeDrop.RefreshUtc &&
                        object.Equals(inputAfterDrop.CurrentObject, inputBeforeDrop.CurrentObject) &&
                        table.Inspect("Station.Word").Quality == MDSVariableQuality.Good,
                        "单点超时只隔离该点并标Stale，仍可采集后续正常点");
                    PADPlcScanResult rescanned = bridge.ScanAsync(
                        new[] { "input" }, CancellationToken.None).GetAwaiter().GetResult();
                    Check(rescanned.AllSucceeded && rescanned.SucceededCount == 1 &&
                        table.Inspect("Station.Input").Quality == MDSVariableQuality.Good,
                        "只允许新的实际FC03成功采样将失败点恢复Good");

                    // 连续holding:100/101必须仅发送一个真实FC03数量=2的请求。
                    int requestsBeforeBlock = plc.Requests;
                    int blockReadsBefore = plc.BlockReads;
                    PADPlcScanResult contiguous = bridge.ScanContiguousAsync(
                        new[] { "word", "input" }, CancellationToken.None).GetAwaiter().GetResult();
                    Check(contiguous.AllSucceeded && contiguous.Points.Count == 2 &&
                        (ushort)contiguous.Points[0].Value == 91 &&
                        (ushort)contiguous.Points[1].Value == 99 &&
                        plc.Requests == requestsBeforeBlock + 1 &&
                        plc.BlockReads == blockReadsBefore + 1 &&
                        table.Inspect("Station.Word").Quality == MDSVariableQuality.Good &&
                        table.Inspect("Station.Input").Quality == MDSVariableQuality.Good,
                        "相邻寄存器必须通过单个MBAP FC03(count=2)取回两个强类型结果");

                    int noPacketBefore = plc.Requests;
                    Reject<ArgumentException>(() => bridge.ScanContiguousAsync(
                        new[] { "input", "word" }, CancellationToken.None).GetAwaiter().GetResult());
                    Reject<ArgumentException>(() => bridge.ScanContiguousAsync(
                        new[] { "word", "word" }, CancellationToken.None).GetAwaiter().GetResult());
                    Reject<ArgumentOutOfRangeException>(() => bridge.ScanContiguousAsync(
                        new string[0], CancellationToken.None).GetAwaiter().GetResult());
                    Check(plc.Requests == noPacketBefore,
                        "连续批读反向地址/重复/空集合不能发送PLC请求");

                    IMDSVariableSnapshot wordBeforeBroken = table.Inspect("Station.Word");
                    IMDSVariableSnapshot inputBeforeBroken = table.Inspect("Station.Input");
                    plc.CorruptNextBlockRead();
                    Reject<InvalidDataException>(() => bridge.ScanContiguousAsync(
                        new[] { "word", "input" }, CancellationToken.None).GetAwaiter().GetResult());
                    IMDSVariableSnapshot wordAfterBroken = table.Inspect("Station.Word");
                    IMDSVariableSnapshot inputAfterBroken = table.Inspect("Station.Input");
                    Check(wordAfterBroken.Quality == MDSVariableQuality.Stale &&
                        inputAfterBroken.Quality == MDSVariableQuality.Stale &&
                        wordAfterBroken.RefreshUtc == wordBeforeBroken.RefreshUtc &&
                        inputAfterBroken.RefreshUtc == inputBeforeBroken.RefreshUtc &&
                        object.Equals(wordAfterBroken.CurrentObject, wordBeforeBroken.CurrentObject) &&
                        object.Equals(inputAfterBroken.CurrentObject, inputBeforeBroken.CurrentObject),
                        "连续FC03回包缺字节时整批拒绝，两个MDS旧值不得被部分覆盖");
                    PADPlcScanResult retried = bridge.ScanContiguousAsync(
                        new[] { "word", "input" }, CancellationToken.None).GetAwaiter().GetResult();
                    Check(retried.AllSucceeded &&
                        table.Inspect("Station.Word").Quality == MDSVariableQuality.Good &&
                        table.Inspect("Station.Input").Quality == MDSVariableQuality.Good,
                        "批读错误后只有新的完整真实回包才允许恢复Good");

                    // 周期PLC采集复用FCRPeriodicJob + 已通过G1的MCN/MPA/MDS。
                    // 只读工作不得重发FC06，Stop必须等待在途扫描，不留后台请求。
                    int writesBeforePolling = plc.Writes;
                    int readRequestsBeforePolling = plc.Requests;
                    PADPlcPollingSession intervalPoll = new PADPlcPollingSession(
                        bridge, new[] { "input", "word" },
                        TimeSpan.FromMilliseconds(80));
                    Reject<ArgumentException>(() => new PADPlcPollingSession(
                        bridge, new[] { "word", "word" }, TimeSpan.FromMilliseconds(80)));
                    Reject<ArgumentOutOfRangeException>(() => new PADPlcPollingSession(
                        bridge, new[] { "word" }, TimeSpan.FromMilliseconds(1)));
                    intervalPoll.Start();
                    Check(intervalPoll.IsStarted, "启动后周期会话必须处于Running");
                    Reject<InvalidOperationException>(() => intervalPoll.Start());
                    Check(SpinWait.SpinUntil(() => intervalPoll.CompletedCycles >= 2, 5000),
                        "周期顺序轮询必须从真实PLC完成至少两轮FC03");
                    intervalPoll.StopAsync().GetAwaiter().GetResult();
                    Check(!intervalPoll.IsStarted && intervalPoll.Completion.IsCompleted,
                        "停止并等待后不得将周期会话继续标为Running");
                    int requestsAfterPollStop = plc.Requests;
                    Check(intervalPoll.SuccessfulCycles >= 2 &&
                        intervalPoll.PartialCycles == 0 &&
                        requestsAfterPollStop >= readRequestsBeforePolling + 4 &&
                        plc.Writes == writesBeforePolling &&
                        !intervalPoll.Completion.IsFaulted,
                        "周期采集必须复用只读链路与串行设备访问，不允许产生FC06");
                    Thread.Sleep(230);
                    Check(plc.Requests == requestsAfterPollStop,
                        "StopAsync完成后不能遗留后台周期读取");
                    Reject<InvalidOperationException>(() => intervalPoll.Start());

                    // 连续点周期轮询应每轮仅发一个FC03双寄存器事务。
                    int blockBeforePolling = plc.BlockReads;
                    PADPlcPollingSession mergedPoll = new PADPlcPollingSession(
                        bridge, new[] { "word", "input" },
                        TimeSpan.FromMilliseconds(80), contiguous: true);
                    mergedPoll.Start();
                    Check(SpinWait.SpinUntil(() => mergedPoll.CompletedCycles >= 2, 5000),
                        "周期连续批读必须真实完成至少两轮");
                    mergedPoll.StopAsync().GetAwaiter().GetResult();
                    Check(plc.BlockReads >= blockBeforePolling + 2 &&
                        mergedPoll.SuccessfulCycles >= 2 &&
                        plc.Writes == writesBeforePolling,
                        "每轮连续扫描只允许正式Modbus FC03 count=2，不得写入设备");

                    // 一点丢帧仅产生部分成功；之后真实采样恢复Good，周期任务不重入。
                    plc.DropNextInputRead();
                    PADPlcPollingSession partialPoll = new PADPlcPollingSession(
                        bridge, new[] { "input", "word" },
                        TimeSpan.FromMilliseconds(80));
                    partialPoll.Start();
                    Check(SpinWait.SpinUntil(() => partialPoll.PartialCycles >= 1, 5000),
                        "单点超时必须记录本轮Partial，不伪报整轮成功");
                    Check(SpinWait.SpinUntil(() => partialPoll.SuccessfulCycles >= 1, 5000),
                        "周期后续真实采样应恢复失败点Good");
                    partialPoll.StopAsync().GetAwaiter().GetResult();
                    Check(table.Inspect("Station.Input").Quality == MDSVariableQuality.Good &&
                        table.Inspect("Station.Word").Quality == MDSVariableQuality.Good &&
                        plc.Writes == writesBeforePolling,
                        "周期故障隔离及恢复不能修改PLC输出寄存器");

                    // Host没有显式启动时，不允许调用网络；停止未启动的会话可安全清理。
                    int beforeNeverStarted = plc.Requests;
                    PADPlcPollingSession neverStarted = new PADPlcPollingSession(
                        bridge, new[] { "word" }, TimeSpan.FromMilliseconds(200));
                    neverStarted.StopAsync().GetAwaiter().GetResult();
                    Check(plc.Requests == beforeNeverStarted,
                        "没有Start的周期会话必须零网络副作用");
                    Reject<InvalidOperationException>(() => neverStarted.Start());

                    // 周期采样的点表非法漂移必须在首帧以前fail-closed，
                    // 完成任务呈Faulted，且原有Good质量变Stale但没有新增采样。
                    int requestsBeforeDriftPoll = plc.Requests;
                    IMDSVariableSnapshot beforeDriftPoll = table.Inspect("Station.Word");
                    PADPlcPollingSession driftPoll = new PADPlcPollingSession(
                        bridge, new[] { "word" }, TimeSpan.FromMilliseconds(80));
                    string priorAddress = livePoint.Address;
                    livePoint.Address = "holding:106";
                    try
                    {
                        driftPoll.Start(runImmediately: false);
                        Check(SpinWait.SpinUntil(() => driftPoll.Completion.IsCompleted, 5000) &&
                            driftPoll.Completion.IsFaulted && !driftPoll.IsStarted,
                            "周期采集点表漂移应在首轮预检后进入显式Faulted");
                        Reject<InvalidOperationException>(() =>
                            driftPoll.StopAsync().GetAwaiter().GetResult());
                        IMDSVariableSnapshot driftQuality = table.Inspect("Station.Word");
                        Check(plc.Requests == requestsBeforeDriftPoll &&
                            driftPoll.CompletedCycles == 0 &&
                            driftQuality.Quality == MDSVariableQuality.Stale &&
                            driftQuality.RefreshUtc == beforeDriftPoll.RefreshUtc &&
                            object.Equals(driftQuality.CurrentObject, beforeDriftPoll.CurrentObject),
                            "非法周期点表不能发送网络帧或继续保留Good质量");
                    }
                    finally
                    {
                        livePoint.Address = priorAddress;
                    }
                    Check((ushort)bridge.ReadAndMapAsync("word", CancellationToken.None)
                        .GetAwaiter().GetResult() == 91 &&
                        table.Inspect("Station.Word").Quality == MDSVariableQuality.Good,
                        "点表恢复后只有重新读取PLC才能恢复MDS Good");

                    // 连接一旦明确关闭，原Good不能继续用于现场控制；
                    // 质量改变不等于新采样，必须保持最后真实样本的RefreshUtc。
                    IMDSVariableSnapshot beforeClose = table.Inspect("Station.Word");
                    connector.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
                    IMDSVariableSnapshot afterClose = table.Inspect("Station.Word");
                    Check(afterClose.Quality == MDSVariableQuality.Stale &&
                        afterClose.RefreshUtc == beforeClose.RefreshUtc &&
                        afterClose.Revision > beforeClose.Revision &&
                        object.Equals(afterClose.CurrentObject, beforeClose.CurrentObject),
                        "关闭Channel应把已读取的PLC值标记Stale而非延续Good");
                    Check(table.Inspect("Station.Input").Quality == MDSVariableQuality.Stale,
                        "断线时只读PLC输入也必须标记Stale");
                    Console.WriteLine("PASS Automation PLC real TCP loopback Modbus 03/06 -> MDS / authorized readback / Unknown");
                }
                finally
                {
                    host.Stop();
                    if (connector.IsStarted)
                        connector.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
                    bridge.Dispose();
                }
            }
            Reject<InvalidOperationException>(() => bridge.ReadAndMapAsync("word", CancellationToken.None)
                .GetAwaiter().GetResult());
            }
        }

        private sealed class TcpPlcLoopback : IDisposable
        {
            private readonly TcpListener _listener;
            private readonly Task _server;
            private int _register = 42;
            private int _requests;
            private int _writes;
            private volatile bool _ignoreWrite;
            private volatile bool _ignoreRead;
            private int _dropInputReadOnce;
            private int _blockReads;
            private int _corruptBlockReadOnce;
            private volatile bool _pauseRead;
            private readonly ManualResetEventSlim _readPaused = new ManualResetEventSlim(false);
            private readonly ManualResetEventSlim _releaseRead = new ManualResetEventSlim(false);
            private Exception _failure;
            public int Port { get; private set; }
            public ushort ActualRegister { get { return (ushort)Volatile.Read(ref _register); } }
            public int Requests { get { return Volatile.Read(ref _requests); } }
            public int Writes { get { return Volatile.Read(ref _writes); } }
            public int BlockReads { get { return Volatile.Read(ref _blockReads); } }
            public bool IgnoreWrite { get { return _ignoreWrite; } set { _ignoreWrite = value; } }
            public bool IgnoreRead { get { return _ignoreRead; } set { _ignoreRead = value; } }
            public void DropNextInputRead() { Interlocked.Exchange(ref _dropInputReadOnce, 1); }
            public void CorruptNextBlockRead() { Interlocked.Exchange(ref _corruptBlockReadOnce, 1); }
            public void PauseNextRead()
            {
                _readPaused.Reset(); _releaseRead.Reset(); _pauseRead = true;
            }
            public bool WaitUntilReadPaused() { return _readPaused.Wait(2500); }
            public void ReleasePausedRead() { _releaseRead.Set(); }

            public TcpPlcLoopback()
            {
                _listener = new TcpListener(IPAddress.Loopback, 0);
                _listener.Start();
                Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
                _server = Task.Run((Action)Serve);
            }

            private static bool ReadExactly(NetworkStream stream, byte[] bytes)
            {
                int offset = 0;
                while (offset != bytes.Length)
                {
                    int read = stream.Read(bytes, offset, bytes.Length - offset);
                    if (read == 0)
                    {
                        if (offset == 0) return false;
                        throw new EndOfStreamException("Modbus TCP MBAP半帧截断");
                    }
                    offset += read;
                }
                return true;
            }

            private void Serve()
            {
                try
                {
                    using (TcpClient peer = _listener.AcceptTcpClient())
                    using (NetworkStream stream = peer.GetStream())
                    {
                        peer.NoDelay = true;
                        while (true)
                        {
                            byte[] mbap = new byte[7];
                            if (!ReadExactly(stream, mbap)) break;
                            int length = (mbap[4] << 8) | mbap[5];
                            if (length < 2 || length > 254)
                                throw new InvalidDataException("Modbus TCP请求MBAP长度不合法");
                            byte[] pdu = new byte[length - 1];
                            if (!ReadExactly(stream, pdu))
                                throw new EndOfStreamException("Modbus请求PDU截断");
                            byte function = pdu[0];
                            if (function == 3 && IgnoreRead) continue;
                            if (function == 3 && pdu.Length == 5 &&
                                ((pdu[1] << 8) | pdu[2]) == 101 &&
                                Interlocked.Exchange(ref _dropInputReadOnce, 0) == 1) continue;
                            if (function == 3 && _pauseRead)
                            {
                                _pauseRead = false;
                                _readPaused.Set();
                                if (!_releaseRead.Wait(5000))
                                    throw new IOException("PLC测试端被阻断的读取未及时释放");
                            }
                            byte[] reply;
                            if (function == 3 && pdu.Length == 5)
                            {
                                int register = (pdu[1] << 8) | pdu[2];
                                int count = (pdu[3] << 8) | pdu[4];
                                if (count < 1 || count > 125 || register + count > 65536)
                                    throw new InvalidDataException("测试FC03数量或地址非法");
                                if (count > 1) Interlocked.Increment(ref _blockReads);
                                bool corrupt = count > 1 &&
                                    Interlocked.Exchange(ref _corruptBlockReadOnce, 0) == 1;
                                int returned = corrupt ? count - 1 : count;
                                reply = new byte[2 + returned * 2];
                                reply[0] = 3;
                                reply[1] = (byte)(returned * 2);
                                for (int i = 0; i < returned; i++)
                                {
                                    ushort value = register + i == 101 ? (ushort)99 : ActualRegister;
                                    reply[2 + i * 2] = (byte)(value >> 8);
                                    reply[3 + i * 2] = (byte)value;
                                }
                            }
                            else if (function == 6 && pdu.Length == 5)
                            {
                                Interlocked.Increment(ref _writes);
                                ushort value = (ushort)((pdu[3] << 8) | pdu[4]);
                                if (!IgnoreWrite) Volatile.Write(ref _register, value);
                                reply = (byte[])pdu.Clone();
                            }
                            else throw new InvalidDataException("不支持的Modbus测试功能码");
                            Interlocked.Increment(ref _requests);
                            byte[] frame = new byte[7 + reply.Length];
                            Buffer.BlockCopy(mbap, 0, frame, 0, 7);
                            frame[4] = 0;
                            frame[5] = (byte)(reply.Length + 1);
                            Buffer.BlockCopy(reply, 0, frame, 7, reply.Length);
                            stream.Write(frame, 0, frame.Length);
                        }
                    }
                }
                catch (SocketException error) { _failure = error; }
                catch (IOException error) { _failure = error; }
                catch (ObjectDisposedException) { }
            }
            public void Dispose()
            {
                _releaseRead.Set();
                _listener.Stop();
                if (!_server.Wait(3000))
                    throw new TimeoutException("PLC TCP回环服务器未退出");
                _readPaused.Dispose(); _releaseRead.Dispose();
                if (_failure != null && !( _failure is IOException || _failure is SocketException ))
                    throw new InvalidOperationException("PLC TCP模拟端意外失败", _failure);
            }
        }
    }
}
