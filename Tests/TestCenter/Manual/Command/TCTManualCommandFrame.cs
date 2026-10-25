using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using CxVisual.Foundation.Data.Commands;
using CxVisual.Mechanism.Data.Variables;
using CxVisual.Mechanism.Runtime.Commands;
using Microsoft.Win32;

namespace CxVisual.TestCenter.UI
{
    /// <summary>
    /// TestCenter人工命令工作区。命令定义、执行、MDS变量均来自正式Owner；
    /// 演示Handler明确标为本地算法，不声称真实设备、远程发现或TCT通过。
    /// </summary>
    public sealed class TCTManualCommandFrame : UserControl, ITCTWorkspaceFrame
    {
        private sealed class HistoryRow
        {
            public string Time { get; set; }
            public string Action { get; set; }
            public string OperationId { get; set; }
            public string Status { get; set; }
            public string Detail { get; set; }
        }

        private readonly MRKCommandCenter _local = new MRKCommandCenter();
        private readonly MRKCommandDescriptor _childContract = PublishedChildContract();
        private Process _childHost;
        private readonly MDSVariableTable _variables = new MDSVariableTable("TestCenter.Command.Manual");
        private readonly MRKCommandMdsBinding _binding = new MRKCommandMdsBinding();
        private readonly List<IDisposable> _registrations = new List<IDisposable>();
        private readonly ObservableCollection<HistoryRow> _history = new ObservableCollection<HistoryRow>();
        private readonly Dictionary<string, TextBox> _editors = new Dictionary<string, TextBox>();
        private readonly Dictionary<string, ComboBox> _sources = new Dictionary<string, ComboBox>();
        private readonly TreeView _commands = new TreeView { MinWidth = 210, BorderThickness = new Thickness(0),
            Background = Brushes.White };
        private readonly Grid _parameterGrid = new Grid { Margin = new Thickness(7) };
        private readonly TextBlock _descriptor = new TextBlock { TextWrapping = TextWrapping.Wrap,
            FontSize = 14, Foreground = Brushes.SlateGray };
        private readonly TextBlock _status = new TextBlock { TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.SteelBlue, FontSize = 14, Margin = new Thickness(5) };
        private readonly TextBlock _operation = new TextBlock { Text = "尚无Operation",
            FontSize = 14, TextWrapping = TextWrapping.Wrap };
        private readonly TextBlock _results = new TextBlock { Text = "没有执行结果",
            FontSize = 14, TextWrapping = TextWrapping.Wrap };
        private readonly TextBlock _progress = new TextBlock { Text = "进度尚未开始",
            Foreground = Brushes.SlateGray };
        private readonly CheckBox _writeBack = new CheckBox { Content = "将 Output 映射到真实MDS变量 LastOutput",
            IsChecked = true, Margin = new Thickness(4, 9, 4, 9) };
        private readonly ComboBox _transport = new ComboBox { Width = 240,
            Margin = new Thickness(3), Height = 29 };
        private readonly TextBox _host = Field(".", 170);
        private readonly TextBox _pipe = Field("CxVisual.Command.Host", 260);
        private MRKCommandCenter _remote;
        private string _remoteEndpoint;
        private MRKCommandDescriptor _selected;
        private MRKCommandResponse _last;
        private Guid _activeOperation;
        private bool _operationRemote;
        private string _operationEndpoint;
        private bool _busy;
        public bool IsExecuting { get { return _busy; } }
        private bool _closed;
        public event Action StateChanged;
        public string WorkspaceId { get { return "manual.command"; } }
        public Guid SessionId { get; private set; }
        public int HistoryCount { get { return _history.Count; } }
        public MRKCommandResponse LastResponse { get { return _last; } }
        public string StatusSummary { get { return _status.Text; } }

