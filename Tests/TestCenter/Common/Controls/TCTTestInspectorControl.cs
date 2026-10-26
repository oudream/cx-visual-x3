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
    public sealed class TCTTestInspectorControl : UserControl
    {
        private readonly TextBlock _metadata = new TextBlock { Margin = new Thickness(10),
            TextWrapping = TextWrapping.Wrap, LineHeight = 24 };
        private readonly DataGrid _assertions = new DataGrid { IsReadOnly = true, AutoGenerateColumns = false,
            CanUserAddRows = false, Margin = new Thickness(3) };
        private readonly TextBlock _source = new TextBlock { Margin = new Thickness(12),
            TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Consolas") };
        private readonly DataGrid _artifacts = new DataGrid { IsReadOnly = true,
            AutoGenerateColumns = true, CanUserAddRows = false };
        private TCTTestSourceLocation _currentSource;
        private readonly Button _openSource = new Button {
            Content = "跳转源码（若本机存在）", Margin = new Thickness(8), Padding = new Thickness(8, 4, 8, 4) };
        private readonly TabControl _tabs = new TabControl();
        private readonly TabItem _observationTab = new TabItem { Header = "观察Owner" };
        private readonly TabItem _objectTab = new TabItem { Header = "工程对象" };
        private readonly TextBlock _objectDetail = new TextBlock
        {
            Margin = new Thickness(12), FontFamily = new FontFamily("Consolas"),
            TextWrapping = TextWrapping.Wrap
        };
        public string SelectedObjectId { get; private set; }
        public string SelectedObjectDetails { get; private set; }
        private readonly TextBlock _observation = new TextBlock { Margin = new Thickness(12),
            FontFamily = new FontFamily("Consolas"), TextWrapping = TextWrapping.Wrap };
        // 供WPF Smoke核实真实选择链，不通过UI推导Pass/Fail。
        public string SelectedObservationTitle { get; private set; }
        public string ArchitectureSmokeSummary { get; private set; }
        private readonly TextBlock _error = new TextBlock { Margin = new Thickness(12),
            FontFamily = new FontFamily("Consolas"), TextWrapping = TextWrapping.Wrap };
        public TCTTestInspectorControl()
        {
            _assertions.Columns.Add(new DataGridTextColumn { Header = "断言", Binding = new Binding("Name"), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
            _assertions.Columns.Add(new DataGridTextColumn { Header = "Expected", Binding = new Binding("Expected") });
            _assertions.Columns.Add(new DataGridTextColumn { Header = "Actual", Binding = new Binding("Actual") });
            _assertions.Columns.Add(new DataGridCheckBoxColumn { Header = "Pass", Binding = new Binding("Passed") });
            TabControl tabs = _tabs;
            tabs.Items.Add(new TabItem { Header = "属性", Content = new ScrollViewer { Content = _metadata } });
            tabs.Items.Add(new TabItem { Header = "断言", Content = _assertions });
            tabs.Items.Add(new TabItem { Header = "异常/Trace", Content = new ScrollViewer { Content = _error } });
            DockPanel sourcePanel = new DockPanel();
            DockPanel.SetDock(_openSource, Dock.Top);
            sourcePanel.Children.Add(_openSource);
            sourcePanel.Children.Add(new ScrollViewer { Content = _source });
            _openSource.IsEnabled = false;
            _openSource.Click += (s, e) =>
            {
                if (_currentSource == null || !System.IO.File.Exists(_currentSource.File)) return;
                try
                {
                    System.Diagnostics.Process.Start("code",
                        "--goto \"" + _currentSource.File + ":" + _currentSource.Line + "\"");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("无法自动打开编辑器，源码位置：" + _currentSource.File +
                        ":" + _currentSource.Line + "\n" + ex.Message, "TestCenter源码定位");
                }
            };
            tabs.Items.Add(new TabItem { Header = "源码定位", Content = sourcePanel });
            tabs.Items.Add(new TabItem { Header = "证据产物", Content = _artifacts });
            _observationTab.Content = new ScrollViewer { Content = _observation };
            tabs.Items.Add(_observationTab);
            _objectTab.Content = new ScrollViewer { Content = _objectDetail };
            tabs.Items.Add(_objectTab);
            Content = tabs;
        }
        /// <summary>
        /// 仅显示真实ArchitectureSmoke独立进程的当前状态，不将退出码伪装为
        /// TestCenter Run/Attempt/PASS，不复用先前NUnit/TCT的断言与证据。
        /// </summary>
        public void ShowArchitectureSmoke(TCTArchitectureSmokeFrame frame)
        {
            if (frame == null) throw new ArgumentNullException("frame");
            TCTArchitectureSmokeRunResult result = frame.LastResult;
            string state = frame.IsRunning ? "运行中" :
                result == null ? "尚未运行" :
                result.Succeeded ? "独立进程退出码0" :
                result.Cancelled ? "用户取消" :
                result.TimedOut ? "独立进程超时" : "独立进程执行失败";
            ArchitectureSmokeSummary = "ArchitectureSmoke 独立架构回归" +
                "\\n范围: " + (frame.CurrentProfileId ?? "未选择") +
                "\\n状态: " + state +
                "\\n输出条数: " + frame.MessageCount +
                (result == null ? "" :
                    "\\nExitCode: " + (result.ExitCode.HasValue ?
                        result.ExitCode.Value.ToString() : "未取得") +
                    "\\n耗时: " + result.Elapsed.TotalMilliseconds.ToString("F1") + " ms") +
                "\\n此处不产生NUnit/TCT逐条Attempt，详细输出在主工作区。";
            ArchitectureSmokeSummary = ArchitectureSmokeSummary.Replace("\\n", "\n");
            _metadata.Text = ArchitectureSmokeSummary;
            _assertions.ItemsSource = null;
            _artifacts.ItemsSource = null;
            _currentSource = null;
            _openSource.IsEnabled = false;
            _source.Text = "独立ArchitectureSmoke.exe，无当前NUnit/TCT用例源码定位。";
            _error.Text = result == null ? "当前没有独立进程运行结果。" :
                (string.IsNullOrWhiteSpace(result.Failure) ?
                    (result.Succeeded ? "独立进程执行成功（非逐项Passed）" : state) :
                    result.Failure);
            SelectedObservationTitle = null;
            SelectedObjectId = null;
            SelectedObjectDetails = null;
            _observation.Text = "ArchitectureSmoke使用原有CLI，不生成测试观察Owner。";
            _objectDetail.Text = "当前非MEM工程对象选择。";
            _tabs.SelectedIndex = 0;
        }

        public void ShowCase(ITCTTestCase test)
        {
            ArchitectureSmokeSummary = null;
            _assertions.ItemsSource = null;
            _artifacts.ItemsSource = null;
            SelectedObservationTitle = null;
            SelectedObjectId = null;
            SelectedObjectDetails = null;
            _objectDetail.Text = "选择真实工程快照中的节点或引用以查看稳定身份。";
            _tabs.SelectedIndex = 0;
            _observation.Text = "请选择真实观察项以查看Owner、身份与证据引用。";
            _currentSource = null; _openSource.IsEnabled = false;
            _source.Text = "尚无PDB源码位置；绝不猜测源码行号。";
            _error.Text = "未运行";
            if (test == null) { _metadata.Text = "请选择测试用例"; return; }
            TCTTestCaseDescriptor d = test.Descriptor;
            _metadata.Text = "Test ID: " + d.Id + "\n名称: " + d.Name +
                "\n方面: " + d.Aspect + "\n组: " + d.Group +
                "\n能力方面: " + d.CapabilityAreaId +
                "\n能力: " + d.CapabilityId + "\n对象: " + d.ObjectId +
                "\n当前状态: NotRun";
        }
        public void ShowResult(TCTTestCaseResult result)
        {
            ArchitectureSmokeSummary = null;
            if (result == null) return;
            SelectedObservationTitle = null;
            SelectedObjectId = null;
            SelectedObjectDetails = null;
            _objectDetail.Text = "选择真实工程快照中的节点或引用以查看稳定身份。";
            _tabs.SelectedIndex = 0;
            _observation.Text = "本Attempt有 " + result.Observations.Count +
                " 条观察。请在专业Viewer点击观察项，查看真实Owner和SHA256。";
            TCTTestCaseDescriptor d = result.Descriptor;
            _metadata.Text = "Test ID: " + d.Id + "\n状态: " + result.Status +
                "\n耗时: " + result.Elapsed.TotalMilliseconds.ToString("F2") +
                " ms\nRunId: " + result.RunId + "\nAttemptId: " + result.AttemptId +
                "\n方面: " + d.Aspect + "\n组: " + d.Group +
                "\n能力方面: " + d.CapabilityAreaId +
                "\n能力: " + d.CapabilityId + "\n对象: " + d.ObjectId +
                "\n断言: " + result.Assertions.Count + " 项" +
                "\n诊断: " + result.Diagnostics.Count + " 项" +
                "\n产物: " + result.Artifacts.Count + " 项";
            _assertions.ItemsSource = result.Assertions;
            _artifacts.ItemsSource = result.Artifacts;
            _currentSource = result.SourceLocation;
            _openSource.IsEnabled = _currentSource != null &&
                System.IO.File.Exists(_currentSource.File);
            _source.Text = _currentSource == null ?
                "该结果未包含可证实的PDB源文件与行号。可查看原始堆栈与诊断。" :
                "真实来源: " + _currentSource.Method +
                "\n文件: " + _currentSource.File + "\n行号: " + _currentSource.Line +
                (_openSource.IsEnabled ? "\n点击按钮跳转到VS Code" :
                    "\n源码不在本机，无法自动打开；不会跳到错误位置。");
            _error.Text = (string.IsNullOrEmpty(result.Error) ? "无异常" : result.Error) +
                string.Join("", result.Diagnostics.Select(diag =>
                    "\n[" + diag.Severity + "] " + diag.Code + " @" + diag.Stage + ": " + diag.Message)) +
                string.Join("", result.Artifacts.Select(a =>
                    "\n[Artifact] " + a.RelativePath + " SHA256=" + a.Sha256));
        }

        public void FocusAssertions() { _tabs.SelectedIndex = 1; }
        public void FocusSource() { _tabs.SelectedIndex = 3; }
        public void ShowObject(string objectId, string verifiedDetails)
        {
            SelectedObjectId = objectId;
            SelectedObjectDetails = string.IsNullOrWhiteSpace(objectId) ?
                "未选择工程对象。" : (verifiedDetails ?? objectId);
            _objectDetail.Text = SelectedObjectDetails;
            _tabs.SelectedItem = _objectTab;
        }

        public void ShowObservation(TCTTestObservation observation)
        {
            if (observation == null)
            {
                SelectedObservationTitle = null;
                _observation.Text = "没有选择观察项。";
                return;
            }
            SelectedObservationTitle = observation.Title;
            _tabs.SelectedItem = _observationTab;
            TCTObservationEnvelope e = observation.Envelope;
            _observation.Text = "Kind: " + observation.Kind +
                "\nTitle: " + observation.Title +
                "\nOrigin: " + observation.Origin +
                "\nDetails: " + observation.Details +
                (e == null ? "\nLegacy: 无结构化Owner身份（不能推断实例或伪造证据）" :
                    "\nSchema: " + e.SchemaVersion +
                    "\nRunId: " + e.RunId +
                    "\nAttemptId: " + e.AttemptId +
                    "\nSourceObjectId: " + e.SourceObjectId +
                    "\nDefinitionAddress: " + e.DefinitionAddress +
                    "\nRuntimeInstanceId: " + (string.IsNullOrEmpty(e.RuntimeInstanceId) ? "未提供" : e.RuntimeInstanceId) +
                    "\nPayload SHA256: " + (e.PayloadRef == null ? "无" : e.PayloadRef.Sha256)) +
                "\nEvidence SHA256: " +
                    (observation.Evidence == null ? "无" : observation.Evidence.Sha256);
        }
    }
}
