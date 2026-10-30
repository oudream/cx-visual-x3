using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Security.Principal;
using System.Text;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Transport;
using CxVisual.Mechanism.Connectivity;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Data.Configuration;
using CxVisual.Mechanism.Data.Variables;
using CxVisual.Mechanism.Runtime.Commands;
using CxVisual.Professional.Automation.Scada;
using CxVisual.Professional.Automation.Plc;

namespace CxVisual.Runtime.Host
{
    /// <summary>只在显式scada-g1-smoke运行：真实OS TCP + MEM工程保存/重开 + MHS Effective + Host重启。
    /// 纯本机测试PLC，不伪造真实硬件G2或安全证据。</summary>
    internal static class MRHScadaSmoke
    {
        private static void Check(bool value,string message)
        {if(!value)throw new InvalidOperationException("SCADA-G1: "+message);}
        private static void Reject<TException>(Action action) where TException:Exception
        {
            try{action();}
            catch(TException){return;}
            throw new InvalidOperationException("SCADA-G1 expected "+typeof(TException).Name);
        }
        // 用真实Windows子进程调用同一Host exe的只读Probe。
        // 主测试进程持续提供OS TCP PLC和Pipe服务，子进程不持有MDS/Connector对象。
        private static MRHScadaPipeReply ProbeFromProcess(int expectedExitCode,
            params string[] arguments)
        {
            if(arguments==null||arguments.Length==0)
                throw new ArgumentException("缺少SCADA Pipe子进程参数");
            List<string> tokens=new List<string>();
            tokens.Add("scada-pipe-probe");
            foreach(string item in arguments)
            {
                // 此测试只使用ASCII Pipe名、GUID、vN；禁止通过参数插入额外CLI操作。
                if(String.IsNullOrEmpty(item)||item.Any(ch=>!Char.IsLetterOrDigit(ch)&&
                    ch!='-'&&ch!='_'&&ch!='.'))
                    throw new InvalidOperationException("SCADA G1 子进程参数不合法");
                tokens.Add(item);
            }
            string exe=Process.GetCurrentProcess().MainModule.FileName;
            ProcessStartInfo start=new ProcessStartInfo(exe,String.Join(" ",tokens));
            start.UseShellExecute=false;
            start.CreateNoWindow=true;
            start.RedirectStandardOutput=true;
            start.RedirectStandardError=true;
            using(Process child=Process.Start(start))
            {
                if(child==null)throw new InvalidOperationException("SCADA G1无法启动真实子进程");
                Task<string> output=child.StandardOutput.ReadToEndAsync();
                Task<string> error=child.StandardError.ReadToEndAsync();
                if(!child.WaitForExit(10000))
                {
                    try{child.Kill();}catch(InvalidOperationException){}
                    child.WaitForExit(3000);
                    throw new TimeoutException("SCADA Pipe真实子进程10秒内未结束");
                }
                Task.WhenAll(output,error).GetAwaiter().GetResult();
                Check(child.ExitCode==expectedExitCode,
                    "跨进程Pipe探针退出码异常: expected="+expectedExitCode+
                    " actual="+child.ExitCode+" stderr="+error.Result);
                byte[] bytes=Encoding.UTF8.GetBytes(output.Result.Trim());
                using(MemoryStream stream=new MemoryStream(bytes,false))
                    return (MRHScadaPipeReply)
                        new DataContractJsonSerializer(typeof(MRHScadaPipeReply))
                            .ReadObject(stream);
            }
        }

        // A5部署合同：真正运行第二个Runtime.Host.exe进程，而非在G1内部直接
        // 调用预检类。安全地转义Windows命令行路径（可以包含空格）。
        private static MRHScadaEngineeringInspection PreflightFromProcess(
            string projectPath,string configRoot,int expectedExit)
        {
            if(projectPath.IndexOf('"')>=0||configRoot.IndexOf('"')>=0)
                throw new ArgumentException("SCADA G1工程路径包含不支持的引号");
            string args="scada-project-check \""+projectPath+"\" \""+configRoot+"\"";
            ProcessStartInfo start=new ProcessStartInfo(
                Process.GetCurrentProcess().MainModule.FileName,args);
            start.UseShellExecute=false;
            start.CreateNoWindow=true;
            start.RedirectStandardOutput=true;
            start.RedirectStandardError=true;
            using(Process child=Process.Start(start))
            {
                if(child==null)throw new InvalidOperationException("不能启动独立SCADA工程预检进程");
                Task<string> stdout=child.StandardOutput.ReadToEndAsync();
                Task<string> stderr=child.StandardError.ReadToEndAsync();
                if(!child.WaitForExit(12000))
                {
                    try{child.Kill();}catch(InvalidOperationException){}
                    child.WaitForExit(3000);
                    throw new TimeoutException("独立SCADA工程预检进程没有按时退出");
                }
                Task.WhenAll(stdout,stderr).GetAwaiter().GetResult();
                Check(child.ExitCode==expectedExit,
                    "独立工程预检退出码不符合预期：expected="+expectedExit+
                    " actual="+child.ExitCode+" error="+stderr.Result);
                if(expectedExit!=0)
                {
                    Check(String.IsNullOrWhiteSpace(stdout.Result),
                        "工程预检失败不能输出误导性的有效部署JSON");
                    return null;
                }
                Check(!String.IsNullOrWhiteSpace(stdout.Result) &&
                    String.IsNullOrWhiteSpace(stderr.Result),
                    "工程预检成功必须只在stdout返回部署JSON");
                using(MemoryStream stream=new MemoryStream(
                    Encoding.UTF8.GetBytes(stdout.Result.Trim()),false))
                    return (MRHScadaEngineeringInspection)
                        new DataContractJsonSerializer(
                            typeof(MRHScadaEngineeringInspection)).ReadObject(stream);
            }
        }

        private sealed class TcpPlcFixture : IDisposable
        {
            private readonly TcpListener _listener;
            private int _wordValue;
            private readonly ushort _inputValue;
            private readonly bool _allowWrites;
            private int _ignoreNextWrite;
            private readonly Task _server;
            private volatile bool _stop;
            private TcpClient _active;
            private int _reads;
            private int _dropReplies;
            private int _writes;
            public int Port {get;private set;}
            public int Reads {get{return Volatile.Read(ref _reads);}}
            public int Writes {get{return Volatile.Read(ref _writes);}}
            public ushort ActualWord {get{return (ushort)Volatile.Read(ref _wordValue);}}
            public void IgnoreNextWrite()
            {
                if(!_allowWrites)throw new InvalidOperationException("只读PLC不可设写入故障");
                Interlocked.Exchange(ref _ignoreNextWrite,1);
            }
            public void DropNextReplies(int count)
            {
                if(count<1||count>8)throw new ArgumentOutOfRangeException("count");
                Interlocked.Exchange(ref _dropReplies,count);
            }
            public TcpPlcFixture(ushort wordValue = 73, ushort inputValue = 99,
                bool allowWrites = false)
            {
                _wordValue=wordValue;_inputValue=inputValue;_allowWrites=allowWrites;
                _listener=new TcpListener(IPAddress.Loopback,0);
                _listener.Start();
                Port=((IPEndPoint)_listener.LocalEndpoint).Port;
                // TCP夹具是整个测试期间同步阻塞Accept/Read的OS socket服务，
                // 不得占用默认线程池长期驻留，避免多PLC+FCR并行时饥饿。
                // LongRunning只属于G1夹具，生产PLC周期仍复用正式FCR。
                _server=Task.Factory.StartNew((Action)Serve,
                    CancellationToken.None,TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);
            }
            private static bool Read(NetworkStream stream,byte[] bytes)
            {
                int offset=0;
                while(offset<bytes.Length)
                {
                    int n=stream.Read(bytes,offset,bytes.Length-offset);
                    if(n<=0)return false;
                    offset+=n;
                }
                return true;
            }
            private void Serve()
            {
                while(!_stop)
                {
                    try
                    {
                        using(TcpClient client=_listener.AcceptTcpClient())
                        using(NetworkStream stream=client.GetStream())
                        {
                            _active=client;client.NoDelay=true;
                            while(!_stop)
                            {
                                byte[] mbap=new byte[7];
                                if(!Read(stream,mbap))break;
                                int length=(mbap[4]<<8)|mbap[5];
                                if(length!=6||mbap[2]!=0||mbap[3]!=0)
                                    throw new InvalidDataException("SCADA夹具MBAP无效");
                                byte[] request=new byte[5];
                                if(!Read(stream,request))break;
                                if(request[0]==6 && _allowWrites)
                                {
                                    int register=(request[1]<<8)|request[2];
                                    ushort next=(ushort)((request[3]<<8)|request[4]);
                                    if(register!=100)
                                        throw new InvalidDataException("SCADA测试夹具拒绝越权FC06地址");
                                    Interlocked.Increment(ref _writes);
                                    if(Interlocked.Exchange(ref _ignoreNextWrite,0)==0)
                                        Volatile.Write(ref _wordValue,(int)next);
                                    // FC06必须原样回显TransactionId/UnitId/地址和写入值；
                                    // ACK不保证现场值已改变，后续正式FC03另作回读确认。
                                    byte[] writeAck=new byte[12];
                                    Array.Copy(mbap,0,writeAck,0,7);
                                    Array.Copy(request,0,writeAck,7,5);
                                    stream.Write(writeAck,0,writeAck.Length);
                                    continue;
                                }
                                if(request[0]!=3)
                                {
                                    Interlocked.Increment(ref _writes);
                                    throw new InvalidDataException("只读SCADA夹具拒绝非FC03");
                                }
                                int first=(request[1]<<8)|request[2];
                                int count=(request[3]<<8)|request[4];
                                // 常规双工站G1仍只允许FC03(count=2)；
                                // A4启用可写点后，已有PAD写回读必须独立发FC03(count=1)。
                                // 测试夹具要根据真实MBAP点数返回合法帧，不能把
                                // 专业命令的正常单寄存器回读误报为非法连续扫描。
                                if(first!=100 || (count!=2 && !(_allowWrites&&count==1)))
                                    throw new InvalidDataException("SCADA FC03地址/数量异常");
                                byte[] response=new byte[9+count*2];
                                Array.Copy(mbap,0,response,0,7);
                                response[4]=0;response[5]=(byte)(3+count*2);
                                response[7]=3;response[8]=(byte)(count*2);
                                ushort current=(ushort)Volatile.Read(ref _wordValue);
                                response[9]=(byte)(current>>8);response[10]=(byte)current;
                                if(count==2)
                                {
                                    response[11]=(byte)(_inputValue>>8);
                                    response[12]=(byte)_inputValue;
                                }
                                Interlocked.Increment(ref _reads);
                                if(Volatile.Read(ref _dropReplies)>0)
                                {
                                    Interlocked.Decrement(ref _dropReplies);
                                    continue; // TCP请求已真实到达，但模拟现场设备未响应。
                                }
                                stream.Write(response,0,response.Length);
                            }
                        }
                    }
                    catch(SocketException) {if(!_stop)throw;}
                    catch(IOException) {if(!_stop)throw;}
                    catch(ObjectDisposedException) {if(!_stop)throw;}
                }
            }
            public void Dispose()
            {
                _stop=true;
                if(_active!=null)_active.Close();
                _listener.Stop();
                if(!_server.Wait(4000))throw new TimeoutException("SCADA测试夹具未退出");
            }
        }