        public IEnumerable<TCTWorkspaceAction> Commands
        {
            get
            {
                return new[] {
                    new TCTWorkspaceAction("command.register", "注册本地演示命令", RegisterDemo, () => !_closed && _registrations.Count == 0),
                    new TCTWorkspaceAction("command.unregister", "注销本地演示命令", UnregisterDemo, () => !_closed && _registrations.Count > 0),
                    new TCTWorkspaceAction("command.enable", "启用当前命令", () => SetEnabled(true), () => !_closed && _selected != null && !IsRemote),
                    new TCTWorkspaceAction("command.disable", "禁用当前命令", () => SetEnabled(false), () => !_closed && _selected != null && !IsRemote),
                    new TCTWorkspaceAction("command.invoke", "调用选中命令", () => Run(InvokeSelectedAsync), () => !_closed && !_busy && _selected != null),
                    new TCTWorkspaceAction("command.query", "查询 Operation", () => Run(QueryAsync), () => !_closed && _activeOperation != Guid.Empty),
                    new TCTWorkspaceAction("command.pause", "协作暂停", () => Run(PauseAsync), () => !_closed && _activeOperation != Guid.Empty),
                    new TCTWorkspaceAction("command.resume", "恢复运行", () => Run(ResumeAsync), () => !_closed && _activeOperation != Guid.Empty),
                    new TCTWorkspaceAction("command.cancel", "请求取消（非急停）", () => Run(CancelAsync), () => !_closed && _activeOperation != Guid.Empty),
                    new TCTWorkspaceAction("command.export", "导出本次人工会话记录", Export, () => !_closed && _history.Count > 0),
                    new TCTWorkspaceAction("command.host.start", "启动独立Windows IPC测试Host", () => Run(StartChildHostAsync),
                        () => !_closed && (_childHost == null || _childHost.HasExited)),
                    new TCTWorkspaceAction("command.host.stop", "停止独立测试Host", StopChildHost,
                        () => !_closed && _childHost != null && !_childHost.HasExited),
                    new TCTWorkspaceAction("command.refresh", "刷新命令目录", RefreshCatalog, () => !_closed)
                };
            }
        }
        private bool IsRemote { get { return _transport.SelectedIndex == 1; } }

        private static TextBox Field(string value, double width = 220)
        {
            return new TextBox { Text = value, Width = width, Height = 29, Margin = new Thickness(3),
                VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(5, 2, 5, 2) };
        }
        private static TextBlock Caption(string value, double size = 14)
        {
            return new TextBlock { Text = value, FontSize = size, Foreground = Brushes.SlateGray,
                Margin = new Thickness(5, 5, 5, 5), TextWrapping = TextWrapping.Wrap };
        }
        private static StackPanel Row()
        { return new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(1, 3, 1, 3) }; }

        public TCTManualCommandFrame()
        {
            SessionId = Guid.NewGuid();
            _variables.Define(new MDSVariableDefinition("InputSample", typeof(double)));
            _variables.Define(new MDSVariableDefinition("LastOutput", typeof(double)));
            _variables.Update("InputSample", 3.0);
            _variables.Update("LastOutput", 0.0);
            _binding.BindVariable<double>("mds.input", _variables, "InputSample");
            _binding.BindVariable<double>("mds.output", _variables, "LastOutput");
            _transport.Items.Add("本地进程内 · 真实MRK");
            _transport.Items.Add("远程OS NamedPipe · 需真实Host");
            _transport.SelectedIndex = 0;
            _transport.SelectionChanged += (s, e) => Changed();
            BuildView();
            RegisterDemo();
            Show("已注册当前会话的本地算法演示命令。远程模式需要外部Host；本页不启动伪造设备。");
        }

        private void BuildView()
        {
            DockPanel main = new DockPanel { Background = Brushes.White, Margin = new Thickness(12) };
            StackPanel title = new StackPanel { Margin = new Thickness(5, 3, 5, 12) };
            title.Children.Add(new TextBlock { Text = "M12 · 统一命令中心 / Command Center",
                FontSize = 22, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(36, 68, 103)) });
            title.Children.Add(Caption("同一 MRKCommandCenter：浏览真实Descriptor、按FDC Schema配置入参、调用/控制Operation并观察结果；人工会话不等于自动测试PASS。"));
            DockPanel.SetDock(title, Dock.Top); main.Children.Add(title);

            StackPanel footer = new StackPanel();
            footer.Children.Add(_status);
            DockPanel.SetDock(footer, Dock.Bottom); main.Children.Add(footer);

            Grid columns = new Grid();
            columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
            columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Border sidebar = new Border { BorderBrush = new SolidColorBrush(Color.FromRgb(217, 227, 238)),
                BorderThickness = new Thickness(1), Padding = new Thickness(7), Child = _commands };
            AutomationProperties.SetAutomationId(_commands, "TCT.Command.Catalog");
            _commands.SelectedItemChanged += (s, e) => {
                TreeViewItem node = e.NewValue as TreeViewItem;
                if (node != null && node.Tag is MRKCommandDescriptor)
                    SelectCommand((MRKCommandDescriptor)node.Tag);
            };
            Grid.SetColumn(sidebar, 0); columns.Children.Add(sidebar);

            TabControl tabs = new TabControl { Margin = new Thickness(11, 0, 0, 0),
                Background = Brushes.White };
            StackPanel paramsContent = new StackPanel();
            paramsContent.Children.Add(_descriptor);
            paramsContent.Children.Add(Caption("输入来源：直接值 / 本地MDS变量 InputSample。JSON可编辑数组、嵌套命名对象；参数由正式FDC Schema校验。"));
            paramsContent.Children.Add(_parameterGrid);
            paramsContent.Children.Add(_writeBack);
            tabs.Items.Add(Tab("参数编辑与Schema", new ScrollViewer {
                Content = paramsContent, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }));

