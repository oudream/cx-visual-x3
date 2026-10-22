using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using CxVisual.TestCenter.Models;
using CxVisual.TestCenter.Fixtures;
using CxVisual.Mechanism.Core;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using CxVisual.TestCenter.Core;

namespace CxVisual.TestCenter.UI
{
    public sealed class TCTTestDocumentHostControl : UserControl
    {
        private readonly TabControl _tabs = new TabControl();
        private readonly Dictionary<string, TabItem> _workspaceTabs =
            new Dictionary<string, TabItem>(StringComparer.Ordinal);
        public event Action<TCTWorkspaceDescriptor> WorkspaceActivated;
        private readonly TCTTestOverviewControl _overview = new TCTTestOverviewControl();
        public TCTTestOverviewControl Overview { get { return _overview; } }
        private readonly List<ITCTTestViewer> _viewers = new List<ITCTTestViewer> {
            new TCTGeometryTestView(), new TCTObjectTestView(), new TCTGraphTestView(),
            new TCTChannelTestView(), new TCTRuntimeTestView()
        };
        // Shell订阅同一条选择事件，Inspector可显示结构化Owner/Attempt/Artifact证据。
        public event Action<TCTTestObservation> ObservationSelected;
        public event Action<string, string> ObjectSelected;
        public int ViewerCount { get { return _tabs.Items.OfType<TabItem>().Count(tab => tab.Content is ITCTTestViewer); } }
        public TCTTestDocumentHostControl()
        {
            foreach (TCTTestViewBase viewer in _viewers.OfType<TCTTestViewBase>())
                viewer.ObservationSelected += observation => {
                    if (ObservationSelected != null) ObservationSelected(observation);
                };
            TCTObjectTestView objectViewer = _viewers.OfType<TCTObjectTestView>().Single();
            objectViewer.ObjectSelected += (id, details) =>
            {
                if (ObjectSelected != null) ObjectSelected(id, details);
            };
            _viewers.OfType<TCTChannelTestView>().Single().FrameSelected += (id, details) => {
                if (ObjectSelected != null) ObjectSelected(id, details);
            };
            foreach (TCTGraphRuntimeViewBase sourceView in _viewers.OfType<TCTGraphRuntimeViewBase>())
                sourceView.SourceSelected += (id, details) => {
                    if (ObjectSelected != null) ObjectSelected(id, details);
                };
            TCTGeometryTestView geometryViewer = _viewers.OfType<TCTGeometryTestView>().Single();
            geometryViewer.GeometrySelected += (id, details) =>
            {
                if (ObjectSelected != null) ObjectSelected(id, details);
            };
            Content = _tabs;
            _tabs.SelectionChanged += (sender, args) =>
            {
                if (!object.ReferenceEquals(args.OriginalSource, _tabs)) return;
                TabItem current = _tabs.SelectedItem as TabItem;
                if (WorkspaceActivated != null)
                    WorkspaceActivated(current == null ? null : current.Tag as TCTWorkspaceDescriptor);
            };
            Clear();
        }
        public UserControl OpenWorkspace(TCTWorkspaceDescriptor descriptor)
        {
            if (descriptor == null || descriptor.Factory == null)
                throw new InvalidOperationException("Unsupported workspace factory");
            TabItem tab;
            if (!_workspaceTabs.TryGetValue(descriptor.WorkspaceId, out tab))
            {
                UserControl content = descriptor.Factory();
                if (content == null) throw new InvalidOperationException(
                    "Workspace factory returned null: " + descriptor.WorkspaceId);
                tab = new TabItem { Header = descriptor.Title, Tag = descriptor, Content = content };
                System.Windows.Automation.AutomationProperties.SetAutomationId(tab,
                    "TCT.Workspace.Tab." + descriptor.WorkspaceId);
                MenuItem close = new MenuItem { Header = "关闭工作区" };
                close.Click += (sender, args) => CloseWorkspace(descriptor.WorkspaceId);
                ContextMenu menu = new ContextMenu(); menu.Items.Add(close);
                tab.ContextMenu = menu;
                _workspaceTabs.Add(descriptor.WorkspaceId, tab);
                _tabs.Items.Add(tab);
            }
            _tabs.SelectedItem = tab;
            return (UserControl)tab.Content;
        }
        public bool CloseWorkspace(string id)
        {
            TabItem tab;
            if (!_workspaceTabs.TryGetValue(id, out tab)) return false;
            ITCTWorkspaceFrame frame = tab.Content as ITCTWorkspaceFrame;
            if (frame != null) frame.OnWorkspaceClosed();
            _tabs.Items.Remove(tab);
            _workspaceTabs.Remove(id);
            return true;
        }
        public void CloseAllWorkspaces()
        {
            foreach (string id in _workspaceTabs.Keys.ToArray())
                CloseWorkspace(id);
        }
        public void Clear()
        {
            TabItem[] saved = _workspaceTabs.Values.ToArray();
            _tabs.Items.Clear();
            foreach (ITCTTestViewer viewer in _viewers) viewer.Clear();
            _tabs.Items.Add(new TabItem { Header = "概览", Content = _overview });
            foreach (TabItem workspace in saved) _tabs.Items.Add(workspace);
        }
        public string ActiveMode
        {
            get { return _tabs.SelectedItem is TabItem ? ((TabItem)_tabs.SelectedItem).Header.ToString() : ""; }
        }

