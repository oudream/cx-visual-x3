using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Xml;
using System.Text;
using AvalonDock.Layout;
using AvalonDock.Serializer.Xml;
using AvalonDock.Themes;
using System.Globalization;
using System.Xml.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;
using CxVisual.TestCenter.Cases;
using CxVisual.TestCenter.Core;
using CxVisual.TestCenter.Execution;
using CxVisual.TestCenter.Reports;
using CxVisual.TestCenter.Models;
using Microsoft.Win32;

namespace CxVisual.TestCenter.UI
{
    public partial class TCTMainWindow : Window
    {
        private readonly TCTTestCatalog _catalog = TCTRegistration.CreateCatalog(includeNUnit: true);
        private readonly TCTTestExecutionCoordinator _coordinator = new TCTTestExecutionCoordinator();
        private readonly TCTTestRibbonController _ribbon = new TCTTestRibbonController();
        private CancellationTokenSource _cancel;
        private TCTTestRunSummary _lastRun;
        private TCTTestRunSummary _displayedRun;
        private readonly List<TCTTestCaseResult> _visibleResults = new List<TCTTestCaseResult>();
        private string _activeHistoryDirectory;
        private string _defaultDockLayout;
        private Dictionary<string, object> _dockViews;

        public TCTMainWindow()
        {
            InitializeComponent();
            Explorer.Bind(_catalog);
            TestPlan.Bind(_catalog, () => Explorer.CheckedIds);
            TestPlan.RunRequested += async ids => await RunAsync(false, ids);
            Explorer.TestSelected += OnTestSelected;
            Results.ResultSelected += OnResultSelected;
            History.HistoryOpened += OnHistoryOpened;
            History.RerunRequested += async ids => await RunAsync(false, ids);
            DocumentHost.ObservationSelected += observation => Inspector.ShowObservation(observation);
            DocumentHost.ObjectSelected += (objectId, details) => Inspector.ShowObject(objectId, details);
            RibbonTray.ToolBars.Add(_ribbon.Build(OnRunAll, OnRunSelected, OnCancel, OnExport,
                OnRerunFailed, OnCopyTestId, OnModeSelected));
            _ribbon.SetContext(null);
            MenuRunAll.Click += (sender, args) => OnRunAll();
            MenuRunSelected.Click += (sender, args) => OnRunSelected();
            MenuCancel.Click += (sender, args) => OnCancel();
            MenuRerunFailed.Click += (sender, args) => OnRerunFailed();
            MenuExportReport.Click += (sender, args) => OnExport();
            MenuSaveHistory.Click += (sender, args) => { History.SaveCurrent(); ActivateDockPanel("test.history"); };
            MenuLoadHistory.Click += (sender, args) => { History.Refresh(); ActivateDockPanel("test.history"); };
            MenuRestoreLayout.Click += (sender, args) => ResetLayout();
            MenuSaveLayout.Click += (sender, args) => SaveLayout();
            MenuThemeLight.Click += (sender, args) => ApplyTheme(false);
            MenuThemeDark.Click += (sender, args) => ApplyTheme(true);
            _dockViews = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "test.explorer", Explorer }, { "test.documents", DocumentHost },
                { "test.inspector", Inspector }, { "test.results", Results },
                { "test.timeline", Timeline }, { "test.history", History },
                { "test.plan", TestPlan }
            };
            ApplyTheme(false);
            _defaultDockLayout = SerializeDockLayout();
            if (!IsSmokeProcess()) LoadLayout();
            StatusText.Text = "Ready · " + _catalog.Cases.Count +
                " 个真实测试用例。默认独立进程隔离；Graph/Channel/Runtime未接入的类型不会伪装通过。";
        }

        private void OnModeSelected(string mode)
        {
            DocumentHost.ShowMode(mode);
            StatusText.Text = "TestCenter · " + mode +
                " · 数据仅来自真实测试结果；无Owner证据时显示NoEvidence。";
        }

        private void OnTestSelected(ITCTTestCase test)
        {
            _ribbon.SetContext(test);
            Inspector.ShowCase(test);
            TCTTestCaseResult result = test == null ? null :
                _visibleResults.LastOrDefault(r => r.Descriptor.Id == test.Descriptor.Id);
            if (result != null) ShowResult(result);
            else DocumentHost.Clear();
        }

        private void OnHistoryOpened(TCTArchivedTestRun run, string directory)
        {
            if (run == null || _cancel != null) return;
            _activeHistoryDirectory = directory;
            _displayedRun = new TCTTestRunSummary(run.Attempts.ToList(), run.RunId);
            _visibleResults.Clear();
            _visibleResults.AddRange(run.Attempts);
            Results.Clear();
            DocumentHost.Clear();
            Timeline.Clear();
            foreach (TCTTestCaseResult attempt in run.Attempts)
                Results.Add(attempt);
            if (run.Attempts.Count > 0)
            {
                TCTTestCaseResult last = run.Attempts[run.Attempts.Count - 1];
                Inspector.ShowResult(last);
                DocumentHost.ShowResult(last, directory);
            }
            StatusText.Text = "Verified History " + run.RunId + " · " +
                run.Attempts.Count + " attempts; Timeline不伪造旧实时事件。";
        }

        private void OnResultSelected(TCTTestCaseResult result)
        {
            if (result != null) ShowResult(result);
        }

        private void ShowResult(TCTTestCaseResult result)
        {
            Inspector.ShowResult(result);
            DocumentHost.ShowResult(result, _activeHistoryDirectory);
            Timeline.FocusAttempt(result.AttemptId);
        }

        private async void OnRerunFailed()
        {
            if (_displayedRun == null) return;
            string[] failures = _displayedRun.Results.Where(r => r.Status != TCTTestStatus.Passed)
                .Select(r => r.Descriptor.Id).Distinct().ToArray();
            if (failures.Length == 0)
            {
                MessageBox.Show(this, "没有需要重跑的失败或未完成用例。", "TestCenter");
                return;
            }
            await RunAsync(false, failures);
        }
        private void OnCopyTestId()
        {
            if (Explorer.SelectedTest != null)
                Clipboard.SetText(Explorer.SelectedTest.Descriptor.Id);
        }
        private async void OnRunAll()
        {
            await RunAsync(false);
        }
        private async void OnRunSelected()
        {
            string[] checkedIds = Explorer.CheckedIds;
            if (checkedIds.Length > 0)
            {
                await RunAsync(false, checkedIds);
                return;
            }
            if (Explorer.SelectedTest == null)
            {
                MessageBox.Show(this, "请选择测试用例，或勾选多项进行批量执行。", "TestCenter");
                return;
            }
            await RunAsync(true);
        }

        private async Task RunAsync(bool selectedOnly, string[] explicitIds = null)
        {
            if (_cancel != null) return;
            TCTTestRunRequest request = new TCTTestRunRequest
            {
                Isolation = TCTTestIsolation.ChildProcess,
                Timeout = TimeSpan.FromSeconds(30)
            };
            if (selectedOnly) request.SelectedIds.Add(Explorer.SelectedTest.Descriptor.Id);
            if (explicitIds != null) foreach (string id in explicitIds) request.SelectedIds.Add(id);
            _lastRun = null;
            _displayedRun = null;
            _visibleResults.Clear();
            _activeHistoryDirectory = null;
            History.SetCurrent(null);
            History.IsEnabled = false;
            Results.Clear(); DocumentHost.Clear(); Timeline.Clear();
            _cancel = new CancellationTokenSource();
            _ribbon.SetRunning(true);
            MenuRunAll.IsEnabled = MenuRunSelected.IsEnabled = MenuRerunFailed.IsEnabled = false;
            MenuCancel.IsEnabled = true;
            StatusText.Text = "Running · 独立TestCenter Worker执行真实测试...";
            try
            {
                _lastRun = await _coordinator.RunAsync(_catalog, request, _cancel.Token,
                    result => Dispatcher.Invoke(() => {
                        _visibleResults.Add(result);
                        Results.Add(result); ShowResult(result);
                    }),
                    evt => Dispatcher.BeginInvoke(new Action(() => Timeline.AddEvent(evt))));
                _displayedRun = _lastRun;
                History.SetCurrent(_lastRun);
                StatusText.Text = "Run " + _lastRun.RunId + " · " + _lastRun.Total + "执行；" +
                    _lastRun.Passed + "通过；" + _lastRun.Failed + "失败/错误；" +
                    _lastRun.Cancelled + "取消；" + _lastRun.Unsupported + "未覆盖";
            }
            catch (Exception ex)
            {
                StatusText.Text = "TestCenter host error: " + ex.Message;
                MessageBox.Show(this, ex.ToString(), "TestCenter执行故障", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _cancel.Dispose(); _cancel = null;
                History.IsEnabled = true;
                _ribbon.SetRunning(false);
                MenuRunAll.IsEnabled = MenuRunSelected.IsEnabled = MenuRerunFailed.IsEnabled = true;
                MenuCancel.IsEnabled = false;
            }
        }

        private void OnCancel()
        {
            if (_cancel == null) return;
            _cancel.Cancel();
            StatusText.Text = "Cancellation requested · 子进程将终止；已完成结果仍保留";
        }

        private void OnExport()
        {
            if (_displayedRun == null || _displayedRun.Total == 0)
            {
                MessageBox.Show(this, "请先运行测试再导出报告。", "TestCenter");
                return;
            }
            SaveFileDialog dlg = new SaveFileDialog
            {
                FileName = "CxVisual-TestCenter-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"),
                DefaultExt = ".xml",
                Filter = "JUnit XML|*.xml|JSON|*.json|文本|*.txt"
            };
            if (dlg.ShowDialog(this) != true) return;
            try
            {
                if (dlg.FilterIndex == 1) TCTReportWriter.WriteJUnit(dlg.FileName, _displayedRun);
                else if (dlg.FilterIndex == 2) TCTReportWriter.WriteJson(dlg.FileName, _displayedRun);
                else TCTReportWriter.WriteText(dlg.FileName, _displayedRun);
                StatusText.Text = "Report saved: " + dlg.FileName;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.ToString(), "报告保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool _darkTheme;
        private const int MaxLayoutBytes = 1024 * 1024;
        private static bool IsSmokeProcess()
        {
            return Environment.GetCommandLineArgs().Any(x =>
                string.Equals(x, "--ui-smoke", StringComparison.OrdinalIgnoreCase));
        }
        private static string LayoutPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CxVisual", "TestCenter", "workbench.layout.xml");
        }
        private void ApplyTheme(bool dark)
        {
            _darkTheme = dark;
            TestDock.Theme = dark ? (Theme)new Vs2013DarkTheme() : new Vs2013LightTheme();
            Background = dark ? new SolidColorBrush(Color.FromRgb(48, 52, 59)) :
                new SolidColorBrush(Color.FromRgb(238, 243, 249));
            RibbonTray.Background = dark ? new SolidColorBrush(Color.FromRgb(63, 69, 79)) :
                new SolidColorBrush(Color.FromRgb(224, 237, 249));
            StatusText.Text = "Theme " + (dark ? "VS Dark" : "Light") +
                " · 显示主题不影响测试结果";
        }
        private void ActivateDockPanel(string id)
        {
            LayoutAnchorable panel = TestDock.Layout.Descendents()
                .OfType<LayoutAnchorable>().FirstOrDefault(x => x.ContentId == id);
            if (panel == null)
                throw new InvalidOperationException("Missing TestCenter panel " + id);
            panel.Show();
            panel.IsActive = true;
        }
        private string SerializeDockLayout()
        {
            XmlLayoutSerializer serializer = new XmlLayoutSerializer(TestDock);
            using (StringWriter writer = new StringWriter(CultureInfo.InvariantCulture))
            {
                serializer.Serialize(writer);
                string xml = writer.ToString();
                ValidateDockLayout(xml);
                return xml;
            }
        }
        private static void ValidateDockLayout(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml) ||
                Encoding.UTF8.GetByteCount(xml) > MaxLayoutBytes)
                throw new InvalidDataException("Dock layout missing or oversized");
            HashSet<string> allowed = new HashSet<string>(StringComparer.Ordinal)
            { "test.explorer", "test.documents", "test.inspector",
                "test.results", "test.timeline", "test.history", "test.plan" };
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            XmlReaderSettings settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                MaxCharactersInDocument = MaxLayoutBytes, MaxCharactersFromEntities = 0
            };
            using (StringReader stream = new StringReader(xml))
            using (XmlReader reader = XmlReader.Create(stream, settings))
            {
                while (reader.Read())
                {
                    if (reader.NodeType != XmlNodeType.Element) continue;
                    string key = reader.GetAttribute("ContentId");
                    if (string.IsNullOrEmpty(key)) continue;
                    if (!allowed.Contains(key) || !seen.Add(key))
                        throw new InvalidDataException("Unknown or duplicated layout ContentId: " + key);
                }
            }
            if (seen.Count != allowed.Count)
                throw new InvalidDataException("Required TestCenter panels missing from layout");
        }
        private void RestoreDockLayout(string xml)
        {
            ValidateDockLayout(xml);
            foreach (LayoutContent element in TestDock.Layout.Descendents().OfType<LayoutContent>().ToArray())
                element.Content = null;
            XmlLayoutSerializer serializer = new XmlLayoutSerializer(TestDock);
            serializer.LayoutSerializationCallback += (sender, args) =>
            {
                object view;
                if (!_dockViews.TryGetValue(args.Model.ContentId, out view))
                {
                    args.Cancel = true;
                    return;
                }
                args.Content = view;
            };
            using (StringReader text = new StringReader(xml))
                serializer.Deserialize(text);
        }
        private void ResetLayout()
        {
            try
            {
                if (string.IsNullOrEmpty(_defaultDockLayout))
                    throw new InvalidOperationException("Default layout unavailable");
                RestoreDockLayout(_defaultDockLayout);
                ApplyTheme(false);
            }
            catch (Exception ex) { StatusText.Text = "恢复布局失败: " + ex.Message; }
        }
        private void SaveLayout()
        {
            try
            {
                string xml = SerializeDockLayout();
                string path = LayoutPath();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temp = path + "." + Guid.NewGuid().ToString("N") + ".partial";
                try
                {
                    File.WriteAllText(temp, xml, new UTF8Encoding(false));
                    if (File.Exists(path)) File.Replace(temp, path, null);
                    else File.Move(temp, path);
                }
                finally { if (File.Exists(temp)) File.Delete(temp); }
                StatusText.Text = "AvalonDock工作台布局保存成功";
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException ||
                ex is UnauthorizedAccessException || ex is InvalidOperationException)
            { StatusText.Text = "布局保存失败: " + ex.Message; }
        }
        private void LoadLayout()
        {
            string path = LayoutPath();
            if (!File.Exists(path)) return;
            try
            {
                FileInfo info = new FileInfo(path);
                if (info.Length > MaxLayoutBytes) throw new InvalidDataException("Oversized saved layout");
                string xml = File.ReadAllText(path, Encoding.UTF8);
                string backup = SerializeDockLayout();
                try { RestoreDockLayout(xml); }
                catch
                {
                    RestoreDockLayout(backup);
                    throw;
                }
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException ||
                ex is UnauthorizedAccessException || ex is InvalidOperationException ||
                ex is XmlException || ex is ArgumentException)
            { StatusText.Text = "布局不可信，使用默认布局：" + ex.Message; }
        }
        protected override void OnClosed(EventArgs e)
        {
            if (_cancel != null) _cancel.Cancel();
            base.OnClosed(e);
        }
    }
}