            StackPanel monitor = new StackPanel { Margin = new Thickness(13) };
            monitor.Children.Add(Caption("OperationId、受理/执行/业务状态彼此独立；Unknown不能作为失败或安全停机证据。"));
            monitor.Children.Add(_operation);
            monitor.Children.Add(_progress);
            monitor.Children.Add(_results);
            tabs.Items.Add(Tab("执行监视", monitor));

            StackPanel ipc = new StackPanel { Margin = new Thickness(13) };
            ipc.Children.Add(Caption("本地模式：测试会话的真实MRK算法Handler；远程模式：连接指定Windows Host的命名管道，不自动建立Host。跨PC身份和安全需另行验证。"));
            StackPanel mode = Row();
            mode.Children.Add(Caption("执行目标"));
            mode.Children.Add(_transport);
            ipc.Children.Add(mode);
            StackPanel address = Row();
            address.Children.Add(Caption("Server")); address.Children.Add(_host);
            address.Children.Add(Caption("Pipe")); address.Children.Add(_pipe);
            ipc.Children.Add(address);
            ipc.Children.Add(Caption("远程定义来自本页已发布的本地演示契约，仅用于调用前编辑校验；本页不宣称已支持远程Schema发现。连接失败/响应丢失显示Unknown，绝不自动重发设备动作。"));
            tabs.Items.Add(Tab("IPC / 安全边界", ipc));

