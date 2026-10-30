using System;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Mechanism.Data.Variables;

namespace CxVisual.Runtime.Host
{
    // 本地同Windows账户、显式启用、只读的有界观察端点。无写入/重放入口。
    [DataContract] internal sealed class MRHScadaPipeRequest
    {
        [DataMember] public int SchemaVersion {get;set;}
        [DataMember] public string Operation {get;set;}
        [DataMember] public string DeviceRef {get;set;}
        [DataMember] public string ExpectedDeploymentVersion {get;set;}
        [DataMember] public string ExpectedConfigVersion {get;set;}
        [DataMember] public string ExpectedGeneration {get;set;}
    }
    [DataContract] internal sealed class MRHScadaPipePoint
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
    [DataContract] internal sealed class MRHScadaPipeDevice
    {
        [DataMember] public string DeviceRef {get;set;}
        [DataMember] public string ScopeId {get;set;}
        [DataMember] public string ConfigVersion {get;set;}
        [DataMember] public string Generation {get;set;}
        [DataMember] public bool Running {get;set;}
        [DataMember] public MRHScadaPipePoint[] Points {get;set;}
    }
    [DataContract] internal sealed class MRHScadaPipeReply
    {
        [DataMember] public int SchemaVersion {get;set;}
        [DataMember] public string Status {get;set;}
        [DataMember] public string ErrorCode {get;set;}
        [DataMember] public string DeploymentVersion {get;set;}
        [DataMember] public MRHScadaPipeDevice[] Devices {get;set;}
    }
    internal static class MRHScadaReadOnlyPipe
    {
        private const int MaxRequest=4096, MaxReply=1048576;

        private static void ValidateName(string name)
        {
            if(String.IsNullOrEmpty(name)||name.Length>80)
                throw new ArgumentException("IPC Pipe名必须为1～80个字符");
            foreach(char c in name)
                if(!((c>='A'&&c<='Z')||(c>='a'&&c<='z')||(c>='0'&&c<='9')||
                    c=='_'||c=='-'||c=='.'))
                    throw new ArgumentException("IPC Pipe名仅允许ASCII字母、数字、_.-");
        }

        private static PipeSecurity OwnerOnly()
        {
            SecurityIdentifier id=WindowsIdentity.GetCurrent().User;
            if(id==null)throw new UnauthorizedAccessException("IPC无法确认运行账户");
            PipeSecurity acl=new PipeSecurity();
            acl.SetAccessRuleProtection(true,false);
            acl.AddAccessRule(new PipeAccessRule(id,
                PipeAccessRights.ReadWrite|PipeAccessRights.CreateNewInstance,
                AccessControlType.Allow));
            return acl;
        }

        private static void ReadFully(Stream stream,byte[] bytes)
        {
            int offset=0;
            while(offset<bytes.Length)
            {
                int n=stream.Read(bytes,offset,bytes.Length-offset);
                if(n<=0)throw new EndOfStreamException("IPC消息截断");
                offset+=n;
            }
        }
        private static T Receive<T>(Stream stream,int capacity)
        {
            byte[] header=new byte[4];
            ReadFully(stream,header);
            int length=BitConverter.ToInt32(header,0);
            if(length<1||length>capacity)throw new InvalidDataException("IPC帧超限");
            byte[] bytes=new byte[length];
            ReadFully(stream,bytes);
            using(MemoryStream input=new MemoryStream(bytes,false))
                return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(input);
        }
        private static void Send<T>(Stream stream,T message,int capacity)
        {
            byte[] bytes;
            using(MemoryStream output=new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(T)).WriteObject(output,message);
                bytes=output.ToArray();
            }
            if(bytes.Length>capacity)throw new InvalidDataException("IPC回复超限");
            byte[] header=BitConverter.GetBytes(bytes.Length);
            stream.Write(header,0,header.Length);
            stream.Write(bytes,0,bytes.Length);
            stream.Flush();
        }
        private static MRHScadaPipeReply Reject(string error)
        {
            return new MRHScadaPipeReply{SchemaVersion=1,Status="Rejected",
                ErrorCode=error,Devices=new MRHScadaPipeDevice[0]};
        }

