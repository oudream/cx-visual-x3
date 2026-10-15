using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using CxVisual.TestCenter.Cases;
using CxVisual.TestCenter.Core;

namespace CxVisual.TestCenter.UI
{
    public partial class TCTMainWindow : Window
    {
        private readonly TCTTestCatalog _catalog = new TCTTestCatalog();
        private readonly TCTTestRunner _runner = new TCTTestRunner();
        private readonly ObservableCollection<TCTTestCaseResult> _results =
            new ObservableCollection<TCTTestCaseResult>();
        private CancellationTokenSource _runCancellation;
        private ITCTTestCase _selected;

        public TCTMainWindow()
        {
            InitializeComponent();
            TCTRealCases.Register(_catalog);
            ResultGrid.ItemsSource = _results;
            BuildExplorer();
            StatusText.Text = "Ready · " + _catalog.Cases.Count +
                " 个真实用例；Graph/Protocol/Runtime测试尚未接入，不展示虚假Passed";
        }

        private void BuildExplorer()
        {
            foreach (TCTTestAspect aspect in new[] { TCTTestAspect.Foundation, TCTTestAspect.Mechanism })
            {
                TreeViewItem root = new TreeViewItem { Header = aspect.ToString(), IsExpanded = true };
                IEnumerable<IGrouping<string, ITCTTestCase>> groups = _catalog.Cases
                    .Where(c => c.Descriptor.Aspect == aspect)
                    .GroupBy(c => c.Descriptor.Group);
                foreach (IGrouping<string, ITCTTestCase> group in groups)
                {
                    TreeViewItem category = new TreeViewItem { Header = group.Key, IsExpanded = true };
                    foreach (ITCTTestCase test in group)
                        category.Items.Add(new TreeViewItem {
                            Header = test.Descriptor.Name, Tag = test,
                            ToolTip = test.Descriptor.Id + " / " + test.Descriptor.CapabilityId
                        });
                    root.Items.Add(category);
                }
                TestTree.Items.Add(root);
            }
            TreeViewItem planned = new TreeViewItem { Header = "未接入：Graph / Channel / Runtime", IsEnabled = false };
            TestTree.Items.Add(planned);
        }

        private void OnTestSelected(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            TreeViewItem node = TestTree.SelectedItem as TreeViewItem;
            _selected = node == null ? null : node.Tag as ITCTTestCase;
            if (_selected == null) return;
            TCTTestCaseDescriptor descriptor = _selected.Descriptor;
            InspectorText.Text = "TestId: " + descriptor.Id +
                "\n测试: " + descriptor.Name +
                "\n方面: " + descriptor.Aspect +
                "\n分类: " + descriptor.Group +
                "\nCapabilityArea: " + descriptor.CapabilityAreaId +
                "\nCapability: " + descriptor.CapabilityId +
                "\n能力对象: " + descriptor.ObjectId +
                "\n状态: 未运行或选择下面的真实结果";
            TCTTestCaseResult result = _results.LastOrDefault(r => r.Descriptor.Id == descriptor.Id);
            if (result == null)
            {
                PreviewText.Text = "选中的测试尚未执行。\n\n" + descriptor.Name +
                    "\n\n测试通过/失败由Runner调用生产代码后决定。";
                AssertionGrid.ItemsSource = null;
                ErrorText.Text = "暂无执行结果。";
            }
            else ShowResult(result);
        }

        private void OnResultSelected(object sender, SelectionChangedEventArgs e)
        {
            TCTTestCaseResult result = ResultGrid.SelectedItem as TCTTestCaseResult;
            if (result != null) ShowResult(result);
        }

        private void ShowResult(TCTTestCaseResult result)
        {
            TCTTestCaseDescriptor descriptor = result.Descriptor;
            string observations = string.Join("\n\n", result.Observations.Select(
                x => "[" + x.Kind + "] " + x.Title + "\n" + x.Details));
            PreviewText.Text = result.Status + " · " + descriptor.Name + "\n" +
                "运行耗时: " + result.Elapsed.TotalMilliseconds.ToString("F2") + " ms\n\n" +
                (observations.Length == 0 ? "测试没有提供可视观察数据。" : observations);
            AssertionGrid.ItemsSource = result.Assertions;
            ErrorText.Text = result.Error.Length == 0 ? "未报告异常。" : result.Error;
            InspectorText.Text = "TestId: " + descriptor.Id + "\n" +
                "方面: " + descriptor.Aspect + "\n分类: " + descriptor.Group + "\n" +
                "Capability: " + descriptor.CapabilityId + "\n" +
                "Object: " + descriptor.ObjectId + "\n" +
                "State: " + result.Status + "\n" +
                "Assertions: " + result.Assertions.Count + "\n" +
                "Duration: " + result.Elapsed.TotalMilliseconds.ToString("F2") + " ms";
        }

        private async void OnRunAll(object sender, RoutedEventArgs e)
        {
            await RunCases(_catalog.Cases);
        }

        private async void OnRunSelected(object sender, RoutedEventArgs e)
        {
            if (_selected == null)
            {
                MessageBox.Show(this, "先在左侧选择一个真实测试用例。", "Test Center");
                return;
            }
            await RunCases(new[] { _selected });
        }

        private async System.Threading.Tasks.Task RunCases(IEnumerable<ITCTTestCase> cases)
        {
            if (_runCancellation != null) return;
            _runCancellation = new CancellationTokenSource();
            RunAllButton.IsEnabled = false;
            RunSelectedButton.IsEnabled = false;
            CancelButton.IsEnabled = true;
            _results.Clear();
            StatusText.Text = "Running · 正在调用真实Foundation/Mechanism代码...";
            try
            {
                TCTTestRunSummary summary = await _runner.RunAsync(
                    cases, _runCancellation.Token, delegate(TCTTestCaseResult result) {
                        _results.Add(result);
                        ResultGrid.SelectedItem = result;
                    });
                StatusText.Text = "Completed · " + summary.Total + " 已运行；" +
                    summary.Passed + " 通过；" + summary.Failed + " 失败；" +
                    summary.Cancelled + " 取消";
            }
            catch (Exception ex)
            {
                StatusText.Text = "TestCenter Runner错误：" + ex.Message;
                ErrorText.Text = ex.ToString();
            }
            finally
            {
                _runCancellation.Dispose();
                _runCancellation = null;
                RunAllButton.IsEnabled = true;
                RunSelectedButton.IsEnabled = true;
                CancelButton.IsEnabled = false;
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            if (_runCancellation != null)
            {
                _runCancellation.Cancel();
                StatusText.Text = "Cancellation requested · 仅支持协作式取消；不宣称能强制终止卡死测试";
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_runCancellation != null) _runCancellation.Cancel();
            base.OnClosed(e);
        }
    }
}
