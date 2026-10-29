using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Mechanism.Runtime.Execution;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Orchestration;
using CxVisual.Mechanism.Data.Configuration;
using CxVisual.Foundation.Data.Types;
using CxVisual.Foundation.Imaging;

namespace CxVisual.Professional.Vision
{
    /// <summary>
    /// G06：复用本专业已有真实CPU灰度阈值/四邻接检测。MRK只调度/追踪，
    /// 不重新实现图像算法；不假冒硬件采图、AI推理或设备互锁。
    /// </summary>
    public sealed class CxVisionThresholdProvider : IMRKVersionedCapabilityResolver, IMOCCapabilityProvider
    {
        public const string CapabilityId="vision.cpu.threshold.inspect";
        /// <summary>
        /// The professional Owner publishes exactly the same capability id/input contract
        /// used by MRK TryResolve. MOC only indexes this declaration, never executes pixels.
        /// </summary>
        public IEnumerable<MOCCapabilityDescriptor> DescribeCapabilities()
        {
            return new[] { new MOCCapabilityDescriptor(CapabilityId,
                "CPU灰度阈值与连通区域检测","Vision/Compute",
                new [] {
                    new MPCPortDescriptor("RawPixels",MPCPortDirection.Input,
                        "byte[]",MPCPortKind.Data,required:true),
                    new MPCPortDescriptor("Width",MPCPortDirection.Input,
                        "int",MPCPortKind.Data,required:true),
                    new MPCPortDescriptor("Height",MPCPortDirection.Input,
                        "int",MPCPortKind.Data,required:true),
                    // 专业检测的CLR返回值仍是唯一StepResult，不另建结果仓库。
                    // MOC保留专业语义TypeId，运行时以Provider校验真实类型。
                    new MPCPortDescriptor("Inspection",MPCPortDirection.Output,
                        CxVisionAcceptedRegionMeasureProvider.InspectionTypeId,
                        MPCPortKind.Data)
                }) };
        }
        // 专业Owner给Host提供不可变的软件能力+配置Revision标识；不能冒充模型/设备版本。
        public long ConfigurationRevision{get{return _configRevision;}}
        /// <summary>Effective CPU input pixel depth, pinned at provider creation.</summary>
        public CxVisionPixelDepth PixelDepth{get{return _depth;}}
        public string ReleaseVersion{get{return "vision.cpu.threshold.v1/config:"+
            _configRevision.ToString(CultureInfo.InvariantCulture);}}
        private readonly CxVisionThresholdPipeline _pipeline;
        private readonly CxVisionPixelDepth _depth;
        private readonly long _configRevision;
        private readonly IMRKCapabilityResolver _fallback;
        private readonly MRKResolvedCapability _capability;
        private readonly Func<string> _effectiveVersionReader;
        private readonly string _capturedEffectiveVersion;

        /// <summary>
        /// 从唯一MHS真源的Effective配置快照构造CPU Provider。
        /// 不使用Saved候选，不触发Apply；新任务边界的生效由MHS/专业Host负责。
        /// 复制配置后释放快照，Provider在本次Plan生命周期内保持固定版本和算法实例。
        /// </summary>
        public static CxVisionThresholdProvider FromEffectiveConfiguration(
            MHSConfigSection<CxVisionInspectionOptions> section,
            IMRKCapabilityResolver fallback=null)
        {
            if(section==null)throw new ArgumentNullException("section");
            using(MHSConfigSnapshot<CxVisionInspectionOptions> snapshot=
                section.CaptureForTask("vision-cpu-provider-"+Guid.NewGuid().ToString("N")))
            {
                string version=snapshot.Version;
                long revision=ParseEffectiveVersion(version);
                return new CxVisionThresholdProvider(snapshot.Get(),revision,fallback,
                    () => section.EffectiveVersion,version);
            }
        }

        /// <summary>
        /// Authorized entry: reading Effective requires MHS permissions. Subsequent
        /// version checks re-evaluate the same policy; revoke => fail closed.
        /// </summary>
        public static CxVisionThresholdProvider FromEffectiveConfiguration(
            MHSConfigAuthorizedSection<CxVisionInspectionOptions> section,
            IMRKCapabilityResolver fallback=null)
        {
            if(section==null)throw new ArgumentNullException("section");
            using(MHSConfigSnapshot<CxVisionInspectionOptions> snapshot=
                section.CaptureForTask("vision-authorized-"+Guid.NewGuid().ToString("N")))
            {
                return new CxVisionThresholdProvider(snapshot.Get(),
                    ParseEffectiveVersion(snapshot.Version),fallback,
                    () => section.Revision.Effective,snapshot.Version);
            }
        }