        private static void ConfigureDevice(string configRoot,string moduleId,string scopeId,
            string channelId,int port,bool writable = false)
        {
            MHSConfigService store=new MHSConfigService(configRoot);
            var section=store.ForScope("module",moduleId)
                .Open<MCNConnectorConfiguration>();
            var saved=section.UpdateAsync(model=>
            {
                model.DisplayName="SCADA工业设备 "+moduleId;
                model.Channel=new FTRTcpClientChannelConfiguration{
                    Id=channelId,Kind=FTRChannelKind.TcpClient,
                    Host="127.0.0.1",Port=port,ConnectTimeoutMs=2000
                };
                model.Protocol=new MPAProtocolConfiguration("protocol-"+moduleId,"modbus.tcp");
                model.Protocol.TimeoutMs=550;
                model.Protocol.Points.Add(new MPAPointDefinition("word100","holding:100",
                    typeof(ushort)){StationAddress="1",CanRead=true,CanWrite=writable});
                model.Protocol.Points.Add(new MPAPointDefinition("input101","holding:101",
                    typeof(ushort)){StationAddress="1",CanRead=true,CanWrite=false});
                foreach(string name in new[]{"word100","input101"})
                    model.Protocol.Mappings.Add(new MPADataMapping{
                        PointRef=name,TargetKind=MPADataTargetKind.Variable,
                        TargetRef=scopeId+"/Station."+(name=="word100"?"Word":"Input"),
                        Direction=MPADataDirection.Inbound,
                        Operation=MPADataOperation.WriteValue,Required=true});
                if(writable)
                    model.Protocol.Mappings.Add(new MPADataMapping{
                        PointRef="word100",TargetKind=MPADataTargetKind.Variable,
                        TargetRef=scopeId+"/Station.Word",
                        Direction=MPADataDirection.Outbound,
                        Operation=MPADataOperation.ReadValue,Required=true});
            },"SCADA多设备正式配置",CancellationToken.None).GetAwaiter().GetResult();
            Check(saved.Success,"多设备MHS保存失败: "+saved.ErrorCode);
            if(section.HasPendingApply)
                Check(section.ApplyPendingAtTaskBoundary(),"多设备MHS未能生效");
        }

