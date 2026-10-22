using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using CxVisual.TestCenter.Core;
using CxVisual.TestCenter.Reports;

namespace CxVisual.TestCenter.UI
{
    /// <summary>History is explicit, immutable and SHA256-verified; not a second results repository.</summary>
    public sealed class TCTTestHistoryControl : UserControl
    {
        private readonly TCTTestResultRepository _repository;
        private readonly string _root;
        private readonly DataGrid _runs = new DataGrid
        {
            AutoGenerateColumns = false, IsReadOnly = true,
            CanUserAddRows = false, SelectionMode = DataGridSelectionMode.Single
        };
        private readonly TextBlock _message = new TextBlock { Margin = new Thickness(6),
            TextWrapping = TextWrapping.Wrap };
        private readonly Button _save = new Button { Content = "归档当前运行", Padding = new Thickness(8, 4, 8, 4) };
        private readonly Button _open = new Button { Content = "打开选中历史", Padding = new Thickness(8, 4, 8, 4) };
        private readonly Button _refresh = new Button { Content = "刷新", Padding = new Thickness(8, 4, 8, 4) };
        private readonly Button _rerun = new Button { Content = "重跑历史所选用例", Padding = new Thickness(8, 4, 8, 4) };
        private readonly ComboBox _before = new ComboBox { Width = 195, Margin = new Thickness(3),
            DisplayMemberPath = "RunId", ToolTip = "Before: 已归档运行" };
        private readonly ComboBox _after = new ComboBox { Width = 195, Margin = new Thickness(3),
            DisplayMemberPath = "RunId", ToolTip = "After: 已归档运行" };
        private readonly Button _compare = new Button { Content = "验证并比较两个Run",
            Margin = new Thickness(3), Padding = new Thickness(8, 4, 8, 4) };
        private TCTTestRunSummary _current;
        private TCTArchivedTestRun _loaded;
        private string _loadedDirectory;
        public event Action<TCTArchivedTestRun, string> HistoryOpened;
        public event Action<string[]> RerunRequested;
        public event Action<TCTArchivedRunComparison, string> ComparisonOpened;
        public string LoadedRunId { get { return _loaded == null ? null : _loaded.RunId; } }
        public int HistoryCount { get { return _runs.Items.Count; } }
        public TCTTestHistoryControl()
            : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CxVisual", "TestCenter", "History"))
        {
        }
        public TCTTestHistoryControl(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("History directory required", "directory");
            _root = Path.GetFullPath(directory);
            _repository = new TCTTestResultRepository(_root);
            _runs.Columns.Add(new DataGridTextColumn { Header = "RunId",
                Binding = new Binding("RunId"), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
            _runs.Columns.Add(new DataGridTextColumn { Header = "Git版本",
                Binding = new Binding("Commit"), Width = new DataGridLength(160) });
            _runs.Columns.Add(new DataGridTextColumn { Header = "平台",
                Binding = new Binding("OS"), Width = new DataGridLength(110) });
            _runs.Columns.Add(new DataGridTextColumn { Header = "Attempts",
                Binding = new Binding("Count"), Width = new DataGridLength(90) });
            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(5) };
            foreach (Button button in new[] { _save, _refresh, _open, _rerun })
            {
                button.Margin = new Thickness(3); buttons.Children.Add(button);
            }
            StackPanel compareControls = new StackPanel
            { Orientation = Orientation.Horizontal, Margin = new Thickness(5) };
            compareControls.Children.Add(new TextBlock { Text = "Before", VerticalAlignment = VerticalAlignment.Center });
            compareControls.Children.Add(_before);
            compareControls.Children.Add(new TextBlock { Text = "After", VerticalAlignment = VerticalAlignment.Center });
            compareControls.Children.Add(_after);
            compareControls.Children.Add(_compare);
            StackPanel top = new StackPanel();
            top.Children.Add(buttons);
            top.Children.Add(compareControls);
            DockPanel panel = new DockPanel();
            DockPanel.SetDock(top, Dock.Top);
            DockPanel.SetDock(_message, Dock.Bottom);
            panel.Children.Add(top);
            panel.Children.Add(_message);
            panel.Children.Add(_runs);
            Content = panel;
            _save.Click += (s, e) => SaveCurrent();
            _refresh.Click += (s, e) => Refresh();
            _open.Click += (s, e) => OpenSelected();
            _rerun.Click += (s, e) => RerunSelected();
            _compare.Click += (s, e) =>
            {
                HistoryRow before = _before.SelectedItem as HistoryRow;
                HistoryRow after = _after.SelectedItem as HistoryRow;
                if (before == null || after == null)
                { _message.Text = "请先选择两个不同历史Run"; return; }
                try { CompareArchives(before.RunId, after.RunId); }
                catch (Exception error) when (error is IOException || error is InvalidDataException ||
                    error is UnauthorizedAccessException || error is ArgumentException ||
                    error is InvalidOperationException)
                { _message.Text = "比较已拒绝：" + error.Message; }
            };
            Refresh();
        }

        public void SetCurrent(TCTTestRunSummary run)
        {
            _current = run;
            _save.IsEnabled = run != null && run.Total > 0;
        }
        public void Refresh()
        {
            var rows = new List<HistoryRow>();
            if (Directory.Exists(_root))
            {
                foreach (string path in Directory.EnumerateDirectories(_root, "Run_*")
                    .OrderByDescending(x => x, StringComparer.Ordinal).Take(500))
                {
                    string name = Path.GetFileName(path);
                    string runId = name.Substring(4);
                    Guid parsed;
                    if (!Guid.TryParseExact(runId, "N", out parsed)) continue;
                    try
                    {
                        TCTStoredTestRun run = _repository.Load(runId);
                        rows.Add(new HistoryRow { RunId = runId,
                            Commit = run.Commit, OS = run.OS, Count = run.Statuses.Count });
                    }
                    catch (IOException) { /* Broken history may remain on disk for forensic inspection. */ }
                    catch (InvalidDataException) { /* Never display corrupt summary as verified. */ }
                    catch (UnauthorizedAccessException) { }
                    catch (KeyNotFoundException) { }
                }
            }
            string beforeId = (_before.SelectedItem as HistoryRow)?.RunId;
            string afterId = (_after.SelectedItem as HistoryRow)?.RunId;
            _runs.ItemsSource = rows;
            _before.ItemsSource = rows;
            _after.ItemsSource = rows;
            _before.SelectedItem = rows.FirstOrDefault(x => x.RunId == beforeId) ??
                rows.Skip(1).FirstOrDefault();
            _after.SelectedItem = rows.FirstOrDefault(x => x.RunId == afterId) ??
                rows.FirstOrDefault();
            _message.Text = "列表仅预览摘要。打开或比较前重新核验Attempt/Artifact/SHA256；不同Commit不可声称同环境回归。";
            _save.IsEnabled = _current != null && _current.Total > 0;
        }
        public void SaveCurrent()
        {
            if (_current == null || _current.Total == 0) return;
            try
            {
                string directory = _repository.Save(_current);
                _message.Text = "已归档（不可覆盖）: " + directory;
                Refresh();
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException ||
                ex is UnauthorizedAccessException || ex is InvalidOperationException)
            { _message.Text = "归档失败：" + ex.Message; }
        }
        private void OpenSelected()
        {
            HistoryRow item = _runs.SelectedItem as HistoryRow;
            if (item == null) { _message.Text = "请选择一个Run"; return; }
            try
            {
                TCTArchivedTestRun loaded = _repository.LoadDetailed(item.RunId);
                _loaded = loaded;
                _loadedDirectory = Path.Combine(_root, "Run_" + item.RunId);
                _message.Text = "校验通过：Run=" + loaded.RunId +
                    " / " + loaded.Attempts.Count + " Attempts";
                if (HistoryOpened != null) HistoryOpened(loaded, _loadedDirectory);
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException ||
                ex is UnauthorizedAccessException || ex is InvalidOperationException ||
                ex is ArgumentException)
            {
                _loaded = null; _loadedDirectory = null;
                _message.Text = "历史验证失败（不会展示旧数据）：" + ex.Message;
            }
        }
        public TCTArchivedRunComparison CompareArchives(string beforeRunId, string afterRunId)
        {
            // Validation is performed by the same repository as ordinary History Replay.
            // Failure must not replace the current displayed run or fabricate a Diff.
            TCTArchivedRunComparison result = _repository.CompareArchived(beforeRunId, afterRunId);
            _message.Text = "已验证对比：" + result.Rows.Count + " 组Attempt；" +
                result.ChangedCount + " 组结果或Observation变化" +
                (result.Comparable ? "" : "；不同Commit/OS或未知环境，不算回归结论");
            if (ComparisonOpened != null) ComparisonOpened(result, _root);
            return result;
        }

        private void RerunSelected()
        {
            if (_loaded == null || _loaded.Attempts.Count == 0)
            { _message.Text = "请先验证并打开一次历史Run"; return; }
            string[] ids = _loaded.Attempts.Select(x => x.Descriptor.Id)
                .Distinct(StringComparer.Ordinal).ToArray();
            if (RerunRequested != null) RerunRequested(ids);
        }
        public void LoadVerified(string runId)
        {
            Guid id;
            if (!Guid.TryParseExact(runId, "N", out id))
                throw new ArgumentException("Invalid history RunId", "runId");
            TCTArchivedTestRun loaded = _repository.LoadDetailed(runId);
            _loaded = loaded;
            _loadedDirectory = Path.Combine(_root, "Run_" + runId);
            if (HistoryOpened != null) HistoryOpened(loaded, _loadedDirectory);
        }
        public sealed class HistoryRow
        {
            public string RunId { get; set; }
            public string Commit { get; set; }
            public string OS { get; set; }
            public int Count { get; set; }
        }
    }
}
