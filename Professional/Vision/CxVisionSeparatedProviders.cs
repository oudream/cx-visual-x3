using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Types;
using CxVisual.Foundation.Imaging;
using CxVisual.Mechanism.Orchestration;
using CxVisual.Mechanism.Runtime.Execution;

namespace CxVisual.Professional.Vision
{
    /// <summary>独立FIM灰度阈值节点；返回原图全尺寸、尚未面积筛选的二值Mask。</summary>
    public sealed class CxVisionBinaryThresholdProvider : IMOCCapabilityProvider, IMRKVersionedCapabilityResolver
    {
        public const string CapabilityId = "vision.cpu.image.threshold-mask";
        public const string MaskTypeId = "vision.cpu.binary.mask";
        private readonly CxVisionPixelDepth _depth;
        private readonly int _threshold;
        private readonly IMRKVersionedCapabilityResolver _fallback;
        private readonly MRKResolvedCapability _capability;

        public CxVisionBinaryThresholdProvider(CxVisionPixelDepth depth, int threshold,
            IMRKVersionedCapabilityResolver fallback = null)
        {
            if (depth != CxVisionPixelDepth.Bits8 && depth != CxVisionPixelDepth.Bits16)
                throw new ArgumentOutOfRangeException("depth");
            if (threshold < 0 || threshold > (depth == CxVisionPixelDepth.Bits8 ? 255 : 65535))
                throw new ArgumentOutOfRangeException("threshold");
            _depth = depth; _threshold = threshold; _fallback = fallback;
            _capability = new MRKResolvedCapability(ExecuteAsync,
                new MRKExecutionOutput(typeof(FIMImageBuffer)),
                dataOutputs: new Dictionary<string, Type> {{"ThresholdMask",typeof(FIMImageBuffer)}},
                inputs: new[] {
                    new MRKCapabilityInput("RawPixels",typeof(byte[])),
                    new MRKCapabilityInput("Width",typeof(int)),
                    new MRKCapabilityInput("Height",typeof(int)),
                    new MRKCapabilityInput("Threshold",typeof(int))
                });
        }
        public string ReleaseVersion
        {
            get { return "vision.cpu.threshold-mask.v1/depth:" +
                ((int)_depth).ToString(CultureInfo.InvariantCulture) + "/threshold:" +
                _threshold.ToString(CultureInfo.InvariantCulture); }
        }
        public IEnumerable<MOCCapabilityDescriptor> DescribeCapabilities()
        {
            return new[] {new MOCCapabilityDescriptor(CapabilityId,"独立FIM灰度阈值分割",
                "Vision/Segmentation",new[] {
                    new MPCPortDescriptor("RawPixels",MPCPortDirection.Input,"byte[]",MPCPortKind.Data,required:true),
                    new MPCPortDescriptor("Width",MPCPortDirection.Input,"int",MPCPortKind.Data,required:true),
                    new MPCPortDescriptor("Height",MPCPortDirection.Input,"int",MPCPortKind.Data,required:true),
                    new MPCPortDescriptor("Threshold",MPCPortDirection.Input,"int",MPCPortKind.Data,required:true),
                    new MPCPortDescriptor("ThresholdMask",MPCPortDirection.Output,MaskTypeId,MPCPortKind.Data)
                })};
        }
        private Task<object> ExecuteAsync(MRKExecutionContext context,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            object raw,width,height,threshold;
            if(!context.BoundInputs.TryGetValue("RawPixels",out raw) ||
               !context.BoundInputs.TryGetValue("Width",out width) ||
               !context.BoundInputs.TryGetValue("Height",out height) ||
               !context.BoundInputs.TryGetValue("Threshold",out threshold) ||
               !(raw is byte[]) || !(width is int) || !(height is int) ||
               !(threshold is int) || (int)threshold!=_threshold)
                throw new InvalidOperationException("vision.segment.required_typed_inputs");
            int w=(int)width,h=(int)height;
            long count=(long)w*h;
            if(w<=0 || h<=0 || count>32000000L)
                throw new ArgumentOutOfRangeException("Width/Height");
            byte[] source=(byte[])raw;
            int bpp=_depth==CxVisionPixelDepth.Bits8?1:2;
            if(source.Length!=count*bpp)
                throw new ArgumentException("vision.segment.raw_size_mismatch");
            var image=new FIMImageBuffer(w,h,
                bpp==1?FIMPixelFormat.Gray8:FIMPixelFormat.Gray16,source);
            FIMImageBuffer mask=FIMImageOperations.ThresholdInRegion(image,_threshold,
                bpp==1?255:65535,FIMPixelRegion.Entire(image),
                null,token).Mask;
            token.ThrowIfCancellationRequested();
            context.Data.Adapters.Register<FIMImageBuffer>(null,immutable:true,
                kind:FDTDataKind.Image,
                sizeEstimator:x=>checked((long)x.Stride*x.Height));
            context.Data.Return("ThresholdMask",mask);
            return Task.FromResult((object)mask);
        }
        public bool TryResolve(string id,out MRKResolvedCapability result)
        {
            if(id==CapabilityId){result=_capability;return true;}
            if(_fallback!=null)return _fallback.TryResolve(id,out result);
            result=null;return false;
        }
        public bool TryGetCapabilityVersion(string id,out string version)
        {
            if(id==CapabilityId){version=ReleaseVersion;return true;}
            if(_fallback!=null)return _fallback.TryGetCapabilityVersion(id,out version);
            version=null;return false;
        }
    }