        // A4真实独立Host装配：MAC/MHS保存工程 + 正式MRKCommandCenter + PAD专业双授权，
        // Windows OS TCP上确实执行FC06、FC03回读及Unknown/Stop拒写；不是危险硬件HIL。
        private static void RunAuthorizedDevice(string root)
        {
            using(TcpPlcFixture plc=new TcpPlcFixture(73,99,allowWrites:true))
            {
                string project=Path.Combine(root,"authorized.cxproj");
                string configs=Path.Combine(root,"authorized-configs");
                const string module="connector-authorized";
                const string scope="authorizedStation";
                Guid projectId,deviceId;
                using(MEMEngineeringSession engineering=MEMEngineering.Create(
                    "A4已批准PLC工站工程"))
                {
                    projectId=engineering.Project.Id;
                    deviceId=engineering.AddPage("ControlledPLC").Id;
                    engineering.SaveAs(project);
                }
                ConfigureDevice(configs,module,scope,"authorized-channel",plc.Port,writable:true);
                MHSConfigService store=new MHSConfigService(configs);
                var deployment=store.ForProject(projectId.ToString("N"))
                    .Open<PADScadaDeploymentConfiguration>();
                var saved=deployment.UpdateAsync(model=>
                {
                    model.Devices.Clear();
                    model.Devices.Add(new PADScadaDeviceInstallation{
                        DeviceNodeGuid=deviceId.ToString("N"),ConnectorModuleId=module,
                        ScopeId=scope,IntervalMs=100,Contiguous=true});
                },"受控PLC正式保存工程与部署",CancellationToken.None)
                    .GetAwaiter().GetResult();
                Check(saved.Success,"A4测试没有保存正式SCADA部署");
                if(deployment.HasPendingApply)
                    Check(deployment.ApplyPendingAtTaskBoundary(),"A4测试SCADA部署未生效");
                string pinnedProject=deployment.EffectiveVersion;
                object approvalGate=new object();
                Func<bool> approved=()=>
                {
                    lock(approvalGate)
                    {
                        try
                        {
                            var live=new MHSConfigService(configs)
                                .OpenExisting<PADScadaDeploymentConfiguration>(
                                    new CxVisual.Foundation.Data.Configuration.FCMConfigIdentity(
                                        "CxVisual.Automation.ScadaDeployment",
                                        "project",projectId.ToString("N")));
                            return !live.HasPendingApply &&
                                live.EffectiveVersion==pinnedProject;
                        }
                        catch{return false;}
                    }
                };
                // 未经项目部署批准即使传入允许写的delegate，也不准装配。
                Reject<UnauthorizedAccessException>(()=>MRHScadaRuntime.Open(
                    project,configs,deviceId,module,scope,100,true,
                    authorizeWrite:(principal,point)=>true));
                // A4可信IPC使用Windows OS认证得到的peer，绝不信任报文Caller。
                // 测试中仍保留既有进程内operator回归；部署授权必须额外检查角色/模式/联锁。
                string actualPeer=WindowsIdentity.GetCurrent().Name;
                Check(!String.IsNullOrWhiteSpace(actualPeer),
                    "Windows MRK受信Pipe必须取得真实宿主账户");
                bool peerAllowed=true,modeAllowed=true,revokeImmediatelyAfterAdmission=false;
                string mrkPipe="CxScada.A4."+Guid.NewGuid().ToString("N");
                using(MRHScadaRuntime host=MRHScadaRuntime.Open(project,configs,
                    deviceId,module,scope,100,true,additionalApproval:approved,
                    authorizeWrite:(principal,point)=>
                        (principal=="operator" ||
                         String.Equals(principal,actualPeer,StringComparison.OrdinalIgnoreCase)) &&
                         point.StableId=="word100"))
                using(MRHScadaTrustedCommandPipe trusted=new MRHScadaTrustedCommandPipe(
                    host,mrkPipe,970731,
                    peer=>peerAllowed&&String.Equals(peer,actualPeer,
                        StringComparison.OrdinalIgnoreCase),
                    context=>
                    {
                        bool admitted=modeAllowed &&
                            (context.Caller=="operator" ||
                             String.Equals(context.Caller,actualPeer,
                                 StringComparison.OrdinalIgnoreCase)) &&
                            context.Descriptor.Definition.PermissionKey==
                                PADPlcMrkCommandRegistration.WritePermission;
                        // Adversarial: authorization passed, but immediately
                        // after MRK admission the control mode is revoked.
                        // PAD must recheck before a physical FC06.
                        if(admitted && revokeImmediatelyAfterAdmission)
                        {
                            revokeImmediatelyAfterAdmission=false;
                            modeAllowed=false;
                        }
                        return admitted;
                    },
                    currentControlGuard:()=>modeAllowed))
                {
                    MRKCommandCenter commands=trusted.Commands;
                    host.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
                    trusted.Start();
                    Check(SpinWait.SpinUntil(()=>host.CompletedCycles>=2,8000) &&
                        host.Device.Inspect("word100").Quality==MDSVariableQuality.Good &&
                        plc.Writes==0,"A4受信Host启动前必须真实读取PLC且尚无写命令");
                    string target=PADPlcMrkCommandRegistration.TargetForDevice(
                        deviceId.ToString("N"));
                    string version=host.ConfigVersion;
                    Func<string,ushort,string,string,Guid,MRKCommandRequest> request=
                        (caller,value,point,expected,operation)=>
                            new MRKCommandRequest(target,
                                PADPlcMrkCommandRegistration.WriteUInt16Code,
                                new System.Collections.Generic.Dictionary<string,object>{
                                    {"StableId",point},{"Value",value},
                                    {"EffectiveVersion",expected},
                                    {"SessionGeneration",host.Generation}
                                },caller:caller,operationId:operation,
                                expectedSchemaVersion:1);
                    int before=plc.Writes;
                    MRKCommandResponse guest=commands.InvokeAsync(request(
                        "guest",84,"word100",version,Guid.NewGuid()))
                        .GetAwaiter().GetResult();
                    Check(guest.Stage==MRKCommandStage.Rejected &&
                        guest.ErrorCode=="Unauthorized"&&plc.Writes==before,
                        "A4未认证MRK角色必须在FC06前拒绝");
                    MRKCommandResponse stale=commands.InvokeAsync(request(
                        "operator",84,"word100","stale-version",Guid.NewGuid()))
                        .GetAwaiter().GetResult();
                    Check(stale.BusinessSuccess!=true&&plc.Writes==before,
                        "A4旧Connector Effective不得发送FC06");
                    MRKCommandResponse staleGeneration=commands.InvokeAsync(
                        new MRKCommandRequest(target,
                            PADPlcMrkCommandRegistration.WriteUInt16Code,
                            new System.Collections.Generic.Dictionary<string,object>{
                                {"StableId","word100"},{"Value",(ushort)84},
                                {"EffectiveVersion",version},
                                {"SessionGeneration",Guid.NewGuid().ToString("N")}
                            },caller:"operator",expectedSchemaVersion:1))
                        .GetAwaiter().GetResult();
                    Check(staleGeneration.BusinessSuccess!=true && plc.Writes==before,
                        "A4旧F17 Generation即使工程版本相同也不得触发FC06");
                    Guid operationId=Guid.NewGuid();
                    MRKCommandResponse written=commands.InvokeAsync(request(
                        "operator",84,"word100",version,operationId))
                        .GetAwaiter().GetResult();
                    Check(written.Stage==MRKCommandStage.Accepted &&
                        written.State==MRKCommandState.Completed &&
                        written.BusinessSuccess==true &&
                        written.BusinessCode=="PLC_REGISTER_READBACK_CONFIRMED" &&
                        plc.ActualWord==84&&plc.Writes==before+1 &&
                        SpinWait.SpinUntil(()=>
                            host.Device.Inspect("word100").HasValue &&
                            object.Equals(host.Device.Inspect("word100").CurrentObject,(ushort)84),
                            5000),
                        "A4真实MRK→FC06→FC03读回与MDS采样未贯通");
                    MRKCommandResponse dedup=commands.InvokeAsync(request(
                        "operator",84,"word100",version,operationId))
                        .GetAwaiter().GetResult();
                    Check(dedup.OperationId==operationId&&
                        dedup.BusinessSuccess==true&&plc.Writes==before+1,
                        "A4同OperationId不能重复提交设备FC06");

                    plc.IgnoreNextWrite();
                    MRKCommandResponse uncertain=commands.InvokeAsync(request(
                        "operator",85,"word100",version,Guid.NewGuid()))
                        .GetAwaiter().GetResult();
                    Check(uncertain.Stage==MRKCommandStage.Accepted &&
                        uncertain.BusinessSuccess==false &&
                        uncertain.BusinessCode=="PLC_REGISTER_OUTCOME_UNKNOWN" &&
                        plc.ActualWord==84 && plc.Writes==before+2,
                        "A4设备已应答但实际未写入：必须Unknown且无自动重试");

                    // 同一已批准设备Session，以正式MRKCommandPipeServer/Client
                    // 真实OS NamedPipe交换，而非使用另一套SCADA远程命令总线。
                    // 报文Caller为伪造值，服务端只能使用OS peer身份。
                    MRKCommandPipeClient ipc=new MRKCommandPipeClient(".",mrkPipe,
                        connectTimeoutMilliseconds:3000,exchangeTimeoutMilliseconds:8000);
                    Func<ushort,string,Guid,MRKCommandRequest> remoteRequest=
                        (value,generation,operation)=>new MRKCommandRequest(target,
                            PADPlcMrkCommandRegistration.WriteUInt16Code,
                            new Dictionary<string,object>{
                                {"StableId","word100"},{"Value",value},
                                {"EffectiveVersion",version},
                                {"SessionGeneration",generation}
                            },caller:"forged-wire-operator",operationId:operation,
                            options:new MRKCommandOptions(timeoutMilliseconds:5000,remote:true),
                            expectedSchemaVersion:1);
                    Guid ipcOperation=Guid.NewGuid();
                    int beforeIpc=plc.Writes;
                    MRKCommandResponse remoteWritten=ipc.InvokeAsync(
                        remoteRequest(86,host.Generation,ipcOperation))
                        .GetAwaiter().GetResult();
                    Check(remoteWritten.Stage==MRKCommandStage.Accepted &&
                        remoteWritten.State==MRKCommandState.Completed &&
                        remoteWritten.BusinessSuccess==true &&
                        remoteWritten.BusinessCode=="PLC_REGISTER_READBACK_CONFIRMED" &&
                        plc.ActualWord==86 && plc.Writes==beforeIpc+1,
                        "真实Windows MRK Pipe OS认证→MRK→FC06→FC03回读未成功" +
                        " Stage=" + remoteWritten.Stage +
                        " State=" + remoteWritten.State +
                        " BusinessSuccess=" + remoteWritten.BusinessSuccess +
                        " BusinessCode=" + remoteWritten.BusinessCode +
                        " ErrorCode=" + remoteWritten.ErrorCode +
                        " Error=" + remoteWritten.Error +
                        " ActualWord=" + plc.ActualWord +
                        " WritesBefore=" + beforeIpc + " WritesAfter=" + plc.Writes);
                    MRKCommandResponse remoteQuery=ipc.QueryAsync(ipcOperation)
                        .GetAwaiter().GetResult();
                    Check(remoteQuery.State==MRKCommandState.Completed &&
                        remoteQuery.OperationId==ipcOperation &&
                        remoteQuery.BusinessSuccess==true &&
                        plc.Writes==beforeIpc+1,
                        "真实MRK Pipe OperationId查询不应重发FC06");
                    MRKCommandResponse oldRemote=ipc.InvokeAsync(
                        remoteRequest(87,Guid.NewGuid().ToString("N"),Guid.NewGuid()))
                        .GetAwaiter().GetResult();
                    Check(oldRemote.BusinessSuccess!=true && plc.Writes==beforeIpc+1,
                        "真实MRK Pipe旧SessionGeneration不能调用设备FC06");
                    modeAllowed=false;
                    try
                    {
                        MRKCommandResponse modeDenied=ipc.InvokeAsync(
                            remoteRequest(87,host.Generation,Guid.NewGuid()))
                            .GetAwaiter().GetResult();
                        Check(modeDenied.Stage==MRKCommandStage.Rejected &&
                            modeDenied.ErrorCode=="Unauthorized" &&
                            plc.Writes==beforeIpc+1,
                            "Host控制模式审批撤销后MRK必须在FC06前拒绝");
                    }
                    finally{modeAllowed=true;}
                    int beforeRevocation=plc.Writes;
                    revokeImmediatelyAfterAdmission=true;
                    try
                    {
                        MRKCommandResponse revokedAfterAdmission=ipc.InvokeAsync(
                            remoteRequest(87,host.Generation,Guid.NewGuid()))
                            .GetAwaiter().GetResult();
                        Check(revokedAfterAdmission.BusinessSuccess!=true &&
                            plc.Writes==beforeRevocation && !modeAllowed,
                            "MRK已经受理后控制模式撤销仍不能在物理FC06前执行");
                    }
                    finally
                    {
                        modeAllowed=true;
                        revokeImmediatelyAfterAdmission=false;
                    }
                    peerAllowed=false;
                    try
                    {
                        MRKCommandResponse peerDenied=ipc.InvokeAsync(
                            remoteRequest(87,host.Generation,Guid.NewGuid()))
                            .GetAwaiter().GetResult();
                        Check(peerDenied.BusinessSuccess!=true &&
                            plc.Writes==beforeIpc+1,
                            "OS Peer授权撤销后Pipe不得触发FC06");
                    }
                    finally{peerAllowed=true;}

                    host.StopAsync().GetAwaiter().GetResult();
                    int afterStop=plc.Writes;
                    MRKCommandResponse after=commands.InvokeAsync(request(
                        "operator",86,"word100",version,Guid.NewGuid()))
                        .GetAwaiter().GetResult();
                    Check(after.BusinessSuccess!=true&&plc.Writes==afterStop&&
                        host.Device.Inspect("word100").Quality==
                            MDSVariableQuality.Stale,
                        "A4旧会话Stop后即使MHS仍同版也必须拒绝FC06并降级MDS");
                    MRKCommandResponse pipeAfterStop=ipc.InvokeAsync(
                        remoteRequest(88,host.Generation,Guid.NewGuid()))
                        .GetAwaiter().GetResult();
                    Check(pipeAfterStop.BusinessSuccess!=true &&
                        plc.Writes==afterStop,
                        "Host Stop后受信MRK Pipe必须FailClosed且零FC06");
                    trusted.StopAsync().GetAwaiter().GetResult();
                }
            }
        }

