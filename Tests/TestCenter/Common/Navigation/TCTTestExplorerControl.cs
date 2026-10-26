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
    public sealed class TCTTestExplorerControl : UserControl
    {
        private readonly TextBox _search = new TextBox { Margin = new Thickness(7, 5, 7, 7),
            Padding = new Thickness(9, 5, 9, 5), MinHeight = 32,
            BorderBrush = new SolidColorBrush(Color.FromRgb(195, 215, 235)),
            ToolTip = "查找已注册工作区类型及已创建实例；具体测试用例在NUnit/TCT计划页查询" };
        private readonly TreeView _tree = new TreeView { Margin = new Thickness(5) };
        private TCTTestCatalog _catalog;
        private TCTWorkspaceRegistry _workspaceRegistry;
        public event Action<TCTWorkspaceDescriptor> WorkspaceSelected;
        public event Action<string> CreateRequested;
        public event Action<string> DeleteRequested;
        public event Action<string> CloseRequested;
        public event Action<string> StartRequested;
        public event Action<string> StopRequested;
        private readonly HashSet<string> _checkedIds = new HashSet<string>(StringComparer.Ordinal);
        public string[] CheckedIds { get { return _checkedIds.OrderBy(x => x, StringComparer.Ordinal).ToArray(); } }
        public event Action<ITCTTestCase> TestSelected;
        public ITCTTestCase SelectedTest { get; private set; }
        public TCTTestExplorerControl()
        {
            System.Windows.Automation.AutomationProperties.SetAutomationId(_search, "TCT.Explorer.Search");
            DockPanel panel = new DockPanel { Background = Brushes.White };
            StackPanel searchPanel = new StackPanel { Background =
                new SolidColorBrush(Color.FromRgb(246, 250, 254)) };
            searchPanel.Children.Add(new TextBlock {
                Text = "搜索资源 / 类型 / 实例", FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(108, 131, 156)),
                Margin = new Thickness(10, 10, 0, 1) });
            searchPanel.Children.Add(_search);
            DockPanel.SetDock(searchPanel, Dock.Top);
            panel.Children.Add(searchPanel);
            panel.Children.Add(_tree);
            Content = panel;
            _search.TextChanged += (s, e) => Refresh();
            _tree.SelectedItemChanged += (s, e) =>
            {
                TreeViewItem item = _tree.SelectedItem as TreeViewItem;
                if (item == null) return;
                TCTWorkspaceDescriptor workspace = item.Tag as TCTWorkspaceDescriptor;
                if (workspace != null)
                {
                    SelectedTest = null;
                    // 分类节点只用于选择/右键创建，不直接创建Frame或隐式打开Tab。
                    if (workspace.IsCreatedInstance && WorkspaceSelected != null)
                        WorkspaceSelected(workspace);
                    return;
                }
                ITCTTestCase test = item.Tag as ITCTTestCase;
                if (test != null)
                {
                    SelectedTest = test;
                    if (TestSelected != null) TestSelected(test);
                }
            };
        }
        public void Bind(TCTTestCatalog catalog) { _catalog = catalog; Refresh(); }
        public void BindWorkspaces(TCTWorkspaceRegistry registry)
        { _workspaceRegistry = registry; Refresh(); }
        public void RefreshWorkspaces() { Refresh(); }
        public void SelectWorkspace(string id)
        {
            foreach (TreeViewItem root in _tree.Items.OfType<TreeViewItem>())
            {
                TreeViewItem match = FindWorkspace(root, id);
                if (match == null) continue;
                match.IsSelected = true;
                match.BringIntoView();
                return;
            }
        }
        // Windows UIA使用实际WPF菜单项的Click路由；不调用另建的TestCenter创建引擎。
        internal void InvokeCreateContextMenuForVerification(string templateId)
        {
            TreeViewItem target = null;
            foreach (TreeViewItem root in _tree.Items.OfType<TreeViewItem>())
            {
                target = FindWorkspace(root, templateId);
                if (target != null) break;
            }
            if (target == null || target.ContextMenu == null)
                throw new InvalidOperationException("找不到可见资源类型：" + templateId);
            string action = "TCT.Workspace.Create." + templateId;
            MenuItem menu = target.ContextMenu.Items.OfType<MenuItem>().FirstOrDefault(x =>
                System.Windows.Automation.AutomationProperties.GetAutomationId(x) == action);
            if (menu == null || !menu.IsEnabled)
                throw new InvalidOperationException("没有可用的真实创建菜单：" + templateId);
            menu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, menu));
        }

        internal void InvokeInstanceContextMenuForVerification(string instanceId, string label)
        {
            TreeViewItem item = null;
            foreach (TreeViewItem root in _tree.Items.OfType<TreeViewItem>())
            {
                item = FindWorkspace(root, instanceId);
                if (item != null) break;
            }
            if (item == null || item.ContextMenu == null)
                throw new InvalidOperationException("实例子叶没有右键菜单：" + instanceId);
            MenuItem command = item.ContextMenu.Items.OfType<MenuItem>()
                .FirstOrDefault(x => (string)x.Header == label);
            if (command == null || !command.IsEnabled)
                throw new InvalidOperationException("实例右键命令不可用：" + label);
            command.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, command));
        }

        private static TreeViewItem FindWorkspace(TreeViewItem parent, string id)
        {
            TCTWorkspaceDescriptor descriptor = parent.Tag as TCTWorkspaceDescriptor;
            if (descriptor != null && descriptor.WorkspaceId == id) return parent;
            foreach (TreeViewItem child in parent.Items.OfType<TreeViewItem>())
            {
                TreeViewItem result = FindWorkspace(child, id);
                if (result != null) return result;
            }
            return null;
        }
        // 图标使用Windows原生Segoe MDL2矢量字形，无额外字体/位图依赖。
        // 根、专业、类型、实例分配稳定图标；选中与右键逻辑仍落在TreeViewItem。
        private static object MakeTreeHeader(TCTWorkspaceDescriptor descriptor)
        {
            string id = descriptor.IsCreatedInstance ? descriptor.TemplateId : descriptor.WorkspaceId;
            string glyph;
            if (id == "manual.root") glyph = "\uE8D4"; // 工作台
            else if (id == "auto.root") glyph = "\uE713"; // 工具/自动化
            else if (id == "scenes.root") glyph = "\uE8B7"; // 案例
            else if (id == "reports.root" || id.StartsWith("reports.", StringComparison.Ordinal))
                glyph = "\uE9D9"; // 报告
            else if (id == "manual.module") glyph = "\uE8B7";
            else if (id.Contains("connector")) glyph = "\uE774"; // 网络
            else if (id.Contains("driver")) glyph = "\uE7F4"; // 设备
            else if (id.Contains("compute")) glyph = "\uE943"; // 计算
            else if (id.Contains("simulation")) glyph = "\uE768"; // 仿真
            else if (id.Contains("service")) glyph = "\uE80F"; // 服务
            else if (id.Contains("channel")) glyph = "\uE839"; // 通道
            else if (id.Contains("protocol")) glyph = "\uE8A5"; // 规约
            else if (id.Contains("message")) glyph = "\uE8F2"; // 消息
            else if (id.Contains("command")) glyph = "\uE7C3"; // 命令
            else if (id.Contains("vision") || id.Contains("2d")) glyph = "\uE8B9";
            else if (id.Contains("3d") || id.Contains("ct")) glyph = "\uE80A";
            else if (id.Contains("architecture-smoke")) glyph = "\uE9D9";
            else if (id.Contains("catalog")) glyph = "\uE8A5";
            else glyph = "\uE8B7";
            if (descriptor.IsCreatedInstance) glyph = "\uE8A5";
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new TextBlock {
                Text = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 14, Width = 22, VerticalAlignment = VerticalAlignment.Center,
                Foreground = descriptor.IsCreatedInstance
                    ? new SolidColorBrush(Color.FromRgb(29, 116, 205))
                    : descriptor.Kind == TCTWorkspaceNodeKind.Root
                    ? new SolidColorBrush(Color.FromRgb(30, 106, 185))
                    : new SolidColorBrush(Color.FromRgb(100, 126, 154)) });
            row.Children.Add(new TextBlock {
                Text = descriptor.Title, FontSize = 14,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = descriptor.IsCreatedInstance ? FontWeights.SemiBold : FontWeights.Normal });
            return row;
        }

        private TreeViewItem WorkspaceItem(TCTWorkspaceDescriptor descriptor)
        {
            TreeViewItem item = new TreeViewItem {
                Header = MakeTreeHeader(descriptor),
                Tag = descriptor, IsExpanded = descriptor.Kind == TCTWorkspaceNodeKind.Root ||
                    descriptor.WorkspaceId == "manual.module",
                FontWeight = descriptor.IsCreatedInstance ? FontWeights.Medium : FontWeights.Normal,
                ToolTip = descriptor.Description +
                    (descriptor.IsCreatedInstance ? " · 单击打开；关闭Tab不删除实例" :
                        " · 右键创建实例；选择类别不会打开新Tab") };
            ContextMenu menu = new ContextMenu();
            if (!descriptor.IsCreatedInstance)
            {
                if (descriptor.Kind == TCTWorkspaceNodeKind.Root)
                {
                    // 根是分类；右键按已注册的类型创建实例，不自动打开根页。
                    if (descriptor.WorkspaceId == "scenes.root" ||
                        descriptor.WorkspaceId == "reports.root")
                    {
                        string rootId = descriptor.WorkspaceId;
                        MenuItem create = new MenuItem { Header = "新建" + descriptor.Title };
                        System.Windows.Automation.AutomationProperties.SetAutomationId(create,
                            "TCT.Workspace.Create." + rootId);
                        create.Click += (sender, args) => {
                            if (CreateRequested != null) CreateRequested(rootId);
                        };
                        menu.Items.Add(create);
                    }
                    foreach (TCTWorkspaceDescriptor child in
                        _workspaceRegistry.Children(descriptor.WorkspaceId)
                        .Where(x => !x.IsCreatedInstance))
                    {
                        if (child.Kind == TCTWorkspaceNodeKind.Group)
                        {
                            MenuItem sub = new MenuItem { Header = "新建" + child.Title };
                            foreach (TCTWorkspaceDescriptor choice in
                                _workspaceRegistry.Children(child.WorkspaceId)
                                .Where(x => !x.IsCreatedInstance))
                            {
                                string templateId = choice.WorkspaceId;
                                MenuItem entry = new MenuItem { Header = choice.Title };
                                entry.Click += (sender, args) => {
                                    if (CreateRequested != null) CreateRequested(templateId);
                                };
                                sub.Items.Add(entry);
                            }
                            menu.Items.Add(sub);
                        }
                        else
                        {
                            string templateId = child.WorkspaceId;
                            MenuItem entry = new MenuItem { Header = "新建" + child.Title };
                            System.Windows.Automation.AutomationProperties.SetAutomationId(entry,
                                "TCT.Workspace.Create." + templateId);
                            entry.Click += (sender, args) => {
                                if (CreateRequested != null) CreateRequested(templateId);
                            };
                            menu.Items.Add(entry);
                        }
                    }
                }
                else if (descriptor.Kind == TCTWorkspaceNodeKind.Group)
                {
                    // 父分类直接创建其下模板，不把父分类误当运行实例。
                    foreach (TCTWorkspaceDescriptor child in _workspaceRegistry.Children(descriptor.WorkspaceId)
                        .Where(x => !x.IsCreatedInstance))
                    {
                        string templateId = child.WorkspaceId;
                        MenuItem createChild = new MenuItem { Header = "创建 " + child.Title };
                        createChild.Click += (sender, args) => {
                            if (CreateRequested != null) CreateRequested(templateId);
                        };
                        menu.Items.Add(createChild);
                    }
                }
                else
                {
                    MenuItem create = new MenuItem {
                        Header = "创建实例",
                        IsEnabled = descriptor.Kind != TCTWorkspaceNodeKind.Root ||
                            descriptor.WorkspaceId == "scenes.root" ||
                            descriptor.WorkspaceId == "reports.root" };
                    System.Windows.Automation.AutomationProperties.SetAutomationId(create,
                        "TCT.Workspace.Create." + descriptor.WorkspaceId);
                    create.Click += (sender, args) => {
                        if (CreateRequested != null) CreateRequested(descriptor.WorkspaceId);
                    };
                    menu.Items.Add(create);
                }
            }
            else
            {
                MenuItem open = new MenuItem { Header = "打开" };
                open.Click += (sender, args) => {
                    if (WorkspaceSelected != null) WorkspaceSelected(descriptor);
                };
                menu.Items.Add(open);
                MenuItem start = new MenuItem { Header = "启动 / 运行",
                    IsEnabled = descriptor.TemplateId == "auto.architecture-smoke" ||
                        descriptor.TemplateId == "manual.channel" ||
                        descriptor.TemplateId == "auto.catalog" };
                start.Click += (sender, args) => {
                    if (StartRequested != null) StartRequested(descriptor.WorkspaceId);
                };
                menu.Items.Add(start);
                MenuItem stop = new MenuItem { Header = "停止",
                    IsEnabled = descriptor.TemplateId == "auto.architecture-smoke" ||
                        descriptor.TemplateId == "manual.channel" ||
                        descriptor.TemplateId == "auto.catalog" };
                stop.Click += (sender, args) => {
                    if (StopRequested != null) StopRequested(descriptor.WorkspaceId);
                };
                menu.Items.Add(stop);
                MenuItem close = new MenuItem { Header = "关闭分页（保留实例）" };
                close.Click += (sender, args) => {
                    if (CloseRequested != null) CloseRequested(descriptor.WorkspaceId);
                };
                menu.Items.Add(close);
                menu.Items.Add(new Separator());
                MenuItem remove = new MenuItem { Header = "删除实例" };
                System.Windows.Automation.AutomationProperties.SetAutomationId(remove,
                    "TCT.Workspace.Delete." + descriptor.WorkspaceId);
                remove.Click += (sender, args) => {
                    if (DeleteRequested != null) DeleteRequested(descriptor.WorkspaceId);
                };
                menu.Items.Add(remove);
            }
            item.ContextMenu = menu;
            if (descriptor.IsCreatedInstance)
            {
                // 用户关了Tab但树节点仍处于选中状态：再次单击也必须重新打开。
                item.PreviewMouseLeftButtonDown += (sender, args) => {
                    if (item.IsSelected && WorkspaceSelected != null)
                        WorkspaceSelected(descriptor);
                };
            }
            System.Windows.Automation.AutomationProperties.SetAutomationId(item,
                "TCT.Workspace." + descriptor.WorkspaceId);
            return item;
        }
        private TreeViewItem BuildWorkspaceItem(TCTWorkspaceDescriptor descriptor, string filter)
        {
            TreeViewItem item = WorkspaceItem(descriptor);
            foreach (TCTWorkspaceDescriptor child in _workspaceRegistry.Children(descriptor.WorkspaceId))
            {
                TreeViewItem nested = BuildWorkspaceItem(child, filter);
                if (nested != null) item.Items.Add(nested);
            }
            bool matches = filter.Length == 0 ||
                descriptor.Title.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                descriptor.WorkspaceId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
            return matches || item.Items.Count > 0 ||
                descriptor.Kind == TCTWorkspaceNodeKind.Root ? item : null;
        }
        private void Refresh()
        {
            _tree.Items.Clear();
            SelectedTest = null;
            string filter = _search.Text.Trim();
            TreeViewItem autoRoot = null;
            if (_workspaceRegistry != null)
            {
                foreach (TCTWorkspaceDescriptor descriptor in _workspaceRegistry.Items.Where(
                    item => item.Kind == TCTWorkspaceNodeKind.Root))
                {
                    TreeViewItem root = BuildWorkspaceItem(descriptor, filter);
                    _tree.Items.Add(root);
                    if (descriptor.WorkspaceId == "auto.root") autoRoot = root;
                }
            }
            // 用例目录仅在创建的NUnit/TCT计划页中展示，资源树只存实例。
        }
    }
}
