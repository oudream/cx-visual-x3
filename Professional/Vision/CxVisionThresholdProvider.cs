using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Mechanism.Runtime.Execution;

namespace CxVisual.Professional.Vision
{
    /// <summary>
    /// G06：复用本专业已有真实CPU灰度阈值/四邻接检测。MRK只调度/追踪，
    /// 不重新实现图像算法；不假冒硬件采图、AI推理或设备互锁。
    /// </summary>
    public sealed class CxVisionThresholdProvider : IMRKCapabilityResolver
    {
        public const string CapabilityId="vision.cpu.threshold.inspect";
        // 专业Owner给Host提供不可变的软件能力+配置Revision标识；不能冒充模型/设备版本。
        public long ConfigurationRevision{get{return _configRevision;}}
        public string ReleaseVersion{get{return "vision.cpu.threshold.v1/config:"+
            _configRevision.ToString(CultureInfo.InvariantCulture);}}
        private readonly CxVisionThresholdPipeline _pipeline;
        private readonly CxVisionPixelDepth _depth;
        private readonly long _configRevision;
        private readonly IMRKCapabilityResolver _fallback;
        private readonly MRKResolvedCapability _capability;

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
                inputs:new[]{
                    new MRKCapabilityInput("RawPixels",typeof(byte[])),
                    new MRKCapabilityInput("Width",typeof(int)),
                    new MRKCapabilityInput("Height",typeof(int))
                });
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
            return Task.FromResult((object)outcome);
        }
    }
}