            DataGrid historyGrid = new DataGrid { ItemsSource = _history, IsReadOnly = true,
                AutoGenerateColumns = true, CanUserAddRows = false, FontSize = 13,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal };
            AutomationProperties.SetAutomationId(historyGrid, "TCT.Command.History");
            tabs.Items.Add(Tab("人工会话证据", historyGrid));
            Grid.SetColumn(tabs, 1); columns.Children.Add(tabs);
            main.Children.Add(columns);
            Content = main;
        }
        private static TabItem Tab(string name, object content)
        { return new TabItem { Header = name, Content = content, Padding = new Thickness(12, 7, 12, 7) }; }
        private void Changed()
        { Action handler = StateChanged; if (handler != null) handler(); }
        private void Show(string text) { _status.Text = text; Changed(); }
        private void Log(string action, Guid operationId, string status, string detail)
        {
            if (_closed) return;
            _history.Add(new HistoryRow { Time = DateTime.Now.ToString("HH:mm:ss.fff"),
                Action = action, OperationId = operationId == Guid.Empty ? "" : operationId.ToString("D"),
                Status = status, Detail = detail ?? "" });
            while (_history.Count > 250) _history.RemoveAt(0);
            Changed();
        }

        private static FDCCommandDefinition Definition(long id, string code)
        {
            return new FDCCommandDefinition(id, code, code == "Multiply" ? "倍乘算法" : "协作长任务",
                new[] {
                    new FDCCommandParameterDefinition("Input", typeof(double), true,
                        minValue: -1000.0, maxValue: 1000.0, description: "原始数值"),
                    new FDCCommandParameterDefinition("Factor", typeof(double), false, true, 2.0,
                        description: "倍率"),
                    new FDCCommandParameterDefinition("Samples", typeof(double[]), false,
                        description: "可选数组（JSON）"),
                    new FDCCommandParameterDefinition("Options", typeof(Dictionary<string, object>),
                        false, description: "可选嵌套对象", children: new[] {
                            new FDCCommandParameterDefinition("Label", typeof(string), false),
                            new FDCCommandParameterDefinition("Enabled", typeof(bool), false)
                        })
                },
                new[] { new FDCCommandParameterDefinition("Output", typeof(double), true) },
                description: "TestCenter虚拟算法Provider，无PLC/机械动作", timeoutMilliseconds: 8000);
        }
        private static MRKCommandDescriptor PublishedChildContract()
        {
            // 与现有ArchitectureSmoke.exe --command-pipe-child发布定义一致。
            // 这是本地已知示例Contract，不等于远端Schema发现。
            FDCCommandDefinition definition = new FDCCommandDefinition(5183, "Calculate",
                "独立Windows进程算法测试", new[] {
                    new FDCCommandParameterDefinition("Input", typeof(double), true,
                        minValue: -1000.0, maxValue: 1000.0),
                    new FDCCommandParameterDefinition("Factor", typeof(double), false, true, 2.0)
                }, new[] { new FDCCommandParameterDefinition("Output", typeof(double), true) },
                description: "ArchitectureSmoke正式IPC测试Host；非工厂PLC或真实设备");
            return new MRKCommandDescriptor("child/algorithm", definition);
        }

        /// <summary>启动已有ArchitectureSmoke独立进程Host；请求和结果真正通过OS Pipe。</summary>
        public async Task StartChildHostAsync()
        {
            if (_closed) throw new ObjectDisposedException("TCTManualCommandFrame");
            if (_childHost != null && !_childHost.HasExited)
                throw new InvalidOperationException("独立测试Host已经运行");
            string executable = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "CxVisual.Tests.ArchitectureSmoke.exe");
            if (!File.Exists(executable))
                throw new FileNotFoundException("ArchitectureSmoke未编译，不能伪造独立Host", executable);
            string name = "CxVisual.TCT.Child." + Guid.NewGuid().ToString("N");
            ProcessStartInfo info = new ProcessStartInfo(executable,
                "--command-pipe-child " + name) {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
                RedirectStandardOutput = true, RedirectStandardError = true };
            _childHost = Process.Start(info);
            if (_childHost == null) throw new InvalidOperationException("独立测试Host无法启动");
            try
            {
                Task<string> ready = _childHost.StandardOutput.ReadLineAsync();
                Task signal = await Task.WhenAny(ready, Task.Delay(7000)).ConfigureAwait(true);
                if (signal != ready || await ready.ConfigureAwait(true) != "IPC_CHILD_READY")
                    throw new TimeoutException("独立IPC Host没有按时完成真实管道启动");
                _host.Text = "."; _pipe.Text = name; _transport.SelectedIndex = 1;
                SelectCommand(_childContract);
                Log("IPC.Host.Start", Guid.Empty, "Ready", "独立进程PID=" + _childHost.Id);
                Show("已启动真实独立Windows测试Host PID=" + _childHost.Id +
                    "，选择了已发布示例Schema；不代表远程发现或设备联锁");
            }
            catch
            {
                StopChildHost();
                throw;
            }
        }
        public void StopChildHost()
        {
            Process child = _childHost;
            _childHost = null;
            if (child == null) return;
            int processId = 0;
            try
            {
                processId = child.Id;
                if (!child.HasExited)
                {
                    try { child.StandardInput.WriteLine(); child.StandardInput.Flush(); }
                    catch (IOException) { }
                    if (!child.WaitForExit(3000))
                    {
                        child.Kill();
                        child.WaitForExit(3000);
                    }
                }
                if (!_closed)
                {
                    Log("IPC.Host.Stop", Guid.Empty, "Stopped",
                        "测试Host PID=" + processId + "（未执行设备急停）");
                    Show("独立测试Host已关闭，原远端操作不能继续假设可查询");
                }
            }
            finally { child.Dispose(); Changed(); }
        }
        public void RegisterDemo()
        {
            if (_closed || _registrations.Count > 0) return;
            foreach (FDCCommandDefinition definition in new[] {
                Definition(950001, "Multiply"), Definition(950002, "ProcessSteps") })
            {
                bool longTask = definition.CommandCode == "ProcessSteps";
                MRKCommandDescriptor descriptor = new MRKCommandDescriptor(
                    "TestCenter/Algorithm", definition, MRKCommandKind.Module,
                    new MRKCommandPolicy(2, supportsCancellation: true, supportsPause: true),
                    new[] { "TestCenter", "Algorithm", "Simulated" });
                IDisposable registration = _local.Register(descriptor, new MRKDelegateCommandHandler(
                    async (context, values, cancellation) =>
                    {
                        if (longTask)
                            for (int i = 0; i < 30; i++)
                            {
                                cancellation.ThrowIfCancellationRequested();
                                await context.WaitWhilePausedAsync(cancellation).ConfigureAwait(false);
                                context.ReportProgress((i + 1) * 100.0 / 30, "演示处理");
                                await Task.Delay(60, cancellation).ConfigureAwait(false);
                            }
                        else context.ReportProgress(100, "演示完成");
                        return new MRKCommandHandlerResult(new Dictionary<string, object> {
                            { "Output", (double)values["Input"] * (double)values["Factor"] }
                        });
                    }));
                _registrations.Add(registration);
            }
            RefreshCatalog();
            Log("Register", Guid.Empty, "Registered", "演示Provider（非实际设备）");
        }
        public void UnregisterDemo()
        {
            foreach (IDisposable registration in _registrations) registration.Dispose();
            _registrations.Clear();
            _selected = null;
            RefreshCatalog();
            Log("Unregister", Guid.Empty, "Unregistered", "只撤销测试Provider注册");
        }
        private void RefreshCatalog()
        {
            _commands.Items.Clear();
            MRKCommandDescriptor[] found = _local.Router.Discover().ToArray();
            foreach (IGrouping<string, MRKCommandDescriptor> group in found.GroupBy(d => d.Target))
            {
                TreeViewItem target = new TreeViewItem { Header = group.Key, IsExpanded = true };
                foreach (MRKCommandDescriptor descriptor in group)
                {
                    TreeViewItem child = new TreeViewItem {
                        Header = descriptor.Definition.CommandCode + " · v" +
                            descriptor.Definition.SchemaVersion, Tag = descriptor };
                    target.Items.Add(child);
                }
                _commands.Items.Add(target);
            }
            // 本地已发布的独立IPC测试契约，不加到本地MRK Registry中。
            // 避免把CLI测试Host误认作本地Handler或自动发现的专业设备。
            TreeViewItem remoteGroup = new TreeViewItem {
                Header = "已知测试Host契约（非远程发现）", IsExpanded = true };
            remoteGroup.Items.Add(new TreeViewItem {
                Header = "child/algorithm · Calculate v1", Tag = _childContract });
            _commands.Items.Add(remoteGroup);
            if (found.Length > 0) SelectCommand(found[0]);
            else SelectCommand(_childContract);
            Changed();
        }
        private void SelectCommand(MRKCommandDescriptor descriptor)
        {
            _selected = descriptor;
            FDCCommandDefinition definition = descriptor.Definition;
            _descriptor.Text = "Target: " + descriptor.Target + "    ID: " + definition.CommandId +
                "    Code: " + definition.CommandCode + "    SchemaVersion: " + definition.SchemaVersion +
                "\n" + definition.Description + "\nPolicy: " + descriptor.Policy.Dispatch +
                " / MaxConcurrent=" + descriptor.Policy.MaxConcurrent +
                " / Cancel=" + descriptor.Policy.SupportsCancellation + " / Pause=" +
                descriptor.Policy.SupportsPause + "\nOutputs: " +
                string.Join(", ", definition.Outputs.Select(x => x.Code + ":" + x.DataType.Name));
            _editors.Clear(); _sources.Clear(); _parameterGrid.Children.Clear();
            _parameterGrid.RowDefinitions.Clear();
            _parameterGrid.ColumnDefinitions.Clear();
            _parameterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(155) });
            _parameterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _parameterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(185) });
            int index = 0;
            foreach (FDCCommandParameterDefinition parameter in definition.Inputs)
            {
                _parameterGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                TextBlock info = Caption(parameter.Code + " : " + parameter.DataType.Name +
                    (parameter.Required ? " *" : "") + (parameter.Unit == null ? "" : " (" + parameter.Unit + ")"));
                Grid.SetRow(info, index); Grid.SetColumn(info, 0); _parameterGrid.Children.Add(info);
                string example = parameter.Code == "Input" ? "3" :
                    parameter.Code == "Factor" ? "2" :
                    parameter.Code == "Samples" ? "[1,2,3]" : "{\"Label\":\"Demo\",\"Enabled\":true}";
                TextBox editor = Field(example);
                editor.MinWidth = 155; editor.Width = double.NaN;
                AutomationProperties.SetAutomationId(editor, "TCT.Command.Input." + parameter.Code);
                _editors.Add(parameter.Code, editor);
                Grid.SetRow(editor, index); Grid.SetColumn(editor, 1); _parameterGrid.Children.Add(editor);
                ComboBox source = new ComboBox { Height = 29, Margin = new Thickness(4), MinWidth = 145 };
                source.Items.Add("直接值");
                if (parameter.DataType == typeof(double)) source.Items.Add("MDS:InputSample");
                source.SelectedIndex = 0;
                _sources.Add(parameter.Code, source);
                Grid.SetRow(source, index); Grid.SetColumn(source, 2); _parameterGrid.Children.Add(source);
                index++;
            }
            Changed();
        }

        private static object ConvertValue(object raw, Type requested)
        {
            if (raw == null) return null;
            Type type = Nullable.GetUnderlyingType(requested) ?? requested;
            if (type == typeof(Dictionary<string, object>) ||
                type == typeof(IDictionary<string, object>) ||
                type == typeof(IReadOnlyDictionary<string, object>))
                return raw as Dictionary<string, object> ??
                    throw new ArgumentException("结构化参数必须是JSON命名对象");
            if (type.IsArray)
            {
                object[] items = raw as object[];
                if (items == null) throw new ArgumentException("数组入参必须是JSON数组");
                Type element = type.GetElementType();
                Array values = Array.CreateInstance(element, items.Length);
                for (int i = 0; i < items.Length; i++)
                    values.SetValue(ConvertValue(items[i], element), i);
                return values;
            }
            if (type.IsEnum) return Enum.Parse(type, Convert.ToString(raw, CultureInfo.InvariantCulture), true);
            if (type == typeof(Guid)) return Guid.Parse(Convert.ToString(raw, CultureInfo.InvariantCulture));
            return Convert.ChangeType(raw, type, CultureInfo.InvariantCulture);
        }
        private Dictionary<string, object> BuildInputs(List<MRKCommandParameterBinding> bindings)
        {
            if (_selected == null) throw new InvalidOperationException("没有选中命令");
            Dictionary<string, object> values = new Dictionary<string, object>(StringComparer.Ordinal);
            Dictionary<string, object> inspected = new Dictionary<string, object>(StringComparer.Ordinal);
            JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = 65536, RecursionLimit = 16 };
            foreach (FDCCommandParameterDefinition item in _selected.Definition.Inputs)
            {
                if (_sources[item.Code].SelectedIndex == 1)
                {
                    bindings.Add(new MRKCommandParameterBinding(item.Code, "mds.input"));
                    inspected.Add(item.Code, _variables.Get<double>("InputSample"));
                    continue;
                }
                string text = _editors[item.Code].Text;
                if (string.IsNullOrWhiteSpace(text) && !item.Required) continue;
                object parsed = json.DeserializeObject(text);
                object value = ConvertValue(parsed, item.DataType);
                values.Add(item.Code, value);
                inspected.Add(item.Code, value);
            }
            // 任何非法类型、范围、缺少Required和嵌套结构必须在调用Handler前拒绝。
            _selected.Definition.BindInputs(inspected);
            return values;
        }
        private void SetEnabled(bool enabled)
        {
            if (_selected == null) return;
            bool changed = _local.Registry.SetEnabled(_selected.Target,
                _selected.Definition.CommandCode, enabled);
            Log(enabled ? "Enable" : "Disable", Guid.Empty, changed ? "OK" : "NotFound",
                _selected.Definition.CommandCode);
            // 禁用后命令不会出现在Discover，但当前Descriptor仍可供再次启用。
            // 不销毁选中上下文，否则用户无法通过右侧操作中心恢复。
            if (enabled) RefreshCatalog();
            Show(changed ? (enabled ? "已启用命令" : "已禁用命令；可在右侧重新启用") :
                "命令不存在；不会调用Handler");
        }
        private MRKCommandCenter ResolveCenter()
        {
            if (!IsRemote) return _local;
            if (string.IsNullOrWhiteSpace(_host.Text) || string.IsNullOrWhiteSpace(_pipe.Text))
                throw new ArgumentException("远程Host和Pipe名称不能为空");
            string endpoint = _host.Text + "|" + _pipe.Text;
            if (_remote == null || _remoteEndpoint != endpoint)
            {
                if (_remote != null) _remote.Dispose();
                _remote = new MRKCommandCenter(remoteClient:
                    new MRKCommandPipeClient(_host.Text.Trim(), _pipe.Text.Trim()));
                _remoteEndpoint = endpoint;
            }
            return _remote;
        }
        public async Task<MRKCommandResponse> InvokeSelectedAsync()
        {
            if (_closed) throw new ObjectDisposedException("TCTManualCommandFrame");
            if (_busy) throw new InvalidOperationException("已有命令正在等待应答");
            if (_selected == null) throw new InvalidOperationException("未选择已注册的命令");
            _busy = true; Changed();
            try
            {
                List<MRKCommandParameterBinding> sources = new List<MRKCommandParameterBinding>();
                Dictionary<string, object> inputs = BuildInputs(sources);
                FDCCommandDefinition definition = _selected.Definition;
                MRKCommandCenter center = ResolveCenter();
                Guid id = Guid.NewGuid();
                _activeOperation = id;
                _operationRemote = IsRemote;
                _operationEndpoint = _operationRemote ? _host.Text + "|" + _pipe.Text : null;
                MRKCommandRequest request = new MRKCommandRequest(_selected.Target,
                    definition.CommandCode, inputs, "TestCenter.Manual", id,
                    options: new MRKCommandOptions(remote: IsRemote, timeoutMilliseconds: 7000),
                    expectedSchemaVersion: definition.SchemaVersion,
                    commandId: definition.CommandId);
                List<MRKCommandParameterBinding> outputs = new List<MRKCommandParameterBinding>();
                if (_writeBack.IsChecked == true)
                    outputs.Add(new MRKCommandParameterBinding("Output", "mds.output"));
                Log("Invoke", id, "Requested", "Target=" + request.Target +
                    "; Code=" + request.CommandCode + "; Remote=" + IsRemote);
                MRKCommandBindingResult result = await center.InvokeBoundResultAsync(request,
                    _binding, sources, outputs).ConfigureAwait(true);
                _last = result.Execution;
                DisplayResponse("Invoke", _last, result.MappingAttempted ?
                    (result.MappingSucceeded ? "MDS写回成功" :
                     "MDS写回失败：" + result.MappingErrorCode + "/" + result.MappingError) :
                    "无输出写回");
                return _last;
            }
            catch (Exception error)
            {
                Log("Invoke", _activeOperation, "Rejected/Exception", error.Message);
                Show("参数预检或命令调用失败：" + error.Message);
                throw;
            }
            finally { _busy = false; Changed(); }
        }
        private void DisplayResponse(string action, MRKCommandResponse response, string note = "")
        {
            _last = response;
            _operation.Text = "Operation: " + response.OperationId + "\nRequest: " +
                response.RequestId + "\nStage: " + response.Stage + "    State: " + response.State;
            _results.Text = "业务结果: " + (response.BusinessSuccess.HasValue ?
                response.BusinessSuccess.Value.ToString() : "未确认") +
                "\n错误: " + response.ErrorCode + " " + response.Error + "\n返回值: " +
                string.Join(", ", response.Outputs.Select(x => x.Key + "=" +
                    Convert.ToString(x.Value, CultureInfo.InvariantCulture))) +
                "\n" + note + "  Duration=" + response.DurationMilliseconds.ToString("F0") + "ms";
            _progress.Text = "当前状态：" + response.State;
            Log(action, response.OperationId, response.Stage + "/" + response.State,
                response.ErrorCode + " " + note);
            Show(action + "：" + response.State + "（业务与设备安全结果分别确认）");
        }
        private void VerifyOperationEndpoint()
        {
            if (_activeOperation == Guid.Empty)
                throw new InvalidOperationException("尚无命令Operation");
            if (IsRemote != _operationRemote ||
                (_operationRemote && _operationEndpoint != _host.Text + "|" + _pipe.Text))
                throw new InvalidOperationException(
                    "请切回原执行模式与Host/Pipe后查询或控制Operation；禁止跨Host误操作");
        }
        public async Task<MRKCommandResponse> QueryAsync()
        {
            VerifyOperationEndpoint();
            MRKCommandResponse reply = IsRemote ?
                await ResolveCenter().QueryRemoteAsync(_activeOperation).ConfigureAwait(true) :
                _local.Query(_activeOperation);
            if (reply == null) throw new KeyNotFoundException("Operation不存在或已离开本地账本");
            DisplayResponse("Query", reply);
            if (!IsRemote)
            {
                MRKCommandMetrics metrics = _local.GetMetrics();
                _progress.Text = "统计：Running=" + metrics.Running +
                    " / Completed=" + metrics.Completed + " / Cancelled=" + metrics.Cancelled;
            }
            return reply;
        }
        public async Task<MRKCommandResponse> PauseAsync()
        { return await ControlAsync("Pause").ConfigureAwait(true); }
        public async Task<MRKCommandResponse> ResumeAsync()
        { return await ControlAsync("Resume").ConfigureAwait(true); }
        public async Task<MRKCommandResponse> CancelAsync()
        { return await ControlAsync("Cancel").ConfigureAwait(true); }
        private async Task<MRKCommandResponse> ControlAsync(string action)
        {
            VerifyOperationEndpoint();
            MRKCommandResponse reply;
            if (IsRemote)
            {
                MRKCommandCenter center = ResolveCenter();
                reply = action == "Pause" ?
                    await center.PauseRemoteAsync(_activeOperation).ConfigureAwait(true) :
                    action == "Resume" ?
                    await center.ResumeRemoteAsync(_activeOperation).ConfigureAwait(true) :
                    await center.CancelRemoteAsync(_activeOperation).ConfigureAwait(true);
            }
            else
            {
                bool accepted = action == "Pause" ? _local.Pause(_activeOperation) :
                    action == "Resume" ? _local.Resume(_activeOperation) :
                    _local.Cancel(_activeOperation);
                MRKCommandResponse current = _local.Query(_activeOperation);
                reply = current == null ?
                    throw new KeyNotFoundException("原Operation不存在") : current;
                if (!accepted) { Log(action, _activeOperation, "Rejected", "策略不允许或状态不可控制"); return reply; }
            }
            DisplayResponse(action, reply, "控制请求不等于PLC急停");
            return reply;
        }
        private async void Run(Func<Task> action)
        {
            try { await action().ConfigureAwait(true); }
            catch (Exception error) { Show("操作拒绝/失败：" + error.Message); }
        }
        private void Export()
        {
            SaveFileDialog picker = new SaveFileDialog {
                Title = "导出当前人工命令会话记录（非正式TCT报告）",
                Filter = "Tab separated text|*.tsv", FileName = "CommandManual-" +
                    SessionId.ToString("N").Substring(0, 8) + ".tsv" };
            if (picker.ShowDialog() != true) return;
            string[] lines = _history.Select(x => x.Time + "\t" + x.Action + "\t" +
                x.OperationId + "\t" + x.Status + "\t" + (x.Detail ?? "").Replace("\t", " "))
                .Prepend("Time\tAction\tOperationId\tStatus\tDetail").ToArray();
            File.WriteAllLines(picker.FileName, lines, System.Text.Encoding.UTF8);
            Show("已导出本次人工会话；没有产生NUnit/TCT Passed证据");
        }
        public void OnWorkspaceClosed()
        {
            if (_closed) return;
            _closed = true;
            if (_activeOperation != Guid.Empty) _local.Cancel(_activeOperation);
            foreach (IDisposable token in _registrations) token.Dispose();
            _registrations.Clear();
            if (_remote != null) _remote.Dispose();
            StopChildHost();
            _binding.Dispose();
            _local.Dispose();
        }

        /// <summary>真实WPF控件+正式MRK/MDS的可重复冒烟，不建立第二测试Runner。</summary>
        public static async Task VerifyLocalAsync()
        {
            TCTManualCommandFrame frame = new TCTManualCommandFrame();
            try
            {
                if (frame._local.Router.Discover().Count != 2)
                    throw new InvalidOperationException("命令目录没有对应真实Registry");
                frame._sources["Input"].SelectedIndex = 1;
                MRKCommandResponse result = await frame.InvokeSelectedAsync().ConfigureAwait(true);
                if (result.State != MRKCommandState.Completed ||
                    (double)result.Outputs["Output"] != 6.0 ||
                    frame._variables.Get<double>("LastOutput") != 6.0)
                    throw new InvalidOperationException("真实MDS输入/输出与命令结果未形成闭环");
                int before = frame._history.Count;
                frame._sources["Input"].SelectedIndex = 0;
                frame._editors["Input"].Text = "3000";
                bool rejected = false;
                try { await frame.InvokeSelectedAsync().ConfigureAwait(true); }
                catch (ArgumentException) { rejected = true; }
                if (!rejected || frame._history.Count <= before)
                    throw new InvalidOperationException("范围错误没有在执行前拒绝并记录");
                frame._editors["Input"].Text = "3";
                frame._editors["Options"].Text = "{\"Unknown\":true}";
                rejected = false;
                try { await frame.InvokeSelectedAsync().ConfigureAwait(true); }
                catch (ArgumentException) { rejected = true; }
                if (!rejected) throw new InvalidOperationException("嵌套对象非法成员未拒绝");
                frame._editors["Options"].Text = "{\"Label\":\"Demo\",\"Enabled\":true}";
                // 本机真实Windows OS Pipe；不冒充双进程或跨PC。
                string pipeName = "CxVisual.TCT.Command." + Guid.NewGuid().ToString("N");
                using (MRKCommandPipeServer server = new MRKCommandPipeServer(
                    frame._local, pipeName, peer => !string.IsNullOrWhiteSpace(peer)))
                {
                    server.Start();
                    try
                    {
                        frame._pipe.Text = pipeName;
                        frame._transport.SelectedIndex = 1;
                        MRKCommandResponse remote = await frame.InvokeSelectedAsync()
                            .ConfigureAwait(true);
                        MRKCommandResponse queried = await frame.QueryAsync().ConfigureAwait(true);
                        if (remote.Stage != MRKCommandStage.Accepted ||
                            remote.State != MRKCommandState.Completed ||
                            queried.OperationId != remote.OperationId ||
                            (double)remote.Outputs["Output"] != 6.0)
                            throw new InvalidOperationException("Windows IPC未完成命令与Operation查询");
                    }
                    finally { await server.StopAsync().ConfigureAwait(true); }
                }
                // 现有ArchitectureSmoke.exe独立进程是真实的OS Host，
                // 不使用TestCenter进程内Server来冒充跨进程证明。
                await frame.StartChildHostAsync().ConfigureAwait(true);
                try
                {
                    MRKCommandResponse independent = await frame.InvokeSelectedAsync()
                        .ConfigureAwait(true);
                    MRKCommandResponse confirmed = await frame.QueryAsync().ConfigureAwait(true);
                    if (independent.State != MRKCommandState.Completed ||
                        confirmed.State != MRKCommandState.Completed ||
                        confirmed.OperationId != independent.OperationId ||
                        (double)independent.Outputs["Output"] != 6.0)
                        throw new InvalidOperationException("独立进程IPC命令未形成实际闭环");
                }
                finally { frame.StopChildHost(); }
                frame._transport.SelectedIndex = 0;
                frame.UnregisterDemo();
                if (frame._local.Router.Discover().Count != 0)
                    throw new InvalidOperationException("注销命令后仍残留Registry");
                frame.RegisterDemo();
                if (frame._local.Router.Discover().Count != 2)
                    throw new InvalidOperationException("重新注册失败");
            }
            finally { frame.OnWorkspaceClosed(); }
        }
    }
}