        private static void RunTwoDevices(string root)
        {
            using(TcpPlcFixture plcA=new TcpPlcFixture(73,99))
            using(TcpPlcFixture plcB=new TcpPlcFixture(45,66))
            {
                string project=Path.Combine(root,"multi.cxproj");
                string configs=Path.Combine(root,"multi-configs");
                Guid a,b,projectId;
                using(MEMEngineeringSession engineering=MEMEngineering.Create("双工站工业工程"))
                {
                    projectId=engineering.Project.Id;
                    a=engineering.AddPage("StationA").Id;
                    b=engineering.AddPage("StationB").Id;
                    engineering.SaveAs(project);
                }
                ConfigureDevice(configs,"connector-A","stationA","station-A-channel",plcA.Port);
                ConfigureDevice(configs,"connector-B","stationB","station-B-channel",plcB.Port);
                // 真正的部署入口只保存工程设备与已生效Connector引用，不存寄存器地址。
                MHSConfigService deploymentStore=new MHSConfigService(configs);
                var deployment=deploymentStore.ForProject(projectId.ToString("N"))
                    .Open<PADScadaDeploymentConfiguration>();
                var deployed=deployment.UpdateAsync(model=>
                {
                    model.Devices.Clear();
                    model.Devices.Add(new PADScadaDeviceInstallation{
                        DeviceNodeGuid=a.ToString("N"),ConnectorModuleId="connector-A",
                        ScopeId="stationA",IntervalMs=85,Contiguous=true});
                    model.Devices.Add(new PADScadaDeviceInstallation{
                        DeviceNodeGuid=b.ToString("N"),ConnectorModuleId="connector-B",
                        ScopeId="stationB",IntervalMs=85,Contiguous=true});
                },"双工站正式SCADA部署",CancellationToken.None).GetAwaiter().GetResult();
                Check(deployed.Success,"SCADA工程部署配置未能持久保存: "+deployed.ErrorCode);
                if(deployment.HasPendingApply)
                    Check(deployment.ApplyPendingAtTaskBoundary(),
                        "SCADA双设备项目Effective未生效");
                // A5无PLC IO工程预检：只从已保存MEM及MHS Effective装配，
                // JSON不包含现场IP或认证材料，且不建立连接/发送FC03。
                MRHScadaEngineeringInspection inspected=
                    MRHScadaEngineeringPreflight.Inspect(project,configs);
                Check(inspected.SchemaVersion==1 &&
                    inspected.Status=="EngineeringValidated" &&
                    !inspected.DeviceCommunicationValidated &&
                    inspected.DeploymentVersion==deployment.EffectiveVersion &&
                    inspected.Devices.Length==2 &&
                    inspected.Devices.Any(x=>x.DeviceRef==a.ToString("N") &&
                        x.ScopeId=="stationA" && x.ConnectorModuleId=="connector-A" &&
                        x.ScanIntervalMs==85 && x.Contiguous &&
                        x.Points.Length==2 &&
                        x.Points.Any(p=>p.StablePointId=="word100" &&
                            p.VariableAddress=="Station.Word" &&
                            p.ValueType=="UInt16" && !p.WritableDefinition)) &&
                    inspected.Devices.Any(x=>x.DeviceRef==b.ToString("N") &&
                        x.ScopeId=="stationB" && x.ConnectorModuleId=="connector-B"),
                    "工程预检必须来自真实MEM/MHS，包含双工站身份、点位与版本");
                MRHScadaEngineeringInspection actualCli=PreflightFromProcess(
                    project,configs,0);
                Check(actualCli.SchemaVersion==1 &&
                    actualCli.Status=="EngineeringValidated" &&
                    !actualCli.DeviceCommunicationValidated &&
                    actualCli.DeploymentVersion==inspected.DeploymentVersion &&
                    actualCli.Devices.Length==2 &&
                    actualCli.Devices.All(x=>x.Points.Length==2) &&
                    plcA.Reads==0 && plcB.Reads==0 &&
                    plcA.Writes==0 && plcB.Writes==0,
                    "独立Host.exe的工程预检必须返回正式双PLC身份且零FC03/FC06");
                string deploymentJson=MRHScadaEngineeringPreflight.ToJson(inspected);
                Check(deploymentJson.Contains("\"EngineeringValidated\"") &&
                    deploymentJson.Contains("\"DeviceCommunicationValidated\":false") &&
                    !deploymentJson.Contains("127.0.0.1") &&
                    plcA.Reads==0 && plcB.Reads==0 &&
                    plcA.Writes==0 && plcB.Writes==0,
                    "SCADA离线工程预检不得连接设备或披露现场连接地址");
                MRHScadaDeviceLaunch launchA=new MRHScadaDeviceLaunch(a,"connector-A",
                    "stationA",85,true);
                MRHScadaDeviceLaunch launchB=new MRHScadaDeviceLaunch(b,"connector-B",
                    "stationB",85,true);
                Reject<InvalidOperationException>(()=>MRHScadaRuntimeGroup.Open(
                    project,configs,new[]{launchA,launchA}));
                Reject<InvalidOperationException>(()=>MRHScadaRuntimeGroup.Open(
                    project,configs,new[]{launchA,
                        new MRHScadaDeviceLaunch(b,"connector-B","stationA",85,true)}));
                Reject<InvalidOperationException>(()=>MRHScadaRuntimeGroup.Open(
                    project,configs,new[]{launchA,
                        new MRHScadaDeviceLaunch(b,"connector-A","stationB",85,true)}));
                Check(plcA.Reads==0&&plcB.Reads==0,
                    "身份/Scope/Module冲突不得发任何TCP命令");
                using(MRHScadaRuntimeGroup group=MRHScadaRuntimeGroup.OpenFromEngineering(
                    project,configs))
                {
                    Check(group.DeploymentVersion==deployment.EffectiveVersion &&
                        group.Count==2&&group.Find(a).DeviceRef!=group.Find(b).DeviceRef &&
                        group.Find(a).Device.DataTable.ScopeId=="stationA" &&
                        group.Find(b).Device.DataTable.ScopeId=="stationB" &&
                        group.Find(a).Device.ChannelId!=group.Find(b).Device.ChannelId,
                        "多设备Group没有独立Device/Scope/Channel所有权");
                    group.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
                    bool twoReady=SpinWait.SpinUntil(()=>
                        (group.Find(a).CompletedCycles>=2 &&
                         group.Find(b).CompletedCycles>=2) ||
                        group.Find(a).Completion.IsCompleted ||
                        group.Find(b).Completion.IsCompleted,10000);
                    Check(twoReady &&
                        group.Find(a).CompletedCycles>=2 &&
                        group.Find(b).CompletedCycles>=2,
                        "两个独立TCP工站没有同时完成至少2轮FC03"+
                        "; A reads="+plcA.Reads+" cycles="+group.Find(a).CompletedCycles+
                        " fault="+(group.Find(a).Completion.Exception==null?"none":
                            group.Find(a).Completion.Exception.ToString())+
                        "; B reads="+plcB.Reads+" cycles="+group.Find(b).CompletedCycles+
                        " fault="+(group.Find(b).Completion.Exception==null?"none":
                            group.Find(b).Completion.Exception.ToString()));
                    // 正式双设备真实FC03持续采样时并发读取同一项目与各Connector
                    // Effective；不能把MHS读文件的独占锁竞争误报成版本漂移。
                    Task[] verifyReaders=Enumerable.Range(0,4).Select(worker=>
                        Task.Run(()=>
                        {
                            for(int turn=0;turn<6;turn++)
                            {
                                if(!group.IsDeploymentApproved ||
                                    !group.Find(a).IsStillApproved ||
                                    !group.Find(b).IsStillApproved)
                                    throw new InvalidOperationException(
                                        "并发MHS版本校验误判漂移 worker="+worker+
                                        " turn="+turn);
                            }
                        })).ToArray();
                    Task.WhenAll(verifyReaders).GetAwaiter().GetResult();
                    Check(!group.Find(a).Completion.IsCompleted &&
                          !group.Find(b).Completion.IsCompleted,
                        "并发MHS读版本不能使两个健康PLC采集任务提前终止");
                    IMDSVariableSnapshot sampleA=group.Find(a).Device.Inspect("word100");
                    IMDSVariableSnapshot sampleB=group.Find(b).Device.Inspect("word100");
                    Check((ushort)sampleA.CurrentObject==73 &&
                        (ushort)sampleB.CurrentObject==45 &&
                        sampleA.Quality==MDSVariableQuality.Good &&
                        sampleB.Quality==MDSVariableQuality.Good &&
                        group.Find(a).Generation!=group.Find(b).Generation &&
                        plcA.Reads>=2&&plcB.Reads>=2 &&
                        plcA.Writes==0&&plcB.Writes==0,
                        "两个工站真实TCP原始值发生串台或出现未授权FC06");
                    string pipeName="scada-g1-"+Guid.NewGuid().ToString("N");
                    using(CancellationTokenSource pipeStop=new CancellationTokenSource())
                    {
                        Task pipeServer=MRHScadaReadOnlyPipe.RunAsync(group,pipeName,pipeStop.Token);
                        try
                        {
                            MRHScadaPipeReply discovery=MRHScadaReadOnlyPipe.ExchangeForSmoke(
                                pipeName,new MRHScadaPipeRequest{SchemaVersion=1,Operation="discover"});
                            Check(discovery.Status=="Ok"&&discovery.Devices.Length==2&&
                                discovery.DeploymentVersion==group.DeploymentVersion&&
                                discovery.Devices.All(x=>x.Points.Length==0 &&
                                    x.Running && x.CompletedCycles>=2 &&
                                    x.SuccessfulCycles>=2 && x.PartialCycles==0),
                                "本机Pipe发现必须包含独立真实FC03成功周期与零部分失败诊断");
                            // 与HMI跨进程结构一致：另一真正Windows进程通过Pipe
                            // 发现这两台由当前Host管理的PLC，不共享任何进程内对象。
                            MRHScadaPipeReply processDiscovery=ProbeFromProcess(0,
                                pipeName,"discover");
                            Check(processDiscovery.Status=="Ok" &&
                                processDiscovery.DeploymentVersion==group.DeploymentVersion &&
                                processDiscovery.Devices.Length==2 &&
                                processDiscovery.Devices.All(x=>x.Running&&
                                    x.SuccessfulCycles>=2),
                                "跨进程Pipe discover未获得真实双PLC运行状态");
                            MRHScadaPipeReply processSnapshot=ProbeFromProcess(0,
                                pipeName,"snapshot",a.ToString("N"),group.DeploymentVersion,
                                group.Find(a).ConfigVersion,group.Find(a).Generation);
                            Check(processSnapshot.Status=="Ok" &&
                                processSnapshot.Devices.Length==1 &&
                                processSnapshot.Devices[0].Points.Any(x=>
                                    x.StablePointId=="word100"&&x.HasValue&&x.Value=="73"&&
                                    x.Quality=="Good"&&x.Revision>0),
                                "跨进程Pipe没有读取PLC-A真实MDS数值、质量或Revision");
                            MRHScadaPipeReply processRejected=ProbeFromProcess(3,
                                pipeName,"snapshot",a.ToString("N"),group.DeploymentVersion,
                                group.Find(a).ConfigVersion,Guid.NewGuid().ToString("N"));
                            Check(processRejected.Status=="Rejected"&&
                                processRejected.ErrorCode=="SESSION_VERSION_MISMATCH" &&
                                plcA.Writes==0&&plcB.Writes==0,
                                "跨进程旧Generation未FailClosed或产生未授权FC06");
                            MRHScadaPipeRequest query=new MRHScadaPipeRequest{
                                SchemaVersion=1,Operation="snapshot",DeviceRef=a.ToString("N"),
                                ExpectedDeploymentVersion=group.DeploymentVersion,
                                ExpectedConfigVersion=group.Find(a).ConfigVersion,
                                ExpectedGeneration=group.Find(a).Generation};
                            MRHScadaPipeReply snapshot=MRHScadaReadOnlyPipe.ExchangeForSmoke(pipeName,query);
                            Check(snapshot.Status=="Ok"&&snapshot.Devices.Length==1&&
                                snapshot.Devices[0].Points.Any(x=>x.StablePointId=="word100"&&
                                    x.HasValue&&x.Value=="73"&&x.Quality=="Good"&&x.Revision>0),
                                "真实Pipe没有读取MDS Good/原值/Revision");
                            // 第二台设备须保留自己的Scope、原始点值和Generation，
                            // 不能因一台PLC的数据源而跨站串写。
                            MRHScadaPipeRequest other=new MRHScadaPipeRequest{
                                SchemaVersion=1,Operation="snapshot",DeviceRef=b.ToString("N"),
                                ExpectedDeploymentVersion=group.DeploymentVersion,
                                ExpectedConfigVersion=group.Find(b).ConfigVersion,
                                ExpectedGeneration=group.Find(b).Generation};
                            MRHScadaPipeReply stationBReply=MRHScadaReadOnlyPipe.ExchangeForSmoke(pipeName,other);
                            Check(stationBReply.Status=="Ok"&&stationBReply.Devices.Length==1&&
                                stationBReply.Devices[0].ScopeId=="stationB"&&
                                stationBReply.Devices[0].Generation!=snapshot.Devices[0].Generation&&
                                stationBReply.Devices[0].Points.Any(x=>x.StablePointId=="word100"&&
                                    x.HasValue&&x.Value=="45"&&x.Quality=="Good"),
                                "本机Pipe跨设备Scope/值/会话串台");
                            query.ExpectedDeploymentVersion="stale-project-effective";
                            MRHScadaPipeReply rejected=MRHScadaReadOnlyPipe.ExchangeForSmoke(pipeName,query);
                            Check(rejected.Status=="Rejected"&&
                                rejected.ErrorCode=="DEPLOYMENT_VERSION_MISMATCH",
                                "IPC旧工程部署Effective必须拒绝");
                            query.ExpectedDeploymentVersion=group.DeploymentVersion;
                            query.ExpectedConfigVersion="stale-connector-effective";
                            rejected=MRHScadaReadOnlyPipe.ExchangeForSmoke(pipeName,query);
                            Check(rejected.Status=="Rejected"&&
                                rejected.ErrorCode=="SESSION_VERSION_MISMATCH",
                                "IPC旧Connector Effective必须拒绝");
                            query.ExpectedConfigVersion=group.Find(a).ConfigVersion;
                            query.ExpectedGeneration=Guid.NewGuid().ToString("N");
                            rejected=MRHScadaReadOnlyPipe.ExchangeForSmoke(pipeName,query);
                            Check(rejected.Status=="Rejected"&&
                                rejected.ErrorCode=="SESSION_VERSION_MISMATCH",
                                "IPC旧Generation未拒绝");
                            query.Operation="write";
                            rejected=MRHScadaReadOnlyPipe.ExchangeForSmoke(pipeName,query);
                            Check(rejected.Status=="Rejected"&&
                                rejected.ErrorCode=="READ_ONLY_OPERATION"&&plcA.Writes==0,
                                "IPC不允许绕过MRK写入设备");
                        }
                        finally
                        {
                            pipeStop.Cancel();
                            pipeServer.GetAwaiter().GetResult();
                        }
                    }
                    group.Find(a).StopAsync().GetAwaiter().GetResult();
                    Check(group.Find(a).Device.Inspect("word100").Quality==
                        MDSVariableQuality.Stale,
                        "单个PLC停止后必须独立降级MDS质量");
                    int beforeB=plcB.Reads;
                    Check(SpinWait.SpinUntil(()=>plcB.Reads>=beforeB+2,5000) &&
                        group.Find(b).Device.Inspect("word100").Quality==
                            MDSVariableQuality.Good,
                        "PLC-A停止不得影响PLC-B的Channel/采集/MDS");
                    Task stopFirst=group.StopAsync();
                    Task stopAgain=group.StopAsync();
                    Check(Object.ReferenceEquals(stopFirst,stopAgain),
                        "多设备并发Stop必须共享同一生命周期完成Task");
                    Task.WhenAll(stopFirst,stopAgain).GetAwaiter().GetResult();
                    int readsA=plcA.Reads,readsB=plcB.Reads;
                    Thread.Sleep(220);
                    Check(plcA.Reads==readsA&&plcB.Reads==readsB,
                        "设备组Stop后不允许任何残余TCP采样");
                }
                // 工程多PLC显式组恢复：A一次真实FC03丢响应导致旧世代故障；
                // 必须Stop/Dispose两套旧Session，重新从MEM/MHS装配并产生新代次。
                // B在组级恢复期间也短暂停止，这不是单设备无缝热迁移。
                using(CancellationTokenSource recoverStop=new CancellationTokenSource())
                {
                    MRHScadaGroupRecoverySupervisor supervisor=
                        new MRHScadaGroupRecoverySupervisor(project,configs,null,2,50);
                    string firstA=null,firstB=null,secondA=null,secondB=null;
                    Task recovery=supervisor.RunAsync(recoverStop.Token,group=>
                    {
                        if(firstA==null)
                        {
                            firstA=group.Find(a).Generation;
                            firstB=group.Find(b).Generation;
                        }
                        else
                        {
                            secondA=group.Find(a).Generation;
                            secondB=group.Find(b).Generation;
                            Check(firstA!=secondA&&firstB!=secondB,
                                "组恢复没有同时替换两个独立PLC SessionGeneration");
                            MRHScadaPipeReply stale=MRHScadaReadOnlyPipe.Respond(group,
                                new MRHScadaPipeRequest{
                                    SchemaVersion=1,Operation="snapshot",DeviceRef=a.ToString("N"),
                                    ExpectedDeploymentVersion=group.DeploymentVersion,
                                    ExpectedConfigVersion=group.Find(a).ConfigVersion,
                                    ExpectedGeneration=firstA});
                            Check(stale.Status=="Rejected"&&
                                stale.ErrorCode=="SESSION_VERSION_MISMATCH",
                                "组恢复之后旧HMI Generation不能读取新的PLC世代");
                        }
                    });
                    try
                    {
                        Check(SpinWait.SpinUntil(()=>
                            supervisor.ConnectedGenerations==1&&plcA.Reads>=4&&plcB.Reads>=4,
                            10000),"组恢复首世代没有实际读取两台TCP设备");
                        int beforeA=plcA.Reads,beforeB=plcB.Reads;
                        plcA.DropNextReplies(1);
                        Check(SpinWait.SpinUntil(()=>
                            supervisor.ConnectedGenerations>=2,12000),
                            "PLC-A真实FC03丢回包未触发有界组重建");
                        Check(supervisor.RecoveryAttempts==1&&secondA!=null&&
                            secondB!=null&&secondA!=firstA&&secondB!=firstB,
                            "组恢复的重试预算或代次所有权异常");
                        Check(SpinWait.SpinUntil(()=>
                            plcA.Reads>=beforeA+2&&plcB.Reads>=beforeB+2,10000),
                            "组恢复后两台PLC没有重新取得真实FC03数据");
                        Check(plcA.Writes==0&&plcB.Writes==0,
                            "组恢复不能重放PLC写指令");
                    }
                    finally
                    {
                        recoverStop.Cancel();
                        try{recovery.GetAwaiter().GetResult();}
                        catch(OperationCanceledException)
                            when(recoverStop.IsCancellationRequested){}
                    }
                    int stoppedA=plcA.Reads,stoppedB=plcB.Reads;
                    Thread.Sleep(220);
                    Check(plcA.Reads==stoppedA&&plcB.Reads==stoppedB,
                        "组恢复停止后仍存在遗留的PLC采集请求");
                }
                // 通信异常可有界恢复，但部署版本漂移绝不可当成通信故障
                // 自动新建连接或继续公开旧PLC的Good事实。
                using(CancellationTokenSource staleStop=new CancellationTokenSource())
                {
                    MRHScadaGroupRecoverySupervisor supervisor=
                        new MRHScadaGroupRecoverySupervisor(project,configs,null,2,50);
                    Task running=supervisor.RunAsync(staleStop.Token);
                    try
                    {
                        Check(SpinWait.SpinUntil(()=>
                            supervisor.ConnectedGenerations==1,8000),
                            "部署漂移负例无法启动原始双PLC组");
                        var changed=deployment.UpdateAsync(model=>
                            model.Devices[0].IntervalMs=125,
                            "多PLC组恢复部署漂移拒绝测试",CancellationToken.None)
                            .GetAwaiter().GetResult();
                        Check(changed.Success,"测试中MHS工程部署漂移提交失败");
                        Check(SpinWait.SpinUntil(()=>running.IsCompleted,8000)&&
                            running.IsFaulted&&supervisor.RecoveryAttempts==0,
                            "组恢复不应自动批准变化的项目部署Effective");
                        Check(plcA.Writes==0&&plcB.Writes==0,
                            "项目部署漂移不能触发写入设备");
                    }
                    finally
                    {
                        staleStop.Cancel();
                        try{running.GetAwaiter().GetResult();}
                        catch(InvalidOperationException) when(running.IsFaulted){}
                        catch(OperationCanceledException)
                            when(staleStop.IsCancellationRequested){}
                    }
                    if(deployment.HasPendingApply)
                        Check(deployment.ApplyPendingAtTaskBoundary(),
                            "漂移负例结束后必须正式发布新的部署供后续测试使用");
                }
                // 独立恢复正反例：PLC-A一笔真实FC03丢包，PLC-B必须
                // 始终保有原来的Channel Generation并继续读取，而不是整组重启。
                using(CancellationTokenSource isolatedStop=new CancellationTokenSource())
                {
                    string isolatedPipe="scada-isolated-"+Guid.NewGuid().ToString("N");
                    MRHScadaIsolatedRecoverySupervisor isolated=
                        new MRHScadaIsolatedRecoverySupervisor(
                            project,configs,isolatedPipe,2,80);
                    object generationsGate=new object();
                    string firstA=null,firstB=null,recoveredA=null;
                    Task isolatedTask=isolated.RunAsync(isolatedStop.Token,device=>
                    {
                        lock(generationsGate)
                        {
                            if(device.DeviceRef==a.ToString("N"))
                            {
                                if(firstA==null)firstA=device.Generation;
                                else recoveredA=device.Generation;
                            }
                            else if(device.DeviceRef==b.ToString("N"))
                            {
                                if(firstB==null)firstB=device.Generation;
                                else throw new InvalidOperationException(
                                    "PLC-B健康设备不得被独立恢复监督器重启");
                            }
                        }
                    });
                    try
                    {
                        Check(SpinWait.SpinUntil(()=>
                            isolated.ConnectedGenerations==2&&
                            !isolatedTask.IsCompleted,8000),
                            "单PLC隔离恢复初始两套真实会话未启动");
                        MRHScadaPipeReply initial=MRHScadaReadOnlyPipe.ExchangeForSmoke(
                            isolatedPipe,new MRHScadaPipeRequest{
                                SchemaVersion=1,Operation="discover"});
                        Check(initial.Status=="Ok"&&initial.Devices.Length==2,
                            "独立恢复初始Pipe未发现两台真实PLC");
                        string pinnedB=initial.Devices.Single(x=>
                            x.DeviceRef==b.ToString("N")).Generation;
                        string pinnedA=initial.Devices.Single(x=>
                            x.DeviceRef==a.ToString("N")).Generation;
                        Check(SpinWait.SpinUntil(()=>plcA.Reads>=3&&plcB.Reads>=3,8000),
                            "独立恢复故障注入前真实FC03采样不完整");
                        plcA.DropNextReplies(1);
                        Check(SpinWait.SpinUntil(()=>
                            isolated.RecoveryAttempts>=1||isolatedTask.IsCompleted,12000) &&
                            !isolatedTask.IsCompleted,
                            "PLC-A无回包没有触发独立有界恢复");
                        int bBefore=plcB.Reads;
                        Check(SpinWait.SpinUntil(()=>plcB.Reads>=bBefore+2,6000),
                            "PLC-A恢复时PLC-B未继续独立周期采样");
                        Check(SpinWait.SpinUntil(()=>
                            isolated.ConnectedGenerations>=3||isolatedTask.IsCompleted,12000) &&
                            !isolatedTask.IsCompleted,
                            "PLC-A未独立重新建立SessionGeneration");
                        MRHScadaPipeReply after=MRHScadaReadOnlyPipe.ExchangeForSmoke(
                            isolatedPipe,new MRHScadaPipeRequest{
                                SchemaVersion=1,Operation="discover"});
                        Check(after.Status=="Ok"&&after.Devices.Length==2&&
                            after.Devices.Single(x=>x.DeviceRef==b.ToString("N")).Generation==pinnedB&&
                            after.Devices.Single(x=>x.DeviceRef==a.ToString("N")).Generation!=pinnedA&&
                            isolated.AttemptsFor(a.ToString("N"))==1&&
                            isolated.AttemptsFor(b.ToString("N"))==0,
                            "独立恢复错误地重建健康PLC-B，或重连后没有替换PLC-A代次");
                        lock(generationsGate)
                            Check(firstA==pinnedA&&firstB==pinnedB&&
                                recoveredA!=null&&recoveredA!=pinnedA,
                                "独立恢复的设备订阅通知或原世代错误");
                        MRHScadaPipeReply stale=MRHScadaReadOnlyPipe.ExchangeForSmoke(
                            isolatedPipe,new MRHScadaPipeRequest{
                                SchemaVersion=1,Operation="snapshot",DeviceRef=a.ToString("N"),
                                ExpectedDeploymentVersion=after.DeploymentVersion,
                                ExpectedConfigVersion=after.Devices.Single(x=>
                                    x.DeviceRef==a.ToString("N")).ConfigVersion,
                                ExpectedGeneration=pinnedA});
                        Check(stale.Status=="Rejected"&&
                            stale.ErrorCode=="SESSION_VERSION_MISMATCH",
                            "旧HMI请求不能读取单PLC重新连接的新会话");
                        string currentA=after.Devices.Single(x=>
                            x.DeviceRef==a.ToString("N")).Generation;
                        string currentVersion=after.Devices.Single(x=>
                            x.DeviceRef==a.ToString("N")).ConfigVersion;
                        bool newSample=SpinWait.SpinUntil(()=>
                        {
                            if(isolatedTask.IsCompleted)return false;
                            try
                            {
                                MRHScadaPipeReply value=MRHScadaReadOnlyPipe.ExchangeForSmoke(
                                    isolatedPipe,new MRHScadaPipeRequest{
                                        SchemaVersion=1,Operation="snapshot",
                                        DeviceRef=a.ToString("N"),
                                        ExpectedDeploymentVersion=after.DeploymentVersion,
                                        ExpectedConfigVersion=currentVersion,
                                        ExpectedGeneration=currentA});
                                return value.Status=="Ok"&&value.Devices.Length==1&&
                                    value.Devices[0].Points.Any(x=>x.StablePointId=="word100"&&
                                        x.HasValue&&x.Value=="73"&&x.Quality=="Good"&&
                                        x.Revision>0);
                            }
                            catch(IOException){return false;}
                        },8000);
                        Check(newSample&&plcA.Writes==0&&plcB.Writes==0,
                            "单PLC隔离恢复的新Generation没有再次发布真实MDS Good或重发FC06");
                    }
                    finally
                    {
                        isolatedStop.Cancel();
                        try{isolatedTask.GetAwaiter().GetResult();}
                        catch(OperationCanceledException)
                            when(isolatedStop.IsCancellationRequested){}
                    }
                    int endedA=plcA.Reads,endedB=plcB.Reads;
                    Thread.Sleep(240);
                    Check(plcA.Reads==endedA&&plcB.Reads==endedB,
                        "独立恢复Supervisor Stop后仍有PLC请求遗留");
                }
                // 项目级Deployment的生效版本同样必须被每台PLC轮询守卫：
                // 不允许Studio部署调整后旧Host继续采集或沿用原设备控制上下文。
                using(MRHScadaRuntimeGroup pinned=MRHScadaRuntimeGroup.OpenFromEngineering(
                    project,configs))
                {
                    pinned.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
                    Check(SpinWait.SpinUntil(()=>
                        pinned.Find(a).CompletedCycles>=1 &&
                        pinned.Find(b).CompletedCycles>=1,10000),
                        "项目部署漂移测试前两个PLC必须有真实Good样本");
                    var modifiedDeployment=deployment.UpdateAsync(model=>
                        model.Devices[0].IntervalMs=120,
                        "Studio发布新采样方案",CancellationToken.None)
                        .GetAwaiter().GetResult();
                    Check(modifiedDeployment.Success,
                        "正式FCM项目部署版本变更提交失败");
                    Check(SpinWait.SpinUntil(()=>
                        pinned.Find(a).Completion.IsFaulted &&
                        pinned.Find(b).Completion.IsFaulted,8000),
                        "项目级MHS Deployment变化应在两个独立工站下一轮发包前FailClosed");
                    Check(pinned.Find(a).Device.Inspect("word100").Quality==
                            MDSVariableQuality.Stale &&
                        pinned.Find(b).Device.Inspect("word100").Quality==
                            MDSVariableQuality.Stale &&
                        plcA.Writes==0&&plcB.Writes==0,
                        "部署漂移不能继续公开Good或触发自动设备写入");
                }
                // 两个设备配置不同Module，但手动误复用ChannelId也必须被Host拒绝。
                MHSConfigService store=new MHSConfigService(configs);
                var second=store.ForScope("module","connector-B")
                    .Open<MCNConnectorConfiguration>();
                var edited=second.UpdateAsync(model=>
                    model.Channel.Id="station-A-channel",
                    "SCADA共享通道冲突负例",CancellationToken.None)
                    .GetAwaiter().GetResult();
                Check(edited.Success,"多设备Channel冲突负例无法保存");
                if(second.HasPendingApply)
                    Check(second.ApplyPendingAtTaskBoundary(),"Channel冲突变更未生效");
                Reject<InvalidOperationException>(()=>MRHScadaRuntimeGroup.Open(
                    project,configs,new[]{launchA,launchB}));
                int preflightReadsA=plcA.Reads,preflightReadsB=plcB.Reads;
                Reject<InvalidOperationException>(()=>
                    MRHScadaEngineeringPreflight.Inspect(project,configs));
                PreflightFromProcess(project,configs,1); // 跨进程同样必须拒绝身份冲突。
                Check(plcA.Reads==preflightReadsA&&plcB.Reads==preflightReadsB,
                    "工程预检拒绝跨设备Channel冲突时不能发送任何FC03");
                Check(plcA.Writes==0&&plcB.Writes==0,
                    "多设备错误装配不能发出写控制命令");
            }
        }

