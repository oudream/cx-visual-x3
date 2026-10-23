using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Collections.ObjectModel;
using System.Text;
using CxVisual.Mechanism.Connectivity;

namespace CxVisual.TestCenter.UI
{
    public sealed class TCTManualChannelFrame : UserControl, ITCTWorkspaceFrame
    {
        private readonly ObservableCollection<TCTManualFrameRow> _frames =
            new ObservableCollection<TCTManualFrameRow>();
        private readonly TextBox _payload = new TextBox {
            Text = "Hello CxVisual", Margin = new Thickness(0, 5, 0, 8), MinHeight = 58,
            AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
        private readonly TextBlock _state = new TextBlock { Foreground = Brushes.SteelBlue,
            Margin = new Thickness(0, 8, 0, 8) };
        private MCCVirtualChannel[] _pair;
        private readonly MPALengthFrameProtocol _protocol = new MPALengthFrameProtocol();
        public string WorkspaceId { get { return "manual.channel"; } }
        public Guid ManualSessionId { get; private set; }
        public int FrameCount { get { return _frames.Count; } }
        public bool IsConnected { get { return _pair != null &&
            _pair[0].State == MCCChannelState.Open && _pair[1].State == MCCChannelState.Open; } }
        public event Action StateChanged;
        public IEnumerable<TCTWorkspaceAction> Commands
        {
            get {
                return new[] {
                    new TCTWorkspaceAction("connect", "连接 Virtual", ConnectVirtual, () => !IsConnected),
                    new TCTWorkspaceAction("disconnect", "断开", Disconnect, () => IsConnected),
                    new TCTWorkspaceAction("send", "发送数据", Send, () => IsConnected)
                };
            }
        }
        public TCTManualChannelFrame()
        {
            ManualSessionId = Guid.NewGuid();
            DockPanel content = new DockPanel { Margin = new Thickness(18) };
            StackPanel options = new StackPanel();
            options.Children.Add(new TextBlock { Text = "通道测试 / Channel Manual Lab", FontSize = 21,
                FontWeight = FontWeights.SemiBold, Foreground = Brushes.DarkSlateBlue });
            options.Children.Add(new TextBlock {
                Text = "模式：Virtual P2P · MCCVirtualChannel + MPALengthFrameProtocol；不是TCP/UDP/串口设备。",
                Foreground = Brushes.DarkGoldenrod, Margin = new Thickness(0, 9, 0, 7),
                TextWrapping = TextWrapping.Wrap });
            options.Children.Add(new TextBlock { Text = "发送内容（UTF-8）：" });
            options.Children.Add(_payload);
            options.Children.Add(_state);
            options.Children.Add(new TextBlock {
                Text = "连接 / 断开 / 发送只在右侧操作中心；人工操作不产生 Passed。",
                Foreground = Brushes.SlateGray, Margin = new Thickness(0, 0, 0, 13) });
            DockPanel.SetDock(options, Dock.Top); content.Children.Add(options);
            DataGrid table = new DataGrid { ItemsSource = _frames,
                AutoGenerateColumns = false, IsReadOnly = true,
                CanUserAddRows = false, GridLinesVisibility = DataGridGridLinesVisibility.None,
                AlternatingRowBackground = Brushes.AliceBlue };
            table.Columns.Add(new DataGridTextColumn { Header = "UTC", Binding =
                new System.Windows.Data.Binding("Timestamp"), Width = new DataGridLength(150) });
            table.Columns.Add(new DataGridTextColumn { Header = "方向", Binding =
                new System.Windows.Data.Binding("Direction"), Width = new DataGridLength(75) });
            table.Columns.Add(new DataGridTextColumn { Header = "通道", Binding =
                new System.Windows.Data.Binding("Channel"), Width = new DataGridLength(85) });
            table.Columns.Add(new DataGridTextColumn { Header = "Raw HEX", Binding =
                new System.Windows.Data.Binding("RawHex"), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
            table.Columns.Add(new DataGridTextColumn { Header = "Decoded / Error", Binding =
                new System.Windows.Data.Binding("Decoded"), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
            content.Children.Add(table);
            Content = content; UpdateState();
            System.Windows.Automation.AutomationProperties.SetAutomationId(this, "TCT.Manual.Channel");
        }
        private void Record(MCCChannelFrame frame)
        {
            if (_frames.Count >= 2000) _frames.RemoveAt(0);
            _frames.Add(new TCTManualFrameRow {
                Timestamp = frame.TimestampUtc.ToString("HH:mm:ss.fff"),
                Direction = frame.Direction.ToString(), Channel = frame.ChannelId,
                RawHex = BitConverter.ToString(frame.Bytes).Replace("-", " "),
                Decoded = !string.IsNullOrEmpty(frame.Error) ? "ERROR: " + frame.Error :
                    frame.DecodedPayload == null ? "" : Encoding.UTF8.GetString(frame.DecodedPayload)
            });
        }
        // Channel 先记录原始帧，再由 Protocol 完成解帧，并更新显示行。
        private void ReceiveForProtocol(MCCChannelReceiveContext received)
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
        private void UpdateState()
        {
            _state.Text = "ManualSessionId: " + ManualSessionId.ToString("D") +
                " · " + (IsConnected ? "Open (Virtual)" : "Closed") +
                " · 帧记录 " + _frames.Count;
            if (StateChanged != null) StateChanged();
        }
        public void ConnectVirtual()
        {
            if (IsConnected) return;
            Disconnect();
            _pair = MCCVirtualChannel.CreatePair("Manual-TX", "Manual-RX");
            _pair[0].FrameRecorded += Record;
            _pair[1].FrameRecorded += Record;
            _pair[1].Received += ReceiveForProtocol;
            _protocol.Reset(new MCCSubChannelAddress(_pair[1].Id));
            try { _pair[0].Open(); _pair[1].Open(); }
            catch { Disconnect(); throw; }
            UpdateState();
        }
        public void Send()
        {
            if (!IsConnected) throw new InvalidOperationException("Virtual Channel is not connected");
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
        public void OnWorkspaceClosed() { Disconnect(); }
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