    /// <summary>独立四邻域Region节点；复用既有专业ThresholdPipeline而非复制连通域算法。</summary>
    public sealed class CxVisionMaskRegionsProvider : IMOCCapabilityProvider,IMRKVersionedCapabilityResolver
    {
        public const string CapabilityId="vision.cpu.mask.connected-regions";
        private readonly CxVisionThresholdPipeline _pipeline;
        private readonly long _revision;
        private readonly double _minimum,_maximum;
        private readonly IMRKVersionedCapabilityResolver _fallback;
        private readonly MRKResolvedCapability _capability;
        public CxVisionMaskRegionsProvider(CxVisionInspectionOptions options,long revision,
            IMRKVersionedCapabilityResolver fallback=null)
        {
            if(options==null)throw new ArgumentNullException("options");
            if(revision<0)throw new ArgumentOutOfRangeException("revision");
            _pipeline=new CxVisionThresholdPipeline(new CxVisionInspectionOptions {
                PixelDepth=CxVisionPixelDepth.Bits8,GrayThreshold=255,
                MinDefectArea=options.MinDefectArea,MaxDefectArea=options.MaxDefectArea,
                UseAi=options.UseAi,AiModelName=options.AiModelName
            });
            _revision=revision;_minimum=options.MinDefectArea;_maximum=options.MaxDefectArea;
            _fallback=fallback;
            _capability=new MRKResolvedCapability(ExecuteAsync,
                new MRKExecutionOutput(typeof(CxVisionInspectionResult)),
                dataOutputs:new Dictionary<string,Type>{{"Inspection",typeof(CxVisionInspectionResult)}},
                inputs:new[]{
                    new MRKCapabilityInput("ThresholdMask",typeof(object)),
                    new MRKCapabilityInput("RuleVersion",typeof(string))
                });
        }
        public string ReleaseVersion
        {
            get {return "vision.cpu.mask-regions.v1/revision:"+
                _revision.ToString(CultureInfo.InvariantCulture)+"/min:"+
                _minimum.ToString("R",CultureInfo.InvariantCulture)+"/max:"+
                _maximum.ToString("R",CultureInfo.InvariantCulture);}
        }
        public IEnumerable<MOCCapabilityDescriptor> DescribeCapabilities()
        {
            return new[]{new MOCCapabilityDescriptor(CapabilityId,"Mask四邻域及面积筛选",
                "Vision/Segmentation",new[]{
                    new MPCPortDescriptor("ThresholdMask",MPCPortDirection.Input,
                        CxVisionBinaryThresholdProvider.MaskTypeId,MPCPortKind.Data,required:true),
                    new MPCPortDescriptor("RuleVersion",MPCPortDirection.Input,
                        "string",MPCPortKind.Data,required:true),
                    new MPCPortDescriptor("Inspection",MPCPortDirection.Output,
                        CxVisionAcceptedRegionMeasureProvider.InspectionTypeId,MPCPortKind.Data)
                })};
        }
        private Task<object> ExecuteAsync(MRKExecutionContext context,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            object ruleVersion;
            if(!context.BoundInputs.TryGetValue("RuleVersion",out ruleVersion) ||
               !(ruleVersion is string) ||
               !string.Equals((string)ruleVersion,ReleaseVersion,StringComparison.Ordinal))
                throw new InvalidOperationException("vision.regions.rule_version_mismatch");
            object source;
            if(!context.BoundInputs.TryGetValue("ThresholdMask",out source) ||
               !(source is FIMImageBuffer))
                throw new InvalidOperationException("vision.regions.binary_mask_required");
            FIMImageBuffer mask=(FIMImageBuffer)source;
            if(mask.PixelFormat!=FIMPixelFormat.Gray8 ||
               (long)mask.Width*mask.Height>32000000L)
                throw new ArgumentException("vision.regions.invalid_mask_format");
            ushort[] values=new ushort[checked(mask.Width*mask.Height)];
            for(int y=0;y<mask.Height;y++)
            {
                token.ThrowIfCancellationRequested();
                for(int x=0;x<mask.Width;x++)
                {
                    double v=mask.GetGrayValue(x,y);
                    if(v!=0 && v!=255)throw new ArgumentException("vision.regions.mask_must_be_0_or_255");
                    values[y*mask.Width+x]=(ushort)v;
                }
            }
            var frame=new CxVisionGrayFrame(mask.Width,mask.Height,CxVisionPixelDepth.Bits8,values);
            CxVisionInspectionResult result=_pipeline.Inspect(frame,_revision,token);
            token.ThrowIfCancellationRequested();
            context.Data.Adapters.Register<CxVisionInspectionResult>(
                CxVisionDataFlow.CopyResult,kind:FDTDataKind.Object,
                sizeEstimator:r=>checked(128L+r.Regions.Count*64L));
            context.Data.Return("Inspection",result);
            return Task.FromResult((object)result);
        }
        public bool TryResolve(string id,out MRKResolvedCapability result)
        {
            if(id==CapabilityId){result=_capability;return true;}
            if(_fallback!=null)return _fallback.TryResolve(id,out result);
            result=null;return false;
        }
        public bool TryGetCapabilityVersion(string id,out string version)
        {
            if(id==CapabilityId){version=ReleaseVersion;return true;}
            if(_fallback!=null)return _fallback.TryGetCapabilityVersion(id,out version);
            version=null;return false;
        }
    }
}