        internal static int Run()
        {
            string root=Path.Combine(Path.GetTempPath(),"CxVisual-Scada-G1-"+
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                using(TcpPlcFixture plc=new TcpPlcFixture())
                {
                    string project=Path.Combine(root,"saved.cxproj");
                    string configs=Path.Combine(root,"configs");
                    string moduleId="plc-connector";
                    Guid deviceGuid;
                    using(MEMEngineeringSession engineering=MEMEngineering.Create("SCADA工程持久资源"))
                    {
                        MEMPage resource=engineering.AddPage("PLC工站资源");
                        deviceGuid=resource.Id;
                        engineering.SaveAs(project);
                    }
                    MHSConfigService store=new MHSConfigService(configs);
                    var persisted=store.ForScope("module",moduleId)
                        .Open<MCNConnectorConfiguration>();
                    var saved=persisted.UpdateAsync(model=>
                    {
                        model.DisplayName="SCADA正式现场连接配置";
                        model.Channel=new FTRTcpClientChannelConfiguration{
                            Id="scada-connector-channel",Kind=FTRChannelKind.TcpClient,
                            Host="127.0.0.1",Port=plc.Port,ConnectTimeoutMs=2000};
                        model.Protocol=new MPAProtocolConfiguration("scada-protocol","modbus.tcp");
                        model.Protocol.TimeoutMs=700;
                        model.Protocol.Points.Add(new MPAPointDefinition("word100","holding:100",
                            typeof(ushort)){StationAddress="1",CanRead=true,CanWrite=false});
                        model.Protocol.Points.Add(new MPAPointDefinition("input101","holding:101",
                            typeof(ushort)){StationAddress="1",CanRead=true,CanWrite=false});
                        foreach(string name in new[]{"word100","input101"})
                            model.Protocol.Mappings.Add(new MPADataMapping{
                                PointRef=name,TargetKind=MPADataTargetKind.Variable,
                                TargetRef=name=="word100"?"plc/Station.Word":"plc/Station.Input",
                                Direction=MPADataDirection.Inbound,
                                Operation=MPADataOperation.WriteValue,Required=true});
                    },"SCADA G1真实生效工程配置",CancellationToken.None)
                    .GetAwaiter().GetResult();
                    Check(saved.Success,"MHS正式配置写入失败："+saved.ErrorCode);
                    if(persisted.HasPendingApply)
                        Check(persisted.ApplyPendingAtTaskBoundary(),"MHS生效失败");

                    // 从磁盘MEM与MHS重新加载；不是内存构造测试点表。
                    Reject<InvalidOperationException>(()=>MRHScadaRuntime.Open(
                        project,configs,Guid.NewGuid(),moduleId,"plc",90,true));
                    Reject<InvalidOperationException>(()=>MRHScadaRuntime.Open(
                        project,configs,deviceGuid,moduleId,"other",90,true));
                    // A4绝不能因装配方传入一个伪造允许写入委托而绕过
                    // MEM项目级正式Deployment批准。
                    Reject<UnauthorizedAccessException>(()=>MRHScadaRuntime.Open(
                        project,configs,deviceGuid,moduleId,"plc",90,true,
                        authorizeWrite:(caller,point)=>true));
                    using(MRHScadaRuntime scada=MRHScadaRuntime.Open(
                        project,configs,deviceGuid,moduleId,"plc",90,true))
                    {
                        Check(scada.DeviceRef==deviceGuid.ToString("N") &&
                            scada.Device.PointDefinitions.Length==2 &&
                            scada.Device.PointDefinitions.All(x=>!x.Writable),
                            "未由MEM/MHS正式资源导出稳定身份或只读点表");
                        using(MRKCommandCenter commands=new MRKCommandCenter(
                            authorize:ctx=>true))
                        {
                            Reject<UnauthorizedAccessException>(()=>
                                scada.Device.RegisterWriteCommands(commands,3001));
                            Reject<UnauthorizedAccessException>(()=>
                                scada.RegisterTrustedWriteCommands(commands,3001));
                            Check(commands.Registry.Discover().Count==0 && plc.Writes==0,
                                "默认SCADA Host禁止任何MRK写命令注册或FC06");
                        }
                        int observed=0;
                        Reject<System.Collections.Generic.KeyNotFoundException>(()=>
                            scada.Device.Subscribe(changes=>{},new[]{"foreign-point"}));
                        using(IDisposable observation=scada.Device.Subscribe(
                            changes =>
                            {
                                if(changes.Any(x=>x.VariableId=="word100" &&
                                    x.Quality==MDSVariableQuality.Good))
                                    Interlocked.Increment(ref observed);
                            },new[]{"word100"}))
                        {
                        scada.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
                        Check(SpinWait.SpinUntil(()=>scada.CompletedCycles>=2,8000),
                            "SCADA Headless未执行两轮真实FC03");
                        IMDSVariableSnapshot word=scada.Device.Inspect("word100");
                        IMDSVariableSnapshot input=scada.Device.Inspect("input101");
                        Check(word.HasValue&&input.HasValue &&
                            (ushort)word.CurrentObject==73 && (ushort)input.CurrentObject==99 &&
                            word.Quality==MDSVariableQuality.Good &&
                            input.Quality==MDSVariableQuality.Good &&
                            scada.CompletedCycles>=2 && plc.Reads>=2 && plc.Writes==0,
                            "SCADA TCP→MPA→PAD→MDS正式采样值或只读权限不符");
                        Check(SpinWait.SpinUntil(()=>Volatile.Read(ref observed)>=1,3500),
                            "真实MDS分发器未按StableId将Good采样通知给订阅者");
                        scada.StopAsync().GetAwaiter().GetResult();
                        Check(scada.Device.Inspect("word100").Quality==MDSVariableQuality.Stale,
                            "SCADA Stop后不得继续把PLC采样认定为Good");
                        int afterStop=plc.Reads;
                        Thread.Sleep(250);
                        Check(plc.Reads==afterStop,"SCADA Host停止后仍在发FC03");
                        }
                    }
                    // 实际TCP设备停止响应：旧FCR采集Faulted、MDS Stale，
                    // 独立Host必须先关闭旧Connector再以相同MEM/MHS版本重建新代次。
                    using(CancellationTokenSource recoveryStop=new CancellationTokenSource())
                    {
                        MRHScadaRecoverySupervisor recovery=
                            new MRHScadaRecoverySupervisor(project,configs,
                                deviceGuid,moduleId,"plc",90,true,
                                maxRecoveryAttempts:2,retryDelayMilliseconds:70);
                        Task recoveryTask=recovery.RunAsync(recoveryStop.Token);
                        Check(SpinWait.SpinUntil(()=>
                            recovery.ConnectedGenerations==1,8000),
                            "SCADA监督器没有完成第一次真正TCP连接");
                        string firstGeneration=recovery.LastGeneration;
                        int beforeRecovery=plc.Reads;
                        Check(SpinWait.SpinUntil(()=>plc.Reads>=beforeRecovery+2,8000),
                            "断线模拟前应先完成正常多次真实FC03采集");
                        plc.DropNextReplies(1);
                        Check(SpinWait.SpinUntil(()=>
                            recovery.ConnectedGenerations>=2,12000),
                            "FC03真实超时未触发SCADA新Generation有界自动重连");
                        Check(recovery.RecoveryAttempts==1 &&
                            recovery.PinnedEffectiveVersion==persisted.EffectiveVersion &&
                            recovery.LastGeneration!=firstGeneration &&
                            plc.Writes==0,
                            "恢复须保持原生效版本、建立新会话且不得发FC06");
                        int afterReconnect=plc.Reads;
                        Check(SpinWait.SpinUntil(()=>
                            plc.Reads>afterReconnect,8000),
                            "重新连接的PLC会话没有恢复真实FC03采样");
                        recoveryStop.Cancel();
                        try {recoveryTask.GetAwaiter().GetResult();}
                        catch(OperationCanceledException)
                            when(recoveryStop.IsCancellationRequested) { }
                        int afterSupervisionStop=plc.Reads;
                        Thread.Sleep(230);
                        Check(plc.Reads==afterSupervisionStop,
                            "恢复监督器停止后仍有后台FC03");
                    }
                    // 同一保存工程在Host重启后重新建立不同Generation，不能沿用旧运行态。
                    using(MRHScadaRuntime restarted=MRHScadaRuntime.Open(
                        project,configs,deviceGuid,moduleId,"plc",90,true))
                    {
                        restarted.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
                        Check(SpinWait.SpinUntil(()=>restarted.CompletedCycles>=1,8000),
                            "保存工程重启无法重新建立TCP和MDS采样");
                        // 正式Effective变更：下一轮在设备IO之前FailClosed。
                        var modified=persisted.UpdateAsync(model=>
                            model.DisplayName="新的SCADA版本待重新审批",
                            "运行版本漂移G1",CancellationToken.None)
                            .GetAwaiter().GetResult();
                        Check(modified.Success,"MHS运行时修改失败");
                        Check(SpinWait.SpinUntil(()=>
                            restarted.Completion.IsCompleted,8000) &&
                            restarted.Completion.IsFaulted && !restarted.IsRunning,
                            "正式Effective/待生效漂移未使长采FailClosed");
                        Check(SpinWait.SpinUntil(()=>
                            restarted.Device.Inspect("word100").Quality==
                                MDSVariableQuality.Stale,3000),
                            "MHS版本漂移后没有通知MDS旧样本Stale");
                    }
                    Check(plc.Writes==0,"SCADA首批只读Host不允许写设备");
                    RunTwoDevices(root);
                    RunAuthorizedDevice(root);
                    Console.WriteLine("PASS SCADA G1 MEM/MHS TCP/MDS polling Recovery TwoStations Isolation Stop restart version-drift and MRK signed-in-process A4 PLC FC06/FC03");
                    return 0;
                }
            }
            finally
            {
                try{Directory.Delete(root,true);}catch(IOException){}
            }
        }
    }
}
