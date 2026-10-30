using System;
using System.IO;
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
        private sealed class TcpPlcFixture : IDisposable
        {
            private readonly TcpListener _listener;
            private readonly ushort _wordValue;
            private readonly ushort _inputValue;
            private readonly Task _server;
            private volatile bool _stop;
            private TcpClient _active;
            private int _reads;
            private int _dropReplies;
            private int _writes;
            public int Port {get;private set;}
            public int Reads {get{return Volatile.Read(ref _reads);}}
            public int Writes {get{return Volatile.Read(ref _writes);}}
            public void DropNextReplies(int count)
            {
                if(count<1||count>8)throw new ArgumentOutOfRangeException("count");
                Interlocked.Exchange(ref _dropReplies,count);
            }
            public TcpPlcFixture(ushort wordValue = 73, ushort inputValue = 99)
            {
                _wordValue=wordValue;_inputValue=inputValue;
                _listener=new TcpListener(IPAddress.Loopback,0);
                _listener.Start();
                Port=((IPEndPoint)_listener.LocalEndpoint).Port;
                _server=Task.Run((Action)Serve);
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
                                if(request[0]!=3)
                                {
                                    Interlocked.Increment(ref _writes);
                                    throw new InvalidDataException("只读SCADA夹具拒绝非FC03");
                                }
                                int first=(request[1]<<8)|request[2];
                                int count=(request[3]<<8)|request[4];
                                if(first!=100||count!=2)
                                    throw new InvalidDataException("SCADA连续FC03地址/数量异常");
                                byte[] response=new byte[13];
                                Array.Copy(mbap,0,response,0,7);
                                response[4]=0;response[5]=7;
                                response[7]=3;response[8]=4;
                                response[9]=(byte)(_wordValue>>8);response[10]=(byte)_wordValue;
                                response[11]=(byte)(_inputValue>>8);response[12]=(byte)_inputValue;
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
            string channelId,int port)
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
                    typeof(ushort)){StationAddress="1",CanRead=true,CanWrite=false});
                model.Protocol.Points.Add(new MPAPointDefinition("input101","holding:101",
                    typeof(ushort)){StationAddress="1",CanRead=true,CanWrite=false});
                foreach(string name in new[]{"word100","input101"})
                    model.Protocol.Mappings.Add(new MPADataMapping{
                        PointRef=name,TargetKind=MPADataTargetKind.Variable,
                        TargetRef=scopeId+"/Station."+(name=="word100"?"Word":"Input"),
                        Direction=MPADataDirection.Inbound,
                        Operation=MPADataOperation.WriteValue,Required=true});
            },"SCADA多设备正式配置",CancellationToken.None).GetAwaiter().GetResult();
            Check(saved.Success,"多设备MHS保存失败: "+saved.ErrorCode);
            if(section.HasPendingApply)
                Check(section.ApplyPendingAtTaskBoundary(),"多设备MHS未能生效");
        }

        private static void RunTwoDevices(string root)
        {
            using(TcpPlcFixture plcA=new TcpPlcFixture(73,99))
            using(TcpPlcFixture plcB=new TcpPlcFixture(45,66))
            {
                string project=Path.Combine(root,"multi.cxproj");
                string configs=Path.Combine(root,"multi-configs");
                Guid a,b;
                using(MEMEngineeringSession engineering=MEMEngineering.Create("双工站工业工程"))
                {
                    a=engineering.AddPage("StationA").Id;
                    b=engineering.AddPage("StationB").Id;
                    engineering.SaveAs(project);
                }
                ConfigureDevice(configs,"connector-A","stationA","station-A-channel",plcA.Port);
                ConfigureDevice(configs,"connector-B","stationB","station-B-channel",plcB.Port);
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
                using(MRHScadaRuntimeGroup group=MRHScadaRuntimeGroup.Open(
                    project,configs,new[]{launchA,launchB}))
                {
                    Check(group.Count==2&&group.Find(a).DeviceRef!=group.Find(b).DeviceRef &&
                        group.Find(a).Device.DataTable.ScopeId=="stationA" &&
                        group.Find(b).Device.DataTable.ScopeId=="stationB" &&
                        group.Find(a).Device.ChannelId!=group.Find(b).Device.ChannelId,
                        "多设备Group没有独立Device/Scope/Channel所有权");
                    group.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
                    Check(SpinWait.SpinUntil(()=>
                        group.Find(a).CompletedCycles>=2 &&
                        group.Find(b).CompletedCycles>=2,10000),
                        "两个独立TCP工站没有同时完成至少2轮FC03");
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
                    group.Find(a).StopAsync().GetAwaiter().GetResult();
                    Check(group.Find(a).Device.Inspect("word100").Quality==
                        MDSVariableQuality.Stale,
                        "单个PLC停止后必须独立降级MDS质量");
                    int beforeB=plcB.Reads;
                    Check(SpinWait.SpinUntil(()=>plcB.Reads>=beforeB+2,5000) &&
                        group.Find(b).Device.Inspect("word100").Quality==
                            MDSVariableQuality.Good,
                        "PLC-A停止不得影响PLC-B的Channel/采集/MDS");
                    group.StopAsync().GetAwaiter().GetResult();
                    int readsA=plcA.Reads,readsB=plcB.Reads;
                    Thread.Sleep(220);
                    Check(plcA.Reads==readsA&&plcB.Reads==readsB,
                        "设备组Stop后不允许任何残余TCP采样");
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
                    using(MRHScadaRuntime scada=MRHScadaRuntime.Open(
                        project,configs,deviceGuid,moduleId,"plc",90,true))
                    {
                        Check(scada.DeviceRef==deviceGuid.ToString("N") &&
                            scada.Device.PointDefinitions.Length==2 &&
                            scada.Device.PointDefinitions.All(x=>!x.Writable),
                            "未由MEM/MHS正式资源导出稳定身份或只读点表");
                        using(MRKCommandCenter commands=new MRKCommandCenter())
                            Reject<UnauthorizedAccessException>(()=>
                                scada.Device.RegisterWriteCommands(commands,3001));
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
                    Console.WriteLine("PASS SCADA G1 MEM/MHS TCP/MDS polling Recovery TwoStations Isolation Stop restart version-drift");
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
