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
            Padding = new Thickness(7, 5, 7, 5), MinHeight = 31,
            BorderBrush = new SolidColorBrush(Color.FromRgb(195, 215, 235)),
            ToolTip = "按ID/名称/能力检索" };
        private readonly TreeView _tree = new TreeView { Margin = new Thickness(5) };
        private TCTTestCatalog _catalog;
        private TCTWorkspaceRegistry _workspaceRegistry;
        public event Action<TCTWorkspaceDescriptor> WorkspaceSelected;
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
                Text = "搜索测试 / Capability / ObjectId", FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(108, 131, 156)),
                Margin = new Thickness(9, 9, 0, 0) });
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
                    if (WorkspaceSelected != null) WorkspaceSelected(workspace);
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
            TreeViewItem item = new TreeViewItem { Header = descriptor.Title,
                Tag = descriptor, IsExpanded = true,
                ToolTip = descriptor.Description +
                    (descriptor.Factory == null &&
                     !descriptor.WorkspaceId.StartsWith("manual.module.", StringComparison.Ordinal)
                     ? " · Unsupported / 待接入" : "") };
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
            if (_catalog == null) return;
            foreach (TCTTestAspect aspect in new[] { TCTTestAspect.Foundation, TCTTestAspect.Mechanism, TCTTestAspect.Integration })
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
