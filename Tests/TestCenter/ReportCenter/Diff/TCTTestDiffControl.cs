using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using CxVisual.TestCenter.Core;
using CxVisual.TestCenter.Reports;

namespace CxVisual.TestCenter.UI
{
    /// <summary>
    /// Verified archive comparison, using the existing Geometry/Object viewers.
    /// Never creates a verdict or modifies source/Golden artifacts.
    /// </summary>
    public sealed class TCTTestDiffControl : UserControl
    {
        private readonly TextBlock _heading = new TextBlock { Margin = new Thickness(9),
            TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold };
        private readonly DataGrid _pairs = new DataGrid { AutoGenerateColumns = false,
            IsReadOnly = true, CanUserAddRows = false, Height = 185 };
        private readonly TextBlock _changes = new TextBlock { Margin = new Thickness(7),
            TextWrapping = TextWrapping.Wrap };
        private readonly Grid _comparison = new Grid();
        private string _historyRoot;
        private TCTArchivedRunComparison _current;

        public TCTArchivedRunComparison CurrentComparison { get { return _current; } }
        public int SelectedEvidenceChanges { get; private set; }
        public TCTTestDiffControl()
        {
            _pairs.Columns.Add(new DataGridTextColumn { Header = "Case / Iteration",
                Binding = new Binding("CaseKey"), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
            _pairs.Columns.Add(new DataGridTextColumn { Header = "Before",
                Binding = new Binding("StatusBefore"), Width = new DataGridLength(90) });
            _pairs.Columns.Add(new DataGridTextColumn { Header = "After",
                Binding = new Binding("StatusAfter"), Width = new DataGridLength(90) });
            _pairs.Columns.Add(new DataGridTextColumn { Header = "Evidence Δ",
                Binding = new Binding("EvidenceChanges"), Width = new DataGridLength(90) });
            _pairs.Columns.Add(new DataGridTextColumn { Header = "变化",
                Binding = new Binding("Difference"), Width = new DataGridLength(110) });
            _pairs.SelectionChanged += (sender, args) =>
                ShowPair(_pairs.SelectedItem as TCTArchivedComparisonRow);
            _comparison.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _comparison.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            _comparison.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            GridSplitter splitter = new GridSplitter { Width = 5, Background = Brushes.LightSteelBlue,
                HorizontalAlignment = HorizontalAlignment.Stretch };
            Grid.SetColumn(splitter, 1);
            _comparison.Children.Add(splitter);
            DockPanel panel = new DockPanel();
            StackPanel header = new StackPanel();
            header.Children.Add(_heading);
            header.Children.Add(_pairs);
            header.Children.Add(_changes);
            DockPanel.SetDock(header, Dock.Top);
            panel.Children.Add(header);
            panel.Children.Add(_comparison);
            Content = panel;
            _heading.Text = "请在History中选择两个Run后对比完整Attempt与SHA256证据。";
        }
        public void ShowComparison(TCTArchivedRunComparison comparison, string historyRoot)
        {
            if (comparison == null) throw new ArgumentNullException("comparison");
            if (string.IsNullOrWhiteSpace(historyRoot)) throw new ArgumentException("historyRoot");
            _current = comparison;
            _historyRoot = historyRoot;
            _heading.Text = "Before " + comparison.Before.RunId + "   →   After " +
                comparison.After.RunId + "\n" +
                (comparison.Comparable ? "同环境与Commit，可比较回归。" :
                    "Commit/OS不同或未知：证据Diff仍可信，但不能宣称同环境性能/结果回归。") +
                "\n已核验双Run全部Attempt及Artifact；变化 " +
                comparison.ChangedCount + " / " + comparison.Rows.Count;
            _pairs.ItemsSource = comparison.Rows;
            _pairs.SelectedIndex = comparison.Rows.Count == 0 ? -1 : 0;
        }
        private void ShowPair(TCTArchivedComparisonRow pair)
        {
            _comparison.Children.OfType<FrameworkElement>().Where(x => !(x is GridSplitter))
                .ToList().ForEach(x => _comparison.Children.Remove(x));
            SelectedEvidenceChanges = pair == null ? 0 : pair.EvidenceChanges;
            if (pair == null) { _changes.Text = ""; return; }
            _changes.Text = pair.Difference + " · " + pair.CaseKey +
                " · 结构化Observation变化 " + pair.EvidenceChanges +
                (pair.Observation == null ? "（一侧缺少Attempt）" :
                    "\n" + string.Join("\n", pair.Observation.Differences
                        .Take(12).Select(x => x.Kind + " / " + x.Key)));
            AddSide(pair.Before, _current.Before.RunId, 0, "Before");
            AddSide(pair.After, _current.After.RunId, 2, "After");
        }
        private void AddSide(TCTTestCaseResult result, string runId, int column, string title)
        {
            StackPanel panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = title + " · " +
                (result == null ? "Missing Attempt" : result.Status.ToString()),
                FontWeight = FontWeights.Bold, Margin = new Thickness(8) });
            if (result != null)
            {
                string path = System.IO.Path.Combine(_historyRoot, "Run_" + runId);
                // A fresh viewer instance per side: one WPF visual cannot have two parents.
                TCTGeometryTestView geometry = new TCTGeometryTestView();
                TCTObjectTestView objects = new TCTObjectTestView();
                geometry.ArchivedEvidenceRoot = path;
                objects.ArchivedEvidenceRoot = path;
                if (geometry.CanDisplay(result)) { geometry.Bind(result); panel.Children.Add(geometry); }
                if (objects.CanDisplay(result)) { objects.Bind(result); panel.Children.Add(objects); }
                if (!geometry.CanDisplay(result) && !objects.CanDisplay(result))
                    panel.Children.Add(new TextBlock { Text = "NoEvidence: 此Attempt无Geometry/Object专业数据",
                        Margin = new Thickness(8) });
            }
            ScrollViewer scroll = new ScrollViewer
            { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Grid.SetColumn(scroll, column);
            _comparison.Children.Add(scroll);
        }
    }
}
