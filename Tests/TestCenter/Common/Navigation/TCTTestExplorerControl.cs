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
            Padding = new Thickness(10, 7, 10, 7), MinHeight = 38,
            BorderBrush = new SolidColorBrush(Color.FromRgb(195, 215, 235)),
            ToolTip = "按ID/名称/能力检索" };
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
                Text = "搜索测试 / Capability / ObjectId", FontSize = 14,
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
        private TreeViewItem WorkspaceItem(TCTWorkspaceDescriptor descriptor)
        {
            TreeViewItem item = new TreeViewItem {
                Header = descriptor.IsCreatedInstance ? "▣  " + descriptor.Title : descriptor.Title,
                Tag = descriptor, IsExpanded = descriptor.Kind == TCTWorkspaceNodeKind.Root ||
                    descriptor.WorkspaceId == "manual.module",
                FontWeight = descriptor.IsCreatedInstance ? FontWeights.Medium : FontWeights.Normal,
                ToolTip = descriptor.Description +
                    (descriptor.IsCreatedInstance ? " · 单击打开；关闭Tab不删除实例" :
                        " · 右键创建实例；选择类别不会打开新Tab") };
            ContextMenu menu = new ContextMenu();
            if (!descriptor.IsCreatedInstance)
            {
                if (descriptor.Kind == TCTWorkspaceNodeKind.Group)
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
                        descriptor.TemplateId == "manual.channel" };
                start.Click += (sender, args) => {
                    if (StartRequested != null) StartRequested(descriptor.WorkspaceId);
                };
                menu.Items.Add(start);
                MenuItem stop = new MenuItem { Header = "停止",
                    IsEnabled = descriptor.TemplateId == "auto.architecture-smoke" ||
                        descriptor.TemplateId == "manual.channel" };
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
            // Catalog定义在用户创建的NUnit/TCT计划页选择，不混入资源实例树。
            // 仍保持独立Catalog/Run Owner，既有结果/历史不会被改变。
            if (_catalog == null) return;
            if (false) foreach (TCTTestAspect aspect in new[] { TCTTestAspect.Foundation, TCTTestAspect.Mechanism, TCTTestAspect.Integration })
            {
                var items = _catalog.Cases.Where(t => t.Descriptor.Aspect == aspect &&
                    (filter.Length == 0 ||
                     t.Descriptor.Id.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                     t.Descriptor.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                     t.Descriptor.CapabilityAreaId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                     t.Descriptor.Group.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0))
                     .GroupBy(t => t.Descriptor.Group).ToList();
                TreeViewItem root = new TreeViewItem { Header = aspect.ToString(), IsExpanded = true };
                foreach (var group in items)
                {
                    TreeViewItem category = new TreeViewItem { Header = group.Key, IsExpanded = true };
                    foreach (ITCTTestCase test in group)
                    {
                        string id = test.Descriptor.Id;
                        CheckBox check = new CheckBox
                        {
                            Content = test.Descriptor.Name, IsChecked = _checkedIds.Contains(id),
                            ToolTip = id + " / " + test.Descriptor.CapabilityId,
                            Margin = new Thickness(0, 2, 0, 2)
                        };
                        check.Checked += (sender, args) => _checkedIds.Add(id);
                        check.Unchecked += (sender, args) => _checkedIds.Remove(id);
                        TreeViewItem entry = new TreeViewItem { Header = check, Tag = test,
                            ToolTip = test.Descriptor.Id + " / " + test.Descriptor.CapabilityId };
                        System.Windows.Automation.AutomationProperties.SetAutomationId(entry,
                            "TCT.Case." + id);
                        category.Items.Add(entry);
                    }
                    root.Items.Add(category);
                }
                if (root.Items.Count > 0)
                {
                    if (autoRoot != null) autoRoot.Items.Add(root);
                    else _tree.Items.Add(root);
                }
            }
        }
    }
}
