using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using CxVisual.Mechanism.Data.Variables;

namespace CxVisual.Professional.HMI
{
    // 仅实现A已发布的read-only Pipe v1消费合同，绝不实现write或自创PLC/Transport服务。
    [DataContract] internal sealed class PHMScadaPipeRequest
    {
        [DataMember] public int SchemaVersion {get;set;}
        [DataMember] public string Operation {get;set;}
        [DataMember] public string DeviceRef {get;set;}
        [DataMember] public string ExpectedDeploymentVersion {get;set;}
        [DataMember] public string ExpectedConfigVersion {get;set;}
        [DataMember] public string ExpectedGeneration {get;set;}
    }
    [DataContract] internal sealed class PHMScadaPipePoint
    {
        [DataMember] public string StablePointId {get;set;}
        [DataMember] public string VariableId {get;set;}
        [DataMember] public string Type {get;set;}
        [DataMember] public string Unit {get;set;}
        [DataMember] public string Value {get;set;}
        [DataMember] public bool HasValue {get;set;}
        [DataMember] public string Quality {get;set;}
        [DataMember] public string RefreshUtc {get;set;}
        [DataMember] public string SourceId {get;set;}
        [DataMember] public long Revision {get;set;}
    }
    [DataContract] internal sealed class PHMScadaPipeDevice
    {
        [DataMember] public string DeviceRef {get;set;}
        [DataMember] public string ScopeId {get;set;}
        [DataMember] public string ConfigVersion {get;set;}
        [DataMember] public string Generation {get;set;}
        [DataMember] public bool Running {get;set;}
        [DataMember] public PHMScadaPipePoint[] Points {get;set;}
    }
    [DataContract] internal sealed class PHMScadaPipeReply
    {
        [DataMember] public int SchemaVersion {get;set;}
        [DataMember] public string Status {get;set;}
        [DataMember] public string ErrorCode {get;set;}
        [DataMember] public string DeploymentVersion {get;set;}
        [DataMember] public PHMScadaPipeDevice[] Devices {get;set;}
    }

