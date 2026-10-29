using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Orchestration;
using CxVisual.Mechanism.Runtime.Execution;

namespace CxVisual.Professional.Vision
{
    public sealed class CxVisionCpuTrace
    {
        public long Sequence {get;internal set;}
        public string StepId {get;internal set;}
        public Guid BlockId {get;internal set;}
        public string State {get;internal set;}
        public DateTime TimestampUtc {get;internal set;}
    }
    public sealed class CxVisionCpuRun
    {
        public Guid PageId {get;internal set;}
        public Guid FlowId {get;internal set;}
        public Guid InspectBlockId {get;internal set;}
        /// <summary>Only populated for opt-in project measurement.</summary>
        public Guid MeasureBlockId {get;internal set;}
        public long? AcceptedRegionPixelArea {get;internal set;}
        public int EngineeringRevision {get;internal set;}
        public string Fingerprint {get;internal set;}
        public string ConfigVersion {get;internal set;}
        public CxVisionInspectionResult Inspection {get;internal set;}
        public CxVisionRunSummary Evidence {get;internal set;}
        public int CompletedTraceEvents {get;internal set;}
        public IReadOnlyList<CxVisionCpuTrace> Trace {get;internal set;}
    }
    /// <summary>
    /// Reusable in-process CPU-only pipeline: actual RAW bytes -> MEM -> MOC -> MRK ->
    /// professional Vision result + SourceMap trace. No hardware/production authorization.
    /// TestCenter and Studio use this same professional Owner, not two executors.
    /// </summary>
    public static class CxVisionRawCpuRunner
    {
        private const string InputId="vision.cpu.local.raw";
        private const string Principal="vision.cpu.local.sandbox";
        private sealed class RawProvider : IMOCCapabilityProvider,IMRKVersionedCapabilityResolver
        {
            private readonly byte[] _bytes;
            private readonly string _version;
            private readonly IMRKVersionedCapabilityResolver _fallback;
            private readonly MRKResolvedCapability _resolved;
            public RawProvider(byte[] bytes,IMRKVersionedCapabilityResolver fallback)
            {
                _bytes=(byte[])bytes.Clone();
                _fallback=fallback;
                using(var hash=SHA256.Create())
                    _version="raw.sha256."+BitConverter.ToString(hash.ComputeHash(_bytes)).Replace("-","");
                _resolved=new MRKResolvedCapability((context,token)=>{
                    token.ThrowIfCancellationRequested();
                    return Task.FromResult((object)((byte[])_bytes.Clone()));
                },new MRKExecutionOutput(typeof(byte[])));
            }
            public IEnumerable<MOCCapabilityDescriptor> DescribeCapabilities()
            {
                return new[]{new MOCCapabilityDescriptor(InputId,"本次RAW输入","Vision/Input",
                    new[]{new MPCPortDescriptor("Pixels",MPCPortDirection.Output,
                        "byte[]",MPCPortKind.Data)})};
            }
            public bool TryResolve(string id,out MRKResolvedCapability result)
            {
                if(id==InputId){result=_resolved;return true;}
                return _fallback.TryResolve(id,out result);
            }
            public bool TryGetCapabilityVersion(string id,out string version)
            {
                if(id==InputId){version=_version;return true;}
                return _fallback.TryGetCapabilityVersion(id,out version);
            }
        }
        private sealed class CpuOnlyPolicy : IMRKPlanPublicationPolicy
        {
            public bool Authorize(string principal,MRKPlanPublishAction action,
                MRKExecutionPlan plan,long revision)
            {
                return principal==Principal && revision==1 && plan!=null &&
                    (plan.Steps.Count==2 || plan.Steps.Count==3);
            }
            public bool IsSafeToActivate(MRKExecutionPlan plan,long revision)
            {
                return revision==1 && plan!=null &&
                    (plan.Steps.Count==2 || plan.Steps.Count==3);
            }
        }
        public static byte[] ReadRaw(string path,int width,int height,
            CxVisionPixelDepth depth,CancellationToken token)
        {
            if(String.IsNullOrWhiteSpace(path))throw new ArgumentException("RAW路径必填","path");
            long count=(long)width*height;
            if(width<=0||height<=0||count>32000000L)
                throw new ArgumentOutOfRangeException("width","RAW尺寸不合法");
            if(depth!=CxVisionPixelDepth.Bits8&&depth!=CxVisionPixelDepth.Bits16)
                throw new ArgumentException("RAW位深不支持");
            int len=checked((int)(count*(depth==CxVisionPixelDepth.Bits8?1:2)));
            token.ThrowIfCancellationRequested();
            using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(file.Length!=len)throw new InvalidDataException("RAW字节数与宽高/Effective位深不符");
                byte[] data=new byte[len];int n=0;
                while(n<len)
                {
                    token.ThrowIfCancellationRequested();
                    int read=file.Read(data,n,len-n);
                    if(read<=0)throw new EndOfStreamException("RAW提前结束");
                    n+=read;
                }
                if(file.ReadByte()!=-1)throw new InvalidDataException("RAW读取时文件增长");
                return data;
            }
        }
        public static Task<CxVisionCpuRun> ExecuteAsync(
            MEMEngineeringSession session,CxVisionThresholdProvider vision,
            string path,int width,int height,CancellationToken cancellation)
        {
            return ExecuteCoreAsync(session,vision,path,width,height,cancellation,false);
        }

