using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using CxVisual.Mechanism.Data.RuntimeData;

namespace CxVisual.TestCenter.UI
{
    /// <summary>在现有MDS Heap上执行的人工生命周期实验台；不制造PLC/GPU实机通过事实。</summary>
    public sealed class TCTManualDataLifecycleFrame : UserControl, ITCTWorkspaceFrame
    {
        private MDSDataHeap _heap;
        private MDSDataHeap _target;
        private IDisposable _activeUse, _retentionHold;
        private readonly ComboBox _dataName = new ComboBox { Width = 135, Margin = new Thickness(4) };
        private readonly ComboBox[] _modes = new ComboBox[5];
        private readonly TextBlock _status = new TextBlock {
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6),
            Foreground = Brushes.DarkSlateBlue, FontSize = 13 };
        private readonly TextBox _diagnostics = new TextBox {
            Margin = new Thickness(6), MinHeight = 130, IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = Brushes.WhiteSmoke, FontFamily = new FontFamily("Consolas"), FontSize = 12 };
        private readonly Button _applyAll;
        private readonly Button _acquire;
        private readonly Button _releaseUse;
        private readonly Button _sweep;
        private bool _closed;
        public string WorkspaceId { get { return "manual.data-lifecycle"; } }
        public bool IsClosed { get { return _closed; } }
        public int ActiveConsumers
        {
            get { return _heap != null && _heap.Contains("Image")
                ? _heap.Lifecycle.Inspect("Image").ActiveConsumers : 0; }
        }
        public bool HasImage { get { return _heap != null && _heap.Contains("Image"); } }
        public string LastError { get; private set; }
        public event Action StateChanged;

        public IEnumerable<TCTWorkspaceAction> Commands
        {
            get
            {
                return new[] {
                    new TCTWorkspaceAction("lifecycle.configure", "组合应用五维", ApplyAll, () => !_closed),
                    new TCTWorkspaceAction("lifecycle.ownership", "单独设置所有权", () => ApplyOne(0), () => !_closed),
                    new TCTWorkspaceAction("lifecycle.lifetime", "单独设置可用期", () => ApplyOne(1), () => !_closed),
                    new TCTWorkspaceAction("lifecycle.retention", "单独设置保留", () => ApplyOne(2), () => !_closed),
                    new TCTWorkspaceAction("lifecycle.transfer", "单独设置交接", () => ApplyOne(3), () => !_closed),
                    new TCTWorkspaceAction("lifecycle.reclamation", "单独设置回收", () => ApplyOne(4), () => !_closed),
                    new TCTWorkspaceAction("lifecycle.acquire", "登记使用", () => Guard(() => {
                        if (_activeUse != null) throw new InvalidOperationException("已有使用凭证");
                        _activeUse = _heap.Lifecycle.AcquireUse(SelectedName());
                    }), () => !_closed),
                    new TCTWorkspaceAction("lifecycle.release-use", "结束使用", () => Guard(() => {
                        if (_activeUse == null) throw new InvalidOperationException("无活跃使用");
                        _activeUse.Dispose(); _activeUse = null;
                    }), () => !_closed),
                    new TCTWorkspaceAction("lifecycle.retain", "临时保留", () => Guard(() => {
                        if (_retentionHold != null) throw new InvalidOperationException("已有保留凭证");
                        _retentionHold = _heap.Lifecycle.Retain(SelectedName(), "TestCenter.Manual");
                    }), () => !_closed),
                    new TCTWorkspaceAction("lifecycle.release-retention", "解除保留", () => Guard(() => {
                        if (_retentionHold == null) throw new InvalidOperationException("无保留凭证");
                        _retentionHold.Dispose(); _retentionHold = null;
                    }), () => !_closed),
                    new TCTWorkspaceAction("lifecycle.close-admission", "关闭新消费者", () => Guard(() =>
                        _heap.Lifecycle.CloseConsumerAdmission(SelectedName())), () => !_closed),
                    new TCTWorkspaceAction("lifecycle.sweep", "模拟TTL到期扫描", () => Guard(() => {
                        int n = _heap.Lifecycle.Sweep(DateTime.UtcNow.AddHours(1));
                        _status.Text = "模拟未来一小时TTL扫描，安全回收：" + n + " 项";
                    }), () => !_closed),
                    new TCTWorkspaceAction("lifecycle.copy", "复制到目标Heap", () => Guard(() =>
                        _heap.CopyTo(SelectedName(), _target, SelectedName() + ".Copy", true)), () => !_closed),
                    new TCTWorkspaceAction("lifecycle.move", "移动到目标Heap", () => Guard(() =>
                        _heap.MoveTo(SelectedName(), _target, SelectedName() + ".Moved", true)), () => !_closed),
                    new TCTWorkspaceAction("lifecycle.release-eligible", "尝试安全释放", () => Guard(() => {
                        bool ok = _heap.Lifecycle.ReleaseEligible(SelectedName());
                        _status.Text = ok ? "安全释放完成" : "因活跃使用/保留而拒绝释放";
                    }), () => !_closed),
                    new TCTWorkspaceAction("lifecycle.reset", "重建模拟数据", Reset, () => !_closed)
                };
            }
        }