        private static long ParseEffectiveVersion(string version)
        {
            long revision;
            if(string.IsNullOrEmpty(version)||version[0]!='v'||
                !long.TryParse(version.Substring(1),NumberStyles.None,
                    CultureInfo.InvariantCulture,out revision)||revision<0)
                throw new InvalidOperationException("vision.provider.invalid_effective_version");
            return revision;
        }

        private CxVisionThresholdProvider(CxVisionInspectionOptions options,long revision,
            IMRKCapabilityResolver fallback,Func<string> effectiveVersionReader,
            string capturedEffectiveVersion):this(options,revision,fallback)
        {
            _effectiveVersionReader=effectiveVersionReader;
            _capturedEffectiveVersion=capturedEffectiveVersion;
        }

        public CxVisionThresholdProvider(CxVisionInspectionOptions options,long configRevision,
            IMRKCapabilityResolver fallback=null)
        {
            if(options==null)throw new ArgumentNullException("options");
            if(configRevision<0)throw new ArgumentOutOfRangeException("configRevision");
            _pipeline=new CxVisionThresholdPipeline(options);
            _depth=options.PixelDepth;
            _configRevision=configRevision;
            _fallback=fallback;
            _capability=new MRKResolvedCapability(ExecuteAsync,
                new MRKExecutionOutput(typeof(CxVisionInspectionResult)),
                // 既有StepResult保留；额外以MDS具名Inspection输出支撑下游测量。
                dataOutputs:new Dictionary<string,Type>{
                    {"Inspection",typeof(CxVisionInspectionResult)} },
                inputs:new[]{
                    new MRKCapabilityInput("RawPixels",typeof(byte[])),
                    new MRKCapabilityInput("Width",typeof(int)),
                    new MRKCapabilityInput("Height",typeof(int))
                });
        }

        public bool TryGetCapabilityVersion(string id,out string version)
        {
            if(string.Equals(id,CapabilityId,StringComparison.Ordinal))
            {
                // 新发布不许复用旧Effective Provider；原运行中的不可变算法实例仍可完成。
                if(_effectiveVersionReader!=null&&
                    !string.Equals(_effectiveVersionReader(),_capturedEffectiveVersion,
                        StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "vision.provider.effective_version_changed");
                version=ReleaseVersion;return true;
            }
            IMRKVersionedCapabilityResolver next=_fallback as IMRKVersionedCapabilityResolver;
            if(next!=null)return next.TryGetCapabilityVersion(id,out version);
            version=null;return false;
        }

        public bool TryResolve(string id,out MRKResolvedCapability result)
        {
            if(string.Equals(id,CapabilityId,StringComparison.Ordinal))
            {result=_capability;return true;}
            if(_fallback!=null)return _fallback.TryResolve(id,out result);
            result=null;return false;
        }

        private Task<object> ExecuteAsync(MRKExecutionContext context,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            object pixels,width,height;
            if(!context.BoundInputs.TryGetValue("RawPixels",out pixels)||
                !context.BoundInputs.TryGetValue("Width",out width)||
                !context.BoundInputs.TryGetValue("Height",out height)||
                !(pixels is byte[])||!(width is int)||!(height is int))
                throw new InvalidOperationException("vision.provider.required_typed_inputs");
            int w=(int)width,h=(int)height;
            long count=(long)w*h;
            if(w<=0||h<=0||count>32000000)
                throw new ArgumentOutOfRangeException("Width/Height");
            int bytesPerPixel=_depth==CxVisionPixelDepth.Bits8?1:2;
            byte[] bytes=(byte[])((byte[])pixels).Clone();
            if(bytes.Length!=count*bytesPerPixel)
                throw new ArgumentException("vision.provider.raw_size_mismatch");
            ushort[] values=new ushort[(int)count];
            for(int i=0;i<values.Length;i++)
            {
                if((i&4095)==0)token.ThrowIfCancellationRequested();
                values[i]=bytesPerPixel==1?(ushort)bytes[i]:
                    (ushort)(bytes[2*i]|(bytes[2*i+1]<<8));
            }
            var frame=new CxVisionGrayFrame(w,h,_depth,values);
            CxVisionInspectionResult outcome=_pipeline.Inspect(frame,_configRevision,token);
            // MOC跨节点Inspection必须经过既有FDT安全冻结。单节点返回曾不需要
            // 复制适配器，接入Measurement后不允许在DataStack共享可变Region引用。
            context.Data.Adapters.Register<CxVisionInspectionResult>(
                CxVisionDataFlow.CopyResult,kind:FDTDataKind.Object,
                sizeEstimator:result=>checked(128L+result.Regions.Count*64L));
            context.Data.Return("Inspection",outcome);
            return Task.FromResult((object)outcome);
        }
    }

