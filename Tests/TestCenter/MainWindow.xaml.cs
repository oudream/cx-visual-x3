using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        public TCTMainWindow()
        {
            InitializeComponent();
            Explorer.Bind(_catalog);
            Explorer.TestSelected += OnTestSelected;
            Results.ResultSelected += OnResultSelected;
            DocumentHost.ObservationSelected += observation => Inspector.ShowObservation(observation);
            RibbonTray.ToolBars.Add(_ribbon.Build(OnRunAll, OnRunSelected, OnCancel, OnExport,
                OnRerunFailed, OnCopyTestId));
            _ribbon.SetContext(null);
            StatusText.Text = "Ready · " + _catalog.Cases.Count +
                " 个真实测试用例。默认独立进程隔离；Graph/Channel/Runtime未接入的类型不会伪装通过。";
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

        private void OnResultSelected(TCTTestCaseResult result)
        {
            if (result != null) ShowResult(result);
        }

        private void ShowResult(TCTTestCaseResult result)
        {
            Inspector.ShowResult(result);
            DocumentHost.ShowResult(result);
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
            Results.Clear(); DocumentHost.Clear(); Timeline.Clear();
            _cancel = new CancellationTokenSource();
            _ribbon.SetRunning(true);
            StatusText.Text = "Running · 独立TestCenter Worker执行真实测试...";
            try
            {
                _lastRun = await _coordinator.RunAsync(_catalog, request, _cancel.Token,
                    result => Dispatcher.Invoke(() => {
                        Results.Add(result); ShowResult(result);
                    }),
                    evt => Dispatcher.BeginInvoke(new Action(() => Timeline.AddEvent(evt))));
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

        protected override void OnClosed(EventArgs e)
        {
            if (_cancel != null) _cancel.Cancel();
            base.OnClosed(e);
        }
    }
}
