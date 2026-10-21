using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        private string _activeHistoryDirectory;

        public TCTMainWindow()
        {
            InitializeComponent();
            Explorer.Bind(_catalog);
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
            MenuSaveHistory.Click += (sender, args) => { History.SaveCurrent(); BottomTabs.SelectedIndex = 2; };
            MenuLoadHistory.Click += (sender, args) => { History.Refresh(); BottomTabs.SelectedIndex = 2; };
            MenuRestoreLayout.Click += (sender, args) => ResetLayout();
            MenuSaveLayout.Click += (sender, args) => SaveLayout();
            MenuThemeLight.Click += (sender, args) => SetTheme(false);
            MenuThemeDark.Click += (sender, args) => SetTheme(true);
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
            TCTTestCaseResult result = _lastRun == null || test == null ? null :
                _lastRun.Results.LastOrDefault(r => r.Descriptor.Id == test.Descriptor.Id);
            if (result != null) ShowResult(result);
            else DocumentHost.Clear();
        }

        private void OnHistoryOpened(TCTArchivedTestRun run, string directory)
        {
            if (run == null) return;
            _activeHistoryDirectory = directory;
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
            if (_lastRun == null) return;
            string[] failures = _lastRun.Results.Where(r => r.Status != TCTTestStatus.Passed)
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
            if (Explorer.SelectedTest == null)
            {
                MessageBox.Show(this, "请先在左边选中具体测试用例。", "TestCenter");
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
            _activeHistoryDirectory = null;
            History.SetCurrent(null);
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
                        Results.Add(result); ShowResult(result);
                    }),
                    evt => Dispatcher.BeginInvoke(new Action(() => Timeline.AddEvent(evt))));
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
            if (_lastRun == null || _lastRun.Total == 0)
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
                if (dlg.FilterIndex == 1) TCTReportWriter.WriteJUnit(dlg.FileName, _lastRun);
                else if (dlg.FilterIndex == 2) TCTReportWriter.WriteJson(dlg.FileName, _lastRun);
                else TCTReportWriter.WriteText(dlg.FileName, _lastRun);
                StatusText.Text = "Report saved: " + dlg.FileName;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.ToString(), "报告保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool _darkTheme;
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
        private void SetTheme(bool dark)
        {
            _darkTheme = dark;
            Background = dark ? new SolidColorBrush(Color.FromRgb(48, 52, 59)) :
                new SolidColorBrush(Color.FromRgb(238, 243, 249));
            RibbonTray.Background = dark ? new SolidColorBrush(Color.FromRgb(63, 69, 79)) :
                new SolidColorBrush(Color.FromRgb(224, 237, 249));
            // 专业数据控件保持高对比白色画布，深色只应用于Shell层。
            StatusText.Text = "主题 " + (dark ? "深灰" : "浅色") +
                " · 数据Viewer颜色不参与Pass/Fail";
        }
        private void ResetLayout()
        {
            LeftColumn.Width = new GridLength(290);
            RightColumn.Width = new GridLength(330);
            BottomRow.Height = new GridLength(255);
            SetTheme(false);
        }
        private void SaveLayout()
        {
            try
            {
                string path = LayoutPath();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                XElement settings = new XElement("TestCenterLayout", new XAttribute("schema", "1"),
                    new XAttribute("left", LeftColumn.ActualWidth.ToString("R", CultureInfo.InvariantCulture)),
                    new XAttribute("right", RightColumn.ActualWidth.ToString("R", CultureInfo.InvariantCulture)),
                    new XAttribute("bottom", BottomRow.ActualHeight.ToString("R", CultureInfo.InvariantCulture)),
                    new XAttribute("dark", _darkTheme));
                string temp = path + ".partial";
                File.WriteAllText(temp, settings.ToString(SaveOptions.DisableFormatting));
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
                StatusText.Text = "工作台布局保存成功";
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            { StatusText.Text = "布局保存失败：" + ex.Message; }
        }
        private void LoadLayout()
        {
            string path = LayoutPath();
            if (!File.Exists(path)) return;
            try
            {
                XDocument settings = XDocument.Load(path);
                XElement root = settings.Root;
                if (root == null || root.Name != "TestCenterLayout" ||
                    (string)root.Attribute("schema") != "1")
                    throw new InvalidDataException("Unknown layout schema");
                double left = double.Parse((string)root.Attribute("left"), CultureInfo.InvariantCulture);
                double right = double.Parse((string)root.Attribute("right"), CultureInfo.InvariantCulture);
                double bottom = double.Parse((string)root.Attribute("bottom"), CultureInfo.InvariantCulture);
                if (double.IsNaN(left) || double.IsNaN(right) || double.IsNaN(bottom) ||
                    left < 190 || left > 650 || right < 230 || right > 650 ||
                    bottom < 150 || bottom > 550)
                    throw new InvalidDataException("Invalid layout size");
                LeftColumn.Width = new GridLength(left);
                RightColumn.Width = new GridLength(right);
                BottomRow.Height = new GridLength(bottom);
                bool dark = (bool?)root.Attribute("dark") ?? false;
                SetTheme(dark);
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException ||
                ex is System.Xml.XmlException || ex is ArgumentException || ex is FormatException)
            {
                ResetLayout();
                StatusText.Text = "布局无效，已恢复默认值：" + ex.Message;
            }
        }
        protected override void OnClosed(EventArgs e)
        {
            if (_cancel != null) _cancel.Cancel();
            base.OnClosed(e);
        }
    }
}
