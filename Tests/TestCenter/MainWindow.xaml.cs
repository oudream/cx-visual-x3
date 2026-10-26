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
using CxVisual.Mechanism.Core.Modules;
using Microsoft.Win32;

namespace CxVisual.TestCenter.UI
{
    public partial class TCTMainWindow : Window
    {
        private readonly TCTTestCatalog _catalog = TCTRegistration.CreateCatalog(includeNUnit: true);
        private ITCTTestCase _selectedCatalogTest;
        private readonly TCTTestExecutionCoordinator _coordinator = new TCTTestExecutionCoordinator();
        private readonly TCTTestRibbonController _ribbon = new TCTTestRibbonController();
        private TCTWorkspaceRegistry _workspaces;
        private TCTCreatedWorkspaceStore _createdWorkspaces;
        private ITCTWorkspaceFrame _currentFrame;
        private bool _openingWorkspace;
        private bool _testConsoleOpenedManually;
        private GridLength _savedTestConsoleWidth = new GridLength(320);
        private bool _automatedRunContext;
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
            _workspaces = TCTWorkspaceRegistry.CreateDefaults(id =>
            {
                TCTWorkspaceRootFrame root = new TCTWorkspaceRootFrame(
                    _workspaces, id, _catalog.Cases.Count);
                root.OpenRequested += child => OpenRegisteredWorkspace(_workspaces.Find(child));
                return root;
            });
            _createdWorkspaces = new TCTCreatedWorkspaceStore(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CxVisual", "TestCenter", "CreatedWorkspaces.v1.xml"));
            try
            {
                _createdWorkspaces.Load(_workspaces.Items.Select(x => x.WorkspaceId));
                foreach (TCTCreatedWorkspace existing in _createdWorkspaces.Items)
                    RegisterCreatedWorkspace(existing);
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException ||
                error is System.Xml.XmlException || error is UnauthorizedAccessException)
            { StatusText.Text = "实例恢复失败；原记录仍保留：" + error.Message; }
            Explorer.Bind(_catalog);
            Explorer.BindWorkspaces(_workspaces);
            Explorer.WorkspaceSelected += OpenRegisteredWorkspace;
            Explorer.CreateRequested += id => {
                try { CreateWorkspaceInstance(id); }
                catch (Exception error) when (error is IOException ||
                    error is InvalidOperationException || error is UnauthorizedAccessException ||
                    error is InvalidDataException)
                { StatusText.Text = "创建实例失败：" + error.Message; }
            };
            Explorer.DeleteRequested += id => {
                try { DeleteWorkspaceInstance(id); }
                catch (Exception error) when (error is IOException ||
                    error is InvalidOperationException || error is UnauthorizedAccessException ||
                    error is InvalidDataException)
                { StatusText.Text = "删除实例失败：" + error.Message; }
            };
            Explorer.CloseRequested += id => DocumentHost.CloseWorkspace(id);
            Explorer.StartRequested += id => ExecuteWorkspaceAction(id, true);
            Explorer.StopRequested += id => ExecuteWorkspaceAction(id, false);
            DocumentHost.WorkspaceActivated += OnWorkspaceActivated;
            Closed += (sender, args) => DocumentHost.CloseAllWorkspaces();
            DocumentHost.Overview.Update(_catalog, null, null);
            DocumentHost.Overview.RunAllRequested += OnRunAll;
            DocumentHost.Overview.RunSelectedRequested += OnRunSelected;
            DocumentHost.Overview.HistoryRequested += () => { History.Refresh(); ActivateDockPanel("test.history"); };
            DocumentHost.Overview.ReportRequested += OnExport;
            TestPlan.Bind(_catalog, () => Explorer.CheckedIds);
            TestPlan.RunRequested += async ids => await RunAsync(false, ids);
            Explorer.TestSelected += OnTestSelected;
            Results.ResultSelected += OnResultSelected;
            History.HistoryOpened += OnHistoryOpened;
            History.ComparisonOpened += (comparison, root) =>
            {
                Diff.ShowComparison(comparison, root);
                ActivateDockPanel("test.diff");
                StatusText.Text = "History Diff: " + comparison.ChangedCount + "/" +
                    comparison.Rows.Count + " different Attempts" +
                    (comparison.Comparable ? "" : " · 跨版本/平台仅展示差异");
            };
            History.RerunRequested += async ids => await RunAsync(false, ids);
            DocumentHost.ObservationSelected += observation => Inspector.ShowObservation(observation);
            DocumentHost.ObjectSelected += (objectId, details) => Inspector.ShowObject(objectId, details);
            AutoActionHost.Children.Add(_ribbon.BuildSideActions(
                OnRunAll, OnRunSelected, OnCancel, OnExport,
                OnRerunFailed, OnCopyTestId, OnModeSelected));
            AutoActionHost.Children.Add(_ribbon.BuildSideContexts(OnModeSelected,
                () => Inspector.FocusAssertions(), () => Inspector.FocusSource(),
                () => ActivateDockPanel("test.history"), () => ActivateDockPanel("test.diff")));
            _ribbon.SetContext(null);
            MenuRunAll.Click += (sender, args) => OnRunAll();
            MenuRunSelected.Click += (sender, args) => OnRunSelected();
            MenuCancel.Click += (sender, args) => OnCancel();
            MenuRerunFailed.Click += (sender, args) => OnRerunFailed();
            MenuExportReport.Click += (sender, args) => OnExport();
            MenuSaveHistory.Click += (sender, args) => { History.SaveCurrent(); ActivateDockPanel("test.history"); };
            MenuLoadHistory.Click += (sender, args) => { History.Refresh(); ActivateDockPanel("test.history"); };
            MenuRestoreLayout.Click += (sender, args) => ResetLayout();
            // 用户按需打开通用测试操作中心，不让它长期挤占专业工作台。
            MenuShowOperations.Click += (sender,args) =>
            {
                _testConsoleOpenedManually = true;
                SetTestConsoleVisible(true);
            };
            HideTestConsoleButton.Click += (sender,args) =>
            {
                _testConsoleOpenedManually = false;
                SetTestConsoleVisible(false);
            };
            MenuSaveLayout.Click += (sender, args) => SaveLayout();
            MenuThemeLight.Click += (sender, args) => ApplyTheme(false);
            MenuThemeDark.Click += (sender, args) => ApplyTheme(true);
            _dockViews = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "test.explorer", Explorer }, { "test.documents", AutomatedWorkbenchFrame },
                { "test.results", Results },
                { "test.timeline", Timeline }, { "test.history", History },
                { "test.plan", TestPlan }, { "test.diff", Diff }
            };
            ApplyTheme(false);
            _defaultDockLayout = SerializeDockLayout();
            if (!IsSmokeProcess()) LoadLayout();
            EnsureDocumentPaneHasSingleHeader();
            StatusText.Text = "Ready · " + _catalog.Cases.Count +
                " 个真实测试用例。默认独立进程隔离；Graph/Channel/Runtime未接入的类型不会伪装通过。";
        }

        // 类型节点仅供右键创建；创建后的实例才有独立页面和稳定身份。
        public TCTWorkspaceDescriptor CreateWorkspaceInstance(string templateId)
        {
            TCTWorkspaceDescriptor template = _workspaces.Find(templateId);
            if (template == null || template.IsCreatedInstance ||
                template.Kind == TCTWorkspaceNodeKind.Group ||
                (template.Kind == TCTWorkspaceNodeKind.Root &&
                 templateId != "scenes.root" && templateId != "reports.root"))
            {
                StatusText.Text = "请在可创建的类型节点上点右键";
                return null;
            }
            int suffix = 1;
            while (_createdWorkspaces.Items.Any(x => x.TemplateId == templateId &&
                x.Title == template.Title + " " + suffix.ToString("D2", CultureInfo.InvariantCulture)))
                suffix++;
            string name = template.Title + " " + suffix.ToString("D2", CultureInfo.InvariantCulture);
            string configId = "";
            if (templateId.StartsWith("manual.module.", StringComparison.Ordinal))
            {
                string kind = templateId.Substring("manual.module.".Length);
                configId = "Module" + char.ToUpperInvariant(kind[0]) + kind.Substring(1) +
                    suffix.ToString("D2", CultureInfo.InvariantCulture);
            }
            TCTCreatedWorkspace record = _createdWorkspaces.Create(templateId, name, configId);
            TCTWorkspaceDescriptor instance = RegisterCreatedWorkspace(record);
            Explorer.RefreshWorkspaces();
            OpenRegisteredWorkspace(instance);
            Explorer.SelectWorkspace(instance.WorkspaceId);
            return instance;
        }

        private TCTWorkspaceDescriptor RegisterCreatedWorkspace(TCTCreatedWorkspace record)
        { return _workspaces.RegisterCreated(record, () => CreateInstanceFrame(record)); }

        private UserControl CreateInstanceFrame(TCTCreatedWorkspace record)
        {
            TCTWorkspaceDescriptor template = _workspaces.Find(record.TemplateId);
            if (template == null) throw new InvalidOperationException("工作区类型不存在");
            if (record.TemplateId.StartsWith("manual.module.", StringComparison.Ordinal))
            {
                MFMModuleKind kind;
                if (!Enum.TryParse(record.TemplateId.Substring("manual.module.".Length),
                    true, out kind)) throw new InvalidOperationException("模块分类不合法");
                TCTManualModuleFrame frame = new TCTManualModuleFrame();
                frame.SelectModuleKind(kind);
                frame.SetModuleId(record.ConfigurationId);
                frame.OpenSelected();
                return frame;
            }
            if (record.TemplateId == "auto.catalog")
            {
                TCTTestPlanControl plan = new TCTTestPlanControl();
                plan.Bind(_catalog, () => plan.CaseIds);
                plan.AssignCases(record.CaseIds);
                plan.CaseIdsChanged += ids => {
                    _createdWorkspaces.UpdateCases(record.Id, ids);
                    RefreshWorkspaceCommands();
                };
                plan.TestSelected += OnTestSelected;
                plan.RunRequested += async ids => await RunAsync(false, ids);
                plan.SetEmbeddedWorkspace(true);
                return plan;
            }
            if (template.Factory != null && template.Kind != TCTWorkspaceNodeKind.Root)
                return template.Factory();
            if (record.TemplateId.StartsWith("reports.", StringComparison.Ordinal) ||
                record.TemplateId == "reports.root")
            {
                // 共用唯一TCT历史仓库与校验器；每个Tab独立浏览状态，不复制Run/Attempt。
                TCTTestHistoryControl report = new TCTTestHistoryControl();
                report.SetCurrent(_displayedRun);
                report.HistoryOpened += OnHistoryOpened;
                report.ComparisonOpened += (comparison, root) => {
                    Diff.ShowComparison(comparison, root);
                    ActivateDockPanel("test.diff");
                };
                report.RerunRequested += async ids => await RunAsync(false, ids);
                return report;
            }
            StackPanel body = new StackPanel { Margin = new Thickness(22) };
            body.Children.Add(new TextBlock { Text = record.Title, FontSize = 22,
                FontWeight = FontWeights.SemiBold, Foreground = Brushes.DarkSlateBlue });
            body.Children.Add(new TextBlock {
                Text = "已创建工作区。该类型目前尚未绑定可执行的正式专业Owner，不显示虚假Passed。",
                Margin = new Thickness(0, 12, 0, 14),
                Foreground = Brushes.DarkGoldenrod, TextWrapping = TextWrapping.Wrap });
            return new UserControl { Content = body };
        }

        public void FocusAutomatedTest(string testId)
        {
            TCTWorkspaceDescriptor suite = _workspaces.Items.FirstOrDefault(x =>
                x.IsCreatedInstance && x.TemplateId == "auto.catalog");
            if (suite == null) suite = CreateWorkspaceInstance("auto.catalog");
            else OpenRegisteredWorkspace(suite);
            TabItem tab = (DocumentHost.Content as TabControl)?.SelectedItem as TabItem;
            TCTTestPlanControl plan = tab?.Content as TCTTestPlanControl;
            if (plan == null) throw new InvalidOperationException("自动测试计划Frame未打开");
            plan.FocusCase(testId);
        }
        public string CreatedConfigurationId(string instanceId)
        {
            TCTCreatedWorkspace record = _createdWorkspaces.Items.FirstOrDefault(x => x.Id == instanceId);
            return record == null ? null : record.ConfigurationId;
        }
        public TCTWorkspaceDescriptor FindOpenCreatedInstance(string templateId)
        {
            return _workspaces.Items.LastOrDefault(x => x.IsCreatedInstance &&
                x.TemplateId == templateId && DocumentHost.HasWorkspace(x.WorkspaceId));
        }
        public TCTWorkspaceDescriptor FindCreatedInstance(string templateId)
        {
            return _workspaces.Items.FirstOrDefault(x => x.IsCreatedInstance &&
                x.TemplateId == templateId);
        }
        private void DeleteWorkspaceInstance(string instanceId)
        {
            TCTWorkspaceDescriptor entry = _workspaces.Find(instanceId);
            if (entry == null || !entry.IsCreatedInstance) return;
            if (entry.TemplateId == "auto.catalog" && _cancel != null)
            {
                StatusText.Text = "自动测试Worker正在运行，不能删除计划实例；请先停止";
                return;
            }
            TabControl tabs = DocumentHost.Content as TabControl;
            TCTManualCommandFrame command = tabs == null ? null : tabs.Items
                .OfType<TabItem>().Where(x =>
                    (x.Tag as TCTWorkspaceDescriptor)?.WorkspaceId == instanceId)
                .Select(x => x.Content).OfType<TCTManualCommandFrame>().FirstOrDefault();
            if (command != null && command.IsExecuting)
            {
                StatusText.Text = "命令仍在等待回执，不能删除工作区。请先取消或等待结算。";
                return;
            }
            TCTManualModuleFrame module = tabs == null ? null : tabs.Items
                .OfType<TabItem>().Where(x =>
                    (x.Tag as TCTWorkspaceDescriptor)?.WorkspaceId == instanceId)
                .Select(x => x.Content).OfType<TCTManualModuleFrame>().FirstOrDefault();
            if (module != null && module.HasUnsavedDraft)
            {
                StatusText.Text = "草稿未保存，删除被阻止。请先保存模块配置";
                return;
            }
            TCTArchitectureSmokeFrame smoke = tabs == null ? null : tabs.Items
                .OfType<TabItem>().Where(x =>
                    (x.Tag as TCTWorkspaceDescriptor)?.WorkspaceId == instanceId)
                .Select(x => x.Content).OfType<TCTArchitectureSmokeFrame>().FirstOrDefault();
            if (smoke != null && smoke.IsRunning)
            {
                StatusText.Text = "独立架构回归正在执行，请先停止再删除";
                return;
            }
            if (module != null && DocumentHost.HasWorkspace(instanceId) &&
                !DocumentHost.CloseWorkspace(instanceId)) return;
            if (smoke != null && DocumentHost.HasWorkspace(instanceId))
                DocumentHost.CloseWorkspace(instanceId);
            if (!_createdWorkspaces.Delete(instanceId)) return;
            if (DocumentHost.HasWorkspace(instanceId))
                DocumentHost.CloseWorkspace(instanceId);
            _workspaces.RemoveCreated(instanceId);
            Explorer.RefreshWorkspaces();
            StatusText.Text = "删除实例 " + entry.Title +
                "（不删除MHS正式配置和既有Run/Attempt证据）";
        }

        private void ExecuteWorkspaceAction(string id, bool start)
        {
            TCTWorkspaceDescriptor instance = _workspaces.Find(id);
            if (instance == null || !instance.IsCreatedInstance) return;
            OpenRegisteredWorkspace(instance);
            TabItem tab = (DocumentHost.Content as TabControl)?.SelectedItem as TabItem;
            ITCTWorkspaceFrame frame = tab?.Content as ITCTWorkspaceFrame;
            if (frame == null) return;
            if (instance.TemplateId == "auto.catalog" && !start)
            {
                OnCancel();
                return;
            }
            string actionId = instance.TemplateId == "auto.architecture-smoke" ?
                (start ? "architecture.run" : "architecture.stop") :
                instance.TemplateId == "manual.channel" ? (start ? "connect" : "disconnect") :
                instance.TemplateId == "auto.catalog" ? "plan.run" : "";
            TCTWorkspaceAction action = frame.Commands.FirstOrDefault(x => x.Id == actionId);
            if (action == null || !action.CanExecute())
            {
                StatusText.Text = "正式Owner操作当前不可用：" + actionId;
                return;
            }
            action.Execute();
            RefreshWorkspaceCommands();
        }

        private void OpenRegisteredWorkspace(TCTWorkspaceDescriptor descriptor)
        {
            if (descriptor == null || !descriptor.IsCreatedInstance || _openingWorkspace) return;
            _openingWorkspace = true;
            try
            {
                DocumentHost.OpenWorkspace(descriptor);
                WorkspaceTitle.Text = descriptor.Title;
                StatusText.Text = descriptor.Title + " · 已打开实例分页（不表示测试通过）";
            }
            catch (Exception error)
            { StatusText.Text = descriptor.WorkspaceId + " · 打开失败：" + error.Message; }
            finally { _openingWorkspace = false; }
        }

        private void OnWorkspaceActivated(TCTWorkspaceDescriptor descriptor)
        {
            // TestConsole是当前自动测试工作区内的可调宽区域，其他专业工作区不自动占位。
            UpdateTestConsole(descriptor);
            if (_currentFrame != null) _currentFrame.StateChanged -= RefreshWorkspaceCommands;
            _currentFrame = null;
            ManualActionHost.Children.Clear();
            if (descriptor == null)
            {
                AutoActionHost.Visibility = Visibility.Visible;
                ManualActionHost.Visibility = Visibility.Collapsed;
                return;
            }
            bool plan = descriptor.WorkspaceId == "auto.root" ||
                descriptor.WorkspaceId == "auto.catalog" ||
                (descriptor.IsCreatedInstance && descriptor.TemplateId == "auto.catalog");
            AutoActionHost.Visibility = plan ? Visibility.Visible : Visibility.Collapsed;
            // 已创建的NUnit/TCT计划同时使用通用Run控制和计划专属命令；
            // ArchitectureSmoke只使用其独立进程的运行/停止，不冒充TCT Attempt。
            bool createdPlan = descriptor.IsCreatedInstance && descriptor.TemplateId == "auto.catalog";
            ManualActionHost.Visibility = createdPlan || !plan ?
                Visibility.Visible : Visibility.Collapsed;
            if (!_openingWorkspace) Explorer.SelectWorkspace(descriptor.WorkspaceId);
            ITCTWorkspaceFrame frame = (DocumentHost.Content as TabControl)
                ?.SelectedItem is TabItem ? ((TabItem)((TabControl)DocumentHost.Content).SelectedItem)
                .Content as ITCTWorkspaceFrame : null;
            _currentFrame = frame;
            if (_currentFrame != null) _currentFrame.StateChanged += RefreshWorkspaceCommands;
            if (createdPlan) Inspector.ShowCase(_selectedCatalogTest);
            RefreshWorkspaceCommands();
            WorkspaceTitle.Text = descriptor.Title;
        }
        public bool IsTestConsoleVisible { get { return TestConsolePanel.Visibility == Visibility.Visible; } }
        private void UpdateTestConsole(TCTWorkspaceDescriptor descriptor)
        {
            EnsureDocumentPaneHasSingleHeader();
            bool autoTest = descriptor != null &&
                (descriptor.WorkspaceId == "auto.root" ||
                 descriptor.WorkspaceId == "auto.catalog" ||
                 descriptor.WorkspaceId == "auto.architecture-smoke" ||
                 (descriptor.IsCreatedInstance &&
                    (descriptor.TemplateId == "auto.catalog" ||
                     descriptor.TemplateId == "auto.architecture-smoke")));
            // 测试工作区的普通选中并不等于有正在执行/查看的Run；
            // 关闭最后一个计划或Smoke页后，概览不应无故保留侧栏。
            if (!autoTest && descriptor != null) _automatedRunContext = false;
            if (!autoTest && !_automatedRunContext) _testConsoleOpenedManually = false;
            SetTestConsoleVisible(autoTest || _automatedRunContext || _testConsoleOpenedManually);
        }

        private void SetTestConsoleVisible(bool show)
        {
            // 仅在真正收起时记下用户拖动后的宽度；普通切换页签不能将其重置为320。
            if (!show && IsTestConsoleVisible &&
                TestConsoleColumn.Width.Value > 0)
                _savedTestConsoleWidth = TestConsoleColumn.Width;
            TestConsolePanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            TestConsoleSplitter.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            TestConsoleColumn.Width = show ? _savedTestConsoleWidth : new GridLength(0);
            TestConsoleSplitterColumn.Width = show ? new GridLength(5) : new GridLength(0);
        }

        /// <summary>
        /// 只有DocumentHost一个AvalonDock文档，内部已有自己的多分页。
        /// 隐藏外层唯一冗余标题条；不影响内部Tab、文档身份或布局序列化。
        /// 旧版已保存布局在Deserialize后可能恢复ShowHeader=true，此处纠正。
        /// </summary>
        private void EnsureDocumentPaneHasSingleHeader()
        {
            foreach (LayoutDocumentPane pane in TestDock.Layout.Descendents()
                .OfType<LayoutDocumentPane>())
            {
                if (pane.Children.OfType<LayoutDocument>()
                    .Any(item => item.ContentId == "test.documents"))
                    pane.ShowHeader = false;
            }
        }
        private void RefreshWorkspaceCommands()
        {
            // 独立进程回归的控制台展示自己的真实状态，不沿用旧NUnit断言和源码。
            TCTArchitectureSmokeFrame architecture = _currentFrame as TCTArchitectureSmokeFrame;
            if (architecture != null) Inspector.ShowArchitectureSmoke(architecture);
            ManualActionHost.Children.Clear();
            if (_currentFrame == null || !_currentFrame.Commands.Any())
            {
                ManualActionHost.Children.Add(new TextBlock {
                    Text = "本分类无主动设备操作；可从左侧注册树或中央列表打开工具。",
                    Foreground = Brushes.SlateGray, TextWrapping = TextWrapping.Wrap });
                return;
            }
            foreach (TCTWorkspaceAction action in _currentFrame.Commands)
            {
                Button button = new Button {
                    Content = action.Label, IsEnabled = action.CanExecute(),
                    Margin = new Thickness(3, 2, 3, 7), Padding = new Thickness(9, 6, 9, 6),
                    Background = Brushes.AliceBlue, BorderBrush = Brushes.LightSteelBlue };
                System.Windows.Automation.AutomationProperties.SetAutomationId(button,
                    action.Id.StartsWith("architecture.", StringComparison.Ordinal) ?
                    "TCT.Automated.Action." + action.Id : "TCT.Manual.Action." + action.Id);
                button.Click += (sender, args) =>
                {
                    if (!action.CanExecute()) return;
                    try { action.Execute(); }
                    catch (Exception error) { StatusText.Text = "人工操作失败：" + error.Message; }
                    RefreshWorkspaceCommands();
                };
                ManualActionHost.Children.Add(button);
            }
        }
        private void OnModeSelected(string mode)
        {
            DocumentHost.ShowMode(mode);
            WorkspaceTitle.Text = mode == "概览" ? "测试主界面 / Overview" : "TestCenter / " + mode;
            StatusText.Text = "TestCenter · " + mode +
                " · 数据仅来自真实测试结果；无Owner证据时显示NoEvidence。";
        }

        private void OnTestSelected(ITCTTestCase test)
        {
            _selectedCatalogTest = test;
            // Catalog用例选择只更新Inspector/上下文，不抢走已打开计划的操作中心。
            TabItem activeTab = (DocumentHost.Content as TabControl)?.SelectedItem as TabItem;
            bool insideCreatedPlan = activeTab != null && activeTab.Content is TCTTestPlanControl;
            TCTWorkspaceDescriptor activeWorkspace = activeTab == null ? null :
                activeTab.Tag as TCTWorkspaceDescriptor;
            bool professionalPage = activeWorkspace != null &&
                !activeWorkspace.WorkspaceId.StartsWith("auto.", StringComparison.Ordinal);
            if (!professionalPage &&
                !(activeTab != null && activeTab.Content is TCTArchitectureSmokeFrame))
            {
                AutoActionHost.Visibility = Visibility.Visible;
                ManualActionHost.Visibility = insideCreatedPlan ? Visibility.Visible : Visibility.Collapsed;
            }
            _ribbon.SetContext(test);
            if (!professionalPage)
                WorkspaceTitle.Text = test == null ? "测试主界面 / Overview" :
                    test.Descriptor.Aspect + " / " + test.Descriptor.Group + " / " + test.Descriptor.Name;
            Inspector.ShowCase(test);
            // 在概览页选中实际Catalog用例时也属于自动测试，不应将取消/断言隐藏。
            if (test != null && !professionalPage && (activeTab == null ||
                !(activeTab.Tag is TCTWorkspaceDescriptor)))
            {
                _automatedRunContext = true;
                SetTestConsoleVisible(true);
            }
            TCTTestCaseResult result = test == null ? null :
                _visibleResults.LastOrDefault(r => r.Descriptor.Id == test.Descriptor.Id);
            if (result != null) ShowResult(result);
            // 选中Catalog定义不销毁用户已创建的实例Tab。
        }

        private void OnHistoryOpened(TCTArchivedTestRun run, string directory)
        {
            if (run == null || _cancel != null) return;
            _activeHistoryDirectory = directory;
            _displayedRun = new TCTTestRunSummary(run.Attempts.ToList(), run.RunId);
            _visibleResults.Clear();
            _visibleResults.AddRange(run.Attempts);
            DocumentHost.Overview.Update(_catalog, _visibleResults, run.RunId);
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
            if (result == null) return;
            _automatedRunContext = true;
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
            if (_selectedCatalogTest != null)
                Clipboard.SetText(_selectedCatalogTest.Descriptor.Id);
        }
        private async void OnRunAll()
        {
            await RunAsync(false);
        }
        private async void OnRunSelected()
        {
            TCTTestPlanControl plan = (DocumentHost.Content as TabControl)?.Items
                .OfType<TabItem>().Where(x => x.IsSelected)
                .Select(x => x.Content).OfType<TCTTestPlanControl>().FirstOrDefault();
            string[] checkedIds = plan == null ? Explorer.CheckedIds : plan.CaseIds;
            if (checkedIds.Length > 0)
            {
                await RunAsync(false, checkedIds);
                return;
            }
            if (_selectedCatalogTest == null)
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
            if (selectedOnly) request.SelectedIds.Add(_selectedCatalogTest.Descriptor.Id);
            if (explicitIds != null) foreach (string id in explicitIds) request.SelectedIds.Add(id);
            _lastRun = null;
            _displayedRun = null;
            _visibleResults.Clear();
            _activeHistoryDirectory = null;
            History.SetCurrent(null);
            History.IsEnabled = false;
            _automatedRunContext = true;
            Results.Clear(); DocumentHost.Clear(); Timeline.Clear();
            DocumentHost.Overview.Update(_catalog, _visibleResults, null);
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
                        DocumentHost.Overview.Update(_catalog, _visibleResults, result.RunId);
                    }),
                    evt => Dispatcher.BeginInvoke(new Action(() => Timeline.AddEvent(evt))));
                _displayedRun = _lastRun;
                DocumentHost.Overview.Update(_catalog, _visibleResults, _lastRun.RunId);
                History.SetCurrent(_lastRun);
                TabControl tabs = DocumentHost.Content as TabControl;
                if (tabs != null)
                    foreach (TCTTestHistoryControl report in tabs.Items
                        .OfType<TabItem>().Select(x => x.Content).OfType<TCTTestHistoryControl>())
                    { report.SetCurrent(_lastRun); report.Refresh(); }
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
            HeaderBand.Background = dark ? new SolidColorBrush(Color.FromRgb(34, 38, 44)) :
                new SolidColorBrush(Color.FromRgb(242, 246, 251));
            ProductNameText.Foreground = dark ? Brushes.WhiteSmoke :
                new SolidColorBrush(Color.FromRgb(36, 68, 103));
            ProjectLabelText.Foreground = dark ? Brushes.Gainsboro : Brushes.SlateGray;
            BreadcrumbBand.Background = dark ? new SolidColorBrush(Color.FromRgb(56, 63, 74)) :
                new SolidColorBrush(Color.FromRgb(219, 234, 248));
            WorkspaceTitle.Foreground = dark ? Brushes.WhiteSmoke :
                new SolidColorBrush(Color.FromRgb(23, 54, 93));
            EnvironmentText.Foreground = dark ? Brushes.Gainsboro :
                new SolidColorBrush(Color.FromRgb(99, 125, 152));
            TestDock.Theme = dark ? (Theme)new Vs2013DarkTheme() : new Vs2013LightTheme();
            Background = dark ? new SolidColorBrush(Color.FromRgb(48, 52, 59)) :
                new SolidColorBrush(Color.FromRgb(238, 243, 249));

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
            { "test.explorer", "test.documents",
                "test.results", "test.timeline", "test.history", "test.plan", "test.diff" };
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
            EnsureDocumentPaneHasSingleHeader();
            // 对已存档的旧Dock布局也恢复当前专业工作区的聚焦显示规则。
            TabItem selected = (DocumentHost.Content as TabControl)?.SelectedItem as TabItem;
            TCTWorkspaceDescriptor current = selected == null ? null :
                selected.Tag as TCTWorkspaceDescriptor;
            UpdateTestConsole(current);
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
                if (xml.Contains("test.inspector"))
                {
                    StatusText.Text = "旧版全局操作中心布局已停用，采用新的自动测试工作台；可重新保存布局。";
                    return;
                }
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
