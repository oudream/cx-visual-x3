using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Data.RuntimeData;
using CxVisual.Mechanism.Visual;

namespace CxVisual.Mechanism.Presentation.Wpf
{
    /// <summary>WPF仅消费专业Owner的只读值描述；不创建变量仓/设备会话。</summary>
    public sealed class MPWLivePoint
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public string Unit { get; set; }
        public string Quality { get; set; }
        public string Source { get; set; }
        public string Updated { get; set; }
    }

    /// <summary>仅用于展示受信宿主已结算的操作反馈；不替代MRK命令结果或设备回读。</summary>
    public enum MPWLiveActionState { Rejected, Accepted, Unknown, Confirmed }

    public sealed class MPWLiveActionFeedback
    {
        public MPWLiveActionFeedback(MPWLiveActionState state, string reason)
        {
            if (!Enum.IsDefined(typeof(MPWLiveActionState), state))
                throw new ArgumentOutOfRangeException("state");
            State = state;
            Reason = reason ?? "";
        }
        public MPWLiveActionState State { get; private set; }
        public string Reason { get; private set; }
    }

    /// <summary>
    /// MEM真实Page + MVI投影 + 现有SceneView的在线只读承载。
    /// 未注入受信操作处理器时Widget不可点击；此类不校验设备身份、不直接操作设备。
    /// 委托必须由应用层先执行工程版本门禁，再交由可信Runtime.Host/MRK最终授权。
    /// </summary>
    public sealed class MPWLiveSceneControl : UserControl, IDisposable
    {
        private readonly MEMEngineeringSession _engineering;
        private readonly Guid _resourceId;
        private readonly int _revision;
        private readonly IMDSExternalBindingResolver _source;
        private readonly Func<IReadOnlyList<MPWLivePoint>> _readPoints;
        private readonly IDisposable _ownedRuntime;
        private readonly Func<MPWWidgetEventArgs, CancellationToken,
            Task<MPWLiveActionFeedback>> _trustedAction;
        private readonly CancellationTokenSource _actionLifetime = new CancellationTokenSource();
        private MPWLiveActionFeedback _lastActionFeedback;
        private string _actionMessage = "";
        private int _actionInFlight;
        private bool _outcomeRequiresReconciliation;
        private readonly SceneView _scene;
        private readonly ListView _pointList;
        private readonly TextBlock _status;
        private readonly DispatcherTimer _refreshClock;
        private IDisposable _subscription;
        private bool _closed;
        private int _queuedRefresh;

        public MPWLiveSceneControl(MEMEngineeringSession engineering, Guid resourceId,
            IMDSExternalBindingResolver source,
            Func<IReadOnlyList<MPWLivePoint>> readPoints,
            Func<Action,IDisposable> subscribe,
            IDisposable ownedRuntime)
            : this(engineering, resourceId, source, readPoints, subscribe, ownedRuntime, null)
        {
        }

        /// <summary>
        /// 操作入口为显式可选注入。没有真实受信Host时传null，保持只读；
        /// 应用Owner必须用PHMHmiRuntimeSession/MVI Action路由并在Host端认证授权。
        /// </summary>
        public MPWLiveSceneControl(MEMEngineeringSession engineering, Guid resourceId,
            IMDSExternalBindingResolver source,
            Func<IReadOnlyList<MPWLivePoint>> readPoints,
            Func<Action,IDisposable> subscribe,
            IDisposable ownedRuntime,
            Func<MPWWidgetEventArgs, CancellationToken, Task<MPWLiveActionFeedback>> trustedAction)
        {
            if (engineering == null) throw new ArgumentNullException("engineering");
            if (resourceId == Guid.Empty) throw new ArgumentException("resourceId");
            if (source == null) throw new ArgumentNullException("source");
            if (readPoints == null) throw new ArgumentNullException("readPoints");
            _engineering = engineering; _resourceId = resourceId;
            _revision = engineering.Revision;
            _source = source; _readPoints = readPoints; _ownedRuntime = ownedRuntime;
            _trustedAction = trustedAction;
            _scene = new SceneView { IsDesignMode = false,
                IsHitTestVisible = trustedAction != null };
            _scene.WidgetEventRequested += OnWidgetEventRequested;
            _status = new TextBlock
            {
                Margin = new Thickness(10, 6, 10, 6),
                Text = "在线只读观察：未连接受信控制通道，禁止设备操作"
            };
            _pointList = new ListView { MinWidth = 255 };
            GridView columns = new GridView();
            columns.Columns.Add(new GridViewColumn {
                Header = "变量", Width = 105, DisplayMemberBinding = new Binding("Name") });
            columns.Columns.Add(new GridViewColumn {
                Header = "值", Width = 85, DisplayMemberBinding = new Binding("Value") });
            columns.Columns.Add(new GridViewColumn {
                Header = "品质", Width = 85, DisplayMemberBinding = new Binding("Quality") });
            columns.Columns.Add(new GridViewColumn {
                Header = "来源", Width = 120, DisplayMemberBinding = new Binding("Source") });
            columns.Columns.Add(new GridViewColumn {
                Header = "更新时间(UTC)", Width = 180, DisplayMemberBinding = new Binding("Updated") });
            _pointList.View = columns;
            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(_scene, 0); grid.Children.Add(_scene);
            Grid.SetColumn(_pointList, 1); grid.Children.Add(_pointList);
            DockPanel dock = new DockPanel();
            DockPanel.SetDock(_status, Dock.Top);
            dock.Children.Add(_status);
            dock.Children.Add(grid);
            Content = dock;
            _refreshClock = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) {
                Interval = TimeSpan.FromSeconds(1)
            };
            _refreshClock.Tick += OnTick;
            try
            {
                _subscription = subscribe == null ? null : subscribe(ScheduleRefresh);
                Refresh();
                _refreshClock.Start();
            }
            catch { Dispose(); throw; }
        }
        /// <summary>可选的专业只读诊断描述，不参与控件操作权限、工程Revision或MDS真值。</summary>
        public Func<string> DiagnosticsProvider { get; set; }
        public bool CanRequestAction
        { get { return !_closed && _trustedAction != null && !_outcomeRequiresReconciliation; } }
        public MPWLiveActionFeedback LastActionFeedback { get { return _lastActionFeedback; } }

        /// <summary>
        /// 真WPF Widget事件只转交显式受信处理器。不并行重发、不自动重试，
        /// 调用进入宿主后即使超时或取消也按Unknown展示，不能当成未执行。
        /// </summary>
        private async void OnWidgetEventRequested(object sender, MPWWidgetEventArgs args)
        {
            if (!CanRequestAction) return;
            if (_engineering.Revision != _revision ||
                _engineering.Find(_resourceId) == null || args.Address == null)
            {
                _lastActionFeedback = new MPWLiveActionFeedback(
                    MPWLiveActionState.Rejected, "hmi.project_or_widget_identity_invalid");
                _actionMessage = "操作拒绝：工程版本或实例地址失效";
                Refresh();
                return;
            }
            if (Interlocked.CompareExchange(ref _actionInFlight, 1, 0) != 0)
            {
                _actionMessage = "已有操作尚未结算：拒绝重复下发";
                Refresh();
                return;
            }
            _lastActionFeedback = null;
            _actionMessage = "操作已请求，等待可信运行宿主结算";
            Refresh();
            try
            {
                MPWLiveActionFeedback outcome =
                    await _trustedAction(args, _actionLifetime.Token);
                if (_closed) return;
                _lastActionFeedback = outcome ?? new MPWLiveActionFeedback(
                    MPWLiveActionState.Unknown, "hmi.host_result_missing");
            }
            catch (Exception error)
            {
                if (_closed) return;
                _lastActionFeedback = new MPWLiveActionFeedback(
                    MPWLiveActionState.Unknown, "hmi.host_outcome_unknown:" +
                        error.GetType().Name);
            }
            finally
            {
                Interlocked.Exchange(ref _actionInFlight, 0);
                if (!_closed)
                {
                    // Unknown可能已经真正发出；Accepted尚未完成最终结算。
                    // 未经新运行会话重新认证、回读和人工核实，不在原画面重发。
                    _outcomeRequiresReconciliation =
                        _lastActionFeedback.State == MPWLiveActionState.Unknown ||
                        _lastActionFeedback.State == MPWLiveActionState.Accepted;
                    if (_outcomeRequiresReconciliation) _scene.IsHitTestVisible = false;
                    _actionMessage = "操作结算：" + _lastActionFeedback.State +
                        " · " + _lastActionFeedback.Reason +
                        (_outcomeRequiresReconciliation ?
                        "（结果未确认：本画面已锁定，请核实现场状态后重新建立受信会话）" :
                        "（寄存器回读不代表机构物理到位）");
                    Refresh();
                }
            }
        }
        private string ReadDiagnosticsSafely()
        {
            Func<string> provider=DiagnosticsProvider;
            if(provider==null)return "";
            try
            {
                string result=provider();
                if(String.IsNullOrWhiteSpace(result))return "";
                return " · "+(result.Length>512?result.Substring(0,512):result);
            }
            catch(Exception error)
            {
                // 诊断附加信息不能让正常的MDS画面被清空或中断WPF刷新。
                return " · 诊断暂不可用："+error.GetType().Name;
            }
        }
        private void OnTick(object sender, EventArgs args) { Refresh(); }
        private void ScheduleRefresh()
        {
            if (_closed || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
            // 高频MDS变化只排一个WPF刷新，不用无界Dispatcher队列放大画面延迟。
            if (Interlocked.CompareExchange(ref _queuedRefresh, 1, 0) != 0) return;
            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    Interlocked.Exchange(ref _queuedRefresh, 0);
                    if (!_closed) Refresh();
                }), DispatcherPriority.Background);
            }
            catch (InvalidOperationException)
            {
                Interlocked.Exchange(ref _queuedRefresh, 0);
            }
        }
        public void Refresh()
        {
            if (_closed) return;
            if (!Dispatcher.CheckAccess()) { ScheduleRefresh(); return; }
            if (_engineering.Revision != _revision ||
                _engineering.Find(_resourceId) == null)
            {
                _refreshClock.Stop();
                _scene.SetScene(new MVISceneItem[0], null);
                _pointList.ItemsSource = null;
                _status.Text = "工程已修改或资源已删除：旧运行画面已失效，请重新打开。";
                return;
            }
            try
            {
                _scene.SetScene(MVISceneProjection.ProjectResource(
                    _engineering, _resourceId, _source), null);
                IReadOnlyList<MPWLivePoint> values = _readPoints();
                _pointList.ItemsSource = values == null ? null : values.ToArray();
                int count = values == null ? 0 : values.Count;
                int degraded = values == null ? 0 : values.Count(point =>
                    !string.Equals(point.Quality, "Good", StringComparison.Ordinal));
                // MVI无Quality语义的工程默认值不可伪装现场正常值，整个画面降亮并显式提示。
                _scene.Opacity = degraded == 0 && count > 0 ? 1.0 : 0.57;
                _status.Text = "MDS只读观察 · "+count+"点，异常/失效 "+degraded+
                    "点 · "+(count == 0 ? "未取得实时点位" :
                        degraded > 0 ? "包含不可信或过期值（画面已降亮）" :
                        "当前采样质量Good")+
                    " · " + (_trustedAction == null ?
                        "未接入受信设备控制通道" :
                        (_outcomeRequiresReconciliation ?
                            "操作结果待核实，已禁止后续操作" :
                            "操作须经受信宿主逐次授权")) +
                    (string.IsNullOrEmpty(_actionMessage) ? "" : " · " + _actionMessage)+
                    ReadDiagnosticsSafely();
            }
            catch (Exception error)
            {
                _scene.SetScene(new MVISceneItem[0], null);
                _pointList.ItemsSource = null;
                _scene.Opacity = 0.57;
                _status.Text = "运行数据不可用：" + error.Message+
                    "（现场控制仍禁止）" +
                    ReadDiagnosticsSafely();
            }
        }
        public void Dispose()
        {
            if (_closed) return;
            _closed = true;
            _actionLifetime.Cancel();
            _scene.WidgetEventRequested -= OnWidgetEventRequested;
            if (_refreshClock != null) { _refreshClock.Stop(); _refreshClock.Tick -= OnTick; }
            if (_subscription != null) { _subscription.Dispose(); _subscription = null; }
            if (_ownedRuntime != null) _ownedRuntime.Dispose();
            _scene.SetScene(new MVISceneItem[0], null);
            _pointList.ItemsSource = null;
            _actionLifetime.Dispose();
        }
    }
}
