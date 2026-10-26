using System;
using CxVisual.Mechanism.Connectivity;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CxVisual.TestCenter.UI
{
    /// <summary>
    /// Desktop UI Automation running on a separate thread against the real TestCenter HWND.
    /// Unlike --ui-smoke this performs ValuePattern, SelectionItemPattern and InvokePattern.
    /// </summary>
    public static class TCTWpfAutomationHarness
    {
        private static void TraceUiStage(string stage)
        {
            string line = DateTime.UtcNow.ToString("O") + "  " + stage;
            string file = Path.Combine("Output", "Debug", "x64", "TestCenter-UIA-Stages.log");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                File.AppendAllText(file, line + Environment.NewLine);
            }
            catch (IOException) { /* UIA diagnostics must not affect test execution. */ }
            Console.WriteLine("TestCenter UIA stage: " + line);
        }

        public static async Task RunAsync(TCTMainWindow window)
        {
            if (window == null) throw new ArgumentNullException("window");
            Directory.CreateDirectory(Path.Combine("Output", "Debug", "x64"));
            File.WriteAllText(Path.Combine("Output", "Debug", "x64", "TestCenter-UIA-Stages.log"),"");
            TraceUiStage("Start full WPF UIA");
            window.Show();
            await window.Dispatcher.InvokeAsync(() => window.UpdateLayout());
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) throw new InvalidOperationException("TestCenter has no HWND");
            TraceUiStage("VerifyOverview");
            await Task.Run(() => VerifyOverview(hwnd, window));
            SaveScreenshot(window, "TestCenter-01-Workspace.png");
            window.Dispatcher.Invoke(new Action(() =>
                window.Explorer.InvokeCreateContextMenuForVerification("auto.catalog")));
            TraceUiStage("Interact");
            await Task.Run(() => Interact(hwnd, window));
            SaveScreenshot(window, "TestCenterUIA.png");
            SaveScreenshot(window, "TestCenter-02-Geometry.png");
            TraceUiStage("InteractObject");
            await Task.Run(() => InteractObject(hwnd, window));
            SaveScreenshot(window, "TestCenter-03-Object.png");
            TraceUiStage("InteractGraph");
            await Task.Run(() => InteractGraph(hwnd, window));
            SaveScreenshot(window, "TestCenter-05-Graph.png");
            TraceUiStage("InteractRuntime");
            await Task.Run(() => InteractRuntime(hwnd, window));
            SaveScreenshot(window, "TestCenter-06-Runtime.png");
            TraceUiStage("InteractChannel");
            await Task.Run(() => InteractChannel(hwnd, window));
            SaveScreenshot(window, "TestCenter-07-Channel.png");
            TraceUiStage("InteractRuntimeJournal");
            await Task.Run(() => InteractRuntimeJournal(hwnd, window));
            SaveScreenshot(window, "TestCenter-08-RuntimeJournal.png");
            window.MenuThemeDark.RaiseEvent(new RoutedEventArgs(
                System.Windows.Controls.MenuItem.ClickEvent, window.MenuThemeDark));
            SaveScreenshot(window, "TestCenter-04-Dark.png");
            window.MenuThemeLight.RaiseEvent(new RoutedEventArgs(
                System.Windows.Controls.MenuItem.ClickEvent, window.MenuThemeLight));
            TraceUiStage("InteractManualRoot");
            await Task.Run(() => InteractManualRoot(hwnd, window));
            SaveScreenshot(window, "TestCenter-09-ManualRoot.png");
            TraceUiStage("InteractManualChannel");
            await Task.Run(() => InteractManualChannel(hwnd, window));
            SaveScreenshot(window, "TestCenter-10-ManualChannel.png");
            window.Dispatcher.Invoke(new Action(() => {
                TCTWorkspaceDescriptor channel = window.FindCreatedInstance("manual.channel");
                if (channel == null || !window.DocumentHost.CloseWorkspace(channel.WorkspaceId))
                    throw new InvalidOperationException("Manual Channel instance tab could not close");
                // 关闭Tab后类型/已创建实例树都必须保留，右键“打开”重用同一实例。
                if (window.FindCreatedInstance("manual.channel") == null ||
                    window.DocumentHost.HasWorkspace(channel.WorkspaceId))
                    throw new InvalidOperationException("关闭Tab误删实例或页面未关");
                window.Explorer.InvokeInstanceContextMenuForVerification(
                    channel.WorkspaceId, "打开");
                if (!window.DocumentHost.HasWorkspace(channel.WorkspaceId))
                    throw new InvalidOperationException("右键打开未恢复已创建实例");
                if (!window.DocumentHost.CloseWorkspace(channel.WorkspaceId))
                    throw new InvalidOperationException("重新打开的Tab无法关闭");
                window.Explorer.InvokeInstanceContextMenuForVerification(
                    channel.WorkspaceId, "删除实例");
                if (window.FindCreatedInstance("manual.channel") != null)
                    throw new InvalidOperationException("右键删除没有移除左树实例");
            }));
            TraceUiStage("InteractManualModule");
            string initialModuleWorkspaceId =
                await Task.Run(() => InteractManualModule(hwnd, window));
            SaveScreenshot(window, "TestCenter-12-ModuleConfig.png");
            window.Dispatcher.Invoke(new Action(() => {
                if (!window.DocumentHost.CloseWorkspace(initialModuleWorkspaceId))
                    throw new InvalidOperationException(
                        "最初创建的Connector模块Tab未按准确WorkspaceId关闭");
            }));
            foreach (CxVisual.Mechanism.Core.Modules.MFMModuleKind kind in
                new[] {
                    CxVisual.Mechanism.Core.Modules.MFMModuleKind.Connector,
                    CxVisual.Mechanism.Core.Modules.MFMModuleKind.Compute,
                    CxVisual.Mechanism.Core.Modules.MFMModuleKind.Simulation,
                    CxVisual.Mechanism.Core.Modules.MFMModuleKind.Service,
                     CxVisual.Mechanism.Core.Modules.MFMModuleKind.Driver
                })
            {
                TraceUiStage("InteractModuleKind" + " / " + kind);
                string moduleWorkspaceId =
                    await Task.Run(() => InteractModuleKind(hwnd, window, kind));
                SaveScreenshot(window, "TestCenter-Module-" + kind + ".png");
                window.Dispatcher.Invoke(new Action(() => {
                    if (!window.DocumentHost.CloseWorkspace(moduleWorkspaceId))
                        throw new InvalidOperationException(
                            "模块实例未按创建时的WorkspaceId准确关闭：" + moduleWorkspaceId);
                }));
            }
            TraceUiStage("InteractArchitectureSmoke");
            await Task.Run(() => InteractArchitectureSmoke(hwnd, window));
            SaveScreenshot(window, "TestCenter-11-ArchitectureSmoke.png");
            window.Dispatcher.Invoke(new Action(() => {
                TCTWorkspaceDescriptor item = window.FindCreatedInstance("auto.architecture-smoke");
                if (item == null || !window.DocumentHost.CloseWorkspace(item.WorkspaceId))
                    throw new InvalidOperationException("ArchitectureSmoke instance tab did not close");
                // 四大根的场景/报告也必须从右键创建实例，且留在左树和Tab中。
                window.Explorer.InvokeCreateContextMenuForVerification("scenes.root");
                window.Explorer.InvokeCreateContextMenuForVerification("reports.root");
                TCTWorkspaceDescriptor scene = window.FindOpenCreatedInstance("scenes.root");
                TCTWorkspaceDescriptor report = window.FindOpenCreatedInstance("reports.root");
                if (scene == null || report == null ||
                    !window.DocumentHost.HasWorkspace(scene.WorkspaceId) ||
                    !window.DocumentHost.HasWorkspace(report.WorkspaceId))
                    throw new InvalidOperationException("场景或报告创建未保留独立工作区");
                // Report必须是正式的历史Owner组件，不能是伪造的空白结果。
                System.Windows.Controls.TabControl tabs =
                    window.DocumentHost.Content as System.Windows.Controls.TabControl;
                if (!tabs.Items.OfType<System.Windows.Controls.TabItem>()
                    .Any(x => (x.Tag as TCTWorkspaceDescriptor)?.WorkspaceId ==
                        report.WorkspaceId && x.Content is TCTTestHistoryControl))
                    throw new InvalidOperationException("报告页没有挂接真实History");
            }));
            SaveScreenshot(window, "TestCenter-13-ScenarioReport.png");
            TraceUiStage("Completed full WPF UIA");
        }
        private static string InteractModuleKind(IntPtr hwnd, TCTMainWindow window,
            CxVisual.Mechanism.Core.Modules.MFMModuleKind kind)
        {
            AutomationElement root = AutomationElement.FromHandle(hwnd);
            TCTWorkspaceDescriptor created = null;
            window.Dispatcher.Invoke(new Action(() => {
                window.Explorer.InvokeCreateContextMenuForVerification(
                    "manual.module." + kind.ToString().ToLowerInvariant());
                created = window.FindOpenCreatedInstance(
                    "manual.module." + kind.ToString().ToLowerInvariant());
            }));
            if (created == null) throw new InvalidOperationException("Module create action failed");
            if (kind == CxVisual.Mechanism.Core.Modules.MFMModuleKind.Connector) TraceUiStage("Connector / created");
            ShowManualConsoleForVerification(window);
            string id = "TCT.Workspace." + created.WorkspaceId;
            SelectionItemPattern node = Find(root, id)
                .GetCurrentPattern(SelectionItemPattern.Pattern) as SelectionItemPattern;
            if (node == null) throw new InvalidOperationException("Missing module tree type " + kind);
            node.Select();
            if (kind == CxVisual.Mechanism.Core.Modules.MFMModuleKind.Connector) TraceUiStage("Connector / selected");
            string moduleId = null;
            window.Dispatcher.Invoke(new Action(() =>
                moduleId = window.CreatedConfigurationId(created.WorkspaceId)));
            bool opened = false;
            for (int attempt = 0; attempt < 100 && !opened; attempt++)
            {
                window.Dispatcher.Invoke(new Action(() => {
                    System.Windows.Controls.TabControl tabs =
                        window.DocumentHost.Content as System.Windows.Controls.TabControl;
                    TCTManualModuleFrame frame = tabs == null ? null : tabs.Items
                        .OfType<System.Windows.Controls.TabItem>().Select(x => x.Content)
                        .OfType<TCTManualModuleFrame>().FirstOrDefault(candidate => candidate.ActiveModuleId == moduleId);
                    opened = frame != null && !frame.IsBusy && frame.HasOpenedConfiguration &&
                        frame.ActiveModuleKind == kind && frame.ActiveModuleId == moduleId;
                }));
                if (!opened) Thread.Sleep(100);
            }
            if (!opened) throw new InvalidOperationException("FCM/MPW typed module editor did not load: " + kind);
            if (kind == CxVisual.Mechanism.Core.Modules.MFMModuleKind.Connector) TraceUiStage("Connector / opened config");
            if (kind == CxVisual.Mechanism.Core.Modules.MFMModuleKind.Connector)
            {
                window.Dispatcher.Invoke(new Action(() => {
                    System.Windows.Controls.TabControl tabs =
                        window.DocumentHost.Content as System.Windows.Controls.TabControl;
                    tabs.Items.OfType<System.Windows.Controls.TabItem>().Select(x => x.Content)
                        .OfType<TCTManualModuleFrame>().Single(candidate => candidate.ActiveModuleId == moduleId).SelectModulePage("channels");
                }));
                TraceUiStage("Connector / selecting channels");
                AutomationElement cards = root.FindFirst(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.AutomationIdProperty,
                        "MPW.Channel.SchemaCards.0"));
                bool rendered = false;
                for (int attempt = 0; attempt < 60 && !rendered; attempt++)
                {
                    cards = root.FindFirst(TreeScope.Descendants,
                        new PropertyCondition(AutomationElement.AutomationIdProperty,
                            "MPW.Channel.SchemaCards.0"));
                    if (cards != null)
                        window.Dispatcher.Invoke(new Action(() => {
                            System.Windows.Controls.TabControl tabs =
                                window.DocumentHost.Content as System.Windows.Controls.TabControl;
                            TCTManualModuleFrame frame = tabs.Items
                                .OfType<System.Windows.Controls.TabItem>().Select(x => x.Content)
                                .OfType<TCTManualModuleFrame>().Single(candidate => candidate.ActiveModuleId == moduleId);
                            rendered = frame.SelectedModulePage == "channels" &&
                                frame.VisibleSchemaEditorCount >= 9;
                        }));
                    if (!rendered) Thread.Sleep(100);
                }
                TraceUiStage("Connector / schema cards rendered=" + rendered);
                if (!rendered) throw new InvalidOperationException(
                    "当前通道专属FCM Schema编辑器未渲染（含WPF布局等待）");
                window.Dispatcher.Invoke(new Action(() => {
                    System.Windows.Controls.TabControl tabs =
                        window.DocumentHost.Content as System.Windows.Controls.TabControl;
                    TCTManualModuleFrame frame = tabs.Items
                        .OfType<System.Windows.Controls.TabItem>().Select(x => x.Content)
                        .OfType<TCTManualModuleFrame>().Single(candidate => candidate.ActiveModuleId == moduleId);
                    frame.SelectModulePage("protocols");
                    if (frame.SelectedModulePage != "protocols" || frame.VisibleSchemaEditorCount < 3)
                        throw new InvalidOperationException("Protocol 身份配置 Schema 未显示");
                    frame.SelectModulePage("points");
                    if (frame.SelectedModulePage != "points")
                        throw new InvalidOperationException("PointTable 分页未被选中");
                }));
                // 空集合没有标量字段，但“添加规约点”必须是真正可操作的MPW集合命令。
                TraceUiStage("Connector / points page");
                AutomationElement pointAdd = Find(root, "MPW.Collection.Add.Protocol.Points");
                InvokePattern insertPoint = pointAdd.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
                if (insertPoint == null)
                    throw new InvalidOperationException("PointTable 正式集合新增命令不可操作");
                TraceUiStage("Connector / before insert point");
                insertPoint.Invoke();
                TraceUiStage("Connector / after insert point");
                bool pointReady = false;
                int countObserved = -1;
                for (int attempt = 0; attempt < 60 && !pointReady; attempt++)
                {
                    window.Dispatcher.Invoke(new Action(() => {
                        System.Windows.Controls.TabControl tabs =
                            window.DocumentHost.Content as System.Windows.Controls.TabControl;
                        TCTManualModuleFrame frame = tabs.Items
                            .OfType<System.Windows.Controls.TabItem>().Select(x => x.Content)
                            .OfType<TCTManualModuleFrame>().Single(candidate => candidate.ActiveModuleId == moduleId);
                        countObserved = frame.ConnectorDraftSnapshot().Protocol.Points.Count;
                        System.Windows.Controls.TextBox pointName =
                            FindWpfByAutomationId<System.Windows.Controls.TextBox>(
                                window, "MPW.Edit.Protocol.Points[0].Name");
                        pointReady = countObserved == 1 && pointName != null && pointName.IsEnabled;
                    }));
                    if (!pointReady) Thread.Sleep(100);
                }
                TraceUiStage("Connector / point ready=" + pointReady);
                if (!pointReady)
                    throw new InvalidOperationException("新增点表后没有可编辑Name字段；草稿PointCount=" +
                        countObserved);
                window.Dispatcher.Invoke(new Action(() =>
                    SaveScreenshot(window, "TestCenter-Protocol-P0-PointAdded.png")));
                window.Dispatcher.Invoke(new Action(() => {
                    System.Windows.Controls.TabControl tabs =
                        window.DocumentHost.Content as System.Windows.Controls.TabControl;
                    TCTManualModuleFrame frame = tabs.Items
                        .OfType<System.Windows.Controls.TabItem>().Select(x => x.Content)
                        .OfType<TCTManualModuleFrame>().Single(candidate => candidate.ActiveModuleId == moduleId);
                    frame.SelectModulePage("mapping");
                    if (frame.SelectedModulePage != "mapping")
                        throw new InvalidOperationException("DataMapping 分页未被选中");
                }));
                TraceUiStage("Connector / mapping page");
                AutomationElement mappingAdd = Find(root, "MPW.Collection.Add.Protocol.Mappings");
                InvokePattern insertMapping = mappingAdd.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
                if (insertMapping == null)
                    throw new InvalidOperationException("DataMapping 正式集合新增命令不可操作");
                TraceUiStage("Connector / before insert mapping");
                insertMapping.Invoke();
                TraceUiStage("Connector / after insert mapping");
                bool mappingReady = false;
                int mappingObserved = -1;
                for (int attempt = 0; attempt < 60 && !mappingReady; attempt++)
                {
                    window.Dispatcher.Invoke(new Action(() => {
                        System.Windows.Controls.TabControl tabs =
                            window.DocumentHost.Content as System.Windows.Controls.TabControl;
                        TCTManualModuleFrame frame = tabs.Items
                            .OfType<System.Windows.Controls.TabItem>().Select(x => x.Content)
                            .OfType<TCTManualModuleFrame>().Single(candidate => candidate.ActiveModuleId == moduleId);
                        mappingObserved = frame.ConnectorDraftSnapshot().Protocol.Mappings.Count;
                        mappingReady = mappingObserved == 1 &&
                            FindWpfByAutomationId<System.Windows.Controls.ComboBox>(
                                window, "MPW.Reference.Protocol.Mappings[0].PointRef") != null;
                    }));
                    if (!mappingReady) Thread.Sleep(100);
                }
                TraceUiStage("Connector / mapping ready=" + mappingReady);
                if (!mappingReady)
                    throw new InvalidOperationException("新增DataMapping未显示PointRef候选；草稿MappingCount=" +
                        mappingObserved);
                // 缺失PointRef/TargetRef时正式FCM校验必须拒绝，不能保存空映射。
                window.Dispatcher.Invoke(new Action(() => {
                    System.Windows.Controls.TabControl tabs =
                        window.DocumentHost.Content as System.Windows.Controls.TabControl;
                    TCTManualModuleFrame frame = tabs.Items
                        .OfType<System.Windows.Controls.TabItem>().Select(x => x.Content)
                        .OfType<TCTManualModuleFrame>().Single(candidate => candidate.ActiveModuleId == moduleId);
                    frame.ValidateCurrent();
                    if (!frame.StatusMessage.Contains("配置存在"))
                        throw new InvalidOperationException("空DataMapping没有被正式FCM拒绝");
                }));
                window.Dispatcher.Invoke(new Action(() => {
                    System.Windows.Controls.ComboBox reference =
                        FindWpfByAutomationId<System.Windows.Controls.ComboBox>(
                            window, "MPW.Reference.Protocol.Mappings[0].PointRef");
                    if (reference == null || !reference.IsEditable)
                        throw new InvalidOperationException("PointRef没有正式点表候选ComboBox");
                    SaveScreenshot(window, "TestCenter-Protocol-P0-MappingAdded.png");
                }));
                TraceUiStage("Connector / before mapping focus commit");
                // ComboBox/TextBox的焦点事件交给WPF UI线程，以免UIA COM调用过程中
                // MPW重新投影销毁被自动化操作的控件，导致线程互等/超时。
                window.Dispatcher.Invoke(new Action(() => {
                    System.Windows.Controls.ComboBox combo = FindWpfByAutomationId<System.Windows.Controls.ComboBox>(
                        window, "MPW.Reference.Protocol.Mappings[0].PointRef");
                    if (combo == null || !combo.IsEditable)
                        throw new InvalidOperationException("MPW PointRef不是可编辑正式点候选");
                    combo.Focus();
                    combo.Text = "point01";
                    System.Windows.Controls.TextBox next = FindWpfByAutomationId<System.Windows.Controls.TextBox>(
                        window, "MPW.Edit.Protocol.Mappings[0].TargetRef");
                    if (next == null) throw new InvalidOperationException("Mapping目标字段未渲染");
                    next.Focus(); // 触发正式MPW的LostKeyboardFocus写回MHS草稿
                    next = FindWpfByAutomationId<System.Windows.Controls.TextBox>(
                        window, "MPW.Edit.Protocol.Mappings[0].TargetRef");
                    if (next == null) throw new InvalidOperationException("Mapping写回后目标字段消失");
                    next.Text = "line/temperature";
                    System.Windows.Controls.CheckBox other = FindWpfByAutomationId<System.Windows.Controls.CheckBox>(
                        window, "MPW.Edit.Protocol.Mappings[0].Required");
                    if (other == null) throw new InvalidOperationException("Mapping Required未渲染");
                    other.Focus(); // 触发正式MPW的LostFocus写回
                }));
                TraceUiStage("Connector / after mapping focus commit");
                string draftProblem = null;
                window.Dispatcher.Invoke(new Action(() => {
                    System.Windows.Controls.TabControl tabs =
                        window.DocumentHost.Content as System.Windows.Controls.TabControl;
                    TCTManualModuleFrame frame = tabs.Items
                        .OfType<System.Windows.Controls.TabItem>().Select(x => x.Content)
                        .OfType<TCTManualModuleFrame>().Single(candidate => candidate.ActiveModuleId == moduleId);
                    TraceUiStage("Connector / dispatch started after focus");
                    MCNConnectorConfiguration draft = frame.ConnectorDraftSnapshot();
                    TraceUiStage("Connector / draft snapshot returned");
                    bool countsOk = draft.Protocol.Points.Count == 1 &&
                        draft.Protocol.Mappings.Count == 1;
                    bool pointOk = countsOk && draft.Protocol.Mappings[0].PointRef == "point01";
                    bool targetOk = countsOk &&
                        draft.Protocol.Mappings[0].TargetRef == "line/temperature";
                    TraceUiStage("Connector / draft counts=" + countsOk +
                        " point=" + pointOk + " target=" + targetOk);
                    if (!countsOk || !pointOk || !targetOk)
                    {
                        draftProblem = "MPW编辑后的映射未进入同一MHS草稿：Counts=" +
                            countsOk + " PointRef=" + pointOk + " TargetRef=" + targetOk;
                        return;
                    }
                    TraceUiStage("Connector / validation starting");
                    frame.ValidateCurrent();
                    TraceUiStage("Connector / validation returned");
                    if (frame.StatusMessage.Contains("配置存在"))
                        draftProblem = "完成真实PointRef和TargetRef后仍无法通过Schema校验：" +
                            frame.StatusMessage;
                }));
                // 不在WPF Dispatcher.Invoke回调内部抛UIA验证异常；否则WPF全局异常UI
                // 可能挡住自动化线程，导致CI只有240秒超时而丢失精确错误。
                if (draftProblem != null)
                    throw new InvalidOperationException(draftProblem);
                TraceUiStage("Connector / validated draft");
                window.Dispatcher.Invoke(new Action(() => SaveScreenshot(window, "TestCenter-Protocol-P0-PointMapping.png")));
                window.Dispatcher.Invoke(new Action(() => {
                    System.Windows.Controls.TabControl tabs =
                        window.DocumentHost.Content as System.Windows.Controls.TabControl;
                    TCTManualModuleFrame frame = tabs.Items
                        .OfType<System.Windows.Controls.TabItem>().Select(x => x.Content)
                        .OfType<TCTManualModuleFrame>().Single(candidate => candidate.ActiveModuleId == moduleId);
                    frame.SelectModulePage("runtime");
                    if (frame.SelectedModulePage != "runtime" || frame.VisibleSchemaEditorCount < 6)
                        throw new InvalidOperationException("Protocol执行策略分页未展示完整Schema");
                    frame.SelectModulePage("channels");
                }));
                TraceUiStage("Connector / runtime schema");
                // MPW按Tab动态投影重新生成控件，切换后必须重新获取本页的UIA元素。
                // 旧Tab已卸载的卡片对象可能返回IsOffscreen，不能误报为布局回退。
                window.Dispatcher.Invoke(new Action(() => window.UpdateLayout()));
                cards = Find(root, "MPW.Channel.SchemaCards.0");
                if (cards.Current.IsOffscreen || cards.Current.BoundingRectangle.IsEmpty)
                    throw new InvalidOperationException(
                        "Connector当前通道配置入口不在可视区域");
            }
            if (kind == CxVisual.Mechanism.Core.Modules.MFMModuleKind.Driver)
            {
                // 不存在真实 Provider 时，驱动配置不许假成功；仍应展示 FCM 注解字段。
                window.Dispatcher.Invoke(new Action(() => {
                    System.Windows.Controls.TabControl tabs =
                        window.DocumentHost.Content as System.Windows.Controls.TabControl;
                    TCTManualModuleFrame frame = tabs.Items
                        .OfType<System.Windows.Controls.TabItem>().Select(x => x.Content)
                        .OfType<TCTManualModuleFrame>().Single(candidate => candidate.ActiveModuleId == moduleId);
                    frame.SelectModulePage("provider");
                    if (frame.VisibleSchemaEditorCount < 1)
                        throw new InvalidOperationException("Driver Provider 正式 FCM 配置没有显示");
                    frame.ValidateCurrent();
                    if (!frame.StatusMessage.Contains("DRIVER_PROVIDER_REQUIRED") &&
                        !frame.StatusMessage.Contains("DriverTypeId"))
                        throw new InvalidOperationException("Driver 没有 Provider 不应通过验证");
                }));
                return created.WorkspaceId;
            }
            if (kind == CxVisual.Mechanism.Core.Modules.MFMModuleKind.Connector) TraceUiStage("Connector / pre save");
            InvokePattern save = Find(root, "TCT.Manual.Action.module.save")
                .GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
            if (save == null) throw new InvalidOperationException("MHS Save command unavailable for " + kind);
            save.Invoke();
            if (kind == CxVisual.Mechanism.Core.Modules.MFMModuleKind.Connector) TraceUiStage("Connector / saving");
            bool saved = false;
            for (int attempt = 0; attempt < 100 && !saved; attempt++)
            {
                window.Dispatcher.Invoke(new Action(() => {
                    System.Windows.Controls.TabControl tabs =
                        window.DocumentHost.Content as System.Windows.Controls.TabControl;
                    TCTManualModuleFrame frame = tabs == null ? null : tabs.Items
                        .OfType<System.Windows.Controls.TabItem>().Select(x => x.Content)
                        .OfType<TCTManualModuleFrame>().FirstOrDefault(candidate => candidate.ActiveModuleId == moduleId);
                    if (frame == null) throw new InvalidOperationException("Module editor vanished");
                    if (!frame.IsBusy && frame.StatusMessage.Contains("配置未保存"))
                        throw new InvalidOperationException("MHS rejected " + kind + ": " + frame.StatusMessage);
                    saved = !frame.IsBusy && frame.StatusMessage.StartsWith("配置已保存",
                        StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(frame.SavedAndEffectiveVersions);
                }));
                if (!saved) Thread.Sleep(100);
            }
            if (!saved) throw new InvalidOperationException("MHS did not save real " + kind + " configuration");
            InvokePattern reopen = Find(root, "TCT.Manual.Action.module.reopen")
                .GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
            if (reopen == null) throw new InvalidOperationException("Module Reopen action unavailable");
            reopen.Invoke();
            if (kind == CxVisual.Mechanism.Core.Modules.MFMModuleKind.Connector) TraceUiStage("Connector / reopened request");
            bool reloaded = false;
            for (int attempt = 0; attempt < 100 && !reloaded; attempt++)
            {
                window.Dispatcher.Invoke(new Action(() => {
                    System.Windows.Controls.TabControl tabs =
                        window.DocumentHost.Content as System.Windows.Controls.TabControl;
                    TCTManualModuleFrame frame = tabs == null ? null : tabs.Items
                        .OfType<System.Windows.Controls.TabItem>().Select(x => x.Content)
                        .OfType<TCTManualModuleFrame>().FirstOrDefault(candidate => candidate.ActiveModuleId == moduleId);
                    reloaded = frame != null && !frame.IsBusy && frame.HasOpenedConfiguration &&
                        frame.ActiveModuleKind == kind && frame.ActiveModuleId == moduleId &&
                        frame.StatusMessage.StartsWith("已创建/打开配置范围", StringComparison.Ordinal);
                }));
                if (!reloaded) Thread.Sleep(100);
            }
            if (!reloaded) throw new InvalidOperationException("MHS re-open verification failed: " + kind);
            if (kind == CxVisual.Mechanism.Core.Modules.MFMModuleKind.Connector)
                window.Dispatcher.Invoke(new Action(() => {
                    System.Windows.Controls.TabControl tabs =
                        window.DocumentHost.Content as System.Windows.Controls.TabControl;
                    TCTManualModuleFrame frame = tabs.Items
                        .OfType<System.Windows.Controls.TabItem>().Select(x => x.Content)
                        .OfType<TCTManualModuleFrame>().Single(candidate => candidate.ActiveModuleId == moduleId);
                    MCNConnectorConfiguration snapshot = frame.ConnectorDraftSnapshot();
                    if (snapshot.Protocol.Points.Count != 1 ||
                        snapshot.Protocol.Mappings.Count != 1 ||
                        snapshot.Protocol.Mappings[0].PointRef != "point01" ||
                        snapshot.Protocol.Mappings[0].TargetRef != "line/temperature")
                        throw new InvalidOperationException("真实MHS保存重开未保留PointTable/DataMapping");
                }));
            if (kind == CxVisual.Mechanism.Core.Modules.MFMModuleKind.Connector)
                window.Dispatcher.Invoke(new Action(() => {
                    System.Windows.Controls.TabControl tabs =
                        window.DocumentHost.Content as System.Windows.Controls.TabControl;
                    tabs.Items.OfType<System.Windows.Controls.TabItem>().Select(x => x.Content)
                        .OfType<TCTManualModuleFrame>().Single(candidate => candidate.ActiveModuleId == moduleId).SelectModulePage("channels");
                }));
            InvokePattern validate = Find(root, "TCT.Manual.Action.module.validate")
                .GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
            if (validate == null) throw new InvalidOperationException("Module validation unavailable");
            validate.Invoke();
            // 真实WPF Tab切换会Unloaded临时内容；回来后配置编辑器必须仍可读取
            // 同一MHS草稿，不得在离开页面时Dispose或另建第二套Editor。
            window.Dispatcher.Invoke(new Action(() => {
                System.Windows.Controls.TabControl tabs =
                    window.DocumentHost.Content as System.Windows.Controls.TabControl;
                System.Windows.Controls.TabItem active = tabs.SelectedItem as System.Windows.Controls.TabItem;
                if (active == null || !(active.Content is TCTManualModuleFrame))
                    throw new InvalidOperationException("模块页在验证前丢失");
                tabs.SelectedIndex = 0;
                tabs.UpdateLayout();
                if (window.DocumentHost.ActiveMode != "概览" ||
                    window.WorkspaceTitle.Text != "测试主界面 / Overview")
                    throw new InvalidOperationException(
                        "切回概览仍残留上一个专业工作区标题");
                tabs.SelectedItem = active;
                tabs.UpdateLayout();
                TCTWorkspaceDescriptor selected = active.Tag as TCTWorkspaceDescriptor;
                if (selected == null || window.WorkspaceTitle.Text != selected.Title)
                    throw new InvalidOperationException(
                        "从概览恢复专业工作区未恢复正确标题");
                TCTManualModuleFrame frame = (TCTManualModuleFrame)active.Content;
                frame.ValidateCurrent();
                if (frame.StatusMessage.StartsWith("模块结构校验失败", StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "切换工作区后通用MPW配置编辑器失效：" + frame.StatusMessage);
            }));
            if (kind == CxVisual.Mechanism.Core.Modules.MFMModuleKind.Connector)
            {
                // 前面主动切换到概览再回到模块：属于跨工作区导航。
                // 专业工作区默认收起此前手动开启的Test Console，是预期行为。
                bool hiddenAfterNavigation = false;
                window.Dispatcher.Invoke(new Action(() =>
                    hiddenAfterNavigation = !window.IsTestConsoleVisible));
                if (!hiddenAfterNavigation)
                    throw new InvalidOperationException(
                        "从概览切回专业工作区未按设计自动收起测试控制台");
                // 测试人员继续执行模块操作，必须使用正式“视图”菜单重新打开。
                // 不在删除中途偷偷改Visibility，也不删减悬空映射及保存重开断言。
                ShowManualConsoleForVerification(window);
                TraceUiStage("Connector / console reopened via View menu after workspace switch");
                TraceUiStage("Connector / before deletion method");
                // UIA worker进入删除闭环的确切边界：区别JIT/入口异常与WPF Dispatcher/UIA阻塞。
                try
                {
                    VerifyProtocolCollectionRemoval(root, window, moduleId);
                }
                catch (Exception error)
                {
                    TraceUiStage("Connector / deletion failed: " + error.GetType().FullName +
                        " / " + error.Message);
                    throw;
                }
                TraceUiStage("Connector / completed");
            }
            return created.WorkspaceId;
        }

        // 通过实际MPW删除按钮删除正式Point与映射，再次保存、重开，检验不存在影子集合。
        private static void VerifyProtocolCollectionRemoval(AutomationElement root,
            TCTMainWindow window, string moduleId)
        {
            TraceUiStage("Connector deletion / before points tab");
            window.Dispatcher.Invoke(new Action(() => {
                System.Windows.Controls.TabControl tabs =
                    window.DocumentHost.Content as System.Windows.Controls.TabControl;
                tabs.Items.OfType<System.Windows.Controls.TabItem>().Select(x => x.Content)
                    .OfType<TCTManualModuleFrame>().Single(candidate => candidate.ActiveModuleId == moduleId).SelectModulePage("points");
            }));
            TraceUiStage("Connector deletion / points tab selected");
            InvokePattern removePoint = Find(root, "MPW.Collection.Remove.Protocol.Points[0]")
                .GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
            if (removePoint == null) throw new InvalidOperationException("Point删除按钮不可访问");
            TraceUiStage("Connector deletion / before remove point Invoke");
            removePoint.Invoke();
            TraceUiStage("Connector deletion / after remove point Invoke");
            // WPF UIA InvokePattern可早于按钮状态刷新返回；等待唯一MHS草稿实际删除，
            // 不能把尚未完成的UI异步投影误判为规约引用校验失败。
            bool pointRemoved = false, danglingMappingRetained = false;
            for (int attempt = 0; attempt < 60 && !pointRemoved; attempt++)
            {
                window.Dispatcher.Invoke(new Action(() => {
                    System.Windows.Controls.TabControl tabs =
                        window.DocumentHost.Content as System.Windows.Controls.TabControl;
                    TCTManualModuleFrame frame = tabs.Items.OfType<System.Windows.Controls.TabItem>()
                        .Select(x => x.Content).OfType<TCTManualModuleFrame>().Single(candidate => candidate.ActiveModuleId == moduleId);
                    MCNConnectorConfiguration draft = frame.ConnectorDraftSnapshot();
                    pointRemoved = draft.Protocol.Points.Count == 0;
                    danglingMappingRetained = draft.Protocol.Mappings.Count == 1;
                }));
                if (!pointRemoved) Thread.Sleep(100);
            }
            TraceUiStage("Connector deletion / point removed=" + pointRemoved + ", mapping remains=" + danglingMappingRetained);
            if (!pointRemoved || !danglingMappingRetained)
                throw new InvalidOperationException(
                    "Point删除未写回MHS或错误级联删除Mapping：PointDeleted=" + pointRemoved +
                    " MappingRetained=" + danglingMappingRetained);
            TraceUiStage("Connector deletion / dangling validation");
            string danglingStatus = null;
            window.Dispatcher.Invoke(new Action(() => {
                System.Windows.Controls.TabControl tabs =
                    window.DocumentHost.Content as System.Windows.Controls.TabControl;
                TCTManualModuleFrame frame = tabs.Items.OfType<System.Windows.Controls.TabItem>()
                    .Select(x => x.Content).OfType<TCTManualModuleFrame>().Single(candidate => candidate.ActiveModuleId == moduleId);
                frame.ValidateCurrent();
                danglingStatus = frame.StatusMessage;
                frame.SelectModulePage("mapping");
            }));
            if (string.IsNullOrEmpty(danglingStatus) || !danglingStatus.Contains("配置存在"))
                throw new InvalidOperationException("删除被引用Point后FCM未拒绝悬空映射");
            TraceUiStage("Connector deletion / mapping tab selected");
            InvokePattern removeMapping = Find(root, "MPW.Collection.Remove.Protocol.Mappings[0]")
                .GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
            if (removeMapping == null) throw new InvalidOperationException("Mapping删除按钮不可访问");
            TraceUiStage("Connector deletion / before remove mapping Invoke");
            removeMapping.Invoke();
            TraceUiStage("Connector deletion / after remove mapping Invoke");
            bool mappingRemoved = false;
            string repairedStatus = null;
            for (int attempt = 0; attempt < 60 && !mappingRemoved; attempt++)
            {
                window.Dispatcher.Invoke(new Action(() => {
                    System.Windows.Controls.TabControl tabs =
                        window.DocumentHost.Content as System.Windows.Controls.TabControl;
                    TCTManualModuleFrame frame = tabs.Items.OfType<System.Windows.Controls.TabItem>()
                        .Select(x => x.Content).OfType<TCTManualModuleFrame>().Single(candidate => candidate.ActiveModuleId == moduleId);
                    MCNConnectorConfiguration draft = frame.ConnectorDraftSnapshot();
                    mappingRemoved = draft.Protocol.Points.Count == 0 &&
                        draft.Protocol.Mappings.Count == 0;
                    if (mappingRemoved)
                    {
                        frame.ValidateCurrent();
                        repairedStatus = frame.StatusMessage;
                    }
                }));
                TraceUiStage("Connector deletion / mapping removed=" + mappingRemoved);
                if (!mappingRemoved) Thread.Sleep(100);
            }
            if (!mappingRemoved)
                throw new InvalidOperationException("删除DataMapping后同一MHS草稿仍有遗留");
            if (repairedStatus == null || repairedStatus.Contains("配置存在"))
                throw new InvalidOperationException("删除悬空映射后未恢复正式校验合法");
            window.Dispatcher.Invoke(new Action(() =>
                SaveScreenshot(window, "TestCenter-Protocol-P0-Deleted.png")));
            // MPW删除会刷新动态Schema，主窗口应保留当前专业工作区手动开启的
            // 测试控制台。先从WPF真实状态核实，再通过独立UIA执行保存，禁止
            // 依赖测试侧重新打开面板掩盖工作区重激活时丢失命令的产品缺陷。
            bool consoleVisible = false;
            bool saveDeclared = false;
            bool moduleStillActive = false;
            window.Dispatcher.Invoke(new Action(() => {
                consoleVisible = window.IsTestConsoleVisible;
                System.Windows.Controls.TabControl tabs =
                    window.DocumentHost.Content as System.Windows.Controls.TabControl;
                System.Windows.Controls.TabItem selected = tabs == null ? null :
                    tabs.SelectedItem as System.Windows.Controls.TabItem;
                TCTManualModuleFrame frame = selected == null ? null :
                    selected.Content as TCTManualModuleFrame;
                moduleStillActive = frame != null && frame.ActiveModuleId == moduleId;
                saveDeclared = frame != null && frame.Commands.Any(x => x.Id == "module.save");
            }));
            TraceUiStage("Connector deletion / pre-save console=" + consoleVisible +
                ", module=" + moduleStillActive + ", command=" + saveDeclared);
            if (!consoleVisible || !moduleStillActive || !saveDeclared)
                throw new InvalidOperationException(
                    "Connector删除后丢失当前工作区测试控制台或正式保存命令：" +
                    "Visible=" + consoleVisible + ", Active=" + moduleStillActive +
                    ", Save=" + saveDeclared);
            InvokePattern save = Find(root, "TCT.Manual.Action.module.save")
                .GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
            if (save == null) throw new InvalidOperationException("映射删除后的保存命令缺失");
            save.Invoke();
            bool saved = false;
            for (int attempt = 0; attempt < 100 && !saved; attempt++)
            {
                window.Dispatcher.Invoke(new Action(() => {
                    System.Windows.Controls.TabControl tabs =
                        window.DocumentHost.Content as System.Windows.Controls.TabControl;
                    TCTManualModuleFrame frame = tabs.Items.OfType<System.Windows.Controls.TabItem>()
                        .Select(x => x.Content).OfType<TCTManualModuleFrame>().Single(candidate => candidate.ActiveModuleId == moduleId);
                    if (!frame.IsBusy && (frame.StatusMessage.Contains("配置未保存") ||
                        frame.StatusMessage.Contains("保存操作失败")))
                        throw new InvalidOperationException("删除后MHS拒绝保存：" + frame.StatusMessage);
                    saved = !frame.IsBusy && frame.StatusMessage.StartsWith("配置已保存",
                        StringComparison.Ordinal);
                }));
                if (!saved) Thread.Sleep(100);
            }
            if (!saved) throw new InvalidOperationException("点表映射删除后保存超时");
            InvokePattern reopen = Find(root, "TCT.Manual.Action.module.reopen")
                .GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
            if (reopen == null) throw new InvalidOperationException("删除后重新打开命令缺失");
            TraceUiStage("Connector deletion / before reopen");
            reopen.Invoke();
            TraceUiStage("Connector deletion / after reopen");
            bool checkedAgain = false;
            for (int attempt = 0; attempt < 100 && !checkedAgain; attempt++)
            {
                window.Dispatcher.Invoke(new Action(() => {
                    System.Windows.Controls.TabControl tabs =
                        window.DocumentHost.Content as System.Windows.Controls.TabControl;
                    TCTManualModuleFrame frame = tabs.Items.OfType<System.Windows.Controls.TabItem>()
                        .Select(x => x.Content).OfType<TCTManualModuleFrame>().Single(candidate => candidate.ActiveModuleId == moduleId);
                    if (!frame.IsBusy && frame.StatusMessage.StartsWith("已创建/打开配置范围",
                        StringComparison.Ordinal))
                    {
                        MCNConnectorConfiguration savedConfig = frame.ConnectorDraftSnapshot();
                        checkedAgain = frame.ActiveModuleId == moduleId &&
                            savedConfig.Protocol.Points.Count == 0 &&
                            savedConfig.Protocol.Mappings.Count == 0;
                    }
                }));
                if (!checkedAgain) Thread.Sleep(100);
            }
            if (!checkedAgain) throw new InvalidOperationException(
                "真实MHS删除后重开，仍出现已删除的规约点或映射");
        }
        private static string InteractManualModule(IntPtr hwnd, TCTMainWindow window)
        {
            AutomationElement root = AutomationElement.FromHandle(hwnd);
            TCTWorkspaceDescriptor instance = null;
            window.Dispatcher.Invoke(new Action(() => {
                window.Explorer.InvokeCreateContextMenuForVerification("manual.module.connector");
                instance = window.FindOpenCreatedInstance("manual.module.connector");
            }));
            if (instance == null) throw new InvalidOperationException("Module instance was not created");
            ShowManualConsoleForVerification(window);
            string actualId = null;
            window.Dispatcher.Invoke(new Action(() =>
                actualId = window.CreatedConfigurationId(instance.WorkspaceId)));
            SelectionItemPattern selected = Find(root, "TCT.Workspace." + instance.WorkspaceId)
                .GetCurrentPattern(SelectionItemPattern.Pattern) as SelectionItemPattern;
            if (selected == null) throw new InvalidOperationException("Module instance is not selectable");
            selected.Select();
            InvokePattern open = Find(root, "TCT.Manual.Action.module.open")
                .GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
            if (open == null) throw new InvalidOperationException("Module configuration create action not available");
            open.Invoke();
            bool created = false;
            for (int attempt = 0; attempt < 90 && !created; attempt++)
            {
                window.Dispatcher.Invoke(new Action(() => {
                    System.Windows.Controls.TabControl tabs =
                        window.DocumentHost.Content as System.Windows.Controls.TabControl;
                    TCTManualModuleFrame frame = tabs == null ? null : tabs.Items
                        .OfType<System.Windows.Controls.TabItem>().Select(x => x.Content)
                        .OfType<TCTManualModuleFrame>().FirstOrDefault();
                    created = frame != null && frame.HasOpenedConfiguration &&
                        frame.ActiveModuleKind == CxVisual.Mechanism.Core.Modules.MFMModuleKind.Connector &&
                        frame.ActiveModuleId == actualId;
                }));
                if (!created) Thread.Sleep(100);
            }
            if (!created) throw new InvalidOperationException(
                "Real MHS module configuration / MPW automatic editor did not open");
            InvokePattern validate = Find(root, "TCT.Manual.Action.module.validate")
                .GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
            if (validate == null) throw new InvalidOperationException("Module validation action unavailable");
            validate.Invoke();
            return instance.WorkspaceId;
        }

        private static void InteractArchitectureSmoke(IntPtr hwnd, TCTMainWindow window)
        {
            AutomationElement root = AutomationElement.FromHandle(hwnd);
            ValuePattern search = Find(root, "TCT.Explorer.Search")
                .GetCurrentPattern(ValuePattern.Pattern) as ValuePattern;
            if (search == null) throw new InvalidOperationException("Explorer Search missing");
            search.SetValue("");
            TCTWorkspaceDescriptor smoke = null;
            window.Dispatcher.Invoke(new Action(() => {
                window.Explorer.InvokeCreateContextMenuForVerification("auto.architecture-smoke");
                smoke = window.FindOpenCreatedInstance("auto.architecture-smoke");
            }));
            if (smoke == null) throw new InvalidOperationException("ArchitectureSmoke instance not created");
            SelectionItemPattern select = Find(root, "TCT.Workspace." + smoke.WorkspaceId)
                .GetCurrentPattern(SelectionItemPattern.Pattern) as SelectionItemPattern;
            if (select == null) throw new InvalidOperationException("ArchitectureSmoke instance not selectable");
            select.Select();
            bool ready = false;
            window.Dispatcher.Invoke(new Action(() => {
                System.Windows.Controls.TabControl tabs =
                    window.DocumentHost.Content as System.Windows.Controls.TabControl;
                TCTArchitectureSmokeFrame frame = tabs == null ? null : tabs.Items
                    .OfType<System.Windows.Controls.TabItem>().Select(t => t.Content)
                    .OfType<TCTArchitectureSmokeFrame>().FirstOrDefault();
                if (frame == null) throw new InvalidOperationException(
                    "ArchitectureSmoke frame did not open");
                frame.SelectProfile("config-wpf");
                ready = !frame.IsRunning && frame.CurrentProfileId == "config-wpf";
            }));
            if (!ready) throw new InvalidOperationException("ArchitectureSmoke CLI mode not selected");
            InvokePattern run = Find(root, "TCT.Automated.Action.architecture.run")
                .GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
            if (run == null) throw new InvalidOperationException("ArchitectureSmoke Run button missing");
            run.Invoke();
            bool completed = false, succeeded = false;
            string details = "";
            for (int attempt = 0; attempt < 450 && !completed; attempt++)
            {
                window.Dispatcher.Invoke(new Action(() => {
                    System.Windows.Controls.TabControl tabs =
                        window.DocumentHost.Content as System.Windows.Controls.TabControl;
                    TCTArchitectureSmokeFrame frame = tabs == null ? null : tabs.Items
                        .OfType<System.Windows.Controls.TabItem>().Select(t => t.Content)
                        .OfType<TCTArchitectureSmokeFrame>().FirstOrDefault();
                    if (frame == null) throw new InvalidOperationException("ArchitectureSmoke frame disappeared");
                    if (!frame.IsRunning && frame.LastResult != null &&
                        frame.MessageCount > 0)
                    {
                        completed = true;
                        succeeded = frame.LastResult.Succeeded && frame.LastResult.ExitCode == 0;
                        details = frame.LastResult.Failure ?? "ExitCode=" + frame.LastResult.ExitCode;
                    }
                }));
                if (!completed) Thread.Sleep(100);
            }
            if (!completed || !succeeded)
                throw new InvalidOperationException("ArchitectureSmoke real CLI run failed: " +
                    (completed ? details : "no verified exit code and actual stdout/stderr within 45 seconds"));
        }
        /// <summary>Independent real HWND/UIA M36 acceptance; no unrelated lengthy Studio/NUnit screens.</summary>
        /// <summary>
        /// 命令工作区独立UIA验收：通过真实AutomationElement填写参数和点击右侧按钮，
        /// 验证Owner命令实际执行及历史观察，不用WPF直接调用模拟UI点击。
        /// </summary>
        /// <summary>
        /// Connector单工作区真实UIA诊断，直接复用全量测试的同一个执行函数，
        /// 不引入虚假数据或第二套编辑器，只缩短定位失败的验证路径。
        /// </summary>
        public static async Task RunConnectorAsync(TCTMainWindow window)
        {
            if (window == null) throw new ArgumentNullException("window");
            window.Show();
            await window.Dispatcher.InvokeAsync(() => window.UpdateLayout());
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) throw new InvalidOperationException("Connector UIA HWND unavailable");
            Directory.CreateDirectory(Path.Combine("Output","Debug","x64"));
            File.WriteAllText(Path.Combine("Output","Debug","x64","TestCenter-UIA-Stages.log"), "");
            TraceUiStage("Connector isolated UIA start");
            await Task.Run(() => InteractModuleKind(hwnd, window,
                CxVisual.Mechanism.Core.Modules.MFMModuleKind.Connector));
            SaveScreenshot(window, "TestCenter-Connector-UIA.png");
            TraceUiStage("Connector isolated UIA complete");
        }

        public static async Task RunCommandAsync(TCTMainWindow window)
        {
            if (window == null) throw new ArgumentNullException("window");
            window.Show();
            await window.Dispatcher.InvokeAsync(() => window.UpdateLayout());
            TCTWorkspaceDescriptor created = window.CreateWorkspaceInstance("manual.command");
            if (created == null) throw new InvalidOperationException("命令页不能创建实例");
            ShowManualConsoleForVerification(window);
            System.Windows.Controls.TabControl tabs =
                window.DocumentHost.Content as System.Windows.Controls.TabControl;
            TCTManualCommandFrame frame =
                (tabs.SelectedItem as System.Windows.Controls.TabItem)?.Content
                    as TCTManualCommandFrame;
            if (frame == null) throw new InvalidOperationException("命令Frame未装配到中央Tab");
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) throw new InvalidOperationException("TestCenter无UIA HWND");

            await Task.Run(() =>
            {
                AutomationElement root = AutomationElement.FromHandle(hwnd);
                AutomationElement tree = Find(root, "TCT.Command.Catalog");
                if (tree.Current.ControlType != ControlType.Tree)
                    throw new InvalidOperationException("命令目标目录未暴露正式UIA Tree");
                AutomationElement input = Find(root, "TCT.Command.Input.Input");
                ValuePattern value = input.GetCurrentPattern(ValuePattern.Pattern) as ValuePattern;
                if (value == null) throw new InvalidOperationException("命令Input未暴露ValuePattern");
                value.SetValue("4");
                AutomationElement invoke = Find(root, "TCT.Manual.Action.command.invoke");
                InvokePattern action = invoke.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
                if (action == null) throw new InvalidOperationException("Command Invoke按钮无法UIA操作");
                action.Invoke();
            }).ConfigureAwait(true);

            bool completed = false;
            for (int i = 0; i < 120; i++)
            {
                CxVisual.Mechanism.Runtime.Commands.MRKCommandResponse actual = frame.LastResponse;
                if (actual != null && actual.State ==
                    CxVisual.Mechanism.Runtime.Commands.MRKCommandState.Completed &&
                    actual.Outputs.ContainsKey("Output") &&
                    (double)actual.Outputs["Output"] == 8.0)
                { completed = true; break; }
                await Task.Delay(25).ConfigureAwait(true);
            }
            if (!completed) throw new InvalidOperationException(
                "UIA输入并调用后，没有拿到正式CommandCenter的Output=8");
            int initial = frame.HistoryCount;
            await Task.Run(() =>
            {
                AutomationElement root = AutomationElement.FromHandle(hwnd);
                AutomationElement query = Find(root, "TCT.Manual.Action.command.query");
                InvokePattern pattern = query.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
                if (pattern == null) throw new InvalidOperationException("远程/本地Query操作按钮缺少UIA");
                pattern.Invoke();
                // WPF仅为当前选中Tab实例化可见内容。必须由真实UIA选择历史页，
                // 不能在尚未展开的Tab中虚构可访问的DataGrid。
                AutomationElement historyTab = Find(root, "TCT.Command.Tab.History");
                SelectionItemPattern select = historyTab.GetCurrentPattern(
                    SelectionItemPattern.Pattern) as SelectionItemPattern;
                if (select == null)
                    throw new InvalidOperationException("人工证据Tab不支持UIA SelectionItemPattern");
                select.Select();
                AutomationElement table = Find(root, "TCT.Command.History");
                if (table.Current.ControlType != ControlType.DataGrid)
                    throw new InvalidOperationException("真实人工会话记录未呈现在DataGrid");
            }).ConfigureAwait(true);
            for (int i = 0; i < 100 && frame.HistoryCount <= initial; i++)
                await Task.Delay(20).ConfigureAwait(true);
            if (frame.HistoryCount <= initial)
                throw new InvalidOperationException("UIA Query没有追加真实Operation观察记录");
            Console.WriteLine("PASS TestCenter Command UIA: tree/ValuePattern/InvokePattern/MRK result/Query/history");
        }

        public static async Task RunMessageAsync(TCTMainWindow window)
        {
            if(window==null)throw new ArgumentNullException("window");
            window.Show();
            await window.Dispatcher.InvokeAsync(() => window.UpdateLayout());
            IntPtr hwnd=new WindowInteropHelper(window).Handle;
            if(hwnd==IntPtr.Zero)throw new InvalidOperationException("Message TestCenter HWND absent");
            await Task.Run(() => InteractManualMessage(hwnd,window));
            SaveScreenshot(window,"TestCenter-13-Message.png");
            window.Dispatcher.Invoke(new Action(() =>
            {
                TCTWorkspaceDescriptor message=window.FindCreatedInstance("manual.message");
                if(message==null || !window.DocumentHost.CloseWorkspace(message.WorkspaceId))
                    throw new InvalidOperationException("Message instance did not release its Tab");
            }));
        }

        private static void InteractManualMessage(IntPtr hwnd, TCTMainWindow window)
        {
            window.Dispatcher.Invoke(new Action(() =>
            {
                window.Explorer.InvokeCreateContextMenuForVerification("manual.message");
                if (window.FindOpenCreatedInstance("manual.message") == null)
                    throw new InvalidOperationException("Manual message workspace not created");
            }));
            TCTWorkspaceDescriptor created=null;
            window.Dispatcher.Invoke(new Action(() =>
                created=window.FindCreatedInstance("manual.message")));
            if(created==null) throw new InvalidOperationException("Message workspace missing stable instance");
            ShowManualConsoleForVerification(window);
            AutomationElement root=AutomationElement.FromHandle(hwnd);
            SelectionItemPattern select=Find(root,"TCT.Workspace."+created.WorkspaceId)
                .GetCurrentPattern(SelectionItemPattern.Pattern) as SelectionItemPattern;
            if(select==null)throw new InvalidOperationException("Message instance selection unavailable");
            select.Select();
            InvokePattern subscribe=Find(root,"TCT.Manual.Action.message.subscribe")
                .GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
            if(subscribe==null)throw new InvalidOperationException("Message subscribe operation unavailable");
            subscribe.Invoke();
            InvokePattern publish=Find(root,"TCT.Manual.Action.message.publish")
                .GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
            if(publish==null)throw new InvalidOperationException("Message publish operation unavailable");
            publish.Invoke();

            bool delivered=false;
            for(int attempt=0;attempt<50&&!delivered;attempt++)
            {
                window.Dispatcher.Invoke(new Action(() =>
                {
                    System.Windows.Controls.TabControl tabs=
                        window.DocumentHost.Content as System.Windows.Controls.TabControl;
                    TCTManualMessageFrame page=tabs==null?null:tabs.Items
                        .OfType<System.Windows.Controls.TabItem>()
                        .Where(x=>(x.Tag as TCTWorkspaceDescriptor)?.WorkspaceId==created.WorkspaceId)
                        .Select(x=>x.Content).OfType<TCTManualMessageFrame>().FirstOrDefault();
                    delivered=page!=null&&page.ObservedCount>=2&&
                        page.StatusSummary.Contains("Delivered");
                }));
                if(!delivered)Thread.Sleep(100);
            }
            if(!delivered)throw new InvalidOperationException(
                "Message TestCenter UIA did not actually deliver message via M36 Owner");

            // 原UIA只覆盖Publish；追加真实Request/Reply及Topic切换后的本地应答重绑定。
            Thread.Sleep(150);
            InvokePattern request=Find(root,"TCT.Manual.Action.message.request")
                .GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
            if(request==null)throw new InvalidOperationException("Message request action unavailable");
            request.Invoke();
            bool replied=false;
            for(int attempt=0;attempt<50&&!replied;attempt++)
            {
                window.Dispatcher.Invoke(new Action(() =>
                {
                    System.Windows.Controls.TabControl tabs=
                        window.DocumentHost.Content as System.Windows.Controls.TabControl;
                    TCTManualMessageFrame page=tabs==null?null:tabs.Items
                        .OfType<System.Windows.Controls.TabItem>()
                        .Where(x=>(x.Tag as TCTWorkspaceDescriptor)?.WorkspaceId==created.WorkspaceId)
                        .Select(x=>x.Content).OfType<TCTManualMessageFrame>().FirstOrDefault();
                    replied=page!=null && page.ObservedCount>=3 &&
                        page.StatusSummary.Contains("本地测试应答");
                }));
                if(!replied)Thread.Sleep(100);
            }
            if(!replied)throw new InvalidOperationException(
                "Message TestCenter UIA did not receive local correlated reply");

            ValuePattern topic=Find(root,"TCT.Message.Topic")
                .GetCurrentPattern(ValuePattern.Pattern) as ValuePattern;
            if(topic==null)throw new InvalidOperationException("Message Topic is not editable by UIA");
            topic.SetValue("/machine/1201/app/XRay/module/Camera2/message/Captured");
            Thread.Sleep(150);
            request=Find(root,"TCT.Manual.Action.message.request")
                .GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
            if(request==null)throw new InvalidOperationException("Message request action lost after Topic update");
            request.Invoke();
            bool rebound=false;
            for(int attempt=0;attempt<50&&!rebound;attempt++)
            {
                window.Dispatcher.Invoke(new Action(() =>
                {
                    System.Windows.Controls.TabControl tabs=
                        window.DocumentHost.Content as System.Windows.Controls.TabControl;
                    TCTManualMessageFrame page=tabs==null?null:tabs.Items
                        .OfType<System.Windows.Controls.TabItem>()
                        .Where(x=>(x.Tag as TCTWorkspaceDescriptor)?.WorkspaceId==created.WorkspaceId)
                        .Select(x=>x.Content).OfType<TCTManualMessageFrame>().FirstOrDefault();
                    rebound=page!=null && page.ObservedCount>=4 &&
                        page.StatusSummary.Contains("本地测试应答");
                }));
                if(!rebound)Thread.Sleep(100);
            }
            if(!rebound)throw new InvalidOperationException(
                "Message TestCenter UIA local responder did not rebind changed Topic");
        }

        private static void InteractManualRoot(IntPtr hwnd, TCTMainWindow window)
        {
            AutomationElement root = AutomationElement.FromHandle(hwnd);
            ValuePattern search = Find(root, "TCT.Explorer.Search")
                .GetCurrentPattern(ValuePattern.Pattern) as ValuePattern;
            if (search == null) throw new InvalidOperationException("Workspace search unavailable");
            search.SetValue("");
            SelectionItemPattern select = Find(root, "TCT.Workspace.manual.root")
                .GetCurrentPattern(SelectionItemPattern.Pattern) as SelectionItemPattern;
            if (select == null) throw new InvalidOperationException("Manual root does not select");
            select.Select();
            // 分类节点不能隐式生成Tab；从右键“创建实例”才建立持久化树子节点。
            window.Dispatcher.Invoke(new Action(() => {
                System.Windows.Controls.TabControl tabs =
                    window.DocumentHost.Content as System.Windows.Controls.TabControl;
                if (tabs.Items.OfType<System.Windows.Controls.TabItem>()
                    .Any(t => (t.Tag as TCTWorkspaceDescriptor)?.WorkspaceId == "manual.root"))
                    throw new InvalidOperationException("根分类不能自动创建工作区Tab");
                window.Explorer.InvokeCreateContextMenuForVerification("manual.channel");
                if (window.FindOpenCreatedInstance("manual.channel") == null)
                    throw new InvalidOperationException("人工Channel右键创建失败");
            }));
            TCTWorkspaceDescriptor created = null;
            window.Dispatcher.Invoke(new Action(() =>
                created = window.FindCreatedInstance("manual.channel")));
            if (created == null || Find(root, "TCT.Workspace." + created.WorkspaceId) == null)
                throw new InvalidOperationException("创建的人工Channel未保留在树中");
        }
        private static void InteractManualChannel(IntPtr hwnd, TCTMainWindow window)
        {
            AutomationElement root = AutomationElement.FromHandle(hwnd);
            TCTWorkspaceDescriptor created = null;
            window.Dispatcher.Invoke(new Action(() =>
                created = window.FindCreatedInstance("manual.channel")));
            SelectionItemPattern selected = Find(root, "TCT.Workspace." + created.WorkspaceId)
                .GetCurrentPattern(SelectionItemPattern.Pattern) as SelectionItemPattern;
            if (selected == null) throw new InvalidOperationException("Channel registry leaf cannot select");
            selected.Select();
            ShowManualConsoleForVerification(window);
            InvokePattern connect = Find(root, "TCT.Manual.Action.connect")
                .GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
            if (connect == null) throw new InvalidOperationException("Channel Connect has no InvokePattern");
            connect.Invoke();
            InvokePattern send = Find(root, "TCT.Manual.Action.send")
                .GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
            if (send == null) throw new InvalidOperationException("Channel Send has no InvokePattern");
            send.Invoke();
            // WPF UIAutomation InvokePattern may return before the Dispatcher has
            // completed the button action. Observe the production Owner state with
            // a bounded wait; never manufacture a receive event or pass result.
            bool verified = false;
            for (int attempt = 0; attempt < 50 && !verified; attempt++)
            {
                window.Dispatcher.Invoke(new Action(() =>
                {
                    System.Windows.Controls.TabControl tabs =
                        window.DocumentHost.Content as System.Windows.Controls.TabControl;
                    TCTManualChannelFrame page = tabs == null ? null : tabs.Items
                        .OfType<System.Windows.Controls.TabItem>().Select(t => t.Content)
                        .OfType<TCTManualChannelFrame>().FirstOrDefault();
                    verified = page != null && page.IsConnected && page.FrameCount >= 2;
                }));
                if (!verified) Thread.Sleep(100);
            }
            if (!verified) throw new InvalidOperationException(
                "Manual Virtual Channel did not capture real production TX/RX");
            InvokePattern disconnect = Find(root, "TCT.Manual.Action.disconnect")
                .GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
            if (disconnect == null) throw new InvalidOperationException("Disconnect unavailable");
            disconnect.Invoke();
            bool closed = false;
            for (int attempt = 0; attempt < 50 && !closed; attempt++)
            {
                window.Dispatcher.Invoke(new Action(() =>
                {
                    System.Windows.Controls.TabControl tabs =
                        window.DocumentHost.Content as System.Windows.Controls.TabControl;
                    TCTManualChannelFrame page = tabs == null ? null : tabs.Items
                        .OfType<System.Windows.Controls.TabItem>().Select(t => t.Content)
                        .OfType<TCTManualChannelFrame>().FirstOrDefault();
                    closed = page != null && !page.IsConnected && page.FrameCount >= 2;
                }));
                if (!closed) Thread.Sleep(100);
            }
            if (!closed) throw new InvalidOperationException("Manual disconnect did not close Owner");
            // 与真实右侧操作中心连通的三拓扑P0验收，不打开未经授权的物理Socket。
            MCCSubChannelAddress savedPeer = null;
            window.Dispatcher.Invoke(new Action(() => {
                System.Windows.Controls.TabControl tabs =
                    window.DocumentHost.Content as System.Windows.Controls.TabControl;
                TCTManualChannelFrame page = tabs.Items.OfType<System.Windows.Controls.TabItem>()
                    .Select(x => x.Content).OfType<TCTManualChannelFrame>().Single();
                page.SelectChannelKind(MCCChannelKind.TcpServer);
                page.SetPeerInput("client01", "127.0.0.1:10001");
                if (page.SelectedTopology != MCCChannelTopology.MultiPeer)
                    throw new InvalidOperationException("MultiPeer Channel类型选择不可用");
            }));
            if (Find(root, "TCT.Channel.Kind").Current.ControlType != ControlType.ComboBox)
                throw new InvalidOperationException("Channel类型选择器必须可由UIA访问");
            InvokePattern register = Find(root, "TCT.Manual.Action.channel.register-peer")
                .GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
            if (register == null) throw new InvalidOperationException("SubChannel register不可操作");
            register.Invoke();
            bool peerRegistered = false;
            for (int attempt = 0; attempt < 50 && !peerRegistered; attempt++)
            {
                window.Dispatcher.Invoke(new Action(() => {
                    System.Windows.Controls.TabControl tabs =
                        window.DocumentHost.Content as System.Windows.Controls.TabControl;
                    TCTManualChannelFrame page = tabs.Items.OfType<System.Windows.Controls.TabItem>()
                        .Select(x => x.Content).OfType<TCTManualChannelFrame>().Single();
                    savedPeer = page.SelectedPeerAddress;
                    peerRegistered = savedPeer != null && page.PeerCount == 1 &&
                        page.IsLiveSubChannel(savedPeer);
                }));
                if (!peerRegistered) Thread.Sleep(100);
            }
            if (!peerRegistered)
            {
                string diagnostic = "";
                window.Dispatcher.Invoke(new Action(() => {
                    System.Windows.Controls.TabControl tabs =
                        window.DocumentHost.Content as System.Windows.Controls.TabControl;
                    TCTManualChannelFrame page = tabs.Items.OfType<System.Windows.Controls.TabItem>()
                        .Select(x => x.Content).OfType<TCTManualChannelFrame>().Single();
                    diagnostic = " Kind=" + page.SelectedChannelKind +
                        " Topology=" + page.SelectedTopology + " PeerCount=" + page.PeerCount +
                        " Selected=" + (page.SelectedPeerAddress == null ? "null" :
                            page.SelectedPeerAddress.ToString()) +
                        " Status=" + page.StatusMessage;
                }));
                throw new InvalidOperationException(
                    "正式SubChannel登记命令未执行或没有返回存活代次：" + diagnostic);
            }
            window.Dispatcher.Invoke(new Action(() => {
                System.Windows.Controls.TabControl tabs =
                    window.DocumentHost.Content as System.Windows.Controls.TabControl;
                TCTManualChannelFrame page = tabs.Items.OfType<System.Windows.Controls.TabItem>()
                    .Select(x => x.Content).OfType<TCTManualChannelFrame>().Single();
                bool duplicateRejected = false;
                try { page.RegisterSubChannel(); }
                catch (InvalidOperationException) { duplicateRejected = true; }
                if (!duplicateRejected || page.PeerCount != 1)
                    throw new InvalidOperationException("重复对端Id未按正式Owner拒绝");
            }));
            InvokePattern remove = Find(root, "TCT.Manual.Action.channel.remove-peer")
                .GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
            if (remove == null) throw new InvalidOperationException("SubChannel Remove不可操作");
            remove.Invoke();
            bool peerRemoved = false;
            for (int attempt = 0; attempt < 50 && !peerRemoved; attempt++)
            {
                window.Dispatcher.Invoke(new Action(() => {
                    System.Windows.Controls.TabControl tabs =
                        window.DocumentHost.Content as System.Windows.Controls.TabControl;
                    TCTManualChannelFrame page = tabs.Items.OfType<System.Windows.Controls.TabItem>()
                        .Select(x => x.Content).OfType<TCTManualChannelFrame>().Single();
                    peerRemoved = page.PeerCount == 0 && !page.IsLiveSubChannel(savedPeer);
                }));
                if (!peerRemoved) Thread.Sleep(100);
            }
            if (!peerRemoved) throw new InvalidOperationException("移除对端后旧Generation仍可寻址");
            window.Dispatcher.Invoke(new Action(() => {
                System.Windows.Controls.TabControl tabs =
                    window.DocumentHost.Content as System.Windows.Controls.TabControl;
                TCTManualChannelFrame page = tabs.Items.OfType<System.Windows.Controls.TabItem>()
                    .Select(x => x.Content).OfType<TCTManualChannelFrame>().Single();
                page.VerifyInvalidOperation();
                if (!page.LastDiagnostic.Contains("旧代次"))
                    throw new InvalidOperationException("MultiPeer失效路由反证未运行");
            }));
            window.Dispatcher.Invoke(new Action(() => SaveScreenshot(window, "TestCenter-Channel-P0-Multi.png")));
            window.Dispatcher.Invoke(new Action(() => {
                System.Windows.Controls.TabControl tabs =
                    window.DocumentHost.Content as System.Windows.Controls.TabControl;
                TCTManualChannelFrame page = tabs.Items.OfType<System.Windows.Controls.TabItem>()
                    .Select(x => x.Content).OfType<TCTManualChannelFrame>().Single();
                page.SelectChannelKind(MCCChannelKind.Serial);
                page.SetStationInput("7");
                if (page.SelectedTopology != MCCChannelTopology.SharedBus)
                    throw new InvalidOperationException("Serial拓扑不是SharedBus");
            }));
            InvokePattern addStation = Find(root, "TCT.Manual.Action.channel.add-station")
                .GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
            if (addStation == null) throw new InvalidOperationException("SharedBus站号编辑不可用");
            addStation.Invoke();
            bool stationRegistered = false;
            for (int attempt = 0; attempt < 50 && !stationRegistered; attempt++)
            {
                window.Dispatcher.Invoke(new Action(() => {
                    System.Windows.Controls.TabControl tabs =
                        window.DocumentHost.Content as System.Windows.Controls.TabControl;
                    TCTManualChannelFrame page = tabs.Items.OfType<System.Windows.Controls.TabItem>()
                        .Select(x => x.Content).OfType<TCTManualChannelFrame>().Single();
                    stationRegistered = page.StationCount == 1 && page.PeerCount == 0;
                }));
                if (!stationRegistered) Thread.Sleep(100);
            }
            if (!stationRegistered)
                throw new InvalidOperationException("SharedBus没有把站号加到正式Protocol点表");
            window.Dispatcher.Invoke(new Action(() => {
                System.Windows.Controls.TabControl tabs =
                    window.DocumentHost.Content as System.Windows.Controls.TabControl;
                TCTManualChannelFrame page = tabs.Items.OfType<System.Windows.Controls.TabItem>()
                    .Select(x => x.Content).OfType<TCTManualChannelFrame>().Single();
                page.SetStationInput("248");
                bool invalidRejected = false;
                try { page.AddStation(); }
                catch (ArgumentException) { invalidRejected = true; }
                if (!invalidRejected || page.StationCount != 1)
                    throw new InvalidOperationException("无效Modbus站号未拒绝或修改了合法点表");
            }));
            InvokePattern removeStation = Find(root, "TCT.Manual.Action.channel.remove-station")
                .GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
            if (removeStation == null) throw new InvalidOperationException("站号删除操作缺失");
            removeStation.Invoke();
            bool stationRemoved = false;
            for (int attempt = 0; attempt < 50 && !stationRemoved; attempt++)
            {
                window.Dispatcher.Invoke(new Action(() => {
                    System.Windows.Controls.TabControl tabs =
                        window.DocumentHost.Content as System.Windows.Controls.TabControl;
                    TCTManualChannelFrame page = tabs.Items.OfType<System.Windows.Controls.TabItem>()
                        .Select(x => x.Content).OfType<TCTManualChannelFrame>().Single();
                    stationRemoved = page.StationCount == 0;
                }));
                if (!stationRemoved) Thread.Sleep(100);
            }
            if (!stationRemoved) throw new InvalidOperationException("删除协议站号失败");
            window.Dispatcher.Invoke(new Action(() => {
                System.Windows.Controls.TabControl tabs =
                    window.DocumentHost.Content as System.Windows.Controls.TabControl;
                TCTManualChannelFrame page = tabs.Items.OfType<System.Windows.Controls.TabItem>()
                    .Select(x => x.Content).OfType<TCTManualChannelFrame>().Single();
                page.VerifyInvalidOperation();
                page.SelectChannelKind(MCCChannelKind.TcpClient);
                if (page.IsConnected || page.SelectedTopology != MCCChannelTopology.SinglePeer)
                    throw new InvalidOperationException("TCPClient不得假装已联机");
            }));
            if (Find(root, "TCT.Manual.Action.connect").Current.IsEnabled)
                throw new InvalidOperationException("未授权TCPClient不应显示可连接");
            window.Dispatcher.Invoke(new Action(() => SaveScreenshot(window, "TestCenter-Channel-P0-SharedBus.png")));
        }
        /// <summary>专业工作区默认隐藏测试控制台；确需TCT命令的UIA主动通过视图菜单打开。</summary>
        private static void ShowManualConsoleForVerification(TCTMainWindow window)
        {
            window.Dispatcher.Invoke(new Action(() =>
            {
                if (!window.IsTestConsoleVisible)
                    window.MenuShowOperations.RaiseEvent(new RoutedEventArgs(
                        System.Windows.Controls.MenuItem.ClickEvent,window.MenuShowOperations));
                window.UpdateLayout();
                if (!window.IsTestConsoleVisible)
                    throw new InvalidOperationException("测试控制台按需打开失败");
            }));
        }

        private static void VerifyOverview(IntPtr hwnd, TCTMainWindow window)
        {
            AutomationElement root = AutomationElement.FromHandle(hwnd);
            if (root == null) throw new InvalidOperationException("Overview HWND unavailable");
            AutomationElement discovered = Find(root, "TCT.Overview.Discovered");
            // 新版测试控制台随自动测试工作区出现，概览不应长期占用右栏。
            window.Dispatcher.Invoke(new Action(() => {
                if (window.IsTestConsoleVisible)
                    throw new InvalidOperationException("Overview must not reserve global Test Console");
                window.MenuShowOperations.RaiseEvent(new RoutedEventArgs(
                    System.Windows.Controls.MenuItem.ClickEvent,window.MenuShowOperations));
                window.UpdateLayout();
            }));
            AutomationElement action = Find(root, "TCT.RunSelected");
            if (action.GetCurrentPattern(InvokePattern.Pattern) == null)
                throw new InvalidOperationException("Explicitly opened Test Console lacks real InvokePattern");
            window.Dispatcher.Invoke(new Action(() => {
                window.HideTestConsoleButton.RaiseEvent(new RoutedEventArgs(
                    System.Windows.Controls.Button.ClickEvent,window.HideTestConsoleButton));
                window.UpdateLayout();
                if (window.IsTestConsoleVisible)
                    throw new InvalidOperationException("Close Test Console must release central workspace width");
            }));
            // UIAutomation COM属性必须留在UIA工作线程读取；放进Dispatcher会自锁至超时。
            string renderedCount = discovered.Current.Name;
            window.Dispatcher.Invoke(new Action(() => {
                if (window.DocumentHost.ActiveMode != "概览" ||
                    window.DocumentHost.Overview.DiscoveredCount <= 0 ||
                    renderedCount != window.DocumentHost.Overview.DiscoveredCount.ToString())
                    throw new InvalidOperationException("Overview is not bound to real test discovery");
                // High-legibility baseline is tested against the actual running WPF window.
                // Never accept a 11-12 DIP window or a hidden ScaleTransform workaround.
                if (window.FontSize < 14 ||
                    System.Windows.Media.TextOptions.GetTextFormattingMode(window) !=
                        System.Windows.Media.TextFormattingMode.Display)
                    throw new InvalidOperationException("TestCenter readable WPF text baseline regressed");
                if (window.BreadcrumbBand.Visibility != Visibility.Collapsed ||
                    window.BreadcrumbBand.ActualHeight > 0.5)
                    throw new InvalidOperationException("Removed Overview banner is still visible");
                if (window.TestDock.AnchorableHeaderTemplate == null ||
                    !ReferenceEquals(window.TestDock.AnchorableHeaderTemplate,
                        Application.Current.FindResource("MPWToolDockHeaderTemplate")))
                    throw new InvalidOperationException("Results dock tabs must use shared MPW tool style");
                System.Windows.Controls.TabControl documentTabs =
                    window.DocumentHost.Content as System.Windows.Controls.TabControl;
                if (documentTabs == null ||
                    !ReferenceEquals(documentTabs.ItemContainerStyle,
                        Application.Current.FindResource("MPWDocumentTabStyle")))
                    throw new InvalidOperationException("Document tabs must use the same shared MPW style");
                if (!(Application.Current.FindResource("MPWTreeItemStyle") is Style) ||
                    !(Application.Current.FindResource("MPWDataGridStyle") is Style))
                    throw new InvalidOperationException("Shared MPW Tree/DataGrid templates not loaded");
                DpiScale dpi = VisualTreeHelper.GetDpi(window);
                if (dpi.DpiScaleX < 0.95 || dpi.DpiScaleY < 0.95)
                    throw new InvalidOperationException("Invalid WPF DPI scale");
            }));
        }
        private static T FindWpfByAutomationId<T>(DependencyObject element, string id)
            where T : FrameworkElement
        {
            T candidate = element as T;
            if (candidate != null &&
                System.Windows.Automation.AutomationProperties.GetAutomationId(candidate) == id)
                return candidate;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                T result = FindWpfByAutomationId<T>(VisualTreeHelper.GetChild(element, i), id);
                if (result != null) return result;
            }
            return null;
        }
        private static AutomationElement Find(AutomationElement root, string id)
        {
            AutomationElement found = null;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                found = root.FindFirst(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.AutomationIdProperty, id));
                if (found != null) break;
                Thread.Sleep(150);
            }
            if (found == null) throw new InvalidOperationException("UIA control not found: " + id);
            return found;
        }
        private static void Interact(IntPtr hwnd, TCTMainWindow window)
        {
            window.Dispatcher.Invoke(new Action(() => window.FocusAutomatedTest("TCT.M2.GEOMETRY.001")));
            AutomationElement root = AutomationElement.FromHandle(hwnd);
            if (root == null) throw new InvalidOperationException("UIA HWND not available");
            AutomationElement search = Find(root, "TCT.Explorer.Search");
            ValuePattern input = search.GetCurrentPattern(ValuePattern.Pattern) as ValuePattern;
            if (input == null || input.Current.IsReadOnly)
                throw new InvalidOperationException("Explorer does not expose editable ValuePattern");
            input.SetValue("TCT.M2.GEOMETRY.001");
            AutomationElement treeItem = Find(root, "TCT.Case.TCT.M2.GEOMETRY.001");
            SelectionItemPattern selector = treeItem.GetCurrentPattern(SelectionItemPattern.Pattern)
                as SelectionItemPattern;
            if (selector == null) throw new InvalidOperationException("Case selection pattern missing");
            selector.Select();
            // 当前目录用例选择由计划页与Inspector承载，不强求隐藏的旧Ribbon。
            AutomationElement run = Find(root, "TCT.Manual.Action.plan.runselected");
            InvokePattern invoke = run.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
            if (invoke == null) throw new InvalidOperationException("Run Selected invoke pattern missing");
            invoke.Invoke();

            bool observed = false;
            for (int attempt = 0; attempt < 240; attempt++)
            {
                bool result = false;
                window.Dispatcher.Invoke(new Action(() => {
                    result = window.Results.ResultCount >= 1 &&
                        window.DocumentHost.ViewerCount >= 1 &&
                        window.MenuRunSelected.IsEnabled &&
                        window.Inspector.SelectedObservationTitle == null;
                }));
                if (result) { observed = true; break; }
                Thread.Sleep(200);
            }
            if (!observed)
                throw new TimeoutException("UIA invocation did not lead to verified result and Geometry viewer");
            bool proof = false;
            window.Dispatcher.Invoke(new Action(() => {
                var tabs = window.DocumentHost.Content as System.Windows.Controls.TabControl;
                if (tabs == null) return;
                TCTGeometryTestView geometry = tabs.Items.OfType<System.Windows.Controls.TabItem>()
                    .Select(x => x.Content).OfType<TCTGeometryTestView>().FirstOrDefault();
                proof = geometry != null && geometry.VerifiedFigureCount == 4 &&
                    geometry.EvidenceProblem == null;
            }));
            if (!proof) throw new InvalidOperationException("UIA ran test but did not show verified geometry");
        }
        private static void InteractObject(IntPtr hwnd, TCTMainWindow window)
        {
            window.Dispatcher.Invoke(new Action(() => window.FocusAutomatedTest("M-OBJ-007")));
            AutomationElement root = AutomationElement.FromHandle(hwnd);
            AutomationElement search = Find(root, "TCT.Explorer.Search");
            ValuePattern input = search.GetCurrentPattern(ValuePattern.Pattern) as ValuePattern;
            if (input == null) throw new InvalidOperationException("Object search has no ValuePattern");
            input.SetValue("M-OBJ-007");
            AutomationElement item = Find(root, "TCT.Case.M-OBJ-007");
            SelectionItemPattern select = item.GetCurrentPattern(SelectionItemPattern.Pattern)
                as SelectionItemPattern;
            if (select == null) throw new InvalidOperationException("Symbol case has no SelectionItemPattern");
            select.Select();
            // 当前目录用例选择由计划页与Inspector承载，不强求隐藏的旧Ribbon。
            AutomationElement run = Find(root, "TCT.Manual.Action.plan.runselected");
            InvokePattern invoke = run.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
            if (invoke == null) throw new InvalidOperationException("TestCenter run command not accessible");
            invoke.Invoke();
            for (int attempt = 0; attempt < 240; attempt++)
            {
                bool ready = false;
                window.Dispatcher.Invoke(new Action(() =>
                {
                    var tabs = window.DocumentHost.Content as System.Windows.Controls.TabControl;
                    if (tabs == null || !window.MenuRunSelected.IsEnabled) return;
                    TCTGeometryTestView geometry = tabs.Items.OfType<System.Windows.Controls.TabItem>()
                        .Select(tab => tab.Content).OfType<TCTGeometryTestView>().FirstOrDefault();
                    TCTObjectTestView objects = tabs.Items.OfType<System.Windows.Controls.TabItem>()
                        .Select(tab => tab.Content).OfType<TCTObjectTestView>().FirstOrDefault();
                    ready = geometry != null && objects != null &&
                        geometry.VerifiedFigureCount == 4 && geometry.EvidenceProblem == null &&
                        objects.VerifiedReferenceCount == 2 && objects.RelationDiagram != null &&
                        objects.RelationDiagram.VerifiedReferenceLines == 2 &&
                        objects.EvidenceProblem == null;
                }));
                if (ready)
                {
                    AutomationElement objectViewer = Find(root, "TCT.Viewer.ObjectTree");
                    SelectionItemPattern objectTab = objectViewer.GetCurrentPattern(
                        SelectionItemPattern.Pattern) as SelectionItemPattern;
                    if (objectTab == null)
                        throw new InvalidOperationException("Object viewer tab has no UIA selection pattern");
                    objectTab.Select();
                    bool selected = false;
                    window.Dispatcher.Invoke(new Action(() =>
                        selected = window.DocumentHost.ActiveMode == "ObjectTree"));
                    if (!selected)
                        throw new InvalidOperationException("Object screenshot is not showing ObjectTree tab");
                    return;
                }
                Thread.Sleep(200);
            }
            throw new TimeoutException("UIA executed Symbol case but did not display verified Geometry/Object evidence");
        }

        private static void InteractGraph(IntPtr hwnd, TCTMainWindow window)
        {
            window.Dispatcher.Invoke(new Action(() => window.FocusAutomatedTest("TCT.M3.GRAPHRUNTIME.001")));
            AutomationElement root = AutomationElement.FromHandle(hwnd);
            ValuePattern search = Find(root, "TCT.Explorer.Search")
                .GetCurrentPattern(ValuePattern.Pattern) as ValuePattern;
            if (search == null) throw new InvalidOperationException("Graph case search lacks ValuePattern");
            search.SetValue("TCT.M3.GRAPHRUNTIME.001");
            AutomationElement item = Find(root, "TCT.Case.TCT.M3.GRAPHRUNTIME.001");
            SelectionItemPattern selected = item.GetCurrentPattern(SelectionItemPattern.Pattern)
                as SelectionItemPattern;
            if (selected == null) throw new InvalidOperationException("Graph case cannot be selected");
            selected.Select();
            InvokePattern run = Find(root, "TCT.Manual.Action.plan.runselected")
                .GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
            if (run == null) throw new InvalidOperationException("Graph test run command unavailable");
            run.Invoke();
            bool ready = false;
            for (int attempt = 0; attempt < 240; attempt++)
            {
                window.Dispatcher.Invoke(new Action(() =>
                {
                    var tabs = window.DocumentHost.Content as System.Windows.Controls.TabControl;
                    if (tabs == null || !window.MenuRunSelected.IsEnabled) return;
                    TCTGraphTestView graph = tabs.Items.OfType<System.Windows.Controls.TabItem>()
                        .Select(x => x.Content).OfType<TCTGraphTestView>().FirstOrDefault();
                    TCTRuntimeTestView runtime = tabs.Items.OfType<System.Windows.Controls.TabItem>()
                        .Select(x => x.Content).OfType<TCTRuntimeTestView>().FirstOrDefault();
                    ready = graph != null && runtime != null &&
                        graph.VerifiedItems > 0 && graph.EvidenceProblem == null &&
                        runtime.VerifiedItems > 0 && runtime.EvidenceProblem == null;
                }));
                if (ready) break;
                Thread.Sleep(200);
            }
            if (!ready) throw new TimeoutException("UIA Graph/MRK test has no verified dual Owner viewers");
            SelectionItemPattern tab = Find(root, "TCT.Viewer.Graph")
                .GetCurrentPattern(SelectionItemPattern.Pattern) as SelectionItemPattern;
            if (tab == null) throw new InvalidOperationException("Graph tab has no UIA selection pattern");
            tab.Select();
            // Graph可视节点必须取自真实MOL；节点和编译Step均可双向定位Owner。
            InvokeFirstOwnerLink(root, "TCT.Graph.Block.");
            WaitForInspector(window, "MOL Definition Block", "Graph可视节点 → MOL Definition");
            InvokeFirstOwnerLink(root, "TCT.Graph.Step.");
            WaitForInspector(window, "SourceMap", "Graph UIA Step → Inspector/SourceMap");
        }

        private static void InteractRuntime(IntPtr hwnd, TCTMainWindow window)
        {
            AutomationElement root = AutomationElement.FromHandle(hwnd);
            SelectionItemPattern tab = Find(root, "TCT.Viewer.Runtime")
                .GetCurrentPattern(SelectionItemPattern.Pattern) as SelectionItemPattern;
            if (tab == null) throw new InvalidOperationException("Runtime tab lacks UIA selection pattern");
            tab.Select();
            InvokeFirstOwnerLink(root, "TCT.Runtime.Step.");
            WaitForInspector(window, "MRK Step", "Runtime UIA Step → MOL definition");
        }

        private static void InteractRuntimeJournal(IntPtr hwnd, TCTMainWindow window)
        {
            window.Dispatcher.Invoke(new Action(() => window.FocusAutomatedTest("TCT.M3.RUNTIME.001")));
            AutomationElement root = AutomationElement.FromHandle(hwnd);
            ValuePattern search = Find(root, "TCT.Explorer.Search")
                .GetCurrentPattern(ValuePattern.Pattern) as ValuePattern;
            if (search == null) throw new InvalidOperationException("MRK session search has no ValuePattern");
            search.SetValue("TCT.M3.RUNTIME.001");
            SelectionItemPattern selected = Find(root, "TCT.Case.TCT.M3.RUNTIME.001")
                .GetCurrentPattern(SelectionItemPattern.Pattern) as SelectionItemPattern;
            if (selected == null) throw new InvalidOperationException("MRK session test not selectable");
            selected.Select();
            InvokePattern run = Find(root, "TCT.Manual.Action.plan.runselected")
                .GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
            if (run == null) throw new InvalidOperationException("MRK Owner run command not invocable");
            run.Invoke();
            bool ready = false;
            for (int attempt = 0; attempt < 240; attempt++)
            {
                window.Dispatcher.Invoke(new Action(() => {
                    var tabs = window.DocumentHost.Content as System.Windows.Controls.TabControl;
                    if (tabs == null || !window.MenuRunSelected.IsEnabled) return;
                    TCTRuntimeTestView owner = tabs.Items.OfType<System.Windows.Controls.TabItem>()
                        .Select(x => x.Content).OfType<TCTRuntimeTestView>().FirstOrDefault();
                    ready = owner != null && owner.VerifiedJournalCount >= 3 &&
                        owner.EvidenceProblem == null;
                }));
                if (ready) break;
                Thread.Sleep(200);
            }
            if (!ready) throw new TimeoutException(
                "MRK RuntimeSession UIA did not show verified Session/Journal evidence");
            SelectionItemPattern tab = Find(root, "TCT.Viewer.Runtime")
                .GetCurrentPattern(SelectionItemPattern.Pattern) as SelectionItemPattern;
            if (tab == null) throw new InvalidOperationException("RuntimeOwner viewer UIA selection missing");
            tab.Select();
            InvokeFirstOwnerLink(root, "TCT.Runtime.Journal.");
            WaitForInspector(window, "MRK Journal", "MRK Journal → Inspector");
        }

        // InvokePattern invokes WPF controls asynchronously. Synchronize on actual Inspector
        // state rather than treating the first dispatcher snapshot as a completed UI action.
        private static void WaitForInspector(TCTMainWindow window, string expected, string operation)
        {
            string last = null, id = null;
            for (int attempt = 0; attempt < 50; attempt++)
            {
                bool updated = false;
                window.Dispatcher.Invoke(new Action(() => {
                    id = window.Inspector.SelectedObjectId;
                    last = window.Inspector.SelectedObjectDetails;
                    updated = !string.IsNullOrWhiteSpace(id) && last != null &&
                        last.Contains(expected);
                }));
                if (updated) return;
                Thread.Sleep(100);
            }
            throw new InvalidOperationException(operation + " failed; selected=" +
                (id ?? "<none>") + "; inspector=" + (last ?? "<none>"));
        }

        private static void InteractChannel(IntPtr hwnd, TCTMainWindow window)
        {
            window.Dispatcher.Invoke(new Action(() => window.FocusAutomatedTest("TCT.M3.CHANNEL.001")));
            AutomationElement root = AutomationElement.FromHandle(hwnd);
            ValuePattern search = Find(root, "TCT.Explorer.Search")
                .GetCurrentPattern(ValuePattern.Pattern) as ValuePattern;
            if (search == null) throw new InvalidOperationException("Channel search has no ValuePattern");
            search.SetValue("TCT.M3.CHANNEL.001");
            SelectionItemPattern selected = Find(root, "TCT.Case.TCT.M3.CHANNEL.001")
                .GetCurrentPattern(SelectionItemPattern.Pattern) as SelectionItemPattern;
            if (selected == null) throw new InvalidOperationException("Channel case lacks UIA selection");
            selected.Select();
            InvokePattern run = Find(root, "TCT.Manual.Action.plan.runselected")
                .GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
            if (run == null) throw new InvalidOperationException("Channel run button not invocable");
            run.Invoke();
            bool ready = false;
            for (int attempt = 0; attempt < 240; attempt++)
            {
                window.Dispatcher.Invoke(new Action(() => {
                    var tabs = window.DocumentHost.Content as System.Windows.Controls.TabControl;
                    if (tabs == null || !window.MenuRunSelected.IsEnabled) return;
                    TCTChannelTestView channelView = tabs.Items.OfType<System.Windows.Controls.TabItem>()
                        .Select(x => x.Content).OfType<TCTChannelTestView>().FirstOrDefault();
                    ready = channelView != null && channelView.VerifiedFrameCount >= 5 &&
                        channelView.VerifiedErrorCount >= 1 && channelView.EvidenceProblem == null;
                }));
                if (ready) break;
                Thread.Sleep(200);
            }
            if (!ready) throw new TimeoutException(
                "Channel UIA did not verify real TX/RX/error evidence from Connectivity");
            SelectionItemPattern tab = Find(root, "TCT.Viewer.Channel")
                .GetCurrentPattern(SelectionItemPattern.Pattern) as SelectionItemPattern;
            if (tab == null) throw new InvalidOperationException("Channel Viewer tab has no SelectionItemPattern");
            tab.Select();
            InvokeFirstOwnerLink(root, "TCT.Channel.Frame.");
            WaitForInspector(window, "Channel Frame", "Channel UIA Frame → Inspector");
        }

        private static void InvokeFirstOwnerLink(AutomationElement root, string prefix)
        {
            AutomationElementCollection buttons = root.FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
            foreach (AutomationElement element in buttons)
            {
                if (!element.Current.AutomationId.StartsWith(prefix, StringComparison.Ordinal)) continue;
                InvokePattern invoke = element.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
                if (invoke == null) throw new InvalidOperationException(prefix + " has no InvokePattern");
                invoke.Invoke();
                return;
            }
            throw new InvalidOperationException("No verified UIA Owner link found: " + prefix);
        }

        private static void SaveScreenshot(TCTMainWindow window, string name)
        {
            window.UpdateLayout();
            DpiScale dpi = VisualTreeHelper.GetDpi(window);
            // RenderTargetBitmap takes physical pixel dimensions. Passing logical
            // WPF DIPs with hardcoded 96 DPI makes high-DPI screenshots blurry.
            int width = (int)Math.Ceiling(window.ActualWidth * dpi.DpiScaleX);
            int height = (int)Math.Ceiling(window.ActualHeight * dpi.DpiScaleY);
            if (width < 500 || height < 350 || width > 8192 || height > 8192)
                throw new InvalidOperationException("UIA screenshot physical viewport invalid");
            RenderTargetBitmap pixels = new RenderTargetBitmap(width, height,
                96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
            pixels.Render(window);
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(pixels));
            string path = Path.Combine(Environment.CurrentDirectory, "Output", "Debug", "x64",
                name);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write))
                encoder.Save(stream);
        }
    }
}
