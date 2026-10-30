using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Orchestration;
using CxVisual.Mechanism.Runtime.Execution;
using CxVisual.Professional.Automation.Plc;
using CxVisual.Professional.Vision;

namespace CxVisual.Studio
{
    /// <summary>
    /// Studio's software-only Host scope: this is an adapter over real
    /// Professional Vision CPU providers, not another image algorithm,
    /// catalog owner, PLC command center or MRK execution engine.
    /// PLC capability is only described for offline authoring; it NEVER
    /// resolves to an executable delegate in the Studio process.
    /// </summary>
    internal sealed class StudioProfessionalFlowResolver :
        IMOCCapabilityProvider, IMRKVersionedCapabilityResolver
    {
        internal const string RawCapabilityId = "vision.cpu.local.raw";
        private readonly byte[] _raw;
        private readonly string _rawVersion;
        private readonly IMRKVersionedCapabilityResolver _cpu;
        private readonly IMRKVersionedCapabilityResolver _simulation;
        private readonly IMOCCapabilityProvider[] _professionals;
        private readonly MRKResolvedCapability _rawCapability;

        internal static MOCCapabilityDescriptor RawDescriptor()
        {
            return new MOCCapabilityDescriptor(RawCapabilityId,
                "离线RAW图像输入（显式绑定文件）", "Vision/Acquisition",
                new[] { new MPCPortDescriptor("Pixels",MPCPortDirection.Output,
                    "byte[]",MPCPortKind.Data) });
        }

        internal StudioProfessionalFlowResolver(CxVisionInspectionOptions options,
            long softwareConfigRevision, byte[] rawBytes,
            IMRKVersionedCapabilityResolver simulation)
        {
            if(options==null)throw new ArgumentNullException("options");
            if(simulation==null)throw new ArgumentNullException("simulation");
            string invalid=CxVisionInspectionOptions.Validate(options);
            if(invalid!=null)throw new ArgumentException(invalid,"options");
            _simulation=simulation;
            _raw=rawBytes==null?null:(byte[])rawBytes.Clone();
            if(_raw!=null)
            {
                using(var sha=SHA256.Create())
                    _rawVersion="studio.raw.sha256."+
                        BitConverter.ToString(sha.ComputeHash(_raw)).Replace("-","");
                _rawCapability=new MRKResolvedCapability((context,token)=>{
                    token.ThrowIfCancellationRequested();
                    byte[] pixels=(byte[])_raw.Clone();
                    context.Data.Return("Pixels",pixels);
                    return Task.FromResult((object)pixels);
                },new MRKExecutionOutput(typeof(byte[])),
                    dataOutputs:new Dictionary<string,Type>{{"Pixels",typeof(byte[])}},
                    inputs:new MRKCapabilityInput[0]);
            }

            var threshold=new CxVisionThresholdProvider(options,softwareConfigRevision,simulation);
            var median=new CxVisionMedianEnhanceProvider(options.PixelDepth,1,threshold);
            var binary=new CxVisionBinaryThresholdProvider(options.PixelDepth,
                options.GrayThreshold,median);
            var regions=new CxVisionMaskRegionsProvider(options,softwareConfigRevision,binary);
            var area=new CxVisionAcceptedRegionMeasureProvider(regions);
            _cpu=area;
            _professionals=new IMOCCapabilityProvider[] {threshold,median,binary,regions,area};
        }

        internal static long ComputeSoftwareRevision(CxVisionInspectionOptions options)
        {
            if(options==null)throw new ArgumentNullException("options");
            string error=CxVisionInspectionOptions.Validate(options);
            if(error!=null)throw new ArgumentException(error,"options");
            string normalized=string.Join("|",new[]{
                ((int)options.PixelDepth).ToString(CultureInfo.InvariantCulture),
                options.GrayThreshold.ToString(CultureInfo.InvariantCulture),
                options.MinDefectArea.ToString("R",CultureInfo.InvariantCulture),
                options.MaxDefectArea.ToString("R",CultureInfo.InvariantCulture),
                options.UseAi.ToString(),options.AiModelName??""
            });
            using(var hash=SHA256.Create())
                return BitConverter.ToInt64(hash.ComputeHash(
                    Encoding.UTF8.GetBytes(normalized)),0)&long.MaxValue;
        }

        public IEnumerable<MOCCapabilityDescriptor> DescribeCapabilities()
        {
            yield return RawDescriptor();
            foreach(var provider in _professionals)
                foreach(var description in provider.DescribeCapabilities())
                    yield return description;
            yield return PADPlcMrkWriteCapabilityProvider.DescribeOfflineContract();
        }

        public bool TryResolve(string id,out MRKResolvedCapability capability)
        {
            if(id==RawCapabilityId)
            {
                capability=_rawCapability;
                return capability!=null;
            }
            if(id==PADPlcMrkWriteCapabilityProvider.CapabilityId)
            {
                capability=null;
                return false; // Only trusted Runtime.Host may assemble device commands.
            }
            return _cpu.TryResolve(id,out capability);
        }

        public bool TryGetCapabilityVersion(string id,out string version)
        {
            if(id==RawCapabilityId)
            {
                version=_rawVersion;
                return version!=null;
            }
            if(id==PADPlcMrkWriteCapabilityProvider.CapabilityId)
            {
                version=null;
                return false;
            }
            return _cpu.TryGetCapabilityVersion(id,out version);
        }

        internal IReadOnlyDictionary<string,string> DescribeSoftwareVersions()
        {
            var versions=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(var item in DescribeCapabilities())
            {
                string version;
                if(TryGetCapabilityVersion(item.Id,out version) &&
                    !string.IsNullOrWhiteSpace(version))
                    versions.Add(item.Id,version);
            }
            return versions;
        }

        internal static bool RequiresHostDeviceAuthorization(string id)
        {
            return StringComparer.Ordinal.Equals(
                id,PADPlcMrkWriteCapabilityProvider.CapabilityId);
        }
    }
}
