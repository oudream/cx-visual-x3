using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Decision;
using CxVisual.Mechanism.Orchestration;
using CxVisual.Mechanism.Runtime.Commands;
using CxVisual.Mechanism.Runtime.Execution;
using CxVisual.Mechanism.Data.Variables;
using CxVisual.Professional.Automation.Plc;
using CxVisual.Professional.Vision;

namespace CxVisual.Tests.ArchitectureSmoke
{
    /// <summary>
    /// B3 cross-professional software integration. Captured pixels and the PLC
    /// endpoint are test inputs, but Vision algorithms, MOC, MRK, the command
    /// center, Modbus TCP protocol and MDS are the real production owners.
    /// This is not X-Ray hardware or a safety interlock HIL test.
    /// </summary>
    internal static class AutomationPlcPlanIntegrationTests
    {
        private const string CaptureId="vision.test.plc.raw-capture";
        private const string CaptureVersion="vision.test.plc.raw-capture.v1";
        private const string ApprovedVersion="automation.plc.uint16-write.v1/points:station01.v1";
        private static void Check(bool ok,string message)
        { if(!ok)throw new InvalidOperationException("AUT-PLAN-B3: "+message); }

        private sealed class RawCaptureResolver : IMRKVersionedCapabilityResolver
        {
            private readonly IMRKVersionedCapabilityResolver _next;
            private readonly MRKResolvedCapability _raw;
            internal RawCaptureResolver(IMRKVersionedCapabilityResolver next)
            {
                _next=next;
                byte[] pixels={210,210,0,0,0,210,0,0,0,0,0,0,0,0,250};
                _raw=new MRKResolvedCapability((ctx,ct)=>{
                    ct.ThrowIfCancellationRequested();
                    byte[] value=(byte[])pixels.Clone();
                    ctx.Data.Return("Pixels",value);
                    return Task.FromResult((object)value);
                },new MRKExecutionOutput(typeof(byte[])),
                    dataOutputs:new Dictionary<string,Type>{{"Pixels",typeof(byte[])}},
                    inputs:new MRKCapabilityInput[0]);
            }
            public bool TryResolve(string id,out MRKResolvedCapability result)
            {
                if(id==CaptureId){result=_raw;return true;}
                return _next.TryResolve(id,out result);
            }
            public bool TryGetCapabilityVersion(string id,out string version)
            {
                if(id==CaptureId){version=CaptureVersion;return true;}
                return _next.TryGetCapabilityVersion(id,out version);
            }
        }

