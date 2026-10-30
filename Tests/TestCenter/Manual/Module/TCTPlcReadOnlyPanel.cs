using System;
using System.Collections.Generic;
using System.Linq;
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
    /// 一次真实FC03报文的只读观察证据。不是MDS Current、不是设备健康Good，
    /// 也不能替代PLC型号、序列号与现场G2/HIL核实。
    /// </summary>
    public sealed class TCTPlcPointReadEvidence
    {
        public string PointName { get; internal set; }
        public string Address { get; internal set; }
        public string ValueType { get; internal set; }
        public string ObservedValue { get; internal set; }
        public DateTime ReceivedUtc { get; internal set; }
    }

    /// <summary>
    /// 使用正式MHS Effective装配真正TCP/MCN/MPA，只读一次或相邻点单报文FC03；
    /// 不伪造实体PLC，不注册旁路设备命令，也不拥有MDS第二份Current。
    /// 没有操作员显式启动时不接触网络。写点权限始终不在本页暴露。
    /// </summary>
    public sealed class TCTPlcReadOnlyPanel : UserControl
    {
        private readonly MHSConfigService _service;
        private readonly Func<bool> _isDraftDirty;
        private readonly TextBox _pointNames = new TextBox {
            Width=350,Margin=new Thickness(4),ToolTip="按地址升序填写同站点点名，以逗号分隔，例如 word100,input101"
        };
        private readonly Button _connect = CreateButton("TCT.Plc.Connect", "连接正式Effective");
        private readonly Button _read = CreateButton("TCT.Plc.Read", "读取一次 FC03");
        private readonly Button _stop = CreateButton("TCT.Plc.Stop", "断开");
        private readonly TextBlock _status = new TextBlock {
            TextWrapping=TextWrapping.Wrap,Margin=new Thickness(4)
        };
        private readonly DataGrid _evidence = new DataGrid {
            IsReadOnly=true,AutoGenerateColumns=true,CanUserAddRows=false,
            CanUserDeleteRows=false,MaxHeight=255,Margin=new Thickness(4)
        };
        private MCNConnectorModule _connector;
        private MCNConnectorConfiguration _effective;
        private string _moduleId;
        private string _effectiveVersion;
        private bool _busy;
        private int _reads;

        public bool IsBusy { get { return _busy; } }
        public bool IsStarted { get { return _connector != null && _connector.IsStarted; } }
        public int SuccessfulReads { get { return _reads; } }
        public string StatusMessage { get { return _status.Text; } }

        private static Button CreateButton(string automationId, string label)
        {
            Button button=new Button {Content=label,Margin=new Thickness(4),
                Padding=new Thickness(9,4,9,4)};
            AutomationProperties.SetAutomationId(button,automationId);
            return button;
        }

        public TCTPlcReadOnlyPanel(MHSConfigService service,Func<bool> isDraftDirty)
        {
            _service=service??throw new ArgumentNullException("service");
            _isDraftDirty=isDraftDirty??throw new ArgumentNullException("isDraftDirty");
            AutomationProperties.SetAutomationId(this,"TCT.Plc.ReadOnlyPanel");
            AutomationProperties.SetAutomationId(_pointNames,"TCT.Plc.PointNames");
            AutomationProperties.SetAutomationId(_status,"TCT.Plc.Status");
            AutomationProperties.SetAutomationId(_evidence,"TCT.Plc.ReadEvidence");
            StackPanel content=new StackPanel {Margin=new Thickness(6)};
            content.Children.Add(new TextBlock {
                Text="PLC/IO · 正式Modbus TCP只读点表诊断",
                FontWeight=FontWeights.SemiBold,FontSize=15
            });
            content.Children.Add(new TextBlock {
                Text="仅显式连接已保存且生效的Connector配置；读取真实TCP FC03响应。"+
                     "它可能来自模拟器，不验证设备SN/固件或硬件安全状态；"+
                     "只读诊断证据不写入MDS Current，也不提供FC06/远程控制。",
                Margin=new Thickness(4,3,4,5),TextWrapping=TextWrapping.Wrap
            });
            WrapPanel buttons=new WrapPanel();
            buttons.Children.Add(_connect);
            buttons.Children.Add(_read);
            buttons.Children.Add(_stop);
            content.Children.Add(buttons);
            WrapPanel pointRow=new WrapPanel {Margin=new Thickness(4)};
            pointRow.Children.Add(new TextBlock {
                Text="读取点名（连续地址、同一Unit）",
                VerticalAlignment=VerticalAlignment.Center
            });
            pointRow.Children.Add(_pointNames);
            content.Children.Add(pointRow);
            content.Children.Add(_status);
            content.Children.Add(_evidence);
            content.Children.Add(new TextBlock {
                Text="读回值仅供诊断；MDS正式采样和质量应通过Automation的PAD ProviderHost。"+
                     "寄存器相等不是物理动作完成。工业PLC G2/G3须独立核实。",
                Margin=new Thickness(4),TextWrapping=TextWrapping.Wrap
            });
            Content=new ScrollViewer {Content=content,MaxHeight=490,
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
            _connect.Click+=async(sender,args)=>await ConnectAsync();
            _read.Click+=async(sender,args)=>await ReadOnceAsync();
            _stop.Click+=async(sender,args)=>await StopAsync();
            Bind(null);
        }

        private void UpdateButtons()
        {
            _connect.IsEnabled=!_busy&&!IsStarted&&!string.IsNullOrWhiteSpace(_moduleId);
            _read.IsEnabled=!_busy&&IsStarted;
            _stop.IsEnabled=!_busy&&IsStarted;
        }

        public void Bind(string moduleId)
        {
            if (_busy||IsStarted)
                throw new InvalidOperationException("PLC诊断仍连接设备，须断开后切换模块");
            _moduleId=moduleId;
            _effective=null;
            _effectiveVersion=null;
            _evidence.ItemsSource=null;
            _reads=0;
            _status.Text=string.IsNullOrWhiteSpace(moduleId) ?
                "请先打开正式Connector配置。" :
                "已选择 "+moduleId+"；尚未连接，必须先保存和生效配置。";
            UpdateButtons();
        }

        private MCNConnectorConfiguration RequireEffective(out string version)
        {
            if(string.IsNullOrWhiteSpace(_moduleId))
                throw new InvalidOperationException("没有选中的Connector模块");
            if(_isDraftDirty())
                throw new InvalidOperationException("存在未保存的配置草稿，禁止连PLC");
            FCMConfigIdentity identity=new FCMConfigIdentity(
                "CxVisual.Module.Connector","module",_moduleId);
            var section=_service.OpenExisting<MCNConnectorConfiguration>(identity);
            MCNConnectorConfiguration config;
            using(var capture=section.CaptureForTask("testcenter-plc-readonly"))
                config=capture.Get();
            version=section.EffectiveVersion;
            config.Validate();
            if(!config.Enabled||config.Protocol.ProtocolTypeId!="modbus.tcp"||
                config.Channel.Kind!=FTRChannelKind.TcpClient)
                throw new NotSupportedException(
                    "本页只支持正式生效的Modbus TCP和TcpClient；不自动转换其它规约/Channel");
            if(config.Protocol.Points==null||
                !config.Protocol.Points.Any(p=>p!=null&&p.CanRead))
                throw new InvalidOperationException("正式点表没有可读寄存器");
            return config;
        }

        public async Task ConnectAsync()
        {
            if(_busy||IsStarted)return;
            _busy=true;UpdateButtons();
            MCNConnectorModule candidate=null;
            try
            {
                string version;
                MCNConnectorConfiguration effective=RequireEffective(out version);
                FTRTcpClientChannel channel=new FTRTcpClientChannel(effective.Channel);
                candidate=new MCNConnectorModule(
                    effective.Describe(_moduleId,MFMModuleKind.Connector),
                    effective,channel,new MPAModbusTcpProtocol());
                await candidate.StartAsync(CancellationToken.None);
                _connector=candidate;_effective=effective;_effectiveVersion=version;
                _reads=0;_evidence.ItemsSource=null;
                _pointNames.Text=effective.Protocol.Points
                    .Where(p=>p!=null&&p.CanRead)
                    .Select(p=>p.Name).First();
                _status.Text="TCP已连接；Effective="+version+
                    "。只读调试，不构成真实PLC设备身份或G2证明。";
            }
            catch(Exception error)
            {
                if(candidate!=null&&candidate.IsStarted)
                {
                    try{await candidate.StopAsync(CancellationToken.None);}
                    catch(Exception){/* 保留原始连接错误 */ }
                }
                _connector=null;_effective=null;_effectiveVersion=null;
                _status.Text="PLC连接失败（不推定设备就绪）: "+error.Message;
            }
            finally{_busy=false;UpdateButtons();}
        }

        public async Task ReadOnceAsync()
        {
            if(_busy||!IsStarted)return;
            _busy=true;UpdateButtons();
            try
            {
                string version;
                RequireEffective(out version);
                if(!string.Equals(version,_effectiveVersion,StringComparison.Ordinal))
                    throw new InvalidOperationException("Connector Effective已变化，必须断开重新装配");
                string[] points=_pointNames.Text.Split(new[]{','},
                    StringSplitOptions.RemoveEmptyEntries)
                    .Select(x=>x.Trim()).ToArray();
                if(points.Length<1||points.Length>64||
                    points.Distinct(StringComparer.Ordinal).Count()!=points.Length)
                    throw new ArgumentException("只允许1～64个不重复的已保存规约点名");
                foreach(string point in points)
                {
                    MPAPointDefinition found=_effective.Protocol.Points.FirstOrDefault(
                        p=>p!=null&&p.Name==point&&p.CanRead);
                    if(found==null)throw new InvalidOperationException("未批准或不可读的点: "+point);
                }
                IReadOnlyList<object> values=await _connector.ReadContiguousPointsAsync(
                    points,new FTRSubChannelAddress(_connector.Channel.Id),
                    CancellationToken.None);
                if(values==null||values.Count!=points.Length)
                    throw new InvalidOperationException("规约返回点数量不匹配");
                DateTime now=DateTime.UtcNow;
                TCTPlcPointReadEvidence[] result=new TCTPlcPointReadEvidence[points.Length];
                for(int i=0;i<result.Length;i++)
                {
                    MPAPointDefinition point=_effective.Protocol.Points.First(p=>p.Name==points[i]);
                    result[i]=new TCTPlcPointReadEvidence {
                        PointName=point.Name,Address=point.Address,
                        ValueType=point.ValueType,
                        ObservedValue=Convert.ToString(values[i],
                            System.Globalization.CultureInfo.InvariantCulture),
                        ReceivedUtc=now
                    };
                }
                _evidence.ItemsSource=result;
                _reads++;
                _status.Text="FC03单事务完成，返回"+result.Length+"个点；"+
                    "这是当次诊断回包，不是MDS运行仓，也不代表物理动作完成。";
            }
            catch(Exception error)
            {
                _evidence.ItemsSource=null;
                _status.Text="读取失败，禁止显示旧诊断值为本次成功结果: "+error.Message;
            }
            finally{_busy=false;UpdateButtons();}
        }

        public async Task StopAsync()
        {
            if(_busy||!IsStarted)return;
            _busy=true;UpdateButtons();
            try
            {
                await _connector.StopAsync(CancellationToken.None);
                _status.Text="Connector已断开；现场设备安全状态未知。";
            }
            catch(Exception error)
            {
                _status.Text="PLC断开结果需要核实: "+error.Message;
            }
            finally
            {
                _connector=null;_effective=null;_effectiveVersion=null;
                _evidence.ItemsSource=null;
                _busy=false;UpdateButtons();
            }
        }
    }
}
