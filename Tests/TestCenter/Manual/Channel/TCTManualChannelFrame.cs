using CxVisual.Foundation.Transport;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CxVisual.Mechanism.Connectivity;

namespace CxVisual.TestCenter.UI
{
    /// <summary>
    /// Channel P0: Single真实Virtual收发，Multi正式SubChannel目录/代次的离线验证，
    /// SharedBus正式Protocol站号点表验证。无设备时绝不声称TCP/UDP/串口已连接。
    /// 实际通信仍由正式MCC Channel Owner提供，不建立第二套Channel/Socket。
    /// </summary>
    public sealed class TCTManualChannelFrame : UserControl, ITCTWorkspaceFrame
    {
        private readonly ObservableCollection<TCTManualFrameRow> _frames =
            new ObservableCollection<TCTManualFrameRow>();
        private readonly ObservableCollection<TCTPeerRow> _peers =
            new ObservableCollection<TCTPeerRow>();
        private readonly ObservableCollection<string> _stations =
            new ObservableCollection<string>();
        private readonly ComboBox _channelKind = new ComboBox {
            MinWidth = 170, FontSize = 16, MinHeight = 34,
            Margin = new Thickness(8, 3, 12, 5) };
        private readonly TextBox _peerId = new TextBox {
            Width = 140, Text = "client01", FontSize = 16, MinHeight = 34, Margin = new Thickness(4) };
        private readonly TextBox _endpoint = new TextBox {
            Width = 180, Text = "127.0.0.1:10001", FontSize = 16, MinHeight = 34,
            Margin = new Thickness(4) };
        private readonly TextBox _station = new TextBox {
            Width = 100, Text = "1", FontSize = 16, MinHeight = 34, Margin = new Thickness(4) };
        private readonly TextBox _payload = new TextBox {
            Text = "Hello CxVisual", Margin = new Thickness(0, 5, 0, 8), MinHeight = 52,
            FontSize = 16, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
        private readonly TextBlock _topology = new TextBlock {
            FontSize = 16, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.DarkSlateBlue, Margin = new Thickness(2, 4, 2, 7) };
        private readonly TextBlock _state = new TextBlock {
            FontSize = 15, TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.SteelBlue, Margin = new Thickness(1, 7, 1, 7) };
        // 只观察正式FTR快照，不维护第二个Socket读者或业务队列。
        private readonly TextBlock _health = new TextBlock {
            FontSize=14,TextWrapping=TextWrapping.Wrap,
            Foreground=Brushes.DarkSlateGray,Margin=new Thickness(2,4,2,7) };
        private readonly DispatcherTimer _healthTimer = new DispatcherTimer {
            Interval=TimeSpan.FromSeconds(1)
        };
        private readonly ListBox _peerList = new ListBox { MinHeight = 70, FontSize = 15,
            Margin = new Thickness(0, 4, 8, 4) };
        private readonly ListBox _stationList = new ListBox { MinHeight = 70, FontSize = 15,
            Margin = new Thickness(0, 4, 8, 4) };
        private readonly StackPanel _multiPanel = new StackPanel();
        private readonly StackPanel _busPanel = new StackPanel();
        private readonly StackPanel _singlePanel = new StackPanel();
        private FTRVirtualChannel[] _pair;
        private FTRMultiChannelBase _multiChannel;
        private readonly MPAProtocolConfiguration _stationProtocol =
            new MPAProtocolConfiguration { Id = "manual-modbus-rtu",
                Kind = MPAProtocolKind.ModbusRtuDraft };
        private readonly MPALengthFrameProtocol _protocol = new MPALengthFrameProtocol();
        private FTRChannelKind _selectedKind = FTRChannelKind.Virtual;
        private string _lastDiagnostic = "";
        public string WorkspaceId { get { return "manual.channel"; } }
        public Guid ManualSessionId { get; private set; }
        public int FrameCount { get { return _frames.Count; } }
        public int PeerCount { get { return _peers.Count; } }
        public int StationCount { get { return _stations.Count; } }
        public FTRSubChannelAddress SelectedPeerAddress
        { get { return (_peerList.SelectedItem as TCTPeerRow)?.Address; } }
        public FTRChannelState MultiChannelState
        { get { return _multiChannel == null ? FTRChannelState.Closed : _multiChannel.State; } }
        public string StatusMessage { get { return _state.Text; } }
        public string HealthMessage { get { return _health.Text; } }
        public string LastDiagnostic { get { return _lastDiagnostic; } }
        public FTRChannelKind SelectedChannelKind { get { return _selectedKind; } }
        public FTRChannelTopology SelectedTopology
        {
            get { return FTRChannelConfigurations.Create(_selectedKind).DescribeType().Topology; }
        }
        public bool IsConnected { get { return _pair != null &&
            _pair[0].State == FTRChannelState.Open && _pair[1].State == FTRChannelState.Open; } }
        public event Action StateChanged;
        public IEnumerable<TCTWorkspaceAction> Commands
        {
            get {
                return new[] {
                    new TCTWorkspaceAction("connect", "连接Virtual单通道", ConnectVirtual,
                        () => SelectedTopology == FTRChannelTopology.SinglePeer &&
                            _selectedKind == FTRChannelKind.Virtual && !IsConnected),
                    new TCTWorkspaceAction("disconnect", "断开Virtual通道", Disconnect, () => IsConnected),
                    new TCTWorkspaceAction("send", "发送Virtual数据", Send, () => IsConnected),
                    new TCTWorkspaceAction("channel.validate", "检查正式Channel拓扑", ValidateTopology),
                    new TCTWorkspaceAction("channel.health", "刷新Channel健康快照", RefreshHealth),
                    new TCTWorkspaceAction("channel.register-peer", "登记离线SubChannel",
                        () => { RegisterSubChannel(); }, () => SelectedTopology == FTRChannelTopology.MultiPeer),
                    new TCTWorkspaceAction("channel.remove-peer", "移除所选SubChannel",
                        RemoveSelectedSubChannel, () => SelectedTopology == FTRChannelTopology.MultiPeer &&
                            _peerList.SelectedItem != null),
                    new TCTWorkspaceAction("channel.add-station", "添加协议点表站号",
                        AddStation, () => SelectedTopology == FTRChannelTopology.SharedBus),
                    new TCTWorkspaceAction("channel.remove-station", "移除所选协议站号",
                        RemoveSelectedStation, () => SelectedTopology == FTRChannelTopology.SharedBus &&
                            _stationList.SelectedItem != null),
                    new TCTWorkspaceAction("channel.negative", "验证非法操作被拒绝", VerifyInvalidOperation)
                };
            }
        }
        public TCTManualChannelFrame()
        {
            ManualSessionId = Guid.NewGuid();
            // 与FTR唯一Kind注册保持一致，不再维护TestCenter私有六类型白名单。
            _channelKind.ItemsSource = Enum.GetValues(typeof(FTRChannelKind))
                .Cast<FTRChannelKind>().Where(kind => kind != FTRChannelKind.Custom).ToArray();
            DockPanel content = new DockPanel { Margin = new Thickness(13, 8, 13, 8) };
            StackPanel options = new StackPanel();
            options.Children.Add(new TextBlock {
                Text = "Channel 专有测试 / Single · Multi · SharedBus", FontSize = 21,
                FontWeight = FontWeights.SemiBold, Foreground = Brushes.DarkSlateBlue });
            WrapPanel picker = new WrapPanel { Margin = new Thickness(0, 7, 0, 0) };
            picker.Children.Add(new TextBlock { Text = "正式通道类型", FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center });
            picker.Children.Add(_channelKind);
            options.Children.Add(picker);
            options.Children.Add(_topology);
            _singlePanel.Children.Add(new TextBlock {
                Text = "本页仅Virtual执行真实收发；其他类型可检查能力与配置，真实收发须在专项测试或授权设备联机中执行。",
                FontSize = 15, Foreground = Brushes.DarkGoldenrod, TextWrapping = TextWrapping.Wrap });
            _singlePanel.Children.Add(new TextBlock { Text = "发送内容（UTF-8）", FontSize = 16 });
            _singlePanel.Children.Add(_payload);
            options.Children.Add(_singlePanel);

            _multiPanel.Children.Add(new TextBlock {
                Text = "Multi：使用生产FTRSubChannelDirectory离线验证对端身份、代次和失效路由；不打开监听Socket。",
                Foreground = Brushes.DarkGoldenrod, FontSize = 15, TextWrapping = TextWrapping.Wrap });
            WrapPanel peerFields = new WrapPanel { Margin = new Thickness(0, 6, 0, 3) };
            peerFields.Children.Add(new TextBlock { Text = "对端 ID", FontSize = 15, VerticalAlignment = VerticalAlignment.Center });
            peerFields.Children.Add(_peerId);
            peerFields.Children.Add(new TextBlock { Text = "远端地址", FontSize = 15, VerticalAlignment = VerticalAlignment.Center });
            peerFields.Children.Add(_endpoint);
            _multiPanel.Children.Add(peerFields);
            _peerList.ItemsSource = _peers;
            _peerList.DisplayMemberPath = "Label";
            _multiPanel.Children.Add(_peerList);
            options.Children.Add(_multiPanel);

            _busPanel.Children.Add(new TextBlock {
                Text = "SharedBus：串口/RS-485共用一条物理链路；站号属于Protocol PointTable，不生成Socket子通道。",
                Foreground = Brushes.DarkGoldenrod, FontSize = 15, TextWrapping = TextWrapping.Wrap });
            WrapPanel stationFields = new WrapPanel { Margin = new Thickness(0, 5, 0, 2) };
            stationFields.Children.Add(new TextBlock { Text = "Modbus站号（1～247）",
                FontSize = 15, VerticalAlignment = VerticalAlignment.Center });
            stationFields.Children.Add(_station);
            _busPanel.Children.Add(stationFields);
            _stationList.ItemsSource = _stations;
            _busPanel.Children.Add(_stationList);
            options.Children.Add(_busPanel);

            options.Children.Add(_state);
            options.Children.Add(new TextBlock {
                Text="F17健康与资源（真实Channel只读快照）",
                FontSize=15,FontWeight=FontWeights.SemiBold,Foreground=Brushes.DarkSlateBlue
            });
            options.Children.Add(_health);
            options.Children.Add(new TextBlock {
                Text = "创建/连接/发送/子通道/站号操作均在右侧操作中心，人工操作不产生自动Passed。",
                Foreground = Brushes.SlateGray, FontSize = 14, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 7) });
            DockPanel.SetDock(options, Dock.Top);
            content.Children.Add(options);

            DataGrid table = new DataGrid { ItemsSource = _frames, FontSize = 15,
                AutoGenerateColumns = false, IsReadOnly = true, MinHeight = 95,
                CanUserAddRows = false, GridLinesVisibility = DataGridGridLinesVisibility.None,
                AlternatingRowBackground = Brushes.AliceBlue };
            table.Columns.Add(new DataGridTextColumn { Header = "UTC", Binding =
                new System.Windows.Data.Binding("Timestamp"), Width = new DataGridLength(140) });
            table.Columns.Add(new DataGridTextColumn { Header = "方向", Binding =
                new System.Windows.Data.Binding("Direction"), Width = new DataGridLength(65) });
            table.Columns.Add(new DataGridTextColumn { Header = "通道", Binding =
                new System.Windows.Data.Binding("Channel"), Width = new DataGridLength(190) });
            table.Columns.Add(new DataGridTextColumn { Header = "Raw HEX", Binding =
                new System.Windows.Data.Binding("RawHex"), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
            table.Columns.Add(new DataGridTextColumn { Header = "Decoded / Error", Binding =
                new System.Windows.Data.Binding("Decoded"), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
            content.Children.Add(table);
            Content = content;
            System.Windows.Automation.AutomationProperties.SetAutomationId(this, "TCT.Manual.Channel");
            System.Windows.Automation.AutomationProperties.SetAutomationId(_channelKind, "TCT.Channel.Kind");
            System.Windows.Automation.AutomationProperties.SetAutomationId(_topology, "TCT.Channel.Topology");
            System.Windows.Automation.AutomationProperties.SetAutomationId(_state, "TCT.Channel.Status");
            System.Windows.Automation.AutomationProperties.SetAutomationId(_health, "TCT.Channel.Health");
            System.Windows.Automation.AutomationProperties.SetAutomationId(_peerList, "TCT.Channel.Peers");
            System.Windows.Automation.AutomationProperties.SetAutomationId(_stationList, "TCT.Channel.Stations");
            System.Windows.Automation.AutomationProperties.SetAutomationId(_peerId, "TCT.Channel.PeerId");
            System.Windows.Automation.AutomationProperties.SetAutomationId(_endpoint, "TCT.Channel.Endpoint");
            System.Windows.Automation.AutomationProperties.SetAutomationId(_station, "TCT.Channel.StationId");
            _channelKind.SelectionChanged += (sender, args) => {
                if (_channelKind.SelectedItem != null) SelectChannelKind((FTRChannelKind)_channelKind.SelectedItem);
            };
            _peerList.SelectionChanged += (sender, args) => NotifyState();
            _stationList.SelectionChanged += (sender, args) => NotifyState();
            _channelKind.SelectedItem = FTRChannelKind.Virtual;
            _healthTimer.Tick+=(sender,args)=>RefreshHealth();
            Loaded+=(sender,args)=>{RefreshHealth();_healthTimer.Start();};
            Unloaded+=(sender,args)=>_healthTimer.Stop();
            UpdateTopology();
            RefreshHealth();
        }
        private void NotifyState()
        {
            Action changed = StateChanged;
            if (changed != null) changed();
        }
        private void UpdateTopology()
        {
            FTRChannelTypeDescriptor type = FTRChannelConfigurations.Create(_selectedKind).DescribeType();
            _topology.Text = "正式类型：" + type.TypeId + "    拓扑：" + type.Topology +
                "    I/O：" + type.IO;
            _singlePanel.Visibility = type.Topology == FTRChannelTopology.SinglePeer ?
                Visibility.Visible : Visibility.Collapsed;
            _multiPanel.Visibility = type.Topology == FTRChannelTopology.MultiPeer ?
                Visibility.Visible : Visibility.Collapsed;
            _busPanel.Visibility = type.Topology == FTRChannelTopology.SharedBus ?
                Visibility.Visible : Visibility.Collapsed;
            UpdateState();
        }
        public void SelectChannelKind(FTRChannelKind kind)
        {
            if (kind == FTRChannelKind.Custom || !Enum.IsDefined(typeof(FTRChannelKind), kind))
                throw new ArgumentOutOfRangeException("kind", "自定义通道需要已注册的专业TypeDescriptor");
            if (kind == _selectedKind && _multiChannel != null &&
                kind != FTRChannelKind.Virtual) { UpdateTopology(); return; }
            if (IsConnected) Disconnect();
            _selectedKind = kind;
            if (!object.Equals(_channelKind.SelectedItem, kind)) _channelKind.SelectedItem = kind;
            // 直接实例化已有正式MultiPeer Channel；只用其生产目录，不执行OpenAsync，
            // 因而不会在测试界面无授权时创建监听Socket或冒充设备连接。
            FTRChannelConfiguration configuration = FTRChannelConfigurations.Create(kind);
            FTRMultiChannelBase next = null;
            if(configuration.DescribeType().Topology == FTRChannelTopology.MultiPeer)
            {
                // 仅创建，不Open；真实Factory是唯一归属，适用于所有已注册MultiPeer实现。
                IFTRChannelTransport channel = new FTRChannelFactory().Create(configuration);
                next = channel as FTRMultiChannelBase;
                if(next == null)
                {
                    channel.Dispose();
                    throw new NotSupportedException("正式MultiPeer实现尚未支持离线SubChannel目录");
                }
            }
            if(_multiChannel != null)_multiChannel.Dispose();
            _multiChannel = next;
            _peers.Clear();
            _stationProtocol.Points.Clear();
            _stations.Clear();
            _lastDiagnostic = "";
            UpdateTopology();
        }
        public void ValidateTopology()
        {
            try
            {
                FTRChannelConfiguration config = FTRChannelConfigurations.Create(_selectedKind);
                config.Validate();
                FTRChannelTypeDescriptor descriptor = config.DescribeType();
                _lastDiagnostic = "正式类型校验通过：" + descriptor.TypeId + " / " +
                    descriptor.Topology + " / " + descriptor.IO + "；不代表设备联机";
            }
            catch (Exception ex) { _lastDiagnostic = "类型校验失败：" + ex.Message; }
            UpdateState();
        }
        public void SetPeerInput(string peerId, string remoteEndpoint)
        { _peerId.Text = peerId ?? ""; _endpoint.Text = remoteEndpoint ?? ""; }
        public void SetStationInput(string station) { _station.Text = station ?? ""; }
        public FTRSubChannelAddress RegisterSubChannel()
        {
            if (SelectedTopology != FTRChannelTopology.MultiPeer || _multiChannel == null)
                throw new InvalidOperationException("只有已创建的真实MultiPeer Channel支持子通道登记");
            try
            {
                FTRSubChannelInfo peer = _multiChannel.RegisterPeer(_peerId.Text, _endpoint.Text);
                _peers.Add(new TCTPeerRow { Address = peer.Address,
                    Label = peer.Address + "  →  " + peer.RemoteEndpoint });
                _peerList.SelectedIndex = _peers.Count - 1;
                _lastDiagnostic = "正式目录已登记对端：" + peer.Address +
                    "（仅离线路由验证，未建立Socket）";
                UpdateState();
                return peer.Address;
            }
            catch (Exception ex)
            {
                _lastDiagnostic = "子通道登记被拒绝：" + ex.Message;
                UpdateState();
                throw;
            }
        }
        public void RemoveSelectedSubChannel()
        {
            TCTPeerRow selected = _peerList.SelectedItem as TCTPeerRow;
            if (selected == null || _multiChannel == null ||
                !_multiChannel.RemovePeer(selected.Address))
                throw new InvalidOperationException("没有可移除的存活SubChannel");
            _peers.Remove(selected);
            _lastDiagnostic = "已移除对端；旧代次路由失效：" + selected.Address;
            UpdateState();
        }
        public void SelectSubChannel(FTRSubChannelAddress address)
        {
            TCTPeerRow item = _peers.FirstOrDefault(x => x.Address.Equals(address));
            _peerList.SelectedItem = item;
        }
        public bool IsLiveSubChannel(FTRSubChannelAddress address)
        { return _multiChannel != null && _multiChannel.FindSubChannel(address) != null; }
        public void AddStation()
        {
            if (SelectedTopology != FTRChannelTopology.SharedBus)
                throw new InvalidOperationException("只有SharedBus使用协议站号点表");
            string station = _station.Text.Trim();
            if (_stationProtocol.Points.Any(x => x.StationAddress == station))
                throw new InvalidOperationException("同一规约站号不能重复");
            MPAPointDefinition point = new MPAPointDefinition {
                Name = "station" + station, Address = "holding:100",
                StationAddress = station, ValueType = "UInt16" };
            _stationProtocol.Points.Add(point);
            try { _stationProtocol.Validate(); }
            catch (Exception ex)
            {
                _stationProtocol.Points.Remove(point);
                _lastDiagnostic = "规约站号被拒绝：" + ex.Message;
                UpdateState();
                throw;
            }
            _stations.Add("Station " + station + " / " + point.Address);
            _stationList.SelectedIndex = _stations.Count - 1;
            _lastDiagnostic = "站号 " + station + " 已加入正式Protocol.Points（未开启串口）";
            UpdateState();
        }
        public void RemoveSelectedStation()
        {
            string selected = _stationList.SelectedItem as string;
            if (selected == null) throw new InvalidOperationException("请先选择站号");
            string station = selected.Substring("Station ".Length).Split('/')[0].Trim();
            _stationProtocol.Points.RemoveAll(x => x.StationAddress == station);
            _stationProtocol.Validate();
            _stations.Remove(selected);
            _lastDiagnostic = "站号 " + station + " 已从正式Protocol.Points移除";
            UpdateState();
        }
        public void VerifyInvalidOperation()
        {
            try
            {
                if (SelectedTopology == FTRChannelTopology.SinglePeer)
                {
                    FTRVirtualChannel[] closed = FTRVirtualChannel.CreatePair("RejectTX", "RejectRX");
                    try { closed[0].Send(new byte[] { 1 }); }
                    catch (InvalidOperationException)
                    { _lastDiagnostic = "已验证：Closed Channel拒绝Send"; UpdateState(); return; }
                }
                else if (SelectedTopology == FTRChannelTopology.MultiPeer)
                {
                    FTRMultiChannelBase channel = _multiChannel;
                    if (channel == null) throw new InvalidOperationException("没有已构造的正式MultiPeer Channel");
                    FTRSubChannelInfo peer = channel.RegisterPeer("rejection-peer", "offline");
                    channel.RemovePeer(peer.Address);
                    if (channel.FindSubChannel(peer.Address) == null)
                    { _lastDiagnostic = "已验证：离线SubChannel旧代次不能重新寻址"; UpdateState(); return; }
                }
                else
                {
                    MPAPointDefinition point = new MPAPointDefinition {
                        Name = "reject-station", Address = "holding:100", StationAddress = "248" };
                    _stationProtocol.Points.Add(point);
                    try { _stationProtocol.Validate(); }
                    catch (ArgumentException)
                    { _lastDiagnostic = "已验证：Protocol拒绝RS-485无效站号248"; UpdateState(); return; }
                    finally { _stationProtocol.Points.Remove(point); }
                }
                throw new InvalidOperationException("正式Owner没有拒绝非法请求");
            }
            catch (Exception ex)
            {
                _lastDiagnostic = "反证失败：" + ex.Message;
                UpdateState();
                throw;
            }
        }
        private void Record(FTRChannelFrame frame)
        {
            if (_frames.Count >= 2000) _frames.RemoveAt(0);
            _frames.Add(new TCTManualFrameRow {
                Timestamp = frame.TimestampUtc.ToString("HH:mm:ss.fff"),
                Direction = frame.Direction.ToString(),
                Channel = frame.Address==null ? frame.ChannelId : frame.Address.ToString(),
                RawHex = BitConverter.ToString(frame.Bytes).Replace("-", " "),
                Decoded = !string.IsNullOrEmpty(frame.Error) ? "ERROR: " + frame.Error :
                    frame.DecodedPayload == null ? "" : Encoding.UTF8.GetString(frame.DecodedPayload)
            });
        }
        private void ReceiveForProtocol(FTRChannelReceiveContext received)
        {
            foreach (byte[] decoded in _protocol.ProcessReceived(received,
                new MPAProtocolContext("length", ManualSessionId.ToString("N"), received.Address)))
            {
                for (int i = _frames.Count - 1; i >= 0; i--)
                {
                    TCTManualFrameRow old = _frames[i];
                    if (old.Channel != received.Address.ChannelId || old.Direction != "Rx") continue;
                    _frames[i] = new TCTManualFrameRow {
                        Timestamp = old.Timestamp, Direction = old.Direction,
                        Channel = old.Channel, RawHex = old.RawHex,
                        Decoded = Encoding.UTF8.GetString(decoded)
                    };
                    break;
                }
            }
        }
        /// <summary>TestCenter只读取FTR正式状态/速率/队列；真实设备必须经显式授权联机。</summary>
        public void RefreshHealth()
        {
            FTRChannelBase channel=_pair!=null ? (FTRChannelBase)_pair[0] : _multiChannel;
            if(channel==null)
            {
                _health.Text="无活动Channel实例；本页不会隐式打开物理设备。";
                return;
            }
            FTRChannelHealthSnapshot h=channel.Health;
            var d=h.Diagnostics;
            var q=d.ReceiveQueue;
            string failure=d.LastFailure==null ? "无" :
                d.LastFailure.Kind+" / "+d.LastFailure.Stage+" / "+d.LastFailure.Scope;
            _health.Text="State="+h.State+"  Session="+h.SessionId.ToString("N")+
                "  Peers="+h.ActivePeers+
                "  TX="+d.SentBytes+" B  RX="+d.ReceivedBytes+" B"+
                "  TX率="+(d.SendBytesPerSecond/1024.0).ToString("F1")+" KiB/s"+
                "  RX率="+(d.ReceiveBytesPerSecond/1024.0).ToString("F1")+" KiB/s"+
                "  Rejected="+d.RejectedSends+
                (d.PayloadBufferByteBudget>0 ?
                    "  多端在途="+d.BufferedPayloadBytes+"/"+d.PayloadBufferByteBudget+
                    " B  峰值="+d.PeakBufferedPayloadBytes+
                    " B  预算拒绝="+d.RejectedPayloadBuffers : "")+
                (q==null ? "" : "  Pool="+q.InUseBytes+"/"+((long)q.BufferSize*q.Capacity)+" B"+
                    "  Backpressure="+q.BackpressureWaits)+
                "\n最近故障："+failure+
                (h.State==FTRChannelState.Closed ? "（离线状态不代表设备可用）" : "");
        }

        private void UpdateState()
        {
            _state.Text = "ManualSession " + ManualSessionId.ToString("D") +
                " · " + (IsConnected ? "Open (Virtual)" : "Closed / 不连接实际设备") +
                " · Frames " + _frames.Count +
                " · SubChannel " + _peers.Count +
                " · MultiOwner " + (_multiChannel == null ? "N/A" : _multiChannel.State.ToString()) +
                " · Protocol Station " + _stations.Count +
                (string.IsNullOrEmpty(_lastDiagnostic) ? "" : "\n" + _lastDiagnostic);
            RefreshHealth();
            NotifyState();
        }
        public void ConnectVirtual()
        {
            if (_selectedKind != FTRChannelKind.Virtual)
                throw new NotSupportedException("当前模式未授权打开真实Socket/串口；请使用Virtual");
            if (IsConnected) return;
            Disconnect();
            _pair = FTRVirtualChannel.CreatePair("Manual-TX", "Manual-RX");
            _pair[0].FrameRecorded += Record;
            _pair[1].FrameRecorded += Record;
            _pair[1].Received += ReceiveForProtocol;
            _protocol.Reset(new FTRSubChannelAddress(_pair[1].Id));
            try { _pair[0].Open(); _pair[1].Open(); }
            catch { Disconnect(); throw; }
            _lastDiagnostic = "正式Virtual两端已打开";
            UpdateState();
        }
        public void Send()
        {
            if (!IsConnected) throw new InvalidOperationException("Virtual Channel尚未连接");
            byte[] bytes = Encoding.UTF8.GetBytes(_payload.Text ?? "");
            if (bytes.Length < 1 || bytes.Length > 4096)
                throw new ArgumentException("Payload must be 1..4096 bytes");
            try
            {
                byte[] encoded = _protocol.PrepareSend(bytes, null);
                for (int start = 0; start < encoded.Length; start += 3)
                {
                    int size = Math.Min(3, encoded.Length - start);
                    byte[] chunk = new byte[size];
                    Buffer.BlockCopy(encoded, start, chunk, 0, size);
                    _pair[0].Send(chunk);
                }
            }
            finally { UpdateState(); }
        }
        public void Disconnect()
        {
            if (_pair != null)
            {
                _pair[0].Close(); _pair[1].Close();
                _pair[0].FrameRecorded -= Record;
                _pair[1].FrameRecorded -= Record;
                _pair[1].Received -= ReceiveForProtocol;
                _pair = null;
            }
            UpdateState();
        }
        public void OnWorkspaceClosed() { _healthTimer.Stop(); Disconnect(); }
        private sealed class TCTPeerRow
        {
            public FTRSubChannelAddress Address { get; set; }
            public string Label { get; set; }
        }
        private sealed class TCTManualFrameRow
        {
            public string Timestamp { get; set; }
            public string Direction { get; set; }
            public string Channel { get; set; }
            public string RawHex { get; set; }
            public string Decoded { get; set; }
        }
    }
}