        public TCTManualDataLifecycleFrame()
        {
            _dataName.Items.Add("Image"); _dataName.Items.Add("Result"); _dataName.SelectedIndex = 0;
            _dataName.SelectionChanged += (s, e) => Refresh();
            string[][] variants = {
                new[] { "允许复制和移动", "禁止移动", "禁止复制" },
                new[] { "Flow 生命周期", "最后消费者" },
                new[] { "Flow 保留", "TTL 2分钟", "Pin 保留" },
                new[] { "复制或移动", "仅复制", "仅移动" },
                new[] { "显式回收", "消费者结束自动回收", "TTL 到期自动回收" }
            };
            for (int i = 0; i < 5; i++)
            {
                _modes[i] = new ComboBox { MinWidth = 200, Margin = new Thickness(4) };
                foreach (string item in variants[i]) _modes[i].Items.Add(item);
                _modes[i].SelectedIndex = 0;
            }
            _applyAll = Button("组合应用五维", "TCT.DataLifecycle.ApplyAll", ApplyAll);
            _acquire = Button("登记使用", "TCT.DataLifecycle.Acquire", () => Execute("lifecycle.acquire"));
            _releaseUse = Button("结束使用", "TCT.DataLifecycle.ReleaseUse", () => Execute("lifecycle.release-use"));
            _sweep = Button("TTL到期扫描", "TCT.DataLifecycle.Sweep", () => Execute("lifecycle.sweep"));
            Build();
            Reset();
        }