    public sealed class PHMScadaPipeRejectedException : InvalidOperationException
    {
        public PHMScadaPipeRejectedException(string errorCode)
            :base("scada.readonly_pipe.rejected:"+errorCode)
        {ErrorCode=errorCode;}
        public string ErrorCode {get;private set;}
    }
    public sealed class PHMScadaDeviceIdentity
    {
        internal PHMScadaDeviceIdentity(string deploymentVersion,PHMScadaPipeDevice device)
        {
            Guid id;
            if(device==null || !Guid.TryParse(device.DeviceRef,out id) || id==Guid.Empty ||
                String.IsNullOrEmpty(deploymentVersion) ||
                String.IsNullOrEmpty(device.ConfigVersion) ||
                String.IsNullOrEmpty(device.Generation) ||
                String.IsNullOrEmpty(device.ScopeId))
                throw new InvalidDataException("hmi.pipe.device_identity_invalid");
            DeviceRef=id;ScopeId=device.ScopeId;ConfigVersion=device.ConfigVersion;
            Generation=device.Generation;DeploymentVersion=deploymentVersion;
            Running=device.Running;
        }
        public Guid DeviceRef {get;private set;}
        public string ScopeId {get;private set;}
        public string ConfigVersion {get;private set;}
        public string Generation {get;private set;}
        public string DeploymentVersion {get;private set;}
        public bool Running {get;private set;}
    }
    public sealed class PHMScadaPointObservation
    {
        internal PHMScadaPointObservation(PHMScadaPipePoint point)
        {
            if(point==null||String.IsNullOrWhiteSpace(point.StablePointId)||
                String.IsNullOrWhiteSpace(point.VariableId)||point.Revision<0)
                throw new InvalidDataException("hmi.pipe.invalid_point");
            MDSVariableQuality quality;
            if(!Enum.TryParse(point.Quality,true,out quality)||
                !Enum.IsDefined(typeof(MDSVariableQuality),quality))
                throw new InvalidDataException("hmi.pipe.quality_invalid");
            // A合同：未采样HasValue=false时RefreshUtc/ChangeUtc为null，不是零时间。
            DateTime refreshed=default(DateTime);
            if(point.HasValue &&
                !DateTime.TryParse(point.RefreshUtc,CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,out refreshed))
                throw new InvalidDataException("hmi.pipe.timestamp_invalid");
            StablePointId=point.StablePointId;VariableId=point.VariableId;
            ValueTypeName=point.Type;
            Unit=point.Unit??"";Source=point.SourceId??"";
            Revision=point.Revision;RefreshUtc=refreshed.ToUniversalTime();
            Quality=quality;HasValue=point.HasValue;
            Value=point.HasValue?ParseValue(point.Type,point.Value):null;
            Text=!HasValue?"—":Convert.ToString(Value,CultureInfo.InvariantCulture);
        }
        public string StablePointId {get;private set;}
        public string VariableId {get;private set;}
        public string ValueTypeName {get;private set;}
        public string Unit {get;private set;}
        public string Source {get;private set;}
        public long Revision {get;private set;}
        public DateTime RefreshUtc {get;private set;}
        public MDSVariableQuality Quality {get;private set;}
        public bool HasValue {get;private set;}
        public object Value {get;private set;}
        public string Text {get;private set;}
        private static object ParseValue(string type,string text)
        {
            if(text==null)throw new InvalidDataException("hmi.pipe.value_missing");
            switch(type)
            {
                case "UInt16":return ushort.Parse(text,NumberStyles.Integer,CultureInfo.InvariantCulture);
                case "Int16":return short.Parse(text,NumberStyles.Integer,CultureInfo.InvariantCulture);
                case "UInt32":return uint.Parse(text,NumberStyles.Integer,CultureInfo.InvariantCulture);
                case "Int32":return int.Parse(text,NumberStyles.Integer,CultureInfo.InvariantCulture);
                case "Byte":return byte.Parse(text,NumberStyles.Integer,CultureInfo.InvariantCulture);
                case "SByte":return sbyte.Parse(text,NumberStyles.Integer,CultureInfo.InvariantCulture);
                case "Boolean":return bool.Parse(text);
                case "Double":return double.Parse(text,NumberStyles.Float,CultureInfo.InvariantCulture);
                case "Single":return float.Parse(text,NumberStyles.Float,CultureInfo.InvariantCulture);
                case "String":return text;
                default:throw new InvalidDataException("hmi.pipe.unsupported_type:"+type);
            }
        }
    }
    public sealed class PHMScadaDeviceSnapshot
    {
        internal PHMScadaDeviceSnapshot(PHMScadaDeviceIdentity identity,PHMScadaPipeDevice device)
        {
            if(!identity.Running||device==null||!device.Running||
                device.DeviceRef!=identity.DeviceRef.ToString("N")||
                device.ScopeId!=identity.ScopeId||
                device.ConfigVersion!=identity.ConfigVersion||
                device.Generation!=identity.Generation||device.Points==null||
                device.Points.Length>64)
                throw new InvalidDataException("hmi.pipe.snapshot_identity_mismatch");
            var points=new List<PHMScadaPointObservation>();
            var names=new HashSet<string>(StringComparer.Ordinal);
            foreach(PHMScadaPipePoint point in device.Points)
            {
                PHMScadaPointObservation sample=new PHMScadaPointObservation(point);
                if(!names.Add(sample.StablePointId))
                    throw new InvalidDataException("hmi.pipe.duplicate_point");
                points.Add(sample);
            }
            Identity=identity;Points=points.AsReadOnly();
        }
        public PHMScadaDeviceIdentity Identity {get;private set;}
        public IReadOnlyList<PHMScadaPointObservation> Points {get;private set;}
    }