        internal static void Run(MRKCommandCenter commands,
            PADPlcPointBridge bridge,MDSVariableTable table,
            Func<int> writes,Func<ushort> actualRegister,Action<bool> ignoreWrite)
        {
            if(commands==null||bridge==null||table==null||writes==null||
                actualRegister==null||ignoreWrite==null)
                throw new ArgumentNullException("B3 mixed-professional prerequisites");
            var options=new CxVisionInspectionOptions{
                PixelDepth=CxVisionPixelDepth.Bits8,GrayThreshold=200,
                MinDefectArea=2,MaxDefectArea=100,UseAi=false};
            var automation=new PADPlcMrkWriteCapabilityProvider(commands,
                "station01","operator",ApprovedVersion);
            var vision=new CxVisionThresholdProvider(options,12,automation);
            var resolver=new RawCaptureResolver(vision);
            var catalog=new MDCStaticCatalogSnapshot(new[]{
                new MOCCapabilityDescriptor(CaptureId,"Test RAW Source",
                    "Vision/TestCapture",new[]{
                        new MPCPortDescriptor("Pixels",MPCPortDirection.Output,
                            "byte[]",MPCPortKind.Data) }),
                vision.DescribeCapabilities().Single(),
                automation.DescribeCapabilities().Single()
            });
            var versions=new Dictionary<string,string>(StringComparer.Ordinal){
                {CaptureId,CaptureVersion},
                {CxVisionThresholdProvider.CapabilityId,vision.ReleaseVersion},
                {PADPlcMrkWriteCapabilityProvider.CapabilityId,ApprovedVersion}
            };
            Guid inspectBlock=Guid.Empty,plcBlock=Guid.Empty;
            MDCDeclarativePlan plan;
            var session=MEMEngineering.Create("B3-vision-plc-real-mixed");
            try
            {
                var root=session.AddPage("Vision-then-PLC").RootContent;
                MOCFlowBuilder.Edit(session,root,flow=>{
                    Guid capture=flow.Step(CaptureId);
                    inspectBlock=flow.Step(CxVisionThresholdProvider.CapabilityId);
                    plcBlock=flow.Step(PADPlcMrkWriteCapabilityProvider.CapabilityId);
                    flow.AddPort(capture,new MPCPortDescriptor("Pixels",
                        MPCPortDirection.Output,"byte[]",MPCPortKind.Data));
                    flow.AddPort(inspectBlock,new MPCPortDescriptor("RawPixels",
                        MPCPortDirection.Input,"byte[]",MPCPortKind.Data,required:true));
                    flow.AddPort(inspectBlock,new MPCPortDescriptor("Width",
                        MPCPortDirection.Input,"int",MPCPortKind.Data,required:true));
                    flow.AddPort(inspectBlock,new MPCPortDescriptor("Height",
                        MPCPortDirection.Input,"int",MPCPortKind.Data,required:true));
                    flow.AddPort(inspectBlock,new MPCPortDescriptor("Inspection",
                        MPCPortDirection.Output,
                        CxVisionAcceptedRegionMeasureProvider.InspectionTypeId,
                        MPCPortKind.Data));
                    flow.AddPort(plcBlock,new MPCPortDescriptor("StableId",
                        MPCPortDirection.Input,"string",MPCPortKind.Data,required:true));
                    flow.AddPort(plcBlock,new MPCPortDescriptor("Value",
                        MPCPortDirection.Input,"ushort",MPCPortKind.Data,required:true));
                    flow.AddPort(plcBlock,new MPCPortDescriptor("RegisterReadbackMatched",
                        MPCPortDirection.Output,"bool",MPCPortKind.Data));
                    flow.SetParameter(inspectBlock,"Width","5");
                    flow.SetParameter(inspectBlock,"Height","3");
                    flow.SetParameter(plcBlock,"StableId","word");
                    flow.SetParameter(plcBlock,"Value","91");
                    flow.Connect(capture,"Done",inspectBlock,"Start");
                    flow.Connect(capture,"Pixels",inspectBlock,"RawPixels",MPCPortKind.Data);
                    flow.Connect(inspectBlock,"Done",plcBlock,"Start");
                });
                // UInt16 parameters are validated by the sole MEM/MOL
                // contract. Neither overflow nor negative values may revise
                // the saved project or reach a device command.
                int beforeInvalid=session.Revision;
                foreach(string invalidValue in new[]{"65536","-1","abc"})
                {
                    bool invalidRejected=false;
                    try
                    {
                        MOCFlowBuilder.Edit(session,root,flow=>
                            flow.SetParameter(plcBlock,"Value",invalidValue));
                    }
                    catch(InvalidOperationException ex)
                    { invalidRejected=ex.Message=="wiring.parameter_type"; }
                    Check(invalidRejected&&session.Revision==beforeInvalid,
                        "UInt16 illegal literal changed MEM revision or passed validation");
                }
                plan=new MDCDeclarativePlanCompiler().Compile(
                    session.GetSnapshot(),root.Id,catalog);
            }
            finally {session.Close(true);}
            // The runtime never reopens MEM, and does not carry execution
            // delegates through the serialized pure Decision archive.
            MDCDeclarativePlan reopened=MDCPlanArchiveCodec.Deserialize(
                MDCPlanArchiveCodec.Serialize(plan));
            var professionalTypes=new Dictionary<string,Type>(StringComparer.Ordinal){
                {CxVisionAcceptedRegionMeasureProvider.InspectionTypeId,
                    typeof(CxVisionInspectionResult)}
            };
            var binder=new MOCManifestRuntimeBinding();
            MOCCompiledPlan compiled=binder.Bind(reopened,catalog,resolver,
                professionalTypes:professionalTypes,expectedCapabilityVersions:versions);
            string visionStep="w"+inspectBlock.ToString("N");
            string plcStep="w"+plcBlock.ToString("N");
            var engine=new MRKExecutionEngine(new FCRWorkerOptions(2,16));
            try
            {
                int before=writes();
                MRKFlowHandle flow=engine.StartFlow(compiled.Plan,
                    compiled.InputDefaults.ToDictionary(x=>x.Key,x=>x.Value));
                Check(flow.Completion.Wait(TimeSpan.FromSeconds(12)),
                    "real Vision+PLC Flow timeout");
                MRKFlowResult done=flow.Completion.GetAwaiter().GetResult();
                CxVisionInspectionResult inspection=done.Results.ContainsKey(visionStep)?
                    done.Results[visionStep] as CxVisionInspectionResult:null;
                MOCTraceView trace=compiled.ReadTrace(flow,0);
                Check(done.State==MRKFlowState.Completed&&
                    inspection!=null&&inspection.ThresholdPixelCount==4&&
                    inspection.Regions.Count==1&&inspection.Regions[0].PixelArea==3&&
                    done.Results.ContainsKey(plcStep)&&
                    object.Equals(done.Results[plcStep],true)&&
                    actualRegister()==91&&writes()==before+1&&
                    table.Get<ushort>("Station.Word")==91&&
                    trace.Events.Any(x=>x.Origin.BlockId==inspectBlock&&
                        x.Transition.State==MRKStepState.Completed)&&
                    trace.Events.Any(x=>x.Origin.BlockId==plcBlock&&
                        x.Transition.State==MRKStepState.Completed),
                    "Decision pure Plan did not run real Vision CPU and authorized Modbus/MDS");

                var changed=new PADPlcMrkWriteCapabilityProvider(commands,
                    "station01","operator",
                    "automation.plc.uint16-write.v1/points:station01.v2");
                var changedVision=new CxVisionThresholdProvider(options,12,changed);
                int beforeReject=writes();
                bool denied=false;
                try {binder.Bind(reopened,catalog,new RawCaptureResolver(changedVision),
                    professionalTypes:professionalTypes,expectedCapabilityVersions:versions);}
                catch(InvalidOperationException ex)
                {denied=ex.Message.StartsWith("moc.bind.provider_version_mismatch:",
                    StringComparison.Ordinal);}
                Check(denied&&writes()==beforeReject,
                    "changed PLC point version was not rejected before device execution");

                // A forged principal cannot be supplied through the MEM plan.
                // It is injected by the Host and checked by MRKCommandCenter
                // before the Modbus backend can execute any physical IO.
                var untrusted=new PADPlcMrkWriteCapabilityProvider(commands,
                    "station01","guest",ApprovedVersion);
                var guestVision=new CxVisionThresholdProvider(options,12,untrusted);
                MOCCompiledPlan guestPlan=binder.Bind(reopened,catalog,
                    new RawCaptureResolver(guestVision),
                    professionalTypes:professionalTypes,expectedCapabilityVersions:versions);
                MRKFlowHandle guestFlow=engine.StartFlow(guestPlan.Plan,
                    guestPlan.InputDefaults.ToDictionary(x=>x.Key,x=>x.Value));
                Check(guestFlow.Completion.Wait(TimeSpan.FromSeconds(12)),
                    "unauthorized PLC Flow did not settle");
                MRKFlowResult refused=guestFlow.Completion.GetAwaiter().GetResult();
                Check(refused.State==MRKFlowState.Failed&&
                    refused.Results.ContainsKey(visionStep)&&
                    !refused.Results.ContainsKey(plcStep)&&
                    writes()==beforeReject,
                    "untrusted Host caller escaped MRK authorization in a mixed professional Plan");

                // 为负例准备一个不同的现场寄存器事实。此前正例已将值设为91，
                // 再写91即使设备忽略写请求，读回仍等于91，不能伪称检测到不一致。
                // 真实FC06后跟FC03将设备设为90，然后忽略“写91”，才形成真实的Unknown。
                var prepareUnknown = commands.InvokeAsync(new MRKCommandRequest(
                    PADPlcMrkCommandRegistration.TargetForDevice("station01"),
                    PADPlcMrkCommandRegistration.WriteUInt16Code,
                    new Dictionary<string, object> {
                        { "StableId", "word" }, { "Value", (ushort)90 }
                    }, caller: "operator", expectedSchemaVersion: 1))
                    .GetAwaiter().GetResult();
                Check(prepareUnknown.State==MRKCommandState.Completed &&
                    prepareUnknown.BusinessSuccess==true &&
                    actualRegister()==90 && writes()==beforeReject+1,
                    "未能通过已授权FC06+FC03将未知结果场景准备为不同实际值");
                int beforeUnknown=writes();

                // Even if a Modbus command was acknowledged, a mismatched
                // readback is UNKNOWN, not Completed PLC StepResult.
                ignoreWrite(true);
                try
                {
                    // An ignored write of the CURRENT value cannot be
                    // distinguished from a successful readback. Demand a
                    // different register value to test a real mismatch.
                    var mismatchedInputs=compiled.InputDefaults.ToDictionary(
                        x=>x.Key,x=>x.Value);
                    mismatchedInputs[plcStep+".Value"]=(ushort)92;
                    MRKFlowHandle uncertain=engine.StartFlow(compiled.Plan,
                        mismatchedInputs);
                    Check(uncertain.Completion.Wait(TimeSpan.FromSeconds(12)),
                        "unknown physical outcome did not settle");
                    MRKFlowResult failed=uncertain.Completion.GetAwaiter().GetResult();
                    Check(failed.State==MRKFlowState.Failed&&
                        failed.Results.ContainsKey(visionStep)&&
                        !failed.Results.ContainsKey(plcStep)&&
                        writes()==beforeUnknown+1&&actualRegister()==90,
                        "unknown PLC outcome published success or retried device operation");
                }
                finally {ignoreWrite(false);}

                // Adversarial Plan: outer MRK policy was incorrectly declared
                // idempotent and requests two attempts. The professional PLC
                // adapter must still prevent a second FC06 after Unknown.
                MRKResolvedCapability plcBinding;
                Check(automation.TryResolve(
                    PADPlcMrkWriteCapabilityProvider.CapabilityId,out plcBinding),
                    "PLC MOC capability resolver disappeared");
                var retryPlan=new MRKExecutionPlan("plc-nonidempotent-retry-guard",
                    new[]{new MRKExecutionStep("plc-write",
                        new string[0],plcBinding.ExecuteAsync,
                        policy:new MRKExecutionPolicy(maxAttempts:2,idempotent:true),
                        inputs:new[]{
                            new MRKExecutionInput("StableId",MRKInputSource.FlowInput,
                                "StableId",typeof(string)),
                            new MRKExecutionInput("Value",MRKInputSource.FlowInput,
                                "Value",typeof(ushort))
                        },output:plcBinding.Output,
                        dataOutputs:plcBinding.DataOutputs)});
                int beforeUnsafeRetry=writes();
                ignoreWrite(true);
                try
                {
                    MRKFlowHandle challenged=engine.StartFlow(retryPlan,
                        new Dictionary<string,object>{
                            {"StableId","word"},{"Value",(ushort)93}
                        });
                    Check(challenged.Completion.Wait(TimeSpan.FromSeconds(12)),
                        "nonidempotent retry safety gate timed out");
                    MRKFlowResult deniedRetry=challenged.Completion.GetAwaiter().GetResult();
                    Check(deniedRetry.State==MRKFlowState.Failed&&
                        !deniedRetry.Results.ContainsKey("plc-write")&&
                        writes()==beforeUnsafeRetry+1,
                        "incorrect outer MRK retry policy duplicated uncertain PLC device write");
                }
                finally {ignoreWrite(false);}
                // 测试结束后经正式授权命令恢复91，避免污染后续同一
                // ArchitectureSmoke流程的点表、MDS和寄存器前置条件。
                var recoveredCommand = commands.InvokeAsync(new MRKCommandRequest(
                    PADPlcMrkCommandRegistration.TargetForDevice("station01"),
                    PADPlcMrkCommandRegistration.WriteUInt16Code,
                    new Dictionary<string, object> {
                        { "StableId", "word" }, { "Value", (ushort)91 }
                    }, caller: "operator", expectedSchemaVersion: 1))
                    .GetAwaiter().GetResult();
                Check(recoveredCommand.State==MRKCommandState.Completed &&
                    recoveredCommand.BusinessSuccess==true && actualRegister()==91,
                    "负例后的PLC实际寄存器未通过授权命令恢复");
                Check((ushort)bridge.ReadAndMapAsync("word",CancellationToken.None)
                    .GetAwaiter().GetResult()==91,
                    "unknown PLC operation requires fresh readback for recovery");
            }
            finally {engine.StopAsync(true).GetAwaiter().GetResult();}
        }
    }
}
