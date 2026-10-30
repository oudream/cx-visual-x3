using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Interop;
using CxVisual.Foundation.Transport;
using CxVisual.Mechanism.Connectivity;
using CxVisual.Mechanism.Core.Modules;
using CxVisual.Mechanism.Data.Configuration;

namespace CxVisual.TestCenter.UI
{
    /// <summary>
    /// 真实WPF/UIA + 正式MHS配置 + OS TCP + MCN/MPA Modbus FC03。
    /// 回环TCP对端是明确的测试夹具，不代表真实PLC或现场G2。
    /// </summary>
    public static class TCTPlcReadOnlyUiSmoke
    {
        private static void Check(bool ok,string reason)
        {if(!ok)throw new InvalidOperationException("TCT.Plc.UIA: "+reason);}
        private static AutomationElement Find(IntPtr hwnd,string id)
        {
            AutomationElement root=AutomationElement.FromHandle(hwnd);
            AutomationElement element=root.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.AutomationIdProperty,id));
            if(element==null)throw new InvalidOperationException("找不到UIA "+id);
            return element;
        }
        private static Task Click(IntPtr hwnd,string id)
        {
            return Task.Run(()=>{
                InvokePattern action=Find(hwnd,id).GetCurrentPattern(
                    InvokePattern.Pattern) as InvokePattern;
                if(action==null)throw new InvalidOperationException("没有Invoke UIA: "+id);
                action.Invoke();
            });
        }
        private static Task SetValue(IntPtr hwnd,string id,string value)
        {
            return Task.Run(()=>{
                ValuePattern input=Find(hwnd,id).GetCurrentPattern(
                    ValuePattern.Pattern) as ValuePattern;
                if(input==null)throw new InvalidOperationException("没有Value UIA: "+id);
                input.SetValue(value);
            });
        }
        private static async Task Until(Func<bool> predicate,string message)
        {
            for(int i=0;i<240;i++)
            {
                if(predicate())return;
                await Task.Delay(50);
            }
            throw new TimeoutException("PLC UIA检查超时: "+message);
        }

        private sealed class TcpRegisterFixture : IDisposable
        {
            private readonly TcpListener _listener;
            private readonly Task _server;
            private TcpClient _peer;
            private int _reads;
            private int _blockReads;
            public int Port {get;private set;}
            public int Reads {get{return Volatile.Read(ref _reads);}}
            public int BlockReads {get{return Volatile.Read(ref _blockReads);}}
            public TcpRegisterFixture()
            {
                _listener=new TcpListener(IPAddress.Loopback,0);
                _listener.Start();
                Port=((IPEndPoint)_listener.LocalEndpoint).Port;
                _server=Task.Run((Action)Serve);
            }
            private static bool ReadFully(NetworkStream stream,byte[] dst)
            {
                int read=0;
                while(read<dst.Length)
                {
                    int n=stream.Read(dst,read,dst.Length-read);
                    if(n==0)
                    {
                        if(read==0)return false;
                        throw new EndOfStreamException("测试夹具半帧断开");
                    }
                    read+=n;
                }
                return true;
            }
            private void Serve()
            {
                try
                {
                    using(TcpClient peer=_listener.AcceptTcpClient())
                    using(NetworkStream stream=peer.GetStream())
                    {
                        _peer=peer;peer.NoDelay=true;
                        while(true)
                        {
                            byte[] header=new byte[7];
                            if(!ReadFully(stream,header))break;
                            int length=(header[4]<<8)|header[5];
                            if(length!=6||header[2]!=0||header[3]!=0)
                                throw new InvalidDataException("测试FC03 MBAP长度/Protocol错误");
                            byte[] request=new byte[5];
                            if(!ReadFully(stream,request))break;
                            if(request[0]!=3)
                                throw new InvalidDataException("只读诊断夹具拒绝非FC03命令");
                            int start=(request[1]<<8)|request[2];
                            int count=(request[3]<<8)|request[4];
                            if(count<1||count>125||start<100||start+count>102)
                                throw new InvalidDataException("诊断点数或站内地址不合法");
                            if(count>1)Interlocked.Increment(ref _blockReads);
                            byte[] response=new byte[7+2+count*2];
                            Array.Copy(header,0,response,0,7);
                            response[4]=0;
                            response[5]=(byte)(3+count*2);
                            response[7]=3;
                            response[8]=(byte)(count*2);
                            for(int i=0;i<count;i++)
                            {
                                ushort value=(ushort)(start+i==100?90:99);
                                response[9+i*2]=(byte)(value>>8);
                                response[10+i*2]=(byte)value;
                            }
                            Interlocked.Increment(ref _reads);
                            stream.Write(response,0,response.Length);
                        }
                    }
                }
                catch(SocketException){}
                catch(ObjectDisposedException){}
                catch(IOException){}
            }
            public void Dispose()
            {
                if(_peer!=null)_peer.Close();
                _listener.Stop();
                if(!_server.Wait(3000))throw new TimeoutException("TCP测试端未关闭");
            }
        }

        public static async Task RunAsync()
        {
            string root=Path.Combine(Path.GetTempPath(),"CxVisual-PlcRead-UIA-"+
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            Window window=null;
            try
            {
                using(TcpRegisterFixture device=new TcpRegisterFixture())
                {
                    string id="Plc"+Guid.NewGuid().ToString("N").Substring(0,9);
                    string configRoot=Path.Combine(root,"Configs");
                    MHSConfigService service=new MHSConfigService(configRoot);
                    var section=service.ForScope("module",id).Open<MCNConnectorConfiguration>();
                    var saved=await section.UpdateAsync(model=>{
                        model.DisplayName="PLC ReadOnly真实TCP UIA";
                        model.Channel=new FTRTcpClientChannelConfiguration {
                            Id="tctplc"+Guid.NewGuid().ToString("N").Substring(0,7),
                            Kind=FTRChannelKind.TcpClient,
                            Host="127.0.0.1",Port=device.Port,ConnectTimeoutMs=2000
                        };
                        model.Protocol=new MPAProtocolConfiguration("plc-uia","modbus.tcp");
                        model.Protocol.TimeoutMs=500;
                        model.Protocol.Points.Add(new MPAPointDefinition(
                            "word100","holding:100",typeof(ushort)){
                            StationAddress="1",CanRead=true,CanWrite=false});
                        model.Protocol.Points.Add(new MPAPointDefinition(
                            "input101","holding:101",typeof(ushort)){
                            StationAddress="1",CanRead=true,CanWrite=false});
                    },"TestCenter PLC只读正式配置",CancellationToken.None);
                    Check(saved.Success,"正式Connector MHS配置保存失败: "+saved.ErrorCode);
                    if(section.HasPendingApply)
                        Check(section.ApplyPendingAtTaskBoundary(),"正式Effective未生效");

                    TCTManualModuleFrame frame=new TCTManualModuleFrame(configRoot);
                    window=new Window {Title="TestCenter PLC ReadOnly UIA",
                        Width=1220,Height=850,Content=frame};
                    window.Show();window.UpdateLayout();
                    frame.SelectModuleKind(MFMModuleKind.Connector);
                    frame.SetModuleId(id);
                    IntPtr hwnd=new WindowInteropHelper(window).Handle;
                    Check(hwnd!=IntPtr.Zero,"WPF窗口未建立");
                    await Click(hwnd,"TCT.Module.Open");
                    await Until(()=>frame.HasOpenedConfiguration&&!frame.IsBusy,
                        "正式Connector打开");
                    await Task.Run(()=>{
                        SelectionItemPattern tab=Find(hwnd,
                            "TCT.Module.Page.connector-plc").GetCurrentPattern(
                            SelectionItemPattern.Pattern) as SelectionItemPattern;
                        if(tab==null)throw new InvalidOperationException("PLC Tab UIA不可选择");
                        tab.Select();
                    });
                    window.UpdateLayout();
                    TCTPlcReadOnlyPanel panel=frame.PlcReadOnlyPanel;
                    Check(panel!=null&&!panel.IsStarted&&panel.SuccessfulReads==0,
                        "未连接PLC面板不能生成任何成功回包");
                    await Click(hwnd,"TCT.Plc.Connect");
                    await Until(()=>panel.IsStarted&&!panel.IsBusy,
                        "正式MCN+FTR TCP连接");
                    await SetValue(hwnd,"TCT.Plc.PointNames","word100,input101");
                    await Click(hwnd,"TCT.Plc.Read");
                    await Until(()=>panel.SuccessfulReads==1&&!panel.IsBusy,
                        "真实FC03连续两点");
                    Check(device.Reads==1&&device.BlockReads==1 &&
                        panel.StatusMessage.StartsWith("FC03单事务完成"),
                        "未通过真实单报文得到两点FC03数据");
                    await SetValue(hwnd,"TCT.Plc.PointNames","input101,word100");
                    await Click(hwnd,"TCT.Plc.Read");
                    await Until(()=>panel.StatusMessage.StartsWith("读取失败")&&!panel.IsBusy,
                        "反序点表应由正式协议拒绝");
                    Check(device.Reads==1&&panel.SuccessfulReads==1,
                        "非法连续地址顺序误发送请求或冒充成功");
                    await Click(hwnd,"TCT.Plc.Stop");
                    await Until(()=>!panel.IsStarted&&!panel.IsBusy,
                        "显式断开PLC Connector");
                    Check(!frame.IsBusy,"停止后模块应可切换");
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                        "TestCenter-PlcReadOnly-UIA-Report.txt"),
                        "PASS TCT.Plc.UIA saved-Effective/real-TCP/MCN/FC03-single-frame/invalid-order/stop"+
                        Environment.NewLine+"Effective="+section.EffectiveVersion+
                        Environment.NewLine+"RealTcpReads="+device.Reads+
                        Environment.NewLine+"BlockReads="+device.BlockReads+
                        Environment.NewLine+"Boundary=SoftwareLoopback_NotRealPlcG2G3");
                }
            }
            finally
            {
                if(window!=null)window.Close();
                try{Directory.Delete(root,true);}catch(IOException){}
            }
        }
    }
}