    /// <summary>
    /// Windows同账户只读Pipe的独立客户端。每次Exchange仅一次请求/回复；
    /// 任何版本漂移拒绝，不接受Host写入，不保存第二份可写实时变量。
    /// </summary>
    public sealed class PHMScadaReadOnlyPipeClient
    {
        private readonly string _pipeName;
        public PHMScadaReadOnlyPipeClient(string pipeName)
        {
            if(String.IsNullOrWhiteSpace(pipeName)||pipeName.Length>80)
                throw new ArgumentException("hmi.pipe.invalid_name","pipeName");
            foreach(char c in pipeName)
                if(!((c>='A'&&c<='Z')||(c>='a'&&c<='z')||
                    (c>='0'&&c<='9')||c=='_'||c=='-'||c=='.'))
                    throw new ArgumentException("hmi.pipe.invalid_name","pipeName");
            _pipeName=pipeName;
        }
        public IReadOnlyList<PHMScadaDeviceIdentity> Discover()
        {
            PHMScadaPipeReply reply=Exchange(new PHMScadaPipeRequest{
                SchemaVersion=1,Operation="discover"});
            if(reply.Devices==null||reply.Devices.Length>256)
                throw new InvalidDataException("hmi.pipe.discovery_budget");
            var devices=new List<PHMScadaDeviceIdentity>();
            var ids=new HashSet<Guid>();
            foreach(PHMScadaPipeDevice item in reply.Devices)
            {
                PHMScadaDeviceIdentity identity=new PHMScadaDeviceIdentity(
                    reply.DeploymentVersion,item);
                if(!ids.Add(identity.DeviceRef))
                    throw new InvalidDataException("hmi.pipe.duplicate_device");
                devices.Add(identity);
            }
            return devices.AsReadOnly();
        }
        public PHMScadaDeviceSnapshot Snapshot(PHMScadaDeviceIdentity identity)
        {
            if(identity==null)throw new ArgumentNullException("identity");
            if(!identity.Running)throw new InvalidOperationException("hmi.pipe.device_not_running");
            PHMScadaPipeReply reply=Exchange(new PHMScadaPipeRequest{
                SchemaVersion=1,Operation="snapshot",
                DeviceRef=identity.DeviceRef.ToString("N"),
                ExpectedDeploymentVersion=identity.DeploymentVersion,
                ExpectedConfigVersion=identity.ConfigVersion,
                ExpectedGeneration=identity.Generation});
            if(reply.DeploymentVersion!=identity.DeploymentVersion||
                reply.Devices==null||reply.Devices.Length!=1)
                throw new InvalidDataException("hmi.pipe.reply_version_or_count");
            return new PHMScadaDeviceSnapshot(identity,reply.Devices[0]);
        }
        private PHMScadaPipeReply Exchange(PHMScadaPipeRequest request)
        {
            using(NamedPipeClientStream pipe=new NamedPipeClientStream(".",
                _pipeName,PipeDirection.InOut,PipeOptions.None))
            {
                pipe.Connect(3000);pipe.ReadTimeout=3000;pipe.WriteTimeout=3000;
                byte[] bytes;
                using(MemoryStream output=new MemoryStream())
                {
                    new DataContractJsonSerializer(typeof(PHMScadaPipeRequest))
                        .WriteObject(output,request);
                    bytes=output.ToArray();
                }
                if(bytes.Length<1||bytes.Length>4096)
                    throw new InvalidDataException("hmi.pipe.request_budget");
                byte[] header=BitConverter.GetBytes(bytes.Length);
                pipe.Write(header,0,header.Length);
                pipe.Write(bytes,0,bytes.Length);
                pipe.Flush();
                byte[] size=new byte[4];ReadExactly(pipe,size);
                int length=BitConverter.ToInt32(size,0);
                if(length<1||length>1048576)
                    throw new InvalidDataException("hmi.pipe.reply_budget");
                byte[] payload=new byte[length];ReadExactly(pipe,payload);
                PHMScadaPipeReply result;
                using(MemoryStream input=new MemoryStream(payload,false))
                    result=(PHMScadaPipeReply)new DataContractJsonSerializer(
                        typeof(PHMScadaPipeReply)).ReadObject(input);
                if(result==null||result.SchemaVersion!=1)
                    throw new InvalidDataException("hmi.pipe.schema_invalid");
                if(result.Status=="Rejected")
                    throw new PHMScadaPipeRejectedException(
                        result.ErrorCode??"HOST_REJECTED");
                if(result.Status!="Ok")throw new InvalidDataException("hmi.pipe.status_invalid");
                return result;
            }
        }
        private static void ReadExactly(Stream input,byte[] bytes)
        {
            int read=0;
            while(read<bytes.Length)
            {
                int n=input.Read(bytes,read,bytes.Length-read);
                if(n<=0)throw new EndOfStreamException("hmi.pipe.truncated_reply");
                read+=n;
            }
        }
    }
}
