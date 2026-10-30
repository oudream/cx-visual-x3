using System;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Mechanism.Data.RuntimeData;
using CxVisual.Professional.HMI;

namespace CxVisual.Tests.ArchitectureSmoke
{
    /// <summary>
    /// Pipe v1线协议消费合同G1：真Windows NamedPipe字节帧、真实HMI解析/代次撤回。
    /// 本测试服务端是契约夹具；A的真正PLC/Runtime.Host跨进程验证由A的G1单独证明。
    /// </summary>
    internal static class HmiScadaPipeTests
    {
        private static void Check(bool ok,string message)
        { if(!ok)throw new InvalidOperationException("HMI-Pipe-G1: "+message); }
        private static void ReadAll(Stream stream,byte[] data)
        {
            int count=0;
            while(count<data.Length)
            {
                int size=stream.Read(data,count,data.Length-count);
                if(size<=0)throw new EndOfStreamException("test pipe truncated");
                count+=size;
            }
        }
        public static void Run()
        {
            string name="cxvisual-hmi-g1-"+Guid.NewGuid().ToString("N");
            string device=Guid.NewGuid().ToString("N");
            string utc=DateTime.UtcNow.ToString("O",CultureInfo.InvariantCulture);
            string discovery="{\"SchemaVersion\":1,\"Status\":\"Ok\",\"ErrorCode\":null,"+
                "\"DeploymentVersion\":\"DEP1\",\"Devices\":[{\"DeviceRef\":\""+device+
                "\",\"ScopeId\":\"plc/test\",\"ConfigVersion\":\"CFG1\","+
                "\"Generation\":\"GEN1\",\"Running\":true,\"Points\":[]}]}";
            string snapshot="{\"SchemaVersion\":1,\"Status\":\"Ok\",\"ErrorCode\":null,"+
                "\"DeploymentVersion\":\"DEP1\",\"Devices\":[{\"DeviceRef\":\""+device+
                "\",\"ScopeId\":\"plc/test\",\"ConfigVersion\":\"CFG1\","+
                "\"Generation\":\"GEN1\",\"Running\":true,\"Points\":[{"+
                "\"StablePointId\":\"word100\",\"VariableId\":\"plc/test/word100\","+
                "\"Type\":\"UInt16\",\"Unit\":\"rpm\",\"Value\":\"91\","+
                "\"HasValue\":true,\"Quality\":\"Good\",\"RefreshUtc\":\""+utc+
                "\",\"ChangeUtc\":\""+utc+"\",\"SourceId\":\"real-mds\","+
                "\"Revision\":4}]}]}";
            string rejected="{\"SchemaVersion\":1,\"Status\":\"Rejected\","+
                "\"ErrorCode\":\"SESSION_VERSION_MISMATCH\",\"Devices\":[]}";
            string[] replies={discovery,snapshot,rejected,snapshot,rejected};
            Task server=Task.Run(()=>
            {
                foreach(string response in replies)
                {
                    using(NamedPipeServerStream pipe=new NamedPipeServerStream(
                        name,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.None))
                    {
                        pipe.WaitForConnection();
                        byte[] length=new byte[4];ReadAll(pipe,length);
                        int count=BitConverter.ToInt32(length,0);
                        Check(count>0&&count<=4096,"客户端请求帧超限");
                        byte[] request=new byte[count];ReadAll(pipe,request);
                        string text=Encoding.UTF8.GetString(request);
                        Check(text.Contains("\"SchemaVersion\":1") &&
                            (text.Contains("\"Operation\":\"discover\"")||
                             text.Contains("\"Operation\":\"snapshot\"")),
                            "请求没有使用A正式v1协议");
                        byte[] data=Encoding.UTF8.GetBytes(response);
                        byte[] size=BitConverter.GetBytes(data.Length);
                        pipe.Write(size,0,4);pipe.Write(data,0,data.Length);pipe.Flush();
                    }
                }
            });
            PHMScadaReadOnlyPipeClient client=new PHMScadaReadOnlyPipeClient(name);
            var devices=client.Discover();
            Check(devices.Count==1&&devices[0].DeviceRef.ToString("N")==device&&
                devices[0].ScopeId=="plc/test","发现设备应来自Host真身份/版本");
            PHMScadaDeviceSnapshot sample=client.Snapshot(devices[0]);
            Check(sample.Points.Count==1&&sample.Points[0].StablePointId=="word100"&&
                sample.Points[0].Value is ushort &&
                (ushort)sample.Points[0].Value==91 &&
                sample.Points[0].Revision==4&&sample.Points[0].Source=="real-mds",
                "Snapshot没有保真MDS UInt16/Revision/Source");
            bool denied=false;
            try{client.Snapshot(devices[0]);}
            catch(PHMScadaPipeRejectedException error)
            {denied=error.ErrorCode=="SESSION_VERSION_MISMATCH";}
            Check(denied,"Host版本漂移必须拒绝，不可重发或重用旧数据");
            using(PHMScadaRemoteReadSession remote=new PHMScadaRemoteReadSession(
                client,devices[0],TimeSpan.FromMinutes(1)))
            using(ManualResetEventSlim first=new ManualResetEventSlim(false))
            using(ManualResetEventSlim lost=new ManualResetEventSlim(false))
            using(remote.Subscribe(()=>
            {
                object value=remote.Resolve(MDSBindingSource.Variable,
                    "word100",typeof(double));
                if(value!=null&&(double)value==91d)first.Set();
                else if(first.IsSet)lost.Set();
            }))
            {
                remote.Start(250);
                Check(first.Wait(TimeSpan.FromSeconds(5)),
                    "真实Pipe首帧不能驱动MVI绑定值");
                Check(lost.Wait(TimeSpan.FromSeconds(5))&&
                    remote.Resolve(MDSBindingSource.Variable,"word100",typeof(double))==null,
                    "新Generation拒绝后必须立刻撤回旧Good运行帧");
            }
            Check(server.Wait(TimeSpan.FromSeconds(10)), "Pipe v1服务端契约夹具没有完成");
            // .NET Framework 4.8真实IPC超时反证：连接成功后Host不回包，
            // 客户端必须在IO截止关闭句柄，不允许HMI后台线程永久阻塞。
            string silentName="cxvisual-hmi-silent-"+Guid.NewGuid().ToString("N");
            Task silent=Task.Run(()=>
            {
                using(NamedPipeServerStream endpoint=new NamedPipeServerStream(silentName,
                    PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.None))
                {
                    endpoint.WaitForConnection();
                    byte[] size=new byte[4];ReadAll(endpoint,size);
                    byte[] packet=new byte[BitConverter.ToInt32(size,0)];
                    ReadAll(endpoint,packet);
                    Thread.Sleep(3700);
                }
            });
            bool expired=false;
            try{new PHMScadaReadOnlyPipeClient(silentName).Discover();}
            catch(TimeoutException){expired=true;}
            Check(expired,"Host连接后不响应必须在3秒IO截止内fail closed");
            Check(silent.Wait(TimeSpan.FromSeconds(7)),
                "真实超时Pipe测试服务端未收敛");
            // Host第一次未采样允许Value、RefreshUtc、ChangeUtc为空，
            // 但不允许客户端把该点伪造为数值0/当前时间/Quality.Good。
            string blankName="cxvisual-hmi-unsampled-"+Guid.NewGuid().ToString("N");
            Task blank=Task.Run(()=>
            {
                using(NamedPipeServerStream endpoint=new NamedPipeServerStream(blankName,
                    PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.None))
                {
                    endpoint.WaitForConnection();
                    byte[] size=new byte[4];ReadAll(endpoint,size);
                    byte[] packet=new byte[BitConverter.ToInt32(size,0)];
                    ReadAll(endpoint,packet);
                    string response="{\"SchemaVersion\":1,\"Status\":\"Ok\","+
                        "\"DeploymentVersion\":\"DEP1\",\"Devices\":[{\"DeviceRef\":\""+device+
                        "\",\"ScopeId\":\"plc/test\",\"ConfigVersion\":\"CFG1\","+
                        "\"Generation\":\"GEN1\",\"Running\":true,\"Points\":[{"+
                        "\"StablePointId\":\"word100\",\"VariableId\":\"plc/test/word100\","+
                        "\"Type\":\"UInt16\",\"HasValue\":false,\"Value\":null,"+
                        "\"Quality\":\"Uncertain\",\"RefreshUtc\":null,"+
                        "\"ChangeUtc\":null,\"Revision\":0}]}]}";
                    byte[] bytes=Encoding.UTF8.GetBytes(response);
                    endpoint.Write(BitConverter.GetBytes(bytes.Length),0,4);
                    endpoint.Write(bytes,0,bytes.Length);endpoint.Flush();
                }
            });
            PHMScadaDeviceSnapshot unsampled=
                new PHMScadaReadOnlyPipeClient(blankName).Snapshot(devices[0]);
            Check(unsampled.Points.Count==1&&!unsampled.Points[0].HasValue&&
                unsampled.Points[0].Value==null&&unsampled.Points[0].Text=="—"&&
                unsampled.Points[0].RefreshUtc==default(DateTime),
                "未采样点不能冒充Good值/采样时间");
            Check(blank.Wait(TimeSpan.FromSeconds(5)),
                "未采样Pipe测试服务端未正常退出");
            Console.WriteLine("PASS HMI Pipe v1 real NamedPipe transport, device identity, "+
                "UInt16/MDS and generation fail-closed (contract fixture, not PLC)");
        }
    }
}
