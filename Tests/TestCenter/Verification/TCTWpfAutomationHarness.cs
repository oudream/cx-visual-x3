using System;
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
        public static async Task RunAsync(TCTMainWindow window)
        {
            if (window == null) throw new ArgumentNullException("window");
            window.Show();
            await window.Dispatcher.InvokeAsync(() => window.UpdateLayout());
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) throw new InvalidOperationException("TestCenter has no HWND");
            await Task.Run(() => VerifyOverview(hwnd, window));
            SaveScreenshot(window, "TestCenter-01-Workspace.png");
            await Task.Run(() => Interact(hwnd, window));
            SaveScreenshot(window, "TestCenterUIA.png");
            SaveScreenshot(window, "TestCenter-02-Geometry.png");
            await Task.Run(() => InteractObject(hwnd, window));
            SaveScreenshot(window, "TestCenter-03-Object.png");
            await Task.Run(() => InteractGraph(hwnd, window));
            SaveScreenshot(window, "TestCenter-05-Graph.png");
            await Task.Run(() => InteractRuntime(hwnd, window));
            SaveScreenshot(window, "TestCenter-06-Runtime.png");
            await Task.Run(() => InteractChannel(hwnd, window));
            SaveScreenshot(window, "TestCenter-07-Channel.png");
            await Task.Run(() => InteractRuntimeJournal(hwnd, window));
            SaveScreenshot(window, "TestCenter-08-RuntimeJournal.png");
            window.MenuThemeDark.RaiseEvent(new RoutedEventArgs(
                System.Windows.Controls.MenuItem.ClickEvent, window.MenuThemeDark));
            SaveScreenshot(window, "TestCenter-04-Dark.png");
            window.MenuThemeLight.RaiseEvent(new RoutedEventArgs(
                System.Windows.Controls.MenuItem.ClickEvent, window.MenuThemeLight));
            await Task.Run(() => InteractManualRoot(hwnd, window));
            SaveScreenshot(window, "TestCenter-09-ManualRoot.png");
            await Task.Run(() => InteractManualChannel(hwnd, window));
            SaveScreenshot(window, "TestCenter-10-ManualChannel.png");
            window.Dispatcher.Invoke(new Action(() => {
                if (!window.DocumentHost.CloseWorkspace("manual.channel"))
                    throw new InvalidOperationException("Manual Channel tab could not close/dispose");
            }));
            await Task.Run(() => InteractManualModule(hwnd, window));
            SaveScreenshot(window, "TestCenter-12-ModuleConfig.png");
            foreach (CxVisual.Mechanism.Core.Modules.MFMModuleKind kind in
                new[] {
                    CxVisual.Mechanism.Core.Modules.MFMModuleKind.Connector,
                    CxVisual.Mechanism.Core.Modules.MFMModuleKind.Compute,
                    CxVisual.Mechanism.Core.Modules.MFMModuleKind.Simulation,
                    CxVisual.Mechanism.Core.Modules.MFMModuleKind.Service,
                     CxVisual.Mechanism.Core.Modules.MFMModuleKind.Driver
                })
            {
                await Task.Run(() => InteractModuleKind(hwnd, window, kind));
                SaveScreenshot(window, "TestCenter-Module-" + kind + ".png");
            }
            window.Dispatcher.Invoke(new Action(() => {
                if (!window.DocumentHost.CloseWorkspace("manual.module"))
                    throw new InvalidOperationException("Module configuration workspace did not close");
            }));
            await Task.Run(() => InteractArchitectureSmoke(hwnd, window));
            SaveScreenshot(window, "TestCenter-11-ArchitectureSmoke.png");
            window.Dispatcher.Invoke(new Action(() => {
                if (!window.DocumentHost.CloseWorkspace("auto.architecture-smoke"))
                    throw new InvalidOperationException("ArchitectureSmoke tab was not disposable");
            }));
        }
        private static void InteractModuleKind(IntPtr hwnd, TCTMainWindow window,
            CxVisual.Mechanism.Core.Modules.MFMModuleKind kind)
        {
            AutomationElement root = AutomationElement.FromHandle(hwnd);
            string id = "TCT.Workspace.manual.module." + kind.ToString().ToLowerInvariant();
            SelectionItemPattern node = Find(root, id)
                .GetCurrentPattern(SelectionItemPattern.Pattern) as SelectionItemPattern;
            if (node == null) throw new InvalidOperationException("Missing module tree type " + kind);
            node.Select();
            string moduleId = "Module" + kind + "01";
            bool opened = false;
            for (int attempt = 0; attempt < 100 && !opened; attempt++)
            {
                window.Dispatcher.Invoke(new Action(() => {
                    System.Windows.Controls.TabControl tabs =
                        window.DocumentHost.Content as System.Windows.Controls.TabControl;
                    TCTManualModuleFrame frame = tabs == null ? null : tabs.Items
                        .OfType<System.Windows.Controls.TabItem>().Select(x => x.Content)
                        .OfType<TCTManualModuleFrame>().FirstOrDefault();
                    opened = frame != null && !frame.IsBusy && frame.HasOpenedConfiguration &&
                        frame.ActiveModuleKind == kind && frame.ActiveModuleId == moduleId;
                }));
                if (!opened) Thread.Sleep(100);
            }
            if (!opened) throw new InvalidOperationException("FCM/MPW typed module editor did not load: " + kind);
            if (kind == CxVisual.Mechanism.Core.Modules.MFMModuleKind.Connector)
            {
                window.Dispatcher.Invoke(new Action(() => {
                    System.Windows.Controls.TabControl tabs =
                        window.DocumentHost.Content as System.Windows.Controls.TabControl;
                    tabs.Items.OfType<System.Windows.Controls.TabItem>().Select(x => x.Content)
                        .OfType<TCTManualModuleFrame>().Single().SelectModulePage("channels");
                }));
                AutomationElement cards = root.FindFirst(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.AutomationIdProperty,
                        "MPW.Channel.SchemaCards.0"));
                if (cards == null)
                {
                    InvokePattern add = Find(root, "MPW.Collection.Add.Channels")
                        .GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
                    if (add == null) throw new InvalidOperationException("MPW channel Add not invokable");
                    add.Invoke();
                }
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
                                .OfType<TCTManualModuleFrame>().Single();
                            rendered = frame.SelectedModulePage == "channels" &&
                                frame.VisibleSchemaEditorCount > 10;
                        }));
                    if (!rendered) Thread.Sleep(100);
                }
                if (!rendered) throw new InvalidOperationException(
                    "MCCChannelConfiguration three-column Schema editor not rendered");
                // 同一通用MPW可编辑规约的Channel引用，候选来自正式Channels。
                window.Dispatcher.Invoke(new Action(() => {
                    System.Windows.Controls.TabControl tabs =
                        window.DocumentHost.Content as System.Windows.Controls.TabControl;
                    tabs.Items.OfType<System.Windows.Controls.TabItem>().Select(x => x.Content)
                        .OfType<TCTManualModuleFrame>().Single().SelectModulePage("protocols");
                }));
                InvokePattern addProtocol = Find(root, "MPW.Collection.Add.Protocols")
                    .GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
                if (addProtocol == null) throw new InvalidOperationException(
                    "正式Protocol列表不能添加配置");
                addProtocol.Invoke();
                AutomationElement choices = Find(root, "MPW.Reference.Protocols[0].ChannelRef");
                if (choices.Current.ControlType != ControlType.ComboBox)
                    throw new InvalidOperationException("通道引用未提供正式草稿的动态可编辑选择器");
                window.Dispatcher.Invoke(new Action(() => {
                    System.Windows.Controls.TabControl tabs =
                        window.DocumentHost.Content as System.Windows.Controls.TabControl;
                    tabs.Items.OfType<System.Windows.Controls.TabItem>().Select(x => x.Content)
                        .OfType<TCTManualModuleFrame>().Single().SelectModulePage("channels");
                }));
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
                        .OfType<TCTManualModuleFrame>().Single();
                    frame.SelectModulePage("provider");
                    if (frame.VisibleSchemaEditorCount < 1)
                        throw new InvalidOperationException("Driver Provider 正式 FCM 配置没有显示");
                    frame.ValidateCurrent();
                    if (!frame.StatusMessage.Contains("DRIVER_PROVIDER_REQUIRED") &&
                        !frame.StatusMessage.Contains("DriverTypeId"))
                        throw new InvalidOperationException("Driver 没有 Provider 不应通过验证");
                }));
                return;
            }
            InvokePattern save = Find(root, "TCT.Manual.Action.module.save")
                .GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
            if (save == null) throw new InvalidOperationException("MHS Save command unavailable for " + kind);
            save.Invoke();
            bool saved = false;
            for (int attempt = 0; attempt < 100 && !saved; attempt++)
            {
                window.Dispatcher.Invoke(new Action(() => {
                    System.Windows.Controls.TabControl tabs =
                        window.DocumentHost.Content as System.Windows.Controls.TabControl;
                    TCTManualModuleFrame frame = tabs == null ? null : tabs.Items
                        .OfType<System.Windows.Controls.TabItem>().Select(x => x.Content)
                        .OfType<TCTManualModuleFrame>().FirstOrDefault();
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
            bool reloaded = false;
            for (int attempt = 0; attempt < 100 && !reloaded; attempt++)
            {
                window.Dispatcher.Invoke(new Action(() => {
                    System.Windows.Controls.TabControl tabs =
                        window.DocumentHost.Content as System.Windows.Controls.TabControl;
                    TCTManualModuleFrame frame = tabs == null ? null : tabs.Items
                        .OfType<System.Windows.Controls.TabItem>().Select(x => x.Content)
                        .OfType<TCTManualModuleFrame>().FirstOrDefault();
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
                    tabs.Items.OfType<System.Windows.Controls.TabItem>().Select(x => x.Content)
                        .OfType<TCTManualModuleFrame>().Single().SelectModulePage("channels");
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
                tabs.SelectedItem = active;
                tabs.UpdateLayout();
                TCTManualModuleFrame frame = (TCTManualModuleFrame)active.Content;
                frame.ValidateCurrent();
                if (frame.StatusMessage.StartsWith("模块结构校验失败", StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "切换工作区后通用MPW配置编辑器失效：" + frame.StatusMessage);
            }));
        }
        private static void InteractManualModule(IntPtr hwnd, TCTMainWindow window)
        {
            AutomationElement root = AutomationElement.FromHandle(hwnd);
            SelectionItemPattern selected = Find(root, "TCT.Workspace.manual.module")
                .GetCurrentPattern(SelectionItemPattern.Pattern) as SelectionItemPattern;
            if (selected == null) throw new InvalidOperationException("Module test workspace is not registered");
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
                        frame.ActiveModuleId == "Module01";
                }));
                if (!created) Thread.Sleep(100);
            }
            if (!created) throw new InvalidOperationException(
                "Real MHS module configuration / MPW automatic editor did not open");
            InvokePattern validate = Find(root, "TCT.Manual.Action.module.validate")
                .GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
            if (validate == null) throw new InvalidOperationException("Module validation action unavailable");
            validate.Invoke();
        }

        private static void InteractArchitectureSmoke(IntPtr hwnd, TCTMainWindow window)
        {
            AutomationElement root = AutomationElement.FromHandle(hwnd);
            ValuePattern search = Find(root, "TCT.Explorer.Search")
                .GetCurrentPattern(ValuePattern.Pattern) as ValuePattern;
            if (search == null) throw new InvalidOperationException("Explorer Search missing");
            search.SetValue("");
            SelectionItemPattern select = Find(root, "TCT.Workspace.auto.architecture-smoke")
                .GetCurrentPattern(SelectionItemPattern.Pattern) as SelectionItemPattern;
            if (select == null) throw new InvalidOperationException(
                "ArchitectureSmoke is not registered under Automated root");
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
            bool visible = false;
            window.Dispatcher.Invoke(new Action(() =>
            {
                System.Windows.Controls.TabControl tabs =
                    window.DocumentHost.Content as System.Windows.Controls.TabControl;
                visible = tabs != null && tabs.Items.OfType<System.Windows.Controls.TabItem>()
                    .Any(t => t.Content is TCTWorkspaceRootFrame &&
                        ((TCTWorkspaceRootFrame)t.Content).WorkspaceId == "manual.root");
            }));
            if (!visible) throw new InvalidOperationException("Root registration did not open a real page");
            if (Find(root, "TCT.Workspace.Entry.manual.channel")
                .GetCurrentPattern(InvokePattern.Pattern) == null)
                throw new InvalidOperationException("Channel entry was not interactive");
        }
        private static void InteractManualChannel(IntPtr hwnd, TCTMainWindow window)
        {
            AutomationElement root = AutomationElement.FromHandle(hwnd);
            SelectionItemPattern selected = Find(root, "TCT.Workspace.manual.channel")
                .GetCurrentPattern(SelectionItemPattern.Pattern) as SelectionItemPattern;
            if (selected == null) throw new InvalidOperationException("Channel registry leaf cannot select");
            selected.Select();
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
        }
        private static void VerifyOverview(IntPtr hwnd, TCTMainWindow window)
        {
            AutomationElement root = AutomationElement.FromHandle(hwnd);
            if (root == null) throw new InvalidOperationException("Overview HWND unavailable");
            AutomationElement discovered = Find(root, "TCT.Overview.Discovered");
            AutomationElement action = Find(root, "TCT.RunSelected");
            if (action.GetCurrentPattern(InvokePattern.Pattern) == null)
                throw new InvalidOperationException("Overview action lacks real InvokePattern");
            // UIAutomation COM属性必须留在UIA工作线程读取；放进Dispatcher会自锁至超时。
            string renderedCount = discovered.Current.Name;
            window.Dispatcher.Invoke(new Action(() => {
                if (window.DocumentHost.ActiveMode != "概览" ||
                    window.DocumentHost.Overview.DiscoveredCount <= 0 ||
                    renderedCount != window.DocumentHost.Overview.DiscoveredCount.ToString())
                    throw new InvalidOperationException("Overview is not bound to real test discovery");
                // High-legibility baseline is tested against the actual running WPF window.
                // Never accept a 11-12 DIP window or a hidden ScaleTransform workaround.
                if (window.FontSize < 16 ||
                    System.Windows.Media.TextOptions.GetTextFormattingMode(window) !=
                        System.Windows.Media.TextFormattingMode.Display)
                    throw new InvalidOperationException("TestCenter readable WPF text baseline regressed");
                DpiScale dpi = VisualTreeHelper.GetDpi(window);
                if (dpi.DpiScaleX < 0.95 || dpi.DpiScaleY < 0.95)
                    throw new InvalidOperationException("Invalid WPF DPI scale");
            }));
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
            AutomationElement context = Find(root, "TCT.Context.Geometry");
            InvokePattern modeAction = context.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
            if (modeAction == null)
                throw new InvalidOperationException("Dynamic Geometry Ribbon does not expose InvokePattern");
            modeAction.Invoke();
            AutomationElement source = Find(root, "TCT.Context.Source");
            if (source.GetCurrentPattern(InvokePattern.Pattern) == null)
                throw new InvalidOperationException("Contextual source navigation missing");
            AutomationElement run = Find(root, "TCT.RunSelected");
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
            AutomationElement mode = Find(root, "TCT.Context.Object");
            InvokePattern context = mode.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
            if (context == null) throw new InvalidOperationException("Object Ribbon command not accessible");
            context.Invoke();
            AutomationElement run = Find(root, "TCT.RunSelected");
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
            InvokePattern run = Find(root, "TCT.RunSelected")
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
            AutomationElement root = AutomationElement.FromHandle(hwnd);
            ValuePattern search = Find(root, "TCT.Explorer.Search")
                .GetCurrentPattern(ValuePattern.Pattern) as ValuePattern;
            if (search == null) throw new InvalidOperationException("MRK session search has no ValuePattern");
            search.SetValue("TCT.M3.RUNTIME.001");
            SelectionItemPattern selected = Find(root, "TCT.Case.TCT.M3.RUNTIME.001")
                .GetCurrentPattern(SelectionItemPattern.Pattern) as SelectionItemPattern;
            if (selected == null) throw new InvalidOperationException("MRK session test not selectable");
            selected.Select();
            InvokePattern run = Find(root, "TCT.RunSelected")
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
            AutomationElement root = AutomationElement.FromHandle(hwnd);
            ValuePattern search = Find(root, "TCT.Explorer.Search")
                .GetCurrentPattern(ValuePattern.Pattern) as ValuePattern;
            if (search == null) throw new InvalidOperationException("Channel search has no ValuePattern");
            search.SetValue("TCT.M3.CHANNEL.001");
            SelectionItemPattern selected = Find(root, "TCT.Case.TCT.M3.CHANNEL.001")
                .GetCurrentPattern(SelectionItemPattern.Pattern) as SelectionItemPattern;
            if (selected == null) throw new InvalidOperationException("Channel case lacks UIA selection");
            selected.Select();
            InvokePattern run = Find(root, "TCT.RunSelected")
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