        public void ShowMode(string mode)
        {
            if (string.IsNullOrWhiteSpace(mode) || mode == "概览")
            {
                _tabs.SelectedIndex = 0;
                return;
            }
            foreach (TabItem existing in _tabs.Items.OfType<TabItem>())
                if (string.Equals(existing.Header as string, mode, StringComparison.OrdinalIgnoreCase))
                {
                    _tabs.SelectedItem = existing;
                    return;
                }
            if (!_viewers.Any(v => string.Equals(v.Kind, mode, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Unknown viewer mode", "mode");
            TabItem missing = new TabItem
            {
                Header = mode,
                Content = new TextBlock
                {
                    Text = mode + " · NoEvidence / Unsupported\n\n" +
                        "尚未从真实生产 Owner 的本次测试 Attempt 中获取此模式的结构化观察。" +
                        "\n请先在左侧运行相应真实测试。不会绘制演示状态或虚假成功结果。",
                    Margin = new Thickness(22), Foreground = Brushes.DarkGoldenrod,
                    TextWrapping = TextWrapping.Wrap
                }
            };
            _tabs.Items.Add(missing);
            _tabs.SelectedItem = missing;
        }

        public void ShowResult(TCTTestCaseResult result)
        {
            ShowResult(result, null);
        }
        public void ShowResult(TCTTestCaseResult result, string verifiedArchiveDirectory)
        {
            Clear();
            _viewers.OfType<TCTObjectTestView>().Single().ArchivedEvidenceRoot =
                verifiedArchiveDirectory;
            _viewers.OfType<TCTGeometryTestView>().Single().ArchivedEvidenceRoot =
                verifiedArchiveDirectory;
            foreach (TCTGraphRuntimeViewBase ownerView in _viewers.OfType<TCTGraphRuntimeViewBase>())
                ownerView.ArchivedEvidenceRoot = verifiedArchiveDirectory;
            _viewers.OfType<TCTChannelTestView>().Single().ArchivedEvidenceRoot =
                verifiedArchiveDirectory;
            if (result == null) return;
            foreach (ITCTTestViewer viewer in _viewers)
            {
                if (!viewer.CanDisplay(result)) continue;
                viewer.Bind(result);
                TabItem tab = new TabItem { Header = viewer.Kind, Content = (UserControl)viewer };
                System.Windows.Automation.AutomationProperties.SetAutomationId(tab,
                    "TCT.Viewer." + viewer.Kind);
                _tabs.Items.Add(tab);
            }
            TabItem firstViewer = _tabs.Items.OfType<TabItem>()
                .FirstOrDefault(t => t.Content is ITCTTestViewer);
            if (firstViewer != null) _tabs.SelectedItem = firstViewer;
        }
    }
}
