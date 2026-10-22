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
    public enum TCTWorkspaceNodeKind { Root, Group, Leaf }
    public enum TCTWorkspaceOpenPolicy { SingleInstance, PerSession, MultiInstance }

    public sealed class TCTWorkspaceDescriptor
    {
        public string WorkspaceId { get; private set; }
        public string ParentId { get; private set; }
        public string Title { get; private set; }
        public string Description { get; private set; }
        public TCTWorkspaceNodeKind Kind { get; private set; }
        public TCTWorkspaceOpenPolicy OpenPolicy { get; private set; }
        public Func<UserControl> Factory { get; private set; }
        public TCTWorkspaceDescriptor(string id, string parent, string title, string description,
            TCTWorkspaceNodeKind kind, Func<UserControl> factory = null,
            TCTWorkspaceOpenPolicy openPolicy = TCTWorkspaceOpenPolicy.SingleInstance)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("WorkspaceId and Title are required");
            WorkspaceId = id; ParentId = parent; Title = title; Description = description ?? "";
            Kind = kind; Factory = factory; OpenPolicy = openPolicy;
        }
    }

    /// <summary>TestCenter UI导航注册表。不是MEM工程树，也不复制TCT Catalog或Run状态。</summary>
    public sealed class TCTWorkspaceRegistry
    {
        private readonly Dictionary<string, TCTWorkspaceDescriptor> _items =
            new Dictionary<string, TCTWorkspaceDescriptor>(StringComparer.Ordinal);
        public IReadOnlyList<TCTWorkspaceDescriptor> Items { get { return _items.Values.ToArray(); } }
        public TCTWorkspaceDescriptor Find(string id)
        {
            TCTWorkspaceDescriptor item;
            return id != null && _items.TryGetValue(id, out item) ? item : null;
        }
        public void Register(TCTWorkspaceDescriptor item)
        {
            if (item == null) throw new ArgumentNullException("item");
            if (_items.ContainsKey(item.WorkspaceId))
                throw new InvalidOperationException("Duplicate WorkspaceId: " + item.WorkspaceId);
            _items.Add(item.WorkspaceId, item);
        }
        public void Validate()
        {
            foreach (TCTWorkspaceDescriptor entry in _items.Values)
            {
                if (entry.Kind == TCTWorkspaceNodeKind.Root && !string.IsNullOrEmpty(entry.ParentId))
                    throw new InvalidOperationException("Root cannot have parent: " + entry.WorkspaceId);
                if (entry.Kind != TCTWorkspaceNodeKind.Root &&
                    (string.IsNullOrEmpty(entry.ParentId) || !_items.ContainsKey(entry.ParentId)))
                    throw new InvalidOperationException("Missing workspace parent: " + entry.WorkspaceId);
                HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
                TCTWorkspaceDescriptor cursor = entry;
                while (cursor != null)
                {
                    if (!visited.Add(cursor.WorkspaceId))
                        throw new InvalidOperationException("Workspace parent cycle: " + entry.WorkspaceId);
                    cursor = Find(cursor.ParentId);
                }
            }
        }
        public IEnumerable<TCTWorkspaceDescriptor> Children(string parentId)
        {
            return _items.Values.Where(x => string.Equals(x.ParentId, parentId, StringComparison.Ordinal));
        }
        public static TCTWorkspaceRegistry CreateDefaults(Func<string, UserControl> rootFactory)
        {
            if (rootFactory == null) throw new ArgumentNullException("rootFactory");
            TCTWorkspaceRegistry registry = new TCTWorkspaceRegistry();
            registry.Register(new TCTWorkspaceDescriptor("manual.root", null, "专有测试（人工操作）",
                "真实人工操作的Channel、Protocol、Graphics与CT/3D工具",
                TCTWorkspaceNodeKind.Root, () => rootFactory("manual.root")));
            registry.Register(new TCTWorkspaceDescriptor("auto.root", null, "自动测试（NUnit/TCT）",
                "统一Catalog、Run、Attempt与Owner证据",
                TCTWorkspaceNodeKind.Root, () => rootFactory("auto.root")));
            registry.Register(new TCTWorkspaceDescriptor("scenes.root", null, "场景案例",
                "经真实工程及Owner核验的场景目录，尚未注册具体场景",
                TCTWorkspaceNodeKind.Root, () => rootFactory("scenes.root")));
            registry.Register(new TCTWorkspaceDescriptor("reports.root", null, "结果报告",
                "自动Run报告、人工操作记录与历史索引分离",
                TCTWorkspaceNodeKind.Root, () => rootFactory("reports.root")));
            registry.Register(new TCTWorkspaceDescriptor("manual.channel", "manual.root", "通道测试",
                "现有生产MCCVirtualChannel的可操作点对点收发，不是真TCP",
                TCTWorkspaceNodeKind.Leaf, () => new TCTManualChannelFrame()));
            registry.Register(new TCTWorkspaceDescriptor("manual.protocol", "manual.root", "规约测试",
                "人工构帧及异常注入：待接入", TCTWorkspaceNodeKind.Leaf));
            registry.Register(new TCTWorkspaceDescriptor("manual.graphics", "manual.root", "图元绘制测试",
                "专用可编辑画布：待接入", TCTWorkspaceNodeKind.Leaf));
            registry.Register(new TCTWorkspaceDescriptor("manual.ct3d", "manual.root", "CT/3D测试",
                "体数据专业Owner未授权接入：待接入", TCTWorkspaceNodeKind.Leaf));
            registry.Register(new TCTWorkspaceDescriptor("reports.auto", "reports.root", "自动测试报告",
                "入口已注册；真实Run与导出由既有Results/History提供", TCTWorkspaceNodeKind.Leaf));
            registry.Register(new TCTWorkspaceDescriptor("reports.manual", "reports.root", "人工测试报告",
                "ManualSession报告尚未接入", TCTWorkspaceNodeKind.Leaf));
            registry.Validate();
            return registry;
        }
    }

    public sealed class TCTWorkspaceAction
    {
        public string Id { get; private set; }
        public string Label { get; private set; }
        public Action Execute { get; private set; }
        public Func<bool> CanExecute { get; private set; }
        public TCTWorkspaceAction(string id, string label, Action execute, Func<bool> canExecute = null)
        {
            Id = id; Label = label; Execute = execute;
            CanExecute = canExecute ?? (() => true);
        }
    }

    public interface ITCTWorkspaceFrame
    {
        string WorkspaceId { get; }
        IEnumerable<TCTWorkspaceAction> Commands { get; }
        event Action StateChanged;
        void OnWorkspaceClosed();
    }

    /// <summary>仅根据注册条目、真实Catalog计数呈现根页；空态不冒充场景和结果。</summary>
    public sealed class TCTWorkspaceRootFrame : UserControl, ITCTWorkspaceFrame
    {
        private readonly TCTWorkspaceDescriptor _descriptor;
        public string WorkspaceId { get { return _descriptor.WorkspaceId; } }
        public IEnumerable<TCTWorkspaceAction> Commands { get { return new TCTWorkspaceAction[0]; } }
        public event Action StateChanged { add { } remove { } }
        public event Action<string> OpenRequested;
        public TCTWorkspaceRootFrame(TCTWorkspaceRegistry registry, string id, int catalogCount)
        {
            _descriptor = registry.Find(id);
            if (_descriptor == null) throw new ArgumentException("Unknown workspace");
            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = Brushes.White };
            StackPanel body = new StackPanel { Margin = new Thickness(22) };
            body.Children.Add(new TextBlock { Text = _descriptor.Title, FontSize = 21,
                Foreground = Brushes.DarkSlateBlue, FontWeight = FontWeights.SemiBold });
            body.Children.Add(new TextBlock { Text = _descriptor.Description,
                Foreground = Brushes.SlateGray, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 18) });
            if (id == "auto.root")
                body.Children.Add(new TextBlock { Text = "当前真实Catalog用例：" + catalogCount +
                    "（此数字不代表正式规格覆盖率）", Margin = new Thickness(0, 0, 0, 15) });
            TCTWorkspaceDescriptor[] children = registry.Children(id).ToArray();
            if (children.Length == 0)
                body.Children.Add(new TextBlock { Text = "尚无已登记的子工作区；不会生成虚假场景或结果。",
                    Foreground = Brushes.DarkGoldenrod, TextWrapping = TextWrapping.Wrap });
            foreach (TCTWorkspaceDescriptor child in children)
            {
                bool supported = child.Factory != null;
                Button open = new Button { HorizontalContentAlignment = HorizontalAlignment.Left,
                    Background = Brushes.White, BorderBrush = Brushes.LightSteelBlue,
                    Padding = new Thickness(10, 9, 10, 9), Margin = new Thickness(0, 0, 0, 8),
                    IsEnabled = supported,
                    Content = child.Title + "   ·   " + (supported ? "打开工作区" : "待接入") +
                        "\n" + child.Description };
                System.Windows.Automation.AutomationProperties.SetAutomationId(open,
                    "TCT.Workspace.Entry." + child.WorkspaceId);
                open.Click += (sender, args) => {
                    if (OpenRequested != null) OpenRequested(child.WorkspaceId);
                };
                body.Children.Add(open);
            }
            scroll.Content = body; Content = scroll;
            System.Windows.Automation.AutomationProperties.SetAutomationId(this,
                "TCT.Workspace.Root." + id);
        }
        public void OnWorkspaceClosed() { }
    }

    /// <summary>人工实验台最小真实闭环。只调用生产Virtual Owner；绝不生成TCT Passed。</summary>
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
        private readonly MPALengthFrameCodec _codec = new MPALengthFrameCodec();
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
                Text = "模式：Virtual P2P · MCCVirtualChannel + MPALengthFrameCodec；不是TCP/UDP/串口设备。",
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
            try { _pair[0].Send(_codec.Encode(bytes), Guid.NewGuid().ToString("N"), 3); }
            finally { UpdateState(); }
        }
        public void Disconnect()
        {
            if (_pair != null)
            {
                _pair[0].Close(); _pair[1].Close();
                _pair[0].FrameRecorded -= Record;
                _pair[1].FrameRecorded -= Record;
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