        internal static MRHScadaPipeReply Respond(MRHScadaRuntimeGroup group,MRHScadaPipeRequest req)
        {
            if(group==null)throw new ArgumentNullException("group");
            if(req==null||req.SchemaVersion!=1)return Reject("SCHEMA_UNSUPPORTED");
            if(!group.IsDeploymentApproved)return Reject("DEPLOYMENT_STALE");
            MRHScadaRuntime[] devices=group.SnapshotDevices();
            if(req.Operation=="discover")
                return new MRHScadaPipeReply{SchemaVersion=1,Status="Ok",
                    DeploymentVersion=group.DeploymentVersion,
                    Devices=devices.Select(x=>new MRHScadaPipeDevice{
                        DeviceRef=x.DeviceRef,ScopeId=x.ScopeId,
                        ConfigVersion=x.ConfigVersion,Generation=x.Generation,
                        Running=x.IsRunning&&x.IsStillApproved,
                        Points=new MRHScadaPipePoint[0]}).ToArray()};
            if(req.Operation!="snapshot")return Reject("READ_ONLY_OPERATION");
            if(String.IsNullOrEmpty(group.DeploymentVersion)||
                req.ExpectedDeploymentVersion!=group.DeploymentVersion)
                return Reject("DEPLOYMENT_VERSION_MISMATCH");
            Guid guid;
            if(!Guid.TryParse(req.DeviceRef,out guid)||guid==Guid.Empty)
                return Reject("DEVICE_ID_INVALID");
            MRHScadaRuntime device=devices.FirstOrDefault(x=>x.DeviceRef==guid.ToString("N"));
            if(device==null)return Reject("DEVICE_NOT_FOUND");
            if(!device.IsRunning||!device.IsStillApproved)return Reject("SESSION_NOT_APPROVED");
            if(String.IsNullOrEmpty(req.ExpectedConfigVersion)||
                String.IsNullOrEmpty(req.ExpectedGeneration)||
                req.ExpectedConfigVersion!=device.ConfigVersion||
                req.ExpectedGeneration!=device.Generation)
                return Reject("SESSION_VERSION_MISMATCH");
            MRHScadaPipePoint[] points=device.Device.PointDefinitions.Select(def=>
            {
                IMDSVariableSnapshot sample=device.Device.Inspect(def.StableId);
                return new MRHScadaPipePoint{
                    StablePointId=def.StableId,VariableId=sample.VariableId,
                    Type=def.ValueType.Name,Unit=def.Unit,HasValue=sample.HasValue,
                    Value=sample.HasValue?
                        Convert.ToString(sample.CurrentObject,CultureInfo.InvariantCulture):null,
                    Quality=sample.Quality.ToString(),Revision=sample.Revision,
                    RefreshUtc=sample.RefreshUtc.ToUniversalTime().ToString("O",CultureInfo.InvariantCulture),
                    SourceId=sample.Source};
            }).ToArray();
            // 快照取得后仍须再次核验，禁止把失效会话返回为Good。
            if(!group.IsDeploymentApproved||!device.IsRunning||!device.IsStillApproved||
                req.ExpectedGeneration!=device.Generation)return Reject("SNAPSHOT_STALE");
            return new MRHScadaPipeReply{SchemaVersion=1,Status="Ok",
                DeploymentVersion=group.DeploymentVersion,
                Devices=new[]{new MRHScadaPipeDevice{
                    DeviceRef=device.DeviceRef,ScopeId=device.ScopeId,
                    ConfigVersion=device.ConfigVersion,Generation=device.Generation,
                    Running=true,Points=points}}};
        }

        internal static Task RunAsync(MRHScadaRuntimeGroup group,string name,
            CancellationToken cancellationToken)
        {
            if(group==null)throw new ArgumentNullException("group");
            ValidateName(name);
            if(String.IsNullOrEmpty(group.DeploymentVersion))
                throw new InvalidOperationException("IPC需要正式项目部署Effective");
            PipeSecurity acl=OwnerOnly();
            return Task.Run(()=>
            {
                while(!cancellationToken.IsCancellationRequested)
                {
                    using(NamedPipeServerStream server=new NamedPipeServerStream(name,
                        PipeDirection.InOut,1,PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous,4096,4096,acl))
                    using(CancellationTokenRegistration registration=cancellationToken.Register(()=>
                    {
                        try{server.Dispose();}catch(ObjectDisposedException){}
                    }))
                    {
                        try
                        {
                            server.WaitForConnection();
                            if(cancellationToken.IsCancellationRequested)break;
                            server.ReadTimeout=3000;
                            server.WriteTimeout=3000;
                            MRHScadaPipeRequest req=Receive<MRHScadaPipeRequest>(server,MaxRequest);
                            Send(server,Respond(group,req),MaxReply);
                        }
                        catch(IOException){if(cancellationToken.IsCancellationRequested)break;}
                        catch(ObjectDisposedException){if(cancellationToken.IsCancellationRequested)break;}
                        catch(SerializationException){} // 不合法输入不执行任何动作
                        catch(InvalidDataException){} // 有界帧拒绝
                    }
                }
            },cancellationToken);
        }

        // 仅用于真实Windows Pipe G1集成测试，生产HMI自行消费公开的v1线协议。
        internal static MRHScadaPipeReply ExchangeForSmoke(string name,MRHScadaPipeRequest req)
        {
            using(NamedPipeClientStream client=new NamedPipeClientStream(".",
                name,PipeDirection.InOut,PipeOptions.None))
            {
                client.Connect(3000);
                client.ReadTimeout=3000;
                client.WriteTimeout=3000;
                Send(client,req,MaxRequest);
                return Receive<MRHScadaPipeReply>(client,MaxReply);
            }
        }
    }
}