        /// <summary>
        /// Opt-in project-backed RAW→Inspect→Measure with MOC source map and pinned versions.
        /// No new graph or executor; original two-step API remains unchanged.
        /// </summary>
        public static Task<CxVisionCpuRun> ExecuteMeasuredAsync(
            MEMEngineeringSession session,CxVisionThresholdProvider vision,
            string path,int width,int height,CancellationToken cancellation)
        {
            return ExecuteCoreAsync(session,vision,path,width,height,cancellation,true);
        }

        private static async Task<CxVisionCpuRun> ExecuteCoreAsync(
            MEMEngineeringSession session,CxVisionThresholdProvider vision,
            string path,int width,int height,CancellationToken cancellation,
            bool includeMeasurement)
        {
            if(session==null)throw new ArgumentNullException("session");
            if(vision==null)throw new ArgumentNullException("vision");
            cancellation.ThrowIfCancellationRequested();
            string version;
            if(!vision.TryGetCapabilityVersion(CxVisionThresholdProvider.CapabilityId,out version)||
                String.IsNullOrWhiteSpace(version))
                throw new InvalidOperationException("CPU Vision配置版本不可用");
            // Validate before modifying the engineering project.
            byte[] pixels=ReadRaw(path,width,height,vision.PixelDepth,cancellation);
            cancellation.ThrowIfCancellationRequested();
            CxVisionAcceptedRegionMeasureProvider measureProvider=includeMeasurement ?
                new CxVisionAcceptedRegionMeasureProvider(vision) : null;
            IMRKVersionedCapabilityResolver resolver=includeMeasurement ?
                (IMRKVersionedCapabilityResolver)measureProvider : vision;
            var source=new RawProvider(pixels,resolver);
            var page=session.AddPage((includeMeasurement ?
                "CPU RAW检测与面积测量 · " : "CPU RAW检测 · ")+Path.GetFileName(path));
            Guid inspect=Guid.Empty,measure=Guid.Empty;
            MOCFlowBuilder.Edit(session,page.RootContent,b=>{
                Guid raw=b.Step(source.DescribeCapabilities().Single());
                inspect=b.Step(vision.DescribeCapabilities().Single());
                b.SetParameter(inspect,"Width",width.ToString(CultureInfo.InvariantCulture));
                b.SetParameter(inspect,"Height",height.ToString(CultureInfo.InvariantCulture));
                b.Connect(raw,"Done",inspect,"Start",MPCPortKind.Control);
                b.Connect(raw,"Pixels",inspect,"RawPixels",MPCPortKind.Data);
                if(includeMeasurement)
                {
                    measure=b.Step(measureProvider.DescribeCapabilities().Single());
                    b.Connect(inspect,"Done",measure,"Start",MPCPortKind.Control);
                    b.Connect(inspect,"Inspection",measure,"Inspection",MPCPortKind.Data);
                    b.ExposeOutput("AcceptedRegionPixelArea",measure,"Area");
                }
            });
            string rawVersion;
            source.TryGetCapabilityVersion(InputId,out rawVersion);
            var versions=new Dictionary<string,string>(StringComparer.Ordinal) {
                {InputId,rawVersion},{CxVisionThresholdProvider.CapabilityId,version}};
            if(includeMeasurement)
                versions.Add(CxVisionAcceptedRegionMeasureProvider.CapabilityId,
                    CxVisionAcceptedRegionMeasureProvider.Version);
            MEMProjectSnapshot snapshot=session.GetSnapshot();
            var candidate=MOCProjectRelease.Prepare(snapshot,page.RootContent.Id,source,versions);
            cancellation.ThrowIfCancellationRequested();
            var baseline=new MRKExecutionPlan("vision.cpu.inert",
                new[]{new MRKExecutionStep("inert",new string[0],
                    (context,token)=>Task.FromResult((object)0))});
            var slot=new MRKExecutionPlanSlot(baseline,0,true);
            var publisher=new MRKExecutionPlanPublisher(slot,baseline,0,new CpuOnlyPolicy());
            var receipt=MOCProjectRelease.Stage(candidate,session.GetSnapshot(),
                source,versions,publisher,Principal,1);
            if(!receipt.Accepted||!publisher.TryActivate(Principal))
                throw new InvalidOperationException("CPU软件发布未获许可");
            using(var active=slot.Capture())
                if(!Object.ReferenceEquals(active.Plan,candidate.Plan.Plan)||active.Revision!=1)
                    throw new InvalidOperationException("MRK软件激活版本不一致");
            var compiled=candidate.Plan;
            var engine=new MRKExecutionEngine(new FCRWorkerOptions(2,16));
            try
            {
                var defaults=compiled.InputDefaults.ToDictionary(x=>x.Key,x=>x.Value,
                    StringComparer.Ordinal);
                var flow=engine.StartFlow(compiled.Plan,defaults);
                using(cancellation.Register(()=>flow.RequestCancel()))
                {
                    var result=await flow.Completion.ConfigureAwait(false);
                    if(result.State!=MRKFlowState.Completed)
                        throw new InvalidOperationException("MRK未完成："+result.State+
                            (result.Error==null?"":" / "+result.Error.ToString()),
                            result.Error);
                    object raw;
                    if(!result.Results.TryGetValue("w"+inspect.ToString("N"),out raw))
                        throw new InvalidOperationException("MRK结果缺失");
                    var inspection=raw as CxVisionInspectionResult;
                    if(inspection==null||inspection.Width!=width||inspection.Height!=height||
                        inspection.ConfigRevision!=vision.ConfigurationRevision)
                        throw new InvalidOperationException("检测结果与配置或输入尺寸不一致");
                    long? acceptedArea=null;
                    if(includeMeasurement)
                    {
                        object measured;
                        if(!result.Results.TryGetValue("w"+measure.ToString("N"),out measured) ||
                           !(measured is long))
                            throw new InvalidOperationException("工程测量节点结果缺失或类型错误");
                        acceptedArea=(long)measured;
                        if(acceptedArea.Value!=result.GetCommittedOutput<long>(
                                "AcceptedRegionPixelArea"))
                            throw new InvalidOperationException(
                                "工程具名Area出口与MOC测量节点结果不一致");
                        if(acceptedArea.Value!=CxVisionDataFlow.MeasureAcceptedPixelArea(
                            inspection,cancellation))
                            throw new InvalidOperationException("工程区域面积与算法结果不一致");
                    }
                    var trace=compiled.ReadTrace(flow,0);
                    if(includeMeasurement && !trace.Events.Any(e=>
                        e.Origin.BlockId==measure &&
                        e.Transition.State==MRKStepState.Completed))
                        throw new InvalidOperationException("工程测量节点完成Trace缺失");
                    int completed=trace.Events.Count(e=>e.Origin.BlockId==inspect&&
                        e.Transition.State==MRKStepState.Completed);
                    if(completed==0||trace.LostCount!=0)
                        throw new InvalidOperationException("MRK真实完成Trace缺失");
                    return new CxVisionCpuRun {
                        PageId=page.Id,FlowId=result.FlowId,InspectBlockId=inspect,
                        MeasureBlockId=measure,AcceptedRegionPixelArea=acceptedArea,
                        EngineeringRevision=snapshot.Revision,Fingerprint=candidate.Fingerprint,
                        ConfigVersion=version,Inspection=inspection,CompletedTraceEvents=completed,
                        Evidence=CxVisionRunSummary.FromInspection(result.FlowId,path,inspection,
                            planFingerprint:candidate.Fingerprint,capabilityVersion:version,
                            completedUtc:result.CompletedUtc,
                            acceptedRegionPixelArea:acceptedArea),
                        Trace=trace.Events.Take(300).Select(e=>new CxVisionCpuTrace {
                            Sequence=e.Transition.Sequence,StepId=e.Transition.StepId,
                            BlockId=e.Origin.BlockId,State=e.Transition.State.ToString(),
                            TimestampUtc=e.Transition.TimestampUtc}).ToArray()
                    };
                }
            }
            finally{await engine.StopAsync(true).ConfigureAwait(false);}
        }
    }
}
