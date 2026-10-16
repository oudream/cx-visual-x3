using System;
using System.Collections.ObjectModel;
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
        public int EventCount { get { return _events.Count; } }
        public TCTTestTimelineControl()
        {
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
            panel.Children.Add(_caption);
            panel.Children.Add(grid);
            grid.ItemsSource = _events;
            Content = panel;
        }
        public void Clear()
        {
            _events.Clear();
            _caption.Text = "等待真实Worker事件...（没有事件不产生演示数据）";
        }
        public void AddEvent(TCTTestEvent evt)
        {
            if (evt == null) return;
            _events.Add(evt);
            _caption.Text = "已收到 " + _events.Count + " 条真实事件，最后Attempt=" + evt.AttemptId;
        }
    }
}
