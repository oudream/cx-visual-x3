using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

    /// <summary>
    /// MEM真实Page + MVI投影 + 现有SceneView的在线只读承载。
    /// Host授权前Widget不可点击，专业必须提供可信入口后才能启用交互。
    /// </summary>
    public sealed class MPWLiveSceneControl : UserControl, IDisposable
    {
        private readonly MEMEngineeringSession _engineering;
        private readonly Guid _resourceId;
        private readonly int _revision;
        private readonly IMDSExternalBindingResolver _source;
        private readonly Func<IReadOnlyList<MPWLivePoint>> _readPoints;
        private readonly IDisposable _ownedRuntime;
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
        {
            if (engineering == null) throw new ArgumentNullException("engineering");
            if (resourceId == Guid.Empty) throw new ArgumentException("resourceId");
            if (source == null) throw new ArgumentNullException("source");
            if (readPoints == null) throw new ArgumentNullException("readPoints");
            _engineering = engineering; _resourceId = resourceId;
            _revision = engineering.Revision;
            _source = source; _readPoints = readPoints; _ownedRuntime = ownedRuntime;
            _scene = new SceneView { IsDesignMode = false, IsHitTestVisible = false };
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
                    " · 未接入受信设备控制通道";
            }
            catch (Exception error)
            {
                _scene.SetScene(new MVISceneItem[0], null);
                _pointList.ItemsSource = null;
                _scene.Opacity = 0.57;
                _status.Text = "运行数据不可用：" + error.Message+
                    "（现场控制仍禁止）";
            }
        }
        public void Dispose()
        {
            if (_closed) return;
            _closed = true;
            if (_refreshClock != null) { _refreshClock.Stop(); _refreshClock.Tick -= OnTick; }
            if (_subscription != null) { _subscription.Dispose(); _subscription = null; }
            if (_ownedRuntime != null) _ownedRuntime.Dispose();
            _scene.SetScene(new MVISceneItem[0], null);
            _pointList.ItemsSource = null;
        }
    }
}
