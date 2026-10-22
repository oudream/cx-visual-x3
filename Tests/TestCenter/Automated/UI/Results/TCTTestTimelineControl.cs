using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using CxVisual.TestCenter.Models;

namespace CxVisual.TestCenter.UI
{
    // A read-only window onto actual Worker events, not a source of Pass/Fail verdict.
    public sealed class TCTTestTimelineControl : UserControl
    {
        private readonly ObservableCollection<TCTTestEvent> _events =
            new ObservableCollection<TCTTestEvent>();
        private readonly TextBlock _caption = new TextBlock {
            Text = "真实Worker开始执行后出现事件；没有事件时不生成模拟时间线。",
            Margin = new Thickness(9), TextWrapping = TextWrapping.Wrap };
        private readonly System.Windows.Data.CollectionViewSource _view =
            new System.Windows.Data.CollectionViewSource();
        private readonly ComboBox _scope = new ComboBox { Width = 160, Margin = new Thickness(7, 3, 7, 3) };
        private string _focusedAttempt;
        public int EventCount { get { return _events.Count; } }
        public int VisibleCount { get { return _view.View == null ? 0 : _view.View.Cast<TCTTestEvent>().Count(); } }
        public string FocusedAttemptId { get { return _focusedAttempt; } }
        public void FocusAttempt(string id)
        {
            // Results选中Attempt时必须实际切换过滤范围；只修改caption会混入其它Worker事件。
            if (string.IsNullOrWhiteSpace(id))
            {
                ShowAll();
                return;
            }
            _focusedAttempt = id;
            _scope.SelectedIndex = 1;
            RefreshScope();
        }
        public void ShowAll()
        {
            _focusedAttempt = null;
            _scope.SelectedIndex = 0;
            RefreshScope();
        }
        public void ShowOnlyAttempt(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("AttemptId required", "id");
            _focusedAttempt = id;
            _scope.SelectedIndex = 1;
            RefreshScope();
        }
        private void RefreshScope()
        {
            if (_view.View == null) return;
            _view.View.Filter = entry =>
            {
                TCTTestEvent evt = entry as TCTTestEvent;
                return evt != null && (_scope.SelectedIndex == 0 ||
                    string.IsNullOrEmpty(_focusedAttempt) || evt.AttemptId == _focusedAttempt);
            };
            _caption.Text = "真实事件总数 " + _events.Count +
                "，当前显示 " + VisibleCount +
                (string.IsNullOrEmpty(_focusedAttempt) ? "（全部）" :
                    "，聚焦 Attempt=" + _focusedAttempt);
        }
        public TCTTestTimelineControl()
        {
            _scope.Items.Add("全部事件");
            _scope.Items.Add("当前Attempt");
            _scope.SelectedIndex = 0;
            _scope.SelectionChanged += (s, e) => RefreshScope();
            _view.Source = _events;
            DataGrid grid = new DataGrid {
                IsReadOnly = true, CanUserAddRows = false, AutoGenerateColumns = false };
            grid.Columns.Add(new DataGridTextColumn { Header = "UTC时间",
                Binding = new Binding("TimestampUtc") { StringFormat = "HH:mm:ss.fff" },
                Width = new DataGridLength(112) });
            grid.Columns.Add(new DataGridTextColumn { Header = "Attempt",
                Binding = new Binding("AttemptId"), Width = new DataGridLength(150) });
            grid.Columns.Add(new DataGridTextColumn { Header = "事件",
                Binding = new Binding("Kind"), Width = new DataGridLength(105) });
            grid.Columns.Add(new DataGridTextColumn { Header = "真实描述",
                Binding = new Binding("Message"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            DockPanel panel = new DockPanel();
            DockPanel.SetDock(_caption, Dock.Top);
            DockPanel.SetDock(_scope, Dock.Top);
            panel.Children.Add(_caption);
            panel.Children.Add(_scope);
            panel.Children.Add(grid);
            grid.ItemsSource = _view.View;
            Content = panel;
        }
        public void Clear()
        {
            _events.Clear();
            _focusedAttempt = null;
            _scope.SelectedIndex = 0;
            _caption.Text = "等待真实Worker事件...（没有事件不产生演示数据）";
        }
        public void AddEvent(TCTTestEvent evt)
        {
            if (evt == null) return;
            _events.Add(evt);
            RefreshScope();
        }
    }
}
