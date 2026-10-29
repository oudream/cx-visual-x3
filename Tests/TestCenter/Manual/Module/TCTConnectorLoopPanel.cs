using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Foundation.Transport;
using CxVisual.Mechanism.Connectivity;
using CxVisual.Mechanism.Core.Modules;
using CxVisual.Mechanism.Data.Configuration;

namespace CxVisual.TestCenter.UI
{
    /// <summary>
    /// A real Channel/Protocol software acceptance route inside the one-Connector MFM UI.
    /// Virtual peer is deliberately NOT a PLC/device simulator or hardware ACK.
    /// </summary>
    public sealed class TCTConnectorLoopPanel : UserControl
    {
        private readonly MHSConfigService _service;
        private readonly Func<bool> _dirty;
        private readonly TextBox _payload=new TextBox {Text="Hello Connector",
            Width=280,Margin=new Thickness(4)};
        private readonly CheckBox _drop=new CheckBox {Content="测试端丢弃响应（验证超时）",
            Margin=new Thickness(7,5,7,4)};
        private readonly TextBlock _status=new TextBlock {TextWrapping=TextWrapping.Wrap,
            Margin=new Thickness(4,3,4,5)};
        private readonly Button _load=Button("TCT.Connector.Load","读取正式Effective");
        private readonly Button _start=Button("TCT.Connector.Start","启动Virtual Channel/Protocol");
        private readonly Button _send=Button("TCT.Connector.Send","发送并等待规约回环");
        private readonly Button _stop=Button("TCT.Connector.Stop","停止Connector");
        private MCNConnectorModule _module;
        private FTRVirtualChannel[] _pair;
        private MPALengthFrameProtocol _peerProtocol;
        private string _id,_effectiveVersion;
        private int _timeoutMs,_responses;
        private bool _busy;
        public bool IsBusy {get{return _busy;}}
        public bool IsStarted {get{return _module!=null&&_module.IsStarted;}}
        public string StatusMessage {get{return _status.Text;}}
        public int SuccessfulResponses {get{return _responses;}}
        public string LastPayload {get;private set;}
        public TCTConnectorLoopPanel(MHSConfigService service,Func<bool> isDraftDirty)
        {
            _service=service??throw new ArgumentNullException("service");
            _dirty=isDraftDirty??throw new ArgumentNullException("isDraftDirty");
            AutomationProperties.SetAutomationId(this,"TCT.Connector.Panel");
            AutomationProperties.SetAutomationId(_status,"TCT.Connector.Status");
            AutomationProperties.SetAutomationId(_payload,"TCT.Connector.Payload");
            AutomationProperties.SetAutomationId(_drop,"TCT.Connector.DropReply");
            var stack=new StackPanel {Margin=new Thickness(4)};
            stack.Children.Add(new TextBlock {Text="Connector · 真实Virtual Channel + LengthFrame协议收发",
                FontWeight=FontWeights.SemiBold});
            stack.Children.Add(new TextBlock {Text=
                "仅验证正式Channel/Protocol的本地回环、CRC帧解析、超时和停止拒绝。"+
                "对端为本次测试的Virtual Peer，并非工业PLC、远程TCP或真实设备执行确认。",
                TextWrapping=TextWrapping.Wrap});
            var buttons=new WrapPanel();
            foreach(var button in new[]{_load,_start,_send,_stop})buttons.Children.Add(button);
            stack.Children.Add(buttons);
            var row=new WrapPanel();
            row.Children.Add(new TextBlock {Text="报文",VerticalAlignment=VerticalAlignment.Center});
            row.Children.Add(_payload);
            row.Children.Add(_drop);
            stack.Children.Add(row);stack.Children.Add(_status);
            Content=new ScrollViewer {Content=stack,MaxHeight=410,
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
            _load.Click+=(s,e)=>LoadEffective();
            _start.Click+=async(s,e)=>await StartAsync();
            _send.Click+=async(s,e)=>await SendAsync();
            _stop.Click+=async(s,e)=>await StopAsync();
            Bind(null);
        }
        private static Button Button(string id,string label)
        {
            var b=new Button {Content=label,Margin=new Thickness(4),
                Padding=new Thickness(8,4,8,4)};
            AutomationProperties.SetAutomationId(b,id);return b;
        }
        public void Bind(string moduleId)
        {
            if(_busy||IsStarted)throw new InvalidOperationException("Connector运行中禁止切换配置");
            if(_id==moduleId)return;
            Cleanup();
            _id=moduleId;_effectiveVersion=null;_responses=0;LastPayload=null;
            _status.Text=moduleId==null?"请先打开正式Connector配置":
                "已选择Connector "+moduleId+"，保存MHS后显式读取Virtual/LengthFrame Effective。";
        }
        private MCNConnectorConfiguration RequireEffective(out string version)
        {
            if(String.IsNullOrWhiteSpace(_id))throw new InvalidOperationException("未选择Connector模块");
            if(_dirty())throw new InvalidOperationException("FCM配置草稿尚未保存");
            var section=_service.OpenExisting<MCNConnectorConfiguration>(
                new FCMConfigIdentity("CxVisual.Module.Connector","module",_id));
            MCNConnectorConfiguration config;
            using(var capture=section.CaptureForTask("connector-virtual-echo"))
                config=capture.Get();
            version=section.EffectiveVersion;
            config.Validate();
            if(!config.Enabled||config.Channel.Kind!=FTRChannelKind.Virtual||
                config.Protocol.Kind!=MPAProtocolKind.LengthFrame)
                throw new InvalidOperationException("本页只支持已生效Virtual Channel + LengthFrame Protocol");
            return config;
        }
        private void Cleanup()
        {
            if(_pair!=null)
            {
                if(_pair[0].State==FTRChannelState.Closed)_pair[0].Dispose();
                if(_pair[1].State==FTRChannelState.Closed)_pair[1].Dispose();
            }
            _pair=null;_module=null;_peerProtocol=null;
        }
        public void LoadEffective()
        {
            if(_busy||IsStarted){_status.Text="先停止Connector后才能装配其它Effective";return;}
            _module=null;_effectiveVersion=null;
            try
            {
                string version;
                MCNConnectorConfiguration config=RequireEffective(out version);
                Cleanup();
                _pair=FTRVirtualChannel.CreatePair(config.Channel.Id,config.Channel.Id+".peer");
                _module=new MCNConnectorModule(config.Describe(_id,MFMModuleKind.Connector),
                    config,_pair[0],new MPALengthFrameProtocol());
                _peerProtocol=new MPALengthFrameProtocol();
                _timeoutMs=config.Protocol.TimeoutMs;
                _effectiveVersion=version;
                _responses=0;LastPayload=null;
                _status.Text="已装配正式Connector Effective "+version+"：1 Channel / 1 Protocol，"+
                    "PointTable="+config.Protocol.Points.Count+
                    " / DataMapping="+config.Protocol.Mappings.Count+
                    "。尚未连接或执行。";
            }
            catch(Exception ex){_status.Text="Connector配置/装配拒绝："+ex.Message;}
        }
        public async Task StartAsync()
        {
            if(_busy){_status.Text="Connector操作未结束";return;}
            _busy=true;
            try
            {
                if(_module==null)throw new InvalidOperationException("请先读取Effective");
                string version;RequireEffective(out version);
                if(version!=_effectiveVersion)throw new InvalidOperationException("Effective变更，重新装配");
                _pair[1].Open();
                _pair[1].Received+=HandleVirtualPeer;
                await _module.StartAsync(CancellationToken.None);
                _status.Text="真实Virtual Channel已Open / LengthFrame协议就绪；"+
                    "Session="+_pair[0].SessionId.ToString("N")+"（仅软件验证）。";
            }
            catch(Exception ex)
            {
                if(_pair!=null&&_pair[1].State==FTRChannelState.Open)
                {_pair[1].Received-=HandleVirtualPeer;_pair[1].Close();}
                _status.Text="Connector启动拒绝："+ex.Message;
            }
            finally{_busy=false;}
        }
        private void HandleVirtualPeer(FTRChannelReceiveContext received)
        {
            var ctx=new MPAProtocolContext(_module.Configuration.Protocol.Id,
                _pair[1].SessionId.ToString("N"),received.Address);
            foreach(byte[] decoded in _peerProtocol.ProcessReceived(received,ctx))
            {
                if(_drop.IsChecked==true)continue;
                byte[] response=_peerProtocol.PrepareSend(decoded,ctx);
                _pair[1].SendAsync(response,new FTRSubChannelAddress(_pair[1].Id),
                    CancellationToken.None).GetAwaiter().GetResult();
            }
        }
        public async Task SendAsync()
        {
            if(_busy){_status.Text="已有Connector操作";return;}
            _busy=true;
            try
            {
                if(!IsStarted)throw new InvalidOperationException("Connector尚未Start");
                string version;RequireEffective(out version);
                if(version!=_effectiveVersion)throw new InvalidOperationException("Effective版本已变");
                string message=_payload.Text;
                if(String.IsNullOrEmpty(message)||Encoding.UTF8.GetByteCount(message)>65536)
                    throw new ArgumentException("报文须为1～65536字节");
                var promise=new TaskCompletionSource<byte[]>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                Action<byte[]> onPayload=bytes=>promise.TrySetResult((byte[])bytes.Clone());
                _module.PayloadReceived+=onPayload;
                try
                {
                    await _module.SendPayloadAsync(Encoding.UTF8.GetBytes(message),
                        CancellationToken.None);
                    Task done=await Task.WhenAny(promise.Task,
                        Task.Delay(Math.Max(1,_timeoutMs)));
                    if(!Object.ReferenceEquals(done,promise.Task))
                        throw new TimeoutException("虚拟对端未返回规约帧（测试丢包超时）");
                    byte[] actual=await promise.Task;
                    string decoded=Encoding.UTF8.GetString(actual);
                    if(decoded!=message)throw new InvalidOperationException("规约数据未保持端到端一致");
                    LastPayload=decoded;_responses++;
                    _status.Text="真实Channel+Protocol回环成功："+decoded+
                        "；接收次数="+_responses+
                        "；Session="+_pair[0].SessionId.ToString("N")+
                        "。不代表现场设备完成动作。";
                }
                finally{_module.PayloadReceived-=onPayload;}
            }
            catch(Exception ex){_status.Text="Connector收发拒绝："+ex.Message;}
            finally{_busy=false;}
        }
        public async Task StopAsync()
        {
            if(_busy){_status.Text="Connector操作未完成";return;}
            _busy=true;
            try
            {
                if(!IsStarted)throw new InvalidOperationException("Connector尚未启动");
                await _module.StopAsync(CancellationToken.None);
                _pair[1].Received-=HandleVirtualPeer;
                _pair[1].Close();
                _status.Text="Connector已Stopped / 两端Virtual Channel Closed；再次Send应拒绝。";
            }
            catch(Exception ex){_status.Text="Connector停止失败："+ex.Message;}
            finally{_busy=false;}
        }
        public void Close()
        {
            if(_busy)throw new InvalidOperationException("Connector仍在收发");
            if(IsStarted)StopAsync().GetAwaiter().GetResult();
            Cleanup();
        }
    }
}