    /// <summary>
    /// 真正独立的面积测量专业Capability，接收MOC正式连接的Inspect.StepResult。
    /// 不重新实现计算，也不注册新图或执行器；同一输入合同可由其他专业Host使用。
    /// </summary>
    public sealed class CxVisionAcceptedRegionMeasureProvider :
        IMOCCapabilityProvider, IMRKVersionedCapabilityResolver
    {
        public const string CapabilityId="vision.cpu.region.accepted-area";
        public const string InspectionTypeId="vision.cpu.inspection.result";
        public const string Version="vision.cpu.region.accepted-area.v1";
        private readonly IMRKVersionedCapabilityResolver _fallback;
        private readonly MRKResolvedCapability _resolved;

        public CxVisionAcceptedRegionMeasureProvider(IMRKVersionedCapabilityResolver fallback=null)
        {
            _fallback=fallback;
            _resolved=new MRKResolvedCapability((context,ct)=>{
                ct.ThrowIfCancellationRequested();
                object value;
                if(!context.BoundInputs.TryGetValue("Inspection",out value) ||
                   !(value is CxVisionInspectionResult))
                    throw new InvalidOperationException("vision.measure.inspection_required");
                long area=CxVisionDataFlow.MeasureAcceptedPixelArea(
                    (CxVisionInspectionResult)value,ct);
                context.Data.Return("Area",area);
                return Task.FromResult((object)area);
            },new MRKExecutionOutput(typeof(long)),
            dataOutputs:new Dictionary<string,Type>{{"Area",typeof(long)}},
            inputs:new[]{new MRKCapabilityInput("Inspection",typeof(object))});
        }

        public IEnumerable<MOCCapabilityDescriptor> DescribeCapabilities()
        {
            return new[]{new MOCCapabilityDescriptor(CapabilityId,
                "接受区域面积测量（像素）","Vision/Measurement",
                new[]{
                    new MPCPortDescriptor("Inspection",MPCPortDirection.Input,
                        InspectionTypeId,MPCPortKind.Data,required:true),
                    new MPCPortDescriptor("Area",MPCPortDirection.Output,
                        "long",MPCPortKind.Data)
                })};
        }

        public bool TryResolve(string id,out MRKResolvedCapability result)
        {
            if(string.Equals(id,CapabilityId,StringComparison.Ordinal))
            {result=_resolved;return true;}
            if(_fallback!=null)return _fallback.TryResolve(id,out result);
            result=null;return false;
        }
        public bool TryGetCapabilityVersion(string id,out string version)
        {
            if(string.Equals(id,CapabilityId,StringComparison.Ordinal))
            {version=Version;return true;}
            if(_fallback!=null)return _fallback.TryGetCapabilityVersion(id,out version);
            version=null;return false;
        }
    }

