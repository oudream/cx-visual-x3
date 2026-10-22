using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using CxVisual.TestCenter.Core;
using CxVisual.TestCenter.Models;

namespace CxVisual.TestCenter.UI
{
    public sealed class TCTTestResultsControl : UserControl
    {
        private readonly ObservableCollection<TCTTestCaseResult> _results =
            new ObservableCollection<TCTTestCaseResult>();
        private readonly DataGrid _grid = new DataGrid { AutoGenerateColumns = false,
            IsReadOnly = true, CanUserAddRows = false };
        public event Action<TCTTestCaseResult> ResultSelected;
        public TCTTestResultsControl()
        {
            _grid.Columns.Add(new DataGridTextColumn { Header = "ID", Binding = new Binding("Descriptor.Id"), Width = new DataGridLength(165) });
            _grid.Columns.Add(new DataGridTextColumn { Header = "测试用例", Binding = new Binding("Descriptor.Name"), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Attempt", Binding = new Binding("AttemptId"), Width = new DataGridLength(125) });
            _grid.Columns.Add(new DataGridTextColumn { Header = "结果", Binding = new Binding("Status"), Width = new DataGridLength(95) });
            _grid.Columns.Add(new DataGridTextColumn { Header = "毫秒", Binding = new Binding("Elapsed.TotalMilliseconds") { StringFormat = "F2" }, Width = new DataGridLength(90) });
            _grid.Columns.Add(new DataGridTextColumn { Header = "诊断", Binding = new Binding("Error"), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
            _grid.SelectionChanged += (s, e) =>
            {
                if (ResultSelected != null) ResultSelected(_grid.SelectedItem as TCTTestCaseResult);
            };
            _grid.ItemsSource = _results; Content = _grid;
        }
        public int ResultCount { get { return _results.Count; } }
        public void Clear() { _results.Clear(); }
        public void Add(TCTTestCaseResult result)
        {
            _results.Add(result);
            _grid.SelectedItem = result;
            _grid.ScrollIntoView(result);
        }
    }

    // 统一工具栏的命令出口；上下文工具随选中方面切换，不复制多套Ribbon。
}
