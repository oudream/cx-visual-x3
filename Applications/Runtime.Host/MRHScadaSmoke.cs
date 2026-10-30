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
            public TcpPlcFixture()
            {
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
                                response[9]=0;response[10]=73;
                                response[11]=0;response[12]=99;
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
                    Console.WriteLine("PASS SCADA G1 MEM-save/reopen MHS-Effective TCP-Modbus MDS polling Stop restart version-drift");
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