        private static Button Button(string label, string id, Action click)
        {
            var b = new Button { Content = label, Padding = new Thickness(9, 5, 9, 5),
                Margin = new Thickness(4), MinHeight = 30 };
            AutomationProperties.SetAutomationId(b, id);
            b.Click += (s, e) => click();
            return b;
        }
        private void Execute(string id)
        {
            TCTWorkspaceAction action = Commands.First(x => x.Id == id);
            action.Execute();
        }
        private void Build()
        {
            var root = new StackPanel { Margin = new Thickness(12) };
            root.Children.Add(new TextBlock { Text = "MDS 数据生命周期 · 五维独立设置与组合使用",
                FontSize = 19, FontWeight = FontWeights.SemiBold, Margin = new Thickness(5, 4, 5, 8) });
            root.Children.Add(new TextBlock { Text =
                "真实原 MDS Heap：创建数据 → 独立或组合设置 → 登记使用/保留 → 复制移动 → 查看阻断 → 安全回收。"
                + " 本台为进程内模拟，非CT/GPU/PLC实机验收。",
                Foreground = Brushes.SlateGray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(5, 0, 5, 12) });
            StackPanel selection = Line();
            selection.Children.Add(Label("选择数据")); selection.Children.Add(_dataName);
            selection.Children.Add(Button("重建数据", "TCT.DataLifecycle.Reset", Reset));
            root.Children.Add(selection);
            string[] labels = { "Ownership 所有权", "Lifetime 可用期", "Retention 保留",
                "Transfer 交接", "Reclamation 回收" };
            string[] ids = { "ownership", "lifetime", "retention", "transfer", "reclamation" };
            for (int i = 0; i < 5; i++)
            {
                int index = i;
                StackPanel line = Line();
                var caption = Label(labels[i]); caption.Width = 165;
                line.Children.Add(caption); line.Children.Add(_modes[i]);
                line.Children.Add(Button("单独应用", "TCT.DataLifecycle." + ids[i],
                    () => ApplyOne(index)));
                root.Children.Add(line);
            }
            var controls = Line();
            controls.Children.Add(_applyAll);
            controls.Children.Add(_acquire); controls.Children.Add(_releaseUse);
            controls.Children.Add(Button("临时保留", "TCT.DataLifecycle.Retain", () => Execute("lifecycle.retain")));
            controls.Children.Add(Button("解除保留", "TCT.DataLifecycle.Unretain", () => Execute("lifecycle.release-retention")));
            root.Children.Add(controls);
            controls = Line();
            controls.Children.Add(Button("关闭新消费者", "TCT.DataLifecycle.Close", () => Execute("lifecycle.close-admission")));
            controls.Children.Add(_sweep);
            controls.Children.Add(Button("安全释放", "TCT.DataLifecycle.Release", () => Execute("lifecycle.release-eligible")));
            controls.Children.Add(Button("Copy", "TCT.DataLifecycle.Copy", () => Execute("lifecycle.copy")));
            controls.Children.Add(Button("Move", "TCT.DataLifecycle.Move", () => Execute("lifecycle.move")));
            root.Children.Add(controls);
            root.Children.Add(_status); root.Children.Add(_diagnostics);
            Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            AutomationProperties.SetAutomationId(this, "TCT.DataLifecycle.Workspace");
        }
        private static StackPanel Line()
        { return new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2) }; }
        private static TextBlock Label(string text)
        { return new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(5), Foreground = Brushes.DarkSlateGray }; }
        private string SelectedName()
        {
            if (_closed) throw new ObjectDisposedException("LifecycleWorkspace");
            return (string)_dataName.SelectedItem ?? "Image";
        }
        private MDSDataOwnershipPolicy Ownership()
        {
            switch (_modes[0].SelectedIndex)
            {
                case 1: return MDSDataOwnershipPolicy.Default.WithMove(false);
                case 2: return MDSDataOwnershipPolicy.Default.WithCopy(false);
                default: return MDSDataOwnershipPolicy.Default;
            }
        }
        private MDSDataLifetimePolicy Lifetime()
        { return _modes[1].SelectedIndex == 1
            ? MDSDataLifetimePolicy.UntilLastConsumer() : MDSDataLifetimePolicy.Flow; }
        private MDSDataRetentionPolicy Retention()
        {
            switch (_modes[2].SelectedIndex)
            {
                case 1: return MDSDataRetentionPolicy.CacheFor(TimeSpan.FromMinutes(2));
                case 2: return MDSDataRetentionPolicy.Pinned();
                default: return MDSDataRetentionPolicy.Flow;
            }
        }
        private MDSDataTransferPolicy Transfer()
        {
            switch (_modes[3].SelectedIndex)
            {
                case 1: return MDSDataTransferPolicy.CopyOnly;
                case 2: return MDSDataTransferPolicy.MoveOnly;
                default: return MDSDataTransferPolicy.CopyAndMove;
            }
        }
        private MDSDataReclamationPolicy Reclamation()
        {
            switch (_modes[4].SelectedIndex)
            {
                case 1: return MDSDataReclamationPolicy.AfterLastConsumer;
                case 2: return MDSDataReclamationPolicy.OnExpiry;
                default: return MDSDataReclamationPolicy.Explicit;
            }
        }
        private void ApplyAll()
        {
            Guard(() => {
                var policy = new MDSDataLifecyclePolicy(Ownership(), Lifetime(),
                    Retention(), Transfer(), Reclamation());
                _heap.Lifecycle.Configure(SelectedName(), policy);
                _status.Text = "五维策略已写入真实 MDS Heap";
            });
        }
        private void ApplyOne(int dimension)
        {
            Guard(() => {
                string name = SelectedName();
                switch (dimension)
                {
                    case 0: _heap.Lifecycle.ConfigureOwnership(name, Ownership()); break;
                    case 1: _heap.Lifecycle.ConfigureLifetime(name, Lifetime()); break;
                    case 2: _heap.Lifecycle.ConfigureRetention(name, Retention()); break;
                    case 3: _heap.Lifecycle.ConfigureTransfer(name, Transfer()); break;
                    case 4: _heap.Lifecycle.ConfigureReclamation(name, Reclamation()); break;
                }
                _status.Text = "第" + (dimension + 1) + "维策略独立生效";
            });
        }
        private void Guard(Action action)
        {
            if (_closed) return;
            try { LastError = null; action(); }
            catch (Exception error)
            {
                LastError = error.GetType().Name + ": " + error.Message;
                _status.Text = LastError;
            }
            finally { Refresh(); }
        }
        private void Refresh()
        {
            if (_closed || _heap == null) return;
            string name = SelectedName();
            if (!_heap.Contains(name))
            {
                _diagnostics.Text = name + " 已回收/已移走。可选择其他数据或重建。";
            }
            else
            {
                MDSDataLifecycleSnapshot snap = _heap.Lifecycle.Inspect(name);
                _diagnostics.Text = "Data: " + name + "\nOwner: " + snap.OwnerId +
                    "\nConsumers: " + snap.ActiveConsumers + " / Holds: " + snap.RetentionHolds +
                    "\nAdmission Closed: " + snap.ConsumerAdmissionClosed +
                    "\nTTL: " + (snap.ExpiresUtc.HasValue ? snap.ExpiresUtc.Value.ToString("O") : "none") +
                    "\nBlockers: " + string.Join(", ", snap.Blockers) +
                    "\nRetention Reasons: " + string.Join(", ", snap.RetentionReasons) +
                    "\nOwnership: Move=" + snap.Policy.Ownership.AllowMove +
                    ", Copy=" + snap.Policy.Ownership.AllowCopy +
                    "\nLifetime: ConsumerBound=" + snap.Policy.Lifetime.ConsumerBound +
                    "\nRetention: Pinned=" + snap.Policy.Retention.IsPinned +
                    "\nTransfer: Copy=" + snap.Policy.Transfer.AllowCopy +
                    ", Move=" + snap.Policy.Transfer.AllowMove +
                    "\nReclamation: AfterConsumers=" + snap.Policy.Reclamation.ReleaseAfterConsumers +
                    ", OnExpiry=" + snap.Policy.Reclamation.ReleaseOnExpiry +
                    "\nBudget retained: " + _heap.Budget.RetainedBytes;
            }
            if (StateChanged != null) StateChanged();
        }
        private void Reset()
        {
            if (_closed) return;
            Guard(() => {
                if (_activeUse != null) { _activeUse.Dispose(); _activeUse = null; }
                if (_retentionHold != null) { _retentionHold.Dispose(); _retentionHold = null; }
                if (_heap != null) _heap.Dispose();
                if (_target != null) _target.Dispose();
                _heap = new MDSDataHeap(null, new MDSDataBudget(8L * 1024 * 1024));
                _target = new MDSDataHeap();
                _heap.Set("Image", new byte[] { 1, 2, 3, 4 });
                _heap.Set("Result", 42);
                _dataName.SelectedIndex = 0;
                _status.Text = "已重建真实数据 Image / Result";
            });
        }

        public void OnWorkspaceClosed()
        {
            if (_closed) return;
            _closed = true;
            if (_activeUse != null) { _activeUse.Dispose(); _activeUse = null; }
            if (_retentionHold != null) { _retentionHold.Dispose(); _retentionHold = null; }
            if (_heap != null) _heap.Dispose();
            if (_target != null) _target.Dispose();
        }

        internal static void VerifyWorkspace()
        {
            TCTWorkspaceRegistry registry = TCTWorkspaceRegistry.CreateDefaults(id => new UserControl());
            var descriptor = registry.Find("manual.data-lifecycle");
            if (descriptor == null || descriptor.Factory == null)
                throw new InvalidOperationException("五维工作区未注册");
            TCTManualDataLifecycleFrame frame = descriptor.Factory() as TCTManualDataLifecycleFrame;
            if (frame == null || frame.Content == null) throw new InvalidOperationException("真实WPF五维面板创建失败");
            try
            {
                frame._modes[1].SelectedIndex = 1;
                frame._modes[2].SelectedIndex = 1;
                frame._modes[3].SelectedIndex = 1;
                frame._modes[4].SelectedIndex = 2;
                frame._applyAll.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                if (frame.LastError != null ||
                    !frame._heap.Lifecycle.GetPolicy("Image").Reclamation.ReleaseOnExpiry)
                    throw new InvalidOperationException("WPF组合设置未真正修改MDS策略：" + frame.LastError);
                frame._acquire.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                if (frame.ActiveConsumers != 1) throw new InvalidOperationException("WPF按钮未登记真实消费者");
                frame._sweep.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                if (!frame.HasImage) throw new InvalidOperationException("TTL在读者仍活跃时提前释放");
                frame._releaseUse.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                frame._sweep.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                if (frame.HasImage) throw new InvalidOperationException("TTL到期后未真实释放数据");
                frame.Commands.Single(x => x.Id == "lifecycle.reset").Execute();
                frame._modes[0].SelectedIndex = 1;
                frame.Commands.Single(x => x.Id == "lifecycle.ownership").Execute();
                if (frame.LastError != null || frame._heap.Lifecycle.GetPolicy("Image").Ownership.AllowMove)
                    throw new InvalidOperationException("独立Ownership设置未生效");
            }
            finally { frame.OnWorkspaceClosed(); }
            if (!frame.IsClosed) throw new InvalidOperationException("关闭五维工作区未完成清理");
        }
    }
}