    /// <summary>
    /// 正式MOC可组合的FIM中值滤波算子。仅提供专业能力适配，像素算法及
    /// 边界复制规则由唯一FIMImageFilters Owner负责；输出深拷贝的紧排RAW像素。
    /// 允许Gray8/Gray16、半径1..3，未装设备/GPU/AI，不代表生产发布授权。
    /// </summary>
    public sealed class CxVisionMedianEnhanceProvider :
        IMOCCapabilityProvider, IMRKVersionedCapabilityResolver
    {
        public const string CapabilityId="vision.cpu.image.median-enhance";
        public const string VersionPrefix="vision.cpu.image.median.v1";
        private readonly CxVisionPixelDepth _depth;
        private readonly int _radius;
        private readonly IMRKVersionedCapabilityResolver _fallback;
        private readonly MRKResolvedCapability _resolved;

        public CxVisionMedianEnhanceProvider(CxVisionPixelDepth depth,int radius,
            IMRKVersionedCapabilityResolver fallback=null)
        {
            if(depth!=CxVisionPixelDepth.Bits8 && depth!=CxVisionPixelDepth.Bits16)
                throw new ArgumentOutOfRangeException("depth");
            if(radius<1 || radius>3)
                throw new ArgumentOutOfRangeException("radius",
                    "中值滤波工程半径只支持1到3像素");
            _depth=depth;
            _radius=radius;
            _fallback=fallback;
            _resolved=new MRKResolvedCapability(ExecuteAsync,
                new MRKExecutionOutput(typeof(byte[])),
                dataOutputs:new Dictionary<string,Type>{{"EnhancedPixels",typeof(byte[])}},
                inputs:new[]{
                    new MRKCapabilityInput("RawPixels",typeof(byte[])),
                    new MRKCapabilityInput("Width",typeof(int)),
                    new MRKCapabilityInput("Height",typeof(int))
                });
        }

        public string ReleaseVersion
        {
            get { return VersionPrefix+"/depth:"+((int)_depth).ToString(CultureInfo.InvariantCulture)+
                "/radius:"+_radius.ToString(CultureInfo.InvariantCulture); }
        }

        public IEnumerable<MOCCapabilityDescriptor> DescribeCapabilities()
        {
            return new[]{new MOCCapabilityDescriptor(CapabilityId,
                "FIM中值降噪增强","Vision/Enhancement",new[]{
                    new MPCPortDescriptor("RawPixels",MPCPortDirection.Input,
                        "byte[]",MPCPortKind.Data,required:true),
                    new MPCPortDescriptor("Width",MPCPortDirection.Input,
                        "int",MPCPortKind.Data,required:true),
                    new MPCPortDescriptor("Height",MPCPortDirection.Input,
                        "int",MPCPortKind.Data,required:true),
                    new MPCPortDescriptor("EnhancedPixels",MPCPortDirection.Output,
                        "byte[]",MPCPortKind.Data)
                })};
        }

        private Task<object> ExecuteAsync(MRKExecutionContext context,CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            object raw,width,height;
            if(!context.BoundInputs.TryGetValue("RawPixels",out raw) ||
                !context.BoundInputs.TryGetValue("Width",out width) ||
                !context.BoundInputs.TryGetValue("Height",out height) ||
                !(raw is byte[]) || !(width is int) || !(height is int))
                throw new InvalidOperationException("vision.enhance.required_typed_inputs");
            int w=(int)width,h=(int)height;
            long pixels=(long)w*h;
            if(w<=0 || h<=0 || pixels>32000000L)
                throw new ArgumentOutOfRangeException("Width/Height");
            int bytesPerPixel=_depth==CxVisionPixelDepth.Bits8?1:2;
            byte[] source=(byte[])raw;
            if(source.Length!=pixels*bytesPerPixel)
                throw new ArgumentException("vision.enhance.raw_size_mismatch");
            // FIMImageBuffer自持有输入快照；FIM滤波返回另一个自持有缓冲。
            var input=new FIMImageBuffer(w,h,
                _depth==CxVisionPixelDepth.Bits8?FIMPixelFormat.Gray8:FIMPixelFormat.Gray16,
                source);
            FIMImageBuffer filtered=FIMImageFilters.Apply(input,FIMNeighborhoodFilter.Median,
                _radius,cancellation);
            byte[] enhanced=filtered.CopyPixels();
            cancellation.ThrowIfCancellationRequested();
            context.Data.Return("EnhancedPixels",enhanced);
            return Task.FromResult((object)enhanced);
        }

        public bool TryResolve(string id,out MRKResolvedCapability result)
        {
            if(string.Equals(id,CapabilityId,StringComparison.Ordinal))
            { result=_resolved; return true; }
            if(_fallback!=null)return _fallback.TryResolve(id,out result);
            result=null;return false;
        }

        public bool TryGetCapabilityVersion(string id,out string version)
        {
            if(string.Equals(id,CapabilityId,StringComparison.Ordinal))
            { version=ReleaseVersion; return true; }
            if(_fallback!=null)return _fallback.TryGetCapabilityVersion(id,out version);
            version=null;return false;
        }
    }
}
